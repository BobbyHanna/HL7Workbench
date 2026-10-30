using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HL7Workbench.Desktop.Documentation;

internal sealed record HelpLink(string Kind, string Target, string Anchor);
internal sealed record HelpSpan(string Kind, string Text, HelpLink? Link);
internal sealed record HelpBlock(string Kind, string Text, IReadOnlyList<HelpSpan> Spans, int Level,
    string Anchor, string Marker, string Language, IReadOnlyList<IReadOnlyList<IReadOnlyList<HelpSpan>>> Rows);
internal sealed record HelpTopic(string Id, string Title, string Source, string SourceHash, string Keywords, IReadOnlyList<HelpBlock> Blocks);
internal sealed record HelpSearchResult(HelpTopic Topic, string Anchor, string Snippet);

/// <summary>Immutable embedded documentation only. No document, filesystem, worker or network service is consulted.</summary>
internal sealed class DocumentationCatalog
{
    internal const int MaximumQueryCharacters = 256, MaximumTerms = 16, MaximumTopics = 64, SnippetCharacters = 160;
    private sealed record CatalogInput(int Version, TopicInput[] Topics);
    private sealed record TopicInput(string Id, string Title, string Source, string SourceHash, string Keywords, BlockInput[] Blocks);
    private sealed record BlockInput(string Kind, string Text, SpanInput[] Spans, int Level = 0, string Anchor = "", string Marker = "", string Language = "", SpanInput[][][]? Rows = null);
    private sealed record SpanInput(string Kind, string Text, LinkInput? Link = null);
    private sealed record LinkInput(string Kind, string Target, string Anchor);
    private static readonly Lazy<DocumentationCatalog> current = new(() =>
    {
        using var input = typeof(DocumentationCatalog).Assembly.GetManifestResourceStream("HL7Workbench.Documentation.json")
            ?? throw new InvalidDataException("Bundled documentation is unavailable.");
        return Load(input);
    });
    internal static DocumentationCatalog Current => current.Value;
    internal IReadOnlyList<HelpTopic> Topics { get; }
    private readonly Dictionary<string, HelpTopic> byId;
    private readonly Dictionary<string, string> search;
    private DocumentationCatalog(HelpTopic[] topics)
    {
        Topics = Array.AsReadOnly(topics); byId = topics.ToDictionary(t => t.Id, StringComparer.Ordinal);
        search = topics.ToDictionary(t => t.Id, t => t.Title + "\n" + t.Keywords + "\n" + string.Join('\n', t.Blocks.Select(b => b.Text)));
    }
    internal HelpTopic? Find(string id) => byId.GetValueOrDefault(id);
    internal bool HasAnchor(string id, string anchor) => Find(id) is { } topic && (anchor.Length == 0 || topic.Blocks.Any(b => b.Kind == "heading" && b.Anchor == anchor));
    internal IReadOnlyList<HelpSearchResult> Search(string? query)
    {
        query = (query ?? "")[..Math.Min(query?.Length ?? 0, MaximumQueryCharacters)];
        var words = query.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > MaximumTerms) return [];
        var results = new List<HelpSearchResult>();
        foreach (var topic in Topics)
        {
            if (!words.All(w => search[topic.Id].Contains(w, StringComparison.OrdinalIgnoreCase))) continue;
            var block = topic.Blocks.FirstOrDefault(b => b.Kind != "heading" && words.Any(w => b.Text.Contains(w, StringComparison.OrdinalIgnoreCase)))
                ?? topic.Blocks.FirstOrDefault(b => b.Kind == "paragraph") ?? topic.Blocks[0];
            var anchor = "";
            foreach (var candidate in topic.Blocks)
            {
                if (candidate.Kind == "heading") anchor = candidate.Anchor;
                if (ReferenceEquals(candidate, block)) break;
            }
            var preview = block.Text.Replace('\n', ' ');
            results.Add(new(topic, anchor, preview.Length <= SnippetCharacters ? preview : preview[..SnippetCharacters] + "…"));
        }
        return results.AsReadOnly();
    }
    internal static DocumentationCatalog Load(Stream input)
    {
        // Only the embedded, build-validated resource is loaded in production. Bound
        // it here as well so damaged deployment metadata cannot cause unbounded UI work.
        using var bytes = new MemoryStream(); var buffer = new byte[8192]; int read;
        while ((read = input.Read(buffer)) != 0)
        { if (bytes.Length + read > 2 * 1024 * 1024) throw new InvalidDataException("Documentation byte budget exceeded."); bytes.Write(buffer, 0, read); }
        var source = JsonSerializer.Deserialize<CatalogInput>(bytes.ToArray(), new JsonSerializerOptions
        { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 16 })
            ?? throw new InvalidDataException("Invalid documentation catalog.");
        if (source.Version != 1 || source.Topics is not { Length: > 0 and <= MaximumTopics }) throw new InvalidDataException("Invalid documentation version/topics.");
        static void Require([DoesNotReturnIf(false)] bool condition) { if (!condition) throw new InvalidDataException("Invalid documentation content."); }
        static IReadOnlyList<HelpSpan> Spans(SpanInput[] values)
        {
            if (values is null || values.Length > 2048) throw new InvalidDataException("Invalid documentation spans.");
            return Array.AsReadOnly(values.Select(s =>
            {
                if (s is null || s.Kind is not ("text" or "strong" or "emphasis" or "code" or "link") || s.Text is null || s.Text.Length > 32768)
                    throw new InvalidDataException("Invalid documentation span.");
                if ((s.Kind == "link") != (s.Link is not null)) throw new InvalidDataException("Invalid documentation link.");
                return new HelpSpan(s.Kind, s.Text, s.Link is { } link ? new(link.Kind, link.Target, link.Anchor) : null);
            }).ToArray());
        }
        var topics = source.Topics.Select(t =>
        {
            Require(t is not null && t.Id is { Length: > 0 and <= 64 } && t.Id[0] is >= 'a' and <= 'z' && t.Id.All(c => char.IsAsciiLetterLower(c) || char.IsAsciiDigit(c) || c == '-'));
            Require(t!.Title is { Length: > 0 and <= 256 } && t.Keywords is { Length: <= 512 } && t.SourceHash is { Length: 64 } && t.SourceHash.All(char.IsAsciiHexDigit));
            Require(t.Source is not null && (t.Source.StartsWith("docs/user/", StringComparison.Ordinal) || t.Source is "docs/reference/limits.md" or "docs/reference/storage-and-formats.md") && !t.Source.Contains("..") && !t.Source.Contains('\\'));
            Require(t.Blocks is { Length: > 0 and <= 512 });
            var blocks = t.Blocks!.Select(b =>
            {
                Require(b is not null && b.Kind is "heading" or "paragraph" or "list" or "quote" or "table" or "code");
                Require(b!.Text is { Length: <= 100000 } && b.Anchor is not null && b.Marker is not null && b.Language is not null);
                Require(b.Kind != "heading" || b.Level is >= 1 and <= 6 && b.Anchor.Length > 0);
                var rows = b.Rows ?? [];
                Require(rows.Length <= 256 && rows.All(r => r is { Length: > 0 and <= 16 }));
                Require(b.Kind != "table" || rows.Length >= 2 && rows.All(r => r.Length == rows[0].Length));
                return new HelpBlock(b.Kind, b.Text, Spans(b.Spans), b.Level, b.Anchor, b.Marker, b.Language,
                    Array.AsReadOnly(rows.Select(r => (IReadOnlyList<IReadOnlyList<HelpSpan>>)Array.AsReadOnly(r.Select(Spans).ToArray())).ToArray()));
            }).ToArray();
            Require(blocks[0].Kind == "heading" && blocks[0].Level == 1);
            Require(blocks.Where(b => b.Kind == "heading").Select(b => b.Anchor).Distinct(StringComparer.Ordinal).Count() == blocks.Count(b => b.Kind == "heading"));
            return new HelpTopic(t.Id, t.Title, t.Source, t.SourceHash, t.Keywords, Array.AsReadOnly(blocks));
        }).ToArray();
        Require(topics.Select(t => t.Id).Distinct(StringComparer.Ordinal).Count() == topics.Length);
        var catalog = new DocumentationCatalog(topics);
        foreach (var span in topics.SelectMany(t => t.Blocks).SelectMany(b => b.Spans.Concat(b.Rows.SelectMany(r => r).SelectMany(c => c))))
        {
            if (span.Link is not { } link) continue;
            Require(link.Target is not null && link.Anchor is not null);
            Require(link.Kind switch
            {
                "topic" => catalog.HasAnchor(link.Target, link.Anchor),
                "api" => link.Target.Length == 0 && link.Anchor.Length == 0,
                "repository" => link.Target.Length is > 0 and < 1024 && !link.Target.StartsWith('/') && !link.Target.Contains("..") && !link.Target.Contains('\\'),
                "external" => Uri.TryCreate(link.Target, UriKind.Absolute, out var uri) && uri.Scheme == "https",
                _ => false
            });
        }
        return catalog;
    }
}
