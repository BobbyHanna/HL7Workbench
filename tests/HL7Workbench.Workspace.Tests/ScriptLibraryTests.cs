using HL7Workbench.Scripting;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class ScriptLibraryTests
{
    private sealed class Library : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-library-test-" + Guid.NewGuid().ToString("N"));
        internal ScriptLibrary Store => new(Root);
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private static Task<UnsavedChangesDecision> Cancel() => Task.FromResult(UnsavedChangesDecision.Cancel);
    [Fact]
    public void CreateSaveReopenRenameAndDuplicateUseStableIdentitiesAndAtomicRecords()
    {
        using var library = new Library(); var store = library.Store;
        var original = store.Create("Synthetic replacement", "Message.SetText(\"PID-5.2\", \"SYNTHETIC\");");
        var reopened = library.Store.Open(original.Id); Assert.Equal(original, reopened);
        var renamed = store.Save(reopened with { Name = "Renamed" });
        Assert.Equal(original.Id, renamed.Id); Assert.Equal("Renamed", library.Store.Open(original.Id).Name);
        var copy = store.Duplicate(renamed); Assert.NotEqual(original.Id, copy.Id); Assert.Equal(original.Source, copy.Source);
        Assert.Equal(3, store.List().Count); Assert.Empty(Directory.GetFiles(library.Root, "*.tmp"));
        Assert.DoesNotContain("Renamed", string.Join("", Directory.GetFiles(library.Root).Select(Path.GetFileName)));
    }
    [Fact]
    public void BundledExampleIsReadOnlyAndCopiesAreIndependent()
    {
        using var library = new Library(); var store = library.Store;
        Assert.Single(store.List()); var example = store.Open(ScriptLibrary.ExampleId);
        Assert.Throws<InvalidOperationException>(() => store.Save(example));
        var copy = store.Duplicate(example); Assert.False(copy.Bundled);
        store.Save(copy with { Source = "Message.Exclude();" });
        Assert.Equal(example, store.Open(ScriptLibrary.ExampleId));
    }
    [Fact]
    public void ExternalChangesAndDeletionDoNotOverwriteSavedEntries()
    {
        using var library = new Library(); var store = library.Store;
        var entry = store.Create("Synthetic", "// first");
        var changed = store.Save(entry with { Source = "// external" });
        Assert.Throws<IOException>(() => store.Save(entry with { Source = "// stale" }));
        Assert.Equal(changed, store.Open(entry.Id));
        File.Delete(Path.Combine(library.Root, entry.Id.ToString("N") + ".json"));
        Assert.Throws<IOException>(() => store.Save(changed));
    }
    [Fact]
    public void EntryLimitsAndUnsupportedRecordsFailWithoutReplacingScripts()
    {
        using var library = new Library(); var store = library.Store;
        Assert.Throws<InvalidDataException>(() => store.Create("", ""));
        Assert.Throws<InvalidDataException>(() => store.Create("Synthetic", new string('X', ScriptLimits.ScriptCharacters + 1)));
        var entry = store.Create("Synthetic", "// preserved");
        File.WriteAllText(Path.Combine(library.Root, entry.Id.ToString("N") + ".json"), "{\"Version\":99}");
        Assert.Throws<InvalidDataException>(() => store.Open(entry.Id));
        var session = new ScriptEditorSession(store); Assert.Contains("unavailable", session.Status);
    }
    [Theory]
    [InlineData(UnsavedChangesDecision.Cancel, false)]
    [InlineData(UnsavedChangesDecision.Save, true)]
    [InlineData(UnsavedChangesDecision.Discard, true)]
    public async Task DirtyDraftCloseDecisionsPreserveOrPublishOnlyReviewedContent(UnsavedChangesDecision decision, bool expected)
    {
        using var library = new Library(); var session = new ScriptEditorSession(library.Store);
        await session.NewAsync(Cancel); session.Name = "Synthetic draft"; session.Source = "Message.Exclude();";
        Assert.True(session.IsDirty);
        Assert.Equal(expected, await session.ConfirmChangesAsync(() => Task.FromResult(decision)));
        if (!expected) { Assert.True(session.IsDirty); Assert.Equal("Message.Exclude();", session.Source); }
        else Assert.False(session.IsDirty);
        Assert.Equal(decision == UnsavedChangesDecision.Save ? 2 : 1, library.Store.List().Count);
    }
    [Fact]
    public async Task FailedSaveAndDialogDismissalKeepDraftAndBlockSwitch()
    {
        using var library = new Library(); var session = new ScriptEditorSession(library.Store);
        await session.NewAsync(Cancel); session.Name = ""; session.Source = "// synthetic draft";
        Assert.False(await session.OpenAsync(ScriptLibrary.ExampleId, () => Task.FromResult(UnsavedChangesDecision.Save)));
        Assert.True(session.IsDirty); Assert.Equal("// synthetic draft", session.Source);
        Assert.False(await session.NewAsync(Cancel)); Assert.Equal("// synthetic draft", session.Source);
    }
    [Fact]
    public async Task PromptConsentCannotDiscardNewerDraftOrRunConcurrentTransitions()
    {
        using var library = new Library(); var session = new ScriptEditorSession(library.Store);
        await session.NewAsync(Cancel); session.Source = "// old";
        var decision = new TaskCompletionSource<UnsavedChangesDecision>();
        var closing = session.ConfirmChangesAsync(() => decision.Task);
        Assert.False(await session.NewAsync(Cancel));
        session.Source = "// newer"; decision.SetResult(UnsavedChangesDecision.Discard);
        Assert.False(await closing); Assert.Equal("// newer", session.Source); Assert.True(session.IsDirty);
    }
    [Fact]
    public async Task DuplicatePreservesUnsavedDraftWithoutOverwritingOriginal()
    {
        using var library = new Library(); var session = new ScriptEditorSession(library.Store);
        var original = library.Store.Create("Synthetic", "// saved");
        await session.OpenAsync(original.Id, Cancel); session.Source = "// unsaved";
        Assert.True(session.Duplicate()); Assert.False(session.IsDirty);
        Assert.Equal("// unsaved", session.Source); Assert.Equal("// saved", library.Store.Open(original.Id).Source);
    }
    [Fact]
    public async Task DeferredShutdownDiscardLeavesDraftUntilAllOtherClosePromptsSucceed()
    {
        using var library = new Library(); var session = new ScriptEditorSession(library.Store);
        await session.NewAsync(Cancel); session.Source = "// retained if an HL7 close is canceled";
        Assert.True(await session.ConfirmChangesAsync(() => Task.FromResult(UnsavedChangesDecision.Discard), discardImmediately: false));
        Assert.True(session.IsDirty); Assert.Equal("// retained if an HL7 close is canceled", session.Source);
        // The shell consumes this consent only after all document prompts and revision checks succeed.
        session.DiscardChanges(); Assert.False(session.IsDirty);
    }
}
