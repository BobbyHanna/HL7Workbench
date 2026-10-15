using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>A range in the original decoded UTF-16 text, excluding the encoding BOM.</summary>
public readonly record struct MessageRange(int Offset, int Length);

/// <summary>Structural tooltip data. A future version/profile resolver can enrich Path separately.</summary>
public sealed record InspectionPathInfo(Hl7Path Path, Hl7ValueState State)
{
    public string StateLabel => State == Hl7ValueState.ExplicitNull ? "Explicit Null" : State.ToString();
}

public sealed class SegmentEntry
{
    private readonly Hl7Segment? segment;
    public Hl7Segment Segment => segment ?? throw new InvalidOperationException("Source-backed entries use Structure and explicit bounded reads.");
    public StructureSegment? Structure { get; }
    public string Identifier => Structure?.Identifier ?? Segment.Identifier;
    public bool CanEdit => Structure?.CanEdit ?? Segment.CanEdit;
    public int Sequence { get; }
    public int Occurrence { get; }
    public string Label => $"{Sequence}. {(Identifier.Length == 0 ? "(blank)" : Identifier)} [{Occurrence}]";
    public CharacterRange Range64 => Structure?.Range ?? new(Segment.CharacterOffset, Segment.Raw.Length);
    public MessageRange Range => new(checked((int)Range64.Offset), checked((int)Range64.Length));
    public bool HasFieldPaths => (Structure?.Fields.Count ?? Segment.Fields.Count) > 0 || CanEdit;
    internal SegmentEntry(Hl7Segment segment, int sequence, int occurrence)
    { this.segment = segment; Sequence = sequence; Occurrence = occurrence; }
    internal SegmentEntry(StructureSegment structure, int sequence, int occurrence)
    { Structure = structure; Sequence = sequence; Occurrence = occurrence; }
    internal int RepetitionCount(int field) => Structure is { } structure ?
        structure.Fields.FirstOrDefault(f => f.Number == field)?.Children.Count ?? 0 : Segment.GetField(field)?.Repetitions.Count ?? 0;
}

/// <summary>A structural value only; no field names or version-specific meaning are inferred.</summary>
public sealed class InspectionNode
{
    private IReadOnlyList<InspectionNode>? children;
    private IReadOnlyList<InspectionNode>? visibleChildren;
    public SegmentEntry Owner { get; }
    public Hl7Path Path { get; }
    public Hl7Element? Element { get; }
    public StructureNode? Structure { get; }
    public bool HasContent => Element is not null || Structure is not null;
    public string Preview => Structure?.Preview ?? (Element?.Raw is { } raw ? raw[..Math.Min(100, raw.Length)] : "");
    public InspectionNode? Parent { get; }
    public string? Raw => Structure is not null ? throw new InvalidOperationException("Use bounded preview access for source-backed values.") : Element?.Raw;
    public Hl7ValueState State => Structure?.State ?? new Hl7Value(Raw).State;
    public CharacterRange? Range64 => Structure?.Range ?? (Element is { } element ? new CharacterRange(element.CharacterOffset, element.Raw.Length) : null);
    public MessageRange? Range => Range64 is { } r ? new(checked((int)r.Offset), checked((int)r.Length)) : null;
    public InspectionPathInfo PathInfo => new(Path, State);
    /// <summary>Compact display only. Path retains the implicit repetition index for lookup.</summary>
    public string DisplayPath => $"{Path.Segment}[{Path.Occurrence}]-{Path.Field}" +
        (Path.Repetition is { } r && Owner.RepetitionCount(Path.Field) > 1 ? $"[{r}]" : "") +
        (Path.Component is { } c ? $".{c}" : "") + (Path.Subcomponent is { } s ? $".{s}" : "");
    public string Label => $"{DisplayPath} · {PathInfo.StateLabel}";
    // Child projections are lazy: expanding one field does not allocate a second full-message hierarchy.
    public IReadOnlyList<InspectionNode> Children => children ??= CreateChildren();
    /// <summary>Only levels introduced by actual Core structural splits, excluding implicit singleton wrappers.</summary>
    public IReadOnlyList<InspectionNode> VisibleChildren => visibleChildren ??= VisibleContents(this);

