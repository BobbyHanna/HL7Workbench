using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class DeidentificationReportToolTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task<MessageDocument> Run(DocumentManager documents)
    {
        var source = MessageFileReader.Open(documents, "SYN-PRIVATE-SOURCE", Hl7Parser.Parse(SyntheticMessageGenerator.Generate(new(), Token).Messages[0]));
        var result = await new ScriptExecution(documents).RunAsync(source, new(ScriptLibrary.PatientDeidentifier.Source), ScriptScope.WholeDocument, token: Token);
        result.Reports?.Dispose(); return result.Document;
    }
    [Fact]
    public async Task ActualShellOpensOneReportFollowsTabsAndRetainsReportsAcrossCloseReopen()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-review-tool-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            Application.Current!.Styles.Add(new Dock.Avalonia.Themes.Fluent.DockFluentTheme());
            Application.Current.DataTemplates.Add(new DockContentTemplate { DockedInspection = true });
            var window = new MainWindow(root); window.Show(); Pump(window);
            var documents = (DocumentManager)typeof(MainWindow).GetField("documents", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
            var dock = window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!; var factory = (WorkspaceDockFactory)dock.Layout!.Factory!;
            try
            {
                var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DeidentificationReportId)!; Assert.Null(tool.Context);
                var first = await Run(documents); factory.OpenDocument(first.Id); Pump(window);
                var raw = dock.GetVisualDescendants().OfType<FileDocumentView>().Single(v => v.Document == first);
                Assert.Empty(raw.GetVisualDescendants().OfType<TransformationReportView>());
                Assert.Empty(dock.GetVisualDescendants().OfType<ScriptEditorView>());
                Click(raw.DeidentificationIndicator.ViewReport); Pump(window);
                var model = Assert.IsType<DeidentificationReportViewModel>(tool.Context); Assert.Same(first.TransformationReport, model.Report);
                var view = Assert.Single(dock.GetVisualDescendants().OfType<TransformationReportView>()); Assert.True(view.Summary.IsFocused);
                Assert.Contains("High-risk findings: 1", view.Summary.Text); Assert.Contains("Retained values requiring review: 3 of 8", view.Summary.Text);
                Assert.Contains(view.Findings.Children.OfType<SelectableTextBlock>(), t => t.Text!.Contains("PV1[1]-7"));
                Assert.DoesNotContain("SYN-PRIVATE", string.Join(" ", view.GetVisualDescendants().OfType<SelectableTextBlock>().Select(t => t.Text)));
                Click(raw.DeidentificationIndicator.ViewReport); Pump(window); Assert.Same(model, tool.Context); Assert.True(view.Summary.IsFocused);
                Assert.Single(dock.GetVisualDescendants().OfType<TransformationReportView>());
                var second = await Run(documents); factory.OpenDocument(second.Id); Pump(window); Assert.Same(second.TransformationReport, model.Report);
                factory.OpenDocument(first.Id); Pump(window); Assert.Same(first.TransformationReport, model.Report);
                first.IsEditMode = true; first.ReplaceWorkingText(first.EditableText + "NTE|1||SYN-LATER\r"); await first.EditRefresh; Pump(window);
                Assert.Contains("Stale output", view.Provenance.Text); Assert.NotEmpty(view.Findings.Children);
                factory.CloseDockable(tool); Pump(window); Assert.Null(model.Report); Assert.Null(tool.Context); Assert.NotNull(first.TransformationReport);
                var menu = window.FindControl<Menu>("MainMenu")!.Items.Cast<MenuItem>().Single(m => Equals(m.Header, "_View")).Items.Cast<MenuItem>().Single(m => Equals(m.Header, "_De-identification Report"));
                menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(window);
                var next = Assert.IsType<DeidentificationReportViewModel>(tool.Context); Assert.NotSame(model, next); Assert.Same(first.TransformationReport, next.Report);
                var plain = MessageFileReader.Open(documents, "Plain", Hl7Parser.Parse("MSH|^~\\&|SYN|TEST|DEST|TEST|20300101||ADT^A01|SYN|T|2.5.1\r"));
                factory.OpenDocument(plain.Id); Pump(window); Assert.Null(next.Report);
                Assert.True(dock.GetVisualDescendants().OfType<TransformationReportView>().Single().EmptyState.IsVisible);
            }
            finally
            {
                foreach (var doc in documents.Documents.ToArray()) documents.Close(doc.Id, true);
                window.Close(); Directory.Delete(root, true);
            }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ReconstructedDockedOrFloatingToolPersistsOnlyPlacement(bool floating)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var output = await Run(documents);
            var tools = new ToolWindowRegistry(); tools.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
            tools.Register(new(WorkspaceDockFactory.DeidentificationReportId, "De-identification Report", () => new DeidentificationReportViewModel(documents), true, true, RestoreVisibility: true));
            using var factory = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
            factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.DeidentificationReportId);
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DeidentificationReportId)!;
            var original = Assert.IsType<DeidentificationReportViewModel>(tool.Context);
            var window = new Window { Content = new DockContentTemplate().Build(tool) }; window.Show();
            try
            {
                Pump(window); window.Content = null; Pump(window);
                window.Content = new DockContentTemplate().Build(tool); Pump(window);
                Assert.Contains("Messages processed: 1", window.GetVisualDescendants().OfType<TransformationReportView>().Single().Summary.Text);
                if (floating) factory.FloatDockable(tool);
                var json = LayoutCodec.Encode(factory.Capture());
                Assert.DoesNotContain(output.TransformationReport!.ExecutionId.ToString(), json); Assert.DoesNotContain("PV1", json); Assert.DoesNotContain("SYN-PRIVATE", json);
                factory.CloseDockable(tool); Pump(window); Assert.Null(original.Report); Assert.NotNull(output.TransformationReport);
                factory.Restore(LayoutCodec.Decode(json), [new(0, 0, 1920, 1080)]);
                var next = Assert.IsType<DeidentificationReportViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.DeidentificationReportId)!.Context);
                Assert.NotSame(original, next); Assert.Same(output.TransformationReport, next.Report);
                Assert.Equal(floating, Assert.Single(factory.Capture().ToolPlacements!, p => p.Id == WorkspaceDockFactory.DeidentificationReportId).Floating is not null);
                factory.CloseFloatingWindows(); factory.CreateLayout(); Assert.Null(next.Report);
            }
            finally { window.Close(); factory.CloseFloatingWindows(); }
            return true;
        }, Token);
    }
}
