using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Content-sized confirmations; dismissal always retains the safe cancel result.</summary>
internal static class ConfirmationDialog
{
    internal static Window Unsaved(string prompt, bool script = false) => Create(script ? "Unsaved script" : "Unsaved changes", prompt,
        ("Save", UnsavedChangesDecision.Save), ("Discard", UnsavedChangesDecision.Discard), ("Cancel", UnsavedChangesDecision.Cancel));
    internal static Window Delete(string name) => Create("Delete Script",
        $"Delete saved script ‘{name}’? This permanently removes its library record and discards any remaining draft. Generated HL7 documents and exported CSV files are kept.",
        ("Delete Script", true), ("Cancel", false));
    internal static Window Overwrite(string name) => Create("Replace existing file?", $"Replace the existing file ‘{name}’?",
        ("Replace", true), ("Cancel", false));
    private static Window Create<T>(string title, string prompt, params (string Label, T Result)[] choices)
    {
        var dialog = new Window { Title = title, Width = 420, MinWidth = 400, MaxWidth = 440, SizeToContent = SizeToContent.Height,
            CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        foreach (var (label, result) in choices)
        {
            var button = new Button { Content = label, IsCancel = label == "Cancel" };
            button.Click += (_, _) => dialog.Close(result); buttons.Children.Add(button);
        }
        var cancel = (Button)buttons.Children[^1];
        dialog.Opened += (_, _) => cancel.Focus();
        dialog.KeyDown += (_, e) => { if (e.Key == Key.Escape) { dialog.Close(choices[^1].Result); e.Handled = true; } };
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 16, Children =
            { new TextBlock { Text = prompt, TextWrapping = TextWrapping.Wrap }, buttons } };
        return dialog;
    }
}
