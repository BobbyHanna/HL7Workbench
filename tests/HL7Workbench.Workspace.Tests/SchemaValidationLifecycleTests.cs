using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class SchemaValidationLifecycleTests : IAsyncDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private readonly DocumentManager documents = new();
    private static string Message(string name = "SYN-1") => SyntheticMessageGenerator.Generate(new() { DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic }, Token).Messages[0].Replace("SYN2-0000002A-0001", name);
    private MessageDocument Open(string text) => MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse(text));
    private static async Task WaitForValidation(MessageDocument document)
    { await document.EditRefresh; await document.ValidationRefresh; }
    public async ValueTask DisposeAsync()
    { var all = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(all.Select(d => d.Disposal)); }

    [Fact]
    public async Task OpeningPreferencesAndEditingDoNotValidateBeforeExplicitApplication()
    {
        documents.Preferences = new(SchemaMode: SchemaInterpretationMode.SelectedTarget, TargetSchema: "hl7.v251.ADT.A01");
        var doc = Open(Message()); Assert.Equal(SchemaValidationState.NotApplied, doc.ValidationState); Assert.Null(doc.AppliedSchemaId); Assert.Null(doc.SchemaValidation);
        doc.IsEditMode = true; doc.ReplaceWorkingText(Message("SYN-2")); await WaitForValidation(doc);
        Assert.Equal(SchemaValidationState.NotApplied, doc.ValidationState); Assert.Null(doc.SchemaValidation);
        var text = doc.EditableText; var dirty = doc.IsDirty; var undo = doc.CanUndo;
        await doc.ApplySchemaAsync("hl7.v251.ADT.A01"); Assert.Equal("hl7.v251.ADT.A01", doc.AppliedSchemaId); Assert.Equal(SchemaValidationState.Incomplete, doc.ValidationState);
        Assert.NotNull(doc.SchemaValidation); Assert.Equal(text, doc.EditableText); Assert.Equal(dirty, doc.IsDirty); Assert.Equal(undo, doc.CanUndo);
        doc.RemoveSchema(); Assert.Null(doc.SchemaValidation); Assert.Equal(SchemaValidationState.NotApplied, doc.ValidationState);
    }

    [Fact]
    public async Task AppliedScopeFollowsStableMessageIdentityAndDoesNotLeakAcrossDocumentsOrMessages()
    {
        var doc = Open(Message()); doc.InsertMessage(MessageInsertionPosition.End, Message("SYN-2")); await doc.EditRefresh;
        await doc.NavigateAsync(1, Token); var firstId = doc.ActiveMessageId; await doc.ApplySchemaAsync("hl7.v251.ADT.A01");
        await doc.NavigateAsync(2, Token); Assert.Equal(SchemaValidationState.NotApplied, doc.ValidationState); Assert.Null(doc.SchemaValidation);
        await doc.NavigateAsync(1, Token); await doc.ValidationRefresh; Assert.Equal(firstId, doc.ActiveMessageId); Assert.NotNull(doc.SchemaValidation);
        var other = Open(Message("SYN-3")); Assert.Equal(SchemaValidationState.NotApplied, other.ValidationState);
        documents.Activate(doc.Id); Assert.NotNull(doc.SchemaValidation); Assert.Null(other.SchemaValidation);
    }

    [Fact]
    public async Task EditDebounceSupersedesOldResultsUndoRedoRevalidateAndRemovingSchemaStopsRefresh()
    {
        var entry = documents.ValidationProfiles.Import(Hl7ProfileImporter.ExportJson(ValidationProfileLibraryTests.Profile()), false, "Synthetic.json", token: Token);
        var doc = Open(Message()); await doc.ApplySchemaAsync(entry.Profile.Id); Assert.Equal(0, doc.SchemaValidation!.Errors);
        doc.IsEditMode = true; var original = doc.EditableText!;
        doc.ReplaceWorkingText(original.Replace("PV1|1|I", "PV1|1|X")); Assert.Equal(SchemaValidationState.Stale, doc.ValidationState); Assert.Null(doc.SchemaValidation);
        doc.ReplaceWorkingText(original.Replace("PV1|1|I", "PV1|1|O")); await WaitForValidation(doc);
        Assert.Contains(doc.SchemaValidation!.Diagnostics, d => d.RuleId == "Value.Allowed" && d.Path == "PV1[1]-2[1]");
        doc.Undo(); await WaitForValidation(doc); doc.Undo(); await WaitForValidation(doc); Assert.Equal(0, doc.SchemaValidation!.Errors);
        doc.Redo(); await WaitForValidation(doc); Assert.True(doc.SchemaValidation!.Errors > 0);
        doc.RemoveSchema(); doc.ReplaceWorkingText(original); await WaitForValidation(doc); Assert.Null(doc.SchemaValidation); Assert.Equal(SchemaValidationState.NotApplied, doc.ValidationState);
    }

    [Fact]
    public async Task ProfileRemovalAndReimportRequireExplicitResolutionWithoutStaleHighlights()
    {
        var library = documents.ValidationProfiles; var entry = library.Import(Hl7ProfileImporter.ExportJson(ValidationProfileLibraryTests.Profile()), false, "Synthetic.json", token: Token);
        var doc = Open(Message()); await doc.ApplySchemaAsync(entry.Profile.Id);
        var operation = doc.ValidationRefresh; library.Import(Hl7ProfileImporter.ExportJson(ValidationProfileLibraryTests.Profile("unrelated")), false, "other.json", token: Token);
        Assert.Same(operation, doc.ValidationRefresh);
        library.Remove(entry.Profile.Id, entry.Revision); Assert.Equal(SchemaValidationState.Unavailable, doc.ValidationState); Assert.Null(doc.SchemaValidation);
        entry = library.Import(Hl7ProfileImporter.ExportJson(entry.Profile), false, "Synthetic.json", token: Token);
        // Reimport may have the same rules, but is a new reviewed library revision.
        Assert.Equal(SchemaValidationState.Unavailable, doc.ValidationState); await doc.ApplySchemaAsync(entry.Profile.Id); Assert.NotNull(doc.SchemaValidation);
    }

    [Fact]
    public async Task ClosingWhileValidationIsPendingDrainsWorkAndSuppressesPublication()
    {
        var doc = Open(Message()); var pending = doc.ApplySchemaAsync("hl7.v251.ADT.A01");
        documents.Close(doc.Id, true); await pending; await doc.Disposal; Assert.Null(doc.SchemaValidation); Assert.Empty(documents.Documents);
    }

    [Fact]
    public async Task RapidReapplicationAndCloseSafelySnapshotConcurrentCompletionDrains()
    {
        // Canceled predecessors can complete on pool threads while Close captures
        // the remaining work. Keep disposal and source-release assertions intact.
        for (var iteration = 0; iteration < 32; iteration++)
        {
            var doc = Open(Message());
            var pending = Enumerable.Range(0, 12).Select(_ => doc.ApplySchemaAsync("hl7.v251.ADT.A01")).ToArray();
            documents.Close(doc.Id, true); await Task.WhenAll(pending); await doc.Disposal;
            Assert.Null(doc.SchemaValidation); Assert.Empty(documents.Documents);
        }
    }

    [Fact]
    public async Task SourceBackedHugeValuesRemainBoundedAndOnlyTheSelectedMessageIsValidated()
    {
        var first = SyntheticMessageGenerator.Generate(new() { MessageType = SyntheticMessageType.OruR01, DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic }, Token).Messages[0];
        var parsed = Hl7Parser.Parse(first); var field = parsed.Segments.First(s => s.Identifier == "OBX").GetField(5)!;
        // Construct a source-backed fixture beyond materialized edit limits without
        // changing those limits or teaching the test another HL7 grammar.
        var large = first.Remove(field.CharacterOffset, field.Raw.Length).Insert(field.CharacterOffset, new string('A', 2 * 1024 * 1024));
        var inputBytes = Encoding.UTF8.GetBytes(large + Message("SYN-SECOND")); using var input = new MemoryStream(inputBytes);
        var doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic large", input, Token); Assert.NotNull(doc.Source); Assert.NotNull(doc.Presentation); Assert.Null(doc.MaterializedMessage);
        var source = doc.Source; var dirty = doc.IsDirty; var detected = await doc.DetectSchemasAsync(Token); Assert.Equal("hl7.v251.ORU.R01", Assert.Single(detected).Id);
        await doc.ApplySchemaAsync("hl7.v251.ORU.R01"); Assert.Same(source, doc.Source); Assert.Null(doc.MaterializedMessage); Assert.Equal(dirty, doc.IsDirty);
        Assert.Equal(SchemaValidationState.Incomplete, doc.ValidationState); Assert.Contains(doc.SchemaValidation!.Diagnostics, d => d.RuleId == "Coverage.Incomplete" && d.Path == "OBX[1]-5[1]");
        await doc.NavigateAsync(2, Token); Assert.Null(doc.SchemaValidation); Assert.Equal(SchemaValidationState.NotApplied, doc.ValidationState);
        await doc.NavigateAsync(1, Token); await doc.ValidationRefresh; Assert.NotNull(doc.SchemaValidation);
    }

    [Fact]
    public async Task SaveAndSaveAsPreserveAppliedSelectionAndDoNotRevalidateUnchangedContent()
    {
        var root = Path.Combine(Path.GetTempPath(), "hl7-schema-save-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var doc = Open(Message()); await doc.ApplySchemaAsync("hl7.v251.ADT.A01"); var result = doc.SchemaValidation; var operation = doc.ValidationRefresh;
            var path = Path.Combine(root, "Synthetic.hl7"); await doc.SaveAsync(path, Token);
            Assert.Same(result, doc.SchemaValidation); Assert.Same(operation, doc.ValidationRefresh); Assert.Equal(Encoding.UTF8.GetBytes(Message()), await File.ReadAllBytesAsync(path, Token));
            await doc.SaveAsync(Path.Combine(root, "Synthetic-copy.hl7"), Token); Assert.Same(result, doc.SchemaValidation); Assert.Equal("hl7.v251.ADT.A01", doc.AppliedSchemaId);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public async Task SupersededExplicitApplicationAndNavigationNeverPublishAnObsoleteProfileResult()
    {
        var doc = Open(Message()); var first = doc.ApplySchemaAsync("hl7.v251.ADT.A03"); var second = doc.ApplySchemaAsync("hl7.v251.ADT.A01"); await Task.WhenAll(first, second);
        Assert.Equal("hl7.v251.ADT.A01", doc.SchemaValidation!.ProfileId); Assert.DoesNotContain(doc.SchemaValidation.Diagnostics, d => d.RuleId == "Profile.Incompatible");
        doc.InsertMessage(MessageInsertionPosition.End, Message("SYN-SECOND")); await doc.EditRefresh; await doc.NavigateAsync(1, Token);
        var pending = doc.ApplySchemaAsync("hl7.v251.ADT.A01"); await doc.NavigateAsync(2, Token); await pending;
        Assert.Null(doc.SchemaValidation); Assert.Equal(SchemaValidationState.NotApplied, doc.ValidationState);
    }

    [Fact]
    public async Task ValidationRetainsOriginalEscapeEncodingAndUnencodableDraftsRemainExplicitlyUnavailable()
    {
        foreach (var encoding in new[] { Encoding.Latin1, Encoding.Unicode })
        {
            var hex = encoding.CodePage == Encoding.Latin1.CodePage ? "\\XE9\\" : "\\XE900\\";
            var profile = new Hl7ValidationProfile { Id = "synthetic.encoding." + encoding.CodePage, Name = "Synthetic encoding check", MessageType = "ADT", TriggerEvent = "A01", MessageStructure = "ADT_A01",
                Structure = [new() { Key = "MSH", Segment = "MSH", Min = 1 }, new() { Key = "PID", Segment = "PID", Min = 1 }],
                Segments = [new() { Id = "MSH", AllowExtraFields = true }, new() { Id = "PID", AllowExtraFields = true, Fields = [new() { Position = 3, Min = 1, AllowedValues = ["\u00e9"] }] }] };
            documents.ValidationProfiles.Import(Hl7ProfileImporter.ExportJson(profile), false, "Encoding.json", token: Token);
            var text = "MSH|^~\\&|SYN||||20300101000000+0000||ADT^A01^ADT_A01|SYN|T|2.5.1\rPID|||" + hex + "\r";
            var original = encoding.GetBytes(text); var doc = MessageFileReader.Open(documents, "Synthetic encoding", Hl7Parser.Parse(original, new() { Encoding = encoding }));
            await doc.ApplySchemaAsync(profile.Id); Assert.Equal(SchemaValidationState.Valid, doc.ValidationState); Assert.Empty(doc.SchemaValidation!.Diagnostics);
            Assert.Equal(original, Hl7Serializer.Serialize(doc.MaterializedMessage!));
            if (encoding.CodePage == Encoding.Latin1.CodePage)
            {
                doc.IsEditMode = true; doc.ReplaceWorkingText(text + "NTE|1||\u6f22\r"); await doc.EditRefresh; await doc.ValidationRefresh;
                Assert.Equal(SchemaValidationState.Unavailable, doc.ValidationState); Assert.Null(doc.SchemaValidation); Assert.Contains("\u6f22", doc.WorkingText);
            }
        }
    }
}
