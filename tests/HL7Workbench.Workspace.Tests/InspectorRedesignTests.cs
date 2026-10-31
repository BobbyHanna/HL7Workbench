using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class InspectorRedesignTests
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261030000000||ADT^A01|SYN-1|T|2.5.1\r";
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void SegmentHierarchyUsesExactOccurrenceCoreDelimitersAndLazyVisibleLevels(bool custom)
    {
        var text = Header + "PID|1||A^MR~B^ALT\rPID|2||C^MR~D^X&Y\rZ99|RAW\\F\\VALUE\r";
        if (custom) text = text.Replace('|', '*').Replace('^', '$').Replace('~', '%').Replace('\\', '!').Replace('&', '@');
        var parsed = Hl7Parser.Parse(text); var inspection = new MessageInspection(parsed);
        inspection.SelectSegment(inspection.Segments[2]);
        var fields = InspectorProjection.Fields(inspection, false);
        Assert.Equal(new[] { 1, 3 }, fields.Select(f => f.Path.Field));
        var field = fields[1]; var target = field.VisibleChildren[1].VisibleChildren[1].VisibleChildren[1];
        inspection.SelectNode(target);
        Assert.Equal("PID[2]-3[2].2.2", target.Path.ToString()); Assert.Equal("Y", target.Raw);
        Assert.Equal(4, inspection.SelectedVisibleBranch.Count);
        Assert.Equal("Y", text.Substring((int)target.Range64!.Value.Offset, (int)target.Range64.Value.Length));
        Assert.Equal(text, parsed.RawText);
    }
    [Fact]
    public void BlankProjectionDistinguishesRepresentedEmptyAndSchemaAbsentWithoutChangingSourceFields()
    {
        var inspection = new MessageInspection(Hl7Parser.Parse("PID|1||A^\r"));
        var profile = new Hl7ValidationProfile { Segments = [new() { Id = "PID", Fields = [new() { Position = 1, DataType = "SI" }, new() { Position = 4, DataType = "ST" }] }] };
        Assert.Equal(new[] { 1, 3 }, InspectorProjection.Fields(inspection, false, profile).Select(f => f.Path.Field));
        var rows = InspectorProjection.Fields(inspection, true, profile);
        Assert.Equal(new[] { 1, 2, 3, 4 }, rows.Select(f => f.Path.Field));
        Assert.Equal(Hl7ValueState.Empty, rows[1].State); Assert.Equal(0, rows[1].Range64!.Value.Length);
        Assert.Equal(Hl7ValueState.Missing, rows[3].State); Assert.Null(rows[3].Range64);
        Assert.Same(rows[3], InspectorProjection.Fields(inspection, true, profile)[3]);
        Assert.Equal(3, inspection.Fields.Count); Assert.DoesNotContain(InspectorProjection.Children(inspection, rows[2], false), n => n.State == Hl7ValueState.Empty);
        inspection.InspectField(2); Assert.Contains(inspection.Fields[1], InspectorProjection.Fields(inspection, false));
        Assert.Equal("Field 1 · SI", InspectorProjection.Describe(rows[0], profile));
        Assert.Equal("Field 3", InspectorProjection.Describe(rows[2], null));
    }
    [Fact]
    public void MshLiteralFieldsAndMalformedCustomSegmentsKeepTheirStructure()
    {
        var inspection = new MessageInspection(Hl7Parser.Parse(Header + "Z99|A^^B\rBAD opaque\r"));
        Assert.Equal("|", inspection.Fields[0].Raw); Assert.Equal("^~\\&", inspection.Fields[1].Raw);
        Assert.Empty(inspection.Fields[1].VisibleChildren);
        inspection.SelectSegment(inspection.Segments[1]);
        Assert.Equal("Field 1", InspectorProjection.Describe(inspection.Fields[0], null));
        inspection.SelectSegment(inspection.Segments[2]); Assert.Empty(InspectorProjection.Fields(inspection, true));
    }
    [Fact]
    public async Task ContextReadsExactLeafAndRejectsSelectionRevisionCancellationAndClosedSource()
    {
        var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Header + "PID|||A^B\\F\\C\r"));
        try
        {
            var inspection = doc.Inspection; inspection.SelectSegment(inspection.Segments[1]);
            var field = inspection.Fields[2]; var component = field.VisibleChildren[1]; inspection.SelectNode(component);
            var context = new InspectionActionContext(doc, inspection, component);
            Assert.Equal("PID[1]-3[1].2", context.Path); Assert.Equal("PID-3.2", context.SearchPath);
            Assert.Equal("B\\F\\C", await context.ReadValueAsync(TestContext.Current.CancellationToken));
            using var canceled = new CancellationTokenSource(); canceled.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => context.ReadValueAsync(canceled.Token));
            inspection.SelectNode(field); Assert.False(context.IsCurrent); Assert.Null(await context.ReadValueAsync(TestContext.Current.CancellationToken));
            var current = new InspectionActionContext(doc, inspection, field);
            doc.IsEditMode = true; doc.ReplaceWorkingText(Header + "PID|||NEW\r"); Assert.False(current.IsCurrent);
        }
        finally { doc.Dispose(); await doc.Disposal; }
    }
    [Theory]
    [InlineData("", 2)] [InlineData(" ", 1)] [InlineData("A", 1)]
    public async Task ExactEmptyWhitespaceAndRepetitionSearchUsesWholeFileAndDoesNotMatchAbsent(string value, int count)
    {
        using var manager = new DocumentManager();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Header + "PID|A^~B^ \r" + Header + "PID|C^A\r" + Header + "PID|D^\r" + Header + "PID\r"));
        var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
        try
        {
            var original = doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length));
            var query = MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Equals, "PID-1.2", value, true)]);
            Assert.Null(query.ValidationError());
            var result = await new MessageSearch().SearchAsync(doc, query, token: TestContext.Current.CancellationToken);
            Assert.Equal(count, result.Matches.Count);
            Assert.Equal(original, doc.Snapshot.ReadBytes(new(0, doc.Snapshot.Length))); Assert.False(doc.IsDirty);
        }
        finally { manager.Close(doc.Id, true); await doc.Disposal; }
    }
    [Fact]
    public async Task LargeSourceValueCannotBecomeCopiedPreviewOrSearchTerm()
    {
        using var manager = new DocumentManager();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Header + "OBX|1|TX|||" + new string('X', 200000) + "\r"));
        var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
        try
        {
            var inspection = doc.ActiveInspection!; inspection.SelectSegment(inspection.Segments[1]); var node = inspection.InspectField(5)!;
            var context = new InspectionActionContext(doc, inspection, node);
            Assert.True(node.Preview.Length <= 100); Assert.False(context.CanCopyValue); Assert.Null(await context.ReadValueAsync(TestContext.Current.CancellationToken));
            Assert.Equal(4096, inspection.ReadPreview(node).Length); Assert.NotNull(context.FindQuery());
        }
        finally { manager.Close(doc.Id, true); await doc.Disposal; }
    }
    [Fact]
    public async Task WholeRepeatedFieldIsNotMisrepresentedAsOneSearchRepetition()
    {
        var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse("PID|A~B"));
        try
        {
            var field = doc.Inspection.InspectField(1)!; var context = new InspectionActionContext(doc, doc.Inspection, field);
            Assert.Null(context.FilterQuery("A~B"));
            doc.Inspection.SelectNode(field.VisibleChildren[1]);
            var repetition = new InspectionActionContext(doc, doc.Inspection, field.VisibleChildren[1]);
            Assert.NotNull(repetition.FilterQuery("B"));
        }
        finally { doc.Dispose(); await doc.Disposal; }
    }
    [Fact]
    public async Task StaleExtractionDialogDoesNotReplaceRetainedDraft()
    {
        var root = Path.Combine(Path.GetTempPath(), "hl7-inspector-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            var draft = new ExtractionConfigurationSession(new(root)); draft.Duplicate(); var id = draft.Id;
            var gate = new TaskCompletionSource<UnsavedChangesDecision>();
            var action = draft.NewAsync(() => gate.Task, () => false);
            gate.SetResult(UnsavedChangesDecision.Discard);
            Assert.False(await action); Assert.Equal(id, draft.Id); Assert.True(draft.IsDirty);
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
}
