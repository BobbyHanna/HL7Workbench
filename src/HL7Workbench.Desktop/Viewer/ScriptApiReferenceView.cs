using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Offline, theme-aware help using ordinary selectable text and the platform clipboard.</summary>
public sealed class ScriptApiReferenceView : Grid
{
    private readonly ScriptApiReferenceViewModel model;
    private bool synchronizing;
    private string? renderedEntryId;
    private readonly Grid body = new();
    private readonly GridSplitter splitter = new();
    private readonly ScrollViewer detailHost;
    public TextBox Search { get; } = new() { PlaceholderText = "Search class, member or keyword", MaxLength = 256 };
    public ComboBox Category { get; } = new() { HorizontalAlignment = HorizontalAlignment.Stretch };
    public ListBox Topics { get; } = new();
    public Button Back { get; } = new() { Content = "Back" };
    public Button CopySignature { get; } = new() { Content = "Copy signature" };
    public Button CopyExample { get; } = new() { Content = "Copy example" };
    public Button CreateScript { get; } = new() { Content = "Create editable script" };
    public TextBlock Title { get; } = new() { FontSize = 18, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
    public TextBox Signature { get; } = Code();
    public TextBox Example { get; } = Code();
    public TextBlock Status { get; } = new() { TextWrapping = TextWrapping.Wrap };
    public TextBlock Version { get; } = new() { FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
    private readonly SelectableTextBlock description = Prose(), parameters = Prose(), returns = Prose(), limitations = Prose();
    public Task ClipboardTask { get; private set; } = Task.CompletedTask;
    public Task<bool> CreationTask { get; private set; } = Task.FromResult(false);

    public ScriptApiReferenceView(ScriptApiReferenceViewModel model)
    {
        this.model = model; Margin = new(8); RowDefinitions = new("Auto,Auto,Auto,*,Auto"); MinWidth = 280; MinHeight = 180;
        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        top.Children.Add(Back); top.Children.Add(Version); Children.Add(top);
        Search.Margin = new(0, 6); SetRow(Search, 1); Children.Add(Search);
        Category.ItemsSource = model.Categories; Category.Margin = new(0, 0, 0, 6); SetRow(Category, 2); Children.Add(Category);
        Topics.ItemTemplate = new FuncDataTemplate<ScriptApiEntry>((entry, _) => new TextBlock
        { Text = entry?.Title, TextWrapping = TextWrapping.Wrap, Margin = new(2) });
        Topics.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
        var detail = new StackPanel { Spacing = 7, Margin = new(8, 0, 2, 0) };
        detail.Children.Add(Title); detail.Children.Add(description);
        var actions = new WrapPanel { Orientation = Orientation.Horizontal };
        foreach (var button in new[] { CopySignature, CopyExample, CreateScript }) { button.Margin = new(0, 0, 5, 3); actions.Children.Add(button); }
        detail.Children.Add(actions);
        void Section(string title, Control content)
        { detail.Children.Add(new TextBlock { Text = title, FontWeight = FontWeight.SemiBold }); detail.Children.Add(content); }
        Section("Signature", Signature); Section("Parameters", parameters); Section("Returns", returns);
        Section("Complete example", Example); Section("Limits and important behavior", limitations);
        detailHost = new ScrollViewer { Content = detail, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        body.Children.Add(Topics); body.Children.Add(splitter); body.Children.Add(detailHost); SetRow(body, 3); Children.Add(body);
        Status.Margin = new(0, 5, 0, 0); SetRow(Status, 4); Children.Add(Status);
        AutomationProperties.SetName(Search, "Search Script API Reference"); AutomationProperties.SetName(Category, "API category");
        AutomationProperties.SetName(Topics, "Script API topics"); AutomationProperties.SetName(Signature, "API signature");
        AutomationProperties.SetName(Example, "Complete script example");
        ToolTip.SetTip(CreateScript, "Start an unsaved My Scripts draft from this starter; existing dirty scripts use the normal save/discard/cancel prompt.");
        Back.Click += (_, _) => model.Back();
        CopySignature.Click += (_, _) => ClipboardTask = CopyAsync(false);
        CopyExample.Click += (_, _) => ClipboardTask = CopyAsync(true);
        CreateScript.Click += (_, _) => CreationTask = CreateAsync();
        Search.PropertyChanged += (_, e) => { if (!synchronizing && e.Property == TextBox.TextProperty) model.Query = Search.Text ?? ""; };
        Category.SelectionChanged += (_, _) => { if (!synchronizing && Category.SelectedItem is string value) model.Category = value; };
        Topics.SelectionChanged += (_, _) => { if (!synchronizing) model.Selected = Topics.SelectedItem as ScriptApiEntry; };
        AttachedToVisualTree += (_, _) => { model.PropertyChanged += Changed; model.Shown += FocusSearch; Refresh(); };
        DetachedFromVisualTree += (_, _) => { model.PropertyChanged -= Changed; model.Shown -= FocusSearch; };
        SizeChanged += (_, _) => ArrangePanes(); ArrangePanes(); Refresh();
    }
    private static SelectableTextBlock Prose() => new() { TextWrapping = TextWrapping.Wrap };
    private static TextBox Code()
    {
        var text = new TextBox { IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.NoWrap,
            FontFamily = new("Consolas, monospace"), MinHeight = 50, MaxHeight = 340 };
        ScrollViewer.SetHorizontalScrollBarVisibility(text, ScrollBarVisibility.Auto);
        ScrollViewer.SetVerticalScrollBarVisibility(text, ScrollBarVisibility.Auto);
        return text;
    }
    private void FocusSearch() { Search.Focus(); Search.SelectAll(); }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void ArrangePanes()
    {
        if (Bounds.Width >= 640)
        {
            body.ColumnDefinitions = new("210,5,*"); body.RowDefinitions = new("*");
            SetColumn(Topics, 0); SetRow(Topics, 0); SetColumn(splitter, 1); SetRow(splitter, 0); SetColumn(detailHost, 2); SetRow(detailHost, 0);
            splitter.ResizeDirection = GridResizeDirection.Columns;
        }
        else
        {
            body.ColumnDefinitions = new("*"); body.RowDefinitions = new("120,5,*");
            SetColumn(Topics, 0); SetRow(Topics, 0); SetColumn(splitter, 0); SetRow(splitter, 1); SetColumn(detailHost, 0); SetRow(detailHost, 2);
            splitter.ResizeDirection = GridResizeDirection.Rows;
        }
    }
    public async Task CopyAsync(bool example)
    {
        var text = example ? model.Selected?.Example : model.Selected?.Signature;
        if (string.IsNullOrEmpty(text) || TopLevel.GetTopLevel(this)?.Clipboard is not { } clipboard) return;
        try { await clipboard.SetTextAsync(text); Status.Text = example ? "Example copied. Paste into an editable script with Ctrl+V." : "Signature copied."; }
        catch (Exception) { Status.Text = "Clipboard unavailable. Select the example text to copy it manually."; }
    }
    private async Task<bool> CreateAsync()
    {
        var created = await model.CreateSelectedAsync();
        Status.Text = created ? "Starter opened in Script Editor. Save Script to keep your copy." : "Current script retained.";
        return created;
    }
    private void Refresh()
    {
        synchronizing = true;
        try
        {
            Search.Text = model.Query; Category.SelectedItem = model.Category;
            Topics.ItemsSource = model.Results; Topics.SelectedItem = model.Selected;
            var entry = model.Selected;
            if (renderedEntryId != entry?.Id) { detailHost.Offset = default; renderedEntryId = entry?.Id; }
            Title.Text = entry?.Title ?? "No matching API topics"; description.Text = entry?.Description ?? "Try a shorter search or another category.";
            Signature.Text = entry?.Signature ?? ""; Example.Text = entry?.Example ?? "";
            parameters.Text = entry?.Parameters ?? ""; returns.Text = entry?.Returns ?? ""; limitations.Text = entry?.Limitations ?? "";
            Back.IsEnabled = model.CanGoBack; CopySignature.IsEnabled = CopyExample.IsEnabled = entry is not null;
            CreateScript.IsVisible = model.CanCreate; Version.Text = model.VersionText;
            Status.Text = $"{model.Results.Count} topics · Examples use synthetic data. Run only trusted C#.";
        }
        finally { synchronizing = false; }
    }
}
