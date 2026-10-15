using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class DockingTests
{
    private static (WorkspaceDockFactory Factory, DocumentManager Documents, DiagnosticsViewModel Diagnostics) Create()
    {
        var documents = new DocumentManager();
        documents.Open(new("synthetic.one", "Synthetic one", Hl7Parser.Parse("MSH|^~\\&|SYN|||||||SYN-1||2.5\r")));
        documents.Open(new("synthetic.two", "Synthetic two", Hl7Parser.Parse("PID|2")));
        var diagnostics = new DiagnosticsViewModel(documents);
        var tools = new ToolWindowRegistry();
        tools.Register(new(WorkspaceDockFactory.BrowserId, "Message Browser", () => new MessageBrowserViewModel(documents)));
        tools.Register(new(WorkspaceDockFactory.DiagnosticsId, "Diagnostics", () => diagnostics));
        var factory = new WorkspaceDockFactory(documents, tools);
        factory.HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() };
        factory.CreateLayout();
        // These compatibility cases exercise two visible tools. Browser is now
        // hidden in a fresh workspace, so explicitly open it for these scenarios.
        factory.ShowTool(WorkspaceDockFactory.BrowserId);
        return (factory, documents, diagnostics);
    }
    [Fact]
    public void DocumentTabsAndToolsAreSeparateAndToolFocusRetainsDocument()
    {
        var (factory, documents, diagnostics) = Create();
        using (diagnostics)
        {
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId)!;
            Assert.False(tool.CanDockAsDocument);
            Assert.True(tool.CanFloat);
            factory.OpenDocument("synthetic.one");
            factory.SetActiveDockable(tool);
            Assert.Equal("synthetic.one", documents.ActiveDocument?.Id);
            Assert.Equal("Synthetic one", diagnostics.ActiveTitle);
            factory.OpenDocument("synthetic.two");
            Assert.Contains(diagnostics.Diagnostics, d => d.Code == "MissingMsh");
        }
    }
    [Fact]
    public void HiddenToolReopensWithoutChangingActiveDocument()
    {
        var (factory, documents, diagnostics) = Create();
        using (diagnostics)
        {
            var active = documents.ActiveDocument;
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId)!;
            factory.CloseDockable(tool);
            Assert.Contains(tool, factory.Root.HiddenDockables!);
            factory.ShowTool(tool.Id!);
            Assert.DoesNotContain(tool, factory.Root.HiddenDockables!);
            Assert.NotNull(tool.Owner);
            Assert.Same(active, documents.ActiveDocument);
        }
    }
    [Fact]
    public void FloatingToolPreservesContentAndRoundTripsGeometryAndResizedProportions()
    {
        var (factory, documents, diagnostics) = Create();
        using (diagnostics)
        {
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId)!;
            var browser = factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId)!;
            browser.Owner!.Proportion = .31;
            var active = documents.ActiveDocument;
            factory.FloatDockable(tool);
            var window = Assert.Single(factory.Root.Windows!);
            Assert.IsType<FakeHostWindow>(window.Host);
            Assert.Same(diagnostics, tool.Context);
            window.Host!.SetPosition(10000, 10000);
            window.Host.SetSize(640, 480);
            var saved = LayoutCodec.Decode(LayoutCodec.Encode(factory.Capture()));
            factory.CloseFloatingWindows();
            factory.Restore(saved, [new(0, 0, 1920, 1080)]);
            var restored = Assert.Single(factory.Root.Windows!);
            Assert.True(Assert.IsType<FakeHostWindow>(restored.Host).Presented);
            Assert.Equal(40, restored.X);
            Assert.Equal(40, restored.Y);
            Assert.Equal(640, restored.Width);
            Assert.Equal(.31, factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId)!.Owner!.Proportion);
            Assert.Same(active, documents.ActiveDocument);
        }
    }

    [Fact]
    public void MultipleToolsCanFloatAndReturnToAToolGroup()
    {
        var (factory, documents, diagnostics) = Create();
        using (diagnostics)
        {
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId)!;
            var browser = factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId)!;
            var context = documents.ActiveDocument;

            factory.FloatDockable(tool);
            factory.FloatDockable(browser);

            Assert.Equal(2, factory.Root.Windows!.Count);
            var hosts = factory.Root.Windows.Select(window => Assert.IsType<FakeHostWindow>(window.Host)).ToArray();
            Assert.Same(context, documents.ActiveDocument);

            var target = (IDock)tool.Owner!;
            factory.MoveDockable((IDock)browser.Owner!, target, browser, tool);

            Assert.Same(target, browser.Owner);
            Assert.Equal(2, target.VisibleDockables!.Count);
            Assert.Same(context, documents.ActiveDocument);

            factory.CloseFloatingWindows();
            Assert.All(hosts, host => Assert.True(host.Exited));
        }
    }

    [Fact]
    public void RepeatedDocumentSwitchingAndClosingKeepsContextConsistent()
    {
        var (factory, documents, diagnostics) = Create();
        using (diagnostics)
        {
            var first = factory.GetDockable<IDocument>("synthetic.one")!;
            var second = factory.GetDockable<IDocument>("synthetic.two")!;
            for (var i = 0; i < 50; i++)
            {
                factory.OpenDocument(i % 2 == 0 ? "synthetic.one" : "synthetic.two");
                Assert.Same(i % 2 == 0 ? first.Context : second.Context, documents.ActiveDocument);
            }

            factory.CloseDockable(first);
            Assert.Equal("synthetic.two", documents.ActiveDocument?.Id);
            factory.CloseDockable(second);
            Assert.Null(documents.ActiveDocument);
            Assert.Empty(documents.Documents);
            Assert.NotNull(factory.Root);
        }
    }

    [Fact]
    public void DisposedFeatureModelsStopReceivingDocumentCallbacks()
    {
        var (factory, documents, diagnostics) = Create();
        using (diagnostics)
        using (var browser = new MessageBrowserViewModel(documents))
        {
            var diagnosticChanges = 0;
            var browserChanges = 0;
            diagnostics.PropertyChanged += (_, _) => diagnosticChanges++;
            browser.PropertyChanged += (_, _) => browserChanges++;
            diagnostics.Dispose();
            browser.Dispose();

            documents.Activate("synthetic.one");

            Assert.Equal(0, diagnosticChanges);
            Assert.Equal(0, browserChanges);
        }

        factory.Dispose();
    }
    [Fact]
    public void ClosingDocumentUpdatesContextAndLayoutDoesNotRecreateClosedDocuments()
    {
        var (factory, documents, diagnostics) = Create();
        using (diagnostics)
        {
            var saved = factory.Capture();
            factory.CloseDockable(factory.GetDockable<IDocument>("synthetic.two"));
            Assert.Equal("synthetic.one", documents.ActiveDocument?.Id);
            factory.Restore(saved, []);
            Assert.Single(documents.Documents);
            Assert.Null(factory.GetDockable<IDocument>("synthetic.two"));
        }
    }
    [Fact]
    public void HiddenToolReopensAfterLayoutRestore()
    {
        var (factory, documents, diagnostics) = Create();
        using (diagnostics)
        {
            factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId));
            var saved = LayoutCodec.Decode(LayoutCodec.Encode(factory.Capture()));
            factory.Restore(saved, []);
            factory.ShowTool(WorkspaceDockFactory.DiagnosticsId);
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId)!;
            Assert.DoesNotContain(tool, factory.Root.HiddenDockables!);
            Assert.Contains(tool, ((IDock)tool.Owner!).VisibleDockables!);
        }
    }
    [Fact]
    public void ToolsMoveBetweenDockGroupsWithoutBecomingDocuments()
    {
        var (factory, documents, diagnostics) = Create();
        using (diagnostics)
        {
            var browser = factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId)!;
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId)!;
            var target = (IDock)browser.Owner!;
            factory.MoveDockable((IDock)tool.Owner!, target, tool, browser);
            Assert.Same(target, tool.Owner);
            Assert.Equal(2, target.VisibleDockables!.Count);
            var saved = factory.Capture();
            factory.Restore(saved, []);
            Assert.Same(factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId)!.Owner,
                factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId)!.Owner);
        }
    }
    [Fact]
    public void BothHiddenToolsReopenAfterRestoreOfCollapsedWorkspace()
    {
        var (factory, documents, diagnostics) = Create();
        using (diagnostics)
        {
            factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId));
            factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId));
            factory.Restore(LayoutCodec.Decode(LayoutCodec.Encode(factory.Capture())), []);
            factory.ShowTool(WorkspaceDockFactory.BrowserId);
            factory.ShowTool(WorkspaceDockFactory.DiagnosticsId);
            Assert.Empty(factory.Root.HiddenDockables!);
            Assert.NotNull(factory.GetDockable<ITool>(WorkspaceDockFactory.BrowserId)!.Owner);
        }
    }
    [Fact]
    public void UnknownToolIdentityIsOmittedAndBrowserSelectionTracksActiveDocument()
    {
        var (factory, documents, diagnostics) = Create();
        using (diagnostics)
        {
            using var browser = new MessageBrowserViewModel(documents);
            browser.OpenRequested += factory.OpenDocument;
            browser.SelectedDocument = documents.Documents[0];
            Assert.Same(documents.Documents[0], documents.ActiveDocument);
            factory.OpenDocument("synthetic.two");
            Assert.Same(documents.ActiveDocument, browser.SelectedDocument);
            var saved = factory.Capture();
            factory.Restore(saved with { HiddenTools = ["tool.future-unknown"] }, []);
            Assert.Equal(2, documents.Documents.Count);
            Assert.Null(factory.GetDockable<ITool>("tool.future-unknown"));
        }
    }
    [Fact]
    public void FeatureModelsHaveNoDockBaseClasses()
    {
        Assert.Equal(typeof(object), typeof(DiagnosticsViewModel).BaseType);
        Assert.Equal(typeof(object), typeof(MessageBrowserViewModel).BaseType);
    }
}

// Tests exercise the real Dock factory/model with a deterministic native-host substitute.
// Pointer gestures, OS windows, and DPI behavior remain desktop acceptance checks.
internal sealed class FakeHostWindow : IHostWindow
{
    public bool Presented { get; private set; }
    public bool Exited { get; private set; }
    public IHostWindowState? HostWindowState => null;
    public bool IsTracked { get; set; }
    public IDockWindow? Window { get; set; }
    private double x = 40, y = 40, width = 600, height = 400;
    private DockWindowState state;
    public void Present(bool isDialog) => Presented = true;
    public void Exit() => Exited = true;
    public void SetPosition(double x, double y) { this.x = x; this.y = y; }
    public void GetPosition(out double x, out double y) { x = this.x; y = this.y; }
    public void SetSize(double width, double height) { this.width = width; this.height = height; }
    public void GetSize(out double width, out double height) { width = this.width; height = this.height; }
    public void SetWindowState(DockWindowState windowState) => state = windowState;
    public DockWindowState GetWindowState() => state;
    public void SetTitle(string? title) { }
    public void SetLayout(IDock layout) { }
    public void SetActive() { }
}
