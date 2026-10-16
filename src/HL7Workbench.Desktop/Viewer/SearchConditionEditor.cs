using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>One bounded visual condition. Controls are retained while typing so edits do not lose focus.</summary>
public sealed class SearchConditionEditor : WrapPanel
{
    private readonly SearchConditionViewModel condition;
    private bool refreshing;
    private SearchTarget? displayedTarget;
    private readonly TextBlock ordinal = new() { VerticalAlignment = VerticalAlignment.Center };
    public ComboBox Target { get; } = new() { Width = 145, ItemsSource = new[] { "Message Text", "HL7 Field", "HL7 Segment" } };
    public TextBox Path { get; } = new() { Width = 150, MaxLength = 64, PlaceholderText = "PID-3.1 / PID" };
    public ComboBox Operator { get; } = new() { Width = 160 };
    public TextBox Value { get; } = new() { MinWidth = 170, MaxLength = MessageSearchQuery.MaximumQueryCharacters, PlaceholderText = "Literal value" };
    public CheckBox CaseSensitive { get; } = new() { Content = "Case sensitive" };
    public Button Remove { get; } = new() { Content = "Remove" };
    public TextBlock Validation { get; } = new() { MaxWidth = 320, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    public SearchConditionEditor(SearchConditionViewModel condition, Action remove, Action? execute = null)
    {
        this.condition = condition; DataContext = condition; Orientation = Orientation.Horizontal;
        Validation.Bind(TextBlock.ForegroundProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(WorkbenchThemeResources.Validation));
        foreach (var control in new Control[] { ordinal, Target, Path, Operator, Value, CaseSensitive, Remove, Validation })
        { control.Margin = new Thickness(0, 0, 6, 5); Children.Add(control); }
        Target.SelectionChanged += (_, _) => { if (!refreshing && Target.SelectedIndex >= 0) condition.Target = (SearchTarget)Target.SelectedIndex; };
        Operator.SelectionChanged += (_, _) => { if (!refreshing && Operator.SelectedIndex >= 0) condition.Operator = SearchCondition.OperatorsFor(condition.Target)[Operator.SelectedIndex]; };
        Path.PropertyChanged += (_, e) => { if (!refreshing && e.Property == TextBox.TextProperty) condition.Path = Path.Text ?? ""; };
        Value.PropertyChanged += (_, e) => { if (!refreshing && e.Property == TextBox.TextProperty) condition.Value = Value.Text ?? ""; };
        CaseSensitive.IsCheckedChanged += (_, _) => { if (!refreshing) condition.CaseSensitive = CaseSensitive.IsChecked == true; };
        Remove.Click += (_, _) => remove();
        Value.KeyDown += (_, e) => { if (e.Key == Key.Enter && execute is not null) { e.Handled = true; execute(); } };
        AttachedToVisualTree += (_, _) => { condition.PropertyChanged += Changed; Refresh(); };
        DetachedFromVisualTree += (_, _) => condition.PropertyChanged -= Changed;
        Refresh();
    }
    public void SetOrdinal(int number)
    {
        ordinal.Text = number + ".";
        foreach (var (control, label) in new (Control, string)[] { (Target, "target"), (Path, "path"), (Operator, "operator"), (Value, "value"), (CaseSensitive, "case sensitive"), (Remove, "remove") })
        { AutomationProperties.SetName(control, $"Condition {number} {label}"); ToolTip.SetTip(control, $"Condition {number} {label}"); }
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    // A newly added row may not be attached until the next layout pass. Its
    // operator-specific controls must still reflect immediate builder edits.
    internal void Synchronize() => Refresh();
    private void Refresh()
    {
        refreshing = true;
        try
        {
            Target.SelectedIndex = (int)condition.Target;
            var operators = SearchCondition.OperatorsFor(condition.Target);
            if (displayedTarget != condition.Target) { Operator.ItemsSource = operators.Select(Label).ToArray(); displayedTarget = condition.Target; }
            Operator.SelectedIndex = operators.ToList().IndexOf(condition.Operator);
            Path.IsVisible = condition.Target != SearchTarget.MessageText; Path.Text = condition.Path;
            Path.PlaceholderText = condition.Target == SearchTarget.Segment ? "Segment, e.g. PV1" : "Field, e.g. PV1-1 / PID-3.1";
            Validation.Text = condition.Snapshot.ValidationError(); Validation.IsVisible = Validation.Text is not null;
            Value.IsVisible = CaseSensitive.IsVisible = condition.Snapshot.NeedsValue; Value.Text = condition.Value;
            CaseSensitive.IsChecked = condition.CaseSensitive;
        }
        finally { refreshing = false; }
    }
    private static string Label(SearchOperator operation) => operation switch
    {
        SearchOperator.DoesNotContain => "Does Not Contain", SearchOperator.DoesNotEqual => "Does Not Equal",
        SearchOperator.DoesNotExist => "Does Not Exist", _ => operation.ToString()
    };
}
