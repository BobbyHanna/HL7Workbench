using System.Collections.ObjectModel;
using System.ComponentModel;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed record MessageDocument(string Id, string Title, Hl7Message Message);

public sealed class DocumentManager : INotifyPropertyChanged
{
    private readonly ObservableCollection<MessageDocument> documents = [];
    public ReadOnlyObservableCollection<MessageDocument> Documents { get; }
    public MessageDocument? ActiveDocument { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public DocumentManager() => Documents = new(documents);

    public void Open(MessageDocument document)
    {
        if (documents.Any(d => d.Id == document.Id)) throw new ArgumentException("Duplicate document identity.");
        documents.Add(document);
        Activate(document.Id);
    }

    public void Activate(string id)
    {
        var document = documents.Single(d => d.Id == id);
        if (ReferenceEquals(ActiveDocument, document)) return;
        ActiveDocument = document;
        PropertyChanged?.Invoke(this, new(nameof(ActiveDocument)));
    }

    public void Close(string id)
    {
        var document = documents.SingleOrDefault(d => d.Id == id);
        if (document is null) return;
        documents.Remove(document);
        if (ActiveDocument != document) return;
        ActiveDocument = documents.FirstOrDefault();
        PropertyChanged?.Invoke(this, new(nameof(ActiveDocument)));
    }
}
