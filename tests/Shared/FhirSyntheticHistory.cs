using System.Text.Json.Nodes;

namespace HL7Workbench.Tests;

/// <summary>Authored fictional R4 graphs shared by history, publication and headless UI regressions.</summary>
internal static class FhirSyntheticHistory
{
    private static JsonObject Reference(string id) => new() { ["reference"] = id };
    private static JsonObject Code(string code) => new() { ["coding"] = new JsonArray(new JsonObject { ["system"] = "http://loinc.org", ["code"] = code }) };
    private static void Add(JsonArray entries, JsonObject resource) => entries.Add(new JsonObject
    {
        ["fullUrl"] = "https://synthetic.example.invalid/fhir/" + (string)resource["resourceType"]! + "/" + (string)resource["id"]!,
        ["resource"] = resource
    });
    private static JsonObject Patient(int n) => new()
    {
        ["resourceType"] = "Patient", ["id"] = $"p-{n:D3}",
        ["name"] = new JsonArray(new JsonObject { ["family"] = "SYNTHETIC SAME" }),
        ["identifier"] = new JsonArray(new JsonObject { ["system"] = "https://github.com/synthetichealth/synthea", ["value"] = $"SYN-HISTORY-{n:D3}" })
    };
    private static JsonObject Observation(int patient, string id, string time, string value) => new()
    {
        ["resourceType"] = "Observation", ["id"] = id, ["status"] = "final", ["subject"] = Reference($"Patient/p-{patient:D3}"),
        ["code"] = Code("SYN-RESULT"), ["effectiveDateTime"] = time, ["valueString"] = value
    };
    private static string Bundle(JsonArray entries) => new JsonObject { ["resourceType"] = "Bundle", ["type"] = "collection", ["entry"] = entries }.ToJsonString();
    public static string History(int patients = 1, int first = 0, bool issues = false)
    {
        var entries = new JsonArray();
        for (var p = first; p < first + patients; p++)
        {
            Add(entries, Patient(p));
            Add(entries, new JsonObject
            {
                ["resourceType"] = "Encounter", ["id"] = $"e-{p:D3}", ["status"] = "finished", ["subject"] = Reference($"Patient/p-{p:D3}"),
                ["class"] = new JsonObject { ["system"] = "http://terminology.hl7.org/CodeSystem/v3-ActCode", ["code"] = "IMP" },
                ["period"] = new JsonObject { ["start"] = "2030-01-02T10:00:00Z", ["end"] = "2030-01-03T12:00:00Z" }
            });
            for (var n = 0; n < 2; n++)
            {
                var observation = Observation(p, $"o-{p:D3}-{n}", "2030-01-02T10:30:00Z", $"SYNTHETIC RESULT {n}");
                observation["encounter"] = Reference($"Encounter/e-{p:D3}"); Add(entries, observation);
            }
            Add(entries, Observation(p, $"standalone-{p:D3}", "2030-01-02T09:00:00Z", "SYNTHETIC STANDALONE"));
            Add(entries, new JsonObject
            {
                ["resourceType"] = "DiagnosticReport", ["id"] = $"r-{p:D3}", ["status"] = "final", ["subject"] = Reference($"Patient/p-{p:D3}"),
                ["encounter"] = Reference($"Encounter/e-{p:D3}"), ["code"] = Code("SYN-PANEL"), ["effectiveDateTime"] = "2030-01-02T10:30:00Z",
                ["result"] = new JsonArray(Reference($"Observation/o-{p:D3}-0"), Reference($"Observation/o-{p:D3}-1"), Reference($"Observation/o-{p:D3}-0"))
            });
            Add(entries, new JsonObject
            {
                ["resourceType"] = "Condition", ["id"] = $"c-{p:D3}", ["subject"] = Reference($"Patient/p-{p:D3}"),
                ["code"] = Code("SYN-CONDITION"), ["onsetDateTime"] = "2030-01-01"
            });
            if (!issues) continue;
            var missing = Observation(p, $"missing-{p:D3}", "", ""); missing.Remove("valueString"); Add(entries, missing);
            var status = Observation(p, $"status-{p:D3}", "", "SYNTHETIC"); status["status"] = "entered-in-error"; Add(entries, status);
            var foreign = Observation(p, $"unresolved-{p:D3}", "", "SYNTHETIC"); foreign["subject"] = Reference("Patient/not-imported"); Add(entries, foreign);
            Add(entries, new JsonObject { ["resourceType"] = "MedicationRequest", ["id"] = $"unsupported-{p:D3}" });
            Add(entries, new JsonObject
            {
                ["resourceType"] = "Encounter", ["id"] = $"planned-{p:D3}", ["status"] = "planned", ["subject"] = Reference($"Patient/p-{p:D3}"),
                ["class"] = new JsonObject { ["system"] = "http://terminology.hl7.org/CodeSystem/v3-ActCode", ["code"] = "IMP" },
                ["period"] = new JsonObject { ["start"] = "2031-01-01T10:00:00Z" }
            });
        }
        return Bundle(entries);
    }
    public static string Observations(int count, int patients = 1, int valueLength = 1, bool missingLastTime = false)
    {
        var entries = new JsonArray();
        for (var p = 0; p < patients; p++)
        {
            Add(entries, Patient(p));
            for (var n = 0; n < count; n++) Add(entries, Observation(p, $"o-{p:D3}-{n:D4}",
                missingLastTime && n + 1 == count ? "" : "2030-01-02T12:00:00Z", new string('|', valueLength)));
        }
        return Bundle(entries);
    }
}
