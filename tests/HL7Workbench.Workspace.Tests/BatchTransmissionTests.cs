using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Transport;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class BatchTransmissionTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static BatchTransmissionOptions Fast(BatchAckPolicy policy = BatchAckPolicy.StopOnUnsuccessful) => new() { IntervalMilliseconds = 250, Policy = policy };
    private static ResolvedEndpoint Endpoint(int port) => new(TransportNetworkTests.Endpoint(port), IPAddress.Loopback);
    private static async Task<MessageDocument> Source(DocumentManager documents, int count = 3, string accept = "", string app = "", int payloadSize = 0)
    {
        using var stream = new MemoryStream();
        for (var n = 1; n <= count; n++)
        {
            var bytes = TransportNetworkTests.Message(accept, app, "SYN-BATCH-" + n);
            await stream.WriteAsync(bytes, Token);
            if (payloadSize > bytes.Length) await stream.WriteAsync(Encoding.UTF8.GetBytes("NTE|1||" + new string('X', payloadSize - bytes.Length - 8) + "\r"), Token);
        }
        stream.Position = 0; return await MessageFileReader.OpenFileAsync(documents, "synthetic.hl7", stream, Token);
    }
    private static async Task Prepare(BatchTransmissionSession batch, MessageDocument source, int port, BatchTransmissionOptions? options = null) =>
        await batch.PrepareAsync(source, Enumerable.Range(1, source.MessageCount).ToArray(), Endpoint(port), options ?? Fast(), Token);
    private static async Task Run(BatchTransmissionSession batch) => await batch.StartAsync(batch.Prepared!.Id, true, false, Token).WaitAsync(TimeSpan.FromSeconds(20), Token);
    [Fact]
    public void SelectionAndConfigurationAreBoundedWithoutTruncation()
    {
        Assert.Equal(1000, new BatchTransmissionOptions().IntervalMilliseconds); Assert.Equal(BatchAckPolicy.StopOnUnsuccessful, new BatchTransmissionOptions().Policy);
        Assert.Equal(new[] { 3, 4, 5 }, BatchTransmissionSession.SelectPositions(11, 3, 5));
        Assert.Equal(new[] { 1, 3, 11 }, BatchTransmissionSession.SelectPositions(11, positions: [11, 1, 3]));
        Assert.Equal(1000, BatchTransmissionSession.SelectPositions(1000).Length);
        Assert.Throws<ArgumentOutOfRangeException>(() => BatchTransmissionSession.SelectPositions(1001));
        Assert.Throws<ArgumentOutOfRangeException>(() => BatchTransmissionSession.SelectPositions(3, positions: [1, 1]));
        Assert.Throws<ArgumentOutOfRangeException>(() => BatchTransmissionSession.SelectPositions(3, positions: [0, 4]));
        Assert.Throws<ArgumentOutOfRangeException>(() => BatchTransmissionSession.SelectPositions(3, 3, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => (Fast() with { IntervalMilliseconds = 249 }).Validate());
        Assert.Throws<ArgumentOutOfRangeException>(() => (Fast() with { IntervalMilliseconds = 60001 }).Validate());
        (Fast() with { IntervalMilliseconds = 60000 }).Validate();
    }
    [Theory]
    [InlineData("edit")] [InlineData("close")] [InlineData("switch")]
    public async Task ImmutablePreparationRetainsOrderBytesAndRejectsStaleOrInactiveStart(string change)
    {
        using var documents = new DocumentManager(); var source = await Source(documents); await using var batch = new BatchTransmissionSession(documents);
        await batch.PrepareAsync(source, [3, 1], Endpoint(2575), Fast(), Token); var prepared = batch.Prepared!;
        Assert.Equal(new[] { 1, 3 }, prepared.Positions); var first = prepared.CopyPayload(0); first[0] = 0;
        Assert.Equal(TransportNetworkTests.Message(id: "SYN-BATCH-1"), prepared.CopyPayload(0));
        if (change == "edit") { source.IsEditMode = true; source.ReplaceWorkingText(Encoding.UTF8.GetString(TransportNetworkTests.Message(id: "SYN-CHANGED"))); await source.EditRefresh; }
        else if (change == "close") documents.Close(source.Id, true);
        else MessageFileReader.Open(documents, "other.hl7", Hl7Parser.Parse(TransportNetworkTests.Message()));
        Assert.False(prepared.IsActive(documents)); Assert.Equal(TransportNetworkTests.Message(id: "SYN-BATCH-1"), prepared.CopyPayload(0));
        await Assert.ThrowsAsync<InvalidOperationException>(() => batch.StartAsync(prepared.Id, true, false, Token)); Assert.Equal(0, batch.Progress.Attempted);
        Assert.Equal(2, batch.Progress.NotAttempted);
    }
    [Fact]
    public async Task ConfirmationsAreBoundToBatchIdentityAndExactDestinationAndTlsFailsClosed()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 1); await using var batch = new BatchTransmissionSession(documents);
        await Prepare(batch, source, 2575);
        await Assert.ThrowsAsync<InvalidOperationException>(() => batch.StartAsync(batch.Prepared!.Id, false, false, Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => batch.StartAsync(Guid.NewGuid(), true, false, Token));
        var external = new ResolvedEndpoint(TransportNetworkTests.Endpoint(2575) with { Host = "192.0.2.1", TestEndpoint = true }, IPAddress.Parse("192.0.2.1"));
        await batch.PrepareAsync(source, [1], external, Fast(), Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => batch.StartAsync(batch.Prepared!.Id, true, false, Token));
        Assert.Contains("OtherNetwork", batch.Prepared!.Review); Assert.Contains("may affect receiving systems", batch.Prepared.Review);
        await Assert.ThrowsAsync<NotSupportedException>(() => batch.PrepareAsync(source, [1], external with { Profile = external.Profile with { Security = TransportSecurity.TlsUnavailable } }, Fast(), Token));
        Assert.Equal(0, batch.Progress.Attempted);
    }
    [Theory]
    [InlineData("", "", TestAckMode.Positive, BatchMessageClassification.ApplicationAccepted, "AA")]
    [InlineData("", "", TestAckMode.ApplicationError, BatchMessageClassification.ApplicationError, "AE")]
    [InlineData("", "", TestAckMode.ApplicationReject, BatchMessageClassification.ApplicationRejected, "AR")]
    [InlineData("AL", "AL", TestAckMode.Positive, BatchMessageClassification.ApplicationAccepted, "CA,AA")]
    [InlineData("AL", "AL", TestAckMode.CommitError, BatchMessageClassification.CommitError, "CE")]
    [InlineData("AL", "AL", TestAckMode.CommitReject, BatchMessageClassification.CommitRejected, "CR")]
    [InlineData("AL", "NE", TestAckMode.Positive, BatchMessageClassification.CommitAcceptedApplicationUnknown, "CA")]
    [InlineData("NE", "NE", TestAckMode.Positive, BatchMessageClassification.ApplicationUnknown, "")]
    public async Task StopPolicyPreservesOriginalEnhancedPhasesAndRequiresAaForAdvancement(string accept, string app, TestAckMode mode, BatchMessageClassification classification, string codes)
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 2, accept, app);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = mode }, false);
        await using var batch = new BatchTransmissionSession(documents); await Prepare(batch, source, listener.LocalEndpoint!.Port); await Run(batch);
        var progress = batch.Progress; Assert.Equal(classification, progress.Results[0].Classification);
        Assert.Equal(codes, string.Join(',', progress.Results[0].Exchange!.Codes)); Assert.True(progress.Results[0].Exchange!.WriteCompleted);
        Assert.Equal(classification == BatchMessageClassification.ApplicationAccepted ? 2 : 1, progress.Attempted);
        // NE/NE has no reply barrier. Local write completion can precede the
        // listener's receipt publication; wait for that actual bounded observation.
        await TransportNetworkTests.WaitAsync(() => listener.Receipts.Count == progress.Attempted);
        Assert.Equal(progress.Attempted, listener.Receipts.Count); Assert.Equal(2 - progress.Attempted, progress.NotAttempted);
        Assert.Equal(classification == BatchMessageClassification.ApplicationAccepted ? 2 : 0, progress.Accepted);
        Assert.Equal(classification == BatchMessageClassification.ApplicationAccepted ? BatchTransmissionState.Completed : BatchTransmissionState.Stopped, progress.State);
        await TransportNetworkTests.WaitAsync(() => listener.Clients.Count == 0);
        if (progress.Results[0].RetainedAckFrames > 0)
        {
            var ack = await batch.OpenAckAsync(progress.Results[0], 0, Token); Assert.True(ack.IsDirty);
            Assert.Equal("SYN-BATCH-1", ack.Message.Get("MSA-2").Raw);
            await Prepare(batch, source, listener.LocalEndpoint!.Port);
            await Assert.ThrowsAsync<InvalidOperationException>(() => batch.OpenAckAsync(progress.Results[0], 0, Token));
        }
    }
    [Theory]
    [InlineData(TestAckMode.ApplicationReject)] [InlineData(TestAckMode.None)]
    public async Task ContinuePolicyKeepsUnsuccessfulResultsOrderedWithoutRetries(TestAckMode mode)
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 3);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = mode }, false);
        await using var batch = new BatchTransmissionSession(documents); await Prepare(batch, source, listener.LocalEndpoint!.Port, Fast(BatchAckPolicy.ContinueOnUnsuccessful)); await Run(batch);
        await TransportNetworkTests.WaitAsync(() => listener.Receipts.Count == 3);
        var p = batch.Progress; Assert.Equal(3, p.Attempted); Assert.Equal(0, p.Accepted); Assert.Equal(0, p.NotAttempted); Assert.Equal(BatchTransmissionState.Completed, p.State);
        Assert.Equal(new[] { 1, 2, 3 }, p.Results.Select(r => r.Position)); Assert.Equal(3, listener.Receipts.Count);
        Assert.Equal(mode == TestAckMode.None ? 3 : 0, p.TimedOut); Assert.Equal(mode == TestAckMode.None ? 3 : 0, p.Uncertain);
        Assert.Equal(mode == TestAckMode.ApplicationReject ? 3 : 0, p.RejectedError);
        for (var i = 1; i < 3; i++) Assert.True(p.Results[i].Started - p.Results[i - 1].Ended >= TimeSpan.FromMilliseconds(245));
        await Assert.ThrowsAsync<InvalidOperationException>(() => batch.StartAsync(batch.Prepared!.Id, true, false, Token));
    }
    [Theory]
    [InlineData(250)] [InlineData(1000)]
    public async Task ProductionLoopbackIsSequentialExactAndPacedEvenAcrossSlowExchange(int interval)
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 3);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        await using var batch = new BatchTransmissionSession(documents); await Prepare(batch, source, listener.LocalEndpoint!.Port, new() { IntervalMilliseconds = interval });
        Assert.Empty(listener.Receipts); var prepared = batch.Prepared!; await Run(batch); var p = batch.Progress;
        Assert.Equal(3, p.Accepted); Assert.True(p.AverageExchange > TimeSpan.Zero); Assert.Null(p.CurrentPosition);
        for (var n = 0; n < 3; n++)
        {
            Assert.Equal(prepared.CopyPayload(n), listener.Receipts[n].Payload); Assert.True(p.Results[n].Exchange!.ApplicationAccepted);
            if (n > 0) Assert.True(p.Results[n].Started - p.Results[n - 1].Ended >= TimeSpan.FromMilliseconds(interval - 5));
        }
        await TransportNetworkTests.WaitAsync(() => listener.Clients.Count == 0);
    }
    [Fact]
    public async Task PausePendingFinishesActiveAckThenPausesAndResumeNeverDuplicatesOrRetargets()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 2);
        var server = new TcpListener(IPAddress.Loopback, 0); server.Start();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token); deadline.CancelAfter(10000);
        var firstReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseAck = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new List<byte[]>();
        var peer = Task.Run(async () =>
        {
            for (var i = 0; i < 2; i++)
            {
                using var client = await server.AcceptTcpClientAsync(deadline.Token);
                var request = await TransportNetworkTests.ReadAsync(client.GetStream(), deadline.Token); received.Add(request);
                if (i == 0) { firstReceived.SetResult(); await releaseAck.Task.WaitAsync(deadline.Token); }
                var ack = Hl7TransportHeader.Read(request, Encoding.UTF8).CreateTestAcks(TestAckMode.Positive, Encoding.UTF8).Single();
                await client.GetStream().WriteAsync(MllpFrame.Encode(ack), deadline.Token);
                Assert.Equal(0, await client.GetStream().ReadAsync(new byte[1], deadline.Token));
            }
        }, deadline.Token);
        try
        {
            await using var batch = new BatchTransmissionSession(documents);
            await batch.PrepareAsync(source, [1, 2], Endpoint(((IPEndPoint)server.LocalEndpoint).Port) with { Profile = TransportNetworkTests.Endpoint(((IPEndPoint)server.LocalEndpoint).Port) with { AckTimeoutSeconds = 5 } }, Fast(), Token);
            var run = batch.StartAsync(batch.Prepared!.Id, true, false, Token); await firstReceived.Task.WaitAsync(deadline.Token);
            batch.Pause(); Assert.Equal(BatchTransmissionState.PausePending, batch.Progress.State); Assert.Equal(1, batch.Progress.Attempted); Assert.Equal(0, batch.Progress.Accepted);
            releaseAck.SetResult(); await TransportNetworkTests.WaitAsync(() => batch.Progress.State == BatchTransmissionState.Paused);
            Assert.Single(batch.Progress.Results, r => r.Attempted); Assert.False(server.Pending());
            Assert.Throws<InvalidOperationException>(() => batch.Resume(Guid.NewGuid()));
            await Assert.ThrowsAsync<InvalidOperationException>(() => batch.PrepareAsync(source, [1], Endpoint(2575), Fast(), Token));
            batch.Resume(batch.Prepared.Id); await run.WaitAsync(deadline.Token); await peer; Assert.Equal(2, received.Count); Assert.Equal(2, batch.Progress.Accepted);
        }
        finally { releaseAck.TrySetResult(); deadline.Cancel(); server.Stop(); }
    }
    [Fact]
    public async Task SourceEditsDuringRunCannotRetargetRemainingCapturedPayloads()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 2);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        await using var batch = new BatchTransmissionSession(documents); await Prepare(batch, source, listener.LocalEndpoint!.Port);
        var prepared = batch.Prepared!; var run = batch.StartAsync(prepared.Id, true, false, Token);
        await TransportNetworkTests.WaitAsync(() => batch.Progress.Accepted == 1); batch.Pause();
        source.IsEditMode = true; source.ReplaceWorkingText(Encoding.UTF8.GetString(TransportNetworkTests.Message(id: "SYN-EDIT"))); await source.EditRefresh;
        documents.Close(source.Id, true); Assert.False(prepared.IsCurrent(documents)); batch.Resume(prepared.Id); await run;
        Assert.Equal(prepared.CopyPayload(1), listener.Receipts[1].Payload); Assert.Equal(2, batch.Progress.Accepted);
    }
    [Theory]
    [InlineData("prepared")] [InlineData("token")]
    public async Task CancelBeforeFirstAttemptKeepsEveryMessageNotAttempted(string when)
    {
        using var documents = new DocumentManager(); var source = await Source(documents); await using var batch = new BatchTransmissionSession(documents); await Prepare(batch, source, 2575);
        if (when == "prepared") batch.Cancel();
        else { using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); await batch.StartAsync(batch.Prepared!.Id, true, false, cancellation.Token); }
        Assert.Equal(BatchTransmissionState.Canceled, batch.Progress.State); Assert.Equal(0, batch.Progress.Attempted); Assert.Equal(3, batch.Progress.NotAttempted); Assert.Equal(0, batch.Progress.Canceled);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancelOrDisposeDuringAckWaitStopsSchedulingAndPreservesUncertainty(bool dispose)
    {
        using var documents = new DocumentManager(); var source = await Source(documents);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = TestAckMode.None }, false);
        var batch = new BatchTransmissionSession(documents); await Prepare(batch, source, listener.LocalEndpoint!.Port);
        var run = batch.StartAsync(batch.Prepared!.Id, true, false, Token); await TransportNetworkTests.WaitAsync(() => listener.Receipts.Count == 1);
        if (dispose) await batch.DisposeAsync(); else batch.Cancel(); await run;
        await TransportNetworkTests.WaitAsync(() => listener.Clients.Count == 0); Assert.Single(listener.Receipts);
        if (!dispose)
        {
            var p = batch.Progress; Assert.Equal(1, p.Canceled); Assert.Equal(1, p.Uncertain); Assert.Equal(2, p.NotAttempted); Assert.Equal(0, p.Accepted);
            Assert.True(p.Results[0].Exchange!.WriteCompleted); Assert.Equal(ExchangeOutcome.Canceled, p.Results[0].Exchange!.Outcome);
        }
        else { Assert.Equal(BatchTransmissionState.Disposed, batch.Progress.State); Assert.Empty(batch.Progress.Results); Assert.Null(batch.Prepared); }
        await batch.DisposeAsync();
    }
    [Fact]
    public async Task CancelAfterFirstWireBytePreservesActualLocalWriteEvidenceAndNeverRetries()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 2, payloadSize: MllpFrame.MaximumPayloadBytes);
        var server = new TcpListener(IPAddress.Loopback, 0); server.Server.ReceiveBufferSize = 1024; server.Start();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(Token); deadline.CancelAfter(10000);
        try
        {
            await using var batch = new BatchTransmissionSession(documents); await Prepare(batch, source, ((IPEndPoint)server.LocalEndpoint).Port);
            var run = batch.StartAsync(batch.Prepared!.Id, true, false, Token);
            using var peer = await server.AcceptTcpClientAsync(deadline.Token); peer.ReceiveBufferSize = 1024;
            Assert.Equal(1, await peer.GetStream().ReadAsync(new byte[1], deadline.Token)); batch.Cancel(); await run.WaitAsync(deadline.Token);
            var p = batch.Progress; Assert.Equal(1, p.Canceled); Assert.Equal(1, p.Uncertain); Assert.Equal(1, p.NotAttempted); Assert.Equal(0, p.Accepted);
            var e = p.Results[0].Exchange!; Assert.True(e.WriteStarted);
            // Windows may buffer the complete frame before the peer's first-byte event.
            // Keep the actual local completion evidence; it is not application acceptance.
            Assert.Equal(e.WriteCompleted ? ExchangeFailureStage.Response : ExchangeFailureStage.Write, e.FailureStage);
            Assert.False(server.Pending());
        }
        finally { server.Stop(); }
    }
    [Fact]
    public async Task CancelAtControlledIncompleteWriteBoundaryRecordsUncertaintyAndSkipsRemainingMessages()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 2);
        var writing = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var attempts = 0;
        await using var batch = new BatchTransmissionSession(documents, async (_, _, _, token) =>
        {
            Interlocked.Increment(ref attempts); writing.TrySetResult();
            try { await Task.Delay(Timeout.Infinite, token); }
            catch (OperationCanceledException) { }
            return new(ExchangeOutcome.Canceled, true, [], [], TimeSpan.FromMilliseconds(1), false, ExchangeFailureStage.Write);
        });
        await Prepare(batch, source, 2575); var run = batch.StartAsync(batch.Prepared!.Id, true, false, Token);
        await writing.Task.WaitAsync(TimeSpan.FromSeconds(5), Token); batch.Cancel(); await run;
        var p = batch.Progress; Assert.Equal(1, attempts); Assert.Equal(1, p.Canceled); Assert.Equal(1, p.Uncertain); Assert.Equal(1, p.NotAttempted);
        Assert.False(p.Results[0].Exchange!.WriteCompleted); Assert.Equal(BatchMessageClassification.Canceled, p.Results[0].Classification); Assert.Equal(0, p.Accepted);
    }
    [Fact]
    public async Task FailedConnectionIsPacedAndContinueCanRecoverOnlyOnTheNextMessage()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 3);
        var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
        await using var listener = new MllpListener(); await using var batch = new BatchTransmissionSession(documents);
        await Prepare(batch, source, port, Fast(BatchAckPolicy.ContinueOnUnsuccessful)); var run = batch.StartAsync(batch.Prepared!.Id, true, false, Token);
        await TransportNetworkTests.WaitAsync(() => batch.Progress.Results[0].Attempted);
        listener.Start(new() { Port = port }, false); await run;
        var p = batch.Progress; Assert.False(p.Results[0].Exchange!.WriteStarted); Assert.False(p.Results[0].Exchange!.WriteCompleted); Assert.Equal(3, p.Attempted); Assert.Equal(2, p.Accepted);
        Assert.Equal(2, listener.Receipts.Count); Assert.Equal(TransportNetworkTests.Message(id: "SYN-BATCH-2"), listener.Receipts[0].Payload);
        Assert.True(p.Results[1].Started - p.Results[0].Ended >= TimeSpan.FromMilliseconds(245));
    }
    [Fact]
    public async Task ListenerShutdownDoesNotTurnACompleteWriteIntoApplicationAcceptance()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 3);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0, AckMode = TestAckMode.None }, false);
        await using var batch = new BatchTransmissionSession(documents); await Prepare(batch, source, listener.LocalEndpoint!.Port);
        var run = batch.StartAsync(batch.Prepared!.Id, true, false, Token); await TransportNetworkTests.WaitAsync(() => listener.Receipts.Count == 1); await listener.StopAsync(); await run;
        Assert.Equal(1, batch.Progress.Attempted); Assert.Equal(2, batch.Progress.NotAttempted); Assert.Equal(0, batch.Progress.Accepted); Assert.Equal(1, batch.Progress.Uncertain);
        Assert.True(batch.Progress.Results[0].Exchange!.WriteCompleted); Assert.Equal(BatchTransmissionState.Stopped, batch.Progress.State);
    }
    [Fact]
    public async Task SnapshotByteLimitRejectsTheWholePreparationWithoutTruncation()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 33, payloadSize: MllpFrame.MaximumPayloadBytes);
        await using var batch = new BatchTransmissionSession(documents);
        var error = await Assert.ThrowsAsync<Hl7LimitException>(() => Prepare(batch, source, 2575)); Assert.Equal("BatchPayloadBytes", error.Limit);
        Assert.Null(batch.Prepared); Assert.Empty(batch.Progress.Results); Assert.Equal(BatchTransmissionState.Faulted, batch.Progress.State);
    }
    [Fact]
    public async Task PrivacySafeAtomicCsvExcludesFreeTextIdentifiersAndPayloadsAndProtectsSources()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 2);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        await using var batch = new BatchTransmissionSession(documents);
        var endpoint = Endpoint(listener.LocalEndpoint!.Port) with { Profile = TransportNetworkTests.Endpoint(listener.LocalEndpoint.Port) with { Name = "=SYN-PRIVATE", Notes = "SYN-SECRET", Host = "localhost" } };
        await batch.PrepareAsync(source, [1, 2], endpoint, Fast(), Token); await Run(batch);
        var directory = Path.Combine(Path.GetTempPath(), "hl7-batch-report-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "report.csv"); await batch.ExportAsync(FileSaveDestination.NewFile(path), Token); var csv = await File.ReadAllTextAsync(path, Token);
            Assert.DoesNotContain("SYN-", csv); Assert.DoesNotContain("MSH|", csv); Assert.DoesNotContain("MSH-10", csv); Assert.DoesNotContain("MSA-2", csv); Assert.DoesNotContain("localhost", csv); Assert.DoesNotContain("synthetic.hl7", csv);
            Assert.Equal(4, csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length); Assert.Contains("\"Summary\"", csv); Assert.Contains("\"ApplicationAccepted\"", csv);
            Assert.All(csv.Split("\r\n", StringSplitOptions.RemoveEmptyEntries), line => Assert.Equal(34, line.Split(',').Length));
            Assert.DoesNotContain("SYN-", string.Join('\n', documents.Activity.Snapshot()));
            await Assert.ThrowsAsync<IOException>(() => batch.ExportAsync(FileSaveDestination.NewFile(path), Token)); Assert.Equal(csv, await File.ReadAllTextAsync(path, Token));
            using var canceled = new CancellationTokenSource(); canceled.Cancel(); var canceledPath = Path.Combine(directory, "canceled.csv");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => batch.ExportAsync(FileSaveDestination.NewFile(canceledPath), canceled.Token)); Assert.False(File.Exists(canceledPath));
            Assert.Single(Directory.GetFiles(directory));
            Assert.StartsWith("\"'", Encoding.UTF8.GetString(CsvRecordEncoder.Encode(["  =SUM(1,2)"])));
        }
        finally { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
    }
    [Fact]
    public async Task Stage11GeneratedHistoryUsesTheSamePreparationAndDoesNotChangeItsReportOrIdentity()
    {
        using var documents = new DocumentManager(); var file = Path.Combine(AppContext.BaseDirectory, "Fixtures", "fhir-r4", "patient-alpha.json");
        var data = await FhirPatientImport.FilesAsync([file], true, token: Token);
        var plan = FhirGenerationPlan.Create(data, new() { PatientKey = data.Patients[0].Key, Encounters = data.Encounters(data.Patients[0].Key).Select(e => e.Key).ToArray() }, Token);
        var output = await new FhirGenerationPublisher(documents).PublishAsync(plan, token: Token); var source = output.Document;
        var report = source.GenerationReport; var ids = source.Messages.Select(m => m.Id).ToArray();
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false); await using var batch = new BatchTransmissionSession(documents);
        await Prepare(batch, source, listener.LocalEndpoint!.Port); await Run(batch);
        Assert.Equal(source.MessageCount, batch.Progress.Accepted); Assert.Equal(ids, source.Messages.Select(m => m.Id)); Assert.Same(report, source.GenerationReport); Assert.True(source.IsDirty);
    }
    [Fact]
    public async Task AckRetentionIsBoundedWithoutLosingOutcomeCountsOrRepeatingMessages()
    {
        using var documents = new DocumentManager(); var source = await Source(documents, 18);
        await using var batch = new BatchTransmissionSession(documents, (_, _, _, _) =>
            Task.FromResult(new ExchangeResult(ExchangeOutcome.ApplicationAccepted, true, [new byte[MllpFrame.MaximumPayloadBytes]], ["AA"], TimeSpan.FromMilliseconds(1), true)));
        await Prepare(batch, source, 2575); await Run(batch); var p = batch.Progress;
        Assert.Equal(18, p.Accepted); Assert.Equal(18, p.Attempted); Assert.Equal(0, p.NotAttempted);
        Assert.Equal(16, p.Results.Sum(r => r.RetainedAckFrames)); Assert.Equal(18, p.Results.Sum(r => r.AckFrames));
        Assert.Equal("AA", p.Results[17].ApplicationStatus); Assert.Contains("not retained", p.Results[17].ToString());
        Assert.All(p.Results, r => Assert.Empty(r.Exchange!.Acknowledgments));
    }
    [Theory]
    [InlineData("NotAttempted", false)] [InlineData("CanceledWrite", true)] [InlineData("TimeoutResponse", true)]
    public void ClassificationNeverClaimsApplicationAcceptanceFromIncompleteOrUnobservedEvidence(string kind, bool wrote)
    {
        ExchangeResult? result = kind == "NotAttempted" ? null : new(kind == "CanceledWrite" ? ExchangeOutcome.Canceled : ExchangeOutcome.TimedOut,
            wrote, [], [], TimeSpan.Zero, WriteCompleted: kind == "TimeoutResponse");
        Assert.NotEqual(BatchMessageClassification.ApplicationAccepted, BatchMessageResult.Classify(result));
        Assert.False(result?.ApplicationAccepted ?? false);
    }
    [Fact]
    public async Task CsvCannotOverwriteAnyOpenSourceOrItsOriginalLocationAfterClosure()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hl7-batch-source-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "synthetic.hl7"); var payload = TransportNetworkTests.Message(); await File.WriteAllBytesAsync(path, payload, Token);
            using var documents = new DocumentManager();
            await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            var source = await MessageFileReader.OpenFileAsync(documents, "synthetic.hl7", input, Token, localPath: path);
            await using var batch = new BatchTransmissionSession(documents); await Prepare(batch, source, 2575); batch.Cancel();
            var destination = await FileSaveDestination.CaptureAsync(path, Token);
            await Assert.ThrowsAsync<IOException>(() => batch.ExportAsync(destination, Token));
            documents.Close(source.Id, true); await Assert.ThrowsAsync<IOException>(() => batch.ExportAsync(destination, Token));
            Assert.Equal(payload, await File.ReadAllBytesAsync(path, Token));
        }
        finally { foreach (var file in Directory.GetFiles(directory)) File.Delete(file); Directory.Delete(directory); }
    }
    [Fact]
    public async Task FramedCustomDelimiterAndCrLfPayloadsRemainExactInThePreparedBatch()
    {
        using var documents = new DocumentManager();
        var first = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(TransportNetworkTests.Message(id: "SYN-CUSTOM")).Replace('|', '*').Replace('^', '%').Replace("\r", "\r\n"));
        var second = TransportNetworkTests.Message(id: "SYN-ORDINARY");
        using var input = new MemoryStream(MllpFrame.Encode(first).Concat(MllpFrame.Encode(second)).ToArray());
        var source = await MessageFileReader.OpenFileAsync(documents, "synthetic.hl7", input, Token);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        await using var batch = new BatchTransmissionSession(documents); await Prepare(batch, source, listener.LocalEndpoint!.Port); await Run(batch);
        Assert.Equal(first, listener.Receipts[0].Payload); Assert.Equal(second, listener.Receipts[1].Payload); Assert.Equal(2, batch.Progress.Accepted);
    }
}
