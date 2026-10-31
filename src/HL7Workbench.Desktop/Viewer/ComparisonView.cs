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
using AvaloniaEdit;
using AvaloniaEdit.Document;
using AvaloniaEdit.Rendering;
using HL7Workbench.Core;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Shared result/selection with separate summary and source presentations. View changes never recapture inputs.</summary>
public sealed class ComparisonView : Grid
{
    private readonly ComparisonViewModel model;
    private bool updating, attached;
    private long readVersion;
    private MessageComparison? displayed;
    private Hl7Difference? displayedSelection;
    private readonly TextBlock status = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock count = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 230, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock copyStatus = new() { TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock emptyText = new() { TextWrapping = TextWrapping.Wrap, MaxWidth = 470, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Grid differencesRegion = new() { RowDefinitions = new("*,Auto") };
    private readonly Grid sourcesRegion = new() { ColumnDefinitions = new("*,6,*") };
    private readonly StackPanel emptyState = new() { Spacing = 12, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Margin = new(16) };
    private readonly ScrollViewer emptyScroll;
    private readonly Button copyButton = new() { Content = "Copy ▾", Margin = new(0, 0, 5, 4) };
    private readonly TextBlock[] scopes = [new(), new()];
    private readonly TextBlock[] labels = [new(), new()];
    private readonly WrapPanel[] paging = [new(), new()];
    private readonly ComparisonWindow?[] windows = new ComparisonWindow?[2];
    private readonly CheckBox[] ignores = [new() { Content = "Ignore MSH-7 timestamp" }, new() { Content = "Ignore MSH-10 control ID" },
        new() { Content = "Ignore trailing empty fields" }, new() { Content = "Ignore segment terminators" }];
    private readonly HashSet<Hl7Difference> groupStarts = [];
    private readonly Expander details = new() { Header = "Selected difference details", HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly TextBlock detailText = new() { TextWrapping = TextWrapping.Wrap };
    public ComparisonSourcePicker SourceA { get; }
    public ComparisonSourcePicker SourceB { get; }
    public TextEditor BeforeEditor { get; } = MessageViewer.CreateEditor("");
    public TextEditor AfterEditor { get; } = MessageViewer.CreateEditor("");
    public ListBox Navigator { get; } = new() { AutoScrollToSelectedItem = true, ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel()) };
    public Button Compare { get; } = new() { Content = "Compare / Refresh" };
    public Button Previous { get; } = new() { Content = "Previous" };
    public Button Next { get; } = new() { Content = "Next" };
    public ToggleButton DifferencesMode { get; } = new() { Content = "Differences" };
    public ToggleButton SideBySideMode { get; } = new() { Content = "Side-by-Side" };
    public Task Loading { get; private set; } = Task.CompletedTask;
    public Task ClipboardTask { get; private set; } = Task.CompletedTask;
    public Task DetailLoading { get; private set; } = Task.CompletedTask;

    public ComparisonView(ComparisonViewModel model)
    {
        this.model = model; Margin = new(8); RowDefinitions = new("Auto,Auto,*,Auto"); RowSpacing = 6;
        SourceA = new(model, true); SourceB = new(model, false);
        var sourceBar = new Grid { ColumnDefinitions = new("*,8,*") };
        sourceBar.Children.Add(SourceA); SetColumn(SourceB, 2); sourceBar.Children.Add(SourceB); Children.Add(sourceBar);
        var toolbar = new AdaptiveActionBar();
        Compare.Click += (_, _) => model.Start(); Previous.Click += (_, _) => model.Move(-1); Next.Click += (_, _) => model.Move(1);
        DifferencesMode.Click += (_, _) => { model.Presentation = ComparisonPresentation.Differences; Refresh(); };
        SideBySideMode.Click += (_, _) => { model.Presentation = ComparisonPresentation.SideBySide; Refresh(); };
        ToolTip.SetTip(Previous, "Previous difference (Shift+F7)"); ToolTip.SetTip(Next, "Next difference (F7)");
        ToolTip.SetTip(DifferencesMode, "Review grouped changes without the source editors"); ToolTip.SetTip(SideBySideMode, "Show both sources and navigate the selected change");
        foreach (var control in new Control[] { Compare, DifferencesMode, SideBySideMode, Previous, Next, count })
        { control.Margin = new(0, 0, 5, 4); toolbar.Children.Add(control); }
        var options = new Button { Content = "Options ▾", Margin = new(0, 0, 5, 4) };
        var optionPanel = new StackPanel { Spacing = 6, Margin = new(12) };
        foreach (var check in ignores)
        {
            optionPanel.Children.Add(check);
            check.IsCheckedChanged += (_, _) =>
            { if (!updating) model.SetOptions(new(ignores[0].IsChecked == true, ignores[1].IsChecked == true, ignores[2].IsChecked == true, ignores[3].IsChecked == true)); };
        }
        optionPanel.Children.Add(ActionButton("Compare active with original", () => model.CompareWithOriginal()));
        optionPanel.Children.Add(ActionButton("Cancel comparison", model.Cancel));
        options.Flyout = new Flyout { Content = optionPanel }; toolbar.Children.Add(options);
        var copyMenu = new ContextMenu { ItemsSource = CopyItems() };
        copyButton.Click += (_, _) => copyMenu.Open(copyButton); toolbar.Children.Add(copyButton);
        SetRow(toolbar, 1); Children.Add(toolbar);
        Navigator.Classes.Add("workbench-surface");
        ScrollViewer.SetHorizontalScrollBarVisibility(Navigator, ScrollBarVisibility.Disabled);
        Navigator.ItemTemplate = new FuncDataTemplate<Hl7Difference>((d, _) => DifferenceRow(d));
        Navigator.SelectionChanged += (_, _) => { if (!updating) model.Select(Navigator.SelectedItem as Hl7Difference); };
        Navigator.ContextMenu = new ContextMenu { ItemsSource = CopyItems() };
        var summary = new Grid(); summary.Children.Add(Navigator);
        emptyState.Children.Add(new TextBlock { Text = "Compare HL7 messages", [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.21"), FontWeight = FontWeight.SemiBold, HorizontalAlignment = HorizontalAlignment.Center });
        emptyState.Children.Add(emptyText);
        emptyState.Children.Add(new WrapPanel { HorizontalAlignment = HorizontalAlignment.Center, Children =
        { ActionButton("Paste HL7 into A", () => ClipboardTask = SourceA.PasteAsync()), ActionButton("Paste HL7 into B", () => ClipboardTask = SourceB.PasteAsync()) } });
        emptyState.Children.Add(new TextBlock { Text = "Or use A and B above to choose Open Message, Original Message, or edit temporary Pasted Text.", MaxWidth = 470, TextWrapping = TextWrapping.Wrap });
        emptyScroll = new ScrollViewer { Content = emptyState, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        summary.Children.Add(emptyScroll); differencesRegion.Children.Add(summary);
        details.Content = new ScrollViewer { Content = detailText, MaxHeight = 150, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
        details.PropertyChanged += (_, e) => { if (e.Property == Expander.IsExpandedProperty && details.IsExpanded) DetailLoading = LoadDetailsAsync(); };
        SetRow(details, 1); differencesRegion.Children.Add(details); SetRow(differencesRegion, 2); Children.Add(differencesRegion);
        BeforeEditor.Document.UndoStack.SizeLimit = AfterEditor.Document.UndoStack.SizeLimit = 0;
        for (var i = 0; i < 2; i++)
        {
            var side = i; var editor = i == 0 ? BeforeEditor : AfterEditor;
            var panel = new Grid { RowDefinitions = new("Auto,*,Auto") };
            labels[i].TextTrimming = TextTrimming.CharacterEllipsis; labels[i].Margin = new(0, 2, 0, 6); panel.Children.Add(labels[i]);
            SetRow(editor, 1); panel.Children.Add(editor);
            paging[i].Children.Add(ActionButton("Earlier source", () => Page(side, -1))); paging[i].Children.Add(ActionButton("Later source", () => Page(side, 1))); paging[i].Children.Add(scopes[i]);
            SetRow(paging[i], 2); panel.Children.Add(paging[i]); SetColumn(panel, i * 2); sourcesRegion.Children.Add(panel);
            ComparisonClipboard.Attach(editor, model, i == 0, task => ClipboardTask = task);
            editor.TextChanged += (_, _) =>
            { if (!updating && (side == 0 ? model.InputA : model.InputB).Kind == ComparisonSourceKind.PastedText) model.SetPastedText(side == 0, editor.Text); };
            AutomationProperties.SetName(editor, i == 0 ? "Message A source editor" : "Message B source editor");
        }
        var splitter = new GridSplitter { ResizeDirection = GridResizeDirection.Columns, HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch, Focusable = true, KeyboardIncrement = 10 };
        splitter.Classes.Add("workbench-splitter"); AutomationProperties.SetName(splitter, "Resize comparison sources");
        SetColumn(splitter, 1); sourcesRegion.Children.Add(splitter); SetRow(sourcesRegion, 2); Children.Add(sourcesRegion);
        var footer = new StackPanel { Children = { status, copyStatus } }; SetRow(footer, 3); Children.Add(footer);
        KeyDown += (_, e) => { if (e.Key == Key.F7) { model.Move(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1); e.Handled = true; } };
        AttachedToVisualTree += (_, _) => { attached = true; model.PropertyChanged += Changed; Refresh(); };
        DetachedFromVisualTree += (_, _) =>
        {
            attached = false; readVersion++; model.PropertyChanged -= Changed; displayed = null; displayedSelection = null;
            updating = true;
            try { Navigator.ItemsSource = null; BeforeEditor.Text = AfterEditor.Text = ""; BeforeEditor.TextArea.TextView.LineTransformers.Clear(); AfterEditor.TextArea.TextView.LineTransformers.Clear(); Array.Clear(windows); }
            finally { updating = false; }
        };
        Refresh();
    }
    private static Button ActionButton(string label, Action action)
    { var button = new Button { Content = label, Margin = new(0, 2, 5, 2) }; button.Click += (_, _) => action(); return button; }
    private MenuItem[] CopyItems() => new[] { "HL7 Path", "Before Value", "After Value", "Difference Summary" }.Select(kind =>
    { var item = new MenuItem { Header = "Copy " + kind }; item.Click += async (_, _) => await CopyAsync(kind); return item; }).ToArray();
    private static string Value(string? value, bool abbreviated) => value is null ? "(missing)" : value.Length == 0 ? "(empty)" :
        value.Replace("\r", "␍").Replace("\n", "␊") + (abbreviated ? " … [preview]" : "");
    private static string GroupKey(Hl7Difference d) => $"{d.Before?.Segment}[{d.Before?.Occurrence}] → {d.After?.Segment}[{d.After?.Occurrence}]";
    private static string PathDisplay(Hl7Difference d) => d.Path + (d.Before is not null && d.After is not null && d.Before.Path != d.After.Path ? $" → {d.After.Path}" : "");
    private Control DifferenceRow(Hl7Difference? d)
    {
        if (d is null) return new TextBlock();
        var row = new StackPanel { Spacing = 5, Margin = new(6, 5) };
        if (groupStarts.Contains(d))
        {
            var l = d.Before ?? d.After!; var r = d.After;
            row.Children.Add(new TextBlock { Text = $"{l.Segment} · occurrence {l.Occurrence}" +
                (r is not null && r.Occurrence != l.Occurrence ? $" → {r.Occurrence}" : ""), FontWeight = FontWeight.SemiBold, Margin = new(0, 4, 0, 3) });
        }
        var location = d.Before ?? d.After!;
        var description = d.Kind switch
        {
            Hl7DifferenceKind.Terminator => "Segment terminator spelling",
            Hl7DifferenceKind.DelimiterDefinition => "MSH delimiter definition",
            Hl7DifferenceKind.SourceSpelling => "Equivalent decoded value; source spelling differs",
            _ => location.Subcomponent is not null ? "Subcomponent value" : location.Component is not null ? "Component value" :
                location.Repetition is not null ? "Repetition value" : location.Field is not null ? "Field value" : "Segment source"
        };
        row.Children.Add(new TextBlock { Text = $"{d.Kind} · {PathDisplay(d)}", FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        row.Children.Add(new TextBlock { Text = description, [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.11") });
        var values = new Grid { ColumnDefinitions = new("*,*"), ColumnSpacing = 12 };
        var before = new TextBlock { Text = "Before: " + Value(d.BeforeValue, d.BeforeAbbreviated), TextWrapping = TextWrapping.Wrap, MaxHeight = 100 };
        var after = new TextBlock { Text = "After: " + Value(d.AfterValue, d.AfterAbbreviated), TextWrapping = TextWrapping.Wrap, MaxHeight = 100 };
        values.Children.Add(before); SetColumn(after, 1); values.Children.Add(after); row.Children.Add(values);
        if (d.BeforeAbbreviated || d.AfterAbbreviated)
            row.Children.Add(new TextBlock { Text = "Preview · expand selected details or use Copy for the complete value.", [!Avalonia.Controls.Documents.TextElement.FontSizeProperty] = new Avalonia.Markup.Xaml.MarkupExtensions.DynamicResourceExtension("ContentFont.11"), TextWrapping = TextWrapping.Wrap });
        return row;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        updating = true;
        try
        {
            var isSummary = model.Presentation == ComparisonPresentation.Differences;
            differencesRegion.IsVisible = isSummary; sourcesRegion.IsVisible = !isSummary;
            status.IsVisible = !isSummary || model.Differences.Count > 0;
            DifferencesMode.IsChecked = isSummary; SideBySideMode.IsChecked = !isSummary;
            status.Text = model.Status; emptyText.Text = model.Status; count.Text = model.Count;
            Compare.IsEnabled = model.Before is not null && model.After is not null;
            Next.IsEnabled = Previous.IsEnabled = model.Differences.Count > 0;
            copyButton.IsEnabled = model.Selected is not null;
            var o = model.Options; var checks = new[] { o.IgnoreMsh7, o.IgnoreMsh10, o.IgnoreTrailingEmptyFields, o.IgnoreTerminators };
            for (var i = 0; i < 4; i++) ignores[i].IsChecked = checks[i];
            if (!ReferenceEquals(displayed, model.Comparison))
            {
                groupStarts.Clear(); string? previous = null;
                foreach (var d in model.Differences) { var key = GroupKey(d); if (key != previous) groupStarts.Add(d); previous = key; }
            }
            Navigator.ItemsSource = model.Differences; Navigator.SelectedItem = model.Selected;
            Navigator.IsVisible = model.Differences.Count > 0; emptyScroll.IsVisible = !Navigator.IsVisible;
            details.IsVisible = model.Selected is not null;
            var inputs = new[] { model.InputA, model.InputB };
            for (var i = 0; i < 2; i++)
            {
                labels[i].Text = $"{(i == 0 ? "A" : "B")} · {inputs[i].Label}"; ToolTip.SetTip(labels[i], labels[i].Text);
                var editor = i == 0 ? BeforeEditor : AfterEditor;
                editor.IsReadOnly = inputs[i].Kind != ComparisonSourceKind.PastedText;
                foreach (var button in paging[i].Children.OfType<Button>()) button.IsVisible = editor.IsReadOnly;
                if (!editor.IsReadOnly && editor.Text != inputs[i].PastedText) editor.Text = inputs[i].PastedText;
            }
        }
        finally { updating = false; }
        // Presentation and theme changes reuse the loaded panes and shared selection.
        if (!ReferenceEquals(displayed, model.Comparison) || !ReferenceEquals(displayedSelection, model.Selected))
        {
            displayed = model.Comparison; displayedSelection = model.Selected; copyStatus.Text = "";
            Loading = LoadBothAsync(); if (details.IsExpanded) DetailLoading = LoadDetailsAsync();
        }
    }
    private async Task LoadBothAsync()
    {
        var version = ++readVersion; var comparison = model.Comparison; var selected = model.Selected;
        if (comparison is null)
        {
            updating = true;
            try
            {
                for (var i = 0; i < 2; i++)
                {
                    var editor = i == 0 ? BeforeEditor : AfterEditor; editor.TextArea.TextView.LineTransformers.Clear();
                    if (editor.IsReadOnly) editor.Text = "";
                    windows[i] = null; scopes[i].Text = editor.IsReadOnly ? "" : "Editable · temporary";
                }
            }
            finally { updating = false; }
            return;
        }
        try
        {
            for (var i = 0; i < 2; i++)
            {
                var location = i == 0 ? selected?.Before : selected?.After;
                var other = i == 0 ? selected?.After : selected?.Before;
                var structure = i == 0 ? comparison.BeforeStructure : comparison.AfterStructure;
                CharacterRange? target = location?.Range;
                if (target is null && other is not null)
                {
                    var segments = structure.Segments.Where(s => s.Identifier == other.Segment).ToArray();
                    if (segments.Length > 0) target = segments[Math.Min(other.Occurrence - 1, segments.Length - 1)].Range;
                }
                var input = i == 0 ? model.InputA : model.InputB;
                var window = input.Kind == ComparisonSourceKind.PastedText ?
                    new ComparisonWindow(input.PastedText, 0, input.PastedText.Length, target) :
                    await comparison.WindowAsync(i == 0, target);
                if (!attached || version != readVersion) return;
                ApplyWindow(i, window, comparison, location?.Range);
            }
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or OperationCanceledException)
        { if (attached && version == readVersion) copyStatus.Text = "Source window unavailable. Compare again."; }
    }
    private void ApplyWindow(int side, ComparisonWindow window, MessageComparison comparison, CharacterRange? selection)
    {
        windows[side] = window;
        var editor = side == 0 ? BeforeEditor : AfterEditor;
        scopes[side].Text = editor.IsReadOnly ? window.Scope : "Editable · temporary";
        editor.TextArea.TextView.LineTransformers.Clear();
        editor.TextArea.TextView.LineTransformers.Add(new SourceColorizer(side == 0 ? comparison.BeforeStructure : comparison.AfterStructure, () => window.Origin, () => Hl7EditorTheme.PaletteFor(editor)));
        editor.TextArea.TextView.LineTransformers.Add(new DifferenceColorizer(model.Differences, side == 0, window.Origin, () => Hl7EditorTheme.PaletteFor(editor), model.Selected));
        updating = true;
        try { if (editor.Text != window.Text) editor.Text = window.Text; }
        finally { updating = false; }
        // Background updates may redraw syntax, but must not move the caret or steal a typing selection.
        if (!editor.IsReadOnly && editor.IsKeyboardFocusWithin) { editor.TextArea.TextView.Redraw(); return; }
        editor.Select(0, 0);
        if (selection is { } range && range.End >= window.Origin && range.Offset <= window.Origin + window.Text.Length)
        {
            var start = (int)Math.Clamp(range.Offset - window.Origin, 0, window.Text.Length);
            var end = (int)Math.Clamp(range.End - window.Origin, start, window.Text.Length);
            editor.Select(start, end - start); editor.ScrollToLine(editor.Document.GetLineByOffset(start).LineNumber);
        }
        else editor.ScrollToLine(1);
        editor.TextArea.TextView.Redraw();
    }
    private async void Page(int side, int direction)
    {
        var editor = side == 0 ? BeforeEditor : AfterEditor;
        if (!editor.IsReadOnly || model.Comparison is not { } comparison || windows[side] is not { } window) return;
        var version = ++readVersion;
        try
        {
            var next = await comparison.WindowAsync(side == 0, origin: window.Origin + direction * (MessageComparison.WindowCharacters - 1024));
            if (attached && version == readVersion && ReferenceEquals(comparison, model.Comparison)) ApplyWindow(side, next, comparison, side == 0 ? model.Selected?.Before?.Range : model.Selected?.After?.Range);
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or OperationCanceledException) { }
    }
    private async Task LoadDetailsAsync()
    {
        var comparison = model.Comparison; var selected = model.Selected;
        if (comparison is null || selected is null) { detailText.Text = ""; return; }
        try
        {
            var before = selected.Before is { } l ? await comparison.PreviewValueAsync(true, l.Range) : "(missing)";
            var after = selected.After is { } r ? await comparison.PreviewValueAsync(false, r.Range) : "(missing)";
            if (attached && ReferenceEquals(comparison, model.Comparison) && selected == model.Selected)
                detailText.Text = $"{selected.Summary}\nBefore: {before}\nAfter: {after}\nDetails show at most 4,096 characters per value. Copy reads the complete value within its limit.";
        }
        catch (Exception e) when (e is IOException or InvalidOperationException or OperationCanceledException) { }
    }
    public async Task CopyAsync(string kind)
    {
        if (model.Comparison is not { IsCurrent: true } comparison || model.Selected is not { } selected) return;
        try
        {
            var text = kind == "HL7 Path" ? PathDisplay(selected) : "";
            if (kind is "Before Value" or "Difference Summary") text = selected.Before is { } left ? await comparison.ValueAsync(true, left.Range) : "";
            if (kind == "After Value") text = selected.After is { } right ? await comparison.ValueAsync(false, right.Range) : "";
            if (kind == "Difference Summary") text = $"{selected.Summary}\nBefore: {(selected.Before is null ? "(missing)" : text)}\nAfter: {(selected.After is { } after ? await comparison.ValueAsync(false, after.Range) : "(missing)")}";
            if (!comparison.IsCurrent || !ReferenceEquals(comparison, model.Comparison) || selected != model.Selected) return;
            if (text.Length > BoundedCopy.MaximumCharacters) throw new Hl7LimitException("ComparisonCopyCharacters");
            if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard) { await clipboard.SetTextAsync(text); copyStatus.Text = "Copied " + kind + "."; }
        }
        catch (Hl7LimitException) { copyStatus.Text = "Copy is limited to 1 Mi UTF-16 characters. The preview was not copied."; }
        catch (Exception e) when (e is IOException or InvalidOperationException or OperationCanceledException) { copyStatus.Text = "Copy unavailable; compare again."; }
    }
}

internal sealed class DifferenceColorizer(IReadOnlyList<Hl7Difference> differences, bool before, long origin,
    Func<Hl7EditorPalette> palette, Hl7Difference? selected) : DocumentColorizingTransformer
{
    protected override void ColorizeLine(DocumentLine line)
    {
        foreach (var d in differences)
        {
            if ((before ? d.Before : d.After)?.Range is not { } range) continue;
            var start = Math.Max(line.Offset, range.Offset - origin); var end = Math.Min(line.EndOffset, range.End - origin);
            if (start >= end) continue;
            var colors = palette();
            var brush = ReferenceEquals(d, selected) ? colors.DifferenceSelected : d.Kind switch
            { Hl7DifferenceKind.Added => colors.DifferenceAdded, Hl7DifferenceKind.Removed => colors.DifferenceRemoved, _ => colors.DifferenceModified };
            ChangeLinePart((int)start, (int)end, element => element.TextRunProperties.SetBackgroundBrush(brush));
        }
    }
}
