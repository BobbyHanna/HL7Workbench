using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace HL7Workbench.Core;

public sealed record Hl7SchemaDiagnostic(string RuleId, Hl7DiagnosticSeverity Severity, string Message,
    string? Path, int SegmentOccurrence, CharacterRange Range, bool IsBoundary, string ProfileId, string Provenance,
    bool IsParsingDiagnostic = false);

public sealed record Hl7SchemaValidationResult(IReadOnlyList<Hl7SchemaDiagnostic> Diagnostics, bool Parsed, bool Complete,
    string ProfileId, int EngineVersion)
{
    public int Errors => Diagnostics.Count(d => d.Severity == Hl7DiagnosticSeverity.Error);
    public int Warnings => Diagnostics.Count(d => d.Severity == Hl7DiagnosticSeverity.Warning);
    public int Information => Diagnostics.Count(d => d.Severity == Hl7DiagnosticSeverity.Information);
}

public sealed record Hl7SchemaValidationOptions
{
    public int MaximumDiagnostics { get; init; } = Hl7ValidationLimits.Diagnostics;
    public int MaximumValueCharacters { get; init; } = Hl7ValidationLimits.InspectedValueCharacters;
    public int MaximumSteps { get; init; } = 1000000;
    public TimeSpan MaximumTime { get; init; } = Hl7ValidationLimits.ExecutionTime;
}

/// <summary>Read-only, deterministic profile evaluation over the existing shared Core structure. All coordinates are message-local UTF-16.</summary>
public static class Hl7SchemaValidator
{
    public const int Version = 1;
    public static Hl7SchemaValidationResult Validate(Hl7Message message, Hl7ValidationProfile profile,
        CancellationToken token = default, Hl7SchemaValidationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (!message.HasDecodedText) return new(Array.AsReadOnly(new[] { new Hl7SchemaDiagnostic("Parse.InvalidEncoding", Hl7DiagnosticSeverity.Error,
            "Source cannot be decoded safely; schema validation is unavailable.", null, 0, new(0, 0), true, profile.Id, "Core parser", true) }), false, false, profile.Id, Version);
        return Validate(Hl7ValidationSourceMap.FromMessage(message), new StringHl7TextSource(message.RawText), message.Encoding, profile, token, options);
    }
    public static Hl7SchemaValidationResult Validate(Hl7Structure structure, IHl7TextSource source, Encoding encoding,
        Hl7ValidationProfile profile, CancellationToken token = default, Hl7SchemaValidationOptions? options = null)
    {
        if (structure.TextLength != source.Length) throw new ArgumentException("Structure and source must describe the same complete message revision.");
        profile = Hl7ValidationLimits.Freeze(profile);
        options ??= new();
        if (options.MaximumDiagnostics is < 1 or > Hl7ValidationLimits.Diagnostics || options.MaximumValueCharacters is < 1 or > Hl7ValidationLimits.InspectedValueCharacters ||
            options.MaximumSteps is < 1 or > 1000000 || options.MaximumTime <= TimeSpan.Zero || options.MaximumTime > Hl7ValidationLimits.ExecutionTime)
            throw new ArgumentOutOfRangeException(nameof(options));
        return new Evaluation(structure, source, encoding, profile, options, token).Run();
    }

