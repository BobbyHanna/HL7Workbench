using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Input.Platform;
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

public sealed class ActivityLogViewTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }

    [Fact]
    public async Task SeverityFiltersClearAndChronologicalSelectedCopyUseSafeSessionHistory()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(async () =>
        {
            var log = new ActivityLog(); log.Record(ActivityKind.ApplicationReady);
            log.RecordDiagnostic(1, 1, new("MissingMsh", Hl7DiagnosticSeverity.Warning, "SYN-SECRET-EXCERPT"), ActivityCategory.Structure);
            log.Record(ActivityKind.FileOpenFailed, 2, failure: ActivityFailure.AccessDenied);
            using var model = new ActivityLogViewModel(log); var view = new ActivityLogView(model);
            var window = new Window { Width = 1100, Height = 350, Content = view }; window.Show();
            try
            {
                Pump(window); Assert.Equal(3, model.Entries.Count);
                view.Entries.SelectedItems!.Add(model.Entries[2]); view.Entries.SelectedItems.Add(model.Entries[0]);
                var copied = view.SelectedText(); Assert.Contains("Application ready", copied); Assert.Contains("File open failed", copied);
                Assert.True(copied.IndexOf("Application ready", StringComparison.Ordinal) < copied.IndexOf("File open failed", StringComparison.Ordinal));
                Assert.DoesNotContain("SYN-SECRET", copied);
                await view.CopyAsync(); Assert.Equal(copied, await window.Clipboard!.TryGetTextAsync());
                view.Severity.SelectedIndex = 2; Assert.Equal(Hl7DiagnosticSeverity.Warning, Assert.Single(model.Entries).Severity);
                view.Severity.SelectedIndex = 3; Assert.Equal(Hl7DiagnosticSeverity.Error, Assert.Single(model.Entries).Severity);
                view.Severity.SelectedIndex = 1; Assert.Equal(Hl7DiagnosticSeverity.Information, Assert.Single(model.Entries).Severity);
                view.Severity.SelectedIndex = 0; Assert.Equal(3, model.Entries.Count);
                view.Clear.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Dispatcher.UIThread.RunJobs();
                Assert.Empty(log.Snapshot()); Assert.Empty(model.Entries);
                log.Record(ActivityKind.FileIndexed, 3, count: 10); Dispatcher.UIThread.RunJobs();
                Assert.Single(model.Entries);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }

    [Fact]
    public async Task TailFollowingAndOlderEntryScrollAnchorSurviveAppendingAndRetentionEviction()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var log = new ActivityLog(); for (var i = 0; i < 1950; i++) log.Record(ActivityKind.FileIndexed, 1, count: i);
            using var model = new ActivityLogViewModel(log); var view = new ActivityLogView(model);
            var window = new Window { Width = 1100, Height = 320, Content = view }; window.Show();
            try
            {
                Pump(window); Pump(window);
                var scroll = view.Entries.GetVisualDescendants().OfType<ScrollViewer>().First();
                Assert.True(scroll.Offset.Y >= scroll.Extent.Height - scroll.Viewport.Height - 2);
                log.Record(ActivityKind.FileOpened, 1); Pump(window); Pump(window);
                Assert.True(scroll.Offset.Y >= scroll.Extent.Height - scroll.Viewport.Height - 2);
                scroll.Offset = new Vector(0, scroll.Extent.Height / 2); Pump(window);
                var offset = scroll.Offset.Y;
                var anchor = FirstVisible(view);
                log.Record(ActivityKind.FileOpened, 2); Pump(window); Pump(window);
                Assert.Equal(anchor, FirstVisible(view)); Assert.InRange(scroll.Offset.Y, offset - 2, offset + 2);
                for (var i = 0; i < 100; i++) log.Record(ActivityKind.FileIndexed, 3, count: i);
                Pump(window); Pump(window);
                Assert.Equal(2000, model.Entries.Count); Assert.Equal(anchor, FirstVisible(view));
                Assert.True(view.Entries.GetVisualDescendants().OfType<ListBoxItem>().Count() < 100);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }

    private static long FirstVisible(ActivityLogView view) => view.Entries.GetVisualDescendants().OfType<ListBoxItem>()
        .Select(item => (Entry: item.DataContext as ActivityEntry, Point: item.TranslatePoint(default, view.Entries)))
        .Where(p => p.Entry is not null && p.Point is { Y: >= 0 }).OrderBy(p => p.Point!.Value.Y).First().Entry!.Sequence;

    [Fact]
    public async Task AlertIndicatorCountsOnlyRealWarningsAndErrorsAndOpensTheLog()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var log = new ActivityLog(); var opened = 0; var indicator = new ActivityStatusIndicator(log, () => opened++);
            var window = new Window { Content = indicator }; window.Show();
            try
            {
                log.Record(ActivityKind.ApplicationReady); Pump(window); Assert.False(indicator.IsVisible);
                log.RecordDiagnostic(1, 1, new("UnsupportedEditEncoding", Hl7DiagnosticSeverity.Information, "ignored"), ActivityCategory.Structure);
                Pump(window); Assert.False(indicator.IsVisible);
                log.RecordDiagnostic(1, 1, new("InvalidEncoding", Hl7DiagnosticSeverity.Error, "ignored"), ActivityCategory.Structure);
                Pump(window); Assert.True(indicator.IsVisible); Assert.Contains("1 error(s)", Assert.IsType<string>(indicator.Content));
                indicator.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Equal(1, opened);
                log.Clear(); Pump(window); Assert.False(indicator.IsVisible);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }

    [Theory]
    [InlineData(1, false)] [InlineData(2, false)] [InlineData(1, true)] [InlineData(2, true)]
    public void LegacyVisibleDiagnosticsPlacementBecomesOneVisibleActivityTool(int version, bool floating)
    {
        using var documents = new DocumentManager(); var legacy = new ToolWindowRegistry();
        legacy.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
        legacy.Register(new(WorkspaceDockFactory.DiagnosticsId, "Diagnostics", () => new object()));
        using var old = new WorkspaceDockFactory(documents, legacy) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        old.CreateLayout(); var original = old.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId)!;
        if (floating) old.FloatDockable(original);
        var group = (IToolDock)original.Owner!; group.Proportion = .31;
        var saved = old.Capture() with { Version = version, ToolPlacements = null };
        var tools = new ToolWindowRegistry(); var created = 0;
        tools.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
        tools.Register(new(WorkspaceDockFactory.ActivityLogId, "Activity Log", () => { created++; return new ActivityLogViewModel(documents.Activity); }, true, true, RestoreVisibility: true));
        using var current = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        current.Restore(LayoutCodec.Decode(LayoutCodec.Encode(saved)), []);
        var tool = current.GetDockable<ITool>(WorkspaceDockFactory.ActivityLogId)!;
        Assert.Equal("Activity Log", tool.Title); Assert.IsType<ActivityLogViewModel>(tool.Context); Assert.Equal(1, created);
        Assert.DoesNotContain(tool, current.Root.HiddenDockables!); Assert.Equal(floating, current.Root.Windows!.Count > 0);
        var place = Assert.Single(current.Capture().ToolPlacements!, p => p.Id == WorkspaceDockFactory.ActivityLogId);
        Assert.Equal(group.Id, place.GroupId); Assert.Equal(.31, place.Proportion); Assert.Equal(floating, place.Floating is not null);
        current.ShowTool(WorkspaceDockFactory.ActivityLogId); Assert.Equal(1, created);
        Assert.IsType<ActivityLogViewModel>(tool.Context); Assert.DoesNotContain(tool, current.Root.HiddenDockables!);
        Assert.Equal(floating, current.Root.Windows!.Count > 0);
        Assert.Single(current.Capture().ToolPlacements!, p => p.Id == WorkspaceDockFactory.ActivityLogId);
        current.CloseFloatingWindows(); old.CloseFloatingWindows();
    }

    [Fact]
    public async Task OptionalToolReopeningReleasesTheViewProjectionAndRetainsSessionEvents()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            using var documents = new DocumentManager(); var tools = new ToolWindowRegistry();
            tools.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
            tools.Register(new(WorkspaceDockFactory.NavigatorId, "Navigator", () => new object()));
            tools.Register(new(WorkspaceDockFactory.FieldInspectorId, "Fields", () => new object()));
            tools.Register(new(WorkspaceDockFactory.ActivityLogId, "Activity Log", () => new ActivityLogViewModel(documents.Activity), true, true, RestoreVisibility: true));
            using var factory = new WorkspaceDockFactory(documents, tools); factory.CreateLayout();
            Assert.True(tools.IsCreated(WorkspaceDockFactory.ActivityLogId));
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.ActivityLogId)!;
            Assert.DoesNotContain(tool, factory.Root.HiddenDockables!);
            documents.Activity.Record(ActivityKind.ApplicationReady);
            factory.ShowTool(tool.Id!); var first = Assert.IsType<ActivityLogViewModel>(tool.Context);
            var content = new DockContentTemplate().Build(tool); var window = new Window { Content = content }; window.Show();
            try
            {
                Pump(window); Assert.Single(content.GetVisualDescendants().OfType<ActivityLogView>());
                factory.CloseDockable(tool); Pump(window); Assert.Null(tool.Context); Assert.Empty(first.Entries);
                Assert.Empty(content.GetVisualDescendants().OfType<ActivityLogView>());
                documents.Activity.Record(ActivityKind.FileOpened, 1); Dispatcher.UIThread.RunJobs();
                Assert.Empty(first.Entries); Assert.False(tools.IsCreated(tool.Id!));
                factory.ShowTool(tool.Id!); Pump(window); var next = Assert.IsType<ActivityLogViewModel>(tool.Context);
                Assert.NotSame(first, next); Assert.Equal(2, next.Entries.Count);
                Assert.Single(content.GetVisualDescendants().OfType<ActivityLogView>());
                var saved = factory.Capture(); window.Content = null;
                factory.Restore(saved, []); Assert.True(tools.IsCreated(tool.Id!));
                Assert.Equal(2, documents.Activity.Snapshot().Count);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
}
