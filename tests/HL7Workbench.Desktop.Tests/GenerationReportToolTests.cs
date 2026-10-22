using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class GenerationReportToolTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task<FhirGenerationPlan> Plan(FhirGenerationScope scope = FhirGenerationScope.SelectedPatient)
    {
        var fixtures = Path.Combine(AppContext.BaseDirectory, "Fixtures", "fhir-r4");
        var data = await FhirPatientImport.FilesAsync([Path.Combine(fixtures, "patient-alpha.json"), Path.Combine(fixtures, "patient-beta.json")], true, token: Token);
        return FhirGenerationPlan.Create(data, new() { PatientKey = data.Patients[0].Key, Scope = scope }, Token);
    }
    private static MessageDocument Plain(DocumentManager documents) => MessageFileReader.Open(documents, "synthetic-plain.hl7", Hl7Parser.Parse("MSH|^~\\&|SYN|||||||SYN-PLAIN|T|2.5.1\rPID|1||SYNTHETIC\r"));

    [Theory]
    [InlineData(false, 380)] [InlineData(true, 1100)]
    public async Task CompactIndicatorUsesOriginalMetadataAndCompleteReportScrollsInBothThemes(bool dark, int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var result = await new FhirGenerationPublisher(documents).PublishAsync(await Plan(), token: Token);
            using var model = new GenerationReportViewModel(documents);
            var raw = new FileDocumentView(result.Document); var report = new GenerationReportView(model);
            var grid = new Grid { RowDefinitions = new("*,230") }; grid.Children.Add(raw); Grid.SetRow(report, 1); grid.Children.Add(report);
            var window = new Window { Content = grid, Width = width, Height = 800, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Pump(window); var indicator = raw.GenerationIndicator;
                Assert.True(indicator.IsVisible); Assert.InRange(indicator.Bounds.Height, 20, 40);
                Assert.Equal($"Generated from FHIR R4 · {result.Report.GeneratedMessages:N0} messages · {result.Report.PatientsIncluded:N0} patient", indicator.Summary.Text);
                Assert.DoesNotContain('\n', indicator.Summary.Text!); Assert.True(indicator.ViewReport.Bounds.Width > 60);
                Assert.Empty(raw.GetVisualDescendants().OfType<GenerationReportView>());
                Assert.Equal(GenerationReportView.Format(result.Report), report.Details.Text);
                Assert.Contains("Selected mapping warnings:", report.Details.Text!); Assert.Contains("Import references:", report.Details.Text!);
                Assert.Contains("Scoped exclusions:", report.Details.Text!); Assert.Contains("source fingerprint", report.Details.Text!);
                Assert.Contains(FhirGenerationReport.Notice, report.Details.Text!); Assert.NotEmpty(result.Report.Warnings);
                Assert.DoesNotContain("SYN-ALPHA", report.Details.Text!); Assert.DoesNotContain("patient-alpha.json", report.Details.Text!);
                Assert.True(report.ReportScroll.Extent.Height > report.ReportScroll.Viewport.Height);
                report.ReportScroll.Offset = new Vector(0, 150); Pump(window); Assert.True(report.ReportScroll.Offset.Y > 0);
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark; Pump(window);
                Assert.Equal(window.RequestedThemeVariant, report.ActualThemeVariant); Assert.Equal(GenerationReportView.Format(result.Report), report.Details.Text);
                window.Width = 520; Pump(window); Assert.True(report.ReportScroll.Viewport.Width > 100);
                var plain = Plain(documents); Pump(window); Assert.False(new FileDocumentView(plain).GenerationIndicator.IsVisible);
                Assert.True(report.EmptyState.IsVisible); Assert.False(report.Details.IsVisible); Assert.Equal("", report.Details.Text);
                Assert.Equal(0, report.ReportScroll.Offset.Y);
                documents.Activate(result.Document.Id); Pump(window); Assert.Equal(GenerationReportView.Format(result.Report), report.Details.Text);
                Assert.Equal(0, report.ReportScroll.Offset.Y);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }

    [Fact]
    public async Task ActualWindowIndicatorAndMenuFocusOneToolThatFollowsDocumentsAndReopensWithoutDeletingReports()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-generation-report-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            Application.Current!.Styles.Add(new Dock.Avalonia.Themes.Fluent.DockFluentTheme());
            Application.Current.DataTemplates.Add(new DockContentTemplate { DockedInspection = true });
            var window = new MainWindow(root); window.Show(); Pump(window);
            var documents = (DocumentManager)typeof(MainWindow).GetField("documents", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            var dock = window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!; var factory = (WorkspaceDockFactory)dock.Layout!.Factory!;
            try
            {
                var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.GenerationReportId)!; Assert.Null(tool.Context);
                var first = await new FhirGenerationPublisher(documents).PublishAsync(await Plan(), token: Token); factory.OpenDocument(first.Document.Id); Pump(window);
                var raw = dock.GetVisualDescendants().OfType<FileDocumentView>().Single(v => v.Document == first.Document);
                Click(raw.GenerationIndicator.ViewReport); Pump(window);
                var model = Assert.IsType<GenerationReportViewModel>(tool.Context); Assert.Same(first.Report, model.Report);
                var view = dock.GetVisualDescendants().OfType<GenerationReportView>().Single(); Assert.True(view.Details.IsFocused, "Opening the report should focus its selectable details after Dock attaches it.");
                Click(raw.GenerationIndicator.ViewReport); Pump(window); Assert.Same(model, tool.Context); Assert.True(view.Details.IsFocused, "Reopening should focus the existing report.");
                Assert.Same(tool, ((IToolDock)tool.Owner!).ActiveDockable); Assert.Single(dock.GetVisualDescendants().OfType<GenerationReportView>());
                var second = await new FhirGenerationPublisher(documents).PublishAsync(await Plan(FhirGenerationScope.EntireImport), token: Token); factory.OpenDocument(second.Document.Id); Pump(window);
                Assert.NotEqual(first.Report.GeneratedMessages, second.Report.GeneratedMessages); Assert.Same(second.Report, model.Report);
                Assert.Equal(GenerationReportView.Format(second.Report), view.Details.Text);
                factory.OpenDocument(first.Document.Id); Pump(window); Assert.Same(first.Report, model.Report);
                var plain = Plain(documents); factory.OpenDocument(plain.Id); Pump(window); Assert.Null(model.Report); Assert.True(view.EmptyState.IsVisible, "Plain document should clear the tool.");
                factory.CloseDockable(tool); Pump(window); Assert.Null(tool.Context); Assert.Null(model.Report); Assert.Same(first.Report, first.Document.GenerationReport);
                factory.OpenDocument(second.Document.Id);
                var menu = window.FindControl<Menu>("MainMenu")!.Items.Cast<MenuItem>().Single(m => Equals(m.Header, "_View")).Items.Cast<MenuItem>().Single(m => Equals(m.Header, "_Generation Report"));
                menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(window);
                var next = Assert.IsType<GenerationReportViewModel>(tool.Context); Assert.NotSame(model, next); Assert.Same(second.Report, next.Report);
                Assert.Equal(GenerationReportView.Format(second.Report), dock.GetVisualDescendants().OfType<GenerationReportView>().Single().Details.Text);
                factory.ConfirmDocumentCloseAsync = _ => Task.FromResult(true); factory.CloseDockable(factory.GetDockable<IDocument>(second.Document.Id)!); Pump(window);
                Assert.DoesNotContain(second.Document, documents.Documents); Assert.Same(first.Report, next.Report);
                factory.CloseDockable(factory.GetDockable<IDocument>(first.Document.Id)!); Pump(window); Assert.Null(next.Report);
                Assert.True(dock.GetVisualDescendants().OfType<GenerationReportView>().Single().EmptyState.IsVisible);
                Assert.Null(factory.GetDockable<ITool>(WorkspaceDockFactory.BatchTransmissionId)!.Context);
                Assert.Null(factory.GetDockable<ITool>(WorkspaceDockFactory.ConnectionManagerId)!.Context);
                Assert.Null(factory.GetDockable<ITool>(WorkspaceDockFactory.MllpListenerId)!.Context);
            }
            finally
            {
                foreach (var document in documents.Documents.ToArray()) documents.Close(document.Id, true);
                window.Close(); Directory.Delete(root, true);
            }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DockAndFloatRestoreOnlyPlacementAndReconstructTheActiveReport(bool floating)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var result = await new FhirGenerationPublisher(documents).PublishAsync(await Plan(), token: Token);
            var tools = new ToolWindowRegistry(); tools.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
            tools.Register(new(WorkspaceDockFactory.GenerationReportId, "Generation Report", () => new GenerationReportViewModel(documents), true, true, RestoreVisibility: true));
            using var factory = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
            factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.GenerationReportId);
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.GenerationReportId)!; var original = Assert.IsType<GenerationReportViewModel>(tool.Context);
            var host = new DockContentTemplate().Build(tool); var window = new Window { Content = host }; window.Show();
            try
            {
                Pump(window); var firstView = Assert.Single(host.GetVisualDescendants().OfType<GenerationReportView>());
                window.Content = null; Pump(window); Assert.Equal("", firstView.Details.Text); Assert.Same(result.Report, original.Report);
                window.Content = new DockContentTemplate().Build(tool); Pump(window);
                Assert.Equal(GenerationReportView.Format(result.Report), window.GetVisualDescendants().OfType<GenerationReportView>().Single().Details.Text);
                if (floating) factory.FloatDockable(tool); ((IToolDock)tool.Owner!).Proportion = .31;
                var layout = factory.Capture(); var json = LayoutCodec.Encode(layout);
                Assert.DoesNotContain(result.Report.SourceFingerprint, json); Assert.DoesNotContain(result.Report.SessionId.ToString(), json);
                Assert.DoesNotContain("Selected mapping warnings", json); Assert.False(tool.CanDockAsDocument);
                factory.CloseDockable(tool); Pump(window); Assert.Null(original.Report); Assert.Same(result.Report, result.Document.GenerationReport);
                factory.Restore(LayoutCodec.Decode(json), [new(0, 0, 1920, 1080)]);
                var restored = factory.GetDockable<ITool>(WorkspaceDockFactory.GenerationReportId)!;
                var next = Assert.IsType<GenerationReportViewModel>(restored.Context); Assert.NotSame(original, next); Assert.Same(result.Report, next.Report);
                var placement = Assert.Single(factory.Capture().ToolPlacements!, p => p.Id == WorkspaceDockFactory.GenerationReportId);
                Assert.Equal(floating, placement.Floating is not null);
                if (!floating) Assert.Equal(.31, placement.Proportion);
                else Assert.Equal(Assert.Single(layout.ToolPlacements!, p => p.Id == WorkspaceDockFactory.GenerationReportId).Floating, placement.Floating);
                factory.CloseFloatingWindows(); factory.CreateLayout(); Assert.Null(next.Report);
                factory.ShowTool(WorkspaceDockFactory.GenerationReportId); Assert.Same(result.Report, Assert.IsType<GenerationReportViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.GenerationReportId)!.Context).Report);
            }
            finally { window.Close(); factory.CloseFloatingWindows(); }
            return true;
        }, Token);
    }

    [Fact]
    public async Task NavigationEditingUndoRedoAndSaveKeepReportIdentityAndExactGeneratedBytes()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-generation-bytes-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            using var documents = new DocumentManager(); var plan = await Plan(); var expected = plan.Generate(Token).GetUtf8Bytes();
            var result = await new FhirGenerationPublisher(documents).PublishAsync(plan, token: Token); var doc = result.Document;
            using var model = new GenerationReportViewModel(documents); var raw = new FileDocumentView(doc); var report = new GenerationReportView(model);
            var body = new Grid { RowDefinitions = new("*,220") }; body.Children.Add(raw); Grid.SetRow(report, 1); body.Children.Add(report);
            var window = new Window { Content = body, Width = 900, Height = 750 }; window.Show();
            try
            {
                var path = Path.Combine(root, "synthetic-output.hl7"); await doc.SaveAsync(path, Token); Assert.Equal(expected, await File.ReadAllBytesAsync(path, Token));
                var ids = doc.Messages.Select(m => m.Id).ToArray(); var metadata = raw.GenerationIndicator.Summary.Text;
                Pump(window); Assert.True(raw.MessageNumberInput.Focus());
                await raw.NavigateAsync(2); Pump(window); var original = doc.EditableText!;
                Assert.False(report.Details.IsFocused); // RAW rebuild can clear focus; the report must not take it.
                doc.IsEditMode = true; doc.ReplaceWorkingText(original + "NTE|1||SYNTHETIC EDIT\r"); await doc.EditRefresh;
                doc.Undo(); await doc.EditRefresh; Pump(window); Assert.Equal(original, doc.EditableText);
                doc.Redo(); await doc.EditRefresh; Assert.Contains("SYNTHETIC EDIT", doc.EditableText!);
                doc.Undo(); await doc.EditRefresh; doc.IsEditMode = false; await raw.NavigateAsync(1); Pump(window);
                Assert.Same(result.Report, model.Report); Assert.Same(result.Report, doc.GenerationReport); Assert.Equal(ids, doc.Messages.Select(m => m.Id));
                Assert.Equal(metadata, raw.GenerationIndicator.Summary.Text); Assert.True(raw.GenerationIndicator.IsVisible);
                window.Content = new FileDocumentView(doc); Pump(window); Assert.Equal(metadata, ((FileDocumentView)window.Content).GenerationIndicator.Summary.Text);
                var roundTrip = Path.Combine(root, "synthetic-roundtrip.hl7");
                await doc.SaveAsync(roundTrip, Token); Assert.Equal(expected, await File.ReadAllBytesAsync(roundTrip, Token));
                Assert.Equal(expected, plan.Generate(Token).GetUtf8Bytes());
            }
            finally { window.Close(); Directory.Delete(root, true); }
            return true;
        }, Token);
    }
}
