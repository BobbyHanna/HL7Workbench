using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class ParserTests
{
    [Fact]
    public void ParsesFullHierarchyAndSpecialMshIndices()
    {
        var message = Hl7Parser.Parse(SyntheticMessages.Message());
        Assert.Empty(message.Diagnostics);
        Assert.Empty(Hl7Validator.Validate(message));
        Assert.Equal("|", message.Get("MSH-1").Raw);
        Assert.Equal("^~\\&", message.Get("MSH-2").Raw);
        Assert.Equal("SYNTHETIC", message.Get("MSH-3").Raw);
        Assert.Equal("ADT^A01", message.Get("MSH-9").Raw);
        Assert.Equal("SYN-0001", message.Get("MSH-10").Raw);
        Assert.Equal("2.5", message.Get("MSH-12").Raw);
        Assert.Equal("SYN-001", message.Get("PID-3.1").Raw);
        Assert.Equal("ALT-001", message.Get("PID-3[2].1").Raw);
        Assert.Equal("Ada", message.Get("PID-5.2").Raw);
        var field = message.Segments[1].GetField(3)!;
        Assert.Equal(2, field.Repetitions.Count);
        Assert.Equal(5, field.Repetitions[0].Components.Count);
        Assert.Equal("MR", field.Repetitions[0].Components[4].Subcomponents[0].Raw);
    }

    [Fact]
    public void DistinguishesMissingEmptyExplicitNullAtAllLevels()
    {
        var message = Hl7Parser.Parse(SyntheticMessages.Header + "\rPID||\"\"|A^~\"\"&|\r");
        Assert.Equal(Hl7ValueState.Empty, message.Get("PID-1").State);
        Assert.Equal(Hl7ValueState.ExplicitNull, message.Get("PID-2").State);
        Assert.Equal(Hl7ValueState.Empty, message.Get("PID-3.2").State);
        Assert.Equal(Hl7ValueState.ExplicitNull, message.Get("PID-3[2].1.1").State);
        Assert.Equal(Hl7ValueState.Empty, message.Get("PID-3[2].1.2").State);
        Assert.Equal(Hl7ValueState.Missing, message.Get("PID-3[2].1.3").State);
        Assert.Equal(Hl7ValueState.Empty, message.Get("PID-4").State);
        Assert.Equal(Hl7ValueState.Missing, message.Get("PID-5").State);
        Assert.Equal(Hl7ValueState.Missing, message.Get("OBX-5").State);
    }

    [Fact]
    public void CustomDelimitersApplyAtEveryLevel()
    {
        const string raw = "MSH*$%!@*SYN*TEST\rPID*1**A$B@C%D$E**Example$Ada*\r";
        var message = Hl7Parser.Parse(raw);
        Assert.Equal(new Hl7Delimiters('*', '$', '%', '!', '@'), message.Delimiters);
        Assert.Equal("*", message.Get("MSH-1").Raw);
        Assert.Equal("$%!@", message.Get("MSH-2").Raw);
        Assert.Single(message.Segments[0].GetField(2)!.Repetitions);
        Assert.Equal("C", message.Get("PID-3.2.2").Raw);
        Assert.Equal("D", message.Get("PID-3[2].1").Raw);
        Assert.Equal("Ada", message.Get("PID-5.2").Raw);
        Assert.Equal(raw, Hl7Serializer.SerializeText(message));
    }

    [Fact]
    public void RetainsOptionalTruncationCharacterWithoutChangingValues()
    {
        var message = Hl7Parser.Parse("MSH|^~\\&#|SYN\rPID|value#\r");
        Assert.Equal('#', message.Delimiters.Truncation);
        Assert.Equal("^~\\&#", message.Get("MSH-2").Raw);
        Assert.Equal("value#", message.Get("PID-1").Raw);
        Assert.Empty(message.Diagnostics);
    }

    [Fact]
    public void UnknownAndZSegmentsRetainOrderAndOccurrences()
    {
        var message = Hl7Parser.Parse(SyntheticMessages.Header + "\rZZZ|a||\rABC|b\rOBX|1||||first\rOBX|2||||second\r");
        Assert.Equal(new[] { "MSH", "ZZZ", "ABC", "OBX", "OBX" }, message.Segments.Select(s => s.Identifier));
        Assert.Equal("first", message.Get("OBX-5").Raw);
        Assert.Equal("second", message.Get("OBX[2]-5").Raw);
        Assert.Equal(Hl7ValueState.Missing, message.Get("OBX[3]-5").State);
        Assert.Equal(3, message.Segments[1].Fields.Count);
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void PreservesTerminatorsAndFinalDelimiter(string ending)
    {
        var raw = SyntheticMessages.Message(ending) + "ZZZ|||" + ending;
        var message = Hl7Parser.Parse(raw);
        Assert.All(message.Segments, s => Assert.Equal(ending, s.Terminator));
        Assert.Equal(3, message.Segments[2].Fields.Count);
        Assert.Equal(raw, Hl7Serializer.SerializeText(message));
    }

    [Fact]
    public void PreservesMixedEndingsBlankLinesAndNoFinalTerminator()
    {
        var raw = "\r\n" + SyntheticMessages.Header + "\rPID|1\n\nZZZ|value\r\nABC|last";
        var message = Hl7Parser.Parse(raw);
        Assert.Equal(new[] { "\r\n", "\r", "\n", "\n", "\r\n", "" }, message.Segments.Select(s => s.Terminator));
        Assert.Contains(message.Diagnostics, d => d.Code == "MshNotFirst");
        Assert.Equal(raw, Hl7Serializer.SerializeText(message));
        message.SetRaw("PID-1", "2");
        Assert.Equal(raw.Replace("PID|1", "PID|2", StringComparison.Ordinal), Hl7Serializer.SerializeText(message));
    }

    [Theory]
    [InlineData("PID|1||synthetic", "MissingMsh", true)]
    [InlineData("MSH", "InvalidDelimiters", false)]
    [InlineData("MSH|", "InvalidDelimiters", false)]
    [InlineData("MSH|^|SYN", "InvalidDelimiters", false)]
    [InlineData("MSH|^^\\&|SYN", "InvalidDelimiters", false)]
    [InlineData("MSH|^~\\&XQ|SYN", "InvalidDelimiters", false)]
    [InlineData("MSH|^~\\&|SYN\rMSH|^~\\&|OTHER", "MultipleMessages", false)]
    [InlineData("PID!1", "MalformedSegment", true)]
    [InlineData("  PID|1", "MalformedSegment", true)]
    [InlineData("P|1", "MalformedSegment", true)]
    public void MalformedMessagesRetainExactContentAndExplainRecovery(string raw, string code, bool editable)
    {
        var message = Hl7Parser.Parse(raw);
        Assert.Equal(raw, Hl7Serializer.SerializeText(message));
        Assert.Contains(message.Diagnostics, d => d.Code == code);
        Assert.Equal(editable, message.CanEdit);
        if (!editable) Assert.Throws<InvalidOperationException>(() => message.SetRaw("MSH-3", "NEW"));
    }

    [Fact]
    public void EmptyMessageIsParsedAndValidatedSeparately()
    {
        var message = Hl7Parser.Parse("");
        Assert.Empty(message.Segments);
        Assert.Equal("", Hl7Serializer.SerializeText(message));
        Assert.Contains(Hl7Validator.Validate(message), d => d.Code == "EmptyMessage");
    }

    [Fact]
    public void ValidationDoesNotChangeParsingOrExposeValues()
    {
        const string raw = "MSH|^~\\&|SYNTHETIC-PRIVATE-MARKER";
        var message = Hl7Parser.Parse(raw);
        Assert.Empty(message.Diagnostics);
        var validation = Hl7Validator.Validate(message);
        Assert.Equal(3, validation.Count);
        Assert.All(validation, d => Assert.DoesNotContain("PRIVATE-MARKER", d.ToString()));
        Assert.Equal(raw, Hl7Serializer.SerializeText(message));
        Assert.Empty(message.Diagnostics);
    }

    [Theory]
    [InlineData("2.3")]
    [InlineData("2.4")]
    [InlineData("2.5")]
    [InlineData("2.6")]
    [InlineData("2.7")]
    [InlineData("2.8.2")]
    [InlineData("unexpected")]
    public void ParsingDoesNotDependOnVersionDictionary(string version)
    {
        var raw = SyntheticMessages.Header[..^3] + version;
        var message = Hl7Parser.Parse(raw);
        Assert.Equal(version, message.Get("MSH-12").Raw);
        Assert.Equal(raw, Hl7Serializer.SerializeText(message));
    }
}
