using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class MessageInspectionTests
{
    private static MessageDocument Document(string text) => new(Guid.NewGuid().ToString("N"), "Synthetic", Hl7Parser.Parse(text));

    [Fact]
    public void SegmentOrderOccurrenceAndRangesIncludeUnknownAndOpaqueLines()
    {
        const string text = "MSH|^~\\&|SYN\rOBX|1|TX|||First\r\n\nOBX|2|TX|||Second\nZ99|custom\rbad line";
        var document = Document(text);
        var state = document.Inspection;
        Assert.Equal(new[] { "MSH", "OBX", "", "OBX", "Z99", "bad" }, state.Segments.Select(s => s.Segment.Identifier));
        Assert.Equal(new[] { 1, 1, 1, 2, 1, 1 }, state.Segments.Select(s => s.Occurrence));
        Assert.Equal(Enumerable.Range(1, 6), state.Segments.Select(s => s.Sequence));
        foreach (var segment in state.Segments)
        {
            state.SelectSegment(segment);
            Assert.Equal(segment.Segment.Raw, text.Substring(state.SelectedRange!.Value.Offset, state.SelectedRange.Value.Length));
        }
        Assert.False(state.SelectedSegment!.HasFieldPaths);
        Assert.Empty(state.Fields);
        Assert.Null(state.InspectField(1));
    }

    [Fact]
    public void MshNumberingAndLiteralEncodingCharactersMatchCore()
    {
        var document = Document("MSH*$%!@*SYN*********99.9\r");
        var state = document.Inspection;
        Assert.Equal("*", state.InspectField(1)!.Raw);
        Assert.Equal("$%!@", state.InspectField(2)!.Raw);
        Assert.Equal("SYN", state.InspectField(3)!.Raw);
        Assert.Equal("MSH[1]-2", state.Fields[1].Path.ToString());
        var literal = Assert.Single(Assert.Single(Assert.Single(state.Fields[1].Children).Children).Children);
        Assert.Equal("$%!@", literal.Raw);
        Assert.Equal("MSH[1]-2[1].1.1", literal.Path.ToString());
        Assert.Equal("99.9", document.DeclaredVersion);
    }

    [Theory]
    [InlineData("MSH|^~\\&|SYN\rOBX|1|TX|||A^B&C~D\rOBX|2|TX|||E^F&G~H", "G")]
    [InlineData("MSH*$%!@*SYN\rOBX*1*TX***A$B@C%D\rOBX*2*TX***E$F@G%H", "G")]
    public void NestedPathsAndRangesComeFromParsedStructure(string text, string expected)
    {
        var document = Document(text);
        var state = document.Inspection;
        state.SelectSegment(state.Segments[2]);
        var field = state.InspectField(5)!;
        Assert.Equal(2, field.Children.Count);
        var subcomponent = field.Children[0].Children[1].Children[1];
        state.SelectNode(subcomponent);
        Assert.Equal("OBX[2]-5[1].2.2", subcomponent.Path.ToString());
        Assert.Equal(expected, subcomponent.Raw);
        Assert.Equal(document.Message.Get(subcomponent.Path).Raw, subcomponent.Raw);
        var range = state.SelectedRange!.Value;
        Assert.Equal(expected, text.Substring(range.Offset, range.Length));
        Assert.False(document.Message.IsModified);
    }

    [Fact]
    public void EmptyExplicitNullMissingAndAbsentHierarchyAreDistinct()
    {
        var state = Document("PID||\"\"|A^\"\"&~\r").Inspection;
        Assert.Equal(Hl7ValueState.Empty, state.InspectField(1)!.State);
        Assert.Equal(0, state.SelectedRange!.Value.Length);
        Assert.Equal(Hl7ValueState.ExplicitNull, state.InspectField(2)!.State);
        var composite = state.InspectField(3)!;
        Assert.Equal(Hl7ValueState.ExplicitNull, composite.Children[0].Children[1].Children[0].State);
        Assert.Equal(Hl7ValueState.Empty, composite.Children[0].Children[1].Children[1].State);
        Assert.Equal(Hl7ValueState.Empty, composite.Children[1].State);
        var missing = state.InspectField(4)!;
        Assert.Equal(Hl7ValueState.Missing, missing.State);
        Assert.Null(missing.Raw);
        Assert.Null(state.SelectedRange);
        Assert.Empty(missing.Children);
        Assert.Equal(3, state.Fields.Count);
        Assert.Same(missing, state.InspectField(4));
    }

    [Fact]
    public void DocumentSelectionSurvivesActivationAndIsIndependent()
    {
        var manager = new DocumentManager();
        var one = Document("PID|one\rOBX|first");
        var two = Document("PID|two\rOBX|second");
        manager.Open(one);
        one.Inspection.SelectSegment(one.Inspection.Segments[1]);
        var selected = one.Inspection.InspectField(1);
        manager.Open(two);
        two.Inspection.InspectField(2);
        manager.Activate(one.Id);
        Assert.Same(selected, one.Inspection.SelectedNode);
        Assert.Equal(2, two.Inspection.SelectedNode!.Path.Field);
        manager.Close(two.Id);
        Assert.Same(selected, manager.ActiveDocument!.Inspection.SelectedNode);
    }

    [Fact]
    public void CaretSynchronizationDoesNotRequestRawNavigationOrRepeatNotifications()
    {
        var state = Document("PID|A^B\rOBX|2").Inspection;
        var calls = new List<bool>();
        state.SelectionChanged += (_, e) => calls.Add(e.NavigateRaw);
        state.SelectOffset(4);
        state.SelectOffset(5);
        Assert.Equal(new[] { false }, calls);
        state.SelectSegment(state.Segments[1]);
        Assert.Equal(new[] { false, true }, calls);
        state.SelectSegment(state.Segments[1]);
        Assert.Equal(2, calls.Count);
        state.InspectField(1);
        Assert.Equal(3, calls.Count);
        state.SelectOffset(state.SelectedRange!.Value.Offset);
        Assert.Equal(3, calls.Count);
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad\rPID|1")]
    [InlineData("bad line\r\r")]
    [InlineData("MSH|bad|SYN\rPID|1")]
    [InlineData("MSH|^~\\&|SYN\rOBX|A\\Z^B\\|C")]
    public void IncompleteAndProvisionalStructuresDoNotLoseSource(string text)
    {
        var document = Document(text);
        foreach (var segment in document.Inspection.Segments)
        {
            document.Inspection.SelectSegment(segment);
            foreach (var field in document.Inspection.Fields)
            {
                document.Inspection.SelectNode(field);
                Assert.InRange(field.Range!.Value.Offset + field.Range.Value.Length, 0, text.Length);
            }
        }
        Assert.Equal(text, document.Message.RawText);
    }

    [Fact]
    public void ForeignSelectionsAndInvalidFieldNumbersAreRejected()
    {
        var one = Document("PID|A").Inspection;
        var two = Document("PID|B").Inspection;
        Assert.Throws<ArgumentException>(() => one.SelectSegment(two.Segments[0]));
        Assert.Throws<ArgumentException>(() => one.SelectNode(two.Fields[0]));
        Assert.Throws<ArgumentOutOfRangeException>(() => one.InspectField(0));
    }
}
