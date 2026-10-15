using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Raw viewer with optional embedded structural panes; Dock adapters use the shared pane controls.</summary>
public sealed class MessageInspectionView : Grid
{
    private readonly MessageInspection inspection;
    private bool synchronizing;
    private bool subscribed;
    private readonly InspectionPanes? panes;
    public TextEditor Editor { get; }
    public PresentationView? PresentationView { get; }
    public Grid RawPane { get; } = new() { RowDefinitions = new("*,Auto,Auto") };
    public TextBlock WindowStatus { get; } = new();
    public ListBox Navigator => panes!.Navigator;
    public TreeView Inspector => panes!.Inspector;
    public Grid LowerPane => panes!;
    public NumericUpDown FieldNumber => panes!.FieldNumber;
    public Button InspectFieldButton => panes!.InspectFieldButton;
    public TextBlock ValueStatus => panes!.ValueStatus;
    public Task LoadPreviewAsync(long offset) => panes!.LoadPreviewAsync(offset);
    public MessageInspectionView(MessageInspection inspection, TextEditor editor, MessageDocument? document = null, bool dockedInspection = false)
    {
        this.inspection = inspection;

        Editor = editor;
        if (document is not null && document.Presentation is null) ViewerRequests.AttachMenu(new(editor, document, inspection));
        if (document?.Presentation is not null) PresentationView = new(document, inspection, editor);
        else editor.TextArea.TextView.LineTransformers.Add(new SelectionColorizer(inspection));
        RawPane.Children.Add((Control?)PresentationView ?? editor); Children.Add(RawPane);
        if (dockedInspection) RowDefinitions = new("*");
        else
        {
            panes = new(inspection, document); RowDefinitions = new("65*,6,35*");
            RowDefinitions[0].MinHeight = 80; RowDefinitions[2].MinHeight = 150; SetRow(panes, 2);
            Children.Add(new GridSplitter { ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext,
                HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, [RowProperty] = 1 });
            Children.Add(panes);
        }
        ApplySelection(false);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (subscribed) return;
        subscribed = true;
        inspection.SelectionChanged += SelectionChanged;
        if (PresentationView is not null) { ApplySelection(false); return; }
        Editor.TextArea.Caret.PositionChanged += CaretChanged;
        Editor.TextArea.AddHandler(PointerPressedEvent, RawPointerPressed, RoutingStrategies.Tunnel);
        var textView = Editor.TextArea.TextView;
        // A TextView contains many coordinates within one control. AvaloniaEdit supplies
        // the hover timing; a conventional Avalonia ToolTip displays the resolved position.
        ToolTip.SetServiceEnabled(textView, false);
        ToolTip.SetPlacement(textView, PlacementMode.Pointer);
        textView.PointerHover += RawPointerHover;
        textView.PointerHoverStopped += RawHoverStopped;
        textView.PointerExited += RawHoverStopped;
        textView.ScrollOffsetChanged += RawScrolled;
        textView.AddHandler(PointerPressedEvent, RawHoverStopped, RoutingStrategies.Tunnel, true);
        ApplySelection(true);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        // Document state outlives reconstructed Dock templates; never let it retain discarded views.
        inspection.SelectionChanged -= SelectionChanged;
        Editor.TextArea.Caret.PositionChanged -= CaretChanged;
        Editor.TextArea.RemoveHandler(PointerPressedEvent, RawPointerPressed);
        var textView = Editor.TextArea.TextView;
        textView.PointerHover -= RawPointerHover;
        textView.PointerHoverStopped -= RawHoverStopped;
        textView.PointerExited -= RawHoverStopped;
        textView.ScrollOffsetChanged -= RawScrolled;
        textView.RemoveHandler(PointerPressedEvent, RawHoverStopped);
        CloseRawToolTip();
        subscribed = false;
        base.OnDetachedFromVisualTree(e);
    }

    private void SelectionChanged(object? sender, InspectionSelectionChangedEventArgs e) => ApplySelection(e.NavigateRaw);
    private void CaretChanged(object? sender, EventArgs e)
    {
        if (!synchronizing) inspection.SelectOffset(Editor.CaretOffset);
    }

    private void RawPointerHover(object? sender, PointerEventArgs e)
    {
        var textView = Editor.TextArea.TextView;
        var point = e.GetPosition(textView);
        var buttons = e.GetCurrentPoint(textView).Properties;
        if (buttons.IsLeftButtonPressed || buttons.IsRightButtonPressed ||
            !textView.Bounds.WithX(0).WithY(0).Contains(point) || !textView.VisualLinesValid) return;
        // Floor resolves the glyph under the pointer; nearest-caret rounding can falsely
        // report the next component when hovering the right half of a separator/value.
        var position = textView.GetPositionFloor(point + textView.ScrollOffset);
        var node = position is { } hit && hit.Column <= Editor.Document.GetLineByNumber(hit.Line).Length ?
            inspection.ResolveOffset(Editor.Document.GetOffset(hit.Location)) : null;
        if (node is null) { CloseRawToolTip(); return; }
        ToolTip.SetTip(textView, InspectionPathToolTip.Format(node.PathInfo));
        ToolTip.SetIsOpen(textView, true);
    }

    private void RawHoverStopped(object? sender, PointerEventArgs e) => CloseRawToolTip();
    private void RawScrolled(object? sender, EventArgs e) => CloseRawToolTip();
    private void CloseRawToolTip()
    {
        var textView = Editor.TextArea.TextView;
        ToolTip.SetIsOpen(textView, false);
        ToolTip.SetTip(textView, null);
    }

    private void RawPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.ClickCount != 2 || !e.GetCurrentPoint(Editor).Properties.IsLeftButtonPressed) return;
        var position = Editor.GetPositionFromPoint(e.GetPosition(Editor));
        if (position is null) return;
        // Handle only the double-click, before AvaloniaEdit's word selection. Single-click,
        // dragging, keyboard selection and copy keep the accepted editor behavior.
        e.Handled = true;
        inspection.SelectDeepestOffset(Editor.Document.GetOffset(position.Value.Location));
    }

    private void ApplySelection(bool navigateRaw)
    {
        synchronizing = true;
        try
        {
            if (PresentationView is null && navigateRaw && inspection.SelectedRange64 is { } range)
            {
                SelectVisibleRange(range);
                var location = Editor.Document.GetLocation(checked((int)Math.Clamp(range.Offset, 0, Editor.Document.TextLength)));
                Editor.ScrollTo(location.Line, location.Column);
            }
            else if (PresentationView is null && navigateRaw) Editor.Select(Editor.CaretOffset, 0);
            Editor.TextArea.TextView.Redraw();
        }
        finally { synchronizing = false; }
    }

    private void SelectVisibleRange(HL7Workbench.Core.CharacterRange range)
    {
        var start = Math.Clamp(range.Offset, 0, Editor.Document.TextLength);
        var end = Math.Clamp(range.End, start, Editor.Document.TextLength);
        Editor.Select(checked((int)start), checked((int)(end - start)));
    }

}

internal sealed class SelectionColorizer(MessageInspection inspection, Func<long>? origin = null) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        if (inspection.SelectedRange64 is not { Length: > 0 } range) return;
        var start = Math.Max(line.Offset, range.Offset - (origin?.Invoke() ?? 0));
        var end = Math.Min(line.EndOffset, range.End - (origin?.Invoke() ?? 0));
        // A persistent background remains visible when keyboard focus moves into the inspector.
        if (start < end) ChangeLinePart(checked((int)start), checked((int)end),
            element => element.TextRunProperties.SetBackgroundBrush(new SolidColorBrush(Color.Parse("#384A65"))));
    }
}
