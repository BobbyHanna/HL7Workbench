using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Virtualized session output with conventional multiple selection and tail following.</summary>
public sealed class ActivityLogView : Grid
{
    private readonly ActivityLogViewModel model;
    private ScrollViewer? scroll;
    private bool following = true;
    private Vector savedOffset;
    private long? anchor;
    private double anchorY;
    private long revision;
    public ListBox Entries { get; } = new() { SelectionMode = SelectionMode.Multiple };
    public ComboBox Severity { get; } = new() { ItemsSource = new[] { "All severities", "Information", "Warning", "Error" }, SelectedIndex = 0, MinWidth = 150 };
    public Button Clear { get; } = new() { Content = "Clear Log", Margin = new(4, 0) };
    public Button Copy { get; } = new() { Content = "Copy Selected Entries" };
    public ActivityLogView(ActivityLogViewModel model)
    {
        this.model = model; RowDefinitions = new("Auto,*"); Margin = new(6);
        AutomationProperties.SetName(this, "Activity Log"); AutomationProperties.SetName(Entries, "Session activity entries");
        ToolTip.SetTip(Severity, "Show all entries or only the selected severity"); ToolTip.SetTip(Clear, "Clear session activity; retained document diagnostics are unchanged");
        ToolTip.SetTip(Copy, "Copy selected safe activity entries in chronological order");
        var bar = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Margin = new(0, 0, 0, 4) };
        bar.Children.Add(Severity); bar.Children.Add(Clear); bar.Children.Add(Copy); Children.Add(bar);
        Entries.ItemsSource = model.Entries;
        Entries.ItemTemplate = new FuncDataTemplate<ActivityEntry>((entry, _) => new TextBlock { Text = entry?.ToString() ?? "", Margin = new(2), FontFamily = new("Consolas, monospace") });
        Entries.ItemsPanel = new FuncTemplate<Panel?>(() => new VirtualizingStackPanel());
        ScrollViewer.SetHorizontalScrollBarVisibility(Entries, ScrollBarVisibility.Auto);
        SetRow(Entries, 1); Children.Add(Entries);
        Severity.SelectionChanged += (_, _) => model.Severity = Severity.SelectedIndex switch
        { 1 => Hl7DiagnosticSeverity.Information, 2 => Hl7DiagnosticSeverity.Warning, 3 => Hl7DiagnosticSeverity.Error, _ => null };
        Clear.Click += (_, _) => model.Clear(); Copy.Click += async (_, _) => await CopyAsync();
        Entries.KeyDown += async (_, e) =>
        {
            if (e.Key == Key.C && e.KeyModifiers.HasFlag(OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control))
            { e.Handled = true; await CopyAsync(); }
        };
    }
    public string SelectedText() => ActivityLogViewModel.CopyText(Entries.SelectedItems?.OfType<ActivityEntry>() ?? []);
    public async Task CopyAsync()
    { if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard && SelectedText() is { Length: > 0 } text) await clipboard.SetTextAsync(text); }
    private void Updating(object? sender, EventArgs e)
    {
        scroll ??= Entries.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        savedOffset = scroll?.Offset ?? default;
        following = scroll is null || scroll.Extent.Height - scroll.Viewport.Height - scroll.Offset.Y <= 2;
        // Keep the visible row anchored when retention removes older rows. Offsets
        // alone cannot preserve the same entry after eviction from a virtual list.
        var first = Entries.GetVisualDescendants().OfType<ListBoxItem>()
            .Select(item => (Item: item, Point: item.TranslatePoint(default, Entries)))
            .Where(p => p.Point is { Y: >= 0 }).OrderBy(p => p.Point!.Value.Y).FirstOrDefault();
        anchor = (first.Item?.DataContext as ActivityEntry)?.Sequence; anchorY = first.Point?.Y ?? 0;
    }
    private void Updated(object? sender, EventArgs e)
    {
        var version = ++revision;
        Dispatcher.UIThread.Post(() =>
        {
            if (version != revision || !this.IsAttachedToVisualTree()) return;
            if (following && model.Entries.LastOrDefault() is { } last) Entries.ScrollIntoView(last);
            else if (scroll is not null)
            {
                var target = anchor is { } id ? model.Entries.FirstOrDefault(entry => entry.Sequence == id) : null;
                if (target is not null)
                {
                    Entries.ScrollIntoView(target); Entries.UpdateLayout();
                    if (Entries.ContainerFromItem(target) is { } container && container.TranslatePoint(default, Entries) is { } point)
                        scroll.Offset = new(savedOffset.X, Math.Max(0, scroll.Offset.Y + point.Y - anchorY));
                    else scroll.Offset = savedOffset;
                }
                else scroll.Offset = savedOffset;
            }
        }, DispatcherPriority.Loaded);
    }
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e); model.Updating += Updating; model.Updated += Updated;
        following = true; Updated(this, EventArgs.Empty);
    }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        revision++; model.Updating -= Updating; model.Updated -= Updated; scroll = null;
        base.OnDetachedFromVisualTree(e);
    }
}
