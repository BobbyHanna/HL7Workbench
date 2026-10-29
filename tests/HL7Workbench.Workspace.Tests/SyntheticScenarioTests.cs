using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class SyntheticScenarioTests : IAsyncDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private readonly DocumentManager documents = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-scenarios-" + Guid.NewGuid().ToString("N"));
    private const string Original = "MSH*$%!@*SYN-ORIGINAL\r\nPID*FICTIONAL\r\n";
    private sealed class InlineProgress(Action<SyntheticGenerationProgress> report) : IProgress<SyntheticGenerationProgress>
    { public void Report(SyntheticGenerationProgress value) => report(value); }
    public async ValueTask DisposeAsync()
    {
        var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    private async Task<MessageDocument> Open(int codePage = 65001)
    {
        var encoding = Encoding.GetEncoding(codePage);
        using var input = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(Original)).ToArray());
        return await MessageFileReader.OpenFileAsync(documents, "Synthetic source", input, Token);
    }
    private Task<SyntheticGenerationResult> Generate(SyntheticScenarioOptions? options = null, MessageDocument? target = null,
        MessageInsertionPosition position = MessageInsertionPosition.End, IProgress<SyntheticGenerationProgress>? progress = null, CancellationToken? token = null) =>
        new SyntheticGeneration(documents).GenerateScenariosAsync(options ?? new(), target, position, progress, token ?? Token);

    [Theory]
    [InlineData(SyntheticScenarioType.AdmissionDischarge)] [InlineData(SyntheticScenarioType.OrderResult)] [InlineData(SyntheticScenarioType.CombinedEncounter)]
    public async Task NewScenarioIsIndependentUnsavedAndHasCountOnlyReportBeforeOpen(SyntheticScenarioType scenario)
    {
        var options = new SyntheticScenarioOptions { Scenario = scenario, Patients = 2, EncountersPerPatient = 2 };
        var source = await Open(); var revision = source.ContentVersion; var seen = false;
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DocumentManager.ActiveDocument) && documents.ActiveDocument != source)
            { seen = true; Assert.NotNull(documents.ActiveDocument!.SyntheticGenerationReport); }
        }
        documents.PropertyChanged += Changed;
        var result = await Generate(options);
        documents.PropertyChanged -= Changed;
        var doc = result.Document; Assert.True(seen); Assert.True(result.IsNewDocument); Assert.Null(doc.SavePath);
        Assert.True(doc.IsDirty); Assert.True(doc.IsEditMode); Assert.Equal(options.SuggestedFileName, doc.Title);
        Assert.Equal(revision, source.ContentVersion); Assert.Single(source.Messages); Assert.Same(doc, documents.ActiveDocument);
        Assert.Equal(options.Normalize(), result.ScenarioOptions); Assert.Equal(options.EstimateMessageCount(), doc.MessageCount);
        var report = Assert.IsType<SyntheticGenerationReport>(doc.SyntheticGenerationReport); Assert.Same(report, result.Report);
        Assert.Equal(2, report.Patients); Assert.Equal(4, report.Encounters); Assert.Equal(doc.MessageCount, report.Messages);
        Assert.Equal(report.Messages, report.MessageTypes.Values.Sum()); Assert.False(report.IsCanceled);
        Assert.Equal(scenario == SyntheticScenarioType.AdmissionDischarge ? 0 : 4, report.Orders);
        Assert.Equal(report.Orders, report.Results); Assert.Equal(report.Results * 2, report.Observations);
        Assert.DoesNotContain("EXAMPLE", report.ToString()); Assert.DoesNotContain("SYN-000", report.ToString());
        Assert.Null(doc.GenerationReport); Assert.All(doc.Messages, m => Assert.Equal(MessageOrigin.Inserted, m.Origin));
        Directory.CreateDirectory(root); var path = Path.Combine(root, "scenario.hl7"); await doc.SaveAsync(path, Token);
        var expected = SyntheticScenarioGenerator.Generate(options, Token).GetUtf8Bytes(); Assert.Equal(expected, await File.ReadAllBytesAsync(path, Token));
        doc.Undo(); await doc.EditRefresh; Assert.Empty(doc.Messages); Assert.Same(report, doc.SyntheticGenerationReport);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(result.MessageIds, doc.Messages.Select(m => m.Id)); Assert.False(doc.IsDirty);
        using var input = File.OpenRead(path); var reopened = await MessageFileReader.OpenFileAsync(documents, "Reopened", input, Token);
        Assert.Equal(result.MessageIds.Count, reopened.MessageCount); Assert.Null(reopened.SyntheticGenerationReport);
    }

    [Theory]
    [InlineData(MessageInsertionPosition.Beginning, 0)] [InlineData(MessageInsertionPosition.BeforeCurrent, 0)]
    [InlineData(MessageInsertionPosition.AfterCurrent, 1)] [InlineData(MessageInsertionPosition.End, 1)]
    public async Task ScenarioInsertionUsesOneHistoryOperationAndPreservesOriginalIdentity(MessageInsertionPosition position, int offset)
    {
        var doc = await Open(); var originalId = doc.ActiveMessageId; var result = await Generate(target: doc, position: position); await doc.EditRefresh;
        Assert.Equal(5, doc.MessageCount); Assert.False(result.IsNewDocument); Assert.Equal(result.MessageIds, doc.Messages.Skip(offset).Take(4).Select(m => m.Id));
        Assert.Equal(result.MessageIds[0], doc.ActiveMessageId); Assert.Same(result.Report, doc.SyntheticGenerationReport);
        doc.Undo(); await doc.EditRefresh; Assert.Single(doc.Messages); Assert.Equal(originalId, doc.ActiveMessageId); Assert.False(doc.IsDirty);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(result.MessageIds, doc.Messages.Skip(offset).Take(4).Select(m => m.Id));
        await doc.NavigateAsync(originalId!.Value, Token); Assert.Equal(Original, doc.EditableText);
    }

    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)]
    public async Task ProspectiveSaveRetainsSourceEncodingAndEveryOriginalByte(int codePage)
    {
        var doc = await Open(codePage); var encoding = Encoding.GetEncoding(codePage); var originalBytes = doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length));
        var options = new SyntheticScenarioOptions { IncludeTransfer = true, ResultsPerOrder = 2 };
        await Generate(options, doc); await doc.EditRefresh; Directory.CreateDirectory(root); var path = Path.Combine(root, "mixed.hl7");
        await doc.SaveAsync(path, Token);
        Assert.Equal(originalBytes, doc.Snapshot.ReadBytes(new(0, doc.Snapshot.Length)));
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(Original + string.Concat(SyntheticScenarioGenerator.Generate(options, Token).Messages))), await File.ReadAllBytesAsync(path, Token));
    }

    [Theory]
    [InlineData("Generating")] [InlineData("Writing")] [InlineData("Indexing staged output")] [InlineData("Verifying staged bytes")]
    public async Task CancellationPreservesExistingReportMembershipDraftSelectionRevisionsAndRedo(string phase)
    {
        var doc = (await Generate()).Document; var originalReport = doc.SyntheticGenerationReport;
        doc.InsertMessage(MessageInsertionPosition.End, "MSH|^~\\&|SYN\r"); await doc.EditRefresh; doc.Undo(); await doc.EditRefresh;
        var ids = doc.Messages.Select(m => m.Id).ToArray(); var selected = doc.ActiveMessageId; var text = doc.EditableText;
        var version = doc.ContentVersion; var revision = doc.SearchRevision;
        using var cancellation = new CancellationTokenSource(); var seen = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Generate(new() { Patients = 250 }, doc, progress: new InlineProgress(p =>
        { if (p.Phase == phase) { seen = true; cancellation.Cancel(); } }), token: cancellation.Token));
        Assert.True(seen); Assert.Equal(ids, doc.Messages.Select(m => m.Id)); Assert.Equal(selected, doc.ActiveMessageId); Assert.Equal(text, doc.EditableText);
        Assert.Equal(version, doc.ContentVersion); Assert.Equal(revision, doc.SearchRevision); Assert.True(doc.CanRedo);
        Assert.Same(originalReport, doc.SyntheticGenerationReport); Assert.False(doc.IsPreparingInsertion);
        await Generate(target: doc); Assert.Equal(8, doc.MessageCount); Assert.NotSame(originalReport, doc.SyntheticGenerationReport);
    }

    [Theory]
    [InlineData("Generating")] [InlineData("Writing")] [InlineData("Verifying staged bytes")]
    public async Task CanceledPrivateOutputNeverOpensAndCanRetry(string phase)
    {
        using var cancellation = new CancellationTokenSource(); var seen = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Generate(new() { Patients = 250 }, progress: new InlineProgress(p =>
        { if (p.Phase == phase) { seen = true; cancellation.Cancel(); } }), token: cancellation.Token));
        Assert.True(seen); Assert.Empty(documents.Documents); Assert.Null(documents.ActiveDocument);
        await Generate(); Assert.Single(documents.Documents);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CapturedDestinationMutationOrClosureCannotRetargetScenario(bool close)
    {
        var doc = await Open(); var other = await Open(); var mutated = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Generate(target: doc, progress: new InlineProgress(p =>
        {
            if (p.Phase != "Generating" || mutated) return;
            mutated = true;
            if (close) documents.Close(doc.Id, discardChanges: true);
            else doc.InsertMessage(MessageInsertionPosition.Beginning, "MSH|^~\\&|SYN-CHANGE\r");
        })));
        Assert.True(mutated); Assert.Single(other.Messages); Assert.Null(other.SyntheticGenerationReport);
        if (close) await doc.Disposal; else { await doc.EditRefresh; Assert.Equal(2, doc.MessageCount); }
    }

    [Fact]
    public async Task PreCanceledAndInvalidOptionsPublishNothingAndStandaloneKeepsVersionOneContent()
    {
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Generate(token: cancellation.Token));
        await Assert.ThrowsAsync<ArgumentException>(() => Generate(new() { Patients = int.MaxValue })); Assert.Empty(documents.Documents);
        var result = await new SyntheticGeneration(documents).GenerateAsync(new() { MessageType = SyntheticMessageType.OruR01, Count = 2 }, token: Token);
        Assert.Null(result.ScenarioOptions); Assert.Equal(2, result.Report!.Results); Assert.Equal(6, result.Report.Observations);
        Assert.Equal("Standalone ORU^R01", result.Report.Scenario);
        Assert.Equal(SyntheticMessageGenerator.Generate(result.Options, Token).Messages[0], result.Document.EditableText);
    }

    [Theory]
    [InlineData(SyntheticDemographicStyle.RealisticSynthetic)] [InlineData(SyntheticDemographicStyle.ObviousPlaceholders)]
    public async Task DemographicStyleSurvivesCapturedOptionsReportHistoryAndExactSave(SyntheticDemographicStyle style)
    {
        var options = new SyntheticScenarioOptions { DemographicStyle = style, Patients = 2, EncountersPerPatient = 2, IncludeTransfer = true };
        var doc = await Open(); var before = doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length));
        var result = await Generate(options, doc); await doc.EditRefresh;
        Assert.Equal(style, result.ScenarioOptions!.DemographicStyle); Assert.Equal(style, result.Report!.DemographicStyle);
        Assert.Equal(2, result.Report.GeneratorVersion); Assert.DoesNotContain("FAIRVIEW", result.Report.ToString()); Assert.DoesNotContain("NORTH VALLEY", result.Report.ToString());
        var report = result.Report; doc.Undo(); await doc.EditRefresh; Assert.Single(doc.Messages); Assert.Same(report, doc.SyntheticGenerationReport);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(21, doc.MessageCount); Assert.Same(report, doc.SyntheticGenerationReport);
        Directory.CreateDirectory(root); var path = Path.Combine(root, "demographics.hl7"); await doc.SaveAsync(path, Token);
        Assert.Equal(before.Concat(SyntheticScenarioGenerator.Generate(options, Token).GetUtf8Bytes()), await File.ReadAllBytesAsync(path, Token));
    }
}
