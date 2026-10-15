using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    private bool shutdownApproved;
    private bool promptingShutdown;
    private readonly string preferencesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HL7Workbench", "editor-preferences.json");
    private void LoadEditPreferences()
    {
        try { documents.Preferences = EditorPreferences.Load(preferencesPath); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { Status.Text = "Editor preferences unavailable; new documents start read-only."; }
        StartInEditMode.IsChecked = documents.Preferences.StartInEditMode;
    }
    private void ChangeEditPreference(object? sender, RoutedEventArgs e)
    {
        var next = new EditorPreferences(StartInEditMode.IsChecked);
        try { next.Save(preferencesPath); documents.Preferences = next; Status.Text = "Edit Mode preference saved for newly opened documents."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        { StartInEditMode.IsChecked = documents.Preferences.StartInEditMode; Status.Text = "Unable to save editor preferences; previous preference retained."; }
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
            string? path = null;
            if (saveAs || document.SavePath is null)
            {
                if (!StorageProvider.CanSave) { Status.Text = "Local file saving is unavailable."; return false; }
                using var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                { Title = "Save HL7 document", SuggestedFileName = document.Title, ShowOverwritePrompt = true });
                if (file is null) return false;
                path = file.TryGetLocalPath() ?? throw new IOException("Local destination required.");
            }
            Status.Text = "Saving document…";
            await document.SaveAsync(path, lifetime.Token);
            Status.Text = "Document saved."; return true;
        }
        catch (OperationCanceledException) { Status.Text = "Save canceled; working text retained."; return false; }
        catch (InvalidOperationException error) { Status.Text = error.Message; return false; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Hl7LimitException or System.Text.EncoderFallbackException or ArgumentException or NotSupportedException)
        { Status.Text = "Save failed; destination and unsaved working text are retained. Check access, encoding, and edit limits, then try again."; return false; }
    }
    private Task<bool> ConfirmDocumentCloseAsync(MessageDocument document) =>
        UnsavedChanges.ConfirmAsync(document, PromptUnsavedAsync, d => SaveDocumentAsync(d, false));
    private async Task<UnsavedChangesDecision> PromptUnsavedAsync(MessageDocument document)
    {
        var dialog = new Window { Title = "Unsaved changes", Width = 470, Height = 175, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        foreach (var (label, decision) in new[] { ("Save", UnsavedChangesDecision.Save), ("Discard", UnsavedChangesDecision.Discard), ("Cancel", UnsavedChangesDecision.Cancel) })
        {
            var button = new Button { Content = label, IsCancel = decision == UnsavedChangesDecision.Cancel };
            button.Click += (_, _) => dialog.Close(decision); buttons.Children.Add(button);
        }
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 20, Children =
            { new TextBlock { Text = $"Save changes to {document.Title}?", TextWrapping = Avalonia.Media.TextWrapping.Wrap }, buttons } };
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
        var dialog = new Window { Title = "Unsaved script", Width = 470, Height = 175, CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Spacing = 8 };
        foreach (var (label, decision) in new[] { ("Save", UnsavedChangesDecision.Save), ("Discard", UnsavedChangesDecision.Discard), ("Cancel", UnsavedChangesDecision.Cancel) })
        {
            var button = new Button { Content = label, IsCancel = decision == UnsavedChangesDecision.Cancel };
            button.Click += (_, _) => dialog.Close(decision); buttons.Children.Add(button);
        }
        dialog.Content = new StackPanel { Margin = new Thickness(16), Spacing = 20, Children =
            { new TextBlock { Text = "Save changes to the script before continuing?", TextWrapping = Avalonia.Media.TextWrapping.Wrap }, buttons } };
        return await dialog.ShowDialog<UnsavedChangesDecision>(this);
    }
}
