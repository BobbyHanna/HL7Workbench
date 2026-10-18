using System.Text;
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
using AvaloniaEdit;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class SearchLayoutThemeTests
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261016000000||ADT^A01|SYN-1|T|2.5\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-search-layout-test-" + Guid.NewGuid().ToString("N"));
        public string Preferences => Path.Combine(Root, "editor-preferences.json");
        public Files() => Directory.CreateDirectory(Root);
        public void Dispose() => Directory.Delete(Root, true);
    }
    [Theory]
    [InlineData(900, 700)] [InlineData(400, 450)] [InlineData(400, 220)]
    public async Task SearchSplitterDragPersistenceModesAndReparentingKeepDiscoveryIntact(int width, int height)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var documents = new DocumentManager();
            using var input = new MemoryStream(Encoding.UTF8.GetBytes(Header + "PID|1||SYN-001\r"));
            var document = await MessageFileReader.OpenFileAsync(documents, "Synthetic", input, Token);
            var preferences = new ApplicationPreferences(files.Preferences); using var model = new SearchViewModel(documents, preferences: preferences);
            model.Value = "SYN-001"; var view = new SearchResultsView(model); var window = new Window { Width = width, Height = height, Content = view }; window.Show();
            Window? floating = null;
            try
            {
                Pump(window); Assert.False(view.Splitter.IsVisible); Assert.True(view.RowDefinitions[1].Height.IsAuto);
                model.BuilderMode = SearchBuilderMode.Advanced;
                var parent = model.RootGroup;
                for (var depth = 1; depth < 4; depth++)
                {
                    model.AddGroup(parent); parent = Assert.Single(parent.Children.OfType<SearchGroupViewModel>());
                    model.AddCondition(parent); parent.Children.OfType<SearchConditionViewModel>().Single().Target = SearchTarget.Segment;
                }
                Pump(window); Assert.True(view.Splitter.IsVisible);
                var before = view.RowDefinitions[1].ActualHeight;
                var available = before + view.RowDefinitions[3].ActualHeight;
                Assert.InRange(before / available, .56, .64); Assert.True(view.Search.IsEffectivelyVisible); Assert.True(view.Cancel.IsEffectivelyVisible);
                model.SearchCommand.Execute(null); await model.SearchingTask; Assert.Single(model.Session.Page); model.Session.FilterNavigator = true;
                var result = model.Session.Results; var pending = model.SearchingTask; var filter = document.NavigationMatches;
                var writes = 0; preferences.PropertyChanged += (_, _) => writes++;
                view.Splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragStartedEvent, Vector = default });
                for (var i = 0; i < 3; i++) view.Splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragDeltaEvent, Vector = new(0, available * .05 * (i + 1)) });
                Pump(window); Assert.True(view.RowDefinitions[1].ActualHeight > before); Assert.Equal(0, writes); Assert.False(File.Exists(files.Preferences));
                view.Splitter.RaiseEvent(new VectorEventArgs { RoutedEvent = Thumb.DragCompletedEvent, Vector = default }); Pump(window);
                Assert.Equal(1, writes); var fraction = model.BuilderFraction; Assert.InRange(fraction, .65, .9);
                Assert.Equal(fraction, EditorPreferences.Load(files.Preferences).SearchLayout!.AdvancedBuilderFraction, 3);
                var json = File.ReadAllText(files.Preferences); Assert.DoesNotContain("SYN-001", json); Assert.DoesNotContain("Results", json);
                view.Splitter.Focus(); view.Splitter.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Down }); Pump(window);
                view.Splitter.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Key.Down });
                Assert.True(model.BuilderFraction > fraction); fraction = model.BuilderFraction;
                Assert.Same(result, model.Session.Results); Assert.Same(pending, model.SearchingTask); Assert.Same(filter, document.NavigationMatches);
                var child = view.RootEditor.Rows.Children.OfType<SearchGroupEditor>().Single(); child.Expansion.IsExpanded = false; Pump(window);
                Assert.Equal(fraction, model.BuilderFraction); Assert.Same(result, model.Session.Results); child.Expansion.IsExpanded = true;
                model.BuilderMode = SearchBuilderMode.Simple; Pump(window); Assert.True(view.RowDefinitions[1].Height.IsAuto); Assert.False(view.Splitter.IsVisible);
                model.BuilderMode = SearchBuilderMode.Advanced; Pump(window); Assert.Equal(fraction, model.BuilderFraction);
                Assert.Equal(fraction, view.RowDefinitions[1].Height.Value, 3);
                model.SearchCommand.Execute(null); await model.SearchingTask; model.Session.FilterNavigator = true; result = model.Session.Results; pending = model.SearchingTask;
                window.Height += 100; Pump(window); Assert.Same(result, model.Session.Results); Assert.Same(pending, model.SearchingTask);
                window.Content = null; floating = new Window { Content = view, Width = width + 50, Height = height + 100 }; floating.Show(); Pump(floating);
                Assert.Equal(fraction, model.BuilderFraction); Assert.Same(result, model.Session.Results); Assert.True(model.Session.FilterNavigator);
                floating.Content = null; window.Content = view; Pump(window); Assert.Same(result, model.Session.Results);
                using var restarted = new SearchViewModel(documents, preferences: new(files.Preferences)); Assert.Equal(fraction, restarted.BuilderFraction);
                Assert.Equal(SearchBuilderMode.Simple, restarted.BuilderMode); Assert.Equal("", restarted.Value); Assert.Null(restarted.Session.Results);
                var recovered = new SearchResultsView(model); window.Content = recovered; Pump(window); Assert.Equal(fraction, recovered.RowDefinitions[1].Height.Value, 3);
                Assert.True(recovered.QueryEditor.Viewport.Height >= 0); Assert.True(recovered.Results.Bounds.Height >= 0);
            }
            finally { floating?.Close(); window.Close(); documents.Close(document.Id, true); await document.Disposal; }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(true, true)]
    public async Task BothHl7SyntaxVariantsUseResolvedPaletteForEverySupportedCategory(bool dark, bool custom)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            var raw = custom ? "MSH*$%!@*SYN\rPID*A$B@C%D*!Zescaped!\r" : "MSH|^~\\&|SYN\rPID|A^B&C~D|\\Zescaped\\\r";
            using var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(raw));
            var view = Assert.IsType<MessageInspectionView>(MessageViewer.CreateActive(document, true)); var editor = view.Editor;
            var window = new Window { Content = view, Width = 900, Height = 500, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light }; window.Show();
            try
            {
                Pump(window); editor.TextArea.TextView.EnsureVisualLines();
                var palette = dark ? Hl7EditorPalette.Dark : Hl7EditorPalette.Light;
                Assert.Same(palette, Hl7EditorTheme.PaletteFor(editor)); Assert.Equal(ColorOf(palette.EditorBackground), ColorOf(editor.Background));
                Assert.Equal(ColorOf(palette.EditorForeground), ColorOf(editor.Foreground)); Assert.Equal(ColorOf(palette.EditorCaret), ColorOf(editor.TextArea.Caret.CaretBrush));
                Assert.Equal(ColorOf(palette.EditorSelectionBackground), ColorOf(editor.TextArea.SelectionBrush)); Assert.Equal(ColorOf(palette.EditorSelectionForeground), ColorOf(editor.TextArea.SelectionForeground));
                Assert.Equal(ColorOf(palette.LineNumbers), ColorOf(editor.LineNumbersForeground));
                var map = new Hl7HighlightMap(document.Message);
                foreach (var kind in Enum.GetValues<Hl7HighlightKind>())
                {
                    var span = map.Spans.First(s => s.Kind == kind); Assert.Equal(ColorOf(palette.SyntaxForeground(kind)), ForegroundAt(editor, span.Offset));
                }
                Assert.Equal(raw, editor.Text); Assert.False(document.IsDirty);
                // A future profile can replace this resource without any renderer rewrite.
                window.Resources[Hl7EditorTheme.PaletteResourceKey] = palette with { SegmentForeground = Brushes.Red };
                Pump(window); editor.TextArea.TextView.EnsureVisualLines(); Assert.Equal(Colors.Red, ForegroundAt(editor, 0));
            }
            finally { window.Close(); }
            return true;
        }, Token);
    }
    private static Color ForegroundAt(TextEditor editor, int offset)
    {
        var line = editor.Document.GetLineByOffset(offset); var column = offset - line.Offset;
        var element = editor.TextArea.TextView.GetVisualLine(line.LineNumber).Elements.Single(e => column >= e.RelativeTextOffset && column < e.RelativeTextOffset + e.DocumentLength);
        return ColorOf(element.TextRunProperties.ForegroundBrush);
    }
    [Fact]
    public async Task LiveThemeChangesPreserveDraftHistoryCaretSelectionScrollAndEffectiveInheritance()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var raw = Header + string.Concat(Enumerable.Range(1, 100).Select(i => $"NTE|{i}||SYN-LINE-{i}\r"));
            using var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(raw)); var originalMessage = document.Message; var original = Hl7Serializer.Serialize(originalMessage);
            var view = Assert.IsType<MessageInspectionView>(MessageViewer.CreateActive(document, true)); var window = new Window { Content = view, Width = 900, Height = 400 }; window.Show();
            try
            {
                document.IsEditMode = true; view.Editor.Select(raw.IndexOf("SYN-LINE-1", StringComparison.Ordinal), 0);
                view.Editor.TextArea.PerformTextInput("SYN-EDIT-"); await document.EditRefresh; Pump(window); Assert.True(document.CanUndo);
                var textDocument = view.Editor.Document; var draft = document.WorkingText; var revision = document.SearchRevision;
                view.Editor.Select(view.Editor.Text.IndexOf("SYN-LINE-80", StringComparison.Ordinal), 5); view.Editor.ScrollToLine(80); Pump(window);
                var caret = view.Editor.CaretOffset; var start = view.Editor.SelectionStart; var length = view.Editor.SelectionLength; var scroll = view.Editor.TextArea.TextView.ScrollOffset;
                foreach (var preference in new[] { ApplicationThemePreference.Light, ApplicationThemePreference.Dark, ApplicationThemePreference.FollowSystem, ApplicationThemePreference.Light })
                {
                    ApplicationTheme.Apply(preference); Pump(window);
                    Assert.Same(textDocument, view.Editor.Document); Assert.Equal(draft, document.WorkingText); Assert.Equal(revision, document.SearchRevision);
                    Assert.True(document.CanUndo); Assert.True(document.IsDirty); Assert.Equal(caret, view.Editor.CaretOffset); Assert.Equal(start, view.Editor.SelectionStart); Assert.Equal(length, view.Editor.SelectionLength);
                    Assert.Equal(scroll, view.Editor.TextArea.TextView.ScrollOffset); Assert.Equal(ColorOf(Hl7EditorTheme.Resolve(window.ActualThemeVariant).EditorBackground), ColorOf(view.Editor.Background));
                }
                ApplicationTheme.Apply(ApplicationThemePreference.FollowSystem); Assert.Equal(ThemeVariant.Default, Application.Current!.RequestedThemeVariant);
                // Simulate an inherited effective-variant notification without changing the OS.
                window.RequestedThemeVariant = ThemeVariant.Dark; Pump(window); Assert.Same(Hl7EditorPalette.Dark, Hl7EditorTheme.PaletteFor(view.Editor));
                window.RequestedThemeVariant = ThemeVariant.Light; Pump(window); Assert.Same(Hl7EditorPalette.Light, Hl7EditorTheme.PaletteFor(view.Editor));
                document.Undo(); await document.EditRefresh; Assert.Equal(raw, view.Editor.Text); document.Redo(); await document.EditRefresh; Assert.Equal(draft, document.WorkingText);
                Assert.Equal(original, Hl7Serializer.Serialize(originalMessage));
            }
            finally { window.Close(); ApplicationTheme.Apply(ApplicationThemePreference.FollowSystem); }
            return true;
        }, Token);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SourceBackedAndVirtualEditorsAndFullSourceWindowChangeThemeWithoutReload(bool virtualized)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var documents = new DocumentManager();
            var text = Header + "OBX|1|TX|||" + (virtualized ? string.Join('~', Enumerable.Repeat("SYN-12345678", 2000)) : new string('x', 256 * 1024)) + "\rNTE|1||SYN-END\r";
            var bytes = Encoding.UTF8.GetBytes(text); using var input = new MemoryStream(bytes);
            var document = await MessageFileReader.OpenFileAsync(documents, "Synthetic", input, Token); Assert.NotNull(document.Source);
            var view = new FileDocumentView(document); var window = new Window { Content = view, Width = 1100, Height = 650 }; window.Show();
            using var session = new RawInspectorSession(document); using var full = new FullRawInspectorView(session); var floating = new Window { Content = full, Width = 900, Height = 500 }; floating.Show();
            try
            {
                await view.ActiveView!.PresentationView!.Ready; await full.Ready; Pump(window); Pump(floating);
                Assert.Equal(virtualized, view.ActiveView.PresentationView.IsVirtualized); var ready = view.ActiveView.PresentationView.Ready; var fullReady = full.Ready;
                var fullDocument = full.Editor.Document; var source = document.Source;
                foreach (var preference in new[] { ApplicationThemePreference.Light, ApplicationThemePreference.Dark })
                {
                    ApplicationTheme.Apply(preference); Pump(window); Pump(floating); var palette = Hl7EditorTheme.Resolve(window.ActualThemeVariant);
                    foreach (var editor in window.GetVisualDescendants().OfType<TextEditor>().Concat(new[] { full.Editor }))
                    { Assert.Equal(ColorOf(palette.EditorBackground), ColorOf(editor.Background)); Assert.Equal(ColorOf(palette.EditorForeground), ColorOf(editor.Foreground)); }
                    if (virtualized) Assert.Equal(ColorOf(palette.EditorBackground), ColorOf(Assert.Single(view.ActiveView.PresentationView.GetVisualDescendants().OfType<ListBox>()).Background));
                    Assert.Same(source, document.Source); Assert.Same(ready, view.ActiveView.PresentationView.Ready); Assert.Same(fullReady, full.Ready); Assert.Same(fullDocument, full.Editor.Document);
                    full.Editor.TextArea.TextView.EnsureVisualLines(); Assert.Equal(ColorOf(palette.SegmentForeground), ForegroundAt(full.Editor, 0));
                }
                Assert.False(document.IsDirty); Assert.Equal(bytes, document.Snapshot!.ReadBytes(new(0, bytes.Length), bytes.Length));
            }
            finally { floating.Close(); window.Close(); documents.Close(document.Id, true); await document.Disposal; ApplicationTheme.Apply(ApplicationThemePreference.FollowSystem); }
            return true;
        }, Token);
    }
    [Fact]
    public async Task SemanticLightResourcesAndSelectionRemainReadableAcrossWindows()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var documents = new DocumentManager(); using var model = new SearchViewModel(documents); model.Value = "SYN"; model.BuilderMode = SearchBuilderMode.Advanced; model.AddGroup(model.RootGroup);
            var view = new SearchResultsView(model); var window = new Window { Content = view, Width = 900, Height = 600 }; window.Show();
            var floating = new Window { Content = new TextBlock { Text = "Synthetic floating tool" } }; floating.Show();
            try
            {
                foreach (var preference in new[] { ApplicationThemePreference.Light, ApplicationThemePreference.Dark })
                {
                    ApplicationTheme.Apply(preference); Pump(window); Pump(floating);
                    foreach (var key in new[] { WorkbenchThemeResources.Panel, WorkbenchThemeResources.Surface, WorkbenchThemeResources.Group, WorkbenchThemeResources.Border, WorkbenchThemeResources.Accent, WorkbenchThemeResources.Validation })
                    { Assert.True(window.TryFindResource(key, window.ActualThemeVariant, out var resource)); Assert.IsAssignableFrom<IBrush>(resource); }
                    Assert.Equal(window.ActualThemeVariant, floating.ActualThemeVariant); Assert.NotNull(view.RootEditor.BorderBrush);
                    Assert.NotNull(view.RootEditor.Rows.Children.OfType<SearchGroupEditor>().Single().Background);
                    view.Results.ItemsSource = new[] { new MessageSearchMatch(1, "ADT", "SYN", "PID-3", 1) }; view.Results.SelectedIndex = 0; Pump(window);
                    Assert.True(Assert.IsType<ListBoxItem>(view.Results.ContainerFromIndex(0)).IsSelected);
                }
                var light = Hl7EditorPalette.Light;
                foreach (var foreground in Enum.GetValues<Hl7HighlightKind>().Select(light.SyntaxForeground).Concat(new[] { light.EditorForeground, light.LineNumbers })) Assert.True(Contrast(foreground, light.EditorBackground) >= 4.5);
                Assert.True(Contrast(light.EditorSelectionForeground, light.EditorSelectionBackground) >= 4.5); Assert.True(Contrast(light.OmissionForeground, light.OmissionBackground) >= 4.5);
            }
            finally { floating.Close(); window.Close(); ApplicationTheme.Apply(ApplicationThemePreference.FollowSystem); }
            return true;
        }, Token);
    }
    private static double Contrast(IBrush foreground, IBrush background)
    {
        double Luminance(Color color)
        {
            double Channel(byte value) { var s = value / 255.0; return s <= .04045 ? s / 12.92 : Math.Pow((s + .055) / 1.055, 2.4); }
            return .2126 * Channel(color.R) + .7152 * Channel(color.G) + .0722 * Channel(color.B);
        }
        var a = Luminance(ColorOf(foreground)); var b = Luminance(ColorOf(background)); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
}
