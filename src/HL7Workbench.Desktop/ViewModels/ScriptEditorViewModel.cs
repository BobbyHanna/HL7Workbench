using System.ComponentModel;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

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
    private readonly string? preferencesPath;
    public int FontSizePoints { get; private set; } = ScriptEditorPreferences.DefaultPoints;
    public double FontSize => ScriptEditorPreferences.ToDevicePixels(FontSizePoints);
    public string SettingsStatus { get; private set; } = "";
    public ScriptEditorSession Session { get; }
    public ScriptScope Scope { get; set; } = ScriptScope.CurrentMessage;
    public bool IsBusy { get; private set; }
    public bool CanRun => !disposed && !IsBusy && Session.Source.Length <= ScriptLimits.ScriptCharacters &&
        documents.ActiveDocument is { IsLoading: false, IsSaving: false, IsCapturingScript: false, MessageCount: > 0 };
    public bool CanReview => outputId is not null && documents.Documents.Any(d => d.Id == outputId);
    public string Status { get; private set; } = "Results open as a new unsaved HL7 document.";
    public string Target => documents.ActiveDocument is { } d ? $"Source: document {d.ActivityNumber}, message {d.MessageNumber} of {d.MessageCount}" : "Open an HL7 document to run a Message script.";
    public IReadOnlyList<ScriptDiagnostic> Diagnostics { get; private set; } = [];
    public Task RunningTask { get; private set; } = Task.CompletedTask;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? ReviewRequested;
    public Func<Task<UnsavedChangesDecision>> PromptUnsavedAsync { get; set; } = () => Task.FromResult(UnsavedChangesDecision.Cancel);
    public ScriptEditorViewModel(DocumentManager documents, ScriptLibrary library, ScriptExecutionOptions? options = null, string? preferencesPath = null)
    {
        this.documents = documents; execution = new(documents, options); Session = new(library);
        this.preferencesPath = preferencesPath;
        if (preferencesPath is not null)
        {
            try { FontSizePoints = ScriptEditorPreferences.Load(preferencesPath).FontSizePoints; }
            catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
            { SettingsStatus = "Font settings unavailable; using 12 pt. Change font size to save a new preference."; }
        }
        Session.PropertyChanged += Changed; documents.PropertyChanged += Changed;
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
    private void Changed(object? sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, new(null));
    public Task RunAsync()
    {
        if (!CanRun || documents.ActiveDocument is not { } source) return Task.CompletedTask;
        var script = new ScriptDefinition(Session.Source);
        var scope = Scope;
        IsBusy = true; Diagnostics = []; Status = "Capturing input…";
        run = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token); Changed(this, new(null));
        RunningTask = ExecuteAsync(source, script, scope, ++generation, run.Token); return RunningTask;
    }
    private async Task ExecuteAsync(MessageDocument source, ScriptDefinition script, ScriptScope scope, long job, CancellationToken token)
    {
        try
        {
            var progress = new Progress<ScriptProgress>(p =>
            {
                if (disposed || !IsBusy || job != generation || token.IsCancellationRequested) return;
                Status = $"{p.Phase}: {p.Processed:N0}/{p.Total:N0} messages; {p.OutputBytes:N0} output bytes"; Changed(this, new(null));
            });
            var result = await execution.RunAsync(source, script, scope, progress, token);
            outputId = result.Document.Id;
            Status = $"Completed: processed {result.Processed:N0}, kept {result.Kept:N0}. New output is unsaved.";
            ReviewRequested?.Invoke(outputId);
        }
        catch (ScriptRunException error) { Diagnostics = error.Diagnostics; Status = "Run failed; no partial output published. See diagnostics."; }
        catch (OperationCanceledException) { Status = "Run canceled; no partial output published."; }
        catch (Exception) { Status = "Run unavailable; source retained."; documents.Activity.Record(ActivityKind.ApplicationError); }
        finally { IsBusy = false; run?.Dispose(); run = null; Changed(this, new(null)); }
    }
    public void Cancel() => run?.Cancel();
    public void Review() { if (CanReview) ReviewRequested?.Invoke(outputId!); }
    public async Task<bool> ConfirmCloseAsync(Func<Task<UnsavedChangesDecision>> prompt, bool discardImmediately = true)
    {
        if (!await Session.ConfirmChangesAsync(prompt, discardImmediately)) return false;
        Cancel(); await RunningTask; return true;
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; lifetime.Cancel();
        Session.PropertyChanged -= Changed; documents.PropertyChanged -= Changed;
        lifetime.Dispose(); PropertyChanged = null; ReviewRequested = null;
        PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Cancel);
    }
}
