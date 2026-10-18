using Avalonia.Controls;
using System.Diagnostics;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using AvaloniaEdit;
using Dock.Avalonia.Controls;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Scripting;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class RawEditingCrashTests
{
    [Fact]
    public async Task PlatformInputCompletesEditorTransactionsAndRetainsDocumentHistory()
    {
        const string childMarker = "HL7WORKBENCH_RAW_INPUT_REGRESSION_CHILD";
        if (Environment.GetEnvironmentVariable(childMarker) != "1")
        {
            // Other headless tests create/dispose their own UI dispatchers. Isolate
            // process-wide AvaloniaEdit key bindings so keyboard commands use the
            // real defaults on their owning dispatcher, without replacing key maps.
            using var process = new Process { StartInfo = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            } };
            process.StartInfo.ArgumentList.Add(typeof(RawEditingCrashTests).Assembly.Location);
            process.StartInfo.ArgumentList.Add("-class");
            process.StartInfo.ArgumentList.Add(typeof(RawEditingCrashTests).FullName!);
            process.StartInfo.Environment[childMarker] = "1";
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            timeout.CancelAfter(TimeSpan.FromMinutes(2));
            Assert.True(process.Start());
            var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
            var errors = process.StandardError.ReadToEndAsync(timeout.Token);
            try { await process.WaitForExitAsync(timeout.Token); }
            finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            Assert.True(process.ExitCode == 0, await output + await errors);
            return;
        }
        // AvaloniaEdit's default KeyBinding objects are process-wide and dispatcher-
        // affine. Exercise all native key commands on one UI thread, as in production.
        await using var session = HeadlessUnitTestSession.StartNew(typeof(RawHoverTestApplication));
        await session.Dispatch(async () =>
        {
            void Key(Window window, Avalonia.Input.Key key, PhysicalKey physical, RawInputModifiers modifiers = RawInputModifiers.None)
            {
                window.KeyPress(key, modifiers, physical, null);
                window.KeyRelease(key, modifiers, physical, null);
            }
            foreach (var dockedInspection in new[] { false, true })
            {
                const string text = "MSH|^~\\&|SYN\rPID|Alpha^Beta\r";
                using var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(text));
                var view = Assert.IsType<MessageInspectionView>(MessageViewer.Create(document, dockedInspection));
                var window = new Window { Width = 1100, Height = 800, Content = view }; window.Show();
                DocumentHistoryShortcuts.Attach(window, () => document);
                try
                {
                    var inspectionTools = dockedInspection ? null : Assert.IsType<InspectionPanes>(view.LowerPane);
                    void PendingTools()
                    {
                        if (inspectionTools is null) return;
                        Assert.Same(inspectionTools, view.LowerPane);
                        Assert.Contains("previous parse", inspectionTools.SnapshotStatus.Text!);
                        Assert.False(inspectionTools.HasCurrentInspection);
                        Assert.NotEmpty(inspectionTools.Navigator.Items);
                    }
                    document.IsEditMode = true;
                    window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    view.Editor.Select(text.IndexOf("Alpha", StringComparison.Ordinal), 0);
                    Assert.True(view.Editor.TextArea.Focus());
                    // This raw input event includes TextArea.OnTextInput's undo group.
                    // Clearing native history in TextChanged used to throw here, before
                    // the debounce delay or any parser work could start.
                    window.KeyTextInput("X");
                    PendingTools();
                    Assert.Equal(text.Replace("Alpha", "XAlpha", StringComparison.Ordinal), document.WorkingText);
                    Assert.Null(document.ActiveInspection);
                    Assert.True(document.CanUndo); Assert.False(view.Editor.Document.UndoStack.CanUndo);
                    await document.EditRefresh;
                    Assert.Equal("XAlpha", document.Message.Get("PID-1.1").Raw);

                    Key(window, Avalonia.Input.Key.Back, PhysicalKey.Backspace);
                    PendingTools();
                    await document.EditRefresh; Assert.Equal("Alpha", document.Message.Get("PID-1.1").Raw);
                    view.Editor.Select(text.IndexOf("Alpha", StringComparison.Ordinal), "Alpha".Length);
                    window.KeyTextInput("Replaced");
                    PendingTools();
                    await document.EditRefresh; Assert.Equal("Replaced", document.Message.Get("PID-1.1").Raw);

                    await window.Clipboard!.SetTextAsync("Pasted");
                    view.Editor.Select(view.Editor.Text.IndexOf("Replaced", StringComparison.Ordinal), "Replaced".Length);
                    Key(window, Avalonia.Input.Key.V, PhysicalKey.V, RawInputModifiers.Control);
                    PendingTools();
                    await document.EditRefresh; Assert.Equal("Pasted", document.Message.Get("PID-1.1").Raw);

                    Key(window, Avalonia.Input.Key.Z, PhysicalKey.Z, RawInputModifiers.Control);
                    PendingTools();
                    await document.EditRefresh; Assert.Equal("Replaced", document.Message.Get("PID-1.1").Raw);
                    Key(window, Avalonia.Input.Key.Y, PhysicalKey.Y, RawInputModifiers.Control);
                    PendingTools();
                    await document.EditRefresh; Assert.Equal("Pasted", document.Message.Get("PID-1.1").Raw);

                    var valueStart = view.Editor.Text.IndexOf("Pasted", StringComparison.Ordinal);
                    view.Editor.Select(valueStart, "Pasted".Length);
                    Key(window, Avalonia.Input.Key.Delete, PhysicalKey.Delete);
                    PendingTools();
                    await document.EditRefresh; Assert.Equal("", document.Message.Get("PID-1.1").Raw);
                    window.KeyTextInput("A"); window.KeyTextInput("B"); window.KeyTextInput("C");
                    await document.EditRefresh; Assert.Equal("ABC", document.Message.Get("PID-1.1").Raw);
                    var beta = view.Editor.Text.IndexOf("Beta", StringComparison.Ordinal);
                    Assert.Equal("PID[1]-1[1].2", document.Inspection.ResolveOffset(beta)!.Path.ToString());
                    Assert.Equal(beta, document.Inspection.ResolveOffset(beta)!.Range64!.Value.Offset);
                    view.Editor.Select(beta, 4); Assert.Equal("Beta", view.Editor.SelectedText);
                    document.IsEditMode = false; window.KeyTextInput("REFUSED");
                    Assert.DoesNotContain("REFUSED", view.Editor.Text); Assert.True(document.IsDirty);
                    Assert.False(view.Editor.Document.UndoStack.CanUndo);
                }
                finally { window.Close(); }
            }

            using var documents = new DocumentManager();
            using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rPID|Alpha^Beta\r"));
            var file = await MessageFileReader.OpenFileAsync(documents, "Synthetic", source, TestContext.Current.CancellationToken);
            using var navigator = new InspectionToolViewModel(documents, true);
            using var fields = new InspectionToolViewModel(documents, false);
            var tools = new ToolWindowRegistry();
            tools.Register(new(WorkspaceDockFactory.NavigatorId, "Segment Navigator", () => navigator));
            tools.Register(new(WorkspaceDockFactory.FieldInspectorId, "Field Inspector", () => fields));
            tools.Register(new(WorkspaceDockFactory.ActivityLogId, "Activity Log", () => new ActivityLogViewModel(documents.Activity), Optional: true, ReleaseOnClose: true, RestoreVisibility: true));
            using var factory = new WorkspaceDockFactory(documents, tools);
            var dock = new DockControl { Layout = factory.CreateLayout() };
            var shell = new Window { Width = 1400, Height = 1000, Content = dock }; shell.Show();
            DocumentHistoryShortcuts.Attach(shell, () => documents.ActiveDocument);
            try
            {
                shell.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var view = Assert.Single(dock.GetVisualDescendants().OfType<MessageInspectionView>());
                Assert.Equal(2, dock.GetVisualDescendants().OfType<InspectionPanes>().Count());
                file.IsEditMode = true;
                file.Inspection.SelectSegment(file.Inspection.Segments[1]); file.Inspection.InspectField(1);
                shell.UpdateLayout(); Dispatcher.UIThread.RunJobs(); Assert.True(view.Editor.TextArea.Focus());
                shell.KeyTextInput("Changed^Value");
                await file.EditRefresh; shell.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.Equal("Changed^Value", file.Message.Get("PID-1").Raw);
                Assert.Equal(2, dock.GetVisualDescendants().OfType<InspectionPanes>().Count());
                var inspector = Assert.Single(dock.GetVisualDescendants().OfType<InspectionPanes>(), p => p.Inspector.IsAttachedToVisualTree());
                Assert.Same(file.Inspection.Fields, inspector.Inspector.ItemsSource);
                Key(shell, Avalonia.Input.Key.Back, PhysicalKey.Backspace);
                await file.EditRefresh; shell.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.Equal("Changed^Valu", file.Message.Get("PID-1").Raw);
            }
            finally { shell.Close(); }

            // Keep these native text-history checks on this same isolated dispatcher:
            // AvaloniaEdit's process-wide command bindings belong to its first UI thread.
            file.IsEditMode = false;
            var workingText = file.EditableText; var version = file.ContentVersion;
            void UnchangedDocumentHistory()
            {
                Assert.Equal(workingText, file.EditableText); Assert.Equal(version, file.ContentVersion);
                Assert.True(file.CanUndo); Assert.False(file.CanRedo);
            }
            var historyWindow = new Window { Width = 1100, Height = 800 };
            DocumentHistoryShortcuts.Attach(historyWindow, () => documents.ActiveDocument);
            historyWindow.Show();
            var libraryRoot = Path.Combine(Path.GetTempPath(), "hl7-keyboard-library-" + Guid.NewGuid().ToString("N"));
            try
            {
                void Focus(Control content, Control target)
                {
                    historyWindow.Content = content; historyWindow.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    Assert.True(target.Focus());
                }
                void Undo() => Key(historyWindow, Avalonia.Input.Key.Z, PhysicalKey.Z, RawInputModifiers.Control);
                void Redo() => Key(historyWindow, Avalonia.Input.Key.Y, PhysicalKey.Y, RawInputModifiers.Control);

                using var search = new SearchViewModel(documents);
                var searchView = new SearchResultsView(search); Focus(searchView, searchView.Value);
                historyWindow.KeyTextInput("SYN-query"); Assert.Equal("SYN-query", searchView.Value.Text);
                Undo(); Assert.Equal("", searchView.Value.Text ?? ""); UnchangedDocumentHistory();
                Undo(); UnchangedDocumentHistory();
                Redo(); Assert.Equal("SYN-query", searchView.Value.Text);
                Assert.True(searchView.Value.IsFocused); UnchangedDocumentHistory();

                using var script = new ScriptEditorViewModel(documents, new ScriptLibrary(libraryRoot));
                await script.Session.NewAsync(() => Task.FromResult(UnsavedChangesDecision.Discard));
                var scriptView = new ScriptEditorView(script); Focus(scriptView, scriptView.Editor.TextArea);
                var scriptText = script.Session.Source; scriptView.Editor.CaretOffset = scriptText.Length;
                historyWindow.KeyTextInput("\n// synthetic keyboard check"); var scriptDraft = script.Session.Source;
                Assert.NotEqual(scriptText, scriptDraft);
                Undo(); Assert.Equal(scriptText, script.Session.Source); UnchangedDocumentHistory();
                Redo(); Assert.Equal(scriptDraft, script.Session.Source);
                Assert.True(scriptView.Editor.TextArea.IsFocused); UnchangedDocumentHistory();

                var independentEditor = new TextEditor { Text = "Synthetic editor" };
                independentEditor.Document.UndoStack.ClearAll();
                Focus(independentEditor, independentEditor.TextArea); independentEditor.CaretOffset = independentEditor.Text.Length;
                historyWindow.KeyTextInput(" change"); Assert.Equal("Synthetic editor change", independentEditor.Text);
                Undo(); Assert.Equal("Synthetic editor", independentEditor.Text); UnchangedDocumentHistory();
                Undo(); UnchangedDocumentHistory();
                Redo(); Assert.Equal("Synthetic editor change", independentEditor.Text);
                Assert.True(independentEditor.TextArea.IsFocused); UnchangedDocumentHistory();

                var fileView = Assert.IsType<FileDocumentView>(MessageViewer.Create(file));
                Focus(fileView, fileView.MessageNumberInput); fileView.MessageNumberInput.CaretIndex = 1;
                historyWindow.KeyTextInput("2"); Assert.Equal("12", fileView.MessageNumberInput.Text);
                Undo(); Assert.Equal("1", fileView.MessageNumberInput.Text); UnchangedDocumentHistory();
                Redo(); Assert.Equal("12", fileView.MessageNumberInput.Text);
                Assert.True(fileView.MessageNumberInput.IsFocused); UnchangedDocumentHistory();
            }
            finally
            {
                historyWindow.Close();
                if (Directory.Exists(libraryRoot)) Directory.Delete(libraryRoot, true);
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
