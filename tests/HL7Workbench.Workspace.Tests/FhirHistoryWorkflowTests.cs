using System.Text.Json;
using HL7Workbench.Core;
using HL7Workbench.Tests;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class FhirHistoryWorkflowTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-fhir-history-" + Guid.NewGuid().ToString("N"));
    private readonly DocumentManager documents = new();
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private async Task<string> File(string json)
    { Directory.CreateDirectory(root); var path = Path.Combine(root, "synthetic-history.json"); await System.IO.File.WriteAllTextAsync(path, json, Token); return path; }
    private static FhirGenerationPlan Entire(FhirImportSession source) => FhirGenerationPlan.Create(source, new() { Scope = FhirGenerationScope.EntireImport }, Token);
    [Fact]
    public async Task DefaultPatientScopeGeneratesWithoutManualSelectionAndSwitchingScopesPreservesAdvancedSelections()
    {
        using var session = new SyntheticPatientsSession(documents); await session.ImportFilesAsync([await File(FhirSyntheticHistory.History())], true, Token);
        Assert.Equal(FhirGenerationScope.SelectedPatient, session.Scope); Assert.Contains("Select a patient", session.PreviewUnavailableReason);
        session.SelectPatient(session.PatientPage[0].Key); Assert.Equal("", session.PreviewUnavailableReason); Assert.Empty(session.SelectedEncounters); Assert.Empty(session.SelectedEvents);
        await session.PreviewAsync(42, true, true, SyntheticGenerationOptions.DefaultTimestamp, Token); Assert.Equal(4, session.Preview!.Messages);
        session.SetScope(FhirGenerationScope.SelectedEvents); Assert.Null(session.Preview); Assert.Contains("select at least one", session.PreviewUnavailableReason);
        var encounter = session.EncounterPage[0].Key; session.SelectEncounters([encounter]); var clinical = session.TimelinePage.First(e => e.Kind == FhirEventKind.ObservationEffective);
        var selected = new FhirEventSelection(clinical.ResourceKey, clinical.Kind); session.SelectEvents([selected]);
        session.SetScope(FhirGenerationScope.EntireImport); session.SetScope(FhirGenerationScope.SelectedPatient); session.SetScope(FhirGenerationScope.SelectedEvents);
        Assert.Equal(encounter, Assert.Single(session.SelectedEncounters)); Assert.Equal(selected, Assert.Single(session.SelectedEvents));
        await session.PreviewAsync(42, true, true, SyntheticGenerationOptions.DefaultTimestamp, Token); Assert.True(session.Preview!.CanGenerate);
        session.SetScope(FhirGenerationScope.SelectedPatient); await session.PreviewAsync(42, true, true, SyntheticGenerationOptions.DefaultTimestamp, Token); await session.GenerateAsync(Token);
        Assert.Equal(4, Assert.Single(documents.Documents).MessageCount); Assert.Equal(4, documents.Documents[0].GenerationReport!.GeneratedMessages);
    }
    [Fact]
    public async Task EntireImportPublishesOneIndependentDocumentWithNormalHistoryExtractionSaveAndCountOnlyReport()
    {
        var path = await File(FhirSyntheticHistory.History(10)); var original = await System.IO.File.ReadAllBytesAsync(path, Token);
        using var session = new SyntheticPatientsSession(documents); await session.ImportFilesAsync([path], true, Token); session.SetScope(FhirGenerationScope.EntireImport);
        Assert.Null(session.PatientKey); Assert.Equal("", session.PreviewUnavailableReason);
        await session.PreviewAsync(123, true, true, SyntheticGenerationOptions.DefaultTimestamp, Token); Assert.Equal(40, session.Preview!.Messages); await session.GenerateAsync(Token);
        var document = Assert.Single(documents.Documents); Assert.Equal(40, document.MessageCount); Assert.True(document.IsDirty); Assert.Null(document.SavePath);
        var report = document.GenerationReport!; Assert.False(report.IsPreview); Assert.Equal(10, report.PatientsIncluded); Assert.Equal(40, report.GeneratedMessages);
        var ids = document.Messages.Select(m => m.Id).ToArray(); document.Undo(); await document.EditRefresh; Assert.Equal(0, document.MessageCount); document.Redo(); await document.EditRefresh;
        Assert.Equal(ids, document.Messages.Select(m => m.Id)); using var extraction = await new FieldExtraction().RunAsync(document, ExtractionLibrary.InterfaceOverview.Configuration, token: Token); Assert.Equal(40, extraction.RowCount);
        var destination = Path.Combine(root, "combined.hl7"); await document.SaveAsync(destination, Token); Assert.False(document.IsDirty);
        Assert.Equal(Entire(session.Data!).Generate(Token).Messages.Count, document.MessageCount); Assert.Equal(original, await System.IO.File.ReadAllBytesAsync(path, Token));
        session.Clear(); Assert.Same(report, document.GenerationReport); Assert.Null(session.Data);
        var serialized = JsonSerializer.Serialize(report) + string.Join('\n', documents.Activity.Snapshot());
        Assert.DoesNotContain("SYN-HISTORY", serialized); Assert.DoesNotContain("SYNTHETIC SAME", serialized); Assert.DoesNotContain("synthetic-history.json", serialized);
    }
    [Theory]
    [InlineData("Mapping")] [InlineData("Writing")] [InlineData("Indexing staged output")] [InlineData("Verifying staged bytes")]
    public async Task EntireImportCancellationAtEveryPublicationPhaseHasNoPartialTabsAndCanRetry(string phase)
    {
        var path = await File(FhirSyntheticHistory.History(10)); var source = await FhirPatientImport.FilesAsync([path], true, token: Token); var plan = Entire(source);
        using var cancel = new CancellationTokenSource(); var reached = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FhirGenerationPublisher(documents).PublishAsync(plan, new Inline<FhirGenerationProgress>(p =>
        { if (p.Phase == phase) { reached = true; cancel.Cancel(); } }), cancel.Token));
        Assert.True(reached); Assert.Empty(documents.Documents); var retry = await new FhirGenerationPublisher(documents).PublishAsync(plan, token: Token);
        Assert.Equal(40, retry.Document.MessageCount); Assert.Single(documents.Documents);
    }
    [Theory]
    [InlineData("empty")] [InlineData("count")] [InlineData("characters")]
    public async Task EmptyOrOverLimitPreviewRefusesPublicationAndRetainsImportedSource(string kind)
    {
        var json = kind switch { "count" => FhirSyntheticHistory.Observations(1001), "characters" => FhirSyntheticHistory.Observations(400, valueLength: 4000), _ => FhirSyntheticHistory.Observations(0) };
        var path = await File(json); var original = await System.IO.File.ReadAllBytesAsync(path, Token); using var session = new SyntheticPatientsSession(documents);
        await session.ImportFilesAsync([path], true, Token); session.SetScope(FhirGenerationScope.EntireImport); await session.PreviewAsync(42, true, true, SyntheticGenerationOptions.DefaultTimestamp, Token);
        Assert.False(session.Preview!.CanGenerate); Assert.Contains(kind == "empty" ? "No eligible" : "Output limit exceeded", session.GenerationUnavailableReason); await session.GenerateAsync(Token); Assert.Empty(documents.Documents);
        var plan = Entire(session.Data!);
        if (kind == "empty") await Assert.ThrowsAsync<InvalidOperationException>(() => new FhirGenerationPublisher(documents).PublishAsync(plan, token: Token));
        else await Assert.ThrowsAsync<Hl7LimitException>(() => new FhirGenerationPublisher(documents).PublishAsync(plan, token: Token));
        Assert.Empty(documents.Documents); Assert.Equal(original, await System.IO.File.ReadAllBytesAsync(path, Token)); Assert.NotNull(session.Data);
    }
    [Fact]
    public async Task CanceledPreviewAndClearDuringGenerationPreventLatePublicationWithoutLosingPriorImport()
    {
        using var session = new SyntheticPatientsSession(documents); await session.ImportFilesAsync([await File(FhirSyntheticHistory.History(10))], true, Token); var source = session.Data;
        session.SetScope(FhirGenerationScope.EntireImport); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await session.PreviewAsync(42, true, true, SyntheticGenerationOptions.DefaultTimestamp, cancel.Token); Assert.True(session.LastCanceled); Assert.Null(session.Preview); Assert.Same(source, session.Data);
        await session.PreviewAsync(42, true, true, SyntheticGenerationOptions.DefaultTimestamp, Token); var generation = session.GenerateAsync(Token); session.Clear(); await generation;
        Assert.Null(session.Data); Assert.Empty(documents.Documents); Assert.Null(session.Preview); Assert.Equal(FhirGenerationScope.SelectedPatient, session.Scope);
    }
    private sealed class Inline<T>(Action<T> action) : IProgress<T> { public void Report(T value) => action(value); }
    public async ValueTask DisposeAsync()
    { var pending = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(pending.Select(d => d.Disposal)); if (Directory.Exists(root)) Directory.Delete(root, true); }
}
