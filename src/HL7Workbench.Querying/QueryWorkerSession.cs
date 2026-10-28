using System.Diagnostics;
using System.IO.Pipes;
using System.Text.Json;

namespace HL7Workbench.Querying;

/// <summary>Parent-side query transport. Contains no SQLite dependency; failures invalidate the worker and discard partial results.</summary>
public sealed class QueryWorkerSession : IAsyncDisposable
{
    private readonly Process process;
    private readonly NamedPipeServerStream pipe;
    private readonly QueryBudgets budgets;
    private readonly QueryWorkerOptions options;
    private readonly CancellationTokenSource lifetime = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object stopGate = new();
    private Task? stopping;
    private readonly Task stdout, stderr;
    private QueryWorkerLifetime? containment;
    private bool sealedInput;
    private volatile bool expired;
    private int importedRows;
    private long importedBytes;
    private readonly Func<QuerySourceRead, CancellationToken, Task<QueryCell>>? sourceRead;
    private int sourceReads;
    public int ProcessId { get; }
    public string EngineVersion { get; private set; } = "";
    private QueryWorkerSession(Process process, NamedPipeServerStream pipe, QueryBudgets budgets, QueryWorkerOptions options,
        Func<QuerySourceRead, CancellationToken, Task<QueryCell>>? sourceRead, TimeProvider sessionClock)
    {
        this.process = process; this.pipe = pipe; this.budgets = budgets; this.options = options;
        this.sourceRead = sourceRead;
        ProcessId = process.Id;
        stdout = DiscardAsync(process.StandardOutput); stderr = DiscardAsync(process.StandardError);
        _ = ReapAtDeadlineAsync(sessionClock);
    }
    public static Task<QueryWorkerSession> StartAsync(string executable, QueryBudgets? budgets = null,
        QueryWorkerOptions? options = null, CancellationToken token = default,
        Func<QuerySourceRead, CancellationToken, Task<QueryCell>>? sourceRead = null, string? currentDocumentId = null) =>
        StartCoreAsync(executable, budgets, options, token, sourceRead, currentDocumentId, TimeProvider.System, TimeProvider.System);

