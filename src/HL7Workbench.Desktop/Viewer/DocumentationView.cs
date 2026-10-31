using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using HL7Workbench.Desktop.Documentation;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Native selectable help. Known links navigate metadata only; examples are never executed.</summary>
internal sealed class DocumentationView : Grid
{
    private readonly DocumentationViewModel model;
    private readonly Grid panes = new();
    private readonly StackPanel article = new() { Spacing = 10 };
    private readonly Dictionary<string, Control> anchors = new(StringComparer.Ordinal);
    private readonly ScrollViewer detail;
    private bool synchronizing, attached;
    private HelpTopic? rendered;
    private string? shownAnchor;
    internal TextBox Search { get; } = new() { PlaceholderText = "Search offline documentation", MaxLength = DocumentationCatalog.MaximumQueryCharacters };
    internal ListBox Topics { get; } = new();
    internal ComboBox Headings { get; } = new() { PlaceholderText = "Topic headings", HorizontalAlignment = HorizontalAlignment.Stretch };
    internal Button Contents { get; } = new() { Content = "Contents" };
    internal Button Back { get; } = new() { Content = "Back" };
    internal TextBlock Status { get; } = new() { TextWrapping = TextWrapping.Wrap };
    internal TextBlock Version { get; } = new() { Text = "Offline · documentation 1", VerticalAlignment = VerticalAlignment.Center };
    internal Task ClipboardTask { get; private set; } = Task.CompletedTask;
    internal DocumentationView(DocumentationViewModel model)
    {
        this.model = model; Margin = new(8); RowDefinitions = new("Auto,Auto,*,Auto"); RowSpacing = 6; MinWidth = 0;
        var actions = new AdaptiveActionBar(Contents, Back, Version); Children.Add(actions);
        SetRow(Search, 1); Children.Add(Search);
        Topics.ItemTemplate = new FuncDataTemplate<HelpSearchResult>((result, _) => new StackPanel { Spacing = 3, Margin = new(3), Children =
        {
            new TextBlock { Text = result?.Topic.Title, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap },
            new TextBlock { Text = result?.Snippet, [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.12"), TextWrapping = TextWrapping.Wrap }
        } });
        Topics.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
        Headings.ItemTemplate = new FuncDataTemplate<HelpBlock>((heading, _) => new TextBlock { Text = heading?.Text, TextWrapping = TextWrapping.Wrap });
        detail = new ScrollViewer { Content = article, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        var topic = new Grid { RowDefinitions = new("Auto,*"), RowSpacing = 8 };
        topic.Children.Add(Headings); SetRow(detail, 1); topic.Children.Add(detail);
        panes.Children.Add(Topics); panes.Children.Add(topic); SetRow(panes, 2); Children.Add(panes);
        SetRow(Status, 3); Children.Add(Status);
        AutomationProperties.SetName(Search, "Search Documentation"); AutomationProperties.SetName(Topics, "Documentation topics and search results");
        AutomationProperties.SetName(Headings, "Documentation topic headings"); AutomationProperties.SetName(detail, "Documentation article");
        AutomationProperties.SetName(Contents, "Documentation contents"); AutomationProperties.SetName(Back, "Back in Documentation");
        Contents.Click += (_, _) => model.Contents(); Back.Click += (_, _) => model.Back();
        Search.PropertyChanged += (_, e) => { if (!synchronizing && e.Property == TextBox.TextProperty) model.Query = Search.Text ?? ""; };
        Topics.SelectionChanged += (_, _) => { if (!synchronizing && Topics.SelectedItem is HelpSearchResult result) model.Navigate(result.Topic.Id, result.Anchor); };
        Headings.SelectionChanged += (_, _) => { if (!synchronizing && Headings.SelectedItem is HelpBlock heading) model.Navigate(model.Topic.Id, heading.Anchor); };
        AttachedToVisualTree += (_, _) => { attached = true; model.PropertyChanged += Changed; model.Shown += FocusSearch; Refresh(); };
        DetachedFromVisualTree += (_, _) => { attached = false; model.PropertyChanged -= Changed; model.Shown -= FocusSearch; };
        SizeChanged += (_, _) => ArrangePanes(); ArrangePanes(); Refresh();
    }
    private void FocusSearch() { Search.Focus(); Search.SelectAll(); }
    private void ArrangePanes()
    {
        var wide = Bounds.Width >= 720;
        // Proportional rows keep even a short dock finite; a fixed results height
        // can otherwise extend over the status row before the article gets space.
        panes.RowDefinitions = new(wide ? "*" : "2*,3*"); panes.ColumnDefinitions = new(wide ? "2*,5*" : "*");
        SetRow(panes.Children[0], 0); SetColumn(panes.Children[0], 0);
        SetRow(panes.Children[1], wide ? 0 : 1); SetColumn(panes.Children[1], wide ? 1 : 0);
        panes.ColumnSpacing = wide ? 10 : 0; panes.RowSpacing = wide ? 0 : 8;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        synchronizing = true;
        try
        {
            Search.Text = model.Query; Topics.ItemsSource = model.Results;
            Topics.SelectedItem = model.Results.FirstOrDefault(r => r.Topic.Id == model.Topic.Id);
            Back.IsEnabled = model.CanBack; Status.Text = model.Feedback;
            if (!ReferenceEquals(rendered, model.Topic))
            {
                rendered = model.Topic; shownAnchor = null; article.Children.Clear(); anchors.Clear();
                foreach (var block in rendered.Blocks)
                {
                    var control = Render(block); article.Children.Add(control);
                    if (block.Kind == "heading") anchors.Add(block.Anchor, control);
                }
                Headings.ItemsSource = rendered.Blocks.Where(b => b.Kind == "heading").ToArray();
            }
            Headings.SelectedItem = model.Topic.Blocks.FirstOrDefault(b => b.Kind == "heading" && b.Anchor == model.Anchor);
            if (shownAnchor != model.Anchor)
            {
                shownAnchor = model.Anchor; var topic = model.Topic; var anchor = model.Anchor;
                detail.ScrollToHome();
                if (anchor.Length > 0) Dispatcher.UIThread.Post(() =>
                {
                    // A queued scroll from a detached/replaced view must not act on
                    // another topic. Navigation itself never steals keyboard focus.
                    if (attached && ReferenceEquals(topic, model.Topic) && model.Anchor == anchor && anchors.TryGetValue(anchor, out var target)) target.BringIntoView();
                }, DispatcherPriority.Loaded);
            }
        }
        finally { synchronizing = false; }
    }
    private Control Render(HelpBlock block)
    {
        if (block.Kind == "code")
        {
            var copy = new Button { Content = "Copy code" }; AutomationProperties.SetName(copy, "Copy documentation code example");
            var code = new TextBox { Text = block.Text, IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
                FontFamily = new("Consolas, monospace"), MinHeight = 65, MaxHeight = 320 };
            ScrollViewer.SetHorizontalScrollBarVisibility(code, ScrollBarVisibility.Auto); ScrollViewer.SetVerticalScrollBarVisibility(code, ScrollBarVisibility.Auto);
            AutomationProperties.SetName(code, "Documentation code example " + block.Language);
            copy.Click += (_, _) => ClipboardTask = CopyAsync(block.Text);
            return new StackPanel { Spacing = 4, Children = { copy, code } };
        }
        if (block.Kind == "table")
        {
            var columns = block.Rows[0].Count; var grid = new Grid { ColumnDefinitions = new(string.Join(',', Enumerable.Repeat("*", columns))), MinWidth = columns * 180 };
            for (var row = 0; row < block.Rows.Count; row++)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                for (var column = 0; column < columns; column++)
                {
                    var cell = Rich(block.Rows[row][column]); if (row == 0) cell.FontWeight = FontWeight.SemiBold;
                    var content = new StackPanel { Spacing = 4, MinWidth = 150, MaxWidth = 400, Children = { cell } };
                    AddLinks(content, block.Rows[row][column]);
                    var border = new Border { Padding = new(6), BorderThickness = new(0, 0, 0, 1), Child = content };
                    border.Bind(Border.BorderBrushProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("SystemControlForegroundBaseLowBrush"));
                    SetRow(border, row); SetColumn(border, column); grid.Children.Add(border);
                }
            }
            AutomationProperties.SetName(grid, "Documentation table");
            return new ScrollViewer { Content = grid, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled };
        }
        var text = Rich(block.Spans);
        if (block.Kind == "heading")
        {
            text.FontWeight = FontWeight.SemiBold;
            text.Bind(TextBlock.FontSizeProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension(block.Level == 1 ? "ContentFont.23" : block.Level == 2 ? "ContentFont.19" : "ContentFont.16"));
        }
        var panel = new StackPanel { Spacing = 4 };
        if (block.Kind == "list")
        {
            var row = new Grid { ColumnDefinitions = new("Auto,*"), ColumnSpacing = 6 };
            row.Children.Add(new TextBlock { Text = block.Marker }); SetColumn(text, 1); row.Children.Add(text); panel.Children.Add(row);
        }
        else panel.Children.Add(text);
        AddLinks(panel, block.Spans);
        if (block.Kind == "quote")
        {
            var quote = new Border { BorderThickness = new(3, 0, 0, 0), Padding = new(10, 3), Child = panel };
            quote.Bind(Border.BorderBrushProperty, new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("SystemControlForegroundBaseMediumBrush"));
            return quote;
        }
        return panel;
    }
    private static SelectableTextBlock Rich(IReadOnlyList<HelpSpan> spans)
    {
        var text = new SelectableTextBlock { TextWrapping = TextWrapping.Wrap };
        foreach (var span in spans)
        {
            var run = new Run(span.Text);
            if (span.Kind == "strong") run.FontWeight = FontWeight.Bold;
            if (span.Kind == "emphasis") run.FontStyle = FontStyle.Italic;
            if (span.Kind == "code") run.FontFamily = new("Consolas, monospace");
            text.Inlines!.Add(run);
        }
        return text;
    }
    private void AddLinks(StackPanel panel, IReadOnlyList<HelpSpan> spans)
    {
        var links = new AdaptiveActionBar();
        foreach (var span in spans.Where(s => s.Link is not null))
        {
            var link = span.Link!;
            var caption = span.Text + (link.Kind == "repository" ? " (repository)" : link.Kind == "external" ? " (external reference)" : link.Kind == "api" ? " (Script API)" : "");
            var button = new Button { Content = new TextBlock { Text = caption, TextWrapping = TextWrapping.Wrap }, Padding = new(6, 3) };
            AutomationProperties.SetName(button, caption); ToolTip.SetTip(button, link.Kind == "topic" ? "Open offline topic" : link.Kind == "api" ? "Open existing Script API Reference" : "Show reference location; no file or network access");
            button.Click += (_, _) => model.Follow(link); links.Children.Add(button);
        }
        if (links.Children.Count > 0) panel.Children.Add(links);
    }
    private async Task CopyAsync(string code)
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) { Status.Text = "Clipboard unavailable; select and copy the example text."; return; }
        try { await clipboard.SetTextAsync(code); Status.Text = "Documentation code copied."; }
        catch (Exception e) when (e is InvalidOperationException or NotSupportedException) { Status.Text = "Clipboard unavailable; select and copy the example text."; }
    }
}
