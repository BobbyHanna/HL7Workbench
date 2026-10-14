using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class InspectionViewTests
{
    [Fact]
    public async Task ListsCaretAndLookupSynchronizeWithoutEditingAndSelectionSurvivesViewReconstruction()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            const string text = "MSH|^~\\&|SYN\rPID||\"\"|Alpha^Beta&Gamma~Delta\rOBX|1|TX|||Synthetic\r";
            var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(text));
            var view = Assert.IsType<MessageInspectionView>(MessageViewer.Create(document));
            var window = new Window { Width = 1100, Height = 900, Content = view };
            window.Show();
            try
            {
                view.Navigator.SelectedItem = document.Inspection.Segments[1];
                Assert.Equal("PID||\"\"|Alpha^Beta&Gamma~Delta", view.Editor.SelectedText);
                Assert.Same(document.Inspection.Fields, view.Inspector.ItemsSource);
                var field = document.Inspection.Fields[2];
                view.Inspector.SelectedItem = field;
                Assert.Equal("Alpha^Beta&Gamma~Delta", view.Editor.SelectedText);
                Assert.Equal(field.Range!.Value.Offset, view.Editor.SelectionStart);
                view.FieldNumber.Value = 1;
                view.InspectFieldButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(Hl7ValueState.Empty, document.Inspection.SelectedNode!.State);
                Assert.Equal(0, view.Editor.SelectionLength);
                view.FieldNumber.Value = 99;
                view.InspectFieldButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(Hl7ValueState.Missing, document.Inspection.SelectedNode!.State);
                Assert.Null(document.Inspection.SelectedRange);
                Assert.Equal(0, view.Editor.SelectionLength);
                view.Editor.CaretOffset = text.IndexOf("Synthetic", StringComparison.Ordinal);
                Assert.Same(document.Inspection.Segments[2], view.Navigator.SelectedItem);
                Assert.Equal("OBX[1]-5", document.Inspection.SelectedNode!.Path.ToString());

                document.Inspection.SelectSegment(document.Inspection.Segments[1]);
                var nested = document.Inspection.Fields[2].Children[0].Children[1].Children[1];
                document.Inspection.SelectNode(nested);
                Assert.Equal("Gamma", view.Editor.SelectedText);
                window.Content = null;
                var oldSelection = view.Editor.SelectionStart;
                var second = new MessageDocument("other", "Other", Hl7Parser.Parse("PID|Other"));
                window.Content = MessageViewer.Create(second);
                second.Inspection.InspectField(1);
                Assert.Same(nested, document.Inspection.SelectedNode);
                var reconstructed = Assert.IsType<MessageInspectionView>(MessageViewer.Create(document));
                window.Content = reconstructed;
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("Gamma", reconstructed.Editor.SelectedText);
                var root = Assert.IsType<TreeViewItem>(reconstructed.Inspector.ContainerFromItem(document.Inspection.Fields[2]));
                Assert.True(root.IsExpanded);
                window.Content = null;
                document.Inspection.InspectField(2);
                // Detached views have unsubscribed from the document's longer-lived state.
                Assert.Equal(oldSelection, view.Editor.SelectionStart);
                var restored = Assert.IsType<MessageInspectionView>(MessageViewer.Create(document));
                window.Content = restored;
                Assert.Equal("\"\"", restored.Editor.SelectedText);
                Assert.Equal(Hl7ValueState.ExplicitNull, document.Inspection.SelectedNode!.State);
                restored.Editor.TextArea.PerformTextInput("replacement");
                Assert.Equal(text, restored.Editor.Text);
                Assert.Equal(text, document.Message.RawText);
                Assert.False(document.Message.IsModified);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DocumentGridRatiosAndActualSplitterDragResizeAdjacentPanes()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse("PID|1"));
            var view = Assert.IsType<MessageInspectionView>(MessageViewer.Create(document));
            var window = new Window { Width = 1100, Height = 900, Content = view };
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.Equal(65, view.RowDefinitions[0].Height.Value);
                Assert.Equal(35, view.RowDefinitions[2].Height.Value);
                Assert.Equal(25, view.LowerPane.ColumnDefinitions[0].Width.Value);
                Assert.Equal(75, view.LowerPane.ColumnDefinitions[2].Width.Value);
                var horizontal = Assert.Single(view.Children.OfType<GridSplitter>());
                var vertical = Assert.Single(view.LowerPane.Children.OfType<GridSplitter>());
                var beforeHeight = view.Editor.Bounds.Height;
                var beforeWidth = view.LowerPane.ColumnDefinitions[0].ActualWidth;
                Drag(horizontal, new(0, -50));
                window.UpdateLayout();
                Assert.True(view.Editor.Bounds.Height < beforeHeight);
                Drag(vertical, new(50, 0));
                window.UpdateLayout();
                Assert.True(view.LowerPane.ColumnDefinitions[0].ActualWidth > beforeWidth);
                Assert.Equal("PID|1", view.Editor.Text);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }

    private static void Drag(GridSplitter splitter, Vector delta)
    {
        splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragStartedEvent, Vector = default });
        splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = delta });
        splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent, Vector = delta });
    }

    [Fact]
    public async Task SelectingDistantSegmentScrollsRawPaneAndRetainsExactMixedTerminatorRange()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var text = string.Concat(Enumerable.Range(1, 200).Select(i => $"OBX|{i}|TX|||Synthetic" + (i % 2 == 0 ? "\r\n" : "\r")));
            var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(text));
            var view = Assert.IsType<MessageInspectionView>(MessageViewer.Create(document));
            var window = new Window { Width = 1000, Height = 700, Content = view };
            window.Show();
            try
            {
                window.UpdateLayout();
                view.Navigator.SelectedItem = document.Inspection.Segments[^1];
                window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("OBX|200|TX|||Synthetic", view.Editor.SelectedText);
                Assert.True(view.Editor.VerticalOffset > 0);
                Assert.Equal(text, view.Editor.Text);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }
}
