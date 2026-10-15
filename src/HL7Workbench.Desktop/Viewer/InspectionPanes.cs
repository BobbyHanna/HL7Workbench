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
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

public enum InspectionPaneKind { Both, Navigator, Inspector }

/// <summary>Shared structural controls, usable inside a document or as independent dock tool content.</summary>
public sealed class InspectionPanes : Grid
{
    private MessageInspection inspection;
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
    public MessageInspection Inspection => inspection;
    public TextBlock SnapshotStatus { get; } = new() { Height = 38, Margin = new(8,2), TextWrapping = TextWrapping.Wrap, FontSize = 11 };
    public bool HasCurrentInspection => inspection.IsCurrent && (document is null ||
        document.InspectionState == DocumentInspectionState.Current && ReferenceEquals(document.ActiveInspection, inspection));
    private long bindingVersion;

    public InspectionPanes(MessageInspection inspection, MessageDocument? document = null, InspectionPaneKind kind = InspectionPaneKind.Both)
    {
        this.inspection = inspection; this.document = document; this.kind = kind;
        ValueOffset = inspection.PreviewOffset;
        ColumnDefinitions = new("25*,6,75*"); ColumnDefinitions[0].MinWidth = 100; ColumnDefinitions[2].MinWidth = 180;
        Navigator.ItemsSource = inspection.Segments;
        // RevealNode handles structural navigation explicitly. Framework auto-scroll
        // would also run when new parse nodes are rebound and shift the user's viewport.
        Inspector.AutoScrollToSelectedItem = false;
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
                button.Click += async (_, _) => { if (HasCurrentInspection) await LoadPreviewAsync(offset()); };
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

        // Disabled controls inherit theme-wide dimming. Keep the snapshot's visual
        // state intact and stop stale structural input before selection/expansion.
        GuardStructuralInput(Navigator);
        GuardStructuralInput(Inspector);
        Navigator.SelectionChanged += (_, _) =>
        {
            if (synchronizing) return;
            if (!HasCurrentInspection) { RestoreSnapshotSelection(); return; }
            if (Navigator.SelectedItem is SegmentEntry segment) this.inspection.SelectSegment(segment);
        };
        Inspector.SelectionChanged += (_, _) =>
        {
            if (synchronizing) return;
            if (!HasCurrentInspection) { RestoreSnapshotSelection(); return; }
            if (Inspector.SelectedItem is InspectionNode node) this.inspection.SelectNode(node);
        };
        InspectFieldButton.Click += (_, _) =>
        {
            if (HasCurrentInspection && FieldNumber.Value is { } number && decimal.Truncate(number) == number) this.inspection.InspectField((int)number);
        };
        ApplySelection(false);
        if (kind != InspectionPaneKind.Both)
        {
            Children.Clear(); ColumnDefinitions = new("*");
            var pane = kind == InspectionPaneKind.Navigator ? navigatorPane : inspectorPane;
            SetColumn(pane, 0); Children.Add(pane);
        }
        RowDefinitions = new("Auto,*");
        foreach (var child in Children) SetRow(child, 1);
        SetColumnSpan(SnapshotStatus, ColumnDefinitions.Count); Children.Add(SnapshotStatus);
        UpdateSnapshotState();
    }

