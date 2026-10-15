using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed record SourceRegion(ByteRange Range, int? MessageNumber, string Reason);
public sealed record IndexedMessage(int Number, ByteRange SourceRange, ByteRange PayloadRange,
    bool Framed, IReadOnlyList<Hl7Diagnostic> Diagnostics);

/// <summary>A complete byte partition. File-level BOM belongs to an unclassified metadata region.</summary>
public sealed class MessageFileIndex
{
    private readonly Encoding encoding;
    public Encoding Encoding => (Encoding)encoding.Clone();
    public int BomLength { get; }
    public IReadOnlyList<SourceRegion> Regions { get; }
    public IReadOnlyList<IndexedMessage> Messages { get; }
    public IReadOnlyList<Hl7Diagnostic> Diagnostics { get; }
    private MessageFileIndex(Encoding encoding, int bomLength, IReadOnlyList<SourceRegion> regions,
        IReadOnlyList<IndexedMessage> messages, IReadOnlyList<Hl7Diagnostic> diagnostics)
    { this.encoding = (Encoding)encoding.Clone(); BomLength = bomLength; Regions = regions; Messages = messages; Diagnostics = diagnostics; }
    public const int MaxIndexedMessages = 100_000;
    public const int MaxSourceRegions = 200_000;
    public static MessageFileIndex Build(SourceSnapshot snapshot, CancellationToken token = default,
        IProgress<long>? progress = null, Encoding? inheritedEncoding = null)
    {
        var prefix = snapshot.ReadBytes(new(0, Math.Min(4, snapshot.Length)));
        var (encoding, bom) = Hl7SourceEncoding.Detect(prefix, inheritedEncoding);
        var reader = new IndexBytes(snapshot, token, progress);
        var width = encoding.CodePage is 1200 or 1201 ? 2 : encoding.CodePage is 12000 or 12001 ? 4 : 1;
        var bigEndian = encoding.CodePage is 1201 or 12001;
        var messages = new List<IndexedMessage>();
        var regions = new List<SourceRegion>();
        var fileDiagnostics = new List<Hl7Diagnostic>();
        long accounted = 0, position = bom;
        long? messageStart = null;
        long payloadStart = 0;
        var framed = false;
        var lineStart = true;
        var boundary = new List<Hl7Diagnostic>();
        static void Diagnostic(List<Hl7Diagnostic> target, Hl7Diagnostic diagnostic)
        {
            if (target.Count < 1024) target.Add(diagnostic);
            else target[^1] = new("DiagnosticsSuppressed", Hl7DiagnosticSeverity.Warning, "Additional boundary/index diagnostics were suppressed.");
        }
        void Gap(long end, string reason)
        {
            if (end > accounted) { regions.Add(new(new(accounted, end - accounted), null, reason)); accounted = end; }
        }
        void Finish(long sourceEnd, long payloadEnd)
        {
            if (messageStart is not { } start) return;
            Gap(start, "Unclassified source content");
            if (messages.Count >= MaxIndexedMessages) throw new Hl7LimitException("IndexedMessages");
            var number = messages.Count + 1;
            messages.Add(new(number, new(start, sourceEnd - start), new(payloadStart, Math.Max(0, payloadEnd - payloadStart)), framed, boundary.ToArray()));
            regions.Add(new(new(start, sourceEnd - start), number, framed ? "MLLP message" : "HL7 message"));
            accounted = sourceEnd;
            messageStart = null; framed = false; boundary.Clear();
        }
        int Unit(long offset)
        {
            if (offset + width > snapshot.Length) return -1;
            if (width == 1) return reader[offset];
            uint value = 0;
            for (var i = 0; i < width; i++) value |= (uint)reader[offset + i] << (8 * (bigEndian ? width - i - 1 : i));
            return value <= int.MaxValue ? (int)value : -1;
        }
        bool Starts(long offset, string value)
        {
            for (var i = 0; i < value.Length; i++) if (Unit(offset + i * width) != value[i]) return false;
            return true;
        }
        if (bom > 0) Gap(bom, "File encoding BOM");
        while (position < snapshot.Length)
        {
            if (width == 1 && !lineStart && reader[position] is not 0x0D and not 0x0A and not 0x0B and not 0x1C and not 0xEF and not 0xFE and not 0xFF)
            {
                position = reader.FindSpecial(position);
                if (position >= snapshot.Length) break;
            }
            if (regions.Count > MaxSourceRegions) throw new Hl7LimitException("SourceRegions");
            // Raw transport octets are recognized before encoded code units. For wide
            // encodings an in-value terminator is inherently ambiguous and is reported.
            if (reader[position] == 0x0B && (lineStart || framed) &&
                (width == 1 || Starts(position + 1, "MSH") || Starts(position + 1, "PID") ||
                 position + 2 < snapshot.Length && reader[position + 1] == 0x1C && reader[position + 2] == 0x0D))
            {
                if (messageStart is not null)
                {
                    if (framed) Diagnostic(boundary, new("IncompleteMllp", Hl7DiagnosticSeverity.Warning, "MLLP start occurred before the previous envelope ended."));
                    Finish(position, position);
                }
                Gap(position, "Unclassified source content");
                messageStart = position; payloadStart = position + 1; framed = true;
                position++; lineStart = true;
                continue;
            }
            if (reader[position] == 0x1C && position + 1 < snapshot.Length && reader[position + 1] == 0x0D && (width == 1 || framed || lineStart))
            {
                if (framed)
                {
                    if (width > 1 && !lineStart) Diagnostic(boundary, new("AmbiguousFraming", Hl7DiagnosticSeverity.Warning,
                        "Raw MLLP ending takes precedence over a potentially matching wide-encoding code unit."));
                    Finish(position + 2, position);
                }
                else
                {
                    Finish(position, position);
                    Gap(position + 2, "Unexpected MLLP ending");
                }
                position += 2; lineStart = true;
                continue;
            }
            if (lineStart)
            {
                if (Starts(position, "MSH") && !framed)
                {
                    Finish(position, position);
                    Gap(position, "Leading source content");
                    messageStart = payloadStart = position;
                }
                else if (!framed && (Starts(position, "FHS") || Starts(position, "BHS") || Starts(position, "BTS") || Starts(position, "FTS")))
                {
                    Finish(position, position);
                    Gap(position, "Unclassified source content");
                    var end = position;
                    while (end < snapshot.Length && Unit(end) is not '\r' and not '\n') end += width;
                    if (end < snapshot.Length)
                    {
                        var cr = Unit(end) == '\r'; end += width;
                        if (cr && Unit(end) == '\n') end += width;
                    }
                    Gap(Math.Min(end, snapshot.Length), "HL7 batch wrapper (not interpreted)");
                    position = end; lineStart = true;
                    continue;
                }
            }
            var unit = Unit(position);
            if (unit == 0xFEFF || width == 1 && reader[position] == 0xEF && position + 2 < snapshot.Length &&
                reader[position + 1] == 0xBB && reader[position + 2] == 0xBF ||
                position + 1 < snapshot.Length && (reader[position] == 0xFF && reader[position + 1] == 0xFE || reader[position] == 0xFE && reader[position + 1] == 0xFF))
            {
                var diagnostic = new Hl7Diagnostic("InteriorBom", Hl7DiagnosticSeverity.Warning,
                    "Unexpected interior BOM is retained under the inherited file encoding.");
                Diagnostic(messageStart is not null ? boundary : fileDiagnostics, diagnostic);
            }
            lineStart = unit is '\r' or '\n';
            position += width;
            if ((position & 65535) < width) progress?.Report(position);
        }
        if (framed) Diagnostic(boundary, new("IncompleteMllp", Hl7DiagnosticSeverity.Warning, "MLLP envelope has no ending; payload is retained to end of file."));
        Finish(snapshot.Length, snapshot.Length);
        Gap(snapshot.Length, "Unclassified source content");
        // A provisional region is eligible if any actual line resembles a Core segment.
        // Leading malformed lines remain opaque message content; batch wrappers retain
        // their separate byte ownership and are never interpreted as messages.
        if (messages.Count == 0)
        {
            var rebuilt = new List<SourceRegion>();
            foreach (var region in regions)
            {
                var plausible = false;
                if (region.Reason != "File encoding BOM" && !region.Reason.StartsWith("HL7 batch wrapper", StringComparison.Ordinal))
                {
                    for (var cursor = region.Range.Offset; cursor < region.Range.End;)
                    {
                        var valid = cursor + 3 * width <= region.Range.End &&
                            Enumerable.Range(0, 3).All(i => Unit(cursor + i * width) is >= 'A' and <= 'Z' or >= '0' and <= '9');
                        var fourth = Unit(cursor + 3 * width);
                        if (valid && (fourth is '|' or '\r' or '\n' || cursor + 3 * width == region.Range.End))
                        { plausible = true; break; }
                        while (cursor < region.Range.End && Unit(cursor) is not '\r' and not '\n') cursor += width;
                        if (cursor < region.Range.End) cursor += width;
                    }
                }
                if (plausible)
                {
                    var ordinal = messages.Count + 1;
                    messages.Add(new(ordinal, region.Range, region.Range, false, []));
                    rebuilt.Add(new(region.Range, ordinal, "Provisional HL7 message"));
                }
                else rebuilt.Add(region);
            }
            regions.Clear(); regions.AddRange(rebuilt);
        }
        progress?.Report(snapshot.Length);
        if (regions.Any(r => r.MessageNumber is null && r.Reason != "File encoding BOM"))
            fileDiagnostics.Add(new("UnclassifiedSource", Hl7DiagnosticSeverity.Warning, "Unclassified source regions remain available for byte inspection."));
        if (messages.Count == 0)
        {
            var decoder = encoding.GetDecoder(); var bytes = new byte[65536]; var chars = new char[65536];
            try
            {
                for (long offset = bom; offset < snapshot.Length;)
                {
                    token.ThrowIfCancellationRequested();
                    var length = (int)Math.Min(bytes.Length, snapshot.Length - offset);
                    snapshot.Read(new(offset, length), bytes.AsSpan(0, length));
                    var used = 0;
                    while (used < length)
                    { decoder.Convert(bytes.AsSpan(used, length - used), chars, offset + length == snapshot.Length, out var consumed, out _, out _); used += consumed; }
                    offset += length;
                }
                decoder.Convert([], chars, true, out _, out _, out _);
            }
            catch (DecoderFallbackException)
            { fileDiagnostics.Add(new("InvalidEncoding", Hl7DiagnosticSeverity.Error, "Unclassified input cannot be decoded safely; original bytes are retained.")); }
        }
        return new(encoding, bom, regions.AsReadOnly(), messages.AsReadOnly(), fileDiagnostics.AsReadOnly());
    }
    private sealed class IndexBytes(SourceSnapshot source, CancellationToken token, IProgress<long>? progress)
    {
        private readonly byte[] bytes = new byte[65536];
        private long start = -1;
        private int count;
        private static readonly byte[] markers = [13, 10, 11, 28, 239, 254, 255];
        public long FindSpecial(long position)
        {
            while (position < source.Length)
            {
                _ = this[position];
                var local = checked((int)(position - start));
                var found = bytes.AsSpan(local, count - local).IndexOfAny(markers);
                if (found >= 0) return position + found;
                position = start + count;
            }
            return source.Length;
        }
        public byte this[long offset]
        {
            get
            {
                if (offset < start || offset >= start + count)
                {
                    token.ThrowIfCancellationRequested();
                    start = offset / bytes.Length * bytes.Length;
                    count = (int)Math.Min(bytes.Length, source.Length - start);
                    source.Read(new(start, count), bytes.AsSpan(0, count));
                    progress?.Report(start);
                }
                return bytes[checked((int)(offset - start))];
            }
        }
    }
}
