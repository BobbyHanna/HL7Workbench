using System.Diagnostics;
using System.IO.Pipes;

namespace HL7Workbench.Scripting;

/// <summary>A fresh worker per job. Dispose always terminates and reaps it, including noncooperative scripts.</summary>
public sealed class ScriptWorkerSession : IAsyncDisposable
{
    private readonly Process process;
    private readonly NamedPipeServerStream pipe;
    private readonly ScriptWorkerOptions options;
    private readonly Task stdout, stderr;
    private WorkerLifetime? resourceLifetime;
    private bool disposed;
    private bool finished;
    private ScriptReportChunk[] reports = [];
    private int processed;
    private int deferred;
    /// <summary>Validated suggestion, available only after successful Complete. Never a destination.</summary>
    public string? OutputFileName { get; private set; }
    public bool IgnoredOutputFileNameSuggestion { get; private set; }
    /// <summary>Transfers this operation's bounded report buffer. Drain after startup, each Process and Complete before continuing.</summary>
    public IReadOnlyList<ScriptReportChunk> DrainReports()
    { var result = reports; reports = []; return result; }
    public int ProcessId => process.Id;
    private ScriptWorkerSession(Process process, NamedPipeServerStream pipe, ScriptWorkerOptions options)
    {
        this.process = process; this.pipe = pipe; this.options = options;
        stdout = DiscardAsync(process.StandardOutput); stderr = DiscardAsync(process.StandardError);
    }
    public static async Task<ScriptWorkerSession> StartAsync(string executable, ScriptDefinition definition,
        ScriptWorkerOptions? options = null, CancellationToken token = default)
    {
        options ??= new(); options.Validate();
        if (definition.ApiVersion != 2)
            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Configuration, "UnsupportedApi"));
        if (definition.Source.Length > ScriptLimits.ScriptCharacters)
            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Configuration, "ScriptLimit"));
        if (!File.Exists(executable)) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Worker, "WorkerUnavailable"));
        var name = "hl7-script-" + Guid.NewGuid().ToString("N");
        var pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var process = new Process { StartInfo = CreateStartInfo(executable, name) };
        // Neither command line nor environment contains scripts, paths to source documents, or message values.
        process.StartInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        ScriptWorkerSession? session = null;
        try
        {
            token.ThrowIfCancellationRequested();
            if (!process.Start()) throw new IOException();
            session = new(process, pipe, options);
            session.resourceLifetime = new(process, options.MemoryLimitBytes);
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(token);
            startup.CancelAfter(options.StartupTimeout);
            var connection = pipe.WaitForConnectionAsync(startup.Token);
            var exited = process.WaitForExitAsync(startup.Token);
            if (await Task.WhenAny(connection, exited) != connection) throw new IOException();
            await connection;
            startup.Cancel();
            await session.ExchangeAsync(new(FrameKind.Initialize, Definition: definition), FrameKind.Ready, options.CompilationTimeout, token);
            var begun = await session.ExchangeAsync(new(FrameKind.Begin), FrameKind.Begun, options.MessageTimeout, token);
            session.reports = begun.Reports ?? [];
            return session;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        { if (session is not null) await session.DisposeAsync(); else { pipe.Dispose(); process.Dispose(); } throw; }
        catch (ScriptRunException)
        { if (session is not null) await session.DisposeAsync(); else { pipe.Dispose(); process.Dispose(); } throw; }
        catch (Exception e) when (e is IOException or System.ComponentModel.Win32Exception or OperationCanceledException or InvalidOperationException)
        {
            if (session is not null) await session.DisposeAsync(); else { pipe.Dispose(); process.Dispose(); }
            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Worker, e is OperationCanceledException ? "Timeout" : "WorkerUnavailable"));
        }
    }
    internal static ProcessStartInfo CreateStartInfo(string executable, string pipeName)
    {
        executable = Path.GetFullPath(executable);
        // A self-contained worker must enter through its own apphost, not an
        // installed dotnet host. Resolve only the adjacent file, never PATH.
        if (executable.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var apphost = Path.Combine(Path.GetDirectoryName(executable)!, Path.GetFileNameWithoutExtension(executable) + (OperatingSystem.IsWindows() ? ".exe" : ""));
            if (File.Exists(apphost)) executable = apphost;
        }
        var dll = executable.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        var start = new ProcessStartInfo
        {
            FileName = dll ? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet" : executable,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (dll) start.ArgumentList.Add(executable);
        start.ArgumentList.Add(pipeName);
        return start;
    }
    public async Task<ScriptMessageResult> ProcessAsync(byte[] bytes, int codePage, int messageNumber, CancellationToken token = default)
    {
        if (finished) throw new InvalidOperationException("Worker job is complete.");
        if (reports.Length != 0) throw new InvalidOperationException("Drain pending reports before processing another message.");
        if (++processed > ScriptLimits.BatchMessages) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "BatchLimit"));
        if (bytes.Length > ScriptLimits.MessageBytes) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputLimit", MessageNumber: messageNumber));
        var response = await ExchangeAsync(new(FrameKind.Message, Bytes: bytes, CodePage: codePage, Number: messageNumber),
            FrameKind.Result, options.MessageTimeout, token);
        if (response.Number != messageNumber || (!response.Excluded && response.Bytes is null) || response.Bytes?.Length > ScriptLimits.MessageBytes)
            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Protocol, "ProtocolError", MessageNumber: messageNumber));
        reports = response.Reports ?? [];
        return new(response.Excluded, response.Bytes);
    }
    public async Task CompleteAsync(CancellationToken token = default)
    {
        if (finished || reports.Length != 0) throw new InvalidOperationException("Job completed or reports not drained.");
        var response = await ExchangeAsync(new(FrameKind.Finish), FrameKind.Finished, options.MessageTimeout, token);
        reports = response.Reports ?? [];
        deferred = response.DeferredCount;
        if (deferred is < 0 or > ScriptLimits.DeferredMessages) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Protocol, "ProtocolError"));
        AcceptOutputNaming(response.OutputNaming);
        finished = true;
    }
    internal void AcceptOutputNaming(ScriptOutputNaming? naming)
    {
        // The worker is a separate trust boundary. Invalid or future naming metadata
        // is advisory only and cannot turn otherwise valid HL7 output into a failed run.
        OutputFileName = null; IgnoredOutputFileNameSuggestion = naming?.IgnoredSuggestion ?? false;
        if (naming is null) return;
        if (naming.Version != 1) { IgnoredOutputFileNameSuggestion = true; return; }
        if (naming.FileName is null) return;
        if (ScriptOutputFileName.TryNormalize(naming.FileName, out var valid)) OutputFileName = valid;
        else IgnoredOutputFileNameSuggestion = true;
    }
    /// <summary>Drains one deferred copy at a time after completion, retaining the original source ordinal for parent validation.</summary>
    public async Task<(int MessageNumber, byte[] Bytes)?> TakeDeferredAsync(CancellationToken token = default)
    {
        if (!finished) throw new InvalidOperationException("Complete the job first.");
        if (deferred == 0) return null;
        var response = await ExchangeAsync(new(FrameKind.Deferred), FrameKind.Result, options.MessageTimeout, token);
        if (response.Bytes is null || response.Bytes.Length > ScriptLimits.MessageBytes || response.Number < 1)
            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Protocol, "ProtocolError"));
        deferred--; return (response.Number, response.Bytes);
    }
    private async Task<ScriptFrame> ExchangeAsync(ScriptFrame request, FrameKind expected, TimeSpan timeout, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token); deadline.CancelAfter(timeout);
        try
        {
            await ScriptProtocol.WriteAsync(pipe, request, deadline.Token);
            var response = await ScriptProtocol.ReadAsync(pipe, deadline.Token);
            if (response.Kind == FrameKind.Failure)
            {
                var diagnostics = response.Diagnostics ?? [new(ScriptPhase.Protocol, "ProtocolError")];
                // Worker messages never carry exception text. Coordinates and canonical paths are generated by the host.
                throw new ScriptRunException(diagnostics);
            }
            if (response.Kind != expected) throw new InvalidDataException();
            return response;
        }
        catch (OperationCanceledException)
        {
            // A separate reader in the worker observes Cancel while user code is executing.
            // Grace is bounded; arbitrary C# loops cannot be interrupted by a token alone.
            try
            {
                using var grace = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
                await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Cancel), grace.Token);
                await process.WaitForExitAsync(grace.Token);
            }
            catch (Exception e) when (e is IOException or OperationCanceledException or InvalidOperationException) { }
            await DisposeAsync();
            if (token.IsCancellationRequested) throw new OperationCanceledException(token);
            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Worker, "Timeout", MessageNumber: request.Number > 0 ? request.Number : null));
        }
        catch (Exception e) when (e is IOException or System.Text.Json.JsonException)
        { throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Worker, "WorkerStopped", MessageNumber: request.Number > 0 ? request.Number : null)); }
    }
    private static async Task DiscardAsync(StreamReader reader)
    {
        // Drain with fixed storage. Console text can contain imported data and never becomes diagnostics/history.
        var buffer = new char[1024];
        try { while (await reader.ReadAsync(buffer) > 0) { } }
        catch (Exception e) when (e is IOException or ObjectDisposedException) { }
    }
    public async ValueTask DisposeAsync()
    {
        if (disposed) return; disposed = true;
        resourceLifetime?.Dispose(); resourceLifetime = null;
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception) { }
        pipe.Dispose();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
        catch (Exception e) when (e is InvalidOperationException or TimeoutException) { }
        process.StandardOutput.Dispose(); process.StandardError.Dispose();
        try { await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(2)); }
        catch (TimeoutException) { }
        process.Dispose();
    }
}
