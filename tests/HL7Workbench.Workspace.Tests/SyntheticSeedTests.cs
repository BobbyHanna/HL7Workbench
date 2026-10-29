using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class SyntheticSeedTests : IAsyncDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private readonly DocumentManager documents = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-seeds-" + Guid.NewGuid().ToString("N"));
    private sealed class InlineProgress(Action<SyntheticGenerationProgress> report) : IProgress<SyntheticGenerationProgress>
    { public void Report(SyntheticGenerationProgress value) => report(value); }
    private async Task<byte[]> Bytes(MessageDocument document, string name)
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, name); await document.SaveAsync(path, Token);
        return await File.ReadAllBytesAsync(path, Token);
    }
    public async ValueTask DisposeAsync()
    {
        var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Theory]
    [InlineData(SyntheticDemographicStyle.RealisticSynthetic)] [InlineData(SyntheticDemographicStyle.ObviousPlaceholders)]
    public async Task AutomaticOperationsResolveOneDistinctControlledSeedAndReportedSeedReproducesExactBytes(SyntheticDemographicStyle style)
    {
        var seeds = new Queue<uint>([123456, 654321]); var calls = 0;
        var service = new SyntheticGeneration(documents, () => { calls++; return seeds.Dequeue(); });
        var options = new SyntheticScenarioOptions { DemographicStyle = style, Patients = 2, EncountersPerPatient = 2, OrdersPerEncounter = 2, ResultsPerOrder = 2 };
        var first = await service.GenerateScenariosAsync(options, token: Token, automaticSeed: true); Assert.Equal(1, calls);
        var second = await service.GenerateScenariosAsync(options, token: Token, automaticSeed: true); Assert.Equal(2, calls);
        Assert.Equal(123456u, first.Report!.Seed); Assert.Equal(654321u, second.Report!.Seed); Assert.True(first.Report.SeedWasAutomatic); Assert.True(second.Report.SeedWasAutomatic);
        var firstBytes = await Bytes(first.Document, "first.hl7"); var secondBytes = await Bytes(second.Document, "second.hl7"); Assert.NotEqual(firstBytes, secondBytes);
        foreach (var result in new[] { first, second })
        {
            Assert.Equal(result.Report!.Seed, result.Options.Seed); Assert.Equal(result.Report.Seed, result.ScenarioOptions!.Seed);
            var expected = options with { Seed = result.Report.Seed };
            Assert.Equal(expected.Normalize(), result.ScenarioOptions);
            Assert.Equal(SyntheticScenarioGenerator.Generate(expected, Token).GetUtf8Bytes(), result == first ? firstBytes : secondBytes);
        }
        var replay = await service.GenerateScenariosAsync(options with { Seed = first.Report.Seed }, token: Token);
        Assert.Equal(2, calls); Assert.False(replay.Report!.SeedWasAutomatic); Assert.Equal(firstBytes, await Bytes(replay.Document, "replay.hl7"));
    }

    [Theory]
    [InlineData(0u)] [InlineData(42u)] [InlineData(uint.MaxValue)]
    public async Task ExplicitSeedsAreExactIncludingZeroAndFortyTwoAndNeverCallProvider(uint seed)
    {
        var service = new SyntheticGeneration(documents, () => throw new InvalidOperationException("Explicit seed must not request randomness."));
        var options = new SyntheticScenarioOptions { Seed = seed, IncludeTransfer = true };
        var first = await service.GenerateScenariosAsync(options, token: Token); var second = await service.GenerateScenariosAsync(options, token: Token);
        Assert.Equal(seed, first.Report!.Seed); Assert.Equal(seed, first.ScenarioOptions!.Seed); Assert.False(first.Report.SeedWasAutomatic);
        var bytes = await Bytes(first.Document, "explicit.hl7"); Assert.Equal(bytes, await Bytes(second.Document, "again.hl7"));
        Assert.Equal(SyntheticScenarioGenerator.Generate(options, Token).GetUtf8Bytes(), bytes);
    }

    [Theory]
    [InlineData("configuration")] [InlineData("canceled")] [InlineData("position")] [InlineData("closed")]
    public async Task RejectedPreflightDoesNotConsumeSeed(string reason)
    {
        var calls = 0; var service = new SyntheticGeneration(documents, () => { calls++; return 17; });
        using var cancellation = new CancellationTokenSource(); if (reason == "canceled") cancellation.Cancel();
        var options = new SyntheticScenarioOptions { Patients = reason == "configuration" ? 0 : 1 };
        MessageDocument? target = null;
        if (reason == "closed") { target = MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse("MSH|^~\\&|SYN\r")); documents.Close(target.Id, true); await target.Disposal; }
        await Assert.ThrowsAnyAsync<Exception>(() => service.GenerateScenariosAsync(options, target,
            reason == "position" ? (MessageInsertionPosition)999 : MessageInsertionPosition.End, token: cancellation.Token, automaticSeed: true));
        Assert.Equal(0, calls); Assert.Empty(documents.Documents);
    }

    [Theory]
    [InlineData("Generating")] [InlineData("Writing")] [InlineData("Indexing staged output")] [InlineData("Verifying staged bytes")]
    public async Task CanceledOperationConsumesOneSeedAndRetryUsesOneNewSeedWithoutMutatingExistingDocument(string phase)
    {
        var initial = await new SyntheticGeneration(documents).GenerateScenariosAsync(new() { Seed = 7 }, token: Token);
        var doc = initial.Document; doc.InsertMessage(MessageInsertionPosition.End, "MSH|^~\\&|SYN\r"); await doc.EditRefresh; doc.Undo(); await doc.EditRefresh;
        var ids = doc.Messages.Select(m => m.Id).ToArray(); var selected = doc.ActiveMessageId; var version = doc.ContentVersion; var revision = doc.SearchRevision; var text = doc.EditableText;
        var calls = 0; var service = new SyntheticGeneration(documents, () => { calls++; return (uint)(100 + calls); });
        using var cancellation = new CancellationTokenSource(); var seen = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GenerateScenariosAsync(new() { Patients = 20 }, doc, progress: new InlineProgress(p =>
        { Assert.Equal(1, calls); if (p.Phase == phase) { seen = true; cancellation.Cancel(); } }), token: cancellation.Token, automaticSeed: true));
        Assert.True(seen); Assert.Equal(1, calls); Assert.Equal(ids, doc.Messages.Select(m => m.Id)); Assert.Equal(selected, doc.ActiveMessageId); Assert.Equal(text, doc.EditableText);
        Assert.Equal(version, doc.ContentVersion); Assert.Equal(revision, doc.SearchRevision); Assert.True(doc.CanRedo); Assert.Same(initial.Report, doc.SyntheticGenerationReport);
        var retry = await service.GenerateScenariosAsync(new(), doc, token: Token, automaticSeed: true);
        Assert.Equal(2, calls); Assert.Equal(102u, retry.Report!.Seed); Assert.Equal(102u, retry.ScenarioOptions!.Seed); Assert.False(doc.CanRedo);
    }

    [Fact]
    public async Task CancellationInsideSeedProviderStopsBeforeGenerationAndExplicitStandaloneDoesNotUseProvider()
    {
        using var cancellation = new CancellationTokenSource(); var calls = 0;
        var service = new SyntheticGeneration(documents, () => { calls++; cancellation.Cancel(); return 0; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GenerateScenariosAsync(new(), token: cancellation.Token, automaticSeed: true));
        Assert.Equal(1, calls); Assert.Empty(documents.Documents);
        var standalone = await service.GenerateAsync(new(), token: Token); Assert.Equal(1, calls); Assert.Equal(42u, standalone.Report!.Seed);
        Assert.False(standalone.Report.SeedWasAutomatic); Assert.Equal(SyntheticMessageGenerator.Generate(new(), Token).Messages[0], standalone.Document.EditableText);
    }
}
