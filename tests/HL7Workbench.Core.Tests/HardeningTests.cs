using System.Text;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class HardeningTests
{
    [Theory]
    [InlineData("PID-50000000")]
    [InlineData("PID-1[50000000]")]
    [InlineData("PID-1.50000000")]
    [InlineData("PID-1.1.50000000")]
    public void HugeExpansionFailsBeforeAllocatingPadding(string path)
    {
        var message = Hl7Parser.Parse("PID|a", new()
        {
            MaxMessageCharacters = 32, MaxMessageBytes = 32, MaxSegmentLength = int.MaxValue,
            MaxElementsPerContainer = int.MaxValue, MaxEditExpansion = int.MaxValue
        });
        // Warm the same failure path before measuring thread-local allocations.
        Assert.Throws<Hl7LimitException>(() => message.SetRaw(path, "x"));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<Hl7LimitException>(() => message.SetRaw(path, "x"));
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal("MaxMessageCharacters", error.Limit);
        Assert.True(allocated < 128 * 1024, $"Rejected edit allocated {allocated} bytes.");
        Assert.Equal("PID|a", message.RawText);
        Assert.False(message.IsModified);
    }

    [Theory]
    [InlineData("PID-2")]
    [InlineData("PID-1[2]")]
    [InlineData("PID-1.2")]
    [InlineData("PID-1.1.2")]
    public void ByteBudgetIsCheckedBeforeLargeNestedReplacementAllocations(string path)
    {
        var message = Hl7Parser.Parse(Encoding.UTF8.GetBytes("PID|a"), new() { MaxMessageBytes = 8 });
        var value = new string('|', 100_000);
        Assert.Throws<Hl7LimitException>(() => message.SetText(path, value));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var error = Assert.Throws<Hl7LimitException>(() => message.SetText(path, value));
        Assert.True(GC.GetAllocatedBytesForCurrentThread() - before < 128 * 1024);
        Assert.Equal("MaxMessageBytes", error.Limit);
        Assert.Equal(Encoding.UTF8.GetBytes("PID|a"), Hl7Serializer.Serialize(message));
    }

    [Theory]
    [InlineData("PID-1000")]
    [InlineData("PID-1[1000]")]
    [InlineData("PID-1.1000")]
    [InlineData("PID-1.1.1000")]
    public void Utf16PaddingRespectsEncodedByteLimit(string path)
    {
        var encoding = new UnicodeEncoding(false, true, true);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes("PID|a")).ToArray();
        var message = Hl7Parser.Parse(bytes, new() { MaxMessageBytes = 32 });
        Assert.Equal("MaxMessageBytes", Assert.Throws<Hl7LimitException>(() => message.SetRaw(path, "x")).Limit);
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
    }

    [Theory]
    [InlineData("$%", false, false)]
    [InlineData("$%!", true, false)]
    [InlineData("$%!@", true, true)]
    public void ShorterMsh2PreservesDeclaredAndAbsentDelimiters(string encodingCharacters, bool escape, bool subcomponent)
    {
        var raw = $"MSH*{encodingCharacters}*SYN*TEST*APP*TEST*20261013120000**ADT$A01*SYN-1*T*2.3\r" +
            "PID*1**SYN-1$AUTH%ALT$OTHER**Example$Ada*literal&back\\slash\r";
        var message = Hl7Parser.Parse(Encoding.UTF8.GetBytes(raw));
        Assert.Equal('*', message.Delimiters.Field);
        Assert.Equal('$', message.Delimiters.Component);
        Assert.Equal('%', message.Delimiters.Repetition);
        Assert.Equal(escape ? '!' : (char?)null, message.Delimiters.Escape);
        Assert.Equal(subcomponent ? '@' : (char?)null, message.Delimiters.Subcomponent);
        Assert.Empty(message.Diagnostics);
        Assert.Equal("$" + encodingCharacters[1..], message.Get("MSH-2").Raw);
        Assert.Equal("ALT", message.Get("PID-3[2].1").Raw);
        Assert.Equal("literal&back\\slash", message.Get("PID-6.1.1").Raw);
        Assert.Equal(Hl7ValueState.Missing, message.Get("PID-6.1.2").State);
        message.SetRaw("PID-5.2", "NewName");
        Assert.Equal(Encoding.UTF8.GetBytes(raw.Replace("Ada", "NewName", StringComparison.Ordinal)), Hl7Serializer.Serialize(message));
        if (!subcomponent) Assert.Throws<InvalidOperationException>(() => message.SetRaw("PID-5.2.2", "new"));
        if (!escape) Assert.Throws<InvalidOperationException>(() => message.SetText("PID-5.2", "literal*separator"));
    }

    [Theory]
    [InlineData("|^~\\&#", "abcde#", "abcde\\P\\")]
    [InlineData("*$%!@+", "abcde+", "abcde!P!")]
    public void TruncationEscapesRetainLiteralMeaning(string definition, string text, string expected)
    {
        var field = definition[0];
        var raw = $"MSH{definition}{field}SYN\rOBX{field}1{field}ST{field}{field}{field}old\r";
        var message = Hl7Parser.Parse(raw);
        message.SetText("OBX-5", text);
        Assert.Equal(expected, message.Get("OBX-5").Raw);
        Assert.Equal(text, message.GetDecoded("OBX-5").Text);
        Assert.Empty(message.Diagnostics);
        Assert.Equal(text, Hl7Parser.Parse(Hl7Serializer.Serialize(message)).GetDecoded("OBX-5").Text);
    }

    [Theory]
    [InlineData("PID|a\\F|b\\F\\|tail")]
    [InlineData("PID|a\\F^b\\F\\|tail")]
    [InlineData("PID|a\\F~b\\F\\|tail")]
    [InlineData("PID|a\\F&b\\F\\|tail")]
    [InlineData("PID|\\Zlocal|value\\|tail")]
    public void AmbiguousSegmentsRemainLosslessButCannotBeEdited(string segment)
    {
        var raw = SyntheticMessages.Header + "\r" + segment + "\nZZZ|safe\r\n";
        var bytes = Encoding.UTF8.GetBytes(raw);
        var message = Hl7Parser.Parse(bytes, new() { MaxDiagnostics = 1 });
        Assert.True(message.CanEdit);
        Assert.False(message.Segments[1].CanEdit);
        Assert.Single(message.Diagnostics);
        Assert.Contains(Hl7Parser.Parse(bytes).Diagnostics, d => d.Code == "AmbiguousEscape");
        Assert.Throws<InvalidOperationException>(() => message.SetRaw("PID-2", "new"));
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
        message.SetRaw("ZZZ-1", "changed");
        Assert.Equal(Encoding.UTF8.GetBytes(raw.Replace("ZZZ|safe", "ZZZ|changed", StringComparison.Ordinal)), Hl7Serializer.Serialize(message));
    }

    [Theory]
    [InlineData(1200)]
    [InlineData(1201)]
    public void ExplicitUtf16ResolvesBomAndLeadingNull(int codePage)
    {
        var encoding = new UnicodeEncoding(codePage == 1201, true, true);
        var raw = "\0\rPID|old\r";
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(raw)).ToArray();
        var message = Hl7Parser.Parse(bytes, new() { Encoding = encoding });
        Assert.Equal(codePage, message.Encoding.CodePage);
        Assert.Equal(encoding.GetPreamble(), message.ByteOrderMark);
        Assert.Equal(raw, message.RawText);
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
        message.SetRaw("PID-1", "new");
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(raw.Replace("old", "new", StringComparison.Ordinal))).ToArray(), Hl7Serializer.Serialize(message));
    }

    [Fact]
    public void MatchingExplicitUnicodePreambleDoesNotDependOnEmitBomSetting()
    {
        var bytes = new byte[] { 0xFF, 0xFE, 0, 0, 0x50, 0, 0x49, 0, 0x44, 0 };
        var message = Hl7Parser.Parse(bytes, new() { Encoding = new UnicodeEncoding(false, false, true) });
        Assert.Equal(1200, message.Encoding.CodePage);
        Assert.Equal("\0PID", message.RawText);
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(1024)]
    public void ManyFormattedNotesSuppressDiagnosticsWithoutRejectingMessage(int cap)
    {
        var raw = SyntheticMessages.Header + "\r" + string.Concat(Enumerable.Repeat("NTE|1||\\H\\synthetic\\N\\\r", 600));
        var bytes = Encoding.UTF8.GetBytes(raw);
        var message = Hl7Parser.Parse(bytes, new() { MaxDiagnostics = cap });
        Assert.Equal(601, message.Segments.Count);
        Assert.Equal(cap, message.Diagnostics.Count);
        Assert.Contains(message.Diagnostics, d => d.Code == "DiagnosticsSuppressed");
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
        message.SetText("NTE[600]-3", "new note");
        Assert.Equal("new note", message.Get("NTE[600]-3").Raw);
    }

    [Theory]
    [InlineData(65001)]
    [InlineData(1200)]
    [InlineData(1201)]
    [InlineData(28591)]
    public void LiteralQuotesAndExplicitNullRemainDifferent(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage);
        var bytes = encoding.GetBytes("PID|old");
        var message = Hl7Parser.Parse(bytes, new() { Encoding = encoding });
        message.SetText("PID-1", "\"\"");
        Assert.Equal(Hl7ValueState.Value, message.Get("PID-1").State);
        Assert.Equal("\"\"", message.GetDecoded("PID-1").Text);
        var reopened = Hl7Parser.Parse(Hl7Serializer.Serialize(message), new() { Encoding = encoding });
        Assert.Equal(Hl7ValueState.Value, reopened.Get("PID-1").State);
        message.SetNull(new Hl7Path("PID", 1));
        Assert.Equal(Hl7ValueState.ExplicitNull, message.Get("PID-1").State);
        Assert.Equal(encoding.GetBytes("PID|\"\""), Hl7Serializer.Serialize(message));
        message.SetRaw("PID-1", "");
        Assert.Equal(Hl7ValueState.Empty, message.Get("PID-1").State);
        Assert.Equal(Hl7ValueState.Missing, message.Get("PID-2").State);
    }

    [Fact]
    public void NoEscapeDefinitionAllowsIntentionalNullButRefusesLiteralQuotes()
    {
        var message = Hl7Parser.Parse("MSH|^~|SYN\rPID|old");
        Assert.Throws<InvalidOperationException>(() => message.SetText("PID-1", "\"\""));
        Assert.Equal("old", message.Get("PID-1").Raw);
        message.SetNull("PID-1");
        Assert.Equal(Hl7ValueState.ExplicitNull, message.Get("PID-1").State);
    }

    [Theory]
    [InlineData('F', "literal|text")]
    [InlineData('E', "literalEtext")]
    [InlineData('X', "\"\"")]
    public void UnrepresentableLiteralTextIsRejectedRatherThanSilentlyChanged(char escape, string value)
    {
        var raw = $"MSH|^~{escape}&|SYN\rPID|old";
        var message = Hl7Parser.Parse(raw);
        Assert.Throws<InvalidOperationException>(() => message.SetText("PID-1", value));
        Assert.Equal(raw, message.RawText);
        Assert.False(message.IsModified);
    }
}
