using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using AvaloniaEdit;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop;

/// <summary>Routes document history once per shell input root, independently of replaceable views.</summary>
internal static class DocumentHistoryShortcuts
{
    // Register only when the shell creates a window, never when a document/editor attaches.
    // Navigation can detach the focused editor and leave the next key targeted at the root.
    internal static void Attach(Window window, Func<MessageDocument?> activeDocument) =>
        window.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            var modifiers = e.KeyModifiers & ~KeyModifiers.Shift;
            if (modifiers is not (KeyModifiers.Control or KeyModifiers.Meta) || e.Key is not (Key.Z or Key.Y)) return;
            var document = activeDocument();
            if (document is null || HasIndependentTextInput(e.Source as Visual, document)) return;
            // Consume even an empty/unavailable document stack, so AvaloniaEdit cannot
            // fall through to a separate local history. Do not move keyboard focus.
            e.Handled = true;
            if (e.Key == Key.Y || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) document.Redo();
            else document.Undo();
        }, RoutingStrategies.Tunnel);

    private static bool HasIndependentTextInput(Visual? target, MessageDocument document)
    {
        TextEditor? editor = null;
        if (target is null) return false;
        foreach (var ancestor in target.GetSelfAndVisualAncestors())
        {
            // A text input keeps its shortcuts even when its own undo stack is empty.
            // This includes Search, numeric navigation inputs and editor search boxes.
            if (ancestor is TextBox) return true;
            if (ancestor is TextEditor textEditor) editor = textEditor;
            if (ancestor is MessageInspectionView view && editor is not null)
                return !view.OwnsEditorHistory(editor, document);
        }
        return editor is not null;
    }
}
