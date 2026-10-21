using System.Collections.ObjectModel;
using System.ComponentModel;

namespace HL7Workbench.Workspace;

public sealed class ExtractionColumnDraft : INotifyPropertyChanged
{
    private string label, path;
    private readonly Func<bool> editable;
    private readonly Action changed;
    public event PropertyChangedEventHandler? PropertyChanged;
    public string Label { get => label; set { if (!editable() || value == label || value.Length > 120) return; label = value; PropertyChanged?.Invoke(this, new(nameof(Label))); changed(); } }
    public string Path { get => path; set { if (!editable() || value == path || value.Length > 128) return; path = value; PropertyChanged?.Invoke(this, new(nameof(Path))); changed(); } }
    internal ExtractionColumnDraft(ExtractionColumn column, Func<bool> editable, Action changed)
    { label = column.Label; path = column.Path; this.editable = editable; this.changed = changed; }
}

/// <summary>Retained settings draft only, independent of disposable extraction results and Dock controls.</summary>
public sealed class ExtractionConfigurationSession : INotifyPropertyChanged
{
    private readonly ExtractionLibrary library;
    private readonly ObservableCollection<ExtractionColumnDraft> columns = [];
    private SavedExtraction entry = ExtractionLibrary.InterfaceOverview;
    private string name = "Interface Overview";
    private ExtractionMode mode; private bool decode = true, descending, numeric;
    private int filterColumn, sortColumn = -1;
    private ExtractionFilterOperator filterOperator;
    private long revision; private bool deciding;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ReadOnlyObservableCollection<ExtractionColumnDraft> Columns { get; }
    public IReadOnlyList<ExtractionLibraryItem> Items { get; private set; } = [];
    public string Status { get; private set; } = "Duplicate Interface Overview to customize it.";
    public bool IsBusy { get; private set; }
    public bool IsDirty { get; private set; }
    public bool IsBuiltIn => entry.BuiltIn;
    public bool CanEdit => !IsBuiltIn && !IsBusy;
    public bool CanDelete => !IsBuiltIn && entry.Revision.Length > 0;
    public Guid Id => entry.Id;
    public long ContentVersion => revision;
    public string Name { get => name; set { if (CanEdit && value != name && value.Length <= 120) { name = value; Edited(); } } }
    public ExtractionMode Mode { get => mode; set { if (CanEdit && mode != value) { mode = value; Edited(); } } }
    public bool DecodeEscapes { get => decode; set { if (CanEdit && decode != value) { decode = value; Edited(); } } }
    public int FilterColumn { get => filterColumn; set { if (CanEdit && filterColumn != value) { filterColumn = value; Edited(); } } }
    public ExtractionFilterOperator FilterOperator { get => filterOperator; set { if (CanEdit && filterOperator != value) { filterOperator = value; Edited(); } } }
    public int SortColumn { get => sortColumn; set { if (CanEdit && sortColumn != value) { sortColumn = value; Edited(); } } }
    public bool Descending { get => descending; set { if (CanEdit && descending != value) { descending = value; Edited(); } } }
    public bool NumericSort { get => numeric; set { if (CanEdit && numeric != value) { numeric = value; Edited(); } } }
    public ExtractionConfigurationSession(ExtractionLibrary library)
    { this.library = library; Columns = new(columns); Apply(entry); Refresh(); }
    public ExtractionConfiguration Snapshot() => new ExtractionConfiguration(columns.Select(c => new ExtractionColumn(c.Label, c.Path)).ToArray(),
        mode, decode, filterColumn, filterOperator, sortColumn, descending, numeric).Snapshot();
    public void AddColumn() { if (CanEdit && columns.Count < 32) { columns.Add(new(new("Column " + (columns.Count + 1), "PID-3"), () => CanEdit, Edited)); Edited(); } }
    public void RemoveColumn(int index)
    {
        if (!CanEdit || columns.Count <= 1 || index < 0 || index >= columns.Count) return;
        columns.RemoveAt(index); filterColumn = 0; sortColumn = -1; Edited();
    }
    public void MoveColumn(int index, int direction)
    {
        var next = index + Math.Sign(direction); if (!CanEdit || index < 0 || index >= columns.Count || next < 0 || next >= columns.Count) return;
        columns.Move(index, next); filterColumn = 0; sortColumn = -1; Edited();
    }
    public void Refresh()
    {
        try { var listing = library.List(); Items = listing.Items; if (listing.InvalidEntries > 0) Status = $"{listing.InvalidEntries} invalid configurations skipped; valid definitions and draft retained."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException) { Status = "Configuration library unavailable; draft retained."; }
        Changed();
    }
    public bool Duplicate()
    {
        if (IsBusy) return false;
        try
        {
            var configuration = Snapshot(); var copyName = name[..Math.Min(name.Length, 114)] + " copy";
            Apply(new(Guid.NewGuid(), copyName, configuration, "")); IsDirty = true; Status = "Editable copy created. Save to keep it."; Changed(); return true;
        }
        catch (Exception error) when (error is ArgumentException or FormatException) { Status = "Fix invalid columns before duplicating; draft retained."; Changed(); return false; }
    }
    public async Task<bool> NewAsync(Func<Task<UnsavedChangesDecision>> prompt)
    {
        if (IsBusy || !await ConfirmChangesAsync(prompt)) return false;
        Apply(new(Guid.NewGuid(), "New extraction", new([new("Patient Identifier", "PID-3")]), "")); IsDirty = true; Changed(); return true;
    }
    public async Task<bool> OpenAsync(Guid id, Func<Task<UnsavedChangesDecision>> prompt)
    {
        if (IsBusy) return false;
        SavedExtraction loaded;
        try { loaded = library.Open(id); } catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException) { Status = "Configuration could not be opened; draft retained."; Changed(); return false; }
        if (!await ConfirmChangesAsync(prompt)) return false;
        Apply(loaded); return true;
    }
    public async Task<bool> SaveAsync(CancellationToken token = default)
    {
        if (!CanEdit) return false;
        try
        {
            var pending = entry with { Name = name, Configuration = Snapshot() }; IsBusy = true; Changed();
            Apply(await library.SaveAsync(pending, token)); Status = "Configuration saved."; Refresh(); return true;
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or FormatException or InvalidOperationException)
        { Status = "Configuration not saved; draft retained. Check names, paths, limits or external changes."; Changed(); return false; }
        finally { IsBusy = false; Changed(); }
    }
    public async Task<bool> DeleteAsync(Func<Task<UnsavedChangesDecision>> prompt, Func<string, Task<bool>> confirm)
    {
        if (!CanDelete || IsBusy || !await ConfirmChangesAsync(prompt, discardImmediately: false)) return false;
        var version = revision;
        if (!await confirm(entry.Name) || version != revision) return false;
        try { IsBusy = true; Changed(); await library.DeleteAsync(entry); Apply(ExtractionLibrary.InterfaceOverview); Refresh(); return true; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or InvalidOperationException)
        { Status = "Configuration not deleted; current draft retained."; Changed(); return false; }
        finally { IsBusy = false; Changed(); }
    }
    public async Task<bool> ConfirmChangesAsync(Func<Task<UnsavedChangesDecision>> prompt, bool discardImmediately = true)
    {
        if (deciding || IsBusy) return false; if (!IsDirty) return true; deciding = true;
        try
        {
            var version = revision; var decision = await prompt(); if (version != revision) return false;
            if (decision == UnsavedChangesDecision.Save) return await SaveAsync();
            if (decision != UnsavedChangesDecision.Discard) return false;
            if (discardImmediately) Apply(entry.Revision.Length == 0 ? ExtractionLibrary.InterfaceOverview : entry); return true;
        }
        finally { deciding = false; }
    }
    private void Apply(SavedExtraction next)
    {
        entry = next; name = next.Name; var config = next.Configuration;
        mode = config.Mode; decode = config.DecodeEscapes; filterColumn = config.FilterColumn; filterOperator = config.FilterOperator;
        sortColumn = config.SortColumn; descending = config.Descending; numeric = config.NumericSort;
        columns.Clear(); foreach (var column in config.Columns) columns.Add(new(column, () => CanEdit, Edited));
        revision++; IsDirty = false; Status = IsBuiltIn ? "Built-in template. Duplicate to edit." : "Configuration ready."; Changed();
    }
    private void Edited() { revision++; IsDirty = true; Changed(); }
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
}
