using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using AvaloniaEdit.Highlighting;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ScriptEditorPolishTests
{
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-script-polish-" + Guid.NewGuid().ToString("N"));
        public string Settings => Path.Combine(Root, "font.json");
        public ScriptLibrary Library => new(Path.Combine(Root, "scripts"));
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private static Task<UnsavedChangesDecision> Cancel() => Task.FromResult(UnsavedChangesDecision.Cancel);
    private static void PressKey(Control target, Key key, KeyModifiers modifiers = KeyModifiers.Control) =>
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });

    [Fact]
    public async Task RenderedSemanticColorsStayReadableAndDoNotPretendDelegateCallsAreMethods()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            using var model = new ScriptEditorViewModel(docs, files.Library);
            await model.Session.NewAsync(Cancel);
            model.Session.Source = "// synthetic comment\nstring label = \"SYNTHETIC\";\nint count = 42;\nMessage.Clear(\"PID-19\");\nAction callback = () => { }; callback();\nif (count > 0) return;";
            var globalMethod = HighlightingManager.Instance.GetDefinition("C#").GetNamedColor("MethodCall").Foreground;
            var view = new ScriptEditorView(model); var window = new Window { Width = 900, Height = 500, Content = view }; window.Show();
            try
            {
                await view.SymbolHighlighting.Pending;
                window.UpdateLayout(); view.Editor.TextArea.TextView.EnsureVisualLines();
                string At(int line, string token)
                {
                    var documentLine = view.Editor.Document.GetLineByNumber(line);
                    var column = view.Editor.Document.GetText(documentLine).IndexOf(token, StringComparison.Ordinal);
                    Assert.True(column >= 0);
                    var element = view.Editor.TextArea.TextView.GetVisualLine(line).Elements.Single(e =>
                        column >= e.RelativeTextOffset && column < e.RelativeTextOffset + e.DocumentLength);
                    return Assert.IsAssignableFrom<ISolidColorBrush>(element.TextRunProperties.ForegroundBrush).Color.ToString();
                }
                Assert.Equal("#ff6a9955", At(1, "synthetic"), ignoreCase: true);
                Assert.Equal("#ff4ec9b0", At(2, "string"), ignoreCase: true);
                Assert.Equal("#ffce9178", At(2, "SYNTHETIC"), ignoreCase: true);
                Assert.Equal("#ffb5cea8", At(3, "42"), ignoreCase: true);
                Assert.Equal("#ffdcdcaa", At(4, "Clear"), ignoreCase: true);
                Assert.Equal("#ffd4d4d4", At(5, "callback();"), ignoreCase: true);
                Assert.Equal("#ffc586c0", At(6, "if"), ignoreCase: true);
                Assert.Same(globalMethod, HighlightingManager.Instance.GetDefinition("C#").GetNamedColor("MethodCall").Foreground);
                Assert.Equal(Color.Parse("#1E1E1E"), Assert.IsAssignableFrom<ISolidColorBrush>(view.Editor.Background).Color);
                Assert.Equal(Color.Parse("#D4D4D4"), Assert.IsAssignableFrom<ISolidColorBrush>(view.Editor.TextArea.Caret.CaretBrush).Color);
                Assert.NotEqual(view.Editor.Background, view.Editor.TextArea.SelectionBrush);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Theory]
    [InlineData(900, 300)] [InlineData(320, 300)] [InlineData(260, 220)]
    public async Task AdaptiveToolbarsLeavePracticalCodeSpaceAndVerticalAccessAtNarrowWidths(double width, double height)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            using var model = new ScriptEditorViewModel(docs, files.Library);
            var view = new ScriptEditorView(model); var window = new Window { Width = width, Height = height, Content = view }; window.Show();
            try
            {
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.False(view.DiagnosticPanel.IsVisible);
                Assert.False(view.DiagnosticPanel.IsExpanded);
                Assert.True(view.Editor.Bounds.Height >= height * .45, $"Code height: {view.Editor.Bounds.Height}, tool height: {height}");
                Assert.True(view.Editor.Bounds.Width <= width);
                Assert.True(view.Save.IsVisible && view.Run.IsVisible && view.ExecutionScope.IsVisible);
                if (width < 600)
                {
                    Assert.True(view.LibraryToolbar.Bounds.Height > view.New.Bounds.Height);
                    view.Save.BringIntoView(); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    var saveRight = view.Save.TranslatePoint(new(view.Save.Bounds.Width, 0), view.LibraryToolbar)!.Value.X;
                    Assert.InRange(saveRight, 0, view.LibraryToolbar.Bounds.Width + 1);
                    Assert.True(view.LayoutSurface.Scroll.Extent.Height > view.LayoutSurface.Scroll.Viewport.Height);
                }
                return true;
            }
            finally { window.Close(); }
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ZoomRoutesOnlyInCodePreservesDraftSelectionUndoAndRestoresAcrossViewsAndRestart()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            using var model = new ScriptEditorViewModel(docs, files.Library, preferencesPath: files.Settings);
            await model.Session.NewAsync(Cancel); model.Session.Source = "// synthetic draft\nMessage.Clear(\"PID-19\");";
            var view = new ScriptEditorView(model); var window = new Window { Width = 900, Height = 500, Content = view }; window.Show();
            try
            {
                window.UpdateLayout(); view.Editor.TextArea.Focus(); view.Editor.CaretOffset = view.Editor.Document.TextLength;
                window.KeyTextInput("\n// typed synthetic change"); Assert.True(view.Editor.Document.UndoStack.CanUndo);
                view.Editor.Select(3, 9); var caret = view.Editor.CaretOffset;
                var selected = view.Editor.SelectedText; var document = view.Editor.Document;
                var version = model.Session.ContentVersion; var text = model.Session.Source;
                var previous = view.Editor.FontSize;
                var point = view.Editor.TextArea.TranslatePoint(new(100, 50), window)!.Value;
                window.MouseWheel(point, new(0, -1)); Assert.Equal(previous, view.Editor.FontSize);
                window.MouseWheel(point, new(0, 1), RawInputModifiers.Control);
                Assert.Equal(13, model.FontSizePoints); Assert.Equal("13 pt", view.ZoomReset.Content);
                window.MouseWheel(point, new(0, -1), RawInputModifiers.Control); Assert.Equal(previous, view.Editor.FontSize, precision: 8);
                PressKey(view.ScriptName, Key.OemPlus); Assert.Equal(previous, view.Editor.FontSize);
                PressKey(view.Editor, Key.OemPlus); Assert.True(view.Editor.FontSize > previous);
                PressKey(view.Editor, Key.Add); PressKey(view.Editor, Key.Subtract);
                Assert.Equal(13, model.FontSizePoints);
                view.ZoomIn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(14, model.FontSizePoints); Assert.Equal("14 pt", view.ZoomReset.Content);
                view.ZoomOut.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(13, model.FontSizePoints);
                Assert.Equal(caret, view.Editor.CaretOffset); Assert.Equal(selected, view.Editor.SelectedText);
                Assert.Same(document, view.Editor.Document); Assert.Equal(version, model.Session.ContentVersion); Assert.Equal(text, model.Session.Source);
                Assert.True(view.Editor.Document.UndoStack.CanUndo); view.Editor.Undo(); view.Editor.Redo(); Assert.Equal(text, model.Session.Source);
                for (var i = 0; i < 50; i++) PressKey(view.Editor, Key.Add);
                Assert.Equal(24, model.FontSizePoints); Assert.Equal(32, view.Editor.FontSize); Assert.False(view.ZoomIn.IsEnabled);
                for (var i = 0; i < 50; i++) PressKey(view.Editor, Key.Subtract);
                Assert.Equal(9, model.FontSizePoints); Assert.Equal(12, view.Editor.FontSize); Assert.False(view.ZoomOut.IsEnabled);
                PressKey(view.Editor, Key.D0); Assert.Equal(12, model.FontSizePoints); Assert.Equal("12 pt", view.ZoomReset.Content);
                PressKey(view.Editor, Key.Add); var chosen = view.Editor.FontSize;
                PressKey(view.Editor, Key.S); Assert.False(model.Session.IsDirty); Assert.Equal(text, files.Library.Open(model.Session.Id).Source);
                window.Content = null; var recreated = new ScriptEditorView(model); window.Content = recreated;
                Assert.Equal(chosen, recreated.Editor.FontSize); Assert.Equal(text, recreated.Editor.Text);
                using var restarted = new ScriptEditorViewModel(docs, files.Library, preferencesPath: files.Settings);
                Assert.Equal(chosen, restarted.FontSize); Assert.Empty(Directory.GetFiles(files.Root, "*.tmp"));
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SavedLegacyFontIsShownInPointsAndResetUsesTwelveWithoutDrift()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var files = new Files(); Directory.CreateDirectory(files.Root);
            File.WriteAllText(files.Settings, "{\"Version\":1,\"FontSize\":18.666666666666668}");
            using var docs = new DocumentManager();
            using var model = new ScriptEditorViewModel(docs, files.Library, preferencesPath: files.Settings);
            var view = new ScriptEditorView(model);
            var window = new Window { Width = 700, Height = 350, Content = view }; window.Show();
            try
            {
                Assert.Equal(14, model.FontSizePoints); Assert.Equal("14 pt", view.ZoomReset.Content);
                Assert.Contains("\"Version\":1", File.ReadAllText(files.Settings));
                PressKey(view.Editor, Key.D0); Assert.Equal(12, model.FontSizePoints); Assert.Equal("12 pt", view.ZoomReset.Content);
                Assert.Contains("\"FontSizePoints\":12", File.ReadAllText(files.Settings));
            }
            finally { window.Close(); }
            using var memoryOnly = new ScriptEditorViewModel(docs, files.Library);
            for (var i = 0; i < 10000; i++) { memoryOnly.Zoom(1); memoryOnly.Zoom(-1); }
            Assert.Equal(12, memoryOnly.FontSizePoints); Assert.Equal(16, memoryOnly.FontSize);
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task DiagnosticsCanExpandNavigateAndCollapseWithoutReplacingDraftOrCodeArea()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager();
            var source = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse("MSH|^~\\&|SYN\rPID|1||SYN-1\r")); docs.Open(source);
            using var model = new ScriptEditorViewModel(docs, files.Library);
            await model.Session.NewAsync(Cancel); model.Session.Source = "// synthetic\nvar invalid = ;";
            var view = new ScriptEditorView(model); var window = new Window { Width = 700, Height = 350, Content = view }; window.Show();
            try
            {
                await model.RunAsync(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
                Assert.True(view.DiagnosticPanel.IsVisible); Assert.False(view.DiagnosticPanel.IsExpanded);
                var collapsedHeight = view.Editor.Bounds.Height;
                view.DiagnosticPanel.IsExpanded = true; window.UpdateLayout();
                Assert.True(view.Diagnostics.Bounds.Height > 0); Assert.True(view.Editor.Bounds.Height < collapsedHeight);
                view.Diagnostics.SelectedIndex = 0;
                PointerEventArgs? pointer = null;
                view.Diagnostics.PointerMoved += (_, e) => pointer = e;
                window.MouseMove(view.Diagnostics.TranslatePoint(new(20, 10), window)!.Value);
                Assert.NotNull(pointer);
                view.Diagnostics.RaiseEvent(new TappedEventArgs(InputElement.DoubleTappedEvent, pointer));
                Assert.Equal(2, view.Editor.TextArea.Caret.Line); Assert.Equal(model.Session.Source, view.Editor.Text);
                view.DiagnosticPanel.IsExpanded = false; window.UpdateLayout();
                Assert.Equal(collapsedHeight, view.Editor.Bounds.Height);
            }
            finally { window.Close(); docs.Close(source.Id); await source.Disposal; }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task UnavailableSettingsAndLibraryFailuresKeepUsableDraftAndVisibleStatus()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); Directory.CreateDirectory(files.Root); File.WriteAllText(files.Settings, "not json");
            using var docs = new DocumentManager();
            using var model = new ScriptEditorViewModel(docs, files.Library, preferencesPath: files.Settings);
            Assert.Equal(12, model.FontSizePoints); Assert.Contains("unavailable", model.SettingsStatus);
            await model.Session.NewAsync(Cancel); model.Session.Source = "// retained";
            File.Delete(files.Settings); Directory.CreateDirectory(files.Settings);
            model.Zoom(1); Assert.Contains("could not be saved", model.SettingsStatus); Assert.Equal("// retained", model.Session.Source);
            var view = new ScriptEditorView(model); var window = new Window { Width = 700, Height = 300, Content = view }; window.Show();
            try
            {
                model.Session.Name = ""; Assert.False(model.Session.Save()); Assert.True(model.Session.HasError);
                Assert.Contains("not saved", view.Status.Text!); Assert.True(model.Session.IsDirty);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
