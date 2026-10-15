using System.Text;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Input;
using HL7Workbench.Core;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using Dock.Model.Mvvm.Controls;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class WindowsAcceptanceTests
{
    [Fact]
    public async Task ValueAndMessageCommandsSwitchAnAlreadyOpenDockedViewerToDistinctScopes()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            var value = new string('C', 20000) + "TAIL";
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rOBX|1|TX|||" + value + "|AFTER\rNTE|1||FOLLOWING"));
            var document = await MessageFileReader.OpenFileAsync(documents, "Synthetic", input, TestContext.Current.CancellationToken);
            var file = new FileDocumentView(document); using var model = new FullRawInspectorViewModel();
            var tool = new Tool { Id = WorkspaceDockFactory.FullRawId, Context = model };
            var content = new DockContentTemplate().Build(tool);
            var grid = new Grid { RowDefinitions = new("*,*") }; grid.Children.Add(file); Grid.SetRow(content, 1); grid.Children.Add(content);
            var window = new Window { Width = 1400, Height = 1100, Content = grid };
            window.AddHandler(ViewerRequests.OpenInspectorEvent, (_, e) => { model.Open(e.Document, e.Range, e.Path); e.Handled = true; }); window.Show();
            try
            {
                var main = file.ActiveView!; await main.PresentationView!.Ready;
                var marker = Assert.Single(document.Presentation!.Fragments, f => f.Marker is not null);
                main.Editor.CaretOffset = (int)marker.Display.Offset;
                main.Editor.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
                var menus = ((IEnumerable<MenuItem>)main.Editor.ContextMenu!.ItemsSource!).ToArray();
                var fullValue = Assert.Single(menus, item => Equals(item.Header, "View Full Value"));
                var fullMessage = Assert.Single(menus, item => Equals(item.Header, "View Full Message Source"));
                Assert.NotNull(ToolTip.GetTip(fullValue)); Assert.NotNull(ToolTip.GetTip(fullMessage));
                for (var repeat = 0; repeat < 3; repeat++)
                {
                    fullValue.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    var view = Assert.Single(content.GetVisualDescendants().OfType<FullRawInspectorView>()); await view.Ready;
                    Assert.Equal(marker.Omitted, model.Session!.Scope); Assert.Contains("OBX", model.Session.Label);
                    Assert.Equal(new string('C', 16384), view.Editor.Text);
                    await view.LoadAsync(value.Length - 4); Assert.Equal("TAIL", view.Editor.Text);
                    var valueSession = model.Session;
                    fullMessage.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    Assert.True(valueSession.IsClosed);
                    view = Assert.Single(content.GetVisualDescendants().OfType<FullRawInspectorView>()); await view.Ready;
                    Assert.Equal(new CharacterRange(0, document.TextLength), model.Session!.Scope); Assert.Equal(0, view.Position); Assert.StartsWith("MSH", view.Editor.Text);
                    await view.LoadAsync(document.TextLength - 9); Assert.Equal("FOLLOWING", view.Editor.Text);
                    Assert.Contains("Full Source Viewer", Assert.Single(view.Children.OfType<TextBlock>()).Text!);
                }
            }
            finally { window.Close(); documents.Close(document.Id); await document.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FieldValueChunkRowAppearsOnlyForLargeValuesAndControlsDescribeTheirScope()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rOBX|1|TX|||" + new string('D', 20000) + "|SHORT\rNTE|1||" + new string('E', 4096)));
            var document = await MessageFileReader.OpenFileAsync(documents, "Synthetic", input, TestContext.Current.CancellationToken);
            var panes = new InspectionPanes(document.Inspection, document, InspectionPaneKind.Inspector);
            var window = new Window { Width = 1200, Height = 900, Content = panes }; window.Show();
            try
            {
                Assert.False(panes.ValueNavigation.IsVisible);
                document.Inspection.SelectSegment(document.Inspection.Segments.Single(s => s.Identifier == "OBX"));
                document.Inspection.InspectField(6); await panes.LoadPreviewAsync(0); Assert.False(panes.ValueNavigation.IsVisible);
                document.Inspection.InspectField(5); await panes.LoadPreviewAsync(0); Assert.True(panes.ValueNavigation.IsVisible);
                Assert.Contains("20000", panes.ValueStatus.Text!);
                Assert.All(panes.ValueNavigation.Children.OfType<Button>(), b => Assert.Contains("value", Assert.IsType<string>(ToolTip.GetTip(b))));
                Assert.NotNull(ToolTip.GetTip(panes.FieldNumber)); Assert.NotNull(ToolTip.GetTip(panes.InspectFieldButton));
                await panes.LoadPreviewAsync(16384); Assert.Equal(16384, panes.ValueOffset); Assert.True(panes.ValueNavigation.IsVisible);
                document.Inspection.SelectSegment(document.Inspection.Segments.Single(s => s.Identifier == "NTE")); document.Inspection.InspectField(3);
                await panes.LoadPreviewAsync(0); Assert.False(panes.ValueNavigation.IsVisible);
                document.Inspection.InspectField(99); Assert.False(panes.ValueNavigation.IsVisible);
                document.Inspection.ClearSelection(); Assert.False(panes.ValueNavigation.IsVisible);
                using var raw = new RawInspectorSession(document); using var sourceView = new FullRawInspectorView(raw);
                Assert.All(sourceView.Children.OfType<WrapPanel>().Single().Children.OfType<Button>(), b => Assert.NotNull(ToolTip.GetTip(b)));
                var file = new FileDocumentView(document); window.Content = file; window.UpdateLayout();
                var buttons = file.GetVisualDescendants().OfType<Button>().Where(b => b.Content is "|<" or "<" or ">" or ">|").ToArray();
                Assert.Equal(4, buttons.Length); Assert.All(buttons, b => Assert.Contains("message", Assert.IsType<string>(ToolTip.GetTip(b))));
            }
            finally { window.Close(); documents.Close(document.Id); await document.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void FreshAndLegacyLayoutsOpenStructuralToolsButRespectExplicitClosedState()
    {
        using var documents = new DocumentManager(); var tools = new ToolWindowRegistry();
        tools.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object())); tools.Register(new(WorkspaceDockFactory.DiagnosticsId, "Diagnostics", () => new object()));
        tools.Register(new(WorkspaceDockFactory.NavigatorId, "Segment Navigator", () => new object())); tools.Register(new(WorkspaceDockFactory.FieldInspectorId, "Field Inspector", () => new object()));
        tools.Register(new(WorkspaceDockFactory.FullRawId, "Full Source Viewer", () => throw new InvalidOperationException("Must remain lazy"), true, true));
        using var factory = new WorkspaceDockFactory(documents, tools); factory.CreateLayout();
        void AssertDefaults()
        {
            foreach (var id in new[] { WorkspaceDockFactory.NavigatorId, WorkspaceDockFactory.FieldInspectorId })
            { var tool = factory.GetDockable<ITool>(id)!; Assert.Contains(tool, ((IDock)tool.Owner!).VisibleDockables!); Assert.DoesNotContain(tool, factory.Root.HiddenDockables!); }
            Assert.False(tools.IsCreated(WorkspaceDockFactory.FullRawId));
        }
        AssertDefaults();
        var old = new WorkspaceLayout(1, new("root", Children: [new("split", Children: [new("documents"), new("splitter"), new("tools", Alignment: "Bottom", Children: [new("tool", WorkspaceDockFactory.DiagnosticsId)])], Vertical: true)]), [], [WorkspaceDockFactory.BrowserId], null);
        factory.Restore(old, []); AssertDefaults();
        factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId));
        var saved = LayoutCodec.Decode(LayoutCodec.Encode(factory.Capture())); factory.Restore(saved, []);
        Assert.Contains(factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId)!, factory.Root.HiddenDockables!);
        Assert.False(tools.IsCreated(WorkspaceDockFactory.FullRawId));
    }
    [Fact]
    public async Task CachedToolTemplateTracksLazyContextReplacementWithoutReattachment()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rNTE|1||" + new string('A', 20000) + "|AFTER"));
            var document = await MessageFileReader.OpenFileAsync(documents, "Synthetic", input, TestContext.Current.CancellationToken);
            var tool = new Tool { Id = WorkspaceDockFactory.FullRawId };
            var content = new DockContentTemplate().Build(tool);
            var window = new Window { Width = 1100, Height = 800, Content = content }; window.Show();
            try
            {
                Assert.Empty(content.GetVisualDescendants().OfType<FullRawInspectorView>());
                for (var reopen = 0; reopen < 3; reopen++)
                {
                    using var model = new FullRawInspectorViewModel(); model.Open(document, null, null);
                    tool.Context = model; window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    var view = Assert.Single(content.GetVisualDescendants().OfType<FullRawInspectorView>());
                    await view.Ready; Assert.StartsWith("MSH", view.Editor.Text); Assert.True(view.Bounds.Width > 0 && view.Bounds.Height > 0);
                    var borrowed = model.Session!; tool.Context = null; model.Dispose(); window.UpdateLayout();
                    Assert.True(borrowed.IsClosed); Assert.Empty(content.GetVisualDescendants().OfType<FullRawInspectorView>());
                    Assert.Equal(0, view.Editor.Document.TextLength);
                }
            }
            finally { window.Close(); documents.Close(document.Id); await document.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProductionOpenOrderLoadsDockedScopeAndReopensAfterFloatingAndRedocking()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            var value = new string('B', 20000) + "TAIL";
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rOBX|1|TX|||" + value + "|AFTER\rNTE|1||FOLLOWING"));
            var document = await MessageFileReader.OpenFileAsync(documents, "Synthetic", input, TestContext.Current.CancellationToken);
            var tools = new ToolWindowRegistry();
            tools.Register(new(WorkspaceDockFactory.BrowserId, "Browser", () => new object()));
            tools.Register(new(WorkspaceDockFactory.DiagnosticsId, "Diagnostics", () => new object()));
            tools.Register(new(WorkspaceDockFactory.FullRawId, "Full Source Viewer", () => new FullRawInspectorViewModel(), true, true));
            using var factory = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
            var dock = new DockControl { Layout = factory.CreateLayout() };
            var window = new Window { Width = 1400, Height = 1000, Content = dock }; window.Show();
            try
            {
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Assert.False(tools.IsCreated(WorkspaceDockFactory.FullRawId));
                var marker = Assert.Single(document.Presentation!.Fragments, f => f.Marker is not null);
                for (var reopen = 0; reopen < 2; reopen++)
                {
                    // Match MainWindow: prepare the session while the dockable still has null Context.
                    var model = (FullRawInspectorViewModel)tools.GetContent(WorkspaceDockFactory.FullRawId);
                    model.Open(document, marker.Omitted, "OBX-5"); factory.ShowTool(WorkspaceDockFactory.FullRawId);
                    window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    var view = Assert.Single(dock.GetVisualDescendants().OfType<FullRawInspectorView>()); await view.Ready;
                    Assert.Equal(new string('B', 16384), view.Editor.Text);
                    var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.FullRawId)!;
                    var group = (IDock)tool.Owner!;
                    factory.FloatDockable(tool); window.UpdateLayout();
                    Assert.Single(factory.Root.Windows!);
                    // The fake native host has no visual root. Exercise its feature content in
                    // another headless root; OS DockControl hosting remains a native acceptance check.
                    var floatingContent = new DockContentTemplate().Build(tool);
                    var floatingWindow = new Window { Width = 1000, Height = 700, Content = floatingContent }; floatingWindow.Show();
                    try
                    {
                        floatingWindow.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                        var floatingView = Assert.Single(floatingContent.GetVisualDescendants().OfType<FullRawInspectorView>()); await floatingView.Ready;
                        Assert.StartsWith("BBBB", floatingView.Editor.Text);
                        floatingWindow.Content = null; floatingWindow.UpdateLayout();
                        factory.MoveDockable((IDock)tool.Owner!, group, tool, null);
                        window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                        view = Assert.Single(dock.GetVisualDescendants().OfType<FullRawInspectorView>()); await view.Ready; Assert.StartsWith("BBBB", view.Editor.Text);
                    }
                    finally { floatingWindow.Close(); factory.CloseFloatingWindows(); }
                    factory.CloseDockable(tool); window.UpdateLayout(); Assert.False(tools.IsCreated(tool.Id!));
                }
            }
            finally { window.Close(); documents.Close(document.Id); await document.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
