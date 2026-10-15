using System.ComponentModel;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Follows active-document context; focusing the tool never activates another document.</summary>
public sealed class InspectionToolViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DocumentManager documents;
    public bool IsNavigator { get; }
    public MessageDocument? Document => documents.ActiveDocument;
    public event PropertyChangedEventHandler? PropertyChanged;
    public InspectionToolViewModel(DocumentManager documents, bool navigator)
    { this.documents = documents; IsNavigator = navigator; documents.PropertyChanged += Changed; }
    private void Changed(object? sender, PropertyChangedEventArgs e) => PropertyChanged?.Invoke(this, new(nameof(Document)));
    public void Dispose() { documents.PropertyChanged -= Changed; PropertyChanged = null; }
}
