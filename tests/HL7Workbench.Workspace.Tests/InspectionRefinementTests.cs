using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class InspectionRefinementTests
{
    private static MessageInspection Inspect(string text) => new(Hl7Parser.Parse(text));

    [Theory]
    [InlineData("EXAMPLE")]
    [InlineData("")]
    [InlineData("\"\"")]
    [InlineData("EXAMPLE\\Zhas^delimiter\\")]
    public void FieldsWithoutStructuralSplitsAreLeaves(string value)
    {
        var state = Inspect("PID|" + value);
        var field = Assert.Single(state.Fields);
        Assert.Empty(field.VisibleChildren);
        Assert.Equal(value, field.Raw);
        Assert.Single(field.Children); // Core's implicit branch remains addressable.
    }

    [Fact]
    public void ComponentsHaveCompactLabelsAndCanonicalImplicitRepetitions()
    {
        var state = Inspect("PID|||||EXAMPLE^CHARLIE");
        var field = state.InspectField(5)!;
        Assert.Equal(new[] { "PID[1]-5.1", "PID[1]-5.2" }, field.VisibleChildren.Select(n => n.DisplayPath));
        Assert.Equal(new[] { "PID[1]-5[1].1", "PID[1]-5[1].2" }, field.VisibleChildren.Select(n => n.Path.ToString()));
        Assert.All(field.VisibleChildren, n => Assert.Empty(n.VisibleChildren));
        var suppressed = field.Children[0].Children[1].Children[0];
        state.SelectNode(suppressed);
        Assert.Same(suppressed, state.SelectedNode);
        Assert.Same(field.VisibleChildren[1], state.SelectedVisibleNode);
        Assert.Equal(new[] { field, field.VisibleChildren[1] }, state.SelectedVisibleBranch);
        Assert.Equal("PID[1]-5[1].2.1", state.SelectedNode!.Path.ToString());
    }

    [Fact]
    public void ActualSubcomponentsAppearConsistentlyUnderTheirComponent()
    {
        var field = Inspect("PID|EXAMPLE^CHARLIE&ROBERT").Fields[0];
        Assert.Empty(field.VisibleChildren[0].VisibleChildren);
        var component = field.VisibleChildren[1];
        Assert.Equal(new[] { "CHARLIE", "ROBERT" }, component.VisibleChildren.Select(n => n.Raw));
        Assert.Equal(new[] { "PID[1]-1.2.1", "PID[1]-1.2.2" }, component.VisibleChildren.Select(n => n.DisplayPath));
        Assert.All(component.VisibleChildren, n => Assert.Empty(n.VisibleChildren));
    }

    [Fact]
    public void ActualRepetitionsAndEmptyDelimiterCreatedNodesSurvive()
    {
        var field = Inspect("PID|SMITH^JOHN~JONES^MARY~").Fields[0];
        Assert.Equal(new[] { "PID[1]-1[1]", "PID[1]-1[2]", "PID[1]-1[3]" }, field.VisibleChildren.Select(n => n.DisplayPath));
        Assert.Equal(new[] { "SMITH", "JOHN" }, field.VisibleChildren[0].VisibleChildren.Select(n => n.Raw));
        Assert.Equal(Hl7ValueState.Empty, field.VisibleChildren[2].State);
        var empty = Inspect("PID|^~&").Fields[0].VisibleChildren;
        Assert.Equal(2, empty.Count);
        Assert.Equal(2, empty[0].VisibleChildren.Count);
        Assert.All(empty[0].VisibleChildren, n => Assert.Equal(Hl7ValueState.Empty, n.State));
        Assert.Equal(2, empty[1].VisibleChildren.Count);
        Assert.All(empty[1].VisibleChildren, n => Assert.Equal(Hl7ValueState.Empty, n.State));
    }

    [Theory]
    [InlineData("MSH|^~\\&|SYN\rPID|A^B&C~D\rPID|E^F&G~H")]
    [InlineData("MSH*$%!@*SYN\rPID*A$B@C%D\rPID*E$F@G%H")]
    public void CustomDelimitersRepeatedSegmentsAndReverseResolutionShareCanonicalNodes(string text)
    {
        var state = Inspect(text);
        state.SelectDeepestOffset(text.LastIndexOf('G'));
        Assert.Equal("PID[2]-1[1].2.2", state.SelectedNode!.Path.ToString());
        Assert.Equal("G", state.SelectedNode.Raw);
        Assert.Equal(4, state.SelectedVisibleBranch.Count);
        state.SelectDeepestOffset(text.LastIndexOf('H'));
        Assert.Equal("PID[2]-1[2]", state.SelectedNode!.Path.ToString());
        Assert.Empty(state.SelectedNode.VisibleChildren);
    }

    [Theory]
    [InlineData("PID|PLAIN", "PLAIN", "PID[1]-1")]
    [InlineData("PID|EXAMPLE^CHARLIE", "CHARLIE", "PID[1]-1[1].2")]
    [InlineData("PID|EXAMPLE^CHARLIE&ROBERT", "ROBERT", "PID[1]-1[1].2.2")]
    [InlineData("PID|A~B^C", "C", "PID[1]-1[2].2")]
    [InlineData("PID|A&B", "B", "PID[1]-1[1].1.2")]
    [InlineData("PID|\"\"", "\"", "PID[1]-1")]
    [InlineData("PID| SPACE ^B", "SPACE", "PID[1]-1[1].1")]
    public void DoubleClickResolvesDeepestVisibleRange(string text, string target, string path)
    {
        var state = Inspect(text);
        state.SelectDeepestOffset(text.IndexOf(target, StringComparison.Ordinal));
        Assert.Equal(path, state.SelectedNode!.Path.ToString());
        Assert.Same(state.SelectedNode, state.SelectedVisibleNode);
        var range = state.SelectedRange!.Value;
        Assert.Equal(state.SelectedNode.Raw, text.Substring(range.Offset, range.Length));
    }

    [Theory]
    [InlineData(0, null)] // identifier
    [InlineData(3, null)] // field separator
    [InlineData(5, "PID[1]-1[1]")] // component separator inside repetition
    [InlineData(7, "PID[1]-1[1].2")] // subcomponent separator
    [InlineData(9, "PID[1]-1")] // repetition separator
    [InlineData(11, "PID[1]-1[2]")] // whitespace inside value
    [InlineData(13, null)] // CR
    [InlineData(14, null)] // LF
    public void SeparatorAndWhitespaceBoundariesSelectContainingVisibleParent(int offset, string? path)
    {
        const string text = "PID|A^B&C~D E\r\n";
        var state = Inspect(text);
        state.SelectDeepestOffset(offset);
        Assert.Equal(path, state.SelectedNode?.Path.ToString());
        Assert.NotNull(state.SelectedSegment);
    }

    [Fact]
    public void MshLiteralFieldsDoNotCreateBranchesFromTheirDelimiterSpelling()
    {
        var state = Inspect("MSH*$%!@*SYN\r");
        state.SelectDeepestOffset(3);
        Assert.Equal("MSH[1]-1", state.SelectedNode!.Path.ToString());
        state.SelectDeepestOffset(5);
        Assert.Equal("MSH[1]-2", state.SelectedNode!.Path.ToString());
        Assert.Empty(state.SelectedNode.VisibleChildren);
        state.SelectDeepestOffset(9);
        Assert.Equal("MSH[1]-3", state.SelectedNode!.Path.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad line\r\nPID|")]
    [InlineData("MSH|bad|SYN\rOBX|A\\Z^B\\|C")]
    [InlineData("PID|^&~~\r\n\nZ99| ")]
    public void AllOffsetsInMalformedAndEmptyContentAreSafe(string text)
    {
        var state = Inspect(text);
        for (var offset = -1; offset <= text.Length + 1; offset++)
        {
            state.SelectDeepestOffset(offset);
            Assert.NotEqual(Hl7ValueState.Missing, state.SelectedNode?.State);
        }
    }

    [Fact]
    public void ReverseSelectionIsDocumentSpecificAndMissingLookupRemainsDistinct()
    {
        var one = Inspect("PID|A^B");
        var two = Inspect("PID|C^D");
        one.SelectDeepestOffset(6);
        two.SelectDeepestOffset(4);
        Assert.Equal("B", one.SelectedNode!.Raw);
        Assert.Equal("C", two.SelectedNode!.Raw);
        var missing = one.InspectField(9)!;
        Assert.Equal(Hl7ValueState.Missing, missing.State);
        Assert.Null(one.SelectedVisibleNode);
        one.SelectDeepestOffset(4);
        Assert.Equal("A", one.SelectedNode!.Raw);
        Assert.Equal("C", two.SelectedNode!.Raw);
    }

    [Fact]
    public void ReverseLookupRetainsOccurrenceIdentityInLongRepeatedMessages()
    {
        var text = string.Concat(Enumerable.Range(1, 15_000).Select(i => $"Z99|SYN-{i}\r"));
        var state = Inspect(text);
        state.SelectDeepestOffset(text.LastIndexOf("SYN-15000", StringComparison.Ordinal));
        Assert.Equal("Z99[15000]-1", state.SelectedNode!.Path.ToString());
        Assert.Equal("SYN-15000", state.SelectedNode.Raw);
        Assert.Single(state.SelectedVisibleBranch);
    }
}
