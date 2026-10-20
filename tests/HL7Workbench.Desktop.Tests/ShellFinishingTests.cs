using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed partial class ScriptEditorTests
{
    private static Dictionary<string, ICommand> RibbonActions() => new[] { "Open File…", "Save HL7", "Save HL7 As…", "Save Layout", "Restore Layout", "Reset Layout / Recover Windows", "Preferences…", "Generate Messages",
        "Message Browser", "Segment Navigator", "Field Inspector", "Full Source Viewer", "Activity Log", "Find Messages", "Clear Search / Restore Navigator", "Message Comparison", "Compare With Original" }
        .Concat(MessageInsertionWorkflow.Choices.Select(c => MessageInsertionWorkflow.CommandName(c.Position)))
        .ToDictionary(n => n, _ => (ICommand)new UiCommand(() => Task.CompletedTask));
    private static void SendShellKey(Control control, Avalonia.Input.Key key) => control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });

    [Fact]
    public async Task ActualMainWindowHasOnlyFileSettingsAndViewMenusWithExistingToolsAndShortcuts()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var files = new Files(); var window = new MainWindow(files.Root); window.Show();
            try
            {
                var menu = window.FindControl<Menu>("MainMenu")!;
                var menus = menu.Items.Cast<MenuItem>().ToArray();
                Assert.Equal(new[] { "_File", "_Settings", "_View" }, menus.Select(i => i.Header));
                Assert.Null(window.FindControl<MenuItem>("ScriptsMenu"));
                Assert.Equal(new[] { "Message _Browser", "_Activity Log", "_Segment Navigator", "_Field Inspector", "Script _Editor", "_Find Messages", "Full _Source Viewer" },
                    menus[2].Items.Cast<MenuItem>().Select(i => i.Header));
                var file = menus[0].Items.Cast<MenuItem>().ToArray();
                Assert.Equal(Key.S, file[1].InputGesture!.Key); Assert.Equal(KeyModifiers.Control, file[1].InputGesture!.KeyModifiers);
                Assert.Equal(Key.S, file[2].InputGesture!.Key); Assert.Equal(KeyModifiers.Control | KeyModifiers.Shift, file[2].InputGesture!.KeyModifiers);
                var ribbon = Assert.IsType<UtilityRibbon>(window.FindControl<ContentControl>("RibbonHost")!.Content);
                Assert.Equal(new[] { "Home", "Scripts", "HL7 Tools", "TCP/MLLP" }, ribbon.Tabs.Select(t => t.Content));
                Assert.False(ribbon.IsExpanded);
                Assert.False(Directory.Exists(Path.Combine(files.Root, "Scripts")));
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task CollapsedShellRibbonOpensEditorAndExecutesAllFormerWindowMenuCommands()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var files = new Files(); var window = new MainWindow(files.Root); window.Show();
            try
            {
                var ribbon = Assert.IsType<UtilityRibbon>(window.FindControl<ContentControl>("RibbonHost")!.Content);
                var dock = window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!;
                var factory = Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory);
                window.UpdateLayout(); var height = dock.Bounds.Height;
                void Click(Control control)
                {
                    var point = Center(control, window); window.MouseMove(point);
                    window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
                    window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                }
                Button Command(string name) => RibbonButtons(ribbon.TemporaryPopup.Child!).Single(b => AutomationProperties.GetName(b) == name);
                Assert.Contains(factory.GetDockable<Dock.Model.Controls.ITool>(WorkspaceDockFactory.ScriptEditorId),
                    Assert.IsAssignableFrom<Dock.Model.Controls.IRootDock>(dock.Layout).HiddenDockables!);
                Click(ribbon.Tabs[1]); Assert.True(ribbon.TemporaryPopup.IsOpen); Assert.Equal(height, dock.Bounds.Height);
                Click(Command("Open Script Editor")); Assert.False(ribbon.TemporaryPopup.IsOpen);
                var editor = factory.GetDockable<Dock.Model.Controls.ITool>(WorkspaceDockFactory.ScriptEditorId)!;
                var model = Assert.IsType<ScriptEditorViewModel>(editor.Context);
                Assert.Same(editor, Assert.IsAssignableFrom<Dock.Model.Controls.IToolDock>(editor.Owner).ActiveDockable);
                Click(ribbon.Tabs[0]); Assert.True(ribbon.TemporaryPopup.IsOpen);
                Assert.False(Command("Save HL7").IsEffectivelyEnabled);
                Assert.False(Command("Save HL7 As…").IsEffectivelyEnabled);
                Click(Command("Save Layout")); Assert.False(ribbon.TemporaryPopup.IsOpen);
                Assert.True(File.Exists(Path.Combine(files.Root, "workspace-layout.json")));
                Assert.Equal("Workspace layout saved.", window.FindControl<TextBlock>("Status")!.Text);
                var savedLayout = dock.Layout;
                Click(ribbon.Tabs[0]); Click(Command("Reset Layout / Recover Windows"));
                Assert.NotSame(savedLayout, dock.Layout); Assert.False(ribbon.TemporaryPopup.IsOpen);
                Assert.Equal("Default workspace restored. Save Layout to keep it.", window.FindControl<TextBlock>("Status")!.Text);
                Click(ribbon.Tabs[0]); Click(Command("Restore Layout")); Assert.False(ribbon.TemporaryPopup.IsOpen);
                Assert.StartsWith("Workspace layout restored;", window.FindControl<TextBlock>("Status")!.Text);
                // Optional Script Editor visibility is restored on demand; opening
                // it again must reconnect the retained draft after layout rebuild.
                Click(ribbon.Tabs[1]); Click(Command("Open Script Editor"));
                var restoredEditor = factory.GetDockable<Dock.Model.Controls.ITool>(WorkspaceDockFactory.ScriptEditorId)!;
                Assert.Same(model, restoredEditor.Context);
                Assert.Same(restoredEditor, Assert.IsAssignableFrom<Dock.Model.Controls.IToolDock>(restoredEditor.Owner).ActiveDockable);
                Assert.False(ribbon.IsExpanded);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task RibbonTemporaryCommandsDoNotResizeDockAndPinPreferenceKeepsSelectedTabAcrossModes()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var files = new Files(); using var docs = new DocumentManager(); using var model = new ScriptEditorViewModel(docs, files.Library);
            var path = Path.Combine(files.Root, "preferences.json"); var state = new ApplicationPreferences(path);
            var ribbon = new UtilityRibbon(state, model, RibbonActions()); var dock = new Border();
            var root = new Grid { RowDefinitions = new("Auto,*") }; root.Children.Add(ribbon); Grid.SetRow(dock, 1); root.Children.Add(dock);
            var window = new Window { Width = 1000, Height = 650, Content = root }; window.Show();
            try
            {
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); var height = dock.Bounds.Height;
                Assert.False(ribbon.IsExpanded); Assert.False(ribbon.PinnedHost.IsVisible);
                ribbon.SelectTab(1); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.True(ribbon.TemporaryPopup.IsOpen); Assert.Equal(height, dock.Bounds.Height);
                window.MouseDown(new Point(40, 500), MouseButton.Left); window.MouseUp(new Point(40, 500), MouseButton.Left);
                Assert.False(ribbon.TemporaryPopup.IsOpen); Assert.Equal(height, dock.Bounds.Height);
                SendShellKey(ribbon.Tabs[1], Avalonia.Input.Key.Left); Assert.Equal(0, ribbon.SelectedTab);
                SendShellKey(ribbon.Tabs[0], Avalonia.Input.Key.Down); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.True(ribbon.TemporaryPopup.IsOpen, "Keyboard-opened ribbon should remain open");
                Assert.NotNull(TopLevel.GetTopLevel(ribbon.TemporaryPopup.Child!));
                Assert.True(ribbon.TemporaryPopup.Child!.GetVisualDescendants().OfType<Button>().First().IsFocused);
                ribbon.SelectTab(1);
                SendShellKey(ribbon.TemporaryPopup.Child!, Avalonia.Input.Key.Escape); Assert.False(ribbon.TemporaryPopup.IsOpen);
                ribbon.Pin.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
                Assert.True(ribbon.IsExpanded); Assert.False(ribbon.TemporaryPopup.IsOpen); Assert.Equal(1, ribbon.SelectedTab);
                Assert.True(new ApplicationPreferences(path).Current.RibbonExpanded); Assert.True(dock.Bounds.Height < height);
                Assert.True(state.Apply(state.Current with { RibbonExpanded = false })); window.UpdateLayout();
                Assert.False(ribbon.IsExpanded); Assert.Equal(1, ribbon.SelectedTab); Assert.Equal(height, dock.Bounds.Height);
                ribbon.SelectTab(3); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var planned = ribbon.TemporaryPopup.Child!.GetVisualDescendants().OfType<Button>().ToArray();
                Assert.Equal(2, planned.Length); Assert.All(planned, p => { Assert.False(p.IsEnabled); Assert.Contains("Planned", AutomationProperties.GetName(p)!); Assert.Contains("Planned", ToolTip.GetTip(p)!.ToString()!); });
                ribbon.TemporaryPopup.IsOpen = false;
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task PreferencesApplyAndCancelAreExplicitAndSchemaControlsAreReserved()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var files = new Files(); var path = Path.Combine(files.Root, "preferences.json"); var state = new ApplicationPreferences(path);
            var window = new PreferencesWindow(state); window.Show();
            window.EditByDefault.IsChecked = true; window.RibbonVisibility.SelectedIndex = 1;
            Assert.False(state.Current.StartInEditMode); Assert.False(state.Current.RibbonExpanded);
            window.ApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(state.Current.StartInEditMode); Assert.True(state.Current.RibbonExpanded);
            window.Categories.SelectedIndex = 1; Assert.False(window.SchemaMode.IsEnabled); Assert.False(window.TargetSchema.IsEnabled);
            Assert.Equal(2, window.SchemaMode.ItemCount); Assert.Equal(0, window.SchemaMode.SelectedIndex);
            window.EditByDefault.IsChecked = false; window.RibbonVisibility.SelectedIndex = 0;
            window.CancelButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Assert.True(new ApplicationPreferences(path).Current.RibbonExpanded); Assert.True(state.Current.StartInEditMode);
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task InlineRenameConfirmsCancelsAndValidatesWithoutDiscardingCodeAndUsesSharedCommands()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager(); using var model = new ScriptEditorViewModel(docs, files.Library);
            await model.Session.NewAsync(Cancel); Assert.True(model.Session.Save()); var id = model.Session.Id;
            model.Session.Source += "// synthetic unsaved"; var source = model.Session.Source;
            var view = new ScriptEditorView(model); var menu = new MenuItem(); ScriptCommandMenu.Populate(menu, model);
            var window = new Window { Content = view, Width = 320, Height = 300 }; window.Show();
            try
            {
                Assert.Same(view.Save.Command, ((MenuItem)menu.Items[2]!).Command); Assert.Same(view.Delete.Command, ((MenuItem)menu.Items[5]!).Command);
                view.NameDisplay.Command!.Execute(null); Dispatcher.UIThread.RunJobs();
                Assert.True(view.ScriptName.IsVisible); Assert.False(view.NameDisplay.IsVisible);
                view.ScriptName.Text = "Renamed synthetic"; SendShellKey(view.ScriptName, Avalonia.Input.Key.Enter);
                Assert.Equal("Renamed synthetic", model.Session.Name); Assert.Equal(source, model.Session.Source); Assert.Equal(id, model.Session.Id); Assert.True(model.Session.IsDirty);
                model.BeginRename(); view.ScriptName.Text = "Canceled"; SendShellKey(view.ScriptName, Avalonia.Input.Key.Escape); Assert.Equal("Renamed synthetic", model.Session.Name);
                model.BeginRename(); Dispatcher.UIThread.RunJobs(); view.ScriptName.Text = " "; view.Editor.TextArea.Focus();
                Assert.Equal("Renamed synthetic", model.Session.Name); Assert.Contains("Name unchanged", model.Session.Status);
                model.SaveCommand.Execute(null); Assert.Equal("Renamed synthetic", files.Library.Open(id).Name); Assert.Equal(source, files.Library.Open(id).Source);
                await model.Session.OpenAsync(ScriptLibrary.ExampleId, Cancel);
                Assert.False(model.RenameCommand.CanExecute(null)); Assert.False(model.DeleteCommand.CanExecute(null));
                model.Scope = ScriptScope.WholeDocument; Assert.Equal(1, view.ExecutionScope.SelectedIndex);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task SharedDeleteAndReportPickerKeepOutputExportAndOriginalReportAttribution()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            var source = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Synthetic)); docs.Open(source);
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel); model.Session.Name = "Producing synthetic script";
            model.Session.Source = "var r = Batch.Report(\"summary\", \"Count\"); void Main() { r.Row(\"1\"); }"; Assert.True(model.Session.Save());
            await model.RunAsync(); var origin = model.ReportOrigin; var reports = model.Reports;
            var picker = new TaskCompletionSource<string?>(); var choosing = model.ChooseAndExportReportAsync("summary", () => picker.Task);
            Assert.False(model.CanRun); Assert.False(model.CanManage); Assert.False(model.CanExport);
            picker.SetResult(null); Assert.False(await choosing); Assert.Same(origin, model.ReportOrigin); Assert.Same(reports, model.Reports);
            var export = Path.Combine(files.Root, "retained.csv"); Assert.True(await model.ExportReportAsync("summary", export));
            var csv = await File.ReadAllBytesAsync(export);
            Assert.True(model.Session.Rename("Renamed after run")); model.PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Discard);
            var confirm = new TaskCompletionSource<bool>(); model.ConfirmDeleteAsync = name => { Assert.Equal("Producing synthetic script", name); return confirm.Task; };
            model.DeleteCommand.Execute(null); Assert.False(model.NewCommand.CanExecute(null)); Assert.False(model.RunCommand.CanExecute(null));
            confirm.SetResult(false); await Task.Yield(); Assert.Equal("Renamed after run", model.Session.Name); Assert.True(model.Session.IsDirty);
            model.ConfirmDeleteAsync = _ => Task.FromResult(true); model.DeleteCommand.Execute(null);
            Assert.Equal(ScriptLibrary.ExampleId, model.Session.Id); Assert.Same(origin, model.ReportOrigin); Assert.Same(reports, model.Reports);
            Assert.Equal(csv, await File.ReadAllBytesAsync(export)); Assert.Equal(2, docs.Documents.Count);
            docs.Activate(source.Id); var output = docs.Documents.Single(d => d != source);
            var notified = false; model.ReviewCommand.CanExecuteChanged += (_, _) => notified = true;
            docs.Close(output.Id, true); await output.Disposal;
            Assert.True(notified); Assert.False(model.ReviewCommand.CanExecute(null)); Assert.Same(origin, model.ReportOrigin);
            foreach (var doc in docs.Documents.ToArray()) { docs.Close(doc.Id, true); await doc.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