    internal InspectionNode(SegmentEntry owner, Hl7Path path, Hl7Element? element, InspectionNode? parent = null)
    { Owner = owner; Path = path; Element = element; Parent = parent; }
    internal InspectionNode(SegmentEntry owner, Hl7Path path, StructureNode structure, InspectionNode? parent = null)
    { Owner = owner; Path = path; Structure = structure; Parent = parent; }

    private static IReadOnlyList<InspectionNode> VisibleContents(InspectionNode node) => node.Structure is { } structure ?
        structure.Kind is StructureKind.Field or StructureKind.Repetition ?
            (structure.Children.Count > 1 ? node.Children : VisibleContents(node.Children[0])) :
        structure.Kind == StructureKind.Component && structure.Children.Count > 1 ? node.Children : [] :
        node.Element switch
    {
        // Core counts actual unescaped splits, so literal MSH-2 and separators inside escapes
        // cannot accidentally create visible children. Empty siblings from real splits survive.
        Hl7Field f => f.Repetitions.Count > 1 ? node.Children : VisibleContents(node.Children[0]),
        Hl7Repetition r => r.Components.Count > 1 ? node.Children : VisibleContents(node.Children[0]),
        Hl7Component c => c.Subcomponents.Count > 1 ? node.Children : [],
        _ => []
    };

    private IReadOnlyList<InspectionNode> CreateChildren() => Structure is { } structure ?
        structure.Children.Select((child, i) => new InspectionNode(Owner, structure.Kind switch
        {
            StructureKind.Field => new Hl7Path(Path.Segment, Path.Field, Path.Occurrence, repetition: i + 1),
            StructureKind.Repetition => new Hl7Path(Path.Segment, Path.Field, Path.Occurrence, Path.Repetition, component: i + 1),
            _ => new Hl7Path(Path.Segment, Path.Field, Path.Occurrence, Path.Repetition, Path.Component, i + 1)
        }, child, this)).ToArray() : Element switch
    {
        Hl7Field field => field.Repetitions.Select((r, i) => new InspectionNode(Owner,
            new(Path.Segment, Path.Field, Path.Occurrence, repetition: i + 1), r, this)).ToArray(),
        Hl7Repetition repetition => repetition.Components.Select((c, i) => new InspectionNode(Owner,
            new(Path.Segment, Path.Field, Path.Occurrence, Path.Repetition, component: i + 1), c, this)).ToArray(),
        Hl7Component component => component.Subcomponents.Select((s, i) => new InspectionNode(Owner,
            new(Path.Segment, Path.Field, Path.Occurrence, Path.Repetition, Path.Component, i + 1), s, this)).ToArray(),
        _ => []
    };
}

public sealed class InspectionSelectionChangedEventArgs(bool navigateRaw) : EventArgs
{
    public bool NavigateRaw { get; } = navigateRaw;
}

/// <summary>Read-only document selection over Core snapshots. Not serialized into workspace layouts.</summary>
public sealed class MessageInspection
{
    private readonly long textLength;
    public DecodedMessageSource? Source { get; }
    private readonly Dictionary<SegmentEntry, IReadOnlyList<InspectionNode>> fieldCache = [];
    public IReadOnlyList<SegmentEntry> Segments { get; }
    public SegmentEntry? SelectedSegment { get; private set; }
    public IReadOnlyList<InspectionNode> Fields { get; private set; } = [];
    public InspectionNode? SelectedNode { get; private set; }
    /// <summary>Session-only bounded value position, retained with structural selection across view reconstruction.</summary>
    public long PreviewOffset { get; set; }
    /// <summary>Visible ancestors to select/reveal. SelectedNode keeps its original canonical identity even when suppressed.</summary>
    public IReadOnlyList<InspectionNode> SelectedVisibleBranch => SelectedNode is { HasContent: true } node ? VisibleBranch(node) : [];
    public InspectionNode? SelectedVisibleNode => SelectedVisibleBranch.LastOrDefault();
    public CharacterRange? SelectedRange64 => SelectedNode is { } node ? node.Range64 : SelectedSegment?.Range64;
    public MessageRange? SelectedRange => SelectedRange64 is { } r ? new(checked((int)r.Offset), checked((int)r.Length)) : null;
    public event EventHandler<InspectionSelectionChangedEventArgs>? SelectionChanged;

