using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ContentFontViewTests
{
    [Theory]
    [InlineData(false, 360)] [InlineData(true, 1000)]
    public async Task GlobalScaleAndFocusedOverrideUpdateNativeEditorWithoutChangingSelectionOrDirtyState(bool dark, int width)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-font-view-" + Guid.NewGuid().ToString("N"));
            var preferences = new ApplicationPreferences(Path.Combine(root, "preferences.json"));
            var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse("PID|Example^Avery"));
            var editor = MessageViewer.CreateEditor(doc.EditableText!); var view = new Grid { Children = { editor } };
            var scope = new ContentFontScope(view, preferences, "document.hl7");
            var help = new TextBox { Text = "Synthetic documentation", AcceptsReturn = true }; var otherScope = new ContentFontScope(help, preferences, "tool.documentation");
            var window = new Window { Content = view, Width = width, Height = 400, RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light };
            var floating = new Window { Content = help, Width = 350, Height = 300 }; window.Show(); floating.Show();
            try
            {
                editor.Select(4, 7); var selection = editor.SelectedText; Assert.Equal(14, editor.FontSize);
                Assert.True(preferences.Apply(preferences.Current with { ContentFonts = new(24) }));
                Assert.Equal(24, editor.FontSize); Assert.Equal(24, help.FontSize);
                var key = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Add, KeyModifiers = KeyModifiers.Control };
                editor.RaiseEvent(key); Assert.True(key.Handled); Assert.Equal(25, editor.FontSize); Assert.Equal(24, help.FontSize);
                Assert.Equal(24, preferences.Current.ContentFonts!.DefaultSize);
                editor.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.D0, KeyModifiers = KeyModifiers.Control });
                Assert.Equal(24, editor.FontSize); Assert.Equal(selection, editor.SelectedText); Assert.False(doc.IsDirty);
                Assert.Equal(24, new ApplicationPreferences(Path.Combine(root, "preferences.json")).Current.ContentFonts!.DefaultSize);
                Assert.True(scope.Size == otherScope.Size);
            }
            finally { window.Close(); floating.Close(); doc.Dispose(); await doc.Disposal; if (Directory.Exists(root)) Directory.Delete(root, true); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task PreferencesApplyResetAndScriptLegacyMigrationUseSharedStorage()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-font-preference-view-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            try
            {
                var preferences = new ApplicationPreferences(Path.Combine(root, "preferences.json"));
                var legacy = Path.Combine(root, "script.json"); new ScriptEditorPreferences(18).Save(legacy);
                using var manager = new DocumentManager(); using var script = new ScriptEditorViewModel(manager, new(Path.Combine(root, "Scripts")), preferencesPath: legacy, contentPreferences: preferences);
                Assert.Equal(24, script.FontSize); Assert.Equal(21, preferences.Current.ContentFonts!.For("tool.script-editor"));
                script.ResetFontSize(); Assert.Equal(16, script.FontSize);
                var window = new PreferencesWindow(preferences); window.Show();
                try
                {
                    window.ContentSize.Value = 28; Assert.True(window.Apply()); Assert.Equal(32, script.FontSize);
                    window.ResetContentSize.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Assert.True(window.Apply());
                    Assert.Equal(14, preferences.Current.ContentFonts!.DefaultSize); Assert.Equal(16, script.FontSize);
                }
                finally { window.Close(); }
            }
            finally { Directory.Delete(root, true); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
