using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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

public sealed class FieldExtractionViewTests
{
    private const string Text = "MSH|^~\\&|SYN|TEST|DEST|TEST|20300101120000||ADT^A01|SYN-CONTROL|T|2.5.1\rPID|1||A~B||Example^Ada\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-extraction-ui-" + Guid.NewGuid().ToString("N"));
        public ExtractionConfigurationSession Draft => new(new ExtractionLibrary(Path.Combine(Root, "Extractions")));
        public ExtractionOptions Options => new() { StorageRoot = Path.Combine(Root, "results") };
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static async Task<MessageDocument> Open(DocumentManager documents, string text = Text)
    { using var input = new MemoryStream(Encoding.UTF8.GetBytes(text)); return await MessageFileReader.OpenFileAsync(documents, "Synthetic.hl7", input, Token); }
    [Fact]
    public async Task OverviewRunPagingFilterSortExportAndEnterNavigationUseTheSameCapturedResults()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager();
            var source = await Open(documents, string.Concat(Enumerable.Repeat(Text, 205)));
            using var model = new FieldExtractionViewModel(documents, files.Draft, files.Options);
            var view = new FieldExtractionView(model); var window = new Window { Content = view, Width = 1100, Height = 780 }; window.Show();
            try
            {
                Pump(window); Assert.Same(source, view.Source.SelectedItem); Assert.True(view.Run.IsEnabled);
                Click(view.Run); await view.ActionTask; Pump(window);
                Assert.Equal(205, model.Session.View!.Count); Assert.Equal(100, view.Results.ItemCount); Assert.Contains("revision", view.SourceStatus.Text!);
                Assert.True(view.Next.IsEnabled); Click(view.Next); await view.ActionTask; Assert.Equal(1, model.Session.PageIndex);
                var row = model.Session.Page[0]; view.Results.SelectedItem = row;
                view.Results.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); await view.NavigationTask;
                Assert.Equal(101, source.MessageNumber); Assert.False(source.IsDirty);
                view.FiltersPanel.IsExpanded = true; Pump(window); view.FilterColumn.SelectedIndex = 2;
                view.FilterOperator.SelectedItem = ExtractionFilterOperator.Equals; view.FilterLiteral.Text = "A"; view.SortColumn.SelectedIndex = 3;
                Click(view.Apply); await view.ActionTask; Pump(window); Assert.Equal(205, model.Session.View.Count);
                model.ChooseExportAsync = () => Task.FromResult<FileSaveDestination?>(FileSaveDestination.NewFile(Path.Combine(files.Root, "all.csv")));
                Click(view.Export); await view.ActionTask;
                Assert.Equal(206, File.ReadLines(Path.Combine(files.Root, "all.csv")).Count()); Assert.False(source.IsDirty);
                Assert.Contains("205", view.Summary.Text!);
                Assert.True(view.Results.GetVisualDescendants().OfType<ListBoxItem>().Count() < 100);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task BuiltInDuplicationColumnEditingAndConfigurationReconstructionPreserveUserOwnership()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); await Open(documents);
            var draft = files.Draft; using var model = new FieldExtractionViewModel(documents, draft, files.Options);
            var view = new FieldExtractionView(model); var window = new Window { Content = view, Width = 900, Height = 750 }; window.Show();
            try
            {
                Assert.True(view.ConfigurationName.IsReadOnly); Click(view.Duplicate); await view.ActionTask;
                view.ConfigurationName.Text = "My configured extraction"; view.ColumnsPanel.IsExpanded = true; Pump(window);
                Assert.False(view.ConfigurationName.IsReadOnly); Click(view.AddColumn); Assert.Equal(7, draft.Columns.Count);
                var paths = view.GetVisualDescendants().OfType<TextBox>().Where(t => (Avalonia.Automation.AutomationProperties.GetName(t) ?? "").StartsWith("Extraction path ", StringComparison.Ordinal)).ToArray();
                Assert.Equal(7, paths.Length); paths[^1].Text = "PID-3.1.1";
                view.Mode.SelectedIndex = 1; Click(view.Save); await view.ActionTask; Assert.False(draft.IsDirty);
                var id = draft.Id; window.Content = null; var next = new FieldExtractionView(model); window.Content = next; Pump(window);
                Assert.Equal("My configured extraction", next.ConfigurationName.Text); Assert.Equal("PID-3.1.1", draft.Columns[^1].Path);
                Assert.Equal(ExtractionMode.ExpandMatches, draft.Mode); Assert.False(draft.IsBuiltIn);
                Assert.True(await draft.OpenAsync(ExtractionLibrary.OverviewId, () => Task.FromResult(UnsavedChangesDecision.Cancel)));
                Assert.Equal(6, draft.Columns.Count); Assert.True(draft.IsBuiltIn);
                Assert.True(await draft.OpenAsync(id, () => Task.FromResult(UnsavedChangesDecision.Cancel))); Assert.Equal(7, draft.Columns.Count);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false, 380)] [InlineData(true, 1100)]
    public async Task ThemeAndNarrowOrWideLayoutsKeepResultsAndKeyboardInputsUsable(bool dark, int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); await Open(documents);
            using var model = new FieldExtractionViewModel(documents, files.Draft, files.Options);
            var view = new FieldExtractionView(model); var window = new Window { Content = view, Width = width, Height = 780, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Click(view.Run); await view.ActionTask; Pump(window);
                Assert.Equal(window.ActualThemeVariant, view.Results.ActualThemeVariant); Assert.True(view.Results.Bounds.Height > 80);
                Assert.True(view.Source.Bounds.Width > 150); Assert.NotNull(view.FilterLiteral.Foreground);
                view.FiltersPanel.IsExpanded = true; Pump(window); view.FilterLiteral.Focus();
                view.FilterLiteral.Text = "SYN-MISSING"; view.FilterColumn.SelectedIndex = 1; view.FilterOperator.SelectedItem = ExtractionFilterOperator.Equals;
                view.FilterLiteral.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter }); await view.ActionTask;
                Assert.Equal(0, model.Session.View!.Count); Assert.False(documents.ActiveDocument!.IsDirty);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task ViewMenuAndDocumentActionShareToolAndCloseReleasesResultsButRetainsDraftOnly()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); var window = new MainWindow(files.Root); window.Show();
            try
            {
                var menu = window.FindControl<MenuItem>("FieldExtractionMenu")!; menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(window);
                var dock = window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!;
                var factory = Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory); var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.FieldExtractionId)!;
                var first = Assert.IsType<FieldExtractionViewModel>(tool.Context);
                Assert.False(first.Session.IsBusy); Assert.Null(first.Session.Source);
                first.Draft.Duplicate(); first.Draft.Name = "Retained selector draft";
                var layout = LayoutCodec.Encode(factory.Capture()); Assert.DoesNotContain("Retained selector draft", layout);
                factory.CloseDockable(tool); Assert.Null(first.Session.Results); Assert.Null(first.Session.Source);
                menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(window);
                var second = Assert.IsType<FieldExtractionViewModel>(tool.Context); Assert.NotSame(first, second); Assert.Same(first.Draft, second.Draft);
                Assert.Equal("Retained selector draft", second.Draft.Name); Assert.Null(second.Session.Results);
                Assert.True(await second.Draft.OpenAsync(ExtractionLibrary.OverviewId, () => Task.FromResult(UnsavedChangesDecision.Discard)));
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task ExplicitDocumentRequestTargetsThatDocumentAndStaleOrClosedResultsRemainClearlyLabeled()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); var source = await Open(documents);
            var other = await Open(documents); var draft = files.Draft; using var model = new FieldExtractionViewModel(documents, draft, files.Options);
            var view = new FieldExtractionView(model); var host = new Grid(); var documentView = new FileDocumentView(source); host.Children.Add(documentView);
            var window = new Window { Content = host, Width = 900, Height = 700 }; window.Show();
            window.AddHandler(ViewerRequests.FieldExtractionEvent, (_, e) => { model.Session.SetSource(e.Document); e.Handled = true; });
            try
            {
                Click(documentView.ExtractFields); Assert.Same(source, model.Session.Source); Assert.Same(other, documents.ActiveDocument);
                window.Content = view; Click(view.Run); await view.ActionTask; Pump(window);
                source.IsEditMode = true; source.ReplaceWorkingText(source.EditableText + "NTE|1||SYN-LATER\r"); await source.EditRefresh; Pump(window);
                Assert.Contains("Stale", view.SourceStatus.Text!); Assert.Equal("SYN-CONTROL", model.Session.Page[0].Values[1].Text);
                Assert.True(documents.Close(source.Id, discardChanges: true)); await source.Disposal; Pump(window);
                Assert.Contains("closed", view.SourceStatus.Text!); Assert.False(view.Navigate.IsEnabled); Assert.True(view.Export.IsEnabled);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task DockedAndFloatingPlacementRestoresWithoutRetainingResultOrFilterData(bool floating)
    {
        using var files = new Files(); using var documents = new DocumentManager(); var source = await Open(documents);
        var draft = files.Draft; var tools = new ToolWindowRegistry();
        tools.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
        tools.Register(new(WorkspaceDockFactory.FieldExtractionId, "Field Extraction", () => new FieldExtractionViewModel(documents, draft, files.Options), true, true));
        using var factory = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.FieldExtractionId);
        var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.FieldExtractionId)!; var first = Assert.IsType<FieldExtractionViewModel>(tool.Context);
        await first.RunAsync(); Assert.NotNull(first.Session.Results); first.FilterLiteral = "SYN-SECRET-FILTER";
        first.Draft.Duplicate(); first.Draft.Name = "Synthetic retained configuration";
        if (floating) factory.FloatDockable(tool);
        ((IToolDock)tool.Owner!).Proportion = .31;
        var layout = factory.Capture(); var json = LayoutCodec.Encode(layout);
        Assert.DoesNotContain("SYN-SECRET-FILTER", json); Assert.DoesNotContain("SYN-CONTROL", json); Assert.DoesNotContain("Synthetic retained configuration", json);
        factory.CloseDockable(tool); Assert.Null(first.Session.Results); Assert.False(source.IsDirty);
        factory.Restore(LayoutCodec.Decode(json), []); factory.ShowTool(WorkspaceDockFactory.FieldExtractionId);
        var reopened = factory.GetDockable<ITool>(WorkspaceDockFactory.FieldExtractionId)!; var next = Assert.IsType<FieldExtractionViewModel>(reopened.Context);
        Assert.NotSame(first, next); Assert.Same(draft, next.Draft); Assert.Null(next.Session.Results); Assert.Equal("", next.FilterLiteral);
        var placement = Assert.Single(factory.Capture().ToolPlacements!, p => p.Id == WorkspaceDockFactory.FieldExtractionId);
        Assert.Equal(floating, placement.Floating is not null);
        if (floating)
        {
            // A single floating root fills its native window; split proportion is
            // meaningful only when docked. Its window bounds are the placement contract.
            var expected = Assert.Single(layout.ToolPlacements!, p => p.Id == WorkspaceDockFactory.FieldExtractionId).Floating;
            Assert.Equal(expected, placement.Floating);
        }
        else Assert.Equal(.31, placement.Proportion);
        factory.CloseFloatingWindows();
    }
}
