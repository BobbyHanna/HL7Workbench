using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Dock.Model.Controls;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class StructuredValueEditingViewTests
{
    private const string Text = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261031000000||ADT^A01|SYN-1|T|2.5.1\rPID|1||SYN||Example^Avery||\r";
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static T Get<T>(MainWindow window, string field) => (T)typeof(MainWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
    private static async Task InShell(Func<MainWindow, DocumentManager, MessageDocument, InspectionPanes, Task> body, bool floating = false)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            Application.Current!.Styles.Add(new Dock.Avalonia.Themes.Fluent.DockFluentTheme());
            Application.Current.DataTemplates.Add(new DockContentTemplate { DockedInspection = true });
            var root = Path.Combine(Path.GetTempPath(), "hl7-structured-view-" + Guid.NewGuid().ToString("N"));
            var window = new MainWindow(root); window.Show(); var manager = Get<DocumentManager>(window, "documents");
            var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Text)); manager.Open(doc); doc.IsEditMode = true;
            var factory = Get<WorkspaceDockFactory>(window, "factory"); factory.OpenDocument(doc.Id); Pump(window);
            if (floating) { factory.FloatDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId)!); Pump(window); }
            var hosts = factory.Root.Windows?.Select(w => w.Host).OfType<Window>().ToArray() ?? [];
            var panes = window.GetVisualDescendants().Concat(hosts.SelectMany(w => w.GetVisualDescendants())).OfType<InspectionPanes>().Single(p => p.Inspector.IsAttachedToVisualTree());
            try
            {
                doc.Inspection.SelectSegment(doc.Inspection.Segments[1]); var field = doc.Inspection.InspectField(5)!;
                panes.PrepareActions(field.VisibleChildren[1]); await body(window, manager, doc, panes);
            }
            finally
            {
                foreach (var dialog in window.OwnedWindows.Concat(hosts.SelectMany(w => w.OwnedWindows)).ToArray()) dialog.Close();
                await window.InspectorActionTask;
                var owned = manager.Documents.Append(doc).Distinct().ToArray();
                foreach (var item in manager.Documents.ToArray()) manager.Close(item.Id, true);
                window.Close(); await Task.WhenAll(owned.Select(d => d.Disposal));
                if (Directory.Exists(root)) Directory.Delete(root, true);
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    private static StructuredValueEditWindow Open(MainWindow window, InspectionPanes panes)
    {
        Assert.True(panes.EditValue.IsEnabled);
        panes.EditValue.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        var owner = Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(panes));
        return Assert.Single(owner.OwnedWindows.OfType<StructuredValueEditWindow>());
    }
    [Theory]
    [InlineData(false, "Rowan")] [InlineData(true, "Rowan")]
    [InlineData(false, "LongerSyntheticName")] [InlineData(true, "Q")]
    public Task ActualInspectorDialogUpdatesRawEditorAndOneDocumentUndoStep(bool floating, string replacement) => InShell(async (window, _, doc, panes) =>
    {
        var raw = window.GetVisualDescendants().OfType<MessageInspectionView>().Single();
        var dialog = Open(window, panes); Assert.Equal("Avery", dialog.CurrentRaw.Text);
        Assert.True(dialog.CurrentRaw.IsReadOnly); Assert.False(dialog.ApplyButton.IsEnabled);
        dialog.Replacement.Text = replacement; Assert.True(dialog.ApplyButton.IsEnabled); Click(dialog.ApplyButton);
        await window.InspectorActionTask; Pump(window);
        Assert.Equal(Text.Replace("Avery", replacement, StringComparison.Ordinal), doc.EditableText);
        Assert.Equal(doc.EditableText, raw.Editor.Text); Assert.Equal(replacement, raw.Editor.SelectedText);
        Assert.Equal("PID[1]-5[1].2", doc.Inspection.SelectedNode!.Path.ToString()); Assert.True(doc.IsDirty);
        doc.Undo(); await doc.EditRefresh; Assert.Equal(Text, doc.EditableText); Assert.False(doc.CanUndo); Assert.False(doc.IsDirty);
        doc.Redo(); await doc.EditRefresh; Assert.Contains("Example^" + replacement, raw.Editor.Text); Assert.False(doc.CanRedo);
    }, floating);
    [Fact]
    public Task UnsafeReplacementShowsFeedbackAndCancelCreatesNoHistory() => InShell(async (window, _, doc, panes) =>
    {
        var dialog = Open(window, panes); dialog.Replacement.Text = "Unsafe^Sibling"; Click(dialog.ApplyButton);
        Assert.Contains("Unsafe raw value", dialog.Feedback.Text); Assert.Contains(dialog, window.OwnedWindows);
        Assert.Equal(Text, doc.EditableText); Assert.False(doc.CanUndo);
        dialog.Replacement.Text = "Staged only"; Click(dialog.CancelButton); await window.InspectorActionTask;
        Assert.Equal(Text, doc.EditableText); Assert.False(doc.IsDirty); Assert.False(doc.CanUndo);
        Assert.Empty(dialog.CurrentRaw.Text!); Assert.Empty(dialog.Replacement.Text!);
    });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DialogRejectsRawEditsOrDocumentSwitchWithoutRetargeting(bool switchDocument) => InShell(async (window, manager, doc, panes) =>
    {
        var dialog = Open(window, panes); dialog.Replacement.Text = "WRONG";
        MessageDocument? other = null;
        if (switchDocument)
        {
            other = new("other", "Synthetic other", Hl7Parser.Parse(Text)); manager.Open(other); manager.Activate(doc.Id);
        }
        else doc.ReplaceWorkingText(Text.Replace("Avery", "Raw draft", StringComparison.Ordinal));
        Assert.False(dialog.ApplyButton.IsEnabled); Assert.Contains("Target changed", dialog.Feedback.Text);
        Click(dialog.ApplyButton); Assert.DoesNotContain("WRONG", doc.EditableText!);
        Click(dialog.CancelButton); await window.InspectorActionTask;
        if (other is not null) { Assert.Equal(Text, other.EditableText); manager.Close(other.Id, true); await other.Disposal; }
        else { doc.Undo(); await doc.EditRefresh; Assert.Equal(Text, doc.EditableText); Assert.False(doc.CanUndo); }
    });
    [Theory]
    [InlineData("mode")][InlineData("msh")][InlineData("absent")]
    public Task MenuExplainsWhyCurrentNodeCannotBeEdited(string kind) => InShell((window, _, doc, panes) =>
    {
        InspectionNode node;
        if (kind == "msh") { doc.Inspection.SelectSegment(doc.Inspection.Segments[0]); node = doc.Inspection.InspectField(2)!; }
        else if (kind == "absent") node = doc.Inspection.InspectField(999)!;
        else { doc.IsEditMode = false; node = doc.Inspection.SelectedNode!; }
        panes.PrepareActions(node); Assert.False(panes.EditValue.IsEnabled); Assert.NotEmpty(Assert.IsType<string>(ToolTip.GetTip(panes.EditValue)));
        panes.EditValue.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Assert.Empty(window.OwnedWindows); Assert.Equal(Text, doc.EditableText);
        return Task.CompletedTask;
    });
}
