using System.ComponentModel;

namespace HL7Workbench.Workspace;

/// <summary>Session-only search ownership, supersession, paging and navigator filtering.
/// No query, result header or match value is logged or serialized.</summary>
public sealed class MessageSearchSession : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private readonly MessageSearch search = new();
    private CancellationTokenSource? run;
    private long generation;
    private bool disposed, filter;
    public MessageDocument? Document { get; private set; }
    public MessageSearchResults? Results { get; private set; }
    public bool IsBusy { get; private set; }
    public string Status { get; private set; } = "Enter a literal search value.";
    public MessageSearchProgress Progress { get; private set; } = new(0, 0, 0, 0);
    public int PageIndex { get; private set; }
    public IReadOnlyList<MessageSearchMatch> Page => Results?.Page(PageIndex) ?? [];
    public int ResultCount => Results?.Matches.Count ?? 0;
    public bool CanFilter => Results?.IsCurrent(Document) == true && !IsBusy;
    public bool FilterNavigator
    {
        get => filter;
        set
        {
            if (value && !CanFilter) return;
            filter = value;
            if (value) Document!.ApplySearchFilter(Results!); else Document?.ClearSearchFilter();
            Changed();
        }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? NavigationRequested;
    public MessageSearchSession(DocumentManager documents)
    { this.documents = documents; documents.PropertyChanged += ActiveChanged; SwitchDocument(); }
    private void ActiveChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(DocumentManager.ActiveDocument)) SwitchDocument(); }
    private void SwitchDocument()
    {
        if (ReferenceEquals(Document, documents.ActiveDocument)) return;
        Clear();
        if (Document is { } old) { old.PropertyChanged -= ContentChanged; old.Closed -= Closed; }
        Document = documents.ActiveDocument;
        if (Document is { } current) { current.PropertyChanged += ContentChanged; current.Closed += Closed; }
        Status = Document is null ? "Open an HL7 document to search." : "Search the active document, including unsaved edits."; Changed();
    }
    private void ContentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(MessageDocument.SearchRevision)) return;
        Clear(); Status = "Document changed. Run the search again."; Changed();
    }
    private void Closed(object? sender, EventArgs e)
    { Clear(); Status = "Document closed. Search results were cleared."; Changed(); }
    public async Task StartAsync(MessageSearchQuery query, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Clear();
        if (query.ValidationError() is { } error) { Status = error; Changed(); return; }
        if (Document is not { } document) { Status = "Open an HL7 document to search."; Changed(); return; }
        var key = generation; var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token); run = cancellation;
        IsBusy = true; Status = "Searching…"; Changed();
        var progress = new Progress<MessageSearchProgress>(value =>
        {
            if (disposed || key != generation || !IsBusy) return;
            Progress = value; Status = $"Scanned {value.Scanned:N0} / {value.Total:N0} messages; {value.Matches:N0} matching messages."; Changed();
        });
        try
        {
            var result = await search.SearchAsync(document, query, progress, cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (disposed || key != generation || !result.IsCurrent(Document)) return;
            Results = result; Progress = new(document.MessageCount, document.MessageCount, ResultCount, result.SkippedMessages);
            Status = $"{ResultCount:N0} matching messages. {result.SkippedMessages:N0} unreadable or over-budget messages skipped.";
        }
        catch (OperationCanceledException) { if (key == generation) Status = "Search canceled. No partial filter applied."; }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        { if (key == generation) Status = "Search unavailable. Source and unsaved edits are unchanged; retry the search."; }
        finally
        {
            cancellation.Dispose();
            if (key == generation) { run = null; IsBusy = false; Changed(); }
        }
    }
    public void Cancel() => run?.Cancel();
    public void Clear()
    {
        generation++; run?.Cancel(); run = null; IsBusy = false;
        filter = false; Document?.ClearSearchFilter(); Results = null; PageIndex = 0; Progress = new(0, 0, 0, 0);
        Status = "Search cleared; full navigator restored."; Changed();
    }
    public void SetPage(int page)
    { if (Results is null || page < 0 || page >= Math.Max(1, Results.PageCount)) return; PageIndex = page; Changed(); }
    public async Task NavigateAsync(MessageSearchMatch match, CancellationToken token = default)
    {
        if (disposed || Document is not { } document || Results?.IsCurrent(document) != true || !Results.Matches.Contains(match)) return;
        NavigationRequested?.Invoke(document.Id);
        if (!ReferenceEquals(document, documents.ActiveDocument) || Results?.IsCurrent(document) != true) return;
        try { await document.NavigateAsync(match.MessageNumber, token); }
        catch (OperationCanceledException) { }
        catch (ObjectDisposedException) { }
    }
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
    public void Dispose()
    {
        if (disposed) return; disposed = true; Clear(); documents.PropertyChanged -= ActiveChanged;
        if (Document is { } document) { document.PropertyChanged -= ContentChanged; document.Closed -= Closed; }
        Document = null; PropertyChanged = null; NavigationRequested = null;
    }
}
