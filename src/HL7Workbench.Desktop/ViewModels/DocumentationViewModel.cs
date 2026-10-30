using System.ComponentModel;
using HL7Workbench.Desktop.Documentation;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Session-only navigation; deliberately has no document/source or execution-service owner.</summary>
internal sealed class DocumentationViewModel : INotifyPropertyChanged, IDisposable
{
    internal const int MaximumHistory = 64;
    private sealed record Location(string Query, string Topic, string Anchor);
    private readonly DocumentationCatalog catalog;
    private readonly List<Location> history = [];
    private string query = "";
    private bool disposed;
    internal DocumentationViewModel() : this(DocumentationCatalog.Current) { }
    internal DocumentationViewModel(DocumentationCatalog catalog)
    { this.catalog = catalog; Topic = catalog.Topics[0]; Results = catalog.Search(""); Feedback = "Offline documentation · Contents"; }
    public event PropertyChangedEventHandler? PropertyChanged;
    internal event Action? Shown;
    internal event Action? ScriptApiRequested;
    internal HelpTopic Topic { get; private set; }
    internal string Anchor { get; private set; } = "";
    internal IReadOnlyList<HelpSearchResult> Results { get; private set; }
    internal string Feedback { get; private set; }
    internal string Query
    {
        get => query;
        set
        {
            if (disposed) return;
            value = (value ?? "")[..Math.Min(value?.Length ?? 0, DocumentationCatalog.MaximumQueryCharacters)];
            if (query == value) return;
            query = value; Results = catalog.Search(value);
            Feedback = value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > DocumentationCatalog.MaximumTerms
                ? "Use at most 16 search terms." : Results.Count == 0 ? "No documentation topics match." : $"Offline documentation · {Results.Count} topic(s)";
            Changed();
        }
    }
    internal bool CanBack => !disposed && history.Count > 0;
    internal void NotifyShown() { if (!disposed) Shown?.Invoke(); }
    internal bool Navigate(string id, string anchor = "")
    {
        if (disposed || !catalog.HasAnchor(id, anchor)) return false;
        if (Topic.Id == id && Anchor == anchor) return true;
        Remember(); Topic = catalog.Find(id)!; Anchor = anchor; Changed(); return true;
    }
    internal void Contents()
    {
        if (disposed) return;
        Remember(); query = ""; Results = catalog.Search(""); Topic = catalog.Topics[0]; Anchor = "";
        Feedback = "Offline documentation · Contents"; Changed();
    }
    internal void Back()
    {
        if (!CanBack) return;
        var location = history[^1]; history.RemoveAt(history.Count - 1);
        query = location.Query; Results = catalog.Search(query); Topic = catalog.Find(location.Topic)!; Anchor = location.Anchor;
        Feedback = "Offline documentation · Back"; Changed();
    }
    internal void Follow(HelpLink link)
    {
        if (disposed) return;
        if (link.Kind == "topic") Navigate(link.Target, link.Anchor);
        else if (link.Kind == "api") ScriptApiRequested?.Invoke();
        else { Feedback = (link.Kind == "repository" ? "Repository reference (not bundled): " : "External reference (not opened): ") + link.Target; Changed(); }
    }
    private void Remember()
    { if (history.Count == MaximumHistory) history.RemoveAt(0); history.Add(new(query, Topic.Id, Anchor)); }
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
    public void Dispose()
    { disposed = true; query = ""; history.Clear(); Shown = null; ScriptApiRequested = null; PropertyChanged = null; }
}
