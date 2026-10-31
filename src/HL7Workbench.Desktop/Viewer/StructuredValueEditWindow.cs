using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>One compact native raw-value draft; the shell owns and disposes its guarded Workspace session.</summary>
internal sealed class StructuredValueEditWindow : Window
{
    private readonly StructuredValueEditSession session;
    internal TextBox CurrentRaw { get; } = new() { IsReadOnly = true, AcceptsReturn = true, MaxHeight = 100, TextWrapping = TextWrapping.NoWrap };
    internal TextBox Replacement { get; } = new() { AcceptsReturn = true, MinHeight = 90, MaxLength = MessageDocument.MaximumEditableLineCharacters, TextWrapping = TextWrapping.NoWrap };
    internal TextBlock Feedback { get; } = new() { TextWrapping = TextWrapping.Wrap };
    internal Button ApplyButton { get; } = new() { Content = "Apply", IsDefault = true };
    internal Button CancelButton { get; } = new() { Content = "Cancel", IsCancel = true };
    internal StructuredValueEditWindow(StructuredValueEditSession session)
    {
        this.session = session;
        Title = "Edit Value — raw HL7"; Width = 620; Height = 480; MinWidth = 420; MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var path = new TextBlock { Text = session.Path.ToString(), FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap };
        var rules = new TextBlock { Text = "Edit raw encoded text. Escapes stay encoded. Fields may contain repetitions/components; repetitions may contain components; components may contain subcomponents. Separators above the selected level and all control characters are rejected.", TextWrapping = TextWrapping.Wrap };
        CurrentRaw.Text = Replacement.Text = session.OriginalValue;
        CurrentRaw.FontFamily = Replacement.FontFamily = new("Consolas, monospace");
        ScrollViewer.SetHorizontalScrollBarVisibility(CurrentRaw, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        ScrollViewer.SetHorizontalScrollBarVisibility(Replacement, Avalonia.Controls.Primitives.ScrollBarVisibility.Auto);
        AutomationProperties.SetName(CurrentRaw, "Current raw encoded HL7 value");
        AutomationProperties.SetName(Replacement, "Replacement raw encoded HL7 value");
        AutomationProperties.SetName(ApplyButton, "Apply one structured HL7 edit");
        var current = new StackPanel { Spacing = 4, Children = { new TextBlock { Text = "Current raw encoded value" }, CurrentRaw } };
        var replacement = new Grid { RowDefinitions = new("Auto,*"), RowSpacing = 4 };
        replacement.Children.Add(new TextBlock { Text = "Replacement (raw encoded)" }); Grid.SetRow(Replacement, 1); replacement.Children.Add(Replacement);
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Children = { ApplyButton, CancelButton } };
        var root = new Grid { Margin = new(16), RowDefinitions = new("Auto,Auto,Auto,*,Auto,Auto"), RowSpacing = 10 };
        foreach (var (control, row) in new (Control, int)[] { (path, 0), (rules, 1), (current, 2), (replacement, 3), (Feedback, 4), (buttons, 5) })
        { Grid.SetRow(control, row); root.Children.Add(control); }
        Content = root;
        Replacement.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Refresh(); };
        session.PropertyChanged += SessionChanged;
        ApplyButton.Click += (_, _) => { if (session.TryApply(Replacement.Text ?? "")) Close(true); else Refresh(); };
        CancelButton.Click += (_, _) => Close(false);
        Opened += (_, _) => { Replacement.Focus(); Replacement.SelectAll(); };
        Closed += (_, _) => { session.PropertyChanged -= SessionChanged; CurrentRaw.Text = Replacement.Text = ""; };
        Refresh();
    }
    private void SessionChanged(object? sender, PropertyChangedEventArgs e) => Refresh();
    private void Refresh()
    {
        Feedback.Text = session.Status;
        ApplyButton.IsEnabled = session.CanApply && Replacement.Text != session.OriginalValue;
    }
}
