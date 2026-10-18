using System.Diagnostics;
using System.IO.Pipes;
using System.Threading.Channels;
using HL7Workbench.Core;
using HL7Workbench.Scripting;
using Microsoft.CodeAnalysis.Scripting;

namespace HL7Workbench.ScriptHost;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 1 || !args[0].StartsWith("hl7-script-", StringComparison.Ordinal)) return 2;
        using var cancellation = new CancellationTokenSource();
        using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut, PipeOptions.Asynchronous);
        try
        {
            await pipe.ConnectAsync(30_000, cancellation.Token);
            var requests = Channel.CreateBounded<ScriptFrame>(1);
            var reader = ReadRequestsAsync(pipe, requests.Writer, cancellation);
            ScriptRunner<Action[]>? runner = null;
            ScriptBatch? batch = null;
            var begun = false;
            var finished = false;
            await foreach (var request in requests.Reader.ReadAllAsync(cancellation.Token))
            {
                if (request.Kind == FrameKind.Initialize && runner is null)
                {
                    var definition = request.Definition ?? throw new InvalidDataException();
                    runner = UnifiedScript.Compile(definition, cancellation.Token);
                    batch = new(cancellation.Token);
                    await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Ready), cancellation.Token);
                }
                else if (request.Kind == FrameKind.Begin && runner is not null && batch is not null && !begun)
                {
                    begun = true;
                    var entries = await runner(new ScriptGlobals(batch, cancellation.Token), cancellation.Token);
                    batch.Initialized(entries[0], entries.Length == 2 ? entries[1] : null);
                    await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Begun, Reports: batch.DrainReports()), cancellation.Token);
                }
                else if (request.Kind == FrameKind.Message && batch is not null && !finished && begun && request.Bytes is { } bytes)
                {
                    ScriptMessage? facade = null;
                    try
                    {
                        var message = ScriptMessage.Parse(bytes, request.CodePage);
                        facade = new(message, cancellation.Token);
                        batch.Invoke(facade, request.Number);
                        cancellation.Token.ThrowIfCancellationRequested();
                        await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Result,
                            Bytes: facade.Excluded ? null : Hl7Serializer.Serialize(message), Number: request.Number, Excluded: facade.Excluded,
                            Reports: batch?.DrainReports()), cancellation.Token);
                    }
                    catch (Exception error) when (error is not OperationCanceledException)
                    {
                        var location = new StackTrace(error, true).GetFrames()?.FirstOrDefault(f => Path.GetFileName(f.GetFileName()) == "user-script.csx");
                        var diagnostics = error is ScriptRunException failure ? failure.Diagnostics.Take(ScriptLimits.Diagnostics).Select(Sanitize).Select(d => d with
                        { MessageNumber = request.Number, Line = d.Line ?? location?.GetFileLineNumber(), Column = d.Column ?? location?.GetFileColumnNumber() }).ToArray() :
                            [new ScriptDiagnostic(ScriptPhase.Execution, "RuntimeFailed", location?.GetFileLineNumber(), location?.GetFileColumnNumber(), request.Number, facade?.LastPath)];
                        await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Failure, Diagnostics: diagnostics), cancellation.Token);
                        return 1;
                    }
                }
                else if (request.Kind == FrameKind.Finish && batch is not null && !finished && begun)
                {
                    batch?.Finish(); finished = true;
                    await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Finished, Reports: batch?.DrainReports(), DeferredCount: batch?.DeferredCount ?? 0,
                        OutputNaming: batch?.OutputNaming), cancellation.Token);
                    if (batch is null || batch.DeferredCount == 0) return 0;
                }
                else if (request.Kind == FrameKind.Deferred && finished && batch?.TakeDeferred() is { } copy)
                {
                    await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Result, Bytes: copy.Bytes, Number: copy.Number), cancellation.Token);
                    if (batch.DeferredCount == 0) return 0;
                }
                else throw new InvalidDataException();
            }
            await reader;
            return 1;
        }
        catch (ScriptRunException error)
        {
            var location = new StackTrace(error, true).GetFrames()?.FirstOrDefault(f => Path.GetFileName(f.GetFileName()) == "user-script.csx");
            try { await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Failure, Diagnostics: error.Diagnostics.Take(ScriptLimits.Diagnostics).Select(Sanitize)
                .Select(d => d with { Line = d.Line ?? location?.GetFileLineNumber(), Column = d.Column ?? location?.GetFileColumnNumber() }).ToArray()), CancellationToken.None); }
            catch (IOException) { }
            return 1;
        }
        catch (Exception error)
        {
            // Initialization/Complete failures follow the same privacy policy as Main.
            var location = new StackTrace(error, true).GetFrames()?.FirstOrDefault(f => Path.GetFileName(f.GetFileName()) == "user-script.csx");
            try { await ScriptProtocol.WriteAsync(pipe, new(FrameKind.Failure, Diagnostics:
                [new(ScriptPhase.Execution, "RuntimeFailed", location?.GetFileLineNumber(), location?.GetFileColumnNumber())]), CancellationToken.None); }
            catch (Exception) { }
            return 1;
        }
    }
    private static ScriptDiagnostic Sanitize(ScriptDiagnostic diagnostic)
    {
        // Scripts can construct the public exception type themselves. Never trust imported
        // codes/paths merely because they arrived wrapped in a host-shaped exception.
        var code = diagnostic.Code;
        var compiler = code is { Length: 6 } && code.StartsWith("CS", StringComparison.Ordinal) && code.AsSpan(2).IndexOfAnyExceptInRange('0', '9') < 0;
        if (!compiler && code is not ("UnsupportedApi" or "MissingMain" or "EntryPointSignature" or "AmbiguousEntryPoint" or "ScriptLimit" or "DirectiveNotSupported" or "LifecyclePhase" or
            "BatchLimit" or "DeferredLimit" or "ReportSchema" or "ReportLimit" or "InputLimit" or "InvalidEncoding" or
            "InvalidPath" or "MissingValue" or "DecodeDiagnostic" or "EditRejected")) code = "RuntimeFailed";
        string? path = null;
        if (diagnostic.Path is { } candidate)
        {
            try { path = Hl7Path.Parse(candidate).ToString(); }
            catch (Exception e) when (e is ArgumentException or FormatException) { }
        }
        return diagnostic with { Code = code, Path = path, Phase = Enum.IsDefined(diagnostic.Phase) ? diagnostic.Phase : ScriptPhase.Execution };
    }
    private static async Task ReadRequestsAsync(Stream pipe, ChannelWriter<ScriptFrame> requests, CancellationTokenSource cancellation)
    {
        try
        {
            while (true)
            {
                var request = await ScriptProtocol.ReadAsync(pipe, cancellation.Token);
                if (request.Kind == FrameKind.Cancel) { cancellation.Cancel(); break; }
                await requests.WriteAsync(request, cancellation.Token);
            }
        }
        catch (Exception e) when (e is IOException or OperationCanceledException or System.Text.Json.JsonException)
        { cancellation.Cancel(); }
        finally { requests.TryComplete(); }
    }
}
