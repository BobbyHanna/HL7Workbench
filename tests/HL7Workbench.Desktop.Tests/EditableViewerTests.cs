using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class EditableViewerTests
{
    [Fact]
    public async Task ActualTextInputRefreshesStructureAndDocumentHistorySurvivesReconstruction()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            const string text = "MSH|^~\\&|SYN\rPID|Alpha^Beta\r";
            using var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(text));
            var view = Assert.IsType<MessageInspectionView>(MessageViewer.Create(document));
            var window = new Window { Width = 1100, Height = 800, Content = view }; window.Show();
            try
            {
                Assert.True(view.Editor.IsReadOnly);
                var toggle = Assert.Single(view.GetVisualDescendants().OfType<CheckBox>());
                toggle.IsChecked = true; Assert.True(document.IsEditMode); Assert.False(view.Editor.IsReadOnly);
                view.Editor.Select(text.IndexOf("Alpha", StringComparison.Ordinal), 0);
                view.Editor.TextArea.PerformTextInput("Long");
                Assert.True(document.IsDirty); Assert.Contains("LongAlpha", document.WorkingText!);
                Assert.Null(document.ActiveInspection);
                await document.EditRefresh; window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.Equal("LongAlpha", document.Message.Get("PID-1.1").Raw);
                view.Editor.CaretOffset = view.Editor.Text.IndexOf("Beta", StringComparison.Ordinal);
                Assert.Equal("PID[1]-1", document.Inspection.SelectedNode!.Path.ToString());
                Assert.Equal("PID[1]-1[1].2", document.Inspection.ResolveOffset(view.Editor.CaretOffset)!.Path.ToString());
                Assert.Equal(document.Inspection.Fields, view.Inspector.ItemsSource);
                toggle.IsChecked = false; view.Editor.TextArea.PerformTextInput("REFUSED");
                Assert.DoesNotContain("REFUSED", view.Editor.Text); Assert.True(document.IsDirty);
                window.Content = null;
                var rebuilt = Assert.IsType<MessageInspectionView>(MessageViewer.Create(document)); window.Content = rebuilt;
                Assert.Contains("LongAlpha", rebuilt.Editor.Text); Assert.True(rebuilt.Editor.IsReadOnly);
                document.IsEditMode = true;
                rebuilt.Editor.TextArea.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Z, KeyModifiers = KeyModifiers.Control });
                await document.EditRefresh; Assert.Equal(text, rebuilt.Editor.Text); Assert.False(document.IsDirty);
                rebuilt.Editor.TextArea.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Y, KeyModifiers = KeyModifiers.Control });
                await document.EditRefresh; Assert.Contains("LongAlpha", rebuilt.Editor.Text); Assert.True(document.IsDirty);
                Assert.Equal(2, rebuilt.Editor.TextArea.TextView.LineTransformers.OfType<AvaloniaEdit.Rendering.DocumentColorizingTransformer>().Count());
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task FileMessageNavigationRetainsEditsWithoutRecreatingEditorWhileTyping()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager();
            using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rPID|One\rMSH|^~\\&|SYN\rPID|Two\r"));
            var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", source, TestContext.Current.CancellationToken);
            var view = Assert.IsType<FileDocumentView>(MessageViewer.Create(document));
            var window = new Window { Width = 1100, Height = 800, Content = view }; window.Show();
            try
            {
                document.IsEditMode = true; var editor = view.ActiveView!.Editor;
                editor.Select(editor.Text.IndexOf("One", StringComparison.Ordinal), 0); editor.TextArea.PerformTextInput("Edited");
                Assert.Same(editor, view.ActiveView.Editor); await document.EditRefresh; Assert.Same(editor, view.ActiveView.Editor);
                await view.NavigateAsync(2); Assert.Contains("Two", view.ActiveView!.Editor.Text);
                await view.NavigateAsync(1); Assert.Contains("EditedOne", view.ActiveView!.Editor.Text);
                document.Undo(); await document.EditRefresh; Assert.DoesNotContain("Edited", view.ActiveView.Editor.Text);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task DockModifiedTitleAndAsyncCloseCancellationProtectInactiveDirtyDocument()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); var registry = new ToolWindowRegistry();
            using var factory = new WorkspaceDockFactory(manager, registry); factory.CreateLayout();
            var first = MessageFileReader.Open(manager, "Synthetic", Hl7Parser.Parse("PID|First\r")); factory.OpenDocument(first.Id);
            var second = MessageFileReader.Open(manager, "Other", Hl7Parser.Parse("PID|Other\r")); factory.OpenDocument(second.Id);
            first.IsEditMode = true; first.ReplaceWorkingText("PID|Edited\r");
            var item = factory.GetDockable<IDocument>(first.Id)!; Assert.Equal("Synthetic *", item.Title);
            var decision = new TaskCompletionSource<bool>(); factory.ConfirmDocumentCloseAsync = _ => decision.Task;
            factory.CloseDockable(item); Assert.Contains(first, manager.Documents); Assert.Same(second, manager.ActiveDocument);
            decision.SetResult(false); await Task.Yield(); Assert.Contains(first, manager.Documents);
            factory.ConfirmDocumentCloseAsync = _ => Task.FromResult(true); factory.CloseDockable(item); await Task.Yield();
            Assert.DoesNotContain(first, manager.Documents); await first.Disposal; Assert.Same(second, manager.ActiveDocument);
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
