using System.ComponentModel;
using System.Diagnostics;
using System.Net.Sockets;
using HL7Workbench.Core;
using HL7Workbench.Transport;

namespace HL7Workbench.Workspace;

public sealed record TransportHistoryEntry(DateTimeOffset Time, string Direction, string Endpoint, string Status,
    string Codes, TimeSpan Duration, string? DocumentId, MessageId? MessageId, byte[] Payload,
    IReadOnlyList<byte[]> Acknowledgments, TransportEncoding Encoding, int? DocumentNumber = null, int? MessageNumber = null,
    bool WriteStarted = false, bool WriteCompleted = false, ExchangeFailureStage FailureStage = ExchangeFailureStage.None, SocketError? SocketError = null)
{
    public string WriteSummary => !WriteStarted ? "MLLP write not attempted" : WriteCompleted ? "MLLP write completed (local TCP)" : "MLLP write incomplete; delivery uncertain";
    public override string ToString() => $"{Time:HH:mm:ss} · {Direction} · {Endpoint} · {Status} · {Codes} · {Duration.TotalSeconds:F2}s" +
        $" · {WriteSummary} · ACK frames received: {Acknowledgments.Count}" +
        (Status == nameof(ExchangeOutcome.ApplicationAccepted) && WriteCompleted ? " · application accepted (correlated AA)" : " · application acceptance not established") +
        (FailureStage == ExchangeFailureStage.None ? "" : " · failure during " + FailureStage) +
        (SocketError is { } error ? " · socket: " + error : "") +
        (DocumentNumber is { } document ? $" · Document {document} / message {MessageNumber}" : "");
}

