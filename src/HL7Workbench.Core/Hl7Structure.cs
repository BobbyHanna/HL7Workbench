using System.Text;

namespace HL7Workbench.Core;

/// <summary>Half-open decoded UTF-16 coordinates local to one message, excluding its BOM.</summary>
public readonly record struct CharacterRange(long Offset, long Length)
{
    public long End => checked(Offset + Length);
    public bool Contains(long position) => position >= Offset && position < End;
}

/// <summary>Half-open byte coordinates. The owning source defines the origin.</summary>
public readonly record struct ByteRange(long Offset, long Length)
{
    public long End => checked(Offset + Length);
}

/// <summary>Bounded random access supplied by a caller. Core never opens or owns files.</summary>
public interface IHl7TextSource
{
    long Length { get; }
    char this[long offset] { get; }
    string Read(CharacterRange range);
    long IndexOfAny(ReadOnlySpan<char> characters, long start, long length);
    ByteRange? Bytes(CharacterRange range);
}

public sealed class StringHl7TextSource(string text) : IHl7TextSource
{
    public long Length => text.Length;
    public char this[long offset] => text[checked((int)offset)];
    public string Read(CharacterRange range) => text.Substring(checked((int)range.Offset), checked((int)range.Length));
    public long IndexOfAny(ReadOnlySpan<char> characters, long start, long length)
    {
        var found = text.AsSpan(checked((int)start), checked((int)length)).IndexOfAny(characters);
        return found < 0 ? -1 : start + found;
    }
    public ByteRange? Bytes(CharacterRange range) => null;
}

public enum StructureKind { Field, Repetition, Component, Subcomponent }

/// <summary>Immutable structural coordinates; content is obtained through explicit bounded source reads.</summary>
public sealed record StructureNode(StructureKind Kind, int Number, CharacterRange Range,
    ByteRange? ByteRange, Hl7ValueState State, IReadOnlyList<StructureNode> Children, string Preview = "");

public sealed record StructureSegment(string Identifier, CharacterRange Range, ByteRange? ByteRange,
    string Terminator, IReadOnlyList<StructureNode> Fields, bool CanEdit);

public sealed record StructureDiagnostic(string Code, Hl7DiagnosticSeverity Severity, string Description,
    long? CharacterOffset = null, string? Path = null);

public sealed record Hl7Structure(long TextLength, Hl7Delimiters Delimiters,
    IReadOnlyList<StructureSegment> Segments, IReadOnlyList<StructureDiagnostic> Diagnostics, bool CanEdit)
{
    /// <summary>Message-level header/delimiter consistency, independent of edit encoding support.
    /// Individual segment CanEdit flags still govern their structural ambiguity. Never infer this from capped diagnostics.</summary>
    public bool HasUnambiguousMessageStructure { get; init; } = CanEdit;
}

/// <summary>Inspection budgets bound structure, not attachment lengths. Legacy parsing supplies its existing limits.</summary>
public sealed record StructureScanOptions
{
    public long MaxSegmentLength { get; init; } = long.MaxValue;
    public int MaxSegments { get; init; } = 100_000;
    public int MaxElements { get; init; } = 250_000;
    public int MaxElementsPerContainer { get; init; } = 10_000;
    public int MaxDiagnostics { get; init; } = 1024;
    public long MaxEscapeTokenLength { get; init; } = 1024 * 1024;
    public int MaxEscapeTokens { get; init; } = 100_000;
}

/// <summary>Shared ER7 grammar for materialized messages and source-backed inspection.</summary>
public static class Hl7StructureScanner
{
    internal static long ClosingEscape(IHl7TextSource source, char escape, long open, long end) =>
        source.IndexOfAny([escape], checked(open + 1), checked(end - open - 1));
    public static Hl7Structure Scan(IHl7TextSource source, Encoding encoding,
        StructureScanOptions? options = null, CancellationToken cancellationToken = default,
        IProgress<long>? progress = null)
    {
        options ??= new();
        if (options.MaxSegmentLength < 1 || options.MaxSegments < 1 || options.MaxElements < 1 ||
            options.MaxElementsPerContainer < 1 || options.MaxDiagnostics < 1 || options.MaxEscapeTokenLength < 1 || options.MaxEscapeTokens < 1)
            throw new ArgumentOutOfRangeException(nameof(options));
        return new Scanner(source, Hl7ParseOptions.Strict(encoding), options, cancellationToken, progress).Run();
    }

