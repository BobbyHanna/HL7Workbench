using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public enum SearchBuilderMode { Simple, Advanced }
public enum SearchTarget { MessageText, Field, Segment }
public enum SearchOperator { Contains, Equals, DoesNotContain, DoesNotEqual, Exists, DoesNotExist }
public enum SearchCombination { All, Any }

/// <summary>A condition or Boolean group in a validated search tree.</summary>
public abstract record SearchExpression;

/// <summary>A bounded Boolean group. Root depth is one; empty groups are invalid.</summary>
public sealed record SearchGroup(SearchCombination Combination, IReadOnlyList<SearchExpression> Children) : SearchExpression
{
    public override string ToString() => "Search group";
}

/// <summary>A literal comparison or structural presence test. Stringification excludes values.</summary>
public sealed record SearchCondition(SearchTarget Target, SearchOperator Operator, string Path = "", string Value = "", bool CaseSensitive = false) : SearchExpression
{
    public override string ToString() => "Search condition";
    public bool NeedsValue => Operator is not (SearchOperator.Exists or SearchOperator.DoesNotExist);
    public string? ValidationError()
    {
        if (!Enum.IsDefined(Target) || !Enum.IsDefined(Operator)) return "Choose a supported target and operator.";
        if (!OperatorsFor(Target).Contains(Operator)) return "Choose an operator supported by this target.";
        var exactField = Target == SearchTarget.Field && Operator is SearchOperator.Equals or SearchOperator.DoesNotEqual;
        if (NeedsValue && (Value is null || !exactField && string.IsNullOrWhiteSpace(Value) || Value.Length > MessageSearchQuery.MaximumQueryCharacters))
            return "Enter a nonblank literal value of at most 1,024 characters.";
        if (Target == SearchTarget.MessageText) return null;
        try
        {
            if (Target == SearchTarget.Segment) _ = new Hl7Path((Path ?? "").Trim(), 1);
            else
            {
                var path = Hl7Path.Parse((Path ?? "").Trim());
                if (Path?.Contains('[') == true)
                    return "Use an unindexed field/component/subcomponent path; all segments and repetitions are searched.";
            }
        }
        catch (Exception e) when (e is ArgumentException or FormatException)
        { return Target == SearchTarget.Segment ? "Enter a three-character uppercase segment, for example PV1 (no field number)." : "Enter a field path such as PV1-1 or PID-3.1. For PV1 alone, choose HL7 Segment."; }
        return null;
    }
    public static IReadOnlyList<SearchOperator> OperatorsFor(SearchTarget target) => target switch
    {
        SearchTarget.MessageText => [SearchOperator.Contains, SearchOperator.DoesNotContain],
        SearchTarget.Field => [SearchOperator.Contains, SearchOperator.Equals, SearchOperator.DoesNotContain, SearchOperator.DoesNotEqual, SearchOperator.Exists, SearchOperator.DoesNotExist],
        SearchTarget.Segment => [SearchOperator.Exists, SearchOperator.DoesNotExist],
        _ => []
    };
    internal SearchCondition Canonical() => this with
    {
        Path = Target == SearchTarget.MessageText ? "" : (Path ?? "").Trim(),
        Value = NeedsValue ? Value : "", CaseSensitive = NeedsValue && CaseSensitive
    };
}

public sealed partial record MessageSearchQuery
{
    public const int CurrentVersion = 2;
    public const int MaximumConditions = 16;
    public const int MaximumDepth = 4;
    public const int MaximumGroups = 16;
    public int Version { get; init; } = CurrentVersion;
    public SearchBuilderMode BuilderMode { get; init; }
    private readonly SearchCombination combination;
    private readonly IReadOnlyList<SearchCondition> conditions = [];
    public SearchCombination Combination { get => Root?.Combination ?? combination; init => combination = value; }
    // Compatibility projection for callers of the original flat builder. The tree is authoritative.
    public IReadOnlyList<SearchCondition> Conditions { get => Root is null ? conditions : Leaves(Root).ToArray(); init => conditions = value; }
    public SearchGroup? Root { get; init; }

    public static MessageSearchQuery Advanced(IEnumerable<SearchCondition> conditions, SearchCombination combination = SearchCombination.All) =>
        new(MessageSearchMode.Text, "") { BuilderMode = SearchBuilderMode.Advanced, Combination = combination,
            Conditions = Array.AsReadOnly(conditions.Take(MaximumConditions + 1).ToArray()) };
    public static MessageSearchQuery Advanced(SearchGroup root) => new(MessageSearchMode.Text, "")
        { BuilderMode = SearchBuilderMode.Advanced, Root = root };
    public SearchGroup EffectiveRoot => Root ?? new(Combination, Conditions?.Cast<SearchExpression>().ToArray()!);
    internal IReadOnlyList<SearchCondition> EffectiveConditions() => BuilderMode == SearchBuilderMode.Advanced ? Leaves(EffectiveRoot).ToArray() :
        [new(Mode == MessageSearchMode.Text ? SearchTarget.MessageText : SearchTarget.Field,
            Mode == MessageSearchMode.Text || Comparison == MessageSearchComparison.Contains ? SearchOperator.Contains : SearchOperator.Equals,
            FieldPath, Value, CaseSensitive)];
    internal static IEnumerable<SearchCondition> Leaves(SearchGroup group) => group.Children.SelectMany(c => c is SearchCondition leaf ? [leaf] : Leaves((SearchGroup)c));
    internal static string? ValidateTree(SearchGroup root)
    {
        var leaves = 0; var groups = 0;
        string? Visit(SearchExpression? node, int depth)
        {
            if (node is SearchCondition condition)
            {
                if (++leaves > MaximumConditions) return "Use between 1 and 16 conditions.";
                return condition.ValidationError() is { } error ? $"Condition {leaves}: {error}" : null;
            }
            if (node is not SearchGroup group || !Enum.IsDefined(group.Combination)) return "Choose a valid group and ALL/ANY operator.";
            if (depth > MaximumDepth) return "Groups support at most 4 levels including the root.";
            if (++groups > MaximumGroups) return "Use at most 16 groups including the root.";
            if (group.Children is null || group.Children.Count == 0) return "Empty group: add a condition or group; use between 1 and 16 conditions.";
            if (group.Children.Count > MaximumConditions + MaximumGroups) return "Group size limit exceeded.";
            foreach (var child in group.Children)
                if (Visit(child, child is SearchGroup ? depth + 1 : depth) is { } error) return error;
            return null;
        }
        return Visit(root, 1);
    }
    /// <summary>Validates and deep-copies the bounded tree before asynchronous work outlives its editor.</summary>
    public MessageSearchQuery Snapshot()
    {
        if (ValidationError() is { } error) throw new ArgumentException(error, "query");
        if (BuilderMode == SearchBuilderMode.Simple) return this;
        SearchGroup Copy(SearchGroup group) => new(group.Combination, Array.AsReadOnly(group.Children.Select(c => c is SearchCondition leaf ? (SearchExpression)leaf.Canonical() : Copy((SearchGroup)c)).ToArray()));
        return Root is null ? this with { Conditions = Array.AsReadOnly(Conditions.Select(c => c.Canonical()).ToArray()) } : this with { Root = Copy(Root), Conditions = [] };
    }
}
