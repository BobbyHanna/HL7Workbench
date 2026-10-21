using System.Text;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class FieldSelectionTests
{
    private const string Text = "MSH|^~\\&|SYN\rPID|1||A&B^TYPE~C&D^ALT||Alpha^Ada~Beta^Bea\rPID|2||E^MR||Gamma^Gina\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Hl7ExtractedValue Read(string path, string text = Text, bool decode = true, int maximum = 65536)
    {
        var source = new StringHl7TextSource(text); var structure = Hl7StructureScanner.Scan(source, Encoding.UTF8, cancellationToken: Token);
        return Hl7FieldSelection.Parse(path).Select(structure, false, token: Token).Matches[0].Read(source, structure, Encoding.UTF8, decode, maximum);
    }
    [Theory]
    [InlineData("PID-3", "PID-3", false, false)]
    [InlineData("PID-5.1", "PID-5.1", false, false)]
    [InlineData("PID-5.1.1", "PID-5.1.1", false, false)]
    [InlineData(" PID[01]-3[02].4.1 ", "PID[1]-3[2].4.1", true, true)]
    [InlineData("PID[2]-3", "PID[2]-3", true, false)]
    public void CanonicalPathsRetainExplicitIndexIntent(string path, string expected, bool occurrence, bool repetition)
    {
        var selector = Hl7FieldSelection.Parse(path); Assert.Equal(expected, selector.CanonicalPath);
        Assert.Equal(occurrence, selector.ExplicitOccurrence); Assert.Equal(repetition, selector.ExplicitRepetition);
    }
    [Theory]
    [InlineData("PID")] [InlineData("pid-3")] [InlineData("PID-0")] [InlineData("PID[0]-3")] [InlineData("PID-3[0]")] [InlineData("PID-3.1.1.1")]
    public void InvalidPathsUseCoreValidation(string path) => Assert.True(Record.Exception(() => Hl7FieldSelection.Parse(path)) is FormatException or ArgumentException);
    [Fact]
    public void FirstMatchSelectsOnlyOneRepetitionAndSupportsAllAddressLevels()
    {
        Assert.Equal("A&B^TYPE", Read("PID-3").Text); Assert.Equal("Alpha", Read("PID-5.1").Text);
        Assert.Equal("B", Read("PID-3.1.2").Text); Assert.Equal("C&D^ALT", Read("PID-3[2]").Text);
        Assert.Equal("E", Read("PID[2]-3.1").Text); Assert.Equal("^~\\&", Read("MSH-2").Text);
        Assert.Equal(Hl7ExtractionStatus.Complete, Read("MSH-2").Status);
    }
    [Fact]
    public void ExpansionIsOrderedAndExplicitOneConstrainsTheSelection()
    {
        var structure = Hl7StructureScanner.Scan(new StringHl7TextSource(Text), Encoding.UTF8, cancellationToken: Token);
        Assert.Equal(new[] { "PID[1]-3[1].1", "PID[1]-3[2].1", "PID[2]-3[1].1" },
            Hl7FieldSelection.Parse("PID-3.1").Select(structure, true, token: Token).Matches.Select(m => m.Path.ToString()));
        Assert.Single(Hl7FieldSelection.Parse("PID[1]-3[1].1").Select(structure, true, token: Token).Matches);
        Assert.Equal(2, Hl7FieldSelection.Parse("PID-3[1].1").Select(structure, true, token: Token).Matches.Count);
        var bounded = Hl7FieldSelection.Parse("PID-3.1").Select(structure, true, 2, Token); Assert.True(bounded.Truncated); Assert.Equal(2, bounded.Matches.Count);
    }
    [Theory]
    [InlineData("NK1-1", Hl7MissingKind.Segment)] [InlineData("PID[3]-1", Hl7MissingKind.Segment)]
    [InlineData("PID-99", Hl7MissingKind.Field)] [InlineData("PID-3[9]", Hl7MissingKind.Repetition)]
    [InlineData("PID-3.9", Hl7MissingKind.Component)] [InlineData("PID-3.1.9", Hl7MissingKind.Subcomponent)]
    public void MissingPositionsRetainTheirReason(string path, Hl7MissingKind missing)
    { var value = Read(path); Assert.Equal(Hl7ValueState.Missing, value.State); Assert.Equal(missing, value.Missing); Assert.Equal("", value.Text); }
    [Fact]
    public void EmptyAndExplicitNullRemainDistinctFromAbsentValuesAndLiteralQuotes()
    {
        const string text = "MSH|^~\\&|SYN\rPID||\"\"|VALUE\r";
        Assert.Equal(Hl7ValueState.Empty, Read("PID-1", text).State); Assert.Equal(Hl7ValueState.ExplicitNull, Read("PID-2", text).State);
        Assert.Equal(Hl7ValueState.Value, Read("PID-3", text).State); Assert.Equal(Hl7ValueState.Missing, Read("PID-4", text).State);
    }
    [Fact]
    public void CustomDelimitersEscapesLimitsAndMalformedValuesAreNeverSilentlyNormalized()
    {
        const string custom = "MSH*$%!?*SYN\rPID*1**A?B$MR%SECOND$MR**Name!S!Literal$Given\r";
        Assert.Equal("Name$Literal", Read("PID-5.1", custom).Text); Assert.Equal("Name!S!Literal", Read("PID-5.1", custom, false).Text);
        Assert.Equal("B", Read("PID-3.1.2", custom).Text);
        var large = Read("PID-5.1", maximum: 2); Assert.Equal(Hl7ExtractionStatus.ValueLimit, large.Status); Assert.Equal("", large.Text);
        Assert.Equal(Hl7ExtractionStatus.DecodeDiagnostic, Read("PID-1", "MSH|^~\\&|SYN\rPID|A\\ZSECRET\\B\r").Status);
        Assert.Equal(Hl7ExtractionStatus.Unreadable, Read("PID-1", "MSH|^~\\&|SYN\rPID|\uD800\r").Status);
        Assert.Equal(Hl7ExtractionStatus.Unstructured, Read("PID-1", "MSH|^~\\&|SYN\rPID invalid\r").Status);
        Assert.NotEqual(Hl7ExtractionStatus.Complete, Read("PID-1", "MSH|^~\\&|SYN\rPID|A\\Z|B\\\r").Status);
    }
    [Fact]
    public void SharedCsvSpellingQuotesAllFieldsAndProtectsFormulasWithoutBom()
    {
        var bytes = CsvRecordEncoder.Encode(["a,b", "a\"b", "a\r\nb", " =1+1", "-2", "@x", "\tx", null, ""]);
        Assert.Equal("\"a,b\",\"a\"\"b\",\"a\r\nb\",\"' =1+1\",\"'-2\",\"'@x\",\"'\tx\",\"\",\"\"\r\n", Encoding.UTF8.GetString(bytes));
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble));
        Assert.Throws<EncoderFallbackException>(() => CsvRecordEncoder.Encode(["\uD800"]));
    }
}
