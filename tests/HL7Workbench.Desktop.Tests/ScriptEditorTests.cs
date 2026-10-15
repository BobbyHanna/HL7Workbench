using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ScriptEditorTests
{
    private const string Synthetic = "MSH|^~\\&|SYN\rPID|1||SYN-001||Example^Ada\r";
    private sealed class Files : IDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-script-ui-test-" + Guid.NewGuid().ToString("N"));
        internal ScriptLibrary Library => new(Path.Combine(Root, "library"));
        internal ScriptExecutionOptions Options => new() { StorageRoot = Path.Combine(Root, "snapshots") };
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private static Task<UnsavedChangesDecision> Cancel() => Task.FromResult(UnsavedChangesDecision.Cancel);
    [Fact]
    public async Task EditorReconstructionRetainsDraftAndRoutedTypingCompletesNativeUndoGroup()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            var view = new ScriptEditorView(model); var window = new Window { Width = 900, Height = 650, Content = view }; window.Show();
            try
            {
                Assert.True(view.Editor.IsReadOnly); Assert.NotNull(view.Editor.SyntaxHighlighting);
                await model.Session.NewAsync(Cancel); view.ScriptName.Text = "Synthetic editable";
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); view.Editor.TextArea.Focus();
                window.KeyTextInput("Message.Clear(\"PID-19\");");
                Assert.Equal("Message.Clear(\"PID-19\");", model.Session.Source); Assert.True(model.Session.IsDirty);
                Assert.True(view.Editor.Document.UndoStack.CanUndo);
                window.Content = null;
                var reconstructed = new ScriptEditorView(model); window.Content = reconstructed;
                Assert.Equal(model.Session.Source, reconstructed.Editor.Text); Assert.Equal("Synthetic editable", reconstructed.ScriptName.Text);
                Assert.False(await model.ConfirmCloseAsync(Cancel)); Assert.True(model.Session.IsDirty);
                Assert.True(model.Session.Save()); Assert.False(model.Session.IsDirty);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task OversizedRoutedInputRestoresBoundedDraftAfterNativeTransactionEnds()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel); model.Session.Source = "// retained";
            var view = new ScriptEditorView(model); var window = new Window { Width = 900, Height = 600, Content = view }; window.Show();
            try
            {
                window.UpdateLayout(); view.Editor.SelectAll(); view.Editor.TextArea.Focus();
                window.KeyTextInput(new string(' ', ScriptLimits.ScriptCharacters + 1));
                Dispatcher.UIThread.RunJobs();
                Assert.Equal("// retained", model.Session.Source); Assert.Equal(model.Session.Source, view.Editor.Text);
                Assert.Contains("limit", view.Status.Text!);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task RunControlsPublishOutputAndLeaveSourceDirtyStateUntouched()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            var source = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Synthetic)); docs.Open(source);
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel); model.Session.Source = "Message.SetText(\"PID-5.2\", \"SYNTHETIC\");";
            var view = new ScriptEditorView(model); var window = new Window { Width = 900, Height = 650, Content = view }; window.Show();
            try
            {
                Assert.True(view.Run.IsEnabled); var run = model.RunAsync(); Assert.True(model.IsBusy); Assert.False(view.Run.IsEnabled); Assert.True(view.Cancel.IsEnabled);
                await run; Dispatcher.UIThread.RunJobs();
                Assert.False(model.IsBusy); Assert.Empty(model.Diagnostics); Assert.True(model.CanReview);
                Assert.Equal(2, docs.Documents.Count); Assert.False(source.IsDirty); Assert.Equal(Synthetic, source.Message.RawText);
                var output = docs.Documents.Single(d => d != source); Assert.True(output.IsDirty); Assert.Null(output.SavePath);
                Assert.Equal("SYNTHETIC", output.Message.Get("PID-5.2").Raw);
                docs.Close(output.Id, true); await output.Disposal;
            }
            finally { window.Close(); docs.Close(source.Id); await source.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task ClosingRunningToolCancelsAndDrainsWithoutOutputOrDraftLoss()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            var source = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Synthetic)); docs.Open(source);
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel); model.Session.Name = "Synthetic loop"; model.Session.Source = "while (true) { }"; Assert.True(model.Session.Save());
            var run = model.RunAsync(); Assert.True(model.IsBusy);
            Assert.True(await model.ConfirmCloseAsync(Cancel)); await run;
            Assert.False(model.IsBusy); Assert.Single(docs.Documents); Assert.Equal("while (true) { }", model.Session.Source);
            Assert.True(!Directory.Exists(files.Options.StorageRoot) || !Directory.EnumerateDirectories(files.Options.StorageRoot).Any());
            docs.Close(source.Id); await source.Disposal;
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task DiagnosticsStayInToolAndOperationalLogOmitsExceptionContent()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            var source = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Synthetic)); docs.Open(source);
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel); model.Session.Source = "throw new Exception(Message.Text(\"PID-3\"));";
            await model.RunAsync(); Assert.Contains(model.Diagnostics, d => d.Code == "RuntimeFailed"); Assert.Single(docs.Documents);
            Assert.DoesNotContain("SYN-001", string.Join("", model.Diagnostics));
            Assert.DoesNotContain("SYN-001", string.Join("", docs.Activity.Snapshot()));
            model.Session.Source = "var x = ;"; await model.RunAsync();
            Assert.Contains(model.Diagnostics, d => d.Phase == ScriptPhase.Compilation && d.Line == 1);
            docs.Close(source.Id); await source.Disposal;
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task DockToolCloseHonorsDirtyDraftAndRestoresPlacementWithoutPersistingScriptText()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            docs.Open(new("synthetic", "Synthetic", Hl7Parser.Parse(Synthetic)));
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel); model.Session.Source = "// SYNTHETIC-PRIVATE-DRAFT";
            var tools = new ToolWindowRegistry(); tools.Register(new(WorkspaceDockFactory.ScriptEditorId, "Script Editor", () => model, Optional: true));
            using var factory = new WorkspaceDockFactory(docs, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
            factory.ConfirmScriptCloseAsync = () => model.ConfirmCloseAsync(Cancel);
            factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.ScriptEditorId);
            var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.ScriptEditorId)!;
            Assert.False(tool.CanDockAsDocument); Assert.True(tool.CanFloat);
            factory.SetActiveDockable(tool); Assert.Equal("synthetic", docs.ActiveDocument!.Id);
            factory.CloseDockable(tool); Assert.DoesNotContain(tool, factory.Root.HiddenDockables!);
            Assert.Equal("// SYNTHETIC-PRIVATE-DRAFT", model.Session.Source);
            factory.FloatDockable(tool);
            var json = LayoutCodec.Encode(factory.Capture()); Assert.DoesNotContain("SYNTHETIC-PRIVATE", json);
            factory.CloseFloatingWindows(); factory.Restore(LayoutCodec.Decode(json), [new(0, 0, 1920, 1080)]);
            factory.ShowTool(WorkspaceDockFactory.ScriptEditorId);
            Assert.Same(model, factory.GetDockable<ITool>(WorkspaceDockFactory.ScriptEditorId)!.Context);
            Assert.True(model.Session.IsDirty);
            factory.ConfirmScriptCloseAsync = () => model.ConfirmCloseAsync(() => Task.FromResult(UnsavedChangesDecision.Discard));
            factory.CloseDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.ScriptEditorId));
            Assert.False(model.Session.IsDirty);
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task ScriptSaveShortcutDoesNotReachDocumentSaveHandler()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            using var model = new ScriptEditorViewModel(docs, files.Library, files.Options);
            await model.Session.NewAsync(Cancel); model.Session.Source = "// synthetic";
            var view = new ScriptEditorView(model); var window = new Window { Content = view, Width = 900, Height = 600 }; window.Show();
            var documentSaves = 0;
            window.KeyDown += (_, e) => { if (e.Key == Key.S) documentSaves++; };
            try
            {
                view.Editor.RaiseEvent(new KeyEventArgs { RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent, Key = Key.S, KeyModifiers = KeyModifiers.Control });
                Assert.False(model.Session.IsDirty); Assert.Equal(0, documentSaves);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
