using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using HL7Workbench.Desktop.Viewer;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    private bool shutdownApproved;
    private bool promptingShutdown;
    private DocumentSaveWorkflow? documentSaves;
    private void AttachDocumentHistoryShortcut(Window window) =>
        DocumentHistoryShortcuts.Attach(window, () => documents.ActiveDocument);
    private void LoadEditPreferences()
    {
        documents.Preferences = preferences.Current;
        if (preferences.Status.Length > 0) Status.Text = preferences.Status;
    }
    private async void SaveDocument(object? sender, RoutedEventArgs e)
    { if (documents.ActiveDocument is { } document) await SaveDocumentAsync(document, false); }
    private async void SaveDocumentAs(object? sender, RoutedEventArgs e)
    { if (documents.ActiveDocument is { } document) await SaveDocumentAsync(document, true); }
    private async Task<bool> SaveDocumentAsync(MessageDocument document, bool saveAs)
    {
        if (document.IsSaving) return false;
        try
        {
            Status.Text = "Saving document…";
            var saved = await (documentSaves ??= new(documents)).SaveAsync(document, saveAs,
                () => SaveFilePicker.ChooseAsync(this, SaveFilePicker.Hl7Options(document), lifetime.Token), lifetime.Token);
            if (!saved) { Status.Text = "Save canceled; working text retained."; return false; }
            Status.Text = "Document saved."; return true;
        }
        catch (OperationCanceledException) { Status.Text = "Save canceled; working text retained."; return false; }
        catch (InvalidOperationException error) { Status.Text = error.Message; return false; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Hl7LimitException or System.Text.EncoderFallbackException or ArgumentException or NotSupportedException)
        { Status.Text = "Save failed; working text and history are retained. Use Save As if the destination changed externally, or check access, encoding and edit limits."; return false; }
    }
    private Task<bool> ConfirmDocumentCloseAsync(MessageDocument document) =>
        UnsavedChanges.ConfirmAsync(document, PromptUnsavedAsync, d => SaveDocumentAsync(d, false));
    private async Task<UnsavedChangesDecision> PromptUnsavedAsync(MessageDocument document)
    {
        var dialog = ConfirmationDialog.Unsaved($"Save changes to {document.Title}?");
        return await dialog.ShowDialog<UnsavedChangesDecision>(this);
    }
    private async void ConfirmWindowClose(object? sender, WindowClosingEventArgs e)
    {
        if (shutdownApproved) { SaveLayout(this, new()); return; }
        if (documents.Documents.Any(d => d.IsSaving)) { e.Cancel = true; Status.Text = "Wait for saving to finish before closing."; return; }
        if (!documents.Documents.Any(d => d.IsDirty) && !scripts.Session.IsDirty && !scripts.IsBusy) { SaveLayout(this, new()); return; }
        e.Cancel = true;
        if (promptingShutdown) return;
        promptingShutdown = true;
        try
        {
            // Discard consent is consumed only once all document close prompts succeed.
            // Canceling a later HL7 prompt must not silently remove the script draft.
            if (!await scripts.ConfirmCloseAsync(PromptUnsavedScriptAsync, discardImmediately: false)) return;
            var scriptVersion = scripts.Session.ContentVersion;
            var revisions = new Dictionary<MessageDocument, long>();
            foreach (var document in documents.Documents.ToArray())
            {
                if (!await ConfirmDocumentCloseAsync(document)) return;
                revisions[document] = document.ContentVersion;
            }
            if (scripts.Session.ContentVersion != scriptVersion || scripts.IsBusy ||
                documents.Documents.Any(d => !revisions.TryGetValue(d, out var version) || version != d.ContentVersion || d.IsSaving)) return;
            if (scripts.Session.IsDirty) scripts.Session.DiscardChanges();
            shutdownApproved = true; Close();
        }
        finally { promptingShutdown = false; }
    }

    private async Task<UnsavedChangesDecision> PromptUnsavedScriptAsync()
    {
        var dialog = ConfirmationDialog.Unsaved("Save changes to the script before continuing?", script: true);
        return await dialog.ShowDialog<UnsavedChangesDecision>(this);
    }
}
