using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
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

public sealed class ComparisonTests
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261016000000||ORU^R01|SYN-1|T|2.5\r";
    private static MessageDocument Doc(string id, string body) => new(id, "Synthetic " + id, Hl7Parser.Parse(Header + body));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public async Task DraftUndoRedoAndIgnoreRecomputationNeverPublishStaleResults()
    {
        using var manager = new DocumentManager(); var doc = Doc("one", "PID|A\r"); manager.Open(doc); doc.IsEditMode = true;
        using var model = new ComparisonViewModel(manager); doc.ReplaceWorkingText(Header + "PID|B\r");
        model.CompareWithOriginal(); await model.Pending; Assert.Single(model.Differences); Assert.True(doc.CanUndo);
        doc.Undo(); Assert.Null(model.Comparison); Assert.Contains("stale", model.Status);
        model.Start(); await model.Pending; Assert.Equal("No differences found.", model.Status);
        doc.Redo(); Assert.Empty(model.Differences); model.Start(); await model.Pending; Assert.Single(model.Differences);
        model.SetOptions(new(true, true)); await model.Pending; Assert.Single(model.Differences); Assert.Contains("enabled", model.Count);
        doc.ReplaceWorkingText(Header.Replace("SYN-1", "SYN-2", StringComparison.Ordinal) + "PID|B\r");
        model.Start(); await model.Pending; Assert.Single(model.Differences);
        model.SetOptions(new()); await model.Pending; Assert.Equal(2, model.Differences.Count); Assert.True(doc.CanUndo); Assert.False(doc.CanRedo);
    }
    [Fact]
    public async Task RapidSwitchesCancelOldWorkAndRetainOnlyTheLatestSource()
    {
        using var manager = new DocumentManager(); var a = Doc("A", "PID|A\r"); var b = Doc("B", "PID|B\r"); var c = Doc("C", "PID|C\r"); manager.Open(a); manager.Open(b); manager.Open(c);
        using var model = new ComparisonViewModel(manager); var running = new List<Task>();
        for (var i = 0; i < 80; i++) { model.SetSources(new(a, 1), new(i % 2 == 0 ? b : c, 1)); model.Start(); running.Add(model.Pending); }
        await Task.WhenAll(running); Assert.Same(c, model.After!.Document); Assert.Equal("C", Assert.Single(model.Differences).AfterValue); Assert.False(model.IsBusy);
        model.Move(1); Assert.Same(model.Differences[0], model.Selected); model.Move(-1); Assert.Same(model.Differences[0], model.Selected);
        manager.Close(c.Id); Assert.Null(model.After); Assert.Null(model.Comparison); Assert.Empty(model.Differences);
        Assert.Contains("closed", model.Status); Assert.Same(a, manager.ActiveDocument);
    }
    [Fact]
    public async Task ClosingAndCancelingInFlightRequestsClearResultsAndReleaseDocumentLeases()
    {
        using var manager = new DocumentManager(); var doc = Doc("one", "PID|A\r"); manager.Open(doc);
        var model = new ComparisonViewModel(manager); model.SetSources(new(doc, 1), new(doc, 1));
        for (var i = 0; i < 30; i++) { model.Start(); var pending = model.Pending; model.Cancel(); await pending; Assert.Null(model.Comparison); }
        model.Start(); var running = model.Pending; model.Dispose(); await running; Assert.Null(model.Before); Assert.Null(model.Comparison);
        manager.Close(doc.Id); await doc.Disposal.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task TwoReadOnlyEditorsNavigateAndThemeChangesRetainSelectedSources(bool dark)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var manager = new DocumentManager(); var a = Doc("A", "PID|A\rOBX|FIRST\rOBX|SECOND\r"); var b = Doc("B", "PID|B\rOBX|FIRST\rOBX|INSERTED\rOBX|SECOND\r"); manager.Open(a); manager.Open(b);
            using var model = new ComparisonViewModel(manager); model.SetSources(new(a, 1), new(b, 1));
            var view = new ComparisonView(model); var window = new Window { Content = view, Width = 1100, Height = 800, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                await model.CompareAsync(); await view.Loading; window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.Equal(2, model.Differences.Count); Assert.Equal("A", view.BeforeEditor.SelectedText); Assert.Equal("B", view.AfterEditor.SelectedText);
                Assert.True(view.BeforeEditor.IsReadOnly); Assert.True(view.AfterEditor.IsReadOnly);
                view.BeforeEditor.TextArea.PerformTextInput("NO WRITE"); Assert.Equal(Header + "PID|A\rOBX|FIRST\rOBX|SECOND\r", view.BeforeEditor.Text);
                view.Next.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await view.Loading;
                Assert.Equal(Hl7DifferenceKind.Added, model.Selected!.Kind); Assert.Equal("OBX|INSERTED", view.AfterEditor.SelectedText);
                view.Previous.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await view.Loading;
                var comparison = model.Comparison; var selected = model.Selected; var before = model.Before; var text = view.BeforeEditor.Text;
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark; window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.Same(comparison, model.Comparison); Assert.Same(selected, model.Selected); Assert.Same(before, model.Before); Assert.Equal(text, view.BeforeEditor.Text);
                await view.CopyAsync("HL7 Path"); Assert.Equal("PID[1]-1", await window.Clipboard!.TryGetTextAsync());
                await view.CopyAsync("Before Value"); Assert.Equal("A", await window.Clipboard!.TryGetTextAsync());
                await view.CopyAsync("After Value"); Assert.Equal("B", await window.Clipboard!.TryGetTextAsync());
                await view.CopyAsync("Difference Summary"); Assert.Equal("Modified: PID[1]-1\nBefore: A\nAfter: B", await window.Clipboard!.TryGetTextAsync());
                view.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F7 }); Assert.Equal(Hl7DifferenceKind.Added, model.Selected!.Kind);
                Assert.False(a.IsDirty); Assert.False(b.IsDirty); Assert.False(a.CanUndo);
                var floating = new Window { Width = 1000, Height = 700 }; window.Content = null; floating.Content = view; floating.Show(); await view.Loading;
                Assert.Same(comparison, model.Comparison); Assert.Same(before, model.Before); floating.Close();
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task ComparisonStartsAtDocumentSizeAndCanCloseFloatRestoreAndReopenWithoutContentPersistence()
    {
        using var manager = new DocumentManager(); var doc = Doc("one", "PID|SECRET-SYNTHETIC\r"); manager.Open(doc);
        var tools = new ToolWindowRegistry(); tools.Register(new(WorkspaceDockFactory.ComparisonId, "Message Comparison", () => new ComparisonViewModel(manager), true, true));
        using var factory = new WorkspaceDockFactory(manager, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        factory.CreateLayout(); Assert.False(tools.IsCreated(WorkspaceDockFactory.ComparisonId)); factory.ShowTool(WorkspaceDockFactory.ComparisonId);
        var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!; Assert.IsAssignableFrom<IDocumentDock>(tool.Owner); Assert.True(tool.CanDockAsDocument); Assert.Same(doc, manager.ActiveDocument);
        var model = Assert.IsType<ComparisonViewModel>(tool.Context); Assert.Equal(ComparisonPresentation.SideBySide, model.Presentation);
        model.Presentation = ComparisonPresentation.Differences; model.CompareWithOriginal(); await model.Pending; Assert.NotNull(model.Comparison);
        model.SetPastedText(true, "PID|PASTE-A"); model.SetPastedText(false, "PID|PASTE-B"); await model.Pending;
        var json = LayoutCodec.Encode(factory.Capture()); Assert.DoesNotContain("SECRET-SYNTHETIC", json); Assert.DoesNotContain("originally loaded", json);
        Assert.DoesNotContain("PASTE-A", json); Assert.DoesNotContain("PASTE-B", json);
        factory.CloseDockable(tool); Assert.Null(model.Comparison); Assert.Null(tool.Context);
        Assert.Empty(model.InputA.PastedText); Assert.Empty(model.InputB.PastedText);
        factory.ShowTool(WorkspaceDockFactory.ComparisonId); Assert.IsAssignableFrom<IDocumentDock>(tool.Owner); Assert.NotSame(model, tool.Context);
        Assert.Equal(ComparisonPresentation.SideBySide, Assert.IsType<ComparisonViewModel>(tool.Context).Presentation);
        factory.FloatDockable(tool); var floating = Assert.Single(factory.Root.Windows!); floating.Host!.SetPosition(200, 120); floating.Host.SetSize(1000, 700);
        var saved = factory.Capture();
        try { LayoutCodec.Encode(saved); }
        catch (InvalidDataException e) { throw new InvalidDataException(System.Text.Json.JsonSerializer.Serialize(saved), e); }
        factory.CloseFloatingWindows(); factory.Restore(saved, [new(0, 0, 1920, 1080)]);
        Assert.False(tools.IsCreated(WorkspaceDockFactory.ComparisonId)); factory.ShowTool(WorkspaceDockFactory.ComparisonId);
        var restored = Assert.Single(factory.Root.Windows!); Assert.Equal(1000, restored.Width); Assert.Equal(700, restored.Height);
        var restoredModel = Assert.IsType<ComparisonViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!.Context);
        Assert.Null(restoredModel.Before); Assert.Equal(ComparisonPresentation.SideBySide, restoredModel.Presentation);
    }
    [Theory]
    [InlineData(ComparisonPresentation.Differences)] [InlineData(ComparisonPresentation.SideBySide)]
    public async Task ExistingSessionRetainsPresentationAndResultsThroughHideRevealFloatResizeAndDock(ComparisonPresentation presentation)
    {
        using var documents = new DocumentManager(); var tools = new ToolWindowRegistry();
        tools.Register(new(WorkspaceDockFactory.ComparisonId, "Message Comparison", () => new ComparisonViewModel(documents), true, true));
        using var factory = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.ComparisonId);
        var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!; var documentDock = Assert.IsAssignableFrom<IDocumentDock>(tool.Owner);
        var model = Assert.IsType<ComparisonViewModel>(tool.Context); model.Presentation = presentation;
        model.SetPastedText(true, Header + "PID|A\rOBX|ONE\r"); model.SetPastedText(false, Header + "PID|B\rOBX|TWO\r"); await model.Pending;
        model.SetOptions(new(IgnoreMsh10: true)); await model.Pending; model.Select(model.Differences[1]);
        var result = model.Comparison; var selected = model.Selected; var pending = model.Pending; var before = model.Before; var after = model.After;
        void AssertSession()
        {
            Assert.Same(model, tool.Context); Assert.Equal(presentation, model.Presentation); Assert.Same(result, model.Comparison); Assert.Same(selected, model.Selected);
            Assert.Same(before, model.Before); Assert.Same(after, model.After); Assert.Same(pending, model.Pending); Assert.True(model.Options.IgnoreMsh10);
            Assert.Equal(Header + "PID|A\rOBX|ONE\r", model.InputA.PastedText); Assert.Equal(Header + "PID|B\rOBX|TWO\r", model.InputB.PastedText);
        }
        factory.HideDockable(tool); Assert.Contains(tool, factory.Root.HiddenDockables!); AssertSession();
        factory.ShowTool(WorkspaceDockFactory.ComparisonId); Assert.DoesNotContain(tool, factory.Root.HiddenDockables!); AssertSession();
        factory.FloatDockable(tool); var floating = Assert.Single(factory.Root.Windows!); floating.Host!.SetSize(850, 600); AssertSession();
        floating.Host.SetSize(1100, 750); factory.ShowTool(WorkspaceDockFactory.ComparisonId); AssertSession();
        factory.MoveDockable((IDock)tool.Owner!, documentDock, tool, null); Assert.Same(documentDock, tool.Owner); AssertSession();
        factory.CloseDockable(tool); factory.ShowTool(WorkspaceDockFactory.ComparisonId);
        var fresh = Assert.IsType<ComparisonViewModel>(tool.Context); Assert.NotSame(model, fresh); Assert.Equal(ComparisonPresentation.SideBySide, fresh.Presentation);
        Assert.Empty(fresh.InputA.PastedText); Assert.Empty(fresh.InputB.PastedText); Assert.Null(fresh.Comparison);
    }
    [Fact]
    public async Task ContextMenuAndRibbonOpenTheSameWorkspaceAndKeepOriginalDraftHistory()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            Application.Current!.Styles.Add(new Dock.Avalonia.Themes.Fluent.DockFluentTheme());
            Application.Current.DataTemplates.Add(new DockContentTemplate { DockedInspection = true });
            var root = Path.Combine(Path.GetTempPath(), "hl7-comparison-shell-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var window = new MainWindow(root); window.Show();
            try
            {
                var control = window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!;
                var factory = Assert.IsType<WorkspaceDockFactory>(control.Layout!.Factory);
                var managerField = typeof(MainWindow).GetField("documents", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                var manager = (DocumentManager)managerField.GetValue(window)!; var doc = Doc("one", "PID|A\r"); manager.Open(doc); factory.OpenDocument(doc.Id);
                doc.IsEditMode = true; doc.ReplaceWorkingText(Header + "PID|B\r"); await doc.EditRefresh;
                var ribbon = Assert.IsType<UtilityRibbon>(window.FindControl<ContentControl>("RibbonHost")!.Content); ribbon.SelectTab(2);
                var commandControls = ribbon.TemporaryPopup.Child!.GetVisualDescendants().OfType<Button>().ToArray();
                var open = commandControls.Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Message Comparison"); open.Command!.Execute(null);
                var initial = Assert.IsType<ComparisonViewModel>(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!.Context); Assert.Null(initial.Before);
                ribbon.TemporaryPopup.IsOpen = false; window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var actualView = Assert.Single(window.GetVisualDescendants().OfType<ComparisonView>()); Assert.True(actualView.Bounds.Width > 500);
                var original = commandControls.Single(b => Avalonia.Automation.AutomationProperties.GetName(b) == "Compare With Original"); original.Command!.Execute(null); await initial.Pending;
                await actualView.Loading; Assert.Equal("A", actualView.BeforeEditor.SelectedText); Assert.Equal("B", actualView.AfterEditor.SelectedText);
                Assert.Single(initial.Differences); factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!);
                var view = Assert.IsType<MessageInspectionView>(MessageViewer.CreateActive(doc)); var host = new ContentControl { Content = view }; window.Content = host;
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var item = view.Editor.ContextMenu!.ItemsSource!.Cast<MenuItem>().Single(i => Equals(i.Header, "Compare With Original"));
                item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.ComparisonId)!; var model = Assert.IsType<ComparisonViewModel>(tool.Context); await model.Pending;
                Assert.Single(model.Differences); Assert.True(doc.IsDirty); Assert.True(doc.CanUndo); Assert.False(doc.CanRedo);
                manager.Close(doc.Id, discardChanges: true);
            }
            finally { window.Close(); Directory.Delete(root, true); }
            return true;
        }, Token);
    }
}