    private sealed class Evaluation(Hl7Structure structure, IHl7TextSource source, Encoding encoding,
        Hl7ValidationProfile profile, Hl7SchemaValidationOptions options, CancellationToken token)
    {
        private readonly List<Hl7SchemaDiagnostic> diagnostics = [];
        private readonly Stopwatch watch = Stopwatch.StartNew();
        private readonly Dictionary<string, int> occurrences = new(StringComparer.Ordinal);
        private readonly Dictionary<int, int> segmentOccurrences = [];
        private readonly Dictionary<string, List<StructureSegment>> segmentIndex = new(StringComparer.Ordinal);
        private readonly HashSet<(string, string?)> incompleteReasons = [];
        private int steps, index;
        private bool complete = true;
        private sealed class BudgetReached : Exception;
        private void Step()
        {
            token.ThrowIfCancellationRequested();
            if (++steps > options.MaximumSteps || (steps % 128 == 0 && watch.Elapsed > options.MaximumTime)) throw new BudgetReached();
        }
        private void Add(string rule, string message, CharacterRange range, string? path = null, int occurrence = 0,
            string? provenance = null, Hl7DiagnosticSeverity severity = Hl7DiagnosticSeverity.Error, bool parsing = false)
        {
            Step();
            if (diagnostics.Count >= options.MaximumDiagnostics) throw new BudgetReached();
            if (occurrence == 0 && path?.Contains('-') == true) occurrence = Hl7Path.Parse(path).Occurrence;
            var start = Math.Clamp(range.Offset, 0, source.Length); var end = Math.Clamp(range.End, start, source.Length);
            diagnostics.Add(new(rule, severity, message, path, occurrence, new(start, end - start), start == end, profile.Id, provenance ?? profile.Id, parsing));
        }
        private void Incomplete(string reason, string? path = null, CharacterRange? range = null)
        {
            complete = false;
            if (incompleteReasons.Add((reason, path))) Add("Coverage.Incomplete", reason, range ?? new(0, 0), path,
                occurrence: path is not null && path.Contains('-') ? Hl7Path.Parse(path).Occurrence : 0, severity: Hl7DiagnosticSeverity.Information);
        }
        public Hl7SchemaValidationResult Run()
        {
            var parsed = structure.Diagnostics.Count == 0 && structure.CanEdit;
            try
            {
                Step();
                if (structure.Segments.Count > Hl7ValidationLimits.Segments) throw new BudgetReached();
                for (var i = 0; i < structure.Segments.Count; i++)
                {
                    Step(); var id = structure.Segments[i].Identifier;
                    occurrences[id] = occurrences.GetValueOrDefault(id) + 1; segmentOccurrences[i] = occurrences[id];
                    if (!segmentIndex.TryGetValue(id, out var list)) segmentIndex[id] = list = [];
                    list.Add(structure.Segments[i]);
                }
                foreach (var d in structure.Diagnostics)
                    Add("Parse." + d.Code, d.Description, new(Math.Clamp(d.CharacterOffset ?? 0, 0, source.Length), 0), d.Path, provenance: "Core structural parser", severity: d.Severity, parsing: true);
                if (!parsed) Incomplete("Source has parsing or ambiguous-structure diagnostics; schema findings do not establish a successful parse.");
                foreach (var warning in profile.CoverageWarnings) Incomplete(warning);
                if (!Compatible()) return Finish(parsed);
                if (structure.Segments.Any(s => profile.UnsupportedSegments.Contains(s.Identifier, StringComparer.Ordinal)))
                {
                    Incomplete("Message contains a branch explicitly outside this profile's supported evaluation scope. Choose a suitable custom profile; structure/content rules were not evaluated.");
                    return Finish(parsed);
                }
                if (profile.Structure.Count == 0) Incomplete("The resolved profile has no message structure.");
                ConsumeSequence(profile.Structure);
                while (index < structure.Segments.Count) Unexpected();
                foreach (var condition in profile.Conditions)
                {
                    Step(); var when = Location(Hl7Path.Parse(condition.IfPath));
                    var applies = when.State == Hl7ValueState.Value;
                    if (applies && condition.EqualsValue is { } expected) applies = Literal(when.Range, condition.IfPath) == expected;
                    if (!applies) continue;
                    var path = Hl7Path.Parse(condition.ThenPath); var then = Location(path);
                    if (condition.Prohibit ? then.State is Hl7ValueState.Value or Hl7ValueState.ExplicitNull : then.State != Hl7ValueState.Value)
                        Add("Condition." + condition.Id, condition.Prohibit ? "Conditional rule prohibits this value." : "Conditional rule requires this value.", then.Range,
                            path.ToString(), path.Occurrence, condition.Provenance, condition.Severity);
                }
            }
            catch (BudgetReached)
            {
                complete = false;
                if (diagnostics.Count == options.MaximumDiagnostics) diagnostics.RemoveAt(diagnostics.Count - 1);
                diagnostics.Add(new("Budget.Incomplete", Hl7DiagnosticSeverity.Warning, "Validation stopped at a diagnostic, work or execution-time budget; additional issues may exist.",
                    null, 0, new(Math.Min(source.Length, index < structure.Segments.Count ? structure.Segments[index].Range.Offset : source.Length), 0), true, profile.Id, "Validation engine budgets"));
            }
            return Finish(parsed);
        }
        private Hl7SchemaValidationResult Finish(bool parsed) => new(Array.AsReadOnly(diagnostics.OrderBy(d => d.Range.Offset).ThenBy(d => d.Range.Length)
            .ThenBy(d => d.RuleId, StringComparer.Ordinal).ThenBy(d => d.Path, StringComparer.Ordinal).ToArray()), parsed, complete && parsed, profile.Id, Version);
        private Hl7ValidationLocation Location(Hl7Path path) => Hl7ValidationSourceMap.Locate(segmentIndex.TryGetValue(path.Segment, out var list) && list.Count >= path.Occurrence ? list[path.Occurrence - 1] : null, structure.TextLength, path);
        private bool Compatible()
        {
            foreach (var (pathText, expected) in new[] { ("MSH-9.1", profile.MessageType), ("MSH-9.2", profile.TriggerEvent), ("MSH-12.1", profile.Hl7Version), ("MSH-9.3", profile.MessageStructure) })
            {
                if (expected.Length == 0) continue;
                var path = Hl7Path.Parse(pathText); var location = Location(path);
                // Missing structure identifiers in older messages are not a claim
                // to a different structure; the remaining declaration still matches.
                if (pathText == "MSH-9.3" && location.State != Hl7ValueState.Value) continue;
                if (Literal(location.Range, pathText) == expected) continue;
                complete = false; Add("Profile.Incompatible", "Message declaration does not match the selected profile; structural validation was not applied.", location.Range, path.ToString()); return false;
            }
            return true;
        }
        private HashSet<string> First(Hl7StructureRule rule)
        {
            Step(); if (rule.Segment is { } segment) return [segment];
            var result = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in rule.Children) { result.UnionWith(First(child)); if (!rule.Choice && child.Min > 0) break; }
            return result;
        }
        private void ConsumeSequence(IReadOnlyList<Hl7StructureRule> rules, HashSet<string>? enclosingFollow = null)
        {
            for (var r = 0; r < rules.Count; r++)
            {
                Step(); var rule = rules[r]; var count = 0; var first = First(rule);
                var follow = new HashSet<string>(enclosingFollow ?? [], StringComparer.Ordinal);
                foreach (var next in rules.Skip(r + 1)) { follow.UnionWith(First(next)); if (next.Min > 0) break; }
                // Unknown extensions cannot hide a later required standard segment.
                while (index < structure.Segments.Count && !first.Contains(structure.Segments[index].Identifier) &&
                    !rules.Skip(r + 1).Any(next => First(next).Contains(structure.Segments[index].Identifier)) && IsUnknown(structure.Segments[index].Identifier)) Unexpected();
                while (index < structure.Segments.Count && first.Contains(structure.Segments[index].Identifier))
                {
                    // A repeated enclosing group can begin with the same segment
                    // as its last occurrence. Return that token to the owner,
                    // rather than misreporting its singleton child as duplicated.
                    if (rule.Max is { } bound && count >= bound && follow.Contains(structure.Segments[index].Identifier)) break;
                    Step(); var before = index; count++;
                    if (rule.Max is { } max && count > max)
                        Add("Structure.Cardinality", "Segment/group exceeds its maximum cardinality.", structure.Segments[index].Range, provenance: rule.Provenance);
                    var childFollow = new HashSet<string>(follow, StringComparer.Ordinal);
                    if (rule.Max is null || count < rule.Max) childFollow.UnionWith(first);
                    if (rule.Segment is { } id) ValidateSegment(id);
                    else if (rule.Choice) ConsumeSequence([rule.Children.First(c => First(c).Contains(structure.Segments[index].Identifier))], childFollow);
                    else ConsumeSequence(rule.Children, childFollow);
                    if (index == before) break;
                }
                if (count < rule.Min)
                    Add("Structure.Required", "Required segment/group is missing: " + rule.Key + ".", new(index < structure.Segments.Count ? structure.Segments[index].Range.Offset : source.Length, 0), provenance: rule.Provenance);
            }
        }
        private bool IsUnknown(string id) => !profile.Segments.Any(s => s.Id == id);
        private void Unexpected()
        {
            Step(); var segment = structure.Segments[index]; var z = segment.Identifier.StartsWith('Z') && IsUnknown(segment.Identifier);
            if (!z || profile.UnknownZSegments != Hl7UnknownSegmentPolicy.Allow)
                Add(z ? "Structure.UnknownZ" : "Structure.Order", z ? "Custom segment has no definition/placement under the applied profile." : "Segment is unexpected or occurs out of order under the applied profile.",
                    segment.Range, occurrence: segmentOccurrences[index], severity: z && profile.UnknownZSegments == Hl7UnknownSegmentPolicy.Warn ? Hl7DiagnosticSeverity.Warning : Hl7DiagnosticSeverity.Error);
            if (z && profile.UnknownZSegments != Hl7UnknownSegmentPolicy.Reject) Incomplete("Undefined custom segment content was not validated.", range: segment.Range);
            index++;
        }
        private void ValidateSegment(string id)
        {
            var segment = structure.Segments[index]; var occurrence = segmentOccurrences[index++];
            var definition = profile.Segments.SingleOrDefault(s => s.Id == id);
            if (definition is null) { Incomplete("Segment definition is unavailable.", id, segment.Range); return; }
            foreach (var rule in definition.Fields)
            {
                Step(); var path = new Hl7Path(id, rule.Position, occurrence); var field = segment.Fields.FirstOrDefault(f => f.Number == rule.Position);
                if (field is null)
                {
                    if (rule.Min > 0) Add("Value.Required", "Required field is missing.", new(segment.Range.End, 0), path.ToString(), occurrence, rule.Provenance);
                    continue;
                }
                // MSH-1/2 are literal delimiter declarations; repetitions/components
                // in those fields are not ordinary ER7 value structure.
                if (id == "MSH" && rule.Position <= 2)
                { CheckValue(rule, field, path, 0, literal: true); continue; }
                var populated = field.Children.Count(c => c.State == Hl7ValueState.Value);
                if (field.State == Hl7ValueState.Value && rule.Max is > 0 and { } max && field.Children.Count > max)
                    Add("Value.Cardinality", "Field exceeds its repetition limit.", field.Range, path.ToString(), occurrence, rule.Provenance);
                if (field.Children.Count > Hl7ValidationLimits.Repetitions) throw new BudgetReached();
                if (populated < rule.Min)
                    Add("Value.Required", "Required field/repetition is empty or null.", field.Range, path.ToString(), occurrence, rule.Provenance);
                if (rule.Max == 0 && field.State is Hl7ValueState.Value or Hl7ValueState.ExplicitNull)
                    Add("Value.Prohibited", "The applied profile prohibits this field.", field.Range, path.ToString(), occurrence, rule.Provenance);
                if (rule.Max == 0) continue;
                foreach (var repetition in field.Children)
                    if (repetition.State == Hl7ValueState.Value) CheckValue(rule, repetition, new(id, rule.Position, occurrence, repetition.Number), 0);
            }
            if (!definition.AllowExtraFields)
                foreach (var field in segment.Fields.Where(f => f.State == Hl7ValueState.Value && !definition.Fields.Any(r => r.Position == f.Number)))
                    Add("Value.Undefined", "Populated field has no definition in the applied profile.", field.Range, new Hl7Path(id, field.Number, occurrence).ToString(), occurrence);
        }
        private string? Literal(CharacterRange range, string path)
        {
            Step(); if (range.Length > options.MaximumValueCharacters) { Incomplete("Value inspection limit exceeded; this value was not fully checked.", path, range); return null; }
            var decoded = Hl7EscapeCodec.Decode(source.Read(range), structure.Delimiters, encoding, options.MaximumValueCharacters, 16);
            if (decoded.Diagnostics.Count != 0) { Incomplete("Value contains unsupported or malformed escapes.", path, range); return null; }
            return decoded.Text;
        }
        private void CheckValue(Hl7ValueRule rule, StructureNode node, Hl7Path path, int level, bool literal = false)
        {
            Step();
            if (rule.Table is not null && rule.AllowedValues.Count == 0)
                Incomplete("Table binding has no supplied membership set; coded membership was not checked.", path.ToString(), node.Range);
            if (rule.DataTypePath is { } typePath)
            {
                var requested = Hl7Path.Parse(typePath);
                requested = new(requested.Segment, requested.Field, path.Occurrence, requested.Repetition, requested.Component, requested.Subcomponent);
                var location = Location(requested); var type = Literal(location.Range, requested.ToString());
                if (type is null || type == "Varies" || !Hl7ValidationCatalog.IsKnownDataType(type, profile.Hl7Version))
                { Incomplete("Contextual datatype declaration is missing or unsupported.", path.ToString(), node.Range); return; }
                rule = rule with { DataType = type, DataTypePath = null };
            }
            if (rule.MaxLength is { } maximum && node.Range.Length > maximum)
                Add("Value.Length", "Value exceeds the declared maximum encoded character length.", node.Range, path.ToString(), path.Occurrence, rule.Provenance);
            var children = rule.Children;
            var typeChildren = Hl7ValidationCatalog.Components(rule.DataType, profile.Hl7Version);
            if (!literal && level < 2 && typeChildren is { Count: > 0 })
            {
                foreach (var component in typeChildren)
                {
                    var explicitRule = children.FirstOrDefault(c => c.Position == component.Position);
                    CheckChild(explicitRule ?? component, node, path, level);
                }
                foreach (var extra in children.Where(c => !typeChildren.Any(t => t.Position == c.Position))) CheckChild(extra, node, path, level);
                foreach (var extra in node.Children.Where(c => c.State == Hl7ValueState.Value && c.Number > typeChildren.Count && !children.Any(t => t.Position == c.Number)))
                    Add("Type.ComponentCount", "Datatype contains an undefined component/subcomponent.", extra.Range, path.ToString(), path.Occurrence, rule.Provenance);
            }
            else if (children.Count > 0 && level < 2)
                foreach (var child in children) CheckChild(child, node, path, level);
            else if (typeChildren is { Count: > 0 } && level >= 2)
                Incomplete("Composite datatype nesting exceeds ER7 component/subcomponent validation support.", path.ToString(), node.Range);
            else
            {
                var text = literal ? source.Read(node.Range) : Literal(node.Range, path.ToString());
                if (text is null) return;
                if (!literal && node.Children.Count > 1 && node.Children.Skip(1).Any(c => c.State == Hl7ValueState.Value))
                    Add("Type.Primitive", "Primitive datatype contains structural components.", node.Range, path.ToString(), path.Occurrence, rule.Provenance);
                if (!ValidPrimitive(rule.DataType, text))
                    Add("Type." + rule.DataType, "Value has invalid supported " + rule.DataType + " syntax.", node.Range, path.ToString(), path.Occurrence, rule.Provenance);
                if (rule.DataType is "GTS" or "Varies") Incomplete("Datatype requires an unsupported contextual/advanced semantic check.", path.ToString(), node.Range);
            }
            if (rule.AllowedValues.Count > 0)
            {
                var text = literal ? source.Read(node.Range) : Literal(node.Range, path.ToString());
                if (text is not null && !rule.AllowedValues.Contains(text, StringComparer.Ordinal))
                    Add("Value.Allowed", "Value is not permitted by the applied profile's value set.", node.Range, path.ToString(), path.Occurrence, rule.Provenance);
            }
        }
        private void CheckChild(Hl7ValueRule rule, StructureNode parent, Hl7Path parentPath, int level)
        {
            Step(); var node = parent.Children.FirstOrDefault(c => c.Number == rule.Position);
            var path = level == 0 ? new Hl7Path(parentPath.Segment, parentPath.Field, parentPath.Occurrence, parentPath.Repetition ?? 1, rule.Position)
                : new Hl7Path(parentPath.Segment, parentPath.Field, parentPath.Occurrence, parentPath.Repetition ?? 1, parentPath.Component, rule.Position);
            if (rule.Max == 0 && node?.State is Hl7ValueState.Value or Hl7ValueState.ExplicitNull)
            { Add("Value.Prohibited", "The applied profile prohibits this component/subcomponent.", node.Range, path.ToString(), path.Occurrence, rule.Provenance); return; }
            if (node?.State != Hl7ValueState.Value)
            { if (rule.Min > 0) Add("Value.Required", "Required component/subcomponent is missing, empty or null.", node?.Range ?? new(parent.Range.End, 0), path.ToString(), path.Occurrence, rule.Provenance); return; }
            if (rule.Max != 0) CheckValue(rule, node, path, level + 1);
        }
        private static bool ValidPrimitive(string type, string text)
        {
            if (text.Length == 0) return true;
            if (type == "NM")
            {
                var at = text[0] is '+' or '-' ? 1 : 0; var digits = 0; var point = false;
                for (; at < text.Length; at++) { if (text[at] is >= '0' and <= '9') digits++; else if (text[at] == '.' && !point) point = true; else return false; }
                return digits > 0;
            }
            if (type == "SI") return text.All(c => c is >= '0' and <= '9');
            if (type is "DT" or "DTM" or "TM") return Hl7ValidationDates.IsValid(type, text);
            return true;
        }
    }
}

