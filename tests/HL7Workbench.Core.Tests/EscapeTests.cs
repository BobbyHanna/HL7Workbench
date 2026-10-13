using System.Text;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class EscapeTests
{
    [Theory]
    [InlineData("\\F\\", "|")]
    [InlineData("\\S\\", "^")]
    [InlineData("\\R\\", "~")]
    [InlineData("\\T\\", "&")]
    [InlineData("\\E\\", "\\")]
    [InlineData("\\X4142\\", "AB")]
    [InlineData("\\Xc3a9\\", "é")]
    [InlineData("text", "text")]
    [InlineData("", "")]
    [InlineData("\"\"", "\"\"")]
    public void SupportedEscapesDecodeExplicitlyWithoutChangingRawContent(string raw, string decoded)
    {
        var message = Hl7Parser.Parse("OBX|1|TX|||" + raw);
        Assert.Equal(raw, message.Get("OBX-5").Raw);
        Assert.Equal(decoded, message.GetDecoded("OBX-5").Text);
        Assert.Empty(message.GetDecoded("OBX-5").Diagnostics);
        Assert.Equal("OBX|1|TX|||" + raw, message.RawText);
    }

    [Theory]
    [InlineData("\\H\\highlight\\N\\", "UnsupportedEscape")]
    [InlineData("\\.br\\", "UnsupportedEscape")]
    [InlineData("\\Zlocal|^~&\\", "UnsupportedEscape")]
    [InlineData("\\C2842\\", "UnsupportedEscape")]
    [InlineData("\\M2442\\", "UnsupportedEscape")]
    [InlineData("\\XG0\\", "InvalidHexEscape")]
    [InlineData("\\X1\\", "InvalidHexEscape")]
    [InlineData("\\X\\", "InvalidHexEscape")]
    [InlineData("\\XFF\\", "InvalidHexEscape")]
    [InlineData("\\F", "UnterminatedEscape")]
    [InlineData("\\\\", "UnsupportedEscape")]
    public void UnsupportedAndMalformedEscapesRemainVerbatim(string raw, string code)
    {
        var message = Hl7Parser.Parse(SyntheticMessages.Header + "\rOBX|1|TX|||" + raw);
        Assert.Equal(raw, message.Get("OBX-5").Raw);
        Assert.Equal(raw, message.GetDecoded("OBX-5").Text);
        Assert.Contains(message.Diagnostics, d => d.Code == code);
        Assert.Contains(message.GetDecoded("OBX-5").Diagnostics, d => d.Code == code);
        Assert.All(message.Diagnostics, d => Assert.True(d.CharacterOffset >= 0));
    }

    [Fact]
    public void UnmatchedEscapeDoesNotHideFollowingFieldsOrComponents()
    {
        var message = Hl7Parser.Parse("OBX|1|TX|||prefix\\F^component|next");
        Assert.Equal("prefix\\F", message.Get("OBX-5.1").Raw);
        Assert.Equal("component", message.Get("OBX-5.2").Raw);
        Assert.Equal("next", message.Get("OBX-6").Raw);
        Assert.Contains(message.Diagnostics, d => d.Code == "UnterminatedEscape");
    }

    [Fact]
    public void CustomEscapeCharacterAndLiteralDelimitersUseMshDefinition()
    {
        var message = Hl7Parser.Parse("MSH*$%!@*SYN\rOBX*1*TX***!F!!S!!R!!T!!E!");
        Assert.Equal("*$%@!", message.GetDecoded("OBX-5").Text);
        message.SetText("OBX-5", "*$%@!");
        Assert.Equal("!F!!S!!R!!T!!E!", message.Get("OBX-5").Raw);
        Assert.Empty(message.Diagnostics);
    }

    [Fact]
    public void DecodingMshEncodingCharactersIsLiteral()
    {
        var message = Hl7Parser.Parse(SyntheticMessages.Header);
        Assert.Equal("^~\\&", message.GetDecoded("MSH-2").Text);
        Assert.Empty(message.GetDecoded("MSH-2").Diagnostics);
    }

    [Fact]
    public void HexDecodingUsesSelectedEncoding()
    {
        var result = Hl7EscapeCodec.Decode("\\XE9\\", Hl7Delimiters.Default, Encoding.Latin1);
        Assert.Equal("é", result.Text);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void SupportedEscapeAfterDiagnosticAtLimitDoesNotFail()
    {
        var result = Hl7EscapeCodec.Decode("\\Q\\\\F\\", Hl7Delimiters.Default, maxDiagnostics: 1);
        Assert.Equal("\\Q\\|", result.Text);
        Assert.Single(result.Diagnostics);
    }

    [Fact]
    public void UnsupportedEscapeNeighborSurvivesAnEdit()
    {
        const string raw = "OBX|1|TX|||\\Zsynthetic\\^old|next";
        var message = Hl7Parser.Parse(raw);
        message.SetRaw("OBX-5.2", "new");
        Assert.Equal(raw.Replace("old", "new", StringComparison.Ordinal), message.RawText);
        Assert.Contains(message.Diagnostics, d => d.Code == "UnsupportedEscape");
    }
}
