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
        var bomLength = 0;
        // An explicit Unicode encoding's matching preamble resolves ambiguous
        // prefixes before automatic longest-BOM detection.
        var explicitPreamble = options.Encoding?.CodePage switch
        {
            65001 => new byte[] { 0xEF, 0xBB, 0xBF },
            1200 => new byte[] { 0xFF, 0xFE },
            1201 => new byte[] { 0xFE, 0xFF },
            12000 => new byte[] { 0xFF, 0xFE, 0, 0 },
            12001 => new byte[] { 0, 0, 0xFE, 0xFF },
            _ => Array.Empty<byte>()
        };
        if (explicitPreamble.Length > 0 && bytes.StartsWith(explicitPreamble))
        { encoding = options.Encoding!; bomLength = explicitPreamble.Length; }
        else if (bytes.StartsWith(new byte[] { 0xFF, 0xFE, 0, 0 })) { encoding = new UTF32Encoding(false, true, true); bomLength = 4; }
        else if (bytes.StartsWith(new byte[] { 0, 0, 0xFE, 0xFF })) { encoding = new UTF32Encoding(true, true, true); bomLength = 4; }
        else if (bytes.StartsWith(new byte[] { 0xEF, 0xBB, 0xBF })) { encoding = new UTF8Encoding(true, true); bomLength = 3; }
        else if (bytes.StartsWith(new byte[] { 0xFF, 0xFE })) { encoding = new UnicodeEncoding(false, true, true); bomLength = 2; }
        else if (bytes.StartsWith(new byte[] { 0xFE, 0xFF })) { encoding = new UnicodeEncoding(true, true, true); bomLength = 2; }
        else encoding = options.Encoding ?? new UTF8Encoding(false, true);
        encoding = Hl7ParseOptions.Strict(encoding);
        if (options.Encoding is not null && bomLength > 0 && options.Encoding.CodePage != encoding.CodePage)
            throw new ArgumentException("Explicit encoding conflicts with the byte-order mark.", nameof(options));
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
        var lines = new List<(int Start, int Length, string Terminator)>();
        for (var start = 0; start < text.Length;)
        {
            var end = start;
            while (end < text.Length && text[end] is not '\r' and not '\n')
            {
                end++;
                Check(end - start, options.MaxSegmentLength, nameof(options.MaxSegmentLength));
            }
            Check(lines.Count + 1, options.MaxSegments, nameof(options.MaxSegments));
            var next = end;
            if (next < text.Length)
            {
                next++;
                if (text[end] == '\r' && next < text.Length && text[next] == '\n') next++;
            }
            lines.Add((start, end - start, text[end..next]));
            start = next;
        }

        var diagnostics = new Hl7DiagnosticCollector(options.MaxDiagnostics);
        var delimiters = Hl7Delimiters.Default;
        var safe = true;
        var headers = lines.Where(line => line.Length >= 3 && text.AsSpan(line.Start, 3).SequenceEqual("MSH")).ToList();
        if (headers.Count == 0)
            diagnostics.Add(new("MissingMsh", Hl7DiagnosticSeverity.Warning, "MSH is missing; standard delimiters are assumed."));
        else
        {
            var header = headers[0];
            var raw = text.Substring(header.Start, header.Length);
            if (raw.Length >= 4)
            {
                var fieldSeparator = raw[3];
                var end = raw.IndexOf(fieldSeparator, 4);
                if (end < 0) end = raw.Length;
                var count = end - 4;
                if (count is >= 2 and <= 5)
                    delimiters = new(fieldSeparator, raw[4], raw[5], count >= 3 ? raw[6] : null,
                        count >= 4 ? raw[7] : null, count == 5 ? raw[8] : null);
                else safe = false;
            }
            else safe = false;
            if (!delimiters.IsValid) safe = false;
            if (!safe)
            {
                delimiters = Hl7Delimiters.Default;
                diagnostics.Add(new("InvalidDelimiters", Hl7DiagnosticSeverity.Error,
                    "MSH delimiter definition is invalid; fallback structure is provisional and editing is disabled.", header.Start));
            }
            if (headers.Count > 1)
            {
                safe = false;
                diagnostics.Add(new("MultipleMessages", Hl7DiagnosticSeverity.Error,
                    "Multiple MSH segments detected; parse one message at a time. Editing is disabled."));
            }
            if (lines.Count > 0 && headers[0].Start != lines[0].Start)
                diagnostics.Add(new("MshNotFirst", Hl7DiagnosticSeverity.Warning, "MSH is not the first segment."));
        }

        var builder = new ModelBuilder(options, delimiters);
        var segments = new List<Hl7Segment>();
        foreach (var line in lines)
        {
            var raw = text.Substring(line.Start, line.Length);
            var identifier = raw[..Math.Min(3, raw.Length)];
            var validId = identifier.Length == 3 && identifier.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9');
            var structured = validId && (raw.Length == 3 || raw.Length > 3 && raw[3] == delimiters.Field);
            var fields = new List<Hl7Field>();
            if (!structured)
                diagnostics.Add(new("MalformedSegment", Hl7DiagnosticSeverity.Warning,
                    "Segment has an invalid identifier or field separator and is retained as opaque content.", line.Start));
            else if (raw.Length > 3)
            {
                if (identifier == "MSH")
                {
                    fields.Add(builder.Field(raw.Substring(3, 1), line.Start + 3, 1, true));
                    var msh2End = raw.IndexOf(delimiters.Field, 4);
                    if (msh2End < 0) msh2End = raw.Length;
                    fields.Add(builder.Field(raw[4..msh2End], line.Start + 4, 2, true));
                    if (msh2End < raw.Length)
                        builder.Fields(raw[(msh2End + 1)..], line.Start + msh2End + 1, fields, 3);
                }
                else builder.Fields(raw[4..], line.Start + 4, fields, 1);
            }
            var payloadStart = identifier == "MSH" ? (fields.Count > 2 ? fields[2].Start - line.Start : raw.Length) : Math.Min(4, raw.Length);
            var ambiguous = structured && Hl7Tokenizer.HasAmbiguousEscape(raw[payloadStart..], delimiters);
            if (ambiguous)
                diagnostics.Add(new("AmbiguousEscape", Hl7DiagnosticSeverity.Error,
                    "An escape spans structural delimiters; coordinates are provisional and this segment cannot be edited.", line.Start));
            segments.Add(new(raw, line.Start, identifier, line.Terminator, fields, structured && !ambiguous));
            foreach (var field in fields.Where(f => identifier != "MSH" || f.Number > 2))
            {
                // Inspect once per raw field; nested nodes retain escape spelling unchanged.
                var escapeDiagnostics = Hl7EscapeCodec.Decode(field.Raw, delimiters, encoding,
                    options.MaxMessageCharacters, options.MaxDiagnostics).Diagnostics;
                diagnostics.AddRange(escapeDiagnostics.Select(d => d with { CharacterOffset = field.Start + (d.CharacterOffset ?? 0) }));
            }
        }
        if (!encoding.IsSingleByte && encoding.CodePage is not 65001 and not 1200 and not 1201)
        {
            diagnostics.Add(new("UnsupportedEditEncoding", Hl7DiagnosticSeverity.Information,
                "Selected encoding supports inspection and original-byte serialization, but not editing."));
        }
        return new(text, options, encoding, bom, bytes, segments, delimiters, diagnostics.ToList(), decoded && safe);
    }

    internal static void Check(long value, int limit, string name)
    {
        if (value > limit) throw new Hl7LimitException(name);
    }

    private sealed class ModelBuilder(Hl7ParseOptions options, Hl7Delimiters delimiters)
    {
        private int count;
        private void Add(int siblings)
        {
            Check(++count, options.MaxElements, nameof(options.MaxElements));
            Check(siblings, options.MaxElementsPerContainer, nameof(options.MaxElementsPerContainer));
        }
        internal void Fields(string raw, int start, List<Hl7Field> fields, int first)
        {
            foreach (var slice in Hl7Tokenizer.Split(raw, delimiters.Field, delimiters.Escape))
            {
                Check(fields.Count + 1, options.MaxElementsPerContainer, nameof(options.MaxElementsPerContainer));
                fields.Add(Field(raw.Substring(slice.Start, slice.Length), start + slice.Start, first++, false));
            }
        }
        internal Hl7Field Field(string raw, int start, int number, bool literal)
        {
            Add(number);
            var repetitions = new List<Hl7Repetition>();
            foreach (var r in Slices(raw, delimiters.Repetition, literal))
            {
                Add(repetitions.Count + 1);
                var repetitionRaw = raw.Substring(r.Start, r.Length);
                var components = new List<Hl7Component>();
                foreach (var c in Slices(repetitionRaw, delimiters.Component, literal))
                {
                    Add(components.Count + 1);
                    var componentRaw = repetitionRaw.Substring(c.Start, c.Length);
                    var subcomponents = new List<Hl7Subcomponent>();
                    foreach (var s in Slices(componentRaw, delimiters.Subcomponent, literal))
                    {
                        Add(subcomponents.Count + 1);
                        subcomponents.Add(new(componentRaw.Substring(s.Start, s.Length), start + r.Start + c.Start + s.Start));
                    }
                    components.Add(new(componentRaw, start + r.Start + c.Start, subcomponents));
                }
                repetitions.Add(new(repetitionRaw, start + r.Start, components));
            }
            return new(raw, start, number, repetitions);
        }
        private IEnumerable<Hl7Slice> Slices(string raw, char? delimiter, bool literal) =>
            literal ? [new Hl7Slice(0, raw.Length)] : Hl7Tokenizer.Split(raw, delimiter, delimiters.Escape);
    }
}
