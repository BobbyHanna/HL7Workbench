using System.ComponentModel;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>A disposable projection of the active document's original generation report.</summary>
public sealed class GenerationReportViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private bool disposed;
    public FhirGenerationReport? Report => disposed ? null : documents.ActiveDocument?.GenerationReport;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Shown;
    internal bool FocusRequested { get; private set; }

    public GenerationReportViewModel(DocumentManager documents)
    { this.documents = documents; documents.PropertyChanged += Changed; }

    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        // Reports describe generation, not subsequent edits or the currently viewed message.
        if (e.PropertyName == nameof(DocumentManager.ActiveDocument)) PropertyChanged?.Invoke(this, new(nameof(Report)));
    }
    public void NotifyShown() { if (!disposed) { FocusRequested = true; Shown?.Invoke(); } }
    internal void FocusHandled() => FocusRequested = false;
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; FocusRequested = false; documents.PropertyChanged -= Changed;
        PropertyChanged?.Invoke(this, new(nameof(Report))); PropertyChanged = null; Shown = null;
    }
}
