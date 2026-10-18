using Avalonia.Controls;
using Avalonia.Input.Platform;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

public sealed partial class MessageInspectionView
{
    private long pasteGeneration;
    internal async Task PasteExactAsync(Func<Task<string?>>? readClipboard = null)
    {
        if (!subscribed || !Editor.IsEffectivelyVisible || document is not { CanEditText: true } || PresentationView is not null) return;
        var pasteRequest = ++pasteGeneration;
        var id = document.ActiveMessageId; var revision = document.ContentVersion;
        var start = Editor.SelectionStart; var length = Editor.SelectionLength; var caret = Editor.CaretOffset;
        try
        {
            var clipboard = TopLevel.GetTopLevel(Editor)?.Clipboard;
            var text = await (readClipboard is not null ? readClipboard() : clipboard is not null ? clipboard.TryGetTextAsync() : Task.FromResult<string?>(null));
            // Clipboard access is asynchronous. A detached editor must never write
            // into the message selected afterwards, or replace a newer draft/selection.
            if (string.IsNullOrEmpty(text) || pasteRequest != pasteGeneration || !subscribed || !Editor.IsEffectivelyVisible || !document.CanEditText || document.ActiveMessageId != id ||
                document.ContentVersion != revision || Editor.SelectionStart != start || Editor.SelectionLength != length || Editor.CaretOffset != caret) return;
            if (text.Length > MessagePresentation.MaximumEditorCharacters - (Editor.Document.TextLength - length))
            { WindowStatus.Text = "Paste refused: the complete-editor limit is 1 Mi characters. Existing work is retained."; return; }
            // The default Paste command normalizes newlines and optionally tabs.
            // Replace through the existing selection/transaction instead, preserving
            // every clipboard character and producing one Workspace history entry.
            using (Editor.Document.RunUpdate()) Editor.TextArea.Selection.ReplaceSelectionWithText(text);
            Editor.TextArea.Caret.BringCaretToView();
        }
        catch (Exception error) when (error is IOException or InvalidOperationException or NotSupportedException or System.Runtime.InteropServices.COMException)
        { WindowStatus.Text = "Paste unavailable. Existing work is retained; try copying the text again."; }
    }
}