    // Only tests substitute clocks/checkpoints. Public callers always use real
    // timers; query/exchange deadlines and containment have no test override.
    internal static async Task<QueryWorkerSession> StartCoreAsync(string executable, QueryBudgets? budgets, QueryWorkerOptions? options,
        CancellationToken token, Func<QuerySourceRead, CancellationToken, Task<QueryCell>>? sourceRead, string? currentDocumentId,
        TimeProvider sessionClock, TimeProvider startupClock,
        Func<QueryStartupPhase, int, CancellationToken, Task>? checkpoint = null)
    {
        budgets ??= new(); options ??= new();
        var watch = Stopwatch.StartNew(); var phase = QueryStartupPhase.Validation;
        NamedPipeServerStream? pipe = null; Process? process = null; var launched = false; var startupExpired = false;
        QueryWorkerSession? session = null;
        try
        {
            budgets.Validate(); options.Validate(); token.ThrowIfCancellationRequested();
            phase = QueryStartupPhase.ProcessLaunch;
            if (!File.Exists(executable)) throw new QueryException(QueryError.WorkerUnavailable);
            var name = "hl7-query-" + Guid.NewGuid().ToString("N");
            pipe = new NamedPipeServerStream(name, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
            process = new Process { StartInfo = StartInfo(executable, name) };
            if (!(launched = process.Start())) throw new IOException();
            session = new(process, pipe, budgets, options, sourceRead, sessionClock);
            phase = QueryStartupPhase.Containment;
            if (checkpoint is not null) await checkpoint(phase, session.ProcessId, token);
            // The worker waits for Initialize, so no SQL/native allocation precedes job assignment.
            session.CheckOpen();
            var containment = new QueryWorkerLifetime(process, QueryLimits.WorkerMemoryBytes);
            // The lifetime timer remains armed during OS attachment. Publish the
            // job under the same gate as Stop; a late attachment must be disposed
            // locally rather than installed after StopCoreAsync has drained.
            lock (session.stopGate)
            {
                try { session.CheckOpen(); session.containment = containment; }
                catch { containment.Dispose(); throw; }
            }
            using var startupDeadline = new CancellationTokenSource(options.StartupTimeout, startupClock);
            using var startup = CancellationTokenSource.CreateLinkedTokenSource(token, startupDeadline.Token, session.lifetime.Token);
            using var monitoring = new CancellationTokenSource();
            var monitor = CancelStartupOnExitAsync(process, startup, monitoring.Token);
            try
            {
                phase = QueryStartupPhase.Handshake;
                if (checkpoint is not null) await checkpoint(phase, session.ProcessId, startup.Token);
                await pipe.WaitForConnectionAsync(startup.Token);
                phase = QueryStartupPhase.Initialization;
                if (checkpoint is not null) await checkpoint(phase, session.ProcessId, startup.Token);
                if (currentDocumentId is not null && (sourceRead is null || currentDocumentId.Length is < 1 or > 128)) throw new QueryException(QueryError.InvalidRequest);
                // The same startup token bounds connection plus initialization.
                var ready = await session.ExchangeAsync(new(QueryFrameKind.Initialize, Budgets: budgets, SourceBacked: sourceRead is not null, CurrentDocumentId: currentDocumentId), QueryFrameKind.Ready, startup.Token, options.StartupTimeout);
                if (ready.EngineVersion is not { Length: > 0 and <= 32 } || ready.EngineVersion.Any(c => !char.IsAsciiDigit(c) && c != '.'))
                    throw new QueryException(QueryError.ProtocolError);
                if (process.HasExited) throw new QueryException(QueryError.WorkerStopped);
                session.EngineVersion = ready.EngineVersion;
                return session;
            }
            finally
            {
                startupExpired = startupDeadline.IsCancellationRequested;
                await monitoring.CancelAsync(); await monitor;
            }
        }
        catch (Exception error)
        {
            var sessionExpired = session?.expired == true;
            var exited = launched && process!.HasExited;
            var failure = token.IsCancellationRequested ? QueryStartupFailure.ParentCancellation :
                sessionExpired ? QueryStartupFailure.SessionDeadline :
                startupExpired || error is QueryException { Code: QueryError.Timeout } ? QueryStartupFailure.StartupTimeout :
                error is QueryException { Code: QueryError.ProtocolError } ? QueryStartupFailure.ProtocolFailure :
                phase == QueryStartupPhase.Validation || error is QueryException { Code: QueryError.InvalidRequest } ? QueryStartupFailure.RequestRejected :
                error is QueryException { Code: not (QueryError.WorkerStopped or QueryError.WorkerUnavailable) } ? QueryStartupFailure.InitializationFailure :
                exited || error is QueryException { Code: QueryError.WorkerStopped } ? QueryStartupFailure.WorkerExit :
                phase == QueryStartupPhase.ProcessLaunch ? QueryStartupFailure.LaunchFailure :
                phase == QueryStartupPhase.Containment ? QueryStartupFailure.ContainmentFailure : QueryStartupFailure.InitializationFailure;
            var diagnostic = new QueryStartupDiagnostic(phase, failure, watch.ElapsedMilliseconds);
            if (session is not null) await session.DisposeAsync();
            else
            {
                pipe?.Dispose();
                if (launched)
                {
                    try { if (!process!.HasExited) process.Kill(true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
                }
                process?.Dispose();
            }
            if (token.IsCancellationRequested)
            {
                var canceled = new OperationCanceledException(token);
                canceled.Data[nameof(QueryStartupDiagnostic)] = diagnostic;
                throw canceled;
            }
            var code = sessionExpired ? QueryError.Timeout : error is QueryException known ? known.Code :
                failure == QueryStartupFailure.WorkerExit ? QueryError.WorkerStopped : error is OperationCanceledException ? QueryError.Timeout : QueryError.WorkerUnavailable;
            if (error is QueryException original && original.Code == code) { original.Startup = diagnostic; throw; }
            throw new QueryException(code) { Startup = diagnostic };
        }
    }

    private static async Task CancelStartupOnExitAsync(Process process, CancellationTokenSource startup, CancellationToken token)
    {
        try { await process.WaitForExitAsync(token); if (!token.IsCancellationRequested) startup.Cancel(); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    /// <summary>SQLite prepares but does not step a query to discover referenced relations. Only available before input is sealed.</summary>
    public async Task<QueryPlan> PlanAsync(string sql, IReadOnlyList<QueryParameter>? parameters = null, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            CheckOpen(); if (sealedInput) throw new QueryException(QueryError.InvalidRequest);
            ValidateSql(sql);
            var response = await ExchangeAsync(new(QueryFrameKind.Plan, Sql: sql, Parameters: parameters?.ToArray() ?? []), QueryFrameKind.Planned, token);
            var plan = response.Plan ?? throw new QueryException(QueryError.ProtocolError);
            if (plan.Tables.Any(t => !Enum.IsDefined(t))) throw new QueryException(QueryError.ProtocolError);
            return plan;
        }
        catch { await StopAsync(); throw; }
        finally { gate.Release(); }
    }
    private static void ValidateSql(string sql)
    {
        if (sql is null || sql.Length is < 1 or > QueryLimits.MaximumSqlCharacters || sql.Contains('\0')) throw new QueryException(QueryError.InvalidRequest);
        try { _ = QueryLimits.Utf8.GetByteCount(sql); } catch (System.Text.EncoderFallbackException) { throw new QueryException(QueryError.InvalidRequest); }
    }
    internal static ProcessStartInfo StartInfo(string executable, string name)
    {
        executable = Path.GetFullPath(executable);
        if (executable.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var apphost = Path.ChangeExtension(executable, OperatingSystem.IsWindows() ? ".exe" : null);
            if (File.Exists(apphost)) executable = apphost;
        }
        var dll = executable.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
        var info = new ProcessStartInfo
        {
            FileName = dll ? Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet" : executable,
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        if (dll) info.ArgumentList.Add(executable);
        // SQL, parameters, row values and source paths never enter arguments/environment.
        info.ArgumentList.Add(name); info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        return info;
    }
    /// <summary>Imports one bounded batch into the closed schema. Import is permanently disabled by the first query.</summary>
    public async Task ImportAsync(IReadOnlyList<QueryImportRow> rows, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            CheckOpen();
            if (sealedInput || rows.Count is < 1 or > QueryLimits.MaximumImportBatchRows) throw new QueryException(QueryError.InvalidRequest);
            var input = rows.ToArray(); var bytes = 0L;
            foreach (var row in input) { if (row is null) throw new QueryException(QueryError.InvalidRequest); bytes += row.Validate(); }
            if (importedRows + input.Length > budgets.ImportedRows || importedBytes + bytes > budgets.ImportBytes) throw new QueryException(QueryError.ImportLimit);
            await ExchangeAsync(new(QueryFrameKind.Import, Input: input), QueryFrameKind.Imported, token);
            importedRows += input.Length; importedBytes += bytes;
        }
        catch
        {
            await StopAsync();
            throw;
        }
        finally { gate.Release(); }
    }
    /// <summary>Returns all rows of one successful bounded statement. Limits/cancellation/errors return no partial collection.</summary>
    public async Task<QueryResult> ExecuteAsync(string sql, IReadOnlyList<QueryParameter>? parameters = null, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            CheckOpen();
            ValidateSql(sql);
            var args = parameters?.ToArray() ?? [];
            if (args.Length > QueryLimits.MaximumParameters || args.Any(p => p is null || p.Value is null)) throw new QueryException(QueryError.ParameterMismatch);
            foreach (var parameter in args) _ = parameter.Value.Size();
            sealedInput = true;
            sourceReads = 0;
            using var queryDeadline = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
            queryDeadline.CancelAfter(TimeSpan.FromMilliseconds(budgets.QueryMilliseconds));
            var watch = Stopwatch.StartNew();
            try
            {
                var header = await ExchangeAsync(new(QueryFrameKind.Execute, Sql: sql, Parameters: args), QueryFrameKind.Header, queryDeadline.Token);
                var columns = header.Columns ?? throw new QueryException(QueryError.ProtocolError);
                if (columns.Length is < 1 or > QueryLimits.MaximumColumns || columns.Any(c => c is null || c.Length > QueryLimits.MaximumSqlCharacters))
                    throw new QueryException(QueryError.ProtocolError);
                long bytes = columns.Sum(c => 32L + QueryLimits.Utf8.GetByteCount(c));
                if (bytes > budgets.ResultBytes) throw new QueryException(QueryError.ProtocolError);
                var rows = new List<QueryRow>();
                while (true)
                {
                    var page = await ExchangeAsync(new(QueryFrameKind.Next), QueryFrameKind.Page, queryDeadline.Token);
                    if (page.Rows is null || page.Rows.Length == 0 && !page.Complete) throw new QueryException(QueryError.ProtocolError);
                    foreach (var row in page.Rows)
                    {
                        if (row is null || row.Cells.Length != columns.Length) throw new QueryException(QueryError.ProtocolError);
                        bytes += row.Size(); rows.Add(row);
                        if (rows.Count > budgets.ResultRows || bytes > budgets.ResultBytes) throw new QueryException(QueryError.ProtocolError);
                    }
                    if (page.Complete) break;
                }
                queryDeadline.Token.ThrowIfCancellationRequested();
                return new(columns, rows.AsReadOnly(), watch.Elapsed, bytes) { Origins = header.Origins ?? [] };
            }
            catch (Exception error)
            {
                await StopAsync(); token.ThrowIfCancellationRequested();
                // Stopping cancels lifetime too; that must not relabel a SQL/policy failure as a timeout.
                if (error is OperationCanceledException) throw new QueryException(QueryError.Timeout);
                if (error is QueryException) throw;
                throw new QueryException(QueryError.ProtocolError);
            }
        }
        finally { gate.Release(); }
    }
    private void CheckOpen()
    {
        if (expired) throw new QueryException(QueryError.Timeout);
        if (stopping is not null) throw new ObjectDisposedException(nameof(QueryWorkerSession));
        if (lifetime.IsCancellationRequested) throw new QueryException(QueryError.Timeout);
    }
    private async Task<QueryFrame> ExchangeAsync(QueryFrame request, QueryFrameKind expected, CancellationToken token, TimeSpan? timeout = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        deadline.CancelAfter(timeout ?? options.OperationTimeout);
        try
        {
            await QueryProtocol.WriteAsync(pipe, request, deadline.Token);
            var response = await QueryProtocol.ReadAsync(pipe, deadline.Token);
            while (response.Kind == QueryFrameKind.SourceRead)
            {
                if (sourceRead is null || expected != QueryFrameKind.Page || response.Read is null || ++sourceReads > QueryLimits.MaximumSourceReads)
                    throw new QueryException(QueryError.ProtocolError);
                response.Read.Validate(); QueryFrame answer;
                try
                {
                    var value = await sourceRead(response.Read, deadline.Token); _ = value.Size();
                    answer = new(QueryFrameKind.SourceValue, Value: value);
                }
                catch (QueryException error) { answer = new(QueryFrameKind.SourceValue, Error: error.Code); }
                await QueryProtocol.WriteAsync(pipe, answer, deadline.Token);
                response = await QueryProtocol.ReadAsync(pipe, deadline.Token);
            }
            if (response.Kind == QueryFrameKind.Failure) throw new QueryException(response.Error ?? QueryError.ProtocolError);
            if (response.Kind != expected) throw new QueryException(QueryError.ProtocolError);
            return response;
        }
        catch (Exception error)
        {
            var canceled = token.IsCancellationRequested;
            if (error is OperationCanceledException)
            {
                try
                {
                    using var grace = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
                    await QueryProtocol.WriteAsync(pipe, new(QueryFrameKind.Cancel), grace.Token);
                    await Task.Delay(200, CancellationToken.None);
                }
                catch (Exception) { }
            }
            await StopAsync();
            if (canceled) throw new OperationCanceledException(token);
            if (error is QueryException) throw;
            throw new QueryException(error switch
            {
                OperationCanceledException => QueryError.Timeout,
                JsonException or InvalidDataException => QueryError.ProtocolError,
                _ => QueryError.WorkerStopped
            });
        }
    }
    private Task StopAsync() { lock (stopGate) return stopping ??= StopCoreAsync(); }
    private async Task ReapAtDeadlineAsync(TimeProvider clock)
    {
        try { await Task.Delay(options.SessionTimeout, clock, lifetime.Token); }
        catch (OperationCanceledException) { return; }
        expired = true;
        await StopAsync();
    }
    private async Task StopCoreAsync()
    {
        lifetime.Cancel(); containment?.Dispose(); containment = null;
        try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (Exception) { }
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } catch (Exception) { }
        pipe.Dispose();
        try { await Task.WhenAll(stdout, stderr).WaitAsync(TimeSpan.FromSeconds(2)); } catch (Exception) { }
    }
    private static async Task DiscardAsync(StreamReader reader)
    {
        var buffer = new char[1024];
        try { while (await reader.ReadAsync(buffer) != 0) { } } catch (Exception) { }
    }
    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        process.Dispose();
    }
}
