using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace HL7Workbench.Core;

public sealed partial class FhirGenerationPlan
{
    private sealed record MappedObservation(string Type, string Code, string Value, string Units);
    private static string Text(string text)
    {
        if (text.Length > 4096) throw new Hl7LimitException("MappedFieldCharacters");
        var result = new StringBuilder(); var start = 0;
        // Core's edit encoder deliberately refuses segment terminators. Encode each
        // ordinary run through it, and represent controls by explicit UTF-8 hex escapes.
        for (var n = 0; n < text.Length; n++)
        {
            if (!char.IsControl(text[n])) continue;
            result.Append(Hl7EscapeCodec.Encode(text[start..n], Hl7Delimiters.Default, 16384));
            result.Append("\\X").Append(Convert.ToHexString(Encoding.UTF8.GetBytes(text[n].ToString()))).Append('\\'); start = n + 1;
        }
        result.Append(Hl7EscapeCodec.Encode(text[start..], Hl7Delimiters.Default, 16384));
        if (result.Length > 16384) throw new Hl7LimitException("MappedFieldCharacters"); return result.ToString();
    }
    private static string Components(params string[] values) => string.Join('^', values.Select(Text));
    private static string Segment(string name, params (int Field, string Value)[] fields)
    {
        var values = new string[fields.Max(f => f.Field) + 1]; values[0] = name;
        foreach (var (field, value) in fields) values[field] = value; return string.Join('|', values) + "\r";
    }
    private static string CodingSystem(string system) => system switch
    {
        "http://loinc.org" => "LN", "http://snomed.info/sct" => "SCT", "http://unitsofmeasure.org" => "UCUM",
        "http://www.nlm.nih.gov/research/umls/rxnorm" => "RXNORM", "http://hl7.org/fhir/sid/icd-10-cm" => "I10C",
        "http://hl7.org/fhir/sid/icd-10" => "I10", "http://hl7.org/fhir/sid/cvx" => "CVX", "" => "99LOCAL", _ => ""
    };
    private static bool TryConcept(FhirConcept? concept, bool cwe, out string value)
    {
        value = ""; if (concept is null) return false;
        var codes = concept.Codings.Where(c => c.Code.Length > 0 && CodingSystem(c.System).Length > 0).Take(2).ToArray();
        try
        {
            if (codes.Length == 0)
            {
                if (concept.Codings.Any(c => c.Code.Length > 0) || concept.Text.Length == 0) return false;
                value = Components("", concept.Text, ""); return true;
            }
            var first = codes[0]; var second = codes.ElementAtOrDefault(1);
            var parts = new[] { first.Code, first.Display, CodingSystem(first.System), second?.Code ?? "", second?.Display ?? "", second is null ? "" : CodingSystem(second.System) };
            value = Components(cwe ? parts.Concat(new[] { "", "", concept.Text }).ToArray() : parts); return true;
        }
        catch (Exception error) when (error is ArgumentException or Hl7LimitException) { return false; }
    }
    private static string ObservationStatus(string status) => status switch { "final" => "F", "preliminary" => "P", "amended" or "corrected" => "C", _ => "" };
    private static string ReportStatus(string status) => status switch { "final" => "F", "preliminary" => "P", "partial" => "A", "amended" or "corrected" or "appended" => "C", _ => "" };
    /// <summary>Exact decimal expansion; never pass through binary floating point or round a source decimal.</summary>
    private static string DecimalText(string text)
    {
        var match = Regex.Match(text, @"^(?<sign>-?)(?<whole>[0-9]+)(?:\.(?<fraction>[0-9]+))?(?:[eE](?<exponent>[+-]?[0-9]+))?$", RegexOptions.CultureInvariant);
        if (!match.Success || text.Length > 128) return "";
        var exponent = 0;
        if (match.Groups["exponent"].Success && (!int.TryParse(match.Groups["exponent"].Value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent) || exponent is < -100 or > 100)) return "";
        var digits = match.Groups["whole"].Value + match.Groups["fraction"].Value; var point = match.Groups["whole"].Length + exponent;
        var number = point <= 0 ? "0." + new string('0', -point) + digits : point >= digits.Length ? digits + new string('0', point - digits.Length) : digits.Insert(point, ".");
        if (number.Length + match.Groups["sign"].Length > 128) return ""; return match.Groups["sign"].Value + number;
    }
    private static bool TryObservation(FhirResource resource, out MappedObservation? result, out FhirMappingWarning warning)
    {
        result = null; warning = FhirMappingWarning.UnsupportedValue;
        if (ObservationStatus(resource.Status).Length == 0) { warning = FhirMappingWarning.UnsupportedStatus; return false; }
        if (!TryConcept(resource.Code, false, out var code)) { warning = FhirMappingWarning.UnsupportedCodeSystem; return false; }
        if (resource.Value is not { } value) return false;
        try
        {
            switch (value.Kind)
            {
                case FhirValueKind.Quantity:
                    var number = DecimalText(value.Text); if (number.Length == 0 || value.Comparator is not ("" or "<" or "<=" or ">" or ">=")) return false;
                    if (value.UnitSystem.Length > 0 && value.UnitSystem != "http://unitsofmeasure.org") { warning = FhirMappingWarning.UnsupportedCodeSystem; return false; }
                    result = new(value.Comparator.Length == 0 ? "NM" : "SN", code, value.Comparator.Length == 0 ? number : Components(value.Comparator, number),
                        value.Unit.Length == 0 && value.UnitCode.Length == 0 ? "" : Components(value.UnitCode, value.Unit, value.UnitSystem.Length == 0 ? "" : "UCUM")); return true;
                case FhirValueKind.String: result = new("TX", code, Text(value.Text), ""); return true;
                case FhirValueKind.Boolean: result = new("CWE", code, Components(value.Text == "true" ? "Y" : "N", value.Text == "true" ? "Yes" : "No", "HL70136"), ""); return true;
                case FhirValueKind.CodeableConcept:
                    if (!TryConcept(value.Concept, true, out var coded)) { warning = FhirMappingWarning.UnsupportedCodeSystem; return false; }
                    result = new("CWE", code, coded, ""); return true;
                default: return false;
            }
        }
        catch (Exception error) when (error is ArgumentException or Hl7LimitException) { return false; }
    }
    private string IdentityScope(FhirResource resource)
    {
        // Contained subjects can legitimately resolve even when two top-level visit
        // fullUrls conflict. Qualify those visits by their captured input, too.
        return resource.FullUrl.Length > 0 && !source.Resources.Any(r => r.Key != resource.Key && r.FullUrl == resource.FullUrl)
            ? resource.FullUrl : resource.InputDigest + "/" + resource.ResourceType + "/" + resource.Id;
    }
    private string PatientIdentifier(FhirResource patient)
    {
        var person = patient.Person!; var value = person.Identifier; var system = person.IdentifierSystem;
        if (value.Length == 0 || source.Patients.Count(r => r.Person?.Identifier == value && r.Person?.IdentifierSystem == system) > 1)
        { value = patient.Id; system = IdentityScope(patient); }
        return Components(value, "", "", "SF" + Digest(system)[..16], "PI");
    }
    private string Patient(FhirResource patient)
    {
        var data = patient.Person!; var birth = FhirClinicalTime.Parse(data.BirthDate);
        return Segment("PID", (1, "1"), (3, PatientIdentifier(patient)), (5, Components(data.Family, data.Given.FirstOrDefault() ?? "", string.Join(' ', data.Given.Skip(1)))),
            (7, birth.IsValid && !birth.IsInstant ? birth.Hl7 : ""), (8, data.Gender switch { "male" => "M", "female" => "F", "other" => "O", "unknown" => "U", _ => "" }),
            (11, Components(string.Join('\n', data.AddressLines), "", data.City, data.State, data.PostalCode, data.Country)));
    }
    private string Visit(FhirResource encounter)
    {
        var start = Time(encounter, FhirEventKind.EncounterStart); var end = Time(encounter, FhirEventKind.EncounterEnd);
        var attending = source.LinksOf(encounter.Key).FirstOrDefault(l => l.Role == FhirLinkRole.AttendingPractitioner && l.Resolution == FhirResolution.Resolved)?.Target;
        var provider = attending is { } key ? source.Resource(key) : null;
        var person = provider?.Person;
        var patientClass = encounter.Encounter?.ClassCode switch { "IMP" => "I", "EMER" => "E", "AMB" or "OBSENC" or "HH" or "VR" => "O", _ => "" };
        if (encounter.Encounter?.ClassSystem is not ("http://terminology.hl7.org/CodeSystem/v3-ActCode" or "http://hl7.org/fhir/v3/ActCode")) patientClass = "";
        return Segment("PV1", (1, "1"), (2, patientClass), (7, provider is null ? "" : Components(provider.Id, person?.Family ?? "", person?.Given.FirstOrDefault() ?? "")),
            (19, Components(encounter.Id, "", "", "SF" + Digest(IdentityScope(encounter))[..16], "VN")),
            (44, start.IsValid ? start.Hl7 : ""), (45, end.IsValid && (!start.IsInstant || !end.IsInstant || end.Order >= start.Order) ? end.Hl7 : ""));
    }
    private string Render(Candidate candidate, int ordinal, CancellationToken token)
    {
        var transport = request.TransportTimestamp.ToString("yyyyMMddHHmmss'+0000'", CultureInfo.InvariantCulture);
        var control = string.Create(CultureInfo.InvariantCulture, $"SF{request.Seed:X8}{selectionFingerprint[..6]}{ordinal:D4}");
        var adt = candidate.Event != "R01"; var type = adt ? "ADT" : "ORU";
        var structure = adt ? candidate.Event == "A03" ? "ADT_A03" : "ADT_A01" : "ORU_R01";
        var text = new StringBuilder($"MSH|^~\\&|SYN-FHIR|SYNTHETIC|TEST-RECEIVER|TEST|{transport}||{type}^{candidate.Event}^{structure}|{control}|T|2.5.1\r");
        void Append(string segment)
        {
            token.ThrowIfCancellationRequested();
            if (segment.Length - 1 > SyntheticMessageGenerator.MaximumLineCharacters) throw new Hl7LimitException("GeneratedSegmentCharacters");
            if (text.Length + segment.Length > SyntheticMessageGenerator.MaximumMessageCharacters) throw new Hl7LimitException("GeneratedMessageCharacters");
            text.Append(segment);
        }
        if (adt) Append(Segment("EVN", (1, candidate.Event), (2, transport), (6, candidate.Time.Hl7)));
        Append(Patient(source.Resource(candidate.Patient)));
        if (candidate.Encounter is { } encounter) Append(Visit(source.Resource(encounter)));
        if (adt) return text.ToString();
        var resource = source.Resource(candidate.Resource); _ = TryConcept(resource.Code, false, out var service);
        var issued = Time(resource, resource.Kind == FhirResourceKind.DiagnosticReport ? FhirEventKind.ReportIssued : FhirEventKind.ObservationIssued);
        var end = Time(resource, resource.Kind == FhirResourceKind.DiagnosticReport ? FhirEventKind.ReportEffectiveEnd : FhirEventKind.ObservationEffectiveEnd);
        Append(Segment("OBR", (1, "1"), (3, Components(resource.Id, "SYN-FHIR")), (4, service), (7, candidate.Time.IsValid ? candidate.Time.Hl7 : ""),
            (8, end.IsValid && (!candidate.Time.IsInstant || !end.IsInstant || end.Order >= candidate.Time.Order) ? end.Hl7 : ""), (22, issued.IsValid ? issued.Hl7 : ""),
            (25, resource.Kind == FhirResourceKind.DiagnosticReport ? ReportStatus(resource.Status) : ObservationStatus(resource.Status))));
        for (var n = 0; n < candidate.Observations.Length; n++)
        {
            var observation = source.Resource(candidate.Observations[n]);
            if (!TryObservation(observation, out var mapped, out _)) throw new InvalidOperationException("Captured observation mapping changed.");
            var effective = Time(observation, FhirEventKind.ObservationEffective);
            Append(Segment("OBX", (1, (n + 1).ToString(CultureInfo.InvariantCulture)), (2, mapped!.Type), (3, mapped.Code), (5, mapped.Value), (6, mapped.Units),
                (7, Text(observation.ReferenceRange)), (8, string.Join('~', observation.Interpretations.Select(Text))), (11, ObservationStatus(observation.Status)), (14, effective.IsValid ? effective.Hl7 : "")));
        }
        return text.ToString();
    }
}
