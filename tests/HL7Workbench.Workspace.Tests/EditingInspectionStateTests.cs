using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class EditingInspectionStateTests
{
    private const string Text = "MSH|^~\\&|SYN\rPID|Alpha^Beta&Gamma\r";
    [Fact]
    public async Task PendingSnapshotRetainsVisualSelectionButCannotResolveOrNavigateOldOffsets()
    {
        using var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Text));
        document.IsEditMode = true;
        var original = document.Inspection;
        original.SelectDeepestOffset(Text.IndexOf("Gamma", StringComparison.Ordinal));
        var path = original.SelectedNode!.Path; var selection = original.SelectedNode;
        var events = 0; original.SelectionChanged += (_, _) => events++;
        document.ReplaceWorkingText(Text.Replace("Alpha", "LongerAlpha", StringComparison.Ordinal));
        Assert.Equal(DocumentInspectionState.Pending, document.InspectionState);
        Assert.Same(original, document.InspectionSnapshot); Assert.Null(document.ActiveInspection);
        Assert.False(original.IsCurrent); Assert.Null(original.ResolveOffset(1));
        original.SelectOffset(1); original.SelectDeepestOffset(1); original.SelectSegment(original.Segments[0]);
        original.SelectNode(selection); original.ClearSelection(); Assert.Null(original.InspectField(1));
        Assert.Equal(0, events); Assert.Same(selection, original.SelectedNode);
        document.ReplaceWorkingText(Text.Replace("Alpha", "MuchLongerAlpha", StringComparison.Ordinal));
        Assert.Same(original, document.InspectionSnapshot);
        await document.EditRefresh;
        Assert.Equal(DocumentInspectionState.Current, document.InspectionState);
        Assert.NotSame(original, document.InspectionSnapshot); Assert.Same(document.ActiveInspection, document.InspectionSnapshot);
        Assert.Equal(path, document.Inspection.SelectedNode!.Path);
        Assert.Equal(document.WorkingText!.IndexOf("Gamma", StringComparison.Ordinal), document.Inspection.SelectedNode.Range64!.Value.Offset);
    }
    [Fact]
    public async Task TolerantMalformedProjectionIsCurrentAndFailedParseIsExplicitlyStaleUntilUndo()
    {
        using var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Text)); document.IsEditMode = true;
        document.ReplaceWorkingText(Text.Replace("^~\\&", "^^^^", StringComparison.Ordinal)); await document.EditRefresh;
        Assert.Equal(DocumentInspectionState.Current, document.InspectionState);
        Assert.Contains(document.ActiveDiagnostics, d => d.Code == "InvalidDelimiters");
        var snapshot = document.InspectionSnapshot; Assert.NotNull(snapshot); Assert.Equal(2, snapshot.Segments.Count);
        document.ReplaceWorkingText("MSH|^~\\&|SYN\rPID|" + new string('A', MessageDocument.MaximumEditableLineCharacters + 1));
        await document.EditRefresh;
        Assert.Equal(DocumentInspectionState.Stale, document.InspectionState); Assert.Same(snapshot, document.InspectionSnapshot);
        Assert.Null(document.ActiveInspection); Assert.False(snapshot.IsCurrent); Assert.Null(snapshot.ResolveOffset(0));
        document.Undo(); await document.EditRefresh;
        Assert.Equal(DocumentInspectionState.Current, document.InspectionState); Assert.True(document.Inspection.IsCurrent);
    }
    [Fact]
    public async Task EmptyWorkingTextHasItsOwnStateAndCanBeEditedAgain()
    {
        using var document = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Text)); document.IsEditMode = true;
        document.ReplaceWorkingText(""); Assert.Equal(DocumentInspectionState.Empty, document.InspectionState);
        await document.EditRefresh; Assert.Empty(document.Inspection.Segments);
        Assert.Equal(DocumentInspectionState.Empty, document.InspectionState);
        document.ReplaceWorkingText("PID|Synthetic"); await document.EditRefresh;
        Assert.Equal(DocumentInspectionState.Current, document.InspectionState); Assert.Single(document.Inspection.Segments);
        document.Undo(); await document.EditRefresh; Assert.Equal(DocumentInspectionState.Empty, document.InspectionState);
        document.Undo(); await document.EditRefresh; Assert.Equal(Text, document.Message.RawText);
    }
    [Fact]
    public async Task PendingSnapshotNeverCrossesMessageNavigationOrPublishesObsoleteResults()
    {
        using var documents = new DocumentManager(); using var source = new MemoryStream(Encoding.UTF8.GetBytes(Text + "MSH|^~\\&|TWO\rPID|Other\r"));
        var document = await MessageFileReader.OpenFileAsync(documents, "Synthetic", source, TestContext.Current.CancellationToken);
        var snapshot = document.InspectionSnapshot; document.IsEditMode = true;
        var draft = Text.Replace("Alpha", "EditedAlpha", StringComparison.Ordinal); document.ReplaceWorkingText(draft);
        var pending = document.EditRefresh;
        var navigation = document.NavigateAsync(2, TestContext.Current.CancellationToken);
        Assert.Null(document.InspectionSnapshot); Assert.Equal(DocumentInspectionState.Unavailable, document.InspectionState);
        await navigation; await pending;
        Assert.Equal(2, document.MessageNumber); Assert.NotSame(snapshot, document.InspectionSnapshot);
        Assert.Equal(DocumentInspectionState.Current, document.InspectionState); Assert.Equal("Other", document.Message.Get("PID-1").Raw);
        await document.NavigateAsync(1, TestContext.Current.CancellationToken); Assert.Equal(draft, document.Message.RawText);
    }
}
