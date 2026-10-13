using System.Globalization;

namespace HL7Workbench.Core;

/// <summary>One-based ER7 coordinate. Omitted occurrence/repetition means 1.</summary>
public sealed record Hl7Path
{
    public string Segment { get; }
    public int Occurrence { get; }
    public int Field { get; }
    public int? Repetition { get; }
    public int? Component { get; }
    public int? Subcomponent { get; }

    public Hl7Path(string segment, int field, int occurrence = 1, int? repetition = null,
        int? component = null, int? subcomponent = null)
    {
        if (segment is null || segment.Length != 3 || !segment.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9'))
            throw new ArgumentException("Segment must contain three uppercase ASCII letters/digits.", nameof(segment));
        if (field < 1 || occurrence < 1 || repetition < 1 || component < 1 || subcomponent < 1)
            throw new ArgumentOutOfRangeException(nameof(field), "All indices are one-based.");
        if (subcomponent is not null && component is null)
            throw new ArgumentException("A subcomponent requires a component.");
        Segment = segment;
        Field = field;
        Occurrence = occurrence;
        Repetition = repetition ?? (component is not null ? 1 : null);
        Component = component;
        Subcomponent = subcomponent;
    }

    public static Hl7Path Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length is < 5 or > 64) throw new FormatException("Invalid HL7 path length.");
        var position = 3;
        var segment = text[..3];
        int ReadNumber()
        {
            var start = position;
            while (position < text.Length && text[position] is >= '0' and <= '9') position++;
            if (start == position || !int.TryParse(text.AsSpan(start, position - start), NumberStyles.None,
                    CultureInfo.InvariantCulture, out var value) || value < 1)
                throw new FormatException("Path indices must be positive integers.");
            return value;
        }
        int? ReadBracket()
        {
            if (position >= text.Length || text[position] != '[') return null;
            position++;
            var value = ReadNumber();
            if (position >= text.Length || text[position++] != ']') throw new FormatException("Missing closing bracket.");
            return value;
        }
        var occurrence = ReadBracket() ?? 1;
        if (position >= text.Length || text[position++] != '-') throw new FormatException("Missing field separator in path.");
        var field = ReadNumber();
        var repetition = ReadBracket();
        int? component = null, subcomponent = null;
        if (position < text.Length && text[position] == '.') { position++; component = ReadNumber(); }
        if (position < text.Length && text[position] == '.') { position++; subcomponent = ReadNumber(); }
        if (position != text.Length) throw new FormatException("Unexpected path suffix.");
        try { return new Hl7Path(segment, field, occurrence, repetition, component, subcomponent); }
        catch (ArgumentException) { throw new FormatException("Invalid segment identifier in path."); }
    }

    public override string ToString() => $"{Segment}[{Occurrence}]-{Field}" +
        (Repetition is { } r ? $"[{r}]" : "") + (Component is { } c ? $".{c}" : "") +
        (Subcomponent is { } s ? $".{s}" : "");
}