internal static class Hl7ValidationDates
{
    internal static bool IsValid(string type, string text)
    {
        var core = text; var timezone = core.LastIndexOfAny(['+', '-']);
        if (timezone >= 0)
        {
            var zone = core[(timezone + 1)..];
            if (type == "DT" || zone.Length != 4 || !zone.All(char.IsAsciiDigit) || int.Parse(zone[..2], CultureInfo.InvariantCulture) > 23 || int.Parse(zone[2..], CultureInfo.InvariantCulture) > 59) return false;
            core = core[..timezone];
        }
        var point = core.IndexOf('.');
        if (point >= 0)
        {
            if (type == "DT" || core.Length - point is < 2 or > 5 || !core[(point + 1)..].All(char.IsAsciiDigit)) return false;
            core = core[..point]; if (core.Length != (type == "TM" ? 6 : 14)) return false;
        }
        if (!core.All(char.IsAsciiDigit) || core.Length % 2 != 0 || (type == "TM" ? core.Length is < 2 or > 6 : core.Length is < 4 or > 14) || (type == "DT" && core.Length > 8)) return false;
        int Number(int start) => int.Parse(core.AsSpan(start, 2), CultureInfo.InvariantCulture);
        var clock = type == "TM" ? 0 : 8;
        if (type != "TM")
        {
            var year = int.Parse(core.AsSpan(0, 4), CultureInfo.InvariantCulture); if (year == 0) return false;
            var month = core.Length >= 6 ? Number(4) : 1; if (month is < 1 or > 12) return false;
            if (core.Length >= 8 && (Number(6) < 1 || Number(6) > DateTime.DaysInMonth(year, month))) return false;
        }
        return (core.Length < clock + 2 || Number(clock) <= 23) && (core.Length < clock + 4 || Number(clock + 2) <= 59) && (core.Length < clock + 6 || Number(clock + 4) <= 59);
    }
}
