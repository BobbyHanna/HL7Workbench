using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class StandaloneSeedTests : IAsyncDisposable
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private readonly DocumentManager documents = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-standalone-seeds-" + Guid.NewGuid().ToString("N"));
    private sealed class InlineProgress(Action<SyntheticGenerationProgress> report) : IProgress<SyntheticGenerationProgress>
    { public void Report(SyntheticGenerationProgress value) => report(value); }
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
    public async Task EveryStandaloneTypeUsesOneFreshControlledSeedAndReportCanReproduceExactVersionOneBytes(SyntheticMessageType type)
    {
        var calls = 0; var seeds = new Queue<uint>([555, 777]); var service = new SyntheticGeneration(documents, () => { calls++; return seeds.Dequeue(); });
        var options = new SyntheticGenerationOptions { MessageType = type, Count = 3 };
        var first = await service.GenerateAsync(options, token: Token, automaticSeed: true); Assert.Equal(1, calls);
        var second = await service.GenerateAsync(options, token: Token, automaticSeed: true); Assert.Equal(2, calls);
        var firstBytes = await Bytes(first.Document, "first.hl7"); var secondBytes = await Bytes(second.Document, "second.hl7"); Assert.NotEqual(firstBytes, secondBytes);
        foreach (var result in new[] { first, second })
        {
            Assert.Null(result.ScenarioOptions); Assert.True(result.Report!.SeedWasAutomatic); Assert.Equal(1, result.Report.GeneratorVersion);
            Assert.Equal(result.Options.Seed, result.Report.Seed); Assert.Equal(SyntheticDemographicStyle.ObviousPlaceholders, result.Report.DemographicStyle);
            Assert.Equal(SyntheticMessageGenerator.Generate(options with { Seed = result.Report.Seed }, Token).GetUtf8Bytes(), result == first ? firstBytes : secondBytes);
            foreach (var id in result.MessageIds)
            { await result.Document.NavigateAsync(id, Token); Assert.StartsWith("SYNTHETIC", result.Document.Message.Get("PID-5.1").Raw); }
        }
        Assert.Equal(555u, first.Report!.Seed); Assert.Equal(777u, second.Report!.Seed);
        var replay = await service.GenerateAsync(options with { Seed = first.Report.Seed }, token: Token);
        Assert.Equal(2, calls); Assert.False(replay.Report!.SeedWasAutomatic); Assert.Equal(firstBytes, await Bytes(replay.Document, "replay.hl7"));
    }

    public static IEnumerable<object[]> ExplicitCases => Enum.GetValues<SyntheticMessageType>()
        .SelectMany(type => new uint[] { 0, 42, uint.MaxValue }.Select(seed => new object[] { type, seed }));

    [Theory]
    [MemberData(nameof(ExplicitCases))]
    public async Task AllTypesAndExplicitBoundarySeedsKeepTheOriginalAlgorithmAndNeverRequestRandomness(SyntheticMessageType type, uint seed)
    {
        var service = new SyntheticGeneration(documents, () => throw new InvalidOperationException("Explicit seed must bypass provider."));
        var options = new SyntheticGenerationOptions { MessageType = type, Seed = seed, Count = 2 };
        var result = await service.GenerateAsync(options, token: Token); var repeat = await service.GenerateAsync(options, token: Token);
        var bytes = await Bytes(result.Document, "explicit.hl7"); Assert.Equal(SyntheticMessageGenerator.Generate(options, Token).GetUtf8Bytes(), bytes);
        Assert.Equal(bytes, await Bytes(repeat.Document, "repeat.hl7")); Assert.Equal(seed, result.Report!.Seed); Assert.False(result.Report.SeedWasAutomatic);
        Assert.Equal(options.Normalize(), result.Options);
    }

    [Theory]
    [InlineData("Generating")] [InlineData("Writing")] [InlineData("Indexing staged output")] [InlineData("Verifying staged bytes")]
    public async Task AutomaticStandaloneCancellationPreservesDestinationAndRetryDrawsOnlyOneNewSeed(string phase)
    {
        var first = await new SyntheticGeneration(documents).GenerateAsync(new() { Seed = 42 }, token: Token); var doc = first.Document;
        doc.InsertMessage(MessageInsertionPosition.End, "MSH|^~\\&|SYN\r"); await doc.EditRefresh; doc.Undo(); await doc.EditRefresh;
        var ids = doc.Messages.Select(m => m.Id).ToArray(); var selected = doc.ActiveMessageId; var version = doc.ContentVersion; var revision = doc.SearchRevision; var text = doc.EditableText;
        var calls = 0; var service = new SyntheticGeneration(documents, () => (uint)++calls); using var cancellation = new CancellationTokenSource(); var seen = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GenerateAsync(new() { Count = 100 }, doc, progress: new InlineProgress(p =>
        { Assert.Equal(1, calls); if (p.Phase == phase) { seen = true; cancellation.Cancel(); } }), token: cancellation.Token, automaticSeed: true));
        Assert.True(seen); Assert.Equal(1, calls); Assert.Equal(ids, doc.Messages.Select(m => m.Id)); Assert.Equal(selected, doc.ActiveMessageId); Assert.Equal(text, doc.EditableText);
        Assert.Equal(version, doc.ContentVersion); Assert.Equal(revision, doc.SearchRevision); Assert.True(doc.CanRedo); Assert.Same(first.Report, doc.SyntheticGenerationReport);
        var retry = await service.GenerateAsync(new(), doc, token: Token, automaticSeed: true); Assert.Equal(2, calls); Assert.Equal(2u, retry.Report!.Seed); Assert.False(doc.CanRedo);
    }

    [Theory]
    [InlineData("count")] [InlineData("type")] [InlineData("position")] [InlineData("canceled")] [InlineData("closed")]
    public async Task InvalidStandalonePreflightAndPreCancellationDoNotConsumeSeeds(string reason)
    {
        var calls = 0; var service = new SyntheticGeneration(documents, () => { calls++; return 0; });
        var options = new SyntheticGenerationOptions { Count = reason == "count" ? 0 : 1, MessageType = reason == "type" ? (SyntheticMessageType)999 : SyntheticMessageType.AdtA01 };
        using var cancellation = new CancellationTokenSource(); if (reason == "canceled") cancellation.Cancel();
        MessageDocument? target = null;
        if (reason == "closed") { target = MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse("MSH|^~\\&|SYN\r")); documents.Close(target.Id, true); await target.Disposal; }
        await Assert.ThrowsAnyAsync<Exception>(() => service.GenerateAsync(options, target, reason == "position" ? (MessageInsertionPosition)999 : MessageInsertionPosition.End,
            token: cancellation.Token, automaticSeed: true));
        Assert.Equal(0, calls); Assert.Empty(documents.Documents);
    }

    [Fact]
    public async Task CancellationDuringStandaloneSeedResolutionNeverPublishesAndSeedZeroIsARealResolvedValue()
    {
        using var cancellation = new CancellationTokenSource(); var calls = 0;
        var service = new SyntheticGeneration(documents, () => { calls++; cancellation.Cancel(); return 0; });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.GenerateAsync(new(), token: cancellation.Token, automaticSeed: true));
        Assert.Equal(1, calls); Assert.Empty(documents.Documents);
        var result = await new SyntheticGeneration(documents, () => 0).GenerateAsync(new(), token: Token, automaticSeed: true);
        Assert.Equal(0u, result.Report!.Seed); Assert.True(result.Report.SeedWasAutomatic);
        Assert.Equal(SyntheticMessageGenerator.Generate(new() { Seed = 0 }, Token).GetUtf8Bytes(), await Bytes(result.Document, "zero.hl7"));
    }
}
