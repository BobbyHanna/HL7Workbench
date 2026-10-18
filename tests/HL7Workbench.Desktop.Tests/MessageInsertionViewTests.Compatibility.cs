using System.Diagnostics;
using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed partial class MessageInsertionViewTests
{
    [Theory]
    [InlineData("SYN-A")] [InlineData("SYN-B")] [InlineData("NO-MATCH")]
    public Task FilteredEofNeverAppendsIncludingLastLogicalMessageAndNoResults(string query) => InWindow(async s =>
    {
        using var search = new MessageSearchSession(s.Manager); await search.StartAsync(new(MessageSearchMode.Text, query), Token);
        search.FilterNavigator = true;
        if (search.ResultCount > 0) await s.View.NavigateAsync(search.Page[0].MessageNumber);
        var selected = s.Document.ActiveMessageId; await s.View.NextAsync(); s.View.NextMessageCommand.Execute(null);
        Assert.Equal(2, s.Document.MessageCount); Assert.Equal(selected, s.Document.ActiveMessageId); Assert.False(s.Document.IsDirty);
        Assert.False(s.View.NextMessageCommand.CanExecute(null)); Assert.NotNull(s.Document.NavigationMatches);
        search.FilterNavigator = false; await s.View.NavigateAsync(2); await s.View.NextAsync(); Assert.Equal(3, s.Document.MessageCount);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task BlankSaveRejectsSafelyThenExactCustomDelimiterContentSavesAndReopens(bool framed) => InWindow(async s =>
    {
        var path = Path.Combine(s.Root, "saved.hl7"); var sentinel = Encoding.UTF8.GetBytes("SYNTHETIC DESTINATION");
        await File.WriteAllBytesAsync(path, sentinel, Token); var destination = await FileSaveDestination.CaptureAsync(path, Token);
        var original = s.Document.Snapshot!.ReadBytes(new(0, s.Document.Snapshot.Length));
        await s.View.NavigateAsync(2); await s.View.NextAsync(); var id = s.Document.ActiveMessageId;
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => s.Document.SaveAsync(destination, Token));
        Assert.Contains("message 3", error.Message); Assert.Equal(sentinel, await File.ReadAllBytesAsync(path, Token));
        Assert.True(s.Document.IsDirty); Assert.Equal(id, s.Document.ActiveMessageId);
        var prompted = false;
        Assert.False(await UnsavedChanges.ConfirmAsync(s.Document, _ => { prompted = true; return Task.FromResult(UnsavedChangesDecision.Save); }, _ => Task.FromResult(false)));
        Assert.True(prompted); Assert.True(s.Document.IsDirty);
        await s.View.ActiveView!.PasteExactAsync(() => Task.FromResult<string?>(C)); await s.Document.EditRefresh;
        await s.Document.SaveAsync(destination, Token); Assert.False(s.Document.IsDirty);
        var expected = original.Concat(Encoding.UTF8.GetBytes(framed ? "\v" + C + "\x1c\r" : C)).ToArray();
        Assert.Equal(expected, await File.ReadAllBytesAsync(path, Token)); Assert.Equal(original, s.Document.Snapshot.ReadBytes(new(0, original.Length)));
        MessageDocument reopened;
        using (var saved = File.OpenRead(path)) reopened = await MessageFileReader.OpenFileAsync(s.Manager, "Saved synthetic", saved, Token);
        Assert.Equal(3, reopened.MessageCount); await reopened.NavigateAsync(3, Token); Assert.Equal(C, reopened.EditableText);
        Assert.Null(s.Document.ActiveEntry!.Value.SourceOrdinal); Assert.False(s.Document.HasOriginalMessage);
        s.Document.Undo(); await s.Document.EditRefresh; Assert.True(s.Document.IsDirty);
        s.Document.Undo(); await s.Document.EditRefresh; Assert.Equal(2, s.Document.MessageCount); Assert.True(s.Document.IsDirty);
        s.Document.Redo(); await s.Document.EditRefresh; s.Document.Redo(); await s.Document.EditRefresh; Assert.False(s.Document.IsDirty);
        s.Document.ReplaceWorkingText(C + "NTE*2**SYN-LATER\r");
        await File.WriteAllTextAsync(path, "EXTERNAL SYNTHETIC CHANGE", Token);
        await Assert.ThrowsAsync<IOException>(() => s.Document.SaveAsync(token: Token));
        Assert.Equal("EXTERNAL SYNTHETIC CHANGE", await File.ReadAllTextAsync(path, Token)); Assert.True(s.Document.IsDirty);
    }, framed ? "\uFEFF\v" + A + "\x1c\r\v" + B + "\x1c\r" : "\uFEFF" + A + B);

    [Fact]
    public Task InsertionInvalidatesSearchRetainsQueryAndComparisonIdentityAndRefreshesInspector() => InWindow(async s =>
    {
        using var search = new SearchViewModel(s.Manager, new SearchLibrary(Path.Combine(s.Root, "searches")));
        search.Value = "SYN-C"; await search.Session.StartAsync(new(MessageSearchMode.Text, "SYN"), Token); search.Session.FilterNavigator = true;
        var old = s.Document.ActiveInspection;
        Assert.True(await s.View.InsertMessageAsync(MessageInsertionPosition.Beginning)); var id = s.Document.ActiveMessageId!.Value;
        Assert.Null(search.Session.Results); Assert.Null(s.Document.NavigationMatches); Assert.Equal("SYN-C", search.Value);
        await s.View.ActiveView!.PasteExactAsync(() => Task.FromResult<string?>(C)); await s.Document.EditRefresh;
        Assert.NotSame(old, s.Document.ActiveInspection); Assert.Equal("C", s.Document.Message.Get("PID-1").Raw);
        Assert.Equal(s.Document.Inspection.Fields, s.View.ActiveView.Inspector.ItemsSource);
        var results = await new MessageSearch().SearchAsync(s.Document, new(MessageSearchMode.Field, "C", FieldPath: "PID-1"), token: Token);
        Assert.Equal(id, Assert.Single(results.Matches).MessageId);
        var source = new ComparisonSource(s.Document, id); Assert.True(source.IsAvailable);
        Assert.False(new ComparisonSource(s.Document, id, original: true).IsAvailable);
        s.Document.Undo(); await s.Document.EditRefresh; s.Document.Undo(); await s.Document.EditRefresh;
        Assert.False(source.IsAvailable); Assert.Equal(0, source.Ordinal); Assert.Equal(id, source.MessageId);
        s.Document.Redo(); await s.Document.EditRefresh; s.Document.Redo(); await s.Document.EditRefresh;
        Assert.True(source.IsAvailable); Assert.Equal(1, source.Ordinal); Assert.Equal(id, s.Document.ActiveMessageId);
    });

    [Theory]
    [InlineData(ScriptScope.CurrentMessage)] [InlineData(ScriptScope.WholeDocument)]
    public Task ScriptCapturesUiInsertionAndPreservesAcceptedSnapshotPolicy(ScriptScope scope) => InWindow(async s =>
    {
        await s.View.NextAsync(); await s.View.NextAsync(); var id = s.Document.ActiveMessageId!.Value;
        var execution = new ScriptExecution(s.Manager, new() { StorageRoot = Path.Combine(s.Root, "jobs") });
        await Assert.ThrowsAsync<ScriptRunException>(() => execution.RunAsync(s.Document, new("void Main() {}"), scope, token: Token));
        await s.View.ActiveView!.PasteExactAsync(() => Task.FromResult<string?>(C)); await s.Document.EditRefresh;
        Task insertion = Task.CompletedTask; var changed = false;
        var result = await execution.RunAsync(s.Document, new("void Main() {}"), scope,
            new InlineProgress<ScriptProgress>(p =>
            {
                if (changed || p.Phase != "Compiling") return;
                changed = true; insertion = s.View.InsertMessageAsync(MessageInsertionPosition.End);
            }), Token);
        await insertion; Assert.True(changed); Assert.True(result.SourceChangedDuringRun);
        Assert.Equal(scope == ScriptScope.CurrentMessage ? 1 : 3, result.MessageSources.Count);
        Assert.Contains(result.MessageSources, m => m.InputMessageId == id); Assert.Equal(4, s.Document.MessageCount);
        Assert.DoesNotContain(result.Input!.Messages, m => m.MessageId == s.Document.ActiveMessageId);
        Assert.Equal("", s.Document.EditableText); Assert.True(s.Document.IsDirty);
    });
    private sealed class InlineProgress<T>(Action<T> callback) : IProgress<T> { public void Report(T value) => callback(value); }

    [Fact]
    public Task OversizedOrMultipleMessagePasteRejectsAtomicallyWithoutRemovingInsertionHistory() => InWindow(async s =>
    {
        Assert.True(await s.View.InsertMessageAsync(MessageInsertionPosition.End)); var id = s.Document.ActiveMessageId;
        var version = s.Document.ContentVersion;
        await s.View.ActiveView!.PasteExactAsync(() => Task.FromResult<string?>(new string('Q', MessagePresentation.MaximumEditorCharacters + 1)));
        Assert.Equal("", s.Document.EditableText); Assert.Equal(version, s.Document.ContentVersion); Assert.Equal(id, s.Document.ActiveMessageId);
        await s.View.ActiveView.PasteExactAsync(() => Task.FromResult<string?>(A + B));
        Assert.Equal("", s.Document.EditableText); Assert.Equal("", s.View.ActiveView.Editor.Text); Assert.Equal(version, s.Document.ContentVersion);
        s.Document.Undo(); await s.Document.EditRefresh; Assert.Equal(2, s.Document.MessageCount); Assert.False(s.Document.IsDirty);
        s.Document.Redo(); await s.Document.EditRefresh; Assert.Equal(id, s.Document.ActiveMessageId);
        await s.View.ActiveView.PasteExactAsync(() => Task.FromResult<string?>(C)); await s.Document.EditRefresh; Assert.Equal(C, s.Document.EditableText);
        Assert.False(s.View.ActiveView.Editor.Document.UndoStack.CanUndo);
    });

    [Fact]
    public Task AppendingFromLargeSourceBackedMessageKeepsInsertionBoundedAndStreamsOriginalBytes() => InWindow(async s =>
    {
        await s.View.NavigateAsync(2); Assert.NotNull(s.Document.Source); Assert.Null(s.Document.MaterializedMessage);
        var length = s.Document.Snapshot!.Length; var hash = System.Security.Cryptography.SHA256.HashData(
            Encoding.UTF8.GetBytes(A + B + "OBX|1|TX|||" + new string('Q', 16 * 1024 * 1024) + "\r"));
        var allocated = GC.GetTotalAllocatedBytes(); await s.View.NextAsync();
        Assert.True(GC.GetTotalAllocatedBytes() - allocated < 8 * 1024 * 1024);
        Assert.Equal(3, s.Document.MessageCount); Assert.Equal("", s.Document.EditableText); Assert.Null(s.Document.Source);
        await s.View.ActiveView!.PasteExactAsync(() => Task.FromResult<string?>(C)); await s.Document.EditRefresh;
        var path = Path.Combine(s.Root, "large-inserted.hl7"); await s.Document.SaveAsync(path, Token);
        await using var saved = File.OpenRead(path); using var prefix = System.Security.Cryptography.IncrementalHash.CreateHash(System.Security.Cryptography.HashAlgorithmName.SHA256);
        var bytes = new byte[65536]; long remaining = length;
        while (remaining > 0) { var read = await saved.ReadAsync(bytes.AsMemory(0, (int)Math.Min(bytes.Length, remaining)), Token); Assert.True(read > 0); prefix.AppendData(bytes, 0, read); remaining -= read; }
        Assert.Equal(hash, prefix.GetHashAndReset()); Assert.Equal(length + Encoding.UTF8.GetByteCount(C), saved.Length); Assert.False(s.Document.IsDirty);
    }, A + B + "OBX|1|TX|||" + new string('Q', 16 * 1024 * 1024) + "\r");

    [Theory]
    [InlineData("mode")] [InlineData("text")] [InlineData("caret")] [InlineData("reattach")]
    public Task DelayedPasteDoesNotOverwriteNewerInputIntent(string change) => InWindow(async s =>
    {
        Assert.True(await s.View.InsertMessageAsync(MessageInsertionPosition.End));
        var view = s.View.ActiveView!; var ready = new TaskCompletionSource<string?>(); var pending = view.PasteExactAsync(() => ready.Task);
        if (change == "mode") s.Document.IsEditMode = false;
        else if (change == "text") s.Document.ReplaceWorkingText("SYN-NEWER");
        else if (change == "caret") { view.Editor.Select(0, 0); await view.PasteExactAsync(() => Task.FromResult<string?>("SYN-NEWER")); }
        else { s.Window.Content = null; s.Window.Content = s.View; }
        ready.SetResult(C); await pending;
        Assert.Equal(change is "text" or "caret" ? "SYN-NEWER" : "", s.Document.EditableText);
    });

    [Theory]
    [InlineData(99999)] [InlineData(100000)]
    public Task NearCapacityUiInsertionAndCapabilityChecksRemainBoundedAndAtomic(int count) => InWindow(async s =>
    {
        if (count == 99999) Assert.True(await s.View.InsertMessageAsync(MessageInsertionPosition.Beginning));
        Assert.Equal(100000, s.Document.MessageCount); var selected = s.Document.ActiveMessageId; var version = s.Document.ContentVersion;
        var allocated = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        for (var i = 0; i < 10000; i++) Assert.NotNull(s.Document.InsertionUnavailableReason);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - allocated < 1024 * 1024);
        Assert.False(s.View.AddMessage.IsEnabled); Assert.All(s.View.InsertionCommands.Values, c => Assert.False(c.CanExecute(null)));
        s.View.Focusable = true; Assert.True(s.View.Focus()); var focus = s.Window.FocusManager!.GetFocusedElement();
        Assert.False(await s.View.InsertMessageAsync(MessageInsertionPosition.End)); Assert.Same(focus, s.Window.FocusManager.GetFocusedElement());
        Assert.Equal(selected, s.Document.ActiveMessageId); Assert.Equal(version, s.Document.ContentVersion);
        await s.View.NavigateAsync(100000); Assert.False(s.View.NextMessageCommand.CanExecute(null)); await s.View.NextAsync();
        Assert.Equal(100000, s.Document.MessageCount); Assert.Contains("100,000", s.View.NavigationStatus);
        Assert.Null(s.Document.Source); Assert.NotNull(s.Document.MaterializedMessage);
        TestContext.Current.TestOutputHelper!.WriteLine($"capacity={count}; capabilityChecksMs={watch.Elapsed.TotalMilliseconds:F3}");
    }, string.Concat(Enumerable.Repeat(A, count)));

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DelayedPasteCannotRetargetAfterNavigationOrViewReplacement(bool replaceView) => InWindow(async s =>
    {
        Assert.True(await s.View.InsertMessageAsync(MessageInsertionPosition.End)); var id = s.Document.ActiveMessageId;
        var ready = new TaskCompletionSource<string?>(); var old = s.View.ActiveView!; var paste = old.PasteExactAsync(() => ready.Task);
        if (replaceView) { s.Window.Content = null; s.Window.Content = MessageViewer.Create(s.Document); }
        else await s.View.NavigateAsync(1);
        ready.SetResult(C); await paste; Assert.Equal("", old.Editor.Text);
        await s.Document.NavigateAsync(id!.Value, Token); Assert.Equal("", s.Document.EditableText);
        Assert.Equal(3, s.Document.MessageCount);
    });
}
