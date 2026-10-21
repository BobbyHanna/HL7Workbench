using System.Collections.Specialized;
using System.ComponentModel;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Disposable extraction tool over a retained configuration draft; no Dock types or persisted source values.</summary>
public sealed class FieldExtractionViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private bool disposed, managing;
    private Guid draftId;
    private ExtractionColumnDraft[] knownColumns = [];
    private string literal = "";
    public FieldExtractionSession Session { get; }
    public ExtractionConfigurationSession Draft { get; }
    public IReadOnlyList<MessageDocument> Documents => documents.Documents;
    public bool CanManage => !disposed && !managing && !Draft.IsBusy && !Session.IsBusy;
    public ExtractionScope Scope { get; set; } = ExtractionScope.WholeDocument;
    public int FilterColumn { get; set; }
    public ExtractionFilterOperator FilterOperator { get; set; }
    public int SortColumn { get; set; } = -1;
    public bool Descending { get; set; }
    public bool NumericSort { get; set; }
    public string FilterLiteral { get => literal; set { if (value.Length <= 1024) literal = value; } }
    public string Error { get; private set; } = "";
    public Task ActionTask { get; private set; } = Task.CompletedTask;
    public Func<Task<UnsavedChangesDecision>> PromptUnsavedAsync { get; set; } = () => Task.FromResult(UnsavedChangesDecision.Cancel);
    public Func<string, Task<bool>> ConfirmDeleteAsync { get; set; } = _ => Task.FromResult(false);
    public Func<Task<FileSaveDestination?>> ChooseExportAsync { get; set; } = () => Task.FromResult<FileSaveDestination?>(null);
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Shown;
    public void NotifyShown() => Shown?.Invoke();
    public FieldExtractionViewModel(DocumentManager documents, ExtractionConfigurationSession draft, ExtractionOptions? options = null)
    {
        this.documents = documents; Draft = draft; Session = new(documents, options);
        Draft.PropertyChanged += Changed; Session.PropertyChanged += Changed;
        ((INotifyCollectionChanged)documents.Documents).CollectionChanged += DocumentsChanged;
        RestorePreferences();
        knownColumns = Draft.Columns.ToArray();
    }
    private void RestorePreferences()
    {
        draftId = Draft.Id; FilterColumn = Draft.FilterColumn; FilterOperator = Draft.FilterOperator;
        SortColumn = Draft.SortColumn; Descending = Draft.Descending; NumericSort = Draft.NumericSort;
        literal = "";
    }
    private void StorePreferences()
    {
        if (!Draft.CanEdit) return;
        Draft.FilterColumn = Math.Clamp(FilterColumn, 0, Draft.Columns.Count - 1); Draft.FilterOperator = FilterOperator;
        Draft.SortColumn = SortColumn >= Draft.Columns.Count ? -1 : SortColumn; Draft.Descending = Descending; Draft.NumericSort = NumericSort;
    }
    public Task RunAsync()
    {
        if (!CanManage) return Task.CompletedTask; Error = "";
        try { return ActionTask = Session.RunAsync(Draft.Snapshot(), Scope); }
        catch (Exception error) when (error is ArgumentException or FormatException)
        { Error = "Use valid HL7 paths, distinct labels and supported preferences before running."; Changed(this, new(null)); return Task.CompletedTask; }
    }
    public Task ApplyViewAsync()
    {
        if (!CanManage) return Task.CompletedTask; Error = "";
        return ActionTask = Session.ApplyViewAsync(new(FilterColumn, FilterOperator, literal), new(SortColumn, Descending, NumericSort));
    }
    public Task ExportAsync() => CanManage ? ActionTask = Session.ExportAsync(ChooseExportAsync) : Task.CompletedTask;
    public Task SaveAsync() => ManageAsync(async () => { StorePreferences(); return await Draft.SaveAsync(); });
    public Task DuplicateAsync() => ManageAsync(() =>
    {
        var filter = FilterColumn; var op = FilterOperator; var sort = SortColumn; var desc = Descending; var number = NumericSort;
        var created = Draft.Duplicate();
        if (created) { FilterColumn = filter; FilterOperator = op; SortColumn = sort; Descending = desc; NumericSort = number; StorePreferences(); }
        return Task.FromResult(created);
    });
    public Task NewAsync() => ManageAsync(() => Draft.NewAsync(PromptUnsavedAsync));
    public Task OpenAsync(Guid id) => ManageAsync(() => Draft.OpenAsync(id, PromptUnsavedAsync));
    public Task DeleteAsync() => ManageAsync(() => Draft.DeleteAsync(PromptUnsavedAsync, ConfirmDeleteAsync));
    private Task ManageAsync(Func<Task<bool>> action)
    {
        if (!CanManage) return Task.CompletedTask;
        return ActionTask = Core();
        async Task Core()
        {
            managing = true; Error = ""; Changed(this, new(null));
            try { _ = await action(); }
            catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or FormatException or InvalidOperationException)
            { Error = "Configuration action unavailable; current draft/results retained."; }
            finally { managing = false; Changed(this, new(null)); }
        }
    }
    private void DocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Changed(this, new(null));
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (draftId != Draft.Id) RestorePreferences();
        else if (!knownColumns.SequenceEqual(Draft.Columns)) { FilterColumn = Draft.FilterColumn; SortColumn = Draft.SortColumn; }
        knownColumns = Draft.Columns.ToArray(); PropertyChanged?.Invoke(this, new(null));
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; Session.Dispose();
        Draft.PropertyChanged -= Changed; Session.PropertyChanged -= Changed;
        ((INotifyCollectionChanged)documents.Documents).CollectionChanged -= DocumentsChanged;
        PropertyChanged = null; Shown = null; literal = "";
        PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Cancel); ConfirmDeleteAsync = _ => Task.FromResult(false);
        ChooseExportAsync = () => Task.FromResult<FileSaveDestination?>(null);
    }
}
