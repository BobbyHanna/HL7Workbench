using AvaloniaEdit;
using AvaloniaEdit.Document;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Session-only handoff when Dock replaces an editor view. Owns no controls or subscriptions.</summary>
internal sealed class ToolEditorState
{
    private TextDocument? document;
    private object? identity;
    private int start, length, caret;
    private bool closed;
    private WeakReference<TextEditor>? owner;
    internal void Capture(TextEditor editor, object key)
    {
        if (closed) return;
        // Dock can attach a replacement before detaching the previous template.
        // A late detach must never replace the new owner's current selection.
        if (owner is null || owner.TryGetTarget(out var current) && ReferenceEquals(current, editor))
        {
            document = editor.Document; identity = key; SaveSelection(editor); owner = null;
        }
        // The old editor must release document event handlers, otherwise retaining
        // its undo document would also retain a detached view and model listeners.
        editor.Document = new TextDocument();
    }
    internal void Restore(TextEditor editor, object key, string source)
    {
        if (closed) return;
        if (Equals(identity, key) && document is { } saved && saved.Text == source)
        {
            if (owner?.TryGetTarget(out var previous) == true && !ReferenceEquals(previous, editor)) SaveSelection(previous);
            editor.Document = saved;
            editor.CaretOffset = Math.Min(caret, saved.TextLength);
            editor.Select(Math.Min(start, saved.TextLength), Math.Min(length, saved.TextLength - Math.Min(start, saved.TextLength)));
        }
        else { document = editor.Document; identity = key; }
        owner = new(editor);
    }
    private void SaveSelection(TextEditor editor) { start = editor.SelectionStart; length = editor.SelectionLength; caret = editor.CaretOffset; }
    internal void Close() { closed = true; document = null; identity = null; owner = null; }
}
