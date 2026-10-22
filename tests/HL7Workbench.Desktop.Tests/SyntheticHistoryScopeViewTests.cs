using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using HL7Workbench.Core;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Tests;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class SyntheticHistoryScopeViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    [Theory]
    [InlineData(false, 380)] [InlineData(true, 1100)]
    public async Task PatientDefaultEntireImportAndAdvancedScopeShareThemedUiAndPreserveSelections(bool dark, int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-fhir-scope-view-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var path = Path.Combine(root, "synthetic-history.json"); await File.WriteAllTextAsync(path, FhirSyntheticHistory.History(10), Token);
            using var documents = new DocumentManager(); using var model = new SyntheticPatientsViewModel(documents); var view = new SyntheticPatientsView(model);
            var window = new Window { Content = view, Width = width, Height = 850, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Pump(window); Assert.Equal(0, view.Scope.SelectedIndex); Assert.False(view.Preview.IsEnabled); Assert.Contains("Import synthetic", view.GenerationState.Text!);
                await model.Session.ImportFilesAsync([path], true, Token); Pump(window); Assert.False(view.Preview.IsEnabled); Assert.Contains("Select a patient", view.GenerationState.Text!);
                view.Patients.SelectedItem = model.Session.PatientPage[0]; Pump(window); Assert.True(view.Preview.IsEnabled); Assert.Empty(model.Session.SelectedEncounters); Assert.Empty(model.Session.SelectedEvents);
                Click(view.Preview); await view.ActionTask; Pump(window); Assert.Equal(4, model.Session.Preview!.Messages); Assert.True(view.Generate.IsEnabled); Assert.Contains("1 patients", view.GenerationState.Text!);
                Click(view.Generate); await view.ActionTask; Pump(window); Assert.Equal(4, Assert.Single(documents.Documents).MessageCount);
                using var reportModel = new GenerationReportViewModel(documents);
                Assert.Contains("Successfully generated", new GenerationReportView(reportModel).Details.Text!);
                Assert.True(new FileDocumentView(documents.Documents[0]).GenerationIndicator.IsVisible);
                view.Scope.SelectedIndex = (int)FhirGenerationScope.SelectedEvents; Pump(window); Assert.False(view.Preview.IsEnabled); Assert.Contains("at least one", view.GenerationState.Text!);
                view.Browse.SelectedIndex = 1; view.Encounters.SelectedItems!.Add(model.Session.EncounterPage[0]); Pump(window); Assert.True(view.Preview.IsEnabled);
                view.Scope.SelectedIndex = (int)FhirGenerationScope.EntireImport; Click(view.Preview); await view.ActionTask; Pump(window); Assert.Equal(40, model.Session.Preview!.Messages); Assert.Equal(10, model.Session.Preview.PatientsIncluded);
                Assert.Contains("10 patients", view.GenerationState.Text!); Click(view.Generate); await view.ActionTask; Pump(window); Assert.Equal(2, documents.Documents.Count); Assert.Equal(40, documents.Documents[1].MessageCount);
                view.Scope.SelectedIndex = (int)FhirGenerationScope.SelectedPatient; view.Scope.SelectedIndex = (int)FhirGenerationScope.SelectedEvents; Pump(window); Assert.Single(model.Session.SelectedEncounters);
                Assert.True(view.Preview.IsEnabled); Click(view.Preview); await view.ActionTask; Pump(window); Assert.Equal(3, model.Session.Preview!.Messages);
                Assert.True(view.Browse.Bounds.Height > 70); Assert.True(view.Scope.Bounds.Width > 100);
                window.Content = new TextBlock(); var rebuilt = new SyntheticPatientsView(model); window.Content = rebuilt; Pump(window); Assert.Equal((int)FhirGenerationScope.SelectedEvents, rebuilt.Scope.SelectedIndex); Assert.Single(model.Session.SelectedEncounters);
                Click(rebuilt.Clear); Pump(window); Assert.False(rebuilt.Generate.IsEnabled); Assert.Equal((int)FhirGenerationScope.SelectedPatient, rebuilt.Scope.SelectedIndex); Assert.Equal(2, documents.Documents.Count);
            }
            finally { window.Close(); Directory.Delete(root, true); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EmptyAndOverLimitHistoriesHaveVisibleDisabledReasonsWithoutPartialOutput(bool exceedsLimit)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-fhir-scope-limit-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var path = Path.Combine(root, "synthetic-limit.json"); await File.WriteAllTextAsync(path, FhirSyntheticHistory.Observations(exceedsLimit ? 1001 : 0), Token);
            using var documents = new DocumentManager(); using var model = new SyntheticPatientsViewModel(documents); var view = new SyntheticPatientsView(model); var window = new Window { Content = view, Width = 900, Height = 850 }; window.Show();
            try
            {
                await model.Session.ImportFilesAsync([path], true, Token); view.Scope.SelectedIndex = (int)FhirGenerationScope.EntireImport; Click(view.Preview); await view.ActionTask; Pump(window);
                Assert.False(view.Generate.IsEnabled); Assert.Contains(exceedsLimit ? "Output limit exceeded" : "No eligible messages", view.GenerationState.Text!);
                Assert.Contains(exceedsLimit ? "1,000-message ceiling exceeded" : "0 messages", view.PreviewSummary.Text!); Assert.Empty(documents.Documents); Assert.True(view.Preview.IsEnabled);
            }
            finally { window.Close(); Directory.Delete(root, true); }
            return true;
        }, Token);
    }
}
