using System.Diagnostics;
using Xunit;

namespace HL7Workbench.Querying.Tests;

public sealed class QueryStartupTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Host => Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll");
    private static Task<QueryWorkerSession> Start(TimeProvider? sessionClock = null, TimeProvider? startupClock = null,
        QueryWorkerOptions? options = null, Func<QueryStartupPhase, int, CancellationToken, Task>? checkpoint = null,
        CancellationToken? token = null, Func<QuerySourceRead, CancellationToken, Task<QueryCell>>? sourceRead = null) =>
        QueryWorkerSession.StartCoreAsync(Host, null, options, token ?? Token, sourceRead, null,
            sessionClock ?? TimeProvider.System, startupClock ?? TimeProvider.System, checkpoint);

    private static void Reaped(int id)
    {
        Assert.True(id > 0);
        try { using var worker = Process.GetProcessById(id); Assert.True(worker.HasExited); }
        catch (ArgumentException) { }
    }
    private static async Task Recover()
    { await using var worker = await QueryWorkerSession.StartAsync(Host, token: Token); Assert.Single((await worker.ExecuteAsync("SELECT 1", token: Token)).Rows); }

    [Fact]
    public async Task TotalSessionDeadlineCanExpireDuringInitializationAndIsNotAStartupBudgetFailure()
    {
        var clock = new ManualDeadlineClock(); var id = 0;
        var error = await Assert.ThrowsAsync<QueryException>(() => Start(sessionClock: clock,
            options: new() { SessionTimeout = TimeSpan.FromSeconds(1) }, checkpoint: async (phase, pid, ct) =>
            {
                id = pid;
                if (phase == QueryStartupPhase.Initialization) { clock.Expire(); await Task.Delay(Timeout.Infinite, ct); }
            }));
        Assert.Equal(QueryError.Timeout, error.Code);
        Assert.Equal(QueryStartupPhase.Initialization, error.Startup!.Phase);
        Assert.Equal(QueryStartupFailure.SessionDeadline, error.Startup.Failure);
        Assert.Contains("SessionDeadline", error.ToString()); Assert.Null(error.InnerException);
        Reaped(id); Assert.True(clock.TimerDisposed); await Recover();
    }

    [Fact]
    public async Task SessionDeadlineRemainsArmedWhileContainmentIsPending()
    {
        var clock = new ManualDeadlineClock(); var id = 0;
        var error = await Assert.ThrowsAsync<QueryException>(() => Start(sessionClock: clock,
            options: new() { SessionTimeout = TimeSpan.FromSeconds(1) }, checkpoint: async (phase, pid, ct) =>
            {
                id = pid;
                if (phase == QueryStartupPhase.Containment)
                {
                    using var worker = Process.GetProcessById(pid);
                    clock.Expire(); await worker.WaitForExitAsync(ct).WaitAsync(TimeSpan.FromSeconds(5), ct);
                    Assert.True(worker.HasExited);
                }
            }));
        Assert.Equal(QueryError.Timeout, error.Code); Assert.Equal(QueryStartupFailure.SessionDeadline, error.Startup!.Failure);
        Assert.Equal(QueryStartupPhase.Containment, error.Startup.Phase);
        Reaped(id); Assert.True(clock.TimerDisposed); await Recover();
    }

    [Theory]
    [InlineData(QueryStartupPhase.Handshake)] [InlineData(QueryStartupPhase.Initialization)]
    public async Task StartupBudgetExpiryReportsItsPhaseAndReapsTheWorker(QueryStartupPhase target)
    {
        var clock = new ManualDeadlineClock(); var id = 0;
        using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(Token); watchdog.CancelAfter(TimeSpan.FromSeconds(30));
        var error = await Assert.ThrowsAsync<QueryException>(() => Start(startupClock: clock, token: watchdog.Token,
            checkpoint: async (phase, pid, ct) =>
            {
                id = pid;
                if (phase == target) { clock.Expire(); await Task.Delay(Timeout.Infinite, ct); }
            }));
        Assert.Equal(QueryError.Timeout, error.Code); Assert.Equal(target, error.Startup!.Phase);
        Assert.Equal(QueryStartupFailure.StartupTimeout, error.Startup.Failure); Assert.True(error.Startup.ElapsedMilliseconds >= 0);
        Assert.Equal(new QueryException(QueryError.Timeout).Message, error.Message);
        Reaped(id); Assert.True(clock.TimerDisposed); await Recover();
    }

    [Theory]
    [InlineData(QueryStartupPhase.Handshake)] [InlineData(QueryStartupPhase.Initialization)]
    public async Task WorkerExitEndsStartupWithoutWaitingForItsDeadline(QueryStartupPhase target)
    {
        var clock = new ManualDeadlineClock(); var id = 0;
        using var watchdog = CancellationTokenSource.CreateLinkedTokenSource(Token); watchdog.CancelAfter(TimeSpan.FromSeconds(30));
        var error = await Assert.ThrowsAsync<QueryException>(() => Start(startupClock: clock, token: watchdog.Token,
            checkpoint: async (phase, pid, ct) =>
            {
                id = pid;
                if (phase == target) { using var worker = Process.GetProcessById(pid); worker.Kill(true); await Task.Delay(Timeout.Infinite, ct); }
            }));
        Assert.Equal(QueryError.WorkerStopped, error.Code); Assert.Equal(target, error.Startup!.Phase);
        Assert.Equal(QueryStartupFailure.WorkerExit, error.Startup.Failure);
        Reaped(id); Assert.True(clock.TimerDisposed); await Recover();
    }

    [Theory]
    [InlineData(QueryStartupPhase.Handshake)] [InlineData(QueryStartupPhase.Initialization)]
    public async Task ParentCancellationRetainsItsTokenAndSafePhaseMetadata(QueryStartupPhase target)
    {
        var id = 0; using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        cancel.CancelAfter(TimeSpan.FromSeconds(30));
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Start(token: cancel.Token,
            checkpoint: async (phase, pid, ct) =>
            {
                id = pid;
                if (phase == target) { cancel.Cancel(); await Task.Delay(Timeout.Infinite, ct); }
            }));
        Assert.Equal(cancel.Token, error.CancellationToken);
        var diagnostic = Assert.IsType<QueryStartupDiagnostic>(error.Data[nameof(QueryStartupDiagnostic)]);
        Assert.Equal(target, diagnostic.Phase); Assert.Equal(QueryStartupFailure.ParentCancellation, diagnostic.Failure);
        Reaped(id); await Recover();
    }

    [Fact]
    public async Task TotalSessionDeadlineStillInterruptsActiveSourceReadsAndDoesNotResetOnActivity()
    {
        var clock = new ManualDeadlineClock(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var worker = await Start(sessionClock: clock, options: new() { SessionTimeout = TimeSpan.FromSeconds(1) },
            sourceRead: async (_, ct) => { entered.TrySetResult(); await Task.Delay(Timeout.Infinite, ct); return QueryCell.Null; });
        var run = worker.ExecuteAsync("SELECT HL7_VALUE('synthetic',1,'PID-3')", token: Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token); clock.Expire();
            var error = await Assert.ThrowsAsync<QueryException>(() => run.WaitAsync(TimeSpan.FromSeconds(5), Token));
            Assert.Equal(QueryError.Timeout, error.Code); Assert.Null(error.Startup);
            Reaped(worker.ProcessId); await Recover();
        }
        finally
        {
            await worker.DisposeAsync();
            try { await run.WaitAsync(TimeSpan.FromSeconds(5), Token); }
            catch (Exception error) when (error is QueryException or OperationCanceledException or ObjectDisposedException) { }
        }
    }

    [Theory]
    [InlineData(QueryError.InvalidRequest, QueryStartupFailure.RequestRejected)]
    [InlineData(QueryError.ResourceLimit, QueryStartupFailure.InitializationFailure)]
    [InlineData(QueryError.ProtocolError, QueryStartupFailure.ProtocolFailure)]
    public async Task KnownStartupFailuresKeepTheirExceptionCodeAndMessage(QueryError code, QueryStartupFailure failure)
    {
        var original = new QueryException(code); var id = 0;
        var error = await Assert.ThrowsAsync<QueryException>(() => Start(checkpoint: (phase, pid, _) =>
        {
            id = pid; if (phase == QueryStartupPhase.Initialization) throw original;
            return Task.CompletedTask;
        }));
        Assert.Same(original, error); Assert.Equal(code, error.Code); Assert.Null(error.InnerException);
        Assert.Equal(new QueryException(code).Message, error.Message);
        Assert.Equal(QueryStartupPhase.Initialization, error.Startup!.Phase); Assert.Equal(failure, error.Startup.Failure);
        Reaped(id);
    }

    [Fact]
    public async Task LaunchDiagnosticsContainNoExecutablePathOrInnerException()
    {
        const string privateMarker = "SYNTHETIC-PRIVATE-EXECUTABLE";
        var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), privateMarker + ".exe");
        var error = await Assert.ThrowsAsync<QueryException>(() => QueryWorkerSession.StartAsync(missing, token: Token));
        Assert.Equal(QueryError.WorkerUnavailable, error.Code); Assert.Equal(QueryStartupPhase.ProcessLaunch, error.Startup!.Phase);
        Assert.Equal(QueryStartupFailure.LaunchFailure, error.Startup.Failure); Assert.Null(error.InnerException);
        Assert.DoesNotContain(privateMarker, error.Message); Assert.DoesNotContain(privateMarker, error.ToString()); Assert.DoesNotContain(privateMarker, error.Startup.ToString());
    }

    [Fact]
    public async Task AlreadyCanceledStartupDoesNotLaunchAProcess()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token); cancel.Cancel(); var checkpoints = 0;
        var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Start(token: cancel.Token,
            checkpoint: (_, _, _) => { checkpoints++; return Task.CompletedTask; }));
        Assert.Equal(0, checkpoints); Assert.Equal(cancel.Token, error.CancellationToken);
        var diagnostic = Assert.IsType<QueryStartupDiagnostic>(error.Data[nameof(QueryStartupDiagnostic)]);
        Assert.Equal(QueryStartupPhase.Validation, diagnostic.Phase); Assert.Equal(QueryStartupFailure.ParentCancellation, diagnostic.Failure);
    }
}
