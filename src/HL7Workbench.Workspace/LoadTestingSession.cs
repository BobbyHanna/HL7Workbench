using System.Diagnostics;
using System.Net.Sockets;
using HL7Workbench.Core;
using HL7Workbench.Transport;

namespace HL7Workbench.Workspace;

/// <summary>One explicitly authorized bounded run. A single dispatcher owns rate/slot admission;
/// individual production connections own framing, deadlines and ACK correlation.</summary>
public sealed partial class LoadTestingSession : IDisposable, IAsyncDisposable
{
    private readonly DocumentManager documents;
    private readonly BatchTransmissionSession preparation;
    private readonly Func<ResolvedEndpoint, byte[], bool, Action<TimeSpan>, CancellationToken, Task<ExchangeResult>> exchangeAsync;
    private readonly object gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? runCancellation;
    private PreparedLoadTest? prepared;
    private LoadTestingState state;
    private LoadStopReason reason;
    private bool disposed;
    private readonly TimeProvider timing;
    private long startedTick;
    private bool clockRunning;
    private TimeSpan finalElapsed;
    private TimeSpan Elapsed => clockRunning ? timing.GetElapsedTime(startedTick) : finalElapsed;
    private readonly List<LoadExchangeResult> rows = [];
    private readonly List<TimeSpan> connects = [], writes = [], acks = [], exchanges = [];
    private DateTimeOffset? started, ended;
    private int attempts, active, completed, connected, completeWrites, ackFrames, commitAccepted, commitRejected, accepted, rejected, missing, timeouts, transportFailures, canceled, uncertain, consecutiveFailures;
    public Task Execution { get; private set; } = Task.CompletedTask;
    public Task Disposal { get; private set; } = Task.CompletedTask;
    public PreparedLoadTest? Prepared { get { lock (gate) return prepared; } }
    public bool IsActive { get { lock (gate) return !Execution.IsCompleted || state is LoadTestingState.Preparing or LoadTestingState.Running; } }
    public LoadTestingSession(DocumentManager documents) : this(documents, ExchangeAsync) { }
    internal LoadTestingSession(DocumentManager documents, Func<ResolvedEndpoint, byte[], bool, Action<TimeSpan>, CancellationToken, Task<ExchangeResult>> exchangeAsync, TimeProvider? timing = null)
    { this.documents = documents; preparation = new(documents); this.exchangeAsync = exchangeAsync; this.timing = timing ?? TimeProvider.System; }
    public LoadTestingProgress Progress
    {
        get
        {
            lock (gate)
            {
                var elapsed = Elapsed; var seconds = elapsed.TotalSeconds;
                double Rate(int count) => seconds <= 0 ? 0 : count / seconds;
                return new(state, reason, attempts, active, completed, connected, completeWrites, ackFrames, commitAccepted, commitRejected, accepted, rejected,
                    missing, timeouts, transportFailures, canceled, uncertain, completed - rows.Count, started, ended, elapsed,
                    prepared is null || state != LoadTestingState.Running ? TimeSpan.Zero : TimeSpan.FromSeconds(Math.Max(0, prepared.Options.DurationSeconds - seconds)), prepared?.Options.MessagesPerSecond ?? 0,
                    Rate(attempts), Rate(completed), Rate(accepted), LoadLatencyStatistics.Calculate(connects), LoadLatencyStatistics.Calculate(writes),
                    LoadLatencyStatistics.Calculate(acks), LoadLatencyStatistics.Calculate(exchanges), Array.AsReadOnly(rows.ToArray()));
            }
        }
    }
    public Task PrepareAsync(MessageDocument source, IReadOnlyList<int> positions, ResolvedEndpoint endpoint, LoadTestingOptions options, CancellationToken token = default)
    {
        options.Validate();
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (IsActive) throw new InvalidOperationException("Stop and drain before preparing a new load test.");
            state = LoadTestingState.Preparing; reason = LoadStopReason.None; prepared = null; rows.Clear(); connects.Clear(); writes.Clear(); acks.Clear(); exchanges.Clear();
            attempts = active = completed = connected = completeWrites = ackFrames = commitAccepted = commitRejected = accepted = rejected = missing = timeouts = transportFailures = canceled = uncertain = consecutiveFailures = 0;
            started = ended = null; clockRunning = false; finalElapsed = TimeSpan.Zero;
            return Execution = PrepareCoreAsync(source, positions, endpoint, options, token);
        }
    }
    private async Task PrepareCoreAsync(MessageDocument source, IReadOnlyList<int> positions, ResolvedEndpoint endpoint, LoadTestingOptions options, CancellationToken token)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            await preparation.PrepareAsync(source, positions, endpoint, new(), linked.Token);
            lock (gate)
            {
                linked.Token.ThrowIfCancellationRequested();
                if (disposed || reason == LoadStopReason.UserCancellation) throw new OperationCanceledException();
                prepared = new(preparation.Prepared!, options); state = LoadTestingState.Prepared;
            }
        }
        catch (OperationCanceledException) { lock (gate) state = LoadTestingState.Canceled; throw; }
        catch { lock (gate) { state = LoadTestingState.Faulted; reason = LoadStopReason.Fault; } throw; }
    }
    public Task StartAsync(Guid reviewedId, bool authorizedTestDestination, bool nonLoopbackConfirmed, CancellationToken token = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!authorizedTestDestination || !Execution.IsCompleted || state != LoadTestingState.Prepared || prepared is not { } run || run.Id != reviewedId || !run.IsActive(documents))
                throw new InvalidOperationException("Prepare and explicitly authorize this current source, destination and configuration.");
            if (run.Endpoint.Scope != AddressScope.Loopback && !nonLoopbackConfirmed) throw new InvalidOperationException("Additional exact non-loopback authorization required.");
            token.ThrowIfCancellationRequested(); started = timing.GetUtcNow(); startedTick = timing.GetTimestamp(); clockRunning = true; state = LoadTestingState.Running;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token); runCancellation = cancellation;
            return Execution = Task.Run(() => RunAsync(run, nonLoopbackConfirmed, cancellation));
        }
    }
    private async Task RunAsync(PreparedLoadTest run, bool nonLoopbackConfirmed, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token; var work = new List<Task>(run.Options.Concurrency);
        using var slots = new SemaphoreSlim(run.Options.Concurrency);
        var budget = TimeSpan.FromSeconds(run.Options.DurationSeconds) - Elapsed;
        using var duration = new CancellationTokenSource(budget > TimeSpan.Zero ? budget : TimeSpan.Zero, timing);
        using var deadline = duration.Token.Register(() => RequestStop(LoadStopReason.DurationLimit));
        var naturalEnd = run.Options.Replay || run.Options.AttemptLimit <= run.SourceCount ? LoadStopReason.AttemptLimit : LoadStopReason.SourceExhausted;
        var limit = run.Options.Replay ? run.Options.AttemptLimit : Math.Min(run.Options.AttemptLimit, run.SourceCount);
        var pacer = new LoadStartPacer(run.Options.MessagesPerSecond);
        documents.Activity.Record(ActivityKind.LoadTestingStarted, count: limit);
        try
        {
            for (var ordinal = 1; ordinal <= limit; ordinal++)
            {
                await slots.WaitAsync(token).ConfigureAwait(false);
                var admitted = false;
                try
                {
                    // There is only one rate gate, including when all slots were
                    // occupied. A freed slot cannot accumulate overdue permits.
                    while (pacer.Remaining(Elapsed) is var remaining && remaining > TimeSpan.Zero)
                        await Task.Delay(remaining, timing, token).ConfigureAwait(false);
                    lock (gate)
                    {
                        token.ThrowIfCancellationRequested();
                        if (Elapsed >= TimeSpan.FromSeconds(run.Options.DurationSeconds)) { RequestStop(LoadStopReason.DurationLimit); token.ThrowIfCancellationRequested(); }
                        attempts++; active++; pacer.Started(Elapsed); admitted = true;
                    }
                    work.RemoveAll(t => t.IsCompleted);
                    work.Add(AttemptAsync(run, ordinal, (ordinal - 1) % run.SourceCount, nonLoopbackConfirmed, slots, token));
                }
                finally { if (!admitted) slots.Release(); }
            }
        }
        catch (OperationCanceledException) { lock (gate) if (reason == LoadStopReason.None) reason = LoadStopReason.UserCancellation; }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or Hl7LimitException)
        { RequestStop(LoadStopReason.Fault); }
        finally
        {
            await Task.WhenAll(work).ConfigureAwait(false);
            lock (gate)
            {
                if (reason == LoadStopReason.None) reason = naturalEnd;
                state = reason is LoadStopReason.UserCancellation or LoadStopReason.Disposed ? LoadTestingState.Canceled : reason == LoadStopReason.Fault ? LoadTestingState.Faulted :
                    reason is LoadStopReason.SourceExhausted or LoadStopReason.AttemptLimit ? LoadTestingState.Completed : LoadTestingState.Stopped;
                ended = timing.GetUtcNow(); finalElapsed = Elapsed; clockRunning = false; runCancellation = null;
            }
            cancellation.Dispose(); documents.Activity.Record(ActivityKind.LoadTestingEnded, count: attempts);
        }
    }
    private async Task AttemptAsync(PreparedLoadTest run, int ordinal, int index, bool nonLoopbackConfirmed, SemaphoreSlim slots, CancellationToken token)
    {
        var start = timing.GetUtcNow(); var timestamp = timing.GetTimestamp(); TimeSpan? connectionDuration = null;
        void Connected(TimeSpan latency)
        {
            lock (gate) { if (connectionDuration is not null) return; connectionDuration = latency; connected++; connects.Add(latency); }
        }
        try
        {
            ExchangeResult exchange;
            try { exchange = await exchangeAsync(run.Endpoint, run.CopyPayload(index), nonLoopbackConfirmed, Connected, token).ConfigureAwait(false); }
            catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException or ArgumentException or InvalidOperationException or Hl7LimitException)
            {
                exchange = new(token.IsCancellationRequested ? ExchangeOutcome.Canceled : ExchangeOutcome.ConnectionFailed, false, [], [], TimeSpan.Zero,
                    FailureStage: ExchangeFailureStage.Connection, SocketError: (error as SocketException)?.SocketErrorCode);
            }
            exchange = exchange with { Duration = timing.GetElapsedTime(timestamp) };
            var result = new BatchMessageResult(run.Positions[index], start, timing.GetUtcNow(), exchange, retainAcks: false);
            lock (gate)
            {
                completed++; active--; exchanges.Add(exchange.Duration);
                if (exchange.WriteDuration is { } write) writes.Add(write);
                if (exchange.AckRoundTrip is { } ack) acks.Add(ack);
                if (exchange.WriteCompleted) completeWrites++;
                ackFrames += result.AckFrames;
                if (exchange.Codes.Contains("CA")) commitAccepted++;
                if (exchange.Outcome is ExchangeOutcome.CommitError or ExchangeOutcome.CommitRejected) commitRejected++;
                if (exchange.ApplicationAccepted) accepted++;
                if (exchange.Outcome is ExchangeOutcome.ApplicationError or ExchangeOutcome.ApplicationRejected) rejected++;
                if (exchange.Outcome is ExchangeOutcome.MissingAck or ExchangeOutcome.MissingAckFields or ExchangeOutcome.InvalidAck or ExchangeOutcome.MismatchedAck or ExchangeOutcome.UnexpectedAck ||
                    exchange.WriteCompleted && exchange.Outcome is ExchangeOutcome.ConditionalAckUnobserved or ExchangeOutcome.TimedOut) missing++;
                if (exchange.Outcome is ExchangeOutcome.TimedOut or ExchangeOutcome.ConditionalAckUnobserved) timeouts++;
                var transportFailure = exchange.Outcome is ExchangeOutcome.ConnectionFailed or ExchangeOutcome.WriteFailed or ExchangeOutcome.Disconnected or ExchangeOutcome.FramingError;
                if (transportFailure) transportFailures++;
                consecutiveFailures = transportFailure ? consecutiveFailures + 1 : 0;
                if (exchange.Outcome == ExchangeOutcome.Canceled) canceled++;
                if (result.Uncertain) uncertain++;
                if (rows.Count < LoadTestingOptions.RetainedResults)
                    rows.Add(new(ordinal, completed, run.Positions[index], run.Batch.Captures[index].MessageId, ordinal > run.SourceCount, start, timing.GetUtcNow(), connectionDuration is not null, connectionDuration, result));
                // Evaluate every completion, independent of the bounded UI row set.
                if (consecutiveFailures >= run.Options.ConsecutiveFailureLimit) RequestStop(LoadStopReason.ConsecutiveTransportFailures);
                else if (completed >= run.Options.MinimumOutcomeSamples)
                {
                    if ((long)(rejected + commitRejected) * 100 >= (long)completed * run.Options.RejectionPercent) RequestStop(LoadStopReason.RejectionRate);
                    else if ((long)timeouts * 100 >= (long)completed * run.Options.TimeoutPercent) RequestStop(LoadStopReason.TimeoutRate);
                }
            }
        }
        finally { slots.Release(); }
    }
    private static async Task<ExchangeResult> ExchangeAsync(ResolvedEndpoint endpoint, byte[] payload, bool nonLoopbackConfirmed, Action<TimeSpan> connected, CancellationToken token)
    {
        using var connection = new MllpConnection(); var start = Stopwatch.GetTimestamp();
        try
        {
            await connection.ConnectAsync(endpoint, nonLoopbackConfirmed, token).ConfigureAwait(false);
            connected(Stopwatch.GetElapsedTime(start)); token.ThrowIfCancellationRequested();
            return await connection.SendAsync(payload, true, nonLoopbackConfirmed, token).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            return new(token.IsCancellationRequested ? ExchangeOutcome.Canceled : error is OperationCanceledException ? ExchangeOutcome.TimedOut : ExchangeOutcome.ConnectionFailed,
                false, [], [], Stopwatch.GetElapsedTime(start), FailureStage: ExchangeFailureStage.Connection,
                SocketError: (error as SocketException ?? error.InnerException as SocketException)?.SocketErrorCode);
        }
    }
    private void RequestStop(LoadStopReason stop)
    {
        lock (gate)
        {
            if (reason == LoadStopReason.None) reason = stop;
            runCancellation?.Cancel();
        }
    }
    public void Cancel()
    {
        lock (gate)
        {
            if (state is not (LoadTestingState.Preparing or LoadTestingState.Prepared or LoadTestingState.Running)) return;
            RequestStop(LoadStopReason.UserCancellation); preparation.Cancel();
            if (state == LoadTestingState.Prepared) state = LoadTestingState.Canceled;
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return; disposed = true; RequestStop(LoadStopReason.Disposed); lifetime.Cancel(); preparation.Dispose();
            Disposal = DrainAsync();
        }
    }
    private async Task DrainAsync()
    {
        try { await Task.WhenAll(Execution, preparation.Disposal).ConfigureAwait(false); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or SocketException or OperationCanceledException or InvalidOperationException or ArgumentException or NotSupportedException or Hl7LimitException) { }
        finally
        {
            lock (gate) { prepared = null; rows.Clear(); connects.Clear(); writes.Clear(); acks.Clear(); exchanges.Clear(); state = LoadTestingState.Disposed; }
            lifetime.Dispose();
        }
    }
    public async ValueTask DisposeAsync() { Dispose(); await Disposal.ConfigureAwait(false); }
}
