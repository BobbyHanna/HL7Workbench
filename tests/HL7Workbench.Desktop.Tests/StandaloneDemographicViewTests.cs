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

public sealed class StandaloneDemographicViewTests
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
    public async Task NewlyOpenedConfigurationUsesRealisticVersionTwoAndExplicitStyleSwitchReproducesOriginalVersionOne(SyntheticMessageType type)
    {
        using var documents = new DocumentManager(); var model = new SyntheticGenerationViewModel(documents, null, () => throw new InvalidOperationException("Explicit seed must bypass provider."))
        { MessageType = type, Seed = "42", Count = "2" };
        Assert.Equal(SyntheticDemographicStyle.RealisticSynthetic, model.DemographicStyle);
        Assert.Contains("Realistic Synthetic", model.Preview); Assert.Contains("generator v2", model.Preview);
        await model.GenerateAsync(); var realistic = Assert.IsType<SyntheticGenerationResult>(model.Result);
        Assert.Equal(SyntheticDemographicStyle.RealisticSynthetic, realistic.Options.DemographicStyle); Assert.Equal(2, realistic.Report!.GeneratorVersion);
        Assert.Contains("Realistic Synthetic", GenerationReportView.Format(realistic.Report));
        Assert.Equal(SyntheticMessageGenerator.Generate(new() { MessageType = type, Seed = 42, Count = 2, DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic }, Token).GetUtf8Bytes(), await Bytes(realistic));
        model.DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders;
        Assert.Contains("generator v1", model.Preview); await model.GenerateAsync(); var legacy = Assert.IsType<SyntheticGenerationResult>(model.Result);
        Assert.Equal(1, legacy.Report!.GeneratorVersion); Assert.Equal(SyntheticDemographicStyle.ObviousPlaceholders, legacy.Report.DemographicStyle);
        Assert.Contains("Obvious Placeholders", GenerationReportView.Format(legacy.Report));
        Assert.Equal(SyntheticMessageGenerator.Generate(new() { MessageType = type, Count = 2 }, Token).GetUtf8Bytes(), await Bytes(legacy));
        model.Mode = SyntheticGenerationMode.CombinedEncounter; Assert.Equal(SyntheticDemographicStyle.ObviousPlaceholders, model.DemographicStyle);
        Assert.Equal("", model.Seed); model.DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic; model.Mode = SyntheticGenerationMode.Standalone;
        Assert.Equal("42", model.Seed); Assert.Contains("Realistic Synthetic", model.Preview);
        await Drain(documents);
    }

    [Theory]
    [InlineData(0u)] [InlineData(uint.MaxValue)]
    public async Task RealisticStandaloneReportCopySeedReplaysOutputAndRetainsActualAutomaticFacilityDefaults(uint seed)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var calls = 0;
            var generator = new SyntheticGenerationViewModel(documents, null, () => { calls++; return seed; })
            { MessageType = SyntheticMessageType.OruR01, SendingApplication = "CUSTOM", PatientPrefix = "PATIENT" };
            for (var n = 0; n < 4; n++) Assert.True(generator.CanGenerate);
            Assert.Equal(0, calls); await generator.GenerateAsync(); var first = Assert.IsType<SyntheticGenerationResult>(generator.Result);
            Assert.Equal(1, calls); Assert.Equal("", generator.Seed); Assert.Equal(seed, first.Report!.Seed);
            Assert.Equal((new SyntheticGenerationOptions { Seed = seed, DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic }).SendingFacility, first.Options.SendingFacility);
            Assert.Equal(first.Options.SendingFacility, first.Document.Message.Get("MSH-4").Raw);
            using var reportModel = new GenerationReportViewModel(documents); var report = new GenerationReportView(reportModel);
            var window = new Window { Content = report }; window.Show();
            try
            {
                Assert.Contains("Realistic Synthetic", report.Details.Text); Assert.Contains("generator v2", report.Details.Text);
                Assert.Contains($"Seed: {seed.ToString(CultureInfo.InvariantCulture)} (automatic)", report.Details.Text);
                report.CopySeed.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await report.ClipboardTask;
                var copied = await window.Clipboard!.TryGetTextAsync(); Assert.Equal(seed.ToString(CultureInfo.InvariantCulture), copied);
                generator.Seed = copied!; await generator.GenerateAsync(); Assert.Equal(1, calls);
                Assert.Equal(await Bytes(first), await Bytes(generator.Result!)); Assert.False(generator.Result!.Report!.SeedWasAutomatic);
            }
            finally { window.Close(); }
            await Drain(documents); return true;
        }, Token);
    }

    [Fact]
    public async Task StandaloneExplicitAdvancedOverridesAreValidatedAndNeverSilentlyReplacedOnStyleSwitch()
    {
        using var documents = new DocumentManager(); var calls = 0;
        var model = new SyntheticGenerationViewModel(documents, null, () => { calls++; return 42; })
        { SendingFacility = "CUSTOM-FAC", ReceivingFacility = "CUSTOM-LAB", PatientPrefix = "TEST" };
        await model.GenerateAsync(); var result = Assert.IsType<SyntheticGenerationResult>(model.Result);
        Assert.Equal("CUSTOM-FAC", result.Document.Message.Get("MSH-4").Raw); Assert.StartsWith("SYN-TEST-", result.Document.Message.Get("PID-3.1").Raw);
        model.DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders; Assert.False(model.CanGenerate); Assert.Contains("Clear overrides", model.Preview);
        await model.GenerateAsync(); Assert.Equal(1, calls); Assert.Same(result, model.Result);
        model.DemographicStyle = SyntheticDemographicStyle.RealisticSynthetic; Assert.True(model.CanGenerate); Assert.Equal("CUSTOM-FAC", model.SendingFacility);
        model.SendingFacility = "BAD|FACILITY"; Assert.False(model.CanGenerate); await model.GenerateAsync(); Assert.Equal(1, calls);
        await Drain(documents);
    }
}
