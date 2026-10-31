using System.Reflection;
using System.Text;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed partial class BulkFieldViewTests
{
    private static string PublicationMessage(int n) => Message(n) + "NTE|1||Avery stays outside PID-5.2\rPID|2||ALT||Avery^Other\r";
    private static string PublicationBatch => string.Concat(Enumerable.Range(1, 30).Select(PublicationMessage));
    private static InspectionPanes CurrentInspector(MainWindow window)
    {
        var factory = Get<WorkspaceDockFactory>(window, "factory");
        var hosts = factory.Root.Windows?.Select(w => w.Host).OfType<Window>().ToArray() ?? [];
        return window.GetVisualDescendants().Concat(hosts.SelectMany(w => w.GetVisualDescendants())).OfType<InspectionPanes>()
            .Single(p => p.Inspector.IsAttachedToVisualTree());
    }
    private static void SelectGivenName(HL7Workbench.Workspace.MessageInspection inspection)
    {
        inspection.SelectSegment(inspection.Segments.Single(s => s.Identifier == "PID" && s.Occurrence == 1));
        var field = inspection.InspectField(5)!; inspection.SelectNode(field.Children[0].Children[1]);
    }
    private static string InspectorText(InspectionPanes panes) => ((TextBox)typeof(InspectionPanes)
        .GetField("rawValue", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panes)!).Text!;
    private static void AssertPublishedViews(MainWindow window, MessageDocument document, string value)
    {
        Pump(window); var factory = Get<WorkspaceDockFactory>(window, "factory");
        var tab = Assert.IsAssignableFrom<IDocument>(factory.GetDockable<IDocument>(document.Id));
        Assert.Same(document, tab.Context); var group = Assert.IsAssignableFrom<IDocumentDock>(tab.Owner);
        Assert.Contains(tab, group.VisibleDockables!); Assert.Same(tab, group.ActiveDockable);
        var view = Assert.Single(window.GetVisualDescendants().OfType<FileDocumentView>(), v => ReferenceEquals(v.Document, document) && v.IsAttachedToVisualTree());
        Assert.All(view.GetVisualAncestors(), ancestor => Assert.True(ancestor.IsVisible));
        Assert.Equal(document.EditableText, view.ActiveView!.Editor.Text);
        Assert.Equal(value, HL7Workbench.Core.Hl7Parser.Parse(view.ActiveView.Editor.Text).Get("PID-5.2").Raw);
        var panes = CurrentInspector(window); Assert.Same(document.ActiveInspection, panes.Inspection);
        Assert.Equal("PID[1]-5[1].2", panes.Inspection.SelectedNode!.Path.ToString()); Assert.Equal(value, InspectorText(panes));
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public Task AveryToMorganPublishesARealThirtyMessageTabAndBothViewsFollowItsIdentity(bool floating, bool currentOnly) => InShell(async (window, manager, source, _) =>
    {
        var factory = Get<WorkspaceDockFactory>(window, "factory"); SelectGivenName(source.Inspection); Pump(window);
        var panes = CurrentInspector(window); panes.PrepareActions(source.Inspection.SelectedNode);
        var original = source.Snapshot!.ReadBytes(new(0, source.Snapshot.Length));
        AssertPublishedViews(window, source, "Avery");
        var dialog = Open(panes, panes.SearchReplace); Assert.Equal("Avery", dialog.Find.Text); dialog.Replacement.Text = "Morgan";
        if (currentOnly) dialog.Scope.SelectedIndex = 0;
        Click(dialog.Analyze); await dialog.Completion;
        Assert.Equal(currentOnly ? 1 : 30, dialog.Result!.Summary.Replacements);
        Assert.Single(manager.Documents); Assert.Same(source, manager.ActiveDocument); AssertPublishedViews(window, source, "Avery");
        Assert.False(source.IsDirty); Assert.False(source.CanUndo);
        dialog.Confirm.IsChecked = true; Click(dialog.Apply); await dialog.Completion; await window.InspectorActionTask; Pump(window);
        Assert.Equal(2, manager.Documents.Count); var result = manager.ActiveDocument!; Assert.NotSame(source, result);
        AssertPublishedViews(window, result, "Morgan");
        Assert.Contains("Bulk Result", factory.GetDockable<IDocument>(result.Id)!.Title); Assert.NotEqual(source.DisplayTitle, result.DisplayTitle);
        Assert.Equal(30, result.MessageCount);
        var expected = string.Concat(Enumerable.Range(1, 30).Select(n => currentOnly && n != 1 ? PublicationMessage(n) : PublicationMessage(n).Replace("Example^Avery", "Example^Morgan", StringComparison.Ordinal)));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), result.Snapshot!.ReadBytes(new(0, result.Snapshot.Length)));
        Assert.Equal(original, source.Snapshot.ReadBytes(new(0, source.Snapshot.Length))); Assert.False(source.IsDirty); Assert.False(source.CanUndo);
        factory.OpenDocument(source.Id); Pump(window); Assert.Same(source, manager.ActiveDocument); AssertPublishedViews(window, source, "Avery");
        factory.OpenDocument(result.Id); Pump(window); AssertPublishedViews(window, result, "Morgan");
        Click(dialog.Apply); await dialog.Completion; Assert.Equal(2, manager.Documents.Count);
    }, floating, PublicationBatch);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task PresentationFailureWithdrawsResultRestoresBothViewsAndAllowsExplicitRetry(bool floating) => InShell(async (window, manager, source, _) =>
    {
        var factory = Get<WorkspaceDockFactory>(window, "factory"); SelectGivenName(source.Inspection); Pump(window);
        var panes = CurrentInspector(window); panes.PrepareActions(source.Inspection.SelectedNode);
        var dialog = Open(panes, panes.SearchReplace); dialog.Replacement.Text = "Morgan";
        Click(dialog.Analyze); await dialog.Completion; dialog.Confirm.IsChecked = true;
        MessageDocument? attempted = null;
        void FailPresentation(object? sender, Dock.Model.Core.Events.ActiveDockableChangedEventArgs e)
        {
            if (e.Dockable is not IDocument { Context: MessageDocument result } || ReferenceEquals(result, source)) return;
            attempted = result; factory.ActiveDockableChanged -= FailPresentation;
            throw new InvalidOperationException("Synthetic presentation failure after the result tab was added.");
        }
        factory.ActiveDockableChanged += FailPresentation;
        try { Click(dialog.Apply); await dialog.Completion; }
        finally { factory.ActiveDockableChanged -= FailPresentation; }
        Assert.NotNull(attempted); await attempted.Disposal;
        Assert.Null(factory.GetDockable<IDocument>(attempted.Id)); Assert.Single(manager.Documents);
        Assert.Same(source, manager.ActiveDocument); AssertPublishedViews(window, source, "Avery");
        Assert.False(source.IsDirty); Assert.False(source.CanUndo); Assert.Null(dialog.PublishedSummary);
        Assert.True(dialog.IsVisible); Assert.True(dialog.Result!.CanApply); Assert.False(dialog.Confirm.IsChecked);
        Assert.Contains(dialog.GetVisualDescendants().OfType<TextBlock>(), text => text.Text?.Contains("No result was retained", StringComparison.Ordinal) == true);
        dialog.Confirm.IsChecked = true; Click(dialog.Apply); await dialog.Completion; await window.InspectorActionTask;
        Assert.Equal(2, manager.Documents.Count); AssertPublishedViews(window, manager.ActiveDocument!, "Morgan");
    }, floating, PublicationBatch);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task RepeatedApplySharesOneCompletionAndCurrentMessageResultOpensAtTheChangedMessage(bool floating) => InShell(async (window, manager, source, _) =>
    {
        await source.NavigateAsync(17, TestContext.Current.CancellationToken); SelectGivenName(source.Inspection); Pump(window);
        var panes = CurrentInspector(window); panes.PrepareActions(source.Inspection.SelectedNode);
        var dialog = Open(panes, panes.SearchReplace); dialog.Scope.SelectedIndex = 0; dialog.Replacement.Text = "Morgan";
        Click(dialog.Analyze); await dialog.Completion; dialog.Confirm.IsChecked = true;
        Click(dialog.Apply); var applying = dialog.Completion; Click(dialog.Apply); Assert.Same(applying, dialog.Completion);
        await applying; await window.InspectorActionTask;
        Assert.Equal(2, manager.Documents.Count); var result = manager.ActiveDocument!;
        Assert.Equal(17, result.MessageNumber); Assert.Equal(30, result.MessageCount); AssertPublishedViews(window, result, "Morgan");
        var expected = string.Concat(Enumerable.Range(1, 30).Select(n => n == 17 ? PublicationMessage(n).Replace("Example^Avery", "Example^Morgan", StringComparison.Ordinal) : PublicationMessage(n)));
        Assert.Equal(Encoding.UTF8.GetBytes(expected), result.Snapshot!.ReadBytes(new(0, result.Snapshot.Length)));
        Get<WorkspaceDockFactory>(window, "factory").OpenDocument(source.Id); Pump(window);
        Assert.Equal(17, source.MessageNumber); AssertPublishedViews(window, source, "Avery"); Assert.False(source.IsDirty);
        Click(dialog.Apply); await dialog.Completion; Assert.Equal(2, manager.Documents.Count);
    }, floating, PublicationBatch);
}
