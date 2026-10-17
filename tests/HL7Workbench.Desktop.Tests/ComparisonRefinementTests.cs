using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ComparisonRefinementTests
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261016000000||ORU^R01|SYN-PASTE|T|2.5\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static T Field<T>(MainWindow window, string name) => (T)typeof(MainWindow).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
    [Fact]
    public async Task RapidPasteEditsDebounceCancelSupersededWorkAndPreserveTheOtherSide()
    {
        using var documents = new DocumentManager(); using var model = new ComparisonViewModel(documents);
        model.SetPastedText(true, Header + "PID|A\rOBX|KEEP\r"); var originalA = model.InputA.PastedText;
        var pending = new List<Task>();
        for (var i = 0; i < 80; i++) { model.SetPastedText(false, Header + $"PID|V{i}\rOBX|CHANGED\r"); pending.Add(model.Pending); }
        Assert.Null(model.Comparison); Assert.False(model.IsBusy); await Task.WhenAll(pending);
        Assert.Equal("V79", model.Differences[0].AfterValue); Assert.Equal(originalA, model.InputA.PastedText); Assert.Empty(documents.Documents);
        model.Select(model.Differences[1]); var path = model.Selected!.Path;
        model.SetPastedText(false, Header + "PID|V80\rOBX|CHANGED-AGAIN\r"); await model.Pending;
        Assert.Equal(path, model.Selected!.Path); Assert.Equal("CHANGED-AGAIN", model.Selected.AfterValue);
        model.SetPastedText(false, Header + "PID|CANCELED\r"); var canceled = model.Pending; model.Cancel(); await canceled; Assert.Null(model.Comparison);
        Assert.Equal(originalA, model.InputA.PastedText);
    }
    [Fact]
    public async Task SourceSwitchingAndClosureKeepInactivePastedDraftsWithoutInvalidatingUnrelatedResults()
    {
        using var documents = new DocumentManager(); var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Header + "PID|DOC\r")); documents.Open(doc);
        using var model = new ComparisonViewModel(documents); model.SetPastedText(true, Header + "PID|A\r"); model.SetPastedText(false, Header + "PID|B\r"); await model.Pending;
        var draft = model.InputA.PastedText; model.InputA.SelectDocument(doc); model.InputA.Kind = ComparisonSourceKind.OpenMessage; await model.Pending;
        Assert.Equal("DOC", model.Differences[0].BeforeValue); Assert.Equal(draft, model.InputA.PastedText);
        model.InputA.Kind = ComparisonSourceKind.OriginalMessage; await model.Pending; Assert.Contains("Original Message", model.Before!.Label);
        model.InputA.Kind = ComparisonSourceKind.PastedText; await model.Pending; Assert.Equal(draft, model.Before!.PastedText);
        var result = model.Comparison; documents.Close(doc.Id); Assert.Same(result, model.Comparison); Assert.Null(model.InputA.Document); Assert.Equal(draft, model.InputA.PastedText);
        var doc2 = new MessageDocument("second", "Synthetic second", Hl7Parser.Parse(Header + "PID|SECOND\r")); documents.Open(doc2);
        model.InputA.SelectDocument(doc2); model.InputA.Kind = ComparisonSourceKind.OpenMessage; await model.Pending; documents.Close(doc2.Id);
        Assert.Null(model.Before); Assert.Null(model.Comparison); Assert.Contains("closed", model.Status); Assert.Equal(draft, model.InputA.PastedText);
        model.InputA.Kind = ComparisonSourceKind.PastedText; await model.Pending; Assert.NotNull(model.Comparison);
    }
    [Fact]
    public async Task ClearReplaceIgnoreOptionsAndDisposalAreSessionOnly()
    {
        using var documents = new DocumentManager(); var model = new ComparisonViewModel(documents);
        model.SetPastedText(true, Header + "PID|A\r"); model.SetPastedText(false, Header.Replace("SYN-PASTE", "SYN-OTHER", StringComparison.Ordinal) + "PID|B\r"); await model.Pending;
        Assert.Equal(2, model.Differences.Count); model.SetOptions(new(IgnoreMsh10: true)); await model.Pending; Assert.Single(model.Differences);
        model.SetPastedText(true, ""); Assert.Null(model.Comparison); Assert.Contains("B", model.InputB.PastedText);
        model.SetPastedText(true, Header + "PID|B\r"); await model.Pending; Assert.Equal("No differences found.", model.Status);
        var unchanged = model.Comparison;
        Assert.False(model.SetPastedText(true, new string('X', ComparisonInput.MaximumPastedCharacters + 1))); Assert.Same(unchanged, model.Comparison); Assert.Contains("retained", model.Status);
        model.SetPastedText(false, Header + Header); await model.Pending; Assert.Null(model.Comparison); Assert.Contains("multiple MSH", model.Status); Assert.Equal(Header + Header, model.InputB.PastedText);
        model.Dispose(); await model.Pending; Assert.Empty(model.InputA.PastedText); Assert.Empty(model.InputB.PastedText); Assert.Null(model.Comparison);
    }
    [Fact]
    public async Task DefaultSideBySideCopiesAndViewSwitchesShareResultSelectionAndLoadedPanes()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); using var model = new ComparisonViewModel(documents); var view = new ComparisonView(model);
            var window = new Window { Width = 900, Height = 650, Content = view }; window.Show();
            try
            {
                var tail = string.Concat(Enumerable.Range(2, 800).Select(i => $"OBX|{i}|TX|SYN||UNCHANGED\r"));
                model.SetPastedText(true, Header + "PID|A\rOBX|ONE\r" + tail); model.SetPastedText(false, Header + "PID|B\rOBX|TWO\r" + tail); await model.Pending; await view.Loading;
                model.SetOptions(new(IgnoreMsh10: true)); await model.Pending; await view.Loading; Pump(window);
                Assert.Equal(ComparisonPresentation.SideBySide, model.Presentation); Assert.True(view.BeforeEditor.IsEffectivelyVisible); Assert.False(view.Navigator.IsEffectivelyVisible);
                Assert.True(view.SideBySideMode.IsChecked); Assert.False(view.DifferencesMode.IsChecked);
                view.DifferencesMode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(window);
                Assert.True(view.Navigator.IsEffectivelyVisible); Assert.False(view.BeforeEditor.IsEffectivelyVisible);
                model.Select(model.Differences[1]); await view.Loading;
                await view.CopyAsync("HL7 Path"); Assert.Equal("OBX[1]-1", await window.Clipboard!.TryGetTextAsync());
                await view.CopyAsync("Before Value"); Assert.Equal("ONE", await window.Clipboard!.TryGetTextAsync());
                var result = model.Comparison; var selected = model.Selected; var loaded = view.Loading; var text = view.BeforeEditor.Text;
                view.SideBySideMode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(window);
                Assert.True(view.BeforeEditor.IsEffectivelyVisible); Assert.False(view.Navigator.IsEffectivelyVisible); Assert.Equal("ONE", view.BeforeEditor.SelectedText);
                Assert.Same(loaded, view.Loading); Assert.Same(result, model.Comparison); Assert.Same(selected, model.Selected);
                var before = model.Before; var after = model.After; var options = model.Options; var pending = model.Pending;
                view.BeforeEditor.ScrollToLine(300); view.AfterEditor.ScrollToLine(500); Pump(window);
                var scrollA = view.BeforeEditor.TextArea.TextView.ScrollOffset; var scrollB = view.AfterEditor.TextArea.TextView.ScrollOffset;
                var caret = view.BeforeEditor.CaretOffset; Assert.True(scrollA.Y > 0, $"A scroll: {scrollA}"); Assert.True(scrollB.Y > scrollA.Y, $"A: {scrollA}; B: {scrollB}");
                view.DifferencesMode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.Same(loaded, view.Loading); Assert.Equal(text, view.BeforeEditor.Text);
                Assert.Same(selected, view.Navigator.SelectedItem); Assert.True(view.DifferencesMode.IsChecked);
                view.SideBySideMode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(window);
                Assert.Equal(scrollA, view.BeforeEditor.TextArea.TextView.ScrollOffset); Assert.Equal(scrollB, view.AfterEditor.TextArea.TextView.ScrollOffset); Assert.Equal(caret, view.BeforeEditor.CaretOffset);
                Assert.Same(before, model.Before); Assert.Same(after, model.After); Assert.Same(options, model.Options); Assert.Same(pending, model.Pending); Assert.Same(loaded, view.Loading);
                view.DifferencesMode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                window.RequestedThemeVariant = ThemeVariant.Dark; Pump(window); Assert.Same(result, model.Comparison); Assert.Equal(Header + "PID|A\rOBX|ONE\r" + tail, model.InputA.PastedText);
                var floating = new Window { Width = 900, Height = 650 }; window.Content = null; floating.Content = view; floating.Show(); await view.Loading;
                Assert.Equal(ComparisonPresentation.Differences, model.Presentation); Assert.True(view.DifferencesMode.IsChecked);
                Assert.Same(result, model.Comparison); Assert.Same(selected, model.Selected); Assert.Equal(Header + "PID|A\rOBX|ONE\r" + tail, model.InputA.PastedText); floating.Close();
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData("\r")] [InlineData("\n")] [InlineData("\r\n")]
    public async Task CtrlVPastesBothSourcesFromDefaultViewWithoutNormalizingClipboardText(string ending)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); using var model = new ComparisonViewModel(documents); var view = new ComparisonView(model);
            var window = new Window { Width = 900, Height = 600, Content = view }; window.Show();
            try
            {
                var a = (Header + "PID|A\r").Replace("\r", ending, StringComparison.Ordinal); var b = (Header + "PID|B\r").Replace("\r", ending, StringComparison.Ordinal);
                await window.Clipboard!.SetTextAsync(a); view.SourceA.SourceButton.Focus();
                view.SourceA.SourceButton.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.V, KeyModifiers = KeyModifiers.Control }); await view.SourceA.ClipboardTask;
                Assert.Equal(a, model.InputA.PastedText); Assert.Empty(model.InputB.PastedText); Assert.Null(model.Comparison);
                await window.Clipboard!.SetTextAsync(b); view.SourceB.SourceButton.Focus();
                view.SourceB.SourceButton.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.V, KeyModifiers = KeyModifiers.Control }); await view.SourceB.ClipboardTask;
                await model.Pending; Assert.Single(model.Differences); Assert.Equal(b, model.InputB.PastedText); Assert.Empty(documents.Documents);
                Assert.Equal(ComparisonPresentation.SideBySide, model.Presentation);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task EmptyDefaultPanesAcceptExactClipboardTextAndTypingWithoutVisitingDifferences()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); using var model = new ComparisonViewModel(documents); var view = new ComparisonView(model);
            var window = new Window { Width = 900, Height = 650, Content = view }; window.Show();
            try
            {
                Pump(window);
                var a = Header + "PID|A\r\n"; var b = Header + "PID|B\r\n";
                await window.Clipboard!.SetTextAsync(a); view.BeforeEditor.Focus(); view.BeforeEditor.TextArea.Focus();
                view.BeforeEditor.TextArea.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.V, KeyModifiers = KeyModifiers.Control }); await view.ClipboardTask;
                await window.Clipboard!.SetTextAsync(b); view.AfterEditor.Focus(); view.AfterEditor.TextArea.Focus();
                view.AfterEditor.TextArea.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.V, KeyModifiers = KeyModifiers.Control }); await view.ClipboardTask;
                await model.Pending; await view.Loading;
                Assert.Equal(a, view.BeforeEditor.Text); Assert.Equal(b, view.AfterEditor.Text); Assert.Single(model.Differences);
                view.AfterEditor.Select(b.IndexOf("PID|B", StringComparison.Ordinal) + 5, 0); view.AfterEditor.TextArea.PerformTextInput("-EDIT"); await model.Pending; await view.Loading;
                Assert.Equal(a, model.InputA.PastedText); Assert.Equal(Header + "PID|B-EDIT\r\n", model.InputB.PastedText);
                Assert.Equal(ComparisonPresentation.SideBySide, model.Presentation); Assert.True(view.SideBySideMode.IsChecked); Assert.Empty(documents.Documents);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task SourceFlyoutAllowsExactPasteAndEditingWithoutStealingTypingFocus()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); using var model = new ComparisonViewModel(documents); var view = new ComparisonView(model);
            var window = new Window { Width = 1000, Height = 700, Content = view }; window.Show();
            try
            {
                model.SetPastedText(false, Header + "PID|B\r\n");
                view.SourceA.SourceButton.Flyout!.ShowAt(view.SourceA.SourceButton); Pump(window);
                var editor = view.SourceA.PasteEditor; editor.Focus(); editor.TextArea.Focus();
                var text = Header + "PID|A\r\n"; await window.Clipboard!.SetTextAsync(text);
                editor.TextArea.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.V, KeyModifiers = KeyModifiers.Control }); await view.SourceA.ClipboardTask;
                Assert.Equal(text, editor.Text); Assert.Equal(text, model.InputA.PastedText);
                var caret = editor.CaretOffset; await model.Pending; Assert.Equal(caret, editor.CaretOffset); Assert.True(editor.IsKeyboardFocusWithin);
                editor.CaretOffset = text.IndexOf("PID|A", StringComparison.Ordinal) + 5; editor.TextArea.PerformTextInput("X"); await model.Pending;
                Assert.Contains("PID|AX", model.InputA.PastedText); Assert.Contains("PID|B", model.InputB.PastedText); Assert.True(editor.IsKeyboardFocusWithin);
                view.SourceA.KindChoice.SelectedIndex = (int)ComparisonSourceKind.OpenMessage; Assert.Contains("PID|AX", model.InputA.PastedText);
                view.SourceA.KindChoice.SelectedIndex = (int)ComparisonSourceKind.PastedText; Assert.Contains("PID|AX", editor.Text);
                view.SourceA.SourceButton.Flyout.Hide();
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task SideBySideAllowsPastedEditingWhileDocumentAndHistoryStayReadOnly()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Header + "PID|ORIGINAL\r")); documents.Open(doc);
            doc.IsEditMode = true; doc.ReplaceWorkingText(Header + "PID|DRAFT\r"); var version = doc.SearchRevision; var draft = doc.WorkingText;
            using var model = new ComparisonViewModel(documents); model.SetSources(ComparisonSource.FromPastedText(Header + "PID|PASTED"), new(doc, 1, true));
            model.Presentation = ComparisonPresentation.SideBySide; var view = new ComparisonView(model); var window = new Window { Width = 1000, Height = 700, Content = view }; window.Show();
            try
            {
                await model.CompareAsync(); await view.Loading; Pump(window); Assert.False(view.BeforeEditor.IsReadOnly); Assert.True(view.AfterEditor.IsReadOnly);
                view.BeforeEditor.Focus(); view.BeforeEditor.TextArea.Focus(); view.BeforeEditor.Select(view.BeforeEditor.Text.Length, 0);
                view.BeforeEditor.TextArea.PerformTextInput("-EDIT"); var caret = view.BeforeEditor.CaretOffset; await model.Pending; await view.Loading;
                Assert.EndsWith("PASTED-EDIT", model.InputA.PastedText); Assert.Equal(caret, view.BeforeEditor.CaretOffset); Assert.True(view.BeforeEditor.IsKeyboardFocusWithin);
                view.AfterEditor.TextArea.PerformTextInput("NO WRITE"); Assert.Equal(Header + "PID|ORIGINAL\r", view.AfterEditor.Text);
                Assert.Equal(version, doc.SearchRevision); Assert.Equal(draft, doc.WorkingText); Assert.True(doc.CanUndo); Assert.False(doc.CanRedo);
            }
            finally { window.Close(); documents.Close(doc.Id, true); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task SettingsOnlyHasPreferencesAndEditDefaultStillPersistsAndAppliesOnStartup()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-stage7-settings-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var window = new MainWindow(root); window.Show();
                try
                {
                    var settings = window.FindControl<Menu>("MainMenu")!.Items.Cast<MenuItem>().Single(m => Equals(m.Header, "_Settings"));
                    Assert.Equal("_Preferences…", Assert.Single(settings.Items.Cast<MenuItem>()).Header); Assert.Null(window.FindControl<MenuItem>("StartInEditMode"));
                    var manager = Field<DocumentManager>(window, "documents"); var existing = new MessageDocument("old", "Synthetic old", Hl7Parser.Parse("PID|OLD")); manager.Open(existing); Assert.False(existing.IsEditMode);
                    var preferences = new PreferencesWindow(Field<ApplicationPreferences>(window, "preferences")); preferences.Show();
                    Assert.True(preferences.EditByDefault.IsEffectivelyVisible); preferences.EditByDefault.IsChecked = true;
                    preferences.ApplyButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); preferences.Close();
                    Assert.False(existing.IsEditMode); var fresh = new MessageDocument("new", "Synthetic new", Hl7Parser.Parse("PID|NEW")); manager.Open(fresh); Assert.True(fresh.IsEditMode);
                }
                finally { window.Close(); }
                var restarted = new MainWindow(root); restarted.Show();
                try
                {
                    var state = Field<ApplicationPreferences>(restarted, "preferences"); Assert.True(state.Current.StartInEditMode);
                    var manager = Field<DocumentManager>(restarted, "documents"); var doc = new MessageDocument("restart", "Synthetic restart", Hl7Parser.Parse("PID|RESTART")); manager.Open(doc); Assert.True(doc.IsEditMode);
                    var preferences = new PreferencesWindow(state); Assert.True(preferences.EditByDefault.IsChecked);
                }
                finally { restarted.Close(); }
            }
            finally { Directory.Delete(root, true); }
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task EmptyWorkspaceMakesPasteDiscoverableAndScrollableAtDockedSize(bool dark)
    {
        var render = Environment.GetEnvironmentVariable("HL7WORKBENCH_COMPARISON_RENDER") == "1";
        await using var ui = HeadlessUnitTestSession.StartNew(render ? typeof(ComparisonRenderTestApplication) : typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var documents = new DocumentManager(); using var model = new ComparisonViewModel(documents); var view = new ComparisonView(model);
            var window = new Window { Width = 600, Height = 450, Content = view, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Pump(window); Assert.False(view.Compare.IsEnabled); Assert.Equal(ComparisonPresentation.SideBySide, model.Presentation);
                Assert.Empty(documents.Documents); Assert.True(view.SideBySideMode.IsChecked); Assert.False(view.DifferencesMode.IsChecked);
                foreach (var editor in new[] { view.BeforeEditor, view.AfterEditor })
                {
                    Assert.True(editor.IsEffectivelyVisible); Assert.False(editor.IsReadOnly); Assert.Empty(editor.Text);
                    Assert.True(editor.Bounds.Width >= 180); Assert.True(editor.Bounds.Height >= 120);
                    editor.Focus(); editor.TextArea.Focus(); Assert.True(editor.IsKeyboardFocusWithin);
                }
                if (render)
                {
                    var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/comparison-visual")); Directory.CreateDirectory(directory);
                    using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
                    frame.Save(Path.Combine(directory, $"600-empty-{(dark ? "dark" : "light")}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                }
                view.DifferencesMode.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump(window);
                Assert.True(view.DifferencesMode.IsChecked); Assert.False(view.SideBySideMode.IsChecked); Assert.False(view.BeforeEditor.IsEffectivelyVisible);
                var buttons = view.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible).ToArray();
                Assert.Contains(buttons, b => Equals(b.Content, "Paste HL7 into A")); Assert.Contains(buttons, b => Equals(b.Content, "Paste HL7 into B"));
            }
            finally { window.Close(); }
        }, Token);
    }
    [Theory]
    [InlineData(1000, 700, false, false)] [InlineData(1000, 700, true, false)]
    [InlineData(1000, 700, false, true)] [InlineData(1000, 700, true, true)]
    [InlineData(600, 450, false, false)] [InlineData(600, 450, true, false)]
    [InlineData(600, 450, false, true)] [InlineData(600, 450, true, true)]
    public async Task PresentationFitsNormalAndNarrowWindowsAndRetainsStateAcrossThemes(int width, int height, bool sideBySide, bool dark)
    {
        var render = Environment.GetEnvironmentVariable("HL7WORKBENCH_COMPARISON_RENDER") == "1";
        await using var ui = HeadlessUnitTestSession.StartNew(render ? typeof(ComparisonRenderTestApplication) : typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager(); using var model = new ComparisonViewModel(documents); var view = new ComparisonView(model);
            model.Presentation = sideBySide ? ComparisonPresentation.SideBySide : ComparisonPresentation.Differences;
            var window = new Window { Width = width, Height = height, Content = view, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                model.SetPastedText(true, Header + "PID|Example^Ada\rOBX|1|TX|SYN-A||" + new string('A', 600) + "\rOBX|2|TX|SYN-B||UNCHANGED\r");
                model.SetPastedText(false, Header + "PID|Example^Bea\rOBX|1|TX|SYN-A||" + new string('B', 600) + "\rOBX|99|TX|SYN-NEW||INSERTED\rOBX|2|TX|SYN-B||UNCHANGED\r");
                await model.Pending; await view.Loading; Pump(window); Assert.Equal(3, model.Differences.Count);
                foreach (var button in view.GetVisualDescendants().OfType<Button>().Where(b => b.IsEffectivelyVisible))
                { var right = button.TranslatePoint(new(button.Bounds.Width, 0), view); if (right is { } point) Assert.InRange(point.X, 0, view.Bounds.Width + 1); }
                if (sideBySide) { Assert.True(view.BeforeEditor.Bounds.Height >= 120, $"Editor height: {view.BeforeEditor.Bounds.Height}"); Assert.True(view.BeforeEditor.Bounds.Width >= 180, $"Editor width: {view.BeforeEditor.Bounds.Width}"); }
                else { Assert.True(view.Navigator.Bounds.Height >= 150); Assert.False(view.BeforeEditor.IsEffectivelyVisible); }
                var selected = model.Selected; var result = model.Comparison; var input = model.InputA.PastedText;
                if (render)
                {
                    var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/comparison-visual")); Directory.CreateDirectory(directory);
                    using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
                    frame.Save(Path.Combine(directory, $"{width}-{(sideBySide ? "sources" : "differences")}-{(dark ? "dark" : "light")}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                }
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark; Pump(window);
                Assert.Same(selected, model.Selected); Assert.Same(result, model.Comparison); Assert.Equal(input, model.InputA.PastedText);
                Assert.Same(model.Selected, view.Navigator.SelectedItem);
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
}

public sealed class ComparisonRenderTestApplication : Application
{
    public override void Initialize()
    {
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        Styles.Add(new Avalonia.Markup.Xaml.Styling.StyleInclude(new Uri("avares://HL7Workbench.Desktop.Tests/"))
        { Source = new Uri("avares://AvaloniaEdit/Themes/Fluent/AvaloniaEdit.xaml") });
        ApplicationTheme.InitializeResources(this);
    }
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<ComparisonRenderTestApplication>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
