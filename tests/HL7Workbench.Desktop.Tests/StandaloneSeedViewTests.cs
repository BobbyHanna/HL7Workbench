using System.Globalization;
using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class StandaloneSeedViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static async Task<byte[]> Bytes(SyntheticGenerationResult result)
    {
        var text = new StringBuilder(); foreach (var id in result.MessageIds) { await result.Document.NavigateAsync(id, Token); text.Append(result.Document.EditableText); }
        return Encoding.UTF8.GetBytes(text.ToString());
    }
    private static async Task Drain(DocumentManager documents)
    { var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal)); }

    [Theory]
    [InlineData(SyntheticMessageType.AdtA01)] [InlineData(SyntheticMessageType.AdtA04)] [InlineData(SyntheticMessageType.AdtA08)]
    [InlineData(SyntheticMessageType.OrmO01)] [InlineData(SyntheticMessageType.OruR01)]
    public async Task BlankStandaloneDraftUsesFreshControlledSeedsForAllFiveTypesWithoutResolvingDuringPreview(SyntheticMessageType type)
    {
        using var documents = new DocumentManager(); var calls = 0;
        var model = new SyntheticGenerationViewModel(documents, null, () => (uint)(100 + ++calls)) { MessageType = type, Count = "2" };
        Assert.Equal("", model.Seed);
        for (var n = 0; n < 5; n++) { Assert.True(model.CanGenerate); Assert.Contains("Seed: automatic", model.Preview); }
        Assert.Equal(0, calls); Assert.DoesNotContain("0000002A", model.Preview);
        await model.GenerateAsync(); var first = Assert.IsType<SyntheticGenerationResult>(model.Result); var firstBytes = await Bytes(first);
        Assert.Equal(1, calls); Assert.Equal(101u, first.Report!.Seed); Assert.True(first.Report.SeedWasAutomatic); Assert.Equal("", model.Seed);
        Assert.Equal(SyntheticMessageGenerator.Generate(new() { MessageType = type, Count = 2, Seed = 101, DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic }, Token).GetUtf8Bytes(), firstBytes);
        await model.GenerateAsync(); Assert.Equal(2, calls); Assert.Equal(102u, model.Result!.Report!.Seed); Assert.NotEqual(firstBytes, await Bytes(model.Result));
        model.Seed = "101"; await model.GenerateAsync(); Assert.Equal(2, calls); Assert.False(model.Result!.Report!.SeedWasAutomatic); Assert.Equal(firstBytes, await Bytes(model.Result));
        await Drain(documents);
    }

    [Theory]
    [InlineData("0", 0u)] [InlineData("42", 42u)] [InlineData("4294967295", uint.MaxValue)]
    public async Task ExplicitStandaloneSeedsRemainExactVersionOneInputs(string text, uint seed)
    {
        using var documents = new DocumentManager(); var model = new SyntheticGenerationViewModel(documents, null, () => throw new InvalidOperationException("Explicit seed must bypass provider."))
        { Seed = text, MessageType = SyntheticMessageType.OruR01, DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders };
        await model.GenerateAsync(); var result = Assert.IsType<SyntheticGenerationResult>(model.Result);
        Assert.Equal(seed, result.Report!.Seed); Assert.False(result.Report.SeedWasAutomatic); Assert.Equal(1, result.Report.GeneratorVersion);
        Assert.Equal(SyntheticMessageGenerator.Generate(new() { Seed = seed, MessageType = SyntheticMessageType.OruR01 }, Token).GetUtf8Bytes(), await Bytes(result));
        await Drain(documents);
    }

    [Theory]
    [InlineData("-1")] [InlineData("4294967296")]
    public async Task UnsupportedSignedOrOverflowingSeedTextFailsBeforeResolution(string text)
    {
        using var documents = new DocumentManager(); var calls = 0;
        var model = new SyntheticGenerationViewModel(documents, null, () => { calls++; return 1; }) { Seed = text };
        Assert.False(model.CanGenerate); await model.GenerateAsync(); Assert.Equal(0, calls); Assert.Null(model.Result); Assert.Empty(documents.Documents);
    }

    [Fact]
    public async Task CancelDuplicateAndRetryNeverWriteAResolvedSeedBackIntoEitherDraft()
    {
        using var documents = new DocumentManager(); var calls = 0; var model = new SyntheticGenerationViewModel(documents, null, () => (uint)++calls) { Count = "1000" };
        var running = model.GenerateAsync(); Assert.True(model.IsBusy); await model.GenerateAsync(); Assert.Equal(1, calls);
        model.Cancel(); await running; Assert.Null(model.Result); Assert.Equal(1, calls); Assert.Empty(documents.Documents); Assert.Equal("", model.Seed);
        model.Count = "1"; await model.GenerateAsync(); Assert.Equal(2, calls); Assert.Equal(2u, model.Result!.Report!.Seed); Assert.Equal("", model.Seed);
        model.Mode = SyntheticGenerationMode.CombinedEncounter; Assert.Equal("", model.Seed); await model.GenerateAsync(); Assert.Equal(3, calls); Assert.Equal(3u, model.Result!.Report!.Seed);
        model.Mode = SyntheticGenerationMode.Standalone; Assert.Equal("", model.Seed);
        model.Seed = "42"; model.Mode = SyntheticGenerationMode.CombinedEncounter; Assert.Equal("", model.Seed);
        model.Seed = "0"; model.Mode = SyntheticGenerationMode.Standalone; Assert.Equal("42", model.Seed);
        model.Mode = SyntheticGenerationMode.CombinedEncounter; Assert.Equal("0", model.Seed);
        await Drain(documents);
    }

    [Theory]
    [InlineData(SyntheticMessageType.AdtA01, 0u)] [InlineData(SyntheticMessageType.OruR01, 42u)]
    public async Task StandaloneReportCopiesActualResolvedSeedForExactReplay(SyntheticMessageType type, uint seed)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var result = await new SyntheticGeneration(documents, () => seed).GenerateAsync(new() { MessageType = type }, token: Token, automaticSeed: true);
            using var model = new GenerationReportViewModel(documents); var view = new GenerationReportView(model);
            var window = new Window { Content = view, Width = 380, Height = 600 }; window.Show();
            try
            {
                Assert.True(view.CopySeed.IsVisible); Assert.True(view.CopySeed.IsEnabled); Assert.Contains($"Seed: {seed.ToString(CultureInfo.InvariantCulture)} (automatic)", view.Details.Text);
                view.CopySeed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await view.ClipboardTask;
                var copied = await window.Clipboard!.TryGetTextAsync(); Assert.Equal(seed.ToString(CultureInfo.InvariantCulture), copied);
                var replay = new SyntheticGenerationViewModel(documents, null, () => throw new InvalidOperationException("Copied seed must bypass provider.")) { MessageType = type, Seed = copied!, DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders };
                await replay.GenerateAsync(); Assert.NotNull(replay.Result); Assert.Equal(await Bytes(result), await Bytes(replay.Result)); Assert.False(replay.Result.Report!.SeedWasAutomatic);
            }
            finally { window.Close(); }
            await Drain(documents); return true;
        }, Token);
    }
}
