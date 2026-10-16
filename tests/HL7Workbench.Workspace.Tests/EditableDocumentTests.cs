using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Tests;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class EditableDocumentTests
{
    private const string First = "MSH|^~\\&|SYN\rPID|Alpha^Beta\r";
    private const string Second = "MSH|^~\\&|SYN2\rPID|Other\r";
    private sealed class Session : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-edit-test-" + Guid.NewGuid().ToString("N"));
        public DocumentManager Manager { get; } = new();
        public Session() => Directory.CreateDirectory(Root);
        public string PathFor(string name) => Path.Combine(Root, name);
        public async Task<MessageDocument> Open(string text = First, Encoding? encoding = null, bool bom = false)
        {
            encoding ??= new UTF8Encoding(false, true);
            using var source = new MemoryStream((bom ? encoding.GetPreamble() : []).Concat(encoding.GetBytes(text)).ToArray());
            return await MessageFileReader.OpenFileAsync(Manager, "Synthetic.hl7", source, TestContext.Current.CancellationToken);
        }
        public async ValueTask DisposeAsync()
        {
            var documents = Manager.Documents.ToArray(); Manager.Dispose();
            await Task.WhenAll(documents.Select(d => d.Disposal)); Directory.Delete(Root, true);
        }
    }
    [Fact]
    public async Task PreferencesPersistOffByDefaultAndApplyOnlyAtOpen()
    {
        await using var s = new Session();
        var path = s.PathFor("preferences.json");
        Assert.False(EditorPreferences.Load(path).StartInEditMode);
        var first = await s.Open(); Assert.False(first.IsEditMode);
        new EditorPreferences(true).Save(path); s.Manager.Preferences = EditorPreferences.Load(path);
        var second = await s.Open(); Assert.True(second.IsEditMode); Assert.False(first.IsEditMode);
        new EditorPreferences().Save(path); s.Manager.Preferences = EditorPreferences.Load(path);
        var third = await s.Open(); Assert.False(third.IsEditMode); Assert.True(second.IsEditMode);
        first.IsEditMode = true; s.Manager.Activate(first.Id); s.Manager.Activate(third.Id);
        Assert.True(first.IsEditMode); Assert.False(third.IsEditMode);
        Assert.DoesNotContain("Synthetic", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task DirtyUndoRedoSavePointAndReadOnlyToggleAreDocumentOwned()
    {
        await using var s = new Session(); var doc = await s.Open();
        Assert.Throws<InvalidOperationException>(() => doc.ReplaceWorkingText(First + "NTE|1"));
        doc.IsEditMode = true; doc.ReplaceWorkingText(First); Assert.False(doc.IsDirty); Assert.False(doc.CanUndo);
        var edited = First.Replace("Alpha", "Changed", StringComparison.Ordinal);
        doc.ReplaceWorkingText(edited); Assert.True(doc.IsDirty); Assert.EndsWith(" *", doc.DisplayTitle);
        Assert.Null(doc.ActiveInspection); Assert.Null(doc.MaterializedMessage);
        await doc.EditRefresh; Assert.Equal("Changed", doc.Message.Get("PID-1.1").Raw);
        doc.IsEditMode = false; doc.Undo(); Assert.Equal(edited, doc.WorkingText); Assert.True(doc.IsDirty);
        doc.IsEditMode = true; doc.Undo(); Assert.False(doc.IsDirty); Assert.True(doc.CanRedo); await doc.EditRefresh;
        doc.Redo(); await doc.EditRefresh; Assert.True(doc.IsDirty);
        await doc.SaveAsync(s.PathFor("saved.hl7"), TestContext.Current.CancellationToken);
        Assert.False(doc.IsDirty); Assert.True(doc.CanUndo);
        doc.Undo(); await doc.EditRefresh; Assert.True(doc.IsDirty);
        doc.Redo(); await doc.EditRefresh; Assert.False(doc.IsDirty);
        doc.Undo(); doc.ReplaceWorkingText(First.Replace("Alpha", "Branch", StringComparison.Ordinal));
        Assert.False(doc.CanRedo); await doc.EditRefresh;
    }
    [Fact]
    public async Task WorkingTextAndHistorySurviveMessageAndDocumentNavigation()
    {
        await using var s = new Session(); var doc = await s.Open(First + Second); doc.IsEditMode = true;
        doc.ReplaceWorkingText(First.Replace("Alpha", "One", StringComparison.Ordinal));
        await doc.NavigateAsync(2, TestContext.Current.CancellationToken);
        doc.ReplaceWorkingText(Second.Replace("Other", "Two", StringComparison.Ordinal)); await doc.EditRefresh;
        var other = await s.Open(); s.Manager.Activate(doc.Id);
        Assert.True(doc.IsEditMode); Assert.False(other.IsEditMode); Assert.Equal("Two", doc.Message.Get("PID-1").Raw);
        await doc.NavigateAsync(1, TestContext.Current.CancellationToken); Assert.Equal("One", doc.Message.Get("PID-1.1").Raw);
        doc.Undo(); await doc.EditRefresh; Assert.Equal(First, doc.WorkingText); Assert.True(doc.IsDirty);
        await doc.NavigateAsync(2, TestContext.Current.CancellationToken); doc.Undo(); await doc.EditRefresh; Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task RapidEditsClearOldCoordinatesAndOnlyLatestCoreProjectionPublishes()
    {
        await using var s = new Session(); var doc = await s.Open(); doc.IsEditMode = true;
        var old = doc.Inspection; old.SelectOffset(First.IndexOf("Beta", StringComparison.Ordinal));
        for (var i = 1; i <= 20; i++) doc.ReplaceWorkingText(First.Replace("Alpha", new string('X', i), StringComparison.Ordinal));
        Assert.Null(doc.ActiveInspection); await doc.EditRefresh;
        Assert.NotSame(old, doc.Inspection);
        var offset = doc.WorkingText!.IndexOf("Beta", StringComparison.Ordinal);
        Assert.Equal("PID[1]-1[1].2", doc.Inspection.ResolveOffset(offset)!.Path.ToString());
        Assert.Equal(offset, doc.Inspection.ResolveOffset(offset)!.Range64!.Value.Offset);
        Assert.Equal(new string('X', 20), doc.Message.Get("PID-1.1").Raw);
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)]
    public async Task SaveSplicesOnlyPayloadAndRetainsBomFramesWrappersEndingsAndOtherMessages(int codePage)
    {
        await using var s = new Session();
        var encoding = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        const string payload = "MSH*$%!@*SYN\r\nPID*é😀$Beta\nNTE*1**Tail\r";
        var before = encoding.GetPreamble().Concat(encoding.GetBytes("FHS|SYN\r\n")).Concat(new byte[] { 0x0B }).ToArray();
        var after = new byte[] { 0x1C, 0x0D }.Concat(encoding.GetBytes(Second + "FTS|1\r\n")).ToArray();
        var original = before.Concat(encoding.GetBytes(payload)).Concat(after).ToArray();
        using var input = new MemoryStream(original);
        var doc = await MessageFileReader.OpenFileAsync(s.Manager, "Synthetic.hl7", input, TestContext.Current.CancellationToken);
        Assert.Equal(2, doc.MessageCount); doc.IsEditMode = true;
        using var raw = new RawInspectorSession(doc);
        var changed = payload.Replace("é😀", "é🧪Longer", StringComparison.Ordinal);
        doc.ReplaceWorkingText(changed); Assert.True(raw.IsClosed); await doc.EditRefresh;
        Assert.Throws<InvalidOperationException>(() => new RawInspectorSession(doc));
        var mapped = await doc.ReadTextAsync(source => Tuple.Create(source.Bytes(new(0, 3))), TestContext.Current.CancellationToken);
        Assert.Null(mapped!.Item1);
        var destination = s.PathFor("saved.hl7");
        await doc.SaveAsync(destination, TestContext.Current.CancellationToken);
        Assert.Equal(before.Concat(encoding.GetBytes(changed)).Concat(after).ToArray(), await File.ReadAllBytesAsync(destination, TestContext.Current.CancellationToken));
        Assert.False(doc.IsDirty); Assert.True(doc.HasWorkingSourceChanges);
        Assert.Equal(original, doc.Snapshot!.ReadBytes(new(0, original.Length)));
        doc.Undo(); await doc.EditRefresh; await doc.SaveAsync(token: TestContext.Current.CancellationToken);
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(12000)] [InlineData(12001)]
    public async Task NoOpSaveAsRetainsEveryByteIncludingMalformedTextAndUnsupportedEditEncodings(int codePage)
    {
        await using var s = new Session(); var encoding = Encoding.GetEncoding(codePage);
        var doc = await s.Open("MSH|^~\\&|SYN\r\nBAD line\n\rPID||\"\"|é😀|\\broken\r\n", encoding, true);
        var path = s.PathFor("unchanged.hl7"); await doc.SaveAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length)), await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.False(doc.IsDirty); Assert.Null(doc.WorkingText);
        if (codePage is 12000 or 12001) { doc.IsEditMode = true; Assert.False(doc.CanEditText); Assert.Contains("encoding", doc.EditingUnavailableReason!); }
    }
    [Fact]
    public async Task SaveAndSaveAsRetainOriginalOverlayAcrossRepeatedSavesAndUndo()
    {
        await using var s = new Session(); var doc = await s.Open(); doc.IsEditMode = true;
        var path = s.PathFor("first.hl7");
        doc.ReplaceWorkingText(First.Replace("Alpha", "One", StringComparison.Ordinal)); await doc.SaveAsync(path, TestContext.Current.CancellationToken);
        doc.ReplaceWorkingText(First.Replace("Alpha", "Two", StringComparison.Ordinal)); await doc.SaveAsync(token: TestContext.Current.CancellationToken);
        var another = s.PathFor("second.hl7"); await doc.SaveAsync(another, TestContext.Current.CancellationToken);
        Assert.Equal(await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken), await File.ReadAllBytesAsync(another, TestContext.Current.CancellationToken));
        doc.Undo(); await doc.SaveAsync(token: TestContext.Current.CancellationToken);
        Assert.Contains("One", await File.ReadAllTextAsync(another, TestContext.Current.CancellationToken));
        Assert.Contains("Two", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        doc.Undo(); await doc.SaveAsync(token: TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(First), await File.ReadAllBytesAsync(another, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task SaveFailureAndCancellationKeepDestinationSavePointHistoryAndDraft()
    {
        await using var s = new Session(); var doc = await s.Open(); doc.IsEditMode = true;
        doc.ReplaceWorkingText(First.Replace("Alpha", "Retained", StringComparison.Ordinal));
        var path = s.PathFor("locked.hl7"); await File.WriteAllTextAsync(path, "SYNTHETIC EXISTING", TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<IOException>(() => doc.SaveAsync(s.PathFor("missing/dest.hl7"), TestContext.Current.CancellationToken));
        Assert.Null(doc.SavePath); Assert.True(doc.IsDirty); Assert.True(doc.CanUndo); Assert.False(doc.IsSaving);
        var consent = await FileSaveDestination.CaptureAsync(path, TestContext.Current.CancellationToken);
        using (var held = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => doc.SaveAsync(consent, TestContext.Current.CancellationToken));
        Assert.Equal("SYNTHETIC EXISTING", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => doc.SaveAsync(path, cancel.Token));
        Assert.True(doc.IsDirty); Assert.Null(doc.SavePath); Assert.False(doc.IsSaving);
        Assert.Empty(Directory.GetFiles(s.Root, ".hl7-save-*"));
        await doc.SaveAsync(await FileSaveDestination.CaptureAsync(path, TestContext.Current.CancellationToken), TestContext.Current.CancellationToken); Assert.False(doc.IsDirty);
        Assert.Contains("Retained", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData("MSH|^~\\&|SYN\rPID|Changed")]
    [InlineData("MSH|^~\\&|SYN\rMSH|^~\\&|NEW\r")]
    [InlineData("PID|Only\r")]
    [InlineData("MSH|^~\\&|SYN\rBTS|1\r")]
    [InlineData("MSH|^~\\&|SYN\rPID|\vFraming\r")]
    public async Task BoundaryChangingDraftsCannotPublishOverAdjacentMessages(string draft)
    {
        await using var s = new Session(); var doc = await s.Open(First + Second); doc.IsEditMode = true; doc.ReplaceWorkingText(draft);
        var path = s.PathFor("retained.hl7"); await File.WriteAllTextAsync(path, "SYNTHETIC EXISTING", TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => doc.SaveAsync(path, TestContext.Current.CancellationToken));
        Assert.True(doc.IsDirty); Assert.Equal(draft, doc.WorkingText);
        Assert.Equal("SYNTHETIC EXISTING", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        await doc.NavigateAsync(2, TestContext.Current.CancellationToken); Assert.Equal("Other", doc.Message.Get("PID-1").Raw);
    }
    [Fact]
    public async Task InvalidDraftRemainsEditableAndSurvivesNavigationWithoutStaleInspection()
    {
        await using var s = new Session(); var doc = await s.Open(First + Second); doc.IsEditMode = true;
        var draft = "MSH|^~\\&|SYN\rPID|" + new string('X', MessageDocument.MaximumEditableLineCharacters) + "\r";
        doc.ReplaceWorkingText(draft); await doc.EditRefresh;
        Assert.NotNull(doc.EditError); Assert.Null(doc.ActiveInspection); Assert.True(doc.CanEditText);
        await Assert.ThrowsAsync<Hl7LimitException>(() => doc.SaveAsync(s.PathFor("invalid.hl7"), TestContext.Current.CancellationToken));
        await doc.NavigateAsync(2, TestContext.Current.CancellationToken); await doc.NavigateAsync(1, TestContext.Current.CancellationToken);
        Assert.Equal(draft, doc.EditableText); Assert.Null(doc.ActiveInspection); Assert.True(doc.CanEditText);
        doc.Undo(); await doc.EditRefresh; Assert.Equal(First, doc.Message.RawText); Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task StrictEncodingRejectsUnrepresentableDraftWithoutSubstitutingBytes()
    {
        using var manager = new DocumentManager();
        var doc = MessageFileReader.Open(manager, "Synthetic", Hl7Parser.Parse(Encoding.Latin1.GetBytes("PID|é\r"), new() { Encoding = Encoding.Latin1 }));
        doc.IsEditMode = true; doc.ReplaceWorkingText("PID|Ω\r"); await doc.EditRefresh;
        Assert.NotNull(doc.EditError); Assert.True(doc.IsDirty);
        await using var s = new Session();
        await Assert.ThrowsAsync<EncoderFallbackException>(() => doc.SaveAsync(s.PathFor("unrepresentable.hl7"), TestContext.Current.CancellationToken));
        doc.Undo(); await doc.EditRefresh; Assert.Equal("PID|é\r", doc.Message.RawText);
    }
    [Fact]
    public async Task LargeSourceCannotBeMaterializedByEditModeAndOtherMessageCanStillBeEditedAndSaved()
    {
        await using var s = new Session();
        var original = First + "OBX|1|TX|||" + new string('Q', 2 * 1024 * 1024) + "\r";
        var doc = await s.Open(original + Second); doc.IsEditMode = true;
        Assert.False(doc.CanEditText); Assert.NotNull(doc.Source); Assert.Null(doc.WorkingText); Assert.NotNull(doc.Presentation);
        Assert.Throws<InvalidOperationException>(() => doc.ReplaceWorkingText("PID|No"));
        await doc.NavigateAsync(2, TestContext.Current.CancellationToken); Assert.True(doc.CanEditText);
        doc.ReplaceWorkingText(Second.Replace("Other", "Edited", StringComparison.Ordinal)); await doc.SaveAsync(s.PathFor("large.hl7"), TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(original + Second.Replace("Other", "Edited", StringComparison.Ordinal)), await File.ReadAllBytesAsync(s.PathFor("large.hl7"), TestContext.Current.CancellationToken));
        await doc.NavigateAsync(1, TestContext.Current.CancellationToken); Assert.NotNull(doc.Source); Assert.Null(doc.WorkingText);
    }
    [Theory]
    [InlineData(UnsavedChangesDecision.Cancel, false)]
    [InlineData(UnsavedChangesDecision.Discard, true)]
    [InlineData(UnsavedChangesDecision.Save, true)]
    public async Task CloseDecisionsHonorCancelDiscardAndSuccessfulSave(UnsavedChangesDecision choice, bool expected)
    {
        await using var s = new Session(); var doc = await s.Open(); doc.IsEditMode = true; doc.ReplaceWorkingText(First + "NTE|1\r");
        Assert.False(s.Manager.Close(doc.Id)); Assert.Contains(doc, s.Manager.Documents);
        var allowed = await UnsavedChanges.ConfirmAsync(doc, _ => Task.FromResult(choice), async d => { await d.SaveAsync(s.PathFor("close.hl7"), TestContext.Current.CancellationToken); return true; });
        Assert.Equal(expected, allowed); Assert.Contains(doc, s.Manager.Documents);
        if (choice == UnsavedChangesDecision.Cancel) Assert.True(doc.IsDirty);
    }
    [Fact]
    public async Task CloseSaveDismissalAndChangedDraftNeverAuthorizeDiscard()
    {
        await using var s = new Session(); var doc = await s.Open(); doc.IsEditMode = true; doc.ReplaceWorkingText(First + "NTE|1\r");
        Assert.False(await UnsavedChanges.ConfirmAsync(doc, _ => Task.FromResult(UnsavedChangesDecision.Save), _ => Task.FromResult(false)));
        Assert.False(await UnsavedChanges.ConfirmAsync(doc, d => { d.ReplaceWorkingText(First + "NTE|2\r"); return Task.FromResult(UnsavedChangesDecision.Discard); }, _ => Task.FromResult(true)));
        Assert.True(doc.IsDirty); Assert.Contains("NTE|2", doc.WorkingText!);
    }
    [Fact]
    public async Task OversizedPasteAndAggregateHistoryBudgetsRejectAtomicallyAndUndoStillWorks()
    {
        await using var s = new Session(); var doc = await s.Open(); doc.IsEditMode = true;
        Assert.Throws<Hl7LimitException>(() => doc.ReplaceWorkingText(new string('X', MessagePresentation.MaximumEditorCharacters + 1)));
        Assert.False(doc.IsDirty); Assert.Null(doc.WorkingText);
        // Many short lines stay within the renderer's accepted line budget. Replacing
        // the full text repeatedly consumes retained delta storage, not source-file size.
        var a = First + string.Concat(Enumerable.Repeat("NTE|1||" + new string('A', 8000) + "\r", 60));
        var b = a.Replace('A', 'B'); string? last = null; var rejected = false;
        for (var i = 0; i < 50; i++)
        {
            var candidate = i % 2 == 0 ? a : b;
            try { doc.ReplaceWorkingText(candidate); last = candidate; }
            catch (Hl7LimitException error) { Assert.Equal("RetainedEditHistory", error.Limit); rejected = true; break; }
        }
        Assert.True(rejected); Assert.Equal(last, doc.WorkingText); Assert.True(doc.CanUndo);
        doc.Undo(); await doc.EditRefresh; Assert.Null(doc.EditError);
        await doc.SaveAsync(s.PathFor("bounded.hl7"), TestContext.Current.CancellationToken);
        Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task SaveFreezesWorkingEditsAndNavigationWhileReadersDrain()
    {
        await using var s = new Session(); var doc = await s.Open(First + Second); doc.IsEditMode = true;
        doc.ReplaceWorkingText(First.Replace("Alpha", "Saved", StringComparison.Ordinal)); await doc.EditRefresh;
        using var release = new ManualResetEventSlim(); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var read = doc.ReadTextAsync(source => { entered.SetResult(); release.Wait(TestContext.Current.CancellationToken); return source.Read(new(0, 3)); }, TestContext.Current.CancellationToken);
        await entered.Task;
        try
        {
            var saving = doc.SaveAsync(s.PathFor("drained.hl7"), TestContext.Current.CancellationToken);
            Assert.True(doc.IsSaving); Assert.False(doc.CanEditText);
            Assert.Throws<InvalidOperationException>(() => doc.ReplaceWorkingText(First));
            doc.Undo(); await doc.NavigateAsync(2, TestContext.Current.CancellationToken); Assert.Equal(1, doc.MessageNumber);
            Assert.False(await UnsavedChanges.ConfirmAsync(doc, _ => throw new InvalidOperationException(), _ => Task.FromResult(true)));
            release.Set(); await read; await saving;
            Assert.False(doc.IsSaving); Assert.False(doc.IsDirty); Assert.Contains("Saved", doc.WorkingText!);
        }
        finally { release.Set(); }
    }
    [Fact]
    public async Task FileStreamSaveUsesOriginalDestinationAndSnapshotRemainsImmutable()
    {
        await using var s = new Session(); var path = s.PathFor("original.hl7");
        await File.WriteAllBytesAsync(path, Encoding.UTF8.GetBytes(First), TestContext.Current.CancellationToken);
        MessageDocument doc;
        await using (var input = File.OpenRead(path)) doc = await MessageFileReader.OpenFileAsync(s.Manager, "original.hl7", input, TestContext.Current.CancellationToken);
        Assert.Equal(path, doc.SavePath); doc.IsEditMode = true; doc.ReplaceWorkingText(First.Replace("Alpha", "Saved", StringComparison.Ordinal));
        await doc.SaveAsync(token: TestContext.Current.CancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(First), doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length)));
        Assert.Contains("Saved", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<IOException>(() => doc.SaveAsync(Path.Combine(doc.Snapshot.DirectoryPath, "source.bin"), TestContext.Current.CancellationToken));
        Assert.False(doc.IsDirty); Assert.Equal(path, doc.SavePath);
    }
    [Fact]
    public async Task UndecodableSnapshotCanBeCopiedWithoutEnablingEditing()
    {
        await using var s = new Session(); using var source = new MemoryStream(new byte[] { 0xFF, 0xFE, 0x42 });
        var doc = await MessageFileReader.OpenFileAsync(s.Manager, "Synthetic", source, TestContext.Current.CancellationToken);
        doc.IsEditMode = true; Assert.False(doc.CanEditText);
        var path = s.PathFor("original.dat"); await doc.SaveAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal(new byte[] { 0xFF, 0xFE, 0x42 }, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task DisposalDuringPendingEditRefreshReleasesSnapshotAfterWorkDrains()
    {
        await using var s = new Session(); var doc = await s.Open(); var directory = doc.Snapshot!.DirectoryPath;
        doc.IsEditMode = true; doc.ReplaceWorkingText(First + "NTE|1\r"); var refresh = doc.EditRefresh;
        Assert.True(s.Manager.Close(doc.Id, discardChanges: true)); await refresh; await doc.Disposal;
        Assert.False(Directory.Exists(directory)); Assert.Empty(s.Manager.Documents);
    }
    [Fact]
    public async Task LargeFileSaveStreamsUntouchedPayloadWithoutFileSizedAllocations()
    {
        var token = TestContext.Current.CancellationToken;
        await using var s = new Session();
        var original = await LargeSyntheticFile.CreateAsync(64L * 1024 * 1024, token);
        try
        {
            MessageDocument doc;
            await using (var input = File.OpenRead(original)) doc = await MessageFileReader.OpenFileAsync(s.Manager, "Synthetic", input, token);
            Assert.False(doc.CanEditText); await doc.NavigateAsync(2, token); doc.IsEditMode = true;
            doc.ReplaceWorkingText(doc.Message.RawText + "NTE|1||Synthetic edit\r"); await doc.EditRefresh;
            var path = s.PathFor("large-saved.hl7"); var allocated = GC.GetTotalAllocatedBytes();
            await doc.SaveAsync(path, token);
            Assert.True(GC.GetTotalAllocatedBytes() - allocated < 16L * 1024 * 1024, "Saving must not allocate a file-sized buffer.");
            await using var output = File.OpenRead(path);
            var first = doc.Index!.Messages[1].PayloadRange.Offset;
            var expected = new byte[65536]; var actual = new byte[65536];
            for (long offset = 0; offset < first;)
            {
                var length = (int)Math.Min(expected.Length, first - offset);
                doc.Snapshot!.Read(new(offset, length), expected.AsSpan(0, length));
                await output.ReadExactlyAsync(actual.AsMemory(0, length), token);
                Assert.Equal(expected.AsSpan(0, length).ToArray(), actual.AsSpan(0, length).ToArray()); offset += length;
            }
            using var tail = new StreamReader(output, Encoding.UTF8);
            Assert.Equal(doc.WorkingText, await tail.ReadToEndAsync(token));
        }
        finally { File.Delete(original); }
    }
    [Fact]
    public async Task HistoryEntryBudgetPreservesExistingWorkAndAllowsUndo()
    {
        await using var s = new Session(); var doc = await s.Open(); doc.IsEditMode = true;
        for (var i = 0; i < MessageDocument.MaximumHistoryEntries; i++) doc.ReplaceWorkingText(First + $"NTE|{i}\r");
        var current = doc.WorkingText;
        var error = Assert.Throws<Hl7LimitException>(() => doc.ReplaceWorkingText(First + "NTE|REFUSED\r"));
        Assert.Equal("RetainedEditHistory", error.Limit); Assert.Equal(current, doc.WorkingText);
        doc.Undo(); await doc.EditRefresh; Assert.NotEqual(current, doc.WorkingText); Assert.True(doc.CanRedo);
        doc.ReplaceWorkingText(First + "NTE|BRANCH\r"); await doc.EditRefresh; Assert.False(doc.CanRedo);
    }
    [Fact]
    public async Task SaveActivityNeverRetainsWorkingTextFilenamesPathsOrExceptionMessages()
    {
        await using var s = new Session(); var doc = await s.Open(); doc.IsEditMode = true;
        doc.ReplaceWorkingText(First.Replace("Alpha", "PRIVATE-SYNTHETIC-TOKEN", StringComparison.Ordinal));
        await doc.SaveAsync(s.PathFor("PRIVATE-SYNTHETIC-FILENAME.hl7"), TestContext.Current.CancellationToken);
        await Assert.ThrowsAnyAsync<IOException>(() => doc.SaveAsync(s.PathFor("missing/PRIVATE-SYNTHETIC-FILENAME.hl7"), TestContext.Current.CancellationToken));
        var entries = s.Manager.Activity.Snapshot();
        Assert.Contains(entries, e => e.Kind == ActivityKind.SaveCompleted); Assert.Equal(ActivityKind.SaveFailed, entries[^1].Kind);
        var retained = System.Text.Json.JsonSerializer.Serialize(entries) + string.Join("\n", entries);
        Assert.DoesNotContain("PRIVATE-SYNTHETIC", retained); Assert.DoesNotContain(s.Root, retained);
    }
    [Fact]
    public async Task OriginalValueExportCannotOverwriteSaveAsDestination()
    {
        await using var s = new Session(); var doc = await s.Open();
        var path = s.PathFor("saved.hl7"); await doc.SaveAsync(path, TestContext.Current.CancellationToken);
        using var raw = new RawInspectorSession(doc, new(0, 3));
        await Assert.ThrowsAsync<IOException>(() => raw.ExportAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(Encoding.UTF8.GetBytes(First), await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }
    [Theory]
    [InlineData(1200, '\u0D1C')]
    [InlineData(1201, '\u1C0D')]
    public async Task EncodedWideCodeUnitsCannotIntroduceHiddenMllpBoundaries(int codePage, char collision)
    {
        await using var s = new Session(); var encoding = Encoding.GetEncoding(codePage);
        var original = encoding.GetPreamble().Concat(new byte[] { 0x0B }).Concat(encoding.GetBytes(First)).Concat(new byte[] { 0x1C, 0x0D }).ToArray();
        using var source = new MemoryStream(original);
        var doc = await MessageFileReader.OpenFileAsync(s.Manager, "Synthetic", source, TestContext.Current.CancellationToken);
        doc.IsEditMode = true; doc.ReplaceWorkingText(First.Replace("Alpha", "Alpha" + collision, StringComparison.Ordinal)); await doc.EditRefresh;
        Assert.NotNull(doc.ActiveInspection);
        await Assert.ThrowsAsync<InvalidOperationException>(() => doc.SaveAsync(s.PathFor("unsafe.hl7"), TestContext.Current.CancellationToken));
        Assert.True(doc.IsDirty); Assert.False(File.Exists(s.PathFor("unsafe.hl7")));
        doc.Undo(); doc.ReplaceWorkingText(First.Replace("Alpha", "Safe", StringComparison.Ordinal));
        await doc.SaveAsync(s.PathFor("safe.hl7"), TestContext.Current.CancellationToken); Assert.False(doc.IsDirty);
    }
}
