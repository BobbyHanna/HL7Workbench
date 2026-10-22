using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using HL7Workbench.Core;

namespace HL7Workbench.Transport;

public sealed record ListenerOptions
{
    public IPAddress BindAddress { get; init; } = IPAddress.Loopback;
    public int Port { get; init; } = 2575;
    public int MaximumClients { get; init; } = 4;
    public int MaximumQueuedReceipts { get; init; } = 64;
    public int MaximumSessionBytes { get; init; } = 16 * 1024 * 1024;
    public int MaximumFrameBytes { get; init; } = MllpFrame.MaximumPayloadBytes;
    public int IdleTimeoutSeconds { get; init; } = 30;
    public TransportEncoding Encoding { get; init; }
    public TestAckMode AckMode { get; init; } = TestAckMode.Positive;
    public void Validate()
    {
        if (Port is < 0 or > 65535 || MaximumClients is < 1 or > 16 || MaximumQueuedReceipts is < 1 or > 200 ||
            MaximumFrameBytes is < 1 or > MllpFrame.MaximumPayloadBytes || MaximumSessionBytes < MaximumFrameBytes || MaximumSessionBytes > 32 * 1024 * 1024 ||
            IdleTimeoutSeconds is < 1 or > 120 || !Enum.IsDefined(Encoding) || !Enum.IsDefined(AckMode)) throw new ArgumentOutOfRangeException(nameof(ListenerOptions));
    }
}
public sealed record TransportReceipt(DateTimeOffset Time, string Remote, byte[] Payload, bool ParseEligible,
    IReadOnlyList<string> AckCodes, string Status, TransportEncoding Encoding, TimeSpan Duration = default,
    long ConnectionSocketBytesRead = 0, int PartialPayloadBytes = 0);

