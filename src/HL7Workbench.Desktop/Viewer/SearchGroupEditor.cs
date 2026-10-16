using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Retained recursive group controls; collapsing does not edit the Boolean expression.</summary>
public sealed class SearchGroupEditor : Border
{
    private readonly SearchViewModel model;
    private readonly SearchGroupViewModel group;
    private readonly int depth;
    private bool refreshing;
    public Expander Expansion { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    public ComboBox Combination { get; } = new() { ItemsSource = new[] { "Match ALL (AND)", "Match ANY (OR)" }, Width = 190 };
    public Button AddCondition { get; } = new() { Content = "Add Condition" };
    public Button AddGroup { get; } = new() { Content = "Add Group" };
    public Button RemoveGroup { get; } = new() { Content = "Remove Group" };
    public StackPanel Rows { get; } = new() { Spacing = 6 };
    public TextBlock Validation { get; } = new() { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    public SearchGroupEditor(SearchViewModel model, SearchGroupViewModel group, int depth = 1)
    {
        this.model = model; this.group = group; this.depth = depth; DataContext = group;
        BorderThickness = new Thickness(1); Padding = new Thickness(6); Margin = new Thickness(depth == 1 ? 0 : 12, 2, 0, 2);
        this.Bind(BorderBrushProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(WorkbenchThemeResources.Border));
        this.Bind(BackgroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(depth == 1 ? WorkbenchThemeResources.Surface : WorkbenchThemeResources.Group));
        Validation.Bind(TextBlock.ForegroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(WorkbenchThemeResources.Validation));
        var header = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in new Control[] { new TextBlock { Text = depth == 1 ? "Root group" : $"Group · level {depth}", VerticalAlignment = VerticalAlignment.Center }, Combination, AddCondition, AddGroup, RemoveGroup })
        { control.Margin = new Thickness(0, 0, 6, 4); header.Children.Add(control); }
        RemoveGroup.IsVisible = depth > 1;
        Expansion.Header = header; Expansion.Content = new StackPanel { Spacing = 4, Children = { Rows, Validation } }; Child = Expansion;
        Expansion.IsExpanded = group.IsExpanded;
        Expansion.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty) group.IsExpanded = Expansion.IsExpanded; };
        Combination.SelectionChanged += (_, _) => { if (!refreshing && Combination.SelectedIndex >= 0) group.Combination = (Workspace.SearchCombination)Combination.SelectedIndex; };
        if (depth == 1) AddCondition.Command = model.AddConditionCommand;
        else AddCondition.Click += (_, _) => model.AddCondition(group);
        AddGroup.Click += (_, _) => model.AddGroup(group); RemoveGroup.Click += (_, _) => model.RemoveGroup(group);
        foreach (var (control, name) in new (Control, string)[] { (Combination, "Match ALL or ANY"), (AddCondition, "Add Condition"), (AddGroup, "Add Group"), (RemoveGroup, "Remove Group") })
        { AutomationProperties.SetName(control, $"Group level {depth}: {name}"); ToolTip.SetTip(control, name); }
        Synchronize();
    }
    internal void Synchronize()
    {
        refreshing = true;
        try
        {
            Combination.SelectedIndex = (int)group.Combination; AddCondition.IsEnabled = model.CanAddCondition; AddGroup.IsEnabled = model.CanAddGroup(group);
            foreach (var stale in Rows.Children.Where(c => !group.Children.Any(item => ReferenceEquals(item, c.DataContext))).ToArray()) Rows.Children.Remove(stale);
            foreach (var item in group.Children)
                if (!Rows.Children.Any(c => ReferenceEquals(c.DataContext, item)))
                    Rows.Children.Add(item is SearchConditionViewModel leaf ? new SearchConditionEditor(leaf, () => model.RemoveCondition(leaf), () => { if (model.SearchCommand.CanExecute(null)) model.SearchCommand.Execute(null); }) : new SearchGroupEditor(model, (SearchGroupViewModel)item, depth + 1));
            foreach (var row in Rows.Children)
                if (row is SearchConditionEditor condition) { condition.SetOrdinal(model.Conditions.IndexOf((SearchConditionViewModel)condition.DataContext!) + 1); condition.Synchronize(); }
                else ((SearchGroupEditor)row).Synchronize();
            Validation.Text = group.Children.Count == 0 ? "This group is empty. Add a condition or group." : ""; Validation.IsVisible = group.Children.Count == 0;
        }
        finally { refreshing = false; }
    }
}
