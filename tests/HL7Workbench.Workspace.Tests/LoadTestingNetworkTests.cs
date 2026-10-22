using System.Net;
using System.Net.Sockets;
using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Transport;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class LoadTestingTests
{
    [Theory]
    [InlineData("", "", TestAckMode.Positive, ExchangeOutcome.ApplicationAccepted, "AA")]
    [InlineData("", "", TestAckMode.ApplicationError, ExchangeOutcome.ApplicationError, "AE")]
    [InlineData("", "", TestAckMode.ApplicationReject, ExchangeOutcome.ApplicationRejected, "AR")]
    [InlineData("AL", "AL", TestAckMode.Positive, ExchangeOutcome.ApplicationAccepted, "CA,AA")]
    [InlineData("AL", "AL", TestAckMode.CommitError, ExchangeOutcome.CommitError, "CE")]
    [InlineData("AL", "AL", TestAckMode.CommitReject, ExchangeOutcome.CommitRejected, "CR")]
    [InlineData("AL", "NE", TestAckMode.Positive, ExchangeOutcome.CommitAccepted, "CA")]
    [InlineData("NE", "NE", TestAckMode.Positive, ExchangeOutcome.SentWithoutAck, "")]
    [InlineData("", "", TestAckMode.None, ExchangeOutcome.TimedOut, "")]
    public async Task ProductionLoadUsesExactFramingAndOriginalEnhancedAckSemantics(string accept, string app, TestAckMode mode, ExchangeOutcome outcome, string codes)
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 1, accept, app);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = mode }, false);
        await using var session = new LoadTestingSession(documents); await Prepare(session, source, Fast(1), listener.LocalEndpoint!.Port); var capture = session.Prepared!;
        await Run(session); await TransportNetworkTests.WaitAsync(() => listener.Receipts.Count == 1 && listener.Clients.Count == 0);
        var p = session.Progress; var row = Assert.Single(p.Results); Assert.Equal(outcome, row.Result.Exchange!.Outcome); Assert.Equal(codes, string.Join(',', row.Result.Exchange.Codes));
        Assert.Equal(capture.CopyPayload(0), Assert.Single(listener.Receipts).Payload); Assert.Equal(1, p.Attempts); Assert.Equal(1, p.Connected); Assert.Equal(1, p.CompleteWrites);
        Assert.Equal(source.GetMessageId(1), row.SourceMessageId); Assert.Equal(1, p.ConnectionLatency.Count); Assert.Equal(1, p.WriteLatency.Count);
        Assert.Equal(codes.Length == 0 ? 0 : codes.Split(',').Length, p.AckFrames); Assert.Equal(codes.Length == 0 ? 0 : 1, p.AckLatency.Count);
        Assert.Equal(outcome == ExchangeOutcome.ApplicationAccepted ? 1 : 0, p.ApplicationAccepted); Assert.Empty(row.Result.Exchange.Acknowledgments);
        Assert.Equal(0, p.Active); Assert.Equal(1, p.Completed); Assert.Equal(LoadStopReason.AttemptLimit, p.StopReason);
    }
    [Fact]
    public async Task ConcurrentCorrelatedProductionExchangesCanCompleteOutOfOrderWithoutLosingSourceOrderOrIdentity()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 2);
        var server = new TcpListener(IPAddress.Loopback, 0); server.Start();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token); deadline.CancelAfter(10000);
        var releaseFirst = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var received = new byte[2][];
        var peer = Task.Run(async () =>
        {
            var workers = new List<Task>();
            for (var i = 0; i < 2; i++)
            {
                var client = await server.AcceptTcpClientAsync(deadline.Token);
                workers.Add(Task.Run(async () =>
                {
                    using (client)
                    {
                        var request = await TransportNetworkTests.ReadAsync(client.GetStream(), deadline.Token);
                        var sourceIndex = request.SequenceEqual(TransportNetworkTests.Message(id: "SYN-LOAD-1")) ? 0 : 1;
                        received[sourceIndex] = request;
                        if (sourceIndex == 0) await releaseFirst.Task.WaitAsync(deadline.Token);
                        var ack = Hl7TransportHeader.Read(request, Encoding.UTF8).CreateTestAcks(TestAckMode.Positive, Encoding.UTF8).Single();
                        await client.GetStream().WriteAsync(MllpFrame.Encode(ack), deadline.Token); Assert.Equal(0, await client.GetStream().ReadAsync(new byte[1], deadline.Token));
                    }
                }, deadline.Token));
            }
            await Task.WhenAll(workers);
        }, deadline.Token);
        try
        {
            await using var session = new LoadTestingSession(documents); await Prepare(session, source, Fast(2) with { Concurrency = 2 }, ((IPEndPoint)server.LocalEndpoint).Port);
            var capture = session.Prepared!; var run = Run(session); await TransportNetworkTests.WaitAsync(() => session.Progress.Completed == 1);
            Assert.Equal(2, Assert.Single(session.Progress.Results).SourcePosition); releaseFirst.SetResult(); await run; await peer;
            Assert.Equal(new[] { 2, 1 }, session.Progress.Results.Select(r => r.SourcePosition)); Assert.Equal(new[] { 1, 2 }, session.Progress.Results.Select(r => r.CompletionOrder));
            Assert.Equal(new[] { 2, 1 }, session.Progress.Results.Select(r => r.Attempt)); Assert.Equal(2, session.Progress.ApplicationAccepted);
            Assert.Equal(capture.CopyPayload(0), received[0]); Assert.Equal(capture.CopyPayload(1), received[1]); Assert.False(server.Pending());
        }
        finally { releaseFirst.TrySetResult(); deadline.Cancel(); server.Stop(); }
    }
    [Theory]
    [InlineData("mismatch", ExchangeOutcome.MismatchedAck)] [InlineData("missing", ExchangeOutcome.MissingAck)] [InlineData("fields", ExchangeOutcome.MissingAckFields)]
    public async Task MissingOrMismatchedProductionAcksAreNeverCountedAsApplicationAccepted(string kind, ExchangeOutcome expected)
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 1);
        var server = new TcpListener(IPAddress.Loopback, 0); server.Start(); using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token); deadline.CancelAfter(5000);
        var peer = Task.Run(async () =>
        {
            using var client = await server.AcceptTcpClientAsync(deadline.Token); var request = await TransportNetworkTests.ReadAsync(client.GetStream(), deadline.Token);
            if (kind == "missing") return;
            var ack = Hl7TransportHeader.Read(request, Encoding.UTF8).CreateTestAcks(TestAckMode.Positive, Encoding.UTF8).Single();
            var text = Encoding.UTF8.GetString(ack).Replace("MSA|AA|SYN-LOAD-1", "MSA|AA|" + (kind == "mismatch" ? "SYN-OTHER" : ""));
            await client.GetStream().WriteAsync(MllpFrame.Encode(Encoding.UTF8.GetBytes(text)), deadline.Token);
        }, deadline.Token);
        try
        {
            await using var session = new LoadTestingSession(documents); await Prepare(session, source, Fast(1), ((IPEndPoint)server.LocalEndpoint).Port); await Run(session); await peer;
            Assert.Equal(expected, Assert.Single(session.Progress.Results).Result.Exchange!.Outcome); Assert.Equal(0, session.Progress.ApplicationAccepted);
            Assert.Equal(1, session.Progress.MissingOrMismatched); Assert.Equal(1, session.Progress.Uncertain);
        }
        finally { deadline.Cancel(); server.Stop(); }
    }
    [Theory]
    [InlineData(1)] [InlineData(3)]
    public async Task ReplayUsesIdenticalPayloadsAndControlIdsWithoutAutomaticRewritingOrRetry(int concurrency)
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 2);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        await using var session = new LoadTestingSession(documents); await Prepare(session, source, Fast(5) with { Replay = true, Concurrency = concurrency }, listener.LocalEndpoint!.Port); var capture = session.Prepared!;
        await Run(session); Assert.Equal(5, listener.Receipts.Count); Assert.Equal(5, session.Progress.ApplicationAccepted);
        var ordered = session.Progress.Results.OrderBy(r => r.Attempt).ToArray();
        Assert.Equal(new[] { 1, 2, 1, 2, 1 }, ordered.Select(r => r.SourcePosition)); Assert.Equal(new[] { false, false, true, true, true }, ordered.Select(r => r.Replayed));
        if (concurrency == 1) for (var n = 0; n < 5; n++) Assert.Equal(capture.CopyPayload(n % 2), listener.Receipts[n].Payload);
        Assert.Equal(3, listener.Receipts.Count(r => r.Payload.SequenceEqual(capture.CopyPayload(0))));
        Assert.Equal(2, listener.Receipts.Count(r => r.Payload.SequenceEqual(capture.CopyPayload(1))));
        Assert.Equal(source.GetMessageId(1), ordered[4].SourceMessageId);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.StartAsync(capture.Id, true, false, Token)); Assert.Equal(5, listener.Receipts.Count);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task StopOrDisposeDuringRealAckWaitDrainsSocketsAndNeverSchedulesAnotherAttempt(bool dispose)
    {
        using var documents = new DocumentManager(); var source = await Source(documents);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = TestAckMode.None }, false);
        await using var session = new LoadTestingSession(documents); await Prepare(session, source, Fast(), listener.LocalEndpoint!.Port); var run = Run(session);
        await TransportNetworkTests.WaitAsync(() => listener.Receipts.Count == 1);
        if (dispose) await session.DisposeAsync(); else session.Cancel(); await run;
        await TransportNetworkTests.WaitAsync(() => listener.Clients.Count == 0); Assert.Single(listener.Receipts); Assert.Equal(0, session.Progress.Active);
        if (dispose) { Assert.Equal(LoadTestingState.Disposed, session.Progress.State); Assert.Null(session.Prepared); Assert.Empty(session.Progress.Results); }
        else { Assert.Equal(1, session.Progress.Canceled); Assert.Equal(1, session.Progress.Uncertain); Assert.Equal(1, session.Progress.CompleteWrites); Assert.Equal(0, session.Progress.ApplicationAccepted); }
    }
    [Fact]
    public async Task ControlledIncompleteWriteCancellationRetainsUncertaintyWithoutClaimingWriteCompletion()
    {
        using var documents = new DocumentManager(); var source = await Source(documents);
        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var session = new LoadTestingSession(documents, async (_, _, _, connected, token) =>
        {
            connected(TimeSpan.Zero); writing.TrySetResult(); try { await Task.Delay(Timeout.Infinite, token); } catch (OperationCanceledException) { }
            return new(ExchangeOutcome.Canceled, true, [], [], TimeSpan.Zero, false, ExchangeFailureStage.Write);
        });
        await Prepare(session, source); var run = Run(session); await writing.Task.WaitAsync(TimeSpan.FromSeconds(5), Token); session.Cancel(); await run;
        var p = session.Progress; Assert.Equal(1, p.Attempts); Assert.Equal(1, p.Uncertain); Assert.Equal(0, p.CompleteWrites); Assert.Equal(0, p.ApplicationAccepted); Assert.Equal(0, p.WriteLatency.Count);
    }
    [Fact]
    public async Task Stage11HistoryAndInFlightSourceChangesCannotAlterTheAuthorizedPayloadsOrGenerationReport()
    {
        using var documents = new DocumentManager(); var file = Path.Combine(AppContext.BaseDirectory, "Fixtures", "fhir-r4", "patient-alpha.json");
        var data = await FhirPatientImport.FilesAsync([file], true, token: Token); var plan = FhirGenerationPlan.Create(data, new() { PatientKey = data.Patients[0].Key, Scope = FhirGenerationScope.SelectedPatient }, Token);
        var output = await new FhirGenerationPublisher(documents).PublishAsync(plan, token: Token); var source = output.Document; var ids = source.Messages.Select(m => m.Id).ToArray();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var bytes = new List<byte[]>();
        await using var session = new LoadTestingSession(documents, async (_, payload, _, connected, token) => { connected(TimeSpan.Zero); bytes.Add(payload); entered.TrySetResult(); await release.Task.WaitAsync(token); return Accepted(); });
        await Prepare(session, source, Fast(10)); var capture = session.Prepared!; var run = Run(session); await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        source.IsEditMode = true; source.ReplaceWorkingText(source.EditableText! + "NTE|1||SYN-EDIT\r"); await source.EditRefresh; await Source(documents, 1); release.TrySetResult(); await run;
        Assert.Equal(source.MessageCount, bytes.Count); for (var n = 0; n < bytes.Count; n++) Assert.Equal(capture.CopyPayload(n), bytes[n]);
        Assert.Equal(ids, source.Messages.Select(m => m.Id)); Assert.Same(output.Report, source.GenerationReport); Assert.True(source.IsDirty); Assert.Equal(LoadStopReason.SourceExhausted, session.Progress.StopReason);
    }
    [Fact]
    public async Task CsvAndSummaryAreAtomicWhitelistedAndProtectSourcesEvenAfterClosure()
    {
        var root = Path.Combine(Path.GetTempPath(), "hl7-load-report-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "synthetic-source.hl7"); var payload = TransportNetworkTests.Message(id: "SYN-PRIVATE-CONTROL"); await File.WriteAllBytesAsync(path, payload, Token);
            using var documents = new DocumentManager(); using var input = new MemoryStream(payload);
            var source = await MessageFileReader.OpenFileAsync(documents, "SYN-PRIVATE-SOURCE.hl7", input, Token, localPath: path);
            var endpoint = Endpoint() with { Profile = Endpoint().Profile with { Name = "=SYN-PRIVATE", Host = "localhost", Notes = "SYN-PRIVATE-NOTES" } };
            await using var session = new LoadTestingSession(documents, (_, _, _, connected, _) => { connected(TimeSpan.FromMilliseconds(1)); return Task.FromResult(Accepted()); });
            await session.PrepareAsync(source, [1], endpoint, Fast(1), Token); await Run(session);
            var csv = Path.Combine(root, "report.csv"); var summary = Path.Combine(root, "summary.txt"); await session.ExportAsync(FileSaveDestination.NewFile(csv), token: Token); await session.ExportAsync(FileSaveDestination.NewFile(summary), true, Token);
            var text = await File.ReadAllTextAsync(csv, Token); var report = await File.ReadAllTextAsync(summary, Token);
            foreach (var value in new[] { "SYN-", "localhost", "127.0.0.1", "MSH|", "MSA-2", "MSH-10", root }) { Assert.DoesNotContain(value, text); Assert.DoesNotContain(value, report); }
            Assert.Contains("\"Summary\"", text); Assert.Contains("CompleteWrites", text); Assert.Contains("OmittedResults", text); Assert.Contains("p99", report); Assert.Contains("not application acceptance", report);
            var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries); Assert.Equal(3, lines.Length); Assert.All(lines, line => Assert.Equal(lines[0].Split(',').Length, line.Split(',').Length));
            await Assert.ThrowsAsync<IOException>(() => session.ExportAsync(FileSaveDestination.NewFile(csv), token: Token)); Assert.Equal(text, await File.ReadAllTextAsync(csv, Token));
            var original = await FileSaveDestination.CaptureAsync(path, Token); documents.Close(source.Id, true); await Assert.ThrowsAsync<IOException>(() => session.ExportAsync(original, token: Token)); Assert.Equal(payload, await File.ReadAllBytesAsync(path, Token));
            using var canceled = new CancellationTokenSource(); canceled.Cancel(); var absent = Path.Combine(root, "canceled.csv"); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ExportAsync(FileSaveDestination.NewFile(absent), token: canceled.Token)); Assert.False(File.Exists(absent));
            Assert.StartsWith("\"'", Encoding.UTF8.GetString(CsvRecordEncoder.Encode(["  =SYNTHETIC()"])), StringComparison.Ordinal); Assert.DoesNotContain("SYN-", string.Join('\n', documents.Activity.Snapshot()));
            Assert.Equal(3, Directory.GetFiles(root).Length);
        }
        finally { foreach (var file in Directory.GetFiles(root)) File.Delete(file); Directory.Delete(root); }
    }
}
