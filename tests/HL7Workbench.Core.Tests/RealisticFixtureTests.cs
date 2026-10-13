using System.Text;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class RealisticFixtureTests
{
    private static byte[] Load(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", name + ".hl7"));

    [Theory]
    [InlineData("adt-a01", "MSH-10", "SYN-ADT1", "SYN-ADT2")]
    [InlineData("orm-o01", "OBR[2]-2.1", "SYN-ORD2", "SYN-NEW-ORDER")]
    [InlineData("oru-r01", "OBX-5", "13.5", "14.1")]
    [InlineData("ack-a01", "MSA-1", "AA", "AE")]
    [InlineData("oru-custom", "OBX-5", "13.5", "14.1")]
    [InlineData("oru-ambiguous", "OBX[2]-5", "unchanged", "changed")]
    public void IndependentlyAuthoredFixturesRoundTripAndEditExactBytes(string name, string path, string before, string after)
    {
        var bytes = Load(name);
        var message = Hl7Parser.Parse(bytes);
        Assert.Equal(before, message.Get(path).Raw);
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
        Assert.False(message.IsModified);
        message.SetRaw(path, after);
        var originalText = Encoding.UTF8.GetString(bytes);
        var expectedText = name == "orm-o01" ? originalText.Replace("OBR|2|" + before, "OBR|2|" + after, StringComparison.Ordinal) :
            originalText.Replace(before, after, StringComparison.Ordinal);
        var expected = Encoding.UTF8.GetBytes(expectedText);
        Assert.Equal(expected, Hl7Serializer.Serialize(message));
        var reopened = Hl7Parser.Parse(expected);
        Assert.Equal(after, reopened.Get(path).Raw);
        Assert.Equal(expected, Hl7Serializer.Serialize(reopened));
    }

    [Fact]
    public void AdtCoordinatesIncludeRepeatedIdentifiersProviderSubcomponentsAndNullSuffix()
    {
        var message = Hl7Parser.Parse(Load("adt-a01"));
        Assert.Empty(message.Diagnostics);
        Assert.Equal(new[] { "MSH", "EVN", "PID", "PV1", "ZSY" }, message.Segments.Select(s => s.Identifier));
        Assert.Equal("ADT_A01", message.Get("MSH-9.3").Raw);
        Assert.Equal("SYN-ALT1", message.Get("PID-3[2].1").Raw);
        Assert.Equal("9.8.7", message.Get("PID-3[2].4.2").Raw);
        Assert.Equal("Casey", message.Get("PV1-7.3").Raw);
        Assert.Equal("ISO", message.Get("PV1-7.9.3").Raw);
        Assert.Equal(Hl7ValueState.ExplicitNull, message.Get("PID-5.4").State);
        Assert.Equal(Hl7ValueState.Empty, message.Get("ZSY-3").State);
        Assert.Equal(Hl7ValueState.Missing, message.Get("ZSY-4").State);
    }

    [Fact]
    public void OrmContainsIndependentOrderGroupsAndConsistentOrderIdentifiers()
    {
        var message = Hl7Parser.Parse(Load("orm-o01"));
        Assert.Empty(message.Diagnostics);
        Assert.Equal(new[] { "MSH", "PID", "ORC", "OBR", "NTE", "ORC", "OBR", "NTE" }, message.Segments.Select(s => s.Identifier));
        Assert.Equal("SYN-ORD1", message.Get("ORC-2.1").Raw);
        Assert.Equal("SYN-ORD1", message.Get("OBR-2.1").Raw);
        Assert.Equal("SYN-ORD2", message.Get("ORC[2]-2.1").Raw);
        Assert.Equal("SYN-ORD2", message.Get("OBR[2]-2.1").Raw);
        Assert.Equal("Synthetic second order", message.Get("NTE[2]-3").Raw);
    }

    [Fact]
    public void OrUContainsNumericCodedRepeatedAndEncapsulatedResults()
    {
        var message = Hl7Parser.Parse(Load("oru-r01"));
        Assert.Equal(2, message.Segments.Count(s => s.Identifier == "OBR"));
        Assert.Equal(4, message.Segments.Count(s => s.Identifier == "OBX"));
        Assert.Equal("POS", message.Get("OBX[2]-5[2].1").Raw);
        Assert.Equal("Base64", message.Get("OBX[3]-5.4").Raw);
        Assert.Equal("SYNTHETIC-ATTACHMENT", Encoding.UTF8.GetString(Convert.FromBase64String(message.Get("OBX[3]-5.5").Raw!)));
        Assert.Equal(Hl7ValueState.ExplicitNull, message.Get("OBX[4]-5").State);
        Assert.Equal("First\\.br\\Second \\H\\marked\\N\\", message.Get("NTE-3").Raw);
        Assert.All(message.Diagnostics, d => Assert.Equal("UnsupportedEscape", d.Code));
        Assert.True(message.Segments.Single(s => s.Identifier == "NTE").CanEdit);
    }

    [Fact]
    public void AckHeaderAndCorrelationFieldUseCorrectMshIndexing()
    {
        var message = Hl7Parser.Parse(Load("ack-a01"));
        Assert.Empty(message.Diagnostics);
        Assert.Equal("SYN-ACK1", message.Get("MSH-10").Raw);
        Assert.Equal("SYN-ADT1", message.Get("MSA-2").Raw);
        Assert.Equal("AA", message.Get("MSA-1").Raw);
    }

    [Fact]
    public void CustomFixtureHasIndependentCoordinatesEscapesAndAttachment()
    {
        var message = Hl7Parser.Parse(Load("oru-custom"));
        Assert.Empty(message.Diagnostics);
        Assert.Equal("SYN-ALT1", message.Get("PID-3[2].1").Raw);
        Assert.Equal("ISO", message.Get("PID-3[2].4.3").Raw);
        Assert.Equal("Literal * separator and final +", message.GetDecoded("NTE-3").Text);
        Assert.Equal("SYNTHETIC-ATTACHMENT", Encoding.UTF8.GetString(Convert.FromBase64String(message.Get("OBX[2]-5.5").Raw!)));
    }

    [Theory]
    [InlineData("\r")]
    [InlineData("\n")]
    [InlineData("\r\n")]
    [InlineData("mixed")]
    public void MultipleResultGroupEditsPreserveUnusualTerminatorsAndUtf16(string ending)
    {
        var lines = Encoding.UTF8.GetString(Load("oru-r01")).Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        var raw = string.Concat(lines.Select((line, index) => line + (ending == "mixed" ?
            new[] { "\r", "\n", "\r\n" }[index % 3] : ending)));
        var encoding = new UnicodeEncoding(false, true, true);
        var original = encoding.GetPreamble().Concat(encoding.GetBytes(raw)).ToArray();
        var message = Hl7Parser.Parse(original);
        Assert.Equal(original, Hl7Serializer.Serialize(message));
        message.SetText("PID-5.2", "Synthetic|Name");
        message.SetRaw("OBX-5", "14.1");
        message.SetNull("OBX[2]-5[2].2");
        var expectedText = raw.Replace("Example^Avery", "Example^Synthetic\\F\\Name", StringComparison.Ordinal)
            .Replace("13.5", "14.1", StringComparison.Ordinal).Replace("POS^Positive^L", "POS^\"\"^L", StringComparison.Ordinal);
        var expected = encoding.GetPreamble().Concat(encoding.GetBytes(expectedText)).ToArray();
        Assert.Equal(expected, Hl7Serializer.Serialize(message));
        var reopened = Hl7Parser.Parse(expected);
        Assert.Equal("Synthetic|Name", reopened.GetDecoded("PID-5.2").Text);
        Assert.Equal(Hl7ValueState.ExplicitNull, reopened.Get("OBX[2]-5[2].2").State);
        Assert.Equal(message.Get("OBX[3]-5").Raw, reopened.Get("OBX[3]-5").Raw);
    }

    [Fact]
    public void MalformedReportLocksOnlyTheAffectedResult()
    {
        var message = Hl7Parser.Parse(Load("oru-ambiguous"));
        Assert.Contains(message.Diagnostics, d => d.Code == "AmbiguousEscape");
        Assert.False(message.Segments.First(s => s.Identifier == "OBX").CanEdit);
        Assert.Throws<InvalidOperationException>(() => message.SetText("OBX-5", "replacement"));
        Assert.Equal("unchanged", message.Get("OBX[2]-5").Raw);
    }
}
