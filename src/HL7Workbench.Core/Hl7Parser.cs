using System.Text;

namespace HL7Workbench.Core;

public static class Hl7Parser
{
    public static Hl7Message Parse(string text, Hl7ParseOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(text);
        options ??= new();
        options.Validate();
        Check(text.Length, options.MaxMessageCharacters, nameof(options.MaxMessageCharacters));
        var encoding = Hl7ParseOptions.Strict(options.Encoding ?? new UTF8Encoding(false, true));
        Check(encoding.GetByteCount(text), options.MaxMessageBytes, nameof(options.MaxMessageBytes));
        return ParseText(text, options, encoding, [], null, true);
    }

    public static Hl7Message Parse(ReadOnlySpan<byte> bytes, Hl7ParseOptions? options = null)
    {
        options ??= new();
        options.Validate();
        Check(bytes.Length, options.MaxMessageBytes, nameof(options.MaxMessageBytes));
        Encoding encoding;
        int bomLength;
        try { (encoding, bomLength) = Hl7SourceEncoding.Detect(bytes, options.Encoding); }
        catch (ArgumentException error) when (error.ParamName == "explicitEncoding")
        { throw new ArgumentException("Explicit encoding conflicts with the byte-order mark.", nameof(options)); }
        var original = bytes.ToArray();
        var bom = bytes[..bomLength].ToArray();
        try
        {
            var content = bytes[bomLength..];
            Check(encoding.GetCharCount(content), options.MaxMessageCharacters, nameof(options.MaxMessageCharacters));
            var text = encoding.GetString(content);
            return ParseText(text, options, encoding, bom, original, true);
        }
        catch (DecoderFallbackException)
        {
            return new Hl7Message("", options, encoding, bom, original, [], Hl7Delimiters.Default,
                [new("InvalidEncoding", Hl7DiagnosticSeverity.Error,
                    "Input cannot be decoded with the selected encoding; original bytes remain available.")], false);
        }
    }

    internal static Hl7Message ParseText(string text, Hl7ParseOptions options, Encoding encoding,
        byte[] bom, byte[]? bytes, bool decoded)
    {
        var structure = Hl7StructureScanner.Scan(new StringHl7TextSource(text), encoding, new()
        {
            MaxSegmentLength = options.MaxSegmentLength, MaxSegments = options.MaxSegments,
            MaxElements = options.MaxElements, MaxElementsPerContainer = options.MaxElementsPerContainer,
            MaxDiagnostics = options.MaxDiagnostics, MaxEscapeTokenLength = long.MaxValue, MaxEscapeTokens = int.MaxValue
        });
        string Raw(StructureNode node) => text.Substring(checked((int)node.Range.Offset), checked((int)node.Range.Length));
        var segments = structure.Segments.Select(segment => new Hl7Segment(
            text.Substring(checked((int)segment.Range.Offset), checked((int)segment.Range.Length)),
            checked((int)segment.Range.Offset), segment.Identifier, segment.Terminator,
            segment.Fields.Select(f => new Hl7Field(Raw(f), checked((int)f.Range.Offset), f.Number,
                f.Children.Select(r => new Hl7Repetition(Raw(r), checked((int)r.Range.Offset),
                    r.Children.Select(c => new Hl7Component(Raw(c), checked((int)c.Range.Offset),
                        c.Children.Select(s => new Hl7Subcomponent(Raw(s), checked((int)s.Range.Offset))).ToList())).ToList())).ToList())).ToList(),
            segment.CanEdit)).ToList();
        var diagnostics = structure.Diagnostics.Select(d => new Hl7Diagnostic(d.Code, d.Severity, d.Description,
            d.CharacterOffset is { } offset ? checked((int)offset) : null, d.Path)).ToList();
        return new(text, options, encoding, bom, bytes, segments, structure.Delimiters, diagnostics, decoded && structure.CanEdit);
    }

    internal static void Check(long value, int limit, string name)
    {
        if (value > limit) throw new Hl7LimitException(name);
    }
}
