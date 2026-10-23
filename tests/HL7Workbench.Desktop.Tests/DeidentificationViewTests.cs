using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class DeidentificationViewTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task PresetsCanBeReviewedDuplicatedConfiguredAndRunWithoutMutatingSource(int preset)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            var token = TestContext.Current.CancellationToken;
            var root = Path.Combine(Path.GetTempPath(), "hl7-deid-view-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            using var documents = new DocumentManager(); var source = MessageFileReader.Open(documents, "SYN-PRIVATE-SOURCE", Hl7Parser.Parse(SyntheticMessageGenerator.Generate(new(), token).Messages[0]));
            using var model = new ScriptEditorViewModel(documents, new ScriptLibrary(Path.Combine(root, "library")), new() { StorageRoot = Path.Combine(root, "jobs") });
            var id = preset == 0 ? ScriptLibrary.PatientDeidentifierId : preset == 1 ? ScriptLibrary.IdentifierPseudonymizerId : ScriptLibrary.TimestampShifterId;
            var window = new Window { Content = new ScriptEditorView(model), Width = 1100, Height = 800 }; window.Show();
            try
            {
                Assert.True(await model.Session.OpenAsync(id, () => Task.FromResult(UnsavedChangesDecision.Cancel)));
                Assert.True(model.Session.IsBundled); Assert.Contains("REVIEW REQUIRED", model.Session.Source); Assert.Single(documents.Documents);
                Assert.True(model.Session.Duplicate()); model.Session.Source = model.Session.Source.Replace("seed = 42", "seed = 123", StringComparison.Ordinal); Assert.True(model.Session.Save());
                model.Scope = ScriptScope.WholeDocument; await model.RunAsync(); Assert.Empty(model.Diagnostics);
                var output = Assert.Single(documents.Documents, d => !ReferenceEquals(d, source)); Assert.Contains("Review Required", output.Title);
                Assert.True(output.IsDirty); Assert.Null(output.SavePath); Assert.False(source.IsDirty); Assert.Equal(2, model.Reports.Count); Assert.True(model.CanExport);
                Assert.DoesNotContain("PRIVATE", output.Title); Assert.NotNull(model.ReportOrigin); Assert.False(model.ReportOrigin.SourceChangedDuringRun);
                var outputView = new FileDocumentView(output); window.Content = outputView;
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.True(outputView.DeidentificationIndicator.IsVisible);
                Assert.Empty(outputView.GetVisualDescendants().OfType<TransformationReportView>());
                using var reportModel = new DeidentificationReportViewModel(documents);
                var panel = new TransformationReportView(reportModel); window.Content = panel;
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.True(panel.IsVisible); Assert.Contains("Completed — review required", panel.Summary.Text);
                Assert.Contains(output.TransformationReport!.PresetName, panel.Provenance.Text); Assert.Contains("Whole document", panel.Provenance.Text);
                Assert.Contains("Captured source revision", panel.Provenance.Text); Assert.Contains("Messages processed: 1", panel.Summary.Text);
                Assert.DoesNotContain("PRIVATE", panel.Provenance.Text); Assert.Contains("No HIPAA certification", panel.Provenance.Text);
                source.IsEditMode = true; source.ReplaceWorkingText(source.EditableText + "NTE|1||SYN-LATER\r"); await source.EditRefresh;
                Assert.Contains("Stale source", panel.Provenance.Text);
                documents.Activate(source.Id); Assert.True(panel.EmptyState.IsVisible);
                window.Content = new FileDocumentView(source); Assert.False(((FileDocumentView)window.Content).DeidentificationIndicator.IsVisible);
                documents.Activate(output.Id);
                var reconstructed = new TransformationReportView(reportModel); window.Content = reconstructed;
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.Contains("Stale source", reconstructed.Provenance.Text);
                Assert.True(documents.Close(source.Id, discardChanges: true)); await source.Disposal;
                Dispatcher.UIThread.RunJobs();
                Assert.Contains("Source closed", reconstructed.Provenance.Text);
            }
            finally
            {
                window.Close(); model.Dispose(); var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal)); Directory.Delete(root, true);
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
