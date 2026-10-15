using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class EditingInspectionViewTests
{
    private const string Text = "MSH|^~\\&|SYN\rPID|Alpha^Beta&Gamma\r";
    private static void Layout(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    [Fact]
    public async Task ContinuousPlatformTypingKeepsDockedToolsScrollAndSelectionWithoutEmptyFlashes()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            var text = Text.Replace("Gamma\r", "Gamma|" + string.Join('|', Enumerable.Repeat("Synthetic", 70)) + "\r", StringComparison.Ordinal) +
                string.Concat(Enumerable.Range(1, 70).Select(i => $"NTE|{i}||Synthetic\r"));
            using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
            var document = await MessageFileReader.OpenFileAsync(documents, "Synthetic", source, TestContext.Current.CancellationToken);
            using var navModel = new InspectionToolViewModel(documents, true); using var fieldModel = new InspectionToolViewModel(documents, false);
            var registry = new ToolWindowRegistry();
            registry.Register(new(WorkspaceDockFactory.NavigatorId, "Segment Navigator", () => navModel));
            registry.Register(new(WorkspaceDockFactory.FieldInspectorId, "Field Inspector", () => fieldModel));
            using var factory = new WorkspaceDockFactory(documents, registry);
            var dock = new DockControl { Layout = factory.CreateLayout() };
            var window = new Window { Width = 1400, Height = 1000, Content = dock }; window.Show();
            try
            {
                Layout(window); document.IsEditMode = true;
                var view = Assert.Single(dock.GetVisualDescendants().OfType<MessageInspectionView>());
                var navigator = Assert.Single(dock.GetVisualDescendants().OfType<InspectionPanes>(), p => p.Navigator.IsAttachedToVisualTree());
                var fields = Assert.Single(dock.GetVisualDescendants().OfType<InspectionPanes>(), p => p.Inspector.IsAttachedToVisualTree());
                document.Inspection.SelectDeepestOffset(text.IndexOf("Gamma", StringComparison.Ordinal)); Layout(window);
                var old = document.Inspection; var selectedPath = old.SelectedNode!.Path;
                var expanded = fields.Inspector.GetVisualDescendants().OfType<TreeViewItem>()
                    .Where(i => i.IsExpanded && i.DataContext is InspectionNode).Select(i => ((InspectionNode)i.DataContext!).Path).ToArray();
                Assert.NotEmpty(expanded);
                var navScroll = navigator.Navigator.GetVisualDescendants().OfType<ScrollViewer>().First();
                var fieldScroll = fields.Inspector.GetVisualDescendants().OfType<ScrollViewer>().First();
                navScroll.Offset = new(0, 120); fieldScroll.Offset = new(0, 24); Layout(window);
                var n = navScroll.Offset; var f = fieldScroll.Offset;
                fields.FieldNumber.Value = 7;
                view.Editor.Select(text.IndexOf("Gamma", StringComparison.Ordinal), 0); Assert.True(view.Editor.TextArea.Focus());
                void Pending()
                {
                    Assert.Equal(DocumentInspectionState.Pending, document.InspectionState);
                    Assert.Same(old, document.InspectionSnapshot);
                    Assert.Contains(navigator, dock.GetVisualDescendants().OfType<InspectionPanes>());
                    Assert.Contains(fields, dock.GetVisualDescendants().OfType<InspectionPanes>());
                    Assert.Contains("previous parse", fields.SnapshotStatus.Text!);
                    Assert.False(fields.HasCurrentInspection); Assert.True(fields.Inspector.IsEffectivelyEnabled); Assert.True(navigator.Navigator.IsEffectivelyEnabled);
                    Assert.Same(old.Fields, fields.Inspector.ItemsSource);
                    Assert.DoesNotContain(dock.GetVisualDescendants().OfType<TextBlock>(), b => b.Text == "No inspectable active message.");
                }
                foreach (var input in new[] { "X", "Y", "Z" })
                {
                    window.KeyTextInput(input); Pending();
                    var caret = view.Editor.CaretOffset; old.SelectDeepestOffset(0); old.SelectSegment(old.Segments[0]);
                    Assert.Equal(caret, view.Editor.CaretOffset); Assert.Null(old.ResolveOffset(0));
                    await Task.Delay(30, TestContext.Current.CancellationToken);
                }
                await document.EditRefresh; Layout(window);
                Assert.Same(navScroll, navigator.Navigator.GetVisualDescendants().OfType<ScrollViewer>().First());
                Assert.Same(fieldScroll, fields.Inspector.GetVisualDescendants().OfType<ScrollViewer>().First());
                Assert.Equal(n, navScroll.Offset); Assert.Equal(f, fieldScroll.Offset);
                Assert.Equal(7, fields.FieldNumber.Value); Assert.True(fields.HasCurrentInspection);
                Assert.Equal(selectedPath, document.Inspection.SelectedNode!.Path);
                var restored = fields.Inspector.GetVisualDescendants().OfType<TreeViewItem>()
                    .Where(i => i.IsExpanded && i.DataContext is InspectionNode).Select(i => ((InspectionNode)i.DataContext!).Path).ToArray();
                Assert.All(expanded, path => Assert.Contains(path, restored));
                Assert.Same(document.Inspection.Fields, fields.Inspector.ItemsSource);
                Assert.Equal("XYZGamma", document.Message.Get("PID-1.2.2").Raw);
                Assert.Contains(fields, dock.GetVisualDescendants().OfType<InspectionPanes>());
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task EmbeddedPanesSurviveMalformedDraftsUndoEmptyTextAndPendingNavigation()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Text + "MSH|^~\\&|TWO\rPID|Other\r"));
            var document = await MessageFileReader.OpenFileAsync(documents, "Synthetic", source, TestContext.Current.CancellationToken);
            var file = Assert.IsType<FileDocumentView>(MessageViewer.Create(document));
            var window = new Window { Width = 1200, Height = 900, Content = file }; window.Show();
            try
            {
                document.IsEditMode = true; Layout(window); var view = file.ActiveView!; var panes = Assert.IsType<InspectionPanes>(view.LowerPane);
                void Replace(string value)
                { view.Editor.Select(0, view.Editor.Document.TextLength); view.Editor.TextArea.PerformTextInput(value); }
                Replace(Text.Replace("^~\\&", "^^^^", StringComparison.Ordinal));
                Assert.Contains("previous parse", panes.SnapshotStatus.Text!); await document.EditRefresh; Layout(window);
                Assert.Same(panes, view.LowerPane); Assert.True(panes.HasCurrentInspection);
                Assert.Contains(document.ActiveDiagnostics, d => d.Code == "InvalidDelimiters");
                Replace("PID|" + new string('Q', MessageDocument.MaximumEditableLineCharacters + 1)); await document.EditRefresh; Layout(window);
                Assert.Same(panes, view.LowerPane); Assert.Equal(DocumentInspectionState.Stale, document.InspectionState);
                Assert.Contains("Previous parse", panes.SnapshotStatus.Text!); Assert.True(panes.InspectFieldButton.IsEffectivelyEnabled);
                Assert.NotEmpty(panes.Navigator.Items); document.Undo(); await document.EditRefresh; Layout(window);
                Assert.Same(panes, view.LowerPane); Assert.True(panes.HasCurrentInspection);
                Replace(""); Assert.Equal(DocumentInspectionState.Empty, document.InspectionState);
                Assert.Contains("empty", panes.SnapshotStatus.Text!); Assert.Empty(panes.Navigator.Items);
                await document.EditRefresh; Assert.Same(panes, view.LowerPane);
                document.Undo(); await document.EditRefresh; document.Redo(); await document.EditRefresh;
                Assert.Equal(DocumentInspectionState.Empty, document.InspectionState); document.Undo(); await document.EditRefresh;
                Replace(Text.Replace("Alpha", "SavedDraft", StringComparison.Ordinal)); var pending = document.EditRefresh;
                await file.NavigateAsync(2); await pending; Layout(window);
                Assert.Equal("Other", document.Message.Get("PID-1").Raw);
                Assert.DoesNotContain(panes, file.GetVisualDescendants().OfType<InspectionPanes>());
                await file.NavigateAsync(1); Layout(window); Assert.Contains("SavedDraft", file.ActiveView!.Editor.Text);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreviousInspectionKeepsEnabledPresentationButBlocksStructuralInput(bool failedParse)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Text));
            var document = await MessageFileReader.OpenFileAsync(documents, "Synthetic", source, TestContext.Current.CancellationToken);
            var file = Assert.IsType<FileDocumentView>(MessageViewer.Create(document));
            var window = new Window { Width = 1200, Height = 900, Content = file }; window.Show();
            try
            {
                document.IsEditMode = true; Layout(window);
                var view = file.ActiveView!; var panes = Assert.IsType<InspectionPanes>(view.LowerPane);
                var inspection = document.Inspection;
                inspection.SelectDeepestOffset(Text.IndexOf("Gamma", StringComparison.Ordinal)); Layout(window);
                view.Editor.Select(Text.IndexOf("Gamma", StringComparison.Ordinal), 0); view.Editor.TextArea.Focus();
                // Let initial focus/selection theme transitions settle before comparing
                // the same realized row across the draft-state notification.
                await Task.Delay(250, TestContext.Current.CancellationToken); Layout(window);
                var segment = panes.Navigator.SelectedItem; var node = panes.Inspector.SelectedItem;
                var selectedRow = panes.Inspector.GetVisualDescendants().OfType<TreeViewItem>().Single(i => i.IsSelected);
                var selectedSegmentRow = panes.Navigator.GetVisualDescendants().OfType<ListBoxItem>().Single(i => i.IsSelected);
                var expanded = panes.Inspector.GetVisualDescendants().OfType<TreeViewItem>().Where(i => i.IsExpanded).ToArray();
                var presentation = selectedRow.GetVisualDescendants().OfType<Control>().Prepend(selectedRow)
                    .Concat(selectedSegmentRow.GetVisualDescendants().OfType<Control>().Prepend(selectedSegmentRow))
                    .Select(c => (Control: c, c.Opacity,
                        Foreground: c is TextBlock text ? text.Foreground : (c as TemplatedControl)?.Foreground,
                        Background: c is Border border ? border.Background : (c as TemplatedControl)?.Background)).ToArray();
                var statusHeight = panes.SnapshotStatus.Bounds.Height;
                var values = panes.GetVisualDescendants().OfType<TextBox>().Select(t => (Control: t, t.Text)).ToArray();
                if (failedParse)
                {
                    view.Editor.TextArea.PerformTextInput(new string('Q', MessageDocument.MaximumEditableLineCharacters + 1));
                    await document.EditRefresh;
                }
                else view.Editor.TextArea.PerformTextInput("X");
                Layout(window);
                Assert.Equal(failedParse ? DocumentInspectionState.Stale : DocumentInspectionState.Pending, document.InspectionState);
                Assert.False(panes.HasCurrentInspection);
                Assert.Contains("previous parse", panes.SnapshotStatus.Text!, StringComparison.OrdinalIgnoreCase);
                Assert.Equal(statusHeight, panes.SnapshotStatus.Bounds.Height);
                Assert.All(new Control[] { panes.Navigator, panes.Inspector, panes.FieldNumber, panes.InspectFieldButton, panes.ValueNavigation },
                    c => { Assert.True(c.IsEffectivelyEnabled); Assert.DoesNotContain(":disabled", c.Classes); });
                Assert.All(presentation, p =>
                {
                    Assert.True(p.Control.IsEffectivelyEnabled); Assert.Equal(p.Opacity, p.Control.Opacity);
                    Assert.Equal(p.Foreground, p.Control is TextBlock text ? text.Foreground : (p.Control as TemplatedControl)?.Foreground);
                    Assert.Equal(p.Background, p.Control is Border border ? border.Background : (p.Control as TemplatedControl)?.Background);
                });
                Assert.All(values, v => Assert.Equal(v.Text, v.Control.Text));
                var caret = view.Editor.CaretOffset; var selection = view.Editor.SelectionLength;
                var navigationEvents = 0; inspection.SelectionChanged += (_, _) => navigationEvents++;
                var otherRow = panes.Navigator.GetVisualDescendants().OfType<ListBoxItem>().First(i => !i.IsSelected);
                var point = otherRow.TranslatePoint(new(10, otherRow.Bounds.Height / 2), window)!.Value;
                window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
                var otherField = panes.Inspector.GetVisualDescendants().OfType<TreeViewItem>().First(i => !i.IsSelected);
                var fieldPoint = otherField.TranslatePoint(new(50, 10), window)!.Value;
                for (var click = 0; click < 2; click++)
                { window.MouseDown(fieldPoint, MouseButton.Left); window.MouseUp(fieldPoint, MouseButton.Left); }
                foreach (var control in new Control[] { panes.Navigator, selectedRow })
                    foreach (var key in new[] { Key.Down, Key.Home, Key.Right, Key.Space, Key.Enter })
                    {
                        var input = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key };
                        control.RaiseEvent(input); Assert.True(input.Handled);
                    }
                panes.Navigator.SelectedItem = inspection.Segments[0];
                panes.Inspector.SelectedItem = inspection.Fields[0];
                panes.FieldNumber.Value = 2;
                panes.InspectFieldButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Same(segment, panes.Navigator.SelectedItem); Assert.Same(node, panes.Inspector.SelectedItem);
                Assert.All(expanded, row => Assert.True(row.IsExpanded));
                Assert.Equal(0, navigationEvents); Assert.Equal(caret, view.Editor.CaretOffset); Assert.Equal(selection, view.Editor.SelectionLength);
                Assert.Null(inspection.ResolveOffset(0));
                if (failedParse) document.Undo();
                await document.EditRefresh; Layout(window);
                Assert.True(panes.HasCurrentInspection); Assert.Same(panes, view.LowerPane);
                panes.FieldNumber.Value = 1;
                panes.InspectFieldButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal("PID[1]-1", document.Inspection.SelectedNode!.Path.ToString());
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
