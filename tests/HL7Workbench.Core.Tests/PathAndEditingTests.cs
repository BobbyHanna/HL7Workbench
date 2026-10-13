using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class PathAndEditingTests
{
    [Theory]
    [InlineData("PID-3.1", "PID[1]-3[1].1")]
    [InlineData("PID-5.2", "PID[1]-5[1].2")]
    [InlineData("OBX-5", "OBX[1]-5")]
    [InlineData("PID[2]-3[4].2.1", "PID[2]-3[4].2.1")]
    [InlineData("PID-3[2]", "PID[1]-3[2]")]
    [InlineData("Z99-01.01", "Z99[1]-1[1].1")]
    public void PathsHaveCanonicalOneBasedCoordinates(string alias, string canonical)
    {
        var path = Hl7Path.Parse(alias);
        Assert.Equal(canonical, path.ToString());
        Assert.Equal(path, Hl7Path.Parse(canonical));
    }

    [Theory]
    [InlineData("")]
    [InlineData("pid-3")]
    [InlineData("PI-3")]
    [InlineData("PID-0")]
    [InlineData("PID[0]-1")]
    [InlineData("PID-1[0]")]
    [InlineData("PID-1.0")]
    [InlineData("PID-1.1.0")]
    [InlineData("PID[-1]-3")]
    [InlineData("PID-3[-1]")]
    [InlineData("PID[1-3")]
    [InlineData("PID-3[]")]
    [InlineData("PID-3.")]
    [InlineData("PID-3.1.1.1")]
    [InlineData("PID -3")]
    [InlineData("PID-3 ")]
    [InlineData("PID-2147483648")]
    [InlineData("PID-٣")]
    public void InvalidPathsAreRejected(string path) => Assert.Throws<FormatException>(() => Hl7Path.Parse(path));

    [Fact]
    public void TypedPathsRejectInvalidCoordinates()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Hl7Path("PID", 0));
        Assert.Throws<ArgumentException>(() => new Hl7Path("PID", 1, subcomponent: 1));
        Assert.Throws<ArgumentException>(() => new Hl7Path("pid", 1));
    }

    [Theory]
    [InlineData("PID-5.2", "Ada", "Synthetic")]
    [InlineData("PID-3[2].1", "ALT-001", "ALT-999")]
    [InlineData("MSH-10", "SYN-0001", "SYN-0002")]
    public void TargetedEditsPreserveEverythingElse(string path, string old, string replacement)
    {
        var raw = SyntheticMessages.Message("\r\n");
        var message = Hl7Parser.Parse(raw);
        message.SetRaw(path, replacement);
        Assert.True(message.IsModified);
        Assert.Equal(raw.Replace(old, replacement, StringComparison.Ordinal), Hl7Serializer.SerializeText(message));
        Assert.Equal(replacement, Hl7Parser.Parse(Hl7Serializer.Serialize(message)).Get(path).Raw);
    }

    [Fact]
    public void WholeFieldAndRepetitionEditsHaveClearScope()
    {
        var message = Hl7Parser.Parse(SyntheticMessages.Message());
        message.SetRaw("PID-3[2]", "NEW^B&C");
        Assert.Equal("SYN-001^^^TEST^MR~NEW^B&C", message.Get("PID-3").Raw);
        message.SetRaw("PID-3", "A&B^C~D");
        Assert.Equal("B", message.Get("PID-3.1.2").Raw);
        Assert.Equal("D", message.Get("PID-3[2].1").Raw);
    }

    [Fact]
    public void MissingFieldsAndNestedElementsExpandWithEmptyPlaceholders()
    {
        var message = Hl7Parser.Parse(SyntheticMessages.Header + "\rPID|1");
        message.SetRaw("PID-5[2].3.2", "VALUE");
        Assert.Equal("PID|1||||~^^&VALUE", message.Segments[1].Raw);
        Assert.Equal(Hl7ValueState.Empty, message.Get("PID-2").State);
        Assert.Equal(Hl7ValueState.Empty, message.Get("PID-5[1].1").State);
        Assert.Equal(Hl7ValueState.Empty, message.Get("PID-5[2].3.1").State);
        Assert.Equal("VALUE", message.Get("PID-5[2].3.2").Raw);
        Assert.Equal(Hl7ValueState.Missing, message.Get("PID-6").State);
    }

    [Fact]
    public void BareSegmentCanReceiveItsFirstFields()
    {
        var message = Hl7Parser.Parse("PID");
        Assert.Equal(Hl7ValueState.Missing, message.Get("PID-1").State);
        message.SetRaw("PID-3", "\"\"");
        Assert.Equal("PID|||\"\"", message.RawText);
        Assert.Equal(Hl7ValueState.ExplicitNull, message.Get("PID-3").State);
    }

    [Fact]
    public void EmptyAndNullEditsAreNotNormalized()
    {
        var message = Hl7Parser.Parse("PID|value");
        message.SetRaw("PID-1", "\"\"");
        Assert.Equal(Hl7ValueState.ExplicitNull, message.Get("PID-1").State);
        message.SetRaw("PID-1", "");
        Assert.Equal("PID|", message.RawText);
        Assert.Equal(Hl7ValueState.Empty, message.Get("PID-1").State);
        Assert.Equal(Hl7ValueState.Missing, message.Get("PID-2").State);
        message.SetRaw("PID-2", "");
        Assert.Equal("PID||", message.RawText);
    }

    [Theory]
    [InlineData("PID-1", "a|b")]
    [InlineData("PID-1[1]", "a~b")]
    [InlineData("PID-1.1", "a^b")]
    [InlineData("PID-1.1.1", "a&b")]
    [InlineData("PID-1", "a\rb")]
    [InlineData("PID-1", "a\nb")]
    [InlineData("PID-1", "\\Zx\r\\")]
    [InlineData("PID-1", "\\F")]
    public void UnsafeRawEditsAreRejectedAtomically(string path, string value)
    {
        var message = Hl7Parser.Parse("PID|original");
        Assert.Throws<ArgumentException>(() => message.SetRaw(path, value));
        Assert.Equal("PID|original", message.RawText);
        Assert.False(message.IsModified);
    }

    [Fact]
    public void TextEditEscapesDelimitersAndPreservesLiteralMeaning()
    {
        var message = Hl7Parser.Parse(SyntheticMessages.Message());
        const string value = "A|B^C~D&E\\F";
        message.SetText("PID-5.2", value);
        Assert.Equal("A\\F\\B\\S\\C\\R\\D\\T\\E\\E\\F", message.Get("PID-5.2").Raw);
        Assert.Equal(value, message.GetDecoded("PID-5.2").Text);
        Assert.Equal("Example", message.Get("PID-5.1").Raw);
    }

    [Theory]
    [InlineData("MSH-1")]
    [InlineData("MSH-2")]
    [InlineData("MSH-2.1")]
    public void DelimiterDefinitionIsReadOnly(string path)
    {
        var message = Hl7Parser.Parse(SyntheticMessages.Message());
        Assert.Throws<InvalidOperationException>(() => message.SetRaw(path, "X"));
        Assert.False(message.IsModified);
    }

    [Fact]
    public void MissingAndOpaqueSegmentsCannotBeCreatedOrEditedImplicitly()
    {
        var message = Hl7Parser.Parse("PID!bad\rZZZ|valid");
        Assert.Throws<InvalidOperationException>(() => message.SetRaw("PID-1", "x"));
        Assert.Throws<KeyNotFoundException>(() => message.SetRaw("OBX-1", "x"));
        message.SetRaw("ZZZ-1", "new");
        Assert.Equal("PID!bad\rZZZ|new", message.RawText);
    }

    [Fact]
    public void NoOpEditDoesNotMarkMessageModified()
    {
        var message = Hl7Parser.Parse(SyntheticMessages.Message());
        message.SetRaw("PID-5.2", "Ada");
        Assert.False(message.IsModified);
    }

    [Fact]
    public void EditingOneOccurrenceLeavesOthersUnchanged()
    {
        var message = Hl7Parser.Parse("OBX|1||||first\rOBX|2||||second\r");
        message.SetRaw("OBX[2]-5", "new");
        Assert.Equal("OBX|1||||first\rOBX|2||||new\r", message.RawText);
    }

    [Theory]
    [InlineData("PID|unclosed\\F|old", "PID-2")]
    [InlineData("PID|unclosed\\F^old", "PID-1.2")]
    public void NewEscapeCannotReinterpretAnExistingMalformedNeighbor(string raw, string path)
    {
        var message = Hl7Parser.Parse(raw);
        Assert.Throws<InvalidOperationException>(() => message.SetText(path, "literal|text"));
        Assert.Equal(raw, message.RawText);
        Assert.False(message.IsModified);
        message.SetRaw(path, "plain");
        Assert.Equal("plain", message.Get(path).Raw);
    }
}