    // Pairing deliberately looks ahead only inside the supplied range. A final unmatched
    // escape is literal, so separators after it remain structural, as in the accepted parser.
    internal static IEnumerable<CharacterRange> Split(IHl7TextSource source, CharacterRange range,
        char? separator, char? escape, CancellationToken cancellationToken = default)
    {
        if (separator is null) { yield return range; yield break; }
        var characters = escape is { } e ? new[] { separator.Value, e } : [separator.Value];
        var start = range.Offset;
        var position = start;
        while (position < range.End)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var found = source.IndexOfAny(characters, position, range.End - position);
            if (found < 0) break;
            if (source[found] == escape)
            {
                var closing = ClosingEscape(source, escape!.Value, found, range.End);
                if (closing >= 0) { position = closing + 1; continue; }
            }
            if (source[found] == separator)
            {
                yield return new(start, found - start);
                start = found + 1;
            }
            position = found + 1;
        }
        yield return new(start, range.End - start);
    }

    private sealed class Scanner(IHl7TextSource source, Encoding encoding, StructureScanOptions options,
        CancellationToken token, IProgress<long>? progress)
    {
        private readonly List<StructureDiagnostic> diagnostics = [];
        private int elements;
        private int escapeTokens;
        private bool suppressed;
        private Hl7Delimiters delimiters = Hl7Delimiters.Default;
        private void Limit(long value, long maximum, string name) { if (value > maximum) throw new Hl7LimitException(name); }
        private void Add(string code, Hl7DiagnosticSeverity severity, string description, long? offset = null)
        {
            if (suppressed) return;
            if (diagnostics.Count < options.MaxDiagnostics) diagnostics.Add(new(code, severity, description, offset));
            else
            {
                diagnostics[^1] = new("DiagnosticsSuppressed", Hl7DiagnosticSeverity.Warning,
                    "Additional diagnostics were suppressed by the configured storage limit.");
                suppressed = true;
            }
        }
        public Hl7Structure Run()
        {
            var lines = new List<(CharacterRange Range, string Terminator)>();
            for (long start = 0; start < source.Length;)
            {
                token.ThrowIfCancellationRequested();
                var end = source.IndexOfAny(['\r', '\n'], start, source.Length - start);
                if (end < 0) end = source.Length;
                Limit(end - start, options.MaxSegmentLength, nameof(Hl7ParseOptions.MaxSegmentLength));
                Limit(lines.Count + 1, options.MaxSegments, nameof(Hl7ParseOptions.MaxSegments));
                var next = end;
                if (next < source.Length)
                {
                    next++;
                    if (source[end] == '\r' && next < source.Length && source[next] == '\n') next++;
                }
                lines.Add((new(start, end - start), source.Read(new(end, next - end))));
                start = next;
            }
            var headers = lines.Where(l => l.Range.Length >= 3 && source.Read(new(l.Range.Offset, 3)) == "MSH").ToArray();
            var safe = true;
            if (headers.Length == 0)
                Add("MissingMsh", Hl7DiagnosticSeverity.Warning, "MSH is missing; standard delimiters are assumed.");
            else
            {
                var header = headers[0].Range;
                if (header.Length >= 4)
                {
                    var field = source[header.Offset + 3];
                    var end = source.IndexOfAny([field], header.Offset + 4, header.Length - 4);
                    if (end < 0) end = header.End;
                    var count = end - header.Offset - 4;
                    if (count is >= 2 and <= 5)
                    {
                        var chars = source.Read(new(header.Offset + 4, count));
                        delimiters = new(field, chars[0], chars[1], count >= 3 ? chars[2] : null,
                            count >= 4 ? chars[3] : null, count == 5 ? chars[4] : null);
                    }
                    else safe = false;
                }
                else safe = false;
                if (!delimiters.IsValid) safe = false;
                if (!safe)
                {
                    delimiters = Hl7Delimiters.Default;
                    Add("InvalidDelimiters", Hl7DiagnosticSeverity.Error,
                        "MSH delimiter definition is invalid; fallback structure is provisional and editing is disabled.", header.Offset);
                }
                if (headers.Length > 1)
                {
                    safe = false;
                    Add("MultipleMessages", Hl7DiagnosticSeverity.Error,
                        "Multiple MSH segments detected; parse one message at a time. Editing is disabled.");
                }
                if (headers[0].Range.Offset != lines[0].Range.Offset)
                    Add("MshNotFirst", Hl7DiagnosticSeverity.Warning, "MSH is not the first segment.");
            }
            var segments = new List<StructureSegment>();
            foreach (var (range, terminator) in lines)
            {
                token.ThrowIfCancellationRequested();
                var id = source.Read(new(range.Offset, Math.Min(3, range.Length)));
                var structured = id.Length == 3 && id.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9') &&
                    (range.Length == 3 || range.Length > 3 && source[range.Offset + 3] == delimiters.Field);
                var fields = new List<StructureNode>();
                if (!structured)
                    Add("MalformedSegment", Hl7DiagnosticSeverity.Warning,
                        "Segment has an invalid identifier or field separator and is retained as opaque content.", range.Offset);
                else if (range.Length > 3)
                {
                    if (id == "MSH")
                    {
                        fields.Add(Node(StructureKind.Field, 1, new(range.Offset + 3, 1), true));
                        var end = source.IndexOfAny([delimiters.Field], range.Offset + 4, range.Length - 4);
                        if (end < 0) end = range.End;
                        fields.Add(Node(StructureKind.Field, 2, new(range.Offset + 4, end - range.Offset - 4), true));
                        if (end < range.End) Fields(new(end + 1, range.End - end - 1), fields, 3);
                    }
                    else Fields(new(range.Offset + 4, range.Length - 4), fields, 1);
                }
                var payload = id == "MSH" ? (fields.Count > 2 ? fields[2].Range.Offset : range.End) : Math.Min(range.Offset + 4, range.End);
                var ambiguous = structured && Ambiguous(new(payload, range.End - payload));
                if (ambiguous) Add("AmbiguousEscape", Hl7DiagnosticSeverity.Error,
                    "An escape spans structural delimiters; coordinates are provisional and this segment cannot be edited.", range.Offset);
                segments.Add(new(id, range, source.Bytes(range), terminator, fields.AsReadOnly(), structured && !ambiguous));
                foreach (var field in fields.Where(f => id != "MSH" || f.Number > 2)) EscapeDiagnostics(field.Range);
                progress?.Report(range.End);
            }
            if (!encoding.IsSingleByte && encoding.CodePage is not 65001 and not 1200 and not 1201)
                Add("UnsupportedEditEncoding", Hl7DiagnosticSeverity.Information,
                    "Selected encoding supports inspection and original-byte serialization, but not editing.");
            return new(source.Length, delimiters, segments.AsReadOnly(), diagnostics.AsReadOnly(),
                safe && (encoding.IsSingleByte || encoding.CodePage is 65001 or 1200 or 1201)) { HasUnambiguousMessageStructure = safe };
        }
        private void Fields(CharacterRange range, List<StructureNode> fields, int first)
        {
            foreach (var slice in Split(source, range, delimiters.Field, delimiters.Escape, token))
            {
                Limit(fields.Count + 1, options.MaxElementsPerContainer, nameof(Hl7ParseOptions.MaxElementsPerContainer));
                fields.Add(Node(StructureKind.Field, first++, slice, false));
            }
        }
        private StructureNode Node(StructureKind kind, int number, CharacterRange range, bool literal)
        {
            token.ThrowIfCancellationRequested();
            Limit(++elements, options.MaxElements, nameof(Hl7ParseOptions.MaxElements));
            Limit(number, options.MaxElementsPerContainer, nameof(Hl7ParseOptions.MaxElementsPerContainer));
            var children = new List<StructureNode>();
            if (kind != StructureKind.Subcomponent)
            {
                var separator = kind switch { StructureKind.Field => delimiters.Repetition,
                    StructureKind.Repetition => delimiters.Component, _ => delimiters.Subcomponent };
                var slices = literal ? new[] { range } : Split(source, range, separator, delimiters.Escape, token);
                foreach (var slice in slices) children.Add(Node(kind + 1, children.Count + 1, slice, literal));
            }
            var state = range.Length == 0 ? Hl7ValueState.Empty : range.Length == 2 &&
                source[range.Offset] == '"' && source[range.Offset + 1] == '"' ? Hl7ValueState.ExplicitNull : Hl7ValueState.Value;
            var previewLength = Math.Min(100, range.Length);
            if (previewLength > 0 && previewLength < range.Length && char.IsHighSurrogate(source[range.Offset + previewLength - 1]) &&
                char.IsLowSurrogate(source[range.Offset + previewLength])) previewLength--;
            return new(kind, number, range, source.Bytes(range), state, children.AsReadOnly(), source.Read(new(range.Offset, previewLength)));
        }
        private bool Ambiguous(CharacterRange range)
        {
            if (delimiters.Escape is not { } escape) return false;
            var position = range.Offset;
            var structural = new List<char> { delimiters.Field, delimiters.Repetition, delimiters.Component };
            if (delimiters.Subcomponent is { } sub) structural.Add(sub);
            while (position < range.End)
            {
                var open = source.IndexOfAny([escape], position, range.End - position);
                if (open < 0) break;
                var close = ClosingEscape(source, escape, open, range.End);
                if (close < 0) break;
                if (source.IndexOfAny(structural.ToArray(), open + 1, close - open - 1) >= 0) return true;
                position = close + 1;
            }
            return false;
        }
        private void EscapeDiagnostics(CharacterRange range)
        {
            if (delimiters.Escape is not { } escape) return;
            long position = range.Offset;
            while (position < range.End)
            {
                token.ThrowIfCancellationRequested();
                var open = source.IndexOfAny([escape], position, range.End - position);
                if (open < 0) break;
                var close = ClosingEscape(source, escape, open, range.End);
                if (close < 0)
                {
                    Add("UnterminatedEscape", Hl7DiagnosticSeverity.Warning, "Escape sequence has no closing delimiter.", open);
                    break;
                }
                Limit(++escapeTokens, options.MaxEscapeTokens, nameof(StructureScanOptions.MaxEscapeTokens));
                Limit(close - open - 1, options.MaxEscapeTokenLength, nameof(StructureScanOptions.MaxEscapeTokenLength));
                var count = close - open - 1;
                var code = count == 1 ? source[open + 1] : '\0';
                var supported = code is 'F' or 'S' or 'R' or 'E' || code == 'T' && delimiters.Subcomponent is not null ||
                    code == 'P' && delimiters.Truncation is not null;
                if (!supported)
                {
                    if (count > 0 && source[open + 1] == 'X')
                    {
                        if (!ValidHex(new(open + 2, count - 1))) Add("InvalidHexEscape", Hl7DiagnosticSeverity.Warning,
                            "Hex escape cannot be decoded using the selected encoding.", open);
                    }
                    else Add("UnsupportedEscape", Hl7DiagnosticSeverity.Warning, "Escape is unsupported and retained verbatim.", open);
                }
                position = close + 1;
            }
        }
        private bool ValidHex(CharacterRange range)
        {
            if (range.Length == 0 || range.Length % 2 != 0) return false;
            var decoder = encoding.GetDecoder();
            var bytes = new byte[4096];
            var chars = new char[8192];
            try
            {
                for (long position = range.Offset; position < range.End;)
                {
                    token.ThrowIfCancellationRequested();
                    var count = (int)Math.Min(bytes.Length, (range.End - position) / 2);
                    for (var i = 0; i < count; i++)
                    {
                        static int Digit(char c) => c is >= '0' and <= '9' ? c - '0' : c is >= 'a' and <= 'f' ? c - 'a' + 10 : c is >= 'A' and <= 'F' ? c - 'A' + 10 : -1;
                        var high = Digit(source[position++]); var low = Digit(source[position++]);
                        if (high < 0 || low < 0) return false;
                        bytes[i] = (byte)(high * 16 + low);
                    }
                    decoder.Convert(bytes.AsSpan(0, count), chars, position == range.End, out _, out _, out _);
                }
                return true;
            }
            catch (DecoderFallbackException) { return false; }
        }
    }
}
