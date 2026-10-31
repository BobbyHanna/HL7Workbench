using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
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

public sealed partial class BulkFieldViewTests
{
    private static string Message(int n, string visit = "I") => $"MSH|^~\\&|SYN|TEST|DEST|TEST|20261031000000||ADT^A01|SYN-{n}|T|2.5.1\rPID|1||SYN||Example^Avery\rPV1|1|{visit}\r";
    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static T Get<T>(MainWindow window, string field) => (T)typeof(MainWindow).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(window)!;
    private static async Task InShell(Func<MainWindow, DocumentManager, MessageDocument, InspectionPanes, Task> body, bool floating = false, string? sourceText = null)
    {
        await using var ui = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await ui.Dispatch(async () =>
        {
            Application.Current!.Styles.Add(new Dock.Avalonia.Themes.Fluent.DockFluentTheme());
            Application.Current.DataTemplates.Add(new DockContentTemplate { DockedInspection = true });
            var root = Path.Combine(Path.GetTempPath(), "hl7-bulk-view-" + Guid.NewGuid().ToString("N"));
            var window = new MainWindow(root); window.Show(); var manager = Get<DocumentManager>(window, "documents");
            var owned = new HashSet<MessageDocument>();
            ((System.Collections.Specialized.INotifyCollectionChanged)manager.Documents).CollectionChanged += Track;
            void Track(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) { foreach (var d in manager.Documents) owned.Add(d); }
            using var input = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(sourceText ?? Message(1) + Message(2, "O") + Message(3)));
            var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic.hl7", input, TestContext.Current.CancellationToken);
            var factory = Get<WorkspaceDockFactory>(window, "factory"); factory.OpenDocument(doc.Id); Pump(window);
            if (floating) { factory.FloatDockable(factory.GetDockable<ITool>(WorkspaceDockFactory.FieldInspectorId)!); Pump(window); }
            var hosts = factory.Root.Windows?.Select(w => w.Host).OfType<Window>().ToArray() ?? [];
            var panes = window.GetVisualDescendants().Concat(hosts.SelectMany(w => w.GetVisualDescendants())).OfType<InspectionPanes>().Single(p => p.Inspector.IsAttachedToVisualTree());
            try
            {
                doc.Inspection.SelectSegment(doc.Inspection.Segments.Single(s => s.Identifier == "PV1")); panes.PrepareActions(doc.Inspection.InspectField(2));
                await body(window, manager, doc, panes);
            }
            finally
            {
                foreach (var dialog in window.OwnedWindows.Concat(hosts.SelectMany(w => w.OwnedWindows)).ToArray()) dialog.Close();
                await window.InspectorActionTask;
                foreach (var d in manager.Documents.ToArray()) manager.Close(d.Id, true);
                window.Close(); await Task.WhenAll(owned.Select(d => d.Disposal));
                ((System.Collections.Specialized.INotifyCollectionChanged)manager.Documents).CollectionChanged -= Track;
                try { Directory.Delete(root, true); } catch (DirectoryNotFoundException) { }
            }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    private static BulkFieldWindow Open(InspectionPanes panes, MenuItem item)
    {
        Assert.True(item.IsEnabled); item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        return Assert.Single(Assert.IsAssignableFrom<Window>(TopLevel.GetTopLevel(panes)).OwnedWindows.OfType<BulkFieldWindow>());
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task UniqueActionRoutesFromDockedOrFloatingInspectorAndFiltersCompleteCapturedMatches(bool floating) => InShell(async (window, _, doc, panes) =>
    {
        Assert.False(doc.IsEditMode); var dialog = Open(panes, panes.UniqueValues); Assert.Equal("Unique Values", dialog.Title);
        Click(dialog.Analyze); await dialog.Completion; Assert.Equal(3, dialog.Result!.Summary.ScannedMessages);
        var values = Assert.IsAssignableFrom<IEnumerable<BulkUniqueValue>>(dialog.Rows.ItemsSource).ToArray(); Assert.Equal(2, values.Length);
        var value = values.Single(v => v.Raw == "I"); Assert.Equal(2, value.NodeCount); Assert.Equal(2, value.MessageCount);
        dialog.Rows.SelectedItem = value; Assert.Equal("State: Value\nComplete raw value:\nI", dialog.Details.Text); Click(dialog.Filter); await window.InspectorActionTask;
        Assert.Equal(2, doc.NavigationMatchIds!.Count); Assert.False(doc.IsDirty); Assert.False(doc.CanUndo); Assert.False(doc.IsEditMode);
    }, floating);
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task PreviewConfirmationAndApplyPublishOneIndependentResult(bool assign) => InShell(async (window, manager, source, panes) =>
    {
        var dialog = Open(panes, assign ? panes.AssignValue : panes.SearchReplace); dialog.Find.Text = "I"; dialog.Replacement.Text = "E";
        Assert.False(dialog.Apply.IsEnabled); Click(dialog.Analyze); await dialog.Completion;
        Assert.Equal(assign ? 3 : 2, dialog.Result!.Summary.Replacements); Assert.False(dialog.Apply.IsEnabled);
        dialog.Rows.SelectedIndex = 0; Assert.Contains("Original raw", dialog.Details.Text); dialog.Confirm.IsChecked = true; Assert.True(dialog.Apply.IsEnabled);
        Click(dialog.Apply); await dialog.Completion; await window.InspectorActionTask;
        Assert.Equal(assign ? 3 : 2, dialog.PublishedSummary!.Replacements);
        Assert.Contains("Source unchanged", window.FindControl<TextBlock>("Status")!.Text);
        Assert.Equal(2, manager.Documents.Count); var output = manager.ActiveDocument!; Assert.NotSame(source, output); Assert.Equal(3, output.MessageCount);
        Assert.Contains("PV1|1|E", output.EditableText); Assert.True(output.IsDirty); Assert.False(source.IsDirty); Assert.False(source.CanUndo); Assert.False(source.IsEditMode);
    });
    [Fact]
    public Task CurrentFilterScopeUsesAllCapturedIdsAndRetainsAllOutputMessages() => InShell(async (window, manager, source, panes) =>
    {
        var matches = await new MessageSearch().SearchAsync(source, new(MessageSearchMode.Field, "I", true, "PV1-2", MessageSearchComparison.Equals), token: TestContext.Current.CancellationToken); source.ApplySearchFilter(matches);
        var dialog = Open(panes, panes.AssignValue); dialog.Scope.SelectedIndex = 2; dialog.Replacement.Text = "E";
        Click(dialog.Analyze); await dialog.Completion; Assert.Equal(2, dialog.Result!.Summary.Replacements);
        dialog.Confirm.IsChecked = true; Click(dialog.Apply); await dialog.Completion; await window.InspectorActionTask;
        var output = manager.ActiveDocument!; Assert.Equal(3, output.MessageCount); await output.NavigateAsync(2); Assert.Contains("PV1|1|O", output.EditableText);
    });
    [Fact]
    public Task ParameterChangesAndSourceRevisionInvalidateConfirmationAndCancelDoesNotMutate() => InShell(async (window, manager, source, panes) =>
    {
        var dialog = Open(panes, panes.AssignValue); dialog.Replacement.Text = "E"; Click(dialog.Analyze); await dialog.Completion;
        dialog.Confirm.IsChecked = true; Assert.True(dialog.Apply.IsEnabled); dialog.Replacement.Text = "O"; Assert.Null(dialog.Result); Assert.False(dialog.Apply.IsEnabled);
        Click(dialog.Analyze); await dialog.Completion; dialog.Confirm.IsChecked = true;
        source.IsEditMode = true; source.ReplaceWorkingText(Message(1, "X")); Assert.False(dialog.Apply.IsEnabled);
        dialog.Close(); await window.InspectorActionTask; Assert.Single(manager.Documents); source.Undo(); await source.EditRefresh; Assert.False(source.IsDirty);
    });
    [Fact]
    public Task MshAndStaleSelectionDisableMutationsWithoutDisablingReadOnlyAnalysis() => InShell((window, _, doc, panes) =>
    {
        doc.Inspection.SelectSegment(doc.Inspection.Segments[0]); panes.PrepareActions(doc.Inspection.InspectField(2));
        Assert.True(panes.UniqueValues.IsEnabled); Assert.False(panes.AssignValue.IsEnabled); Assert.False(panes.SearchReplace.IsEnabled);
        var staleNode = doc.Inspection.SelectedNode; doc.IsEditMode = true; doc.ReplaceWorkingText(Message(1, "O"));
        panes.PrepareActions(staleNode); Assert.False(panes.UniqueValues.IsEnabled); Assert.False(panes.AssignValue.IsEnabled);
        return Task.CompletedTask;
    });
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task NativeResultsSupportKeyboardFocusContentZoomAndBoundedLayout(bool largeDark) => InShell(async (window, _, source, panes) =>
    {
        var preferences = Get<ApplicationPreferences>(window, "preferences");
        if (largeDark) { Application.Current!.RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Dark; Assert.True(preferences.SetContentFont("tool.field-inspector", 31)); }
        var dialog = Open(panes, panes.UniqueValues); dialog.Width = 600; dialog.Height = 600; Pump(dialog);
        Assert.NotNull(AutomationProperties.GetName(dialog.Rows)); dialog.Scope.Focus(); Assert.True(dialog.Scope.IsFocused);
        Click(dialog.Analyze); await dialog.Completion; Pump(dialog);
        dialog.Rows.Focus(); dialog.Rows.SelectedIndex = 0; Assert.NotEmpty(dialog.Details.Text!);
        dialog.Rows.BringIntoView(); Pump(dialog); AssertFullyVisible(dialog.Rows, dialog);
        var old = preferences.Current.ContentFonts?.For("tool.field-inspector") ?? 14;
        var point = dialog.Rows.TranslatePoint(new Point(30, 30), dialog)!.Value; dialog.MouseWheel(point, new(0, 1), RawInputModifiers.Control);
        Assert.Equal(old + 1, preferences.Current.ContentFonts!.For("tool.field-inspector"));
        Pump(dialog);
        dialog.Rows.BringIntoView(); Pump(dialog); AssertFullyVisible(dialog.Rows, dialog);
        dialog.Width = 1400; dialog.Height = 950; Pump(dialog); Assert.True(dialog.Rows.Bounds.Height > 0);
        dialog.Close(); await window.InspectorActionTask; Assert.False(source.IsDirty);
    });
}
