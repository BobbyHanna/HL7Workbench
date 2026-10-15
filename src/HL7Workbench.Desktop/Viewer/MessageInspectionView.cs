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
    private MessageInspection inspection;
    private readonly MessageDocument? document;
    private bool updatingText;
    private readonly CheckBox editMode = new() { Content = "Edit Mode", Margin = new(4) };
    private readonly Button undo = new() { Content = "Undo", Margin = new(4) };
    private readonly Button redo = new() { Content = "Redo", Margin = new(4) };
    public Action<HL7Workbench.Core.Hl7Message?>? HighlightChanged { get; set; }
    private bool synchronizing;
    private bool subscribed;
    private InspectionPanes? panes;
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
    private bool HasCurrentInspection => inspection.IsCurrent && (document is null ||
        document.InspectionState == DocumentInspectionState.Current && ReferenceEquals(document.ActiveInspection, inspection));
    public MessageInspectionView(MessageInspection inspection, TextEditor editor, MessageDocument? document = null, bool dockedInspection = false)
    {
        this.inspection = inspection;
        this.document = document;

        Editor = editor;
        if (document is not null && document.Presentation is null) ViewerRequests.AttachMenu(new(editor, document, inspection));
        if (document?.Presentation is not null) PresentationView = new(document, inspection, editor);
        else editor.TextArea.TextView.LineTransformers.Add(new SelectionColorizer(() => HasCurrentInspection ? this.inspection : null));
        // Workspace owns history across navigation and view reconstruction. Limit
        // local retention before input starts: TextChanged can run inside AvaloniaEdit's
        // input undo group, where clearing the stack would violate its transaction.
        if (document is not null && PresentationView is null) editor.Document.UndoStack.SizeLimit = 0;
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
        if (document is not null)
        {
            var bar = new StackPanel { Orientation = Orientation.Horizontal, Children = { editMode, undo, redo, WindowStatus } };
            SetRow(bar, 1); RawPane.Children.Add(bar);
            ToolTip.SetTip(editMode, "Enable manual editing for this document. Turning it off retains unsaved changes.");
            editMode.IsCheckedChanged += (_, _) => { if (!updatingText) document.IsEditMode = editMode.IsChecked == true; };
            undo.Click += (_, _) => document.Undo(); redo.Click += (_, _) => document.Redo();
            Editor.TextArea.AddHandler(KeyDownEvent, EditKeyDown, RoutingStrategies.Tunnel);
            UpdateEditingControls();
        }
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (subscribed) return;
        subscribed = true;
        if (document is not null) document.PropertyChanged += DocumentChanged;
        inspection.SelectionChanged += SelectionChanged;
        if (PresentationView is not null) { ApplySelection(false); return; }
        Editor.TextArea.Caret.PositionChanged += CaretChanged;
        Editor.TextChanged += WorkingTextChanged;
        UpdateFromDocument();
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
        if (document is not null) document.PropertyChanged -= DocumentChanged;
        Editor.TextChanged -= WorkingTextChanged;
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

    private void WorkingTextChanged(object? sender, EventArgs e)
    {
        if (updatingText || document is null || PresentationView is not null) return;
        try { document.ReplaceWorkingText(Editor.Text); }
        catch (Exception error) when (error is InvalidOperationException or HL7Workbench.Core.Hl7LimitException)
        {
            updatingText = true;
            try { var caret = Editor.CaretOffset; Editor.Text = document.EditableText ?? ""; Editor.CaretOffset = Math.Min(caret, Editor.Document.TextLength); }
            finally { updatingText = false; }
            WindowStatus.Text = "Edit refused: the document edit/history budget was reached or editing is unavailable. Existing work is retained.";
        }
    }
    private void EditKeyDown(object? sender, KeyEventArgs e)
    {
        if (document is null || PresentationView is not null) return;
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && e.Key is Key.Z or Key.Y)
        {
            e.Handled = true;
            if (e.Key == Key.Y || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) document.Redo(); else document.Undo();
        }
    }
    private void DocumentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "ActiveContent") UpdateFromDocument(); else UpdateEditingControls();
    }
    private void UpdateEditingControls()
    {
        if (document is null) return;
        updatingText = true;
        try { editMode.IsChecked = document.IsEditMode; }
        finally { updatingText = false; }
        Editor.IsReadOnly = PresentationView is not null || !document.CanEditText;
        undo.IsEnabled = document.IsEditMode && document.CanUndo; redo.IsEnabled = document.IsEditMode && document.CanRedo;
        WindowStatus.Text = document.EditError ?? (document.IsRefreshingEdit ? "Refreshing structure…" : document.EditingUnavailableReason ??
            (document.HasWorkingSourceChanges ? "Working text · use File → Save to publish edits" : ""));
    }
    private void UpdateFromDocument()
    {
        if (document is null) return;
        UpdateEditingControls();
        if (PresentationView is not null || document.IsLoading) return;
        CloseRawToolTip();
        updatingText = true; synchronizing = true;
        try
        {
            if (document.EditableText is { } text && Editor.Text != text)
            {
                var caret = Editor.CaretOffset; Editor.Text = text;
                Editor.CaretOffset = Math.Min(caret, text.Length);
            }
            var next = document.InspectionSnapshot;
            if (next is not null && !ReferenceEquals(next, inspection))
            {
                inspection.SelectionChanged -= SelectionChanged; inspection = next;
                inspection.SelectionChanged += SelectionChanged;
            }
            if (next is not null) panes?.UpdateInspection(next);
            if (document.ActiveInspection is not null && document.WorkingText is not null) inspection.SelectOffset(Editor.CaretOffset);
            HighlightChanged?.Invoke(document.MaterializedMessage);
            Editor.TextArea.TextView.Redraw();
        }
        finally { updatingText = false; synchronizing = false; }
    }

    private void SelectionChanged(object? sender, InspectionSelectionChangedEventArgs e) => ApplySelection(e.NavigateRaw);
    private void CaretChanged(object? sender, EventArgs e)
    {
        if (!synchronizing && HasCurrentInspection) inspection.SelectOffset(Editor.CaretOffset);
    }

    private void RawPointerHover(object? sender, PointerEventArgs e)
    {
        if (!HasCurrentInspection) { CloseRawToolTip(); return; }
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
        if (!HasCurrentInspection) return;
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
        if (!HasCurrentInspection) return;
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

internal sealed class SelectionColorizer(Func<MessageInspection?> current, Func<long>? origin = null) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        if (current()?.SelectedRange64 is not { Length: > 0 } range) return;
        var start = Math.Max(line.Offset, range.Offset - (origin?.Invoke() ?? 0));
        var end = Math.Min(line.EndOffset, range.End - (origin?.Invoke() ?? 0));
        // A persistent background remains visible when keyboard focus moves into the inspector.
        if (start < end) ChangeLinePart(checked((int)start), checked((int)end),
            element => element.TextRunProperties.SetBackgroundBrush(new SolidColorBrush(Color.Parse("#384A65"))));
    }
}
