using System.Collections.ObjectModel;
using System.ComponentModel;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

public sealed partial class SearchViewModel : INotifyPropertyChanged, IDisposable
{
    private string value = "", fieldPath = "PID-3.1";
    private MessageSearchMode mode;
    private MessageSearchComparison comparison;
    private bool caseSensitive, applyingQuery, advancedTouched, disposed;
    private SearchBuilderMode builderMode;
    public SearchGroupViewModel RootGroup { get; private set; } = null!;
    private readonly ObservableCollection<SearchConditionViewModel> conditions = [];
    public ReadOnlyObservableCollection<SearchConditionViewModel> Conditions { get; }
    private readonly ApplicationPreferences? preferences;
    public double BuilderFraction { get; private set; }
    /// <summary>Called only after a completed splitter gesture, never during query editing or layout.</summary>
    public void SaveBuilderFraction(double fraction)
    {
        var layout = new SearchPaneLayout(AdvancedBuilderFraction: fraction).Validated();
        if (Math.Abs(BuilderFraction - layout.AdvancedBuilderFraction) < 0.001) return;
        BuilderFraction = layout.AdvancedBuilderFraction;
        preferences?.Apply(preferences.Current with { SearchLayout = layout });
    }
    public MessageSearchSession Session { get; }
    public string Value { get => value; set => SetQuery(ref this.value, value); }
    public string FieldPath { get => fieldPath; set => SetQuery(ref fieldPath, value); }
    public MessageSearchMode Mode { get => mode; set => SetQuery(ref mode, value); }
    public MessageSearchComparison Comparison { get => comparison; set => SetQuery(ref comparison, value); }
    public bool CaseSensitive { get => caseSensitive; set => SetQuery(ref caseSensitive, value); }
    public SearchCombination Combination { get => RootGroup.Combination; set => RootGroup.Combination = value; }
    public SearchBuilderMode BuilderMode
    {
        get => builderMode;
        set
        {
            if (builderMode == value) return;
            if (value == SearchBuilderMode.Advanced && !advancedTouched && !applyingQuery)
            {
                ReplaceConditions([new(Mode == MessageSearchMode.Text ? SearchTarget.MessageText : SearchTarget.Field,
                    Mode == MessageSearchMode.Text || Comparison == MessageSearchComparison.Contains ? SearchOperator.Contains : SearchOperator.Equals,
                    FieldPath, Value, CaseSensitive)]);
                advancedTouched = true;
            }
            builderMode = value; QueryChanged();
        }
    }
    public string? ValidationError => BuildQuery().ValidationError();
    public string ContextDescription { get; private set; } = "Active file, including unsaved edits.";
    internal async Task StartFromInspectorAsync(InspectionActionContext context, bool filter, CancellationToken token = default)
    {
        if (!context.IsCurrent || !ReferenceEquals(Session.Document, context.Document)) return;
        var value = filter ? await context.ReadValueAsync(token) : null;
        if (!context.IsCurrent || !ReferenceEquals(Session.Document, context.Document) || filter && value is null) return;
        var query = filter ? context.FilterQuery(value!) : context.FindQuery();
        if (query is null) return;
        ApplyQuery(query);
        ContextDescription = filter ? "Structural raw equality (case-sensitive); any matching occurrence/repetition in the entire active file, including unsaved edits." :
            "Structural path presence; all occurrences/repetitions in the entire active file, including unsaved edits. This is not literal text search.";
        SearchingTask = Session.StartAsync(query, token);
        Notify();
        await SearchingTask;
        if (filter && context.IsCurrent && ReferenceEquals(Session.Document, context.Document)) Session.FilterNavigator = true;
    }
    public Task SearchingTask { get; private set; } = Task.CompletedTask;
    public UiCommand SearchCommand { get; }
    public UiCommand CancelCommand { get; }
    public UiCommand ClearCommand { get; }
    public UiCommand OpenCommand { get; }
    public UiCommand AddConditionCommand { get; }
    public event Action? OpenRequested;
    public event Action? FocusRequested;
    public event PropertyChangedEventHandler? PropertyChanged;
    public SearchViewModel(DocumentManager documents, SearchLibrary? library = null, ApplicationPreferences? preferences = null)
    {
        this.preferences = preferences; BuilderFraction = preferences?.Current.SearchLayout?.Validated().AdvancedBuilderFraction ?? 0.6;
        Session = new(documents); Conditions = new(conditions); this.library = library;
        ReplaceConditions([new(SearchTarget.MessageText, SearchOperator.Contains)]);
        SearchCommand = new(() => SearchingTask = Session.StartAsync(BuildQuery()), () => Session.Document is not null && !Session.IsBusy);
        CancelCommand = new(() => { Session.Cancel(); return Task.CompletedTask; }, () => Session.IsBusy);
        // Clear discovery state, not the authored query or its saved definition.
        ClearCommand = new(() => { Session.Clear(); return Task.CompletedTask; });
        OpenCommand = new(() => { OpenRequested?.Invoke(); return Task.CompletedTask; });
        AddConditionCommand = new(() => { AddCondition(); return Task.CompletedTask; }, () => BuilderMode == SearchBuilderMode.Advanced && CanAddCondition);
        InitializeLibraryCommands();
        Session.PropertyChanged += Changed;
        LibraryTask = RefreshLibraryAsync();
    }
    public MessageSearchQuery BuildQuery() => BuilderMode == SearchBuilderMode.Advanced ?
        MessageSearchQuery.Advanced(RootGroup.Snapshot) : new(Mode, Value, CaseSensitive, FieldPath, Comparison);
    public bool CanAddCondition => conditions.Count < MessageSearchQuery.MaximumConditions;
    public bool CanAddGroup(SearchGroupViewModel parent) => RootGroup.Groups.Count() < MessageSearchQuery.MaximumGroups && Depth(RootGroup, parent, 1) is > 0 and < MessageSearchQuery.MaximumDepth;
    private static int Depth(SearchGroupViewModel group, SearchGroupViewModel target, int depth) => ReferenceEquals(group, target) ? depth : group.Children.OfType<SearchGroupViewModel>().Select(g => Depth(g, target, depth + 1)).FirstOrDefault(d => d > 0);
    public void AddCondition() => AddCondition(RootGroup);
    public void AddCondition(SearchGroupViewModel parent)
    {
        if (!CanAddCondition || !RootGroup.Groups.Contains(parent)) return;
        var row = new SearchConditionViewModel(new(SearchTarget.Field, SearchOperator.Contains, "PID-3.1"));
        row.PropertyChanged += ConditionChanged; parent.Items.Add(row); Reindex(); advancedTouched = true; QueryChanged();
    }
    public void AddGroup(SearchGroupViewModel parent)
    { if (!CanAddGroup(parent)) return; parent.Items.Add(new SearchGroupViewModel(SearchCombination.All, GroupChanged)); Reindex(); advancedTouched = true; QueryChanged(); }
    public void RemoveGroup(SearchGroupViewModel group)
    {
        if (ReferenceEquals(group, RootGroup)) return;
        var parent = RootGroup.Groups.FirstOrDefault(g => g.Items.Contains(group)); if (parent is null) return;
        foreach (var leaf in group.Leaves) leaf.PropertyChanged -= ConditionChanged;
        parent.Items.Remove(group); Reindex(); advancedTouched = true; QueryChanged();
    }
    public void RemoveCondition(SearchConditionViewModel condition)
    { var parent = RootGroup.Groups.FirstOrDefault(g => g.Items.Contains(condition)); if (parent is null) return; parent.Items.Remove(condition); condition.PropertyChanged -= ConditionChanged; Reindex(); advancedTouched = true; QueryChanged(); }
    private void ReplaceConditions(IEnumerable<SearchCondition> replacements)
    {
        foreach (var row in conditions) row.PropertyChanged -= ConditionChanged;
        RootGroup = CreateGroup(new(SearchCombination.All, replacements.Cast<SearchExpression>().ToArray())); Reindex();
    }
    private SearchGroupViewModel CreateGroup(SearchGroup group)
    {
        var result = new SearchGroupViewModel(group.Combination, GroupChanged);
        foreach (var child in group.Children)
            if (child is SearchCondition leaf) { var row = new SearchConditionViewModel(leaf); row.PropertyChanged += ConditionChanged; result.Items.Add(row); }
            else result.Items.Add(CreateGroup((SearchGroup)child));
        return result;
    }
    private void Reindex() { conditions.Clear(); foreach (var leaf in RootGroup.Leaves) conditions.Add(leaf); }
    private void GroupChanged() { advancedTouched = true; QueryChanged(); }
    private void SetQuery<T>(ref T field, T newValue)
    { if (EqualityComparer<T>.Default.Equals(field, newValue)) return; field = newValue; QueryChanged(); }
    private void ConditionChanged(object? sender, PropertyChangedEventArgs e) { advancedTouched = true; QueryChanged(); }
    private void QueryChanged()
    { if (applyingQuery) return; ContextDescription = "Active file, including unsaved edits."; Session.Clear(); Notify(); }
    private void ApplyQuery(MessageSearchQuery query)
    {
        applyingQuery = true;
        try
        {
            mode = query.Mode; value = query.Value; fieldPath = query.FieldPath; comparison = query.Comparison; caseSensitive = query.CaseSensitive;
            builderMode = query.BuilderMode;
            if (builderMode == SearchBuilderMode.Advanced)
            {
                foreach (var row in conditions) row.PropertyChanged -= ConditionChanged;
                RootGroup = CreateGroup(query.EffectiveRoot); Reindex(); advancedTouched = true;
            }
        }
        finally { applyingQuery = false; }
        QueryChanged();
    }
    internal void NotifyShown() => FocusRequested?.Invoke();
    private void Changed(object? sender, PropertyChangedEventArgs e) => Notify();
    private void Notify()
    {
        if (disposed) return;
        SearchCommand.Refresh(); CancelCommand.Refresh(); ClearCommand.Refresh(); AddConditionCommand.Refresh();
        RefreshLibraryCommands(); PropertyChanged?.Invoke(this, new(null));
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true; libraryLifetime.Cancel(); libraryLifetime.Dispose();
        Session.PropertyChanged -= Changed; Session.Dispose(); value = fieldPath = "";
        foreach (var condition in conditions) condition.PropertyChanged -= ConditionChanged;
        RootGroup.Items.Clear(); conditions.Clear(); loadedSearch = null; SavedSearches = []; OpenRequested = FocusRequested = null; PropertyChanged = null;
        ConfirmDeleteAsync = null;
    }
}
