using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class DefaultWorkspaceLayoutTests
{
    private static (WorkspaceDockFactory Factory, ToolWindowRegistry Tools) Create(DocumentManager documents)
    {
        var tools = new ToolWindowRegistry();
        tools.Register(new(WorkspaceDockFactory.BrowserId, "Message Browser", () => new MessageBrowserViewModel(documents)));
        tools.Register(new(WorkspaceDockFactory.NavigatorId, "Segment Navigator", () => new InspectionToolViewModel(documents, true)));
        tools.Register(new(WorkspaceDockFactory.FieldInspectorId, "Field Inspector", () => new InspectionToolViewModel(documents, false)));
        tools.Register(new(WorkspaceDockFactory.ActivityLogId, "Activity Log", () => new ActivityLogViewModel(documents.Activity), true, true, RestoreVisibility: true));
        tools.Register(new(WorkspaceDockFactory.FullRawId, "Full Source Viewer", () => new FullRawInspectorViewModel(), true, true));
        var factory = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        factory.CreateLayout(); return (factory, tools);
    }
    private static IEnumerable<IDockable> Walk(IDockable item)
    {
        yield return item;
        if (item is IDock dock) foreach (var child in dock.VisibleDockables ?? []) foreach (var nested in Walk(child)) yield return nested;
    }
    private static void AssertUnique(WorkspaceDockFactory factory)
    {
        var all = Walk(factory.Root).Concat(factory.Root.Windows!.SelectMany(w => Walk(w.Layout!)))
            .Concat(factory.Root.HiddenDockables!).OfType<ITool>().ToArray();
        Assert.Equal(5, all.Length); Assert.Equal(5, all.Select(t => t.Id).Distinct().Count());
    }

    [Fact]
    public void FreshWorkspaceUsesIndependentRightToolsAndVisibleBottomActivityWithoutBrowser()
    {
        using var documents = new DocumentManager(); var (factory, tools) = Create(documents); using var owned = factory;
        var main = Assert.IsAssignableFrom<IProportionalDock>(Assert.Single(factory.Root.VisibleDockables!));
        Assert.Equal(Orientation.Vertical, main.Orientation);
        var upper = Assert.IsAssignableFrom<IProportionalDock>(main.VisibleDockables![0]); Assert.Equal(.73, upper.Proportion);
        Assert.Equal(Orientation.Horizontal, upper.Orientation);
        var editor = Assert.IsAssignableFrom<IDocumentDock>(upper.VisibleDockables![0]); Assert.Equal(.78, editor.Proportion);
        var right = Assert.IsAssignableFrom<IProportionalDock>(upper.VisibleDockables![2]);
        Assert.Equal(.22, right.Proportion); Assert.Equal(Orientation.Vertical, right.Orientation);
        var nav = factory.GetDockable<ITool>(WorkspaceDockFactory.NavigatorId)!;
        var field = factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId)!;
        Assert.NotSame(nav.Owner, field.Owner); Assert.Same(right, nav.Owner!.Owner); Assert.Same(right, field.Owner!.Owner);
        Assert.Equal(.5, nav.Owner.Proportion); Assert.Equal(.5, field.Owner.Proportion);
        var activity = factory.GetDockable<ITool>(WorkspaceDockFactory.ActivityLogId)!;
        Assert.Same(main, activity.Owner!.Owner); Assert.Equal(.27, activity.Owner.Proportion);
        Assert.Equal(Alignment.Bottom, ((IToolDock)activity.Owner).Alignment);
        Assert.True(tools.IsCreated(activity.Id!)); Assert.IsType<ActivityLogViewModel>(activity.Context);
        Assert.Contains(factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId)!, factory.Root.HiddenDockables!);
        Assert.Contains(factory.GetDockable<ITool>(WorkspaceDockFactory.FullRawId)!, factory.Root.HiddenDockables!);
        Assert.False(tools.IsCreated(WorkspaceDockFactory.FullRawId)); AssertUnique(factory);
    }

    [Theory]
    [InlineData(1)] [InlineData(2)]
    public void SavedHiddenPreferencesRemainHiddenAndActivityStaysUninitialized(int version)
    {
        using var documents = new DocumentManager(); var (factory, tools) = Create(documents); using var owned = factory;
        factory.ShowTool(WorkspaceDockFactory.BrowserId);
        factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.ActivityLogId));
        factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId));
        var saved = factory.Capture() with { Version = version };
        documents.Activity.Record(ActivityKind.ApplicationReady);
        factory.Restore(LayoutCodec.Decode(LayoutCodec.Encode(saved)), []);
        Assert.False(tools.IsCreated(WorkspaceDockFactory.ActivityLogId));
        Assert.Contains(factory.GetDockable<ITool>(WorkspaceDockFactory.ActivityLogId)!, factory.Root.HiddenDockables!);
        Assert.Contains(factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId)!, factory.Root.HiddenDockables!);
        Assert.DoesNotContain(factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId)!, factory.Root.HiddenDockables!);
        Assert.Single(documents.Activity.Snapshot()); AssertUnique(factory);
        factory.ShowTool(WorkspaceDockFactory.ActivityLogId);
        Assert.Single(Assert.IsType<ActivityLogViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.ActivityLogId)!.Context).Entries);
    }

    [Fact]
    public void SavedGroupsTabOrderAndFloatingActivityArePreservedWithoutApplyingNewDefaults()
    {
        using var documents = new DocumentManager(); var (factory, _) = Create(documents); using var owned = factory;
        factory.ShowTool(WorkspaceDockFactory.BrowserId);
        var browser = factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId)!;
        var fields = factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId)!;
        var navigator = factory.GetDockable<ITool>(WorkspaceDockFactory.NavigatorId)!;
        factory.MoveDockable((IDock)fields.Owner!, (IDock)navigator.Owner!, fields, navigator);
        var tabOrder = ((IDock)navigator.Owner!).VisibleDockables!.Select(t => t.Id).ToArray();
        navigator.Owner.Proportion = .63;
        var activity = factory.GetDockable<ITool>(WorkspaceDockFactory.ActivityLogId)!; factory.FloatDockable(activity);
        var window = Assert.Single(factory.Root.Windows!); window.Host!.SetPosition(250, 190); window.Host.SetSize(850, 450);
        var saved = LayoutCodec.Decode(LayoutCodec.Encode(factory.Capture())); factory.CloseFloatingWindows();
        factory.Restore(saved, [new(0, 0, 1920, 1080)]);
        navigator = factory.GetDockable<ITool>(WorkspaceDockFactory.NavigatorId)!; fields = factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId)!;
        Assert.Same(navigator.Owner, fields.Owner); Assert.Equal(.63, navigator.Owner!.Proportion);
        Assert.Equal(tabOrder, ((IDock)navigator.Owner).VisibleDockables!.Select(t => t.Id));
        Assert.DoesNotContain(factory.GetDockable<ITool>(browser.Id!)!, factory.Root.HiddenDockables!);
        var restored = Assert.Single(factory.Root.Windows!); Assert.Equal(250, restored.X); Assert.Equal(190, restored.Y);
        Assert.Equal(850, restored.Width); Assert.Equal(450, restored.Height); AssertUnique(factory);
        factory.CloseFloatingWindows();
    }

    [Fact]
    public void LegacyLeftBrowserAndBottomToolTabsRemainInTheirSavedArrangement()
    {
        using var documents = new DocumentManager(); var (factory, _) = Create(documents); using var owned = factory;
        foreach (var version in new[] { 1, 2 })
        {
            var legacy = new WorkspaceLayout(version,
                new("root", "workspace", Children: [new("split", "legacy-horizontal", Children:
                [
                    new("tools", "browser-dock", .22, WorkspaceDockFactory.BrowserId, "Left", [new("tool", WorkspaceDockFactory.BrowserId)]),
                    new("splitter", "legacy-left-separator"),
                    new("split", "legacy-center", .78, Children:
                    [
                        new("documents", "documents"), new("splitter", "legacy-bottom-separator"),
                        new("tools", "diagnostics-dock", .24, WorkspaceDockFactory.FieldInspectorId, "Bottom",
                            [new("tool", WorkspaceDockFactory.NavigatorId), new("tool", WorkspaceDockFactory.FieldInspectorId), new("tool", WorkspaceDockFactory.DiagnosticsId)])
                    ], Vertical: true)
                ])]), [], [WorkspaceDockFactory.FullRawId], null);
            factory.Restore(LayoutCodec.Decode(LayoutCodec.Encode(legacy)), []);
            var browser = factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId)!;
            Assert.Equal(Alignment.Left, ((IToolDock)browser.Owner!).Alignment); Assert.Equal(.22, browser.Owner.Proportion);
            var nav = factory.GetDockable<ITool>(WorkspaceDockFactory.NavigatorId)!;
            var field = factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId)!;
            var activity = factory.GetDockable<ITool>(WorkspaceDockFactory.ActivityLogId)!;
            Assert.Same(nav.Owner, field.Owner); Assert.Same(field.Owner, activity.Owner); Assert.Equal(.24, nav.Owner!.Proportion);
            Assert.Same(field, ((IDock)nav.Owner).ActiveDockable);
            Assert.IsType<ActivityLogViewModel>(activity.Context);
            Assert.DoesNotContain(Walk(factory.Root), d => d.Id == "inspection-stack"); AssertUnique(factory);
        }
    }

    [Fact]
    public void ResetReappliesDefaultsWhileKeepingDocumentsHistoryAndOptionalSourceLaziness()
    {
        using var documents = new DocumentManager(); documents.Open(new("synthetic", "Synthetic", Hl7Parser.Parse("PID|SYN")));
        documents.Activity.Record(ActivityKind.ApplicationReady);
        var (factory, tools) = Create(documents); using var owned = factory;
        factory.ShowTool(WorkspaceDockFactory.BrowserId); factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.ActivityLogId));
        factory.CreateLayout();
        Assert.Single(documents.Documents); Assert.NotNull(factory.GetDockable<IDocument>("synthetic"));
        Assert.DoesNotContain(factory.GetDockable<ITool>(WorkspaceDockFactory.ActivityLogId)!, factory.Root.HiddenDockables!);
        Assert.Contains(factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId)!, factory.Root.HiddenDockables!);
        Assert.False(tools.IsCreated(WorkspaceDockFactory.FullRawId)); Assert.Contains(documents.Activity.Snapshot(), e => e.Kind == ActivityKind.ApplicationReady);
        AssertUnique(factory);
    }

    [Fact]
    public async Task ProportionalDefaultRendersBothRightToolsAndFullWidthBottomLogAfterResize()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(() =>
        {
            using var documents = new DocumentManager(); documents.Open(new("synthetic", "Synthetic", Hl7Parser.Parse("PID|SYN^OTHER")));
            var (factory, _) = Create(documents); using var owned = factory;
            var dock = new DockControl { Layout = factory.Root }; var window = new Window { Width = 1500, Height = 1000, Content = dock }; window.Show();
            try
            {
                foreach (var size in new[] { (1500, 1000), (1100, 750) })
                {
                    window.Width = size.Item1; window.Height = size.Item2; window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                    var editor = Assert.Single(dock.GetVisualDescendants().OfType<MessageInspectionView>());
                    var nav = Assert.Single(dock.GetVisualDescendants().OfType<InspectionPanes>(), p => p.Navigator.IsAttachedToVisualTree());
                    var field = Assert.Single(dock.GetVisualDescendants().OfType<InspectionPanes>(), p => p.Inspector.IsAttachedToVisualTree());
                    var log = Assert.Single(dock.GetVisualDescendants().OfType<ActivityLogView>());
                    var editorPoint = editor.TranslatePoint(default, dock)!.Value; var navPoint = nav.TranslatePoint(default, dock)!.Value;
                    var fieldPoint = field.TranslatePoint(default, dock)!.Value; var logPoint = log.TranslatePoint(default, dock)!.Value;
                    Assert.True(editor.Bounds.Width > nav.Bounds.Width * 2);
                    Assert.True(navPoint.X > editorPoint.X + editor.Bounds.Width - 2); Assert.True(fieldPoint.Y > navPoint.Y);
                    Assert.InRange(Math.Abs(navPoint.X - fieldPoint.X), 0, 2);
                    Assert.True(logPoint.Y > editorPoint.Y + editor.Bounds.Height - 2);
                    Assert.True(log.Bounds.Width > editor.Bounds.Width + nav.Bounds.Width - 35);
                    Assert.True(nav.Bounds.Height > 0); Assert.True(field.Bounds.Height > 0); AssertUnique(factory);
                }
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ExplicitSourceRetainsDocumentTabContextButSameFileNavigationStillInvalidatesIt()
    {
        using var documents = new DocumentManager();
        using var firstInput = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rPID|FIRST\rMSH|^~\\&|SYN\rPID|SECOND"));
        var first = await MessageFileReader.OpenFileAsync(documents, "Synthetic", firstInput, TestContext.Current.CancellationToken);
        using var secondInput = new MemoryStream(Encoding.UTF8.GetBytes("PID|OTHER"));
        var second = await MessageFileReader.OpenFileAsync(documents, "Synthetic", secondInput, TestContext.Current.CancellationToken);
        using var model = new FullRawInspectorViewModel(); model.Open(first, null, null);
        var initial = model.Session!;
        documents.Activate(first.Id); documents.Activate(second.Id);
        Assert.False(initial.IsClosed); Assert.Contains("FIRST", (await initial.ReadAsync(0, TestContext.Current.CancellationToken))!.Text);
        model.Open(second, null, null); Assert.True(initial.IsClosed); var replacement = model.Session!;
        Assert.Contains("OTHER", (await replacement.ReadAsync(0, TestContext.Current.CancellationToken))!.Text);
        model.Open(first, null, null); Assert.True(replacement.IsClosed);
        var beforeNavigation = model.Session!; await first.NavigateAsync(2, TestContext.Current.CancellationToken);
        Assert.True(beforeNavigation.IsClosed); // Deliberately unchanged per the user's scope clarification.
        model.Open(first, null, null); var bound = model.Session!; documents.Close(first.Id); Assert.True(bound.IsClosed);
        model.Open(second, null, null); var closing = model.Session!; model.Dispose(); Assert.True(closing.IsClosed);
        documents.Close(second.Id); await Task.WhenAll(first.Disposal, second.Disposal);
    }
}
