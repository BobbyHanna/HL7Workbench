using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using HL7Workbench.Querying;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

public sealed class SqlScopeDocument(MessageDocument document) : INotifyPropertyChanged
{
    private bool selected;
    public MessageDocument Document { get; } = document;
    public string Label => $"Document {Document.ActivityNumber} · {Document.DisplayTitle}";
    public bool Selected { get => selected; set { if (selected == value) return; selected = value; PropertyChanged?.Invoke(this, new(nameof(Selected))); } }
    public event PropertyChangedEventHandler? PropertyChanged;
    internal void RefreshLabel() => PropertyChanged?.Invoke(this, new(nameof(Label)));
    public override string ToString() => "SQL scope choice";
}

/// <summary>Owns one SQL tool's cancellation, journal and draft, independently of Dock view reconstruction. Never logs SQL or values.</summary>
public sealed partial class SqlQueryViewModel : INotifyPropertyChanged, IDisposable
{
    internal ToolEditorState EditorState { get; } = new();
    public const string DefaultSql = "SELECT DocumentId, MessageId, MessageNumber, MessageType, TriggerEvent, MessageControlId\nFROM Messages\nORDER BY MessageNumber;";
    private readonly DocumentManager documents;
    private readonly SqlQuery execution;
    private readonly SqlQueryHistory history;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private bool disposed, managing, selectedScope;
    private string sql = DefaultSql, name = "", baselineSql = DefaultSql, baselineName = "";
    private MessageDocument[] runningSources = [];
    private string[] resultDocumentIds = [];
    private string resultSql = "", resultScope = "";
    private string? cancellationReason;
    private readonly HashSet<MessageDocument> observed = [];
    public ObservableCollection<SqlScopeDocument> ScopeDocuments { get; } = [];
    public bool SelectedScope { get => selectedScope; set { selectedScope = value; Notify(); } }
    public string Sql { get => sql; set { if (sql == value || disposed) return; sql = value; ContentVersion++; Notify(); } }
    public string Name { get => name; set { if (name == value || disposed) return; name = value; ContentVersion++; Notify(); } }
    public bool IsDirty => sql != baselineSql || name != baselineName;
    public long ContentVersion { get; private set; }
    public bool IsBusy { get; private set; }
    public bool CanManage => !disposed && !IsBusy && !managing;
    public bool CanRun => CanManage && SqlQueryLibrary.ValidSql(sql) && (selectedScope ? ScopeDocuments.Count(d => d.Selected) is > 0 and <= 16 : documents.ActiveDocument is not null);
    public string Target => selectedScope ? $"{ScopeDocuments.Count(d => d.Selected)} open documents selected" :
        documents.ActiveDocument is { } d ? $"Current: document {d.ActivityNumber}" : "Open an HL7 document to query";
    public string Status { get; private set; } = "Ready. Ctrl+Enter runs one read-only query locally.";
    public string ResultStatus => Results is null ? "No completed result. No rows have been published." :
        $"Complete · {Results.RowCount:N0} rows · SQL/transfer {Results.ExecutionDuration.TotalSeconds:0.000}s · total {Results.Duration.TotalSeconds:0.000}s · {resultScope}. " +
        (resultSql != sql ? "SQL draft differs; outputs still use the last completed query. " : "") +
        (Results.IsCurrent && resultDocumentIds.All(id => documents.Documents.Any(d => d.Id == id)) ?
            "Captured revisions are current." : "Captured revisions are stale or closed; outputs still use the captured bytes.");
    public SqlQueryResults? Results { get; private set; }
    public IReadOnlyList<SqlPreviewRow> Page { get; private set; } = [];
    public int PageIndex { get; private set; }
    public int PageCount => Math.Max(1, ((Results?.RowCount ?? 0) + 99) / 100);
    public int SortColumn { get; private set; } = -1;
    public bool Descending { get; private set; }
    private int[]? order;
    public IReadOnlyList<SqlQueryHistoryEntry> History => history.Entries;
    public Task ActionTask { get; private set; } = Task.CompletedTask;
    public Task Disposal { get; private set; } = Task.CompletedTask;
    public UiCommand RunCommand { get; }
    public UiCommand CancelCommand { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? OpenDocumentRequested;
    public event Action? Shown;
    public Func<Task<FileSaveDestination?>> ChooseExportAsync { get; set; } = () => Task.FromResult<FileSaveDestination?>(null);
    public SqlQueryViewModel(DocumentManager documents, SqlQueryLibrary library, SqlQueryHistory? history = null, SqlQueryOptions? options = null)
    {
        this.documents = documents; this.library = library; this.history = history ?? new(); execution = new(documents, options);
        RunCommand = new(() => RunAsync(), () => CanRun); CancelCommand = new(() => { Cancel(); return Task.CompletedTask; }, () => IsBusy);
        documents.PropertyChanged += DocumentsChanged;
        ((INotifyCollectionChanged)documents.Documents).CollectionChanged += MembershipChanged;
        SyncDocuments(); LibraryTask = RefreshLibraryAsync();
    }
    internal void NotifyShown() { if (!disposed) Shown?.Invoke(); }
    public void ReportStatus(string message) { if (!disposed) { Status = message; Notify(); } }
    private void SyncDocuments()
    {
        foreach (var d in observed.Where(d => !documents.Documents.Contains(d)).ToArray())
        { d.PropertyChanged -= SourceChanged; observed.Remove(d); }
        foreach (var choice in ScopeDocuments.Where(c => !documents.Documents.Contains(c.Document)).ToArray())
        { choice.PropertyChanged -= DocumentsChanged; ScopeDocuments.Remove(choice); }
        foreach (var d in documents.Documents)
        {
            if (observed.Add(d)) d.PropertyChanged += SourceChanged;
            if (!ScopeDocuments.Any(c => c.Document == d)) { var choice = new SqlScopeDocument(d); choice.PropertyChanged += DocumentsChanged; ScopeDocuments.Add(choice); }
        }
        if (IsBusy && runningSources.Any(d => !documents.Documents.Contains(d))) { cancellationReason = "Source closed; operation canceled. Run again with an open source."; Cancel(); }
    }
    private void MembershipChanged(object? sender, NotifyCollectionChangedEventArgs e) { SyncDocuments(); Notify(); }
    private void DocumentsChanged(object? sender, PropertyChangedEventArgs e) => Notify();
    private void SourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MessageDocument.DisplayTitle))
            foreach (var choice in ScopeDocuments.Where(c => ReferenceEquals(c.Document, sender))) choice.RefreshLabel();
        if (e.PropertyName == nameof(MessageDocument.SearchRevision) && IsBusy && runningSources.Contains(sender))
        { cancellationReason = "Source revision changed; query canceled. Run again to capture the current revision."; Cancel(); }
        if (e.PropertyName is nameof(MessageDocument.SearchRevision) or nameof(MessageDocument.DisplayTitle)) Notify();
    }
    public Task RunAsync()
    {
        if (!CanRun) return Task.CompletedTask;
        var text = sql;
        var sources = selectedScope ? ScopeDocuments.Where(c => c.Selected).Select(c => c.Document).ToArray() : [documents.ActiveDocument!];
        Results?.Dispose(); Results = null; resultDocumentIds = []; resultSql = resultScope = ""; Page = []; order = null; SortColumn = -1; PageIndex = 0; ClearSelection(); FullValue = "";
        return OperateAsync("Capturing source snapshots…", async token =>
        {
            runningSources = sources;
            var progress = new Progress<SqlQueryProgress>(p =>
            { if (!disposed && !token.IsCancellationRequested && IsBusy) { Status = $"{p.Phase}: {p.Completed:N0}/{p.Total:N0}"; Notify(); } });
            try
            {
                var result = await execution.RunAsync(text, sources, progress: progress, token: token);
                if (disposed || token.IsCancellationRequested) { result.Dispose(); token.ThrowIfCancellationRequested(); return; }
                Results = result; resultDocumentIds = sources.Select(d => d.Id).ToArray(); resultSql = text;
                resultScope = "Captured documents " + string.Join(", ", sources.Select(d => d.ActivityNumber)); Page = await result.PageAsync(0, token);
                history.Add(text, "Completed", result.RowCount); Status = "Query completed. All rows are available; display previews may be abbreviated.";
            }
            catch (Exception error)
            { if (!disposed) history.Add(text, error is OperationCanceledException ? "Canceled" : "Failed", 0); Results?.Dispose(); Results = null; Page = []; throw; }
            finally { runningSources = []; }
        });
    }
    private Task OperateAsync(string status, Func<CancellationToken, Task> action)
    {
        if (!CanManage) return Task.CompletedTask;
        IsBusy = true; Status = status; cancellationReason = null; operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); Notify();
        return ActionTask = Core(operation.Token);
        async Task Core(CancellationToken token)
        {
            try { await action(token); }
            catch (OperationCanceledException) { if (!disposed) Status = cancellationReason ?? "Operation canceled. No partial result or output published."; }
            catch (QueryException error) { if (!disposed) Status = Explain(error.Code); }
            catch (Exception) { if (!disposed) Status = "Operation unavailable; source documents are unchanged. Retry or reduce the scope."; }
            finally { IsBusy = false; operation?.Dispose(); operation = null; Notify(); }
        }
    }
    private static string Explain(QueryError error) => error switch
    {
        QueryError.SourceUnavailable => "Source/output unavailable. Extraction requires original identity columns, compatible encodings, safe framing and no batch wrappers or unsafe joins. No document was published.",
        QueryError.InvalidValue => "A decoded value has unresolved escapes or cannot be represented losslessly. Inspect HL7_RAW and HL7_STATUS instead.",
        _ => new QueryException(error).Message
    };
    public Task ChangePageAsync(int page) => CanManage && Results is { } result && page >= 0 && page < PageCount ?
        OperateAsync("Reading result page…", async token => { Page = await result.OrderedPageAsync(page, order, token); PageIndex = page; ClearSelection(); FullValue = ""; Status = "Complete result; page loaded."; }) : Task.CompletedTask;
    public Task SortAsync(int column) => CanManage && Results is { } result ? OperateAsync("Sorting complete result journal…", async token =>
    {
        var descending = SortColumn == column && !Descending;
        var sorted = await result.SortOrderAsync(column, descending, token);
        var page = await result.OrderedPageAsync(0, sorted, token);
        order = sorted; Page = page; SortColumn = column; Descending = descending; PageIndex = 0; ClearSelection(); FullValue = "";
        Status = "All result rows sorted locally; text uses ordinal comparison. CSV follows this order. Extraction preserves source order.";
    }) : Task.CompletedTask;
    public Task ExportAsync() => CanManage && Results is { } result ? OperateAsync("Choose a CSV destination…", async token =>
    {
        var destination = await ChooseExportAsync(); token.ThrowIfCancellationRequested();
        if (destination is null) { Status = "Export canceled."; return; }
        await result.ExportCsvAsync(destination, order, token); Status = $"Exported all {result.RowCount:N0} rows to CSV, including rows outside the visible page.";
    }) : Task.CompletedTask;
    public Task ExtractAsync() => CanManage && Results is { CanIdentifyMessages: true } result ? OperateAsync("Opening matching messages from the complete journal…", async token =>
    {
        var output = await result.OpenMatchingMessagesAsync(documents, token);
        if (output is null) Status = "No matching messages; no document opened.";
        else { OpenDocumentRequested?.Invoke(output.Id); Status = $"Opened {output.MessageCount:N0} unique captured messages in a new unsaved document. Original documents are unchanged."; }
    }) : Task.CompletedTask;
    public void Cancel() => operation?.Cancel();
    private void Notify()
    { if (disposed) return; RunCommand.Refresh(); CancelCommand.Refresh(); PropertyChanged?.Invoke(this, new(null)); }
    public void Dispose()
    {
        if (disposed) return; disposed = true; lifetime.Cancel(); EditorState.Close(); Results?.Dispose(); Results = null; Page = []; order = null;
        documents.PropertyChanged -= DocumentsChanged; ((INotifyCollectionChanged)documents.Documents).CollectionChanged -= MembershipChanged;
        foreach (var d in observed) d.PropertyChanged -= SourceChanged; observed.Clear();
        foreach (var d in ScopeDocuments) d.PropertyChanged -= DocumentsChanged; ScopeDocuments.Clear();
        sql = baselineSql = name = baselineName = FullValue = resultSql = resultScope = ""; resultDocumentIds = []; Selection.Clear(); saved = null; SavedQueries = [];
        OpenDocumentRequested = null; Shown = null; PropertyChanged = null;
        PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Cancel); ConfirmDeleteAsync = _ => Task.FromResult(false); ChooseExportAsync = () => Task.FromResult<FileSaveDestination?>(null);
        Disposal = DrainAsync(ActionTask, LibraryTask);
    }
    private async Task DrainAsync(Task action, Task libraryAction)
    { try { await Task.WhenAll(action, libraryAction); } catch (Exception) { } finally { lifetime.Dispose(); } }
}