    private void GuardStructuralInput(Control control)
    {
        control.AddHandler(PointerPressedEvent, (_, e) =>
        {
            // Leave scrollbars and wheel input available while previous rows are shown.
            if (!HasCurrentInspection && e.Source is Visual source &&
                source.GetSelfAndVisualAncestors().TakeWhile(v => !ReferenceEquals(v, control))
                    .Any(v => v is ListBoxItem or TreeViewItem)) e.Handled = true;
        }, RoutingStrategies.Tunnel);
        control.AddHandler(DoubleTappedEvent, (_, e) => { if (!HasCurrentInspection) e.Handled = true; }, RoutingStrategies.Tunnel);
        control.AddHandler(KeyDownEvent, (_, e) =>
        {
            if (!HasCurrentInspection && e.Key is Key.Up or Key.Down or Key.Left or Key.Right or
                Key.Home or Key.End or Key.PageUp or Key.PageDown or Key.Space or Key.Enter or
                Key.Add or Key.Subtract or Key.Multiply) e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }

    private void RestoreSnapshotSelection()
    {
        // Also guard programmatic/accessibility selection changes; do not rebind,
        // reveal or read source ranges while the displayed coordinates are stale.
        synchronizing = true;
        try
        {
            Navigator.SelectedItem = inspection.SelectedSegment;
            Inspector.SelectedItem = inspection.SelectedVisibleNode;
        }
        finally { synchronizing = false; }
    }

    /// <summary>Rebinds projections without replacing controls, splitters or scroll hosts.</summary>
    public void UpdateInspection(MessageInspection next)
    {
        if (!ReferenceEquals(next, inspection))
        {
            var version = ++bindingVersion;
            var expanded = Inspector.GetVisualDescendants().OfType<TreeViewItem>()
                .Where(t => t.IsExpanded && t.DataContext is InspectionNode).Select(t => ((InspectionNode)t.DataContext!).Path).ToHashSet();
            var navigatorScroll = Navigator.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            var inspectorScroll = Inspector.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
            var navOffset = navigatorScroll?.Offset; var fieldOffset = inspectorScroll?.Offset;
            inspection.SelectionChanged -= SelectionChanged;
            previewRequest++; previewCancellation?.Cancel();
            inspection = next; if (subscribed) inspection.SelectionChanged += SelectionChanged;
            synchronizing = true;
            try { Navigator.ItemsSource = inspection.Segments; }
            finally { synchronizing = false; }
            // Rebinding after typing must not scroll the inspector to a newly allocated
            // node. Explicit structural navigation still reveals its selected branch.
            revealedNode = inspection.SelectedVisibleNode;
            ApplySelection(false);
            if (subscribed) Dispatcher.UIThread.Post(() =>
            {
                if (!subscribed || version != bindingVersion) return;
                for (var depth = 0; depth < 4 && expanded.Count > 0; depth++)
                {
                    Inspector.UpdateLayout();
                    foreach (var item in Inspector.GetVisualDescendants().OfType<TreeViewItem>().ToArray())
                        if (item.DataContext is InspectionNode node && expanded.Remove(node.Path)) item.IsExpanded = true;
                }
                if (navOffset is { } n && navigatorScroll is not null) navigatorScroll.Offset = n;
                if (fieldOffset is { } f && inspectorScroll is not null) inspectorScroll.Offset = f;
            }, DispatcherPriority.Loaded);
        }
        UpdateSnapshotState();
    }
    private void UpdateSnapshotState()
    {
        var state = document?.InspectionState ?? DocumentInspectionState.Current;
        SnapshotStatus.Text = state switch
        {
            DocumentInspectionState.Pending => "Updating inspection · previous parse shown; structural actions paused.",
            DocumentInspectionState.Stale => "Previous parse shown · correct the draft to refresh; structural actions paused.",
            DocumentInspectionState.Empty => "Working message is empty.",
            DocumentInspectionState.Unavailable => "Inspection unavailable for this message.",
            _ => "Inspection of current text"
        };
        InspectFieldButton.IsEnabled = state != DocumentInspectionState.Empty && inspection.SelectedSegment?.HasFieldPaths == true;
        if (state == DocumentInspectionState.Empty)
        {
            synchronizing = true;
            try
            {
                Navigator.ItemsSource = Array.Empty<SegmentEntry>(); Inspector.ItemsSource = Array.Empty<InspectionNode>();
                displayedFields = null; rawValue.Text = ""; selectionTitle.Text = "No field values in empty working text.";
                fieldStatus.Text = "Field Inspector · empty message";
            }
            finally { synchronizing = false; }
        }
        else if (!ReferenceEquals(Navigator.ItemsSource, inspection.Segments))
        {
            synchronizing = true;
            try { Navigator.ItemsSource = inspection.Segments; }
            finally { synchronizing = false; }
            ApplySelection(false);
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
    { base.OnAttachedToVisualTree(e); subscribed = true; revealedNode = null; inspection.SelectionChanged += SelectionChanged; ApplySelection(false); UpdateSnapshotState(); }
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
                    if (subscribed && HasCurrentInspection && ReferenceEquals(inspection.SelectedVisibleNode, visibleNode)) RevealNode();
                }, DispatcherPriority.Loaded);
            }
        }
        finally { synchronizing = false; }
    }
    public async Task LoadPreviewAsync(long offset)
    {
        if (!HasCurrentInspection) return;
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