    public MessageInspection(Hl7Message message)
    {
        textLength = message.HasDecodedText ? message.RawText.Length : 0;
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        Segments = message.Segments.Select((segment, index) =>
        {
            occurrences.TryGetValue(segment.Identifier, out var occurrence);
            occurrences[segment.Identifier] = ++occurrence;
            return new SegmentEntry(segment, index + 1, occurrence);
        }).ToArray();
        SetSegment(Segments.FirstOrDefault());
    }
    public MessageInspection(Hl7Structure structure, DecodedMessageSource source)
    {
        Source = source;
        textLength = structure.TextLength;
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        Segments = structure.Segments.Select((s, i) =>
        {
            occurrences.TryGetValue(s.Identifier, out var occurrence);
            occurrences[s.Identifier] = ++occurrence;
            return new SegmentEntry(s, i + 1, occurrence);
        }).ToArray();
    }
    public void ClearSelection()
    {
        SetSegment(null);
        SelectionChanged?.Invoke(this, new(false));
    }
    public string ReadPreview(InspectionNode node, long offset = 0, int maximum = 4096)
    {
        if (node.Range64 is not { } range) return string.Empty;
        if (offset < 0 || offset > range.Length || maximum < 2 || maximum > DecodedMessageSource.MaxReadCharacters)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (Source is { } source)
        {
            var start = range.Offset + offset;
            if (source.Align(start) != start) throw new ArgumentOutOfRangeException(nameof(offset), "Offset falls inside a surrogate pair.");
            var end = source.Align(Math.Min(range.End, start + maximum));
            return source.Read(new(start, end - start));
        }
        var raw = node.Raw ?? string.Empty;
        return raw.Substring(checked((int)offset), (int)Math.Min(maximum, raw.Length - offset));
    }


    private void SetSegment(SegmentEntry? segment)
    {
        SelectedSegment = segment;
        SelectedNode = null;
        PreviewOffset = 0;
        Fields = segment is null ? [] : GetFields(segment);
    }

    private IReadOnlyList<InspectionNode> GetFields(SegmentEntry segment)
    {
        if (!fieldCache.TryGetValue(segment, out var fields))
        {
            fields = segment.Structure is { } structure ? structure.Fields.Select(field => new InspectionNode(segment,
                new(segment.Identifier, field.Number, segment.Occurrence), field)).ToArray() :
                segment.Segment.Fields.Select(field => new InspectionNode(segment,
                new(segment.Identifier, field.Number, segment.Occurrence), field)).ToArray();
            fieldCache.Add(segment, fields);
        }
        return fields;
    }

    public void SelectSegment(SegmentEntry segment)
    {
        if (!Segments.Contains(segment)) throw new ArgumentException("Segment belongs to another document.", nameof(segment));
        if (ReferenceEquals(SelectedSegment, segment) && SelectedNode is null) return;
        if (!ReferenceEquals(SelectedSegment, segment)) SetSegment(segment);
        else SelectedNode = null;
        SelectionChanged?.Invoke(this, new(true));
    }

    public void SelectNode(InspectionNode node)
    {
        if (!ReferenceEquals(node.Owner, SelectedSegment)) throw new ArgumentException("Value belongs to another segment.", nameof(node));
        if (ReferenceEquals(SelectedNode, node)) return;
        SelectedNode = node;
        PreviewOffset = 0;
        SelectionChanged?.Invoke(this, new(true));
    }

    private IReadOnlyList<InspectionNode> VisibleBranch(InspectionNode node)
    {
        var canonical = new Stack<InspectionNode>();
        for (var current = node; current is not null; current = current.Parent) canonical.Push(current);
        var branch = new List<InspectionNode>();
        IReadOnlyList<InspectionNode> visible = Fields;
        foreach (var candidate in canonical)
        {
            var match = visible.FirstOrDefault(n => n.Path == candidate.Path);
            if (match is null) continue;
            branch.Add(match);
            visible = match.VisibleChildren;
        }
        return branch;
    }

