using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class SyntheticDemographicViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static T Input<T>(Control root, string label) where T : Control => root.GetVisualDescendants().OfType<T>()
        .Single(c => AutomationProperties.GetLabeledBy(c) is TextBlock text && text.Text == label);

    [Fact]
    public async Task NewConfigurationsDefaultToRealisticAndSelectingStandalonePlaceholdersRetainsOriginalContract()
    {
        using var documents = new DocumentManager(); var model = new SyntheticGenerationViewModel(documents, null);
        Assert.Equal(SyntheticGenerationMode.Standalone, model.Mode); Assert.Equal(SyntheticDemographicStyle.RealisticSynthetic, model.DemographicStyle);
        Assert.Contains("Realistic Synthetic", model.Preview); Assert.Contains("generator v2", model.Preview);
        model.Mode = SyntheticGenerationMode.CombinedEncounter; Assert.Contains("Realistic Synthetic", model.Preview); Assert.Contains("estimated 4 messages", model.Preview);
        model.DemographicStyle = SyntheticDemographicStyle.ObviousPlaceholders; Assert.Contains("Obvious Placeholders", model.Preview);
        model.Mode = SyntheticGenerationMode.Standalone; model.Seed = "42"; await model.GenerateAsync();
        var result = Assert.IsType<SyntheticGenerationResult>(model.Result); Assert.Null(result.ScenarioOptions); Assert.Equal(SyntheticDemographicStyle.ObviousPlaceholders, result.Report!.DemographicStyle);
        Assert.Equal(SyntheticMessageGenerator.Generate(new(), Token).Messages[0], result.Document.EditableText);
        model.Mode = SyntheticGenerationMode.CombinedEncounter; Assert.Equal(SyntheticDemographicStyle.ObviousPlaceholders, model.DemographicStyle);
        var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
    }

    [Theory]
    [InlineData(SyntheticDemographicStyle.RealisticSynthetic)] [InlineData(SyntheticDemographicStyle.ObviousPlaceholders)]
    public async Task BothModesPublishWithStyleMetadataAndNeverReplaceExplicitLegacyFacilityValues(SyntheticDemographicStyle style)
    {
        using var documents = new DocumentManager(); var model = new SyntheticGenerationViewModel(documents, null)
        {
            Mode = SyntheticGenerationMode.CombinedEncounter, DemographicStyle = style, SendingFacility = "SYN-FACILITY", ReceivingFacility = "SYN-DEST",
            SendingApplication = "SYN-GENERATOR", ReceivingApplication = "SYN-RECEIVER", PatientPrefix = "DEMO", Orders = "2", Results = "2", IncludeTransfer = true
        };
        Assert.True(model.CanGenerate); await model.GenerateAsync(); var result = Assert.IsType<SyntheticGenerationResult>(model.Result);
        Assert.Equal(style, result.ScenarioOptions!.DemographicStyle); Assert.Equal(style, result.Report!.DemographicStyle); Assert.Equal(2, result.Report.GeneratorVersion);
        Assert.Contains(result.ScenarioOptions.DemographicStyleLabel, GenerationReportView.Format(result.Report));
        foreach (var id in result.MessageIds)
        {
            await result.Document.NavigateAsync(id, Token);
            Assert.Equal("SYN-FACILITY", result.Document.Message.Get("MSH-4").Raw); Assert.Equal("SYN-DEST", result.Document.Message.Get("MSH-6").Raw);
            Assert.StartsWith("SYN-DEMO-", result.Document.Message.Get("PID-3.1").Raw);
        }
        var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
    }

    [Theory]
    [InlineData(false, 380, 600)] [InlineData(true, 680, 760)]
    public async Task StyleSelectorAutoDefaultsAndAdvancedOverridesRemainUsableInBothThemes(bool dark, int width, int height)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var documents = new DocumentManager(); var dialog = new SyntheticGenerationDialog(documents)
            { Width = width, Height = height, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; dialog.Show();
            try
            {
                Input<ComboBox>(dialog, "Generate").SelectedIndex = 3; Pump(dialog);
                Input<TextBox>(dialog, "Seed (optional)").Text = "42"; Pump(dialog);
                var styles = Input<ComboBox>(dialog, "Demographic style"); Assert.Equal(0, styles.SelectedIndex);
                Assert.Equal(new[] { "Realistic Synthetic", "Obvious Placeholders" }, styles.Items.Cast<string>());
                Assert.False(dialog.AdvancedOptions.IsExpanded); Assert.Contains("Realistic Synthetic", dialog.Model.Preview);
                styles.BringIntoView(); Pump(dialog); Assert.True(styles.Focus()); Assert.True(styles.IsEffectivelyVisible);
                dialog.AdvancedOptions.IsExpanded = true; Pump(dialog);
                var facility = Input<TextBox>(dialog, "Sending facility"); Assert.Equal("", facility.Text);
                var options = new SyntheticScenarioOptions(); Assert.StartsWith(options.SendingFacility, facility.PlaceholderText);
                facility.Text = "TEST-FAC"; Pump(dialog); Assert.Equal("TEST-FAC", dialog.Model.SendingFacility);
                styles.SelectedIndex = 1; Pump(dialog); Assert.Equal("SYN-FACILITY · SYN-FACILITY", facility.PlaceholderText);
                Assert.Equal("TEST-FAC", facility.Text); Assert.Contains("Obvious Placeholders", dialog.Model.Preview); Assert.True(dialog.Generate.IsEnabled);
                styles.SelectedIndex = 0; dialog.AdvancedOptions.IsExpanded = false; Pump(dialog);
                Assert.Equal("TEST-FAC", dialog.Model.SendingFacility); Assert.Contains("Realistic Synthetic", dialog.Model.Preview);
                Assert.True(new Rect(dialog.Bounds.Size).Contains(new Rect(dialog.Generate.TranslatePoint(default, dialog)!.Value, dialog.Generate.Bounds.Size)));
                Input<ComboBox>(dialog, "Generate").SelectedIndex = 0; Pump(dialog); Assert.True(styles.IsEffectivelyVisible);
                Assert.Contains("generator v2", dialog.Model.Preview); Assert.True(dialog.Generate.IsEnabled);
            }
            finally { dialog.Close(); }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task SameDialogGeneratesChosenStyleAndOpensMatchingReport(int style)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var owner = new Window(); owner.Show(); var dialog = new SyntheticGenerationDialog(documents);
            try
            {
                var completion = dialog.ShowDialog<SyntheticGenerationResult?>(owner); Input<ComboBox>(dialog, "Generate").SelectedIndex = 3; Pump(dialog);
                Input<TextBox>(dialog, "Seed (optional)").Text = "42"; Pump(dialog);
                Input<ComboBox>(dialog, "Demographic style").SelectedIndex = style; Pump(dialog); Click(dialog.Generate);
                var result = Assert.IsType<SyntheticGenerationResult>(await completion.WaitAsync(TimeSpan.FromSeconds(10), Token));
                Assert.Equal((SyntheticDemographicStyle)style, result.ScenarioOptions!.DemographicStyle);
                Assert.Equal((SyntheticDemographicStyle)style, result.Report!.DemographicStyle);
                Assert.Equal(4, result.MessageIds.Count); var m = result.Document.Message;
                if (style == 1) { Assert.StartsWith("SYNTHETIC", m.Get("PID-5.1").Raw); Assert.Equal("SYN-FACILITY", m.Get("MSH-4").Raw); }
                else { Assert.DoesNotContain("SYNTHETIC", m.Get("PID-5").Raw!); Assert.Equal(new SyntheticScenarioOptions().SendingFacility, m.Get("MSH-4").Raw); }
                using var model = new GenerationReportViewModel(documents); var view = new GenerationReportView(model);
                Assert.Equal(GenerationReportView.Format(result.Report), view.Details.Text);
                var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
            }
            finally { dialog.Close(); owner.Close(); }
            return true;
        }, Token);
    }

    [Fact]
    public void InvalidStyleDisablesBothScenarioAndStandaloneGeneration()
    {
        using var documents = new DocumentManager(); var model = new SyntheticGenerationViewModel(documents, null)
        { Mode = SyntheticGenerationMode.CombinedEncounter, DemographicStyle = (SyntheticDemographicStyle)99 };
        Assert.False(model.CanGenerate); Assert.Contains("demographic style", model.Preview);
        model.Mode = SyntheticGenerationMode.Standalone; Assert.False(model.CanGenerate);
    }
}
