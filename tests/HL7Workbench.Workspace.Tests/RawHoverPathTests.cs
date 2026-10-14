using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class RawHoverPathTests
{
    [Theory]
    [InlineData("PID|||||SMITH^JOHN", "SMITH", "PID[1]-5[1].1")]
    [InlineData("PID|||||SMITH^JOHN", "JOHN", "PID[1]-5[1].2")]
    [InlineData("OBX|1|TX|||SYNTHETIC", "SYNTHETIC", "OBX[1]-5")]
    [InlineData("PID|A\r\nOBX|B\nPID|SMITH^JOHN", "JOHN", "PID[2]-1[1].2")]
    [InlineData("PID|SMITH^JOHN~JONES^MARY&ANN", "JONES", "PID[1]-1[2].1")]
    [InlineData("PID|SMITH^JOHN~JONES^MARY&ANN", "ANN", "PID[1]-1[2].2.2")]
    [InlineData("MSH*$%!@*SYN\rZ99*A$B@C%D$E@F", "F", "Z99[1]-1[2].2.2")]
    [InlineData("MSH|^~\\&|SYN", "SYN", "MSH[1]-3")]
    [InlineData("MSH|^~\\&|SYN", "|", "MSH[1]-1")]
    [InlineData("MSH|^~\\&|SYN", "^", "MSH[1]-2")]
    [InlineData("MSH*$%!@*SYN", "!", "MSH[1]-2")]
    [InlineData("MSH*$%!@*SYN", "*", "MSH[1]-1")]
    [InlineData("PID|  SYN  ", "  ", "PID[1]-1")]
    [InlineData("PID||\"\"|A", "\"", "PID[1]-2")]
    [InlineData("PID|A^B&C~D", "^", "PID[1]-1[1]")]
    [InlineData("PID|A^B&C~D", "&", "PID[1]-1[1].2")]
    [InlineData("PID|A^B&C~D", "~", "PID[1]-1")]
    [InlineData("PID|A^B\\Z^&~inside\\", "inside", "PID[1]-1[1].2")]
    [InlineData("PID|A^B\\Z^&~inside\\", "&", "PID[1]-1[1].2")]
    [InlineData("MSH|bad|SYN\rZ99|A^B", "B", "Z99[1]-1")]
    public void RawCharactersResolveFromCoreStructureWithoutChangingSelection(string text, string target, string expected)
    {
        var inspection = new MessageInspection(Hl7Parser.Parse(text));
        var segment = inspection.SelectedSegment;
        var fields = inspection.Fields;
        var selected = inspection.SelectedNode;
        var changes = 0;
        inspection.SelectionChanged += (_, _) => changes++;
        var offset = text.IndexOf(target, StringComparison.Ordinal);
        var node = inspection.ResolveOffset(offset);
        Assert.NotNull(node);
        Assert.Equal(expected, node.Path.ToString());
        // Repeated queries reuse the structural projection, including across segments.
        Assert.Same(node, inspection.ResolveOffset(offset));
        Assert.Same(segment, inspection.SelectedSegment);
        Assert.Same(fields, inspection.Fields);
        Assert.Same(selected, inspection.SelectedNode);
        Assert.Equal(0, changes);
    }

    [Theory]
    [InlineData("", 0)]
    [InlineData("PID||", 3)]
    [InlineData("PID||", 4)]
    [InlineData("PID||", 5)]
    [InlineData("PID|A\r\n", 5)]
    [InlineData("PID|A\r\n", 6)]
    [InlineData("PID|A\r\n\rZ99|B", 7)]
    [InlineData("PID|A", -1)]
    [InlineData("PID|A", 0)]
    [InlineData("PID|A", 5)]
    [InlineData("PID|A", 6)]
    [InlineData("bad line\rPID|A", 4)]
    [InlineData("bad|value", 4)]
    public void NonValuePositionsDoNotInventPaths(string text, int offset)
    {
        var inspection = new MessageInspection(Hl7Parser.Parse(text));
        Assert.Null(inspection.ResolveOffset(offset));
    }

    [Theory]
    [InlineData("PID||\"\"|^&~\r\n\nZ99| ")]
    [InlineData("MSH|bad|SYN\rOBX|A\\Z^B\\|C")]
    [InlineData("bad line\rMSH\rPID|")]
    public void EveryMalformedBoundaryIsSafeAndNeverSynthesizesMissingValues(string text)
    {
        var inspection = new MessageInspection(Hl7Parser.Parse(text));
        for (var offset = -1; offset <= text.Length + 1; offset++)
            Assert.NotEqual(Hl7ValueState.Missing, inspection.ResolveOffset(offset)?.State);
    }

    [Fact]
    public void LargeRepeatedMessageLookupPreservesOccurrenceAndDocumentState()
    {
        var text = string.Concat(Enumerable.Range(1, 15000).Select(i => $"OBX|SYN-{i}\r"));
        var inspection = new MessageInspection(Hl7Parser.Parse(text));
        var offset = text.LastIndexOf("SYN-15000", StringComparison.Ordinal);
        Assert.Equal("OBX[15000]-1", inspection.ResolveOffset(offset)!.Path.ToString());
        Assert.Equal(1, inspection.SelectedSegment!.Occurrence);
        var other = new MessageInspection(Hl7Parser.Parse("OBX|OTHER"));
        Assert.NotSame(inspection.ResolveOffset(offset), other.ResolveOffset(4));
        Assert.Null(inspection.SelectedNode);
    }
}
