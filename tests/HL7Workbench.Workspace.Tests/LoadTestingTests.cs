using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Transport;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class LoadTestingTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static ResolvedEndpoint Endpoint(int port = 2575) => new(TransportNetworkTests.Endpoint(port), IPAddress.Loopback);
    private static LoadTestingOptions Fast(int attempts = 3) => new() { MessagesPerSecond = 20, AttemptLimit = attempts, DurationSeconds = 10 };
    private static async Task<MessageDocument> Source(DocumentManager documents, int count = 3, string accept = "", string app = "")
    {
        using var input = new MemoryStream(Enumerable.Range(1, count).SelectMany(n => TransportNetworkTests.Message(accept, app, "SYN-LOAD-" + n)).ToArray());
        return await MessageFileReader.OpenFileAsync(documents, "synthetic-load.hl7", input, Token);
    }
    private static Task Prepare(LoadTestingSession session, MessageDocument source, LoadTestingOptions? options = null, int port = 2575) =>
        session.PrepareAsync(source, Enumerable.Range(1, source.MessageCount).ToArray(), Endpoint(port), options ?? Fast(), Token);
    private static Task Run(LoadTestingSession session) => session.StartAsync(session.Prepared!.Id, true, false, Token).WaitAsync(TimeSpan.FromSeconds(15), Token);
    private static ExchangeResult Accepted() => new(ExchangeOutcome.ApplicationAccepted, true, [new byte[] { 1 }], ["AA"], TimeSpan.Zero, true, WriteDuration: TimeSpan.FromMilliseconds(2), AckRoundTrip: TimeSpan.FromMilliseconds(3));

    [Fact]
    public void DefaultBoundsAndGlobalPacerDoNotAccumulateOverduePermits()
    {
        var defaults = new LoadTestingOptions(); defaults.Validate(); Assert.Equal(1, defaults.Concurrency); Assert.Equal(1, defaults.MessagesPerSecond); Assert.Equal(60, defaults.DurationSeconds); Assert.False(defaults.Replay);
        var pacer = new LoadStartPacer(20); Assert.True(pacer.Remaining(TimeSpan.Zero) <= TimeSpan.Zero); pacer.Started(TimeSpan.Zero);
        Assert.Equal(TimeSpan.FromMilliseconds(50), pacer.Remaining(TimeSpan.Zero));
        Assert.True(pacer.Remaining(TimeSpan.FromSeconds(8)) < TimeSpan.Zero); pacer.Started(TimeSpan.FromSeconds(8));
        Assert.Equal(TimeSpan.FromMilliseconds(50), pacer.Remaining(TimeSpan.FromSeconds(8)));
    }
    [Theory]
    [InlineData("concurrency-low")] [InlineData("concurrency-high")] [InlineData("rate-low")] [InlineData("rate-high")] [InlineData("nan")]
    [InlineData("duration-low")] [InlineData("duration-high")] [InlineData("attempt-low")] [InlineData("attempt-high")]
    [InlineData("failures")] [InlineData("rejections")] [InlineData("timeouts")] [InlineData("samples")]
    public void UnsafeConfigurationCannotRemoveOrLoosenHardLimits(string kind)
    {
        var invalid = kind switch
        {
            "concurrency-low" => new LoadTestingOptions { Concurrency = 0 }, "concurrency-high" => new() { Concurrency = 9 },
            "rate-low" => new() { MessagesPerSecond = 0 }, "rate-high" => new() { MessagesPerSecond = 20.01 }, "nan" => new() { MessagesPerSecond = double.NaN },
            "duration-low" => new() { DurationSeconds = 0 }, "duration-high" => new() { DurationSeconds = 301 },
            "attempt-low" => new() { AttemptLimit = 0 }, "attempt-high" => new() { AttemptLimit = 5001 }, "failures" => new() { ConsecutiveFailureLimit = 4 },
            "rejections" => new() { RejectionPercent = 21 }, "timeouts" => new() { TimeoutPercent = 21 }, _ => new() { MinimumOutcomeSamples = 6 }
        };
        Assert.Throws<ArgumentOutOfRangeException>(invalid.Validate);
        new LoadTestingOptions { Concurrency = 8, MessagesPerSecond = 20, DurationSeconds = 300, AttemptLimit = 5000, ConsecutiveFailureLimit = 1, RejectionPercent = 1, TimeoutPercent = 1, MinimumOutcomeSamples = 1 }.Validate();
    }
    [Fact]
    public void PercentilesUseDocumentedNearestRanksAndDiscloseInsufficientSamples()
    {
        Assert.Null(LoadLatencyStatistics.Calculate([]).AverageMs);
        var one = LoadLatencyStatistics.Calculate([TimeSpan.FromMilliseconds(7)]); Assert.Equal(7, one.AverageMs); Assert.Null(one.P50Ms); Assert.Null(one.P95Ms); Assert.Null(one.P99Ms);
        var twenty = LoadLatencyStatistics.Calculate(Enumerable.Range(1, 20).Reverse().Select(n => TimeSpan.FromMilliseconds(n)));
        Assert.Equal(10, twenty.P50Ms); Assert.Equal(19, twenty.P95Ms); Assert.Null(twenty.P99Ms); Assert.Equal(10.5, twenty.AverageMs);
        var hundred = LoadLatencyStatistics.Calculate(Enumerable.Range(1, 100).Select(n => TimeSpan.FromMilliseconds(n)));
        Assert.Equal(50, hundred.P50Ms); Assert.Equal(95, hundred.P95Ms); Assert.Equal(99, hundred.P99Ms); Assert.Equal(1, hundred.MinimumMs); Assert.Equal(100, hundred.MaximumMs);
    }
    [Fact]
    public async Task OneGlobalRateGateAndEightSlotsBoundAllWorkersAndCancellationPreventsNewAdmission()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 1);
        var timestamps = new ConcurrentQueue<long>(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var calls = 0;
        await using var session = new LoadTestingSession(documents, async (_, _, _, connected, token) =>
        {
            timestamps.Enqueue(Stopwatch.GetTimestamp()); connected(TimeSpan.FromMilliseconds(1)); if (Interlocked.Increment(ref calls) == 8) entered.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { }
            return new(ExchangeOutcome.Canceled, true, [], [], TimeSpan.Zero, true, ExchangeFailureStage.Response);
        });
        await Prepare(session, source, Fast(50) with { Concurrency = 8, Replay = true }); var run = Run(session);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token); Assert.Equal(8, session.Progress.Active); Assert.Equal(8, session.Progress.Attempts);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(session.Prepared!.Id, true, false, Token));
        session.Cancel(); await run; Assert.Equal(8, calls); Assert.Equal(0, session.Progress.Active); Assert.Equal(8, session.Progress.Canceled); Assert.Equal(8, session.Progress.Uncertain);
        var starts = timestamps.ToArray(); for (var n = 1; n < starts.Length; n++) Assert.True(Stopwatch.GetElapsedTime(starts[n - 1], starts[n]) >= TimeSpan.FromMilliseconds(45));
        Assert.Equal(LoadStopReason.UserCancellation, session.Progress.StopReason);
    }
    [Theory]
    [InlineData("edit")] [InlineData("close")] [InlineData("switch")]
    public async Task PreparedIdentityAndExactPayloadSurviveButStaleOrInactiveSourcesCannotStart(string change)
    {
        using var documents = new DocumentManager(); var source = await Source(documents); await using var session = new LoadTestingSession(documents);
        await Prepare(session, source); var capture = session.Prepared!; var before = capture.CopyPayload(0); var altered = capture.CopyPayload(0); altered[0] = 0;
        if (change == "edit") { source.IsEditMode = true; source.ReplaceWorkingText(Encoding.UTF8.GetString(TransportNetworkTests.Message())); await source.EditRefresh; }
        if (change == "close") documents.Close(source.Id, true);
        if (change == "switch") await Source(documents, 1);
        Assert.Equal(before, capture.CopyPayload(0)); Assert.False(capture.IsActive(documents));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(capture.Id, true, false, Token)); Assert.Equal(0, session.Progress.Attempts);
    }
    [Fact]
    public async Task AuthorizationPinsIdentityAndDestinationAndNeverTreatsPrivateOrTestLabelsAsConsent()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 1); await using var session = new LoadTestingSession(documents);
        var external = Endpoint() with { Profile = Endpoint().Profile with { Host = "192.0.2.1", TestEndpoint = true }, Address = IPAddress.Parse("192.0.2.1") };
        await session.PrepareAsync(source, [1], external, Fast(), Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(session.Prepared!.Id, false, true, Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(Guid.NewGuid(), true, true, Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(session.Prepared!.Id, true, false, Token));
        Assert.Contains("192.0.2.1", session.Prepared!.Review); Assert.Contains("authorized to load-test", session.Prepared.Review); Assert.Contains("reorder", session.Prepared.Review);
        await Assert.ThrowsAsync<NotSupportedException>(() => session.PrepareAsync(source, [1], external with { Profile = external.Profile with { Security = TransportSecurity.TlsUnavailable } }, Fast(), Token));
        Assert.Equal(0, session.Progress.Attempts);
    }
    [Theory]
    [InlineData("transport", LoadStopReason.ConsecutiveTransportFailures, 2)]
    [InlineData("reject", LoadStopReason.RejectionRate, 1)]
    [InlineData("timeout", LoadStopReason.TimeoutRate, 1)]
    public async Task StopThresholdsAreAppliedAtCompletionAndNeverRetry(string kind, LoadStopReason stop, int expected)
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 1); var calls = 0;
        await using var session = new LoadTestingSession(documents, (_, _, _, _, _) =>
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(kind switch { "transport" => new ExchangeResult(ExchangeOutcome.ConnectionFailed, false, [], [], TimeSpan.Zero), "reject" => new(ExchangeOutcome.ApplicationRejected, true, [new byte[] { 1 }], ["AR"], TimeSpan.Zero, true), _ => new(ExchangeOutcome.TimedOut, true, [], [], TimeSpan.Zero, true) });
        });
        await Prepare(session, source, Fast(50) with { Replay = true, ConsecutiveFailureLimit = 2, MinimumOutcomeSamples = 1 }); await Run(session);
        Assert.Equal(stop, session.Progress.StopReason); Assert.Equal(expected, calls); Assert.Equal(expected, session.Progress.Completed); Assert.Equal(0, session.Progress.ApplicationAccepted);
    }
    [Fact]
    public async Task VirtualMonotonicClockExercisesDurationLimitWithoutStartingAtOrAfterDeadline()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 1); var time = new ManualLoadTime(); var starts = new List<long>();
        await using var session = new LoadTestingSession(documents, (_, _, _, connected, _) => { starts.Add(time.GetTimestamp()); connected(TimeSpan.Zero); return Task.FromResult(Accepted()); }, time);
        await Prepare(session, source, Fast(5000) with { Replay = true, DurationSeconds = 1 }); var run = Run(session); await Drive(time, run);
        Assert.Equal(LoadStopReason.DurationLimit, session.Progress.StopReason); Assert.NotEmpty(starts); Assert.All(starts, tick => Assert.True(tick < TimeSpan.TicksPerSecond));
        Assert.InRange(session.Progress.Attempts, 1, 20); Assert.Equal(0, session.Progress.Active);
    }
    [Fact]
    public async Task RetentionAndExactAllOutcomeStatisticsRemainBoundedAcrossMoreThanAThousandCompletionsAndRepeatedRuns()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 1); var time = new ManualLoadTime();
        await using var session = new LoadTestingSession(documents, (_, _, _, connected, _) =>
        {
            connected(TimeSpan.FromMilliseconds(1)); time.Advance(TimeSpan.FromMilliseconds(50)); return Task.FromResult(Accepted());
        }, time);
        // Each deterministic exchange consumes exactly one global rate interval.
        // Advance simulated work rather than waiting for Windows timer resolution.
        await Prepare(session, source, Fast(1005) with { Replay = true, DurationSeconds = 120 }); await Run(session);
        var p = session.Progress; Assert.Equal(1005, p.Attempts); Assert.Equal(1005, p.ApplicationAccepted); Assert.Equal(1005, p.CompleteWrites); Assert.Equal(1005, p.AckFrames);
        Assert.Equal(1000, p.Results.Count); Assert.Equal(5, p.OmittedResults); Assert.Equal(1005, p.ExchangeLatency.Count); Assert.Equal(1005, p.ConnectionLatency.Count); Assert.Equal(1005, p.AckLatency.Count);
        Assert.All(p.Results, r => Assert.Empty(r.Result.Exchange!.Acknowledgments)); Assert.Equal(LoadStopReason.AttemptLimit, p.StopReason);
        Assert.Equal(p.Attempts / p.Elapsed.TotalSeconds, p.AttemptRate); Assert.Equal(p.Completed / p.Elapsed.TotalSeconds, p.CompletionRate); Assert.Equal(p.ApplicationAccepted / p.Elapsed.TotalSeconds, p.AcceptedRate);
        await Prepare(session, source, Fast(1)); await Run(session); Assert.Equal(1, session.Progress.Attempts); Assert.Single(session.Progress.Results); Assert.Equal(0, session.Progress.OmittedResults);
    }
    private static async Task Drive(ManualLoadTime time, Task execution)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token); deadline.CancelAfter(12000);
        while (!execution.IsCompleted) { time.Advance(TimeSpan.FromMilliseconds(25)); await Task.Delay(1, deadline.Token); }
        await execution;
    }
    // A real TimeProvider test clock exercises the same delay/deadline APIs as
    // production without relaxing the public rate or resource bounds.
    private sealed class ManualLoadTime : TimeProvider
    {
        private readonly object gate = new(); private long ticks; private readonly List<Timer> timers = [];
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() { lock (gate) return ticks; }
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(GetTimestamp());
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        { var timer = new Timer(this, callback, state); timer.Change(dueTime, period); return timer; }
        public void Advance(TimeSpan span)
        {
            Timer[] due; lock (gate) { ticks += span.Ticks; due = timers.Where(t => t.Due <= ticks).ToArray(); foreach (var timer in due) { timers.Remove(timer); if (timer.Period > 0) { timer.Due = ticks + timer.Period; timers.Add(timer); } } }
            foreach (var timer in due) timer.Callback(timer.State);
        }
        private sealed class Timer(ManualLoadTime owner, TimerCallback callback, object? state) : ITimer
        {
            internal readonly TimerCallback Callback = callback; internal readonly object? State = state; internal long Due, Period; private bool disposed;
            public bool Change(TimeSpan due, TimeSpan period)
            { lock (owner.gate) { if (disposed) return false; owner.timers.Remove(this); Due = owner.ticks + due.Ticks; Period = period.Ticks; if (due >= TimeSpan.Zero) owner.timers.Add(this); return true; } }
            public void Dispose() { lock (owner.gate) { disposed = true; owner.timers.Remove(this); } }
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
