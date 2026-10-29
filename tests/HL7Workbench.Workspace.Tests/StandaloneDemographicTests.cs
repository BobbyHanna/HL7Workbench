using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class StandaloneDemographicTests : IAsyncDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private readonly DocumentManager documents = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-realistic-standalone-" + Guid.NewGuid().ToString("N"));
    private sealed class InlineProgress(Action<SyntheticGenerationProgress> report) : IProgress<SyntheticGenerationProgress>
    { public void Report(SyntheticGenerationProgress value) => report(value); }
    private static SyntheticGenerationOptions Options => new() { DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic, Count = 3 };
    private async Task<byte[]> Bytes(MessageDocument doc, string name)
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, name); await doc.SaveAsync(path, Token);
        return await File.ReadAllBytesAsync(path, Token);
    }
    public async ValueTask DisposeAsync()
    {
        var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }

    [Theory]
    [InlineData(SyntheticMessageType.AdtA01)] [InlineData(SyntheticMessageType.AdtA04)] [InlineData(SyntheticMessageType.AdtA08)]
    [InlineData(SyntheticMessageType.OrmO01)] [InlineData(SyntheticMessageType.OruR01)]
    public async Task AutomaticRealisticBatchesResolveOnceAfterPreflightReportActualStyleVersionAndReplayExactSavedBytes(SyntheticMessageType type)
    {
        var calls = 0; var seeds = new Queue<uint>([0, uint.MaxValue]); var service = new SyntheticGeneration(documents, () => { calls++; return seeds.Dequeue(); });
        var options = Options with { MessageType = type, Prefix = "demo", SendingApplication = "CUSTOM" };
        var first = await service.GenerateAsync(options, token: Token, automaticSeed: true);
        Assert.Equal(1, calls); Assert.Equal(0u, first.Report!.Seed); Assert.Equal(first.Options.Seed, first.Report.Seed);
        Assert.Equal(SyntheticDemographicStyle.RealisticSynthetic, first.Report.DemographicStyle); Assert.Equal(2, first.Report.GeneratorVersion);
        Assert.Equal(options.Count, first.Report.Messages); Assert.Equal(options.Count, first.MessageIds.Count);
        Assert.True(first.Report.SeedWasAutomatic); Assert.Equal("CUSTOM", first.Options.SendingApplication);
        Assert.Equal((options with { Seed = 0 }).Normalize().SendingFacility, first.Options.SendingFacility);
        Assert.Equal(first.Options.SendingFacility, first.Document.Message.Get("MSH-4").Raw);
        var firstBytes = await Bytes(first.Document, "first.hl7");
        Assert.Equal(SyntheticMessageGenerator.Generate(options with { Seed = 0 }, Token).GetUtf8Bytes(), firstBytes);
        var second = await service.GenerateAsync(options, token: Token, automaticSeed: true);
        Assert.Equal(2, calls); Assert.Equal(uint.MaxValue, second.Report!.Seed);
        Assert.Equal((options with { Seed = uint.MaxValue }).Normalize().SendingFacility, second.Options.SendingFacility);
        Assert.NotEqual(firstBytes, await Bytes(second.Document, "second.hl7"));
        var replay = await service.GenerateAsync(options with { Seed = first.Report.Seed }, token: Token);
        Assert.Equal(2, calls); Assert.False(replay.Report!.SeedWasAutomatic); Assert.Equal(firstBytes, await Bytes(replay.Document, "replay.hl7"));
    }

    [Theory]
    [InlineData("Generating")] [InlineData("Writing")] [InlineData("Indexing staged output")] [InlineData("Verifying staged bytes")]
    public async Task CanceledRealisticInsertionRetainsBytesHistorySelectionAndReportThenRetryUsesOneNewSeed(string phase)
    {
        var initial = await new SyntheticGeneration(documents).GenerateAsync(Options with { Seed = 42 }, token: Token);
        var doc = initial.Document; doc.InsertMessage(MessageInsertionPosition.End, "MSH|^~\\&|SYN\r"); await doc.EditRefresh; doc.Undo(); await doc.EditRefresh;
        var ids = doc.Messages.Select(m => m.Id).ToArray(); var selected = doc.ActiveMessageId; var version = doc.ContentVersion; var revision = doc.SearchRevision; var text = doc.EditableText;
        var calls = 0; var service = new SyntheticGeneration(documents, () => (uint)(100 + ++calls));
        using var cancellation = new CancellationTokenSource(); var seen = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GenerateAsync(Options with { Count = 20 }, doc, progress: new InlineProgress(p =>
        { Assert.Equal(1, calls); if (p.Phase == phase) { seen = true; cancellation.Cancel(); } }), token: cancellation.Token, automaticSeed: true));
        Assert.True(seen); Assert.Equal(1, calls); Assert.Equal(ids, doc.Messages.Select(m => m.Id)); Assert.Equal(selected, doc.ActiveMessageId); Assert.Equal(text, doc.EditableText);
        Assert.Equal(version, doc.ContentVersion); Assert.Equal(revision, doc.SearchRevision); Assert.True(doc.CanRedo); Assert.Same(initial.Report, doc.SyntheticGenerationReport);
        var retry = await service.GenerateAsync(Options, doc, token: Token, automaticSeed: true);
        Assert.Equal(2, calls); Assert.Equal(102u, retry.Report!.Seed); Assert.Equal(102u, retry.Options.Seed); Assert.False(doc.CanRedo);
        Assert.Equal(6, doc.MessageCount); doc.Undo(); await doc.EditRefresh; Assert.Equal(ids, doc.Messages.Select(m => m.Id));
        doc.Redo(); await doc.EditRefresh; Assert.Equal(6, doc.MessageCount); Assert.Same(retry.Report, doc.SyntheticGenerationReport);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InvalidRealisticOverrideOrPreCancellationDoesNotSelectAnySeed(bool canceled)
    {
        using var cancellation = new CancellationTokenSource(); if (canceled) cancellation.Cancel();
        var calls = 0; var service = new SyntheticGeneration(documents, () => { calls++; return 1; });
        await Assert.ThrowsAnyAsync<Exception>(() => service.GenerateAsync(canceled ? Options : Options with { SendingFacility = "BAD|FIELD" }, token: cancellation.Token, automaticSeed: true));
        Assert.Equal(0, calls); Assert.Empty(documents.Documents);
    }
}
