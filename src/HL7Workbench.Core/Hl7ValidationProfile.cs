using System.Collections.ObjectModel;
using System.Text.Json.Serialization;

namespace HL7Workbench.Core;

/// <summary>Declarative rule over a field, component or subcomponent; null Max means unbounded by the profile, not by execution budgets.</summary>
public sealed record Hl7ValueRule
{
    public int Position { get; init; }
    public int Min { get; init; }
    public int? Max { get; init; } = 1;
    public string DataType { get; init; } = "ST";
    public string? DataTypePath { get; init; }
    public int? MaxLength { get; init; }
    public string? Table { get; init; }
    public IReadOnlyList<string> AllowedValues { get; init; } = [];
    public IReadOnlyList<Hl7ValueRule> Children { get; init; } = [];
    public string Provenance { get; init; } = "";
}

public sealed record Hl7SegmentRule
{
    public string Id { get; init; } = "";
    public IReadOnlyList<Hl7ValueRule> Fields { get; init; } = [];
    public bool AllowExtraFields { get; init; }
}

/// <summary>Ordered segment/group slot with stable sibling key. After places a new inherited slot explicitly.</summary>
public sealed record Hl7StructureRule
{
    public string Key { get; init; } = "";
    public string? Segment { get; init; }
    public int Min { get; init; }
    public int? Max { get; init; } = 1;
    public string? After { get; init; }
    public bool Choice { get; init; }
    public IReadOnlyList<Hl7StructureRule> Children { get; init; } = [];
    public string Provenance { get; init; } = "";
}

public sealed record Hl7ConditionalRule
{
    public string Id { get; init; } = "";
    public string IfPath { get; init; } = "";
    /// <summary>Null means present/non-null; otherwise compare the decoded literal using ordinal equality.</summary>
    public string? EqualsValue { get; init; }
    public string ThenPath { get; init; } = "";
    public bool Prohibit { get; init; }
    public Hl7DiagnosticSeverity Severity { get; init; } = Hl7DiagnosticSeverity.Error;
    public string Provenance { get; init; } = "";
}

[JsonConverter(typeof(JsonStringEnumConverter<Hl7UnknownSegmentPolicy>))]
public enum Hl7UnknownSegmentPolicy { Reject, Warn, Allow }

/// <summary>Versioned import model. Importers/catalog/resolver freeze and validate all collections before publication.</summary>
public sealed record Hl7ValidationProfile
{
    public int FormatVersion { get; init; } = 1;
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public string ProfileVersion { get; init; } = "1";
    public string Hl7Version { get; init; } = "2.5.1";
    public string MessageType { get; init; } = "";
    public string TriggerEvent { get; init; } = "";
    public string MessageStructure { get; init; } = "";
    public string? BaseProfile { get; init; }
    public string? TradingPartner { get; init; }
    public string? Organization { get; init; }
    public string Source { get; init; } = "";
    public Hl7UnknownSegmentPolicy UnknownZSegments { get; init; } = Hl7UnknownSegmentPolicy.Warn;
    public IReadOnlyList<Hl7StructureRule> Structure { get; init; } = [];
    public IReadOnlyList<Hl7SegmentRule> Segments { get; init; } = [];
    public IReadOnlyList<Hl7ConditionalRule> Conditions { get; init; } = [];
    public IReadOnlyList<string> CoverageWarnings { get; init; } = [];
    /// <summary>Explicitly out-of-scope branches. Their presence stops schema evaluation with Incomplete, not invented constraints.</summary>
    public IReadOnlyList<string> UnsupportedSegments { get; init; } = [];
}

/// <summary>New budgets for validation/profile input only; existing parser/editor/source limits are unchanged.</summary>
public static class Hl7ValidationLimits
{
    public const int ProfileBytes = 1024 * 1024;
    public const int ProfileDepth = 16;
    public const int ProfileRules = 10000;
    public const int Profiles = 128;
    public const int Diagnostics = 1000;
    public const int InspectedValueCharacters = 4096;
    public const int Repetitions = 10000;
    public const int Segments = 100000;
    public static TimeSpan ExecutionTime => TimeSpan.FromSeconds(5);

