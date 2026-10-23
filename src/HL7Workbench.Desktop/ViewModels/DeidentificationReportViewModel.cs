using System.ComponentModel;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Disposable, metadata-only projection following the selected output document.</summary>
public sealed class DeidentificationReportViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private TransformationReport? observed;
    private bool disposed;
    public TransformationReport? Report => disposed ? null : observed;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Shown;
    internal bool FocusRequested { get; private set; }
    public DeidentificationReportViewModel(DocumentManager documents)
    { this.documents = documents; documents.PropertyChanged += DocumentChanged; Bind(); }
    private void DocumentChanged(object? sender, PropertyChangedEventArgs e)
    { if (e.PropertyName == nameof(DocumentManager.ActiveDocument)) Bind(); }
    private void Bind()
    {
        if (observed is not null) observed.PropertyChanged -= Changed;
        observed = documents.ActiveDocument?.TransformationReport;
        if (observed is not null) observed.PropertyChanged += Changed;
        Changed(this, new(nameof(Report)));
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, new(nameof(Report)));
    public void NotifyShown() { if (!disposed) { FocusRequested = true; Shown?.Invoke(); } }
    internal void FocusHandled() => FocusRequested = false;
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; FocusRequested = false; documents.PropertyChanged -= DocumentChanged;
        if (observed is not null) observed.PropertyChanged -= Changed;
        observed = null; Changed(this, new(nameof(Report))); PropertyChanged = null; Shown = null;
    }
}
