using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>One displayed interval. Markers have no source characters; Omitted identifies their target.</summary>
public sealed record DisplayFragment(CharacterRange Display, CharacterRange? Source, CharacterRange? Omitted = null,
    ByteRange? OriginalBytes = null, string? Marker = null, bool IsOpaque = false);
public sealed record PresentationBlock(int Number, CharacterRange Display);

/// <summary>Presentation-only folding, with interval mappings rather than per-character mappings.</summary>
public sealed class MessagePresentation
{
    internal const int ValueThreshold = 16 * 1024;
    public const int PreviewCharacters = 256;
    public const int BlockCharacters = 16 * 1024;
    public const int MaximumEditorCharacters = 1024 * 1024;
    public IReadOnlyList<DisplayFragment> Fragments { get; }
    public IReadOnlyList<PresentationBlock> Blocks { get; }
    public long Length { get; }
    public long SourceLength { get; }
    public bool RequiresVirtualization { get; }
    private MessagePresentation(List<DisplayFragment> fragments, List<PresentationBlock> blocks, long length, long sourceLength, bool brokenLine)
    { Fragments = fragments.AsReadOnly(); Blocks = blocks.AsReadOnly(); Length = length; SourceLength = sourceLength; RequiresVirtualization = length > MaximumEditorCharacters || brokenLine; }

    public static MessagePresentation Create(IHl7TextSource source, Hl7Structure? structure, CancellationToken token = default)
    {
        var omissions = new List<(CharacterRange Range, bool Opaque)>();
        void Leaf(StructureNode node)
        {
            token.ThrowIfCancellationRequested();
            if (node.Children.Count > 0) { foreach (var child in node.Children) Leaf(child); }
            else if (node.Range.Length > ValueThreshold) omissions.Add((node.Range, false));
        }
        if (structure is null)
        {
            if (source.Length > ValueThreshold) omissions.Add((new(0, source.Length), true));
        }
        else foreach (var segment in structure.Segments)
        {
            if (segment.Fields.Count == 0 && segment.Range.Length > ValueThreshold) omissions.Add((segment.Range, true));
            else foreach (var field in segment.Fields) Leaf(field);
        }
        var fragments = new List<DisplayFragment>();
        long display = 0, position = 0;
        void Append(CharacterRange range)
        {
            if (range.Length == 0) return;
            fragments.Add(new(new(display, range.Length), range)); display = checked(display + range.Length);
        }
        long Align(long offset) => offset > 0 && offset < source.Length && char.IsLowSurrogate(source[offset]) && char.IsHighSurrogate(source[offset - 1]) ? offset - 1 : offset;
        foreach (var (range, opaque) in omissions)
        {
            Append(new(position, range.Offset - position));
            var prefixEnd = Align(range.Offset + PreviewCharacters);
            var suffixStart = Align(range.End - PreviewCharacters);
            Append(new(range.Offset, prefixEnd - range.Offset));
            var omitted = new CharacterRange(prefixEnd, suffixStart - prefixEnd);
            var marker = opaque ? $"⟦opaque source: {omitted.Length:N0} UTF-16 units omitted; inspect original⟧" :
                $"⟦{omitted.Length:N0} UTF-16 units omitted; inspect full value⟧";
            // OriginalBytes is the entire value, not merely the hidden middle. Exports exclude adjacent delimiters.
            fragments.Add(new(new(display, marker.Length), null, range, source.Bytes(range), marker, opaque));
            display += marker.Length;
            Append(new(suffixStart, range.End - suffixStart)); position = range.End;
        }
        Append(new(position, source.Length - position));
        var blocks = new List<PresentationBlock>();
        // Limit each rendered line, including pathological segments with thousands of small values.
        // Breaks between blocks are UI boundaries, not fabricated source characters.
        long blockStart = 0, blockLength = 0;
        var brokenLine = false;
        void Flush() { if (blockLength > 0) { blocks.Add(new(blocks.Count, new(blockStart, blockLength))); blockStart += blockLength; blockLength = 0; } }
        foreach (var fragment in fragments)
        {
            long consumed = 0;
            while (consumed < fragment.Display.Length)
            {
                token.ThrowIfCancellationRequested();
                var count = Math.Min(BlockCharacters - blockLength, fragment.Display.Length - consumed);
                if (fragment.Source is { } raw)
                {
                    var a = raw.Offset + consumed;
                    var newline = source.IndexOfAny(['\r', '\n'], a, count);
                    if (newline >= 0)
                    {
                        count = newline - a + 1;
                        if (source[newline] == '\r' && newline + 1 < raw.End && source[newline + 1] == '\n' && blockLength + count < BlockCharacters) count++;
                    }
                    else if (a + count < raw.End && Align(a + count) != a + count)
                    { if (count == 1) { Flush(); continue; } count--; }
                    blockLength += count; consumed += count;
                    if (newline >= 0 || blockLength >= BlockCharacters - 1) { if (newline < 0) brokenLine = true; Flush(); }
                }
                else { blockLength += count; consumed += count; if (blockLength >= BlockCharacters) { brokenLine = true; Flush(); } }
            }
        }
        Flush();
        return new(fragments, blocks, display, source.Length, brokenLine);
    }

