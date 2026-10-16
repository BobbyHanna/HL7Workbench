using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

public sealed partial class SearchViewModel
{
    private readonly SearchLibrary? library;
    private readonly CancellationTokenSource libraryLifetime = new();
    private SavedSearch? loadedSearch;
    private Guid? selectedSearch;
    private string searchName = "";
    private long listingGeneration;
    public bool HasLibrary => library is not null;
    public bool IsLibraryBusy { get; private set; }
    public IReadOnlyList<SavedSearchItem> SavedSearches { get; private set; } = [];
    public Guid? SelectedSearchId { get => selectedSearch; set { selectedSearch = value; Notify(); } }
    public string SearchName { get => searchName; set { searchName = value; Notify(); } }
    public string? NameValidation => searchName.Length > 0 && !SearchLibrary.ValidName(searchName) ?
        "Use at most 120 characters, without edge spaces, path characters or control characters." : null;
    public const string StorageDisclosure = "Save and Update store query names and values locally as unencrypted JSON. They may contain sensitive data. No automatic history.";
    public string LibraryStatus { get; private set; } = StorageDisclosure;
    public Task LibraryTask { get; private set; } = Task.CompletedTask;
    public Func<string, Task<bool>>? ConfirmDeleteAsync { get; set; }
    public UiCommand SaveSearchCommand { get; private set; } = null!;
    public UiCommand LoadSearchCommand { get; private set; } = null!;
    public UiCommand UpdateSearchCommand { get; private set; } = null!;
    public UiCommand RenameSearchCommand { get; private set; } = null!;
    public UiCommand DeleteSearchCommand { get; private set; } = null!;
    private bool CanManageLibrary => HasLibrary && !IsLibraryBusy && !disposed;
    private bool HasLoadedSelection => loadedSearch is { } entry && entry.Id == SelectedSearchId;
    private void InitializeLibraryCommands()
    {
        SaveSearchCommand = new(() => LibraryTask = SaveSearchAsync(), () => CanManageLibrary && SearchLibrary.ValidName(SearchName) && ValidationError is null);
        LoadSearchCommand = new(() => LibraryTask = LoadSearchAsync(), () => CanManageLibrary && SelectedSearchId is not null);
        UpdateSearchCommand = new(() => LibraryTask = UpdateSearchAsync(), () => CanManageLibrary && HasLoadedSelection && ValidationError is null);
        RenameSearchCommand = new(() => LibraryTask = RenameSearchAsync(), () => CanManageLibrary && HasLoadedSelection && SearchLibrary.ValidName(SearchName));
        DeleteSearchCommand = new(() => LibraryTask = DeleteSearchAsync(), () => CanManageLibrary && HasLoadedSelection);
    }
    private void RefreshLibraryCommands()
    { SaveSearchCommand.Refresh(); LoadSearchCommand.Refresh(); UpdateSearchCommand.Refresh(); RenameSearchCommand.Refresh(); DeleteSearchCommand.Refresh(); }
    private async Task RefreshLibraryAsync()
    {
        if (library is null) return;
        try { await ReloadItemsAsync(libraryLifetime.Token); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or OperationCanceledException)
        { if (!disposed) { LibraryStatus = "Search Library unavailable. Current query and documents are unchanged."; Notify(); } }
    }
    private async Task ReloadItemsAsync(CancellationToken token)
    {
        var generation = ++listingGeneration;
        var listing = await Task.Run(library!.List, token);
        if (disposed || generation != listingGeneration) return;
        SavedSearches = listing.Items;
        LibraryStatus = listing.InvalidEntries == 0 ? StorageDisclosure :
            $"{listing.InvalidEntries} malformed/unsupported definitions skipped; other searches remain available.";
        Notify();
    }
    public Task SaveSearchAsync() => ManageAsync(async token =>
    {
        var query = BuildQuery().Snapshot(); var name = SearchName;
        token.ThrowIfCancellationRequested();
        loadedSearch = await library!.CreateAsync(name, query, token); selectedSearch = loadedSearch.Id;
        await ReloadItemsAsync(token);
    });
    public Task LoadSearchAsync() => ManageAsync(async token =>
    {
        if (SelectedSearchId is not { } id) return;
        var entry = await Task.Run(() => library!.Open(id), token); token.ThrowIfCancellationRequested();
        loadedSearch = entry; searchName = entry.Name; ApplyQuery(entry.Query); await ReloadItemsAsync(token);
    });
    public Task UpdateSearchAsync() => ManageAsync(async token =>
    {
        if (!HasLoadedSelection) return;
        var entry = loadedSearch!; var query = BuildQuery().Snapshot();
        token.ThrowIfCancellationRequested();
        loadedSearch = await library!.UpdateAsync(entry, query, token); await ReloadItemsAsync(token);
    });
    public Task RenameSearchAsync() => ManageAsync(async token =>
    {
        if (!HasLoadedSelection) return;
        loadedSearch = await library!.RenameAsync(loadedSearch!, SearchName, token); await ReloadItemsAsync(token);
    });
    public Task DeleteSearchAsync() => ManageAsync(async token =>
    {
        if (!HasLoadedSelection) return;
        var entry = loadedSearch!;
        if (ConfirmDeleteAsync is null || !await ConfirmDeleteAsync(entry.Name)) { LibraryStatus = "Delete canceled; saved definition kept."; return; }
        token.ThrowIfCancellationRequested(); await library!.DeleteAsync(entry, token);
        loadedSearch = null; selectedSearch = null; await ReloadItemsAsync(token);
    });
    private async Task ManageAsync(Func<CancellationToken, Task> action)
    {
        if (!CanManageLibrary) return;
        IsLibraryBusy = true; Notify();
        try { await action(libraryLifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        { if (!disposed) LibraryStatus = e is InvalidOperationException ? "Search name already in use or storage limit reached; choose another name." : "Search Library operation failed or the definition changed. Reload before retrying; current query is kept."; }
        finally { IsLibraryBusy = false; Notify(); }
    }
}
