using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using AvaloniaEdit;
using AvaloniaEdit.Highlighting;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Querying;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>One dockable editor and paged results surface. Dock may replace this view without replacing its model.</summary>
public sealed class SqlQueryView : Grid
{
    private readonly SqlQueryViewModel model;
    private bool synchronizing, subscribed;
    private IReadOnlyList<SqlPreviewRow>? renderedPage;
    private SqlQueryResults? renderedResult;
    private readonly Grid headers = new();
    private readonly Dictionary<(int Row, int Column), Border> realized = [];
    private double[] widths = [];
    private int displayedSort = -2;
    private bool displayedDescending;
    private Guid? displayedSavedId;
    public TextEditor Editor { get; } = new() { ShowLineNumbers = true, FontFamily = new("Consolas, monospace"), [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.14") };
    public Button Run { get; } = new() { Content = "Run · Ctrl+Enter" };
    public Button Cancel { get; } = new() { Content = "Cancel" };
    public ComboBox Scope { get; } = new() { ItemsSource = new[] { "Current document", "Selected open documents" }, Width = 205 };
    public ItemsControl ScopeChoices { get; } = new();
    public Expander DocumentsPanel { get; } = new() { Header = "Choose open documents", IsVisible = false };
    public TextBlock Target { get; } = new() { VerticalAlignment = VerticalAlignment.Center };
    public ComboBox Library { get; } = new() { Width = 185, PlaceholderText = "Saved SQL queries" };
    public Button Open { get; } = new() { Content = "Open" };
    public TextBox QueryName { get; } = new() { Width = 180, MaxLength = 120, PlaceholderText = "Query name" };
    public Button New { get; } = new() { Content = "New" };
    public Button Save { get; } = new() { Content = "Save query" };
    public Button Rename { get; } = new() { Content = "Rename saved" };
    public Button Delete { get; } = new() { Content = "Delete…" };
    public ComboBox History { get; } = new() { Width = 250, PlaceholderText = "Session history" };
    public Button OpenHistory { get; } = new() { Content = "Open history" };
    public TextBlock Status { get; } = new() { TextWrapping = TextWrapping.Wrap, MaxHeight = 58 };
    public TextBlock ResultStatus { get; } = new() { TextWrapping = TextWrapping.Wrap, MaxHeight = 55 };
    public TextBlock LibraryStatus { get; } = new() { TextWrapping = TextWrapping.Wrap, [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.11"), MaxHeight = 46 };
    public TextBlock DraftStatus { get; } = new() { VerticalAlignment = VerticalAlignment.Center };
    public ListBox Results { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch, SelectionMode = SelectionMode.Multiple, Focusable = true };
    public Button Previous { get; } = new() { Content = "Previous" };
    public Button Next { get; } = new() { Content = "Next" };
    public TextBlock PageStatus { get; } = new() { VerticalAlignment = VerticalAlignment.Center };
    public Button Copy { get; } = new() { Content = "Copy selection" };
    public Button Inspect { get; } = new() { Content = "Inspect full value" };
    public Button Navigate { get; } = new() { Content = "Go to source" };
    public Button Export { get; } = new() { Content = "Export all results to CSV…" };
    public Button Extract { get; } = new() { Content = "Open Matching Messages in New Document" };
    public TextEditor FullValue { get; } = new() { IsReadOnly = true, ShowLineNumbers = false, FontFamily = new("Consolas, monospace"), [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.13"), Height = 135 };
    public TextBlock FullValueKind { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public Expander FullValuePanel { get; } = new() { Header = "Complete selected value", HorizontalAlignment = HorizontalAlignment.Stretch };
    public Task ActionTask { get; private set; } = Task.CompletedTask;
    public Task ClipboardTask { get; private set; } = Task.CompletedTask;
    public ResponsiveToolSurface LayoutSurface { get; }

    public SqlQueryView(SqlQueryViewModel model)
    {
        this.model = model; Margin = new(5);
        RowDefinitions = new("Auto,Auto,Auto,Auto,2*,3,3*,Auto,Auto,Auto"); RowSpacing = 3;
        Add(0, Toolbar(Library, Open, QueryName, New, Save, Rename, Delete, DraftStatus));
        Add(1, Toolbar(Scope, Target, History, OpenHistory));
        Add(2, DocumentsPanel); Add(3, new StackPanel { Spacing = 2, Children = { LibraryStatus, Status } }); Add(4, Editor);
        var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Rows, ResizeBehavior = GridResizeBehavior.PreviousAndNext, HorizontalAlignment = HorizontalAlignment.Stretch };
        splitter.Bind(GridSplitter.BackgroundProperty, new DynamicResourceExtension(WorkbenchThemeResources.Border));
        ToolTip.SetTip(splitter, "Drag to resize the SQL editor and results."); Add(5, splitter);
        var table = new Grid { RowDefinitions = new("Auto,*") }; table.Children.Add(headers); SetRow(Results, 1); table.Children.Add(Results);
        Add(6, new ScrollViewer { Content = table, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled });
        Add(7, new StackPanel { Spacing = 3, Children = { ResultStatus, Toolbar(Previous, PageStatus, Next, Copy, Inspect, Navigate), Toolbar(Export, Extract) } });
        FullValuePanel.Content = new StackPanel { Spacing = 3, Children = { FullValueKind, FullValue } }; Add(8, FullValuePanel);
        Add(9, new TextBlock { Text = "Click a cell; Shift-click selects a rectangle; Ctrl-click adds cells. Click # to select a row. Headers sort all rows. Double-click deliberately opens the source.", [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.11"), TextWrapping = TextWrapping.Wrap });
        // Reuse AvaloniaEdit's lexical highlighter; no Roslyn analysis or additional dependency.
        Hl7EditorTheme.Attach(Editor); Hl7EditorTheme.Attach(FullValue);
        SqlHighlighting.Attach(Editor);
        Editor.Options.EnableHyperlinks = Editor.Options.EnableEmailHyperlinks = false;
        FullValue.Options.EnableHyperlinks = FullValue.Options.EnableEmailHyperlinks = false;
        Editor.Document.UndoStack.SizeLimit = 128;
        Results.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
        Results.Padding = new(0);
        Results.Styles.Add(new Style(x => x.OfType<ListBoxItem>()) { Setters =
        {
            new Setter(ListBoxItem.PaddingProperty, new Thickness(0)), new Setter(ListBoxItem.MarginProperty, new Thickness(0)),
            new Setter(ListBoxItem.HorizontalContentAlignmentProperty, HorizontalAlignment.Stretch), new Setter(ListBoxItem.MinHeightProperty, 0d)
        } });
        Results.ItemTemplate = new FuncDataTemplate<SqlPreviewRow>((row, _) => CreateRow(row));
        ScopeChoices.ItemsSource = model.ScopeDocuments;
        ScopeChoices.ItemTemplate = new FuncDataTemplate<SqlScopeDocument>((item, _) =>
        {
            var box = new CheckBox { Content = item?.Label, IsChecked = item?.Selected == true };
            if (item is not null) box.Bind(ContentControl.ContentProperty, new Binding(nameof(SqlScopeDocument.Label)) { Source = item });
            box.IsCheckedChanged += (_, _) => { if (item is not null) item.Selected = box.IsChecked == true; }; return box;
        });
        DocumentsPanel.Content = new ScrollViewer { MaxHeight = 95, Content = ScopeChoices };
        Library.ItemTemplate = new FuncDataTemplate<SqlQueryLibraryItem>((entry, _) => new TextBlock { Text = entry?.Name });
        History.ItemTemplate = new FuncDataTemplate<SqlQueryHistoryEntry>((entry, _) => new TextBlock { Text = entry?.Label });
        Run.Command = model.RunCommand; Cancel.Command = model.CancelCommand;
        New.Click += (_, _) => ActionTask = model.NewAsync(); Save.Click += (_, _) => ActionTask = model.SaveAsync();
        Rename.Click += (_, _) => ActionTask = model.RenameAsync(); Delete.Click += (_, _) => ActionTask = model.DeleteAsync();
        Open.Click += (_, _) => { if (Library.SelectedItem is SqlQueryLibraryItem entry) ActionTask = model.OpenAsync(entry.Id); };
        OpenHistory.Click += (_, _) => { if (History.SelectedItem is SqlQueryHistoryEntry entry) ActionTask = model.OpenHistoryAsync(entry); };
        Previous.Click += (_, _) => ActionTask = model.ChangePageAsync(model.PageIndex - 1); Next.Click += (_, _) => ActionTask = model.ChangePageAsync(model.PageIndex + 1);
        Navigate.Click += (_, _) => ActionTask = model.NavigateAsync(); Export.Click += (_, _) => ActionTask = model.ExportAsync(); Extract.Click += (_, _) => ActionTask = model.ExtractAsync();
        Copy.Click += (_, _) => ClipboardTask = CopyAsync();
        Inspect.Click += (_, _) => { FullValuePanel.IsExpanded = true; ActionTask = model.InspectAsync(); };
        Scope.SelectionChanged += (_, _) => { if (!synchronizing) model.SelectedScope = Scope.SelectedIndex == 1; };
        QueryName.PropertyChanged += (_, e) => { if (!synchronizing && e.Property == TextBox.TextProperty) model.Name = QueryName.Text ?? ""; };
        Editor.TextChanged += (_, _) =>
        {
            if (synchronizing) return;
            if (Editor.Text.Length <= QueryLimits.MaximumSqlCharacters) { model.Sql = Editor.Text; return; }
            // Defer rollback until AvaloniaEdit closes its native undo transaction (Stage 4 invariant).
            var rejected = Editor.Text;
            Dispatcher.UIThread.Post(() =>
            {
                if (Editor.Text != rejected) return;
                synchronizing = true; try { Editor.Text = model.Sql; } finally { synchronizing = false; }
                model.ReportStatus("SQL is limited to 65,536 characters; the previous draft was retained.");
            });
        };
        Editor.AddHandler(TextInputEvent, (_, e) =>
        {
            if ((long)Editor.Document.TextLength - Editor.SelectionLength + (e.Text?.Length ?? 0) <= QueryLimits.MaximumSqlCharacters) return;
            e.Handled = true; model.ReportStatus("SQL is limited to 65,536 characters; the previous draft was retained.");
        }, RoutingStrategies.Tunnel);
        Editor.TextArea.AddHandler(KeyDownEvent, (_, e) =>
        { if (ComparisonClipboard.IsPaste(e) && !Editor.IsReadOnly) { e.Handled = true; ClipboardTask = PasteAsync(); } }, RoutingStrategies.Tunnel);
        var paste = new MenuItem { Header = "Paste SQL text" }; paste.Click += (_, _) => ClipboardTask = PasteAsync();
        Editor.ContextMenu = new ContextMenu { ItemsSource = new[] { paste } };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.KeyModifiers != KeyModifiers.Control) return;
            if (e.Key == Key.Enter) { e.Handled = true; model.RunCommand.Execute(null); }
            else if (e.Key == Key.S) { e.Handled = true; ActionTask = model.SaveAsync(); }
        }, RoutingStrategies.Tunnel);
        Results.AddHandler(KeyDownEvent, (_, e) =>
        { if (e.Key == Key.C && e.KeyModifiers == KeyModifiers.Control) { e.Handled = true; ClipboardTask = CopyAsync(); } }, RoutingStrategies.Tunnel);
        Library.SelectionChanged += (_, _) => RefreshButtons(); History.SelectionChanged += (_, _) => RefreshButtons();
        AttachedToVisualTree += (_, _) =>
        {
            synchronizing = true;
            try { model.EditorState.Restore(Editor, model, model.Sql); }
            finally { synchronizing = false; }
            if (!subscribed) { model.PropertyChanged += Changed; model.Shown += FocusEditor; subscribed = true; }
            Refresh();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            if (subscribed) { model.PropertyChanged -= Changed; model.Shown -= FocusEditor; subscribed = false; }
            synchronizing = true;
            try { model.EditorState.Capture(Editor, model); }
            finally { synchronizing = false; }
        };
        ToolTip.SetTip(Extract, "Uses every authoritative identity in the complete captured journal, across all pages. Deduplicates and preserves source order; SQL LIMIT intentionally limits selection.");
        ToolTip.SetTip(Navigate, "Requires one selected source row with authoritative DocumentId/MessageId and an unchanged, still-open original revision. Selecting a row alone never navigates.");
        ToolTip.SetTip(Rename, "Rename changes the stored name only. Save query also persists the SQL draft.");
        ToolTip.SetTip(Save, SqlQueryLibrary.StorageDisclosure);
        ToolTip.SetTip(Inspect, "Loads one complete bounded value. No preview abbreviation is copied into this inspector.");
        AutomationProperties.SetName(Editor, "SQL query editor"); AutomationProperties.SetName(Results, "SQL query results");
        AutomationProperties.SetName(QueryName, "SQL query name"); AutomationProperties.SetName(Scope, "SQL query scope");
        Status.MaxHeight = ResultStatus.MaxHeight = LibraryStatus.MaxHeight = double.PositiveInfinity;
        LayoutSurface = ResponsiveToolSurface.Attach(this, new AdaptiveActionBar(Run, Cancel),
            (4, ToolLayoutMetrics.EditorHeight), (6, ToolLayoutMetrics.ListHeight + 40));
        Refresh();
    }
    private void Add(int row, Control control) { SetRow(control, row); Children.Add(control); }
    private static AdaptiveActionBar Toolbar(params Control[] controls) => new(controls);
    private void FocusEditor() => Editor.Focus();
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private async Task CopyAsync()
    {
        try
        {
            var text = model.CopySelectionText(); if (text is null) return;
            if (TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) { model.ReportStatus("Clipboard unavailable. Use CSV export."); return; }
            await clipboard.SetTextAsync(text); model.ReportStatus("Copied complete selected cells as quoted tab-separated text; SQL NULL is explicitly labeled.");
        }
        catch (Exception) { model.ReportStatus("Clipboard unavailable. Use CSV export."); }
    }
    private async Task PasteAsync()
    {
        if (Editor.IsReadOnly || !subscribed) return;
        var original = model.Sql; var start = Editor.SelectionStart; var length = Editor.SelectionLength;
        try
        {
            var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
            if (clipboard is null) return;
            var text = await clipboard.TryGetTextAsync(); if (text is null || Editor.IsReadOnly || !subscribed) return;
            if (model.Sql != original || Editor.Text != original) { model.ReportStatus("Query changed while reading the clipboard. Paste again."); return; }
            if ((long)original.Length - length + text.Length > QueryLimits.MaximumSqlCharacters)
            { model.ReportStatus("Paste exceeds the 65,536-character SQL limit. Previous draft retained."); return; }
            Editor.Document.Replace(start, length, text); Editor.CaretOffset = start + text.Length;
        }
        catch (Exception) { model.ReportStatus("SQL clipboard paste unavailable; the current draft is retained."); }
    }
    private ColumnDefinitions Columns()
    {
        var columns = new ColumnDefinitions(); columns.Add(new() { Width = new(48, GridUnitType.Pixel) });
        foreach (var width in widths) columns.Add(new() { Width = new(width, GridUnitType.Pixel) }); return columns;
    }
    private Control CreateRow(SqlPreviewRow? row)
    {
        var grid = new Grid { ColumnDefinitions = Columns(), MinHeight = 30 };
        if (row is null) return grid;
        var pageRow = model.Page.ToList().IndexOf(row);
        var number = new Button { Content = row.RowNumber.ToString(), Padding = new(3, 2), HorizontalAlignment = HorizontalAlignment.Stretch };
        number.Click += (_, _) => model.SelectCell(pageRow, -1); grid.Children.Add(number);
        for (var i = 0; i < row.Cells.Count; i++)
        {
            var column = i; var cell = row.Cells[i];
            var text = new TextBlock { Text = Preview(cell), TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new("Consolas, monospace"), [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.12"), FontStyle = cell.Kind == QueryValueKind.Null ? FontStyle.Italic : FontStyle.Normal };
            var border = new Border { Child = text, Padding = new(6, 4), BorderThickness = new(0, 0, 1, 1), ClipToBounds = true, Background = Brushes.Transparent };
            border.Bind(Border.BorderBrushProperty, new DynamicResourceExtension(WorkbenchThemeResources.Border));
            AutomationProperties.SetAutomationId(border, $"sql-cell-{row.RowNumber}-{column}");
            ToolTip.SetTip(border, $"{cell.Kind}" + (cell.Abbreviated ? " · abbreviated display; Inspect full value reads the complete bounded value." : " · complete bounded preview"));
            border.PointerPressed += (_, e) =>
            { model.SelectCell(pageRow, column, e.KeyModifiers.HasFlag(KeyModifiers.Shift), e.KeyModifiers.HasFlag(KeyModifiers.Control)); Results.Focus(); e.Handled = true; };
            border.DoubleTapped += (_, e) => { ActionTask = model.NavigateAsync(); e.Handled = true; };
            border.AttachedToVisualTree += (_, _) => { realized[(row.RowNumber - 1, column)] = border; PaintSelection(); };
            border.DetachedFromVisualTree += (_, _) => { if (realized.GetValueOrDefault((row.RowNumber - 1, column)) == border) realized.Remove((row.RowNumber - 1, column)); };
            SetColumn(border, i + 1); grid.Children.Add(border);
        }
        return grid;
    }
    public static string Preview(SqlPreviewCell cell) => cell.Kind == QueryValueKind.Null ? "(SQL NULL)" :
        cell.Kind == QueryValueKind.Text && cell.Preview.Length == 0 ? "(empty TEXT)" :
        cell.Preview.Replace("\r", "↵", StringComparison.Ordinal).Replace("\n", "↵", StringComparison.Ordinal).Replace("\t", "⇥", StringComparison.Ordinal) + (cell.Abbreviated ? " …" : "");
    private static string HeaderLabel(string name)
    {
        // SQLite can derive a column name from a long multiline expression/literal.
        // Column metadata stays exact for export, while headers remain bounded single lines.
        var count = Math.Min(name.Length, 80);
        if (count < name.Length && count > 0 && char.IsHighSurrogate(name[count - 1])) count--;
        return name[..count].Replace("\r", "↵", StringComparison.Ordinal).Replace("\n", "↵", StringComparison.Ordinal).Replace("\t", "⇥", StringComparison.Ordinal) + (count < name.Length ? " …" : "");
    }
    private void PaintSelection()
    {
        foreach (var (cell, border) in realized)
        {
            // Use a translucent accent so foreground remains legible in either theme.
            border.Background = model.Selection.Contains(cell) ? new SolidColorBrush(Color.FromArgb(80, 70, 135, 220)) : Brushes.Transparent;
        }
    }
    private void RefreshButtons()
    {
        Open.IsEnabled = model.CanManage && Library.SelectedItem is SqlQueryLibraryItem;
        OpenHistory.IsEnabled = model.CanManage && History.SelectedItem is SqlQueryHistoryEntry;
    }
    private void Refresh()
    {
        synchronizing = true;
        try
        {
            if (Editor.Text != model.Sql) Editor.Text = model.Sql;
            Editor.IsReadOnly = !model.CanManage; QueryName.Text = model.Name; QueryName.IsReadOnly = !model.CanManage;
            Scope.SelectedIndex = model.SelectedScope ? 1 : 0; Scope.IsEnabled = DocumentsPanel.IsEnabled = model.CanManage;
            DocumentsPanel.IsVisible = model.SelectedScope; Target.Text = model.Target;
            var selectedLibrary = (Library.SelectedItem as SqlQueryLibraryItem)?.Id ?? model.SavedId;
            if (!ReferenceEquals(Library.ItemsSource, model.SavedQueries)) { Library.ItemsSource = model.SavedQueries; Library.SelectedItem = model.SavedQueries.FirstOrDefault(e => e.Id == selectedLibrary); }
            if (displayedSavedId != model.SavedId)
            { displayedSavedId = model.SavedId; Library.SelectedItem = model.SavedQueries.FirstOrDefault(e => e.Id == model.SavedId); }
            var selectedHistory = History.SelectedItem; History.ItemsSource = model.History; History.SelectedItem = selectedHistory;
            Library.IsEnabled = History.IsEnabled = New.IsEnabled = Save.IsEnabled = model.CanManage;
            Rename.IsEnabled = Delete.IsEnabled = model.CanManage && model.SavedId is not null;
            DraftStatus.Text = model.IsDirty ? "● Unsaved query" : model.SavedId is null ? "Session draft" : "Saved";
            LibraryStatus.Text = model.LibraryStatus; ToolTip.SetTip(LibraryStatus, model.LibraryStatus);
            Status.Text = model.Status; ToolTip.SetTip(Status, model.Status); ResultStatus.Text = model.ResultStatus;
            ToolTip.SetTip(ResultStatus, model.ResultStatus);
            if (!ReferenceEquals(renderedResult, model.Results))
            {
                renderedResult = model.Results;
                widths = (model.Results?.Columns ?? []).Select((label, column) => Math.Clamp(Math.Max(label.Length, model.Page.Take(10).Select(r => Preview(r.Cells[column]).Length).DefaultIfEmpty(12).Max()) * 8.4 + 36, 115, 320)).ToArray();
                displayedSort = -2;
            }
            if (displayedSort != model.SortColumn || displayedDescending != model.Descending)
            {
                displayedSort = model.SortColumn; displayedDescending = model.Descending; headers.Children.Clear(); headers.ColumnDefinitions = Columns();
                headers.Children.Add(new TextBlock { Text = "#", Padding = new(6), FontWeight = FontWeight.SemiBold });
                for (var i = 0; i < (model.Results?.Columns.Count ?? 0); i++)
                {
                    var column = i; var label = HeaderLabel(model.Results!.Columns[i]); var button = new Button { Content = label + (i == displayedSort ? model.Descending ? " ▼" : " ▲" : ""),
                        HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Left, FontWeight = FontWeight.SemiBold };
                    ToolTip.SetTip(button, label + " · Click to sort all rows. Full column names are retained in SQL and CSV.");
                    button.Click += (_, _) => ActionTask = model.SortAsync(column); SetColumn(button, i + 1); headers.Children.Add(button);
                }
            }
            headers.IsEnabled = model.CanManage && model.Results is not null;
            if (!ReferenceEquals(renderedPage, model.Page)) { renderedPage = model.Page; realized.Clear(); Results.ItemsSource = renderedPage; }
            Results.IsEnabled = model.CanManage; PaintSelection(); RefreshButtons();
            Previous.IsEnabled = model.CanManage && model.Results is not null && model.PageIndex > 0;
            Next.IsEnabled = model.CanManage && model.Results is not null && model.PageIndex + 1 < model.PageCount;
            PageStatus.Text = model.Results is null ? "No results" : $"Page {model.PageIndex + 1}/{model.PageCount} · {model.Selection.Count} cells selected";
            Copy.IsEnabled = model.CanManage && model.Selection.Count > 0; Inspect.IsEnabled = model.CanManage && model.HasSelectedCell;
            Navigate.IsEnabled = model.CanNavigate; Export.IsEnabled = model.CanManage && model.Results is not null;
            Extract.IsEnabled = model.CanManage && model.Results?.CanIdentifyMessages == true;
            ToolTip.SetTip(Extract, model.Results?.CanIdentifyMessages == true ?
                "Uses every authoritative identity across all journal pages, not just selected rows. Deduplicates and preserves source order; SQL LIMIT intentionally limits selection." :
                "Return original DocumentId and MessageId columns to extract messages. Aggregates and computed aliases cannot identify source messages. Run a separate identity-selection query.");
            if (FullValue.Text != model.FullValue) FullValue.Text = model.FullValue;
            FullValueKind.Text = model.FullValueKind;
        }
        finally { synchronizing = false; }
    }
}
