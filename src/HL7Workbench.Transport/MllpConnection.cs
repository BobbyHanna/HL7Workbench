using System.Diagnostics;
using System.Net.Sockets;
using HL7Workbench.Core;

namespace HL7Workbench.Transport;

public enum ExchangeOutcome { ApplicationAccepted, ApplicationError, ApplicationRejected, CommitAccepted, CommitError, CommitRejected,
    SentWithoutAck, ConditionalAckUnobserved, TimedOut, MissingAck, MissingAckFields, InvalidAck, MismatchedAck, Disconnected, Canceled, FramingError, UnexpectedAck,
    ConnectionFailed, WriteFailed }
public enum ExchangeFailureStage { None, Connection, Write, Response }
/// <param name="WriteDuration">Monotonic duration of a complete local frame write; absent for incomplete writes.</param>
/// <param name="AckRoundTrip">Time from completed write to the last received complete ACK frame, including invalid/mismatched frames; absent when no complete frame arrives.</param>
public sealed record ExchangeResult(ExchangeOutcome Outcome, bool WriteStarted, IReadOnlyList<byte[]> Acknowledgments,
    IReadOnlyList<string> Codes, TimeSpan Duration, bool WriteCompleted = false, ExchangeFailureStage FailureStage = ExchangeFailureStage.None,
    SocketError? SocketError = null, TimeSpan? WriteDuration = null, TimeSpan? AckRoundTrip = null)
{
    public bool ApplicationAccepted => WriteCompleted && Outcome == ExchangeOutcome.ApplicationAccepted && Codes.Contains("AA");
    public string WriteSummary => !WriteStarted ? "MLLP write not attempted" : WriteCompleted ? "MLLP write completed (local TCP)" : "MLLP write incomplete; delivery uncertain";
    public string Summary => $"{Outcome} · {WriteSummary} · ACK frames received: {Acknowledgments.Count} · " +
        (ApplicationAccepted ? "application accepted (correlated AA)" : "application acceptance not established") +
        (FailureStage == ExchangeFailureStage.None ? "" : " · failure during " + FailureStage) +
        (SocketError is { } error ? " · socket: " + error : "");
}

