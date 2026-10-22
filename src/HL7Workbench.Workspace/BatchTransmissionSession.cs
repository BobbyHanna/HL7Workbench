using System.Diagnostics;
using System.Net.Sockets;
using HL7Workbench.Core;
using HL7Workbench.Transport;

namespace HL7Workbench.Workspace;

/// <summary>Owns one explicitly authorized, non-persistent sequential run. All network work
/// uses MllpConnection; pause/cancel and disposal share this owner's task and lifetime.</summary>
public sealed partial class BatchTransmissionSession : IDisposable, IAsyncDisposable
{
    private readonly DocumentManager documents;
    private readonly Func<ResolvedEndpoint, byte[], bool, CancellationToken, Task<ExchangeResult>> exchangeAsync;
    public BatchTransmissionSession(DocumentManager documents) : this(documents, ExchangeAsync) { }
    // Orchestration tests can hold an explicit write boundary without assuming when
    // an OS accepts its entire send buffer. Desktop always uses the production path.
    internal BatchTransmissionSession(DocumentManager documents, Func<ResolvedEndpoint, byte[], bool, CancellationToken, Task<ExchangeResult>> exchangeAsync)
    { this.documents = documents; this.exchangeAsync = exchangeAsync; }
    private readonly object gate = new();
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? runCancellation;
    private TaskCompletionSource? resume;
    private BatchTransmissionState state;
    private PreparedTransmissionBatch? prepared;
    private BatchMessageResult[] results = [];
    private readonly Stopwatch elapsed = new();
    private int? current;
    private int retainedAckBytes;
    private bool pause, disposed;
    public PreparedTransmissionBatch? Prepared { get { lock (gate) return prepared; } }
    public Task Execution { get; private set; } = Task.CompletedTask;
    public Task Disposal { get; private set; } = Task.CompletedTask;
    public bool IsActive { get { lock (gate) return !Execution.IsCompleted || state is BatchTransmissionState.Preparing or BatchTransmissionState.Running or BatchTransmissionState.PausePending or BatchTransmissionState.Paused; } }
    public BatchTransmissionProgress Progress
    {
        get
        {
            lock (gate)
            {
                var done = results.Where(r => r.Attempted).ToArray();
                var attempting = done.Length + (current is null ? 0 : 1);
                return new(state, results.Length, attempting, done.Count(r => r.Classification == BatchMessageClassification.ApplicationAccepted),
                    done.Count(r => r.Classification is BatchMessageClassification.ApplicationError or BatchMessageClassification.ApplicationRejected or BatchMessageClassification.CommitError or BatchMessageClassification.CommitRejected),
                    done.Count(r => r.Classification == BatchMessageClassification.Timeout), done.Count(r => r.Uncertain),
                    done.Count(r => r.Classification == BatchMessageClassification.Canceled), results.Length - attempting, current,
                    elapsed.Elapsed, done.Length == 0 ? TimeSpan.Zero : TimeSpan.FromTicks((long)done.Average(r => r.Exchange!.Duration.Ticks)), Array.AsReadOnly((BatchMessageResult[])results.Clone()));
            }
        }
    }
    public static int[] SelectPositions(int messageCount, int? first = null, int? last = null, IReadOnlyList<int>? positions = null)
    {
        if (positions is null)
        {
            var start = first ?? 1; var end = last ?? messageCount;
            if (start < 1 || end < start || end > messageCount || end - start + 1 > BatchTransmissionOptions.MaximumMessages)
                throw new ArgumentOutOfRangeException(nameof(first));
            return Enumerable.Range(start, end - start + 1).ToArray();
        }
        if (positions.Count is 0 or > BatchTransmissionOptions.MaximumMessages || positions.Distinct().Count() != positions.Count || positions.Any(p => p < 1 || p > messageCount))
            throw new ArgumentOutOfRangeException(nameof(positions));
        return positions.Order().ToArray();
    }
    public Task PrepareAsync(MessageDocument source, IReadOnlyList<int> positions, ResolvedEndpoint endpoint,
        BatchTransmissionOptions options, CancellationToken token = default)
    {
        options.Validate(); endpoint.Profile.Validate();
        if (endpoint.Profile.Security != TransportSecurity.PlainTcp) throw new NotSupportedException("TLS unavailable; no fallback.");
        var selection = SelectPositions(source.MessageCount, positions: positions);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (IsActive) throw new InvalidOperationException("Cancel and drain the batch before preparing another.");
            state = BatchTransmissionState.Preparing; prepared = null; results = []; elapsed.Reset(); current = null;
            return Execution = PrepareCoreAsync(source, selection, endpoint, options, token);
        }
    }
    private async Task PrepareCoreAsync(MessageDocument source, int[] positions, ResolvedEndpoint endpoint, BatchTransmissionOptions options, CancellationToken token)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token);
        lock (gate) runCancellation = cancellation;
        try
        {
            var revision = source.SearchRevision; var captures = new List<TransportMessageCapture>(); var bytes = 0;
            foreach (var position in positions)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                var capture = await source.CaptureTransportMessageAsync(position, endpoint.Profile.Encoding, cancellation.Token);
                if (!source.IsSearchCurrent(revision) || !capture.IsCurrent(documents)) throw new InvalidOperationException("Source changed; prepare again.");
                bytes = checked(bytes + capture.Length);
                if (bytes > BatchTransmissionOptions.MaximumPayloadBytes) throw new Hl7LimitException("BatchPayloadBytes");
                captures.Add(capture);
            }
            cancellation.Token.ThrowIfCancellationRequested();
            lock (gate)
            {
                cancellation.Token.ThrowIfCancellationRequested();
                if (disposed) return;
                prepared = new(source, revision, captures.ToArray(), endpoint, options, bytes);
                results = positions.Select(p => new BatchMessageResult(p)).ToArray(); retainedAckBytes = 0;
                state = BatchTransmissionState.Prepared;
            }
        }
        catch (OperationCanceledException) { lock (gate) if (!disposed) state = BatchTransmissionState.Canceled; throw; }
        catch { lock (gate) if (!disposed) state = BatchTransmissionState.Faulted; throw; }
        finally { lock (gate) runCancellation = null; }
    }
    public Task StartAsync(Guid reviewedBatchId, bool confirmed, bool nonLoopbackConfirmed, CancellationToken token = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!confirmed || !Execution.IsCompleted || state != BatchTransmissionState.Prepared || prepared is not { } batch || batch.Id != reviewedBatchId)
                throw new InvalidOperationException("Review and authorize this prepared batch before starting.");
            if (!batch.IsActive(documents)) throw new InvalidOperationException("Source changed, closed or inactive; prepare and review again.");
            if (batch.Endpoint.Scope != AddressScope.Loopback && !nonLoopbackConfirmed) throw new InvalidOperationException("Exact non-loopback destination confirmation required.");
            pause = false; resume = null; current = null; elapsed.Restart(); state = BatchTransmissionState.Running;
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token);
            runCancellation = cancellation;
            return Execution = Task.Run(() => RunAsync(batch, nonLoopbackConfirmed, cancellation));
        }
    }
    private async Task RunAsync(PreparedTransmissionBatch batch, bool nonLoopbackConfirmed, CancellationTokenSource cancellation)
    {
        var token = cancellation.Token; long? priorEnd = null;
        documents.Activity.Record(ActivityKind.BatchTransmissionStarted, count: batch.Count);
        try
        {
            for (var i = 0; i < batch.Count; i++)
            {
                while (true)
                {
                    Task? wait = null;
                    lock (gate)
                    {
                        token.ThrowIfCancellationRequested();
                        if (pause) { state = BatchTransmissionState.Paused; wait = resume!.Task; }
                        else
                        {
                            var remaining = priorEnd is { } end ? TimeSpan.FromMilliseconds(batch.Options.IntervalMilliseconds) - Stopwatch.GetElapsedTime(end) : TimeSpan.Zero;
                            if (remaining > TimeSpan.Zero) wait = Task.Delay(remaining, token);
                            else { current = batch.Positions[i]; state = BatchTransmissionState.Running; break; }
                        }
                    }
                    await wait.WaitAsync(token).ConfigureAwait(false);
                }
                var started = DateTimeOffset.UtcNow;
                var exchange = await exchangeAsync(batch.Endpoint, batch.CopyPayload(i), nonLoopbackConfirmed, token).ConfigureAwait(false);
                lock (gate)
                {
                    var ackBytes = exchange.Acknowledgments.Sum(a => a.Length); var retain = retainedAckBytes + ackBytes <= BatchTransmissionOptions.MaximumAckBytes;
                    if (retain) retainedAckBytes += ackBytes;
                    results[i] = new(batch.Positions[i], started, DateTimeOffset.UtcNow, exchange, retain);
                    current = null; priorEnd = Stopwatch.GetTimestamp();
                    if (token.IsCancellationRequested) { state = BatchTransmissionState.Canceled; return; }
                    if (!exchange.ApplicationAccepted && batch.Options.Policy == BatchAckPolicy.StopOnUnsuccessful)
                    { state = BatchTransmissionState.Stopped; return; }
                }
            }
            lock (gate) state = BatchTransmissionState.Completed;
        }
        catch (OperationCanceledException) { lock (gate) state = BatchTransmissionState.Canceled; }
        catch (Exception error) when (error is IOException or InvalidOperationException or ArgumentException or Hl7LimitException)
        { lock (gate) state = BatchTransmissionState.Faulted; }
        finally
        {
            lock (gate) { current = null; elapsed.Stop(); runCancellation = null; }
            cancellation.Dispose(); documents.Activity.Record(ActivityKind.BatchTransmissionEnded, count: Progress.Attempted);
        }
    }
    private static async Task<ExchangeResult> ExchangeAsync(ResolvedEndpoint endpoint, byte[] payload, bool nonLoopbackConfirmed, CancellationToken token)
    {
        using var connection = new MllpConnection(); var clock = Stopwatch.StartNew();
        try
        {
            await connection.ConnectAsync(endpoint, nonLoopbackConfirmed, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return (await connection.SendAsync(payload, true, nonLoopbackConfirmed, token).ConfigureAwait(false)) with { Duration = clock.Elapsed };
        }
        catch (Exception error) when (error is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
        {
            return new(token.IsCancellationRequested ? ExchangeOutcome.Canceled : error is OperationCanceledException ? ExchangeOutcome.TimedOut : ExchangeOutcome.ConnectionFailed,
                false, [], [], clock.Elapsed, FailureStage: ExchangeFailureStage.Connection,
                SocketError: (error as SocketException ?? error.InnerException as SocketException)?.SocketErrorCode);
        }
    }
    public void Pause()
    {
        lock (gate)
        {
            if (state is not (BatchTransmissionState.Running or BatchTransmissionState.PausePending)) return;
            pause = true; resume ??= new(TaskCreationOptions.RunContinuationsAsynchronously);
            state = current is null ? BatchTransmissionState.Paused : BatchTransmissionState.PausePending;
        }
    }
    public void Resume(Guid reviewedBatchId)
    {
        lock (gate)
        {
            if (disposed || prepared?.Id != reviewedBatchId || state is not (BatchTransmissionState.Paused or BatchTransmissionState.PausePending))
                throw new InvalidOperationException("Resume applies only to the unchanged authorized batch and destination.");
            pause = false; state = BatchTransmissionState.Running; resume?.TrySetResult(); resume = null;
        }
    }
    public void Cancel()
    {
        lock (gate)
        {
            runCancellation?.Cancel();
            if (state == BatchTransmissionState.Prepared) state = BatchTransmissionState.Canceled;
        }
    }
    public Task<MessageDocument> OpenAckAsync(BatchMessageResult result, int index, CancellationToken token = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (prepared is null || !results.Contains(result)) throw new InvalidOperationException("ACK belongs to an unavailable batch.");
            return TransportSession.OpenLocalPayloadAsync(documents, result.CopyAcknowledgment(index), prepared.Endpoint.Profile.Encoding, "Batch ACK.hl7", token);
        }
    }
    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return; disposed = true; lifetime.Cancel(); runCancellation?.Cancel();
            Disposal = DrainAsync();
        }
    }
    private async Task DrainAsync()
    {
        try { await Execution.ConfigureAwait(false); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException or ArgumentException or Hl7LimitException) { }
        finally { lock (gate) { prepared = null; results = []; state = BatchTransmissionState.Disposed; } lifetime.Dispose(); }
    }
    public async ValueTask DisposeAsync() { Dispose(); await Disposal.ConfigureAwait(false); }
    public Task<MessageDocument> OpenMessageAsync(BatchMessageResult result, CancellationToken token = default)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var index = Array.IndexOf(results, result);
            if (prepared is null || index < 0) throw new InvalidOperationException("Message belongs to an unavailable batch.");
            return TransportSession.OpenLocalPayloadAsync(documents, prepared.CopyPayload(index), prepared.Endpoint.Profile.Encoding, "Captured batch message.hl7", token);
        }
    }
}