    public DisplayFragment? Hit(long offset)
    {
        var low = 0; var high = Fragments.Count;
        while (low < high) { var middle = low + (high - low) / 2; if (Fragments[middle].Display.Offset <= offset) low = middle + 1; else high = middle; }
        return low > 0 && Fragments[low - 1].Display.Contains(offset) ? Fragments[low - 1] : null;
    }
    public long? SourceOffset(long displayOffset) => Hit(displayOffset) is { Source: { } range } fragment ? range.Offset + displayOffset - fragment.Display.Offset : null;
    public long DisplayOffset(long sourceOffset)
    {
        if (sourceOffset < 0 || sourceOffset > SourceLength) throw new ArgumentOutOfRangeException(nameof(sourceOffset));
        var low = 0; var high = Fragments.Count;
        while (low < high)
        {
            var mid = low + (high - low) / 2;
            var f = Fragments[mid]; var end = f.Source?.End ?? Fragments[mid + 1].Source!.Value.Offset;
            if (end <= sourceOffset) low = mid + 1; else high = mid;
        }
        if (low == Fragments.Count) return Length;
        var fragment = Fragments[low];
        return fragment.Source is { } raw ? fragment.Display.Offset + Math.Clamp(sourceOffset - raw.Offset, 0, raw.Length) : fragment.Display.Offset;
    }
    public IEnumerable<DisplayFragment> Intersecting(CharacterRange display)
    {
        var first = Hit(display.Offset);
        if (first is null) yield break;
        var low = 0; var high = Fragments.Count;
        while (low < high) { var mid = low + (high - low) / 2; if (Fragments[mid].Display.End <= display.Offset) low = mid + 1; else high = mid; }
        for (var i = low; i < Fragments.Count && Fragments[i].Display.Offset < display.End; i++) yield return Fragments[i];
    }
    public string Read(IHl7TextSource source, CharacterRange display)
    {
        if (display.Offset < 0 || display.End > Length || display.Length > MaximumEditorCharacters) throw new ArgumentOutOfRangeException(nameof(display));
        var text = new StringBuilder(checked((int)display.Length));
        foreach (var f in Intersecting(display))
        {
            var start = Math.Max(f.Display.Offset, display.Offset); var count = Math.Min(f.Display.End, display.End) - start;
            if (f.Source is { } raw)
            {
                while (count > 0)
                {
                    var take = Math.Min(count, 16384); text.Append(source.Read(new(raw.Offset + start - f.Display.Offset, take))); start += take; count -= take;
                }
            }
            else text.Append(f.Marker.AsSpan(checked((int)(start - f.Display.Offset)), checked((int)count)));
        }
        return text.ToString();
    }
    /// <summary>True only when a selection corresponds to one contiguous original interval.</summary>
    public bool IsContiguousSource(CharacterRange display) => display.Offset >= 0 && display.Length >= 0 && display.End <= Length &&
        (display.Length == 0 || Intersecting(display).All(f => f.Source is not null));
}