/// <summary>One explicit connection, one exchange at a time. Failed/ambiguous exchanges are never reused or retried.</summary>
public sealed class MllpConnection : IDisposable
{
    private TcpClient? client;
    private MllpReader? reader;
    private int exchanging;
    public ResolvedEndpoint? Destination { get; private set; }
    public bool IsConnected => client is not null;
    public async Task ConnectAsync(ResolvedEndpoint destination, bool nonLoopbackConfirmed, CancellationToken token = default)
    {
        if (IsConnected || Interlocked.CompareExchange(ref exchanging, 1, 0) != 0) throw new InvalidOperationException("Disconnect before connecting again.");
        try
        {
            destination.Profile.Validate();
            if (destination.Profile.Security != TransportSecurity.PlainTcp) throw new NotSupportedException("TLS unavailable.");
            if (destination.Scope != AddressScope.Loopback && !nonLoopbackConfirmed) throw new InvalidOperationException("Non-loopback confirmation is required.");
            var socket = new TcpClient(destination.Address.AddressFamily) { NoDelay = true };
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(destination.Profile.ConnectTimeoutSeconds));
            try { await socket.ConnectAsync(destination.Address, destination.Profile.Port, timeout.Token).ConfigureAwait(false); }
            catch { socket.Dispose(); throw; }
            client = socket; reader = new(socket.GetStream(), MllpFrame.MaximumPayloadBytes); Destination = destination;
        }
        finally { Volatile.Write(ref exchanging, 0); }
    }
    public async Task<ExchangeResult> SendAsync(ReadOnlyMemory<byte> payload, bool sendConfirmed, bool nonLoopbackConfirmed,
        CancellationToken token = default)
    {
        if (!sendConfirmed) throw new InvalidOperationException("Explicit send confirmation is required.");
        if (Interlocked.CompareExchange(ref exchanging, 1, 0) != 0) throw new InvalidOperationException("An exchange is already active.");
        var clock = Stopwatch.StartNew(); var frames = new List<byte[]>(); var codes = new List<string>(); var wrote = false; var completed = false;
        Hl7TransportHeader? requestHeader = null;
        long writeStart = 0, writeEnd = 0, lastAck = 0;
        ExchangeResult Result(ExchangeOutcome outcome, ExchangeFailureStage failure = ExchangeFailureStage.None, SocketError? socketError = null) =>
            new(outcome, wrote, frames.AsReadOnly(), codes.AsReadOnly(), clock.Elapsed, completed, failure, socketError,
                completed ? Stopwatch.GetElapsedTime(writeStart, writeEnd) : null,
                lastAck != 0 ? Stopwatch.GetElapsedTime(writeEnd, lastAck) : null);
        ExchangeFailureStage FailureStage() => completed ? ExchangeFailureStage.Response : wrote ? ExchangeFailureStage.Write : ExchangeFailureStage.None;
        try
        {
            var destination = Destination ?? throw new InvalidOperationException("Connect first.");
            var socket = client ?? throw new InvalidOperationException("Connect first.");
            var responseReader = reader ?? throw new InvalidOperationException("Connect first.");
            if (destination.Scope != AddressScope.Loopback && !nonLoopbackConfirmed) throw new InvalidOperationException("Non-loopback send confirmation is required.");
            var header = Hl7TransportHeader.Read(payload.Span, destination.Profile.GetEncoding());
            requestHeader = header;
            var framed = MllpFrame.Encode(payload.Span);
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(destination.Profile.AckTimeoutSeconds));
            timeout.Token.ThrowIfCancellationRequested(); wrote = true;
            writeStart = Stopwatch.GetTimestamp();
            await socket.GetStream().WriteAsync(framed, timeout.Token).ConfigureAwait(false);
            writeEnd = Stopwatch.GetTimestamp();
            // NetworkStream is unbuffered: successful completion covers the entire
            // supplied frame locally. Flush is a no-op and cannot prove peer receipt.
            completed = true;
            if (header.Accept == AckRequest.NE && header.Application == AckRequest.NE) { Disconnect(); return Result(ExchangeOutcome.SentWithoutAck); }
            var acceptDone = header.Accept == AckRequest.NE; var appDone = header.Application == AckRequest.NE;
            while (!acceptDone || !appDone)
            {
                var frame = await responseReader.ReadAsync(timeout.Token).ConfigureAwait(false);
                if (frame is null) { Disconnect(); return Result(ExchangeOutcome.MissingAck); }
                frames.Add(frame);
                lastAck = Stopwatch.GetTimestamp();
                string code;
                try { code = header.ReadAck(frame, destination.Profile.GetEncoding()); }
                catch (Hl7AckException error)
                {
                    Disconnect(); return Result(error.Failure switch { AckValidationFailure.Mismatch => ExchangeOutcome.MismatchedAck, AckValidationFailure.MissingFields => ExchangeOutcome.MissingAckFields, _ => ExchangeOutcome.InvalidAck });
                }
                catch (InvalidDataException) { Disconnect(); return Result(ExchangeOutcome.InvalidAck); }
                codes.Add(code);
                var commit = code.StartsWith('C'); var request = commit ? header.Accept : header.Application;
                // ER suppresses a successful commit ACK. A subsequent application
                // response may therefore be the first frame of a valid exchange.
                if (!commit && !acceptDone && header.Accept == AckRequest.ER) acceptDone = true;
                if (commit && acceptDone || !commit && (appDone || !acceptDone) || !Hl7TransportHeader.Wants(request, code is "CA" or "AA"))
                { Disconnect(); return Result(ExchangeOutcome.UnexpectedAck); }
                if (code is "AE" or "AR" or "CE" or "CR")
                {
                    Disconnect(); return Result(code switch { "AE" => ExchangeOutcome.ApplicationError, "AR" => ExchangeOutcome.ApplicationRejected, "CE" => ExchangeOutcome.CommitError, _ => ExchangeOutcome.CommitRejected });
                }
                if (commit) acceptDone = true; else appDone = true;
            }
            if (responseReader.Pending > 0) { Disconnect(); return Result(ExchangeOutcome.UnexpectedAck); }
            // Closing also prevents an unsolicited late frame being mistaken for the
            // next request, including reuse of the same MSH-10. Reconnect is explicit.
            Disconnect(); return Result(codes.Contains("AA") ? ExchangeOutcome.ApplicationAccepted : ExchangeOutcome.CommitAccepted);
        }
        catch (OperationCanceledException)
        {
            Disconnect();
            if (token.IsCancellationRequested) return Result(ExchangeOutcome.Canceled, FailureStage());
            // No conditional silence is reported as application success.
            var conditional = requestHeader is { } h && (h.Accept is AckRequest.SU or AckRequest.ER || h.Application is AckRequest.SU or AckRequest.ER);
            return Result(conditional ? ExchangeOutcome.ConditionalAckUnobserved : ExchangeOutcome.TimedOut, FailureStage());
        }
        catch (MllpException) { Disconnect(); return Result(ExchangeOutcome.FramingError); }
        catch (Hl7LimitException) { Disconnect(); return Result(ExchangeOutcome.InvalidAck); }
        catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException)
        {
            Disconnect(); var code = (error as SocketException ?? error.InnerException as SocketException)?.SocketErrorCode;
            return Result(token.IsCancellationRequested ? ExchangeOutcome.Canceled : completed ? ExchangeOutcome.Disconnected : ExchangeOutcome.WriteFailed, FailureStage(), code);
        }
        finally { Volatile.Write(ref exchanging, 0); }
    }
    public void Disconnect() { client?.Dispose(); client = null; reader?.Dispose(); reader = null; }
    public void Dispose() => Disconnect();
}
