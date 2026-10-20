using System.Diagnostics;
using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class SyntheticGenerationTests : IAsyncDisposable
{
    private const string A = "MSH|^~\\&|SYN-ORIGINAL-A\rPID|A\r";
    private const string B = "MSH*$%!@*SYN-ORIGINAL-B\r\nPID*B\r\n";
    private readonly DocumentManager documents = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-generator-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
    private Task<SyntheticGenerationResult> Generate(MessageDocument? target = null, int count = 1,
        MessageInsertionPosition position = MessageInsertionPosition.End, IProgress<SyntheticGenerationProgress>? progress = null, CancellationToken? token = null) =>
        new SyntheticGeneration(documents).GenerateAsync(new() { Count = count }, target, position, progress, token ?? Token);
    private async Task<MessageDocument> Open(string text = A + B, int codePage = 65001)
    {
        Directory.CreateDirectory(root); var encoding = Encoding.GetEncoding(codePage);
        using var input = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        return await MessageFileReader.OpenFileAsync(documents, "Synthetic source.hl7", input, Token);
    }
    public async ValueTask DisposeAsync()
    {
        var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    private sealed record State(MessageId[] Ids, MessageId? Selected, string? Text, bool Dirty, bool Undo, bool Redo, long Version, long Revision, string? SavePath);
    private static State Capture(MessageDocument doc) => new(doc.Messages.Select(m => m.Id).ToArray(), doc.ActiveMessageId, doc.EditableText, doc.IsDirty, doc.CanUndo, doc.CanRedo, doc.ContentVersion, doc.SearchRevision, doc.SavePath);
    private static void Unchanged(State before, MessageDocument doc)
    {
        var after = Capture(doc); Assert.Equal(before.Ids, after.Ids);
        Assert.Equal(before with { Ids = after.Ids }, after); Assert.False(doc.IsPreparingInsertion);
    }
    [Theory]
    [InlineData(MessageInsertionPosition.BeforeCurrent, 2)] [InlineData(MessageInsertionPosition.AfterCurrent, 3)]
    [InlineData(MessageInsertionPosition.Beginning, 1)] [InlineData(MessageInsertionPosition.End, 3)]
    public async Task BatchUsesEveryLogicalPositionOneHistoryOperationAndStableIdentities(MessageInsertionPosition position, int start)
    {
        var doc = await Open(); var originals = doc.Messages.ToArray(); await doc.NavigateAsync(2, Token);
        var result = await Generate(doc, 3, position); await doc.EditRefresh;
        Assert.False(result.IsNewDocument); Assert.True(doc.IsEditMode); Assert.Equal(5, doc.MessageCount);
        Assert.Equal(result.MessageIds, doc.Messages.Skip(start - 1).Take(3).Select(m => m.Id));
        Assert.Equal(result.MessageIds[0], doc.ActiveMessageId); Assert.True(doc.IsDirty);
        Assert.All(doc.Messages.Where(m => result.MessageIds.Contains(m.Id)), m => { Assert.Equal(MessageOrigin.Inserted, m.Origin); Assert.Null(m.SourceOrdinal); });
        Assert.Null(doc.ActiveOriginalMessage); Assert.False(doc.HasOriginalMessage);
        for (var i = 0; i < 3; i++)
        {
            await doc.NavigateAsync(result.MessageIds[i], Token);
            Assert.Equal(result.Options.ControlId(i + 1), doc.Message.Get("MSH-10").Raw);
        }
        doc.IsEditMode = false; doc.Undo(); await doc.EditRefresh;
        Assert.Equal(originals, doc.Messages); Assert.False(doc.IsDirty); Assert.False(doc.CanUndo);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(result.MessageIds[0], doc.ActiveMessageId);
        Assert.Equal(result.MessageIds, doc.Messages.Skip(start - 1).Take(3).Select(m => m.Id));
        Assert.False(doc.CanRedo); Assert.True(doc.IsDirty); Assert.False(doc.IsEditMode);
        await doc.NavigateAsync(originals[0].Id, Token); Assert.Equal(A, doc.EditableText);
        await doc.NavigateAsync(originals[1].Id, Token); Assert.Equal(B, doc.EditableText);
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)]
    public async Task ExistingCustomDelimitersEncodingsAndSourceBytesSurviveSave(int codePage)
    {
        var doc = await Open(codePage: codePage); var encoding = Encoding.GetEncoding(codePage);
        var original = doc.Messages.ToArray(); var bytes = doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length));
        var generated = await Generate(doc, 2, MessageInsertionPosition.AfterCurrent); await doc.EditRefresh;
        var path = Path.Combine(root, "mixed.hl7"); await doc.SaveAsync(path, Token); Assert.False(doc.IsDirty);
        Assert.Equal(bytes, doc.Snapshot.ReadBytes(new(0, doc.Snapshot.Length)));
        var payload = string.Concat(SyntheticMessageGenerator.Generate(new() { Count = 2 }, Token).Messages);
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(A + payload + B)), await File.ReadAllBytesAsync(path, Token));
        using var input = File.OpenRead(path); var reopened = await MessageFileReader.OpenFileAsync(documents, "Reopened", input, Token);
        Assert.Equal(4, reopened.MessageCount); await reopened.NavigateAsync(4, Token); Assert.Equal(B, reopened.EditableText);
        Assert.Equal(original[0].Id, doc.GetMessageId(1)); Assert.Equal(original[1].Id, doc.GetMessageId(4));
        doc.Undo(); await doc.EditRefresh; Assert.True(doc.IsDirty); doc.Redo(); await doc.EditRefresh; Assert.False(doc.IsDirty);
        Assert.Equal(generated.MessageIds[0], doc.ActiveMessageId);
    }
    [Theory]
    [InlineData(SyntheticMessageType.AdtA01)] [InlineData(SyntheticMessageType.OruR01)]
    public async Task NewOutputIsIndependentUnsavedAndUsesNormalSaveAs(SyntheticMessageType type)
    {
        var source = await Open(); await source.SaveAsync(Path.Combine(root, "source.hl7"), Token); var before = Capture(source);
        var config = new SyntheticGenerationOptions { MessageType = type, Count = 2 };
        var first = await new SyntheticGeneration(documents).GenerateAsync(config, token: Token);
        var second = await new SyntheticGeneration(documents).GenerateAsync(config, token: Token);
        Assert.Equal(config.SuggestedFileName, first.Document.Title); Assert.NotEqual(first.Document.DisplayTitle, second.Document.DisplayTitle);
        Assert.True(first.IsNewDocument); Assert.Null(first.Document.SavePath); Assert.True(first.Document.IsDirty); Assert.True(first.Document.CanEditText);
        Assert.Same(second.Document, documents.ActiveDocument); Assert.NotEqual(first.MessageIds, second.MessageIds);
        Assert.All(first.Document.Messages, m => { Assert.Equal(MessageOrigin.Inserted, m.Origin); Assert.Null(m.SourceOrdinal); });
        Assert.False(first.Document.HasOriginalMessage); Unchanged(before, source);
        var path = Path.Combine(root, "generated.hl7"); await first.Document.SaveAsync(path, Token); Assert.False(first.Document.IsDirty);
        Assert.Equal(SyntheticMessageGenerator.Generate(config, Token).GetUtf8Bytes(), await File.ReadAllBytesAsync(path, Token));
        first.Document.Undo(); await first.Document.EditRefresh; Assert.Empty(first.Document.Messages); Assert.True(first.Document.IsDirty);
        first.Document.Redo(); await first.Document.EditRefresh; Assert.False(first.Document.IsDirty); Assert.Equal(first.MessageIds, first.Document.Messages.Select(m => m.Id));
        Assert.True(second.Document.IsDirty); Assert.Null(second.Document.SavePath);
        using var input = File.OpenRead(path); var reopened = await MessageFileReader.OpenFileAsync(documents, "Reopened", input, Token);
        Assert.Equal(2, reopened.MessageCount); Assert.Equal(first.Document.EditableText, reopened.EditableText);
        Assert.False(await UnsavedChanges.ConfirmAsync(second.Document, _ => Task.FromResult(UnsavedChangesDecision.Cancel), _ => Task.FromResult(false)));
    }
    [Theory]
    [InlineData("Generating")] [InlineData("Writing")] [InlineData("Indexing staged output")] [InlineData("Verifying staged bytes")]
    public async Task CancellationPreservesTextSelectionRevisionSavePointAndRedo(string phase)
    {
        var doc = await Open(); doc.InsertMessage(MessageInsertionPosition.End, A); await doc.EditRefresh; doc.Undo(); await doc.EditRefresh;
        var before = Capture(doc); using var cancellation = new CancellationTokenSource(); var seen = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Generate(doc, 1000, progress: new InlineProgress<SyntheticGenerationProgress>(p =>
        { if (p.Phase == phase) { seen = true; cancellation.Cancel(); } }), token: cancellation.Token));
        Assert.True(seen); Unchanged(before, doc);
        var retry = await Generate(doc, 1); await doc.EditRefresh;
        Assert.Equal(SyntheticMessageGenerator.Generate(new(), Token).Messages[0], doc.EditableText); Assert.Single(retry.MessageIds);
    }
    [Fact]
    public async Task PreCanceledNewOutputNeverAppearsAndGenerationHasNoUiThreadRequirement()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Generate(token: cancellation.Token)); Assert.Empty(documents.Documents);
        var threads = new HashSet<int>();
        await Generate(count: 1000, progress: new InlineProgress<SyntheticGenerationProgress>(p => { if (p.Phase == "Generating") threads.Add(Environment.CurrentManagedThreadId); }));
        Assert.NotEmpty(threads); Assert.Single(documents.Documents);
    }
    [Theory]
    [InlineData("Generating")] [InlineData("Writing")] [InlineData("Verifying staged bytes")]
    public async Task CanceledNewOutputNeverPublishesEvenAfterPrivateInsertionPreparation(string phase)
    {
        using var cancellation = new CancellationTokenSource(); var seen = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Generate(count: 1000, token: cancellation.Token,
            progress: new InlineProgress<SyntheticGenerationProgress>(p => { if (p.Phase == phase) { seen = true; cancellation.Cancel(); } })));
        Assert.True(seen); Assert.Empty(documents.Documents); Assert.Null(documents.ActiveDocument);
    }
    [Fact]
    public async Task StrictDestinationEncodingFailurePreservesTheUnrepresentableDraft()
    {
        Directory.CreateDirectory(root);
        var parsed = Hl7Parser.Parse(A, new() { Encoding = Encoding.ASCII });
        var doc = MessageFileReader.Open(documents, "Synthetic ASCII", parsed); doc.IsEditMode = true;
        doc.ReplaceWorkingText(A + "NTE|SYN-☃\r"); await doc.EditRefresh; var before = Capture(doc);
        await Assert.ThrowsAsync<EncoderFallbackException>(() => Generate(doc, 2)); Unchanged(before, doc);
    }
    [Theory]
    [InlineData("MSH|^~\\&|SYN\rPID|unterminated")]
    [InlineData("FHS|^~\\&|SYN\rBHS|^~\\&|SYN\rMSH|^~\\&|SYN\rBTS|1\rFTS|1\r")]
    [InlineData("\vMSH|^~\\&|SYN\r")]
    [InlineData("opaque source bytes")]
    public async Task UnsafeJoinOrContainerFailsAtomicallyWithoutRepair(string text)
    {
        var doc = await Open(text); var before = Capture(doc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Generate(doc)); Unchanged(before, doc);
    }
    [Fact]
    public async Task BeginningOfUnterminatedSourceIsSafeButBlankDraftIsNotSilentlyOmitted()
    {
        var doc = await Open("MSH|^~\\&|SYN\rPID|unterminated"); await Generate(doc, position: MessageInsertionPosition.Beginning); await doc.EditRefresh;
        Assert.Equal(2, doc.MessageCount);
        doc.InsertMessage(MessageInsertionPosition.End); await doc.EditRefresh; var before = Capture(doc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Generate(doc)); Unchanged(before, doc);
    }
    [Fact]
    public async Task MemoryOriginalAlsoRequiresProspectiveMembershipProofBeforeFirstInsertion()
    {
        var doc = MessageFileReader.Open(documents, "Synthetic memory", Hl7Parser.Parse("MSH|^~\\&|SYN\rPID|unterminated"));
        var before = Capture(doc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Generate(doc)); Unchanged(before, doc);
        await Generate(doc, position: MessageInsertionPosition.Beginning); await doc.EditRefresh; Assert.Equal(2, doc.MessageCount);
    }
    [Fact]
    public async Task RetainedBudgetFailurePreservesRedoAndAllDrafts()
    {
        var doc = await Open();
        doc.InsertMessages(MessageInsertionPosition.End, Enumerable.Repeat(new string('A', 1024 * 1024), 8).ToArray()); await doc.EditRefresh;
        var before = Capture(doc); await Assert.ThrowsAsync<Hl7LimitException>(() => Generate(doc)); Unchanged(before, doc);
    }
    [Fact]
    public async Task HistoryEntryBudgetRejectsWholeRequestBeforePublication()
    {
        var doc = await Open(); doc.IsEditMode = true;
        for (var i = 0; i < MessageDocument.MaximumHistoryEntries; i++) doc.ReplaceWorkingText(i % 2 == 0 ? A + "NTE|SYN\r" : A);
        await doc.EditRefresh; var before = Capture(doc);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Generate(doc, 100)); Unchanged(before, doc);
        doc.Undo(); await doc.EditRefresh; Assert.True(doc.CanRedo);
        var result = await Generate(doc, 1); await doc.EditRefresh; Assert.Single(result.MessageIds); Assert.False(doc.CanRedo);
    }
    [Fact]
    public async Task FramedInsertionRetainsTransportAndAllOriginalBytes()
    {
        var doc = await Open("\v" + A + "\x1c\r\v" + B + "\x1c\r");
        await Generate(doc, 2, MessageInsertionPosition.AfterCurrent); await doc.EditRefresh;
        var path = Path.Combine(root, "framed.hl7"); await doc.SaveAsync(path, Token);
        var generated = SyntheticMessageGenerator.Generate(new() { Count = 2 }, Token);
        var expected = "\v" + A + "\x1c\r" + string.Concat(generated.Messages.Select(m => "\v" + m + "\x1c\r")) + "\v" + B + "\x1c\r";
        Assert.Equal(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(expected)), await File.ReadAllBytesAsync(path, Token));
    }
    [Fact]
    public async Task TargetNavigationDuringGenerationIsRejectedRatherThanRetargetingAnchor()
    {
        var doc = await Open(); Task navigation = Task.CompletedTask; var changed = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Generate(doc, 1000, progress: new InlineProgress<SyntheticGenerationProgress>(p =>
        { if (changed || p.Phase != "Generating") return; changed = true; navigation = doc.NavigateAsync(2, Token); })));
        await navigation; Assert.Equal(2, doc.MessageCount); Assert.False(doc.IsDirty); Assert.Equal(2, doc.MessageNumber);
    }
    [Fact]
    public async Task ActiveTabSwitchNeverRetargetsCapturedDocument()
    {
        var doc = await Open(); var other = await Open(A); documents.Activate(doc.Id);
        var result = await Generate(doc, 100, progress: new InlineProgress<SyntheticGenerationProgress>(p => { if (p.Phase == "Generating") documents.Activate(other.Id); }));
        await doc.EditRefresh; Assert.Same(doc, result.Document); Assert.Same(other, documents.ActiveDocument);
        Assert.Equal(102, doc.MessageCount); Assert.Single(other.Messages); Assert.False(other.IsDirty);
        documents.Activate(doc.Id); doc.IsEditMode = false; doc.Undo(); await doc.EditRefresh; Assert.Equal(2, doc.MessageCount);
        documents.Activate(other.Id); other.Undo(); Assert.Single(other.Messages); documents.Activate(doc.Id); doc.Redo(); await doc.EditRefresh; Assert.Equal(102, doc.MessageCount);
    }
    [Fact]
    public async Task TargetClosureDuringGenerationPublishesNothing()
    {
        var doc = await Open(); var closed = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Generate(doc, 1000, progress: new InlineProgress<SyntheticGenerationProgress>(p =>
        { if (!closed && p.Phase == "Generating") { closed = documents.Close(doc.Id); } })));
        await doc.Disposal; Assert.True(closed); Assert.Empty(documents.Documents);
    }
    [Fact]
    public async Task ProspectiveValidationFreezesMutationsUntilCanceledAndReleasesOwnedStorage()
    {
        var doc = await Open(); doc.IsEditMode = true; var before = Capture(doc);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var task = Generate(doc, progress: new InlineProgress<SyntheticGenerationProgress>(p =>
        { if (p.Phase == "Indexing staged output") { entered.SetResult(); release.Task.GetAwaiter().GetResult(); } }), token: cancellation.Token);
        await entered.Task.WaitAsync(Token);
        try
        {
            Assert.True(doc.IsPreparingInsertion); Assert.False(doc.CanEditText); Assert.False(doc.CanUndo); Assert.False(doc.CanRedo);
            Assert.Throws<InvalidOperationException>(() => doc.InsertMessage(MessageInsertionPosition.End));
            Assert.Throws<InvalidOperationException>(() => doc.ReplaceWorkingText(A + "NTE|blocked\r"));
            Assert.False(documents.Close(doc.Id, discardChanges: true)); await doc.NavigateAsync(2, Token); Assert.Equal(before.Selected, doc.ActiveMessageId);
            await Assert.ThrowsAsync<InvalidOperationException>(() => doc.SaveAsync(Path.Combine(root, "must-not-save.hl7"), Token));
            await Assert.ThrowsAsync<HL7Workbench.Scripting.ScriptRunException>(() => doc.CaptureScriptAsync(1024 * 1024, Path.Combine(root, "capture"), null, doc.SearchRevision, Token));
            cancellation.Cancel();
        }
        finally { release.TrySetResult(); }
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task); Unchanged(before, doc); Assert.True(doc.CanEditText);
    }
    [Theory]
    [InlineData(1)] [InlineData(100)] [InlineData(1000)]
    public async Task RepresentativeBatchPerformanceRemainsBounded(int count)
    {
        var doc = await Open(); var allocated = GC.GetTotalAllocatedBytes(); var clock = Stopwatch.StartNew();
        var result = await Generate(doc, count); await doc.EditRefresh;
        var bytes = GC.GetTotalAllocatedBytes() - allocated;
        Assert.Equal(count + 2, doc.MessageCount); Assert.True(bytes < 128L * 1024 * 1024, $"Generation/insertion allocated {bytes} bytes");
        TestContext.Current.TestOutputHelper!.WriteLine($"generator count={count}; totalMs={clock.Elapsed.TotalMilliseconds:F3}; generationMs={result.GenerationTime.TotalMilliseconds:F3}; validationPublicationMs={result.PublicationTime.TotalMilliseconds:F3}; allocatedBytes={bytes}");
    }
}
