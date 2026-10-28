using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class FhirWorkflowTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-fhir-workflow-" + Guid.NewGuid().ToString("N"));
    private readonly DocumentManager documents = new();
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Fixtures => Path.Combine(AppContext.BaseDirectory, "Fixtures", "fhir-r4");
    private async Task<string> FileCopy(string file = "patient-alpha.json")
    { Directory.CreateDirectory(root); var path = Path.Combine(root,file); await File.WriteAllBytesAsync(path,await File.ReadAllBytesAsync(Path.Combine(Fixtures,file),Token),Token); return path; }
    private static FhirGenerationPlan Plan(FhirImportSession data) => FhirGenerationPlan.Create(data,new(){PatientKey=data.Patients[0].Key,Encounters=data.Encounters(data.Patients[0].Key).Select(e=>e.Key).ToArray()},Token);
    [Fact]
    public async Task LocalMultipleFilesAndFolderImportAreReadOnlyOfflineAndHaveStableFingerprints()
    {
        var a=await FileCopy(); var b=await FileCopy("patient-beta.json"); var before=new[]{(a,SHA256.HashData(await File.ReadAllBytesAsync(a,Token))),(b,SHA256.HashData(await File.ReadAllBytesAsync(b,Token)))};
        var files=await FhirPatientImport.FilesAsync([b,a],true,token:Token); var folder=await FhirPatientImport.FolderAsync(root,true,token:Token);
        var withoutAcknowledgment=await FhirPatientImport.FilesAsync([b,a],token:Token); Assert.Equal(files.Fingerprint,withoutAcknowledgment.Fingerprint);
        Assert.Equal(folder.Fingerprint,(await FhirPatientImport.FolderAsync(root,token:Token)).Fingerprint);
        Assert.Equal(2,files.Patients.Count); Assert.Equal(files.Fingerprint,folder.Fingerprint); Assert.Equal(Plan(files).Generate(Token).GetUtf8Bytes(),Plan(folder).Generate(Token).GetUtf8Bytes());
        foreach(var (path,hash) in before) Assert.Equal(hash,SHA256.HashData(await File.ReadAllBytesAsync(path,Token)));
        await Assert.ThrowsAsync<InvalidDataException>(()=>FhirPatientImport.FilesAsync(["https://synthetic.example.invalid/patient.json"],true,token:Token));
        if(OperatingSystem.IsWindows()) await Assert.ThrowsAsync<InvalidDataException>(()=>FhirPatientImport.FilesAsync([@"\\synthetic.invalid\share\patient.json"],true,token:Token));
    }
    [Fact]
    public async Task FilenameDoesNotConferSafetyAndFolderDiscoveryDoesNotRecurse()
    {
        var path=await FileCopy(); await Assert.ThrowsAsync<InvalidOperationException>(()=>FhirPatientImport.FilesAsync([path],false,token:Token));
        Directory.CreateDirectory(Path.Combine(root,"nested")); await File.WriteAllTextAsync(Path.Combine(root,"nested","broken.json"),"not JSON",Token);
        var data=await FhirPatientImport.FolderAsync(root,true,token:Token); Assert.Single(data.Patients); Assert.Equal(1,data.Summary.Files);
    }
    [Fact]
    public async Task PublicationUsesNormalInsertedHistorySaveExtractionAndIndependentCountOnlyReport()
    {
        var data=await FhirPatientImport.FilesAsync([await FileCopy()],true,token:Token); var plan=Plan(data);
        var result=await new FhirGenerationPublisher(documents).PublishAsync(plan,token:Token); var doc=result.Document;
        Assert.Equal(3,doc.MessageCount); Assert.True(doc.IsDirty); Assert.Null(doc.SavePath); Assert.True(doc.HasGeneratedFileName); Assert.Same(result.Report,doc.GenerationReport);
        Assert.All(doc.Messages,m=>{Assert.Equal(MessageOrigin.Inserted,m.Origin);Assert.Null(m.SourceOrdinal);});
        var ids=doc.Messages.Select(m=>m.Id).ToArray(); doc.Undo();await doc.EditRefresh;Assert.Equal(0,doc.MessageCount);doc.Redo();await doc.EditRefresh;Assert.Equal(ids,doc.Messages.Select(m=>m.Id));
        using var extraction=await new FieldExtraction().RunAsync(doc,ExtractionLibrary.InterfaceOverview.Configuration,token:Token);
        Assert.Equal(3,extraction.RowCount); Assert.Equal("SYN-ALPHA",extraction.ReadRow(0).Values[2].Text.Split('^')[0]);
        var save=Path.Combine(root,"generated.hl7");await doc.SaveAsync(save,Token);Assert.False(doc.IsDirty);Assert.Equal(plan.Generate(Token).GetUtf8Bytes(),await File.ReadAllBytesAsync(save,Token));
        var report=JsonSerializer.Serialize(result.Report);foreach(var secret in new[]{"SYN-ALPHA","EXAMPLE","Glucose","115.25","patient-alpha.json"})Assert.DoesNotContain(secret,report);
        var log=string.Join('\n',documents.Activity.Snapshot());foreach(var secret in new[]{"SYN-ALPHA","EXAMPLE","Glucose","115.25","patient-alpha.json"})Assert.DoesNotContain(secret,log);
        var second=await new FhirGenerationPublisher(documents).PublishAsync(plan,token:Token);Assert.NotEqual(doc.DisplayTitle,second.Document.DisplayTitle);Assert.Null(second.Document.SavePath);
    }
    [Theory]
    [InlineData("Mapping")] [InlineData("Writing")] [InlineData("Indexing staged output")] [InlineData("Verifying staged bytes")]
    public async Task CancellationAtEveryPublicationPhaseNeverOpensAPartialDocument(string phase)
    {
        var path=await FileCopy();var original=await File.ReadAllBytesAsync(path,Token);var data=await FhirPatientImport.FilesAsync([path],true,token:Token);var plan=Plan(data);using var cancel=new CancellationTokenSource();var reached=false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new FhirGenerationPublisher(documents).PublishAsync(plan,new Inline<FhirGenerationProgress>(p=>{if(p.Phase==phase){reached=true;cancel.Cancel();}}),cancel.Token));
        Assert.True(reached);Assert.Empty(documents.Documents);Assert.Equal(original,await File.ReadAllBytesAsync(path,Token));
        var retry=await new FhirGenerationPublisher(documents).PublishAsync(plan,token:Token);Assert.Equal(3,retry.Document.MessageCount);
    }
    [Fact]
    public async Task CapturedSessionConflictBlocksPublicationAndNeverChangesAnExistingHl7Document()
    {
        using var input=new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN|TEST|DEST|TEST|20300101120000||ADT^A01|SYN-EXISTING|T|2.5.1\rPID|1\r"));
        var existing=await MessageFileReader.OpenFileAsync(documents,"Existing synthetic",input,Token);var bytes=existing.Snapshot!.ReadBytes(new(0,existing.Snapshot.Length));var revision=existing.SearchRevision;
        var data=await FhirPatientImport.FilesAsync([await FileCopy()],true,token:Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new FhirGenerationPublisher(documents).PublishAsync(Plan(data),token:Token,captureIsCurrent:()=>false));
        Assert.Same(existing,Assert.Single(documents.Documents));Assert.Equal(revision,existing.SearchRevision);Assert.False(existing.IsDirty);Assert.Equal(bytes,existing.Snapshot.ReadBytes(new(0,existing.Snapshot.Length)));
    }
    [Fact]
    public async Task FailedOrCanceledReplacementRetainsSessionAndClearOrDisposePreventsLateImport()
    {
        var path=await FileCopy();using var session=new SyntheticPatientsSession(documents);await session.ImportFilesAsync([path],true,Token);var original=session.Data;
        var broken=Path.Combine(root,"SYN-SECRET.json");await File.WriteAllTextAsync(broken,"{\"SYN-SECRET\":",Token);await session.ImportFilesAsync([broken],true,Token);
        Assert.Same(original,session.Data);Assert.DoesNotContain("SYN-SECRET",session.Status);
        using var cancel=new CancellationTokenSource();cancel.Cancel();await session.ImportFilesAsync([path],true,cancel.Token);Assert.Same(original,session.Data);Assert.True(session.LastCanceled);
        var pending=session.ImportFilesAsync([path],true,Token);session.Clear();await pending;Assert.Null(session.Data);Assert.Empty(session.PatientPage);Assert.Null(session.Preview);
        await session.ImportFilesAsync([path],true,Token);Assert.NotNull(session.Data);session.Dispose();Assert.Null(session.Data);
    }
    [Fact]
    public async Task PatientSearchSelectionPreviewAndClearingRemainSessionLocalAndDoNotRetargetTabs()
    {
        await FileCopy();await FileCopy("patient-beta.json");using var session=new SyntheticPatientsSession(documents);await session.ImportFolderAsync(root,true,Token);var data=session.Data!;
        await session.FilterPatientsAsync("syn-alpha",Token);Assert.Single(session.PatientPage);session.SelectPatient(session.PatientPage[0].Key);Assert.Single(session.EncounterPage);
        session.SelectEncounters(session.EncounterPage.Select(r=>r.Key));await session.PreviewAsync(123,true,true,SyntheticGenerationOptions.DefaultTimestamp,Token);Assert.NotNull(session.Preview);
        var published=0;session.Generated+=_=>published++;await session.GenerateAsync(Token);Assert.Equal(1,published);var doc=Assert.Single(documents.Documents);var report=doc.GenerationReport;
        session.Clear();Assert.Null(session.Data);Assert.Equal("",session.PatientFilter);Assert.Empty(session.TimelinePage);Assert.Same(report,doc.GenerationReport);Assert.True(doc.IsDirty);
        await session.ImportFolderAsync(root,true,Token);Assert.NotEqual(data.Id,session.Data!.Id);Assert.Empty(session.SelectedEncounters);Assert.Null(session.Preview);
    }
    [Fact]
    public async Task CanceledMultiFileReadDisposesStreamsAndDoesNotModifySources()
    {
        var a=await FileCopy();var b=await FileCopy("patient-beta.json");using var cancellation=new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>FhirPatientImport.FilesAsync([a,b],true,progress:new Inline<FhirImportProgress>(_=>cancellation.Cancel()),token:cancellation.Token));
        using var exclusive=new FileStream(a,FileMode.Open,FileAccess.Read,FileShare.None);Assert.True(exclusive.Length>0);
    }
    [Fact]
    public async Task LargeGraphPagesIndexesAndReferenceCyclesRemainBounded()
    {
        Directory.CreateDirectory(root);var count=Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS")=="1"?10_000:300;
        var json=new StringBuilder("{\"resourceType\":\"Bundle\",\"type\":\"collection\",\"entry\":[{\"resource\":{\"resourceType\":\"Patient\",\"id\":\"syn-large\",\"name\":[{\"family\":\"SYNTHETIC\"}]}}");
        for(var n=0;n<count;n++)json.Append(", {\"resource\":{\"resourceType\":\"Observation\",\"id\":\"o").Append(n).Append("\",\"status\":\"final\",\"subject\":{\"reference\":\"Patient/syn-large\"},\"derivedFrom\":[{\"reference\":\"Observation/o").Append((n+1)%count).Append("\"}],\"code\":{\"coding\":[{\"system\":\"http://loinc.org\",\"code\":\"2345-7\"}]},\"effectiveDateTime\":\"2030-01-01T12:00:00Z\",\"valueQuantity\":{\"value\":1}}}");
        json.Append("]}");var path=Path.Combine(root,"large.json");await File.WriteAllTextAsync(path,json.ToString(),Token);var before=GC.GetTotalMemory(true);
        using var session=new SyntheticPatientsSession(documents);await session.ImportFilesAsync([path],true,Token);var retained=GC.GetTotalMemory(true)-before;
        Assert.NotNull(session.Data);Assert.Equal(count+1,session.Data.Resources.Count);Assert.True(retained<96L*1024*1024,"Projected graph and indexes must remain bounded, not retain raw Bundle JSON.");
        session.SelectPatient(session.PatientPage[0].Key);Assert.Equal(100,session.TimelinePage.Count);session.SetTimelinePage(session.TimelinePageCount-1);Assert.InRange(session.TimelinePage.Count,1,100);
        Assert.Equal(count,session.Data.Summary.UnsupportedReferences);Assert.Empty(documents.Documents);session.Clear();Assert.Null(session.Data);
    }
    private sealed class Inline<T>(Action<T> report):IProgress<T>{public void Report(T value)=>report(value);}
    [Fact]
    public async Task InvalidFileBudgetsFailBeforeEnumeratingOrDiscoveringInputs()
    {
        var unexpectedEnumeration=Enumerable.Range(0,1).Select<int,string>(_=>throw new InvalidOperationException("Must not enumerate."));
        await Assert.ThrowsAsync<ArgumentException>(()=>FhirPatientImport.FilesAsync(unexpectedEnumeration,true,new(){Files=int.MaxValue},token:Token));
        await Assert.ThrowsAsync<ArgumentException>(()=>FhirPatientImport.FolderAsync(root,true,new(){Files=0},token:Token));
        Assert.False(Directory.Exists(root));
    }
    public async ValueTask DisposeAsync(){var pending=documents.Documents.ToArray();documents.Dispose();await Task.WhenAll(pending.Select(d=>d.Disposal));if(Directory.Exists(root))Directory.Delete(root,true);}
}
