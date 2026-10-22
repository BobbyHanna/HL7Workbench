using System.Net;
using System.Text;
using System.Text.Json;
using HL7Workbench.Core;
using HL7Workbench.Transport;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class TransportWorkspaceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public async Task CaptureSelectsExactlyOneOriginalPayloadWithoutOuterFramingOrPreviewNormalization()
    {
        using var documents = new DocumentManager(); var a = TransportNetworkTests.Message(id: "SYN-A"); var b = TransportNetworkTests.Message(id: "SYN-B");
        using var source = new MemoryStream(MllpFrame.Encode(a).Concat(MllpFrame.Encode(b)).ToArray());
        var document = await MessageFileReader.OpenFileAsync(documents, "synthetic.hl7", source, Token); Assert.Equal(2, document.MessageCount);
        var capture = await document.CaptureTransportMessageAsync(2, TransportEncoding.Utf8, Token);
        Assert.Equal(b, capture.CopyPayload()); Assert.Equal(2, capture.MessageNumber); Assert.Equal(document.GetMessageId(2), capture.MessageId); Assert.Equal(1, document.MessageNumber); Assert.False(capture.Transcoded);
        Assert.True(capture.IsCurrent(documents)); documents.Close(document.Id); Assert.False(capture.IsCurrent(documents));
    }
    [Fact]
    public async Task EditModeOffRetainsWorkingTextAndReviewBecomesStaleAfterAnotherEdit()
    {
        using var documents = new DocumentManager(); var original = Encoding.UTF8.GetString(TransportNetworkTests.Message());
        var document = MessageFileReader.Open(documents, "synthetic.hl7", Hl7Parser.Parse(original)); document.IsEditMode = true;
        var changed = original + "NTE|1||SYN-EDITED\r"; document.ReplaceWorkingText(changed); document.IsEditMode = false;
        var capture = await document.CaptureTransportMessageAsync(1, TransportEncoding.Utf8, Token); Assert.Equal(changed, Encoding.UTF8.GetString(capture.CopyPayload()));
        Assert.True(capture.IsCurrent(documents)); document.IsEditMode = true; document.ReplaceWorkingText(changed + "NTE|2||SYN-NEXT\r"); Assert.False(capture.IsCurrent(documents));
        await document.EditRefresh;
    }
    [Fact]
    public async Task ExplicitUtf16ToUtf8TranscodePreservesCharactersAndTerminatorsWithoutChangingDocument()
    {
        using var documents = new DocumentManager(); var text = Encoding.UTF8.GetString(TransportNetworkTests.Message()) + "NTE|1||SYN-é\r\n";
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(text)).ToArray(); using var source = new MemoryStream(bytes);
        var document = await MessageFileReader.OpenFileAsync(documents, "synthetic.hl7", source, Token);
        var capture = await document.CaptureTransportMessageAsync(1, TransportEncoding.Utf8, Token); Assert.True(capture.Transcoded); Assert.Equal(Encoding.UTF8.GetBytes(text), capture.CopyPayload()); Assert.False(document.IsDirty);
        await Assert.ThrowsAsync<EncoderFallbackException>(() => document.CaptureTransportMessageAsync(1, TransportEncoding.Ascii, Token)); Assert.False(document.IsDirty);
    }
    [Fact]
    public async Task UnmodifiedUtf8BomAndCustomDelimitersRemainExact()
    {
        using var documents = new DocumentManager(); var bytes = Encoding.UTF8.GetPreamble().Concat(TransportNetworkTests.Message()).ToArray();
        var document = MessageFileReader.Open(documents, "synthetic.hl7", Hl7Parser.Parse(bytes));
        Assert.Equal(bytes, (await document.CaptureTransportMessageAsync(1, TransportEncoding.Utf8, Token)).CopyPayload());
    }
    [Fact]
    public async Task IndependentGeneratedDocumentUsesSameCaptureAndReceivedDocumentsAreUnsavedAndLossless()
    {
        using var documents = new DocumentManager(); var bytes = TransportNetworkTests.Message();
        var document = await TransportSession.OpenLocalPayloadAsync(documents, bytes, TransportEncoding.Utf8, "Received MLLP.hl7", Token);
        Assert.True(document.IsDirty); Assert.Null(document.SavePath); Assert.Equal(bytes, Hl7Serializer.Serialize(document.Message));
        Assert.Equal(bytes, (await document.CaptureTransportMessageAsync(1, TransportEncoding.Utf8, Token)).CopyPayload());
    }
    [Fact]
    public async Task CanceledPayloadInspectionDoesNotPublishAPartialDocument()
    {
        using var documents = new DocumentManager(); using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TransportSession.OpenLocalPayloadAsync(documents,
            TransportNetworkTests.Message(), TransportEncoding.Utf8, "Received MLLP.hl7", canceled.Token));
        Assert.Empty(documents.Documents); Assert.Null(documents.ActiveDocument);
    }
    [Fact]
    public async Task Stage11GeneratedCombinedDocumentSendsOnlyTheChosenMessageWithoutChangingReportOrIdentity()
    {
        using var documents = new DocumentManager(); var file = Path.Combine(AppContext.BaseDirectory, "Fixtures", "fhir-r4", "patient-alpha.json");
        var data = await FhirPatientImport.FilesAsync([file], true, token: Token);
        var plan = FhirGenerationPlan.Create(data, new() { PatientKey = data.Patients[0].Key, Encounters = data.Encounters(data.Patients[0].Key).Select(e => e.Key).ToArray() }, Token);
        var output = await new FhirGenerationPublisher(documents).PublishAsync(plan, token: Token); var doc = output.Document; Assert.True(doc.MessageCount > 1);
        var ids = doc.Messages.Select(m => m.Id).ToArray(); var report = doc.GenerationReport; var capture = await doc.CaptureTransportMessageAsync(2, TransportEncoding.Utf8, Token);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        using var session = new TransportSession(documents); await session.ConnectAsync(new(TransportNetworkTests.Endpoint(listener.LocalEndpoint!.Port), IPAddress.Loopback), false, Token);
        await session.SendAsync(capture, true, false, Token); Assert.Equal("ApplicationAccepted", Assert.Single(session.History).Status);
        Assert.Equal(capture.CopyPayload(), Assert.Single(listener.Receipts).Payload); Assert.Equal(ids, doc.Messages.Select(m => m.Id)); Assert.Same(report, doc.GenerationReport); Assert.True(doc.IsDirty);
    }
    [Fact]
    public async Task StaleReviewAndAbsentConfirmationNeverWriteAndActiveDocumentScopeIsRetained()
    {
        using var documents = new DocumentManager(); var document = MessageFileReader.Open(documents, "synthetic.hl7", Hl7Parser.Parse(TransportNetworkTests.Message()));
        var capture = await document.CaptureTransportMessageAsync(1, TransportEncoding.Utf8, Token);
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        using var session = new TransportSession(documents); await session.ConnectAsync(new(TransportNetworkTests.Endpoint(listener.LocalEndpoint!.Port), IPAddress.Loopback), false, Token);
        await session.SendAsync(capture, false, false, Token); Assert.Empty(listener.Receipts); Assert.Empty(session.History);
        document.IsEditMode = true; document.ReplaceWorkingText(Encoding.UTF8.GetString(TransportNetworkTests.Message()) + "NTE|SYN-EDIT\r"); await document.EditRefresh;
        await session.SendAsync(capture, true, false, Token); Assert.Empty(listener.Receipts); Assert.Empty(session.History); Assert.Contains("changed", session.Status);
    }
    [Fact]
    public async Task SessionStoresInspectableAckAndDocumentIdentityWithoutPayloadInActivityLog()
    {
        using var documents = new DocumentManager(); var document = MessageFileReader.Open(documents, "synthetic.hl7", Hl7Parser.Parse(TransportNetworkTests.Message()));
        await using var listener = new MllpListener(); listener.Start(new() { Port = 0 }, false);
        using var session = new TransportSession(documents); await session.ConnectAsync(new(TransportNetworkTests.Endpoint(listener.LocalEndpoint!.Port), IPAddress.Loopback), false, Token);
        var capture = await document.CaptureTransportMessageAsync(1, TransportEncoding.Utf8, Token); await session.SendAsync(capture, true, false, Token);
        var row = Assert.Single(session.History); Assert.Equal(document.Id, row.DocumentId); Assert.Equal(capture.MessageId, row.MessageId); Assert.Equal("AA", row.Codes);
        Assert.Contains($"Document {document.ActivityNumber} / message 1", row.ToString());
        var ack = await session.OpenPayloadAsync(row, 0, Token); Assert.True(ack.IsDirty); Assert.Equal(row.Acknowledgments[0], Hl7Serializer.Serialize(ack.Message));
        var log = string.Join("\n", documents.Activity.Snapshot()); Assert.DoesNotContain("SYN-TRANSPORT", log); Assert.DoesNotContain("MSH|", log); Assert.DoesNotContain("127.0.0.1", log);
        session.ClearHistory(); Assert.Empty(session.History); Assert.Equal(2, documents.Documents.Count);
    }
    [Theory]
    [InlineData("127.0.0.1", AddressScope.Loopback)] [InlineData("::1", AddressScope.Loopback)] [InlineData("::ffff:127.0.0.1", AddressScope.Loopback)]
    [InlineData("10.1.2.3", AddressScope.PrivateNetwork)] [InlineData("172.16.0.1", AddressScope.PrivateNetwork)] [InlineData("192.168.0.2", AddressScope.PrivateNetwork)]
    [InlineData("fc00::1", AddressScope.PrivateNetwork)] [InlineData("192.0.2.1", AddressScope.OtherNetwork)] [InlineData("172.32.0.1", AddressScope.OtherNetwork)]
    public void EndpointClassificationNeverClaimsNonproduction(string address, AddressScope expected) => Assert.Equal(expected, ResolvedEndpoint.Classify(IPAddress.Parse(address)));
    [Fact]
    public void ProfilesRoundTripOnlyWhitelistedMetadataAndTlsCannotFallback()
    {
        var root = Path.Combine(Path.GetTempPath(), "hl7-transport-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "endpoints.json"); var library = new TransportProfiles(path); Assert.Empty(library.Load());
            var profile = new TransportEndpoint { Name = "Synthetic test", Host = "localhost", Notes = "Loopback smoke", TestEndpoint = true, Encoding = TransportEncoding.Ascii, Security = TransportSecurity.TlsUnavailable };
            library.Save([profile]); Assert.Equal(profile, Assert.Single(library.Load())); var text = File.ReadAllText(path);
            Assert.DoesNotContain("Payload", text); Assert.DoesNotContain("Password", text); Assert.DoesNotContain("PrivateKey", text); Assert.DoesNotContain("Connected", text);
            File.WriteAllText(path, text.Replace("\"Version\": 1", "\"Password\": \"SYN-SECRET\", \"Version\": 1")); Assert.Throws<JsonException>(() => library.Load());
            Assert.Empty(Directory.GetFiles(root, "*.tmp"));
        }
        finally { File.Delete(Path.Combine(root, "endpoints.json")); Directory.Delete(root); }
    }
    [Theory]
    [InlineData("{\"Version\":2,\"Profiles\":[]}")]
    [InlineData("{\"Version\":1,\"Profiles\":null}")]
    public void InvalidProfileFilesAreRejectedWithoutModification(string json)
    {
        var path = Path.Combine(Path.GetTempPath(), "hl7-transport-profile-" + Guid.NewGuid().ToString("N") + ".json"); File.WriteAllText(path, json);
        try { Assert.Throws<InvalidDataException>(() => new TransportProfiles(path).Load()); Assert.Equal(json, File.ReadAllText(path)); }
        finally { File.Delete(path); }
    }
}
