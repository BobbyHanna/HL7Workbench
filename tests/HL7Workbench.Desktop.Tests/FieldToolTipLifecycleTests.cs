using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class FieldToolTipLifecycleTests
{
    [Theory]
    [InlineData(0, false)] [InlineData(0, true)]
    [InlineData(1, false)] [InlineData(1, true)]
    [InlineData(2, false)] [InlineData(2, true)]
    public async Task StationaryDoubleClickStaysDismissedAcrossRepeatedEnterHoverAndLayout(int kind, bool changeLayout)
    {
        await using var headless = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await headless.Dispatch(async () =>
        {
            const string prefix = "MSH|^~\\&|SYN\rPID|1||SYN-001||Example^Ada\r";
            var text = prefix + (kind == 1 ? "NTE|1||" + new string('X', 20000) + "\r" : "");
            using var manager = new DocumentManager(); using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
            var clock = new ManualHoverScheduler();
            FullRawInspectorView? full = null;
            var file = kind != 2 ? new FileDocumentView(document, true, clock.Schedule) : null;
            Control content = file is not null ? file : full = new(new RawInspectorSession(document), clock.Schedule);
            var border = new Border { Child = content };
            var window = new Window { Width = 1200, Height = 800, Content = border }; window.Show();
            try
            {
                if (full is not null) await full.Ready;
                if (kind == 1) await Assert.IsType<PresentationView>(file!.ActiveView!.PresentationView).Ready;
                if (kind == 0) Assert.Null(file!.ActiveView!.PresentationView);
                var editor = full?.Editor ?? file!.ActiveView!.Editor;
                var view = editor.TextArea.TextView;
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                PointerEventArgs? moved = null;
                view.PointerMoved += (_, e) => moved = e;
                var hit = GlyphPoint(window, editor, editor.Text.IndexOf("Ada", StringComparison.Ordinal));
                window.MouseMove(new(1, 1)); window.MouseMove(hit);
                var beforePress = Assert.IsAssignableFrom<PointerEventArgs>(moved);
                Notify(view, beforePress, TextView.PointerHoverEvent);
                clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay);
                Assert.False(ToolTip.GetServiceEnabled(view)); Assert.True(ToolTip.GetIsOpen(view));
                Assert.Contains("PID[1]-5[1].2", Assert.IsType<string>(ToolTip.GetTip(view)));

                window.MouseDown(hit, MouseButton.Left); window.MouseUp(hit, MouseButton.Left);
                window.MouseDown(hit, MouseButton.Left); window.MouseUp(hit, MouseButton.Left);
                Assert.Equal("PID[1]-5[1].2", document.Inspection.SelectedNode!.Path.ToString());
                var selection = (editor.SelectionStart, editor.SelectionLength, editor.CaretOffset);
                Assert.False(ToolTip.GetIsOpen(view));

                // Enter notifications can restart AvaloniaEdit's timer without physical
                // movement (popup closure or a new hit test after inspection/layout).
                for (var repetition = 0; repetition < 3; repetition++)
                {
                    Notify(view, beforePress, InputElement.PointerExitedEvent);
                    Notify(view, beforePress, InputElement.PointerEnteredEvent);
                    Notify(view, beforePress, TextView.PointerHoverEvent);
                    clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay);
                    Assert.False(ToolTip.GetIsOpen(view)); Assert.Null(ToolTip.GetTip(view));
                }
                // Even an independent pending open must pass Avalonia's opening gate.
                ToolTip.SetIsOpen(view, true); Assert.False(ToolTip.GetIsOpen(view));
                if (changeLayout)
                {
                    border.Padding = new Thickness(8, 0, 0, 0); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                }
                // A native re-hit-test can report a move at the same window position.
                // Its TextView-relative position changes when the editor has moved.
                window.MouseMove(hit);
                Notify(view, Assert.IsAssignableFrom<PointerEventArgs>(moved), TextView.PointerHoverEvent);
                clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay);
                Assert.False(ToolTip.GetIsOpen(view)); Assert.Null(ToolTip.GetTip(view));
                Assert.Equal(selection, (editor.SelectionStart, editor.SelectionLength, editor.CaretOffset));

                var other = GlyphPoint(window, editor, editor.Text.IndexOf("SYN-001", StringComparison.Ordinal));
                window.MouseMove(other); Notify(view, moved!, TextView.PointerHoverEvent);
                clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay);
                Assert.True(ToolTip.GetIsOpen(view));
                Assert.Contains("PID[1]-3", Assert.IsType<string>(ToolTip.GetTip(view)));
                // A delayed notification from the former position cannot replace this tip.
                Notify(view, beforePress, TextView.PointerHoverEvent);
                Assert.Contains("PID[1]-3", Assert.IsType<string>(ToolTip.GetTip(view)));
                window.MouseMove(new(1, 1));
                window.MouseMove(GlyphPoint(window, editor, editor.Text.IndexOf("Ada", StringComparison.Ordinal)));
                Notify(view, moved!, TextView.PointerHoverEvent);
                clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay);
                Assert.True(ToolTip.GetIsOpen(view));
                Assert.Contains("PID[1]-5[1].2", Assert.IsType<string>(ToolTip.GetTip(view)));
                Assert.Equal(selection, (editor.SelectionStart, editor.SelectionLength, editor.CaretOffset));
                window.Content = null;
                Notify(view, moved!, TextView.PointerHoverEvent);
                Assert.False(ToolTip.GetIsOpen(view)); Assert.Null(ToolTip.GetTip(view));
            }
            finally { window.Close(); full?.Dispose(); manager.Close(document.Id); await document.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task HeldMovementReleaseAndReattachmentDoNotRearmDismissal()
    {
        await using var headless = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await headless.Dispatch(() =>
        {
            var editor = MessageViewer.CreateEditor("MSH|^~\\&|SYN\rPID|SYNTHETIC");
            var clock = new ManualHoverScheduler();
            var tips = new FieldToolTipLifecycle(editor, _ => new("field", "Synthetic path"), clock.Schedule);
            var view = editor.TextArea.TextView;
            var window = new Window { Width = 700, Height = 400, Content = editor }; window.Show();
            try
            {
                window.UpdateLayout();
                PointerEventArgs? moved = null; view.PointerMoved += (_, e) => moved = e;
                var hit = GlyphPoint(window, editor, 4);
                window.MouseMove(hit); clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay); Assert.True(ToolTip.GetIsOpen(view));
                window.MouseDown(hit, MouseButton.Left);
                window.MouseMove(hit + new Vector(8, 0)); clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay); Assert.False(ToolTip.GetIsOpen(view));
                window.MouseUp(hit + new Vector(8, 0), MouseButton.Left);
                // Releasing at the dragged position does not turn the held move into a hover.
                clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay); Assert.False(ToolTip.GetIsOpen(view));
                window.Content = null; window.Content = editor; window.UpdateLayout();
                clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay); Assert.False(ToolTip.GetIsOpen(view));
                ToolTip.SetIsOpen(view, true); Assert.False(ToolTip.GetIsOpen(view));
                window.MouseMove(hit + new Vector(9, 0)); clock.AdvanceBy(FieldToolTipLifecycle.HoverDelay); Assert.True(ToolTip.GetIsOpen(view));
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    private static void Notify(TextView view, PointerEventArgs args, RoutedEvent<PointerEventArgs> routedEvent)
    {
        args.RoutedEvent = routedEvent; args.Handled = false; view.RaiseEvent(args);
    }

    private static Point GlyphPoint(Window window, TextEditor editor, int offset)
    {
        var view = editor.TextArea.TextView;
        var position = view.GetVisualPosition(new TextViewPosition(editor.Document.GetLocation(offset)), VisualYPosition.LineMiddle);
        return view.TranslatePoint(position - view.ScrollOffset + new Vector(2, 0), window)!.Value;
    }
}
