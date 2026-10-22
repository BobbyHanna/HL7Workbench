using HL7Workbench.Core;
using HL7Workbench.Transport;

namespace HL7Workbench.Workspace;

public enum LoadTestingState { Empty, Preparing, Prepared, Running, Completed, Stopped, Canceled, Faulted, Disposed }
public enum LoadStopReason { None, SourceExhausted, AttemptLimit, DurationLimit, UserCancellation, ConsecutiveTransportFailures, RejectionRate, TimeoutRate, Fault, Disposed }

/// <summary>Hard ceilings cannot be loosened by settings. Failure thresholds may only be tightened.</summary>
public sealed record LoadTestingOptions
{
    public const int MaximumAttempts = 5000, MaximumConcurrency = 8, MaximumDurationSeconds = 300, RetainedResults = 1000;
    public int Concurrency { get; init; } = 1;
    public double MessagesPerSecond { get; init; } = 1;
    public int DurationSeconds { get; init; } = 60;
    public int AttemptLimit { get; init; } = 60;
    public bool Replay { get; init; }
    public int ConsecutiveFailureLimit { get; init; } = 3;
    public int RejectionPercent { get; init; } = 20;
    public int TimeoutPercent { get; init; } = 20;
    public int MinimumOutcomeSamples { get; init; } = 5;
    public void Validate()
    {
        if (Concurrency is < 1 or > MaximumConcurrency || !double.IsFinite(MessagesPerSecond) || MessagesPerSecond is < .1 or > 20 ||
            DurationSeconds is < 1 or > MaximumDurationSeconds || AttemptLimit is < 1 or > MaximumAttempts ||
            ConsecutiveFailureLimit is < 1 or > 3 || RejectionPercent is < 1 or > 20 || TimeoutPercent is < 1 or > 20 || MinimumOutcomeSamples is < 1 or > 5)
            throw new ArgumentOutOfRangeException(nameof(Concurrency), "Load limits or stop thresholds are outside supported bounds.");
    }
}

/// <summary>Captures reuse the Stage 13A preparation authority; authorization never follows changed DNS or source edits.</summary>
public sealed class PreparedLoadTest
{
    internal PreparedTransmissionBatch Batch { get; }
    public Guid Id { get; } = Guid.NewGuid();
    public LoadTestingOptions Options { get; }
    public ResolvedEndpoint Endpoint => Batch.Endpoint;
    public int SourceCount => Batch.Count;
    public int PayloadBytes => Batch.PayloadBytes;
    public int DocumentNumber => Batch.DocumentNumber;
    public long Revision => Batch.Revision;
    public IReadOnlyList<int> Positions => Batch.Positions;
    public bool IsActive(DocumentManager documents) => Batch.IsActive(documents);
    public byte[] CopyPayload(int index) => Batch.CopyPayload(index);
    internal PreparedLoadTest(PreparedTransmissionBatch batch, LoadTestingOptions options) { Batch = batch; Options = options; }
    public string Review => $"Controlled Load Test {Id}\n{Endpoint.Display}\nDocument {DocumentNumber}, revision {Revision}; {SourceCount} captured messages, {PayloadBytes:N0} bytes." +
        $"\nCaptured source positions: {string.Join(", ", Positions)}." +
        $"\nConcurrency: {Options.Concurrency} (maximum 8). Global target: {Options.MessagesPerSecond:N2} messages/second (maximum 20; achieved rate may be lower)." +
        $"\nDuration: {Options.DurationSeconds} seconds; maximum attempts: {Options.AttemptLimit}. Stop at the first limit." +
        $"\nReplay captured test messages: {Options.Replay}. Replays retain identical MSH-10, patient, visit and clinical content; duplicate control IDs may be rejected." +
        $"\nStop: {Options.ConsecutiveFailureLimit} consecutive transport failures, rejection ≥ {Options.RejectionPercent}% or timeout ≥ {Options.TimeoutPercent}% after {Options.MinimumOutcomeSamples} completed exchanges." +
        (Batch.Transcoded ? "\nExplicit strict encoding conversion was required; original source unchanged." : "\nCaptured source encoding retained.") +
        "\nIncreasing concurrency can reorder registration/results and clinical events. One fresh connection per attempt. No retry, fallback, pooling or restart resume." +
        "\nI am authorized to load-test this exact destination using synthetic test data. These limits do not guarantee receiver safety. Authorize only this reviewed run?";
}

/// <summary>Exact bounded samples. Nearest-rank percentiles require 2/20/100 samples for p50/p95/p99 respectively.</summary>
public sealed record LoadLatencyStatistics(int Count, double? MinimumMs, double? MaximumMs, double? AverageMs, double? P50Ms, double? P95Ms, double? P99Ms)
{
    public static LoadLatencyStatistics Calculate(IEnumerable<TimeSpan> samples)
    {
        var values = samples.Select(s => s.TotalMilliseconds).Order().ToArray();
        if (values.Length == 0) return new(0, null, null, null, null, null, null);
        double? Percentile(double fraction, int minimum) => values.Length < minimum ? null : values[(int)Math.Ceiling(fraction * values.Length) - 1];
        return new(values.Length, values[0], values[^1], values.Average(), Percentile(.50, 2), Percentile(.95, 20), Percentile(.99, 100));
    }
}

/// <summary>Metadata only; payload and ACK bytes are not retained. Source and completion order remain separate.</summary>
public sealed record LoadExchangeResult(int Attempt, int CompletionOrder, int SourcePosition, MessageId SourceMessageId, bool Replayed,
    DateTimeOffset Started, DateTimeOffset Ended, bool Connected, TimeSpan? ConnectionDuration, BatchMessageResult Result)
{
    public override string ToString() => $"Attempt {Attempt} · completion {CompletionOrder} · source position {SourcePosition}{(Replayed ? " · replay" : "")} · {Result}";
}

public sealed record LoadTestingProgress(LoadTestingState State, LoadStopReason StopReason, int Attempts, int Active, int Completed,
    int Connected, int CompleteWrites, int AckFrames, int CommitAccepted, int CommitRejectedError, int ApplicationAccepted, int ApplicationRejectedError,
    int MissingOrMismatched, int Timeouts, int TransportFailures, int Canceled, int Uncertain, int OmittedResults,
    DateTimeOffset? Started, DateTimeOffset? Ended, TimeSpan Elapsed, TimeSpan Remaining, double TargetRate, double AttemptRate, double CompletionRate, double AcceptedRate,
    LoadLatencyStatistics ConnectionLatency, LoadLatencyStatistics WriteLatency, LoadLatencyStatistics AckLatency, LoadLatencyStatistics ExchangeLatency,
    IReadOnlyList<LoadExchangeResult> Results);

// One dispatcher uses this gate across every slot. Advancing from the actual start
// (not an old deadline) prevents catch-up bursts after slow exchanges or scheduling stalls.
internal sealed class LoadStartPacer(double rate)
{
    private TimeSpan? previous;
    internal TimeSpan Remaining(TimeSpan now) => previous is { } last ? TimeSpan.FromSeconds(1 / rate) - (now - last) : TimeSpan.Zero;
    internal void Started(TimeSpan now) => previous = now;
}
