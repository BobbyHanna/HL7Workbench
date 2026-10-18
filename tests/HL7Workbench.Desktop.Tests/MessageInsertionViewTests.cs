using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed partial class MessageInsertionViewTests
{
    private const string A = "MSH|^~\\&|SYN-A\rPID|A\r";
    private const string B = "MSH|^~\\&|SYN-B\rPID|B\r";
    private const string C = "MSH*$%!@*SYN-C\r\nPID*C\r\nNTE*1**SYN\tTEXT\n";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed record State(DocumentManager Manager, MessageDocument Document, FileDocumentView View, Window Window, string Root);
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
    private static void History(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.Control, key == Key.Z ? PhysicalKey.Z : PhysicalKey.Y, null);
        window.KeyRelease(key, RawInputModifiers.Control, key == Key.Z ? PhysicalKey.Z : PhysicalKey.Y, null);
    }
    private static void Click(State s, Button button)
    {
        Pump(s.Window); var point = button.TranslatePoint(new Avalonia.Point(button.Bounds.Width / 2, button.Bounds.Height / 2), s.Window)!.Value;
        s.Window.MouseMove(point); s.Window.MouseDown(point, MouseButton.Left); s.Window.MouseUp(point, MouseButton.Left); Pump(s.Window);
    }
    private static async Task InWindow(Func<State, Task> test, string text = A + B, bool memory = false)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var doc = memory ? MessageFileReader.Open(manager, "Synthetic memory", Hl7Parser.Parse(text)) :
                await MessageFileReader.OpenFileAsync(manager, "Synthetic.hl7", input, Token);
            var view = Assert.IsType<FileDocumentView>(MessageViewer.Create(doc));
            var window = new Window { Content = view, Width = 1300, Height = 900 }; window.Show();
            DocumentHistoryShortcuts.Attach(window, () => manager.ActiveDocument);
            var root = Path.Combine(Path.GetTempPath(), "hl7-insertion-ui-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try { Pump(window); await test(new(manager, doc, view, window, root)); }
            finally
            {
                window.Close(); var owned = manager.Documents.ToArray(); manager.Dispose(); await Task.WhenAll(owned.Select(d => d.Disposal));
                Directory.Delete(root, true);
            }
            return true;
        }, Token);
    }

    // Called in a fresh process: AvaloniaEdit's default key bindings are static
    // dispatcher-owned objects, as in the permanent raw-input regression harness.
    internal static Task NativePasteCase() => InWindow(async s =>
    {
        var ids = s.Document.Messages.Select(m => m.Id).ToArray(); Assert.False(s.Document.IsEditMode);
        await s.View.NextAsync(); Assert.Equal(ids[1], s.Document.ActiveMessageId); Assert.False(s.Document.IsEditMode);
        var next = s.View.GetVisualDescendants().OfType<Button>().Single(b => b.Content is ">");
        Click(s, next); Assert.Equal(3, s.Document.MessageCount); Assert.True(s.Document.IsEditMode);
        var inserted = s.Document.ActiveMessageId; Assert.Equal("", s.View.ActiveView!.Editor.Text);
        Assert.Same(s.View.ActiveView.Editor.TextArea, s.Window.FocusManager!.GetFocusedElement());
        Click(s, next); Assert.Equal(3, s.Document.MessageCount); Assert.Equal(inserted, s.Document.ActiveMessageId);
        // Next focuses the same blank editor again; paste requires no editor click.
        await s.View.NextAsync(); Pump(s.Window);
        Assert.Same(s.View.ActiveView.Editor.TextArea, s.Window.FocusManager!.GetFocusedElement());
        await s.Window.Clipboard!.SetTextAsync(C);
        s.Window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, null); s.Window.KeyRelease(Key.V, RawInputModifiers.Control, PhysicalKey.V, null);
        await Dispatcher.UIThread.InvokeAsync(() => { }); await s.Document.EditRefresh; Pump(s.Window);
        Assert.Equal(C, s.Document.EditableText); Assert.Equal(C, s.View.ActiveView.Editor.Text);
        Assert.Equal("C", s.Document.Message.Get("PID-1").Raw);
        History(s.Window, Key.Z); await s.Document.EditRefresh; Assert.Equal("", s.Document.EditableText); Assert.Equal(inserted, s.Document.ActiveMessageId);
        History(s.Window, Key.Z); await s.Document.EditRefresh; Assert.Equal(2, s.Document.MessageCount); Assert.Equal(ids[1], s.Document.ActiveMessageId);
        History(s.Window, Key.Y); await s.Document.EditRefresh; Assert.Equal(inserted, s.Document.ActiveMessageId);
        History(s.Window, Key.Y); await s.Document.EditRefresh; Assert.Equal(C, s.Document.EditableText); Assert.False(s.Document.CanRedo);
        await s.View.NextAsync(); Assert.Equal(4, s.Document.MessageCount); Assert.NotEqual(inserted, s.Document.ActiveMessageId);
        await s.View.NavigateAsync(3); Assert.Equal(inserted, s.Document.ActiveMessageId); Assert.Equal(C, s.View.ActiveView.Editor.Text);
        Assert.Equal(ids, s.Document.Messages.Take(2).Select(m => m.Id));
    });

    [Theory]
    [InlineData("")] [InlineData("\r\n\r")] [InlineData(" \t\r\n\u2003")]
    public Task TrailingGuardReadsPendingWorkingDraftEvenWithEditModeOff(string draft) => InWindow(async s =>
    {
        await s.View.NavigateAsync(2); await s.View.NextAsync(); var id = s.Document.ActiveMessageId;
        s.Document.ReplaceWorkingText(draft); s.Document.IsEditMode = false;
        await s.View.NextAsync(); await s.View.NextAsync(); Assert.Equal(3, s.Document.MessageCount); Assert.Equal(id, s.Document.ActiveMessageId);
        s.Document.IsEditMode = true; s.Document.ReplaceWorkingText("MSH");
        await s.View.NextAsync(); Assert.Equal(4, s.Document.MessageCount); Assert.NotEqual(id, s.Document.ActiveMessageId);
    });

    [Theory]
    [InlineData(MessageInsertionPosition.BeforeCurrent, 2)] [InlineData(MessageInsertionPosition.AfterCurrent, 3)]
    [InlineData(MessageInsertionPosition.Beginning, 1)] [InlineData(MessageInsertionPosition.End, 4)]
    public Task AllExplicitPositionsUseLogicalSequenceAndFocusBlankDraft(MessageInsertionPosition position, int expected) => InWindow(async s =>
    {
        var original = s.Document.GetMessageId(2);
        Assert.True(await s.View.InsertMessageAsync(MessageInsertionPosition.Beginning)); s.Document.ReplaceWorkingText(A); await s.Document.EditRefresh;
        await s.Document.NavigateAsync(original, Token); Assert.Equal(3, s.Document.MessageNumber);
        if (position == MessageInsertionPosition.BeforeCurrent) expected = 3;
        if (position == MessageInsertionPosition.AfterCurrent) expected = 4;
        s.Document.IsEditMode = false;
        Assert.True(await s.View.InsertMessageAsync(position)); var inserted = s.Document.ActiveMessageId; Pump(s.Window);
        Assert.Equal(expected, s.Document.MessageNumber); Assert.Equal(4, s.Document.MessageCount);
        Assert.Equal(expected.ToString(), s.View.MessageNumberInput.Text); Assert.True(s.Document.IsEditMode); Assert.True(s.Document.IsDirty);
        Assert.Equal("", s.View.ActiveView!.Editor.Text); Assert.False(s.View.ActiveView.Editor.IsReadOnly);
        Assert.Same(s.View.ActiveView.Editor.TextArea, s.Window.FocusManager!.GetFocusedElement());
        Assert.Equal(MessageOrigin.Inserted, s.Document.ActiveEntry!.Value.Origin); Assert.Null(s.Document.ActiveEntry.Value.SourceOrdinal);
        s.View.InsertionCommands[MessageInsertionPosition.AfterCurrent].Execute(null);
        Assert.Equal(5, s.Document.MessageCount); Assert.NotEqual(inserted, s.Document.ActiveMessageId);
        Assert.Equal(5, s.Document.Messages.Select(m => m.Id).Distinct().Count()); Assert.False(s.Manager.Preferences.StartInEditMode);
    });

    [Fact]
    public Task InsertEditHistoryWorksWithoutFocusAndSurvivesReconstructedMemoryHost() => InWindow(async s =>
    {
        var original = s.Document.ActiveMessageId;
        Assert.True(await s.View.InsertMessageAsync(MessageInsertionPosition.End)); var inserted = s.Document.ActiveMessageId;
        s.Window.KeyTextInput("MSH|^~\\&|SYN"); await s.Document.EditRefresh; Assert.Equal("MSH|^~\\&|SYN", s.Document.EditableText);
        s.Document.IsEditMode = false; Assert.True(s.View.MessageNumberInput.Focus());
        // A navigation TextBox owns Ctrl+Z. Use the root after reconstruction to exercise document history.
        s.Window.Content = null; var rebuilt = Assert.IsType<FileDocumentView>(MessageViewer.Create(s.Document)); s.Window.Content = rebuilt; Pump(s.Window);
        History(s.Window, Key.Z); await s.Document.EditRefresh; Assert.Equal("", s.Document.EditableText); Assert.Equal(inserted, s.Document.ActiveMessageId);
        History(s.Window, Key.Z); await s.Document.EditRefresh; Assert.Equal(original, s.Document.ActiveMessageId); Assert.Equal(1, s.Document.MessageCount); Assert.False(s.Document.IsDirty);
        History(s.Window, Key.Y); await s.Document.EditRefresh; Assert.Equal(inserted, s.Document.ActiveMessageId);
        History(s.Window, Key.Y); await s.Document.EditRefresh; Assert.Equal("MSH|^~\\&|SYN", s.Document.EditableText); Assert.False(s.Document.CanRedo);
        History(s.Window, Key.Z); await s.Document.EditRefresh;
        Assert.True(await rebuilt.InsertMessageAsync(MessageInsertionPosition.End)); Assert.False(s.Document.CanRedo);
    }, A, memory: true);

    [Theory]
    [InlineData(MessageInsertionPosition.Beginning)] [InlineData(MessageInsertionPosition.End)]
    public Task EmptyDocumentCanCreateFirstMessageAndUndoBackToZero(MessageInsertionPosition position) => InWindow(async s =>
    {
        Assert.Equal(0, s.Document.MessageCount);
        Assert.False(s.View.InsertionCommands[MessageInsertionPosition.BeforeCurrent].CanExecute(null));
        Assert.False(s.View.InsertionCommands[MessageInsertionPosition.AfterCurrent].CanExecute(null));
        Assert.False(await s.View.InsertMessageAsync(MessageInsertionPosition.BeforeCurrent)); Assert.False(s.Document.IsDirty);
        Assert.True(await s.View.InsertMessageAsync(position)); var id = s.Document.ActiveMessageId; Assert.Equal(1, s.Document.MessageNumber);
        s.Document.Undo(); await s.Document.EditRefresh; Assert.Empty(s.Document.Messages); Assert.False(s.Document.IsDirty);
        s.Document.Redo(); await s.Document.EditRefresh; Assert.Equal(id, s.Document.ActiveMessageId);
    }, "");

    [Theory]
    [InlineData("PID|SYN\r")] [InlineData("arbitrary source")] [InlineData("BHS|SYN\r" + A + "BTS|1\r")]
    public Task UnsupportedSourceRejectsInsertionWithoutSelectionHistoryOrFocusChange(string text) => InWindow(async s =>
    {
        var id = s.Document.ActiveMessageId; var revision = s.Document.ContentVersion;
        s.View.Focusable = true; Assert.True(s.View.Focus()); var focus = s.Window.FocusManager!.GetFocusedElement();
        Assert.False(s.View.AddMessage.IsEnabled); Assert.NotNull(s.Document.InsertionUnavailableReason);
        Assert.False(await s.View.InsertMessageAsync(MessageInsertionPosition.End));
        Assert.Equal(id, s.Document.ActiveMessageId); Assert.Equal(revision, s.Document.ContentVersion); Assert.False(s.Document.CanUndo); Assert.False(s.Document.IsDirty);
        Assert.Same(focus, s.Window.FocusManager.GetFocusedElement()); Assert.NotEmpty(s.View.NavigationStatus!);
    }, text);
}
