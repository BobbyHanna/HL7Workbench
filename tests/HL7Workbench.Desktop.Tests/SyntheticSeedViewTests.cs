using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
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

public sealed class SyntheticSeedViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static T Input<T>(Control root, string label) where T : Control => root.GetVisualDescendants().OfType<T>()
        .Single(c => AutomationProperties.GetLabeledBy(c) is TextBlock text && text.Text == label);
    private static async Task<byte[]> Bytes(SyntheticGenerationResult result)
    {
        var text = new StringBuilder(); foreach (var id in result.MessageIds) { await result.Document.NavigateAsync(id, Token); text.Append(result.Document.EditableText); }
        return Encoding.UTF8.GetBytes(text.ToString());
    }
    private static async Task Drain(DocumentManager documents)
    { var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal)); }

    [Fact]
    public async Task BlankScenarioSeedDoesNotResolveDuringValidationAndEachGenerateUsesOneNewControlledSeed()
    {
        using var documents = new DocumentManager(); var calls = 0;
        var model = new SyntheticGenerationViewModel(documents, null, () => { calls++; return (uint)(1000 + calls); }) { Mode = SyntheticGenerationMode.CombinedEncounter };
        Assert.Equal("", model.Seed);
        for (var n = 0; n < 5; n++) { Assert.True(model.CanGenerate); Assert.Contains("seed automatic", model.Preview); }
        Assert.Equal(0, calls); model.Count = "0"; await model.GenerateAsync(); Assert.Equal(0, calls); Assert.Null(model.Result);
        model.Count = "2"; await model.GenerateAsync(); var first = Assert.IsType<SyntheticGenerationResult>(model.Result); var original = await Bytes(first);
        Assert.Equal(1, calls); Assert.Equal(1001u, first.Report!.Seed); Assert.True(first.Report.SeedWasAutomatic); Assert.Equal("", model.Seed);
        await model.GenerateAsync(); var second = Assert.IsType<SyntheticGenerationResult>(model.Result);
        Assert.Equal(2, calls); Assert.Equal(1002u, second.Report!.Seed); Assert.NotEqual(original, await Bytes(second));
        Assert.Equal(SyntheticScenarioGenerator.Generate(first.ScenarioOptions!, Token).GetUtf8Bytes(), original);
        Assert.Equal(first.Report.Seed, first.ScenarioOptions!.Seed);
        model.Seed = first.Report.Seed.ToString(CultureInfo.InvariantCulture); await model.GenerateAsync();
        Assert.Equal(2, calls); Assert.False(model.Result!.Report!.SeedWasAutomatic); Assert.Equal(original, await Bytes(model.Result));
        await Drain(documents);
    }

    [Theory]
    [InlineData("0", 0u)] [InlineData("42", 42u)] [InlineData("4294967295", uint.MaxValue)]
    public async Task ExplicitSeedTextIsUsedExactlyWithoutCallingRandomProvider(string text, uint expected)
    {
        using var documents = new DocumentManager();
        var model = new SyntheticGenerationViewModel(documents, null, () => throw new InvalidOperationException("Unexpected seed resolution."))
        { Mode = SyntheticGenerationMode.CombinedEncounter, Seed = text };
        Assert.True(model.CanGenerate); await model.GenerateAsync(); var result = Assert.IsType<SyntheticGenerationResult>(model.Result);
        Assert.Equal(expected, result.Report!.Seed); Assert.False(result.Report.SeedWasAutomatic);
        Assert.Contains($"Seed: {text} (explicit)", GenerationReportView.Format(result.Report));
        Assert.Equal(SyntheticScenarioGenerator.Generate(new() { Seed = expected }, Token).GetUtf8Bytes(), await Bytes(result));
        await Drain(documents);
    }

    [Fact]
    public async Task CancelAndDuplicateClickDoNotResolveAgainAndRetryStartsNewOperation()
    {
        using var documents = new DocumentManager(); var calls = 0;
        var model = new SyntheticGenerationViewModel(documents, null, () => { calls++; return (uint)calls; })
        { Mode = SyntheticGenerationMode.CombinedEncounter, Count = "250" };
        var run = model.GenerateAsync(); Assert.True(model.IsBusy); await model.GenerateAsync(); Assert.Equal(1, calls);
        model.Cancel(); await run; Assert.Equal(1, calls); Assert.Null(model.Result); Assert.Empty(documents.Documents);
        model.Count = "1"; await model.GenerateAsync(); Assert.Equal(2, calls); Assert.Equal(2u, model.Result!.Report!.Seed);
        await Drain(documents);
    }

    [Theory]
    [InlineData(false, 380)] [InlineData(true, 680)]
    public async Task ScenarioAndStandaloneDraftsStaySeparateAndAutomaticPreviewDoesNotPromiseSeedFortyTwo(bool dark, int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var documents = new DocumentManager(); var dialog = new SyntheticGenerationDialog(documents)
            { Width = width, Height = 700, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; dialog.Show();
            try
            {
                var modes = Input<ComboBox>(dialog, "Generate"); var seed = Input<TextBox>(dialog, "Seed (optional)"); Assert.Equal("", seed.Text);
                Assert.Contains("Seed: automatic", dialog.Model.Preview); seed.Text = "42"; Pump(dialog);
                modes.SelectedIndex = 3; Pump(dialog); Assert.Equal("", seed.Text); Assert.Contains("Automatic", seed.PlaceholderText); Assert.Contains("seed automatic", dialog.Model.Preview);
                dialog.AdvancedOptions.IsExpanded = true; Pump(dialog); Assert.Contains("resolved on Generate", Input<TextBox>(dialog, "Sending facility").PlaceholderText);
                seed.Text = "0"; Pump(dialog); Assert.Equal("0", dialog.Model.Seed); Assert.True(dialog.Generate.IsEnabled);
                modes.SelectedIndex = 0; Pump(dialog); Assert.Equal("42", seed.Text); Assert.Contains("0000002A", dialog.Model.Preview);
                modes.SelectedIndex = 3; Pump(dialog); Assert.Equal("0", seed.Text);
                seed.Text = ""; Pump(dialog); Assert.Contains("seed automatic", dialog.Model.Preview); Assert.True(dialog.Generate.IsEnabled);
                Assert.Equal(SyntheticGenerationDialog.SeedHelp, AutomationProperties.GetHelpText(seed));
                Assert.Contains("cryptographically random", SyntheticGenerationDialog.SeedHelp);
            }
            finally { dialog.Close(); }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(0u)] [InlineData(42u)]
    public async Task ReportCopySeedCopiesActualAutomaticValueAndPastingItReproducesOutput(uint seed)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var result = await new SyntheticGeneration(documents, () => seed).GenerateScenariosAsync(new(), token: Token, automaticSeed: true);
            using var model = new GenerationReportViewModel(documents); var view = new GenerationReportView(model);
            var window = new Window { Width = 380, Height = 600, Content = view }; window.Show();
            try
            {
                Pump(window); Assert.True(view.CopySeed.IsVisible); Assert.True(view.CopySeed.IsEnabled);
                Assert.Contains($"Seed: {seed.ToString(CultureInfo.InvariantCulture)} (automatic)", view.Details.Text);
                Click(view.CopySeed); await view.ClipboardTask;
                var copied = await window.Clipboard!.TryGetTextAsync(); Assert.Equal(seed.ToString(CultureInfo.InvariantCulture), copied);
                Assert.Contains("Seed copied", view.CopyStatus.Text);
                var replay = new SyntheticGenerationViewModel(documents, null, () => throw new InvalidOperationException("Copied seed must be explicit."))
                { Mode = SyntheticGenerationMode.CombinedEncounter, Seed = copied! };
                await replay.GenerateAsync(); Assert.Equal(await Bytes(result), await Bytes(replay.Result!)); Assert.False(replay.Result!.Report!.SeedWasAutomatic);
                var plain = MessageFileReader.Open(documents, "Synthetic plain", Hl7Parser.Parse("MSH|^~\\&|SYN\r")); Pump(window);
                Assert.False(view.CopySeed.IsVisible); Assert.False(view.CopySeed.IsEnabled); Assert.True(view.EmptyState.IsVisible); Assert.Same(plain, documents.ActiveDocument);
            }
            finally { window.Close(); }
            await Drain(documents); return true;
        }, Token);
    }
}
