using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

public enum InspectionPaneKind { Both, Navigator, Inspector }

/// <summary>Shared structural controls, usable inside a document or as independent dock tool content.</summary>
public sealed class InspectionPanes : Grid
{
    private readonly MessageInspection inspection;
    private readonly InspectionPaneKind kind;
    private readonly TextBlock selectionTitle = new() { TextWrapping = TextWrapping.Wrap };
    private readonly Border selectionHeader = new() { Background = Brushes.Transparent };
    private readonly TextBox rawValue = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap };
    private readonly TextBlock fieldStatus = new() { TextWrapping = TextWrapping.Wrap };
    private bool synchronizing;
    private bool subscribed;
    private IReadOnlyList<InspectionNode>? displayedFields;
    private InspectionNode? revealedNode;
    public ListBox Navigator { get; } = new();
    public TreeView Inspector { get; } = new();
    public long ValueOffset { get; private set; }
    public TextBlock ValueStatus { get; } = new();
    public WrapPanel ValueNavigation { get; } = new() { Orientation = Orientation.Horizontal, IsVisible = false };
    public NumericUpDown PreviewOffset { get; } = new() { Minimum = 0, Value = 0, Width = 150, FormatString = "0" };
    private readonly MessageDocument? document;
    private long previewRequest;
    private CancellationTokenSource? previewCancellation;
    public NumericUpDown FieldNumber { get; } = new() { Minimum = 1, Maximum = int.MaxValue, Value = 1, Width = 100, FormatString = "0" };
    public Button InspectFieldButton { get; } = new() { Content = "Inspect field" };

    public InspectionPanes(MessageInspection inspection, MessageDocument? document = null, InspectionPaneKind kind = InspectionPaneKind.Both)
    {
        this.inspection = inspection; this.document = document; this.kind = kind;
        ValueOffset = inspection.PreviewOffset;
        ColumnDefinitions = new("25*,6,75*"); ColumnDefinitions[0].MinWidth = 100; ColumnDefinitions[2].MinWidth = 180;
        Navigator.ItemsSource = inspection.Segments;
        Inspector.AutoScrollToSelectedItem = true;
        Navigator.ItemTemplate = new FuncDataTemplate(data => data is SegmentEntry,
            (data, _) => new TextBlock { Text = (data as SegmentEntry)?.Label ?? string.Empty });
        Inspector.ItemTemplate = new FuncTreeDataTemplate<InspectionNode>(
            (node, _) => CreateInspectorRow(node), node => node?.VisibleChildren ?? []);
        var navigatorPane = Pane("Segment Navigator", Navigator);
        var inspectorPane = new Grid { RowDefinitions = new("Auto,Auto,*,Auto,Auto,80"), Margin = new(8) };
        fieldStatus.FontWeight = FontWeight.SemiBold;
        inspectorPane.Children.Add(fieldStatus);
        var lookup = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Margin = new(0,4),
            Children = { new TextBlock { Text = "Field number", VerticalAlignment = VerticalAlignment.Center }, FieldNumber, InspectFieldButton } };
        ToolTip.SetTip(FieldNumber, "Field number within the selected segment");
        ToolTip.SetTip(InspectFieldButton, "Inspect the numbered field in the selected segment");
        SetRow(lookup, 1);
        SetRow(Inspector, 2);
        selectionTitle.IsHitTestVisible = false;
        selectionHeader.Child = selectionTitle;
        SetRow(selectionHeader, 3);
        SetRow(rawValue, 5);
        inspectorPane.Children.Add(lookup);
        inspectorPane.Children.Add(Inspector);
        inspectorPane.Children.Add(selectionHeader);
        var valueControls = ValueNavigation;
        valueControls.Children.Add(new TextBlock { Text = "Selected value chunks", VerticalAlignment = VerticalAlignment.Center });
        valueControls.Children.Add(ValueStatus);
        if (document?.Source is not null)
        {
            void ValueButton(string label, string tip, Func<long> offset)
            {
                var button = new Button { Content = label, Margin = new(2) };
                ToolTip.SetTip(button, tip);
                button.Click += async (_, _) => await LoadPreviewAsync(offset());
                valueControls.Children.Add(button);
            }
            ValueButton("First", "First chunk of selected field value", () => 0);
            ValueButton("Previous", "Previous chunk of selected field value", () =>
            {
                var value = Math.Max(0, ValueOffset - 4096);
                var start = inspection.SelectedNode?.Range64?.Offset ?? 0;
                return document.Source!.Align(start + value) - start;
            });
            ValueButton("Next", "Next chunk of selected field value", () => Math.Min(inspection.SelectedNode?.Range64?.Length ?? 0, ValueOffset + rawValue.Text?.Length ?? 0));
            ValueButton("Last", "Last chunk of selected field value", () =>
            {
                var range = inspection.SelectedNode?.Range64 ?? new HL7Workbench.Core.CharacterRange(0, 0);
                var value = Math.Max(0, range.Length - 4096);
                return document.Source!.Align(range.Offset + value) == range.Offset + value ? value : value + 1;
            });
            valueControls.Children.Add(PreviewOffset);
            ToolTip.SetTip(PreviewOffset, "UTF-16 offset within selected value");
            ValueButton("Go", "Jump to offset within selected value", () => PreviewOffset.Value is { } value && decimal.Truncate(value) == value ? checked((long)value) : -1);
        }
        SetRow(valueControls, 4);
        inspectorPane.Children.Add(valueControls);
        inspectorPane.Children.Add(rawValue);
        SetColumn(inspectorPane, 2);
        Children.Add(navigatorPane);
        Children.Add(new GridSplitter { ResizeDirection = GridResizeDirection.Columns, ResizeBehavior = GridResizeBehavior.PreviousAndNext,
            HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, [ColumnProperty] = 1 });
        Children.Add(inspectorPane);

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
        if (kind != InspectionPaneKind.Both)
        {
            Children.Clear(); ColumnDefinitions = new("*");
            var pane = kind == InspectionPaneKind.Navigator ? navigatorPane : inspectorPane;
            SetColumn(pane, 0); Children.Add(pane);
        }
    }
    private static Control CreateInspectorRow(InspectionNode? node)
    {
        if (node is null) return new TextBlock();
        var path = new TextBlock { Text = node.DisplayPath };
        // Keep row labels presentation-only so the normal header receives selection input.
        return new StackPanel { IsHitTestVisible = false, Orientation = Orientation.Horizontal, Spacing = 10, Children =
        {
            path, new TextBlock { Text = $"{node.PathInfo.StateLabel}   {node.Preview + (node.Range64?.Length > 100 ? "…" : "")}" }
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
    { base.OnAttachedToVisualTree(e); subscribed = true; revealedNode = null; inspection.SelectionChanged += SelectionChanged; ApplySelection(false); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        subscribed = false; previewRequest++; previewCancellation?.Cancel(); previewCancellation?.Dispose(); previewCancellation = null;
        inspection.SelectionChanged -= SelectionChanged; base.OnDetachedFromVisualTree(e);
    }
    private void SelectionChanged(object? sender, InspectionSelectionChangedEventArgs e)
    { if (e.NavigateRaw) revealedNode = null; ApplySelection(e.NavigateRaw); }
    private void ApplySelection(bool navigateRaw)
    {
        synchronizing = true;
        try
        {
            Navigator.SelectedItem = inspection.SelectedSegment;
            if (kind == InspectionPaneKind.Navigator) return;
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
                null => inspection.Segments.Count == 0 ? "Field Inspector · no segments" : "Field Inspector · select a segment",
                { HasFieldPaths: false } => "Field Inspector · opaque segment; no parsed fields",
                { CanEdit: false } => "Field Inspector · provisional structure; see Activity Log",
                _ => "Field Inspector · expand fields for structure"
            };
            var node = inspection.SelectedNode;
            ValueNavigation.IsVisible = document?.Source is not null && node?.Range64 is { Length: > 4096 };
            selectionTitle.Text = node is null ? "Select a field to inspect its raw value." :
                $"{node.Path} · {node.PathInfo.StateLabel}" + (node.Range64 is { Length: 0 } ? " · zero-length range" : node.Range64 is null ? " · no raw range" : "");
            if (kind != InspectionPaneKind.Navigator && document?.Source is not null)
            {
                rawValue.Text = node?.Preview ?? string.Empty;
                if (subscribed) _ = LoadPreviewAsync(inspection.PreviewOffset);
            }
            else rawValue.Text = node?.Raw ?? string.Empty;
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
        }
        finally { synchronizing = false; }
    }
    public async Task LoadPreviewAsync(long offset)
    {
        var node = inspection.SelectedNode;
        if (node is null || document?.Source is null) { ValueStatus.Text = ""; return; }
        if (node.Range64 is not { } range || offset < 0 || offset > range.Length) { ValueStatus.Text = "Offset outside value."; return; }
        var version = ++previewRequest;
        previewCancellation?.Cancel(); previewCancellation?.Dispose(); previewCancellation = new();
        try
        {
            var text = await document.ReadAsync(_ => inspection.ReadPreview(node, offset), previewCancellation.Token);
            if (version != previewRequest || !subscribed || !ReferenceEquals(node, inspection.SelectedNode) || text is null) return;
            ValueOffset = offset; PreviewOffset.Maximum = range.Length; PreviewOffset.Value = offset;
            inspection.PreviewOffset = offset;
            rawValue.Text = text;
            ValueStatus.Text = $"Value {offset}–{offset + text.Length} / {range.Length} UTF-16; {node.Structure?.ByteRange?.Length} bytes";
        }
        catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or System.IO.IOException)
        { if (subscribed) { ValueStatus.Text = "Value read unavailable."; if (error is System.IO.IOException) document?.RecordActivity(ActivityKind.SourceReadFailed); } }
        catch (ArgumentOutOfRangeException) { if (subscribed) ValueStatus.Text = "Offset falls inside a UTF-16 surrogate pair."; }
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
