using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Querying;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class SqlQueryToolTests : IAsyncDisposable
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20300101000000||ADT^A01|SYN-SAME|T|2.5.1\r";
    private const string Slow = "WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<1000000000) SELECT sum(x) FROM n";
    private readonly string root = Path.Combine(Path.GetTempPath(), "sql-ui-synthetic-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Type TestApplication => Environment.GetEnvironmentVariable("HL7WORKBENCH_SQL_RENDER_DIRECTORY") is null ? typeof(RawHoverTestApplication) : typeof(SqlQueryRenderTestApplication);
    private SqlQueryOptions Options => new() { WorkerPath = Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll"), StorageRoot = Path.Combine(root, "snapshots") };
    private SqlQueryLibrary Library => new(Path.Combine(root, "queries"));
    private static void Click(Button button)
    { if (button.Command is { } command) command.Execute(button.CommandParameter); else button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static async Task<MessageDocument> OpenDocument(DocumentManager documents, int count = 1, string patient = "SYN-ID")
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(string.Concat(Enumerable.Range(1, count).Select(n => Header + "PID|1||" + patient + "-" + n + "||Synthetic^Example\rPV1|1|I\r"))));
        return await MessageFileReader.OpenFileAsync(documents, "Synthetic.hl7", input, Token);
    }
    private SqlQueryViewModel Model(DocumentManager documents, SqlQueryHistory? history = null) => new(documents, Library, history, Options);
    private static Window Host(SqlQueryView view, int width = 1150)
    { var window = new Window { Content = view, Width = width, Height = 800 }; window.Show(); Pump(window); return window; }
    [Fact]
    public async Task MenuOpensAnOptionalSqlToolAndDefaultLayoutRemainsUnchanged()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(TestApplication);
        await ui.Dispatch(() =>
        {
            var window = new MainWindow(root); window.Show(); Pump(window);
            try
            {
                var factory = (WorkspaceDockFactory)typeof(MainWindow).GetField("factory", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
                var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.SqlQueryId)!;
                Assert.Null(tool.Context); Assert.Contains(tool, factory.Root.HiddenDockables!);
                window.FindControl<MenuItem>("SqlQueryMenu")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(window);
                Assert.IsType<SqlQueryViewModel>(tool.Context);
                Assert.Single(window.GetVisualDescendants().OfType<SqlQueryView>());
                Assert.DoesNotContain("SELECT", LayoutCodec.Encode(factory.Capture()), StringComparison.Ordinal);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(390)] [InlineData(1150)]
    public async Task EditorCtrlEnterRunsRealSqlWithBoundedPagesAndHighlighting(int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(TestApplication);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); await OpenDocument(documents, 130);
            using var model = Model(documents); await model.LibraryTask; var view = new SqlQueryView(model); var window = Host(view, width);
            try
            {
                Assert.NotNull(view.Editor.SyntaxHighlighting); Assert.False(view.Editor.IsReadOnly);
                view.Editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Control });
                Assert.True(model.IsBusy); Assert.True(view.Editor.IsReadOnly); Assert.True(view.Cancel.IsEnabled);
                await model.ActionTask; Pump(window);
                Assert.NotNull(model.Results); Assert.Equal(130, model.Results.RowCount); Assert.Equal(100, model.Page.Count);
                Assert.Contains("Complete", view.ResultStatus.Text!, StringComparison.Ordinal); Assert.Contains("130", view.ResultStatus.Text!, StringComparison.Ordinal);
                Assert.True(view.Next.IsEnabled); Assert.InRange(view.Editor.Bounds.Height, 50, 600);
                Click(view.Next); await view.ActionTask; Pump(window); Assert.Equal(30, model.Page.Count); Assert.Equal(1, model.PageIndex);
                Assert.Single(model.History); Assert.False(model.IsDirty); Assert.Empty(Directory.GetFiles(root, "*.json", SearchOption.AllDirectories));
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task CurrentAndSelectedDocumentScopesStayPinnedAcrossDocumentSwitches()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(TestApplication);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var first = await OpenDocument(documents, patient: "SYN-FIRST"); var second = await OpenDocument(documents, patient: "SYN-SECOND");
            using var model = Model(documents); var view = new SqlQueryView(model); var window = Host(view);
            try
            {
                model.Sql = "SELECT DocumentId,MessageId,HL7_VALUE(MessageId,'PID-3.1') FROM Messages";
                await model.RunAsync(); Assert.Equal("SYN-SECOND-1", model.Results!.ReadRow(0).Cells[2].Text);
                view.Scope.SelectedIndex = 1; foreach (var choice in model.ScopeDocuments) choice.Selected = true;
                model.Sql = "SELECT DocumentId,MessageId,HL7_VALUE(DocumentId,MessageId,'PID-3.1') FROM Messages ORDER BY DocumentId";
                var run = model.RunAsync(); documents.Activate(first.Id); await run; Pump(window);
                Assert.Equal(2, model.Results!.RowCount); Assert.True(view.DocumentsPanel.IsVisible);
                model.Sql += "\n-- draft change"; Assert.Contains("SQL draft differs", model.ResultStatus, StringComparison.Ordinal);
                Assert.Equal(new[] { "SYN-FIRST-1", "SYN-SECOND-1" }, Enumerable.Range(0, 2).Select(i => model.Results.ReadRow(i).Cells[2].Text).Order());
                documents.Close(second.Id, true); Pump(window); Assert.Single(model.ScopeDocuments); Assert.Contains("stale or closed", view.ResultStatus.Text!, StringComparison.Ordinal);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task SortingPagingCopyAndFullValueInspectionUseCompleteJournalValues()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(TestApplication);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); await OpenDocument(documents, 130);
            using var model = Model(documents); var view = new SqlQueryView(model); var window = Host(view);
            try
            {
                var longHeader = "Synthetic" + new string('\n', 1000);
                model.Sql = "SELECT 1 AS \"" + longHeader + "\" FROM Messages LIMIT 1"; await model.RunAsync(); Pump(window);
                Assert.Equal(longHeader, model.Results!.Columns[0]);
                var boundedHeader = view.GetVisualDescendants().OfType<Button>().Single(b => b.Content is string text && text.StartsWith("Synthetic↵", StringComparison.Ordinal));
                Assert.DoesNotContain('\n', (string)boundedHeader.Content!); Assert.InRange(boundedHeader.Bounds.Height, 20, 50);
                var full = "synthetic,\"quoted\"\nλ" + new string('V', 1000);
                model.Sql = "SELECT DocumentId,MessageId,MessageNumber,NULL AS N,'' AS E,'" + full + "' AS LongValue FROM Messages ORDER BY MessageNumber DESC";
                await model.RunAsync(); Pump(window); Assert.True(model.Page[0].Cells[5].Abbreviated);
                var header = view.GetVisualDescendants().OfType<Button>().Single(b => Equals(b.Content, "MessageNumber")); Click(header); await view.ActionTask;
                Assert.Equal("1", model.Page[0].Cells[2].Preview); await model.ChangePageAsync(1); Assert.Equal("101", model.Page[0].Cells[2].Preview);
                await model.ChangePageAsync(0); model.SelectCell(0, 5); Click(view.Inspect); await view.ActionTask; Assert.Equal(full, view.FullValue.Text);
                model.SelectCell(0, 3); model.SelectCell(1, 5, extend: true); Click(view.Copy); await view.ClipboardTask;
                var clipboard = await window.Clipboard!.TryGetTextAsync(); Assert.NotNull(clipboard);
                Assert.Contains("(SQL NULL)\t\"\"\t\"synthetic,\"\"quoted\"\"", clipboard, StringComparison.Ordinal); Assert.Contains(new string('V', 1000), clipboard, StringComparison.Ordinal);
                Assert.Equal("(SQL NULL)", SqlQueryView.Preview(new(QueryValueKind.Null, "", false)));
                Assert.Equal("(empty TEXT)", SqlQueryView.Preview(new(QueryValueKind.Text, "", false)));
                Assert.DoesNotContain("…", clipboard, StringComparison.Ordinal);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task NavigationIsDeliberateUsesAuthoritativeSortedRowAndRefusesStaleSources()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(TestApplication);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var first = await OpenDocument(documents, 3); var second = await OpenDocument(documents);
            using var model = Model(documents); model.SelectedScope = true; model.ScopeDocuments.Single(c => c.Document == first).Selected = true;
            model.Sql = "SELECT DocumentId,MessageId,MessageNumber FROM Messages ORDER BY MessageNumber DESC";
            var view = new SqlQueryView(model); var window = Host(view); var opened = new List<string>();
            model.OpenDocumentRequested += id => { documents.Activate(id); opened.Add(id); };
            try
            {
                await model.RunAsync(); Pump(window); model.SelectCell(0, 2); Assert.Empty(opened); Assert.Same(second, documents.ActiveDocument);
                Assert.True(view.Navigate.IsEnabled);
                var cell = view.Results.GetVisualDescendants().OfType<Border>().Single(b => AutomationProperties.GetAutomationId(b) == "sql-cell-1-2");
                var point = cell.TranslatePoint(new Point(10, 10), window)!.Value;
                window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
                window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
                await view.ActionTask;
                Assert.Equal(first.Id, Assert.Single(opened)); Assert.Equal(3, first.MessageNumber); Assert.False(first.IsDirty);
                first.IsEditMode = true; first.ReplaceWorkingText(Header + "PID|1||SYN-EDIT\r"); await first.EditRefresh; Pump(window);
                Assert.False(model.CanNavigate); Assert.False(view.Navigate.IsEnabled); Assert.Contains("stale or closed", view.ResultStatus.Text!, StringComparison.Ordinal);
                Assert.True(view.Extract.IsEnabled);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task CsvAndMatchingActionsUseAllPagesAndPreserveOriginalDocuments()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(TestApplication);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var original = await OpenDocument(documents, 130); var before = original.Snapshot!.ReadBytes(new(0, original.Snapshot.Length), int.MaxValue);
            using var model = Model(documents); var view = new SqlQueryView(model); var window = Host(view);
            model.ChooseExportAsync = () => Task.FromResult<FileSaveDestination?>(FileSaveDestination.NewFile(Path.Combine(root, "all-results.csv")));
            try
            {
                await model.RunAsync(); await model.SortAsync(2); model.SelectCell(0, 0);
                Click(view.Export); await view.ActionTask; Assert.Equal(131, (await File.ReadAllLinesAsync(Path.Combine(root, "all-results.csv"), Token)).Length);
                Click(view.Extract); await view.ActionTask; var output = Assert.Single(documents.Documents, d => d != original);
                Assert.Equal(130, output.MessageCount); Assert.True(output.IsDirty); Assert.Null(output.SavePath);
                Assert.Equal(before, output.Snapshot!.ReadBytes(new(0, output.Snapshot.Length), int.MaxValue)); Assert.False(original.IsDirty);
                Assert.Equal(before, original.Snapshot.ReadBytes(new(0, original.Snapshot.Length), int.MaxValue));
                model.Sql = "SELECT count(*) FROM Messages"; await model.RunAsync(); Pump(window);
                Assert.False(view.Extract.IsEnabled); Assert.False(view.Navigate.IsEnabled);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancelAndDocumentClosureReapQueryAndReleaseSnapshotStorage(bool closeSource)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(TestApplication);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var source = await OpenDocument(documents);
            using var model = Model(documents); var view = new SqlQueryView(model); var window = Host(view);
            try
            {
                model.Sql = Slow; var run = model.RunAsync(); await Task.Delay(150, Token);
                if (closeSource) documents.Close(source.Id, true); else Click(view.Cancel);
                await run.WaitAsync(TimeSpan.FromSeconds(10), Token); Pump(window);
                Assert.False(model.IsBusy); Assert.Null(model.Results); Assert.Empty(model.Page); Assert.False(view.Extract.IsEnabled);
                Assert.Contains("canceled", model.Status, StringComparison.OrdinalIgnoreCase);
                if (Directory.Exists(Options.StorageRoot)) Assert.Empty(Directory.EnumerateFiles(Options.StorageRoot!, "*", SearchOption.AllDirectories));
                Assert.Equal("Canceled", Assert.Single(model.History).Outcome);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task SqlErrorsClearOldResultsAndNeverLeakLiteralsOrFilenamesIntoStatusOrLogs()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(TestApplication);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); await OpenDocument(documents);
            using var model = Model(documents); var view = new SqlQueryView(model); var window = Host(view);
            try
            {
                await model.RunAsync(); model.Sql = "SELECT missing_column || 'SYN-PRIVATE-LITERAL' FROM Messages"; await model.RunAsync(); Pump(window);
                Assert.Null(model.Results); Assert.Empty(model.Page); Assert.Contains("could not be prepared", model.Status, StringComparison.Ordinal);
                Assert.DoesNotContain("SYN-PRIVATE-LITERAL", model.Status, StringComparison.Ordinal);
                Assert.DoesNotContain("SYN-PRIVATE-LITERAL", string.Join("", documents.Activity.Snapshot()), StringComparison.Ordinal);
                Assert.DoesNotContain("Synthetic.hl7", string.Join("", documents.Activity.Snapshot()), StringComparison.Ordinal);
                Assert.False(view.Export.IsEnabled); Assert.Equal(2, model.History.Count);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task SavedQueryActionsPromptForDraftChangesAndRenameDoesNotSaveSql()
    {
        using var documents = new DocumentManager(); using var model = Model(documents); await model.LibraryTask;
        Assert.False(Directory.Exists(Path.Combine(root, "queries")));
        model.Name = "Synthetic saved"; model.Sql = "SELECT 'SYN-EXPLICIT-SAVE'"; await model.SaveAsync(); Assert.False(model.IsDirty);
        var id = model.SavedId!.Value; model.Sql = "SELECT 'SYN-UNSAVED'"; model.Name = "Synthetic renamed"; await model.RenameAsync();
        Assert.True(model.IsDirty); Assert.Equal("SELECT 'SYN-EXPLICIT-SAVE'", Library.Open(id).Sql);
        model.PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Cancel); await model.NewAsync(); Assert.Equal(id, model.SavedId);
        model.PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Save); await model.NewAsync(); Assert.Null(model.SavedId); Assert.False(model.IsDirty);
        await model.OpenAsync(id); Assert.Equal("SELECT 'SYN-UNSAVED'", model.Sql); Assert.Equal("Synthetic renamed", model.Name);
        model.ConfirmDeleteAsync = _ => Task.FromResult(false); await model.DeleteAsync(); Assert.Single(Library.List().Items);
        model.Sql = "SELECT 3"; model.ConfirmDeleteAsync = _ => Task.FromResult(true); await model.DeleteAsync(); Assert.Empty(Library.List().Items);
        Assert.Null(model.SavedId); Assert.Equal("SELECT 3", model.Sql);
    }
    [Fact]
    public async Task NamedQueryToolbarKeepsSavedSelectionAndDraftActionsConsistent()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(TestApplication);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); using var model = Model(documents); await model.LibraryTask;
            var view = new SqlQueryView(model); var window = Host(view);
            model.PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Discard);
            try
            {
                view.QueryName.Text = "Synthetic first"; view.Editor.Text = "SELECT 1"; Click(view.Save); await view.ActionTask;
                var first = model.SavedId; Assert.Equal(first, Assert.IsType<SqlQueryLibraryItem>(view.Library.SelectedItem).Id);
                Click(view.New); await view.ActionTask; Assert.Null(view.Library.SelectedItem);
                view.QueryName.Text = "Synthetic second"; view.Editor.Text = "SELECT 2"; Click(view.Save); await view.ActionTask;
                var second = model.SavedId; Assert.NotEqual(first, second); Assert.Equal(second, Assert.IsType<SqlQueryLibraryItem>(view.Library.SelectedItem).Id);
                view.Library.SelectedItem = model.SavedQueries.Single(i => i.Id == first); Click(view.Open); await view.ActionTask;
                Assert.Equal("SELECT 1", view.Editor.Text); Assert.Equal(first, model.SavedId);
                view.Editor.Text = "SELECT 3"; view.QueryName.Text = "Synthetic renamed"; Click(view.Rename); await view.ActionTask;
                Assert.Equal("SELECT 1", Library.Open(first!.Value).Sql); Assert.True(model.IsDirty);
                Click(view.Save); await view.ActionTask; Assert.Equal("SELECT 3", Library.Open(first.Value).Sql);
                model.ConfirmDeleteAsync = _ => Task.FromResult(true); Click(view.Delete); await view.ActionTask;
                Assert.Null(model.SavedId); Assert.Null(view.Library.SelectedItem); Assert.Equal(second, Assert.Single(Library.List().Items).Id);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task CloseConsentIsVersionGuardedAndLaterShutdownCancellationKeepsDraft()
    {
        using var documents = new DocumentManager(); using var model = Model(documents); await model.LibraryTask; model.Sql = "SELECT 2";
        model.PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Discard);
        Assert.True(await model.ConfirmCloseAsync(discardImmediately: false)); Assert.True(model.IsDirty); Assert.Equal("SELECT 2", model.Sql);
        model.PromptUnsavedAsync = () => { model.Sql = "SELECT 3"; return Task.FromResult(UnsavedChangesDecision.Discard); };
        Assert.False(await model.ConfirmCloseAsync()); Assert.True(model.IsDirty);
        model.PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Save); Assert.False(await model.ConfirmCloseAsync()); Assert.True(model.IsDirty);
        model.Name = "Synthetic conflict"; await model.SaveAsync(); var stored = Library.Open(model.SavedId!.Value);
        await Library.UpdateAsync(stored, stored.Name, "SELECT 'SYN-EXTERNAL'", Token); model.Sql = "SELECT 'SYN-RETAIN-DRAFT'";
        Assert.False(await model.ConfirmCloseAsync()); Assert.True(model.IsDirty); Assert.Equal("SELECT 'SYN-RETAIN-DRAFT'", model.Sql);
        Assert.Contains("Close canceled", model.LibraryStatus, StringComparison.Ordinal); Assert.Equal("SELECT 'SYN-EXTERNAL'", Library.Open(stored.Id).Sql);
    }
    [Fact]
    public async Task OversizedClipboardIsRejectedBeforeEditorMutationAndNewDraftDoesNotRelabelOldResults()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(TestApplication);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); await OpenDocument(documents);
            using var model = Model(documents); var view = new SqlQueryView(model); var window = Host(view);
            try
            {
                await model.RunAsync(); var original = model.Sql; var result = model.Results;
                var clipboard = window.Clipboard!;
                await clipboard.SetTextAsync(new string('S', QueryLimits.MaximumSqlCharacters + 1));
                // Paste is handled at TextArea, the routed keyboard input root.
                view.Editor.TextArea.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.V, KeyModifiers = KeyModifiers.Control });
                await view.ClipboardTask; Assert.Equal(original, model.Sql); Assert.Equal(original, view.Editor.Text);
                view.Editor.Select(0, view.Editor.Document.TextLength); await clipboard.SetTextAsync("SELECT 'SYN-NEW-DRAFT'");
                view.Editor.TextArea.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.V, KeyModifiers = KeyModifiers.Control });
                await view.ClipboardTask; Pump(window);
                Assert.Equal("SELECT 'SYN-NEW-DRAFT'", model.Sql); Assert.Same(result, model.Results);
                Assert.Contains("SQL draft differs", view.ResultStatus.Text!, StringComparison.Ordinal); Assert.True(model.IsDirty);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task DockCloseReopenAndFloatingPersistOnlyPlacementAndRetainBoundedSessionHistory()
    {
        using var documents = new DocumentManager(); await OpenDocument(documents); var tools = new ToolWindowRegistry(); var history = new SqlQueryHistory();
        tools.Register(new(WorkspaceDockFactory.DiagnosticsId, "Activity Log", () => new object()));
        tools.Register(new(WorkspaceDockFactory.SqlQueryId, "SQL Query", () => Model(documents, history), Optional: true, ReleaseOnClose: true, RestoreVisibility: true));
        using var factory = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        factory.ConfirmSqlCloseAsync = () => ((SqlQueryViewModel)tools.GetContent(WorkspaceDockFactory.SqlQueryId)).ConfirmCloseAsync();
        factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.SqlQueryId); var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.SqlQueryId)!;
        var model = Assert.IsType<SqlQueryViewModel>(tool.Context); await model.LibraryTask;
        model.Sql = "SELECT 'SYN-SESSION-ONLY'"; await model.RunAsync();
        model.PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Cancel); factory.CloseDockable(tool); Assert.True(tools.IsCreated(tool.Id!));
        factory.FloatDockable(tool); var floating = Assert.Single(factory.Root.Windows!); floating.Host!.SetPosition(200, 180); floating.Host.SetSize(1000, 750);
        var json = LayoutCodec.Encode(factory.Capture()); Assert.DoesNotContain("SYN-SESSION-ONLY", json, StringComparison.Ordinal); Assert.DoesNotContain("SELECT", json, StringComparison.Ordinal);
        model.PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Discard); factory.CloseDockable(tool); Assert.False(tools.IsCreated(tool.Id!)); Assert.Null(model.Results);
        factory.ShowTool(tool.Id!); var reopened = Assert.IsType<SqlQueryViewModel>(tool.Context); Assert.NotSame(model, reopened); Assert.Equal(SqlQueryViewModel.DefaultSql, reopened.Sql);
        Assert.Single(reopened.History); factory.CloseFloatingWindows(); factory.Restore(LayoutCodec.Decode(json), [new(0, 0, 1920, 1080)]);
        Assert.IsType<SqlQueryViewModel>(factory.GetDockable<ITool>(tool.Id!)!.Context); // Visible restore starts a clean session, never restores SQL/results.
    }
    [Fact]
    public async Task ToolClosureDrainsAnActiveOperationBeforeReleasingItsModel()
    {
        using var documents = new DocumentManager(); await OpenDocument(documents);
        var history = new SqlQueryHistory(); using var model = Model(documents, history); await model.LibraryTask;
        model.Sql = Slow; model.PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Discard);
        var run = model.RunAsync(); Assert.True(model.IsBusy);
        Assert.True(await model.ConfirmCloseAsync().WaitAsync(TimeSpan.FromSeconds(10), Token));
        model.Dispose(); await run; Assert.False(model.IsBusy); Assert.Null(model.Results);
        Assert.Equal("Canceled", Assert.Single(history.Entries).Outcome);
        if (Directory.Exists(Options.StorageRoot)) Assert.Empty(Directory.EnumerateFiles(Options.StorageRoot!, "*", SearchOption.AllDirectories));
    }
    [Fact]
    public async Task HistoryAndClipboardHaveIndependentCapsAndFailedOpenRetainsDirtyDraft()
    {
        var history = new SqlQueryHistory();
        for (var n = 0; n < 40; n++) history.Add(new string('S', 16384), "Completed", n);
        Assert.InRange(history.Entries.Count, 1, 32); Assert.True(history.Entries.Sum(e => e.Sql.Length) <= 256 * 1024);
        Assert.DoesNotContain(new string('S', 50), history.Entries[0].ToString(), StringComparison.Ordinal);
        using var documents = new DocumentManager(); await OpenDocument(documents, 100);
        using var model = Model(documents, history); await model.LibraryTask;
        model.Sql = "SELECT '" + new string('S', 16000) + "' FROM Messages"; await model.RunAsync();
        model.SelectCell(0, 0); model.SelectCell(99, 0, extend: true);
        Assert.Null(model.CopySelectionText()); Assert.Contains("clipboard limit", model.Status, StringComparison.Ordinal);
        model.PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Discard);
        var before = model.Sql; await model.OpenAsync(Guid.NewGuid()); Assert.True(model.IsDirty); Assert.Equal(before, model.Sql);
        history.Clear(); Assert.Empty(history.Entries);
    }
    [Theory]
    [InlineData(false, 390)] [InlineData(true, 390)] [InlineData(false, 1200)] [InlineData(true, 1200)]
    public async Task CompactAndWideLayoutsRemainUsableInBothThemes(bool dark, int width)
    {
        var renderDirectory = Environment.GetEnvironmentVariable("HL7WORKBENCH_SQL_RENDER_DIRECTORY");
        await using var ui = HeadlessUnitTestSession.StartNew(TestApplication);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); await OpenDocument(documents, 130);
            using var model = Model(documents); await model.LibraryTask;
            model.Sql = "SELECT MessageNumber,MessageType,HL7_VALUE(MessageId,'PID-3.1') AS PatientId,HL7_VALUE(MessageId,'PV1-2') AS Class,NULL AS Missing,'' AS Empty FROM Messages ORDER BY MessageNumber";
            var view = new SqlQueryView(model); var window = new Window { Content = view, Width = width, Height = 800, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            window.Show();
            try
            {
                await model.RunAsync(); Pump(window); Assert.NotNull(model.Results);
                Assert.True(view.Editor.Bounds.Height > 50); Assert.True(view.Results.Bounds.Height > 80);
                Assert.InRange(view.Results.GetVisualDescendants().OfType<ListBoxItem>().Count(), 1, 99);
                Assert.Equal(100, model.Page.Count); Assert.True(view.Export.IsEffectivelyVisible);
                if (renderDirectory is not null)
                {
                    Directory.CreateDirectory(renderDirectory); await Task.Delay(60, Token); Pump(window);
                    using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
                    frame.Save(Path.Combine(renderDirectory, $"sql-{width}-{(dark ? "dark" : "light")}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                    Assert.True(new FileInfo(Path.Combine(renderDirectory, $"sql-{width}-{(dark ? "dark" : "light")}.png")).Length > 5000, "Rendered screenshot must contain visible controls, not a blank frame.");
                }
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    public async ValueTask DisposeAsync()
    { await Task.Yield(); if (Directory.Exists(root)) Directory.Delete(root, true); }
}

public sealed class SqlQueryRenderTestApplication : App
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<SqlQueryRenderTestApplication>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
