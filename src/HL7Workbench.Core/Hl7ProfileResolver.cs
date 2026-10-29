namespace HL7Workbench.Core;

public sealed record Hl7ResolvedProfile(Hl7ValidationProfile Profile, IReadOnlyList<string> Lineage);

/// <summary>Deterministic, narrowing-only inheritance. Built-ins are never mutated and relaxations are rejected.</summary>
public static class Hl7ProfileResolver
{
    public static Hl7ResolvedProfile Resolve(string id, IEnumerable<Hl7ValidationProfile> profiles)
    {
        var available = profiles.Take(Hl7ValidationLimits.Profiles + 8).ToArray();
        if (available.Length > Hl7ValidationLimits.Profiles + 7 || available.Select(p => p.Id).Distinct().Count() != available.Length)
            throw new ArgumentException("Profile catalog is oversized or has duplicate identifiers.");
        var byId = available.ToDictionary(p => p.Id, StringComparer.Ordinal); var path = new HashSet<string>(StringComparer.Ordinal); var lineage = new List<string>();
        Hl7ValidationProfile Visit(string requested)
        {
            if (path.Count >= Hl7ValidationLimits.ProfileDepth || !path.Add(requested)) throw new ArgumentException("Profile inheritance is cyclic or too deep.");
            if (!byId.TryGetValue(requested, out var raw)) throw new ArgumentException("Base profile is unavailable: " + requested);
            var child = Hl7ValidationLimits.Freeze(raw);
            if (child.BaseProfile is { } parent)
            {
                var basis = Visit(parent);
                if (child.Hl7Version != basis.Hl7Version || child.MessageType != basis.MessageType || child.TriggerEvent != basis.TriggerEvent || child.MessageStructure != basis.MessageStructure)
                    throw new ArgumentException("Inherited profiles must retain the parent's version, message, trigger and structure.");
                if ((int)child.UnknownZSegments > (int)basis.UnknownZSegments) throw new ArgumentException("A derived profile cannot weaken the unknown-Z policy.");
                child = child with
                {
                    BaseProfile = null, Structure = MergeStructure(basis.Structure, child.Structure),
                    Segments = MergeSegments(basis.Segments, child.Segments),
                    Conditions = MergeConditions(basis.Conditions, child.Conditions),
                    CoverageWarnings = Array.AsReadOnly(basis.CoverageWarnings.Concat(child.CoverageWarnings).Distinct().ToArray()),
                    UnsupportedSegments = Array.AsReadOnly(basis.UnsupportedSegments.Concat(child.UnsupportedSegments).Distinct().ToArray())
                };
            }
            path.Remove(requested); lineage.Add(requested); return Hl7ValidationLimits.Freeze(child);
        }
        var result = Visit(id);
        var defined = result.Segments.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        void Check(IReadOnlyList<Hl7StructureRule> rules)
        { foreach (var rule in rules) { if (rule.Segment is { } segment && !defined.Contains(segment)) throw new ArgumentException("Structure references an undefined segment: " + segment); Check(rule.Children); } }
        Check(result.Structure);
        return new(result, Array.AsReadOnly(lineage.ToArray()));
    }
    private static void Narrow(int minimum, int? maximum, int nextMinimum, int? nextMaximum)
    { if (nextMinimum < minimum || maximum is { } max && (nextMaximum is null || nextMaximum > max)) throw new ArgumentException("An inherited rule cannot relax cardinality or required usage."); }
    private static IReadOnlyList<Hl7StructureRule> MergeStructure(IReadOnlyList<Hl7StructureRule> basis, IReadOnlyList<Hl7StructureRule> additions)
    {
        var result = basis.ToList();
        foreach (var next in additions)
        {
            var index = result.FindIndex(r => r.Key == next.Key);
            if (index >= 0)
            {
                var current = result[index]; Narrow(current.Min, current.Max, next.Min, next.Max);
                if (current.Segment != next.Segment || current.Choice != next.Choice || next.After is not null) throw new ArgumentException("Inherited structure slots cannot change identity, alternatives or ordering.");
                if (current.Choice && next.Children.Any(n => !current.Children.Any(b => b.Key == n.Key))) throw new ArgumentException("A derived choice cannot add alternatives to its base constraint.");
                result[index] = next with { Children = MergeStructure(current.Children, next.Children) };
            }
            else
            {
                if (next.After is null) throw new ArgumentException("A new inherited structure slot requires an explicit After sibling key.");
                var anchor = result.FindIndex(r => r.Key == next.After); if (anchor < 0) throw new ArgumentException("Structure insertion anchor is unavailable: " + next.After);
                result.Insert(anchor + 1, next with { After = null });
            }
        }
        return Array.AsReadOnly(result.ToArray());
    }
    private static IReadOnlyList<Hl7SegmentRule> MergeSegments(IReadOnlyList<Hl7SegmentRule> basis, IReadOnlyList<Hl7SegmentRule> additions)
    {
        var result = basis.ToList();
        foreach (var next in additions)
        {
            var index = result.FindIndex(r => r.Id == next.Id);
            if (index < 0) result.Add(next);
            else
            {
                var current = result[index];
                if (!current.AllowExtraFields && next.AllowExtraFields) throw new ArgumentException("A derived segment cannot allow previously undefined fields.");
                if (!current.AllowExtraFields && next.Fields.Any(f => !current.Fields.Any(b => b.Position == f.Position)))
                    throw new ArgumentException("A derived segment cannot introduce fields prohibited by the closed base definition.");
                result[index] = next with { Fields = MergeValues(current.Fields, next.Fields) };
            }
        }
        return Array.AsReadOnly(result.ToArray());
    }
    private static IReadOnlyList<Hl7ValueRule> MergeValues(IReadOnlyList<Hl7ValueRule> basis, IReadOnlyList<Hl7ValueRule> additions)
    {
        var result = basis.ToList();
        foreach (var next in additions)
        {
            var index = result.FindIndex(r => r.Position == next.Position);
            if (index < 0) result.Add(next);
            else
            {
                var current = result[index]; Narrow(current.Min, current.Max, next.Min, next.Max);
                if (current.DataType != next.DataType || current.MaxLength is { } length && next.MaxLength > length ||
                    next.DataTypePath is not null && next.DataTypePath != current.DataTypePath ||
                    next.Table is not null && current.Table is not null && next.Table != current.Table)
                    throw new ArgumentException("Inherited datatype, table or maximum-length constraints conflict.");
                if (current.AllowedValues.Count > 0 && next.AllowedValues.Any(v => !current.AllowedValues.Contains(v, StringComparer.Ordinal)))
                    throw new ArgumentException("Inherited value sets can only be narrowed.");
                result[index] = next with { MaxLength = next.MaxLength ?? current.MaxLength, DataTypePath = next.DataTypePath ?? current.DataTypePath, Table = next.Table ?? current.Table,
                    AllowedValues = next.AllowedValues.Count > 0 ? next.AllowedValues : current.AllowedValues, Children = MergeValues(current.Children, next.Children) };
            }
        }
        return Array.AsReadOnly(result.OrderBy(r => r.Position).ToArray());
    }
    private static IReadOnlyList<Hl7ConditionalRule> MergeConditions(IReadOnlyList<Hl7ConditionalRule> basis, IReadOnlyList<Hl7ConditionalRule> additions)
    {
        if (additions.Any(n => basis.Any(b => b.Id == n.Id))) throw new ArgumentException("Inherited conditional rule IDs cannot be replaced.");
        return Array.AsReadOnly(basis.Concat(additions).ToArray());
    }
}
