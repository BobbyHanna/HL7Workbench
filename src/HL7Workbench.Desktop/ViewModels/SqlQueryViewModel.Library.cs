using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

public sealed partial class SqlQueryViewModel
{
    private readonly SqlQueryLibrary library;
    private SavedSqlQuery? saved;
    private long listingGeneration;
    public IReadOnlyList<SqlQueryLibraryItem> SavedQueries { get; private set; } = [];
    public Guid? SavedId => saved?.Id;
    public Task LibraryTask { get; private set; } = Task.CompletedTask;
    public string LibraryStatus { get; private set; } = SqlQueryLibrary.StorageDisclosure;
    public Func<Task<UnsavedChangesDecision>> PromptUnsavedAsync { get; set; } = () => Task.FromResult(UnsavedChangesDecision.Cancel);
    public Func<string, Task<bool>> ConfirmDeleteAsync { get; set; } = _ => Task.FromResult(false);
    private async Task RefreshLibraryAsync()
    {
        var generation = ++listingGeneration;
        try
        {
            var listing = await Task.Run(library.List, lifetime.Token);
            if (disposed || generation != listingGeneration) return;
            SavedQueries = listing.Items;
            LibraryStatus = (listing.InvalidEntries == 0 ? "" : $"{listing.InvalidEntries} malformed/unsupported definitions skipped. ") + SqlQueryLibrary.StorageDisclosure;
        }
        catch (Exception) { if (!disposed && generation == listingGeneration) LibraryStatus = "SQL Library unavailable; current draft is retained. " + SqlQueryLibrary.StorageDisclosure; }
        Notify();
    }
    private Task ManageAsync(Func<Task> action)
    {
        if (!CanManage) return Task.CompletedTask;
        managing = true; Notify();
        return LibraryTask = Core();
        async Task Core()
        {
            try { await action(); }
            catch (OperationCanceledException) { }
            catch (Exception) { if (!disposed) LibraryStatus = "SQL Library action failed: check names, permissions or external changes. Current draft is retained. " + SqlQueryLibrary.StorageDisclosure; }
            finally { managing = false; Notify(); }
        }
    }
    private void ApplyDraft(string text, string title, SavedSqlQuery? entry)
    { sql = baselineSql = text; name = baselineName = title; saved = entry; ContentVersion++; FullValue = ""; Notify(); }
    private async Task<bool> ConfirmDraftAsync(bool discardImmediately)
    {
        if (!IsDirty) return true;
        var version = ContentVersion; var decision = await PromptUnsavedAsync();
        if (disposed || ContentVersion != version) return false;
        if (decision == UnsavedChangesDecision.Save) return await SaveCoreAsync() && !IsDirty && ContentVersion == version;
        if (decision != UnsavedChangesDecision.Discard) return false;
        if (discardImmediately) { baselineSql = sql; baselineName = name; Notify(); }
        return true;
    }
    public async Task<bool> ConfirmCloseAsync(bool discardImmediately = true)
    {
        if (disposed) return true;
        if (managing) return false;
        try
        {
            Cancel(); await ActionTask;
            return await ConfirmDraftAsync(discardImmediately);
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception)
        {
            // Closing is also called from an async window event, outside ManageAsync.
            // Storage conflicts must retain the draft rather than escape that boundary.
            if (!disposed) { LibraryStatus = "Query save failed or the definition changed. Close canceled; current draft retained. " + SqlQueryLibrary.StorageDisclosure; Notify(); }
            return false;
        }
    }
    public Task NewAsync() => ManageAsync(async () =>
    { if (await ConfirmDraftAsync(false)) { ApplyDraft(DefaultSql, "", null); Status = "New query draft. Nothing is saved automatically."; } });
    public Task OpenAsync(Guid id) => ManageAsync(async () =>
    {
        if (!await ConfirmDraftAsync(false)) return;
        var entry = await Task.Run(() => library.Open(id), lifetime.Token);
        if (disposed) return; ApplyDraft(entry.Sql, entry.Name, entry); Status = "Saved query opened; press Run to execute.";
    });
    public Task OpenHistoryAsync(SqlQueryHistoryEntry entry) => ManageAsync(async () =>
    {
        if (!history.Entries.Contains(entry) || !await ConfirmDraftAsync(false)) return;
        ApplyDraft(entry.Sql, "", null); baselineSql = DefaultSql; ContentVersion++; Status = "Session history copied into a new unsaved draft. Press Run to execute.";
    });
    public Task SaveAsync() => ManageAsync(async () => { await SaveCoreAsync(); });
    private async Task<bool> SaveCoreAsync()
    {
        if (!SqlQueryLibrary.ValidName(name) || !SqlQueryLibrary.ValidSql(sql))
        { LibraryStatus = "Enter a query name (up to 120 characters, no path/control characters) and SQL (up to 65,536 Unicode characters) before saving."; Notify(); return false; }
        var title = name; var text = sql;
        var entry = saved is null ? await library.CreateAsync(title, text, lifetime.Token) : await library.UpdateAsync(saved, title, text, lifetime.Token);
        if (disposed) return false;
        saved = entry; baselineName = entry.Name; baselineSql = entry.Sql;
        await RefreshLibraryAsync(); Status = "Query explicitly saved locally. " + SqlQueryLibrary.StorageDisclosure; return true;
    }
    public Task RenameAsync() => ManageAsync(async () =>
    {
        if (saved is null) return;
        var entry = await library.RenameAsync(saved, name, lifetime.Token);
        if (disposed) return; saved = entry; baselineName = entry.Name;
        await RefreshLibraryAsync(); Status = "Saved query renamed. Unsaved SQL edits remain unsaved.";
    });
    public Task DeleteAsync() => ManageAsync(async () =>
    {
        if (saved is null || !await ConfirmDraftAsync(false) || saved is not { } entry) return;
        var version = ContentVersion;
        if (!await ConfirmDeleteAsync(entry.Name) || ContentVersion != version || disposed) return;
        await library.DeleteAsync(entry, lifetime.Token);
        if (disposed) return;
        saved = null; name = baselineName = ""; baselineSql = DefaultSql; ContentVersion++;
        await RefreshLibraryAsync(); Status = "Saved definition deleted. Current SQL remains a session-only draft; results and source documents are unchanged.";
    });
}
