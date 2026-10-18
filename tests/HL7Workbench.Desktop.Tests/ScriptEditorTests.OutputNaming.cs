using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Dock.Model.Controls;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed partial class ScriptEditorTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OutputNameUsesExecutionTimeScriptNameOrCompleteOverrideDespiteRenameAndSelectionChanges(bool custom)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            var source = new MessageDocument("synthetic", "Source.hl7", Hl7Parser.Parse(Synthetic)); docs.Open(source);
            var token = TestContext.Current.CancellationToken;
            var sourcePath = Path.Combine(files.Root, "Source.hl7"); Directory.CreateDirectory(files.Root);
            await source.SaveAsync(sourcePath, token); source.IsEditMode = true; var revision = source.SearchRevision;
            using var model = new ViewModels.ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel); model.Session.Name = "Patient Deidentifier";
            model.Session.Source = "var r = Batch.Report(\"counts\", \"Count\"); void Main() { r.Row(\"1\"); }" +
                (custom ? " void Complete() { Batch.SetOutputFileName(\"Custom_Audit.HL7\"); Batch.SetOutputFileName(\"../blocked\"); }" : "");
            Assert.True(model.Session.Save());
            var view = new ScriptEditorView(model); var window = new Window { Content = view, Width = 1000, Height = 750 }; window.Show();
            var edited = false;
            model.PropertyChanged += (_, _) =>
            {
                if (edited || !model.IsBusy || !model.Status.StartsWith("Compiling:", StringComparison.Ordinal)) return;
                edited = true; source.ReplaceWorkingText(Synthetic + "NTE|SYN-LATER\r");
            };
            try
            {
                var running = model.RunAsync(); Assert.True(model.IsBusy);
                model.Session.Name = "Renamed during execution";
                Assert.True(await model.Session.OpenAsync(ScriptLibrary.ExampleId, () => Task.FromResult(UnsavedChangesDecision.Discard)));
                await running; Dispatcher.UIThread.RunJobs();
                Assert.Empty(model.Diagnostics); Assert.True(edited); Assert.True(model.OutputOrigin!.SourceChangedDuringRun);
                Assert.Equal("Patient Deidentifier", model.OutputOrigin.ScriptName); Assert.Equal(revision, model.OutputOrigin.Input!.Revision);
                Assert.Equal(source.Id, model.OutputOrigin.Input.DocumentId); Assert.Same(model.OutputOrigin, model.ReportOrigin);
                var output = Assert.Single(docs.Documents, d => d != source);
                var expected = custom ? "Custom_Audit.hl7" : "Patient Deidentifier.hl7";
                Assert.Equal(expected, output.Title); Assert.Equal(expected, model.OutputOrigin.SuggestedFileName);
                Assert.Equal(expected, SaveFilePicker.Hl7Options(output).SuggestedFileName);
                Assert.True(output.IsDirty); Assert.Null(output.SavePath); Assert.Single(model.Reports);
                Assert.Equal(Synthetic, output.EditableText); Assert.Equal(Synthetic + "NTE|SYN-LATER\r", source.EditableText);
                Assert.Contains("Stale source", view.OutputAttribution.Text);
                Assert.Equal(custom, model.OutputOrigin.IgnoredOutputFileNameSuggestion);
                if (custom) Assert.Contains("invalid filename suggestion was ignored", model.Status);
                var path = Path.Combine(files.Root, expected);
                Assert.True(await new DocumentSaveWorkflow(docs).SaveAsync(output, true,
                    () => Task.FromResult<FileSaveDestination?>(FileSaveDestination.NewFile(path)), token));
                Assert.Equal(expected, output.DisplayTitle); Assert.False(output.IsDirty); Assert.Equal(path, output.SavePath);
                Assert.Equal(sourcePath, source.SavePath); Assert.Equal("Source.hl7", source.Title); Assert.Equal("synthetic", source.Id);
                window.Content = view = new ScriptEditorView(model); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.Contains(expected, ToolTip.GetTip(view.OutputAttribution)!.ToString());
                Assert.Contains("Stale source", view.OutputAttribution.Text); await source.EditRefresh;
            }
            finally
            {
                window.Close(); model.Dispose(); var owned = docs.Documents.ToArray(); docs.Dispose();
                await Task.WhenAll(owned.Select(d => d.Disposal));
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DuplicateGeneratedDockTabsKeepUnsuffixedSaveAsSuggestionsAndDirtyDraftNameOmitsMarker()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            var source = new MessageDocument("synthetic", "Source.hl7", Hl7Parser.Parse(Synthetic)); docs.Open(source);
            using var model = new ViewModels.ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel); model.Session.Name = "Unsaved Script"; model.Session.Source = "void Main() {}";
            using var factory = new WorkspaceDockFactory(docs, new ToolWindowRegistry()); factory.CreateLayout();
            model.ReviewRequested += factory.OpenDocument;
            try
            {
                Assert.True(model.Session.IsDirty); await model.RunAsync(); Assert.Empty(model.Diagnostics);
                var first = Assert.Single(docs.Documents, d => d != source);
                docs.Activate(source.Id); await model.RunAsync(); Assert.Empty(model.Diagnostics);
                var second = Assert.Single(docs.Documents, d => d != source && d != first);
                Assert.NotEqual(first.Id, second.Id); Assert.Equal("Unsaved Script.hl7", first.Title); Assert.Equal(first.Title, second.Title);
                Assert.Equal("Unsaved Script.hl7 *", factory.GetDockable<IDocument>(first.Id)!.Title);
                Assert.Equal("Unsaved Script.hl7 (2) *", factory.GetDockable<IDocument>(second.Id)!.Title);
                Assert.Equal(first.Title, SaveFilePicker.Hl7Options(first).SuggestedFileName);
                Assert.Equal(first.Title, SaveFilePicker.Hl7Options(second).SuggestedFileName);
                Directory.CreateDirectory(files.Root); var target = Path.Combine(files.Root, "Saved.hl7");
                await second.SaveAsync(target, TestContext.Current.CancellationToken);
                Assert.Equal("Saved.hl7", factory.GetDockable<IDocument>(second.Id)!.Title);
                Assert.Equal("Unsaved Script.hl7 *", factory.GetDockable<IDocument>(first.Id)!.Title);
                Assert.Equal("Saved.hl7", SaveFilePicker.Hl7Options(second).SuggestedFileName);
                Assert.Equal("Source.hl7", source.Title); Assert.Null(source.SavePath);
            }
            finally
            {
                model.Dispose(); var owned = docs.Documents.ToArray(); docs.Dispose(); await Task.WhenAll(owned.Select(d => d.Disposal));
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
