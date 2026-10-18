using HL7Workbench.Scripting;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class StructuralCompatibilityTests
{
    private ScriptExecutionOptions ScriptOptions => new() { StorageRoot = Path.Combine(root, "jobs") };
    private Task<ScriptExecutionResult> Run(MessageDocument source, ScriptScope scope = ScriptScope.WholeDocument,
        string code = "void Main() {}", IProgress<ScriptProgress>? progress = null) =>
        new ScriptExecution(documents, ScriptOptions).RunAsync(source, new(code), scope, progress, Token);

    [Fact]
    public async Task SwitchingActiveDocumentDuringRunKeepsCapturedSourceWithoutRetargeting()
    {
        var source = await Open(); var other = await Open(C); documents.Activate(source.Id);
        var result = await Run(source, progress: new InlineProgress<ScriptProgress>(p =>
        { if (p.Phase == "Compiling") documents.Activate(other.Id); }));
        Assert.False(result.SourceChangedDuringRun); Assert.Same(result.Document, documents.ActiveDocument);
        Assert.Equal(3, documents.Documents.Count); Assert.Equal(A, source.EditableText); Assert.Equal(C, other.EditableText);
        Assert.Equal(source.Id, result.Input!.DocumentId); Assert.True(result.Input.IsCurrent(source));
        Assert.Equal(source.Messages.Select(m => m.Id), result.MessageSources.Select(m => m.InputMessageId));
        Assert.Equal(A, result.Document.EditableText);
    }

    [Fact]
    public async Task CurrentScriptTargetsShiftedStableIdAndNavigationDoesNotRetargetItsJobOrdinal()
    {
        var doc = await Open(); var selected = doc.GetMessageId(2);
        var insertion = doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
        await doc.NavigateAsync(selected, Token); Task navigation = Task.CompletedTask;
        var result = await Run(doc, ScriptScope.CurrentMessage, "void Main() { Message.SetText(\"PID-1\", \"SYN-\" + Batch.MessageNumber); }",
            new InlineProgress<ScriptProgress>(p => { if (p.Phase == "Compiling") navigation = doc.NavigateAsync(insertion, Token); }));
        await navigation;
        Assert.Equal("SYN-2", result.Document.Message.Get("PID-1").Raw);
        Assert.Equal(selected, Assert.Single(result.MessageSources).InputMessageId);
        Assert.Equal(selected, result.Input!.Resolve(2)!.MessageId); Assert.Equal(3, result.Input.Resolve(2)!.LogicalPosition);
        Assert.Equal(2, result.Input.Resolve(2)!.OriginalOrdinal); Assert.DoesNotContain(result.Input.Messages, m => m.MessageId == insertion);
        Assert.Equal(insertion, doc.ActiveMessageId); Assert.Equal(C, doc.EditableText);
        Assert.False(result.SourceChangedDuringRun);
    }

    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)]
    public async Task WholeScriptUsesLogicalOrderCurrentDraftsAndStableOutputAttribution(int encoding)
    {
        var doc = await Open(codePage: encoding); var original = doc.GetMessageId(2);
        doc.InsertMessage(MessageInsertionPosition.AfterCurrent, C); await doc.EditRefresh;
        await doc.NavigateAsync(original, Token); doc.IsEditMode = true; doc.ReplaceWorkingText(B + "NTE|SYN-EDIT\r");
        var expected = doc.Messages.Select(e => e.Id).ToArray();
        var result = await Run(doc);
        Assert.Equal(expected, result.Input!.Messages.Select(m => m.MessageId));
        Assert.Equal(expected, result.MessageSources.Select(m => m.InputMessageId));
        Assert.Equal(new int?[] { 1, null, 2 }, result.Input.Messages.Select(m => m.OriginalOrdinal));
        for (var i = 0; i < expected.Length; i++) Assert.Equal(result.Document.GetMessageId(i + 1), result.MessageSources[i].OutputMessageId);
        await result.Document.NavigateAsync(2, Token); Assert.Equal(C, result.Document.EditableText);
        await result.Document.NavigateAsync(3, Token); Assert.Equal(B + "NTE|SYN-EDIT\r", result.Document.EditableText);
        Assert.Equal(B + "NTE|SYN-EDIT\r", doc.EditableText); Assert.True(doc.IsDirty);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InvalidInactiveInsertionHasNoFallbackAndIsExplicitlyAbsentFromCurrentJob(bool selectedInsertion)
    {
        var doc = await Open(); var original = doc.GetMessageId(2);
        var invalid = doc.InsertMessage(MessageInsertionPosition.Beginning); await doc.EditRefresh;
        var valid = doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        var selected = selectedInsertion ? valid : original; await doc.NavigateAsync(selected, Token);
        var result = await Run(doc, ScriptScope.CurrentMessage);
        Assert.Equal(selected, Assert.Single(result.MessageSources).InputMessageId);
        Assert.DoesNotContain(result.Input!.Messages, m => m.MessageId == invalid);
        Assert.Equal(selectedInsertion ? C : B, result.Document.EditableText);
        Assert.Equal(4, doc.MessageCount); Assert.Equal(1, doc.GetLogicalPosition(invalid));
    }

    [Theory]
    [InlineData("")] [InlineData("PID|SYN-INCOMPLETE\r")]
    public async Task RequiredInvalidInsertedMessageFailsBeforeWorkerWithoutPartialOutput(string text)
    {
        var doc = await Open(); var id = doc.InsertMessage(MessageInsertionPosition.Beginning, text); await doc.EditRefresh;
        var options = ScriptOptions with { WorkerPath = Path.Combine(root, "worker-must-not-start.dll") };
        foreach (var scope in new[] { ScriptScope.CurrentMessage, ScriptScope.WholeDocument })
        {
            var error = await Assert.ThrowsAsync<ScriptRunException>(() => new ScriptExecution(documents, options).RunAsync(doc,
                new("void Main() {}"), scope, token: Token));
            var diagnostic = Assert.Single(error.Diagnostics); Assert.Equal("BoundaryChange", diagnostic.Code);
            Assert.Equal(id, ScriptInputMapping.ForFailure(error)!.Resolve(diagnostic.MessageNumber!.Value)!.MessageId);
            Assert.Single(documents.Documents); Assert.Equal(text, doc.EditableText);
        }
    }

    [Fact]
    public async Task DeferredOutputUsesJobMappingRatherThanOriginalOrCurrentPosition()
    {
        var doc = await Open(); var a = doc.GetMessageId(1); var b = doc.GetMessageId(2);
        var inserted = doc.InsertMessage(MessageInsertionPosition.AfterCurrent, C); await doc.EditRefresh;
        var result = await Run(doc, code: "void Main() { if (Batch.MessageNumber == 1) { Batch.Defer(Message); Message.Exclude(); } }");
        Assert.Equal(new[] { inserted, b, a }, result.MessageSources.Select(m => m.InputMessageId));
        Assert.Equal(new[] { 2, 3, 1 }, result.MessageSources.Select(m => m.JobOrdinal));
        Assert.Equal(3, result.Processed); Assert.Equal(3, result.Kept);
    }

    [Fact]
    public async Task WorkerFailureOrdinalResolvesToShiftedOriginalIdentity()
    {
        var doc = await Open(); var target = doc.GetMessageId(2);
        doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
        var error = await Assert.ThrowsAsync<ScriptRunException>(() => Run(doc, code:
            "void Main() { if (Message.Read(\"PID-1\").Raw == \"B\") throw new System.Exception(); }"));
        var diagnostic = Assert.Single(error.Diagnostics, d => d.MessageNumber is not null);
        var attribution = ScriptInputMapping.ForFailure(error)!.Resolve(diagnostic.MessageNumber!.Value)!;
        Assert.Equal(target, attribution.MessageId); Assert.Equal(3, attribution.LogicalPosition); Assert.Equal(2, attribution.OriginalOrdinal);
        Assert.Single(documents.Documents);
    }

    [Theory]
    [InlineData("insert")] [InlineData("undo")] [InlineData("redo")]
    public async Task StructuralChangeDuringExecutionKeepsAttributedCapturedOutputAndRetainsSource(string change)
    {
        var doc = await Open(); var target = doc.GetMessageId(1);
        var inserted = doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        if (change == "redo") { doc.Undo(); await doc.EditRefresh; }
        await doc.NavigateAsync(target, Token); var revision = doc.SearchRevision;
        var capturedIds = doc.Messages.Select(m => m.Id).ToArray(); var changed = false;
        var result = await Run(doc, progress: new InlineProgress<ScriptProgress>(p =>
        {
            // Insertion happens after a worker result has already been staged, so
            // publication must still use the sealed input order and attribution.
            if (changed || p.Phase != (change == "insert" ? "Processing" : "Compiling")) return;
            changed = true;
            if (change == "insert") doc.InsertMessage(MessageInsertionPosition.Beginning, A);
            else if (change == "undo") doc.Undo(); else doc.Redo();
        }));
        Assert.True(changed); Assert.True(doc.SearchRevision > revision); Assert.True(result.SourceChangedDuringRun);
        Assert.Equal(revision, result.Input!.Revision); Assert.False(result.Input.IsCurrent(doc));
        Assert.Equal(capturedIds, result.Input.Messages.Select(m => m.MessageId));
        Assert.Equal(capturedIds, result.MessageSources.Select(m => m.InputMessageId));
        Assert.Equal(capturedIds.Length, result.Document.MessageCount); Assert.Equal(2, documents.Documents.Count);
        Assert.Equal(A, result.Document.EditableText);
        await result.Document.NavigateAsync(2, Token); Assert.Equal(B, result.Document.EditableText);
        if (change != "redo") { await result.Document.NavigateAsync(3, Token); Assert.Equal(C, result.Document.EditableText); }
        Assert.Equal(change == "undo" ? 0 : change == "redo" ? 3 : 4, doc.GetLogicalPosition(inserted));
        Assert.True(result.Document.IsDirty); Assert.Null(result.Document.SavePath);
        await doc.EditRefresh;
    }

    [Fact]
    public async Task CurrentScriptKeepsCapturedInsertionAfterUndoRemovesItsSourceMembership()
    {
        var doc = await Open(); var inserted = doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        var revision = doc.SearchRevision;
        var result = await Run(doc, ScriptScope.CurrentMessage, progress: new InlineProgress<ScriptProgress>(p =>
        { if (p.Phase == "Compiling") doc.Undo(); }));
        Assert.True(result.SourceChangedDuringRun); Assert.Equal(0, doc.GetLogicalPosition(inserted));
        Assert.Equal(inserted, Assert.Single(result.MessageSources).InputMessageId);
        Assert.Equal(revision, result.Input!.Revision); Assert.Equal(3, result.Input.Resolve(3)!.LogicalPosition);
        Assert.Null(result.Input.Resolve(3)!.OriginalOrdinal); Assert.Equal(C, result.Document.EditableText);
        Assert.Equal(2, doc.MessageCount); await doc.EditRefresh;
    }

    [Fact]
    public async Task SourceChangeDoesNotMaskWorkerFailureOrPublishPartialOutput()
    {
        var doc = await Open(); var target = doc.GetMessageId(2); var revision = doc.SearchRevision;
        var error = await Assert.ThrowsAsync<ScriptRunException>(() => Run(doc,
            code: "void Main() { if (Batch.MessageNumber == 2) throw new System.Exception(); }",
            progress: new InlineProgress<ScriptProgress>(p =>
            { if (p.Phase == "Compiling") doc.InsertMessage(MessageInsertionPosition.Beginning, C); })));
        var diagnostic = Assert.Single(error.Diagnostics, d => d.Phase == ScriptPhase.Execution);
        Assert.DoesNotContain(error.Diagnostics, d => d.Code == "InputChanged");
        var mapping = ScriptInputMapping.ForFailure(error)!;
        Assert.Equal(revision, mapping.Revision); Assert.Equal(target, mapping.Resolve(diagnostic.MessageNumber!.Value)!.MessageId);
        Assert.Equal(3, doc.GetLogicalPosition(target)); Assert.Single(documents.Documents);
        Assert.Empty(Directory.EnumerateDirectories(ScriptOptions.StorageRoot!)); await doc.EditRefresh;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancellationOrSourceClosureAfterEditingStillPreventsPublication(bool closeSource)
    {
        var doc = await Open(); using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ScriptExecution(documents, ScriptOptions).RunAsync(doc,
            new("void Main() {}"), ScriptScope.WholeDocument, new InlineProgress<ScriptProgress>(p =>
            {
                if (p.Phase != "Compiling") return;
                doc.InsertMessage(MessageInsertionPosition.Beginning, C);
                if (closeSource) documents.Close(doc.Id, discardChanges: true); else cancel.Cancel();
            }), cancel.Token));
        Assert.DoesNotContain(documents.Documents, d => d != doc);
        if (closeSource) { Assert.Empty(documents.Documents); await doc.Disposal; }
        else { Assert.Single(documents.Documents); Assert.Equal(3, doc.MessageCount); await doc.EditRefresh; }
        Assert.Empty(Directory.EnumerateDirectories(ScriptOptions.StorageRoot!));
    }
}
