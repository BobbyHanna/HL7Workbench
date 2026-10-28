using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>DIP conventions for individual tool content, independent of window size or display pixels.</summary>
public static class ToolLayoutMetrics
{
    public const double Gap = 6;
    public const double SectionGap = 10;
    public const double FullWidth = 600;
    public const double CompactWidth = 400;
    public const double NarrowWidth = 250;
    public const double EditorHeight = 150;
    public const double ListHeight = 120;
    public const double InspectorHeight = 100;
    public const double BrowserHeight = ListHeight + 150;
    public const double ControlHeight = 28;
    public const double MaximumPinnedHeight = 160;
}

/// <summary>Native wrapping, stable logical/tab order and real controls: no command cloning, reparenting, hidden actions or horizontal toolbar clipping.</summary>
public sealed class AdaptiveActionBar : WrapPanel
{
    private readonly Dictionary<Control, (double Minimum, double Maximum)> sizing = [];
    private static readonly FuncDataTemplate<string> WrappedContent = new((text, _) => new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap });
    public AdaptiveActionBar() { Orientation = Orientation.Horizontal; }
    public AdaptiveActionBar(params Control[] controls) : this() { foreach (var control in controls) Children.Add(control); }
    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (var stale in sizing.Keys.Where(c => !Children.Contains(c)).ToArray()) sizing.Remove(stale);
        foreach (var control in Children)
        {
            if (!sizing.TryGetValue(control, out var original))
            {
                sizing.Add(control, original = (control.MinWidth, control.MaxWidth));
                control.Margin = new(control.Margin.Left, control.Margin.Top, control.Margin.Right + ToolLayoutMetrics.Gap, control.Margin.Bottom + ToolLayoutMetrics.Gap);
                if (control is ContentControl { Content: string } content) content.ContentTemplate = WrappedContent;
                if (control is TextBlock text) text.TextWrapping = TextWrapping.Wrap;
            }
            if (double.IsFinite(availableSize.Width))
            {
                var width = Math.Max(0, availableSize.Width - control.Margin.Left - control.Margin.Right);
                control.SetCurrentValue(MinWidthProperty, Math.Min(original.Minimum, width));
                control.SetCurrentValue(MaxWidthProperty, Math.Min(original.Maximum, width));
            }
            else { control.SetCurrentValue(MinWidthProperty, original.Minimum); control.SetCurrentValue(MaxWidthProperty, original.Maximum); }
        }
        return base.MeasureOverride(availableSize);
    }
}

