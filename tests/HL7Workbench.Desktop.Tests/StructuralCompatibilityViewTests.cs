using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class StructuralCompatibilityViewTests
{
    private const string A = "MSH|^~\\&|SYN-A\rPID|A\r";
    private const string B = "MSH|^~\\&|SYN-B\rPID|B\r";
    private const string C = "MSH*$%!@*SYN-C\rPID*C\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
    private static void Key(Window window, Key key)
    {
        var physical = key == Avalonia.Input.Key.Z ? PhysicalKey.Z : PhysicalKey.Y;
        window.KeyPress(key, RawInputModifiers.Control, physical, null); window.KeyRelease(key, RawInputModifiers.Control, physical, null);
    }
    private static async Task<MessageDocument> Open(DocumentManager manager)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(A + B));
        return await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, Token);
    }

    [Fact]
    public async Task CapturedOutputAndReportsKeepVisibleStaleSourceAttributionAcrossFailureAndViewReconstruction()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); var doc = await Open(manager); doc.IsEditMode = true;
            doc.ReplaceWorkingText(A + "NTE|SYN-CAPTURED\r"); var revision = doc.SearchRevision;
            var directory = Path.Combine(Path.GetTempPath(), "hl7-script-stale-ui-" + Guid.NewGuid().ToString("N"));
            using var model = new ScriptEditorViewModel(manager, new ScriptLibrary(directory),
                new ScriptExecutionOptions { StorageRoot = Path.Combine(directory, "jobs") });
            Assert.True(await model.Session.NewAsync(() => Task.FromResult(UnsavedChangesDecision.Cancel)));
            model.Session.Source = "var r = Batch.Report(\"counts\", \"Count\"); void Main() { r.Row(\"1\"); }";
            var view = new ScriptEditorView(model); var window = new Window { Content = view, Width = 1000, Height = 700 }; window.Show();
            var changed = false;
            model.PropertyChanged += (_, _) =>
            {
                if (changed || !model.IsBusy || !model.Status.StartsWith("Compiling:", StringComparison.Ordinal)) return;
                changed = true; doc.ReplaceWorkingText(A + "NTE|SYN-LATER\r");
            };
            try
            {
                Assert.False(view.OutputAttribution.IsVisible);
                await model.RunAsync(); Pump(window);
                Assert.True(changed); Assert.Empty(model.Diagnostics); Assert.StartsWith("Completed:", model.Status);
                var origin = Assert.IsType<ScriptReportOrigin>(model.OutputOrigin);
                Assert.True(origin.SourceChangedDuringRun); Assert.Equal(revision, origin.Input!.Revision);
                Assert.Equal(doc.ActivityNumber, origin.SourceDocumentNumber);
                Assert.Equal(doc.Id, origin.Input.DocumentId); Assert.Same(origin, model.ReportOrigin);
                Assert.Contains($"Captured source revision {revision}", view.OutputAttribution.Text);
                Assert.Contains($"document {doc.ActivityNumber}", view.OutputAttribution.Text);
                Assert.Contains("Stale source:", view.OutputAttribution.Text); Assert.True(view.OutputAttribution.IsVisible);
                Assert.Contains("Stale source:", model.Status); Assert.Contains("Stale source:", ToolTip.GetTip(view.Reports)!.ToString());
                var output = Assert.Single(manager.Documents, d => d != doc);
                Assert.Equal(A + "NTE|SYN-CAPTURED\r", output.EditableText);
                Assert.Equal(A + "NTE|SYN-LATER\r", doc.EditableText);
                var reports = new ScriptReportsWindow(model); reports.Show();
                Assert.Contains("Stale source:", reports.Origin.Text); reports.Close();

                manager.Activate(doc.Id); model.Session.Source = "void Main() {";
                await model.RunAsync(); Assert.NotEmpty(model.Diagnostics);
                Assert.Same(origin, model.OutputOrigin); Assert.Same(origin, model.ReportOrigin);
                window.Content = view = new ScriptEditorView(model); Pump(window);
                Assert.True(view.OutputAttribution.IsVisible); Assert.Contains("Stale source:", view.OutputAttribution.Text);
                Assert.Contains($"Captured source revision {revision}", view.OutputAttribution.Text);
                Assert.Contains("Stale source:", ToolTip.GetTip(view.Reports)!.ToString());
                await doc.EditRefresh;
            }
            finally
            {
                window.Close(); model.Dispose(); var owned = manager.Documents.ToArray(); manager.Dispose();
                await Task.WhenAll(owned.Select(d => d.Disposal));
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ScriptDiagnosticDisplaysCapturedLogicalPositionAndSeparateWorkerOrdinal(bool memoryOriginal)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager();
            var doc = memoryOriginal ? MessageFileReader.Open(manager, "Synthetic memory", Hl7Parser.Parse(B)) : await Open(manager);
            var target = doc.GetMessageId(memoryOriginal ? 1 : 2);
            doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
            doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
            await doc.NavigateAsync(target, Token); doc.IsEditMode = true; doc.ReplaceWorkingText(B + A);
            var directory = Path.Combine(Path.GetTempPath(), "hl7-script-identity-ui-" + Guid.NewGuid().ToString("N"));
            using var model = new ScriptEditorViewModel(manager, new ScriptLibrary(directory));
            model.Session.Source = "void Main() {}";
            var view = new ScriptEditorView(model); var window = new Window { Content = view, Width = 1000, Height = 800 }; window.Show();
            try
            {
                await model.RunAsync(); view.DiagnosticPanel.IsExpanded = true; Pump(window);
                var ordinal = memoryOriginal ? 1 : 2; var position = memoryOriginal ? 3 : 4;
                var diagnostic = Assert.Single(model.Diagnostics); Assert.Equal(ordinal, diagnostic.MessageNumber);
                Assert.Equal(target, model.DiagnosticInput!.Resolve(ordinal)!.MessageId);
                Assert.Equal(position, model.DiagnosticInput.Resolve(ordinal)!.LogicalPosition);
                var formatted = model.FormatDiagnostic(diagnostic);
                Assert.Contains($"captured message {position}", formatted); Assert.Contains($"job ordinal {ordinal}", formatted);
                Assert.Contains(memoryOriginal ? "no original file ordinal" : "original ordinal 2", formatted);
                Assert.Contains(view.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == formatted);
                Assert.Single(manager.Documents); Assert.Equal(B + A, doc.EditableText);
            }
            finally
            {
                window.Close(); manager.Dispose(); await doc.Disposal;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
            return true;
        }, Token);
    }

    [Fact]
    public async Task ComparisonPickerRetainsUnavailableIdentityThroughUndoRedoAndPositionRefresh()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); var doc = await Open(manager);
            var original = doc.GetMessageId(1); var inserted = doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
            using var model = new ComparisonViewModel(manager);
            model.SetSources(new(doc, inserted), new(doc, original));
            var comparison = new ComparisonView(model); var file = Assert.IsType<FileDocumentView>(MessageViewer.Create(doc));
            var grid = new Grid { RowDefinitions = new("*,*") }; Grid.SetRow(comparison, 1); grid.Children.Add(file); grid.Children.Add(comparison);
            var window = new Window { Content = grid, Width = 1200, Height = 1000 };
            DocumentHistoryShortcuts.Attach(window, () => manager.ActiveDocument); window.Show();
            try
            {
                model.Start(); await model.Pending; Pump(window); Assert.NotNull(model.Comparison);
                Assert.True(comparison.SourceA.SourceButton.Focus()); var focus = window.FocusManager!.GetFocusedElement();
                var discarded = file.ActiveView!;
                Key(window, Avalonia.Input.Key.Z); await doc.EditRefresh; Pump(window);
                Assert.Equal(inserted, model.InputA.MessageId); Assert.Equal(0, model.InputA.Ordinal);
                Assert.Contains("unavailable", ToolTip.GetTip(comparison.SourceA.SourceButton)!.ToString());
                Assert.False(discarded.IsAttachedToVisualTree()); Assert.Equal(B, file.ActiveView!.Editor.Text);
                Assert.Same(focus, window.FocusManager.GetFocusedElement());
                model.Start(); await model.Pending; Assert.Null(model.Comparison); Assert.Contains("unavailable", model.Status);
                Key(window, Avalonia.Input.Key.Y); await doc.EditRefresh; Pump(window);
                Assert.Equal(inserted, model.InputA.MessageId); Assert.Equal(3, model.InputA.Ordinal);
                model.Start(); await model.Pending; Assert.NotNull(model.Comparison);
                Assert.Equal(C, (await model.Comparison.WindowAsync(true, token: Token)).Text);
                model.CompareWithOriginal(doc); Assert.Contains("no Original Message", model.Status);
                model.Start(); var pending = model.Pending;
                doc.InsertMessage(MessageInsertionPosition.Beginning, A); await doc.EditRefresh; await pending; Pump(window);
                Assert.Equal(inserted, model.InputA.MessageId); Assert.Equal(4, model.InputA.Ordinal);
                Assert.Contains("message 4", ToolTip.GetTip(comparison.SourceA.SourceButton)!.ToString());
                Assert.Null(model.Comparison);
            }
            finally { window.Close(); manager.Dispose(); await doc.Disposal; }
            return true;
        }, Token);
    }

    [Fact]
    public async Task SearchToolPreservesAuthoredQueryAndNavigatorResolvesMatchingIdsAfterInsertion()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); var doc = await Open(manager); var target = doc.GetMessageId(2);
            doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
            using var search = new SearchViewModel(manager) { Value = "SYN-B" };
            var searchView = new SearchResultsView(search); var file = Assert.IsType<FileDocumentView>(MessageViewer.Create(doc));
            var grid = new Grid { RowDefinitions = new("*,*") }; Grid.SetRow(searchView, 1); grid.Children.Add(file); grid.Children.Add(searchView);
            var window = new Window { Content = grid, Width = 1100, Height = 900 }; window.Show();
            try
            {
                search.SearchCommand.Execute(null); await search.SearchingTask;
                var match = Assert.Single(search.Session.Page); Assert.Equal(target, match.MessageId);
                search.Session.FilterNavigator = true; Pump(window);
                Assert.Equal(new[] { target }, doc.NavigationMatchIds);
                await file.NavigateAsync(1); Assert.NotEqual(target, doc.ActiveMessageId);
                await file.NavigateAsync(3); Assert.Equal(target, doc.ActiveMessageId); Assert.Equal(B, file.ActiveView!.Editor.Text);
                Assert.Equal(3, file.FilteredMessages.SelectedItem);
                doc.InsertMessage(MessageInsertionPosition.Beginning, A); await doc.EditRefresh; Pump(window);
                Assert.Equal("SYN-B", search.Value); Assert.Equal("SYN-B", searchView.Value.Text);
                Assert.Null(search.Session.Results); Assert.Null(doc.NavigationMatchIds); Assert.False(file.FilteredMessages.IsVisible);
                search.SearchCommand.Execute(null); await search.SearchingTask;
                var shifted = Assert.Single(search.Session.Page); Assert.Equal(4, shifted.MessageNumber); Assert.Equal(target, shifted.MessageId);
                await search.Session.NavigateAsync(shifted, Token); Pump(window);
                Assert.Equal(target, doc.ActiveMessageId); Assert.Equal("4", file.MessageNumberInput.Text);
                search.SearchCommand.Execute(null); var pending = search.SearchingTask;
                doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh; await pending; Pump(window);
                Assert.Null(search.Session.Results); Assert.Null(doc.NavigationMatchIds); Assert.Equal("SYN-B", search.Value);
            }
            finally { window.Close(); manager.Dispose(); await doc.Disposal; }
            return true;
        }, Token);
    }
}
