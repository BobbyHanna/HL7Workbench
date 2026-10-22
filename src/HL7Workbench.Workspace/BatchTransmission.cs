using System.Collections.ObjectModel;
using HL7Workbench.Core;
using HL7Workbench.Transport;

namespace HL7Workbench.Workspace;

public enum BatchAckPolicy { StopOnUnsuccessful, ContinueOnUnsuccessful }
public enum BatchTransmissionState { Empty, Preparing, Prepared, Running, PausePending, Paused, Completed, Stopped, Canceled, Faulted, Disposed }
public enum BatchMessageClassification { NotAttempted, ApplicationAccepted, ApplicationError, ApplicationRejected, CommitError, CommitRejected, CommitAcceptedApplicationUnknown, TransportFailure, Timeout, UncertainDelivery, Canceled, ApplicationUnknown }

/// <summary>One connection per message. The interval is a minimum wait after an exchange ends,
/// which also bounds starts after failures; it is not a receiver overload guarantee.</summary>
public sealed record BatchTransmissionOptions
{
    public const int MaximumMessages = 1000;
    public const int MaximumPayloadBytes = 32 * 1024 * 1024;
    public const int MaximumAckBytes = 16 * 1024 * 1024;
    public int IntervalMilliseconds { get; init; } = 1000;
    public BatchAckPolicy Policy { get; init; }
    public void Validate()
    {
        if (IntervalMilliseconds is < 250 or > 60000 || !Enum.IsDefined(Policy))
            throw new ArgumentOutOfRangeException(nameof(IntervalMilliseconds));
    }
}

/// <summary>Immutable ordered payloads captured through the Stage 12 authority. No socket is
/// opened in preparation. A changed/closed/inactive source must be prepared and reviewed again.</summary>
public sealed class PreparedTransmissionBatch
{
    private readonly MessageDocument source;
    internal readonly TransportMessageCapture[] Captures;
    internal readonly FileSaveDestination[] ProtectedDestinations;
    internal string? SourceStorage => source.Snapshot?.DirectoryPath;
    public Guid Id { get; } = Guid.NewGuid();
    public string DocumentId => source.Id;
    public int DocumentNumber { get; }
    public long Revision { get; }
    public ResolvedEndpoint Endpoint { get; }
    public BatchTransmissionOptions Options { get; }
    public IReadOnlyList<int> Positions { get; }
    public int Count => Captures.Length;
    public int PayloadBytes { get; }
    public bool Transcoded => Captures.Any(c => c.Transcoded);
    public TimeSpan MinimumDuration => TimeSpan.FromMilliseconds((long)(Count - 1) * Options.IntervalMilliseconds);
    public bool IsCurrent(DocumentManager documents) => documents.Documents.Contains(source) && source.IsSearchCurrent(Revision);
    public bool IsActive(DocumentManager documents) => IsCurrent(documents) && ReferenceEquals(documents.ActiveDocument, source);
    internal PreparedTransmissionBatch(MessageDocument source, long revision, TransportMessageCapture[] captures,
        ResolvedEndpoint endpoint, BatchTransmissionOptions options, int bytes)
    {
        this.source = source; Revision = revision; Captures = captures; Endpoint = endpoint; Options = options;
        DocumentNumber = source.ActivityNumber; PayloadBytes = bytes;
        Positions = Array.AsReadOnly(captures.Select(c => c.MessageNumber).ToArray());
        ProtectedDestinations = source.ExtractionDestinations();
    }
    public byte[] CopyPayload(int index) => Captures[index].CopyPayload();
    public string Review => $"Batch {Id}\n{Endpoint.Display}\nSource Document {DocumentNumber} ({DocumentId}), revision {Revision}." +
        $"\nMessages: {Count}; positions {string.Join(", ", Positions.Take(20))}{(Count > 20 ? " (remaining positions listed in the prepared table)" : "")}. Payload bytes: {PayloadBytes:N0}." +
        $"\nMode: sequential, one message per connection. Interval after each exchange: {Options.IntervalMilliseconds:N0} ms." +
        $"\nACK policy: {Options.Policy}. Estimated minimum interval time: {MinimumDuration.TotalSeconds:N2} seconds, plus connection/ACK time." +
        (Transcoded ? "\nExplicit encoding conversion was required; characters retained, source bytes differ." : "\nSource encoding retained.") +
        "\nMessages may affect receiving systems. This pacing does not guarantee protection from overload. No retries, scheduling or destination fallback. Authorize this batch run?";
}

