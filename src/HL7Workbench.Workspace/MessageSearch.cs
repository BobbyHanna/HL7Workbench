using System.Diagnostics;
using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public enum MessageSearchMode { Text, Field }
public enum MessageSearchComparison { Contains, Equals }

/// <summary>Literal session-only input. ToString never includes a search term.</summary>
public sealed partial record MessageSearchQuery(MessageSearchMode Mode, string Value, bool CaseSensitive = false,
    string FieldPath = "", MessageSearchComparison Comparison = MessageSearchComparison.Contains)
{
    public const int MaximumQueryCharacters = 1024;
    public override string ToString() => "Message search query";
    public string? ValidationError()
    {
        if (Version != CurrentVersion) return "Unsupported search query version.";
        if (!Enum.IsDefined(BuilderMode) || !Enum.IsDefined(Combination)) return "Choose a supported builder mode and combination.";
        if (BuilderMode == SearchBuilderMode.Advanced)
        {
            return ValidateTree(EffectiveRoot);
        }
        if (!Enum.IsDefined(Mode) || !Enum.IsDefined(Comparison)) return "Choose a supported search mode and comparison.";
        if (string.IsNullOrWhiteSpace(Value)) return "Enter a nonblank search value.";
        if (Value.Length > MaximumQueryCharacters) return "Search values are limited to 1,024 characters.";
        if (Mode == MessageSearchMode.Field)
        {
            try
            {
                var path = Hl7Path.Parse((FieldPath ?? "").Trim());
                if (FieldPath?.Contains('[') == true) return "Use an unindexed field/component/subcomponent path; all segments and repetitions are searched.";
            }
            catch (Exception e) when (e is ArgumentException or FormatException) { return "Enter a valid field/component path, for example PID-3.1."; }
        }
        return null;
    }
}

/// <summary>Bounded display metadata only. No matched value/snippet is retained. Control IDs stay session-only.</summary>
public sealed record MessageSearchMatch(int MessageNumber, string MessageType, string ControlId, string Location, long MatchCount)
{
    /// <summary>Identity within the originating document; MessageNumber is its position at capture.</summary>
    public MessageId MessageId { get; init; }
    public int? OriginalOrdinal { get; init; }
    public string ConditionSummary { get; init; } = "";
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
public sealed partial class MessageSearch
{
    public Task<MessageSearchResults> SearchAsync(MessageDocument document, MessageSearchQuery query,
        IProgress<MessageSearchProgress>? progress = null, CancellationToken token = default)
    {
        query = query.Snapshot();
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
        Criterion Compile(SearchCondition c) => new(c.Canonical(),
            c.Target == SearchTarget.Field ? Hl7Path.Parse(c.Path.Trim()) : null,
            new MessageSearchQuery(MessageSearchMode.Text, c.Value, c.CaseSensitive));
        var expressionRoot = query.BuilderMode == SearchBuilderMode.Advanced ? (SearchExpression)query.EffectiveRoot : query.EffectiveConditions()[0];
        var leaves = query.EffectiveConditions();
        var compiled = leaves.Distinct().ToDictionary(c => c, Compile);
        var matches = new List<MessageSearchMatch>(); var skipped = 0; var clock = Stopwatch.StartNew();
        for (var i = 0; i < capture.Count; i++)
        {
            token.ThrowIfCancellationRequested(); DecodedMessageSource? decoded = null;
            try
            {
                IHl7TextSource source;
                var entry = capture.Entries[i];
                if (entry.Text is { } draft) source = new StringHl7TextSource(draft);
                else
                {
                    var range = capture.Index!.Messages[entry.Entry.SourceOrdinal!.Value - 1].PayloadRange; var encoding = capture.Encoding;
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
                Hl7Structure? structure = null;
                Hl7Structure Structure() => structure ??= Hl7StructureScanner.Scan(source, capture.Encoding, cancellationToken: token);
                var advanced = query.BuilderMode == SearchBuilderMode.Advanced;
                long count = 0, first = -1; var location = "Text"; var summary = ""; var leafIndex = 0; var groups = 0;
                bool Visit(SearchExpression expression, int depth)
                {
                    token.ThrowIfCancellationRequested();
                    if (expression is SearchGroup group)
                    {
                        // Defense in depth: evaluation cannot walk an unbounded or empty tree.
                        if (++groups > MessageSearchQuery.MaximumGroups || depth > MessageSearchQuery.MaximumDepth || group.Children.Count == 0) throw new ArgumentException("Invalid search tree.");
                        var all = group.Combination == SearchCombination.All;
                        foreach (var child in group.Children)
                            if (Visit(child, child is SearchGroup ? depth + 1 : depth) != all) return !all;
                        return all;
                    }
                    if (++leafIndex > MessageSearchQuery.MaximumConditions) throw new ArgumentException("Invalid search tree.");
                    var criterion = compiled[(SearchCondition)expression];
                    var evaluated = Evaluate(source, Structure, criterion, !advanced, token);
                    if (evaluated.Matches)
                    {
                        if (count == 0) { location = evaluated.Location; first = evaluated.First; }
                        count += advanced ? 1 : evaluated.Count;
                    }
                    return evaluated.Matches;
                }
                var accepted = Visit(expressionRoot, 1);
                if (accepted)
                {
                    // Optional headers must not turn a valid literal match into a skip.
                    // Required structural conditions propagate their budget failure above.
                    if (structure is null) { try { _ = Structure(); } catch (Hl7LimitException) { } }
                    if (first >= 0 && structure is not null) location = TextLocation(structure, first);
                    if (advanced) summary = query.Root is null ? (query.Combination == SearchCombination.All ? $"All {leaves.Count} conditions" : $"Condition {leafIndex} (first ANY match)") : $"{count} satisfied evaluated conditions";
                    // The existing index permits at most 100,000 messages. One bounded
                    // row per stable identity, with capped headers, bounds resident metadata;
                    // the desktop exposes only a 200-row page to its virtualized list.
                    if (matches.Count >= MessageFileIndex.MaxIndexedMessages) throw new Hl7LimitException("SearchResults");
                    matches.Add(new(entry.Position, Header(structure, source, 9, 48), Header(structure, source, 10, 96), location, count)
                    { MessageId = entry.Entry.Id, OriginalOrdinal = entry.Entry.SourceOrdinal, ConditionSummary = summary });
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
    private static (long Count, long First) Count(IHl7TextSource source, CharacterRange range, MessageSearchQuery query, CancellationToken token, bool firstOnly = false)
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
                if (firstOnly) return (count, first);
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
