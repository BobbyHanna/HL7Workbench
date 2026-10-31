using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ContentFontWheelTests
{
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static Point PointOn(Control control, Window window) => control.TranslatePoint(new Point(Math.Min(40, control.Bounds.Width / 2), Math.Min(40, control.Bounds.Height / 2)), window)!.Value;
    [Theory]
    [InlineData("raw")][InlineData("inspector")][InlineData("sql")][InlineData("help")][InlineData("script")]
    public async Task ActualWheelUsesSharedScopeInEachContentToolWithoutDoubleZoom(string tool)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-wheel-tools-" + Guid.NewGuid().ToString("N"));
            var preferences = new ApplicationPreferences(Path.Combine(root, "preferences.json")); using var manager = new DocumentManager();
            var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse("PID|Example^Avery")); manager.Open(doc);
            IDisposable? model = null; Control content, target; string key;
            if (tool == "inspector")
            {
                var panes = new InspectionPanes(doc.Inspection, doc, InspectionPaneKind.Inspector); doc.Inspection.InspectField(1);
                content = panes; target = panes.Inspector; key = "tool.field-inspector";
            }
            else if (tool == "sql")
            {
                var sql = new SqlQueryViewModel(manager, new(Path.Combine(root, "Sql"))); model = sql;
                var view = new SqlQueryView(sql); content = view; target = view.Editor; key = "tool.sql-query";
            }
            else if (tool == "help")
            {
                var help = new DocumentationViewModel(); model = help; var view = new DocumentationView(help);
                content = view; target = view.Topics; key = "tool.documentation";
            }
            else if (tool == "script")
            {
                var script = new ScriptEditorViewModel(manager, new(Path.Combine(root, "Scripts")), contentPreferences: preferences); model = script;
                var view = new ScriptEditorView(script); content = view; target = view.Editor; key = "tool.script-editor";
            }
            else { content = target = MessageViewer.CreateEditor(doc.EditableText!); key = "document.hl7"; }
            var scope = new ContentFontScope(content, preferences, key);
            var window = new Window { Width = 900, Height = 700, Content = content }; window.Show();
            try
            {
                Pump(window); var point = PointOn(target, window); var original = doc.EditableText;
                window.MouseWheel(point, new(0, 1), RawInputModifiers.Control); Assert.Equal(15, scope.Size);
                Assert.Equal(15, new ApplicationPreferences(Path.Combine(root, "preferences.json")).Current.ContentFonts!.For(key));
                window.MouseWheel(point, new(0, -1), RawInputModifiers.Control); Assert.Equal(14, scope.Size);
                Assert.Equal(original, doc.EditableText); Assert.False(doc.IsDirty); Assert.False(doc.CanUndo);
            }
            finally
            {
                window.Close(); model?.Dispose(); if (model is SqlQueryViewModel sql) await sql.Disposal;
                manager.Dispose(); await doc.Disposal; if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task PointerTargetOverridesKeyboardFocusAndNestedScopeConsumesExactlyOnce()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-wheel-target-" + Guid.NewGuid().ToString("N"));
            var preferences = new ApplicationPreferences(Path.Combine(root, "preferences.json"));
            var left = MessageViewer.CreateEditor("SYNTHETIC"); var right = MessageViewer.CreateEditor("SYNTHETIC SQL");
            var inner = new Grid { Children = { right } }; var panel = new Grid { ColumnDefinitions = new("*,*"), Children = { left, inner } }; Grid.SetColumn(inner, 1);
            var outerScope = new ContentFontScope(panel, preferences, "document.hl7"); var innerScope = new ContentFontScope(inner, preferences, "tool.sql-query");
            var window = new Window { Content = panel, Width = 900, Height = 500 }; window.Show();
            try
            {
                Pump(window); left.TextArea.Focus(); left.Select(1, 3); var selected = left.SelectedText;
                window.MouseWheel(PointOn(right, window), new(0, 1), RawInputModifiers.Control);
                Assert.Equal(14, outerScope.Size); Assert.Equal(15, innerScope.Size); Assert.Equal(selected, left.SelectedText);
                window.MouseWheel(PointOn(left, window), new(0, -1), RawInputModifiers.Control); Assert.Equal(13, outerScope.Size); Assert.Equal(15, innerScope.Size);
            }
            finally { window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public async Task NormalHorizontalModifiedAndChromeWheelGesturesKeepTheirExistingBehavior()
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-wheel-scroll-" + Guid.NewGuid().ToString("N"));
            var preferences = new ApplicationPreferences(Path.Combine(root, "preferences.json"));
            var editor = MessageViewer.CreateEditor(string.Join('\n', Enumerable.Repeat("SYNTHETIC " + new string('X', 300), 200)));
            var button = new Button { Content = "Tool command" }; var grid = new Grid { RowDefinitions = new("Auto,*"), Children = { button, editor } }; Grid.SetRow(editor, 1);
            var scope = new ContentFontScope(grid, preferences, "document.hl7");
            var window = new Window { Content = grid, Width = 500, Height = 350 }; window.Show();
            var modalContent = new TextBox { Text = "Independent dialog" }; var dialog = new Window { Content = modalContent, Width = 400, Height = 250 }; dialog.Show();
            try
            {
                Pump(window); var point = PointOn(editor, window); window.MouseWheel(point, new(0, -3)); Pump(window);
                Assert.True(editor.VerticalOffset > 0); Assert.Equal(14, scope.Size);
                window.MouseWheel(point, new(1, 0), RawInputModifiers.Control); Assert.Equal(14, scope.Size);
                window.MouseWheel(point, new(1, 1), RawInputModifiers.Control); Assert.Equal(14, scope.Size);
                window.MouseWheel(point, new(0, 1), RawInputModifiers.Control | RawInputModifiers.Shift); Assert.Equal(14, scope.Size);
                window.MouseWheel(PointOn(button, window), new(0, 1), RawInputModifiers.Control); Assert.Equal(14, scope.Size);
                Pump(dialog); var modalSize = modalContent.FontSize;
                dialog.MouseWheel(PointOn(modalContent, dialog), new(0, 1), RawInputModifiers.Control); Assert.Equal(14, scope.Size); Assert.Equal(modalSize, modalContent.FontSize);
                Assert.Null(preferences.Current.ContentFonts);
            }
            finally { dialog.Close(); window.Close(); if (Directory.Exists(root)) Directory.Delete(root, true); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Theory]
    [InlineData(12, -1)] [InlineData(32, 1)]
    public async Task ValidZoomAtExistingBoundsIsConsumedWithoutChangingGlobalPreference(double size, int direction)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "hl7-wheel-bound-" + Guid.NewGuid().ToString("N"));
            var preferences = new ApplicationPreferences(Path.Combine(root, "preferences.json")); Assert.True(preferences.Apply(preferences.Current with { ContentFonts = new(size) }));
            var editor = MessageViewer.CreateEditor("SYNTHETIC"); var scope = new ContentFontScope(editor, preferences, "document.hl7");
            bool? handled = null; editor.AddHandler(InputElement.PointerWheelChangedEvent, (_, e) => handled = e.Handled, RoutingStrategies.Bubble, handledEventsToo: true);
            var window = new Window { Content = editor, Width = 500, Height = 400 }; window.Show();
            try
            {
                Pump(window); window.MouseWheel(PointOn(editor, window), new(0, direction), RawInputModifiers.Control);
                Assert.True(handled); Assert.Equal(size, scope.Size); Assert.Equal(size, preferences.Current.ContentFonts!.DefaultSize);
            }
            finally { window.Close(); Directory.Delete(root, true); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
