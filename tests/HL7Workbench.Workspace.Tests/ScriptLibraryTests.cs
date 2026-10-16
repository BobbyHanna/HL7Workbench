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
    [Theory]
    [InlineData("")] [InlineData("   ")] [InlineData("Invalid\tname")]
    public async Task InvalidRenamePreservesIdentitySourceAndSavedRecord(string name)
    {
        using var library = new Library(); var original = library.Store.Create("Synthetic", "void Main() {}");
        var session = new ScriptEditorSession(library.Store); await session.OpenAsync(original.Id, Cancel); session.Source += "\n// unsaved";
        var draft = session.Source; Assert.False(session.Rename(name)); Assert.Equal("Synthetic", session.Name);
        Assert.Equal(original.Id, session.Id); Assert.Equal(draft, session.Source); Assert.True(session.IsDirty);
        Assert.Equal(original, library.Store.Open(original.Id));
        Assert.True(session.Rename("Renamed synthetic")); Assert.Equal(draft, session.Source); Assert.True(session.Save());
        Assert.Equal(original.Id, session.Id); Assert.Equal("Renamed synthetic", library.Store.Open(original.Id).Name);
        Assert.Equal(draft, library.Store.Open(original.Id).Source);
    }
    [Fact]
    public async Task DeleteProtectsExamplesUnsavedDraftsAndExternallyChangedRecords()
    {
        using var library = new Library(); var session = new ScriptEditorSession(library.Store);
        Assert.False(session.CanDelete); Assert.False(session.Rename("Not allowed"));
        Assert.Throws<InvalidOperationException>(() => library.Store.Delete(ScriptLibrary.Example));
        Assert.Throws<InvalidOperationException>(() => library.Store.Delete(ScriptLibrary.PatientExample with { Bundled = false }));
        await session.NewAsync(Cancel); Assert.False(session.CanDelete);
        Assert.False(await session.DeleteAsync(Cancel, _ => throw new InvalidOperationException()));
        Assert.True(session.Save()); var saved = library.Store.Open(session.Id);
        library.Store.Save(saved with { Source = "void Main() {} // external" });
        session.Source += "// draft"; var source = session.Source;
        Assert.False(await session.DeleteAsync(() => Task.FromResult(UnsavedChangesDecision.Discard), _ => Task.FromResult(true)));
        Assert.Equal(source, session.Source); Assert.Equal(saved.Id, session.Id); Assert.Contains("not deleted", session.Status);
        Assert.Contains("external", library.Store.Open(saved.Id).Source);
    }
    [Theory]
    [InlineData(UnsavedChangesDecision.Save)] [InlineData(UnsavedChangesDecision.Discard)] [InlineData(UnsavedChangesDecision.Cancel)]
    public async Task DeleteConsumesDirtyConsentOnlyAfterConfirmationAndUsesValidFallback(UnsavedChangesDecision decision)
    {
        using var library = new Library(); var saved = library.Store.Create("Synthetic saved", "void Main() {}");
        var session = new ScriptEditorSession(library.Store); await session.OpenAsync(saved.Id, Cancel);
        session.Name = "Synthetic draft name"; session.Source += " // dirty"; var draft = session.Source;
        var confirmations = 0;
        Assert.False(await session.DeleteAsync(() => Task.FromResult(decision), name => { confirmations++; return Task.FromResult(false); }));
        Assert.Equal(decision == UnsavedChangesDecision.Cancel ? 0 : 1, confirmations);
        Assert.Equal(saved.Id, session.Id); Assert.Equal(draft, session.Source);
        Assert.Contains(library.Store.List(), i => i.Id == saved.Id);
        Assert.True(await session.DeleteAsync(() => Task.FromResult(UnsavedChangesDecision.Discard), name =>
        { Assert.Equal(decision == UnsavedChangesDecision.Save ? "Synthetic draft name" : saved.Name, name); return Task.FromResult(true); }));
        Assert.Equal(ScriptLibrary.ExampleId, session.Id); Assert.False(session.IsDirty); Assert.False(session.CanDelete);
        Assert.DoesNotContain(library.Store.List(), i => i.Id == saved.Id); Assert.Empty(Directory.GetFiles(library.Root, "*.tmp"));
    }
    [Fact]
    public async Task DeleteConfirmationCannotDiscardNewerDraftAndObsoleteRecordsRemainProtectedUntilExplicitConsent()
    {
        using var library = new Library(); Directory.CreateDirectory(library.Root); var id = Guid.NewGuid();
        var path = Path.Combine(library.Root, id.ToString("N") + ".json");
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(new { Version = 1, Id = id, Name = "Legacy synthetic", Source = "Message.Exclude();" }));
        var session = new ScriptEditorSession(library.Store); await session.OpenAsync(id, Cancel);
        var confirmation = new TaskCompletionSource<bool>(); var deleting = session.DeleteAsync(Cancel, _ => confirmation.Task);
        session.Source += " // newer"; confirmation.SetResult(true);
        Assert.False(await deleting); Assert.True(session.IsObsolete); Assert.True(File.Exists(path));
        Assert.True(await session.DeleteAsync(() => Task.FromResult(UnsavedChangesDecision.Discard), _ => Task.FromResult(true)));
        Assert.False(File.Exists(path)); Assert.Equal(ScriptLibrary.ExampleId, session.Id);
    }
    [Fact]
    public async Task UnifiedDraftSaveReopenDuplicateCancelAndDiscardPreserveSource()
    {
        using var library = new Library(); var session = new ScriptEditorSession(library.Store);
        Assert.True(await session.NewAsync(Cancel)); Assert.Contains("void Main()", session.Source); Assert.True(session.IsDirty);
        session.Source = "int count = 0; void Main() { count++; }";
        Assert.True(session.Save()); var id = session.Id; var source = session.Source;
        session.Source += "\n// synthetic draft"; Assert.True(session.IsDirty);
        Assert.False(await session.NewAsync(Cancel)); Assert.Equal(id, session.Id);
        session.DiscardChanges(); Assert.False(session.IsDirty); Assert.Equal(source, session.Source);
        Assert.True(session.Duplicate()); Assert.Equal(source, session.Source); Assert.NotEqual(id, session.Id);
        Assert.True(await session.NewAsync(Cancel));
        Assert.True(await session.OpenAsync(id, () => Task.FromResult(UnsavedChangesDecision.Discard))); Assert.Equal(source, session.Source);
        var json = File.ReadAllText(Path.Combine(library.Root, id.ToString("N") + ".json"));
        Assert.Contains("\"Version\":3", json); Assert.DoesNotContain("Mode", json); Assert.DoesNotContain("Kind", json);
    }
    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task ObsoleteRecordsArePreservedBlockedAndAdoptedOnlyByExplicitSave(int version)
    {
        using var library = new Library(); Directory.CreateDirectory(library.Root);
        var id = Guid.NewGuid(); var path = Path.Combine(library.Root, id.ToString("N") + ".json");
        var old = System.Text.Json.JsonSerializer.Serialize(new { Version = version, Id = id, Name = "Legacy", Source = "Message.Exclude();", Mode = 1 });
        File.WriteAllText(path, old);
        var entry = library.Store.Open(id); Assert.True(entry.Obsolete); Assert.Equal(old, File.ReadAllText(path));
        Assert.True(library.Store.List().Single(i => i.Id == id).Obsolete);
        var copy = library.Store.Duplicate(entry); Assert.True(copy.Obsolete); Assert.True(library.Store.Open(copy.Id).Obsolete);
        var session = new ScriptEditorSession(library.Store); Assert.True(await session.OpenAsync(id, Cancel));
        Assert.True(session.IsObsolete); Assert.Contains("void Main()", session.Status); Assert.False(session.IsDirty);
        session.Source = "void Main() { Message.Exclude(); }";
        Assert.True(session.IsObsolete); Assert.Equal(old, File.ReadAllText(path));
        Assert.True(session.Save()); Assert.False(session.IsObsolete);
        Assert.False(library.Store.Open(id).Obsolete);
        Assert.Contains("\"Version\":3", File.ReadAllText(path)); Assert.DoesNotContain("Mode", File.ReadAllText(path));
        Assert.True(library.Store.Open(copy.Id).Obsolete);
    }
    [Fact]
    public async Task BothUnifiedExamplesAreImmutableDuplicableAndReopenWithoutKinds()
    {
        using var library = new Library(); var session = new ScriptEditorSession(library.Store);
        foreach (var id in new[] { ScriptLibrary.ExampleId, ScriptLibrary.PatientExampleId })
        {
            Assert.True(await session.OpenAsync(id, Cancel)); Assert.True(session.IsBundled);
            var source = session.Source; Assert.Contains("void Main()", source);
            session.Source = "changed"; Assert.Equal(source, session.Source); Assert.False(session.Save());
            Assert.True(session.Duplicate()); Assert.False(session.IsBundled); Assert.False(session.IsObsolete);
            Assert.Equal(source, session.Source); var copyId = session.Id;
            session.Source += "\n// draft"; Assert.True(session.Duplicate());
            Assert.Equal(source, library.Store.Open(copyId).Source);
        }
    }
    [Fact]
    public void CreateSaveReopenRenameAndDuplicateUseStableIdentitiesAndAtomicRecords()
    {
        using var library = new Library(); var store = library.Store;
        var original = store.Create("Synthetic replacement", "Message.SetText(\"PID-5.2\", \"SYNTHETIC\");");
        var reopened = library.Store.Open(original.Id); Assert.Equal(original, reopened);
        var renamed = store.Save(reopened with { Name = "Renamed" });
        Assert.Equal(original.Id, renamed.Id); Assert.Equal("Renamed", library.Store.Open(original.Id).Name);
        var copy = store.Duplicate(renamed); Assert.NotEqual(original.Id, copy.Id); Assert.Equal(original.Source, copy.Source);
        Assert.Equal(4, store.List().Count); Assert.Empty(Directory.GetFiles(library.Root, "*.tmp"));
        Assert.DoesNotContain("Renamed", string.Join("", Directory.GetFiles(library.Root).Select(Path.GetFileName)));
    }
    [Fact]
    public void BundledExampleIsReadOnlyAndCopiesAreIndependent()
    {
        using var library = new Library(); var store = library.Store;
        Assert.Equal(2, store.List().Count); var example = store.Open(ScriptLibrary.ExampleId);
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
        Assert.Equal(decision == UnsavedChangesDecision.Save ? 3 : 2, library.Store.List().Count);
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
