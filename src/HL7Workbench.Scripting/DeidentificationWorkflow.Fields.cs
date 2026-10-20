using System.Globalization;
using HL7Workbench.Core;

namespace HL7Workbench.Scripting;

public sealed partial class DeidentificationWorkflow
{
    private static readonly HashSet<string> KnownSegments = new(StringComparer.Ordinal)
    { "MSH", "EVN", "PID", "PD1", "NK1", "PV1", "PV2", "IN1", "IN2", "GT1", "ORC", "OBR", "OBX", "NTE", "DG1", "AL1" };
    private static bool IsDate(string segment, int field) => segment switch
    {
        "MSH" => field == 7, "EVN" => field is 2 or 3 or 6, "PID" => field is 7 or 29 or 33,
        "PD1" => field is 13 or 17 or 18, "NK1" => field is 8 or 9 or 16, "PV1" => field is 44 or 45,
        "PV2" => field is 8 or 9, "IN1" => field is 12 or 13 or 18 or 30, "IN2" => field == 17,
        "GT1" => field is 8 or 13 or 14 or 24 or 31 or 32, "ORC" => field is 9 or 15 or 27, "OBR" => field is 6 or 7 or 8 or 14 or 22 or 36,
        "OBX" => field is 12 or 14 or 19, "DG1" => field == 5, "AL1" => field == 6, _ => false
    };
    private static (string Domain, int Authority, int Type)? Identity(string segment, int field) => (segment, field) switch
    {
        ("PID", 2 or 3 or 4) => ("patient", 4, 5), ("PID", 18) => ("account", 4, 5),
        ("PV1", 5 or 19 or 50) => ("visit", 4, 5), ("GT1", 2) => ("guarantor", 4, 5),
        ("ORC", 2 or 3 or 4) or ("OBR", 2 or 3) => ("order", 2, 0),
        ("MSH", 10) => ("message", 0, 0), ("PID", 19 or 20) => ("personal-id", 0, 0),
        ("IN1", 49) or ("IN2", 25 or 26 or 61) => ("insurance", 4, 5),
        ("IN1", 2 or 3 or 36) or ("IN2", 1 or 2 or 6 or 8 or 10) => ("insurance", 0, 0),
        ("OBR", 18 or 19 or 20 or 21) => ("accession", 0, 0), _ => null
    };
    private static bool IsProvider(string segment, int field) => segment switch
    {
        "PV1" => field is 7 or 8 or 9 or 17 or 52, "PD1" => field == 4,
        "ORC" => field is 10 or 11 or 12 or 19, "OBR" => field is 10 or 16 or 28 or 32 or 33 or 34 or 35,
        "OBX" => field == 16, _ => false
    };
    private void Transform(Hl7Message model, Hl7Segment segment, Hl7Field field, Context context, List<ScriptMessage.LiteralEdit> edits)
    {
        var name = segment.Identifier; var number = field.Number;
        var dataType = segment.GetField(2)?.Raw;
        if (options.Preset == DeidentificationPreset.PatientDeidentifier && SubstituteDemographics(model, segment, field, context, edits)) return;
        if (options.Preset == DeidentificationPreset.PatientDeidentifier && IsAgeObservation(segment) && number is 3 or 6)
        {
            var ageCode = segment.GetField(3)!.Repetitions[0].Components[0].Raw;
            Replace(field, (_, c, _) => number == 3 ? c switch { 0 => ageCode, 1 => "Age", 2 => ageCode == "AGE" ? "L" : "LN", _ => "" } : c switch { 0 => "a", 1 => "year", 2 => "UCUM", _ => "" }, edits);
            return;
        }
        if (IsDate(name, number) || name == "OBX" && number == 5 && dataType is "DT" or "DTM" or "TS")
        {
            if (options.Preset == DeidentificationPreset.IdentifierPseudonymizer) { retained++; return; }
            Shift(field, edits); return;
        }
        if (options.Preset == DeidentificationPreset.TimestampShifter)
        { retained++; highRisk++; return; }
        if (Identity(name, number) is { } identity)
        { Identifiers(model, field, context, identity.Domain, identity.Authority, identity.Type, edits); return; }
        if (IsProvider(name, number)) { Identifiers(model, field, context, "provider", 9, 13, edits); return; }
        if (options.Preset == DeidentificationPreset.IdentifierPseudonymizer)
        { retained++; highRisk++; return; }
        if (name == "MSH" && number is 3 or 4 or 5 or 6 || name == "PV1" && number is 3 or 6 or 11 or 42 or 43 || name == "OBX" && number is 15 or 18)
        { Identifiers(model, field, context, "organization-location", 0, 0, edits); return; }
        if (name == "OBX" && number == 5)
        {
            if (dataType == "NM" && field.Repetitions.All(r => r.Components.Count == 1 && r.Components[0].Subcomponents.Count == 1 &&
                (r.Value.State != Hl7ValueState.Value || decimal.TryParse(r.Raw, NumberStyles.Float, CultureInfo.InvariantCulture, out _)))) { retained++; return; }
            highRisk++; Suppress(field, edits); return;
        }
        if (name == "OBX" && number == 6 && KeepClinical(model, name, field))
        {
            foreach (var rep in field.Repetitions)
                if (rep.Components.Count > 1) foreach (var part in rep.Components[1].Subcomponents) Write(part, "Synthetic unit", edits);
            retained++; return;
        }
        if (KeepClinical(model, name, field)) { retained++; return; }
        if (name == "NTE" || !KnownSegments.Contains(name) || name == "OBX") highRisk++;
        Suppress(field, edits);
    }
    private static bool KeepClinical(Hl7Message model, string name, Hl7Field field)
    {
        var n = field.Number;
        // This is intentionally a small allowlist, not an inferred HL7 schema.
        // Other values are suppressed; retained clinical/quasi-identifying values
        // are counted and the report always demands review.
        if (n == 1 && name != "MSH" && field.Raw.All(char.IsAsciiDigit)) return true;
        if (name == "MSH" && n is 9 or 11 or 12) return true;
        if (name == "EVN" && n == 1 || name == "PV1" && n is 2 or 4 || name == "PID" && n is 8 or 24 or 25 or 30) return true;
        if (name == "ORC" && n is 1 or 5 || name == "OBR" && n is 24 or 25 || name == "OBX" && n is 2 or 8 or 11) return true;
        if (name == "OBX" && n == 6)
            return field.Repetitions.All(r => r.Components.Count <= 3 && r.Components[0].Subcomponents.Count == 1 &&
                (r.Components.Count < 3 || r.Components[2].Raw is "" or "UCUM") &&
                r.Components[0].Raw is "mg/dL" or "mmol/L" or "g/dL" or "Cel" or "kg" or "cm" or "mm[Hg]" or "%" or "1" or "/min");
        return false;
    }
    private void Shift(Hl7Field field, List<ScriptMessage.LiteralEdit> edits)
    {
        foreach (var rep in field.Repetitions.Where(r => r.Value.State == Hl7ValueState.Value))
        {
            var leaf = rep.Components[0].Subcomponents[0];
            var supported = rep.Components.Count <= 2 && rep.Components[0].Subcomponents.Count == 1 &&
                (rep.Components.Count == 1 || rep.Components[1].Raw is "" or "Y" or "L" or "D" or "H" or "M" or "S");
            if (supported && Hl7TimestampShift.TryShift(leaf.Raw, ShiftDays, out var value))
            { Write(leaf, value, edits); shifted++; }
            else
            {
                unsupportedTimes++;
                if (options.Preset == DeidentificationPreset.PatientDeidentifier)
                    foreach (var component in rep.Components) foreach (var part in component.Subcomponents) Write(part, "", edits, suppress: true);
                else retained++;
            }
        }
    }
    private void Identifiers(Hl7Message model, Hl7Field field, Context context, string domain, int authority, int type, List<ScriptMessage.LiteralEdit> edits)
    {
        foreach (var rep in field.Repetitions.Where(r => r.Value.State == Hl7ValueState.Value))
        {
            var qualified = authority > 0 ? QualifiedIdentity(model, rep, domain, authority, type) : null;
            var first = ComponentText(model, rep, 1);
            if (first is null) { highRisk++; foreach (var component in rep.Components) foreach (var leaf in component.Subcomponents) Write(leaf, "REDACTED", edits, true); continue; }
            // Never correlate unqualified patient/provider/visit identifiers by a
            // bare number or a name. Unsupported scope stays explicitly unresolved.
            var unresolved = authority > 0 && qualified is null || context.Unresolved && domain == "patient";
            if (unresolved) highRisk++;
            var identity = qualified ?? Digest(domain, first, unresolved ? context.Tag : "");
            if (context.Unresolved && domain == "patient") identity = Digest("ambiguous-patient", identity, context.Tag);
            var profile = options.Preset == DeidentificationPreset.PatientDeidentifier ? Profile(domain == "patient" ? context.Tag : domain + ":" + identity) : null;
            if (profile is not null) { SyntheticIdentifier(rep, profile, domain, authority, type, identity, edits); continue; }
            var synthetic = Issue(domain, identity);
            for (var c = 0; c < rep.Components.Count; c++)
            foreach (var leaf in rep.Components[c].Subcomponents)
            {
                if (c == 0) Write(leaf, synthetic, edits);
                else if (c + 1 == type && leaf.Raw is "MR" or "PI" or "PN" or "AN" or "VN" or "SS" or "DL" or "NPI") retained++;
                else Write(leaf, c + 1 == authority ? Issue("authority", ComponentText(model, rep, authority) ?? "") : "SYNTHETIC", edits);
            }
        }
    }
}
