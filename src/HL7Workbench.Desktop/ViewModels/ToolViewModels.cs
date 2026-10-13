using System.Collections.ObjectModel;
using System.ComponentModel;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

public sealed class MessageBrowserViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private bool disposed;
    public ReadOnlyObservableCollection<MessageDocument> Documents => documents.Documents;
    public MessageDocument? SelectedDocument
    {
        get => documents.ActiveDocument;
        set { if (value is not null) Open(value.Id); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action<string>? OpenRequested;
    public MessageBrowserViewModel(DocumentManager documents)
    {
        this.documents = documents;
        documents.PropertyChanged += Changed;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (!disposed) PropertyChanged?.Invoke(this, new(nameof(SelectedDocument)));
    }
    public void Open(string id) => OpenRequested?.Invoke(id);
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        documents.PropertyChanged -= Changed;
        OpenRequested = null;
    }
}

public sealed class DiagnosticsViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    private bool disposed;
    public string ActiveTitle => documents.ActiveDocument?.Title ?? "No active message";
    public IReadOnlyList<Hl7Diagnostic> Diagnostics => documents.ActiveDocument is { } document
        ? document.Message.Diagnostics.Concat(Hl7Validator.Validate(document.Message)).ToArray() : [];
    public event PropertyChangedEventHandler? PropertyChanged;
    public DiagnosticsViewModel(DocumentManager documents)
    {
        this.documents = documents;
        documents.PropertyChanged += Changed;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (disposed) return;
        PropertyChanged?.Invoke(this, new(nameof(ActiveTitle)));
        PropertyChanged?.Invoke(this, new(nameof(Diagnostics)));
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        documents.PropertyChanged -= Changed;
    }
}
