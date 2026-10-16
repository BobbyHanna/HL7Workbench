using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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

public sealed class SearchToolTests
{
    private const string First = "MSH|^~\\&|SYN|||||||SYN-1||2.5\rPID|1||SYN-ONE||Example^Ada\r";
    private const string Second = "MSH|^~\\&|SYN|||||||SYN-2||2.5\rPID|1||SYN-TWO||Other^Bea\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-search-ui-" + Guid.NewGuid().ToString("N"));
        public Files() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }
    private static async Task<MessageDocument> Open(DocumentManager docs, string text = First + Second)
    { using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text)); return await MessageFileReader.OpenFileAsync(docs, "Synthetic.hl7", input, Token); }
    private static void Key(Control control, Avalonia.Input.Key key, KeyModifiers modifiers = KeyModifiers.None) =>
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SearchControlsCommandsFocusPagingSelectionAndNavigationAreReadOnly(bool dark)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var docs = new DocumentManager(); var doc = await Open(docs); using var model = new SearchViewModel(docs);
            var view = new SearchResultsView(model); var window = new Window { Width = 900, Height = 500, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = view }; window.Show();
            try
            {
                Assert.Same(model.SearchCommand, view.Search.Command); Assert.Same(model.ClearCommand, view.Clear.Command); Assert.False(view.Cancel.IsEffectivelyEnabled);
                Assert.Equal("Search", view.Search.Content); Assert.Equal("Clear", view.Clear.Content);
                model.OpenRequested += model.NotifyShown;
                var idleSearch = model.SearchingTask;
                model.OpenCommand.Execute(null); Dispatcher.UIThread.RunJobs(); Assert.True(view.Value.IsFocused);
                Assert.Same(idleSearch, model.SearchingTask); Assert.False(model.Session.IsBusy); Assert.Null(model.Session.Results);
                view.Value.Text = "SYN-TWO"; model.SearchCommand.Execute(null); await model.SearchingTask;
                Assert.Equal(1, model.Session.ResultCount); Assert.Equal(2, Assert.Single(model.Session.Page).MessageNumber);
                view.Results.SelectedIndex = 0; Assert.Equal(1, doc.MessageNumber);
                Key(view.Results, Avalonia.Input.Key.Enter); await view.NavigationTask;
                Assert.Equal(2, doc.MessageNumber); Assert.False(doc.IsEditMode); Assert.False(doc.IsDirty); Assert.Single(docs.Documents);
                await doc.NavigateAsync(1, Token); window.UpdateLayout();
                var row = view.Results.ContainerFromIndex(0)!;
                var point = row.TranslatePoint(new Point(10, row.Bounds.Height / 2), window)!.Value;
                window.MouseMove(point); window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
                Assert.Equal(1, doc.MessageNumber);
                window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left); await view.NavigationTask;
                Assert.Equal(2, doc.MessageNumber);
                view.Filter.IsChecked = true; Assert.Equal(new[] { 2 }, doc.NavigationMatches);
                var completedSearch = model.SearchingTask; var completedResults = model.Session.Results;
                view.Mode.Focus(); model.OpenCommand.Execute(null); Dispatcher.UIThread.RunJobs();
                Assert.True(view.Value.IsFocused); Assert.Same(completedSearch, model.SearchingTask); Assert.Same(completedResults, model.Session.Results);
                Assert.True(model.Session.FilterNavigator); Assert.Equal(2, doc.MessageNumber);
                view.Mode.SelectedIndex = 1; view.FieldPath.Text = "PID-3"; view.Comparison.SelectedIndex = 1; view.Value.Text = "SYN-TWO";
                Assert.Equal(MessageSearchMode.Field, model.Mode); Assert.Equal(MessageSearchComparison.Equals, model.Comparison);
                Key(view.Value, Avalonia.Input.Key.Enter); await model.SearchingTask; Assert.Equal(1, model.Session.ResultCount); Assert.Null(doc.NavigationMatches);
                view.FieldPath.Text = "not-a-path"; model.SearchCommand.Execute(null); await model.SearchingTask;
                Assert.Contains("valid", view.Status.Text!); Assert.Equal(0, view.Results.ItemCount);
                view.FieldPath.Text = "PID-3"; model.SearchCommand.Execute(null); Assert.True(view.Cancel.IsEffectivelyEnabled);
                model.CancelCommand.Execute(null); await model.SearchingTask;
                Assert.Null(model.Session.Results); Assert.Contains("canceled", view.Status.Text!); Assert.False(view.Cancel.IsEffectivelyEnabled);
                // Stage 6B Clear retains the authored query while clearing discovery state.
                model.ClearCommand.Execute(null); Assert.Equal("SYN-TWO", model.Value); Assert.Null(doc.NavigationMatches); Assert.Null(model.Session.Results);
                Assert.Equal(window.ActualThemeVariant, view.ActualThemeVariant);
                window.Content = null; var reconstructed = new SearchResultsView(model); window.Content = reconstructed;
                Assert.Same(model.SearchCommand, reconstructed.Search.Command);
            }
            finally { window.Close(); docs.Close(doc.Id, true); await doc.Disposal; }
            return true;
        }, Token);
    }
    [Fact]
    public async Task FilteredExistingNavigatorShowsMatchingOrdinalsAndRestoresAllNavigation()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var docs = new DocumentManager(); var doc = await Open(docs, First + Second + First);
            using var session = new MessageSearchSession(docs); var view = new FileDocumentView(doc); var window = new Window { Content = view, Width = 900, Height = 600 }; window.Show();
            try
            {
                await session.StartAsync(new(MessageSearchMode.Text, "SYN-TWO"), Token); session.FilterNavigator = true;
                Assert.True(view.FilteredMessages.IsVisible); Assert.Equal(new[] { 2 }, view.FilteredMessages.Items.Cast<int>());
                Assert.Null(view.FilteredMessages.SelectedItem); Assert.Equal(1, doc.MessageNumber);
                view.MessageNumberInput.Text = "3"; await view.JumpAsync(); Assert.Equal(1, doc.MessageNumber);
                await view.NavigateAsync(2); Assert.Equal(2, doc.MessageNumber);
                session.FilterNavigator = false; Assert.False(view.FilteredMessages.IsVisible);
                view.MessageNumberInput.Text = "3"; await view.JumpAsync(); Assert.Equal(3, doc.MessageNumber);
                Assert.Equal(3, doc.MessageCount); Assert.False(doc.IsDirty);
                await session.StartAsync(new(MessageSearchMode.Text, "NO-MATCH"), Token); session.FilterNavigator = true;
                Assert.Empty(view.FilteredMessages.Items); await view.NavigateAsync(1); Assert.Equal(3, doc.MessageNumber);
                session.Clear(); await view.NavigateAsync(1); Assert.Equal(1, doc.MessageNumber);
            }
            finally { window.Close(); docs.Close(doc.Id, true); await doc.Disposal; }
            return true;
        }, Token);
    }
    [Fact]
    public async Task ResultPagesRemainBoundedAndVirtualized()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var docs = new DocumentManager(); var doc = await Open(docs, string.Concat(Enumerable.Repeat(First, 451)));
            using var model = new SearchViewModel(docs); var view = new SearchResultsView(model); var window = new Window { Content = view, Width = 700, Height = 400 }; window.Show();
            try
            {
                await model.Session.StartAsync(new(MessageSearchMode.Text, "SYN-ONE"), Token); window.UpdateLayout();
                Assert.Equal(200, view.Results.ItemCount); Assert.True(view.Next.IsEnabled); Assert.False(view.Previous.IsEnabled);
                Assert.IsType<VirtualizingStackPanel>(view.Results.ItemsPanel!.Build());
                view.Next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(201, ((MessageSearchMatch)view.Results.Items[0]!).MessageNumber);
                view.Next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(51, view.Results.ItemCount); Assert.False(view.Next.IsEnabled);
            }
            finally { window.Close(); docs.Close(doc.Id, true); await doc.Disposal; }
            return true;
        }, Token);
    }
    [Fact]
    public async Task MainAndFloatingShortcutRootsOpenTheSameDockToolAndRetainThreeMenus()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            // Use the application's Dock theme/template so focus is checked on
            // the revealed tool view, rather than only on the factory model.
            Application.Current!.Styles.Add(new Dock.Avalonia.Themes.Fluent.DockFluentTheme());
            Application.Current.DataTemplates.Add(new DockContentTemplate { DockedInspection = true });
            using var files = new Files(); var window = new MainWindow(files.Root); window.Show();
            try
            {
                var dock = window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!;
                var factory = Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory);
                Key(window, Avalonia.Input.Key.F, KeyModifiers.Control);
                var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.SearchResultsId)!;
                var model = Assert.IsType<SearchViewModel>(tool.Context); Assert.Same(tool, Assert.IsAssignableFrom<IToolDock>(tool.Owner).ActiveDockable);
                Assert.Equal("Find Messages", tool.Title);
                var focusRequests = 0; model.FocusRequested += () => focusRequests++;
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Assert.Equal(1, focusRequests);
                var idleSearch = model.SearchingTask;
                var menu = window.FindControl<Menu>("MainMenu")!;
                Assert.Equal(new[] { "_File", "_Settings", "_View" }, menu.Items.Cast<MenuItem>().Select(m => m.Header));
                var findMenu = ((MenuItem)menu.Items[2]!).Items.Cast<MenuItem>().Single(m => Equals(m.Header, "_Find Messages"));
                Assert.Same(model.OpenCommand, findMenu.Command); Assert.Equal("Find Messages", AutomationProperties.GetName(findMenu));
                Assert.Equal(Avalonia.Input.Key.F, findMenu.InputGesture!.Key); Assert.Equal(KeyModifiers.Control, findMenu.InputGesture.KeyModifiers);
                Assert.Contains("Find Messages", ToolTip.GetTip(findMenu)!.ToString());
                var ribbon = Assert.IsType<UtilityRibbon>(window.FindControl<ContentControl>("RibbonHost")!.Content);
                ribbon.SelectTab(2); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var commands = ribbon.TemporaryPopup.Child!.GetVisualDescendants().OfType<Button>().ToArray();
                var findButton = commands.Single(b => AutomationProperties.GetName(b) == "Find Messages");
                Assert.Same(model.OpenCommand, findButton.Command); Assert.Contains("Find Messages", ToolTip.GetTip(findButton)!.ToString());
                Assert.DoesNotContain(commands, b => AutomationProperties.GetName(b) is "Focus Search" or "Search Results");
                Assert.Same(model.ClearCommand, commands.Single(b => AutomationProperties.GetName(b) == "Clear Search / Restore Navigator").Command);
                ribbon.TemporaryPopup.IsOpen = false;
                void RevealAndFocus(Action open)
                {
                    factory.CloseDockable(tool); Assert.Contains(tool, factory.Root.HiddenDockables!);
                    var beforeFocus = focusRequests; open(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    Assert.Equal(beforeFocus + 1, focusRequests); Assert.DoesNotContain(tool, factory.Root.HiddenDockables!); Assert.Same(model, tool.Context);
                    Assert.Same(tool, Assert.IsAssignableFrom<IToolDock>(tool.Owner).ActiveDockable);
                    Assert.Same(idleSearch, model.SearchingTask); Assert.False(model.Session.IsBusy); Assert.Null(model.Session.Results);
                    var view = Assert.Single(window.GetVisualDescendants().OfType<SearchResultsView>()); Assert.True(view.Value.IsFocused);
                }
                RevealAndFocus(() => findButton.Command!.Execute(null));
                RevealAndFocus(() => findMenu.Command!.Execute(null));
                RevealAndFocus(() => Key(window, Avalonia.Input.Key.F, KeyModifiers.Control));
                factory.CloseDockable(tool); Assert.Contains(tool, factory.Root.HiddenDockables!);
                var floatingInputRoot = Assert.IsAssignableFrom<Window>(factory.HostWindowLocator![nameof(IDockWindow)]());
                try
                {
                    var beforeFocus = focusRequests; Key(floatingInputRoot, Avalonia.Input.Key.F, KeyModifiers.Control); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    Assert.Equal(beforeFocus + 1, focusRequests); Assert.DoesNotContain(tool, factory.Root.HiddenDockables!); Assert.Same(model, tool.Context);
                    Assert.Same(idleSearch, model.SearchingTask); Assert.False(model.Session.IsBusy);
                    model.BuilderMode = SearchBuilderMode.Advanced; window.UpdateLayout();
                    var advancedView = Assert.Single(window.GetVisualDescendants().OfType<SearchResultsView>());
                    var condition = (SearchConditionEditor)advancedView.ConditionRows.Children[0]; condition.Value.Text = "SYN-ADVANCED";
                    Key(floatingInputRoot, Avalonia.Input.Key.F, KeyModifiers.Control); Dispatcher.UIThread.RunJobs();
                    Assert.True(condition.Value.IsFocused); Assert.Same(idleSearch, model.SearchingTask); Assert.False(model.Session.IsBusy);
                }
                finally { floatingInputRoot.Close(); }
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public void SearchToolPlacementRoundTripsWithoutQueryOrResultPersistence()
    {
        using var docs = new DocumentManager(); docs.Open(new("synthetic", "Synthetic", Hl7Parser.Parse(First)));
        using var model = new SearchViewModel(docs) { Value = "SYN-PRIVATE-QUERY" };
        Assert.Equal("tool.search-results", WorkspaceDockFactory.SearchResultsId);
        var registry = new ToolWindowRegistry(); registry.Register(new(WorkspaceDockFactory.SearchResultsId, "Find Messages", () => model, Optional: true, RestoreVisibility: true));
        using var factory = new WorkspaceDockFactory(docs, registry) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.SearchResultsId); var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.SearchResultsId)!;
        Assert.False(tool.CanDockAsDocument); factory.FloatDockable(tool);
        var saved = LayoutCodec.Encode(factory.Capture()); Assert.Contains(WorkspaceDockFactory.SearchResultsId, saved); Assert.DoesNotContain("PRIVATE", saved);
        factory.CloseFloatingWindows(); factory.Restore(LayoutCodec.Decode(saved), [new(0, 0, 1920, 1080)]);
        Assert.Same(model, factory.GetDockable<ITool>(WorkspaceDockFactory.SearchResultsId)!.Context); Assert.Single(factory.Root.Windows!);
        Assert.Equal("Find Messages", factory.GetDockable<ITool>("tool.search-results")!.Title);
        factory.CloseFloatingWindows(); factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.SearchResultsId);
        Assert.Same(model, factory.GetDockable<ITool>(WorkspaceDockFactory.SearchResultsId)!.Context);
        Assert.Equal("Find Messages", factory.GetDockable<ITool>("tool.search-results")!.Title);
    }
}
