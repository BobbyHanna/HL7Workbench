using System.Text;

namespace HL7Workbench.Core;

public enum Hl7DifferenceKind { Added, Removed, Modified, DelimiterDefinition, Terminator, SourceSpelling }
public sealed record Hl7ComparisonOptions(bool IgnoreMsh7 = false, bool IgnoreMsh10 = false,
    bool IgnoreTrailingEmptyFields = false, bool IgnoreTerminators = false);

/// <summary>One side's message-local UTF-16 coordinates. Occurrences belong to this side, not to the aligned row.</summary>
public sealed record Hl7ComparisonLocation(string Segment, int Occurrence, CharacterRange Range,
    int? Field = null, int? Repetition = null, int? Component = null, int? Subcomponent = null)
{
    public string Path => $"{Segment}[{Occurrence}]" + (Field is { } f ? $"-{f}" : "") +
        (Repetition is { } r ? $"[{r}]" : "") + (Component is { } c ? $".{c}" : "") + (Subcomponent is { } s ? $".{s}" : "");
}

/// <summary>Values are explicit bounded previews; null means absent, empty means present-empty. Ranges remain authoritative.</summary>
public sealed record Hl7Difference(Hl7DifferenceKind Kind, Hl7ComparisonLocation? Before, Hl7ComparisonLocation? After,
    string? BeforeValue, string? AfterValue, bool BeforeAbbreviated, bool AfterAbbreviated)
{
    public string Path => Before?.Path ?? After!.Path;
    public string Summary => $"{Kind}: {Path}" + (Before is not null && After is not null && Before.Path != After.Path ? $" → {After.Path}" : "");
    public override string ToString() => Summary;
}

public sealed record Hl7ComparisonResult(IReadOnlyList<Hl7Difference> Differences);

/// <summary>Read-only structural diff over caller-owned immutable sources. Scanner budgets and an explicit result limit
/// bound storage. Alignment uses at most 64 future segments (32 repetitions), with earliest/shortest exact anchors.
/// Outside that window same-name segments pair as modifications; no Set ID is treated as a key.</summary>
public static class Hl7Comparison
{
    public const int MaximumDifferences = 20_000;
    public const int PreviewCharacters = 256;
    public static Hl7ComparisonResult Compare(IHl7TextSource before, Hl7Structure beforeStructure,
        IHl7TextSource after, Hl7Structure afterStructure, Hl7ComparisonOptions? options = null,
        CancellationToken cancellationToken = default, Encoding? beforeEncoding = null, Encoding? afterEncoding = null) =>
        new Engine(before, beforeStructure, after, afterStructure, options ?? new(), cancellationToken,
            beforeEncoding ?? Encoding.UTF8, afterEncoding ?? Encoding.UTF8).Run();

