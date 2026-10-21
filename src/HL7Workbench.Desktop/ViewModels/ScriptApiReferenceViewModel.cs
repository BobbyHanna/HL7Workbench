using System.ComponentModel;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Small offline help navigation state, retained across Dock view reconstruction; contains no document/source references.</summary>
public sealed class ScriptApiReferenceViewModel : INotifyPropertyChanged
{
    public const string AllCategories = "All categories";
    private sealed record Location(string Query, string Category, string? EntryId);
    private readonly List<Location> history = [];
    private readonly ScriptApiReference reference = ScriptApiReference.Current;
    private string query = "", category = AllCategories;
    private ScriptApiEntry? selected;
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
    private void Filter()
    {
        Results = reference.Search(query, category == AllCategories ? null : category);
        if (selected is null || !Results.Contains(selected)) selected = Results.FirstOrDefault();
        Changed();
    }
    private void Remember()
    {
        if (history.Count == 64) history.RemoveAt(0);
        history.Add(new(query, category, selected?.Id));
    }
    public void Back()
    {
        if (history.Count == 0) return;
        var previous = history[^1]; history.RemoveAt(history.Count - 1);
        query = previous.Query; category = previous.Category;
        Results = reference.Search(query, category == AllCategories ? null : category);
        selected = Results.FirstOrDefault(e => e.Id == previous.EntryId) ?? Results.FirstOrDefault(); Changed();
    }
    public Task<bool> CreateSelectedAsync() => CanCreate ? CreateScriptAsync(selected!.Id) : Task.FromResult(false);
    private void Changed() => PropertyChanged?.Invoke(this, new(null));
}
