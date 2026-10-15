using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Rendering;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

internal sealed record FieldToolTipTarget(object Key, string Text)
{
    public static FieldToolTipTarget? From(InspectionNode? node) => node is null ? null : new(
        new Hl7Path(node.Path.Segment, node.Path.Field, node.Path.Occurrence), InspectionPathToolTip.Format(node.PathInfo));
}

/// <summary>Acquires displayed structural targets, schedules field hover, and retains click dismissal.</summary>
internal sealed class FieldToolTipLifecycle
{
    // Retain AvaloniaEdit's normal hover interval, but measure it per structural field,
    // not from a two-pixel box and a retained PointerEventArgs instance.
    internal static readonly TimeSpan HoverDelay = TimeSpan.FromMilliseconds(400);
    private readonly TextEditor editor;
    private readonly TextView view;
    private readonly Func<int, FieldToolTipTarget?> resolve;
    private readonly Func<Action, TimeSpan, IDisposable> schedule;
    private PixelPoint? pointerPosition;
    private FieldToolTipTarget? target;
    private IDisposable? pending;
    private long generation;
    private bool dismissed;
    private bool pressed;

    public FieldToolTipLifecycle(TextEditor editor, Func<int, FieldToolTipTarget?> resolve,
        Func<Action, TimeSpan, IDisposable>? schedule = null)
    {
        this.editor = editor; view = editor.TextArea.TextView; this.resolve = resolve;
        this.schedule = schedule ?? ((action, delay) => DispatcherTimer.RunOnce(action, delay, DispatcherPriority.Background));
        ToolTip.SetServiceEnabled(view, false); ToolTip.SetPlacement(view, PlacementMode.Pointer);
        editor.TextArea.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            pressed = true; pointerPosition = Position(e); Close();
        }, RoutingStrategies.Tunnel, true);
        editor.TextArea.AddHandler(InputElement.PointerReleasedEvent, (_, _) => pressed = false, RoutingStrategies.Tunnel, true);
        editor.TextArea.AddHandler(InputElement.PointerMovedEvent, (_, e) => Observe(e, true), RoutingStrategies.Tunnel, true);
        view.PointerEntered += (_, e) => Observe(e, false);
        view.PointerExited += (_, e) =>
        {
            Observe(e, true);
            // Popup/layout hit tests can report an exit at the same in-row position.
            if (CurrentOffset() is null) ClearTarget();
        };
        view.ScrollOffsetChanged += (_, _) => Close();
        editor.TextChanged += (_, _) => Close();
        view.DocumentChanged += (_, _) => Close();
        editor.DetachedFromVisualTree += (_, _) => { Close(); pointerPosition = null; };
        view.LayoutUpdated += (_, _) => Revalidate();
        view.AddHandler(ToolTip.ToolTipOpeningEvent, (_, e) =>
        {
            if (dismissed || target is null || TopLevel.GetTopLevel(view) is null) e.Cancel = true;
        });
    }

    private PixelPoint? Position(PointerEventArgs e)
    {
        var root = TopLevel.GetTopLevel(view);
        return root?.PointToScreen(e.GetPosition(root));
    }

    private int? CurrentOffset()
    {
        if (pointerPosition is not { } screen || TopLevel.GetTopLevel(view) is not { } root) return null;
        var point = root.TranslatePoint(root.PointToClient(screen), view);
        return point is { } p ? OffsetAt(editor, p) : null;
    }

    /// <summary>Accepts the rendered text row and complete character cell, including space inside a value.</summary>
    internal static int? OffsetAt(TextEditor editor, Point point)
    {
        var view = editor.TextArea.TextView;
        if (!view.VisualLinesValid || !new Rect(view.Bounds.Size).Contains(point)) return null;
        var documentPoint = point + view.ScrollOffset;
        var position = view.GetPositionFloor(documentPoint);
        if (position is not { } hit) return null;
        var line = editor.Document.GetLineByNumber(hit.Line);
        if (hit.Column > line.Length) return null;
        // GetPositionFloor clamps horizontal hits. Reject blank space past this
        // rendered row (including wrapped rows), rather than inventing a last-field hit.
        var visual = view.GetVisualLineFromVisualTop(documentPoint.Y);
        var row = visual?.TextLines.FirstOrDefault(t => documentPoint.Y >= visual.GetTextLineVisualYPosition(t, VisualYPosition.LineTop) &&
            documentPoint.Y < visual.GetTextLineVisualYPosition(t, VisualYPosition.LineBottom));
        if (row is null || documentPoint.X < row.Start || documentPoint.X >= row.Start + row.WidthIncludingTrailingWhitespace) return null;
        return editor.Document.GetOffset(hit.Location);
    }

    private FieldToolTipTarget? Acquire() => CurrentOffset() is { } offset ? resolve(offset) : null;

    private void Observe(PointerEventArgs e, bool movement)
    {
        var position = Position(e);
        var changed = position is not null && position != pointerPosition;
        pointerPosition = position;
        var buttons = e.GetCurrentPoint(view).Properties;
        if (pressed || buttons.IsLeftButtonPressed || buttons.IsRightButtonPressed) { Close(); return; }
        if (dismissed)
        {
            // Enter/exit, release and same-screen-position moves never rearm a click
            // or scroll dismissal. Movement while dragging also cannot rearm it.
            if (!movement || !changed) return;
            dismissed = false;
        }
        var next = Acquire();
        if (next is null) { ClearTarget(); return; }
        if (target?.Key.Equals(next.Key) == true)
        {
            target = next;
            if (ToolTip.GetIsOpen(view)) ToolTip.SetTip(view, next.Text);
            return;
        }
        ClearTarget(); target = next;
        var version = generation;
        pending = schedule(() =>
        {
            if (version != generation || dismissed || target is null) return;
            pending?.Dispose(); pending = null;
            var current = Acquire();
            if (current is null || !current.Key.Equals(target.Key)) { Close(); return; }
            target = current;
            ToolTip.SetTip(view, current.Text); ToolTip.SetIsOpen(view, true);
        }, HoverDelay);
    }

    private void Revalidate()
    {
        if (target is null || !view.VisualLinesValid) return;
        var current = Acquire();
        if (current is null || !current.Key.Equals(target.Key)) { Close(); return; }
        target = current;
        if (ToolTip.GetIsOpen(view)) ToolTip.SetTip(view, current.Text);
    }

    private void ClearTarget()
    {
        generation++; pending?.Dispose(); pending = null; target = null;
        ToolTip.SetIsOpen(view, false); ToolTip.SetTip(view, null);
    }

    public void Close() { dismissed |= pointerPosition is not null; ClearTarget(); }
}
