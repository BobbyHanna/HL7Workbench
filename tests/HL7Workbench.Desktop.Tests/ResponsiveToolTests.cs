using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Desktop.Docking;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ResponsiveToolTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "responsive-synthetic-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Type AppType => Environment.GetEnvironmentVariable("HL7WORKBENCH_RESPONSIVE_RENDER_DIRECTORY") is null ? typeof(RawHoverTestApplication) : typeof(ResponsiveRenderApplication);
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "fhir-r4", "patient-alpha.json");
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button)
    { if (button.Command is { } command) command.Execute(button.CommandParameter); else button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }
    private static (Window Window, Border Host, TextBlock Tabs) Host(Control tool, double width, double height, bool dark = false, double scale = 1)
    {
        var tabs = new TextBlock { Text = "Dock tabs — protected outside content", Height = 24 };
        var box = new Border { Width = width, Height = height, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top, Child = scale == 1 ? tool : new LayoutTransformControl { Child = tool, LayoutTransform = new ScaleTransform(scale, scale) } };
        var grid = new Grid { RowDefinitions = new("Auto,Auto,Auto,*") };
        grid.Children.Add(new TextBlock { Text = "Tool header", Height = 24 }); Grid.SetRow(box, 1); grid.Children.Add(box); Grid.SetRow(tabs, 2); grid.Children.Add(tabs);
        var window = new Window { Content = grid, Width = 1300, Height = 950, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
        window.Show(); Pump(window); return (window, box, tabs);
    }
    private static void AssertBars(Control view)
    {
        foreach (var bar in view.GetVisualDescendants().OfType<AdaptiveActionBar>().Where(c => c.IsEffectivelyVisible && c.Bounds.Width > 0))
        {
            var boxes = bar.Children.Where(c => c.IsVisible && c.Bounds.Width > 0).Select(c => (Control: c, Rect: new Rect(c.TranslatePoint(default, bar)!.Value, c.Bounds.Size))).ToArray();
            foreach (var (_, box) in boxes)
            { Assert.InRange(box.X, -.1, bar.Bounds.Width + .1); Assert.InRange(box.Right, 0, bar.Bounds.Width + .5); Assert.InRange(box.Bottom, 0, bar.Bounds.Height + .5); }
            for (var i = 0; i < boxes.Length; i++) for (var j = i + 1; j < boxes.Length; j++)
            { var overlap = boxes[i].Rect.Intersect(boxes[j].Rect); Assert.True(overlap.Width <= .1 || overlap.Height <= .1, $"Actions overlap: {boxes[i].Control.GetType().Name} / {boxes[j].Control.GetType().Name}"); }
        }
    }
    private static void Reach(Window window, Control view, Control control)
    {
        Pump(window); // Expanders and async model changes must arrange before navigation.
        control.BringIntoView(); Pump(window);
        var rect = new Rect(control.TranslatePoint(default, view)!.Value, control.Bounds.Size);
        Assert.True(rect.Width > 0 && rect.Height > 0);
        Assert.InRange(rect.X, -.1, view.Bounds.Width + .1); Assert.InRange(rect.Right, 0, view.Bounds.Width + .5);
        Assert.True(rect.Bottom > 0 && rect.Top < view.Bounds.Height, $"{control.GetType().Name} {rect} outside {view.Bounds.Size}; " + string.Join("; ", control.GetVisualAncestors().OfType<ScrollViewer>().Select(s => $"scroll={s.Bounds} extent={s.Extent} viewport={s.Viewport} offset={s.Offset}")) + (view is SqlQueryView sql ? " rows=" + string.Join("; ", sql.LayoutSurface.Content.Children.Select(c => $"{Grid.GetRow(c)}:{c.GetType().Name} desired={c.DesiredSize} bounds={c.Bounds}")) : ""));
        foreach (var scroll in control.GetVisualAncestors().OfType<ScrollViewer>().Where(c => c != view))
        {
            var viewport = new Rect(scroll.TranslatePoint(default, view)!.Value, scroll.Bounds.Size);
            Assert.True(rect.Bottom > viewport.Top && rect.Top < viewport.Bottom, $"{control.GetType().Name} is clipped by its scroll viewport {viewport}: {rect}");
        }
        if (control.IsEffectivelyEnabled && control.Focusable)
        {
            var focusTarget = control is NumericUpDown ? control.GetVisualDescendants().OfType<TextBox>().First() : control;
            focusTarget.Focus(NavigationMethod.Tab);
            Assert.True(control.IsKeyboardFocusWithin, $"Keyboard focus must reach {control.GetType().Name}");
        }
    }
    private static void Render(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("HL7WORKBENCH_RESPONSIVE_RENDER_DIRECTORY") is not { } directory) return;
        Directory.CreateDirectory(directory); Pump(window);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
        var path = Path.Combine(directory, name + ".png"); frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Assert.True(new FileInfo(path).Length > 5000, "A blank fake headless frame is not visual evidence.");
    }
    private static void ReachMenu(Window window, Button anchor, Button action)
    {
        anchor.Flyout!.ShowAt(anchor); Pump(window);
        var popup = Assert.IsAssignableFrom<Control>(TopLevel.GetTopLevel(action)); popup.UpdateLayout();
        var rect = new Rect(action.TranslatePoint(default, popup)!.Value, action.Bounds.Size);
        Assert.True(rect.Width > 0 && rect.Height > 0); Assert.InRange(rect.Right, 0, popup.Bounds.Width + .5);
        Assert.True(action.Focus(NavigationMethod.Tab) || action.IsKeyboardFocusWithin);
        anchor.Flyout.Hide();
    }
    [Theory]
    [InlineData(1200, 800)] [InlineData(900, 600)] [InlineData(600, 400)] [InlineData(350, 300)] [InlineData(250, 250)]
    public async Task SyntheticWorkflowIsReachableInsideActualToolConstraints(double width, double height)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); using var model = new SyntheticPatientsViewModel(documents);
            model.ChooseFilesAsync = () => Task.FromResult<IReadOnlyList<string>>([Fixture]);
            var view = new SyntheticPatientsView(model); var s = Host(view, width, height);
            try
            {
                AssertBars(view); Assert.True(view.EmptyState.IsVisible); Assert.False(view.WorkspaceBody.IsVisible);
                Render(s.Window, $"fhir-{width}x{height}-empty");
                Assert.False(view.GenerationPanel.IsExpanded); Reach(s.Window, view, view.EmptyImport);
                ReachMenu(s.Window, view.Import, view.ImportFiles); Click(view.ImportFiles); await view.ActionTask; Pump(s.Window);
                Assert.Single(model.Session.PatientPage); Reach(s.Window, view, view.Search); view.Search.Text = "SYN-ALPHA";
                Click(view.Find); await view.ActionTask; view.Patients.SelectedItem = model.Session.PatientPage[0]; Pump(s.Window);
                Assert.NotNull(model.Session.PatientKey); Render(s.Window, $"fhir-{width}x{height}-overview"); view.GenerationPanel.IsExpanded = true; Pump(s.Window); Reach(s.Window, view, view.Scope);
                var patientKey = model.Session.PatientKey; var data = model.Session.Data;
                Reach(s.Window, view, view.Preview); Click(view.Preview); await view.ActionTask; Pump(s.Window); Assert.True(view.Generate.IsEnabled);
                Reach(s.Window, view, view.Generate); Click(view.Generate); await view.ActionTask; Assert.Single(documents.Documents);
                Reach(s.Window, view, view.Import); ReachMenu(s.Window, view.Import, view.ImportFolder); ReachMenu(s.Window, view.More, view.Clear);
                Assert.False(view.Cancel.IsVisible);
                Reach(s.Window, view, view.Patients); Assert.True(view.Patients.Bounds.Height >= ToolLayoutMetrics.ListHeight);
                Reach(s.Window, view, view.PatientPageStatus); Assert.False(view.PatientsPrevious.IsVisible); Assert.False(view.PatientsNext.IsVisible);
                view.Browse.SelectedIndex = 1; view.Encounters.SelectedItem = model.Session.EncounterPage[0]; Pump(s.Window);
                Reach(s.Window, view, view.Details); Reach(s.Window, view, view.Scope);
                var browseRect = new Rect(view.Browse.TranslatePoint(default, view)!.Value, view.Browse.Bounds.Size);
                var detailRect = new Rect(view.ClinicalPanel.TranslatePoint(default, view)!.Value, view.ClinicalPanel.Bounds.Size);
                Assert.True(browseRect.Bottom <= detailRect.Top + .5, $"Clinical tabs {browseRect} overlap details {detailRect}");
                var mainRect = new Rect(view.WorkspaceBody.TranslatePoint(default, view)!.Value, view.WorkspaceBody.Bounds.Size);
                var generationRect = new Rect(view.GenerationPanel.TranslatePoint(default, view)!.Value, view.GenerationPanel.Bounds.Size);
                Assert.True(mainRect.Bottom <= generationRect.Top + .5, $"Patient workspace {mainRect} overlaps generation {generationRect}");
                Assert.InRange(view.Bounds.Width, 0, width); Assert.InRange(view.Bounds.Height, 0, height);
                var bottom = view.TranslatePoint(new(0, view.Bounds.Height), s.Window)!.Value.Y;
                Assert.True(bottom <= s.Tabs.TranslatePoint(default, s.Window)!.Value.Y + .5);
                if (height <= 400) Assert.True(view.LayoutSurface.Scroll.Extent.Height > view.LayoutSurface.Scroll.Viewport.Height);
                AssertBars(view); Render(s.Window, $"synthetic-{width}x{height}-actions");
                Reach(s.Window, view, view.Generate); Render(s.Window, $"synthetic-{width}x{height}-generation");
                for (var n = 0; n < 4; n++) { s.Host.Width = n % 2 == 0 ? 900 : width; s.Host.Height = n % 2 == 0 ? 600 : height; Pump(s.Window); }
                Assert.Equal(patientKey, model.Session.PatientKey); Assert.Same(data, model.Session.Data);
                Assert.Equal("SYN-ALPHA", view.Search.Text); Assert.NotNull(view.Patients.SelectedItem);
            }
            finally { s.Window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(1200, 800)] [InlineData(900, 600)] [InlineData(600, 400)] [InlineData(350, 300)] [InlineData(250, 250)]
    public async Task SqlEditorResultsAndEveryOutputRemainReachableWithoutRecreation(double width, double height)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("MSH|^~\\&|SYN|||||||SYN|T|2.5.1\rPID|1||SYN-ID\r", 130))));
            await MessageFileReader.OpenFileAsync(documents, "Synthetic.hl7", input, Token);
            using var model = new SqlQueryViewModel(documents, new(Path.Combine(root, "queries")), options: new() { WorkerPath = Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll"), StorageRoot = Path.Combine(root, "sql") });
            var view = new SqlQueryView(model); var editor = view.Editor; var results = view.Results; var s = Host(view, width, height, true);
            try
            {
                Reach(s.Window, view, view.Run); Click(view.Run); await model.ActionTask; Pump(s.Window);
                Assert.NotNull(model.Results); Assert.Equal(100, model.Page.Count);
                Reach(s.Window, view, view.Editor); Assert.True(view.Editor.Bounds.Height >= ToolLayoutMetrics.EditorHeight);
                // The table is deliberately wider than the tool; its horizontal
                // viewport, rather than the complete data canvas, must fit.
                var tableScroll = view.Results.GetVisualAncestors().OfType<ScrollViewer>().First(v => v.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto);
                Reach(s.Window, view, tableScroll); Assert.True(view.Results.Bounds.Height >= 90);
                tableScroll.Offset = new(Math.Max(0, tableScroll.Extent.Width - tableScroll.Viewport.Width), 0); Pump(s.Window);
                if (width < 900) Assert.True(tableScroll.Offset.X > 0);
                Assert.InRange(view.Results.GetVisualDescendants().OfType<ListBoxItem>().Count(), 1, 99);
                foreach (var action in new Control[] { view.Cancel, view.Library, view.QueryName, view.Save, view.Rename, view.Delete, view.Scope, view.History, view.Copy, view.Navigate, view.Export, view.Extract, view.Next }) Reach(s.Window, view, action);
                Click(view.Next); await view.ActionTask; Assert.Equal(30, model.Page.Count);
                model.SelectCell(0, 2); Reach(s.Window, view, view.Inspect); Click(view.Inspect); await view.ActionTask;
                Reach(s.Window, view, view.FullValue); Assert.NotEmpty(view.FullValue.Text);
                view.Editor.Select(7, 10); var undo = view.Editor.Document.UndoStack;
                for (var n = 0; n < 4; n++) { s.Host.Width = n % 2 == 0 ? 600 : width; Pump(s.Window); AssertBars(view); }
                Assert.Same(editor, view.Editor); Assert.Same(results, view.Results); Assert.Equal(1, model.PageIndex);
                Assert.Equal(7, view.Editor.SelectionStart); Assert.Equal(10, view.Editor.SelectionLength); Assert.Same(undo, view.Editor.Document.UndoStack);
                var journal = model.Results; var selection = model.Selection.ToArray();
                s.Host.Child = null; Pump(s.Window); var replacement = new SqlQueryView(model); s.Host.Child = replacement; Pump(s.Window);
                Assert.Same(journal, model.Results); Assert.Equal(1, model.PageIndex); Assert.Equal(selection, model.Selection.ToArray());
                Assert.Equal(7, replacement.Editor.SelectionStart); Assert.Equal(10, replacement.Editor.SelectionLength); Assert.Same(undo, replacement.Editor.Document.UndoStack);
                Render(s.Window, $"sql-{width}x{height}");
            }
            finally { s.Window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(1200, 800)] [InlineData(900, 600)] [InlineData(600, 400)] [InlineData(350, 300)] [InlineData(250, 250)]
    public async Task ScriptActionsEditorSelectionAndUndoSurviveResizingAndViewHandoff(double width, double height)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            using var model = new ScriptEditorViewModel(documents, new ScriptLibrary(Path.Combine(root, "scripts")));
            await model.Session.NewAsync(() => Task.FromResult(UnsavedChangesDecision.Cancel));
            var view = new ScriptEditorView(model); var s = Host(view, width, height);
            try
            {
                view.Editor.Document.Insert(0, "// Synthetic responsive test\n"); view.Editor.Select(3, 9);
                var document = view.Editor.Document; var code = view.Editor.Text;
                Assert.True(document.UndoStack.CanUndo);
                foreach (var action in new Control[] { view.Run, view.Cancel, view.ExecutionScope, view.Library, view.New, view.Save, view.Actions, view.ApiReference, view.Editor }) Reach(s.Window, view, action);
                Assert.True(view.Editor.Bounds.Height >= ToolLayoutMetrics.EditorHeight); AssertBars(view);
                foreach (var size in new[] { (600d, 400d), (250d, 250d), (1200d, 800d), (width, height) })
                { s.Host.Width = size.Item1; s.Host.Height = size.Item2; Pump(s.Window); AssertBars(view); }
                Assert.Same(document, view.Editor.Document); Assert.Equal(code, view.Editor.Text);
                Assert.Equal(3, view.Editor.SelectionStart); Assert.Equal(9, view.Editor.SelectionLength);
                Render(s.Window, $"script-{width}x{height}");
                s.Host.Child = null; Pump(s.Window);
                var replacement = new ScriptEditorView(model); s.Host.Child = replacement; Pump(s.Window);
                Assert.Same(document, replacement.Editor.Document); Assert.Equal(3, replacement.Editor.SelectionStart); Assert.Equal(9, replacement.Editor.SelectionLength);
                document.UndoStack.Undo(); Assert.DoesNotContain("Synthetic responsive test", model.Session.Source, StringComparison.Ordinal);
                Assert.Equal(model.Session.Source, replacement.Editor.Text);
                s.Host.Child = null; Pump(s.Window);
                await model.Session.NewAsync(() => Task.FromResult(UnsavedChangesDecision.Discard));
                var differentScript = new ScriptEditorView(model); s.Host.Child = differentScript; Pump(s.Window);
                Assert.NotSame(document, differentScript.Editor.Document); // Same starter text, different authoritative draft identity.
                Assert.Equal(model.Session.Source, differentScript.Editor.Text);
            }
            finally { s.Window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task HeightOnlyChangesAndExpandingDetailsUpdateExtentWithoutUnboundedListRealization()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(() =>
        {
            var details = new Expander { Header = "Details", Content = new TextBox { Height = 240, AcceptsReturn = true } };
            var list = new ListBox { ItemsSource = Enumerable.Range(0, 10000).ToArray() };
            var tool = new Grid { RowDefinitions = new("Auto,*") }; tool.Children.Add(details); Grid.SetRow(list, 1); tool.Children.Add(list);
            var surface = ResponsiveToolSurface.Attach(tool, (1, ToolLayoutMetrics.ListHeight)); var s = Host(tool, 350, 300);
            try
            {
                foreach (var height in new[] { 800d, 250, 600, 300 })
                {
                    s.Host.Height = height; Pump(s.Window); Assert.InRange(surface.Scroll.Viewport.Height, height - 2, height + 2);
                    details.IsExpanded = true; Pump(s.Window); Reach(s.Window, tool, list);
                    Assert.InRange(list.GetVisualDescendants().OfType<ListBoxItem>().Count(), 1, 100);
                    Assert.True(surface.Scroll.Extent.Height >= 240 + ToolLayoutMetrics.ListHeight);
                    details.IsExpanded = false; Pump(s.Window);
                }
            }
            finally { s.Window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task ActualDockContentAdaptsBetweenBottomRightAndFloatingRootsWithoutLosingDraftUndo()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse("MSH|^~\\&|SYN\rPID|1||SYN-ID\r"));
            using var model = new ScriptEditorViewModel(documents, new ScriptLibrary(Path.Combine(root, "dock-scripts")));
            await model.Session.NewAsync(() => Task.FromResult(UnsavedChangesDecision.Cancel));
            var tools = new ToolWindowRegistry(); tools.Register(new(WorkspaceDockFactory.DiagnosticsId, "Activity Log", () => new object()));
            tools.Register(new(WorkspaceDockFactory.ScriptEditorId, "Script Editor", () => model));
            using var factory = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
            var dock = new DockControl { Layout = factory.CreateLayout() };
            var window = new Window { Width = 900, Height = 600, Content = dock }; window.Show();
            try
            {
                factory.ShowTool(WorkspaceDockFactory.ScriptEditorId); Pump(window);
                var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.ScriptEditorId)!;
                var bottom = Assert.IsAssignableFrom<IToolDock>(tool.Owner); Assert.Equal(Alignment.Bottom, bottom.Alignment);
                var view = Assert.Single(dock.GetVisualDescendants().OfType<ScriptEditorView>());
                view.Editor.Document.Insert(0, "// Synthetic dock handoff\n"); view.Editor.Select(3, 8);
                var document = view.Editor.Document; AssertBars(view); Reach(window, view, view.Save);
                var center = factory.FindDockable(factory.Root, item => item is IDocumentDock) as IDock;
                Assert.NotNull(center); factory.RemoveDockable(tool, false); factory.SplitToDock(center, tool, DockOperation.Right); Pump(window);
                view = Assert.Single(dock.GetVisualDescendants().OfType<ScriptEditorView>()); AssertBars(view);
                Assert.Same(document, view.Editor.Document); Assert.Equal(3, view.Editor.SelectionStart);
                var right = Assert.IsAssignableFrom<IDock>(tool.Owner);
                factory.FloatDockable(tool); Pump(window); Assert.Single(factory.Root.Windows!);
                // FakeHostWindow verifies Dock ownership. A real headless Window
                // measures floating content; native chrome/DPI remains manual.
                var floatingContent = new DockContentTemplate().Build(tool); var floating = new Window { Content = floatingContent, Width = 600, Height = 400 }; floating.Show();
                try
                {
                    Pump(floating); view = Assert.Single(floating.GetVisualDescendants().OfType<ScriptEditorView>());
                    foreach (var size in new[] { (250d, 250d), (900d, 600d), (350d, 300d) })
                    { floating.Width = size.Item1; floating.Height = size.Item2; Pump(floating); AssertBars(view); Reach(floating, view, view.Save); }
                    Assert.Same(document, view.Editor.Document); Assert.Equal(8, view.Editor.SelectionLength);
                    floating.Content = null; Pump(floating); factory.MoveDockable((IDock)tool.Owner!, bottom, tool, null); factory.SetActiveDockable(tool); Pump(window);
                }
                finally { floating.Close(); factory.CloseFloatingWindows(); }
                view = Assert.Single(dock.GetVisualDescendants().OfType<ScriptEditorView>()); Assert.Same(document, view.Editor.Document);
                factory.CloseDockable(tool); Pump(window); factory.ShowTool(tool.Id!); Pump(window);
                view = Assert.Single(dock.GetVisualDescendants().OfType<ScriptEditorView>()); Assert.Same(document, view.Editor.Document);
                Assert.Contains("Synthetic dock handoff", model.Session.Source, StringComparison.Ordinal);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(1)] [InlineData(1.25)] [InlineData(1.5)]
    public async Task LayoutScalingAndRepeatedThresholdCrossingKeepActionsFiniteAndFocusReachable(double scale)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(() =>
        {
            var fields = new StackPanel(); var input = new TextBox(); var field = new ResponsiveFormField("Long wrapped field label that stays associated with its input", input); fields.Children.Add(field);
            var longAction = new Button { Content = "A deliberately long action label remains readable at increased scale" };
            var bars = new AdaptiveActionBar(new Button { Content = "Primary" }, longAction, new ComboBox { Width = 350 });
            var list = new ListBox { ItemsSource = Enumerable.Range(0, 5000).ToArray() };
            var tool = new Grid { RowDefinitions = new("Auto,*") }; tool.Children.Add(fields); Grid.SetRow(list, 1); tool.Children.Add(list);
            var surface = ResponsiveToolSurface.Attach(tool, bars, (1, ToolLayoutMetrics.ListHeight)); var s = Host(tool, 350, 300, scale: scale);
            try
            {
                foreach (var width in new[] { 600d, 599, 400, 399, 250, 180, 600, 1200, 350 })
                {
                    s.Host.Width = width; Pump(s.Window); AssertBars(tool); Reach(s.Window, tool, input);
                    Assert.True(list.Bounds.Height >= ToolLayoutMetrics.ListHeight);
                    Assert.InRange(list.GetVisualDescendants().OfType<ListBoxItem>().Count(), 1, 100);
                    var extent = surface.Scroll.Extent; Pump(s.Window); Assert.Equal(extent, surface.Scroll.Extent);
                }
                Assert.Same(field.Label, Avalonia.Automation.AutomationProperties.GetLabeledBy(input));
                Reach(s.Window, tool, longAction);
            }
            finally { s.Window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task LargeValueInspectorChunkActionsRemainReachableWithFiniteTreeViewport()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rOBX|1|TX|||" + new string('S', 20000) + "\r"));
            var document = await MessageFileReader.OpenFileAsync(documents, "Synthetic large value", input, Token);
            var view = new InspectionPanes(document.Inspection, document, InspectionPaneKind.Inspector); var s = Host(view, 250, 250);
            try
            {
                document.Inspection.SelectSegment(document.Inspection.Segments[1]); document.Inspection.InspectField(5); await view.LoadPreviewAsync(0); Pump(s.Window);
                var selected = document.Inspection.SelectedNode; Assert.True(view.ValueNavigation.IsVisible);
                foreach (var control in view.ValueNavigation.Children.Where(c => c.IsVisible)) Reach(s.Window, view, control);
                Assert.True(view.Inspector.Bounds.Height >= ToolLayoutMetrics.InspectorHeight); AssertBars(view);
                await view.LoadPreviewAsync(16384); Assert.Equal(16384, view.ValueOffset);
                foreach (var width in new[] { 1200d, 600, 350, 250 }) { s.Host.Width = width; Pump(s.Window); }
                Assert.Equal(16384, view.ValueOffset); Assert.Same(selected, document.Inspection.SelectedNode);
                Reach(s.Window, view, view.PreviewOffset); Render(s.Window, "inspector-large-250x250");
            }
            finally { s.Window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task OuterOverflowRespondsToWheelKeyboardAndFocusedControlNavigation()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(() =>
        {
            var fields = new StackPanel(); var inputs = Enumerable.Range(0, 20).Select(i => new TextBox { Text = $"Synthetic input {i}" }).ToArray();
            foreach (var input in inputs) fields.Children.Add(new ResponsiveFormField("Input", input));
            var tool = new Grid { RowDefinitions = new("Auto") }; tool.Children.Add(fields);
            var surface = ResponsiveToolSurface.Attach(tool); var s = Host(tool, 250, 250);
            try
            {
                Assert.True(surface.Scroll.Extent.Height > surface.Scroll.Viewport.Height);
                var point = surface.Scroll.TranslatePoint(new(220, 100), s.Window)!.Value;
                s.Window.MouseWheel(point, new(0, -3)); Pump(s.Window); Assert.True(surface.Scroll.Offset.Y > 0);
                Assert.True(surface.Scroll.Focus()); var before = surface.Scroll.Offset.Y;
                s.Window.KeyPress(Key.PageDown, Avalonia.Input.RawInputModifiers.None, PhysicalKey.PageDown, null);
                s.Window.KeyRelease(Key.PageDown, Avalonia.Input.RawInputModifiers.None, PhysicalKey.PageDown, null); Pump(s.Window); Assert.True(surface.Scroll.Offset.Y > before);
                Reach(s.Window, tool, inputs[^1]); Assert.True(inputs[^1].IsFocused); Assert.True(surface.Scroll.Offset.Y > 500);
                Reach(s.Window, tool, inputs[0]); Assert.True(inputs[0].IsFocused); Assert.InRange(surface.Scroll.Offset.Y, 0, 50);
                for (var index = 1; index < inputs.Length; index++)
                {
                    s.Window.KeyPress(Key.Tab, Avalonia.Input.RawInputModifiers.None, PhysicalKey.Tab, null);
                    s.Window.KeyRelease(Key.Tab, Avalonia.Input.RawInputModifiers.None, PhysicalKey.Tab, null); Pump(s.Window);
                    Assert.True(inputs[index].IsFocused, "Tab order follows the original form control order without trapping focus in scrolling content.");
                }
            }
            finally { s.Window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task InspectorNavigatorAndActivityActionsWrapInANarrowShortHost()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(() =>
        {
            using var documents = new DocumentManager(); var doc = MessageFileReader.Open(documents, "Synthetic", Hl7Parser.Parse("MSH|^~\\&|SYN\rPID|1||SYN-ID||Family^Given\r"));
            using var activity = new ActivityLogViewModel(documents.Activity);
            var inspector = new InspectionPanes(doc.Inspection, doc, InspectionPaneKind.Inspector); var s = Host(inspector, 250, 250);
            try
            {
                doc.Inspection.SelectSegment(doc.Inspection.Segments[1]); inspector.UpdateInspection(doc.Inspection); Pump(s.Window);
                Reach(s.Window, inspector, inspector.FieldNumber); Reach(s.Window, inspector, inspector.InspectFieldButton); AssertBars(inspector);
                Render(s.Window, "inspector-250x250");
                var selectedSegment = doc.Inspection.SelectedSegment;
                documents.Activity.Record(ActivityKind.ApplicationReady); Pump(s.Window);
                var log = new ActivityLogView(activity); s.Host.Child = log; Pump(s.Window); AssertBars(log);
                Reach(s.Window, log, log.Severity); Reach(s.Window, log, log.Copy); Reach(s.Window, log, log.Clear);
                Assert.True(log.Entries.Bounds.Height >= ToolLayoutMetrics.ListHeight); Render(s.Window, "activity-250x250");
                log.Severity.SelectedIndex = 1; log.Entries.SelectedItem = activity.Entries.FirstOrDefault(); var selectedEntry = log.Entries.SelectedItem;
                Assert.NotNull(selectedEntry);
                foreach (var width in new[] { 1200d, 600, 350, 250 }) { s.Host.Width = width; Pump(s.Window); }
                Assert.Equal(1, log.Severity.SelectedIndex); Assert.Equal(selectedEntry, log.Entries.SelectedItem);
                s.Host.Child = null; Pump(s.Window); var replacementLog = new ActivityLogView(activity); s.Host.Child = replacementLog; Pump(s.Window);
                Assert.Equal(1, replacementLog.Severity.SelectedIndex); Assert.Same(selectedEntry, replacementLog.Entries.SelectedItem);
                var navigator = new InspectionPanes(doc.Inspection, doc, InspectionPaneKind.Navigator); s.Host.Child = navigator; Pump(s.Window);
                Assert.True(navigator.Navigator.Bounds.Height >= ToolLayoutMetrics.ListHeight); Reach(s.Window, navigator, navigator.Navigator);
                Assert.Same(selectedSegment, doc.Inspection.SelectedSegment);
            }
            finally { s.Window.Close(); }
            return true;
        }, Token);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}

public sealed class ResponsiveRenderApplication : App
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ResponsiveRenderApplication>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
