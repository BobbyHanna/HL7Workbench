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

public sealed class RefinedDockingTests
{
    private static WorkspaceDockFactory Factory(DocumentManager documents, ToolWindowRegistry tools)
    {
        tools.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
        tools.Register(new(WorkspaceDockFactory.DiagnosticsId, "Diagnostics", () => new object()));
        tools.Register(new(WorkspaceDockFactory.NavigatorId, "Segment Navigator", () => new InspectionToolViewModel(documents, true)));
        tools.Register(new(WorkspaceDockFactory.FieldInspectorId, "Field Inspector", () => new InspectionToolViewModel(documents, false)));
        var factory = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        return factory;
    }
    [Fact]
    public void InspectorIsLazyAndCloseReleasesSessionWhileRememberingGroupAndOrder()
    {
        using var documents = new DocumentManager(); documents.Open(new("synthetic", "Synthetic", Hl7Parser.Parse("PID|SYN")));
        var tools = new ToolWindowRegistry(); var created = 0;
        tools.Register(new(WorkspaceDockFactory.FullRawId, "Full Source Viewer", () => { created++; return new FullRawInspectorViewModel(); }, true, true));
        using var factory = Factory(documents, tools); factory.CreateLayout();
        Assert.False(tools.IsCreated(WorkspaceDockFactory.FullRawId)); Assert.Equal(0, created);
        factory.ShowTool(WorkspaceDockFactory.FullRawId);
        var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.FullRawId)!;
        var model = Assert.IsType<FullRawInspectorViewModel>(tool.Context); model.Open(documents.ActiveDocument!, null, null);
        var session = model.Session!; var group = Assert.IsAssignableFrom<IToolDock>(tool.Owner);
        Assert.Equal(Alignment.Bottom, group.Alignment); group.Proportion = .33;
        var index = group.VisibleDockables!.IndexOf(tool);
        factory.CloseDockable(tool);
        Assert.True(session.IsClosed); Assert.Null(tool.Context); Assert.False(tools.IsCreated(tool.Id!));
        var saved = LayoutCodec.Decode(LayoutCodec.Encode(factory.Capture()));
        var place = Assert.Single(saved.ToolPlacements!, p => p.Id == tool.Id);
        Assert.Equal(.33, place.Proportion); Assert.Equal(index, place.TabIndex);
        factory.ShowTool(tool.Id!); Assert.Same(group, tool.Owner); Assert.Equal(index, group.VisibleDockables.IndexOf(tool)); Assert.Equal(2, created);
        Assert.Same(documents.Documents[0], documents.ActiveDocument);
    }
    [Fact]
    public void RestartOmitsOptionalContentAndReopensInRememberedTabbedGroup()
    {
        using var documents = new DocumentManager(); var tools = new ToolWindowRegistry(); var created = 0;
        tools.Register(new(WorkspaceDockFactory.FullRawId, "Full Source Viewer", () => { created++; return new FullRawInspectorViewModel(); }, true, true));
        using var factory = Factory(documents, tools); factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.FullRawId);
        var raw = factory.GetDockable<ITool>(WorkspaceDockFactory.FullRawId)!; var group = (IToolDock)raw.Owner!;
        var fields = factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId)!;
        factory.MoveDockable((IDock)fields.Owner!, group, fields, null);
        factory.MoveDockable(group, group, raw, fields);
        var order = group.VisibleDockables!.Select(d => d.Id).ToArray();
        var saved = LayoutCodec.Decode(LayoutCodec.Encode(factory.Capture()));
        factory.Restore(saved, [new(0, 0, 1920, 1080)]);
        Assert.Equal(1, created); Assert.False(tools.IsCreated(WorkspaceDockFactory.FullRawId));
        Assert.Null(factory.GetDockable<ITool>(WorkspaceDockFactory.FullRawId)!.Context);
        factory.ShowTool(WorkspaceDockFactory.FullRawId);
        var restored = (IToolDock)factory.GetDockable<ITool>(WorkspaceDockFactory.FullRawId)!.Owner!;
        Assert.Equal(order, restored.VisibleDockables!.Select(d => d.Id)); Assert.Equal(2, created);
        Assert.DoesNotContain("MSH", LayoutCodec.Encode(saved));
    }
    [Fact]
    public void OptionalFloatingInspectorRestoresPlacementOnlyWhenOpened()
    {
        using var documents = new DocumentManager(); var tools = new ToolWindowRegistry();
        tools.Register(new(WorkspaceDockFactory.FullRawId, "Full Source Viewer", () => new FullRawInspectorViewModel(), true, true));
        using var factory = Factory(documents, tools); factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.FullRawId);
        var raw = factory.GetDockable<ITool>(WorkspaceDockFactory.FullRawId)!; factory.FloatDockable(raw);
        var window = Assert.Single(factory.Root.Windows!); window.Host!.SetPosition(240, 180); window.Host.SetSize(800, 600);
        var saved = LayoutCodec.Decode(LayoutCodec.Encode(factory.Capture())); factory.CloseFloatingWindows(); factory.Restore(saved, [new(0, 0, 1920, 1080)]);
        Assert.Empty(factory.Root.Windows!); Assert.False(tools.IsCreated(raw.Id!));
        factory.ShowTool(raw.Id!); var restored = Assert.Single(factory.Root.Windows!);
        Assert.Equal(240, restored.X); Assert.Equal(180, restored.Y); Assert.Equal(800, restored.Width); Assert.Equal(600, restored.Height);
        var active = factory.GetDockable<ITool>(raw.Id!)!; var target = (IDock)factory.GetDockable<ITool>(WorkspaceDockFactory.NavigatorId)!.Owner!;
        factory.MoveDockable((IDock)active.Owner!, target, active, null); Assert.Same(target, active.Owner);
    }
    [Fact]
    public void ClosedSoleInspectorGroupCanBeRecreatedAfterRestart()
    {
        using var documents = new DocumentManager(); var tools = new ToolWindowRegistry();
        tools.Register(new(WorkspaceDockFactory.FullRawId, "Full Source Viewer", () => new FullRawInspectorViewModel(), true, true));
        using var factory = Factory(documents, tools); factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.FullRawId);
        var raw = factory.GetDockable<ITool>(WorkspaceDockFactory.FullRawId)!;
        var navigator = factory.GetDockable<ITool>(WorkspaceDockFactory.NavigatorId)!;
        factory.RemoveDockable(raw, false);
        var separate = new Dock.Model.Mvvm.Controls.ToolDock { Alignment = Alignment.Right, VisibleDockables = factory.CreateList<IDockable>(raw), ActiveDockable = raw };
        factory.SplitToDock((IDock)navigator.Owner!, separate, DockOperation.Right);
        var group = (IToolDock)raw.Owner!; Assert.Single(group.VisibleDockables!); group.Proportion = .28;
        factory.CloseDockable(raw); var saved = LayoutCodec.Decode(LayoutCodec.Encode(factory.Capture()));
        var place = Assert.Single(saved.ToolPlacements!, p => p.Id == raw.Id);
        factory.Restore(saved, []); factory.ShowTool(raw.Id!);
        var restored = (IToolDock)factory.GetDockable<ITool>(raw.Id!)!.Owner!;
        Assert.Equal(place.GroupId, restored.Id); Assert.Equal(place.Proportion, restored.Proportion);
        Assert.NotNull(factory.FindDockable(factory.Root, d => d is IDocumentDock));
    }
    [Fact]
    public async Task DockedPanesShareSelectionAndToolTabsRetainActiveDocument()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(() =>
        {
            using var documents = new DocumentManager(); var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse("MSH|^~\\&|SYN\rPID|ONE^TWO\rNTE|1||NOTE")); documents.Open(document);
            var tools = new ToolWindowRegistry(); using var factory = Factory(documents, tools); var dock = new DockControl { Layout = factory.CreateLayout() };
            var window = new Window { Width = 1400, Height = 1000, Content = dock }; window.Show();
            try
            {
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var raw = Assert.Single(dock.GetVisualDescendants().OfType<MessageInspectionView>()); Assert.Single(raw.RowDefinitions);
                var navigator = factory.GetDockable<ITool>(WorkspaceDockFactory.NavigatorId)!;
                var fields = factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId)!;
                Assert.NotSame(navigator.Owner, fields.Owner);
                Assert.Equal(2, dock.GetVisualDescendants().OfType<InspectionPanes>().Count());
                var panes = Assert.Single(dock.GetVisualDescendants().OfType<InspectionPanes>(), p => p.Navigator.IsAttachedToVisualTree());
                panes.Navigator.SelectedItem = document.Inspection.Segments[1]; Assert.Contains("PID|ONE", raw.Editor.SelectedText);
                factory.SetActiveDockable(fields); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                panes = Assert.Single(dock.GetVisualDescendants().OfType<InspectionPanes>(), p => p.Inspector.IsAttachedToVisualTree()); Assert.Same(document.Inspection.Fields, panes.Inspector.ItemsSource);
                panes.Inspector.SelectedItem = document.Inspection.Fields[0]; Assert.Equal("ONE^TWO", raw.Editor.SelectedText);
                Assert.Same(document, documents.ActiveDocument);
                factory.CloseDockable(fields); Assert.NotNull(navigator.Owner); Assert.Same(document, documents.ActiveDocument);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
