using System.Text;
using System.Text.Json.Nodes;
using HL7Workbench.Core;
using HL7Workbench.Tests;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class FhirHistoryScopeTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Task<FhirImportSession> Import(params string[] inputs) => FhirR4Importer.ImportAsync(inputs.Select(text =>
        new FhirImportInput(_ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(text))))), true, token: Token);
    private static FhirGenerationPlan Plan(FhirImportSession source, FhirGenerationScope scope = FhirGenerationScope.SelectedPatient, uint seed = 42) =>
        FhirGenerationPlan.Create(source, new() { Scope = scope, PatientKey = source.Patients[0].Key, Seed = seed }, Token);

    [Fact]
    public async Task CompletePatientIncludesUnencounteredResultsWithoutManualSelectionsOrDuplicateReportLinks()
    {
        var source = await Import(FhirSyntheticHistory.History()); var plan = Plan(source); var batch = plan.Generate(Token);
        Assert.Equal(4, plan.Preview.Messages); Assert.Equal(batch.Messages.Count, plan.Preview.Messages); Assert.Equal(1, plan.Preview.PatientsIncluded);
        Assert.Equal(1, plan.Preview.EncountersConsidered); Assert.Equal(1, plan.Preview.AdtA01); Assert.Equal(1, plan.Preview.AdtA03); Assert.Equal(2, plan.Preview.OruR01);
        Assert.Equal(3, plan.Preview.Observations); Assert.Equal(batch.TotalCharacters, plan.Preview.MeasuredCharacters); Assert.True(plan.Preview.CharacterMeasurementComplete);
        Assert.Equal(0, plan.Preview.GeneratedMessages); Assert.True(plan.Preview.IsPreview); Assert.False(batch.Report.IsPreview); Assert.Equal(4, batch.Report.GeneratedMessages);
        var messages = batch.Messages.Select(text => Hl7Parser.Parse(text)).ToArray(); Assert.All(messages, m => Assert.Equal(messages[0].Get("PID-3").Raw, m.Get("PID-3").Raw));
        Assert.Equal("standalone-000", messages[0].Get("OBR-3.1").Raw); Assert.DoesNotContain(messages[0].Segments, s => s.Identifier == "PV1");
        var visits = messages.Where(m => m.Segments.Any(s => s.Identifier == "PV1")).ToArray(); Assert.All(visits, m => Assert.Equal(visits[0].Get("PV1-19").Raw, m.Get("PV1-19").Raw));
        Assert.Equal(2, messages.Single(m => m.Get("OBR-3.1").Raw == "r-000").Segments.Count(s => s.Identifier == "OBX"));
        Assert.DoesNotContain(messages, m => m.Segments.Any(s => s.Identifier == "DG1")); Assert.Equal(4, messages.Select(m => m.Get("MSH-10").Raw).Distinct().Count());
    }
    [Fact]
    public async Task EntireImportIsPatientGroupedChronologicalAndReproducibleAcrossInputOrderAndSeedChanges()
    {
        var first = FhirSyntheticHistory.History(5); var second = FhirSyntheticHistory.History(5, 5);
        var source = await Import(first, second); var reverse = await Import(second, first);
        var plan = Plan(source, FhirGenerationScope.EntireImport); var output = plan.Generate(Token);
        Assert.Equal(10, plan.Preview.PatientsIncluded); Assert.Equal(10, plan.Preview.EncountersConsidered); Assert.Equal(40, plan.Preview.Messages);
        Assert.Equal(output.GetUtf8Bytes(), Plan(reverse, FhirGenerationScope.EntireImport).Generate(Token).GetUtf8Bytes());
        var messages = output.Messages.Select(text => Hl7Parser.Parse(text)).ToArray(); Assert.Equal(40, messages.Select(m => m.Get("MSH-10").Raw).Distinct().Count());
        Assert.Equal(10, messages.Select(m => m.Get("PID-3").Raw).Distinct().Count());
        var patientOnly = Plan(source).Generate(Token).Messages.Select(text => Hl7Parser.Parse(text)).ToArray();
        Assert.Equal(patientOnly.Select(m => m.Get("PID-3").Raw), messages.Take(4).Select(m => m.Get("PID-3").Raw));
        Assert.Equal(patientOnly.Select(m => m.Get("PV1-19").Raw), messages.Take(4).Select(m => m.Get("PV1-19").Raw));
        foreach (var group in messages.Chunk(4))
        {
            Assert.Single(group.Select(m => m.Get("PID-3").Raw).Distinct());
            Assert.Equal(new[] { "R01", "A01", "R01", "A03" }, group.Select(m => m.Get("MSH-9.2").Raw));
        }
        var changed = Plan(source, FhirGenerationScope.EntireImport, 123).Generate(Token).Messages.Select(text => Hl7Parser.Parse(text)).ToArray();
        Assert.Equal(messages.Select(m => m.Get("PID-3").Raw), changed.Select(m => m.Get("PID-3").Raw));
        Assert.Equal(messages.Select(m => m.Get("PV1-19").Raw), changed.Select(m => m.Get("PV1-19").Raw));
        Assert.NotEqual(messages[0].Get("MSH-10").Raw, changed[0].Get("MSH-10").Raw);
    }
    [Fact]
    public async Task AdvancedSelectionStillDeduplicatesEncounterAndRepeatedTimelineReachability()
    {
        var source = await Import(FhirSyntheticHistory.History()); var patient = source.Patients[0].Key; var encounter = source.Encounters(patient)[0].Key;
        var events = source.Timeline(patient).Where(e => e.Kind is not (FhirEventKind.ConditionOnset or FhirEventKind.ConditionRecorded)).Select(e => new FhirEventSelection(e.ResourceKey, e.Kind)).ToArray();
        var plan = FhirGenerationPlan.Create(source, new() { Scope = FhirGenerationScope.SelectedEvents, PatientKey = patient, Encounters = [encounter, encounter], Events = events.Concat(events).ToArray() }, Token);
        Assert.Equal(4, plan.Generate(Token).Messages.Count); Assert.Equal(4, plan.Preview.Messages); Assert.Equal(3, plan.Preview.Observations);
    }
    [Fact]
    public async Task EqualDatesUseStableResourceTiesAndMissingDatesStayLastWithoutInventedDatesOrAdt()
    {
        var source = await Import(FhirSyntheticHistory.Observations(3, missingLastTime: true)); var plan = Plan(source);
        var messages = plan.Generate(Token).Messages.Select(text => Hl7Parser.Parse(text)).ToArray();
        Assert.Equal(new[] { "o-000-0000", "o-000-0001", "o-000-0002" }, messages.Select(m => m.Get("OBR-3.1").Raw));
        Assert.Equal(0, plan.Preview.AdtA01 + plan.Preview.AdtA03 + plan.Preview.AdtA04); Assert.True(string.IsNullOrEmpty(messages[^1].Get("OBR-7").Raw));
        Assert.True(string.IsNullOrEmpty(messages[^1].Get("OBX-14").Raw)); Assert.True(plan.Preview.Warnings[FhirMappingWarning.MissingOrInvalidClinicalTime] > 0);
    }
    [Theory]
    [InlineData(true, false, 2)] [InlineData(false, true, 2)]
    public async Task CompleteScopesHonorMessageModeWithoutAddingMappings(bool adt, bool oru, int expected)
    {
        var source = await Import(FhirSyntheticHistory.History()); var plan = FhirGenerationPlan.Create(source, new() { Scope = FhirGenerationScope.SelectedPatient, PatientKey = source.Patients[0].Key, Adt = adt, Oru = oru }, Token);
        Assert.Equal(expected, plan.Preview.Messages); Assert.Equal(expected, plan.Generate(Token).Messages.Count);
    }
    [Fact]
    public async Task PreviewClassifiesIneligibleUnsupportedAndUnassociatedDataWithoutPublishingErrorsOrSourceValues()
    {
        var plan = Plan(await Import(FhirSyntheticHistory.History(issues: true)), FhirGenerationScope.EntireImport);
        Assert.Equal(4, plan.Preview.Messages); Assert.Equal(2, plan.Preview.IneligibleEvents); Assert.Equal(5, plan.Preview.SkippedResources);
        Assert.Equal(2, plan.Preview.IneligibleReasons[FhirAdtIneligibility.UnsupportedStatus]);
        foreach (var reason in new[] { FhirMappingWarning.UnsupportedEvent, FhirMappingWarning.UnsupportedValue, FhirMappingWarning.UnsupportedStatus, FhirMappingWarning.UnsupportedResourceType, FhirMappingWarning.UnassociatedClinicalResource })
            Assert.True(plan.Preview.Warnings[reason] > 0);
        var report = System.Text.Json.JsonSerializer.Serialize(plan.Generate(Token).Report); Assert.DoesNotContain("SYN-HISTORY", report); Assert.DoesNotContain("SYNTHETIC SAME", report); Assert.DoesNotContain("SYNTHETIC RESULT", report);
    }
    [Fact]
    public async Task NoEligiblePatientHistoryReturnsExplicitZeroPreviewAndRefusesGeneration()
    {
        var plan = Plan(await Import(FhirSyntheticHistory.Observations(0))); Assert.Equal(0, plan.Preview.Messages); Assert.False(plan.Preview.CanGenerate);
        Assert.Equal(1, plan.Preview.PatientsIncluded); Assert.True(plan.Preview.CharacterMeasurementComplete); Assert.Empty(plan.Preview.Limits);
        Assert.Throws<InvalidOperationException>(() => plan.Generate(Token));
    }
    [Fact]
    public async Task MessageLimitCountsAllPatientsWithoutTruncationAndAllowsNarrowingToOnePatient()
    {
        var source = await Import(FhirSyntheticHistory.Observations(501, 2)); var plan = Plan(source, FhirGenerationScope.EntireImport);
        Assert.Equal(1002, plan.Preview.Messages); Assert.False(plan.Preview.CanGenerate); Assert.Contains(FhirGenerationLimit.MessageCount, plan.Preview.Limits);
        Assert.Throws<Hl7LimitException>(() => plan.Generate(Token)); Assert.Equal(501, Plan(source).Generate(Token).Messages.Count);
    }
    [Fact]
    public async Task CharacterLimitMeasuresEscapedOutputRatherThanSourceLengthAndNeverReturnsPartialBatch()
    {
        var plan = Plan(await Import(FhirSyntheticHistory.Observations(400, valueLength: 4000)));
        Assert.Equal(400, plan.Preview.Messages); Assert.Contains(FhirGenerationLimit.TotalCharacters, plan.Preview.Limits);
        Assert.True(plan.Preview.MeasuredCharacters > SyntheticMessageGenerator.MaximumCharacters); Assert.False(plan.Preview.CharacterMeasurementComplete);
        Assert.Throws<Hl7LimitException>(() => plan.Generate(Token));
    }
    [Fact]
    public async Task PreviewAndEntireImportGenerationRespectCancellation()
    {
        var source = await Import(FhirSyntheticHistory.History(10)); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => FhirGenerationPlan.Create(source, new() { Scope = FhirGenerationScope.EntireImport }, cancel.Token));
        using var during = new CancellationTokenSource(); Assert.ThrowsAny<OperationCanceledException>(() => Plan(source, FhirGenerationScope.EntireImport).Generate(during.Token, new Inline(_ => during.Cancel())));
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PreviewAlsoRefusesExistingFieldAndMessageCeilingsBeforePublication(bool oversizedMessage)
    {
        var json = JsonNode.Parse(FhirSyntheticHistory.Observations(oversizedMessage ? 500 : 1, valueLength: 4000))!.AsObject();
        var entries = json["entry"]!.AsArray();
        if (oversizedMessage)
        {
            var results = new JsonArray(); foreach (var entry in entries.Skip(1)) results.Add(new JsonObject { ["reference"] = "Observation/" + (string)entry!["resource"]!["id"]! });
            entries.Add(new JsonObject { ["resource"] = new JsonObject
            {
                ["resourceType"] = "DiagnosticReport", ["id"] = "syn-large-report", ["subject"] = new JsonObject { ["reference"] = "Patient/p-000" }, ["status"] = "final",
                ["code"] = new JsonObject { ["text"] = "SYNTHETIC PANEL" }, ["effectiveDateTime"] = "2030-01-02T12:00:00Z", ["result"] = results
            } });
        }
        else entries[0]!["resource"]!["name"]![0]!["family"] = new string('X', 5000);
        var plan = Plan(await Import(json.ToJsonString())); Assert.Contains(oversizedMessage ? FhirGenerationLimit.MessageCharacters : FhirGenerationLimit.MappedFieldCharacters, plan.Preview.Limits);
        Assert.False(plan.Preview.CanGenerate); Assert.Throws<Hl7LimitException>(() => plan.Generate(Token));
    }
    private sealed class Inline(Action<int> action) : IProgress<int> { public void Report(int value) => action(value); }
    [Fact]
    public async Task ThousandsOfEncountersKeepCompleteEligibilityCountsBeyondTheOutputCeiling()
    {
        var json = JsonNode.Parse(FhirSyntheticHistory.Observations(0))!.AsObject(); var entries = json["entry"]!.AsArray();
        for (var n = 0; n < 2000; n++)
        {
            var id = $"syn-e-{n:D4}";
            entries.Add(new JsonObject { ["fullUrl"] = "https://synthetic.example.invalid/fhir/Encounter/" + id, ["resource"] = new JsonObject
            {
                ["resourceType"] = "Encounter", ["id"] = id, ["subject"] = new JsonObject { ["reference"] = "Patient/p-000" }, ["status"] = "finished",
                ["class"] = new JsonObject { ["system"] = "http://terminology.hl7.org/CodeSystem/v3-ActCode", ["code"] = "IMP" },
                ["period"] = new JsonObject { ["start"] = "2030-01-02T10:00:00Z", ["end"] = "2030-01-03T10:00:00Z" }
            } });
        }
        var plan = Plan(await Import(json.ToJsonString()), FhirGenerationScope.EntireImport);
        Assert.Equal(2000, plan.Preview.EncountersConsidered); Assert.Equal(2000, plan.Preview.AdtA01); Assert.Equal(2000, plan.Preview.AdtA03);
        Assert.Equal(4000, plan.Preview.Messages); Assert.Equal(0, plan.Preview.IneligibleEvents); Assert.Contains(FhirGenerationLimit.MessageCount, plan.Preview.Limits);
        Assert.Throws<Hl7LimitException>(() => plan.Generate(Token));
    }
}
