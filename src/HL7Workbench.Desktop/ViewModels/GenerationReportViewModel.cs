using System.ComponentModel;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>A disposable projection of the active document's original generation report.</summary>
public sealed class GenerationReportViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private bool disposed;
    private MessageDocument? observed;
    public FhirGenerationReport? Report => disposed ? null : documents.ActiveDocument?.GenerationReport;
    public SyntheticGenerationReport? SyntheticReport => disposed ? null : documents.ActiveDocument?.SyntheticGenerationReport;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Shown;
    internal bool FocusRequested { get; private set; }

    public GenerationReportViewModel(DocumentManager documents)
    { this.documents = documents; documents.PropertyChanged += Changed; Observe(); }

    private void Observe()
    {
        if (observed is not null) observed.PropertyChanged -= ReportChanged;
        observed = documents.ActiveDocument;
        if (observed is not null) observed.PropertyChanged += ReportChanged;
    }
    private void ReportChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(MessageDocument.SyntheticGenerationReport)) PropertyChanged?.Invoke(this, new(null)); }

    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        // Reports describe generation, not subsequent edits or the currently viewed message.
        if (e.PropertyName == nameof(DocumentManager.ActiveDocument)) { Observe(); PropertyChanged?.Invoke(this, new(null)); }
    }
    public void NotifyShown() { if (!disposed) { FocusRequested = true; Shown?.Invoke(); } }
    internal void FocusHandled() => FocusRequested = false;
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; FocusRequested = false; documents.PropertyChanged -= Changed;
        if (observed is not null) observed.PropertyChanged -= ReportChanged;
        observed = null;
        PropertyChanged?.Invoke(this, new(null)); PropertyChanged = null; Shown = null;
    }
}
