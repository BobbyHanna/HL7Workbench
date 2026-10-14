using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Diagnostics;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using Dock.Avalonia.Controls;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class RawEditorToolTipTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HoveringActualRawGlyphOpensCoordinateTipWithoutSelectingEvenAfterReconstruction(bool custom)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            const string standard = "MSH|^~\\&|SYN\rPID|||||SMITH^JOHN~JONES^MARY&ANN\rOBX|1|TX|||SYNTHETIC\rPID|||||SECOND^PERSON";
            var text = custom ? standard.Replace('|', '*').Replace('^', '$').Replace('~', '%').Replace('\\', '!').Replace('&', '@') : standard;
            var manager = new DocumentManager();
            var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(text));
            manager.Open(document);
            var tools = new ToolWindowRegistry();
            tools.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
            tools.Register(new(WorkspaceDockFactory.DiagnosticsId, "Diagnostics", () => new object()));
            using var factory = new WorkspaceDockFactory(manager, tools);
            var dock = new DockControl { Layout = factory.CreateLayout() };
            var window = new Window { Width = 1400, Height = 1100, Content = dock };
            window.Show();
            try
            {
                for (var generation = 0; generation < 2; generation++)
                {
                    if (generation != 0) dock.Layout = factory.CreateLayout();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var view = Assert.Single(dock.GetVisualDescendants().OfType<MessageInspectionView>());
                    var textView = view.Editor.TextArea.TextView;
                    // Preserve an actual multi-line user selection, not just an empty caret.
                    view.Editor.Select(0, text.IndexOf("PID", StringComparison.Ordinal) + 4);
                    window.UpdateLayout();
                    var fields = document.Inspection.Fields;
                    var selected = document.Inspection.SelectedNode;
                    var segment = document.Inspection.SelectedSegment;
                    var caret = view.Editor.CaretOffset;
                    var start = view.Editor.SelectionStart;
                    var length = view.Editor.SelectionLength;
                    foreach (var (value, expected) in new[]
                    {
                        ("SMITH", "PID[1]-5[1].1"), ("JOHN", "PID[1]-5[1].2"),
                        ("ANN", "PID[1]-5[2].2.2"), ("SYNTHETIC", "OBX[1]-5"),
                        ("PERSON", "PID[2]-5[1].2")
                    })
                    {
                        var point = GlyphPoint(window, view, text.IndexOf(value, StringComparison.Ordinal));
                        window.MouseMove(new Point(1, 1));
                        window.MouseMove(point);
                        await WaitForHover();
                        Assert.True(ToolTip.GetIsOpen(textView));
                        var popup = Assert.IsType<ToolTip>(textView.GetValue(ToolTipDiagnostics.ToolTipProperty));
                        Assert.NotNull(popup.GetVisualParent());
                        Assert.True(popup.Bounds.Width > 0 && popup.Bounds.Height > 0);
                        var tip = Assert.IsType<string>(ToolTip.GetTip(textView));
                        Assert.Contains($"Path: {expected}", tip);
                        Assert.DoesNotContain(value, tip);
                        Assert.DoesNotContain("State:", tip);
                        Assert.Equal(PlacementMode.Pointer, ToolTip.GetPlacement(textView));
                        Assert.Same(fields, document.Inspection.Fields);
                        Assert.Same(selected, document.Inspection.SelectedNode);
                        Assert.Same(segment, document.Inspection.SelectedSegment);
                        Assert.Equal(caret, view.Editor.CaretOffset);
                        Assert.Equal(start, view.Editor.SelectionStart);
                        Assert.Equal(length, view.Editor.SelectionLength);
                    }
                    window.MouseMove(new Point(1, 1));
                    Dispatcher.UIThread.RunJobs();
                    Assert.False(ToolTip.GetIsOpen(textView));
                    Assert.Null(ToolTip.GetTip(textView));
                    Assert.Equal(text, view.Editor.Text);
                    Assert.False(document.Message.IsModified);
                    Assert.True(view.Editor.IsReadOnly);
                    Assert.All(view.Inspector.GetVisualDescendants().OfType<Control>(), c => Assert.Null(ToolTip.GetTip(c)));
                }
            }
            finally { window.Close(); }
            return 0;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GlyphFloorBoundariesScrollingDismissalAndDetachedCleanup()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            var text = "MSH|^~\\&|SYN\rPID||\"\"|A^B&C~D\r\nbad line\r" +
                string.Concat(Enumerable.Range(1, 100).Select(i => $"OBX|{new string('X', 150)}^SYN-{i}\r"));
            var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(text));
            var view = Assert.IsType<MessageInspectionView>(MessageViewer.Create(document));
            var window = new Window { Width = 1000, Height = 800, Content = view };
            window.Show();
            try
            {
                window.UpdateLayout();
                var textView = view.Editor.TextArea.TextView;
                foreach (var (offset, path) in new (int, string?)[]
                {
                    (3, "MSH[1]-1"), (4, "MSH[1]-2"), (8, null),
                    (text.IndexOf("PID", StringComparison.Ordinal), null),
                    (text.IndexOf("||", StringComparison.Ordinal) + 1, null),
                    (text.IndexOf('"'), "PID[1]-2"),
                    (text.IndexOf("A^", StringComparison.Ordinal) + 1, "PID[1]-3[1]"),
                    (text.IndexOf('&'), "MSH[1]-2"),
                    (text.IndexOf("bad line", StringComparison.Ordinal) + 4, null)
                })
                {
                    window.MouseMove(new Point(1, 1));
                    // Right half of the glyph is still that character, not the next caret position.
                    window.MouseMove(GlyphPoint(window, view, offset, .85));
                    await WaitForHover();
                    Assert.Equal(path is not null, ToolTip.GetIsOpen(textView));
                    if (path is not null) Assert.Contains($"Path: {path}", Assert.IsType<string>(ToolTip.GetTip(textView)));
                }
                var line = view.Editor.Document.GetLineByNumber(2);
                var endPoint = GlyphPoint(window, view, line.EndOffset) + new Vector(50, 0);
                window.MouseMove(endPoint);
                await WaitForHover();
                Assert.False(ToolTip.GetIsOpen(textView));

                var offsetLast = text.LastIndexOf("SYN-100", StringComparison.Ordinal);
                var location = view.Editor.Document.GetLocation(offsetLast);
                view.Editor.ScrollTo(location.Line, location.Column);
                window.UpdateLayout();
                // Exercise the scrollbar's actual offset, independent of ScrollTo's column policy.
                var scrollViewer = view.Editor.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "PART_ScrollViewer");
                scrollViewer.Offset = new Vector(1800, scrollViewer.Offset.Y);
                window.UpdateLayout();
                window.MouseMove(new Point(1, 1));
                var scrolledPoint = GlyphPoint(window, view, offsetLast);
                window.MouseMove(scrolledPoint);
                await WaitForHover();
                Assert.True(ToolTip.GetIsOpen(textView), $"Scrolled hover={scrolledPoint}; scroll={textView.ScrollOffset}; bounds={textView.Bounds}; valid={textView.VisualLinesValid}");
                Assert.Contains("Path: OBX[100]-1[1].2", Assert.IsType<string>(ToolTip.GetTip(textView)));
                window.MouseDown(scrolledPoint, MouseButton.Left);
                Assert.False(ToolTip.GetIsOpen(textView));
                window.MouseUp(scrolledPoint, MouseButton.Left);
                view.Editor.ScrollToHome();
                window.UpdateLayout();
                Assert.False(ToolTip.GetIsOpen(textView));
                window.MouseMove(new Point(1, 1));
                window.MouseMove(GlyphPoint(window, view, 4));
                await WaitForHover();
                Assert.True(ToolTip.GetIsOpen(textView));
                window.Content = null;
                Assert.False(ToolTip.GetIsOpen(textView));
                Assert.Null(ToolTip.GetTip(textView));
            }
            finally { window.Close(); }
            return 0;
        }, TestContext.Current.CancellationToken);
    }

    private static Point GlyphPoint(Window window, MessageInspectionView view, int offset, double fraction = .4)
    {
        var textView = view.Editor.TextArea.TextView;
        var location = view.Editor.Document.GetLocation(offset);
        var position = textView.GetVisualPosition(new TextViewPosition(location), VisualYPosition.LineMiddle) - textView.ScrollOffset;
        var next = textView.GetVisualPosition(new TextViewPosition(location.Line, location.Column + 1), VisualYPosition.LineMiddle) - textView.ScrollOffset;
        return textView.TranslatePoint(position + new Vector((next.X - position.X) * fraction, 0), window)!.Value;
    }

    private static Task WaitForHover() => Task.Delay(650, TestContext.Current.CancellationToken);
}

public sealed class RawHoverTestApplication : App
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<RawHoverTestApplication>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}
