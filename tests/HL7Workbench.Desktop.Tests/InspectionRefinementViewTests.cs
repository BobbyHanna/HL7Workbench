using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class InspectionRefinementViewTests
{
    [Theory]
    [InlineData("Z99|A^B", "Z99[1]-1[1].2")]
    [InlineData("PID|A^\"\"", "PID[1]-1[1].2")]
    [InlineData("PID|A^", "PID[1]-1[1].2")]
    public void TooltipUsesCanonicalCoordinatesOnly(string text, string path)
    {
        var message = Hl7Parser.Parse(text);
        var node = new MessageInspection(message).Fields[0].VisibleChildren[1];
        var tip = InspectionPathToolTip.Format(node.PathInfo);
        Assert.Contains($"Path: {path}", tip);
        Assert.Contains("Segment occurrence: 1", tip);
        Assert.Contains("Field: 1", tip);
        Assert.Contains("Repetition: 1", tip);
        Assert.Contains("Component: 2", tip);
        Assert.DoesNotContain("State:", tip);
        Assert.DoesNotContain("Subcomponent:", tip);
        Assert.True(tip.Length < 200);
    }

    [Fact]
    public void TooltipsCoverRepetitionsSubcomponentsAndUnknownVersionsWithoutDefinitions()
    {
        var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse("MSH*$%!@*SYN*********99.9\rZ99*A$B@C%D\rZ99*E$F@G%H"));
        var state = document.Inspection;
        state.SelectDeepestOffset(document.Message.RawText.LastIndexOf('G'));
        var tip = InspectionPathToolTip.Format(state.SelectedNode!.PathInfo);
        Assert.Contains("Path: Z99[2]-1[1].2.2", tip);
        Assert.Contains("Subcomponent: 2", tip);
        Assert.Equal("99.9", document.DeclaredVersion);
        Assert.DoesNotContain("G", tip);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualDoubleClickHitTestingRevealsOnlyVisibleAncestors(bool custom)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            const string standard = "MSH|^~\\&|SYN\rPID|||||EXAMPLE^CHARLIE&ROBERT||||||ONE^TWO^THREE\r";
            var text = custom ? standard.Replace('|', '*').Replace('^', '$').Replace('~', '%').Replace('\\', '!').Replace('&', '@') : standard;
            var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(text));
            var view = Assert.IsType<MessageInspectionView>(MessageViewer.CreateActive(document));
            var window = new Window { Width = 1100, Height = 900, Content = view };
            window.Show();
            try
            {
                window.UpdateLayout();
                DoubleClick(window, view, text.IndexOf("ROBERT", StringComparison.Ordinal) + 2);
                Assert.Equal("PID[1]-5[1].2.2", document.Inspection.SelectedNode!.Path.ToString());
                Assert.Equal("ROBERT", view.Editor.SelectedText);
                Assert.Same(document.Inspection.SelectedNode, view.Inspector.SelectedItem);
                var field = document.Inspection.Fields[4];
                var root = Assert.IsType<TreeViewItem>(view.Inspector.ContainerFromItem(field));
                Assert.True(root.IsExpanded);
                var component = field.VisibleChildren[1];
                var componentContainer = Assert.IsType<TreeViewItem>(root.ContainerFromItem(component));
                Assert.True(componentContainer.IsExpanded);
                var unrelated = Assert.IsType<TreeViewItem>(view.Inspector.ContainerFromItem(document.Inspection.Fields[10]));
                Assert.False(unrelated.IsExpanded);
                var row = Assert.IsAssignableFrom<Control>(view.Inspector.ItemTemplate!.Build(component));
                var pathText = row.GetVisualDescendants().OfType<TextBlock>().First();
                Assert.Equal("PID[1]-5.2", pathText.Text);
                Assert.Null(ToolTip.GetTip(pathText));
                Assert.Null(ToolTip.GetTip(componentContainer));
                Assert.Null(ToolTip.GetTip(row.GetVisualDescendants().OfType<TextBlock>().Last()));

                DoubleClick(window, view, text.IndexOf("THREE", StringComparison.Ordinal) + 2);
                Assert.Equal("PID[1]-11[1].3", document.Inspection.SelectedNode!.Path.ToString());
                Assert.Equal("THREE", view.Editor.SelectedText);
                var addressRoot = Assert.IsType<TreeViewItem>(view.Inspector.ContainerFromItem(document.Inspection.Fields[10]));
                Assert.True(addressRoot.IsExpanded);
                var selectedContainer = Assert.IsType<TreeViewItem>(addressRoot.ContainerFromItem(Assert.IsType<InspectionNode>(document.Inspection.SelectedVisibleNode)));
                var visiblePosition = selectedContainer.TranslatePoint(default, view.Inspector)!.Value;
                Assert.InRange(visiblePosition.Y, 0, view.Inspector.Bounds.Height - 1);

                // A suppressed canonical leaf still selects the meaningful visible component.
                var suppressed = field.Children[0].Children[0].Children[0];
                document.Inspection.SelectNode(suppressed);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(suppressed, document.Inspection.SelectedNode);
                Assert.Same(field.VisibleChildren[0], view.Inspector.SelectedItem);
                Assert.Equal("EXAMPLE", view.Editor.SelectedText);
                view.FieldNumber.Value = 99;
                view.InspectFieldButton.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                var missingPath = view.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text?.StartsWith("PID[1]-99 ·", StringComparison.Ordinal) == true);
                Assert.Null(ToolTip.GetTip(Assert.IsType<Border>(missingPath.GetVisualParent())));
                Assert.Equal(text, view.Editor.Text);
                Assert.False(document.Message.IsModified);
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }

    private static void DoubleClick(Window window, MessageInspectionView view, int offset)
    {
        var location = view.Editor.Document.GetLocation(offset);
        var textView = view.Editor.TextArea.TextView;
        var position = textView.GetVisualPosition(new TextViewPosition(location), VisualYPosition.LineMiddle) - textView.ScrollOffset;
        var point = textView.TranslatePoint(position, window)!.Value;
        // Moving to a different word resets the click series, as with a real pointer.
        window.MouseMove(point);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
