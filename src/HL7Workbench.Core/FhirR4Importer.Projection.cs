using System.Text.Json;

namespace HL7Workbench.Core;

public static partial class FhirR4Importer
{
    private sealed partial class Builder
    {
        private FhirResource Project(JsonElement json, int key, FhirResourceKind kind, string type, string id, string fullUrl,
            int input, string digest, int entry, int? owner, string content)
        {
            string Text(JsonElement value, string name) => Keep(S(value, name));
            var links = new List<FhirLink>(); var times = new List<FhirTimedEvent>(); var omitted = 0;
            void Link(JsonElement value, FhirLinkRole role)
            {
                if (value.ValueKind == JsonValueKind.Undefined) return;
                if (links.Count == 1024 || ++projectedReferences > limits.References) throw Failure("ReferenceLimit");
                links.Add(new(role, Text(value, "reference"), Text(value, "type")));
            }
            void Time(FhirEventKind eventKind, string value) => times.Add(new(eventKind, FhirClinicalTime.Parse(Keep(value))));
            FhirPerson? person = null; FhirEncounter? encounter = null; FhirConcept? code = null; FhirValue? result = null;
            var status = ""; var range = ""; var interpretations = new List<string>(); var organization = ""; var synthea = false;
            if (kind is FhirResourceKind.Patient or FhirResourceKind.Practitioner)
            {
                var names = A(json, "name"); var name = names.FirstOrDefault(n => S(n, "use") == "official");
                if (name.ValueKind == JsonValueKind.Undefined) name = names.FirstOrDefault();
                var given = A(name, "given").Take(8).Select(s => Keep(s.GetString() ?? "")).ToArray();
                var identifiers = A(json, "identifier");
                synthea = identifiers.Any(i => S(i, "system") == "https://github.com/synthetichealth/synthea");
                var identifier = identifiers.FirstOrDefault(i => S(i, "system") == "https://github.com/synthetichealth/synthea" && S(i, "value").Length > 0);
                if (identifier.ValueKind == JsonValueKind.Undefined) identifier = identifiers.FirstOrDefault(i => A(O(i, "type"), "coding").Any(c => S(c, "code") == "MR") && S(i, "value").Length > 0);
                // Do not accidentally select an SSN merely because it precedes the patient identifier.
                var address = A(json, "address").FirstOrDefault();
                person = new(Text(name, "family"), Array.AsReadOnly(given), Text(json, "birthDate"), Text(json, "gender"),
                    Text(identifier, "value"), Text(identifier, "system"), Array.AsReadOnly(A(address, "line").Take(8).Select(s => Keep(s.GetString() ?? "")).ToArray()),
                    Text(address, "city"), Text(address, "state"), Text(address, "postalCode"), Text(address, "country"));
                omitted += Math.Max(0, names.Length - 1) + Math.Max(0, A(name, "given").Length - 8) + Math.Max(0, A(json, "address").Length - 1);
                Link(O(json, "managingOrganization"), FhirLinkRole.ManagingOrganization);
            }
            if (kind == FhirResourceKind.Encounter)
            {
                encounter = new(Text(json, "status"), Text(O(json, "class"), "code"), Text(O(json, "class"), "system"));
                Time(FhirEventKind.EncounterStart, S(O(json, "period"), "start")); Time(FhirEventKind.EncounterEnd, S(O(json, "period"), "end"));
                Link(O(json, "subject"), FhirLinkRole.Subject); Link(O(json, "serviceProvider"), FhirLinkRole.ServiceProvider);
                foreach (var participant in A(json, "participant"))
                {
                    var attending = A(participant, "type").Any(t => A(t, "coding").Any(c => S(c, "code") == "ATND" && S(c, "system") == "http://terminology.hl7.org/CodeSystem/v3-ParticipationType"));
                    Link(O(participant, "individual"), attending ? FhirLinkRole.AttendingPractitioner : FhirLinkRole.Practitioner);
                }
            }
            if (kind is FhirResourceKind.Condition or FhirResourceKind.Observation or FhirResourceKind.DiagnosticReport)
            {
                if (json.TryGetProperty("context", out _)) throw Failure("UnsupportedR4Shape");
                Link(O(json, "subject"), FhirLinkRole.Subject); Link(O(json, "encounter"), FhirLinkRole.Encounter);
                code = Concept(O(json, "code"));
                if (kind == FhirResourceKind.Condition)
                {
                    status = Text(A(O(json, "clinicalStatus"), "coding").FirstOrDefault(), "code");
                    Time(FhirEventKind.ConditionOnset, S(json, "onsetDateTime")); Time(FhirEventKind.ConditionRecorded, S(json, "recordedDate"));
                    if (json.EnumerateObject().Any(p => p.Name.StartsWith("onset", StringComparison.Ordinal) && p.Name != "onsetDateTime")) omitted++;
                }
                else
                {
                    status = Text(json, "status");
                    var observation = kind == FhirResourceKind.Observation;
                    var effective = Text(json, "effectiveDateTime"); var period = O(json, "effectivePeriod");
                    if (effective.Length > 0 && period.ValueKind != JsonValueKind.Undefined) throw Failure("ChoiceConflict");
                    Time(observation ? FhirEventKind.ObservationEffective : FhirEventKind.ReportEffective, effective.Length > 0 ? effective : S(period, "start"));
                    if (period.ValueKind != JsonValueKind.Undefined) Time(observation ? FhirEventKind.ObservationEffectiveEnd : FhirEventKind.ReportEffectiveEnd, S(period, "end"));
                    Time(observation ? FhirEventKind.ObservationIssued : FhirEventKind.ReportIssued, S(json, "issued"));
                    if (json.EnumerateObject().Any(p => p.Name.StartsWith("effective", StringComparison.Ordinal) && p.Name is not ("effectiveDateTime" or "effectivePeriod"))) omitted++;
                    if (observation)
                    {
                        result = Value(json);
                        if (A(json, "component").Length > 0) omitted += A(json, "component").Length;
                        var ranges = A(json, "referenceRange");
                        if (ranges.Length == 1)
                        {
                            var item = ranges[0]; var low = O(item, "low"); var high = O(item, "high");
                            if (A(item, "appliesTo").Length > 0 || O(item, "age").ValueKind != JsonValueKind.Undefined || O(item, "type").ValueKind != JsonValueKind.Undefined) omitted++;
                            else if (S(item, "text").Length > 0) range = Text(item, "text");
                            else
                            {
                                var a = Number(low, "value"); var b = Number(high, "value");
                                // Only comparable ranges with the result's own units may become OBX-7.
                                bool Compatible(JsonElement quantity) => quantity.ValueKind == JsonValueKind.Undefined ||
                                    S(quantity, "code") == result.UnitCode && S(quantity, "system") == result.UnitSystem && S(quantity, "unit") == result.Unit;
                                if (Compatible(low) && Compatible(high) && S(low, "comparator").Length == 0 && S(high, "comparator").Length == 0 && (a.Length > 0 || b.Length > 0))
                                    range = Keep(a.Length == 0 ? "<=" + b : b.Length == 0 ? ">=" + a : a + "-" + b);
                                else omitted++;
                            }
                        }
                        else omitted += ranges.Length;
                        foreach (var interpretation in A(json, "interpretation"))
                            foreach (var coding in A(interpretation, "coding"))
                            {
                                if (S(coding, "system") == "http://terminology.hl7.org/CodeSystem/v3-ObservationInterpretation" &&
                                    S(coding, "code") is "N" or "A" or "H" or "L" or "HH" or "LL" or "AA" or "U" or "D" or "B" or "W") interpretations.Add(Text(coding, "code"));
                                else omitted++;
                            }
                    }
                    else
                    {
                        foreach (var reference in A(json, "result")) Link(reference, FhirLinkRole.Result);
                        omitted += A(json, "presentedForm").Length + (S(json, "conclusion").Length > 0 ? 1 : 0);
                    }
                }
            }
            if (kind == FhirResourceKind.Organization) organization = Text(json, "name");
            return new(key, kind, Keep(type), Keep(id), Keep(fullUrl), input, digest, entry, owner, content, person, encounter,
                code, status, result, range, Array.AsReadOnly(interpretations.ToArray()), Array.AsReadOnly(links.ToArray()), Array.AsReadOnly(times.ToArray()), organization, synthea, omitted);
        }
        private FhirConcept Concept(JsonElement value)
        {
            var codings = A(value, "coding"); if (codings.Length > 16) throw Failure("CodingLimit");
            return new(Keep(S(value, "text")), Array.AsReadOnly(codings.Select(c => new FhirCoding(Keep(S(c, "code")), Keep(S(c, "display")), Keep(S(c, "system")))).ToArray()));
        }
        private FhirValue Value(JsonElement json)
        {
            var choices = json.EnumerateObject().Where(p => p.Name.StartsWith("value", StringComparison.Ordinal) && !p.Name.StartsWith('_')).ToArray();
            if (choices.Length > 1) throw Failure("ChoiceConflict");
            if (choices.Length > 0 && O(json, "dataAbsentReason").ValueKind != JsonValueKind.Undefined) throw Failure("ValueAbsentConflict");
            var empty = new FhirValue(FhirValueKind.Missing, "", "", "", "", ""); if (choices.Length == 0) return empty;
            var choice = choices[0];
            if (choice.Name == "valueQuantity")
            {
                var quantity = O(json, "valueQuantity"); var text = Number(quantity, "value");
                return new(text.Length == 0 ? FhirValueKind.Missing : FhirValueKind.Quantity, Keep(text), Keep(S(quantity, "comparator")),
                    Keep(S(quantity, "unit")), Keep(S(quantity, "code")), Keep(S(quantity, "system")));
            }
            if (choice.Name == "valueString")
            { var text = Keep(S(json, "valueString")); return empty with { Kind = text.Length == 0 ? FhirValueKind.Missing : FhirValueKind.String, Text = text }; }
            if (choice.Name == "valueBoolean")
            {
                if (choice.Value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) throw Failure("BooleanShape");
                return empty with { Kind = FhirValueKind.Boolean, Text = choice.Value.GetBoolean() ? "true" : "false" };
            }
            if (choice.Name == "valueCodeableConcept") return empty with { Kind = FhirValueKind.CodeableConcept, Concept = Concept(O(json, "valueCodeableConcept")) };
            return empty with { Kind = FhirValueKind.Unsupported };
        }
        private static string Number(JsonElement json, string name)
        {
            if (json.ValueKind == JsonValueKind.Undefined || !json.TryGetProperty(name, out var value) || value.ValueKind == JsonValueKind.Null) return "";
            if (value.ValueKind != JsonValueKind.Number) throw Failure("NumberShape"); return value.GetRawText();
        }
    }
}
