using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed partial class ScriptEditorTests
{
    [Theory]
    [InlineData(0, 1)] [InlineData(1, 2)]
    public async Task SameMainScriptReportsAndExportsOnEitherScopeInTheTool(int scope, int messages)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Synthetic + Synthetic));
            var source = await MessageFileReader.OpenFileAsync(docs, "Synthetic", input, TestContext.Current.CancellationToken);
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel);
            var view = new ScriptEditorView(model); var window = new Window { Content = view, Width = 1200, Height = 650 }; window.Show();
            try
            {
                Assert.Equal("Apply to:", view.ScopeLabel.Text);
                view.ExecutionScope.SelectedIndex = scope;
                model.Session.Source = """
                    var n = 0;
                    var r = Batch.Report("summary", "Messages");
                    void Main()
                    {
                        var message = Message;
                        n++;
                    }

                    void Complete()
                    {
                        r.Row(n.ToString());
                    }
                    """;
                Assert.True(model.Session.Save());
                var run = model.RunAsync(); Assert.True(model.IsBusy); Assert.True(view.Cancel.IsEnabled);
                Assert.False(view.New.IsEnabled); Assert.False(view.ExecutionScope.IsEnabled);
                await run; Dispatcher.UIThread.RunJobs();
                Assert.Empty(model.Diagnostics); Assert.True(view.Reports.IsEnabled); Assert.True(model.CanExport);
                var reportsWindow = new ScriptReportsWindow(model); reportsWindow.Show();
                Assert.Single(model.Reports); Assert.Equal("summary", ((ScriptReportInfo)reportsWindow.Reports.SelectedItem!).Name);
                Assert.True(reportsWindow.Export.IsEnabled);
                var origin = Assert.IsType<ScriptReportOrigin>(model.ReportOrigin);
                Assert.Equal(model.Session.DisplayName, origin.ScriptName); Assert.Equal(model.Scope, origin.Scope);
                Assert.Equal(source.Id, origin.Input!.DocumentId); Assert.Equal(source.SearchRevision, origin.Input.Revision);
                Assert.Equal(source.ActivityNumber, origin.SourceDocumentNumber);
                Assert.False(origin.SourceChangedDuringRun); Assert.Same(origin, model.OutputOrigin);
                Assert.Equal(model.ReportOrigin!.Description, reportsWindow.Origin.Text); reportsWindow.Close();
                var output = docs.Documents.Single(d => d != source); Assert.Equal(messages, output.MessageCount); Assert.True(output.IsDirty);
                var path = Path.Combine(files.Root, "summary.csv");
                Assert.True(await model.ExportReportAsync("summary", path));
                Assert.Equal($"\"Messages\"\r\n\"{messages}\"\r\n", await File.ReadAllTextAsync(path));
                Assert.False(await model.ExportReportAsync("summary", path)); Assert.Contains("Choose the destination again", model.Status);
                Assert.True(await model.ChooseAndExportReportDestinationAsync("summary", async () => await FileSaveDestination.CaptureAsync(path)));
                Assert.Single(model.Reports); Assert.False(source.IsDirty);
                window.Content = new ScriptEditorView(model); Assert.Equal(scope, ((ScriptEditorView)window.Content).ExecutionScope.SelectedIndex);
                docs.Close(output.Id, true); await output.Disposal;
            }
            finally { window.Close(); docs.Close(source.Id); await source.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task BatchFailureAndCancellationKeepLastSuccessfulReportsAndSafeDiagnostics(bool cancel)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            var source = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Synthetic)); docs.Open(source);
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel);
            model.Session.Name = "First synthetic report";
            model.Session.Source = """
                var r = Batch.Report("counts", "Count");
                void Main()
                {
                    var message = Message;
                    r.Row("1");
                }
                """;
            Assert.True(model.Session.Save());
            await model.RunAsync(); var first = docs.Documents.Single(d => d != source);
            var origin = Assert.IsType<ScriptReportOrigin>(model.ReportOrigin);
            docs.Activate(source.Id);
            Assert.True(await model.Session.NewAsync(Cancel));
            model.Session.Name = "Second synthetic script"; model.Scope = ScriptScope.WholeDocument;
            model.Session.Source = cancel ? """
                void Main()
                {
                    var message = Message;
                    while (true)
                    {
                    }
                }
                """ :
                """
                    void Main()
                    {
                        var message = Message;
                    }

                    void Complete()
                    {
                        throw new Exception("SYNTHETIC-PRIVATE");
                    }
                    """;
            var running = model.RunAsync(); Assert.True(model.IsBusy);
            if (cancel) model.Cancel();
            await running;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(2, docs.Documents.Count); Assert.Single(model.Reports); Assert.True(model.CanExport);
            Assert.DoesNotContain("PRIVATE", model.Status + string.Join("", model.Diagnostics) + string.Join("", docs.Activity.Snapshot()));
            Assert.Contains(cancel ? "canceled" : "failed", model.Status);
            Assert.Same(origin, model.ReportOrigin);
            Assert.Equal("First synthetic report", origin.ScriptName);
            Assert.Equal(ScriptScope.CurrentMessage, origin.Scope);
            Assert.DoesNotContain("Second synthetic", origin.Description);
            var path = Path.Combine(files.Root, "retained.csv"); Assert.True(await model.ExportReportAsync("counts", path));
            model.Dispose(); Assert.False(model.CanExport);
            docs.Close(first.Id, true); await first.Disposal; docs.Close(source.Id); await source.Disposal;
            Assert.Empty(Directory.GetDirectories(files.Options.StorageRoot!));
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
