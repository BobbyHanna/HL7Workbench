using System.Text;
using Avalonia.Controls;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class StructuralHistoryViewTests
{
    private const string A = "MSH|^~\\&|SYN-A\rPID|A\r";
    private const string B = "MSH|^~\\&|SYN-B\rPID|B\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Key(Window window, Key key)
    {
        var physical = key == Avalonia.Input.Key.Z ? PhysicalKey.Z : PhysicalKey.Y;
        window.KeyPress(key, RawInputModifiers.Control, physical, null);
        window.KeyRelease(key, RawInputModifiers.Control, physical, null);
    }
    [Fact]
    public async Task ChronologicalUndoUpdatesEditorAndInspectorForOtherMessageWithEditModeOff()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); using var input = new MemoryStream(Encoding.UTF8.GetBytes(A + B));
            var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, Token);
            var view = Assert.IsType<FileDocumentView>(MessageViewer.Create(doc));
            var window = new Window { Content = view, Width = 1000, Height = 700 }; window.Show();
            DocumentHistoryShortcuts.Attach(window, () => manager.ActiveDocument);
            try
            {
                doc.IsEditMode = true;
                view.ActiveView!.Editor.TextArea.PerformTextInput("SYN-");
                await doc.EditRefresh;
                await doc.NavigateAsync(2, Token); view.ActiveView!.Editor.TextArea.PerformTextInput("SYN-");
                await doc.EditRefresh; await doc.NavigateAsync(1, Token); doc.IsEditMode = false;
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Assert.True(view.ActiveView!.Editor.TextArea.Focus());
                var focusedEditor = view.ActiveView.Editor;
                Key(window, Avalonia.Input.Key.Z); await doc.EditRefresh; window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, doc.MessageNumber); Assert.Equal(B, view.ActiveView!.Editor.Text);
                Assert.False(focusedEditor.IsAttachedToVisualTree());
                Assert.Null(window.FocusManager!.GetFocusedElement());
                Assert.Equal(doc.Inspection.Fields, view.ActiveView.Inspector.ItemsSource); Assert.True(view.ActiveView.Editor.IsReadOnly);
                Assert.Equal("2", view.MessageNumberInput.Text);
                Key(window, Avalonia.Input.Key.Z); await doc.EditRefresh; Assert.Equal(1, doc.MessageNumber); Assert.Equal(A, view.ActiveView.Editor.Text);
                Assert.False(doc.IsDirty); Assert.False(doc.CanUndo);
                // Start redo on the other message as well, so its first key must
                // navigate/detach the focused view before the immediately following key.
                await doc.NavigateAsync(2, Token); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.True(view.ActiveView!.Editor.TextArea.Focus()); var redoFocusedEditor = view.ActiveView.Editor;
                Key(window, Avalonia.Input.Key.Y); await doc.EditRefresh; Assert.Equal(1, doc.MessageNumber); Assert.StartsWith("SYN-", view.ActiveView.Editor.Text);
                Assert.False(redoFocusedEditor.IsAttachedToVisualTree()); Assert.Null(window.FocusManager.GetFocusedElement());
                Key(window, Avalonia.Input.Key.Y); await doc.EditRefresh; Assert.Equal(2, doc.MessageNumber); Assert.StartsWith("SYN-", view.ActiveView.Editor.Text);
                Assert.False(view.ActiveView.Editor.Document.UndoStack.CanUndo);
            }
            finally { window.Close(); manager.Dispose(); await doc.Disposal; }
            return true;
        }, Token);
    }
    [Fact]
    public async Task SameLogicalNumberWithDifferentIdentityReplacesViewAndDetachedEditorCannotWrite()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); using var input = new MemoryStream(Encoding.UTF8.GetBytes(A));
            var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, Token); var original = doc.ActiveMessageId;
            var view = Assert.IsType<FileDocumentView>(MessageViewer.Create(doc));
            var window = new Window { Content = view, Width = 1000, Height = 700 }; window.Show();
            DocumentHistoryShortcuts.Attach(window, () => manager.ActiveDocument);
            try
            {
                var inserted = doc.InsertMessage(MessageInsertionPosition.Beginning, B); await doc.EditRefresh;
                var discarded = view.ActiveView!.Editor; Assert.Equal(1, doc.MessageNumber); Assert.Equal(B, discarded.Text);
                doc.Undo(); await doc.EditRefresh;
                Assert.Equal(original, doc.ActiveMessageId); Assert.Equal(1, doc.MessageNumber); Assert.Equal(A, view.ActiveView!.Editor.Text);
                Assert.NotSame(discarded, view.ActiveView.Editor);
                discarded.Document.Insert(0, "SYN-DETACHED"); Assert.Equal(A, doc.EditableText); Assert.False(doc.IsDirty);
                doc.Redo(); await doc.EditRefresh;
                Assert.Equal(inserted, doc.ActiveMessageId); Assert.Equal(B, view.ActiveView.Editor.Text);
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "/ 2");
                Assert.Equal(doc.Inspection.Fields, view.ActiveView.Inspector.ItemsSource);
                window.Content = null;
                var rebuilt = Assert.IsType<FileDocumentView>(MessageViewer.Create(doc)); window.Content = rebuilt;
                // Reconstructing a whole document view must neither remove the root
                // route nor add another invocation. No editor is focused here.
                Key(window, Avalonia.Input.Key.Z); await doc.EditRefresh;
                Assert.Equal(original, doc.ActiveMessageId); Assert.Equal(A, rebuilt.ActiveView!.Editor.Text);
                Key(window, Avalonia.Input.Key.Y); await doc.EditRefresh;
                Assert.Equal(inserted, doc.ActiveMessageId); Assert.Equal(B, rebuilt.ActiveView.Editor.Text);
            }
            finally { window.Close(); manager.Dispose(); await doc.Disposal; }
            return true;
        }, Token);
    }
    [Fact]
    public async Task UndoFromSourceBackedViewNavigatesToBoundedAffectedDraft()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(A + B + "OBX|1|TX|||" + new string('Q', 2 * 1024 * 1024) + "\r"));
            var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, Token);
            var view = Assert.IsType<FileDocumentView>(MessageViewer.Create(doc)); var window = new Window { Content = view, Width = 1000, Height = 700 }; window.Show();
            DocumentHistoryShortcuts.Attach(window, () => manager.ActiveDocument);
            try
            {
                doc.IsEditMode = true; doc.ReplaceWorkingText(A + "NTE|1\r"); await doc.EditRefresh;
                await doc.NavigateAsync(2, Token); Assert.NotNull(doc.Source); Assert.Null(doc.MaterializedMessage); doc.IsEditMode = false;
                Key(window, Avalonia.Input.Key.Z); await doc.EditRefresh;
                Assert.Equal(1, doc.MessageNumber); Assert.Equal(A, view.ActiveView!.Editor.Text); Assert.False(doc.IsDirty);
            }
            finally { window.Close(); manager.Dispose(); await doc.Disposal; }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MainAndFloatingRootsUseActiveTabFromInspectionToolsWithoutMovingFocus(bool inspector)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            Application.Current!.Styles.Add(new Dock.Avalonia.Themes.Fluent.DockFluentTheme());
            Application.Current.DataTemplates.Add(new DockContentTemplate { DockedInspection = true });
            var configuration = Path.Combine(Path.GetTempPath(), "hl7-history-ui-" + Guid.NewGuid().ToString("N"));
            var window = new MainWindow(configuration) { Width = 1400, Height = 1000 }; window.Show();
            DocumentManager? manager = null; Window? floating = null;
            try
            {
                var dock = window.FindControl<DockControl>("WorkspaceDock")!;
                var factory = Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory);
                // Obtain the shell's existing document manager through a real tool;
                // this exercises production root registration without test-only shell APIs.
                factory.ShowTool(WorkspaceDockFactory.ComparisonId);
                manager = Assert.IsType<ComparisonViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!.Context).Documents;
                factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!);
                using var input = new MemoryStream(Encoding.UTF8.GetBytes(A + B));
                var first = await MessageFileReader.OpenFileAsync(manager, "Synthetic file", input, Token);
                factory.OpenDocument(first.Id);
                first.IsEditMode = true; first.ReplaceWorkingText(A + "NTE|first\r"); await first.EditRefresh;
                await first.NavigateAsync(2, Token); first.ReplaceWorkingText(B + "NTE|second\r"); await first.EditRefresh;
                await first.NavigateAsync(1, Token); first.IsEditMode = false;
                var second = MessageFileReader.Open(manager, "Synthetic tab", Hl7Parser.Parse(A)); factory.OpenDocument(second.Id);
                second.IsEditMode = true; second.ReplaceWorkingText(A + "NTE|one\r"); await second.EditRefresh;
                second.ReplaceWorkingText(A + "NTE|two\r"); await second.EditRefresh; second.IsEditMode = false;
                factory.SetActiveDockable(factory.GetDockable<IDocument>(first.Id)!);
                Assert.Same(first, manager.ActiveDocument);
                first.Inspection.SelectSegment(first.Inspection.Segments[1]); first.Inspection.InspectField(1);
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var panes = Assert.Single(window.GetVisualDescendants().OfType<InspectionPanes>(),
                    p => inspector ? p.Inspector.IsAttachedToVisualTree() : p.Navigator.IsAttachedToVisualTree());
                var target = Assert.IsAssignableFrom<Control>(inspector ? panes.Inspector.ContainerFromIndex(0) : panes.Navigator.ContainerFromIndex(0));
                Assert.True(target.Focus()); var focus = window.FocusManager!.GetFocusedElement();
                Key(window, Avalonia.Input.Key.Z); await first.EditRefresh;
                Assert.Equal(2, first.MessageNumber); Assert.Equal(B, first.EditableText);
                Key(window, Avalonia.Input.Key.Z); await first.EditRefresh;
                Assert.Equal(1, first.MessageNumber); Assert.Equal(A, first.EditableText); Assert.False(first.CanUndo);
                if (target.IsAttachedToVisualTree()) Assert.Same(focus, window.FocusManager.GetFocusedElement());
                else Assert.Null(window.FocusManager.GetFocusedElement());
                Assert.Equal(A + "NTE|two\r", second.EditableText);

                factory.SetActiveDockable(factory.GetDockable<IDocument>(second.Id)!);
                Assert.Same(second, manager.ActiveDocument);
                Key(window, Avalonia.Input.Key.Z); await second.EditRefresh;
                Assert.Equal(A + "NTE|one\r", second.EditableText); Assert.True(second.CanUndo);
                Assert.Equal(A, first.EditableText); Assert.True(first.CanRedo);
                factory.SetActiveDockable(factory.GetDockable<IDocument>(first.Id)!);
                Assert.Same(first, manager.ActiveDocument);
                Key(window, Avalonia.Input.Key.Y); await first.EditRefresh;
                Assert.Equal(1, first.MessageNumber); Assert.Equal(A + "NTE|first\r", first.EditableText);
                Key(window, Avalonia.Input.Key.Y); await first.EditRefresh;
                Assert.Equal(2, first.MessageNumber); Assert.Equal(B + "NTE|second\r", first.EditableText);

                factory.FloatDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.NavigatorId)!);
                floating = Assert.IsAssignableFrom<Window>(Assert.Single(factory.Root.Windows!).Host);
                floating.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var floatingPanes = Assert.Single(floating.GetVisualDescendants().OfType<InspectionPanes>());
                var floatingTarget = Assert.IsType<ListBoxItem>(floatingPanes.Navigator.ContainerFromIndex(0));
                Assert.True(floatingTarget.Focus());
                var floatingFocus = floating.FocusManager!.GetFocusedElement();
                Key(floating, Avalonia.Input.Key.Z); await first.EditRefresh;
                Assert.Equal(B, first.EditableText); Assert.Equal(2, first.MessageNumber);
                Key(floating, Avalonia.Input.Key.Z); await first.EditRefresh;
                Assert.Equal(A, first.EditableText); Assert.Equal(1, first.MessageNumber);
                Key(floating, Avalonia.Input.Key.Y); await first.EditRefresh;
                Key(floating, Avalonia.Input.Key.Y); await first.EditRefresh;
                Assert.Equal(B + "NTE|second\r", first.EditableText); Assert.False(first.CanRedo);
                if (floatingTarget.IsAttachedToVisualTree()) Assert.Same(floatingFocus, floating.FocusManager.GetFocusedElement());
                else Assert.Null(floating.FocusManager.GetFocusedElement());
                Assert.Equal(A + "NTE|one\r", second.EditableText);
            }
            finally
            {
                floating?.Close();
                var documents = manager?.Documents.ToArray() ?? [];
                foreach (var document in documents) manager!.Close(document.Id, discardChanges: true);
                window.Close();
                await Task.WhenAll(documents.Select(d => d.Disposal));
                if (Directory.Exists(configuration)) Directory.Delete(configuration, true);
            }
            return true;
        }, Token);
    }
}
