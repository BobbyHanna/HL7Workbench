using System.ComponentModel;
using System.Windows.Input;
using Avalonia.Controls;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Shell-owned schema commands; document state stays in Workspace and the existing library dialog.</summary>
internal sealed class SchemaValidationCommands : IDisposable
{
    private readonly DocumentManager documents;
    private readonly Func<Window> owner;
    private readonly Action<MessageDocument, int> navigate;
    private MessageDocument? observed;
    private bool disposed;
    internal SchemaSelectionDialog? Dialog { get; private set; }
    internal Task Pending { get; private set; } = Task.CompletedTask;
    internal event Action<string>? Feedback;
    internal Func<Avalonia.Platform.Storage.FilePickerOpenOptions, Task<IReadOnlyList<Avalonia.Platform.Storage.IStorageFile>>>? PickProfile { get; set; }
    internal IReadOnlyDictionary<string, ICommand> Actions { get; }
    internal SchemaValidationCommands(DocumentManager documents, Func<Window> owner, Action<MessageDocument, int> navigate)
    {
        this.documents = documents; this.owner = owner; this.navigate = navigate;
        Actions = new Dictionary<string, ICommand>
        {
            ["Apply/Change Schema"] = new UiCommand(() => OpenAsync(apply: true), () => CanApply && Dialog is null && !disposed),
            ["Remove Schema"] = new UiCommand(() => { documents.ActiveDocument?.RemoveSchema(); return Task.CompletedTask; },
                () => !disposed && documents.ActiveDocument?.AppliedSchemaId is not null),
            ["Import Schema"] = new UiCommand(() => OpenAsync(import: true), () => !disposed),
            ["Manage Schemas"] = new UiCommand(() => OpenAsync(), () => !disposed),
            ["Previous Issue"] = new UiCommand(() => Navigate(-1), () => HasIssues),
            ["Next Issue"] = new UiCommand(() => Navigate(1), () => HasIssues)
        };
        documents.PropertyChanged += Changed; ObserveActive();
    }
    private bool CanApply => documents.ActiveDocument is { IsLoading: false, IsRefreshingEdit: false, ActiveMessageId: not null } d &&
        (d.EditableText is not null || d.Source is not null);
    private bool HasIssues => !disposed && documents.ActiveDocument?.SchemaValidation?.Diagnostics.Count > 0;
    private Task Navigate(int direction)
    { if (documents.ActiveDocument is { } document) navigate(document, direction); return Task.CompletedTask; }
    private void Changed(object? sender, PropertyChangedEventArgs e) { ObserveActive(); Refresh(); }
    private void ObserveActive()
    {
        if (ReferenceEquals(observed, documents.ActiveDocument)) return;
        if (observed is not null) observed.PropertyChanged -= Changed;
        observed = documents.ActiveDocument;
        if (observed is not null) observed.PropertyChanged += Changed;
    }
    private void Refresh() { foreach (var command in Actions.Values.Cast<UiCommand>()) command.Refresh(); }
    private Task OpenAsync(bool apply = false, bool import = false)
    {
        if (disposed) return Task.CompletedTask;
        if (Dialog is { } existing) { existing.Activate(); return import ? existing.ImportAsync() : Task.CompletedTask; }
        return Pending = RunDialogAsync(apply, import);
    }
    private async Task RunDialogAsync(bool apply, bool import)
    {
        var target = apply ? documents.ActiveDocument : null;
        if (apply && !CanApply) return;
        var id = target?.ActiveMessageId; var revision = target?.ContentVersion;
        var dialog = new SchemaSelectionDialog(documents.ValidationProfiles, target);
        if (PickProfile is not null) dialog.PickProfile = PickProfile;
        Dialog = dialog; Refresh();
        try
        {
            var selection = dialog.ShowDialog<string?>(owner());
            if (import) { await dialog.Ready; await dialog.ImportAsync(); }
            var choice = await selection;
            // A modal selector can outlive a message switch, close, or document
            // activation from another Dock host. Never apply its stale choice.
            if (disposed || choice is null || target is null || !ReferenceEquals(target, documents.ActiveDocument) ||
                !documents.Documents.Contains(target) || target.ActiveMessageId != id || target.ContentVersion != revision)
            {
                if (!disposed && choice is not null) Feedback?.Invoke("The selected message changed. Apply/Change Schema again for the active message.");
                return;
            }
            try { await target.ApplySchemaAsync(choice); }
            catch (Exception e) when (e is ArgumentException or InvalidOperationException or ObjectDisposedException)
            { Feedback?.Invoke("Schema application is unavailable. Select a prepared message/current profile and retry."); }
        }
        finally { Dialog = null; Refresh(); }
    }
    public void Dispose()
    {
        disposed = true; documents.PropertyChanged -= Changed;
        if (observed is not null) observed.PropertyChanged -= Changed;
        observed = null; Dialog?.Close(); Refresh();
    }
}
