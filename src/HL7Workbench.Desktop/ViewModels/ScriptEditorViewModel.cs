using System.ComponentModel;
using System.Collections.Specialized;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Session-only attribution captured from script metadata at run start, never from message bodies or report rows.</summary>
public sealed record ScriptReportOrigin(string ScriptName, ScriptScope Scope)
{
    public string Description => $"Last successful run: {ScriptName} · " +
        (Scope == ScriptScope.WholeDocument ? "Whole document" : "Current message") +
        ". Retained results are independent of the selected script. Export before another successful run or application shutdown.";
}

/// <summary>Tool commands over a Workspace-owned draft and execution service; contains no Dock types.</summary>
public sealed class ScriptEditorViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private readonly ScriptExecution execution;
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? run;
    private bool disposed;
    private long generation;
    private string? outputId;
    private ScriptReports? reports;
    private UiCommand[] commands = [];
    private ScriptScope scope = ScriptScope.CurrentMessage;
    public bool IsManaging { get; private set; }
    public bool CanManage => !disposed && !IsBusy && !IsExporting && !IsManaging;
    public bool IsRenaming { get; private set; }
    public string RenameDraft { get; set; } = "";
    public UiCommand NewCommand { get; }
    public UiCommand OpenCommand { get; }
    public UiCommand SaveCommand { get; }
    public UiCommand DuplicateCommand { get; }
    public UiCommand RenameCommand { get; }
    public UiCommand DeleteCommand { get; }
    public UiCommand RunCommand { get; }
    public UiCommand CancelCommand { get; }
    public UiCommand ReviewCommand { get; }
    public UiCommand ReportsCommand { get; }
    public event Action? OpenEditorRequested;
    public event Action? EditorShown;
    internal void NotifyEditorShown() => EditorShown?.Invoke();
    public Func<string, Task<bool>> ConfirmDeleteAsync { get; set; } = _ => Task.FromResult(false);
    public Func<Task> ShowReportsAsync { get; set; } = () => Task.CompletedTask;
    public IReadOnlyList<ScriptReportInfo> Reports => reports?.Items ?? [];
    public ScriptReportOrigin? ReportOrigin { get; private set; }
    public bool IsExporting { get; private set; }
    public bool CanExport => !disposed && !IsBusy && !IsExporting && Reports.Count > 0;
    private readonly string? preferencesPath;
    public int FontSizePoints { get; private set; } = ScriptEditorPreferences.DefaultPoints;
    public double FontSize => ScriptEditorPreferences.ToDevicePixels(FontSizePoints);
    public string SettingsStatus { get; private set; } = "";
    public ScriptEditorSession Session { get; }
    public ScriptScope Scope { get => scope; set { if (!CanManage || scope == value) return; scope = value; Changed(this, new(nameof(Scope))); } }
    public bool IsBusy { get; private set; }
    public bool CanRun => CanManage && !Session.IsObsolete && Session.Source.Length <= ScriptLimits.ScriptCharacters &&
        documents.ActiveDocument is { IsLoading: false, IsSaving: false, IsCapturingScript: false, MessageCount: > 0 };
    public bool CanReview => !disposed && outputId is not null && documents.Documents.Any(d => d.Id == outputId);
    public string Status { get; private set; } = "Results open as a new unsaved HL7 document.";
    public string Target => documents.ActiveDocument is { } d ? $"Source: document {d.ActivityNumber}, message {d.MessageNumber} of {d.MessageCount}" : "Open an HL7 document to run a script.";
    public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; private set; } = [];
    public Task RunningTask { get; private set; } = Task.CompletedTask;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? ReviewRequested;
    public Func<Task<UnsavedChangesDecision>> PromptUnsavedAsync { get; set; } = () => Task.FromResult(UnsavedChangesDecision.Cancel);
    public ScriptEditorViewModel(DocumentManager documents, ScriptLibrary library, ScriptExecutionOptions? options = null, string? preferencesPath = null)
    {
        this.documents = documents; execution = new(documents, options); Session = new(library);
        NewCommand = new(() => ManageAsync(async () => { if (await Session.NewAsync(PromptUnsavedAsync)) OpenEditorRequested?.Invoke(); }), () => CanManage);
        OpenCommand = new(() => { OpenEditorRequested?.Invoke(); return Task.CompletedTask; }, () => !disposed);
        SaveCommand = new(() => { if (CommitRename()) Session.Save(); return Task.CompletedTask; }, () => CanManage && !Session.IsBundled);
        DuplicateCommand = new(() => { if (CommitRename() && Session.Duplicate()) OpenEditorRequested?.Invoke(); return Task.CompletedTask; }, () => CanManage);
        RenameCommand = new(() => { BeginRename(); return Task.CompletedTask; }, () => CanManage && !Session.IsBundled);
        DeleteCommand = new(() => ManageAsync(() => Session.DeleteAsync(PromptUnsavedAsync, ConfirmDeleteAsync)), () => CanManage && Session.CanDelete);
        RunCommand = new(async () => { if (CommitRename()) await RunAsync(); }, () => CanRun);
        CancelCommand = new(() => { Cancel(); return Task.CompletedTask; }, () => IsBusy);
        ReviewCommand = new(() => { Review(); return Task.CompletedTask; }, () => CanReview);
        ReportsCommand = new(() => ManageAsync(ShowReportsAsync), () => CanManage && CanExport);
        commands = [NewCommand, OpenCommand, SaveCommand, DuplicateCommand, RenameCommand, DeleteCommand, RunCommand, CancelCommand, ReviewCommand, ReportsCommand];
        this.preferencesPath = preferencesPath;
        if (preferencesPath is not null)
        {
            try { FontSizePoints = ScriptEditorPreferences.Load(preferencesPath).FontSizePoints; }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            { SettingsStatus = "Font settings unavailable; using 12 pt. Change font size to save a new preference."; }
        }
        Session.PropertyChanged += Changed; documents.PropertyChanged += Changed;
        ((INotifyCollectionChanged)documents.Documents).CollectionChanged += DocumentsChanged;
    }
    public void SetFontSizePoints(int points)
    {
        FontSizePoints = ScriptEditorPreferences.BoundPoints(points);
        if (preferencesPath is not null)
        {
            try { new ScriptEditorPreferences(FontSizePoints).Save(preferencesPath); SettingsStatus = ""; }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException)
            { SettingsStatus = "Font changed for this session; preference could not be saved."; }
        }
        Changed(this, new(nameof(FontSize)));
    }
    public void Zoom(int direction) => SetFontSizePoints(FontSizePoints + Math.Sign(direction));
    private void Changed(object? sender, PropertyChangedEventArgs e)
    { foreach (var command in commands) command.Refresh(); PropertyChanged?.Invoke(this, new(null)); }
    private void DocumentsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Changed(this, new(null));
    private async Task ManageAsync(Func<Task> action)
    {
        if (!CanManage || !CommitRename()) return;
        IsManaging = true; Changed(this, new(null));
        try { await action(); }
        catch (Exception) { Status = "Script action unavailable; current draft and reports retained."; }
        finally { IsManaging = false; Changed(this, new(null)); }
    }
    public void BeginRename()
    {
        if (!CanManage || Session.IsBundled) return;
        RenameDraft = Session.Name; IsRenaming = true;
        OpenEditorRequested?.Invoke(); Changed(this, new(null));
    }
    public bool CommitRename()
    {
        if (!IsRenaming) return true;
        IsRenaming = false;
        var result = Session.Rename(RenameDraft); Changed(this, new(null)); return result;
    }
    public void CancelRename() { IsRenaming = false; Changed(this, new(null)); }
    public async Task<bool> OpenLibraryAsync(Guid id)
    {
        var opened = false;
        await ManageAsync(async () => opened = await Session.OpenAsync(id, PromptUnsavedAsync)); return opened;
    }
    public Task RunAsync()
    {
        if (!CanRun || documents.ActiveDocument is not { } source) return Task.CompletedTask;
        var script = new ScriptDefinition(Session.Source);
        var scope = Scope;
        var origin = new ScriptReportOrigin(Session.DisplayName, scope);
        IsBusy = true; Diagnostics = []; Status = "Capturing input…";
        run = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); Changed(this, new(null));
        RunningTask = ExecuteAsync(source, script, scope, origin, ++generation, run.Token); return RunningTask;
    }
    private async Task ExecuteAsync(MessageDocument source, ScriptDefinition script, ScriptScope scope, ScriptReportOrigin origin, long job, CancellationToken token)
    {
        try
        {
            var progress = new Progress<ScriptProgress>(p =>
            {
                if (disposed || !IsBusy || job != generation || token.IsCancellationRequested) return;
                Status = $"{p.Phase}: {p.Processed:N0}/{p.Total:N0} messages; {p.OutputBytes:N0} output bytes"; Changed(this, new(null));
            });
            var result = await execution.RunAsync(source, script, scope, progress, token);
            if (disposed) { result.Reports?.Dispose(); return; }
            reports?.Dispose(); reports = result.Reports;
            // Update attribution together with successful report replacement. Selection changes,
            // draft edits, cancellation and failure must never relabel a previous result.
            ReportOrigin = Reports.Count > 0 ? origin : null;
            outputId = result.Document.Id;
            Status = $"Completed: processed {result.Processed:N0}, kept {result.Kept:N0}. New output is unsaved. {Reports.Count} CSV reports available to export.";
            ReviewRequested?.Invoke(outputId);
        }
        catch (ScriptRunException error) { Diagnostics = error.Diagnostics; Status = "Run failed; no partial output published. See diagnostics."; }
        catch (OperationCanceledException) { Status = "Run canceled; no partial output published."; }
        catch (Exception) { Status = "Run unavailable; source retained."; documents.Activity.Record(ActivityKind.ApplicationError); }
        finally { IsBusy = false; run?.Dispose(); run = null; Changed(this, new(null)); }
    }
    public void Cancel() => run?.Cancel();
    public void Review() { if (CanReview) ReviewRequested?.Invoke(outputId!); }
    public async Task<bool> ExportReportAsync(string name, string destination, CancellationToken token = default)
        => await ChooseAndExportReportAsync(name, () => Task.FromResult<string?>(destination), token);
    /// <summary>Retains the producing journal and blocks replacement throughout the native picker and export.</summary>
    public async Task<bool> ChooseAndExportReportAsync(string name, Func<Task<string?>> choose, CancellationToken token = default)
        => await ChooseAndExportReportDestinationAsync(name, async () =>
        {
            var path = await choose(); return path is null ? null : FileSaveDestination.NewFile(path);
        }, token);
    /// <summary>Retains reports while choosing a destination with explicit replacement consent.</summary>
    public async Task<bool> ChooseAndExportReportDestinationAsync(string name, Func<Task<FileSaveDestination?>> choose, CancellationToken token = default)
    {
        if (!CanExport || reports is null) return false;
        var available = reports;
        IsExporting = true; Changed(this, new(null));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token);
        try
        {
            var destination = await choose();
            if (destination is null) { Status = "CSV export canceled; staged reports remain available."; return false; }
            await available.ExportAsync(name, destination, cancellation.Token);
            Status = "CSV exported."; return true;
        }
        catch (OperationCanceledException) { Status = "CSV export canceled; staged reports remain available."; return false; }
        catch (Exception) { Status = "CSV export failed. Choose the destination again and retry; staged reports remain available."; return false; }
        finally { IsExporting = false; Changed(this, new(null)); }
    }
    public async Task<bool> ConfirmCloseAsync(Func<Task<UnsavedChangesDecision>> prompt, bool discardImmediately = true)
    {
        if (!CommitRename() || IsManaging) return false;
        if (!await Session.ConfirmChangesAsync(prompt, discardImmediately)) return false;
        Cancel(); await RunningTask; return true;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; lifetime.Cancel();
        reports?.Dispose(); reports = null; ReportOrigin = null;
        Session.PropertyChanged -= Changed; documents.PropertyChanged -= Changed;
        ((INotifyCollectionChanged)documents.Documents).CollectionChanged -= DocumentsChanged;
        lifetime.Dispose(); PropertyChanged = null; ReviewRequested = null;
        PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Cancel);
        OpenEditorRequested = EditorShown = null; ConfirmDeleteAsync = _ => Task.FromResult(false); ShowReportsAsync = () => Task.CompletedTask;
    }
}
