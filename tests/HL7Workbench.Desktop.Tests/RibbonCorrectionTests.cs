using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed partial class ScriptEditorTests
{
    [Fact]
    public async Task RibbonPointerSwitchesOnFirstClickWithoutDismissalAndSelectedTabToggles()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            using var files = new Files(); using var docs = new DocumentManager(); using var model = new ScriptEditorViewModel(docs, files.Library);
            var state = new ApplicationPreferences(Path.Combine(files.Root, "preferences.json"));
            var ribbon = new UtilityRibbon(state, model, RibbonActions());
            var dock = new Border(); var root = new Grid { RowDefinitions = new("Auto,*") };
            root.Children.Add(ribbon); Grid.SetRow(dock, 1); root.Children.Add(dock);
            var window = new Window { Width = 1100, Height = 600, Content = root }; window.Show();
            try
            {
                window.UpdateLayout(); var height = dock.Bounds.Height;
                void Click(Control control)
                {
                    var point = Center(control, window); window.MouseMove(point);
                    window.MouseDown(point, MouseButton.Left); window.MouseUp(point, MouseButton.Left);
                    window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                }
                Click(ribbon.Tabs[0]); Assert.True(ribbon.TemporaryPopup.IsOpen);
                var host = ribbon.TemporaryPopup.Child; var closed = 0; ribbon.TemporaryPopup.Closed += (_, _) => closed++;
                foreach (var index in new[] { 1, 2, 3, 0, 1 })
                {
                    var point = Center(ribbon.Tabs[index], window); window.MouseMove(point); window.MouseDown(point, MouseButton.Left);
                    Assert.True(ribbon.TemporaryPopup.IsOpen); Assert.Equal(0, closed);
                    window.MouseUp(point, MouseButton.Left); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                    Assert.Equal(index, ribbon.SelectedTab); Assert.True(ribbon.TemporaryPopup.IsOpen);
                    Assert.Same(host, ribbon.TemporaryPopup.Child); Assert.Equal(0, closed); Assert.Equal(height, dock.Bounds.Height);
                }
                Click(ribbon.Tabs[1]); Assert.False(ribbon.TemporaryPopup.IsOpen);
                Click(ribbon.Tabs[1]); Assert.True(ribbon.TemporaryPopup.IsOpen);
                window.MouseDown(new Point(30, 500), MouseButton.Left); window.MouseUp(new Point(30, 500), MouseButton.Left);
                Assert.False(ribbon.TemporaryPopup.IsOpen); Assert.Equal(height, dock.Bounds.Height);
                Click(ribbon.Tabs[0]); SendShellKey(ribbon.TemporaryPopup.Child!, Avalonia.Input.Key.Escape); Assert.False(ribbon.TemporaryPopup.IsOpen);
                Click(ribbon.Tabs[1]); Click(ribbon.Pin); Assert.True(ribbon.IsExpanded); Assert.False(ribbon.TemporaryPopup.IsOpen);
                var pinnedHeight = dock.Bounds.Height;
                Click(ribbon.Tabs[2]); Assert.Equal(2, ribbon.SelectedTab); Assert.True(ribbon.IsExpanded); Assert.Equal(pinnedHeight, dock.Bounds.Height);
                Click(ribbon.Tabs[2]); Assert.True(ribbon.IsExpanded);
                Assert.True(new ApplicationPreferences(Path.Combine(files.Root, "preferences.json")).Current.RibbonExpanded);
                Click(ribbon.Pin); Assert.False(ribbon.IsExpanded); Assert.False(ribbon.TemporaryPopup.IsOpen); Assert.Equal(2, ribbon.SelectedTab);
                Assert.False(new ApplicationPreferences(Path.Combine(files.Root, "preferences.json")).Current.RibbonExpanded);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task RibbonScriptGroupsShareCommandsAndTrackDecisionsAndScope()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager(); using var model = new ScriptEditorViewModel(docs, files.Library);
            docs.Open(new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Synthetic)));
            var state = new ApplicationPreferences(Path.Combine(files.Root, "preferences.json")); Assert.True(state.Apply(state.Current with { RibbonExpanded = true }));
            var ribbon = new UtilityRibbon(state, model, RibbonActions()); var window = new Window { Content = ribbon, Width = 1100, Height = 240 }; window.Show();
            try
            {
                ribbon.SelectTab(1); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var buttons = RibbonButtons(ribbon.PinnedHost);
                Assert.Equal(new[] { "New", "Save", "Duplicate", "Rename", "Delete", "Open Script Editor", "Run", "Cancel", "Review Output", "Reports" }, buttons.Select(AutomationProperties.GetName));
                var menu = new MenuItem(); ScriptCommandMenu.Populate(menu, model);
                // Inventory the removed menu against the live ribbon: every former
                // entry must retain its exact command instance and operation gate.
                Assert.Equal(menu.Items.Count, buttons.Length);
                foreach (var item in menu.Items.Cast<MenuItem>()) Assert.Single(buttons, b => ReferenceEquals(item.Command, b.Command));
                Assert.Same(model.OpenCommand, buttons[5].Command);
                Assert.Same(model.RunCommand, buttons[6].Command); Assert.Contains("accent", buttons[6].Classes);
                Assert.Same(model.ReviewCommand, buttons[8].Command); Assert.Same(model.ReportsCommand, buttons[9].Command);
                Assert.False(buttons[1].IsEffectivelyEnabled); Assert.False(buttons[3].IsEffectivelyEnabled); Assert.False(buttons[4].IsEffectivelyEnabled);
                Assert.True(buttons[5].IsEffectivelyEnabled); Assert.True(buttons[6].IsEffectivelyEnabled); Assert.False(buttons[7].IsEffectivelyEnabled);
                Assert.True(await model.Session.NewAsync(Cancel)); Assert.True(model.Session.Save());
                Assert.True(buttons[1].IsEffectivelyEnabled); Assert.True(buttons[3].IsEffectivelyEnabled); Assert.True(buttons[4].IsEffectivelyEnabled);
                var scope = ribbon.PinnedHost.GetVisualDescendants().OfType<ComboBox>().Single();
                model.Scope = Scripting.ScriptScope.WholeDocument; Assert.Equal(1, scope.SelectedIndex);
                scope.SelectedIndex = 0; Assert.Equal(Scripting.ScriptScope.CurrentMessage, model.Scope);
                var confirm = new TaskCompletionSource<bool>(); model.ConfirmDeleteAsync = _ => confirm.Task; model.DeleteCommand.Execute(null);
                Assert.False(scope.IsEnabled); Assert.All(buttons.Take(8).Where(b => b.Command != model.OpenCommand), b => Assert.False(b.IsEffectivelyEnabled));
                Assert.True(buttons[5].IsEffectivelyEnabled);
                confirm.SetResult(false); await Task.Yield(); Assert.True(buttons[6].IsEffectivelyEnabled);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Theory]
    [InlineData(true, true)] [InlineData(true, false)] [InlineData(false, true)] [InlineData(false, false)]
    public async Task RibbonUsesWindowThemeInBothModesAndRendersCompactReadableGroups(bool dark, bool pinned)
    {
        // Real Skia rendering runs in a filtered child process: Avalonia caches
        // typefaces globally, so mixing drawing backends would pollute other tests.
        var rendering = Environment.GetEnvironmentVariable("HL7WORKBENCH_RIBBON_RENDER") == "1";
        await using var ui = HeadlessUnitTestSession.StartNew(rendering ? typeof(RibbonRenderTestApplication) : typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            using var files = new Files(); using var docs = new DocumentManager(); using var model = new ScriptEditorViewModel(docs, files.Library);
            docs.Open(new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Synthetic)));
            Assert.True(await model.Session.NewAsync(Cancel)); Assert.True(model.Session.Save());
            var state = new ApplicationPreferences(Path.Combine(files.Root, "preferences.json")); Assert.True(state.Apply(state.Current with { RibbonExpanded = pinned }));
            var ribbon = new UtilityRibbon(state, model, RibbonActions());
            var root = new Grid { RowDefinitions = new("Auto,*") }; root.Children.Add(ribbon);
            var window = new Window { RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light, Content = root, Width = 1100, Height = 280 }; window.Show();
            try
            {
                ribbon.SelectTab(1); window.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                var host = pinned ? (Control)ribbon.PinnedHost : ribbon.TemporaryPopup.Child!;
                var surface = host.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("ribbon-surface"));
                void CheckTheme(ThemeVariant theme)
                {
                    Assert.True(Application.Current!.TryFindResource("SystemControlBackgroundChromeMediumLowBrush", theme, out var resource));
                    var background = Assert.IsAssignableFrom<ISolidColorBrush>(surface.Background).Color;
                    Assert.Equal(Assert.IsAssignableFrom<ISolidColorBrush>(resource).Color, background);
                    var headings = surface.GetVisualDescendants().OfType<TextBlock>().Where(t => t.Classes.Contains("ribbon-group-title")).ToArray();
                    Assert.Equal(new[] { "Script Library", "Execution", "Results" }, headings.Select(h => h.Text));
                    Assert.All(headings, h => Assert.True(Contrast(Assert.IsAssignableFrom<ISolidColorBrush>(h.Foreground).Color, background) >= 4.5));
                    Assert.Equal(theme, surface.ActualThemeVariant);
                }
                CheckTheme(dark ? ThemeVariant.Dark : ThemeVariant.Light);
                Assert.True(ribbon.Tabs[1].IsChecked);
                Assert.InRange(surface.Bounds.Height, 80, 110);
                var buttons = RibbonButtons(host); Assert.All(buttons, b => { Assert.True(b.Bounds.Height >= 30); Assert.True(b.FontSize >= 13); });
                Assert.Equal(10, buttons.Length); Assert.All(buttons, b => Assert.Single(b.GetVisualDescendants().OfType<PathIcon>()));
                var run = buttons.Single(b => AutomationProperties.GetName(b) == "Run"); Assert.True(run.IsEffectivelyEnabled);
                window.MouseMove(Center(run, window)); Assert.True(run.IsPointerOver);
                run.Focus(NavigationMethod.Tab); Assert.True(run.IsFocused);
                if (rendering)
                {
                    var directory = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../artifacts/ribbon-visual")); Directory.CreateDirectory(directory);
                    using var frame = window.CaptureRenderedFrame(); Assert.NotNull(frame);
                    frame.Save(Path.Combine(directory, $"{(dark ? "dark" : "light")}-{(pinned ? "pinned" : "overlay")}.png"), Avalonia.Media.Imaging.PngBitmapEncoderOptions.Default);
                }
                window.MouseDown(Center(run, window), MouseButton.Left); Assert.Contains(":pressed", run.Classes);
                window.MouseMove(new Point(1050, 240)); window.MouseUp(new Point(1050, 240), MouseButton.Left);
                Assert.DoesNotContain(":pressed", run.Classes); Assert.False(model.IsBusy);
                // Theme changes must update existing surfaces, including an already-open popup.
                window.RequestedThemeVariant = dark ? ThemeVariant.Light : ThemeVariant.Dark;
                window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); CheckTheme(dark ? ThemeVariant.Light : ThemeVariant.Dark);
            }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    private static Point Center(Control control, Window window) => control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;
    private static Button[] RibbonButtons(Control root) => root.GetVisualDescendants().OfType<Button>().Where(b => b.Classes.Contains("ribbon-command")).ToArray();
    private static double Contrast(Color first, Color second)
    {
        static double Linear(byte value) { var v = value / 255d; return v <= .04045 ? v / 12.92 : Math.Pow((v + .055) / 1.055, 2.4); }
        static double Luminance(Color color) => .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
        var a = Luminance(first); var b = Luminance(second); return (Math.Max(a, b) + .05) / (Math.Min(a, b) + .05);
    }
}

public sealed class RibbonRenderTestApplication : Application
{
    public override void Initialize() => Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<RibbonRenderTestApplication>().UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
