using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
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
    public ComboBox BuilderMode { get; } = new() { ItemsSource = new[] { "Simple", "Advanced" }, Width = 120 };
    public SearchGroupEditor RootEditor { get; private set; } = null!;
    public ComboBox Combination => RootEditor.Combination;
    public Button AddCondition => RootEditor.AddCondition;
    public StackPanel ConditionRows => RootEditor.Rows;
    public Expander LibraryPanel { get; } = new() { Header = "Search Library", HorizontalAlignment = HorizontalAlignment.Stretch };
    public ComboBox SavedSearches { get; } = new() { Width = 210, PlaceholderText = "Saved search", ItemTemplate = new FuncDataTemplate<SavedSearchItem>((item, _) => new TextBlock { Text = item?.Name, TextTrimming = TextTrimming.CharacterEllipsis }) };
    public TextBox SearchName { get; } = new() { Width = 180, MaxLength = 120, PlaceholderText = "Search name" };
    public Button SaveSearch { get; } = new() { Content = "Save Search" };
    public Button LoadSearch { get; } = new() { Content = "Load Search" };
    public Button UpdateSearch { get; } = new() { Content = "Update" };
    public Button RenameSearch { get; } = new() { Content = "Rename" };
    public Button DeleteSearch { get; } = new() { Content = "Delete" };
    public TextBlock LibraryStatus { get; } = new() { TextWrapping = TextWrapping.Wrap };
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
    private readonly WrapPanel fieldControls = new() { Orientation = Orientation.Horizontal };
    private readonly WrapPanel simpleControls = new() { Orientation = Orientation.Horizontal };
    private readonly StackPanel advancedControls = new() { Spacing = 4 };
    public ScrollViewer QueryEditor { get; } = new() { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
    public Grid ResultRegion { get; } = new() { RowDefinitions = new("Auto,Auto,*,Auto"), RowSpacing = 4 };
    public GridSplitter Splitter { get; } = new() { ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext,
        HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch, Focusable = true, KeyboardIncrement = 10 };
    private SearchBuilderMode? displayedBuilderMode;
    public SearchResultsView(SearchViewModel model)
    {
        this.model = model; Margin = new Thickness(8); RowDefinitions = new("Auto,3*,6,2*"); RowSpacing = 4;
        var controls = new AdaptiveActionBar();
        foreach (var control in new Control[] { BuilderMode, Search, Cancel, Clear, Filter }) { control.Margin = new Thickness(0, 0, 6, 4); controls.Children.Add(control); }
        foreach (var control in new Control[] { Mode, Value, CaseSensitive }) { control.Margin = new Thickness(0, 0, 6, 4); simpleControls.Children.Add(control); }
        fieldControls.Children.Add(new TextBlock { Text = "Field path", VerticalAlignment = VerticalAlignment.Center }); fieldControls.Children.Add(FieldPath); fieldControls.Children.Add(Comparison);
        RootEditor = new(model, model.RootGroup); advancedControls.Children.Add(RootEditor);
        var libraryControls = new AdaptiveActionBar();
        foreach (var control in new Control[] { SavedSearches, LoadSearch, SearchName, SaveSearch, UpdateSearch, RenameSearch, DeleteSearch })
        { control.Margin = new Thickness(0, 0, 6, 4); libraryControls.Children.Add(control); }
        LibraryPanel.Content = new StackPanel { Spacing = 4, Children =
        {
            new TextBlock { Text = SearchViewModel.StorageDisclosure, TextWrapping = TextWrapping.Wrap },
            libraryControls, LibraryStatus
        } };
        Children.Add(controls);
        QueryEditor.Content = new StackPanel { Spacing = 4, Children = { simpleControls, fieldControls, advancedControls, LibraryPanel } };
        SetRow(QueryEditor, 1); Children.Add(QueryEditor); SetRow(Splitter, 2); Children.Add(Splitter); SetRow(ResultRegion, 3); Children.Add(ResultRegion);
        Splitter.Classes.Add("workbench-splitter"); Results.Classes.Add("workbench-surface");
        AutomationProperties.SetName(Splitter, "Resize query builder and results"); ToolTip.SetTip(Splitter, "Drag to resize query builder/results; use arrow keys when focused");
        Splitter.AddHandler(Thumb.DragCompletedEvent, (_, _) => SaveSplit(), Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        Splitter.AddHandler(KeyUpEvent, (_, e) => { if (e.Key is Key.Up or Key.Down or Key.Left or Key.Right) SaveSplit(); }, Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        SizeChanged += (_, _) => AdaptToHeight();
        foreach (var (control, label) in new (Control, string)[] { (BuilderMode, "Search builder mode"), (Combination, "Match ALL or ANY conditions"), (AddCondition, "Add Condition"),
            (Mode, "Simple search target"), (Value, "Search value"), (FieldPath, "Field path"), (Comparison, "Comparison"), (CaseSensitive, "Case sensitive"),
            (SavedSearches, "Saved search"), (SearchName, "Search name"), (SaveSearch, "Save Search"), (LoadSearch, "Load Search"), (UpdateSearch, "Update saved search"), (RenameSearch, "Rename saved search"), (DeleteSearch, "Delete saved search") })
        { AutomationProperties.SetName(control, label); ToolTip.SetTip(control, label); }
        ToolTip.SetTip(Clear, "Clear results and restore navigation; retain the current query and saved definitions");
        ToolTip.SetTip(UpdateSearch, "Explicitly replace the loaded local definition with the current query");
        ToolTip.SetTip(RenameSearch, "Rename the loaded definition using Search name");
        ResultRegion.Children.Add(Status);
        var heading = new TextBlock { Text = "Message | Message type | Control ID | Location / conditions | Count", FontWeight = FontWeight.SemiBold };
        SetRow(heading, 1); ResultRegion.Children.Add(heading);
        Results.ItemTemplate = new FuncDataTemplate<MessageSearchMatch>((row, _) =>
        {
            var grid = new Grid { ColumnDefinitions = new("65,100,140,*,80"), MinWidth = 530 };
            var values = row is null ? new string[5] : new[] { row.MessageNumber.ToString(), row.MessageType, row.ControlId, row.Location, row.MatchCount.ToString("N0") };
            for (var i = 0; i < values.Length; i++)
            {
                var text = new TextBlock { Text = values[i], TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4) };
                Control cell = i == 3 && row?.ConditionSummary.Length > 0 ? new StackPanel { Children = { text, new TextBlock { Text = row.ConditionSummary, FontSize = 11, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(4, 0) } } } : text;
                SetColumn(cell, i); grid.Children.Add(cell);
            }
            return grid;
        });
        ScrollViewer.SetHorizontalScrollBarVisibility(Results, ScrollBarVisibility.Auto);
        SetRow(Results, 2); ResultRegion.Children.Add(Results);
        var footer = new AdaptiveActionBar();
        foreach (var control in new Control[] { Count, Previous, page, Next }) { control.Margin = new Thickness(0, 0, 8, 0); footer.Children.Add(control); }
        SetRow(footer, 3); ResultRegion.Children.Add(footer);
        Search.Command = model.SearchCommand; Cancel.Command = model.CancelCommand; Clear.Command = model.ClearCommand;
        SaveSearch.Command = model.SaveSearchCommand; LoadSearch.Command = model.LoadSearchCommand; UpdateSearch.Command = model.UpdateSearchCommand;
        RenameSearch.Command = model.RenameSearchCommand; DeleteSearch.Command = model.DeleteSearchCommand;
        Mode.SelectedIndex = (int)model.Mode; Value.Text = model.Value; FieldPath.Text = model.FieldPath; Comparison.SelectedIndex = (int)model.Comparison; CaseSensitive.IsChecked = model.CaseSensitive;
        BuilderMode.SelectionChanged += (_, _) => { if (!refreshing && BuilderMode.SelectedIndex >= 0) model.BuilderMode = (SearchBuilderMode)BuilderMode.SelectedIndex; };
        Mode.SelectionChanged += (_, _) => { if (!refreshing && Mode.SelectedIndex >= 0) model.Mode = (MessageSearchMode)Mode.SelectedIndex; };
        Comparison.SelectionChanged += (_, _) => { if (!refreshing && Comparison.SelectedIndex >= 0) model.Comparison = (MessageSearchComparison)Comparison.SelectedIndex; };
        // TextChanged is deferred by the text box. Observe the property synchronously
        // so pressing Enter immediately after typing cannot search the prior value.
        Value.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty && !refreshing) model.Value = Value.Text ?? ""; };
        FieldPath.PropertyChanged += (_, e) => { if (!refreshing && e.Property == TextBox.TextProperty) model.FieldPath = FieldPath.Text ?? ""; };
        CaseSensitive.IsCheckedChanged += (_, _) => { if (!refreshing) model.CaseSensitive = CaseSensitive.IsChecked == true; };
        SearchName.PropertyChanged += (_, e) => { if (!refreshing && e.Property == TextBox.TextProperty) model.SearchName = SearchName.Text ?? ""; };
        SavedSearches.SelectionChanged += (_, _) => { if (!refreshing) model.SelectedSearchId = (SavedSearches.SelectedItem as SavedSearchItem)?.Id; };
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
    private void SaveSplit()
    {
        if (model.BuilderMode != SearchBuilderMode.Advanced) return;
        UpdateLayout(); var total = RowDefinitions[1].ActualHeight + RowDefinitions[3].ActualHeight;
        if (total > 0) model.SaveBuilderFraction(Math.Clamp(RowDefinitions[1].ActualHeight / total, 0.1, 0.9));
    }
    private void AdaptToHeight()
    {
        var available = Math.Max(0, Bounds.Height - RowDefinitions[0].ActualHeight - 18);
        var advanced = model.BuilderMode == SearchBuilderMode.Advanced;
        // Scale minimums down in short panes; never force the outer Dock to grow.
        RowDefinitions[1].MinHeight = advanced ? Math.Min(48, available * .12) : 0;
        RowDefinitions[3].MinHeight = Math.Min(64, available * .15);
        QueryEditor.MaxHeight = advanced ? double.PositiveInfinity : Math.Min(180, available * .4);
    }
    private void ApplyModeLayout()
    {
        if (displayedBuilderMode == model.BuilderMode) return;
        displayedBuilderMode = model.BuilderMode;
        var advanced = model.BuilderMode == SearchBuilderMode.Advanced;
        RowDefinitions[1].Height = advanced ? new GridLength(model.BuilderFraction, GridUnitType.Star) : GridLength.Auto;
        RowDefinitions[2].Height = new GridLength(advanced ? 6 : 0);
        RowDefinitions[3].Height = new GridLength(advanced ? 1 - model.BuilderFraction : 1, GridUnitType.Star);
        Splitter.IsVisible = advanced; AdaptToHeight();
    }
    private void FocusSearch() => Dispatcher.UIThread.Post(() =>
    {
        if (model.BuilderMode == SearchBuilderMode.Simple) { Value.Focus(); Value.SelectAll(); return; }
        // Reveal the first leaf through collapsed groups before focusing it.
        void Expand(SearchGroupEditor editor) { editor.Expansion.IsExpanded = true; foreach (var child in editor.Rows.Children.OfType<SearchGroupEditor>()) Expand(child); }
        Expand(RootEditor);
        var row = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(RootEditor).OfType<SearchConditionEditor>().FirstOrDefault();
        if (row is null) { AddCondition.Focus(); return; }
        var input = row.Value.IsVisible ? row.Value : row.Path; input.Focus(); input.SelectAll();
    });
    private void Refresh()
    {
        refreshing = true;
        try
        {
            BuilderMode.SelectedIndex = (int)model.BuilderMode;
            ApplyModeLayout();
            if (!ReferenceEquals(RootEditor.DataContext, model.RootGroup)) { advancedControls.Children.Clear(); RootEditor = new(model, model.RootGroup); advancedControls.Children.Add(RootEditor); }
            RootEditor.Synchronize();
            simpleControls.IsVisible = model.BuilderMode == SearchBuilderMode.Simple;
            advancedControls.IsVisible = !simpleControls.IsVisible;
            fieldControls.IsVisible = simpleControls.IsVisible && model.Mode == MessageSearchMode.Field;
            Mode.SelectedIndex = (int)model.Mode; Comparison.SelectedIndex = (int)model.Comparison; FieldPath.Text = model.FieldPath; CaseSensitive.IsChecked = model.CaseSensitive;
            LibraryPanel.IsVisible = model.HasLibrary; LibraryStatus.Text = model.NameValidation ?? model.LibraryStatus;
            if (!ReferenceEquals(SavedSearches.ItemsSource, model.SavedSearches)) SavedSearches.ItemsSource = model.SavedSearches;
            SavedSearches.SelectedItem = model.SavedSearches.FirstOrDefault(i => i.Id == model.SelectedSearchId); SearchName.Text = model.SearchName;
            SavedSearches.IsEnabled = SearchName.IsEnabled = !model.IsLibraryBusy;
            Filter.IsEnabled = model.Session.CanFilter; Filter.IsChecked = model.Session.FilterNavigator;
            if (Value.Text != model.Value) Value.Text = model.Value;
            Status.Text = model.ValidationError ?? model.Session.Status; Count.Text = $"{model.Session.ResultCount:N0} matching messages";
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
