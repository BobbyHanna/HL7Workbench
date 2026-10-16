using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class SaveAcceptanceTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-picker-test-" + Guid.NewGuid().ToString("N"));
    private const string Synthetic = "MSH|^~\\&|SYN\rPID|1||SYN-1||Example^Ada\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private string Target => Path.Combine(root, "synthetic.hl7");
    public SaveAcceptanceTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    private static Task Write(Stream output, CancellationToken token) => output.WriteAsync(Encoding.UTF8.GetBytes("SYNTHETIC NEW"), token).AsTask();
    private static MessageDocument Open(DocumentManager docs)
    {
        var doc = new MessageDocument(Guid.NewGuid().ToString(), "Synthetic.hl7", Hl7Parser.Parse(Synthetic)); docs.Open(doc);
        doc.IsEditMode = true; doc.ReplaceWorkingText(Synthetic.Replace("Ada", "SYNTHETIC", StringComparison.Ordinal)); return doc;
    }
    [Theory]
    [InlineData(true, true, true)] [InlineData(false, true, true)] [InlineData(false, false, false)]
    public async Task PickerPolicyUsesTrustedNativeConsentOrExplicitApplicationConsent(bool native, bool confirm, bool save)
    {
        await File.WriteAllTextAsync(Target, "SYNTHETIC OLD", Token); var prompts = 0;
        var selected = await SaveFilePicker.ChooseAsync(() => Task.FromResult<string?>(Target), native,
            _ => { prompts++; return Task.FromResult(confirm); }, Token);
        Assert.Equal(native ? 0 : 1, prompts); Assert.Equal(save, selected is not null);
        Assert.Equal("SYNTHETIC OLD", await File.ReadAllTextAsync(Target, Token));
        if (selected is not null) await selected.WriteAsync(Write, Token);
        Assert.Equal(save ? "SYNTHETIC NEW" : "SYNTHETIC OLD", await File.ReadAllTextAsync(Target, Token));
        Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Fact]
    public async Task PickerCancellationHasNoConfirmationOrFileChanges()
    {
        await File.WriteAllTextAsync(Target, "SYNTHETIC OLD", Token);
        Assert.Null(await SaveFilePicker.ChooseAsync(() => Task.FromResult<string?>(null), true, _ => throw new InvalidOperationException(), Token));
        Assert.Equal("SYNTHETIC OLD", await File.ReadAllTextAsync(Target, Token)); Assert.Single(Directory.GetFiles(root));
    }
    [Fact]
    public async Task NewPickerDestinationNeedsNoConfirmationButLaterCollisionIsRefused()
    {
        var selected = await SaveFilePicker.ChooseAsync(() => Task.FromResult<string?>(Target), false, _ => throw new InvalidOperationException(), Token);
        await File.WriteAllTextAsync(Target, "SYNTHETIC COLLISION", Token);
        await Assert.ThrowsAnyAsync<IOException>(() => selected!.WriteAsync(Write, Token));
        Assert.Equal("SYNTHETIC COLLISION", await File.ReadAllTextAsync(Target, Token)); Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Fact]
    public async Task DestinationChangedDuringApplicationConfirmationIsRefused()
    {
        await File.WriteAllTextAsync(Target, "SYNTHETIC OLD", Token);
        var selected = await SaveFilePicker.ChooseAsync(() => Task.FromResult<string?>(Target), false, async _ =>
        { await File.WriteAllTextAsync(Target, "SYNTHETIC CONCURRENT", Token); return true; }, Token);
        await Assert.ThrowsAsync<IOException>(() => selected!.WriteAsync(Write, Token));
        Assert.Equal("SYNTHETIC CONCURRENT", await File.ReadAllTextAsync(Target, Token)); Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Fact]
    public async Task CancelingDocumentPickerRetainsDraftAndIdentity()
    {
        using var docs = new DocumentManager(); var doc = Open(docs); await doc.EditRefresh;
        Assert.False(await new DocumentSaveWorkflow(docs).SaveAsync(doc, true, () => Task.FromResult<FileSaveDestination?>(null), Token));
        Assert.True(doc.IsDirty); Assert.Null(doc.SavePath); Assert.Equal("Synthetic.hl7", doc.Title); Assert.True(doc.CanUndo);
        Assert.Empty(Directory.GetFiles(root));
    }
    [Fact]
    public async Task AssociatedSaveDoesNotOpenPickerAndKeepsUndoAndDocumentIdentity()
    {
        using var docs = new DocumentManager(); var doc = Open(docs); await doc.EditRefresh; var workflow = new DocumentSaveWorkflow(docs); var id = doc.Id;
        Assert.True(await workflow.SaveAsync(doc, true, () => Task.FromResult<FileSaveDestination?>(FileSaveDestination.NewFile(Target)), Token));
        doc.ReplaceWorkingText(Synthetic.Replace("Ada", "SYNTHETIC SECOND", StringComparison.Ordinal)); await doc.EditRefresh;
        Assert.True(await workflow.SaveAsync(doc, false, () => throw new InvalidOperationException("Unexpected picker."), Token));
        Assert.Equal(id, doc.Id); Assert.Equal(Target, doc.SavePath); Assert.False(doc.IsDirty); Assert.True(doc.CanUndo);
        Assert.Contains("SYNTHETIC SECOND", await File.ReadAllTextAsync(Target, Token));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PendingPickerCannotSaveClosedOrNewerDocumentAndRejectsConcurrentSave(bool close)
    {
        using var docs = new DocumentManager(); var doc = Open(docs); await doc.EditRefresh;
        var workflow = new DocumentSaveWorkflow(docs); var picker = new TaskCompletionSource<FileSaveDestination?>();
        var pending = workflow.SaveAsync(doc, true, () => picker.Task, Token);
        Assert.False(await workflow.SaveAsync(doc, true, () => throw new InvalidOperationException(), Token));
        if (close) docs.Close(doc.Id, true); else { doc.ReplaceWorkingText(Synthetic); await doc.EditRefresh; }
        picker.SetResult(FileSaveDestination.NewFile(Target)); await Assert.ThrowsAsync<InvalidOperationException>(() => pending);
        Assert.False(File.Exists(Target)); Assert.Null(doc.SavePath);
    }
    [Fact]
    public async Task ChangingActiveDocumentDuringPickerDoesNotRedirectSave()
    {
        using var docs = new DocumentManager(); var doc = Open(docs); await doc.EditRefresh;
        var picker = new TaskCompletionSource<FileSaveDestination?>(); var pending = new DocumentSaveWorkflow(docs).SaveAsync(doc, true, () => picker.Task, Token);
        var other = Open(docs); await other.EditRefresh; Assert.Same(other, docs.ActiveDocument);
        picker.SetResult(FileSaveDestination.NewFile(Target)); Assert.True(await pending);
        Assert.Equal(Target, doc.SavePath); Assert.Null(other.SavePath); Assert.True(other.IsDirty);
    }
    [Theory]
    [InlineData(0, UnsavedChangesDecision.Save)] [InlineData(1, UnsavedChangesDecision.Discard)] [InlineData(2, UnsavedChangesDecision.Cancel)]
    public async Task CompactUnsavedDialogReturnsExistingDecisions(int button, UnsavedChangesDecision expected)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var owner = new Window(); owner.Show(); var dialog = ConfirmationDialog.Unsaved("Save changes to Synthetic.hl7?");
            try
            {
                var result = dialog.ShowDialog<UnsavedChangesDecision>(owner); dialog.UpdateLayout();
                Assert.Equal(SizeToContent.Height, dialog.SizeToContent); Assert.InRange(dialog.Width, 400, 440);
                Assert.InRange(dialog.Bounds.Height, 70, 175); Assert.Equal(400, dialog.MinWidth); Assert.Equal(440, dialog.MaxWidth);
                var buttons = dialog.GetVisualDescendants().OfType<Button>().ToArray(); Assert.Equal(new[] { "Save", "Discard", "Cancel" }, buttons.Select(b => b.Content));
                Assert.True(buttons[2].IsFocused); buttons[button].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(expected, await result);
            }
            finally { dialog.Close(); owner.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DeleteAndOverwriteConfirmationsReturnExplicitButtonResults(bool accept)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var owner = new Window(); owner.Show();
            foreach (var dialog in new[] { ConfirmationDialog.Delete("Synthetic"), ConfirmationDialog.Overwrite("Synthetic.csv") })
            {
                var result = dialog.ShowDialog<bool>(owner); dialog.UpdateLayout();
                var buttons = dialog.GetVisualDescendants().OfType<Button>().ToArray();
                buttons[accept ? 0 : 1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(accept, await result);
            }
            owner.Close(); return true;
        }, Token);
    }
    [Theory]
    [InlineData(false, false)] [InlineData(false, true)] [InlineData(true, false)] [InlineData(true, true)]
    public async Task LongConfirmationTextWrapsInBothThemesAndEscapeOrCloseCancels(bool dark, bool escape)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var theme = dark ? ThemeVariant.Dark : ThemeVariant.Light; var owner = new Window { RequestedThemeVariant = theme }; owner.Show();
            var longName = "Synthetic-" + new string('x', 180) + ".hl7";
            foreach (var script in new[] { false, true })
            {
                var dialog = ConfirmationDialog.Unsaved("Save changes to " + longName + "?", script); dialog.RequestedThemeVariant = theme;
                var result = dialog.ShowDialog<UnsavedChangesDecision>(owner); dialog.UpdateLayout();
                Assert.Equal(SizeToContent.Height, dialog.SizeToContent); Assert.Equal(theme, dialog.ActualThemeVariant);
                var text = dialog.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text!.Contains(longName));
                Assert.Equal(Avalonia.Media.TextWrapping.Wrap, text.TextWrapping); Assert.True(text.Bounds.Height > 25);
                var panel = Assert.IsType<StackPanel>(dialog.Content); Assert.True(dialog.Bounds.Height >= panel.DesiredSize.Height);
                if (escape) dialog.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); else dialog.Close();
                Assert.Equal(UnsavedChangesDecision.Cancel, await result);
            }
            var delete = ConfirmationDialog.Delete(longName); var deletion = delete.ShowDialog<bool>(owner); delete.UpdateLayout();
            Assert.Equal(SizeToContent.Height, delete.SizeToContent); Assert.InRange(delete.Width, 400, 440);
            if (escape) delete.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); else delete.Close();
            Assert.False(await deletion); owner.Close(); return true;
        }, Token);
    }
}
