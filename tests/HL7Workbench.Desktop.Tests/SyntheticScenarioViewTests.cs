using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class SyntheticScenarioViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static T Input<T>(Control root, string label) where T : Control => root.GetVisualDescendants().OfType<T>()
        .Single(c => AutomationProperties.GetLabeledBy(c) is TextBlock text && text.Text == label);

    [Theory]
    [InlineData("patients", "251")] [InlineData("encounters", "0")] [InlineData("orders", "21")] [InlineData("reports", "11")]
    [InlineData("orders", "2147483648")] [InlineData("reports", "1.5")] [InlineData("facility", "A|B")]
    [InlineData("value", "NaN")] [InlineData("value", "100001")] [InlineData("prefix", "A^B")]
    public async Task ScenarioValidationDisablesGenerationWithoutPublishing(string field, string value)
    {
        using var documents = new DocumentManager(); var model = new SyntheticGenerationViewModel(documents, null) { Mode = SyntheticGenerationMode.CombinedEncounter };
        switch (field)
        {
            case "patients": model.Count = value; break; case "encounters": model.Encounters = value; break;
            case "orders": model.Orders = value; break; case "reports": model.Results = value; break;
            case "facility": model.SendingFacility = value; break; case "value": model.ObservationValue = value; break;
            default: model.PatientPrefix = value; break;
        }
        Assert.False(model.CanGenerate); Assert.NotEmpty(model.Preview); await model.GenerateAsync();
        Assert.Null(model.Result); Assert.NotEmpty(model.Status); Assert.Empty(documents.Documents);
    }

    [Fact]
    public void EstimateIsLightweightAndModeSwitchRetainsDraftsWithoutApplyingHiddenOrderSettings()
    {
        using var documents = new DocumentManager(); var model = new SyntheticGenerationViewModel(documents, null)
        { Mode = SyntheticGenerationMode.CombinedEncounter, Count = "2", Encounters = "3", Orders = "2", Results = "3", IncludeTransfer = true };
        var before = GC.GetAllocatedBytesForCurrentThread(); var preview = model.Preview;
        Assert.Contains("estimated 66 messages", preview); Assert.Contains("6 encounters", preview); Assert.True(model.CanGenerate);
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 128 * 1024); Assert.Empty(documents.Documents);
        model.Mode = SyntheticGenerationMode.AdmissionDischarge; Assert.True(model.CanGenerate); Assert.Contains("estimated 18 messages", model.Preview);
        model.Mode = SyntheticGenerationMode.OrderResult; Assert.True(model.CanGenerate); Assert.Contains("estimated 48 messages", model.Preview);
        model.Mode = SyntheticGenerationMode.Standalone; Assert.True(model.CanGenerate); Assert.Contains("2 message", model.Preview);
        model.Mode = SyntheticGenerationMode.CombinedEncounter; Assert.Equal("2", model.Orders); Assert.Equal("3", model.Results); Assert.True(model.IncludeTransfer);
        model.EncounterType = SyntheticEncounterType.Outpatient; Assert.False(model.CanGenerate); Assert.Contains("Transfer", model.Preview);
    }

    [Theory]
    [InlineData(false, 380, 600)] [InlineData(true, 680, 760)]
    public async Task DialogProvidesReachableAdvancedControlsAndFixedGenerateButtonsInBothThemes(bool dark, int width, int height)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var documents = new DocumentManager(); var dialog = new SyntheticGenerationDialog(documents)
            { Width = width, Height = height, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            dialog.Show();
            try
            {
                Input<ComboBox>(dialog, "Generate").SelectedIndex = 3; Pump(dialog);
                Assert.True(dialog.Model.IsScenario); Assert.False(dialog.AdvancedOptions.IsExpanded); Assert.True(dialog.Generate.IsEnabled);
                Assert.True(dialog.ConfigurationScroll.Viewport.Height > 60); Assert.True(dialog.Generate.Bounds.Width > 50);
                dialog.AdvancedOptions.IsExpanded = true; Pump(dialog);
                var facility = Input<TextBox>(dialog, "Sending facility"); facility.BringIntoView(); Pump(dialog);
                facility.Text = "SYN-TEST-FAC"; Input<TextBox>(dialog, "First analyte value").Text = "101.5";
                Pump(dialog);
                Assert.True(dialog.Model.CanGenerate); Assert.Equal("SYN-TEST-FAC", dialog.Model.SendingFacility);
                var value = Input<TextBox>(dialog, "First analyte value"); value.BringIntoView(); Pump(dialog);
                Assert.True(value.IsEffectivelyVisible); Assert.True(dialog.ConfigurationScroll.Offset.Y > 0);
                var bounds = new Rect(value.TranslatePoint(default, dialog)!.Value, value.Bounds.Size);
                Assert.True(bounds.Intersects(new Rect(dialog.Bounds.Size))); Assert.True(dialog.Generate.IsEffectivelyVisible);
                dialog.AdvancedOptions.IsExpanded = false; Pump(dialog); Assert.Equal("101.5", dialog.Model.ObservationValue);
                dialog.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark; Pump(dialog);
                Assert.Equal(dialog.RequestedThemeVariant, dialog.ActualThemeVariant); Assert.True(dialog.Generate.IsEnabled);
                var buttonBounds = new Rect(dialog.Generate.TranslatePoint(default, dialog)!.Value, dialog.Generate.Bounds.Size);
                Assert.True(new Rect(dialog.Bounds.Size).Contains(buttonBounds));
            }
            finally { dialog.Close(); }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(1, 2)] [InlineData(2, 2)] [InlineData(3, 4)]
    public async Task ExistingDialogPublishesScenarioAndReturnsItsReport(int mode, int expected)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var owner = new Window(); owner.Show(); var dialog = new SyntheticGenerationDialog(documents);
            try
            {
                var completion = dialog.ShowDialog<SyntheticGenerationResult?>(owner); Input<ComboBox>(dialog, "Generate").SelectedIndex = (int)mode;
                Click(dialog.Generate); Assert.True(dialog.Model.IsBusy); Assert.False(dialog.Generate.IsEnabled);
                var result = Assert.IsType<SyntheticGenerationResult>(await completion.WaitAsync(TimeSpan.FromSeconds(10), Token));
                Assert.Equal(expected, result.MessageIds.Count); Assert.NotNull(result.ScenarioOptions); Assert.Equal(expected, result.Report!.Messages);
                Assert.Same(result.Document, documents.ActiveDocument); Assert.False(dialog.Model.IsBusy);
                Assert.Contains("Generated", dialog.Model.Status);
                var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
            }
            finally { dialog.Close(); owner.Close(); }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ScenarioCancelAndDismissalDrainBeforeClosingWithoutPartialTab(bool dismiss)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var owner = new Window(); owner.Show();
            var dialog = new SyntheticGenerationDialog(documents); dialog.Model.Mode = SyntheticGenerationMode.CombinedEncounter; dialog.Model.Count = "250";
            try
            {
                var completion = dialog.ShowDialog<SyntheticGenerationResult?>(owner); Click(dialog.Generate); Assert.True(dialog.Model.IsBusy);
                if (dismiss) dialog.Close(); else Click(dialog.CancelGeneration);
                Assert.Null(await completion.WaitAsync(TimeSpan.FromSeconds(10), Token)); Assert.False(dialog.Model.IsBusy); Assert.Empty(documents.Documents);
                Assert.Contains("canceled", dialog.Model.Status);
            }
            finally { dialog.Close(); owner.Close(); }
            return true;
        }, Token);
    }

    [Fact]
    public async Task ReportFollowsInsertionsInSameTabAndReconstructsAfterToolClosureWithoutPersistingContent()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var registry = new ToolWindowRegistry();
            registry.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
            registry.Register(new(WorkspaceDockFactory.ActivityLogId, "Activity Log", () => new object()));
            registry.Register(new(WorkspaceDockFactory.GenerationReportId, "Generation Report", () => new GenerationReportViewModel(documents), Optional: true, ReleaseOnClose: true, RestoreVisibility: true));
            using var factory = new WorkspaceDockFactory(documents, registry); factory.CreateLayout(); factory.InitLayout(factory.Root);
            var result = await new SyntheticGeneration(documents).GenerateScenariosAsync(new() { OrdersPerEncounter = 2, ResultsPerOrder = 2, IncludeTransfer = true }, token: Token);
            factory.ShowTool(WorkspaceDockFactory.GenerationReportId);
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.GenerationReportId)!;
            var model = Assert.IsType<GenerationReportViewModel>(tool.Context); var reportView = new GenerationReportView(model);
            var raw = new FileDocumentView(result.Document); var grid = new Grid { RowDefinitions = new("*,220") };
            grid.Children.Add(raw); Grid.SetRow(reportView, 1); grid.Children.Add(reportView);
            var window = new Window { Width = 600, Height = 700, Content = grid }; window.Show();
            try
            {
                Pump(window); Assert.Same(result.Report, model.SyntheticReport); Assert.Null(model.Report); Assert.True(raw.GenerationIndicator.IsVisible);
                Assert.Contains("Combined encounter", raw.GenerationIndicator.Summary.Text!); Assert.Contains("Result reports: 4", reportView.Details.Text!);
                Assert.Contains("OBX observations: 8", reportView.Details.Text!); Assert.Contains("ADT^A02: 1", reportView.Details.Text!);
                Assert.DoesNotContain("SYNTHETIC0001", reportView.Details.Text!); Assert.DoesNotContain("EXAMPLE", reportView.Details.Text!);
                var oldReport = result.Report;
                var second = await new SyntheticGeneration(documents).GenerateScenariosAsync(new() { Scenario = SyntheticScenarioType.OrderResult }, result.Document, token: Token);
                Pump(window); Assert.Same(second.Report, model.SyntheticReport); Assert.Contains("Order to result", raw.GenerationIndicator.Summary.Text!);
                Assert.Equal(GenerationReportView.Format(second.Report!), reportView.Details.Text); Assert.NotSame(oldReport, second.Report);
                result.Document.Undo(); await result.Document.EditRefresh; Pump(window); Assert.Same(second.Report, model.SyntheticReport);
                var json = LayoutCodec.Encode(factory.Capture()); Assert.DoesNotContain("Order to result", json); Assert.DoesNotContain("2030", json);
                factory.CloseDockable(tool); Assert.Null(model.SyntheticReport); Assert.Same(second.Report, result.Document.SyntheticGenerationReport);
                factory.ShowTool(WorkspaceDockFactory.GenerationReportId);
                var replacement = Assert.IsType<GenerationReportViewModel>(tool.Context); Assert.NotSame(model, replacement); Assert.Same(second.Report, replacement.SyntheticReport);
                var plain = MessageFileReader.Open(documents, "Synthetic plain", Hl7Parser.Parse("MSH|^~\\&|SYN\r"));
                Assert.Null(replacement.SyntheticReport); Assert.Same(plain, documents.ActiveDocument);
                documents.Activate(result.Document.Id); Assert.Same(second.Report, replacement.SyntheticReport);
            }
            finally { window.Close(); }
            var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
            return true;
        }, Token);
    }

    [Fact]
    public async Task ScenarioUsesExistingShellActivationFocusAndReportCommand()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-scenario-shell-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            Application.Current!.Styles.Add(new Dock.Avalonia.Themes.Fluent.DockFluentTheme());
            Application.Current.DataTemplates.Add(new DockContentTemplate { DockedInspection = true });
            var window = new MainWindow(root) { Width = 1200, Height = 900 }; window.Show();
            var dock = window.FindControl<DockControl>("WorkspaceDock")!; var factory = Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory);
            factory.ShowTool(WorkspaceDockFactory.ComparisonId);
            var documents = Assert.IsType<ComparisonViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!.Context).Documents;
            factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!);
            try
            {
                var model = new SyntheticGenerationViewModel(documents, null) { Mode = SyntheticGenerationMode.CombinedEncounter, IncludeTransfer = true };
                await model.GenerateAsync(); var result = Assert.IsType<SyntheticGenerationResult>(model.Result);
                await window.ActivateGeneratedResultAsync(result).WaitAsync(TimeSpan.FromSeconds(10), Token); Pump(window);
                var raw = dock.GetVisualDescendants().OfType<FileDocumentView>().Single(v => v.Document == result.Document);
                Assert.Same(raw.ActiveView!.Editor.TextArea, window.FocusManager!.GetFocusedElement());
                Assert.Equal(result.MessageIds[0], result.Document.ActiveMessageId); Assert.Equal(5, result.Document.MessageCount);
                Click(raw.GenerationIndicator.ViewReport); Pump(window);
                var report = Assert.IsType<GenerationReportViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.GenerationReportId)!.Context);
                Assert.Same(result.Report, report.SyntheticReport); Assert.Equal(GenerationReportView.Format(result.Report!), dock.GetVisualDescendants().OfType<GenerationReportView>().Single().Details.Text);
                Assert.Null(factory.GetDockable<ITool>(WorkspaceDockFactory.ConnectionManagerId)!.Context);
                Assert.Null(factory.GetDockable<ITool>(WorkspaceDockFactory.MllpListenerId)!.Context);
            }
            finally
            {
                var docs = documents.Documents.ToArray(); foreach (var doc in docs) documents.Close(doc.Id, true);
                window.Close(); await Task.WhenAll(docs.Select(d => d.Disposal)); Directory.Delete(root, true);
            }
            return true;
        }, Token);
    }

    [Fact]
    public async Task SyntheticInsertionPreservesAndDisplaysExistingFhirProvenance()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            var fixture = Path.Combine(AppContext.BaseDirectory, "Fixtures", "fhir-r4", "patient-alpha.json");
            var import = await FhirPatientImport.FilesAsync([fixture], token: Token);
            var plan = FhirGenerationPlan.Create(import, new() { Scope = FhirGenerationScope.SelectedPatient, PatientKey = import.Patients[0].Key }, Token);
            var fhir = await new FhirGenerationPublisher(documents).PublishAsync(plan, token: Token);
            using var model = new GenerationReportViewModel(documents); var view = new GenerationReportView(model);
            var window = new Window { Content = view, Width = 600, Height = 600 }; window.Show();
            try
            {
                var synthetic = await new SyntheticGeneration(documents).GenerateScenariosAsync(new(), fhir.Document, token: Token); Pump(window);
                Assert.Same(fhir.Report, model.Report); Assert.Same(synthetic.Report, model.SyntheticReport);
                Assert.Equal(GenerationReportView.Format(synthetic.Report!) + "\n\n" + GenerationReportView.Format(fhir.Report), view.Details.Text);
                Assert.Same(fhir.Report, fhir.Document.GenerationReport);
            }
            finally { window.Close(); }
            var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
            return true;
        }, Token);
    }
}
