using System.Net;
using System.Net.Sockets;
using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Transport;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class TransportNetworkTests
{
    internal static byte[] Message(string accept = "", string application = "", string id = "SYN-TRANSPORT") => Encoding.UTF8.GetBytes(
        $"MSH|^~\\&|SYN-SEND|SYN-FAC|SYN-RECV|SYN-DEST|20300101120000||ADT^A01|{id}|T|2.5.1|||{accept}|{application}\rPID|1||SYNTHETIC\r");
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    internal static async Task WaitAsync(Func<bool> ready)
    { using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token); timeout.CancelAfter(5000); while (!ready()) await Task.Delay(10, timeout.Token); }
    internal static TransportEndpoint Endpoint(int port) => new() { Port = port, ConnectTimeoutSeconds = 1, AckTimeoutSeconds = 1 };
    internal static async Task<byte[]> ReadAsync(NetworkStream stream, CancellationToken token)
    {
        using var decoder = new MllpDecoder(); var buffer = new byte[8192];
        while (true) { var n = await stream.ReadAsync(buffer, token); if (n == 0) { decoder.Complete(); throw new EndOfStreamException(); } decoder.Feed(buffer.AsSpan(0, n)); if (decoder.Take() is { } result) return result; }
    }
    [Theory]
    [InlineData("", "", TestAckMode.Positive, ExchangeOutcome.ApplicationAccepted, "AA")]
    [InlineData("", "", TestAckMode.ApplicationError, ExchangeOutcome.ApplicationError, "AE")]
    [InlineData("", "", TestAckMode.ApplicationReject, ExchangeOutcome.ApplicationRejected, "AR")]
    [InlineData("AL", "AL", TestAckMode.Positive, ExchangeOutcome.ApplicationAccepted, "CA,AA")]
    [InlineData("AL", "AL", TestAckMode.CommitError, ExchangeOutcome.CommitError, "CE")]
    [InlineData("AL", "AL", TestAckMode.CommitReject, ExchangeOutcome.CommitRejected, "CR")]
    [InlineData("AL", "NE", TestAckMode.Positive, ExchangeOutcome.CommitAccepted, "CA")]
    [InlineData("ER", "AL", TestAckMode.Positive, ExchangeOutcome.ApplicationAccepted, "AA")]
    [InlineData("NE", "NE", TestAckMode.Positive, ExchangeOutcome.SentWithoutAck, "")]
    public async Task RealLoopbackListenerPreservesBytesAndNegotiatesPhases(string accept, string app, TestAckMode mode, ExchangeOutcome expected, string codes)
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = mode }, false);
        using var connection = new MllpConnection(); await connection.ConnectAsync(new(Endpoint(listener.LocalEndpoint!.Port), IPAddress.Loopback), false, Token);
        var bytes = Message(accept, app); var result = await connection.SendAsync(bytes, true, false, Token);
        Assert.Equal(expected, result.Outcome); Assert.Equal(codes, string.Join(",", result.Codes)); Assert.True(result.WriteStarted); Assert.True(result.WriteCompleted); Assert.False(connection.IsConnected);
        await WaitAsync(() => listener.Receipts.Count == 1); Assert.Equal(bytes, listener.Receipts[0].Payload); Assert.True(listener.Receipts[0].ParseEligible);
        Assert.Equal(bytes.Length + 3, listener.Receipts[0].ConnectionSocketBytesRead);
        await listener.StopAsync(); Assert.Empty(listener.Clients);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ServerCanCoalesceOrFragmentTwoAcks(bool fragmented)
    {
        var server = new TcpListener(IPAddress.Loopback, 0); server.Start(); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token); timeout.CancelAfter(5000);
        try
        {
            var peer = Task.Run(async () =>
            {
                using var client = await server.AcceptTcpClientAsync(timeout.Token); var request = await ReadAsync(client.GetStream(), timeout.Token);
                var frames = Hl7TransportHeader.Read(request, Encoding.UTF8).CreateTestAcks(TestAckMode.Positive, Encoding.UTF8).SelectMany(a => MllpFrame.Encode(a)).ToArray();
                if (fragmented) foreach (var b in frames) await client.GetStream().WriteAsync(new byte[] { b }, timeout.Token); else await client.GetStream().WriteAsync(frames, timeout.Token);
            }, timeout.Token);
            using var connection = new MllpConnection(); await connection.ConnectAsync(new(Endpoint(((IPEndPoint)server.LocalEndpoint).Port), IPAddress.Loopback), false, timeout.Token);
            var result = await connection.SendAsync(Message("AL", "AL"), true, false, timeout.Token); Assert.Equal(ExchangeOutcome.ApplicationAccepted, result.Outcome); Assert.Equal(2, result.Acknowledgments.Count); await peer;
        }
        finally { server.Stop(); }
    }
    [Theory]
    [InlineData("mismatch", ExchangeOutcome.MismatchedAck)]
    [InlineData("missing", ExchangeOutcome.MissingAckFields)]
    [InlineData("none", ExchangeOutcome.MissingAck)]
    [InlineData("partial", ExchangeOutcome.FramingError)]
    [InlineData("duplicate", ExchangeOutcome.UnexpectedAck)]
    public async Task InvalidMissingMismatchedOrExtraResponsesAreNotSuccess(string mode, ExchangeOutcome expected)
    {
        var server = new TcpListener(IPAddress.Loopback, 0); server.Start(); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token); timeout.CancelAfter(5000);
        try
        {
            var peer = Task.Run(async () =>
            {
                using var client = await server.AcceptTcpClientAsync(timeout.Token); var request = await ReadAsync(client.GetStream(), timeout.Token);
                if (mode == "none") return;
                if (mode == "partial") { await client.GetStream().WriteAsync(new byte[] { 11, 65 }, timeout.Token); return; }
                var ack = Hl7TransportHeader.Read(request, Encoding.UTF8).CreateTestAcks(TestAckMode.Positive, Encoding.UTF8)[0];
                if (mode == "mismatch") ack = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(ack).Replace("MSA|AA|SYN-TRANSPORT", "MSA|AA|SYN-OTHER"));
                if (mode == "missing") ack = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(ack).Replace("MSA|AA|SYN-TRANSPORT", "MSA|AA|"));
                var wire = MllpFrame.Encode(ack); await client.GetStream().WriteAsync(mode == "duplicate" ? wire.Concat(wire).ToArray() : wire, timeout.Token);
            }, timeout.Token);
            using var connection = new MllpConnection(); await connection.ConnectAsync(new(Endpoint(((IPEndPoint)server.LocalEndpoint).Port), IPAddress.Loopback), false, timeout.Token);
            var result = await connection.SendAsync(Message(), true, false, timeout.Token); Assert.Equal(expected, result.Outcome); Assert.False(result.ApplicationAccepted); await peer;
        }
        finally { server.Stop(); }
    }
    [Theory]
    [InlineData(false, false, ExchangeOutcome.TimedOut)]
    [InlineData(true, false, ExchangeOutcome.ConditionalAckUnobserved)]
    [InlineData(false, true, ExchangeOutcome.Canceled)]
    public async Task SilenceCancellationAndTimeoutNeverRetry(bool conditional, bool cancel, ExchangeOutcome expected)
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = TestAckMode.None }, false);
        using var connection = new MllpConnection(); await connection.ConnectAsync(new(Endpoint(listener.LocalEndpoint!.Port), IPAddress.Loopback), false, Token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token); if (cancel) cancellation.CancelAfter(100);
        var result = await connection.SendAsync(conditional ? Message("NE", "ER") : Message(), true, false, cancellation.Token);
        Assert.Equal(expected, result.Outcome); Assert.True(result.WriteStarted); Assert.False(connection.IsConnected); Assert.Single(listener.Receipts);
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.SendAsync(Message(), true, false, Token)); Assert.Single(listener.Receipts);
    }
    [Fact]
    public async Task ManualSendAndNonLoopbackSecurityGuardsRunBeforeNetworkUse()
    {
        using var connection = new MllpConnection();
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.SendAsync(Message(), false, false, Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => connection.ConnectAsync(new(Endpoint(2575), IPAddress.Parse("192.0.2.1")), false, Token));
        await Assert.ThrowsAsync<NotSupportedException>(() => ResolvedEndpoint.ResolveAsync(Endpoint(2575) with { Security = TransportSecurity.TlsUnavailable }, Token));
        await using var listener = new MllpListener(); Assert.True(IPAddress.IsLoopback(new ListenerOptions().BindAddress));
        Assert.Throws<InvalidOperationException>(() => listener.Start(new() { BindAddress = IPAddress.Any, Port = 0 }, false)); Assert.False(listener.IsRunning);
    }
    [Fact]
    public async Task ListenerStopsDrainsAndRestartsWithFragmentedAndMalformedClients()
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = TestAckMode.None }, false); var first = listener.LocalEndpoint!;
        using (var client = new TcpClient())
        { await client.ConnectAsync(first.Address, first.Port, Token); await client.GetStream().WriteAsync(new byte[] { 11, 65 }, Token); await WaitAsync(() => listener.Clients.Count == 1); }
        await WaitAsync(() => listener.Receipts.Count == 1); Assert.Contains("Truncated", listener.Receipts[0].Status);
        using var pending = new TcpClient(); await pending.ConnectAsync(first.Address, first.Port, Token); await WaitAsync(() => listener.Clients.Count == 1);
        await listener.StopAsync(); Assert.Empty(listener.Clients); Assert.False(listener.IsRunning);
        listener.Start(new() { Port = first.Port }, false); Assert.True(listener.IsRunning);
        await listener.StopAsync();
    }
    [Fact]
    public async Task SimultaneousStopAndDisposalShareOneDrainAndCannotReviveDisposedListener()
    {
        var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, listener.LocalEndpoint!.Port, Token);
        await client.GetStream().WriteAsync(new byte[] { 11, 65 }, Token); await WaitAsync(() => listener.Clients.Count == 1);
        var stopped = listener.StopAsync(); Assert.Same(stopped, listener.StopAsync());
        var disposed = listener.DisposeAsync().AsTask(); await Task.WhenAll(stopped, disposed); Assert.Empty(listener.Clients); Assert.False(listener.IsRunning);
        Assert.Throws<ObjectDisposedException>(() => listener.Start(new() { Port = 0 }, false)); await listener.DisposeAsync();
    }
    [Fact]
    public async Task ClientAndReceiptAdmissionRemainBoundedAndOverflowIsNotAcknowledged()
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, MaximumClients = 1, MaximumQueuedReceipts = 1 }, false);
        using var first = new TcpClient(); await first.ConnectAsync(IPAddress.Loopback, listener.LocalEndpoint!.Port, Token); await WaitAsync(() => listener.Clients.Count == 1);
        using var second = new TcpClient(); await second.ConnectAsync(IPAddress.Loopback, listener.LocalEndpoint.Port, Token); await WaitAsync(() => listener.RejectedClients == 1);
        Assert.Single(listener.Clients); await first.GetStream().WriteAsync(MllpFrame.Encode(Message()), Token); await ReadAsync(first.GetStream(), Token);
        await first.GetStream().WriteAsync(MllpFrame.Encode(Message(id: "SYN-SECOND")), Token); await WaitAsync(() => listener.RejectedClients == 2);
        Assert.Single(listener.Receipts); using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token); timeout.CancelAfter(5000);
        Assert.Equal(0, await first.GetStream().ReadAsync(new byte[1], timeout.Token));
        listener.ClearReceipts(); Assert.Empty(listener.Receipts);
    }
    [Fact]
    public async Task MalformedMessageIsRetainedWithoutAutomaticAa()
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, listener.LocalEndpoint!.Port, Token);
        var bytes = Encoding.UTF8.GetBytes("PID|SYNTHETIC\r"); await client.GetStream().WriteAsync(MllpFrame.Encode(bytes), Token);
        await WaitAsync(() => listener.Receipts.Count == 1); var receipt = listener.Receipts[0]; Assert.Equal(bytes, receipt.Payload); Assert.False(receipt.ParseEligible); Assert.Empty(receipt.AckCodes);
    }
    [Fact]
    public async Task MultipleConcurrentClientsRemainIndependentAndExact()
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false); var endpoint = new ResolvedEndpoint(Endpoint(listener.LocalEndpoint!.Port), IPAddress.Loopback);
        var sends = Enumerable.Range(1, 4).Select(async n =>
        {
            using var connection = new MllpConnection(); await connection.ConnectAsync(endpoint, false, Token);
            var result = await connection.SendAsync(Message(id: "SYN-CONCURRENT-" + n), true, false, Token); Assert.Equal(ExchangeOutcome.ApplicationAccepted, result.Outcome);
        });
        await Task.WhenAll(sends); Assert.Equal(4, listener.Receipts.Count); Assert.Equal(0, listener.RejectedClients); Assert.Equal(4, listener.Receipts.Select(r => Encoding.UTF8.GetString(r.Payload)).Distinct().Count());
    }
    [Fact]
    public async Task CustomDelimiterMessageAndCrLfAreNotNormalizedByTheNetworkBoundary()
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        using var connection = new MllpConnection(); await connection.ConnectAsync(new(Endpoint(listener.LocalEndpoint!.Port), IPAddress.Loopback), false, Token);
        var bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Message()).Replace('|', '*').Replace('^', '%').Replace("\r", "\r\n"));
        var result = await connection.SendAsync(bytes, true, false, Token); Assert.Equal(ExchangeOutcome.ApplicationAccepted, result.Outcome); Assert.Equal(bytes, Assert.Single(listener.Receipts).Payload);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task NetworkOversizeAndPartialFrameIdleTimeoutAreRecordedWithoutAck(bool timeout)
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, MaximumFrameBytes = 32, IdleTimeoutSeconds = 1 }, false);
        using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, listener.LocalEndpoint!.Port, Token);
        await client.GetStream().WriteAsync(timeout ? new byte[] { 11, 65 } : new byte[] { 11 }.Concat(Enumerable.Repeat((byte)65, 33)).ToArray(), Token);
        await WaitAsync(() => listener.Receipts.Count == 1); Assert.Empty(listener.Receipts[0].AckCodes); Assert.Contains(timeout ? "timeout" : "Oversized", listener.Receipts[0].Status);
        Assert.Empty(listener.Receipts[0].Payload);
        Assert.Equal(timeout ? 2 : 34, listener.Receipts[0].ConnectionSocketBytesRead);
        Assert.Equal(timeout ? 1 : 32, listener.Receipts[0].PartialPayloadBytes);
    }
    [Fact]
    public async Task CompletedConnectTokenCanBeCanceledAndDisposedWithoutCancelingTheExchange()
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        using var connection = new MllpConnection(); var connecting = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await connection.ConnectAsync(new(Endpoint(listener.LocalEndpoint!.Port), IPAddress.Loopback), false, connecting.Token);
        connecting.Cancel(); connecting.Dispose();
        var payload = Message(); var result = await connection.SendAsync(payload, true, false, Token);
        Assert.Equal(ExchangeOutcome.ApplicationAccepted, result.Outcome); Assert.True(result.WriteCompleted); Assert.True(result.ApplicationAccepted);
        var receipt = Assert.Single(listener.Receipts); Assert.Equal(payload, receipt.Payload); Assert.Equal(new[] { "AA" }, receipt.AckCodes);
        Assert.Equal("AA", Hl7TransportHeader.Read(payload, Encoding.UTF8).ReadAck(Assert.Single(result.Acknowledgments), Encoding.UTF8));
        Assert.False(connection.IsConnected); await WaitAsync(() => listener.Clients.Count == 0);
    }
    [Theory]
    [InlineData(new byte[] { 11 }, 0)]
    [InlineData(new byte[] { 11, 65, 28 }, 1)]
    public async Task IncompleteFrameTimeoutSeparatesSocketBytesFromCompletedPayload(byte[] wire, int partialPayload)
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, IdleTimeoutSeconds = 1 }, false);
        using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, listener.LocalEndpoint!.Port, Token);
        await client.GetStream().WriteAsync(wire, Token); await WaitAsync(() => listener.Receipts.Count == 1);
        var receipt = listener.Receipts[0]; Assert.Equal("Partial-frame timeout", receipt.Status);
        Assert.Equal(wire.Length, receipt.ConnectionSocketBytesRead); Assert.Equal(partialPayload, receipt.PartialPayloadBytes); Assert.Empty(receipt.Payload); Assert.Empty(receipt.AckCodes);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancellationOrListenerShutdownAfterWriteDoesNotEraseWriteCompletionOrAcceptMissingAck(bool cancel)
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = TestAckMode.None }, false);
        using var connection = new MllpConnection(); await connection.ConnectAsync(new(Endpoint(listener.LocalEndpoint!.Port) with { AckTimeoutSeconds = 10 }, IPAddress.Loopback), false, Token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var pending = connection.SendAsync(Message(), true, false, cancellation.Token);
        await WaitAsync(() => listener.Receipts.Count == 1);
        if (cancel) cancellation.Cancel(); else await listener.StopAsync();
        var result = await pending.WaitAsync(TimeSpan.FromSeconds(5), Token);
        Assert.True(result.WriteStarted); Assert.True(result.WriteCompleted); Assert.Empty(result.Acknowledgments); Assert.False(result.ApplicationAccepted);
        Assert.True(cancel ? result.Outcome == ExchangeOutcome.Canceled : result.Outcome is ExchangeOutcome.MissingAck or ExchangeOutcome.Disconnected);
        Assert.False(connection.IsConnected); Assert.Single(listener.Receipts);
    }
    [Fact]
    public async Task ExpiredCheckedSocketCannotBeReportedAsSuccessfulDelivery()
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, IdleTimeoutSeconds = 1 }, false);
        using var connection = new MllpConnection(); await connection.ConnectAsync(new(Endpoint(listener.LocalEndpoint!.Port), IPAddress.Loopback), false, Token);
        await WaitAsync(() => listener.Receipts.Count == 1);
        var idle = listener.Receipts[0]; Assert.Equal(0, idle.ConnectionSocketBytesRead); Assert.Equal(0, idle.PartialPayloadBytes); Assert.Empty(idle.Payload);
        var result = await connection.SendAsync(Message(), true, false, Token);
        Assert.False(result.ApplicationAccepted); Assert.Empty(result.Acknowledgments);
        Assert.True(result.WriteCompleted ? result.Outcome is ExchangeOutcome.MissingAck or ExchangeOutcome.Disconnected : result.Outcome == ExchangeOutcome.WriteFailed);
        Assert.Single(listener.Receipts); Assert.False(connection.IsConnected);
    }
    [Fact]
    public async Task SessionByteBudgetDoesNotAcknowledgeTheOverflowFrame()
    {
        var bytes = Message(); await using var listener = new MllpListener(); listener.Start(new() { Port = 0, MaximumFrameBytes = bytes.Length, MaximumSessionBytes = bytes.Length }, false);
        using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, listener.LocalEndpoint!.Port, Token);
        await client.GetStream().WriteAsync(MllpFrame.Encode(bytes), Token); await ReadAsync(client.GetStream(), Token);
        await client.GetStream().WriteAsync(MllpFrame.Encode(bytes), Token); await WaitAsync(() => listener.RejectedClients == 1); Assert.Single(listener.Receipts);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Token); timeout.CancelAfter(5000); Assert.Equal(0, await client.GetStream().ReadAsync(new byte[1], timeout.Token));
    }
    [Fact]
    public async Task SelectedEncodingRejectsInvalidInputAndRefusedPortDoesNotFallback()
    {
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, Encoding = TransportEncoding.Ascii }, false);
        using var client = new TcpClient(); await client.ConnectAsync(IPAddress.Loopback, listener.LocalEndpoint!.Port, Token);
        var bytes = Message().Concat(new byte[] { 0xE9 }).ToArray(); await client.GetStream().WriteAsync(MllpFrame.Encode(bytes), Token);
        await WaitAsync(() => listener.Receipts.Count == 1); Assert.False(listener.Receipts[0].ParseEligible); Assert.Empty(listener.Receipts[0].AckCodes); Assert.Equal(bytes, listener.Receipts[0].Payload);
        var portProbe = new TcpListener(IPAddress.Loopback, 0); portProbe.Start(); var port = ((IPEndPoint)portProbe.LocalEndpoint).Port; portProbe.Stop();
        using var connection = new MllpConnection(); var clock = System.Diagnostics.Stopwatch.StartNew();
        var failure = await Record.ExceptionAsync(() => connection.ConnectAsync(new(Endpoint(port), IPAddress.Loopback), false, Token));
        // Windows can report a closed port after its SYN retransmission delay;
        // our explicit one-second deadline may expire before SocketError.Refused.
        Assert.True(failure is SocketException or OperationCanceledException); Assert.True(clock.Elapsed < TimeSpan.FromSeconds(4));
        Assert.False(connection.IsConnected); Assert.Single(listener.Receipts);
    }
}
