using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

namespace HL7Workbench.Core;

/// <summary>Strict native JSON and deliberately bounded v2.5/2.5.1 conformance-profile XML translation. No external references are resolved.</summary>
public static class Hl7ProfileImporter
{
    private static readonly JsonSerializerOptions json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = Hl7ValidationLimits.ProfileDepth, Converters = { new JsonStringEnumConverter<Hl7DiagnosticSeverity>(allowIntegerValues: false) }, WriteIndented = true
    };
    public static Hl7ValidationProfile ImportJson(ReadOnlyMemory<byte> bytes, CancellationToken token = default)
    {
        CheckSize(bytes.Length); token.ThrowIfCancellationRequested();
        if (bytes.Span.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) bytes = bytes[3..];
        try
        {
            using var document = JsonDocument.Parse(bytes, new() { MaxDepth = Hl7ValidationLimits.ProfileDepth }); var nodes = 0;
            void Check(JsonElement element)
            {
                token.ThrowIfCancellationRequested(); if (++nodes > Hl7ValidationLimits.ProfileRules * 16) throw new ArgumentException("JSON structural limit exceeded.");
                if (element.ValueKind == JsonValueKind.Object)
                {
                    var names = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var property in element.EnumerateObject()) { if (!names.Add(property.Name)) throw new ArgumentException("Duplicate JSON property: " + property.Name); Check(property.Value); }
                }
                else if (element.ValueKind == JsonValueKind.Array) foreach (var item in element.EnumerateArray()) Check(item);
            }
            Check(document.RootElement);
            return Hl7ValidationLimits.Freeze(JsonSerializer.Deserialize<Hl7ValidationProfile>(bytes.Span, json) ?? throw new ArgumentException("Profile cannot be null."));
        }
        catch (JsonException) { throw new ArgumentException("Invalid or unsupported native profile JSON. Check version, property names, types and nesting."); }
    }
    public static byte[] ExportJson(Hl7ValidationProfile profile) => JsonSerializer.SerializeToUtf8Bytes(Hl7ValidationLimits.Freeze(profile), json);
    private static void CheckSize(int size) { if (size is < 1 or > Hl7ValidationLimits.ProfileBytes) throw new ArgumentException("Profile input must be 1 byte to 1 MiB."); }
    public static Hl7ValidationProfile ImportXml(ReadOnlyMemory<byte> bytes, CancellationToken token = default)
    {
        CheckSize(bytes.Length); token.ThrowIfCancellationRequested();
        try
        {
            var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null,
                MaxCharactersInDocument = Hl7ValidationLimits.ProfileBytes, MaxCharactersFromEntities = 0, IgnoreComments = true, IgnoreProcessingInstructions = false };
            using var input = new MemoryStream(bytes.ToArray(), false);
            // Inspect depth/count before constructing a bounded DOM. No DOM is
            // ever constructed from unchecked external-entity or recursive input.
            using (var reader = XmlReader.Create(input, settings))
            {
                var nodes = 0;
                while (reader.Read()) { token.ThrowIfCancellationRequested(); if (reader.Depth > Hl7ValidationLimits.ProfileDepth || ++nodes > Hl7ValidationLimits.ProfileRules * 16) throw new ArgumentException("XML structural budget exceeded.");
                    if (reader.NodeType == XmlNodeType.ProcessingInstruction) throw new ArgumentException("XML processing instructions are unsupported."); }
            }
            input.Position = 0; using var safe = XmlReader.Create(input, settings); var document = XDocument.Load(safe);
            var root = document.Root ?? throw new ArgumentException("XML profile root is missing.");
            if (root.Name.LocalName != "HL7v2xConformanceProfile" || root.Name.NamespaceName is not ("" or "urn:hl7-org:v2xml"))
                throw new ArgumentException("Expected a supported HL7v2xConformanceProfile document, not an HL7 message or arbitrary XML.");
            var warnings = new List<string>();
            if (root.DescendantsAndSelf().Any(e => e.Name.LocalName is not ("ImpNote" or "Reference" or "Encoding") &&
                e.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))))
                throw new ArgumentException("Unstructured XML rule text must be expressed in supported constraints or explicit implementation notes.");
            static string Attr(XElement e, string name, string fallback = "") => (string?)e.Attribute(name) ?? fallback;
            static void Attributes(XElement e, params string[] allowed)
            {
                foreach (var a in e.Attributes())
                    if (!a.IsNamespaceDeclaration && (a.Name.NamespaceName.Length != 0 || !allowed.Contains(a.Name.LocalName)))
                        throw new ArgumentException("Unsupported XML attribute at " + e.Name.LocalName + ": " + a.Name.LocalName);
            }
            static void Children(XElement e, params string[] allowed)
            { foreach (var child in e.Elements()) if (child.Name.Namespace != e.Name.Namespace || !allowed.Contains(child.Name.LocalName)) throw new ArgumentException("Unsupported XML construct: " + child.Name.LocalName); }
            Attributes(root, "HL7Version", "ProfileType"); Children(root, "MetaData", "Encodings", "HL7v2xStaticDef", "ImpNote");
            if (Attr(root, "HL7Version") != "2.5.1" || Attr(root, "ProfileType") is not ("Constrainable" or "Implementation"))
                throw new ArgumentException("Supported XML subset requires HL7Version 2.5.1 and ProfileType Constrainable/Implementation.");
            var definition = root.Elements().SingleOrDefault(e => e.Name.LocalName == "HL7v2xStaticDef") ?? throw new ArgumentException("Exactly one HL7v2xStaticDef is required.");
            Attributes(definition, "MsgType", "EventType", "MsgStructID", "Identifier", "EventDesc", "Role"); Children(definition, "MetaData", "Segment", "SegGroup", "ImpNote");
            var metadata = root.Elements().SingleOrDefault(e => e.Name.LocalName == "MetaData");
            foreach (var meta in root.Descendants().Where(e => e.Name.LocalName == "MetaData"))
            { Attributes(meta, "Name", "OrgName", "Version", "Status", "Topics", "Date"); Children(meta); }
            foreach (var encodings in root.Elements().Where(e => e.Name.LocalName == "Encodings"))
            { Attributes(encodings); Children(encodings, "Encoding"); foreach (var encoding in encodings.Elements()) { Attributes(encoding); if (encoding.Value.Trim() != "ER7") throw new ArgumentException("Only ER7 profile encoding is supported."); } }
            foreach (var note in root.Descendants().Where(e => e.Name.LocalName is "ImpNote" or "Reference"))
            { Attributes(note); Children(note); if (!string.IsNullOrWhiteSpace(note.Value)) warnings.Add("Narrative implementation notes/references are retained as an incomplete-coverage notice; they are not executable constraints."); }
            static (int Min, int? Max) Bounds(XElement e)
            {
                var usage = Attr(e, "Usage", "O");
                if (usage is not ("R" or "RE" or "O" or "X" or "B")) throw new ArgumentException("Conditional/unsupported XML Usage requires explicit native JSON predicates.");
                var min = Attr(e, "Min", usage == "R" ? "1" : "0"); var max = Attr(e, "Max", "1");
                if (!int.TryParse(min, out var minimum) || max != "*" && !int.TryParse(max, out _)) throw new ArgumentException("Invalid XML cardinality.");
                if (usage == "X") { if (minimum != 0) throw new ArgumentException("Prohibited usage contradicts minimum cardinality."); return (0, 0); }
                if (usage == "R" && minimum < 1) throw new ArgumentException("Required usage contradicts minimum cardinality.");
                return (minimum, max == "*" ? null : int.Parse(max, System.Globalization.CultureInfo.InvariantCulture));
            }
            IReadOnlyList<Hl7ValueRule> Values(XElement parent, string name)
            {
                return parent.Elements().Where(e => e.Name.LocalName == name).Select((e, i) =>
                {
                    token.ThrowIfCancellationRequested(); Attributes(e, "Name", "Usage", "Min", "Max", "Datatype", "Length", "Table", "ItemNo", "ConstantValue");
                    var childName = name == "Field" ? "Component" : name == "Component" ? "SubComponent" : null;
                    Children(e, childName ?? "ImpNote", "ImpNote", "Reference");
                    if (Attr(e, "Usage") == "RE") warnings.Add("XML RE usage depends on sender knowledge and cannot be fully evaluated from this message alone.");
                    var bound = Bounds(e); var length = Attr(e, "Length");
                    if (length.Length > 0 && !int.TryParse(length, out _)) throw new ArgumentException("Unsupported XML Length constraint.");
                    var constant = (string?)e.Attribute("ConstantValue");
                    return new Hl7ValueRule { Position = i + 1, Min = bound.Min, Max = bound.Max, DataType = Attr(e, "Datatype", "ST"),
                        MaxLength = length.Length == 0 || length == "99999" && Attr(e, "Datatype") == "Varies" ? null : int.Parse(length, System.Globalization.CultureInfo.InvariantCulture), Table = (string?)e.Attribute("Table"),
                        AllowedValues = constant is null ? [] : [constant], Children = childName is null ? [] : Values(e, childName) };
                }).ToArray();
            }
            var segments = new Dictionary<string, Hl7SegmentRule>(StringComparer.Ordinal);
            IReadOnlyList<Hl7StructureRule> Structure(XElement parent)
            {
                var counts = new Dictionary<string, int>();
                return parent.Elements().Where(e => e.Name.LocalName is "Segment" or "SegGroup").Select(e =>
                {
                    token.ThrowIfCancellationRequested(); var segment = e.Name.LocalName == "Segment"; Attributes(e, "Name", "LongName", "Usage", "Min", "Max");
                    Children(e, segment ? "Field" : "Segment", segment ? "ImpNote" : "SegGroup", segment ? "Reference" : "ImpNote");
                    var name = Attr(e, "Name"); var bounds = Bounds(e); counts[name] = counts.GetValueOrDefault(name) + 1;
                    if (Attr(e, "Usage") == "RE") warnings.Add("XML RE usage cannot establish whether absent data was known to the sender.");
                    if (segment)
                    {
                        var value = new Hl7SegmentRule { Id = name, Fields = Values(e, "Field") };
                        if (segments.TryGetValue(name, out var prior) && JsonSerializer.Serialize(prior, json) != JsonSerializer.Serialize(value, json))
                            throw new ArgumentException("Context-specific segment flavors are not supported; use distinct native profile constraints.");
                        segments[name] = value;
                    }
                    return new Hl7StructureRule { Key = name + (counts[name] > 1 ? "_" + counts[name] : ""), Segment = segment ? name : null,
                        Min = bounds.Min, Max = bounds.Max, Children = segment ? [] : Structure(e) };
                }).ToArray();
            }
            var structure = Structure(definition);
            var id = Attr(definition, "Identifier", "xml." + Convert.ToHexString(SHA256.HashData(bytes.Span))[..24]);
            return Hl7ValidationLimits.Freeze(new() { Id = id, Name = metadata is null ? id : Attr(metadata, "Name", id),
                ProfileVersion = metadata is null ? "1" : Attr(metadata, "Version", "1"), Organization = metadata is null ? null : (string?)metadata.Attribute("OrgName"),
                Hl7Version = Attr(root, "HL7Version"), MessageType = Attr(definition, "MsgType"), TriggerEvent = Attr(definition, "EventType"), MessageStructure = Attr(definition, "MsgStructID"),
                Source = "Imported HL7v2xConformanceProfile XML", Description = Attr(definition, "EventDesc"), Structure = structure, Segments = segments.Values.ToArray(), CoverageWarnings = warnings.Distinct().ToArray() });
        }
        catch (Exception e) when (e is XmlException or InvalidOperationException or FormatException or OverflowException)
        { throw new ArgumentException("Unsafe, malformed or unsupported HL7 conformance-profile XML. Check the documented subset."); }
    }
}
