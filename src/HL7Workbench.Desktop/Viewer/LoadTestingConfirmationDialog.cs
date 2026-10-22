using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Bounded scrollable review keeps consent controls reachable for the complete captured position list.</summary>
internal static class LoadTestingConfirmationDialog
{
    internal static Window Create(string title, string prompt)
    {
        var dialog = new Window { Title = title, Width = 640, Height = 620, MinWidth = 400, MinHeight = 300, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var confirm = new Button { Content = "Confirm authorization" }; var cancel = new Button { Content = "Cancel", IsCancel = true };
        confirm.Click += (_, _) => dialog.Close(true); cancel.Click += (_, _) => dialog.Close(false);
        var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8, Margin = new(0, 12, 0, 0), Children = { confirm, cancel } };
        var review = new ScrollViewer { Name = "LoadAuthorizationScroll", HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = new SelectableTextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap } };
        var body = new Grid { RowDefinitions = new("*,Auto"), Margin = new Thickness(16) }; body.Children.Add(review); Grid.SetRow(actions, 1); body.Children.Add(actions); dialog.Content = body;
        dialog.Opened += (_, _) => cancel.Focus();
        dialog.KeyDown += (_, e) => { if (e.Key == Key.Escape) { e.Handled = true; dialog.Close(false); } };
        return dialog;
    }
}
