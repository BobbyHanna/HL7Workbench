using System.Reflection;
using System.Text;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class DocumentationViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-documentation-" + Guid.NewGuid().ToString("N"));
        public void Dispose() { try { Directory.Delete(Root, true); } catch (DirectoryNotFoundException) { } }
    }
    [Theory]
    [InlineData(240, false)] [InlineData(300, true)]
    public async Task ShortNarrowHelpKeepsResultsInsideItsViewportAboveStatus(int height, bool dark)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var model = new DocumentationViewModel(); var view = new DocumentationView(model);
            var window = new Window { Content = view, Width = 320, Height = height, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Pump(window);
                var bottom = view.Topics.TranslatePoint(new Point(0, view.Topics.Bounds.Height), view)!.Value.Y;
                Assert.True(bottom <= view.Status.Bounds.Top, $"Results end {bottom} must precede status {view.Status.Bounds.Top}.");
                Assert.True(view.Topics.Bounds.Height > 0); Assert.True(view.Headings.Bounds.Width <= view.Bounds.Width);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(360, false, 1)] [InlineData(1000, true, 1)]
    [InlineData(360, true, 1.5)] [InlineData(1200, false, 2)]
    public async Task NativeContentsSearchTablesAndCodeReflowAcrossThemesAndScales(int width, bool dark, double scale)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var model = new DocumentationViewModel(); var view = new DocumentationView(model);
            var host = new LayoutTransformControl { LayoutTransform = new ScaleTransform(scale, scale), Child = view };
            var window = new Window { Content = host, Width = width, Height = 800, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Pump(window); model.NotifyShown(); Pump(window);
                Assert.True(view.Search.IsFocused); Assert.Equal("Search Documentation", AutomationProperties.GetName(view.Search));
                Assert.True(view.Search.Bounds.Width > 80); Assert.True(view.Topics.Bounds.Height > 70);
                view.Search.Text = "unfindable-help-term"; Assert.Empty(model.Results); Assert.Contains("No documentation", view.Status.Text!);
                Click(view.Contents); model.Navigate("query-with-sql"); Pump(window);
                Assert.NotEmpty(view.GetVisualDescendants().OfType<SelectableTextBlock>());
                var code = view.GetVisualDescendants().OfType<TextBox>().First(b => b.IsReadOnly && b.Text!.Contains("SELECT DocumentId", StringComparison.Ordinal));
                Assert.True(code.Bounds.Width > 60); Assert.Contains("MessageControlId", code.Text!);
                var copy = view.GetVisualDescendants().OfType<Button>().First(b => Equals(b.Content, "Copy code"));
                Click(copy); await view.ClipboardTask; Assert.Equal(code.Text, await window.Clipboard!.TryGetTextAsync());
                model.Navigate("privacy-and-storage", "storage-and-retention"); Pump(window);
                Assert.Contains(view.GetVisualDescendants().OfType<Grid>(), g => AutomationProperties.GetName(g) == "Documentation table");
                Assert.True(view.Headings.Bounds.Width <= view.Bounds.Width + 1); Assert.True(view.Status.Bounds.Width <= view.Bounds.Width + 1);
                var original = view.Search.ActualThemeVariant; window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark; Pump(window);
                Assert.NotEqual(original, view.Search.ActualThemeVariant);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task MenuWithoutDocumentLinksToExistingApiAndCloseReopenRestoreResetKeepPlacementOnly()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var files = new Files(); var window = new MainWindow(files.Root); window.Show(); var contentHost = new Window();
            try
            {
                var dock = window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!;
                var factory = Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory);
                var menu = window.FindControl<MenuItem>("DocumentationMenu")!; Assert.True(menu.IsEnabled);
                menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(window);
                var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DocumentationId)!;
                var model = Assert.IsType<DocumentationViewModel>(tool.Context); Assert.Equal("contents", model.Topic.Id);
                model.Navigate("run-and-manage-scripts"); Pump(window);
                contentHost.Content = new DockContentTemplate().Build(tool); contentHost.Show(); Pump(contentHost);
                var view = contentHost.GetVisualDescendants().OfType<DocumentationView>().First();
                Click(view.GetVisualDescendants().OfType<Button>().First(b => AutomationProperties.GetName(b)?.EndsWith("(Script API)", StringComparison.Ordinal) == true)); Pump(window);
                Assert.IsType<ScriptApiReferenceViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.ScriptApiReferenceId)!.Context);
                factory.ShowTool(WorkspaceDockFactory.DocumentationId); factory.FloatDockable(tool); Pump(window);
                Assert.NotEmpty(factory.Root.Windows!); model.Query = "seed"; model.Navigate("generate-synthetic-hl7");
                var layout = factory.Capture(); var json = LayoutCodec.Encode(layout);
                Assert.Contains(WorkspaceDockFactory.DocumentationId, json); Assert.DoesNotContain("seed", json); Assert.DoesNotContain("generate-synthetic-hl7", json);
                dock.Layout = factory.Restore(layout, []); factory.ShowTool(WorkspaceDockFactory.DocumentationId); Pump(window);
                var restored = Assert.IsType<DocumentationViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.DocumentationId)!.Context);
                Assert.NotSame(model, restored); Assert.Empty(restored.Query); Assert.Equal("contents", restored.Topic.Id);
                tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DocumentationId)!; factory.CloseDockable(tool); Pump(window);
                Assert.Null(tool.Context); factory.ShowTool(WorkspaceDockFactory.DocumentationId); Pump(window);
                Assert.NotSame(restored, Assert.IsType<DocumentationViewModel>(tool.Context));
                dock.Layout = factory.CreateLayout(); factory.InitLayout(dock.Layout); factory.ShowTool(WorkspaceDockFactory.DocumentationId); Pump(window);
                Assert.Empty(Assert.IsType<DocumentationViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.DocumentationId)!.Context).Query);
                Assert.False(Directory.Exists(Path.Combine(files.Root, "Scripts")));
            }
            finally { contentHost.Close(); window.Close(); }
            // A fresh application model cannot restore search/navigation from metadata.
            var restarted = new MainWindow(files.Root); restarted.Show();
            try
            {
                restarted.FindControl<MenuItem>("DocumentationMenu")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(restarted);
                var factory = Assert.IsType<WorkspaceDockFactory>(restarted.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!.Layout!.Factory);
                Assert.Empty(Assert.IsType<DocumentationViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.DocumentationId)!.Context).Query);
            }
            finally { restarted.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task HelpDoesNotMutateDocumentsOrCreateExecutionNetworkOrQueryToolsAndDetachedViewsUnsubscribe()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); var window = new MainWindow(files.Root); window.Show(); MessageDocument? document = null;
            var secondary = new Window();
            try
            {
                var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var manager = (DocumentManager)typeof(MainWindow).GetField("documents", flags)!.GetValue(window)!;
                var registry = (ToolWindowRegistry)typeof(MainWindow).GetField("registry", flags)!.GetValue(window)!;
                var bytes = Encoding.UTF8.GetBytes("MSH|^~\\&|SYN|||||20300101000000||ADT^A01|SYN-HELP|T|2.5.1\rPID|1||SYN-1\r");
                using var input = new MemoryStream(bytes); document = await MessageFileReader.OpenFileAsync(manager, "Synthetic Help", input, Token);
                var revision = document.SearchRevision; var content = document.ContentVersion; var dirty = document.IsDirty;
                var text = document.EditableText; var position = document.MessageNumber;
                window.FindControl<MenuItem>("DocumentationMenu")!.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(window);
                var factory = Assert.IsType<WorkspaceDockFactory>(window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!.Layout!.Factory);
                var model = Assert.IsType<DocumentationViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.DocumentationId)!.Context);
                var old = new DocumentationView(model); secondary.Content = old; secondary.Show(); Pump(secondary); secondary.Content = null;
                model.Query = "SYN-HELP"; Assert.Empty(model.Results); model.Query = "socket"; model.Navigate("test-with-mllp"); Pump(window);
                Assert.Empty(old.Search.Text!); Assert.NotEqual(model.Query, old.Search.Text);
                foreach (var id in new[] { WorkspaceDockFactory.SqlQueryId, WorkspaceDockFactory.ScriptEditorId, WorkspaceDockFactory.ConnectionManagerId, WorkspaceDockFactory.MllpListenerId, WorkspaceDockFactory.BatchTransmissionId, WorkspaceDockFactory.LoadTestingId }) Assert.False(registry.IsCreated(id));
                Assert.Equal(revision, document.SearchRevision); Assert.Equal(content, document.ContentVersion); Assert.Equal(dirty, document.IsDirty);
                Assert.Equal(text, document.EditableText); Assert.Equal(position, document.MessageNumber); Assert.False(document.IsSaving);
                Assert.Null(document.AppliedSchemaId); Assert.False(Directory.Exists(Path.Combine(files.Root, "SqlQueries")));
            }
            finally { secondary.Close(); window.Close(); if (document is not null) await document.Disposal; }
            return true;
        }, Token);
    }
    [Fact]
    public async Task ApprovedDescriptionsUseRibbonSchemaLocationAndBothGenerationSources()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var files = new Files(); var preferences = new PreferencesWindow(new ApplicationPreferences(Path.Combine(files.Root, "editor-preferences.json")));
            preferences.Show(); preferences.Categories.SelectedIndex = 1; Pump(preferences);
            Assert.Contains(preferences.GetVisualDescendants().OfType<TextBlock>(), t => t.Text?.Contains("HL7 Tools → Schema Validation", StringComparison.Ordinal) == true);
            var window = new MainWindow(files.Root);
            try
            {
                var report = window.FindControl<Menu>("MainMenu")!.GetLogicalDescendants().OfType<MenuItem>().First(m => Equals(m.Header, "_Generation Report"));
                var tooltip = Assert.IsType<string>(ToolTip.GetTip(report)); Assert.Contains("synthetic HL7", tooltip); Assert.Contains("FHIR", tooltip);
            }
            finally { window.Close(); preferences.Close(); }
            return true;
        }, Token);
    }
}