/// <summary>Ephemeral local history, never a persistent payload log. UI callers own the synchronization context.</summary>
public sealed class TransportSession(DocumentManager documents) : INotifyPropertyChanged, IDisposable
{
    private readonly MllpConnection connection = new();
    private readonly List<TransportHistoryEntry> history = [];
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? work;
    private bool disposed;
    private int retainedBytes;
    public bool IsBusy => work is not null;
    public bool IsConnected => connection.IsConnected;
    public ResolvedEndpoint? Destination => connection.Destination;
    public string Status { get; private set; } = "Disconnected. Connect explicitly; each exchange disconnects after its result.";
    public IReadOnlyList<TransportHistoryEntry> History => history.ToArray();
    public Task ActionTask { get; private set; } = Task.CompletedTask;
    public event PropertyChangedEventHandler? PropertyChanged;
    public async Task ConnectAsync(ResolvedEndpoint endpoint, bool nonLoopbackConfirmed, CancellationToken token = default)
    {
        if (disposed || IsBusy) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); work = cancellation; Changed();
        try
        {
            await connection.ConnectAsync(endpoint, nonLoopbackConfirmed, cancellation.Token);
            // Connect completion can win the socket race just as the UI cancels.
            // Recheck on the owning context so Cancel never leaves a live connection.
            if (disposed || cancellation.IsCancellationRequested) { connection.Disconnect(); Status = "Connection canceled; disconnected."; }
            else Status = "Connection checked · " + endpoint.Display + " · Confirming Send opens one fresh connection; no retry.";
        }
        catch (Exception error) when (SafeFailure(error)) { Status = "Connection unavailable, canceled or timed out. No message sent; no fallback or retry."; documents.Activity.Record(ActivityKind.TransportFailed); }
        finally { work = null; Changed(); }
    }
    public Task SendAsync(TransportMessageCapture capture, bool confirmed, bool nonLoopbackConfirmed, CancellationToken token = default)
    {
        if (disposed || IsBusy || !confirmed) return Task.CompletedTask;
        return ActionTask = SendCoreAsync(capture, nonLoopbackConfirmed, token);
    }
    private async Task SendCoreAsync(TransportMessageCapture capture, bool nonLoopbackConfirmed, CancellationToken token)
    {
        if (!capture.IsCurrent(documents) || !IsConnected || Destination?.Profile.Encoding != capture.Encoding)
        { Status = "Source or connection changed. Capture and review again; nothing sent."; Changed(); return; }
        var endpoint = Destination!;
        var documentNumber = documents.Documents.FirstOrDefault(d => d.Id == capture.DocumentId)?.ActivityNumber;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token); work = cancellation; Changed();
        try
        {
            var bytes = capture.CopyPayload();
            // Human review can outlast a peer's idle deadline. The review explicitly
            // authorizes one new connection and one write to this pinned address.
            // Retire the check connection; never attempt a write on it or retry one.
            connection.Disconnect();
            var result = await Task.Run(async () =>
            {
                var clock = Stopwatch.StartNew();
                try
                {
                    await connection.ConnectAsync(endpoint, nonLoopbackConfirmed, cancellation.Token).ConfigureAwait(false);
                    cancellation.Token.ThrowIfCancellationRequested();
                    return (await connection.SendAsync(bytes, true, nonLoopbackConfirmed, cancellation.Token).ConfigureAwait(false)) with { Duration = clock.Elapsed };
                }
                catch (Exception error) when (error is SocketException or IOException or OperationCanceledException or ObjectDisposedException)
                {
                    connection.Disconnect();
                    return new ExchangeResult(cancellation.IsCancellationRequested ? ExchangeOutcome.Canceled : error is OperationCanceledException ? ExchangeOutcome.TimedOut : ExchangeOutcome.ConnectionFailed,
                        false, [], [], clock.Elapsed, FailureStage: ExchangeFailureStage.Connection,
                        SocketError: (error as SocketException ?? error.InnerException as SocketException)?.SocketErrorCode);
                }
            });
            if (disposed) return;
            Status = result.Summary + " · Disconnected. No automatic retry.";
            Add(new(DateTimeOffset.UtcNow, "Outbound", endpoint.Display, result.Outcome.ToString(), string.Join(", ", result.Codes), result.Duration,
                capture.DocumentId, capture.MessageId, bytes, result.Acknowledgments, capture.Encoding,
                documentNumber, capture.MessageNumber, result.WriteStarted, result.WriteCompleted, result.FailureStage, result.SocketError));
            documents.Activity.Record(result.Outcome is ExchangeOutcome.ConnectionFailed or ExchangeOutcome.WriteFailed or ExchangeOutcome.Disconnected or ExchangeOutcome.TimedOut
                ? ActivityKind.TransportFailed : ActivityKind.TransportExchange, count: 1);
        }
        catch (Exception error) when (SafeFailure(error)) { Status = "Send unavailable. Review source, encoding and destination again. No retry."; documents.Activity.Record(ActivityKind.TransportFailed); }
        finally { work = null; Changed(); }
    }
    public Task<MessageDocument> OpenPayloadAsync(TransportHistoryEntry item, int? ack = null, CancellationToken token = default)
    {
        var bytes = ack is { } n ? item.Acknowledgments[n] : item.Payload;
        return OpenLocalPayloadAsync(documents, bytes, item.Encoding, ack is null ? "Transport message.hl7" : "Transport ACK.hl7", token);
    }
    public static async Task<MessageDocument> OpenLocalPayloadAsync(DocumentManager documents, byte[] bytes, TransportEncoding encoding, string title, CancellationToken token = default)
    {
        var message = await Task.Run(() => Hl7Parser.Parse(bytes, new() { Encoding = TransportEndpoint.StrictEncoding(encoding) }), token);
        token.ThrowIfCancellationRequested();
        var document = MessageFileReader.Open(documents, title, message); document.MarkGeneratedUnsaved(); return document;
    }
    private void Add(TransportHistoryEntry item)
    {
        var size = item.Payload.Length + item.Acknowledgments.Sum(a => a.Length);
        while (history.Count > 0 && (history.Count >= 200 || retainedBytes + size > 16 * 1024 * 1024))
        { var old = history[0]; retainedBytes -= old.Payload.Length + old.Acknowledgments.Sum(a => a.Length); history.RemoveAt(0); }
        history.Add(item); retainedBytes += size;
    }
    public void ClearHistory() { history.Clear(); retainedBytes = 0; Changed(); }
    public void Disconnect() { work?.Cancel(); connection.Disconnect(); Status = "Disconnected. No retry."; Changed(); }
    private void Changed() { if (!disposed) PropertyChanged?.Invoke(this, new(null)); }
    private static bool SafeFailure(Exception error) => error is IOException or System.Net.Sockets.SocketException or ArgumentException or InvalidOperationException or NotSupportedException or OperationCanceledException or Hl7LimitException;
    public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); work?.Cancel(); connection.Dispose(); ClearHistory(); lifetime.Dispose(); }
}
