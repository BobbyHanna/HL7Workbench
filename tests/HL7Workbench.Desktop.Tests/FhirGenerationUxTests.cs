using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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

public sealed class FhirGenerationUxTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "fhir-r4", "patient-alpha.json");
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Reach(Window window, Control view, Control target)
    {
        Pump(window); target.BringIntoView(); Pump(window);
        var bounds = new Rect(target.TranslatePoint(default, view)!.Value, target.Bounds.Size);
        Assert.True(bounds.Width > 0 && bounds.Height > 0 && bounds.Intersects(new Rect(view.Bounds.Size)), $"{target.GetType().Name}: {bounds} outside {view.Bounds.Size}");
        foreach (var scroll in target.GetVisualAncestors().OfType<ScrollViewer>())
            Assert.True(bounds.Intersects(new Rect(scroll.TranslatePoint(default, view)!.Value, scroll.Bounds.Size)));
    }
    private static void CheckHelp(TextBox input, Control label, string expected)
    {
        foreach (var target in new[] { input, label })
        {
            Assert.Equal(expected, Assert.IsType<TextBlock>(ToolTip.GetTip(target)).Text);
            Assert.Equal(expected, AutomationProperties.GetHelpText(target));
            Assert.DoesNotContain("F1", expected, StringComparison.OrdinalIgnoreCase);
        }
        input.Focus(); input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F1 });
        Assert.True(ToolTip.GetIsOpen(input));
        input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape }); Assert.False(ToolTip.GetIsOpen(input));
    }
    [Theory]
    [InlineData(false, 1200, 800)] [InlineData(true, 900, 600)] [InlineData(false, 600, 400)]
    [InlineData(true, 350, 300)] [InlineData(false, 250, 250)]
    public async Task DirectActionAndAdvancedSeedHelpRemainReachableAndRetainStateAcrossConstrainedRemounts(bool dark, int width, int height)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); using var model = new SyntheticPatientsViewModel(documents);
            var view = new SyntheticPatientsView(model); var host = new Border { Width = width, Height = height, Child = view };
            var window = new Window { Content = new Grid { Children = { host } }, Width = 1300, Height = 950, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Pump(window); Assert.False(view.Generate.IsEnabled); Assert.False(view.AdvancedOptions.IsExpanded);
                await model.Session.ImportFilesAsync([Fixture], Token); Assert.False(view.Generate.IsEnabled);
                view.Patients.SelectedItem = model.Session.PatientPage[0]; view.GenerationPanel.IsExpanded = true; Pump(window);
                Assert.True(view.Generate.IsEnabled); Assert.Null(model.Session.Preview); Assert.Contains("accent", view.Generate.Classes);
                Reach(window, view, view.Generate); Click(view.Generate); Assert.True(model.IsBusy); Assert.False(view.Generate.IsEnabled); Assert.True(view.Cancel.IsVisible);
                await view.ActionTask; Pump(window); Assert.Equal(3, Assert.Single(documents.Documents).MessageCount); Assert.True(view.Generate.IsEnabled); Assert.False(view.Cancel.IsVisible);
                view.AdvancedOptions.IsExpanded = true; Reach(window, view, view.Seed); CheckHelp(view.Seed, view.SeedField.Label, SyntheticPatientsView.SeedHelp);
                Assert.Contains("MSH-10", SyntheticPatientsView.SeedHelp); Assert.Contains("remain unchanged", SyntheticPatientsView.SeedHelp);
                view.Seed.Text = "73"; view.TransportTime.Text = "2032-02-03T04:05:06+00:00"; Assert.Null(model.Session.Preview);
                view.AdvancedOptions.IsExpanded = false; Pump(window); Assert.Equal("73", model.Seed); Assert.True(view.Generate.IsEnabled);
                view.AdvancedOptions.IsExpanded = true; var seed = view.Seed;
                foreach (var size in new[] { (250d, 250d), (1200d, 800d), (350d, 300d), ((double)width, (double)height) })
                {
                    host.Width = size.Item1; host.Height = size.Item2; Pump(window); Reach(window, view, view.Seed); Reach(window, view, view.Generate);
                    Assert.Same(seed, view.Seed); Assert.True(view.Patients.Bounds.Height > 0 && double.IsFinite(view.Patients.Bounds.Height)); Assert.Equal("73", view.Seed.Text);
                }
                host.Child = null; var replacement = new SyntheticPatientsView(model); host.Child = replacement; Pump(window);
                Assert.True(replacement.GenerationPanel.IsExpanded); Assert.True(replacement.AdvancedOptions.IsExpanded);
                Assert.Equal("73", replacement.Seed.Text); Assert.Equal("2032-02-03T04:05:06+00:00", replacement.TransportTime.Text);
                Reach(window, replacement, replacement.Seed); CheckHelp(replacement.Seed, replacement.SeedField.Label, SyntheticPatientsView.SeedHelp);
                Click(replacement.Preview); await replacement.ActionTask; Assert.Single(documents.Documents);
                var preview = model.Session.Preview; Click(replacement.Generate); await replacement.ActionTask;
                Assert.Same(preview, model.Session.Preview); Assert.Equal(2, documents.Documents.Count);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BuiltInGeneratorSeedLabelAndInputHaveSpecificAccessibleKeyboardHelp(bool dark)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var documents = new DocumentManager(); var dialog = new SyntheticGenerationDialog(documents) { RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; dialog.Show();
            try
            {
                Pump(dialog); var seed = Assert.Single(dialog.GetVisualDescendants().OfType<TextBox>(), t => AutomationProperties.GetLabeledBy(t) is TextBlock { Text: "Seed (optional)" });
                CheckHelp(seed, AutomationProperties.GetLabeledBy(seed)!, SyntheticGenerationDialog.SeedHelp);
                Assert.Contains("birth dates", SyntheticGenerationDialog.SeedHelp); Assert.Contains("Explicit 0 and 42", SyntheticGenerationDialog.SeedHelp);
                seed.Text = ""; Assert.True(dialog.Model.CanGenerate); Assert.Contains("Seed: automatic", dialog.Model.Preview);
            }
            finally { dialog.Close(); }
        }, Token);
    }
    [Theory]
    [InlineData("", "2030-01-01T00:00:00+00:00")]
    [InlineData("4294967296", "2030-01-01T00:00:00+00:00")]
    [InlineData("42", "2030-01-01")]
    [InlineData("42", "0001-01-01T00:00:00+00:00")]
    public async Task InvalidAdvancedConfigurationBlocksDirectGenerationBeforeAnyPublication(string seed, string timestamp)
    {
        using var documents = new DocumentManager(); using var model = new SyntheticPatientsViewModel(documents);
        await model.Session.ImportFilesAsync([Fixture], Token); model.Session.SelectPatient(model.Session.PatientPage[0].Key);
        model.Seed = seed; model.TransportTimestamp = timestamp; Assert.False(model.CanGenerate); Assert.False(model.CanPreview);
        await model.GenerateAsync(); Assert.Empty(documents.Documents); Assert.NotEmpty(model.ConfigurationError); Assert.False(model.IsBusy);
        model.Seed = "42"; model.TransportTimestamp = "2030-01-01T00:00:00+00:00"; Assert.True(model.CanGenerate);
    }
}
