using System.Text;
using System.Text.Json.Nodes;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class FhirImportTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Fixture(string name = "patient-alpha.json") => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "fhir-r4", name));
    private static FhirImportInput Input(string text) => new(_ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(text))));
    private static Task<FhirImportSession> Import(string? text = null, FhirImportLimits? limits = null, bool confirm = true) =>
        FhirR4Importer.ImportAsync([Input(text ?? Fixture())], confirm, limits, Token);
    private static string Changed(Action<JsonObject> edit)
    { var root = JsonNode.Parse(Fixture())!.AsObject(); edit(root); return root.ToJsonString(); }
    private static JsonObject Resource(JsonObject bundle, string type) => bundle["entry"]!.AsArray().Select(n => n!["resource"]!.AsObject()).First(n => (string?)n["resourceType"] == type);
    [Fact]
    public async Task ImportWithoutAcknowledgmentKeepsValidationLimitsCancellationAndContentFreeErrors()
    {
        var session = await FhirR4Importer.ImportAsync([Input(Fixture())], token: Token);
        Assert.Single(session.Patients);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => FhirR4Importer.ImportAsync([Input(Fixture())], new FhirImportLimits { FileBytes = 1 }, Token));
        Assert.DoesNotContain("SYN-ALPHA", error.Message, StringComparison.Ordinal);
        await Assert.ThrowsAsync<InvalidDataException>(() => FhirR4Importer.ImportAsync([Input("{invalid-synthetic-json")], token: Token));
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FhirR4Importer.ImportAsync([Input(Fixture())], token: canceled.Token));
    }
    [Fact]
    public async Task TransactionCollectionFullUrlsRelativeContainedAndCrossInputLinksPreserveExplicitIdentity()
    {
        var session = await FhirR4Importer.ImportAsync([Input(Fixture()), Input(Fixture("patient-beta.json"))], true, token: Token);
        Assert.Equal(2, session.Patients.Count); Assert.Equal(1, session.Summary.DeduplicatedResources);
        Assert.Equal(1, session.Summary.UnsupportedResources); Assert.Equal(0, session.Summary.UnresolvedReferences);
        var patient = session.Patients.Single(p => p.Person!.Identifier == "SYN-ALPHA");
        Assert.Equal("SYNTHETIC EXAMPLE", patient.Person!.DisplayName);
        var other = session.Patients.Single(p => p.Person!.Identifier == "SYN-BETA"); Assert.NotEqual(patient.Key, other.Key);
        var report = session.Resources.Single(r => r.Kind == FhirResourceKind.DiagnosticReport);
        Assert.Equal(5, session.LinksOf(report.Key).Count(l => l.Role == FhirLinkRole.Result && l.Resolution == FhirResolution.Resolved));
        Assert.All(session.Resources.Where(r => r.Kind == FhirResourceKind.Observation), r => Assert.Equal(patient.Key, session.PatientOf(r.Key)));
        Assert.Contains(session.Timeline(patient.Key), e => e.Kind == FhirEventKind.ConditionOnset && e.Time.Precision == FhirTimePrecision.Day);
        Assert.DoesNotContain(session.Timeline(other.Key), e => e.Kind == FhirEventKind.ObservationEffective);
        Assert.Equal(session.Timeline(patient.Key).OrderBy(e => e.Time.Order).Select(e => e.Time.Order), session.Timeline(patient.Key).Select(e => e.Time.Order));
        Assert.DoesNotContain("EXAMPLE", session.ToString()); Assert.DoesNotContain("SYN-ALPHA", patient.ToString());
    }
    [Theory]
    [InlineData("json")] [InlineData("version")] [InlineData("bundle")] [InlineData("id")] [InlineData("duplicateId")]
    [InlineData("duplicateUrl")] [InlineData("choice")] [InlineData("object")] [InlineData("duplicateProperty")] [InlineData("nestedContained")]
    public async Task InvalidInputsFailWithFixedContentFreeDiagnostics(string defect)
    {
        var text = defect switch
        {
            "json" => "{\"secret\":\"SYN-SECRET\"", "duplicateProperty" => "{\"resourceType\":\"Bundle\",\"resourceType\":\"SYN-SECRET\"}",
            _ => Changed(root =>
            {
                var entries = root["entry"]!.AsArray();
                switch (defect)
                {
                    case "version": root["fhirVersion"] = "5.0.0"; break;
                    case "bundle": root["type"] = "history"; break;
                    case "id": Resource(root, "Patient")["id"] = "SYN/SECRET"; break;
                    case "duplicateId": entries.Add(entries[0]!.DeepClone()); break;
                    case "duplicateUrl": entries[1]!["fullUrl"] = (string?)entries[0]!["fullUrl"]; break;
                    case "choice": Resource(root, "Observation")["valueString"] = "SYN-SECRET"; break;
                    case "object": entries[0]!["resource"] = "SYN-SECRET"; break;
                    case "nestedContained": Resource(root, "DiagnosticReport")["contained"]![0]!["contained"] = new JsonArray(new JsonObject { ["resourceType"]="Patient", ["id"]="SYN-SECRET" }); break;
                }
            })
        };
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => Import(text)); Assert.DoesNotContain("SYN-SECRET", error.Message);
    }
    [Theory]
    [InlineData("file")] [InlineData("total")] [InlineData("resources")] [InlineData("patients")]
    [InlineData("string")] [InlineData("projected")] [InlineData("nodes")] [InlineData("refs")] [InlineData("depth")] [InlineData("timeline")]
    public async Task EveryImportBudgetRejectsWithoutReturningAPartialSession(string budget)
    {
        var limits = budget switch
        {
            "file" => new FhirImportLimits { FileBytes = 32 }, "total" => new() { TotalBytes = 32 },
            "resources" => new() { Resources = 2 }, "patients" => new() { Patients = 1 }, "string" => new() { StringCharacters = 8 },
            "projected" => new() { ProjectedCharacters = 16 }, "nodes" => new() { JsonNodes = 4 }, "refs" => new() { References = 1 },
            "depth" => new() { Depth = 2 }, _ => new() { TimelineEvents = 1 }
        };
        await Assert.ThrowsAsync<InvalidDataException>(() => FhirR4Importer.ImportAsync([Input(Fixture()), Input(Fixture("patient-beta.json"))], true, limits, Token));
    }
    [Fact]
    public async Task ConfirmationCancellationAndFileCountAreEnforcedBeforeOpeningInputs()
    {
        var opens = 0; var input = new FhirImportInput(_ => { opens++; return Task.FromResult<Stream>(new MemoryStream()); });
        await Assert.ThrowsAsync<InvalidOperationException>(() => FhirR4Importer.ImportAsync([input], false, token: Token)); Assert.Equal(0, opens);
        await Assert.ThrowsAsync<InvalidDataException>(() => FhirR4Importer.ImportAsync([input,input], true, new() { Files=1 }, Token)); Assert.Equal(0, opens);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => FhirR4Importer.ImportAsync([input], true, token: cancel.Token)); Assert.Equal(0, opens);
    }
    [Fact]
    public async Task AmbiguousAliasesForeignEncountersAndUnresolvedLinksNeverAssignAnotherPatient()
    {
        var beta = JsonNode.Parse(Fixture("patient-beta.json"))!.AsObject();
        var observation = Resource(beta, "Observation"); observation["subject"]!["reference"] = "Patient/bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb";
        var session = await FhirR4Importer.ImportAsync([Input(Fixture()),Input(beta.ToJsonString())], true, token: Token);
        var foreign = session.Resources.Single(r => r.Id.StartsWith("cccc", StringComparison.Ordinal)); Assert.True(session.IsQuarantined(foreign.Key));
        Assert.Null(session.EncounterOf(foreign.Key)); Assert.Equal(1, session.Summary.ConflictingReferences);
        var conflict = Changed(root => { Resource(root,"Patient")["name"]![0]!["family"]="ANOTHER SYNTHETIC"; });
        var ambiguous = await FhirR4Importer.ImportAsync([Input(Fixture()),Input(conflict)], true, token: Token);
        Assert.Equal(2, ambiguous.Patients.Count); Assert.True(ambiguous.Summary.AmbiguousReferences > 0);
        Assert.Contains(ambiguous.Resources, r => r.Kind == FhirResourceKind.Encounter && ambiguous.PatientOf(r.Key) is null);
    }
    [Theory]
    [InlineData("2030",FhirTimePrecision.Year,"2030")] [InlineData("2030-02",FhirTimePrecision.Month,"203002")]
    [InlineData("2030-02-03",FhirTimePrecision.Day,"20300203")] [InlineData("2030-02-03T12:00:00.123-05:00",FhirTimePrecision.Instant,"20300203120000.123-0500")]
    [InlineData("",FhirTimePrecision.Missing,"")] [InlineData("2030-02-30",FhirTimePrecision.Invalid,"")]
    [InlineData("2030-02-03T12:00:00",FhirTimePrecision.Invalid,"")] [InlineData("2030-02-03T12:00:00.123456Z",FhirTimePrecision.Invalid,"")]
    public void ClinicalTimestampsRetainActualPrecisionAndNeverInventAnInstant(string text,FhirTimePrecision precision,string hl7)
    { var value=FhirClinicalTime.Parse(text); Assert.Equal(text,value.Text); Assert.Equal(precision,value.Precision); Assert.Equal(hl7,value.Hl7); Assert.Equal(precision==FhirTimePrecision.Instant,value.Instant is not null); }
    [Fact]
    public async Task ClinicalJsonIsNeverDeduplicatedAcrossDifferentRelativeReferenceScopes()
    {
        var a=JsonNode.Parse(Fixture())!.AsObject();var b=JsonNode.Parse(Fixture())!.AsObject();
        a["entry"]![0]!["fullUrl"]="urn:uuid:00000000-0000-4000-8000-000000000001";
        b["entry"]![0]!["fullUrl"]="urn:uuid:00000000-0000-4000-8000-000000000002";
        Resource(b,"Patient")["name"]![0]!["family"]="ANOTHER SYNTHETIC";
        var data=await FhirR4Importer.ImportAsync([Input(a.ToJsonString()),Input(b.ToJsonString())],true,token:Token);
        Assert.Equal(2,data.Resources.Count(r=>r.Kind==FhirResourceKind.DiagnosticReport));Assert.True(data.Summary.AmbiguousAliases>0);
        var observations=data.Resources.Where(r=>r.Id.StartsWith("4444",StringComparison.Ordinal)).ToArray();Assert.Equal(2,observations.Length);
        Assert.NotEqual(data.PatientOf(observations[0].Key),data.PatientOf(observations[1].Key));
    }
    [Fact]
    public async Task RestBaseRelativeReferencesCannotBindAnotherServerAndContainmentNeverInheritsSubject()
    {
        var text=Changed(root=>
        {
            var report=Resource(root,"DiagnosticReport");report["contained"]![0]!.AsObject().Remove("subject");
            var observation=Resource(root,"Observation");var id=(string)observation["id"]!;
            root["entry"]!.AsArray().First(e=>ReferenceEquals(e!["resource"],observation))!["fullUrl"]="https://other.example.invalid/fhir/Observation/"+id;
        });
        var data=await Import(text);var contained=data.Resources.Single(r=>r.Container is not null);Assert.Null(data.PatientOf(contained.Key));
        var observation=data.Resources.First(r=>r.Kind==FhirResourceKind.Observation);Assert.Null(data.PatientOf(observation.Key));Assert.True(data.Summary.UnresolvedReferences>0);
    }
}