/// <summary>Bounded local outcome. No payload/control identifiers are included in display or CSV.
/// Exact ACK bytes are session-only and returned as copies for deliberate viewer publication.</summary>
public sealed class BatchMessageResult
{
    private readonly byte[][] acknowledgments;
    public int Position { get; }
    public DateTimeOffset? Started { get; }
    public DateTimeOffset? Ended { get; }
    public ExchangeResult? Exchange { get; }
    public int AckFrames { get; }
    public int RetainedAckFrames => acknowledgments.Length;
    public bool Attempted => Started is not null;
    public bool Uncertain => Exchange is { WriteStarted: true, ApplicationAccepted: false } e &&
        e.Outcome is not (ExchangeOutcome.ApplicationError or ExchangeOutcome.ApplicationRejected or ExchangeOutcome.CommitError or ExchangeOutcome.CommitRejected);
    public BatchMessageClassification Classification => Classify(Exchange);
    public string CommitStatus => Exchange?.Codes.FirstOrDefault(c => c is "CA" or "CE" or "CR") ?? "Unobserved";
    public string ApplicationStatus => Exchange?.Codes.FirstOrDefault(c => c is "AA" or "AE" or "AR") ?? "Unobserved";
    public string Correlation => Exchange?.Outcome switch
    {
        ExchangeOutcome.MismatchedAck => "Mismatch",
        ExchangeOutcome.InvalidAck or ExchangeOutcome.MissingAckFields or ExchangeOutcome.UnexpectedAck => "InvalidOrUnexpected",
        _ => Exchange?.Codes.Count > 0 ? "Validated" : "Unobserved"
    };
    internal BatchMessageResult(int position, DateTimeOffset? started = null, DateTimeOffset? ended = null,
        ExchangeResult? exchange = null, bool retainAcks = true)
    {
        Position = position; Started = started; Ended = ended;
        AckFrames = exchange?.Acknowledgments.Count ?? 0;
        acknowledgments = retainAcks ? exchange?.Acknowledgments.Select(a => (byte[])a.Clone()).ToArray() ?? [] : [];
        // Keep metadata independently of the ACK retention budget; never retain hidden payloads
        // through ExchangeResult after deciding that the byte budget is exhausted.
        Exchange = exchange is null ? null : exchange with { Acknowledgments = [], Codes = new ReadOnlyCollection<string>(exchange.Codes.ToArray()) };
    }
    public byte[] CopyAcknowledgment(int index) => (byte[])acknowledgments[index].Clone();
    public static BatchMessageClassification Classify(ExchangeResult? e) => e?.Outcome switch
    {
        null => BatchMessageClassification.NotAttempted,
        ExchangeOutcome.ApplicationAccepted when e.ApplicationAccepted => BatchMessageClassification.ApplicationAccepted,
        ExchangeOutcome.ApplicationError => BatchMessageClassification.ApplicationError,
        ExchangeOutcome.ApplicationRejected => BatchMessageClassification.ApplicationRejected,
        ExchangeOutcome.CommitError => BatchMessageClassification.CommitError,
        ExchangeOutcome.CommitRejected => BatchMessageClassification.CommitRejected,
        ExchangeOutcome.Canceled => BatchMessageClassification.Canceled,
        ExchangeOutcome.TimedOut or ExchangeOutcome.ConditionalAckUnobserved => BatchMessageClassification.Timeout,
        ExchangeOutcome.CommitAccepted => BatchMessageClassification.CommitAcceptedApplicationUnknown,
        ExchangeOutcome.SentWithoutAck => BatchMessageClassification.ApplicationUnknown,
        _ when e.WriteStarted => BatchMessageClassification.UncertainDelivery,
        _ => BatchMessageClassification.TransportFailure
    };
    public override string ToString() => $"Message {Position} · {Classification} · {Exchange?.WriteSummary ?? "not attempted"}" +
        $" · commit {CommitStatus} · application {ApplicationStatus} · correlation {Correlation} · ACK frames {AckFrames}" +
        (Started is { } start ? $" · {start:HH:mm:ss.fff}–{Ended:HH:mm:ss.fff} UTC · {Exchange?.Duration.TotalMilliseconds:F1} ms · {Exchange?.Outcome} · phase {Exchange?.FailureStage}" : "") +
        (Exchange?.SocketError is { } socket ? " · socket " + socket : "") +
        (RetainedAckFrames < AckFrames ? " (bytes not retained: session budget)" : "") +
        (Uncertain ? " · delivery/processing uncertain" : "");
}

public sealed record BatchTransmissionProgress(BatchTransmissionState State, int Total, int Attempted, int Accepted,
    int RejectedError, int TimedOut, int Uncertain, int Canceled, int NotAttempted, int? CurrentPosition,
    TimeSpan Elapsed, TimeSpan AverageExchange, IReadOnlyList<BatchMessageResult> Results);
