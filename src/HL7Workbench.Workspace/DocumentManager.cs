using System.Collections.ObjectModel;
using System.ComponentModel;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;


public sealed class DocumentManager : INotifyPropertyChanged, IDisposable
{
    private readonly ObservableCollection<MessageDocument> documents = [];
    private bool refreshingTitles;
    public ReadOnlyObservableCollection<MessageDocument> Documents { get; }
    public MessageDocument? ActiveDocument { get; private set; }
    public ActivityLog Activity { get; }
    public EditorPreferences Preferences { get; set; } = new();
    public event PropertyChangedEventHandler? PropertyChanged;
    public DocumentManager(ActivityLog? activity = null) { Documents = new(documents); Activity = activity ?? new(); }

    public void Open(MessageDocument document)
    {
        if (documents.Any(d => d.Id == document.Id)) throw new ArgumentException("Duplicate document identity.");
        document.IsEditMode = Preferences.StartInEditMode;
        documents.Add(document);
        if (document.ActivityNumber == 0) document.ActivityNumber = Activity.NextDocumentNumber();
        document.AttachActivity(Activity);
        document.PropertyChanged += ContentChanged;
        RefreshGeneratedTitles();
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

    public bool Close(string id, bool discardChanges = false)
    {
        var document = documents.SingleOrDefault(d => d.Id == id);
        if (document is null) return true;
        if (document.IsSaving || document.IsPreparingInsertion || document.IsDirty && !discardChanges) return false;
        documents.Remove(document);
        document.PropertyChanged -= ContentChanged;
        document.Dispose();
        RefreshGeneratedTitles();
        if (ActiveDocument != document) return true;
        ActiveDocument = documents.FirstOrDefault();
        PropertyChanged?.Invoke(this, new(nameof(ActiveDocument)));
        return true;
    }
    private void ContentChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MessageDocument.DisplayTitle)) RefreshGeneratedTitles();
        if (sender is MessageDocument document && e.PropertyName == "ActiveContent") document.ObserveActivity();
        if (ReferenceEquals(sender, ActiveDocument) && e.PropertyName is "ActiveContent" or nameof(MessageDocument.IsSaving) or nameof(MessageDocument.IsCapturingScript) or nameof(MessageDocument.IsPreparingInsertion))
            PropertyChanged?.Invoke(this, new("ActiveContent"));
    }
    private void RefreshGeneratedTitles()
    {
        if (refreshingTitles) return;
        refreshingTitles = true;
        try
        {
            // Reserve the bare name for saved/source documents. Only generated,
            // still-unsaved tabs receive suffixes; Title remains the Save As suggestion.
            var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var document in documents.Where(d => !d.HasGeneratedFileName)) used[document.Title] = 1;
            foreach (var document in documents.Where(d => d.HasGeneratedFileName))
            {
                var ordinal = used.GetValueOrDefault(document.Title) + 1;
                document.SetScriptTitleOrdinal(ordinal); used[document.Title] = ordinal;
            }
        }
        finally { refreshingTitles = false; }
    }
    public void Dispose() { foreach (var document in documents.ToArray()) { document.PropertyChanged -= ContentChanged; document.Dispose(); } documents.Clear(); ActiveDocument = null; }
}
