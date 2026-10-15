using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using AvaloniaEdit;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Clipboard text is explicit and bounded, independently of logical structural selection.</summary>
public static class BoundedCopy
{
    public const int MaximumCharacters = 1024 * 1024;
    public static void Attach(TextEditor editor, Func<bool>? contiguous = null)
    {
        editor.TextArea.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) != 0 && e.Key is Key.C or Key.Insert &&
                (editor.SelectionLength > MaximumCharacters || contiguous?.Invoke() == false))
            { e.Handled = true; ShowLimit(editor, contiguous?.Invoke() == false); }
        }, RoutingStrategies.Tunnel);
        var copy = new MenuItem { Header = "Copy" };
        copy.Click += async (_, _) =>
        {
            if (editor.SelectionLength > MaximumCharacters || contiguous?.Invoke() == false) { ShowLimit(editor, contiguous?.Invoke() == false); return; }
            if (TopLevel.GetTopLevel(editor)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(editor.SelectedText);
        };
        var preview = new MenuItem { Header = "Copy Visible Preview" };
        preview.Click += async (_, _) =>
        {
            if (editor.SelectionLength > MaximumCharacters) { ShowLimit(editor); return; }
            if (TopLevel.GetTopLevel(editor)?.Clipboard is { } clipboard) await clipboard.SetTextAsync(editor.SelectedText);
        };
        editor.ContextMenu = new ContextMenu { ItemsSource = contiguous is null ? new[] { copy } : new[] { copy, preview } };
    }
    private static void ShowLimit(TextEditor editor, bool omitted = false)
    {
        ToolTip.SetTip(editor, omitted ? "Selection includes a display-only omission. Use Copy Visible Preview or export original source." : "Copy is limited to 1 Mi UTF-16 characters. Select a smaller interval.");
        ToolTip.SetIsOpen(editor, true);
    }
}
