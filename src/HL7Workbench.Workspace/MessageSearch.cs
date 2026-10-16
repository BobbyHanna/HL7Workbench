using System.Diagnostics;
using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public enum MessageSearchMode { Text, Field }
public enum MessageSearchComparison { Contains, Equals }

/// <summary>Literal session-only input. ToString never includes a search term.</summary>
public sealed record MessageSearchQuery(MessageSearchMode Mode, string Value, bool CaseSensitive = false,
    string FieldPath = "", MessageSearchComparison Comparison = MessageSearchComparison.Contains)
{
    public const int MaximumQueryCharacters = 1024;
    public override string ToString() => "Message search query";
    public string? ValidationError()
    {
        if (!Enum.IsDefined(Mode) || !Enum.IsDefined(Comparison)) return "Choose a supported search mode and comparison.";
        if (string.IsNullOrWhiteSpace(Value)) return "Enter a nonblank search value.";
        if (Value.Length > MaximumQueryCharacters) return "Search values are limited to 1,024 characters.";
        if (Mode == MessageSearchMode.Field)
        {
            try
            {
                var path = Hl7Path.Parse((FieldPath ?? "").Trim());
                if (FieldPath?.Contains('[') == true || path.Subcomponent is not null) return "Use SEG-field or SEG-field.component; all segments and repetitions are searched.";
            }
            catch (Exception e) when (e is ArgumentException or FormatException) { return "Enter a valid field/component path, for example PID-3.1."; }
        }
        return null;
    }
}

/// <summary>Bounded display metadata only. No matched value/snippet is retained. Control IDs stay session-only.</summary>
public sealed record MessageSearchMatch(int MessageNumber, string MessageType, string ControlId, string Location, long MatchCount)
{
    public override string ToString() => $"Message {MessageNumber}: {MatchCount} matches";
}
public sealed record MessageSearchProgress(int Scanned, int Total, int Matches, int Skipped);

public sealed class MessageSearchResults
{
    public const int PageSize = 200;
    private readonly MessageDocument document;
    public string DocumentId => document.Id;
    public long Revision { get; }
    public IReadOnlyList<MessageSearchMatch> Matches { get; }
    public int SkippedMessages { get; }
    public int PageCount => (Matches.Count + PageSize - 1) / PageSize;
    internal MessageSearchResults(MessageDocument document, long revision, List<MessageSearchMatch> matches, int skipped)
    { this.document = document; Revision = revision; Matches = matches.AsReadOnly(); SkippedMessages = skipped; }
    public bool IsCurrent(MessageDocument? candidate) => ReferenceEquals(document, candidate) && document.IsSearchCurrent(Revision);
    public IReadOnlyList<MessageSearchMatch> Page(int page)
    {
        if (page < 0 || page >= Math.Max(1, PageCount)) throw new ArgumentOutOfRangeException(nameof(page));
        return Matches.Skip(page * PageSize).Take(PageSize).ToArray();
    }
}

