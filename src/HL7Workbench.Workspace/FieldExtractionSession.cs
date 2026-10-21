using System.ComponentModel;

namespace HL7Workbench.Workspace;

/// <summary>Feature lifetime, retained result attribution, paging and navigation over existing documents. No UI/Dock dependencies.</summary>
public sealed class FieldExtractionSession : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private readonly FieldExtraction extraction;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? operation;
    private MessageDocument? resultSource;
    private bool disposed; private long generation;
    public MessageDocument? Source { get; private set; }
    public ExtractionResults? Results { get; private set; }
    public ExtractionView? View { get; private set; }
    public IReadOnlyList<ExtractionPreviewRow> Page { get; private set; } = [];
    public int PageIndex { get; private set; }
    public bool IsBusy { get; private set; }
    public string Status { get; private set; } = "Choose columns and run a read-only extraction.";
    public string LastOperation { get; private set; } = "None";
    public bool LastOperationCanceled { get; private set; }
    public ExtractionProgress Progress { get; private set; } = new(0, 0, 0);
    public Task RunningTask { get; private set; } = Task.CompletedTask;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? NavigationRequested;
    public FieldExtractionSession(DocumentManager documents, ExtractionOptions? options = null)
    {
        this.documents = documents; extraction = new(options); documents.PropertyChanged += ActiveChanged;
        SetSource(documents.ActiveDocument);
    }
    private void ActiveChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (Source is null && Results is null && !IsBusy && e.PropertyName == nameof(DocumentManager.ActiveDocument)) SetSource(documents.ActiveDocument);
    }
    public void UseActiveDocument() => SetSource(documents.ActiveDocument);
    public void SetSource(MessageDocument? document)
    {
        if (disposed || IsBusy || ReferenceEquals(Source, document) || document is not null && !documents.Documents.Contains(document)) return;
        if (Source is { } old) { old.PropertyChanged -= SourceChanged; old.Closed -= SourceClosed; }
        Source = document;
        if (Source is { } next) { next.PropertyChanged += SourceChanged; next.Closed += SourceClosed; }
        Changed();
    }
    private void SourceChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (IsBusy && LastOperation == "Extraction" && e.PropertyName == nameof(MessageDocument.SearchRevision)) operation?.Cancel();
        Changed();
    }
    private void SourceClosed(object? sender, EventArgs e)
    {
        operation?.Cancel();
        if (Source is { } old) { old.PropertyChanged -= SourceChanged; old.Closed -= SourceClosed; }
        Source = null; Changed();
    }
    private void ResultChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(MessageDocument.SavePath)) Results?.RefreshSourceProtection(); Changed(); }
    private void ResultClosed(object? sender, EventArgs e)
    { Results?.RefreshSourceProtection(); ObserveResultSource(null); Changed(); }
    private void ObserveResultSource(MessageDocument? document)
    {
        if (resultSource is { } old) { old.PropertyChanged -= ResultChanged; old.Closed -= ResultClosed; }
        resultSource = document;
        if (resultSource is { } next) { next.PropertyChanged += ResultChanged; next.Closed += ResultClosed; }
    }
    public Task RunAsync(ExtractionConfiguration config, ExtractionScope scope, CancellationToken token = default)
    {
        if (disposed || IsBusy || Source is not { } source) return Task.CompletedTask;
        return RunningTask = OperateAsync("Extraction", async ct =>
        {
            var key = generation;
            var progress = new Progress<ExtractionProgress>(p =>
            { if (!disposed && key == generation && IsBusy) { Progress = p; Status = $"Examined {p.Examined:N0}/{p.Total:N0}; staged {p.Rows:N0} rows."; Changed(); } });
            ExtractionResults? pending = null;
            try
            {
                pending = await extraction.RunAsync(source, config, scope, progress, ct);
                var page = await pending.AllRows.PageAsync(0, ct); ct.ThrowIfCancellationRequested();
                if (disposed) return;
                ObserveResultSource(null); Results?.Dispose(); Results = pending; pending = null;
                View = Results.AllRows; PageIndex = 0; Page = page; ObserveResultSource(source);
                Progress = new(Results.Summary.ExaminedMessages, Results.Summary.TotalMessages, Results.RowCount);
                Status = Results.Summary.Truncated ? $"Truncated results: {Results.Summary.Limit}. {Results.RowCount:N0} rows retained; review the summary." : $"Completed: {Results.RowCount:N0} rows. Source HL7 unchanged.";
            }
            finally { pending?.Dispose(); }
        }, token);
    }
    public Task ApplyViewAsync(ExtractionFilter filter, ExtractionSort sort, CancellationToken token = default)
    {
        if (disposed || IsBusy || Results is not { } result) return Task.CompletedTask;
        return RunningTask = OperateAsync("Filter/sort", async ct =>
        {
            var view = await result.CreateViewAsync(filter, sort, ct); var page = await view.PageAsync(0, ct); ct.ThrowIfCancellationRequested();
            if (disposed) return; View = view; Page = page; PageIndex = 0; Status = $"{view.Count:N0} selected rows; export includes this entire selection/order.";
        }, token);
    }
    public Task SetPageAsync(int page, CancellationToken token = default)
    {
        if (disposed || IsBusy || View is not { } view || page < 0 || page >= view.PageCount) return Task.CompletedTask;
        return RunningTask = OperateAsync("Page", async ct =>
        { var rows = await view.PageAsync(page, ct); ct.ThrowIfCancellationRequested(); if (!disposed) { Page = rows; PageIndex = page; Status = $"Page {page + 1:N0}/{view.PageCount:N0}; {view.Count:N0} selected rows."; } }, token);
    }
    public Task ExportAsync(Func<Task<FileSaveDestination?>> choose, CancellationToken token = default)
    {
        if (disposed || IsBusy || Results is not { } result || View is not { } view) return Task.CompletedTask;
        return RunningTask = OperateAsync("CSV export", async ct =>
        {
            var destination = await choose(); ct.ThrowIfCancellationRequested();
            if (destination is null) { Status = "CSV export canceled; results retained."; return; }
            result.CheckSourceDestination(destination, documents.Documents.SelectMany(d => d.ExtractionDestinations()));
            await view.ExportAsync(destination, token: ct); Status = $"Exported {view.Count:N0} selected rows. Source HL7 unchanged.";
        }, token);
    }
    private async Task OperateAsync(string name, Func<CancellationToken, Task> action, CancellationToken token)
    {
        if (disposed || IsBusy) return;
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token); operation = cancellation;
        generation++; LastOperation = name; LastOperationCanceled = false; IsBusy = true; Status = name + " running…"; Changed();
        try { await action(cancellation.Token); }
        catch (OperationCanceledException) { LastOperationCanceled = true; Status = name + " canceled; no partial output published. Previous completed results retained."; }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or FormatException or InvalidOperationException or System.Text.Json.JsonException)
        { Status = name + " unavailable; source and previous results retained. Check configuration, limits and destination."; }
        finally { operation = null; IsBusy = false; if (!disposed) Changed(); }
    }
    public void Cancel() => operation?.Cancel();
    public async Task NavigateAsync(ExtractionPreviewRow row, CancellationToken token = default)
    {
        if (disposed || Results is not { } result || row.ResultId != result.Id || !Page.Contains(row) || result.SourceDocument is not { } document ||
            !documents.Documents.Contains(document) || result.ResolveMessage(row.MessageKey) is not { } id || document.GetLogicalPosition(id) == 0)
        { Status = "Captured source message is unavailable. Rerun against an open source."; Changed(); return; }
        NavigationRequested?.Invoke(document.Id);
        if (!documents.Documents.Contains(document)) return;
        documents.Activate(document.Id);
        try { await document.NavigateAsync(id, token); }
        catch (OperationCanceledException) { } catch (ObjectDisposedException) { }
        Changed();
    }
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
    public void Dispose()
    {
        if (disposed) return; disposed = true; generation++; lifetime.Cancel(); operation?.Cancel();
        documents.PropertyChanged -= ActiveChanged;
        if (Source is { } source) { source.PropertyChanged -= SourceChanged; source.Closed -= SourceClosed; }
        ObserveResultSource(null); Source = null; Results?.Dispose(); Results = null; View = null; Page = [];
        lifetime.Dispose(); PropertyChanged = null; NavigationRequested = null;
    }
}
