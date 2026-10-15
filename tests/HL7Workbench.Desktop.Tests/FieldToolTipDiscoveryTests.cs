using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class FieldToolTipDiscoveryTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public async Task NaturalMovementKeepsFieldDeadlineAndUsesLatestPath(int kind, bool custom)
    {
        await using var headless = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await headless.Dispatch(async () =>
        {
            var text = "MSH|^~\\&|SYN\rPID|1||SYN-001||Example Long^Ada\r" +
                (kind == 1 ? "NTE|1||" + new string('X', 20000) + "\r" : "");
            if (custom) text = text.Replace('|', '*').Replace('^', '$').Replace('~', '%').Replace('\\', '!').Replace('&', '@');
            using var manager = new DocumentManager(); using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
            var clock = new ManualHoverScheduler(); FullRawInspectorView? full = null;
            var file = kind == 2 ? null : new FileDocumentView(document, true, clock.Schedule);
            Control content = file is not null ? file : full = new(new RawInspectorSession(document), clock.Schedule);
            var border = new Border { Child = content };
            var window = new Window { Width = 1200, Height = 700, Content = border }; window.Show();
            try
            {
                if (full is not null) await full.Ready;
                if (kind == 1) await file!.ActiveView!.PresentationView!.Ready;
                var editor = full?.Editor ?? file!.ActiveView!.Editor;
                var view = editor.TextArea.TextView;
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var selection = (editor.SelectionStart, editor.SelectionLength, editor.CaretOffset);
                var ada = editor.Text.IndexOf("Ada", StringComparison.Ordinal);
                var hit = CellPoint(window, editor, ada, .2, .2);
                PointerEventArgs? moved = null; view.PointerMoved += (_, e) => moved = e;
                window.MouseMove(new(1, 1)); window.MouseMove(hit);
                var firstEvent = Assert.IsAssignableFrom<PointerEventArgs>(moved);
                var scheduled = clock.ScheduledCount; Assert.True(scheduled > 0);
                var obsolete = clock.Callbacks[^1];
                clock.AdvanceBy(TimeSpan.FromMilliseconds(150));
                window.MouseMove(hit + new Vector(1, 0));
                // AvaloniaEdit's retained coordinate no longer decides eligibility.
                firstEvent.RoutedEvent = TextView.PointerHoverEvent; firstEvent.Handled = false; view.RaiseEvent(firstEvent);
                Assert.False(ToolTip.GetIsOpen(view));
                clock.AdvanceBy(TimeSpan.FromMilliseconds(100));
                window.MouseMove(CellPoint(window, editor, ada + 2, .85, .8));
                Assert.Equal(scheduled, clock.ScheduledCount);
                clock.AdvanceBy(TimeSpan.FromMilliseconds(149)); Assert.False(ToolTip.GetIsOpen(view));
                clock.AdvanceBy(TimeSpan.FromMilliseconds(1)); AssertTip(view, "PID[1]-5[1].2");

                window.MouseMove(CellPoint(window, editor, editor.Text.IndexOf("Example", StringComparison.Ordinal), .85, .2));
                AssertTip(view, "PID[1]-5[1].1"); Assert.Equal(scheduled, clock.ScheduledCount);
                window.MouseMove(CellPoint(window, editor, editor.Text.IndexOf(" Long", StringComparison.Ordinal), .4, .8));
                AssertTip(view, "PID[1]-5[1].1"); Assert.Equal(scheduled, clock.ScheduledCount);
                Assert.Equal(selection, (editor.SelectionStart, editor.SelectionLength, editor.CaretOffset));

                window.MouseMove(CellPoint(window, editor, editor.Text.IndexOf("SYN-001", StringComparison.Ordinal), .4, .5));
                Assert.False(ToolTip.GetIsOpen(view)); Assert.Null(ToolTip.GetTip(view));
                Assert.Equal(scheduled + 1, clock.ScheduledCount);
                obsolete(); Assert.False(ToolTip.GetIsOpen(view));
                clock.AdvanceBy(TimeSpan.FromMilliseconds(399)); Assert.False(ToolTip.GetIsOpen(view));
                clock.AdvanceBy(TimeSpan.FromMilliseconds(1)); AssertTip(view, "PID[1]-3");
                firstEvent.RoutedEvent = TextView.PointerHoverEvent; firstEvent.Handled = false; view.RaiseEvent(firstEvent);
                AssertTip(view, "PID[1]-3");

                var line = editor.Document.GetLineByOffset(ada);
                window.MouseMove(CellPoint(window, editor, line.EndOffset, 0, .5) + new Vector(40, 0));
                clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay);
                Assert.False(ToolTip.GetIsOpen(view)); Assert.Null(ToolTip.GetTip(view));
                window.MouseMove(CellPoint(window, editor, ada, .4, .5));
                clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay); AssertTip(view, "PID[1]-5[1].2");
                var click = CellPoint(window, editor, ada, .4, .5);
                window.MouseDown(click, MouseButton.Left); window.MouseUp(click, MouseButton.Left);
                window.MouseDown(click, MouseButton.Left); window.MouseUp(click, MouseButton.Left);
                clock.AdvanceBy(TimeSpan.FromSeconds(2)); Assert.False(ToolTip.GetIsOpen(view));
                border.Padding = new Thickness(8, 0, 0, 0); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                window.MouseMove(click); clock.AdvanceBy(TimeSpan.FromSeconds(2)); Assert.False(ToolTip.GetIsOpen(view));
                window.MouseMove(new(1, 1)); window.MouseMove(CellPoint(window, editor, ada, .6, .5));
                clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay); AssertTip(view, "PID[1]-5[1].2");
            }
            finally { window.Close(); full?.Dispose(); manager.Close(document.Id); await document.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task RenderedRowsAcceptCharacterCellsButNotWhitespacePastWrappedOrFinalRow(bool wrapped)
    {
        await using var headless = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await headless.Dispatch(() =>
        {
            var editor = MessageViewer.CreateEditor("MSH|^~\\&|SYN\rPID|" + new string('X', 180) + "|END");
            editor.WordWrap = wrapped;
            var window = new Window { Width = 350, Height = 650, Content = editor }; window.Show();
            try
            {
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var view = editor.TextArea.TextView;
                var line = view.GetVisualLine(2)!;
                foreach (var row in line.TextLines)
                {
                    var y = line.GetTextLineVisualYPosition(row, VisualYPosition.LineMiddle) - view.ScrollOffset.Y;
                    var x = row.Start + Math.Min(5, row.WidthIncludingTrailingWhitespace / 2) - view.ScrollOffset.X;
                    Assert.NotNull(FieldToolTipLifecycle.OffsetAt(editor, new(x, y)));
                    var past = row.Start + row.WidthIncludingTrailingWhitespace + 3 - view.ScrollOffset.X;
                    Assert.Null(FieldToolTipLifecycle.OffsetAt(editor, new(past, y)));
                }
                Assert.Null(FieldToolTipLifecycle.OffsetAt(editor, new(5, view.Bounds.Height - 1)));
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    private static void AssertTip(TextView view, string path)
    {
        Assert.True(ToolTip.GetIsOpen(view)); Assert.Contains("Path: " + path, Assert.IsType<string>(ToolTip.GetTip(view)));
    }
    private static Point CellPoint(Window window, TextEditor editor, int offset, double x, double y)
    {
        var view = editor.TextArea.TextView;
        var location = editor.Document.GetLocation(offset);
        var top = view.GetVisualPosition(new TextViewPosition(location), VisualYPosition.LineTop);
        var bottom = view.GetVisualPosition(new TextViewPosition(location), VisualYPosition.LineBottom);
        var next = offset < editor.Document.TextLength ? view.GetVisualPosition(new TextViewPosition(editor.Document.GetLocation(offset + 1)), VisualYPosition.LineTop) : top;
        return view.TranslatePoint(new Point(top.X + (next.X - top.X) * x, top.Y + (bottom.Y - top.Y) * y) - view.ScrollOffset, window)!.Value;
    }
}
