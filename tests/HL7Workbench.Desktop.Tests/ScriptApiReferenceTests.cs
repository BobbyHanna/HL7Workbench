using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ScriptApiReferenceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-reference-ui-" + Guid.NewGuid().ToString("N"));
        public ScriptLibrary Library => new(Path.Combine(Root, "Scripts"));
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    internal static async Task NativeReferencePasteCase()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager();
            using var script = new ScriptEditorViewModel(documents, files.Library);
            await script.Session.NewAsync(() => Task.FromResult(UnsavedChangesDecision.Cancel));
            var editor = new ScriptEditorView(script); var model = new ScriptApiReferenceViewModel(); var view = new ScriptApiReferenceView(model);
            var panel = new Grid { ColumnDefinitions = new("*,*") }; panel.Children.Add(view); Grid.SetColumn(editor, 1); panel.Children.Add(editor);
            var window = new Window { Content = panel, Width = 1200, Height = 720 }; window.Show();
            try
            {
                Pump(window); Assert.True(view.Signature.IsReadOnly); Assert.True(view.Example.IsReadOnly);
                view.Search.Text = "SetText"; Assert.Contains(model.Results, e => e.Id == "set-text");
                view.Category.SelectedItem = "Read and write"; view.Topics.SelectedItem = model.Results.Single(e => e.Id == "set-text");
                var entry = model.Selected!; Assert.Equal(entry.ExactSignature, view.Signature.Text);
                Click(view.CopySignature); await view.ClipboardTask; Assert.Equal(entry.ExactSignature, await window.Clipboard!.TryGetTextAsync());
                Click(view.CopyExample); await view.ClipboardTask; Assert.Equal(entry.Example, await window.Clipboard!.TryGetTextAsync());
                var previous = editor.Editor.Text; editor.Editor.SelectAll(); editor.Editor.TextArea.Focus();
                window.KeyPress(Key.V, Avalonia.Input.RawInputModifiers.Control, PhysicalKey.V, null);
                window.KeyRelease(Key.V, Avalonia.Input.RawInputModifiers.Control, PhysicalKey.V, null); Pump(window);
                Assert.Equal(entry.Example, script.Session.Source); Assert.True(editor.Editor.Document.UndoStack.CanUndo);
                window.KeyPress(Key.Z, Avalonia.Input.RawInputModifiers.Control, PhysicalKey.Z, null);
                window.KeyRelease(Key.Z, Avalonia.Input.RawInputModifiers.Control, PhysicalKey.Z, null); Pump(window);
                Assert.Equal(previous, script.Session.Source);
                view.Search.Text = "unfindable-reference-term"; Assert.Empty(model.Results); Assert.False(view.CopyExample.IsEnabled);
                Click(view.Back); Assert.Equal("SetText", view.Search.Text); Assert.Equal(entry, model.Selected);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task CreateStarterUsesExistingEditorCommandGateAndDirtyDraftConsent()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager(); using var script = new ScriptEditorViewModel(documents, files.Library);
            var shown = 0; script.OpenEditorRequested += () => shown++;
            var model = new ScriptApiReferenceViewModel { CreateScriptAsync = script.CreateFromReferenceAsync };
            var view = new ScriptApiReferenceView(model); var window = new Window { Content = view, Width = 700, Height = 650 }; window.Show();
            try
            {
                model.Query = "ScriptMessage.Read"; model.Selected = model.Results.Single(e => e.Id == "read"); Pump(window);
                Assert.True(view.CreateScript.IsVisible); Click(view.CreateScript); Assert.True(await view.CreationTask);
                Assert.Equal(1, shown); Assert.Equal(model.Selected.Example, script.Session.Source); Assert.False(script.Session.IsBundled); Assert.True(script.Session.IsDirty);
                var original = script.Session.Source;
                model.Query = "bounded count"; model.Selected = model.Results.Single(e => e.Id == "report");
                Click(view.CreateScript); Assert.False(await view.CreationTask); Assert.Equal(original, script.Session.Source); Assert.Equal(1, shown);
                script.PromptUnsavedAsync = () => Task.FromResult(UnsavedChangesDecision.Discard);
                Click(view.CreateScript); Assert.True(await view.CreationTask); Assert.Equal(2, shown);
                Assert.Equal("Bounded count report", script.Session.Name); Assert.True(script.Session.Save());
                Assert.Equal(script.Session.Source, files.Library.Open(script.Session.Id).Source);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false, 360)] [InlineData(true, 1000)]
    public async Task StandardControlsFollowThemeAndRemainUsableAtNarrowAndWideWidths(bool dark, int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            var model = new ScriptApiReferenceViewModel(); var view = new ScriptApiReferenceView(model);
            var window = new Window { Content = view, Width = width, Height = 720, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Pump(window); Assert.Equal(dark ? ThemeVariant.Dark : ThemeVariant.Light, view.Example.ActualThemeVariant);
                Assert.IsAssignableFrom<IBrush>(view.Example.Foreground); Assert.IsAssignableFrom<IBrush>(view.Example.Background);
                Assert.True(view.Search.Bounds.Width > 200); Assert.True(view.Topics.Bounds.Height >= 100); Assert.True(view.Signature.Bounds.Width > 100);
                Assert.True(view.Topics.Bounds.Width <= width); Assert.Contains("Offline", view.Version.Text!);
                var foreground = view.Example.Foreground;
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark; Pump(window);
                Assert.NotEqual(foreground, view.Example.Foreground);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task HelpAndEditorOpenTheSameOfflineDockToolAcrossCloseAndLayoutRestore()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); var window = new MainWindow(files.Root); window.Show();
            try
            {
                var dock = window.FindControl<Dock.Avalonia.Controls.DockControl>("WorkspaceDock")!;
                var factory = Assert.IsType<WorkspaceDockFactory>(dock.Layout!.Factory);
                var help = window.FindControl<MenuItem>("ScriptApiReferenceMenu")!;
                help.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(window);
                var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.ScriptApiReferenceId)!;
                var reference = Assert.IsType<ScriptApiReferenceViewModel>(tool.Context); reference.Query = "SetText";
                Assert.Same(tool, Assert.IsAssignableFrom<IToolDock>(tool.Owner).ActiveDockable);
                var referenceContent = new DockContentTemplate().Build(tool);
                var secondary = new Window { Content = referenceContent }; secondary.Show(); Pump(secondary);
                var old = Assert.Single(referenceContent.GetVisualDescendants().OfType<ScriptApiReferenceView>());
                factory.CloseDockable(tool); secondary.Content = null; Pump(secondary);
                reference.Query = "SetRaw"; Assert.Equal("SetText", old.Search.Text);
                factory.ShowTool(WorkspaceDockFactory.ScriptEditorId);
                var editorTool = factory.GetDockable<ITool>(WorkspaceDockFactory.ScriptEditorId)!;
                var script = Assert.IsType<ScriptEditorViewModel>(editorTool.Context);
                script.ApiReferenceCommand.Execute(null); secondary.Content = new DockContentTemplate().Build(tool); Pump(window); Pump(secondary);
                referenceContent = (Control)secondary.Content;
                Assert.Same(reference, tool.Context); Assert.Equal("SetRaw", Assert.Single(referenceContent.GetVisualDescendants().OfType<ScriptApiReferenceView>()).Search.Text);
                await script.Session.NewAsync(() => Task.FromResult(UnsavedChangesDecision.Cancel));
                script.Session.Source = "void Main(){ Message.SetText(\"PID-5\", \"SYNTHETIC\"); }";
                var code = new ScriptEditorView(script); var codeWindow = new Window { Content = code }; codeWindow.Show(); Pump(codeWindow);
                try
                {
                    code.Editor.CaretOffset = code.Editor.Text.IndexOf("SetText", StringComparison.Ordinal) + 3;
                    code.Editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.F1 }); await code.Assistance.Pending;
                    Assert.Equal("set-text", reference.Selected!.Id); Assert.Same(tool, factory.GetDockable<ITool>(WorkspaceDockFactory.ScriptApiReferenceId));
                    Assert.False(script.IsBusy); reference.Back(); Assert.Equal("SetRaw", reference.Query);
                }
                finally { codeWindow.Close(); script.Session.DiscardChanges(); }
                factory.FloatDockable(tool); Pump(window); Assert.NotEmpty(factory.Root.Windows!);
                var saved = factory.Capture(); Assert.DoesNotContain("SetRaw", LayoutCodec.Encode(saved));
                secondary.Content = null; secondary.Close();
                dock.Layout = factory.Restore(saved, []); factory.ShowTool(WorkspaceDockFactory.ScriptApiReferenceId); Pump(window);
                Assert.Same(reference, factory.GetDockable<ITool>(WorkspaceDockFactory.ScriptApiReferenceId)!.Context);
                Assert.Equal("SetRaw", reference.Query); Assert.False(Directory.Exists(Path.Combine(files.Root, "Scripts")));
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public void BackHistoryIsBoundedAndRestoresSearchCategoryAndSelection()
    {
        var model = new ScriptApiReferenceViewModel();
        model.Query = "Read"; model.Category = "Read and write"; model.Selected = model.Results.Single(e => e.Id == "read");
        model.Query = "SetText"; model.Back(); Assert.Equal("Read", model.Query); Assert.Equal("Read and write", model.Category); Assert.Equal("read", model.Selected!.Id);
        for (var i = 0; i < 100; i++) model.Query = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var steps = 0; while (model.CanGoBack) { model.Back(); steps++; }
        Assert.Equal(64, steps); model.Back(); Assert.False(model.CanGoBack);
    }
}
