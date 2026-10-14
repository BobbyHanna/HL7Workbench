using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Document-internal panes. These are ordinary grids, not Dock tools.</summary>
public sealed class MessageInspectionView : Grid
{
    private readonly MessageInspection inspection;
    private readonly TextBlock selectionTitle = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Border selectionHeader = new() { Background = Brushes.Transparent };
    private readonly TextBox rawValue = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap };
    private readonly TextBlock fieldStatus = new() { TextWrapping = TextWrapping.Wrap };
    private bool synchronizing;
    private bool subscribed;
    private IReadOnlyList<InspectionNode>? displayedFields;
    private InspectionNode? revealedNode;
    public TextEditor Editor { get; }
    public ListBox Navigator { get; } = new();
    public TreeView Inspector { get; } = new();
    public Grid LowerPane { get; } = new();
    public NumericUpDown FieldNumber { get; } = new() { Minimum = 1, Maximum = int.MaxValue, Value = 1, Width = 100, FormatString = "0" };
    public Button InspectFieldButton { get; } = new() { Content = "Inspect field" };

    public MessageInspectionView(MessageInspection inspection, TextEditor editor)
    {
        this.inspection = inspection;
        Editor = editor;
        editor.TextArea.TextView.LineTransformers.Add(new SelectionColorizer(inspection));
        RowDefinitions = new("65*,6,35*");
        RowDefinitions[0].MinHeight = 80;
        RowDefinitions[2].MinHeight = 150;
        LowerPane.ColumnDefinitions = new("25*,6,75*");
        LowerPane.ColumnDefinitions[0].MinWidth = 100;
        LowerPane.ColumnDefinitions[2].MinWidth = 180;
        SetRow(LowerPane, 2);
        Children.Add(editor);
        Children.Add(new GridSplitter { ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, [RowProperty] = 1 });
        Children.Add(LowerPane);

        Navigator.ItemsSource = inspection.Segments;
        Inspector.AutoScrollToSelectedItem = true;
        Navigator.ItemTemplate = new FuncDataTemplate(data => data is SegmentEntry,
            (data, _) => new TextBlock { Text = (data as SegmentEntry)?.Label ?? string.Empty });
        Inspector.ItemTemplate = new FuncTreeDataTemplate<InspectionNode>(
            (node, _) => CreateInspectorRow(node), node => node?.VisibleChildren ?? []);
        var navigatorPane = Pane("Segment Navigator", Navigator);
        var inspectorPane = new Grid { RowDefinitions = new("Auto,Auto,*,Auto,48"), Margin = new(8) };
        fieldStatus.FontWeight = FontWeight.SemiBold;
        inspectorPane.Children.Add(fieldStatus);
        var lookup = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new(0,4),
            Children = { new TextBlock { Text = "Field number", VerticalAlignment = VerticalAlignment.Center }, FieldNumber, InspectFieldButton } };
        SetRow(lookup, 1);
        SetRow(Inspector, 2);
        selectionTitle.IsHitTestVisible = false;
        selectionHeader.Child = selectionTitle;
        SetRow(selectionHeader, 3);
        SetRow(rawValue, 4);
        inspectorPane.Children.Add(lookup);
        inspectorPane.Children.Add(Inspector);
        inspectorPane.Children.Add(selectionHeader);
        inspectorPane.Children.Add(rawValue);
        SetColumn(inspectorPane, 2);
        LowerPane.Children.Add(navigatorPane);
        LowerPane.Children.Add(new GridSplitter { ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, [ColumnProperty] = 1 });
        LowerPane.Children.Add(inspectorPane);

        Navigator.SelectionChanged += (_, _) =>
        {
            if (!synchronizing && Navigator.SelectedItem is SegmentEntry segment) inspection.SelectSegment(segment);
        };
        Inspector.SelectionChanged += (_, _) =>
        {
            if (!synchronizing && Inspector.SelectedItem is InspectionNode node) inspection.SelectNode(node);
        };
        InspectFieldButton.Click += (_, _) =>
        {
            if (FieldNumber.Value is { } number && decimal.Truncate(number) == number) inspection.InspectField((int)number);
        };
        ApplySelection(false);
    }

    private static string Preview(string? raw) => raw is { Length: > 100 } ? raw[..100] + "… (select for full value)" : raw ?? string.Empty;

    private static Control CreateInspectorRow(InspectionNode? node)
    {
        if (node is null) return new TextBlock();
        var path = new TextBlock { Text = node.DisplayPath };
        // Keep row labels presentation-only so the normal header receives selection input.
        return new StackPanel { IsHitTestVisible = false, Orientation = Orientation.Horizontal, Spacing = 10, Children =
        {
            path, new TextBlock { Text = $"{node.PathInfo.StateLabel}   {Preview(node.Raw)}" }
        }};
    }

    private static Grid Pane(string title, Control content)
    {
        var pane = new Grid { RowDefinitions = new("Auto,*"), Margin = new(8) };
        pane.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold });
        SetRow(content, 1);
        pane.Children.Add(content);
        return pane;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (subscribed) return;
        subscribed = true;
        revealedNode = null;
        inspection.SelectionChanged += SelectionChanged;
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
        revealedNode = null;
        inspection.SelectDeepestOffset(Editor.Document.GetOffset(position.Value.Location));
    }

    private void ApplySelection(bool navigateRaw)
    {
        synchronizing = true;
        try
        {
            Navigator.SelectedItem = inspection.SelectedSegment;
            if (!ReferenceEquals(displayedFields, inspection.Fields))
            {
                displayedFields = inspection.Fields;
                Inspector.ItemsSource = displayedFields;
            }
            // A requested missing field has no tree row or raw coordinate; its state is shown below.
            var visibleNode = inspection.SelectedVisibleNode;
            Inspector.SelectedItem = visibleNode;
            InspectFieldButton.IsEnabled = inspection.SelectedSegment?.HasFieldPaths == true;
            fieldStatus.Text = inspection.SelectedSegment switch
            {
                null => "Field Inspector · no segments",
                { HasFieldPaths: false } => "Field Inspector · opaque segment; no parsed fields",
                { Segment.CanEdit: false } => "Field Inspector · provisional structure; see Diagnostics",
                _ => "Field Inspector · expand fields for structure"
            };
            var node = inspection.SelectedNode;
            selectionTitle.Text = node is null ? "Select a field to inspect its raw value." :
                $"{node.Path} · {node.PathInfo.StateLabel}" + (node.Range is { Length: 0 } ? " · zero-length range" : node.Range is null ? " · no raw range" : "");
            rawValue.Text = node?.Raw ?? string.Empty;
            if (subscribed && visibleNode is not null && !ReferenceEquals(revealedNode, visibleNode))
            {
                revealedNode = visibleNode;
                // Containers may not exist until the next layout. Restore just the selected
                // branch, not the entire hierarchy, when Dock reconstructs document content.
                Dispatcher.UIThread.Post(() =>
                {
                    if (subscribed && ReferenceEquals(inspection.SelectedVisibleNode, visibleNode)) RevealNode();
                }, DispatcherPriority.Loaded);
            }
            if (navigateRaw && inspection.SelectedRange is { } range)
            {
                Editor.Select(range.Offset, range.Length);
                var location = Editor.Document.GetLocation(range.Offset);
                Editor.ScrollTo(location.Line, location.Column);
            }
            else if (navigateRaw) Editor.Select(Editor.CaretOffset, 0);
            Editor.TextArea.TextView.Redraw();
        }
        finally { synchronizing = false; }
    }

    private void RevealNode()
    {
        var branch = inspection.SelectedVisibleBranch;
        ItemsControl parent = Inspector;
        foreach (var value in branch)
        {
            parent.UpdateLayout();
            if (parent.ContainerFromItem(value) is not TreeViewItem container) break;
            if (!ReferenceEquals(value, branch[^1])) container.IsExpanded = true;
            else container.BringIntoView();
            parent = container;
        }
    }
}

internal sealed class SelectionColorizer(MessageInspection inspection) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        if (inspection.SelectedRange is not { Length: > 0 } range) return;
        var start = Math.Max(line.Offset, range.Offset);
        var end = Math.Min(line.EndOffset, range.Offset + range.Length);
        // A persistent background remains visible when keyboard focus moves into the inspector.
        if (start < end) ChangeLinePart(start, end,
            element => element.TextRunProperties.SetBackgroundBrush(new SolidColorBrush(Color.Parse("#384A65"))));
    }
}
