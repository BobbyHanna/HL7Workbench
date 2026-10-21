using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Interactivity;
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
    [Fact]
    public async Task OneNewActionCreatesMainDraftAndPreservesSaveDuplicateCancelAndReconstruction()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            var view = new ScriptEditorView(model); var window = new Window { Content = view, Width = 320, Height = 300 }; window.Show();
            try
            {
                Assert.True(view.Editor.IsReadOnly); var bundled = model.Session.Source;
                model.Session.Source = "changed"; Assert.Equal(bundled, model.Session.Source);
                Assert.True(model.Session.Duplicate()); Assert.False(view.Editor.IsReadOnly);
                view.New.Command!.Execute(null);
                Assert.Equal("New", view.New.Content); Assert.Null(view.New.Flyout);
                Assert.Equal("Apply to:", view.ScopeLabel.Text);
                Assert.Contains("void Main()", model.Session.Source); Assert.True(model.Session.IsDirty);
                var selected = Assert.IsType<ScriptLibraryItem>(view.Library.SelectedItem);
                Assert.Equal(model.Session.DisplayName, selected.Name); Assert.False(selected.Obsolete);
                var toolbar = ((StackPanel)view.LibraryToolbar.Content!).Children;
                Assert.Equal(6, toolbar.Count); Assert.Same(view.Library, toolbar[0]); Assert.Same(view.New, toolbar[2]);
                Assert.Same(view.Save, toolbar[3]); Assert.Same(view.Actions, toolbar[4]); Assert.NotNull(view.Actions.Flyout);
                Assert.Same(view.ApiReference, toolbar[5]); Assert.Same(model.ApiReferenceCommand, view.ApiReference.Command);
                Assert.Same(model.DuplicateCommand, view.Duplicate.Command); Assert.Same(model.ReportsCommand, view.Reports.Command);
                Assert.True(model.Session.Save()); var id = model.Session.Id; var saved = model.Session.Source;
                model.Session.Source += "\n// synthetic draft"; var draft = model.Session.Source;
                var prompts = 0; view.PromptUnsavedAsync = () => { prompts++; return Cancel(); };
                view.New.Command!.Execute(null);
                Assert.Equal(1, prompts); Assert.Equal(id, model.Session.Id); Assert.Equal(draft, model.Session.Source);
                Assert.True(model.Session.Duplicate()); Assert.Equal(draft, model.Session.Source);
                Assert.Equal(saved, files.Library.Open(id).Source);
                window.Content = view = new ScriptEditorView(model); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.Equal(draft, view.Editor.Text); Assert.Equal(model.Session.DisplayName, ((ScriptLibraryItem)view.Library.SelectedItem!).Name);
                Assert.Equal(ScrollBarVisibility.Auto, view.LibraryToolbar.HorizontalScrollBarVisibility);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public async Task ObsoleteScriptsShowWarningAndCannotRunUntilExplicitAdoption(int version)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); var directory = Path.Combine(files.Root, "library"); Directory.CreateDirectory(directory);
            var id = Guid.NewGuid(); var path = Path.Combine(directory, id.ToString("N") + ".json");
            var original = System.Text.Json.JsonSerializer.Serialize(new { Version = version, Id = id, Name = "Legacy synthetic", Source = "Message.Exclude();", Mode = 0 });
            File.WriteAllText(path, original);
            using var docs = new DocumentManager();
            var source = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Synthetic)); docs.Open(source);
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            Assert.True(await model.Session.OpenAsync(id, Cancel));
            var view = new ScriptEditorView(model); var window = new Window { Content = view, Width = 600, Height = 350 }; window.Show();
            try
            {
                Assert.True(model.Session.IsObsolete); Assert.False(model.CanRun); Assert.False(view.Run.IsEnabled);
                Assert.True(((ScriptLibraryItem)view.Library.SelectedItem!).Obsolete);
                Assert.Contains("Obsolete", view.Status.Text!); Assert.Equal(original, File.ReadAllText(path));
                await model.RunAsync(); Assert.Single(docs.Documents);
                model.Session.Source = "void Main() { Message.Exclude(); }";
                Assert.False(model.CanRun); Assert.Equal(original, File.ReadAllText(path));
                view.Save.Command!.Execute(null);
                Assert.True(model.CanRun); Assert.False(model.Session.IsObsolete);
                Assert.False(((ScriptLibraryItem)view.Library.SelectedItem!).Obsolete);
                Assert.DoesNotContain("Mode", File.ReadAllText(path));
                await model.RunAsync(); Assert.Empty(model.Diagnostics); Assert.Equal(2, docs.Documents.Count);
                var output = docs.Documents.Single(d => d != source); Assert.Equal(0, output.MessageCount);
                Assert.Equal(Synthetic, source.Message.RawText);
                docs.Close(output.Id, true); await output.Disposal;
            }
            finally { window.Close(); docs.Close(source.Id); await source.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task RetainedReportAttributionSurvivesScriptSwitchRenameAndViewReconstruction()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            var source = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Synthetic)); docs.Open(source);
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel);
            model.Session.Name = "Synthetic counters";
            model.Session.Source = "var r = Batch.Report(\"counts\", \"Count\"); void Main() { r.Row(\"1\"); }";
            Assert.True(model.Session.Save());
            var view = new ScriptEditorView(model); var window = new Window { Content = view, Width = 320, Height = 300 }; window.Show();
            try
            {
                Assert.Null(model.ReportOrigin); Assert.False(view.Reports.IsEnabled);
                model.Scope = ScriptScope.WholeDocument;
                var run = model.RunAsync();
                model.Session.Name = "Renamed during execution";
                await run; Dispatcher.UIThread.RunJobs(); Assert.Empty(model.Diagnostics);
                var reports = model.Reports; var origin = Assert.IsType<ScriptReportOrigin>(model.ReportOrigin);
                Assert.Equal("Synthetic counters", origin.ScriptName); Assert.Equal(ScriptScope.WholeDocument, origin.Scope);
                Assert.Equal(source.Id, origin.Input!.DocumentId); Assert.False(origin.SourceChangedDuringRun);
                Assert.Equal(source.ActivityNumber, origin.SourceDocumentNumber);
                Assert.Same(origin, model.OutputOrigin);
                Assert.True(await model.Session.OpenAsync(ScriptLibrary.ExampleId, () => Task.FromResult(UnsavedChangesDecision.Discard)));
                model.Scope = ScriptScope.CurrentMessage;
                Assert.Same(reports, model.Reports); Assert.Same(origin, model.ReportOrigin);
                Assert.Contains("Synthetic counters", ToolTip.GetTip(view.Reports)!.ToString()!);
                Assert.Contains("Whole document", ToolTip.GetTip(view.Reports)!.ToString()!);
                Assert.DoesNotContain("SYN-001", origin.Description); Assert.DoesNotContain("r.Row", origin.Description);
                Assert.DoesNotContain("Renamed during", origin.Description); Assert.True(model.CanExport);
                window.Content = view = new ScriptEditorView(model); window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                var reportWindow = new ScriptReportsWindow(model); reportWindow.Show();
                Assert.True(view.Reports.IsEnabled); Assert.Equal(origin.Description, reportWindow.Origin.Text);
                Assert.Equal(reports, reportWindow.Reports.ItemsSource); reportWindow.Close();
                Assert.True(view.ExecutionToolbar.Extent.Width > view.ExecutionToolbar.Viewport.Width);
                view.ExecutionToolbar.Offset = new Vector(view.ExecutionToolbar.Extent.Width, 0); window.UpdateLayout();
                Assert.True(view.ExecutionToolbar.Offset.X > 0);
                var path = Path.Combine(files.Root, "retained.csv"); Assert.True(await model.ExportReportAsync("counts", path));
                Assert.False(await model.ExportReportAsync("counts", path)); Assert.Same(origin, model.ReportOrigin);
                docs.Activate(source.Id); Assert.True(await model.Session.OpenAsync(ScriptLibrary.PatientExampleId, Cancel));
                await model.RunAsync(); Assert.Equal(ScriptLibrary.PatientExample.Name, model.ReportOrigin!.ScriptName);
                Assert.NotSame(origin, model.ReportOrigin);
                docs.Activate(source.Id); Assert.True(await model.Session.OpenAsync(ScriptLibrary.ExampleId, Cancel));
                await model.RunAsync(); Assert.Null(model.ReportOrigin); Assert.Empty(model.Reports);
                Assert.False(view.Reports.IsEnabled); Assert.False(model.CanExport);
                foreach (var result in docs.Documents.Where(d => d != source).ToArray()) { docs.Close(result.Id, true); await result.Disposal; }
            }
            finally { window.Close(); docs.Close(source.Id); await source.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