    /// <summary>Validates untrusted rules and returns a deep immutable snapshot. No inherited changes are resolved here.</summary>
    public static Hl7ValidationProfile Freeze(Hl7ValidationProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var count = 0; var unboundTable = false;
        void Rule(int depth) { if (++count > ProfileRules || depth > ProfileDepth) throw new ArgumentException("Profile rule count or nesting limit exceeded."); }
        static string Text(string? value, int max, bool empty = false)
        {
            if (value is null || value.Length > max || (!empty && string.IsNullOrWhiteSpace(value)) || value.Any(char.IsControl))
                throw new ArgumentException("Profile metadata is missing, too long or contains control characters.");
            return value;
        }
        static void Bounds(int min, int? max)
        { if (min < 0 || min > Repetitions || max < min || max > Repetitions) throw new ArgumentException("Profile cardinality is invalid or exceeds the rule limit."); }
        IReadOnlyList<Hl7ValueRule> Values(IReadOnlyList<Hl7ValueRule> rules, int depth)
        {
            ArgumentNullException.ThrowIfNull(rules);
            if (rules.Count > 0 && depth > 3) throw new ArgumentException("Value rules support fields, components and subcomponents only.");
            if (rules.Any(r => r is null)) throw new ArgumentException("Value rules cannot be null.");
            if (rules.Select(r => r.Position).Distinct().Count() != rules.Count) throw new ArgumentException("Duplicate field/component positions.");
            return Array.AsReadOnly(rules.Select(r =>
            {
                Rule(depth); Bounds(r.Min, r.Max);
                if (depth > 1 && (r.Min > 1 || r.Max is null or > 1)) throw new ArgumentException("Components/subcomponents cannot declare repetition cardinality.");
                if (r.Position is < 1 or > 1000 || r.MaxLength is < 1 or > 1000000) throw new ArgumentException("Invalid field position or length.");
                Text(r.DataType, 24); Text(r.Table ?? "", 32, true);
                if (r.DataTypePath is not null) _ = Hl7Path.Parse(r.DataTypePath);
                if (!Hl7ValidationCatalog.IsKnownDataType(r.DataType, profile.Hl7Version)) throw new ArgumentException("Unsupported datatype: " + r.DataType);
                if (r.Children is null || r.Children.Any(c => c is null)) throw new ArgumentException("Component rule collections cannot contain nulls.");
                var components = Hl7ValidationCatalog.Components(r.DataType, profile.Hl7Version);
                if (r.Children.Count > 0 && (components is null || r.Children.Any(c => c.Position > components.Count)))
                    throw new ArgumentException("Component constraints must target declared positions of a supported composite datatype.");
                if (r.AllowedValues is null || r.AllowedValues.Count > 1000) throw new ArgumentException("Value-set limit exceeded.");
                if (r.Table is not null && r.AllowedValues.Count == 0) unboundTable = true;
                var table = r.Table is not null && r.Table.Length <= 4 && r.Table.All(char.IsAsciiDigit) ? r.Table.PadLeft(4, '0') : r.Table;
                return r with { Table = table, Children = Values(r.Children, depth + 1), AllowedValues = Array.AsReadOnly(r.AllowedValues.Select(v => Text(v, 256, true)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()),
                    Provenance = Text(string.IsNullOrEmpty(r.Provenance) ? profile.Id : r.Provenance, 512) };
            }).OrderBy(r => r.Position).ToArray());
        }
        IReadOnlyList<Hl7StructureRule> Structure(IReadOnlyList<Hl7StructureRule> rules, int depth)
        {
            ArgumentNullException.ThrowIfNull(rules);
            if (rules.Any(r => r is null || r.Children is null)) throw new ArgumentException("Structure rules/children cannot be null.");
            if (rules.Select(r => r.Key).Distinct(StringComparer.Ordinal).Count() != rules.Count) throw new ArgumentException("Duplicate structure slot keys.");
            return Array.AsReadOnly(rules.Select(r =>
            {
                Rule(depth); Bounds(r.Min, r.Max); Text(r.Key, 128); Text(r.After ?? "", 128, true);
                if (r.Segment is not null) _ = new Hl7Path(r.Segment, 1);
                if ((r.Segment is null) == (r.Children.Count == 0)) throw new ArgumentException("A structure rule must be a segment or a nonempty group.");
                if (r.Segment is not null && r.Choice) throw new ArgumentException("Only a group can declare alternatives.");
                return r with { Children = Structure(r.Children, depth + 1), Provenance = Text(string.IsNullOrEmpty(r.Provenance) ? profile.Id : r.Provenance, 512) };
            }).ToArray());
        }
        if (profile.FormatVersion != 1 || !Enum.IsDefined(profile.UnknownZSegments)) throw new ArgumentException("Unsupported profile format version or Z policy.");
        Text(profile.Id, 128); Text(profile.Name, 160); Text(profile.Description, 2048, true); Text(profile.ProfileVersion, 64);
        Text(profile.Hl7Version, 16); Text(profile.MessageType, 3); Text(profile.TriggerEvent, 3); Text(profile.MessageStructure, 32, true);
        if (!Hl7ValidationCatalog.IsSupportedVersion(profile.Hl7Version)) throw new ArgumentException("This validation-engine release supports HL7 v2.5.1 profiles only.");
        foreach (var text in new[] { profile.BaseProfile, profile.TradingPartner, profile.Organization, profile.Source }) Text(text ?? "", 512, true);
        if (profile.Segments is null || profile.Conditions is null || profile.CoverageWarnings is null || profile.UnsupportedSegments is null || profile.UnsupportedSegments.Count > 128 || profile.Segments.Any(s => s is null) || profile.Conditions.Any(c => c is null))
            throw new ArgumentException("Profile rule collections cannot contain nulls.");
        if (profile.Segments.Select(s => s.Id).Distinct().Count() != profile.Segments.Count) throw new ArgumentException("Duplicate segment definitions.");
        var segments = profile.Segments.Select(s => { Rule(1); _ = new Hl7Path(s.Id, 1); return s with { Fields = Values(s.Fields, 1) }; }).ToArray();
        var conditions = profile.Conditions.Select(c =>
        {
            Rule(1); Text(c.Id, 128); _ = Hl7Path.Parse(c.IfPath); _ = Hl7Path.Parse(c.ThenPath); Text(c.EqualsValue ?? "", 256, true);
            if (!Enum.IsDefined(c.Severity)) throw new ArgumentException("Unsupported conditional severity.");
            return c with { Provenance = Text(string.IsNullOrEmpty(c.Provenance) ? profile.Id : c.Provenance, 512) };
        }).ToArray();
        if (conditions.Select(c => c.Id).Distinct().Count() != conditions.Length) throw new ArgumentException("Duplicate conditional rule IDs.");
        var coverage = profile.CoverageWarnings.Select(w => Text(w, 512));
        foreach (var segment in profile.UnsupportedSegments) _ = new Hl7Path(segment, 1);
        if (profile.UnsupportedSegments.Count > 0) coverage = coverage.Append("Some message branches are explicitly deferred; their presence yields Incomplete without structure/content evaluation.");
        if (unboundTable) coverage = coverage.Append("Declared table bindings without supplied membership sets cannot establish coded-value membership.");
        return profile with { Structure = Structure(profile.Structure, 1), Segments = Array.AsReadOnly(segments), Conditions = Array.AsReadOnly(conditions),
            CoverageWarnings = Array.AsReadOnly(coverage.Distinct().ToArray()), UnsupportedSegments = Array.AsReadOnly(profile.UnsupportedSegments.Distinct().Order(StringComparer.Ordinal).ToArray()) };
    }
}
