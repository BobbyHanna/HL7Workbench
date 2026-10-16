using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Session-only builder structure. Expansion is presentation state and never changes the query.</summary>
public sealed class SearchGroupViewModel
{
    private SearchCombination combination;
    private readonly Action changed;
    internal readonly List<object> Items = [];
    public IReadOnlyList<object> Children => Items;
    public bool IsExpanded { get; set; } = true;
    public SearchCombination Combination { get => combination; set { if (combination == value) return; combination = value; changed(); } }
    internal SearchGroupViewModel(SearchCombination combination, Action changed) { this.combination = combination; this.changed = changed; }
    public SearchGroup Snapshot => new(Combination, Items.Select(c => c is SearchConditionViewModel leaf ? (SearchExpression)leaf.Snapshot : ((SearchGroupViewModel)c).Snapshot).ToArray());
    internal IEnumerable<SearchGroupViewModel> Groups => new[] { this }.Concat(Items.OfType<SearchGroupViewModel>().SelectMany(g => g.Groups));
    internal IEnumerable<SearchConditionViewModel> Leaves => Items.SelectMany(c => c is SearchConditionViewModel leaf ? [leaf] : ((SearchGroupViewModel)c).Leaves);
}