    /// <summary>Double-click resolution to the deepest visible structural node. Separators select their
    /// containing visible parent; identifiers, field separators, terminators and opaque text select the segment.
    /// Whitespace inside a value belongs to that value. No missing value is synthesized from a raw hit.</summary>
    public void SelectDeepestOffset(long offset)
    {
        var segment = FindSegment(offset);
        if (segment is null) return;
        if (!ReferenceEquals(SelectedSegment, segment)) SetSegment(segment);
        var resolved = ResolveOffset(offset);
        if (!ReferenceEquals(SelectedNode, resolved)) PreviewOffset = 0;
        SelectedNode = resolved;
        // Even an already-selected node must request navigation: a repeated double-click may
        // need to reveal a collapsed branch or replace the editor's native word selection.
        SelectionChanged?.Invoke(this, new(true));
    }

    /// <summary>
    /// Resolves a raw character to its deepest meaningful structural node without changing
    /// selection. Uses Core ranges and cached projections, with no reparsing or UI tree rebuild.
    /// Structural separators resolve to their containing node. Field separators (except MSH-1),
    /// identifiers, terminators, empty/missing values and opaque text have no value character.
    /// Whitespace inside an actual value retains that value's path.
    /// </summary>
    public InspectionNode? ResolveOffset(long offset)
    {
        var segment = FindSegment(offset);
        if (segment is null || !Contains(segment.Range64, offset)) return null;
        var node = NodeAt(GetFields(segment), offset);
        while (node is not null)
        {
            var child = NodeAt(node.VisibleChildren, offset);
            if (child is null) break;
            node = child;
        }
        return node;
    }

    private static bool Contains(CharacterRange? range, long offset) => range is { } r && offset >= r.Offset && offset < r.Offset + r.Length;

    private static InspectionNode? NodeAt(IReadOnlyList<InspectionNode> nodes, long offset)
    {
        var low = 0;
        var high = nodes.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (nodes[middle].Range64!.Value.Offset <= offset) low = middle + 1;
            else high = middle;
        }
        return low > 0 && Contains(nodes[low - 1].Range64, offset) ? nodes[low - 1] : null;
    }

    private SegmentEntry? FindSegment(long offset)
    {
        if (offset < 0 || offset > textLength || Segments.Count == 0) return null;
        var low = 0;
        var high = Segments.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (Segments[middle].Range64.Offset <= offset) low = middle + 1;
            else high = middle;
        }
        return Segments[Math.Max(0, low - 1)];
    }

    /// <summary>Looks up a field number without assuming how many fields a version/profile requires.</summary>
    public InspectionNode? InspectField(int number)
    {
        if (number < 1) throw new ArgumentOutOfRangeException(nameof(number));
        if (SelectedSegment is not { HasFieldPaths: true } segment) return null;
        if (SelectedNode is { HasContent: false } missing && missing.Path.Field == number) return missing;
        var node = Fields.FirstOrDefault(f => f.Path.Field == number) ?? new InspectionNode(segment,
            new(segment.Identifier, number, segment.Occurrence), (Hl7Element?)null);
        SelectNode(node);
        return node;
    }

    /// <summary>Updates context from a raw caret without replacing the user's raw text selection.</summary>
    public void SelectOffset(long offset)
    {
        var segment = FindSegment(offset);
        if (segment is null) return;
        var changed = !ReferenceEquals(SelectedSegment, segment);
        if (changed) SetSegment(segment);
        // Keep a selected nested value when its caret is inside that value. Terminators and
        // separators otherwise select segment context rather than inventing a field range.
        if (!changed && SelectedNode?.Range64 is { } selected && offset >= selected.Offset &&
            (offset < selected.Offset + selected.Length || selected.Length == 0 && offset == selected.Offset)) return;
        var field = Fields.FirstOrDefault(f => f.Range64 is { } range && offset >= range.Offset &&
            (offset < range.Offset + range.Length || range.Length == 0 && offset == range.Offset));
        if (!changed && ReferenceEquals(SelectedNode, field)) return;
        SelectedNode = field;
        PreviewOffset = 0;
        SelectionChanged?.Invoke(this, new(false));
    }
}
