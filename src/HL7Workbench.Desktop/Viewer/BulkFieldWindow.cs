using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>One transient native review surface. The shell awaits its work and resource drain on close.</summary>
internal sealed class BulkFieldWindow : Window, IAsyncDisposable
{
    private readonly MessageDocument source;
    private readonly DocumentManager documents;
    private readonly Hl7Path path;
    private readonly BulkFieldOperation operation;
    private readonly BulkFieldOperations service;
    private readonly Action<MessageDocument>? presentResult;
    private BulkFieldResults? result;
    private CancellationTokenSource? cancellation;
    private bool busy, closed;
    private int page;
    private BulkFieldSort sort;
    private bool descending;
    internal ComboBox Scope { get; } = new() { ItemsSource = new[] { "Current message", "Active file", "Current navigator filter (all matches)" }, SelectedIndex = 1, HorizontalAlignment = HorizontalAlignment.Stretch };
    internal CheckBox AllOccurrences { get; } = new() { Content = "Every occurrence of this segment type" };
    internal TextBox Find { get; } = new() { MaxLength = 65536 };
    internal TextBox Replacement { get; } = new() { MaxLength = MessageDocument.MaximumEditableLineCharacters };
    internal Button Analyze { get; } = new() { Content = "Analyze / Preview", IsDefault = true };
    internal Button Apply { get; } = new() { Content = "Apply all to new document" };
    internal CheckBox Confirm { get; } = new() { Content = "I reviewed the preview; apply all valid proposals to a separate complete dataset" };
    internal Button CancelRun { get; } = new() { Content = "Cancel operation", IsEnabled = false };
    internal Button Filter { get; } = new() { Content = "Filter Navigator by selected value" };
    internal Button Navigate { get; } = new() { Content = "Go to first matching message" };
    internal Button Previous { get; } = new() { Content = "Previous page" };
    internal Button Next { get; } = new() { Content = "Next page" };
    internal Button SortValue { get; } = new() { Content = "Sort by value" };
    internal Button SortFrequency { get; } = new() { Content = "Sort by frequency" };
    internal ListBox Rows { get; } = new();
    internal TextBox Details { get; } = new() { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap, Height = 100 };
    internal TextBlock Status { get; } = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock pageStatus = new();
    internal Task Completion { get; private set; } = Task.CompletedTask;
    internal BulkFieldResults? Result => result;
    internal BulkFieldSummary? PublishedSummary { get; private set; }
    internal ResponsiveToolSurface LayoutSurface { get; }
    internal BulkFieldWindow(DocumentManager documents, MessageDocument source, Hl7Path path, BulkFieldOperation operation,
        ApplicationPreferences preferences, BulkFieldOptions? options = null, string? initialFindValue = null, Action<MessageDocument>? presentResult = null)
    {
        this.documents = documents; this.source = source; this.path = path; this.operation = operation; service = new(options);
        this.presentResult = presentResult;
        var unique = operation == BulkFieldOperation.UniqueValues;
        Title = unique ? "Unique Values" : operation == BulkFieldOperation.Assign ? "Assign Value" : "Structural Search & Replace";
        Width = 980; Height = 780; MinWidth = 580; MinHeight = 560; WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var identity = new TextBlock { Text = $"{path} · {source.DisplayTitle}", TextTrimming = TextTrimming.CharacterEllipsis };
        ToolTip.SetTip(identity, $"{source.DisplayTitle} · {path}\nSelected repetition and structural level stay fixed. Whole fields include all their repetitions.");
        ToolTip.SetTip(AllOccurrences, $"Off: only segment occurrence {path.Occurrence}. On: each occurrence, with the same field/repetition/component indexes.");
        AllOccurrences.Content = $"Every {path.Segment} occurrence";
        Find.IsVisible = operation == BulkFieldOperation.SearchReplace; Replacement.IsVisible = !unique;
        Find.Text = Find.IsVisible && initialFindValue is { Length: <= 65536 } ? initialFindValue : "";
        Replacement.Text = "";
        Find.PlaceholderText = "Enter a complete raw value";
        Replacement.PlaceholderText = "Empty clears existing values";
        var inputs = new StackPanel { Spacing = 6, Children = { identity } };
        if (!unique) inputs.Children.Add(new TextBlock { Text = "Exact raw HL7 encoded values; case sensitive.", TextWrapping = TextWrapping.Wrap });
        if (Find.IsVisible) inputs.Children.Add(ValueField("Find value:", Find));
        if (!unique) inputs.Children.Add(ValueField(operation == BulkFieldOperation.Assign ? "New value:" : "Replace with:", Replacement));
        inputs.Children.Add(new ResponsiveFormField("Operation scope", Scope)); inputs.Children.Add(AllOccurrences);
        Confirm.Content = new TextBlock { Text = "I reviewed the preview; apply all valid proposals to a separate complete dataset", TextWrapping = TextWrapping.Wrap, MaxWidth = 440 };
        var controls = new AdaptiveActionBar(Analyze, CancelRun, SortValue, SortFrequency, Previous, Next, pageStatus);
        var actions = new AdaptiveActionBar(Filter, Navigate, Confirm, Apply, new Button { Content = "Close", IsCancel = true });
        ((Button)actions.Children.Last()).Click += (_, _) => Close();
        SortValue.IsVisible = SortFrequency.IsVisible = Filter.IsVisible = Navigate.IsVisible = unique;
        Confirm.IsVisible = Apply.IsVisible = !unique;
        Rows.ItemTemplate = new FuncDataTemplate<object>((row, _) => new TextBlock
        {
            Text = row is BulkUniqueValue v ? $"{v.Preview}     Occurrences: {v.Occurrences:N0}     Present nodes: {v.NodeCount:N0}     Messages: {v.MessageCount:N0}" :
                row is BulkFieldChange c ? $"Message {c.MessageNumber} · {c.Path} · {c.Status}\n{c.OriginalPreview}  →  {c.ReplacementPreview}" : "",
            TextWrapping = TextWrapping.Wrap
        });
        Rows.SelectionChanged += (_, _) =>
        {
            Details.Text = Rows.SelectedItem is BulkUniqueValue v ? $"State: {v.State}\nComplete raw value:\n{v.Raw}" :
                Rows.SelectedItem is BulkFieldChange c ? $"Message {c.MessageNumber} · {c.MessageId} · {c.Path} · {c.Status}\nOriginal raw:\n{c.Original}\nReplacement raw:\n{c.Replacement}" : "";
            Refresh();
        };
        // Essential value inputs must precede Analyze in the visible form. A small
        // independently capped parameter scroller hid them beneath scope controls.
        // Reuse whole-surface overflow and a finite results minimum instead.
        var summary = new ScrollViewer { Content = Status, MaxHeight = 48, HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled };
        var details = new Expander { Header = "Inspect complete selected values", Content = Details, MaxHeight = 120, HorizontalAlignment = HorizontalAlignment.Stretch };
        var root = new Grid { Margin = new(16), RowDefinitions = new("Auto,Auto,Auto,*,Auto,Auto"), RowSpacing = 8 };
        foreach (var (control, row) in new (Control, int)[] { (inputs, 0), (controls, 1), (summary, 2), (Rows, 3), (details, 4), (actions, 5) })
        { Grid.SetRow(control, row); root.Children.Add(control); }
        LayoutSurface = ResponsiveToolSurface.Attach(root, (3, ToolLayoutMetrics.ListHeight));
        Content = root; _ = new ContentFontScope(root, preferences, "tool.field-inspector");
        AutomationProperties.SetName(Rows, unique ? "Unique raw values, node counts and message counts" : "Proposed changes and validation status");
        AutomationProperties.SetName(Details, "Complete selected result values");
        AutomationProperties.SetName(Scope, "Explicit bulk operation scope");
        AutomationProperties.SetName(Find, "Find value: exact raw HL7 encoded text, case sensitive");
        AutomationProperties.SetName(Replacement, operation == BulkFieldOperation.Assign ? "New value: raw HL7 encoded text; empty clears values" : "Replace with: raw HL7 encoded text; empty clears values");
        AutomationProperties.SetName(Confirm, "Confirm all proposed replacements in a separate result document");
        Analyze.Click += (_, _) => { if (!busy && !closed) Completion = AnalyzeAsync(); };
        Apply.Click += (_, _) => { if (!busy && !closed) Completion = ApplyAsync(); };
        CancelRun.Click += (_, _) => cancellation?.Cancel();
        Scope.SelectionChanged += (_, _) => Invalidate(); AllOccurrences.IsCheckedChanged += (_, _) => Invalidate();
        Find.PropertyChanged += ParameterChanged; Replacement.PropertyChanged += ParameterChanged;
        Confirm.IsCheckedChanged += (_, _) => Refresh();
        SortValue.Click += (_, _) => Sort(BulkFieldSort.Value); SortFrequency.Click += (_, _) => Sort(BulkFieldSort.Frequency);
        Previous.Click += (_, _) => { page--; ShowPage(); }; Next.Click += (_, _) => { page++; ShowPage(); };
        Filter.Click += (_, _) =>
        {
            if (result is null || Rows.SelectedItem is not BulkUniqueValue value) return;
            try { result.FilterNavigator(value); documents.Activate(source.Id); Close(); }
            catch (InvalidOperationException) { Status.Text = "Source or scope changed. Analyze again."; Refresh(); }
        };
        Navigate.Click += (_, _) => { if (!busy && !closed) Completion = NavigateAsync(); };
        source.PropertyChanged += SourceChanged; source.Closed += SourceClosed;
        Opened += (_, _) =>
        {
            if (Find.IsVisible) { Find.Focus(); Find.SelectAll(); }
            else if (Replacement.IsVisible) Replacement.Focus();
            else Scope.Focus();
        };
        Closed += (_, _) => { closed = true; cancellation?.Cancel(); source.PropertyChanged -= SourceChanged; source.Closed -= SourceClosed;
            Find.Text = Replacement.Text = Details.Text = ""; Rows.ItemsSource = null; };
        Status.Text = "Choose scope, then analyze. Source stays unchanged; mutations require preview and confirmation."; Refresh();
    }
    private static Grid ValueField(string text, TextBox input)
    {
        var label = new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center };
        var field = new Grid { ColumnDefinitions = new("Auto,*"), ColumnSpacing = 8 };
        input.VerticalAlignment = VerticalAlignment.Center; input.HorizontalAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetLabeledBy(input, label); field.Children.Add(label); Grid.SetColumn(input, 1); field.Children.Add(input);
        return field;
    }
    private void ParameterChanged(object? sender, AvaloniaPropertyChangedEventArgs e) { if (e.Property == TextBox.TextProperty) Invalidate(); }
    private void SourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MessageDocument.SearchRevision) or nameof(MessageDocument.NavigationMatchIds))
        { if (result?.IsCurrent == false) { cancellation?.Cancel(); Status.Text = "Source or captured filter changed. Analyze again."; Refresh(); } }
    }
    private void SourceClosed(object? sender, EventArgs e) { cancellation?.Cancel(); Status.Text = "Source closed; analysis and publication are unavailable."; Refresh(); }
    private void Invalidate()
    {
        if (closed || busy) return;
        result?.Dispose(); result = null; Rows.ItemsSource = null; Details.Text = ""; Confirm.IsChecked = false; Refresh();
    }
    private void Begin() { busy = true; cancellation = new(); Confirm.IsChecked = false; Refresh(); }
    private void End() { cancellation?.Dispose(); cancellation = null; busy = false; if (!closed) Refresh(); }
    private async Task AnalyzeAsync()
    {
        if (busy || closed) return;
        if (Scope.SelectedIndex == 2 && source.NavigationMatchIds is null) { Status.Text = "Apply a current navigator filter first, or choose Current message / Active file."; return; }
        Invalidate(); Begin();
        try
        {
            var request = new BulkFieldRequest(path, operation, (BulkFieldScope)Scope.SelectedIndex, Find.Text ?? "", Replacement.Text ?? "", AllOccurrences.IsChecked == true);
            var complete = await service.AnalyzeAsync(source, request, Progress(), cancellation!.Token);
            if (closed) { complete.Dispose(); return; }
            result = complete; page = 0; ShowPage();
            var s = result.Summary;
            Status.Text = $"Scanned {s.ScannedMessages:N0}; containing path {s.MessagesContainingPath:N0}; selected slots {s.SelectedSlots:N0}; present nodes {s.Nodes:N0}; absent {s.Absent:N0}; skipped {s.Skipped:N0}; unchanged {s.Unchanged:N0}; invalid {s.Invalid:N0}; proposed {s.Replacements:N0} in {s.ChangedMessages:N0} messages. Captured revision {result.Revision}.";
        }
        catch (Exception e) when (Expected(e)) { if (!closed) Status.Text = Failure(e); }
        finally { End(); }
    }
    private async Task ApplyAsync()
    {
        if (busy || closed || result is null || !Apply.IsEnabled) return;
        var confirmed = Confirm.IsChecked == true; Begin();
        try { await result.ApplyAsync(documents, confirmed, Progress(), cancellation!.Token, presentResult); PublishedSummary = result.Summary; Close(); }
        catch (Exception e) when (Expected(e)) { if (!closed) Status.Text = Failure(e); }
        finally { End(); }
    }
    private async Task NavigateAsync()
    {
        if (busy || result is null || Rows.SelectedItem is not BulkUniqueValue value) return; Begin();
        try { await result.NavigateAsync(value, cancellation!.Token); documents.Activate(source.Id); }
        catch (Exception e) when (Expected(e)) { if (!closed) Status.Text = Failure(e); }
        finally { End(); }
    }
    private IProgress<BulkFieldProgress> Progress() => new Progress<BulkFieldProgress>(p => { if (!closed && busy) Status.Text = $"{p.Phase}: {p.Completed:N0} / {p.Total:N0}"; });
    private static bool Expected(Exception e) => e is OperationCanceledException or IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException or Hl7LimitException or HL7Workbench.Scripting.ScriptRunException;
    private static string Failure(Exception e) => e is BulkFieldPublicationException ? "Result could not be opened. No result was retained; source is unchanged. Review and confirm again to retry." :
        e is OperationCanceledException ? "Canceled. No result was published; source is unchanged." :
        e is Hl7LimitException ? "Operation exceeded a resource budget. No partial result was retained or published." :
        "Operation unavailable: check source freshness, scope, raw replacement, encoding and editing/capture limits. No partial result was published.";
    private void Sort(BulkFieldSort value)
    {
        if (sort == value) descending = !descending; else { sort = value; descending = value == BulkFieldSort.Frequency; }
        SortValue.Content = "Value" + (sort == BulkFieldSort.Value ? descending ? " ↓" : " ↑" : "");
        SortFrequency.Content = "Frequency" + (sort == BulkFieldSort.Frequency ? descending ? " ↓" : " ↑" : "");
        page = 0; ShowPage();
    }
    private void ShowPage()
    {
        if (result is null || closed) return;
        Rows.ItemsSource = operation == BulkFieldOperation.UniqueValues ? result.UniquePage(page, sort, descending) : result.ChangePage(page);
        pageStatus.Text = $"Page {page + 1:N0} / {result.PageCount:N0} · {result.RowCount:N0} rows"; Refresh();
    }
    private void Refresh()
    {
        Analyze.IsEnabled = !busy && !closed && documents.Documents.Contains(source);
        Scope.IsEnabled = AllOccurrences.IsEnabled = Find.IsEnabled = Replacement.IsEnabled = !busy;
        CancelRun.IsEnabled = busy; Confirm.IsEnabled = !busy && result?.CanApply == true;
        Apply.IsEnabled = !busy && result?.CanApply == true && Confirm.IsChecked == true;
        Filter.IsEnabled = Navigate.IsEnabled = !busy && result?.IsCurrent == true && Rows.SelectedItem is BulkUniqueValue;
        Previous.IsEnabled = !busy && result is not null && page > 0; Next.IsEnabled = !busy && result is not null && page + 1 < result.PageCount;
        SortValue.IsEnabled = SortFrequency.IsEnabled = !busy && result is not null;
    }
    public async ValueTask DisposeAsync()
    {
        cancellation?.Cancel(); await Completion;
        if (result is { } owned) { owned.Dispose(); await owned.Disposal; result = null; }
    }
}
