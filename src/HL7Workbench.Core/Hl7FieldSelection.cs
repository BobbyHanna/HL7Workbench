using System.Globalization;
using System.Text;

namespace HL7Workbench.Core;

public enum Hl7MissingKind { None, Segment, Field, Repetition, Component, Subcomponent, Alignment }
public enum Hl7ExtractionStatus { Complete, DecodeDiagnostic, ValueLimit, Unstructured, Unreadable }

/// <summary>A complete selected value, or an explicit missing/unavailable state. Never a presentation preview.</summary>
public sealed record Hl7ExtractedValue(Hl7ValueState State, Hl7MissingKind Missing, Hl7ExtractionStatus Status, string Text, string Path)
{
    public string StateLabel => State == Hl7ValueState.Missing ? Missing == Hl7MissingKind.None ? "Unavailable" : "Missing" + Missing : State.ToString();
    public override string ToString() => "Extracted HL7 value";
}

/// <summary>Core-validated path plus explicit-index intent, which Hl7Path's defaulted coordinates alone cannot retain.</summary>
public sealed class Hl7FieldSelection
{
    public Hl7Path Path { get; }
    public bool ExplicitOccurrence { get; }
    public bool ExplicitRepetition { get; }
    public string CanonicalPath { get; }
    private Hl7FieldSelection(Hl7Path path, bool occurrence, bool repetition)
    {
        Path = path; ExplicitOccurrence = occurrence; ExplicitRepetition = repetition;
        string N(int n) => n.ToString(CultureInfo.InvariantCulture);
        CanonicalPath = path.Segment + (occurrence ? "[" + N(path.Occurrence) + "]" : "") + "-" + N(path.Field) +
            (repetition ? "[" + N(path.Repetition!.Value) + "]" : "") +
            (path.Component is { } c ? "." + N(c) : "") + (path.Subcomponent is { } s ? "." + N(s) : "");
    }
    public static Hl7FieldSelection Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        text = text.Trim(); var path = Hl7Path.Parse(text);
        // Grammar and numbers are validated exclusively by Hl7Path. These two flags
        // retain user intent (including [1]) when expansion uses omitted indexes.
        return new(path, text[3] == '[', text.IndexOf('[', text.IndexOf('-') + 1) >= 0);
    }
    /// <summary>Selects bounded structural addresses, without reading or decoding their contents.</summary>
    public Hl7FieldMatches Select(Hl7Structure structure, bool expand, int maximumMatches = 4096, CancellationToken token = default)
    {
        if (maximumMatches < 1) throw new ArgumentOutOfRangeException(nameof(maximumMatches));
        var matches = new List<Hl7FieldMatch>(); var truncated = false; var occurrence = 0;
        void Add(StructureNode? node, Hl7MissingKind missing, int segment, int repetition, Hl7ExtractionStatus status = Hl7ExtractionStatus.Complete)
        {
            if (matches.Count == maximumMatches) { truncated = true; return; }
            matches.Add(new(new(Path.Segment, Path.Field, segment, repetition, Path.Component, Path.Subcomponent), node, missing, status));
        }
        foreach (var segment in structure.Segments.Where(s => s.Identifier == Path.Segment))
        {
            token.ThrowIfCancellationRequested(); occurrence++;
            if ((!expand || ExplicitOccurrence) && occurrence != Path.Occurrence) continue;
            var field = segment.Fields.FirstOrDefault(f => f.Number == Path.Field);
            if (field is null)
                Add(null, Hl7MissingKind.Field, occurrence, Path.Repetition ?? 1,
                    segment.Fields.Count == 0 && segment.Range.Length > 3 ? Hl7ExtractionStatus.Unstructured : Hl7ExtractionStatus.Complete);
            else
            {
                // MSH-1/2 are literal fields; their encoding characters are not a repeated composite.
                var repetitions = field.Children.Count == 0 ? new[] { field } : field.Children;
                var selected = repetitions.Where(r => expand && !ExplicitRepetition || r.Number == (Path.Repetition ?? 1)).ToArray();
                if (field.Children.Count == 0) selected = (Path.Repetition ?? 1) == 1 ? [field] : [];
                if (selected.Length == 0) Add(null, Hl7MissingKind.Repetition, occurrence, Path.Repetition ?? 1);
                foreach (var repetition in selected)
                {
                    token.ThrowIfCancellationRequested();
                    var number = field.Children.Count == 0 ? 1 : repetition.Number;
                    var node = repetition; var missing = Hl7MissingKind.None;
                    if (Path.Component is { } component)
                    { node = node.Children.FirstOrDefault(n => n.Number == component); if (node is null) missing = Hl7MissingKind.Component; }
                    if (node is not null && Path.Subcomponent is { } subcomponent)
                    { node = node.Children.FirstOrDefault(n => n.Number == subcomponent); if (node is null) missing = Hl7MissingKind.Subcomponent; }
                    Add(node, missing, occurrence, number, segment.CanEdit ? Hl7ExtractionStatus.Complete : Hl7ExtractionStatus.Unstructured);
                    if (truncated) break;
                }
            }
            if (truncated || !expand || ExplicitOccurrence) break;
        }
        if (matches.Count == 0) Add(null, Hl7MissingKind.Segment, Path.Occurrence, Path.Repetition ?? 1);
        return new(matches.AsReadOnly(), truncated);
    }
}

public sealed record Hl7FieldMatches(IReadOnlyList<Hl7FieldMatch> Matches, bool Truncated);

/// <summary>A selected source address. Reading is explicit and length-bounded; omission markers never become values.</summary>
public sealed record Hl7FieldMatch(Hl7Path Path, StructureNode? Node, Hl7MissingKind Missing, Hl7ExtractionStatus Status)
{
    public Hl7ExtractedValue Read(IHl7TextSource source, Hl7Structure structure, Encoding encoding, bool decode = true, int maximumCharacters = 65536)
    {
        if (maximumCharacters < 1) throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        if (Node is null) return new(Hl7ValueState.Missing, Missing, Status, "", Path.ToString());
        if (Node.Range.Length > maximumCharacters) return new(Node.State, Missing, Hl7ExtractionStatus.ValueLimit, "", Path.ToString());
        if (Node.State is Hl7ValueState.Empty or Hl7ValueState.ExplicitNull) return new(Node.State, Missing, Status, "", Path.ToString());
        var raw = source.Read(Node.Range);
        try { _ = new UTF8Encoding(false, true).GetByteCount(raw); }
        catch (EncoderFallbackException) { return new(Node.State, Missing, Hl7ExtractionStatus.Unreadable, "", Path.ToString()); }
        if (!decode || Path.Segment == "MSH" && Path.Field <= 2) return new(Node.State, Missing, Status, raw, Path.ToString());
        try
        {
            var decoded = Hl7EscapeCodec.Decode(raw, structure.Delimiters, encoding, maximumCharacters);
            return new(Node.State, Missing, decoded.Diagnostics.Count == 0 ? Status : Hl7ExtractionStatus.DecodeDiagnostic, decoded.Text, Path.ToString());
        }
        catch (Hl7LimitException) { return new(Node.State, Missing, Hl7ExtractionStatus.ValueLimit, "", Path.ToString()); }
    }
}