/// <summary>A native label/input pair. Wide forms align labels beside inputs; compact forms stack them without replacing controls.</summary>
public sealed class ResponsiveFormField : Grid
{
    private bool? wide;
    public TextBlock Label { get; }
    public Control Input { get; }
    public ResponsiveFormField(string label, Control input)
    {
        Input = input; Label = new() { Text = label, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
        RowSpacing = ToolLayoutMetrics.Gap; ColumnSpacing = ToolLayoutMetrics.Gap; Margin = new(0, 0, 0, ToolLayoutMetrics.Gap);
        input.Width = double.NaN; input.MinWidth = 0; input.HorizontalAlignment = HorizontalAlignment.Stretch;
        if (string.IsNullOrEmpty(AutomationProperties.GetName(input))) AutomationProperties.SetName(input, label);
        AutomationProperties.SetLabeledBy(input, Label);
        Children.Add(Label); Children.Add(input);
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        var next = availableSize.Width >= ToolLayoutMetrics.FullWidth;
        if (wide != next)
        {
            wide = next;
            ColumnDefinitions = new(next ? "180,*" : "*"); RowDefinitions = new(next ? "Auto" : "Auto,Auto");
            SetColumn(Input, next ? 1 : 0); SetRow(Input, next ? 0 : 1);
        }
        return base.MeasureOverride(availableSize);
    }
}

/// <summary>One outer vertical overflow region around a finite-measured Grid. Inner editors/lists keep native, virtualized viewports.
/// Auto sections may grow; star-row minimums provide usable dense content before the whole tool needs scrolling.</summary>
public sealed class ResponsiveToolSurface : Decorator
{
    private readonly FiniteContent host = new();
    public ScrollViewer Scroll { get; }
    public Grid Content { get; }
    public ScrollViewer? PrimaryScroll { get; private set; }
    private ResponsiveToolSurface(ToolContentGrid content)
    {
        Content = content; host.Child = content;
        Scroll = new() { Content = host, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Focusable = true };
        AutomationProperties.SetName(Scroll, "Tool content scrolling"); Child = Scroll; ClipToBounds = true;
    }
    protected override Size MeasureOverride(Size availableSize)
    {
        // An ordinary ScrollViewer supplies infinite height, which turns star rows into
        // Auto and defeats virtualized lists. Supply the actual tool viewport instead.
        host.SetHeightBudget(double.IsFinite(availableSize.Height) ? Math.Max(0, availableSize.Height) : ToolLayoutMetrics.ListHeight);
        return base.MeasureOverride(availableSize);
    }
    private sealed class FiniteContent : Decorator
    {
        private double heightBudget;
        internal void SetHeightBudget(double value)
        {
            if (heightBudget == value) return;
            heightBudget = value; InvalidateMeasure();
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            if (Child is not Grid grid) return default;
            grid.Measure(new(availableSize.Width, heightBudget));
            // Keep native Grid sizing, but expose its unclipped measure result to
            // the outer scroller. Editors and virtualized lists never see infinity.
            var extent = Math.Max(heightBudget, grid.DesiredSize.Height);
            // Re-measure at that finite extent so nested star rows also receive
            // their expanded allocation, rather than arranging cached short rows.
            grid.Measure(new(availableSize.Width, extent));
            return new(grid.DesiredSize.Width, Math.Max(extent, grid.DesiredSize.Height));
        }
    }
    /// <summary>Composes an existing tool once, preserving controls, row definitions, splitters and event ownership.</summary>
    public static ResponsiveToolSurface Attach(Grid tool, params (int Row, double MinimumHeight)[] denseRows)
        => Attach(tool, null, denseRows);
    /// <summary>Keeps a small essential action bar above the scrolling body. A direct existing child can be promoted without cloning its controls.</summary>
    public static ResponsiveToolSurface Attach(Grid tool, Control? primaryActions, params (int Row, double MinimumHeight)[] denseRows)
    {
        var content = new ToolContentGrid { RowDefinitions = tool.RowDefinitions, ColumnDefinitions = tool.ColumnDefinitions,
            RowSpacing = tool.RowSpacing, ColumnSpacing = tool.ColumnSpacing };
        foreach (var (row, minimum) in denseRows) content.RowDefinitions[row].MinHeight = Math.Max(content.RowDefinitions[row].MinHeight, minimum);
        var children = tool.Children.ToArray(); tool.Children.Clear(); foreach (var child in children.Where(c => c != primaryActions)) content.Children.Add(child);
        tool.RowDefinitions = new(primaryActions is null ? "*" : "Auto,*"); tool.ColumnDefinitions = new("*"); tool.RowSpacing = tool.ColumnSpacing = 0;
        tool.MinWidth = tool.MinHeight = 0; tool.ClipToBounds = true;
        var surface = new ResponsiveToolSurface(content);
        if (primaryActions is not null)
        {
            Grid.SetRow(primaryActions, 0); Grid.SetColumn(primaryActions, 0);
            var primary = new ScrollViewer { Content = primaryActions, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = ToolLayoutMetrics.MaximumPinnedHeight };
            // Only an exceptionally small host needs a second scroll region. It prevents
            // wrapped essential actions from consuming the entire tool height.
            tool.SizeChanged += (_, _) => primary.MaxHeight = Math.Max(ToolLayoutMetrics.ControlHeight,
                Math.Min(ToolLayoutMetrics.MaximumPinnedHeight, tool.Bounds.Height / 2));
            surface.PrimaryScroll = primary; tool.Children.Add(primary); Grid.SetRow(surface, 1);
        }
        tool.Children.Add(surface); return surface;
    }
}

/// <summary>Native Grid sizing with a visible vertical overflow extent. Use for nested
/// Auto/star compositions inside a responsive surface; dense children still measure finitely.</summary>
public sealed class ToolContentGrid : Grid
{
    private double contentHeight;
    protected override Size MeasureOverride(Size availableSize)
    {
        var desired = base.MeasureOverride(availableSize); contentHeight = desired.Height; return desired;
    }
    protected override Size MeasureCore(Size availableSize)
    {
        var desired = base.MeasureCore(availableSize);
        // Default Layoutable measurement clips DesiredSize to the viewport even
        // when Grid's Auto rows plus star minimums exceed it. Preserve that native
        // extent so scrolling can reach the overflow, including newly expanded rows.
        var height = Math.Clamp(double.IsNaN(Height) ? contentHeight : Height, MinHeight, MaxHeight);
        return new(desired.Width, Math.Max(desired.Height, height + Margin.Top + Margin.Bottom));
    }
}