    private sealed class Engine(IHl7TextSource a, Hl7Structure sa, IHl7TextSource b, Hl7Structure sb,
        Hl7ComparisonOptions options, CancellationToken token, Encoding beforeEncoding, Encoding afterEncoding)
    {
        private readonly List<Hl7Difference> differences = [];
        private bool Ignored(string id, int field) => id == "MSH" && (field == 7 && options.IgnoreMsh7 || field == 10 && options.IgnoreMsh10);
        private static int FieldCount(StructureSegment s, bool trim)
        {
            var count = s.Fields.Count;
            while (trim && count > (s.Identifier == "MSH" ? 2 : 0) && s.Fields[count - 1].Range.Length == 0) count--;
            return count;
        }
        public Hl7ComparisonResult Run()
        {
            token.ThrowIfCancellationRequested();
            var aa = sa.Segments; var bb = sb.Segments;
            var ah = aa.Select(s => Fingerprint(s, a)).ToArray(); var bh = bb.Select(s => Fingerprint(s, b)).ToArray();
            var ao = Occurrences(aa); var bo = Occurrences(bb);
            foreach (var (x, y) in Align(ah, bh, 64, aa.Select(s => s.Identifier).ToArray(), bb.Select(s => s.Identifier).ToArray()))
            {
                token.ThrowIfCancellationRequested();
                if (x < 0) { Add(Hl7DifferenceKind.Added, null, Location(bb[y], bo[y])); continue; }
                if (y < 0) { Add(Hl7DifferenceKind.Removed, Location(aa[x], ao[x]), null); continue; }
                var left = aa[x]; var right = bb[y];
                var la = Location(left, ao[x]); var lb = Location(right, bo[y]);
                if (left.Identifier != right.Identifier)
                { Add(Hl7DifferenceKind.Removed, la, null); Add(Hl7DifferenceKind.Added, null, lb); continue; }
                if (left.Fields.Count == 0 && right.Fields.Count == 0 ||
                    left.Fields.Count == 0 && !left.CanEdit || right.Fields.Count == 0 && !right.CanEdit)
                { if (!Equal(a, left.Range, b, right.Range)) Add(Hl7DifferenceKind.Modified, la, lb); }
                else
                {
                    var ac = FieldCount(left, options.IgnoreTrailingEmptyFields); var bc = FieldCount(right, options.IgnoreTrailingEmptyFields);
                    for (var f = 0; f < Math.Max(ac, bc); f++)
                    {
                        if (Ignored(left.Identifier, f + 1)) continue;
                        var an = f < ac ? left.Fields[f] : null; var bn = f < bc ? right.Fields[f] : null;
                        Nodes(an, bn, la with { Field = f + 1 }, lb with { Field = f + 1 }, left.Identifier == "MSH" && f < 2);
                    }
                }
                if (!options.IgnoreTerminators && left.Terminator != right.Terminator)
                    Add(Hl7DifferenceKind.Terminator, la with { Range = new(left.Range.End, left.Terminator.Length) },
                        lb with { Range = new(right.Range.End, right.Terminator.Length) });
            }
            return new(differences.AsReadOnly());
        }
        private static Hl7ComparisonLocation Location(StructureSegment s, int occurrence) => new(s.Identifier, occurrence, s.Range);
        private static int[] Occurrences(IReadOnlyList<StructureSegment> segments)
        {
            var counts = new Dictionary<string, int>(); var result = new int[segments.Count];
            for (var i = 0; i < result.Length; i++) { counts.TryGetValue(segments[i].Identifier, out var n); result[i] = counts[segments[i].Identifier] = n + 1; }
            return result;
        }
        private void Nodes(StructureNode? an, StructureNode? bn, Hl7ComparisonLocation la, Hl7ComparisonLocation lb, bool literal = false)
        {
            token.ThrowIfCancellationRequested();
            if (an is null || bn is null)
            { Add(an is null ? Hl7DifferenceKind.Added : Hl7DifferenceKind.Removed, an is null ? null : la with { Range = an.Range }, bn is null ? null : lb with { Range = bn.Range }); return; }
            la = la with { Range = an.Range }; lb = lb with { Range = bn.Range };
            if (literal) { if (!Equal(a, an.Range, b, bn.Range)) Add(Hl7DifferenceKind.DelimiterDefinition, la, lb); return; }
            if (an.Children.Count == 0 && bn.Children.Count == 0)
            {
                if (!Equal(a, an.Range, b, bn.Range))
                    Add(Semantic(a, an.Range, sa) is { } av && Semantic(b, bn.Range, sb) is { } bv && av == bv ? Hl7DifferenceKind.SourceSpelling : Hl7DifferenceKind.Modified, la, lb);
                return;
            }
            // Collapse implicit singleton wrappers to the meaningful field/component level.
            if (an.Children.Count == 1 && bn.Children.Count == 1 && IsLeafChain(an) && IsLeafChain(bn))
            {
                if (!Equal(a, an.Range, b, bn.Range))
                    Add(Semantic(a, an.Range, sa) is { } av && Semantic(b, bn.Range, sb) is { } bv && av == bv ? Hl7DifferenceKind.SourceSpelling : Hl7DifferenceKind.Modified, la, lb);
                return;
            }
            IEnumerable<(int, int)> pairs = an.Kind == StructureKind.Field ?
                Align(an.Children.Select(n => NodeHash(n, a)).ToArray(), bn.Children.Select(n => NodeHash(n, b)).ToArray(), 32) :
                Enumerable.Range(0, Math.Max(an.Children.Count, bn.Children.Count)).Select(i => (i < an.Children.Count ? i : -1, i < bn.Children.Count ? i : -1));
            foreach (var (x, y) in pairs)
            {
                var ac = x < 0 ? null : an.Children[x]; var bc = y < 0 ? null : bn.Children[y];
                Hl7ComparisonLocation Child(Hl7ComparisonLocation l, int n) => an.Kind switch
                { StructureKind.Field => l with { Repetition = n }, StructureKind.Repetition => l with { Component = n }, _ => l with { Subcomponent = n } };
                Nodes(ac, bc, Child(la, x + 1), Child(lb, y + 1));
            }
        }
        private static bool IsLeafChain(StructureNode n) => n.Children.Count == 0 || n.Children.Count == 1 && IsLeafChain(n.Children[0]);
        private string? Semantic(IHl7TextSource source, CharacterRange range, Hl7Structure structure)
        {
            if (range.Length > 64 * 1024) return null;
            var raw = source.Read(range);
            // Explicit null is a state, not the decoded literal two-quote string.
            if (raw == "\"\"") return null;
            var decoded = Hl7EscapeCodec.Decode(raw, structure.Delimiters, ReferenceEquals(source, a) ? beforeEncoding : afterEncoding);
            return decoded.Diagnostics.Count == 0 ? decoded.Text : null;
        }
        private void Add(Hl7DifferenceKind kind, Hl7ComparisonLocation? left, Hl7ComparisonLocation? right)
        {
            token.ThrowIfCancellationRequested();
            if (differences.Count == MaximumDifferences) throw new Hl7LimitException("ComparisonDifferences");
            string? Preview(IHl7TextSource source, Hl7ComparisonLocation? location)
            {
                if (location is null) return null;
                var range = location.Range; var length = Math.Min(PreviewCharacters, range.Length);
                if (length > 0 && length < range.Length && char.IsHighSurrogate(source[range.Offset + length - 1]) && char.IsLowSurrogate(source[range.Offset + length])) length--;
                return source.Read(new(range.Offset, length));
            }
            differences.Add(new(kind, left, right, Preview(a, left), Preview(b, right), left?.Range.Length > PreviewCharacters, right?.Range.Length > PreviewCharacters));
        }
        private bool Equal(IHl7TextSource left, CharacterRange lr, IHl7TextSource right, CharacterRange rr)
        {
            if (lr.Length != rr.Length) return false;
            for (long p = 0; p < lr.Length; p += 4096)
            { token.ThrowIfCancellationRequested(); var n = Math.Min(4096, lr.Length - p); if (left.Read(new(lr.Offset + p, n)) != right.Read(new(rr.Offset + p, n))) return false; }
            return true;
        }
        // Fingerprints choose anchors only. Every aligned pair is still compared against actual source ranges.
        private ulong Fingerprint(StructureSegment s, IHl7TextSource source)
        {
            ulong hash = 14695981039346656037; foreach (var c in s.Identifier) Mix(ref hash, c);
            if (s.Fields.Count == 0) HashRange(ref hash, source, s.Range);
            for (var i = 0; i < FieldCount(s, options.IgnoreTrailingEmptyFields); i++)
                if (!(s.Identifier == "MSH" && i < 2) && !Ignored(s.Identifier, i + 1)) HashNode(ref hash, s.Fields[i], source);
            return hash;
        }
        private ulong NodeHash(StructureNode node, IHl7TextSource source) { ulong h = 14695981039346656037; HashNode(ref h, node, source); return h; }
        private static void Mix(ref ulong hash, ulong value) { hash = unchecked((hash ^ value) * 1099511628211); }
        private void HashNode(ref ulong hash, StructureNode node, IHl7TextSource source)
        {
            token.ThrowIfCancellationRequested(); Mix(ref hash, (ulong)node.Kind); Mix(ref hash, (ulong)node.Children.Count);
            if (node.Children.Count == 0) HashRange(ref hash, source, node.Range);
            else foreach (var child in node.Children) HashNode(ref hash, child, source);
        }
        private void HashRange(ref ulong hash, IHl7TextSource source, CharacterRange range)
        {
            Mix(ref hash, (ulong)range.Length);
            for (long p = 0; p < range.Length; p += 4096)
            { token.ThrowIfCancellationRequested(); foreach (var c in source.Read(new(range.Offset + p, Math.Min(4096, range.Length - p)))) Mix(ref hash, c); }
        }
        private IEnumerable<(int, int)> Align(ulong[] left, ulong[] right, int window, string[]? leftNames = null, string[]? rightNames = null)
        {
            var x = 0; var y = 0;
            while (x < left.Length && y < right.Length)
            {
                token.ThrowIfCancellationRequested();
                if (left[x] != right[y])
                {
                    var insert = -1; var remove = -1;
                    for (var d = 1; d <= window; d++)
                    { if (y + d < right.Length && left[x] == right[y + d]) { insert = d; break; } }
                    for (var d = 1; d <= window; d++)
                    { if (x + d < left.Length && left[x + d] == right[y]) { remove = d; break; } }
                    if (insert > 0 && (remove < 0 || insert <= remove)) { for (var d = 0; d < insert; d++) yield return (-1, y++); continue; }
                    if (remove > 0) { for (var d = 0; d < remove; d++) yield return (x++, -1); continue; }
                    // Exact anchors may be absent when neighboring segments were also edited.
                    // A bounded name anchor still recognizes a new segment before those modifications.
                    if (leftNames is not null && rightNames is not null && leftNames[x] != rightNames[y])
                    {
                        for (var d = 1; d <= window; d++)
                            if (y + d < right.Length && leftNames[x] == rightNames[y + d]) { insert = d; break; }
                        for (var d = 1; d <= window; d++)
                            if (x + d < left.Length && leftNames[x + d] == rightNames[y]) { remove = d; break; }
                        if (insert > 0 && (remove < 0 || insert <= remove)) { for (var d = 0; d < insert; d++) yield return (-1, y++); continue; }
                        if (remove > 0) { for (var d = 0; d < remove; d++) yield return (x++, -1); continue; }
                    }
                }
                yield return (x++, y++);
            }
            while (x < left.Length) { token.ThrowIfCancellationRequested(); yield return (x++, -1); }
            while (y < right.Length) { token.ThrowIfCancellationRequested(); yield return (-1, y++); }
        }
    }
}
