using System.Text.Json;

namespace HL7Workbench.Core;

/// <summary>Immutable, explicitly scoped v2.5.1 catalog. Resource attribution/license are recorded alongside the model.</summary>
public static class Hl7ValidationCatalog
{
    private sealed record Model(string Source, IReadOnlyDictionary<string, IReadOnlyList<Hl7StructureRule>> Structures,
        IReadOnlyList<Hl7SegmentRule> Segments, IReadOnlyDictionary<string, IReadOnlyList<Hl7ValueRule>> DataTypes);
    private static readonly Lazy<Model> model = new(Load);
    private static readonly Lazy<IReadOnlyList<Hl7ValidationProfile>> profiles = new(CreateProfiles);
    public static IReadOnlyList<Hl7ValidationProfile> BuiltIns => profiles.Value;
    public static bool IsBuiltInId(string id) => id.StartsWith("hl7.v251.", StringComparison.Ordinal);
    public static bool IsSupportedVersion(string version) => version == "2.5.1";
    public static bool IsKnownDataType(string type, string version = "2.5.1") => IsSupportedVersion(version) &&
        (type is "ST" or "TX" or "FT" or "ID" or "IS" or "NM" or "SI" or "DT" or "DTM" or "TM" or "GTS" or "TN" or "Varies" || model.Value.DataTypes.ContainsKey(type));
    internal static IReadOnlyList<Hl7ValueRule>? Components(string type, string version = "2.5.1") => IsSupportedVersion(version) ? model.Value.DataTypes.GetValueOrDefault(type) : null;
    public static IReadOnlyList<Hl7ValidationProfile> Detect(string type, string trigger, string version, string? structure = null) =>
        Array.AsReadOnly(BuiltIns.Where(p => p.MessageType == type && p.TriggerEvent == trigger && p.Hl7Version == version &&
            (string.IsNullOrEmpty(structure) || p.MessageStructure == structure)).ToArray());
    public static IReadOnlyList<Hl7ValidationProfile> Detect(Hl7Message message)
    {
        string Value(string path) => message.Get(path).State == Hl7ValueState.Value ? message.GetDecoded(path).Text : "";
        return message.HasDecodedText ? Detect(Value("MSH-9.1"), Value("MSH-9.2"), Value("MSH-12.1"), Value("MSH-9.3")) : [];
    }
    public static IReadOnlyList<Hl7ValidationProfile> Detect(Hl7Structure structure, IHl7TextSource source, System.Text.Encoding encoding)
    {
        string Value(string path)
        {
            var location = Hl7ValidationSourceMap.Locate(structure, Hl7Path.Parse(path));
            return location.State != Hl7ValueState.Value || location.Range.Length > 128 ? "" : Hl7EscapeCodec.Decode(source.Read(location.Range), structure.Delimiters, encoding, 128, 16).Text;
        }
        return Detect(Value("MSH-9.1"), Value("MSH-9.2"), Value("MSH-12.1"), Value("MSH-9.3"));
    }
    private static IReadOnlyList<Hl7ValidationProfile> CreateProfiles()
    {
        return Array.AsReadOnly(new[] { ("ADT", "A01", "ADT_A01"), ("ADT", "A04", "ADT_A01"), ("ADT", "A08", "ADT_A01"),
            ("ADT", "A02", "ADT_A02"), ("ADT", "A03", "ADT_A03"), ("ORM", "O01", "ORM_O01"), ("ORU", "R01", "ORU_R01") }
            .Select(m => Hl7ValidationLimits.Freeze(new Hl7ValidationProfile { Id = $"hl7.v251.{m.Item1}.{m.Item2}", Name = $"HL7 2.5.1 {m.Item1}^{m.Item2}",
                MessageType = m.Item1, TriggerEvent = m.Item2, MessageStructure = m.Item3, Structure = model.Value.Structures[m.Item3], Segments = model.Value.Segments,
                Source = model.Value.Source, UnsupportedSegments = m.Item1 == "ORM" ? ["RQD", "RQ1", "RXO", "ODS", "ODT"] : [],
                CoverageWarnings = ["Base model covers segment/group order and field declarations, not all conditional usage/component requirements or terminology memberships.",
                    "External/user-defined tables, advanced datatype semantics and implementation-guide conformance require an appropriate explicit custom profile."] })).ToArray());
    }
    private static Model Load()
    {
        using var stream = typeof(Hl7ValidationCatalog).Assembly.GetManifestResourceStream("HL7Workbench.Core.ValidationCatalog.v251-model.json")!;
        using var document = JsonDocument.Parse(stream); var root = document.RootElement; var source = root.GetProperty("source").GetString()!;
        static int? Number(JsonElement e, string name) => e.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number ? value.GetInt32() : null;
        Hl7ValueRule Value(JsonElement e) => new() { Position = e.GetProperty("position").GetInt32(), Min = Number(e, "min") ?? 0,
            Max = e.TryGetProperty("max", out _) ? Number(e, "max") : 1, DataType = e.GetProperty("dataType").GetString()!, MaxLength = Number(e, "maxLength"),
            Table = e.TryGetProperty("table", out var table) ? table.GetString() : null, Provenance = source };
        IReadOnlyList<Hl7StructureRule> Structure(JsonElement elements) => Array.AsReadOnly(elements.EnumerateArray().Select(e => new Hl7StructureRule
        { Key = e.GetProperty("key").GetString()!, Segment = e.TryGetProperty("segment", out var s) ? s.GetString() : null, Min = Number(e, "min") ?? 0, Max = Number(e, "max"),
            Choice = e.TryGetProperty("choice", out var choice) && choice.GetBoolean(),
            Children = e.TryGetProperty("children", out var children) ? Structure(children) : [], Provenance = source }).ToArray());
        return new(source, root.GetProperty("structures").EnumerateObject().ToDictionary(p => p.Name, p => Structure(p.Value)),
            Array.AsReadOnly(root.GetProperty("segments").EnumerateObject().Select(p => new Hl7SegmentRule { Id = p.Name, Fields = Array.AsReadOnly(p.Value.EnumerateArray().Select(Value)
                .Select(v => p.Name == "OBX" && v.Position == 5 ? v with { DataTypePath = "OBX-2" } : v).ToArray()) }).ToArray()),
            root.GetProperty("datatypes").EnumerateObject().ToDictionary(p => p.Name, p => (IReadOnlyList<Hl7ValueRule>)Array.AsReadOnly(p.Value.EnumerateArray().Select(Value).ToArray())));
    }
}
