using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Virtualized, paged table over authoritative disk-backed results; only presentation cells reach this view.</summary>
public sealed class FieldExtractionView : Grid
{
    private readonly FieldExtractionViewModel model;
    private bool refreshing, subscribed;
    private ExtractionColumnDraft[] shownColumns = [];
    private string[] headers = [];
    private IReadOnlyList<ExtractionPreviewRow>? shownPage;
    private readonly StackPanel columnRows = new() { Spacing = 3 };
    private readonly Grid header = new();
    public ComboBox Source { get; } = new() { Width = 220 };
    public Button UseActive { get; } = new() { Content = "Use active document" };
    public ComboBox Scope { get; } = new() { ItemsSource = new[] { "Current Message", "Whole Document" }, Width = 160 };
    public Button Run { get; } = new() { Content = "Extract" };
    public Button Cancel { get; } = new() { Content = "Cancel" };
    public ComboBox Library { get; } = new() { Width = 190 };
    public Button Load { get; } = new() { Content = "Load" };
    public TextBox ConfigurationName { get; } = new() { Width = 190, MaxLength = 120, PlaceholderText = "Configuration name" };
    public Button New { get; } = new() { Content = "New" };
    public Button Duplicate { get; } = new() { Content = "Duplicate" };
    public Button Rename { get; } = new() { Content = "Rename" };
    public Button Save { get; } = new() { Content = "Save" };
    public Button Delete { get; } = new() { Content = "Delete…" };
    public Expander ColumnsPanel { get; } = new() { Header = "Columns and matching policy", HorizontalAlignment = HorizontalAlignment.Stretch };
    public ComboBox Mode { get; } = new() { ItemsSource = new[] { "First Match", "Expand Matches (ordinal alignment)" }, Width = 255 };
    public CheckBox Decode { get; } = new() { Content = "Decode HL7 escapes" };
    public Button AddColumn { get; } = new() { Content = "Add column" };
    public Expander FiltersPanel { get; } = new() { Header = "Filter and sort results", HorizontalAlignment = HorizontalAlignment.Stretch };
    public ComboBox FilterColumn { get; } = new() { Width = 175 };
    public ComboBox FilterOperator { get; } = new() { ItemsSource = Enum.GetValues<ExtractionFilterOperator>(), Width = 180 };
    public TextBox FilterLiteral { get; } = new() { Width = 200, MaxLength = 1024, PlaceholderText = "Session-only comparison value" };
    public ComboBox SortColumn { get; } = new() { Width = 175 };
    public CheckBox Descending { get; } = new() { Content = "Descending" };
    public CheckBox Numeric { get; } = new() { Content = "Numeric sort" };
    public Button Apply { get; } = new() { Content = "Apply filter/sort" };
    public ListBox Results { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    public Button Previous { get; } = new() { Content = "Previous page" };
    public Button Next { get; } = new() { Content = "Next page" };
    public Button Navigate { get; } = new() { Content = "Go to source" };
    public Button Export { get; } = new() { Content = "Export selected rows to CSV…" };
    public TextBlock Status { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public TextBlock SourceStatus { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public TextBlock PageStatus { get; } = new() { VerticalAlignment = VerticalAlignment.Center };
    public TextBlock Summary { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public Expander SummaryPanel { get; } = new() { Header = "Extraction Summary", HorizontalAlignment = HorizontalAlignment.Stretch };
    public Task ActionTask { get; private set; } = Task.CompletedTask;
    public Task NavigationTask { get; private set; } = Task.CompletedTask;
    public ResponsiveToolSurface LayoutSurface { get; }

    public FieldExtractionView(FieldExtractionViewModel model)
    {
        this.model = model; Margin = new(8); MinWidth = 280; MinHeight = 180; RowDefinitions = new("Auto,Auto,Auto,Auto,Auto,*,Auto,Auto"); RowSpacing = 4;
        Source.ItemsSource = model.Documents;
        Source.ItemTemplate = new FuncDataTemplate<MessageDocument>((doc, _) =>
        {
            var label = new TextBlock { DataContext = doc, TextTrimming = TextTrimming.CharacterEllipsis };
            label.Bind(TextBlock.TextProperty, new Binding(nameof(MessageDocument.DisplayTitle)) { StringFormat = "Document " + doc?.ActivityNumber + " · {0}" }); return label;
        });
        AddRow(0, Toolbar(Source, UseActive, Scope, Run, Cancel));
        Library.ItemTemplate = new FuncDataTemplate<ExtractionLibraryItem>((item, _) => new TextBlock { Text = item is null ? "" : (item.BuiltIn ? "Built-in · " : "My Extractions · ") + item.Name });
        AddRow(1, Toolbar(Library, Load, ConfigurationName, New, Duplicate, Rename, Save, Delete));
        ColumnsPanel.Content = new ScrollViewer { MaxHeight = 240, Content = new StackPanel { Spacing = 4, Children =
        {
            new TextBlock { Text = "Duplicate the built-in to edit. Omitted indexes expand; explicit [1] stays fixed. Expand aligns nth matches and broadcasts scalar columns; it does not join clinical groups.", TextWrapping = TextWrapping.Wrap },
            Strip(Mode, Decode, AddColumn), columnRows
        } } };
        AddRow(2, ColumnsPanel);
        FiltersPanel.Content = new StackPanel { Spacing = 4, Children =
        {
            Strip(new TextBlock { Text = "Filter column", VerticalAlignment = VerticalAlignment.Center }, FilterColumn, FilterOperator, FilterLiteral),
            Strip(new TextBlock { Text = "Sort column", VerticalAlignment = VerticalAlignment.Center }, SortColumn, Descending, Numeric, Apply),
            new TextBlock { Text = "Literal values are session-only and never saved in configurations. Filters/sorts apply to all stored rows; header buttons also sort.", TextWrapping = TextWrapping.Wrap }
        } };
        AddRow(3, FiltersPanel);
        AddRow(4, new StackPanel { Spacing = 2, Children = { SourceStatus, Status } });
        Results.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
        Results.ItemTemplate = new FuncDataTemplate<ExtractionPreviewRow>((row, _) => Row(row));
        var table = new Grid { RowDefinitions = new("Auto,*") }; table.Children.Add(header); SetRow(Results, 1); table.Children.Add(Results);
        AddRow(5, new ScrollViewer { Content = table, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        AddRow(6, Toolbar(Previous, PageStatus, Next, Navigate, Export));
        SummaryPanel.Content = new ScrollViewer { MaxHeight = 180, Content = Summary }; AddRow(7, SummaryPanel);
        AutomationProperties.SetName(Source, "Extraction source document"); AutomationProperties.SetName(Results, "Extracted field results");
        AutomationProperties.SetName(FilterLiteral, "Extraction filter comparison value"); AutomationProperties.SetName(ConfigurationName, "Extraction configuration name");
        FilterOperator.ItemTemplate = new FuncDataTemplate<ExtractionFilterOperator>((op, _) => new TextBlock { Text = op switch
        {
            ExtractionFilterOperator.None => "No filter", ExtractionFilterOperator.Contains => "Contains (ignore case)",
            ExtractionFilterOperator.Equals => "Exact match (ignore case)", ExtractionFilterOperator.Missing => "Is missing",
            ExtractionFilterOperator.Empty => "Is empty", ExtractionFilterOperator.ExplicitNull => "Is HL7 null",
            ExtractionFilterOperator.Unavailable => "Diagnosed/unavailable", ExtractionFilterOperator.NumericGreaterThan => "Number >",
            ExtractionFilterOperator.NumericLessThan => "Number <", _ => "Number ="
        } });
        ToolTip.SetTip(Export, "Export every selected filtered/sorted row, including all pages, with explicit state/status/path companion columns.");
        ToolTip.SetTip(Decode, "Decode HL7 escape sequences through Core. Unsupported escapes are retained with a diagnostic status; binary attachments are never decoded.");
        UseActive.Click += (_, _) => model.Session.UseActiveDocument();
        Run.Click += (_, _) => ActionTask = model.RunAsync(); Cancel.Click += (_, _) => model.Session.Cancel();
        New.Click += (_, _) => ActionTask = model.NewAsync(); Duplicate.Click += (_, _) => ActionTask = model.DuplicateAsync();
        Save.Click += (_, _) => ActionTask = model.SaveAsync(); Delete.Click += (_, _) => ActionTask = model.DeleteAsync();
        Rename.Click += (_, _) => { if (model.Draft.CanEdit) { ConfigurationName.Focus(); ConfigurationName.SelectAll(); } };
        Load.Click += (_, _) => { if (Library.SelectedItem is ExtractionLibraryItem entry) ActionTask = model.OpenAsync(entry.Id); };
        AddColumn.Click += (_, _) => model.Draft.AddColumn();
        Source.SelectionChanged += (_, _) => { if (!refreshing) model.Session.SetSource(Source.SelectedItem as MessageDocument); };
        Scope.SelectionChanged += (_, _) => { if (!refreshing && Scope.SelectedIndex >= 0) model.Scope = (ExtractionScope)Scope.SelectedIndex; };
        Mode.SelectionChanged += (_, _) => { if (!refreshing && Mode.SelectedIndex >= 0) model.Draft.Mode = (ExtractionMode)Mode.SelectedIndex; };
        Decode.IsCheckedChanged += (_, _) => { if (!refreshing) model.Draft.DecodeEscapes = Decode.IsChecked == true; };
        ConfigurationName.PropertyChanged += (_, e) => { if (!refreshing && e.Property == TextBox.TextProperty) model.Draft.Name = ConfigurationName.Text ?? ""; };
        FilterLiteral.PropertyChanged += (_, e) => { if (!refreshing && e.Property == TextBox.TextProperty) model.FilterLiteral = FilterLiteral.Text ?? ""; };
        FilterColumn.SelectionChanged += (_, _) => { if (!refreshing && FilterColumn.SelectedIndex >= 0) model.FilterColumn = FilterColumn.SelectedIndex; };
        FilterOperator.SelectionChanged += (_, _) => { if (!refreshing && FilterOperator.SelectedItem is ExtractionFilterOperator op) model.FilterOperator = op; };
        SortColumn.SelectionChanged += (_, _) => { if (!refreshing) model.SortColumn = SortColumn.SelectedIndex - 1; };
        Descending.IsCheckedChanged += (_, _) => { if (!refreshing) model.Descending = Descending.IsChecked == true; };
        Numeric.IsCheckedChanged += (_, _) => { if (!refreshing) model.NumericSort = Numeric.IsChecked == true; };
        Apply.Click += (_, _) => ActionTask = model.ApplyViewAsync();
        Export.Click += (_, _) => ActionTask = model.ExportAsync();
        Previous.Click += (_, _) => ActionTask = model.Session.SetPageAsync(model.Session.PageIndex - 1);
        Next.Click += (_, _) => ActionTask = model.Session.SetPageAsync(model.Session.PageIndex + 1);
        Navigate.Click += (_, _) => NavigateSelected(); Results.DoubleTapped += (_, _) => NavigateSelected();
        Results.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; NavigateSelected(); } };
        FilterLiteral.KeyDown += (_, e) => { if (e.Key == Key.Enter) { e.Handled = true; ActionTask = model.ApplyViewAsync(); } };
        AttachedToVisualTree += (_, _) => { if (!subscribed) { model.PropertyChanged += Changed; model.Shown += FocusSource; subscribed = true; } Refresh(); };
        DetachedFromVisualTree += (_, _) => { model.PropertyChanged -= Changed; model.Shown -= FocusSource; subscribed = false; };
        LayoutSurface = ResponsiveToolSurface.Attach(this, (5, ToolLayoutMetrics.ListHeight + 40));
        Refresh();
    }
    private void AddRow(int row, Control control) { SetRow(control, row); Children.Add(control); }
    private static AdaptiveActionBar Toolbar(params Control[] controls) => new(controls);
    private static WrapPanel Strip(params Control[] controls)
    {
        return new AdaptiveActionBar(controls);
    }
    private void NavigateSelected() { if (Results.SelectedItem is ExtractionPreviewRow row) NavigationTask = model.Session.NavigateAsync(row); }
    private void FocusSource() => Source.Focus();
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private ColumnDefinitions TableColumns() => new("85,115,60" + string.Concat(headers.Select(_ => ",200")));
    private Grid TableGrid() => new() { ColumnDefinitions = TableColumns() };
    private Control Row(ExtractionPreviewRow? row)
    {
        if (row is null) return new TextBlock();
        var grid = TableGrid();
        void Cell(int index, Control content) { SetColumn(content, index); content.Margin = new(4, 2); grid.Children.Add(content); }
        Cell(0, new TextBlock { Text = row.CapturedPosition.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        Cell(1, new TextBlock { Text = row.MessageKey.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        Cell(2, new TextBlock { Text = row.Expansion.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        for (var i = 0; i < row.Values.Count; i++)
        {
            var value = row.Values[i];
            var display = value.Text.Replace("\r", "␍", StringComparison.Ordinal).Replace("\n", "␊", StringComparison.Ordinal).Replace("\t", "⇥", StringComparison.Ordinal);
            if (value.Abbreviated) display += "… [preview]";
            if (display.Length == 0) display = "[" + (value.Status == "Complete" ? value.State : value.Status) + "]";
            var cell = new StackPanel { Children =
            {
                new TextBlock { Text = display, TextTrimming = TextTrimming.CharacterEllipsis },
                new TextBlock { Text = value.Path + " · " + value.State + (value.Status == "Complete" ? "" : " · " + value.Status), [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.10"), TextTrimming = TextTrimming.CharacterEllipsis }
            } };
            ToolTip.SetTip(cell, value.Path + " · " + value.State + " · " + value.Status + (value.Abbreviated ? ". Display is abbreviated; CSV uses the complete stored value." : ""));
            Cell(i + 3, cell);
        }
        return grid;
    }
    private void RefreshColumns()
    {
        if (shownColumns.SequenceEqual(model.Draft.Columns)) { foreach (var child in columnRows.Children) child.IsEnabled = model.Draft.CanEdit && model.CanManage; return; }
        shownColumns = model.Draft.Columns.ToArray(); columnRows.Children.Clear();
        for (var i = 0; i < shownColumns.Length; i++)
        {
            var index = i; var column = shownColumns[i];
            var label = new TextBox { Width = 185, MaxLength = 120, PlaceholderText = "Column label", DataContext = column };
            var path = new TextBox { Width = 190, MaxLength = 64, PlaceholderText = "PID[2]-3[1].4.1", DataContext = column };
            label.Bind(TextBox.TextProperty, new Binding(nameof(ExtractionColumnDraft.Label)) { Mode = BindingMode.TwoWay });
            path.Bind(TextBox.TextProperty, new Binding(nameof(ExtractionColumnDraft.Path)) { Mode = BindingMode.TwoWay });
            AutomationProperties.SetName(path, "Extraction path " + (i + 1)); AutomationProperties.SetName(label, "Extraction label " + (i + 1));
            var up = new Button { Content = "↑" }; var down = new Button { Content = "↓" }; var remove = new Button { Content = "Remove" };
            up.Click += (_, _) => model.Draft.MoveColumn(index, -1); down.Click += (_, _) => model.Draft.MoveColumn(index, 1); remove.Click += (_, _) => model.Draft.RemoveColumn(index);
            var controls = Strip(label, path, up, down, remove); controls.IsEnabled = model.Draft.CanEdit && model.CanManage; columnRows.Children.Add(controls);
        }
    }
    private void Refresh()
    {
        refreshing = true;
        try
        {
            var session = model.Session; var draft = model.Draft;
            Source.SelectedItem = session.Source; Scope.SelectedIndex = (int)model.Scope;
            Source.IsEnabled = UseActive.IsEnabled = Scope.IsEnabled = model.CanManage;
            Library.ItemsSource = draft.Items; Library.SelectedItem = draft.Items.FirstOrDefault(i => i.Id == draft.Id);
            ConfigurationName.Text = draft.Name; ConfigurationName.IsReadOnly = !draft.CanEdit || !model.CanManage;
            Load.IsEnabled = New.IsEnabled = Duplicate.IsEnabled = model.CanManage; Library.IsEnabled = model.CanManage;
            Rename.IsEnabled = Save.IsEnabled = draft.CanEdit && model.CanManage; Delete.IsEnabled = draft.CanDelete && model.CanManage;
            Mode.SelectedIndex = (int)draft.Mode; Decode.IsChecked = draft.DecodeEscapes;
            Mode.IsEnabled = Decode.IsEnabled = AddColumn.IsEnabled = draft.CanEdit && model.CanManage; RefreshColumns();
            Run.IsEnabled = model.CanManage && session.Source is { MessageCount: > 0, IsLoading: false }; Cancel.IsEnabled = session.IsBusy;
            header.IsEnabled = model.CanManage && session.Results is not null;
            var labels = (session.Results?.Configuration.Columns.Select(c => c.Label) ?? draft.Columns.Select(c => c.Label)).ToArray();
            if (!headers.SequenceEqual(labels))
            {
                headers = labels; header.Children.Clear(); header.ColumnDefinitions = TableColumns();
                var titles = new[] { "Message", "Identity", "Match" }.Concat(headers).ToArray();
                for (var i = 0; i < titles.Length; i++)
                {
                    var index = i - 3; var button = new Button { Content = titles[i], HorizontalAlignment = HorizontalAlignment.Stretch, FontWeight = FontWeight.SemiBold };
                    button.Click += (_, _) => { if (index >= 0 && session.Results is not null) { model.Descending = model.SortColumn == index && !model.Descending; model.SortColumn = index; ActionTask = model.ApplyViewAsync(); } };
                    SetColumn(button, i); header.Children.Add(button);
                }
                FilterColumn.ItemsSource = headers; SortColumn.ItemsSource = new[] { "Source order" }.Concat(headers).ToArray();
            }
            FilterColumn.SelectedIndex = Math.Clamp(model.FilterColumn, 0, headers.Length - 1); FilterOperator.SelectedItem = model.FilterOperator;
            SortColumn.SelectedIndex = Math.Clamp(model.SortColumn + 1, 0, headers.Length); Descending.IsChecked = model.Descending; Numeric.IsChecked = model.NumericSort;
            FilterLiteral.Text = model.FilterLiteral;
            FiltersPanel.IsEnabled = model.CanManage && session.Results is not null;
            if (!ReferenceEquals(shownPage, session.Page)) { shownPage = session.Page; Results.ItemsSource = shownPage; }
            Previous.IsEnabled = model.CanManage && session.PageIndex > 0; Next.IsEnabled = model.CanManage && session.View is { } v && session.PageIndex + 1 < v.PageCount;
            Navigate.IsEnabled = model.CanManage && session.Results?.SourceIsOpen == true; Export.IsEnabled = model.CanManage && session.View is not null;
            PageStatus.Text = session.View is { } view ? $"Page {session.PageIndex + 1}/{view.PageCount} · {view.Count:N0} selected rows" : "No results";
            SourceStatus.Text = session.Results?.SourceDescription ?? "No completed extraction. Choose a source document and run.";
            Status.Text = (draft.IsDirty ? "* Unsaved configuration · " : "") + (model.Error.Length > 0 ? model.Error : session.Status) + " " + draft.Status;
            ToolTip.SetTip(SourceStatus, SourceStatus.Text); ToolTip.SetTip(Status, Status.Text);
            var summary = session.Results?.Summary;
            Summary.Text = summary is null ? "No completed summary. " + session.LastOperation :
                $"Examined {summary.ExaminedMessages:N0}/{summary.TotalMessages:N0} messages; {summary.MessagesWithMatches:N0} with selected positions; {summary.Rows:N0} rows produced. " +
                $"Unreadable messages: {summary.UnreadableMessages:N0}; expansion-limited messages: {summary.ExpansionLimitedMessages:N0}. " +
                (summary.Truncated ? "TRUNCATED: " + summary.Limit + ". " : "Complete within configured limits. ") +
                $"Last operation: {session.LastOperation}" + (session.LastOperationCanceled ? " canceled; prior completed results retained." : ".") + "\n" + session.Results!.SourceDescription + "\n" +
                string.Join("\n", summary.Columns.Select(c => $"{c.Label}: missing {c.Missing:N0}; empty {c.Empty:N0}; null {c.ExplicitNull:N0}; diagnosed/unavailable {c.Unavailable:N0}; distinct " + (c.DistinctLowerBound ? ">" : "") + c.DistinctValues.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }
        finally { refreshing = false; }
    }
}
