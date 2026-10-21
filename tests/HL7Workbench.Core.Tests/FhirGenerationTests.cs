using System.Text;
using System.Text.Json.Nodes;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class FhirGenerationTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Fixture(string name = "patient-alpha.json") => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "fhir-r4", name));
    private static async Task<FhirImportSession> Import(string? text = null)
    { var data = text ?? Fixture(); return await FhirR4Importer.ImportAsync([new(_ => Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(data))))], true, token: Token); }
    private static FhirGenerationPlan Plan(FhirImportSession data, uint seed = 42) => FhirGenerationPlan.Create(data, new()
    { PatientKey = data.Patients[0].Key, Encounters = data.Encounters(data.Patients[0].Key).Select(e => e.Key).ToArray(), Seed = seed }, Token);
    private static JsonObject Resource(JsonObject bundle, string type) => bundle["entry"]!.AsArray().Select(n => n!["resource"]!.AsObject()).First(n => (string?)n["resourceType"] == type);
    [Fact]
    public async Task MappedAdtAndReportOruShareSourceIdentityAndRetainTypedCodesValuesUnitsDatesAndStates()
    {
        var data = await Import(); var plan = Plan(data); var output = plan.Generate(Token);
        Assert.Equal(3, output.Messages.Count); Assert.Equal(1, output.Report.AdtA01); Assert.Equal(1, output.Report.AdtA03); Assert.Equal(1, output.Report.OruR01); Assert.Equal(5, output.Report.Observations);
        var messages = output.Messages.Select(m => Hl7Parser.Parse(m)).ToArray();
        Assert.All(messages, m => { Assert.True(m.CanEdit); Assert.Equal("2.5.1", m.Get("MSH-12").Raw); Assert.Equal("T", m.Get("MSH-11").Raw); Assert.Equal(messages[0].Get("PID-3").Raw, m.Get("PID-3").Raw); Assert.Equal(messages[0].Get("PV1-19").Raw,m.Get("PV1-19").Raw); });
        Assert.Equal(messages.Length, messages.Select(m => m.Get("MSH-10").Raw).Distinct().Count()); Assert.All(messages,m=>Assert.Equal(20,m.Get("MSH-10").Raw!.Length));
        Assert.Equal("A01",messages[0].Get("MSH-9.2").Raw); Assert.Equal("A03",messages[^1].Get("MSH-9.2").Raw);
        Assert.Equal("19800203",messages[0].Get("PID-7").Raw); Assert.Equal("20300102100000+0000",messages[0].Get("EVN-6").Raw);
        var oru=messages.Single(m=>m.Get("MSH-9.1").Raw=="ORU");
        Assert.Equal("24323-8",oru.Get("OBR-4.1").Raw); Assert.Equal("LN",oru.Get("OBR-4.3").Raw);
        var glucose=oru.Segments.Single(s=>s.Identifier=="OBX" && s.GetField(3)?.Value.Raw?.StartsWith("2345-7",StringComparison.Ordinal)==true);
        Assert.Equal("NM",glucose.GetField(2)!.Value.Raw); Assert.Equal("115.25",glucose.GetField(5)!.Value.Raw); Assert.Equal("mg/dL^mg/dL^UCUM",glucose.GetField(6)!.Value.Raw); Assert.Equal("70-110",glucose.GetField(7)!.Value.Raw); Assert.Equal("H",glucose.GetField(8)!.Value.Raw);
        Assert.Equal("20300102103000+0000",glucose.GetField(14)!.Value.Raw); Assert.Null(glucose.GetField(19));
        var textIndex=oru.Segments.Where(s=>s.Identifier=="OBX").Select((s,i)=>(s,i)).Single(t=>t.s.GetField(2)!.Value.Raw=="TX").i+1;
        Assert.Equal(data.Resources.Single(r=>r.Value?.Kind==FhirValueKind.String).Value!.Text,oru.GetDecoded($"OBX[{textIndex}]-5").Text);
        Assert.Contains(oru.Segments,s=>s.Identifier=="OBX" && s.GetField(5)!.Value.Raw!.StartsWith("N^No^HL70136",StringComparison.Ordinal));
        Assert.Contains(oru.Segments,s=>s.Identifier=="OBX" && s.GetField(5)!.Value.Raw!.Contains("266919005^Never smoked^SCT",StringComparison.Ordinal) && s.GetField(11)!.Value.Raw=="C");
        Assert.DoesNotContain("SYN-ALPHA",output.Report.ToString()); Assert.DoesNotContain("EXAMPLE",output.Report.ToString());
    }
    [Fact]
    public async Task SameSourceSelectionAndSeedProduceIdenticalBytesAndSeedChangesOnlyTransportIdentity()
    {
        var first=await Import(); var second=await Import(); Assert.NotEqual(first.Id,second.Id);
        var a=Plan(first).Generate(Token); var b=Plan(second).Generate(Token); Assert.Equal(a.GetUtf8Bytes(),b.GetUtf8Bytes());
        var changed=Plan(first,43).Generate(Token); Assert.NotEqual(a.Messages[0],changed.Messages[0]);
        Assert.Equal(Hl7Parser.Parse(a.Messages[0]).Get("PID-3").Raw,Hl7Parser.Parse(changed.Messages[0]).Get("PID-3").Raw);
        Assert.Equal(Hl7Parser.Parse(a.Messages[0]).Get("PV1-19").Raw,Hl7Parser.Parse(changed.Messages[0]).Get("PV1-19").Raw);
    }
    [Theory]
    [InlineData("AMB","finished","2030-01-03T12:00:00Z",1,0,0)]
    [InlineData("EMER","in-progress","",1,0,0)]
    [InlineData("IMP","in-progress","",0,1,0)]
    [InlineData("IMP","finished","2030-01-03T12:00:00Z",0,1,1)]
    [InlineData("IMP","planned","2030-01-03T12:00:00Z",0,0,0)]
    [InlineData("IMP","finished","2030-01-01T12:00:00Z",0,0,0)]
    [InlineData("UNKNOWN","finished","2030-01-03T12:00:00Z",0,0,0)]
    public async Task AdtEventsRequireActualClassStatusAndConsistentCompleteTimes(string code,string status,string end,int registration,int admission,int discharge)
    {
        var json=JsonNode.Parse(Fixture())!.AsObject(); var encounter=Resource(json,"Encounter"); encounter["class"]!["code"]=code; encounter["status"]=status; encounter["period"]!["end"]=end;
        var plan=Plan(await Import(json.ToJsonString())); Assert.Equal(registration,plan.Preview.AdtA04); Assert.Equal(admission,plan.Preview.AdtA01); Assert.Equal(discharge,plan.Preview.AdtA03);
    }
    [Fact]
    public async Task MissingAndUnsupportedValuesStatusesAndSystemsAreNeverNormalResults()
    {
        var data=await Import(); var patient=data.Patients[0]; var unsupported=data.Resources.Where(r=>r.Value?.Kind is FhirValueKind.Missing or FhirValueKind.Unsupported).ToArray();
        var plan=FhirGenerationPlan.Create(data,new(){PatientKey=patient.Key,Adt=false,Events=unsupported.Select(r=>new FhirEventSelection(r.Key,FhirEventKind.ObservationEffective)).ToArray()},Token);
        Assert.Equal(0,plan.Preview.Messages); Assert.Equal(2,plan.Preview.Warnings[FhirMappingWarning.UnsupportedValue]); Assert.Throws<InvalidOperationException>(()=>plan.Generate(Token));
        var json=JsonNode.Parse(Fixture())!.AsObject(); Resource(json,"Observation")["status"]="unknown";
        var changed=Plan(await Import(json.ToJsonString())); Assert.True(changed.Preview.Warnings[FhirMappingWarning.UnsupportedStatus]>0);
        Resource(json,"Observation")["status"]="final"; Resource(json,"Observation")["code"]!["coding"]![0]!["system"]="https://unknown.example.invalid/code";
        changed=Plan(await Import(json.ToJsonString())); Assert.True(changed.Preview.Warnings[FhirMappingWarning.UnsupportedCodeSystem]>0);
    }
    [Fact]
    public async Task StandaloneObservationNeedsNoInventedEncounterOrPanelAndSelectionCannotCrossPatients()
    {
        var json=JsonNode.Parse(Fixture())!.AsObject(); var observation=Resource(json,"Observation"); observation.Remove("encounter");
        var data=await Import(json.ToJsonString()); var selected=data.Resources.First(r=>r.Kind==FhirResourceKind.Observation);
        var plan=FhirGenerationPlan.Create(data,new(){PatientKey=data.Patients[0].Key,Adt=false,Events=[new(selected.Key,FhirEventKind.ObservationEffective)]},Token);
        var oru=Hl7Parser.Parse(Assert.Single(plan.Generate(Token).Messages)); Assert.DoesNotContain(oru.Segments,s=>s.Identifier=="PV1"); Assert.Equal("2345-7",oru.Get("OBR-4.1").Raw); Assert.Single(oru.Segments,s=>s.Identifier=="OBX");
        Assert.Throws<ArgumentException>(()=>FhirGenerationPlan.Create(data,new(){PatientKey=data.Patients[0].Key,Encounters=[selected.Key]},Token));
    }
    [Fact]
    public async Task GenerationCancellationIsWholeBatchAndLiteralsCannotInjectSegmentsOrNullStates()
    {
        var plan=Plan(await Import()); using var cancel=new CancellationTokenSource();
        Assert.ThrowsAny<OperationCanceledException>(()=>plan.Generate(cancel.Token,new Inline<int>(_=>cancel.Cancel())));
        var json=JsonNode.Parse(Fixture())!.AsObject(); Resource(json,"Patient")["name"]![0]!["family"]="\"\"\rPID|INJECTED";
        var mapped=Plan(await Import(json.ToJsonString())).Generate(Token); Assert.All(mapped.Messages,text=>Assert.Single(Hl7Parser.Parse(text).Segments,s=>s.Identifier=="PID"));
        Assert.Equal("\"\"\rPID|INJECTED",Hl7Parser.Parse(mapped.Messages[0]).GetDecoded("PID-5.1").Text);
    }
    [Theory]
    [InlineData("1.2345678901234567890123456789","","NM","1.2345678901234567890123456789")]
    [InlineData("1e-5","","NM","0.00001")]
    [InlineData("12.5",">=","SN",">=^12.5")]
    public async Task QuantitiesPreserveExactDecimalMeaningWithoutFloatingPointRounding(string number,string comparator,string type,string expected)
    {
        var json=JsonNode.Parse(Fixture())!.AsObject();var quantity=Resource(json,"Observation")["valueQuantity"]!;quantity["value"]=JsonNode.Parse(number);quantity["comparator"]=comparator;
        var mapped=Plan(await Import(json.ToJsonString())).Generate(Token).Messages.Select(t=>Hl7Parser.Parse(t)).Single(m=>m.Get("MSH-9.1").Raw=="ORU");
        var glucose=mapped.Segments.Single(s=>s.Identifier=="OBX"&&s.GetField(3)!.Value.Raw!.StartsWith("2345-7",StringComparison.Ordinal));Assert.Equal(type,glucose.GetField(2)!.Value.Raw);Assert.Equal(expected,glucose.GetField(5)!.Value.Raw);
    }
    [Fact]
    public async Task DemographicCollisionsAreQualifiedAndConditionalRangesAreExplicitlyOmitted()
    {
        var alpha=Fixture();var beta=Fixture("patient-beta.json").Replace("SYN-BETA","SYN-ALPHA",StringComparison.Ordinal);
        var data=await FhirR4Importer.ImportAsync(new[]{alpha,beta}.Select(t=>new FhirImportInput(_=>Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(t))))),true,token:Token);
        var patients=data.Patients.Select(p=>FhirGenerationPlan.Create(data,new(){PatientKey=p.Key,Encounters=data.Encounters(p.Key).Select(e=>e.Key).ToArray()},Token)).ToArray();
        Assert.NotEqual(Hl7Parser.Parse(patients[0].Generate(Token).Messages[0]).Get("PID-3").Raw,Hl7Parser.Parse(patients[1].Generate(Token).Messages[0]).Get("PID-3").Raw);
        Assert.All(patients,p=>Assert.True(p.Preview.Warnings[FhirMappingWarning.QualifiedIdentity]>0));
        var json=JsonNode.Parse(alpha)!.AsObject();Resource(json,"Observation")["referenceRange"]![0]!["age"]=new JsonObject{["low"]=new JsonObject{["value"]=100}};
        var plan=Plan(await Import(json.ToJsonString()));var oru=plan.Generate(Token).Messages.Select(t=>Hl7Parser.Parse(t)).Single(m=>m.Get("MSH-9.1").Raw=="ORU");Assert.True(plan.Preview.Warnings[FhirMappingWarning.OmittedDetail]>0);
        Assert.Equal("",oru.Segments.Single(s=>s.Identifier=="OBX"&&s.GetField(3)!.Value.Raw!.StartsWith("2345-7",StringComparison.Ordinal)).GetField(7)!.Value.Raw);
    }
    [Theory]
    [InlineData("")] [InlineData("2030-01-02")] [InlineData("not a date")]
    public async Task AdtCannotInventMissingOrIncompleteEventTimestamps(string start)
    {
        var json=JsonNode.Parse(Fixture())!.AsObject();Resource(json,"Encounter")["period"]!["start"]=start;var plan=Plan(await Import(json.ToJsonString()));Assert.Equal(0,plan.Preview.AdtA01);Assert.Equal(0,plan.Preview.AdtA03);Assert.True(plan.Preview.Warnings[FhirMappingWarning.IneligibleAdt]>0);
    }
    [Fact]
    public async Task ConflictingVisitFullUrlsWithExplicitContainedSubjectsKeepDistinctQualifiedVisitIds()
    {
        static string Bundle(string family) => new JsonObject
        {
            ["resourceType"]="Bundle",["type"]="collection",["entry"]=new JsonArray(new JsonObject
            {
                ["fullUrl"]="urn:uuid:11111111-1111-4111-8111-111111111111",["resource"]=new JsonObject
                {
                    ["resourceType"]="Encounter",["id"]="syn-visit",["status"]="in-progress",
                    ["class"]=new JsonObject{["system"]="http://terminology.hl7.org/CodeSystem/v3-ActCode",["code"]="IMP"},
                    ["period"]=new JsonObject{["start"]="2030-01-01T12:00:00Z"},["subject"]=new JsonObject{["reference"]="#p"},
                    ["contained"]=new JsonArray(new JsonObject{["resourceType"]="Patient",["id"]="p",["name"]=new JsonArray(new JsonObject{["family"]=family})})
                }
            })
        }.ToJsonString();
        var data=await FhirR4Importer.ImportAsync(new[]{Bundle("SYNTHETIC A"),Bundle("SYNTHETIC B")}.Select(t=>new FhirImportInput(_=>Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(t))))),true,token:Token);
        var plans=data.Patients.Select(p=>FhirGenerationPlan.Create(data,new(){PatientKey=p.Key,Encounters=data.Encounters(p.Key).Select(e=>e.Key).ToArray(),Oru=false},Token)).ToArray();
        var messages=plans.Select(p=>Hl7Parser.Parse(Assert.Single(p.Generate(Token).Messages))).ToArray();
        Assert.Equal(2,messages.Select(m=>m.Get("PID-3").Raw).Distinct().Count());Assert.Equal(2,messages.Select(m=>m.Get("PV1-19").Raw).Distinct().Count());
        Assert.All(plans,p=>Assert.True(p.Preview.Warnings[FhirMappingWarning.QualifiedIdentity]>0));
    }
    [Fact]
    public async Task ReportCodingNamespaceAndAlternateLossAreDisclosedInTheCapturedReport()
    {
        var baseline=Plan(await Import()).Preview.Warnings.GetValueOrDefault(FhirMappingWarning.OmittedDetail);
        var json=JsonNode.Parse(Fixture())!.AsObject();var codes=Resource(json,"DiagnosticReport")["code"]!["coding"]!.AsArray();codes[0]!.AsObject().Remove("system");
        codes.Add(new JsonObject{["system"]="http://loinc.org",["code"]="SYN-ALT-1"});codes.Add(new JsonObject{["system"]="http://loinc.org",["code"]="SYN-ALT-2"});
        var plan=Plan(await Import(json.ToJsonString()));Assert.Equal(baseline+2,plan.Preview.Warnings[FhirMappingWarning.OmittedDetail]);
        var oru=plan.Generate(Token).Messages.Select(t=>Hl7Parser.Parse(t)).Single(m=>m.Get("MSH-9.1").Raw=="ORU");Assert.Equal("99LOCAL",oru.Get("OBR-4.3").Raw);Assert.Equal("SYN-ALT-1",oru.Get("OBR-4.4").Raw);
    }
    private sealed class Inline<T>(Action<T> report):IProgress<T>{ public void Report(T value)=>report(value); }
}