/// <summary>Read-only scans over the original byte index plus immutable unsaved drafts.
/// Only one message's bounded text/structure is resident. All scans run off the caller's UI thread.</summary>
public sealed class MessageSearch
{
    public Task<MessageSearchResults> SearchAsync(MessageDocument document, MessageSearchQuery query,
        IProgress<MessageSearchProgress>? progress = null, CancellationToken token = default)
    {
        if (query.ValidationError() is { } error) throw new ArgumentException(error, nameof(query));
        var capture = document.CaptureSearch();
        // Do not pass token to Task.Run: its delegate must run the finally block even
        // for a pre-canceled request, to release the document's source read lease.
        return Task.Run(() =>
        {
            using (capture)
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, capture.Lifetime, capture.Changes))
                return Scan(capture, query, progress, linked.Token);
        });
    }
    private static MessageSearchResults Scan(MessageDocument.SearchCapture capture, MessageSearchQuery query,
        IProgress<MessageSearchProgress>? progress, CancellationToken token)
    {
        var path = query.Mode == MessageSearchMode.Field ? Hl7Path.Parse(query.FieldPath.Trim()) : null;
        var matches = new List<MessageSearchMatch>(); var skipped = 0; var clock = Stopwatch.StartNew();
        for (var i = 0; i < capture.Count; i++)
        {
            token.ThrowIfCancellationRequested(); DecodedMessageSource? decoded = null;
            try
            {
                IHl7TextSource source;
                if (capture.Drafts.TryGetValue(i + 1, out var draft)) source = new StringHl7TextSource(draft);
                else
                {
                    var range = capture.Index!.Messages[i].PayloadRange; var encoding = capture.Encoding;
                    var prefix = capture.Snapshot!.ReadBytes(new(range.Offset, Math.Min(4, range.Length)));
                    var bom = Hl7SourceEncoding.Detect(prefix, encoding).BomLength;
                    if (range.Length <= 128 * 1024)
                    {
                        encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
                        var bytes = capture.Snapshot.ReadBytes(range, 128 * 1024);
                        source = new StringHl7TextSource(encoding.GetString(bytes.AsSpan(bom)));
                    }
                    else source = decoded = DecodedMessageSource.Create(capture.Snapshot, range, encoding, bom, token);
                }
                Hl7Structure? structure = null; long count = 0, first = -1; string location = "Text";
                if (path is null)
                {
                    (count, first) = Count(source, new(0, source.Length), query, token);
                    if (count > 0)
                    {
                        try { structure = Hl7StructureScanner.Scan(source, capture.Encoding, cancellationToken: token); }
                        catch (Hl7LimitException) { } // Text still matches if structural metadata exceeds its independent budget.
                        if (structure is not null) location = TextLocation(structure, first);
                    }
                }
                else
                {
                    structure = Hl7StructureScanner.Scan(source, capture.Encoding, cancellationToken: token);
                    var occurrence = 0;
                    foreach (var segment in structure.Segments.Where(s => s.Identifier == path.Segment))
                    {
                        occurrence++;
                        var field = segment.Fields.FirstOrDefault(f => f.Number == path.Field);
                        if (field is null) continue;
                        foreach (var repetition in field.Children)
                        {
                            var node = path.Component is { } component ? repetition.Children.FirstOrDefault(c => c.Number == component) : repetition;
                            if (node is null) continue;
                            var hits = query.Comparison == MessageSearchComparison.Equals ?
                                (node.Range.Length == query.Value.Length && string.Equals(source.Read(node.Range), query.Value, Comparison(query)) ? 1L : 0L) :
                                Count(source, node.Range, query, token).Count;
                            if (hits > 0 && count == 0) location = new Hl7Path(path.Segment, path.Field, occurrence, repetition.Number, path.Component).ToString();
                            count += hits;
                        }
                    }
                }
                if (count > 0)
                {
                    // The existing index permits at most 100,000 messages. One bounded
                    // row per ordinal, with capped headers, bounds resident metadata;
                    // the desktop exposes only a 200-row page to its virtualized list.
                    if (matches.Count >= MessageFileIndex.MaxIndexedMessages) throw new Hl7LimitException("SearchResults");
                    matches.Add(new(i + 1, Header(structure, source, 9, 48), Header(structure, source, 10, 96), location, count));
                }
            }
            catch (Exception e) when (e is DecoderFallbackException or Hl7LimitException) { skipped++; }
            finally { decoded?.Dispose(); }
            if (i == 0 || clock.ElapsedMilliseconds >= 100 || i + 1 == capture.Count)
            { progress?.Report(new(i + 1, capture.Count, matches.Count, skipped)); clock.Restart(); }
        }
        token.ThrowIfCancellationRequested();
        if (!capture.Document.IsSearchCurrent(capture.Revision)) throw new OperationCanceledException(token);
        return new(capture.Document, capture.Revision, matches, skipped);
    }
    private static StringComparison Comparison(MessageSearchQuery query) => query.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
    private static (long Count, long First) Count(IHl7TextSource source, CharacterRange range, MessageSearchQuery query, CancellationToken token)
    {
        long count = 0, first = -1, next = range.Offset; var carry = "";
        for (var offset = range.Offset; offset < range.End;)
        {
            token.ThrowIfCancellationRequested(); var size = (int)Math.Min(32 * 1024, range.End - offset);
            var window = carry + source.Read(new(offset, size)); var origin = offset - carry.Length;
            var start = (int)Math.Max(0, next - origin);
            while (start <= window.Length - query.Value.Length)
            {
                token.ThrowIfCancellationRequested(); var found = window.IndexOf(query.Value, start, Comparison(query));
                if (found < 0) break;
                if (first < 0) first = origin + found;
                count++; next = origin + found + query.Value.Length; start = found + query.Value.Length;
            }
            carry = window[^Math.Min(query.Value.Length - 1, window.Length)..]; offset += size;
        }
        return (count, first);
    }
    private static string Header(Hl7Structure? structure, IHl7TextSource source, int field, int maximum)
    {
        var node = structure?.Segments.FirstOrDefault(s => s.Identifier == "MSH")?.Fields.FirstOrDefault(f => f.Number == field);
        if (node is null) return "";
        var length = Math.Min(node.Range.Length, maximum);
        if (length > 0 && length < node.Range.Length && char.IsHighSurrogate(source[node.Range.Offset + length - 1])) length--;
        return new string(source.Read(new(node.Range.Offset, length)).Select(c => char.IsControl(c) ? ' ' : c).ToArray()) + (length < node.Range.Length ? "…" : "");
    }
    private static string TextLocation(Hl7Structure structure, long offset)
    {
        foreach (var segment in structure.Segments)
        foreach (var field in segment.Fields)
        {
            if (!field.Range.Contains(offset)) continue;
            var component = field.Children.SelectMany(r => r.Children).FirstOrDefault(c => c.Range.Contains(offset));
            return segment.Identifier + "-" + field.Number + (component is null ? "" : "." + component.Number);
        }
        return "Text";
    }
}
