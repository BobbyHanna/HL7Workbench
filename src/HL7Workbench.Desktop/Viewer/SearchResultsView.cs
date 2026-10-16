using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>A paged, virtualized dock tool. Row text excludes snippets and searched field values.</summary>
public sealed class SearchResultsView : Grid
{
    private readonly SearchViewModel model;
    private bool subscribed, refreshing;
    private int displayedPage = -1;
    private MessageSearchResults? displayedResults;
    public ComboBox Mode { get; } = new() { ItemsSource = new[] { "Text", "HL7 Field" }, Width = 110 };
    public TextBox Value { get; } = new() { PlaceholderText = "Literal search value", MinWidth = 170, MaxLength = MessageSearchQuery.MaximumQueryCharacters };
    public TextBox FieldPath { get; } = new() { PlaceholderText = "Field path, e.g. PID-3.1", Width = 170, MaxLength = 64 };
    public ComboBox Comparison { get; } = new() { ItemsSource = new[] { "Contains", "Equals" }, Width = 110 };
    public CheckBox CaseSensitive { get; } = new() { Content = "Case sensitive" };
    public Button Search { get; } = new() { Content = "Search" };
    public Button Cancel { get; } = new() { Content = "Cancel" };
    public Button Clear { get; } = new() { Content = "Clear" };
    public CheckBox Filter { get; } = new() { Content = "Filter Navigator" };
    public ListBox Results { get; } = new() { ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()) };
    public TextBlock Status { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public TextBlock Count { get; } = new();
    public Button Previous { get; } = new() { Content = "Previous page" };
    public Button Next { get; } = new() { Content = "Next page" };
    internal Task NavigationTask { get; private set; } = Task.CompletedTask;
    private readonly TextBlock page = new();
    private readonly StackPanel fieldControls = new() { Orientation = Orientation.Horizontal, Spacing = 6 };
    public SearchResultsView(SearchViewModel model)
    {
        this.model = model; Margin = new Thickness(8); RowDefinitions = new("Auto,Auto,Auto,*,Auto"); RowSpacing = 6;
        var controls = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in new Control[] { Mode, Value, CaseSensitive, Search, Cancel, Clear, Filter }) { control.Margin = new Thickness(0, 0, 6, 4); controls.Children.Add(control); }
        fieldControls.Children.Add(new TextBlock { Text = "Field path", VerticalAlignment = VerticalAlignment.Center }); fieldControls.Children.Add(FieldPath); fieldControls.Children.Add(Comparison);
        Children.Add(new StackPanel { Spacing = 4, Children = { controls, fieldControls } });
        SetRow(Status, 1); Children.Add(Status);
        var heading = new TextBlock { Text = "Message | Message type | Control ID | Match location | Matches", FontWeight = FontWeight.SemiBold };
        SetRow(heading, 2); Children.Add(heading);
        Results.ItemTemplate = new FuncDataTemplate<MessageSearchMatch>((row, _) =>
        {
            var grid = new Grid { ColumnDefinitions = new("65,100,140,*,80"), MinWidth = 530 };
            var values = row is null ? new string[5] : new[] { row.MessageNumber.ToString(), row.MessageType, row.ControlId, row.Location, row.MatchCount.ToString("N0") };
            for (var i = 0; i < values.Length; i++) { var text = new TextBlock { Text = values[i], TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4) }; SetColumn(text, i); grid.Children.Add(text); }
            return grid;
        });
        ScrollViewer.SetHorizontalScrollBarVisibility(Results, ScrollBarVisibility.Auto);
        SetRow(Results, 3); Children.Add(Results);
        var footer = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var control in new Control[] { Count, Previous, page, Next }) { control.Margin = new Thickness(0, 0, 8, 0); footer.Children.Add(control); }
        SetRow(footer, 4); Children.Add(footer);
        Search.Command = model.SearchCommand; Cancel.Command = model.CancelCommand; Clear.Command = model.ClearCommand;
        Mode.SelectedIndex = (int)model.Mode; Value.Text = model.Value; FieldPath.Text = model.FieldPath; Comparison.SelectedIndex = (int)model.Comparison; CaseSensitive.IsChecked = model.CaseSensitive;
        Mode.SelectionChanged += (_, _) => { model.Mode = (MessageSearchMode)Mode.SelectedIndex; Refresh(); };
        Comparison.SelectionChanged += (_, _) => model.Comparison = (MessageSearchComparison)Comparison.SelectedIndex;
        // TextChanged is deferred by the text box. Observe the property synchronously
        // so pressing Enter immediately after typing cannot search the prior value.
        Value.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && !refreshing) model.Value = Value.Text ?? ""; };
        FieldPath.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) model.FieldPath = FieldPath.Text ?? ""; };
        CaseSensitive.IsCheckedChanged += (_, _) => model.CaseSensitive = CaseSensitive.IsChecked == true;
        Filter.IsCheckedChanged += (_, _) => { if (!refreshing) model.Session.FilterNavigator = Filter.IsChecked == true; };
        Previous.Click += (_, _) => model.Session.SetPage(model.Session.PageIndex - 1);
        Next.Click += (_, _) => model.Session.SetPage(model.Session.PageIndex + 1);
        Results.DoubleTapped += async (_, _) => { if (Results.SelectedItem is MessageSearchMatch match) await (NavigationTask = model.Session.NavigateAsync(match)); };
        Results.KeyDown += async (_, e) => { if (e.Key == Key.Enter && Results.SelectedItem is MessageSearchMatch match) { e.Handled = true; await (NavigationTask = model.Session.NavigateAsync(match)); } };
        Value.KeyDown += (_, e) => { if (e.Key == Key.Enter && model.SearchCommand.CanExecute(null)) { e.Handled = true; model.SearchCommand.Execute(null); } };
        AttachedToVisualTree += (_, _) => { if (!subscribed) { model.PropertyChanged += Changed; model.FocusRequested += FocusSearch; subscribed = true; } Refresh(); };
        DetachedFromVisualTree += (_, _) => { model.PropertyChanged -= Changed; model.FocusRequested -= FocusSearch; subscribed = false; };
        Refresh();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void FocusSearch() => Dispatcher.UIThread.Post(() => { Value.Focus(); Value.SelectAll(); });
    private void Refresh()
    {
        refreshing = true;
        try
        {
            fieldControls.IsVisible = model.Mode == MessageSearchMode.Field;
            Filter.IsEnabled = model.Session.CanFilter; Filter.IsChecked = model.Session.FilterNavigator;
            if (Value.Text != model.Value) Value.Text = model.Value;
            Status.Text = model.Session.Status; Count.Text = $"{model.Session.ResultCount:N0} matching messages";
            if (!ReferenceEquals(displayedResults, model.Session.Results) || displayedPage != model.Session.PageIndex)
            {
                Results.ItemsSource = model.Session.Page; displayedResults = model.Session.Results; displayedPage = model.Session.PageIndex;
            }
            Previous.IsEnabled = model.Session.PageIndex > 0;
            Next.IsEnabled = model.Session.PageIndex + 1 < (model.Session.Results?.PageCount ?? 0);
            page.Text = $"Page {(model.Session.ResultCount == 0 ? 0 : model.Session.PageIndex + 1)} / {model.Session.Results?.PageCount ?? 0}";
        }
        finally { refreshing = false; }
    }
}
