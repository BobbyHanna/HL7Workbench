using HL7Workbench.Core;
using HL7Workbench.Tests;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class FhirDirectGenerationTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-fhir-direct-" + Guid.NewGuid().ToString("N"));
    private readonly DocumentManager documents = new();
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static DateTimeOffset At => SyntheticGenerationOptions.DefaultTimestamp;
    private async Task<string> Input(string json)
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "synthetic.json");
        await File.WriteAllTextAsync(path, json, Token); return path;
    }
    private async Task<byte[]> Bytes(MessageDocument document, string name)
    {
        var path = Path.Combine(root, name + ".hl7"); await document.SaveAsync(path, Token); return await File.ReadAllBytesAsync(path, Token);
    }
    [Fact]
    public async Task DirectAndPreparedGenerationHaveIdenticalBytesAndReuseOnlyCurrentPreparation()
    {
        var path = await Input(FhirSyntheticHistory.History(2, issues: true)); var original = await File.ReadAllBytesAsync(path, Token);
        using var session = new SyntheticPatientsSession(documents); await session.ImportFilesAsync([path], Token);
        await session.GenerateAsync(42, true, true, At, Token); Assert.Empty(documents.Documents);
        session.SelectPatient(session.PatientPage[0].Key); Assert.Null(session.Preview);
        await session.GenerateAsync(42, true, true, At, Token);
        var direct = Assert.Single(documents.Documents); Assert.Equal(4, direct.MessageCount); Assert.True(direct.IsDirty); Assert.Null(direct.SavePath);
        var report = session.Preview!; Assert.True(report.CharacterMeasurementComplete); Assert.NotEmpty(report.Warnings);
        Assert.NotNull(direct.GenerationReport); Assert.False(direct.GenerationReport.IsPreview); Assert.NotEmpty(direct.GenerationReport.Warnings);
        await session.PreviewAsync(42, true, true, At, Token); Assert.Same(report, session.Preview); Assert.Single(documents.Documents);
        await session.GenerateAsync(42, true, true, At, Token); Assert.Same(report, session.Preview);
        var directBytes = await Bytes(direct, "direct"); Assert.Equal(directBytes, await Bytes(documents.Documents[1], "prepared"));
        await session.GenerateAsync(42, true, true, At, Token);
        Assert.Equal(directBytes, await Bytes(documents.Documents[2], "repeat"));
        Assert.Equal(original, await File.ReadAllBytesAsync(path, Token));
        var logs = string.Join('\n', documents.Activity.Snapshot()); Assert.DoesNotContain("SYN-HISTORY", logs); Assert.DoesNotContain("SYNTHETIC SAME", logs);
    }
    [Theory]
    [InlineData("seed")] [InlineData("timestamp")] [InlineData("adt")] [InlineData("oru")]
    public async Task ScalarRequestChangesCannotReuseAStalePreviewWithoutUiInvalidation(string changed)
    {
        using var session = new SyntheticPatientsSession(documents); await session.ImportFilesAsync([await Input(FhirSyntheticHistory.History())], Token);
        session.SelectPatient(session.PatientPage[0].Key); await session.PreviewAsync(42, true, true, At, Token); var previous = session.Preview!;
        var seed = changed == "seed" ? 123u : 42u; var time = changed == "timestamp" ? At.AddDays(1) : At;
        await session.GenerateAsync(seed, changed != "adt", changed != "oru", time, Token);
        Assert.NotSame(previous, session.Preview); var report = Assert.Single(documents.Documents).GenerationReport!;
        Assert.Equal(seed, report.Seed); Assert.Equal(time, report.TransportTimestamp);
        Assert.Equal(changed == "adt" ? 2 : changed == "oru" ? 2 : 4, report.GeneratedMessages);
    }
    [Fact]
    public async Task DefaultFullScopesAndExplicitSelectionIncludingClearedSelectionKeepTheirMeaning()
    {
        using var session = new SyntheticPatientsSession(documents); await session.ImportFilesAsync([await Input(FhirSyntheticHistory.History(2))], Token);
        session.SelectPatient(session.PatientPage[0].Key); session.SetScope(FhirGenerationScope.SelectedEvents);
        var item = session.TimelinePage.First(e => session.Data!.Resource(e.ResourceKey).Id.StartsWith("standalone-", StringComparison.Ordinal));
        var selected = new FhirEventSelection(item.ResourceKey, item.Kind); session.SelectEvents([selected]);
        await session.GenerateAsync(42, true, true, At, Token); Assert.Equal(1, Assert.Single(documents.Documents).MessageCount); Assert.Equal(selected, Assert.Single(session.SelectedEvents));
        session.ClearSelection(); Assert.Null(session.Preview); Assert.Contains("at least one", session.GenerationUnavailableReason);
        await session.GenerateAsync(42, true, true, At, Token); Assert.Single(documents.Documents);
        session.SetScope(FhirGenerationScope.SelectedPatient); await session.GenerateAsync(42, true, true, At, Token); Assert.Equal(4, documents.Documents[1].MessageCount);
        session.ClearSelection(); session.SetScope(FhirGenerationScope.EntireImport); await session.GenerateAsync(42, true, true, At, Token); Assert.Equal(8, documents.Documents[2].MessageCount);
        var prior = session.Preview; session.SelectPatient(session.PatientPage[1].Key); Assert.Null(session.Preview);
        session.SetScope(FhirGenerationScope.SelectedPatient); await session.GenerateAsync(42, true, true, At, Token); Assert.NotSame(prior, session.Preview);
        var captured = session.Data; await session.ImportFilesAsync([await Input(FhirSyntheticHistory.History())], Token);
        Assert.NotSame(captured, session.Data); Assert.Null(session.Preview); Assert.Null(session.PatientKey);
    }
    [Theory]
    [InlineData("empty")] [InlineData("count")] [InlineData("characters")] [InlineData("configuration")]
    public async Task DirectPreparationBlocksEmptyOverLimitAndInvalidRequestsWithoutPublication(string kind)
    {
        var json = kind switch { "count" => FhirSyntheticHistory.Observations(1001), "characters" => FhirSyntheticHistory.Observations(400, valueLength: 4000), "empty" => FhirSyntheticHistory.Observations(0), _ => FhirSyntheticHistory.History() };
        var path = await Input(json); var original = await File.ReadAllBytesAsync(path, Token);
        using var session = new SyntheticPatientsSession(documents); await session.ImportFilesAsync([path], Token); session.SetScope(FhirGenerationScope.EntireImport);
        await session.GenerateAsync(42, kind != "configuration", kind != "configuration", At, Token);
        Assert.Empty(documents.Documents); Assert.False(session.IsBusy); Assert.NotNull(session.Data);
        Assert.Contains(kind == "configuration" ? "failed" : kind == "empty" ? "No eligible" : "Output limit exceeded", session.Status);
        if (kind != "configuration") Assert.False(session.Preview!.CanGenerate);
        Assert.Equal(original, await File.ReadAllBytesAsync(path, Token));
    }
    [Theory]
    [InlineData("prepare")] [InlineData("publish")] [InlineData("clear")] [InlineData("invalidate")]
    public async Task CancellationAndRevisionChangesDuringDirectGenerationNeverPublishAndRestoreBusyState(string phase)
    {
        using var session = new SyntheticPatientsSession(documents); await session.ImportFilesAsync([await Input(FhirSyntheticHistory.History(10))], Token);
        session.SetScope(FhirGenerationScope.EntireImport); var imported = session.Data; var reached = false;
        session.PropertyChanged += (_, _) =>
        {
            if (reached || session.Status != (phase == "prepare" ? "Preparing generation eligibility and output size…" : "Generating HL7…")) return;
            reached = true;
            if (phase == "clear") session.Clear(); else if (phase == "invalidate") session.InvalidatePreview(); else session.Cancel();
        };
        await session.GenerateAsync(42, true, true, At, Token);
        Assert.True(reached); Assert.Empty(documents.Documents); Assert.True(session.LastCanceled); Assert.False(session.IsBusy);
        if (phase == "clear") Assert.Null(session.Data); else
        {
            Assert.Same(imported, session.Data); await session.GenerateAsync(42, true, true, At, Token); Assert.Equal(40, Assert.Single(documents.Documents).MessageCount);
        }
    }
    public async ValueTask DisposeAsync()
    {
        var pending = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(pending.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
