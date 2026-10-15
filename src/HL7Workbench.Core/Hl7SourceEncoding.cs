using System.Text;

namespace HL7Workbench.Core;

/// <summary>Shared strict BOM policy; MSH-18 never selects an encoding.</summary>
public static class Hl7SourceEncoding
{
    public static (Encoding Encoding, int BomLength) Detect(ReadOnlySpan<byte> bytes, Encoding? explicitEncoding = null)
    {
        Encoding encoding;
        var bomLength = 0;
        // An explicit Unicode encoding's matching preamble resolves ambiguous
        // prefixes before automatic longest-BOM detection.
        var explicitPreamble = explicitEncoding?.CodePage switch
        {
            65001 => new byte[] { 0xEF, 0xBB, 0xBF },
            1200 => new byte[] { 0xFF, 0xFE },
            1201 => new byte[] { 0xFE, 0xFF },
            12000 => new byte[] { 0xFF, 0xFE, 0, 0 },
            12001 => new byte[] { 0, 0, 0xFE, 0xFF },
            _ => Array.Empty<byte>()
        };
        if (explicitPreamble.Length > 0 && bytes.StartsWith(explicitPreamble))
        { encoding = explicitEncoding!; bomLength = explicitPreamble.Length; }
        else if (bytes.StartsWith(new byte[] { 0xFF, 0xFE, 0, 0 })) { encoding = new UTF32Encoding(false, true, true); bomLength = 4; }
        else if (bytes.StartsWith(new byte[] { 0, 0, 0xFE, 0xFF })) { encoding = new UTF32Encoding(true, true, true); bomLength = 4; }
        else if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) { encoding = new UTF8Encoding(true, true); bomLength = 3; }
        else if (bytes.StartsWith(new byte[] { 0xFF, 0xFE })) { encoding = new UnicodeEncoding(false, true, true); bomLength = 2; }
        else if (bytes.StartsWith(new byte[] { 0xFE, 0xFF })) { encoding = new UnicodeEncoding(true, true, true); bomLength = 2; }
        else encoding = explicitEncoding ?? new UTF8Encoding(false, true);
        encoding = Hl7ParseOptions.Strict(encoding);
        if (explicitEncoding is not null && bomLength > 0 && explicitEncoding.CodePage != encoding.CodePage)
            throw new ArgumentException("Explicit encoding conflicts with the byte-order mark.", nameof(explicitEncoding));
        return (encoding, bomLength);
    }
}
