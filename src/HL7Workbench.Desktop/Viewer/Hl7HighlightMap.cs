using HL7Workbench.Core;

namespace HL7Workbench.Desktop.Viewer;

public enum Hl7HighlightKind { Segment, Field, Component, Repetition, Subcomponent, Escape }
public sealed record Hl7HighlightSpan(int Offset, int Length, Hl7HighlightKind Kind);

/// <summary>Presentation spans derived from Core coordinates, independent of any version dictionary.</summary>
public sealed class Hl7HighlightMap
{
    public IReadOnlyList<Hl7HighlightSpan> Spans { get; }

    public Hl7HighlightMap(Hl7Message message)
    {
        var spans = new List<Hl7HighlightSpan>();
        void Add(int offset, int length, Hl7HighlightKind kind) { if (length > 0) spans.Add(new(offset, length, kind)); }
        void Separators<T>(IReadOnlyList<T> children, Hl7HighlightKind kind) where T : Hl7Element
        {
            foreach (var child in children.Skip(1)) Add(child.CharacterOffset - 1, 1, kind);
        }
        foreach (var segment in message.Segments)
        {
            // Opaque malformed lines have no structural coordinates to promise to the user.
            if (segment.Fields.Count == 0 && !segment.CanEdit) continue;
            Add(segment.CharacterOffset, segment.Identifier.Length, Hl7HighlightKind.Segment);
            if (segment.Raw.Length > 3) Add(segment.CharacterOffset + 3, 1, Hl7HighlightKind.Field);
            foreach (var field in segment.Fields)
            {
                if (segment.Identifier == "MSH" && field.Number <= 2)
                {
                    if (field.Number == 2)
                    {
                        var kinds = new[] { Hl7HighlightKind.Component, Hl7HighlightKind.Repetition, Hl7HighlightKind.Escape, Hl7HighlightKind.Subcomponent };
                        for (var i = 0; i < Math.Min(field.Raw.Length, kinds.Length); i++) Add(field.CharacterOffset + i, 1, kinds[i]);
                    }
                    continue;
                }
                if (field.Number > 1) Add(field.CharacterOffset - 1, 1, Hl7HighlightKind.Field);
                Separators(field.Repetitions, Hl7HighlightKind.Repetition);
                foreach (var repetition in field.Repetitions)
                {
                    Separators(repetition.Components, Hl7HighlightKind.Component);
                    foreach (var component in repetition.Components) Separators(component.Subcomponents, Hl7HighlightKind.Subcomponent);
                }
                // Core already decided which delimiters are structural. Escape markers are visual
                // accents only; delimiters inside paired escapes remain ordinary values.
                if (message.Delimiters.Escape is { } escape)
                    for (var i = 0; i < field.Raw.Length; i++)
                        if (field.Raw[i] == escape) Add(field.CharacterOffset + i, 1, Hl7HighlightKind.Escape);
            }
        }
        Spans = spans.OrderBy(s => s.Offset).ToArray();
    }

    /// <summary>Only spans intersecting the visible line; binary lookup avoids rescanning large files when scrolling.</summary>
    public IEnumerable<Hl7HighlightSpan> Intersecting(int offset, int length)
    {
        var low = 0;
        var high = Spans.Count;
        while (low < high)
        {
            var middle = low + (high - low) / 2;
            if (Spans[middle].Offset + Spans[middle].Length <= offset) low = middle + 1;
            else high = middle;
        }
        for (var i = low; i < Spans.Count && Spans[i].Offset < offset + length; i++) yield return Spans[i];
    }
}
