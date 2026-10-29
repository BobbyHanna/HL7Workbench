namespace HL7Workbench.Core;

public sealed record Hl7ValidationLocation(CharacterRange Range, bool IsBoundary, Hl7ValueState State, StructureNode? Node);

/// <summary>Exact message-local UTF-16 coordinates, including zero-width anchors. No preview text is interpreted as source.</summary>
public static class Hl7ValidationSourceMap
{
    public static Hl7ValidationLocation Locate(Hl7Structure structure, Hl7Path path)
    {
        var segment = structure.Segments.Where(s => s.Identifier == path.Segment).Skip(path.Occurrence - 1).FirstOrDefault();
        return Locate(segment, structure.TextLength, path);
    }
    internal static Hl7ValidationLocation Locate(StructureSegment? segment, long textLength, Hl7Path path)
    {
        if (segment is null) return new(new(textLength, 0), true, Hl7ValueState.Missing, null);
        var node = segment.Fields.FirstOrDefault(f => f.Number == path.Field); var boundary = segment.Range.End;
        if (node is not null)
        {
            foreach (var position in new[] { path.Repetition, path.Component, path.Subcomponent })
            {
                if (position is null) break;
                boundary = node.Range.End; node = node.Children.FirstOrDefault(c => c.Number == position);
                if (node is null) break;
            }
        }
        return node is null ? new(new(boundary, 0), true, Hl7ValueState.Missing, null) : new(node.Range, node.Range.Length == 0, node.State, node);
    }
    internal static Hl7Structure FromMessage(Hl7Message message)
    {
        StructureNode Node(Hl7Element e, StructureKind kind, int number, IEnumerable<Hl7Element> children) =>
            new(kind, number, new(e.CharacterOffset, e.Raw.Length), null, e.Value.State, Array.AsReadOnly(children.Select((child, i) => kind switch
            {
                StructureKind.Field => Node(child, StructureKind.Repetition, i + 1, ((Hl7Repetition)child).Components),
                StructureKind.Repetition => Node(child, StructureKind.Component, i + 1, ((Hl7Component)child).Subcomponents),
                _ => Node(child, StructureKind.Subcomponent, i + 1, [])
            }).ToArray()));
        return new(message.RawText.Length, message.Delimiters, Array.AsReadOnly(message.Segments.Select(s => new StructureSegment(s.Identifier,
            new(s.CharacterOffset, s.Raw.Length), null, s.Terminator, Array.AsReadOnly(s.Fields.Select(f => Node(f, StructureKind.Field, f.Number, f.Repetitions)).ToArray()), s.CanEdit)).ToArray()),
            Array.AsReadOnly(message.Diagnostics.Select(d => new StructureDiagnostic(d.Code, d.Severity, d.Description, d.CharacterOffset, d.Path)).ToArray()), message.CanEdit);
    }
}
