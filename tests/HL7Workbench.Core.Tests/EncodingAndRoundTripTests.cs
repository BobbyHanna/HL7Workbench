using System.Text;
using Xunit;

namespace HL7Workbench.Core.Tests;

public sealed class EncodingAndRoundTripTests
{
    public static IEnumerable<object[]> Encodings()
    {
        yield return [new UTF8Encoding(false, true)];
        yield return [new UTF8Encoding(true, true)];
        yield return [new UnicodeEncoding(false, true, true)];
        yield return [new UnicodeEncoding(true, true, true)];
        yield return [Encoding.Latin1];
        yield return [Encoding.ASCII];
    }

    [Theory]
    [MemberData(nameof(Encodings))]
    public void UnmodifiedBytesAndEncodingArePreserved(Encoding encoding)
    {
        var raw = SyntheticMessages.Header + "\r\nPID||\"\"|A^~B&\rZZZ|||\n\nABC!malformed";
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(raw)).ToArray();
        var message = Hl7Parser.Parse(bytes, new() { Encoding = encoding });
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
        Assert.Equal(bytes, message.OriginalBytes);
        Assert.Equal(encoding.CodePage, message.Encoding.CodePage);
        Assert.Equal(encoding.GetPreamble(), message.ByteOrderMark);
        Assert.False(message.IsModified);
    }

    [Theory]
    [MemberData(nameof(Encodings))]
    public void ModifiedBytesPreserveBomTerminatorsAndUntouchedContent(Encoding encoding)
    {
        var raw = SyntheticMessages.Message("\r\n") + "ZZZ|\\Zpreserve\\||\n";
        var original = encoding.GetPreamble().Concat(encoding.GetBytes(raw)).ToArray();
        var message = Hl7Parser.Parse(original, new() { Encoding = encoding });
        message.SetRaw("PID-5.2", "LongerName");
        message.SetRaw("MSH-10", "SYN-2");
        var expectedText = raw.Replace("Ada", "LongerName", StringComparison.Ordinal).Replace("SYN-0001", "SYN-2", StringComparison.Ordinal);
        var expected = encoding.GetPreamble().Concat(encoding.GetBytes(expectedText)).ToArray();
        Assert.Equal(expected, Hl7Serializer.Serialize(message));
        Assert.Equal(original, message.OriginalBytes);
        Assert.Equal("LongerName", Hl7Parser.Parse(expected, new() { Encoding = encoding }).Get("PID-5.2").Raw);
    }

    [Theory]
    [InlineData(65001, false)]
    [InlineData(1200, false)]
    [InlineData(1201, false)]
    [InlineData(28591, false)]
    [InlineData(65001, true)]
    public void NonAsciiAndSupplementaryCharactersRoundTripAndEdit(int codePage, bool bom)
    {
        Encoding encoding = codePage switch
        {
            65001 => new UTF8Encoding(bom, true),
            1200 => new UnicodeEncoding(false, true, true),
            1201 => new UnicodeEncoding(true, true, true),
            _ => Encoding.Latin1
        };
        var name = codePage == 28591 ? "Exémple" : "Exémple😀";
        var raw = SyntheticMessages.Header + "\rPID|" + name + "|old\r";
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(raw)).ToArray();
        var message = Hl7Parser.Parse(bytes, new() { Encoding = encoding });
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
        message.SetRaw("PID-2", "new");
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(raw.Replace("old", "new", StringComparison.Ordinal))).ToArray(),
            Hl7Serializer.Serialize(message));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF, 0xFE, 0, 0 }, 12000)]
    [InlineData(new byte[] { 0, 0, 0xFE, 0xFF }, 12001)]
    public void Utf32BomIsDetectedAndEditingIsExplicitlyUnsupported(byte[] bom, int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage);
        var bytes = bom.Concat(encoding.GetBytes(SyntheticMessages.Message())).ToArray();
        var message = Hl7Parser.Parse(bytes);
        Assert.Equal(codePage, message.Encoding.CodePage);
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
        Assert.False(message.CanEdit);
        Assert.Throws<InvalidOperationException>(() => message.SetRaw("PID-5.2", "new"));
    }

    [Theory]
    [InlineData(new byte[] { 0xFF })]
    [InlineData(new byte[] { 0xC3 })]
    [InlineData(new byte[] { 0xEF, 0xBB, 0xBF, 0xFF })]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x41 })]
    [InlineData(new byte[] { 0xFF, 0xFE, 0x00, 0xD8 })]
    public void InvalidBytesRemainRecoverableWithoutReplacementCharacters(byte[] bytes)
    {
        var message = Hl7Parser.Parse(bytes);
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
        Assert.Contains(message.Diagnostics, d => d.Code == "InvalidEncoding");
        Assert.Empty(message.Segments);
        Assert.False(message.HasDecodedText);
        Assert.Throws<InvalidOperationException>(() => message.RawText);
        Assert.False(message.CanEdit);
        Assert.Throws<InvalidOperationException>(() => Hl7Serializer.SerializeText(message));
        Assert.Throws<InvalidOperationException>(() => message.SetRaw("PID-1", "new"));
    }

    [Fact]
    public void UnrepresentableEditsFailAtomically()
    {
        var bytes = Encoding.ASCII.GetBytes("PID|old");
        var message = Hl7Parser.Parse(bytes, new() { Encoding = Encoding.ASCII });
        Assert.Throws<EncoderFallbackException>(() => message.SetRaw("PID-1", "é"));
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
        Assert.False(message.IsModified);
    }

    [Fact]
    public void ExplicitEncodingCannotConflictWithBom()
    {
        var bytes = new byte[] { 0xEF, 0xBB, 0xBF, 0x41 };
        Assert.Throws<ArgumentException>(() => Hl7Parser.Parse(bytes, new() { Encoding = Encoding.Latin1 }));
    }

    [Fact]
    public void ByteOwnershipAndEncodingCannotBeMutatedExternally()
    {
        var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes("PID|old")).ToArray();
        var expected = (byte[])bytes.Clone();
        var message = Hl7Parser.Parse(bytes);
        bytes[0] = 0;
        message.OriginalBytes![0] = 0;
        message.ByteOrderMark[0] = 0;
        var output = Hl7Serializer.Serialize(message);
        output[0] = 0;
        message.Encoding.EncoderFallback = EncoderFallback.ReplacementFallback;
        Assert.Equal(expected, Hl7Serializer.Serialize(message));
    }

    [Fact]
    public void TextInputHasNoClaimOfOriginalBytesOrImplicitBom()
    {
        var message = Hl7Parser.Parse("PID|é", new() { Encoding = new UTF8Encoding(true) });
        Assert.False(message.HasOriginalBytes);
        Assert.Null(message.OriginalBytes);
        Assert.Empty(message.ByteOrderMark);
        Assert.Equal(Encoding.UTF8.GetBytes("PID|é"), Hl7Serializer.Serialize(message));
    }

    [Fact]
    public void DeterministicDelimiterCorpusPreservesBytesBeforeAndAfterEdits()
    {
        var sets = new[] { "|^~\\&", "*$%!@", ":!%?$", ";%$!@" };
        var endings = new[] { "\r", "\n", "\r\n", "" };
        foreach (var set in sets)
        foreach (var ending in endings)
        for (var i = 0; i < 12; i++)
        {
            var raw = $"MSH{set[0]}{set[1..]}{set[0]}SYN\r" +
                $"PID{set[0]}{set[0]}ID{i}{set[1]}A{set[4]}B{set[2]}ALT{set[0]}{set[0]}" + ending;
            var bytes = Encoding.UTF8.GetBytes(raw);
            var message = Hl7Parser.Parse(bytes);
            Assert.Equal(bytes, Hl7Serializer.Serialize(message));
            Assert.Equal("B", message.Get("PID-2.2.2").Raw);
            message.SetRaw("PID-2[2].1", "NEW");
            Assert.Equal(Encoding.UTF8.GetBytes(raw.Replace("ALT", "NEW", StringComparison.Ordinal)), Hl7Serializer.Serialize(message));
        }
    }

    [Theory]
    [InlineData(65001)]
    [InlineData(1200)]
    [InlineData(1201)]
    public void MinimalByteSplicesNeverSplitSurrogatePairs(int codePage)
    {
        var encoding = codePage == 65001 ? (Encoding)new UTF8Encoding(true, true) :
            new UnicodeEncoding(codePage == 1201, true, true);
        const string raw = "PID|A😀B😀C|unchanged";
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(raw)).ToArray();
        var message = Hl7Parser.Parse(bytes);
        message.SetRaw("PID-1", "A😁B😁C");
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes("PID|A😁B😁C|unchanged")).ToArray(), Hl7Serializer.Serialize(message));
        message.SetRaw("PID-1", "A😁B");
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes("PID|A😁B|unchanged")).ToArray(), Hl7Serializer.Serialize(message));
    }

    [Fact]
    public void DeterministicMalformedCorpusAlwaysPreservesUnmodifiedBytes()
    {
        var random = new Random(20261013);
        const string alphabet = "ABC012|^~\\&\" \t\r\n!?";
        for (var example = 0; example < 300; example++)
        {
            var characters = new char[random.Next(0, 200)];
            for (var i = 0; i < characters.Length; i++) characters[i] = alphabet[random.Next(alphabet.Length)];
            var bytes = Encoding.UTF8.GetBytes(characters);
            var message = Hl7Parser.Parse(bytes);
            Assert.Equal(bytes, Hl7Serializer.Serialize(message));
        }
    }
}
