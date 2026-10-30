using System.ComponentModel;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Small offline help navigation state, retained across Dock view reconstruction; contains no document/source references.</summary>
public sealed class ScriptApiReferenceViewModel : INotifyPropertyChanged
{
    public const string AllCategories = "All categories";
    private sealed record Location(string Query, string Category, string? EntryId, string[]? Topics, string Notice);
    private readonly List<Location> history = [];
    private readonly ScriptApiReference reference = ScriptApiReference.Current;
    private string query = "", category = AllCategories;
    private ScriptApiEntry? selected;
    private string[]? contextTopics;
    public string LookupNotice { get; private set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Shown;
    public Func<string, Task<bool>> CreateScriptAsync { get; set; } = _ => Task.FromResult(false);
    public IReadOnlyList<string> Categories { get; }
    public IReadOnlyList<ScriptApiEntry> Results { get; private set; }
    public string VersionText => $"Offline · reference {reference.Version} · scripting API {reference.ApiVersion}";
    public string Query { get => query; set { value = (value ?? "")[..Math.Min(value?.Length ?? 0, 256)]; if (query == value) return; Remember(); query = value; Filter(); } }
    public string Category { get => category; set { if (!Categories.Contains(value) || category == value) return; Remember(); category = value; Filter(); } }
    public ScriptApiEntry? Selected
    {
        get => selected;
        set { if (selected == value || value is not null && !Results.Contains(value)) return; Remember(); selected = value; Changed(); }
    }
    public bool CanGoBack => history.Count > 0;
    public bool CanCreate => selected?.TemplateName is not null;
    public ScriptApiReferenceViewModel()
    {
        Categories = new[] { AllCategories }.Concat(reference.Categories).ToArray();
        Results = reference.Entries; selected = Results[0];
    }
    public void NotifyShown() => Shown?.Invoke();
    /// <summary>Navigate from compiler-only help without keeping any script text or document ownership.</summary>
    public void OpenTopics(IReadOnlyList<string> topics, string notice)
    {
        Remember(); query = ""; category = AllCategories;
        contextTopics = topics.Where(id => reference.Entries.Any(e => e.Id == id)).Distinct().ToArray();
        LookupNotice = notice; ApplyFilter(); selected = Results.FirstOrDefault(); Changed();
    }
    public void OpenRelated(string id)
    {
        if (selected?.Related.Contains(id) != true) return;
        OpenTopics([id], "Related API topic.");
    }
    private void Filter()
    {
        contextTopics = null; LookupNotice = ""; ApplyFilter();
        if (selected is null || !Results.Contains(selected)) selected = Results.FirstOrDefault();
        Changed();
    }
    private void Remember()
    {
        if (history.Count == 64) history.RemoveAt(0);
        history.Add(new(query, category, selected?.Id, contextTopics, LookupNotice));
    }
    public void Back()
    {
        if (history.Count == 0) return;
        var previous = history[^1]; history.RemoveAt(history.Count - 1);
        query = previous.Query; category = previous.Category;
        contextTopics = previous.Topics; LookupNotice = previous.Notice; ApplyFilter();
        selected = Results.FirstOrDefault(e => e.Id == previous.EntryId) ?? Results.FirstOrDefault(); Changed();
    }
    private void ApplyFilter() => Results = contextTopics is null ? reference.Search(query, category == AllCategories ? null : category) :
        reference.Entries.Where(e => contextTopics.Contains(e.Id)).ToArray();
    public Task<bool> CreateSelectedAsync() => CanCreate ? CreateScriptAsync(selected!.Id) : Task.FromResult(false);
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
}
