using System.ComponentModel;

namespace HL7Workbench.Workspace;

/// <summary>Draft lifetime is independent of tool controls/layout. Dismissal and failed saves always retain the draft.</summary>
public sealed class ScriptEditorSession : INotifyPropertyChanged
{
    private readonly ScriptLibrary library;
    private LibraryScript entry = ScriptLibrary.Example;
    private LibraryScript saved = ScriptLibrary.Example;
    private long revision;
    private bool deciding;
    public event PropertyChangedEventHandler? PropertyChanged;
    public IReadOnlyList<ScriptLibraryItem> Items { get; private set; } = [];
    public string Status { get; private set; } = "Duplicate the bundled example to edit it.";
    public bool HasError { get; private set; }
    public string Name
    {
        get => entry.Name;
        set { if (entry.Bundled || value == entry.Name) return; entry = entry with { Name = value }; revision++; Changed(); }
    }
    public string Source
    {
        get => entry.Source;
        set { if (entry.Bundled || value == entry.Source) return; entry = entry with { Source = value }; revision++; Changed(); }
    }
    public Guid Id => entry.Id;
    public long ContentVersion => revision;
    public bool IsBundled => entry.Bundled;
    public bool IsDirty => entry.Revision.Length == 0 || entry.Name != saved.Name || entry.Source != saved.Source;
    public string DisplayName => Name + (IsDirty ? " *" : "");
    public ScriptEditorSession(ScriptLibrary library) { this.library = library; Refresh(); }
    public void Refresh()
    {
        try { Items = library.List(); HasError = false; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        { HasError = true; Status = "Script Library unavailable. Existing drafts are retained; check local storage and entry format."; }
        Changed();
    }
    public bool Save()
    {
        try
        {
            entry = library.Save(entry); saved = entry; Status = "Script saved."; Refresh(); Changed(); return true;
        }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { HasError = true; Status = "Script was not saved. Draft retained. Check local access or external changes; duplicate the draft to save another copy."; Changed(); return false; }
    }
    public async Task<bool> OpenAsync(Guid id, Func<Task<UnsavedChangesDecision>> prompt)
    {
        LibraryScript next;
        try { next = library.Open(id); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        { HasError = true; Status = "Script could not be reopened; current draft retained."; Changed(); return false; }
        if (!await ConfirmChangesAsync(prompt)) return false;
        Apply(next); return true;
    }
    public async Task<bool> NewAsync(Func<Task<UnsavedChangesDecision>> prompt)
    {
        if (!await ConfirmChangesAsync(prompt)) return false;
        Apply(new(Guid.NewGuid(), "New script", "", "")); return true;
    }
    /// <summary>Duplicate the actual draft, including unsaved content, without overwriting its saved original.</summary>
    public bool Duplicate()
    {
        try { var copy = library.Duplicate(entry); Apply(copy); Refresh(); return true; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { HasError = true; Status = "Unable to duplicate script. Current draft retained."; Changed(); return false; }
    }
    public async Task<bool> ConfirmChangesAsync(Func<Task<UnsavedChangesDecision>> prompt, bool discardImmediately = true)
    {
        if (!IsDirty) return true;
        if (deciding) return false;
        deciding = true;
        try
        {
            var version = revision;
            var choice = await prompt();
            if (revision != version) return false;
            if (choice == UnsavedChangesDecision.Save) return Save() && !IsDirty;
            if (choice != UnsavedChangesDecision.Discard) return false;
            if (discardImmediately) DiscardChanges();
            return true;
        }
        finally { deciding = false; }
    }
    public void DiscardChanges() => Apply(saved.Revision.Length == 0 ? ScriptLibrary.Example : saved);
    private void Apply(LibraryScript next)
    { entry = saved = next; revision++; HasError = false; Status = next.Bundled ? "Bundled example. Duplicate it to edit." : "Script ready."; Changed(); }
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
}
