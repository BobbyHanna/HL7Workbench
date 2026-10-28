using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Desktop;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class FhirWorkspaceTests
{
    private static Type AppType => Environment.GetEnvironmentVariable("HL7WORKBENCH_RESPONSIVE_RENDER_DIRECTORY") is null ? typeof(RawHoverTestApplication) : typeof(ResponsiveRenderApplication);
    private static string Fixture => Path.Combine(AppContext.BaseDirectory, "Fixtures", "fhir-r4", "patient-alpha.json");
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static double BottomRatio(DockControl dock) =>
        dock.GetVisualDescendants().OfType<ToolDockControl>().Single(c => (c.DataContext as IDock)?.Id == "diagnostics-dock").Bounds.Height /
        dock.GetVisualDescendants().OfType<ProportionalDockControl>().Single(c => (c.DataContext as IDock)?.Id == "workspace-main").Bounds.Height;
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Capture(Window window, string name)
    {
        if (Environment.GetEnvironmentVariable("HL7WORKBENCH_RESPONSIVE_RENDER_DIRECTORY") is not { } directory) return;
        Directory.CreateDirectory(directory); Pump(window); AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame); var path = Path.Combine(directory, name + ".png"); frame.Save(path, Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
        Assert.True(new FileInfo(path).Length > 5000, "A blank headless frame is not rendered evidence.");
    }
    [Fact]
    public async Task WholeDockHierarchyKeepsShortBottomAndFloatingWorkspaceReachableAndRetainsClinicalState()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(async () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "fhir-hierarchy-synthetic-" + Guid.NewGuid().ToString("N"));
            var window = new MainWindow(directory) { Width = 900, Height = 600 }; window.Show();
            try
            {
                Pump(window); var dock = window.FindControl<DockControl>("WorkspaceDock")!; var factory = Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory);
                factory.HostWindowLocator![nameof(IDockWindow)] = () => new FakeHostWindow();
                factory.ShowTool(WorkspaceDockFactory.SyntheticPatientsId); Pump(window);
                var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.SyntheticPatientsId)!; var model = Assert.IsType<SyntheticPatientsViewModel>(tool.Context);
                var view = Assert.Single(dock.GetVisualDescendants().OfType<SyntheticPatientsView>());
                Assert.True(view.Bounds.Height < 200); Reach(window, view, view.EmptyImport); Capture(window, "fhir-bottom-empty");
                model.ChooseFilesAsync = () => Task.FromResult<IReadOnlyList<string>>([Fixture]); await model.ImportFilesAsync(); Pump(window);
                Reach(window, view, view.Search); view.Patients.SelectedItem = model.Session.PatientPage[0];
                view.Browse.SelectedIndex = 1; view.Encounters.SelectedItems!.Add(model.Session.EncounterPage[0]);
                view.Seed.Text = "91"; view.GenerationPanel.IsExpanded = true; await model.PreviewAsync(); Pump(window);
                Reach(window, view, view.Details); Reach(window, view, view.Preview); Reach(window, view, view.Generate); Capture(window, "fhir-bottom-generation");
                var data = model.Session.Data; var patient = model.Session.PatientKey; var preview = model.Session.Preview; var bottom = Assert.IsAssignableFrom<IDock>(tool.Owner);
                var center = Assert.IsAssignableFrom<IDock>(factory.FindDockable(factory.Root, d => d is IDocumentDock));
                factory.RemoveDockable(tool, false); factory.SplitToDock(center, tool, DockOperation.Right); Pump(window);
                view = Assert.Single(dock.GetVisualDescendants().OfType<SyntheticPatientsView>());
                Reach(window, view, view.Search); Reach(window, view, view.Generate); Assert.Same(preview, model.Session.Preview); Capture(window, "fhir-right-generation");
                factory.FloatDockable(tool); Pump(window); var content = new DockContentTemplate().Build(tool); var floating = new Window { Content = content, Width = 600, Height = 400 }; floating.Show();
                try
                {
                    Pump(floating); var replacement = Assert.Single(content.GetVisualDescendants().OfType<SyntheticPatientsView>());
                    foreach (var size in new[] { (600d, 400d), (250d, 250d), (1200d, 800d), (350d, 300d) })
                    {
                        floating.Width = size.Item1; floating.Height = size.Item2; Pump(floating);
                        Reach(floating, replacement, replacement.Search); Reach(floating, replacement, replacement.Generate);
                        Assert.Equal(1, replacement.Browse.SelectedIndex); Assert.Equal("91", replacement.Seed.Text); Assert.Single(model.Session.SelectedEncounters);
                        Assert.Same(data, model.Session.Data); Assert.Equal(patient, model.Session.PatientKey); Assert.Same(preview, model.Session.Preview);
                        Capture(floating, $"fhir-floating-{size.Item1}x{size.Item2}");
                    }
                    floating.Content = null; Pump(floating); factory.MoveDockable((IDock)tool.Owner!, bottom, tool, null); factory.SetActiveDockable(tool); Pump(window);
                }
                finally { floating.Close(); factory.CloseFloatingWindows(); }
                view = Assert.Single(dock.GetVisualDescendants().OfType<SyntheticPatientsView>()); Assert.Equal("91", view.Seed.Text); Assert.True(view.GenerationPanel.IsExpanded);
                factory.CloseDockable(tool); Pump(window); Assert.Null(model.Session.Data);
            }
            finally { window.Close(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task MissingOptionalClinicalDataAndRejectedPatientFreeImportsKeepExplicitStates()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(async () =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "fhir-empty-synthetic-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, "synthetic.json");
            using var documents = new DocumentManager(); using var model = new SyntheticPatientsViewModel(documents);
            var view = new SyntheticPatientsView(model); var window = new Window { Content = view, Width = 350, Height = 300 }; window.Show();
            try
            {
                await File.WriteAllTextAsync(path, "{\"resourceType\":\"Bundle\",\"type\":\"collection\",\"entry\":[{\"resource\":{\"resourceType\":\"Patient\",\"id\":\"SYN-EMPTY\"}}]}", Token);
                await model.Session.ImportFilesAsync([path], Token); view.Patients.SelectedItem = model.Session.PatientPage[0]; Pump(window);
                Assert.Contains("(not supplied)", view.Overview.Text!, StringComparison.Ordinal);
                view.Browse.SelectedIndex = 1; Pump(window); Assert.True(view.EncounterEmpty.IsVisible); Assert.False(view.Encounters.IsVisible);
                view.Browse.SelectedIndex = 2; Pump(window); Assert.True(view.TimelineEmpty.IsVisible); Assert.False(view.Timeline.IsVisible);
                var previous = model.Session.Data;
                await File.WriteAllTextAsync(path, "{\"resourceType\":\"Bundle\",\"type\":\"collection\",\"entry\":[{\"resource\":{\"resourceType\":\"Organization\",\"id\":\"SYN-ORG\"}}]}", Token);
                await model.Session.ImportFilesAsync([path], Token); Pump(window);
                Assert.Same(previous, model.Session.Data); Assert.Contains("failed", model.Status, StringComparison.OrdinalIgnoreCase);
                model.Clear(); Pump(window); Assert.True(view.EmptyState.IsVisible); Assert.False(view.WorkspaceBody.IsVisible); Assert.Equal("No patients loaded", view.EmptyTitle.Text);
                Capture(window, "fhir-no-patients");
            }
            finally { window.Close(); Directory.Delete(directory, true); }
            return true;
        }, Token);
    }
    private static void Reach(Window window, Control view, Control target)
    {
        Pump(window); target.BringIntoView(); Pump(window);
        var rect = new Rect(target.TranslatePoint(default, view)!.Value, target.Bounds.Size);
        Assert.True(rect.Width > 0 && rect.Height > 0 && rect.Top < view.Bounds.Height && rect.Bottom > 0, $"{target.GetType().Name}: {rect} outside {view.Bounds.Size}");
        foreach (var scroll in target.GetVisualAncestors().OfType<ScrollViewer>())
        {
            var viewport = new Rect(scroll.TranslatePoint(default, view)!.Value, scroll.Bounds.Size);
            Assert.True(rect.Intersects(viewport), $"{rect} clipped by scroll {viewport}");
        }
    }
    [Fact]
    public async Task FolderImportAndReplacementValidationNeedNoAcknowledgmentAndRemainSessionOnly()
    {
        var directory = Path.Combine(Path.GetTempPath(), "fhir-folder-synthetic-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            File.Copy(Fixture, Path.Combine(directory, "patient.json")); Directory.CreateDirectory(Path.Combine(directory, "nested"));
            File.WriteAllText(Path.Combine(directory, "nested", "bad.json"), "not-json");
            using var documents = new DocumentManager(); using var model = new SyntheticPatientsViewModel(documents);
            model.ChooseFolderAsync = () => Task.FromResult<string?>(directory); await model.ImportFolderAsync();
            var session = Assert.IsType<FhirImportSession>(model.Session.Data); Assert.Single(session.Patients); Assert.Equal(1, session.Summary.Files);
            File.WriteAllText(Path.Combine(directory, "bad.json"), "{invalid-synthetic-json"); await model.ImportFolderAsync(); Assert.Same(session, model.Session.Data);
            Assert.DoesNotContain("SYN-ALPHA", model.Status, StringComparison.Ordinal); Assert.Empty(documents.Documents);
            model.Clear(); Assert.Null(model.Session.Data);
        }
        finally { Directory.Delete(directory, true); }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task ActiveCancelIsDirectlyVisibleAndPreventsLatePickerImport(bool folder)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); using var model = new SyntheticPatientsViewModel(documents);
            var picker = new TaskCompletionSource();
            model.ChooseFilesAsync = async () => { await picker.Task; return [Fixture]; };
            model.ChooseFolderAsync = async () => { await picker.Task; return Path.GetDirectoryName(Fixture); };
            var view = new SyntheticPatientsView(model); var window = new Window { Content = view, Width = 350, Height = 300 }; window.Show();
            try
            {
                var pending = folder ? model.ImportFolderAsync() : model.ImportFilesAsync(); Pump(window);
                Assert.True(view.Cancel.IsVisible); Reach(window, view, view.Cancel); Click(view.Cancel);
                picker.SetResult(); await pending; Pump(window);
                Assert.Null(model.Session.Data); Assert.False(view.Cancel.IsVisible); Assert.Contains("canceled", model.Status, StringComparison.OrdinalIgnoreCase);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(1)] [InlineData(1.25)] [InlineData(1.5)]
    public async Task EmptySearchAndClinicalStatesAndGenerationSettingsSurviveScaledTransitions(double scale)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); using var model = new SyntheticPatientsViewModel(documents);
            var view = new SyntheticPatientsView(model); var host = new Border { Width = 600, Height = 400, Child = new LayoutTransformControl { Child = view, LayoutTransform = new ScaleTransform(scale, scale) } };
            var window = new Window { Content = new Grid { Children = { host } }, Width = 1300, Height = 950 }; window.Show();
            try
            {
                Pump(window); Assert.True(view.EmptyState.IsVisible); Assert.False(view.Patients.IsEffectivelyVisible); Assert.False(view.GenerationPanel.IsExpanded);
                await model.Session.ImportFilesAsync([Fixture], Token); Pump(window); Assert.True(view.SelectionPrompt.IsVisible);
                await model.Session.FilterPatientsAsync("NO-SYNTHETIC-MATCH", Token); Pump(window); Assert.True(view.PatientEmpty.IsVisible); Assert.False(view.Patients.IsVisible);
                await model.Session.FilterPatientsAsync("", Token); view.Patients.SelectedItem = model.Session.PatientPage[0]; view.GenerationPanel.IsExpanded = true;
                view.Seed.Text = "73"; view.Mode.SelectedIndex = 1; await model.PreviewAsync(); Pump(window); var preview = model.Session.Preview;
                Assert.Contains("Birth date:", view.Overview.Text!, StringComparison.Ordinal);
                foreach (var width in new[] { 1200d, 900, 600, 350, 250, 600, 900 })
                { host.Width = width; Pump(window); Reach(window, view, view.Search); Reach(window, view, view.Generate); Assert.True(view.Patients.Bounds.Height >= 120); }
                view.GenerationPanel.IsExpanded = false; Pump(window); view.GenerationPanel.IsExpanded = true; Pump(window);
                Assert.Equal("73", view.Seed.Text); Assert.Equal(1, view.Mode.SelectedIndex); Assert.Same(preview, model.Session.Preview);
                var selected = model.Session.PatientKey; host.Child = null; Pump(window); var replacement = new SyntheticPatientsView(model); host.Child = replacement; Pump(window);
                Assert.True(replacement.GenerationPanel.IsExpanded); Assert.Equal("73", replacement.Seed.Text); Assert.Equal(selected, model.Session.PatientKey); Assert.Same(preview, model.Session.Preview);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task SavedLayoutStartupKeepsCustomProportionsAndStableRenamedToolIdentity()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "fhir-saved-synthetic-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
            var first = new MainWindow(directory) { Width = 900, Height = 600 }; first.Show();
            try
            {
                Pump(first); var dock = first.FindControl<DockControl>("WorkspaceDock")!; var factory = Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory);
                factory.FindDockable(factory.Root, d => d.Id == "workspace-content")!.Proportion = .6;
                factory.FindDockable(factory.Root, d => d.Id == "diagnostics-dock")!.Proportion = .4; Pump(first);
                File.WriteAllText(Path.Combine(directory, "workspace-layout.json"), LayoutCodec.Encode(factory.Capture()));
            }
            finally { first.Close(); }
            var restored = new MainWindow(directory) { Width = 900, Height = 600 }; restored.Show();
            try
            {
                Pump(restored); var dock = restored.FindControl<DockControl>("WorkspaceDock")!; Assert.InRange(BottomRatio(dock), .36, .44);
                var factory = Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory); factory.ShowTool(WorkspaceDockFactory.SyntheticPatientsId); Pump(restored);
                Assert.Equal("tool.synthetic-patients", WorkspaceDockFactory.SyntheticPatientsId);
                Assert.Equal("FHIR Patient Workspace", factory.GetDockable<ITool>(WorkspaceDockFactory.SyntheticPatientsId)!.Title);
                Assert.Equal("FHIR Patient _Workspace", restored.FindControl<MenuItem>("SyntheticPatientsMenu")!.Header);
                Assert.NotEmpty(dock.GetVisualDescendants().OfType<SyntheticPatientsView>());
            }
            finally { restored.Close(); Directory.Delete(directory, true); }
        }, Token);
    }
    [Fact]
    public async Task FreshRestoredWindowMeasuresDefaultDockRatiosAgainstActualContent()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(AppType);
        await ui.Dispatch(() =>
        {
            var directory = Path.Combine(Path.GetTempPath(), "fhir-dock-synthetic-" + Guid.NewGuid().ToString("N"));
            var window = new MainWindow(directory) { Width = 900, Height = 600 }; window.Show();
            try
            {
                Pump(window); var dock = window.FindControl<DockControl>("WorkspaceDock")!;
                var factory = Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory);
                var frames = dock.GetVisualDescendants().OfType<Control>().Where(c => c.DataContext is IDock && c.Bounds.Width > 0).ToArray();
                var debug = string.Join("\n", frames.Select(c => $"{c.GetType().Name} {(c.DataContext as IDock)?.Id} {c.Bounds}"));
                var bottom = frames.OfType<ToolDockControl>().Single(c => (c.DataContext as IDock)?.Id == "diagnostics-dock");
                var main = frames.OfType<ProportionalDockControl>().Single(c => (c.DataContext as IDock)?.Id == "workspace-main");
                Assert.InRange(bottom.Bounds.Height / main.Bounds.Height, .23, .31);
                Assert.True(main.Bounds.Height <= dock.Bounds.Height + 1, debug);
                var upper = frames.OfType<ProportionalDockControl>().Single(c => (c.DataContext as IDock)?.Id == "workspace-content");
                Assert.True(upper.Bounds.Height > bottom.Bounds.Height * 2, debug);
                factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.ActivityLogId)!); Pump(window);
                factory.ShowTool(WorkspaceDockFactory.ActivityLogId); Pump(window);
                var reopenedBottom = dock.GetVisualDescendants().OfType<ToolDockControl>().Single(c => (c.DataContext as IDock)?.Id == "diagnostics-dock");
                var reopenedMain = dock.GetVisualDescendants().OfType<ProportionalDockControl>().Single(c => (c.DataContext as IDock)?.Id == "workspace-main");
                Assert.True(reopenedBottom.Bounds.Height / reopenedMain.Bounds.Height is > .23 and < .31,
                    $"After hiding/reopening: bottom={reopenedBottom.Bounds}, parent={reopenedMain.Bounds}; " + string.Join("; ", ((IDock)reopenedMain.DataContext!).VisibleDockables!.Select(d => $"{d.Id}: {d.Proportion}")));
                foreach (var size in new[] { (1200d, 800d), (900d, 600d), (720d, 480d), (900d, 600d) })
                { window.Width = size.Item1; window.Height = size.Item2; Pump(window); Assert.InRange(BottomRatio(dock), .23, .31); }
                var upperModel = factory.FindDockable(factory.Root, d => d.Id == "workspace-content")!;
                var bottomModel = factory.FindDockable(factory.Root, d => d.Id == "diagnostics-dock")!;
                upperModel.Proportion = .6; bottomModel.Proportion = .4; Pump(window);
                var saved = factory.Capture();
                dock.Layout = factory.CreateLayout(); var cancel = DefaultDockSizing.Arm(dock, factory);
                dock.Layout = factory.Restore(saved, []); Pump(window); cancel();
                Assert.InRange(BottomRatio(dock), .36, .44); // Queued defaults cannot overwrite a saved graph.
                window.Width = 1200; window.Height = 800; Pump(window); Assert.InRange(BottomRatio(dock), .36, .44);
                dock.Layout = factory.CreateLayout(); cancel = DefaultDockSizing.Arm(dock, factory); Pump(window); cancel();
                Assert.InRange(BottomRatio(dock), .23, .31);
            }
            finally { window.Close(); if (Directory.Exists(directory)) Directory.Delete(directory, true); }
        }, TestContext.Current.CancellationToken);
    }
}