/// <summary>Bounded listener. Receipt admission precedes test ACK: an overflow is never acknowledged as accepted.</summary>
public sealed class MllpListener : IAsyncDisposable
{
    private readonly object gate = new();
    private readonly object lifecycleGate = new();
    private readonly Queue<TransportReceipt> receipts = new();
    private readonly ConcurrentDictionary<int, (TcpClient Client, Task Work)> clients = new();
    private TcpListener? server;
    private CancellationTokenSource? lifetime;
    private Task acceptTask = Task.CompletedTask;
    private Task stoppingTask = Task.CompletedTask;
    private bool disposed;
    private long retainedBytes;
    private int ordinal, rejected;
    public bool IsRunning => server is not null;
    public IPEndPoint? LocalEndpoint => server?.LocalEndpoint as IPEndPoint;
    public int RejectedClients => Volatile.Read(ref rejected);
    public IReadOnlyList<string> Clients => clients.Values.Select(c => SafeRemote(c.Client)).ToArray();
    public IReadOnlyList<TransportReceipt> Receipts { get { lock (gate) return receipts.ToArray(); } }
    public void Start(ListenerOptions options, bool nonLoopbackConfirmed)
    {
        options.Validate();
        lock (lifecycleGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (IsRunning || !acceptTask.IsCompleted || !stoppingTask.IsCompleted) throw new InvalidOperationException("Stop and drain the listener first.");
            if (!IPAddress.IsLoopback(options.BindAddress) && !nonLoopbackConfirmed) throw new InvalidOperationException("Non-loopback binding requires confirmation.");
            var listener = new TcpListener(options.BindAddress, options.Port);
            listener.Start(options.MaximumClients); server = listener; lifetime = new();
            acceptTask = AcceptAsync(listener, options, lifetime.Token);
        }
    }
    private async Task AcceptAsync(TcpListener listener, ListenerOptions options, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(token).ConfigureAwait(false);
                if (clients.Count >= options.MaximumClients) { Interlocked.Increment(ref rejected); client.Dispose(); continue; }
                var id = Interlocked.Increment(ref ordinal);
                // Register before the worker can complete, so shutdown owns every socket.
                var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var task = HandleAsync(client, options, ready.Task, token); clients[id] = (client, task); ready.SetResult();
                _ = RemoveAsync(id, task);
            }
        }
        catch (Exception error) when (error is OperationCanceledException or SocketException or ObjectDisposedException) { }
    }
    private async Task RemoveAsync(int id, Task task) { await task.ConfigureAwait(false); clients.TryRemove(id, out _); }
    private async Task HandleAsync(TcpClient client, ListenerOptions options, Task ready, CancellationToken token)
    {
        await ready.ConfigureAwait(false); var remote = SafeRemote(client); var encoding = TransportEndpoint.StrictEncoding(options.Encoding); var clock = Stopwatch.StartNew();
        using (client)
        using (var reader = new MllpReader(client.GetStream(), options.MaximumFrameBytes))
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    clock.Restart();
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(options.IdleTimeoutSeconds));
                    var bytes = await reader.ReadAsync(timeout.Token).ConfigureAwait(false); if (bytes is null) break;
                    IReadOnlyList<byte[]> acks = []; var eligible = false;
                    try { var header = Hl7TransportHeader.Read(bytes, encoding); eligible = true; acks = header.CreateTestAcks(options.AckMode, encoding); }
                    catch (Exception error) when (error is InvalidDataException or Hl7LimitException or ArgumentException or InvalidOperationException) { eligible = false; }
                    var codes = acks.Select(a => Hl7Parser.Parse(a, new() { Encoding = encoding }).GetDecoded("MSA-1").Text).ToArray();
                    if (!Admit(new(DateTimeOffset.UtcNow, remote, bytes, eligible, codes, eligible ? "Complete frame · eligible · test ACK prepared (delivery not asserted)" : "Complete frame · message/test ACK ineligible · no test ACK", options.Encoding, clock.Elapsed, reader.SocketBytesRead, reader.PartialPayloadBytes), options)) break;
                    foreach (var ack in acks) await client.GetStream().WriteAsync(MllpFrame.Encode(ack), timeout.Token).ConfigureAwait(false);
                }
            }
            catch (MllpException error) { Admit(new(DateTimeOffset.UtcNow, remote, [], false, [], "Framing: " + error.Failure, options.Encoding, clock.Elapsed, reader.SocketBytesRead, reader.PartialPayloadBytes), options); }
            catch (OperationCanceledException)
            {
                if (!token.IsCancellationRequested) Admit(new(DateTimeOffset.UtcNow, remote, [], false, [], reader.FrameInProgress ? "Partial-frame timeout" : "Idle timeout before next frame",
                    options.Encoding, clock.Elapsed, reader.SocketBytesRead, reader.PartialPayloadBytes), options);
            }
            catch (Exception error) when (error is IOException or SocketException or ObjectDisposedException) { }
        }
    }
    private bool Admit(TransportReceipt receipt, ListenerOptions options)
    {
        lock (gate)
        {
            if (receipts.Count >= options.MaximumQueuedReceipts || retainedBytes + receipt.Payload.Length > options.MaximumSessionBytes)
            { Interlocked.Increment(ref rejected); return false; }
            receipts.Enqueue(receipt); retainedBytes += receipt.Payload.Length; return true;
        }
    }
    public void ClearReceipts() { lock (gate) { receipts.Clear(); retainedBytes = 0; } }
    public Task StopAsync()
    {
        lock (lifecycleGate)
        {
            // Stop, tool disposal and window shutdown share one drain. Starting a
            // new generation before it finishes could otherwise clear new clients.
            if (server is null) return stoppingTask;
            var cancellation = lifetime!; lifetime = null;
            var socket = server; server = null; cancellation.Cancel(); socket.Stop();
            return stoppingTask = DrainAsync(cancellation, acceptTask);
        }
    }
    private async Task DrainAsync(CancellationTokenSource cancellation, Task accepting)
    {
        await accepting.ConfigureAwait(false);
        var pending = clients.Values.ToArray(); foreach (var item in pending) item.Client.Dispose();
        await Task.WhenAll(pending.Select(c => c.Work)).ConfigureAwait(false); clients.Clear();
        cancellation.Dispose();
    }
    private static string SafeRemote(TcpClient client) { try { return client.Client.RemoteEndPoint?.ToString() ?? "Disconnected"; } catch (ObjectDisposedException) { return "Disconnected"; } }
    public async ValueTask DisposeAsync()
    { lock (lifecycleGate) disposed = true; await StopAsync().ConfigureAwait(false); ClearReceipts(); }
}
