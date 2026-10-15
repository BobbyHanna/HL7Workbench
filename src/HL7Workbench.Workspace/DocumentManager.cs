using System.Collections.ObjectModel;
using System.ComponentModel;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;


public sealed class DocumentManager : INotifyPropertyChanged, IDisposable
{
    private readonly ObservableCollection<MessageDocument> documents = [];
    public ReadOnlyObservableCollection<MessageDocument> Documents { get; }
    public MessageDocument? ActiveDocument { get; private set; }
    public ActivityLog Activity { get; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public DocumentManager(ActivityLog? activity = null) { Documents = new(documents); Activity = activity ?? new(); }

    public void Open(MessageDocument document)
    {
        if (documents.Any(d => d.Id == document.Id)) throw new ArgumentException("Duplicate document identity.");
        documents.Add(document);
        if (document.ActivityNumber == 0) document.ActivityNumber = Activity.NextDocumentNumber();
        document.AttachActivity(Activity);
        document.PropertyChanged += ContentChanged;
        Activate(document.Id);
    }

    public void Activate(string id)
    {
        var document = documents.Single(d => d.Id == id);
        if (ReferenceEquals(ActiveDocument, document)) return;
        ActiveDocument?.CancelScan();
        ActiveDocument = document;
        PropertyChanged?.Invoke(this, new(nameof(ActiveDocument)));
    }

    public void Close(string id)
    {
        var document = documents.SingleOrDefault(d => d.Id == id);
        if (document is null) return;
        documents.Remove(document);
        document.PropertyChanged -= ContentChanged;
        document.Dispose();
        if (ActiveDocument != document) return;
        ActiveDocument = documents.FirstOrDefault();
        PropertyChanged?.Invoke(this, new(nameof(ActiveDocument)));
    }
    private void ContentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is MessageDocument document && e.PropertyName == "ActiveContent") document.ObserveActivity();
        if (ReferenceEquals(sender, ActiveDocument) && e.PropertyName == "ActiveContent") PropertyChanged?.Invoke(this, new("ActiveContent"));
    }
    public void Dispose() { foreach (var document in documents.ToArray()) Close(document.Id); }
}
