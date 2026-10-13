using System.Collections.ObjectModel;
using System.Text;

namespace HL7Workbench.Core;

public sealed class Hl7Message
{
    private readonly Hl7ParseOptions options;
    private readonly Encoding encoding;
    private readonly byte[] bom;
    private readonly byte[]? originalBytes;
    private byte[]? currentBytes;
    private string text;
    public ReadOnlyCollection<Hl7Segment> Segments { get; private set; }
    public ReadOnlyCollection<Hl7Diagnostic> Diagnostics { get; private set; }
    public Hl7Delimiters Delimiters { get; private set; }
    public Encoding Encoding => (Encoding)encoding.Clone();
    public byte[] ByteOrderMark => (byte[])bom.Clone();
    public byte[]? OriginalBytes => originalBytes is null ? null : (byte[])originalBytes.Clone();
    public bool HasOriginalBytes => originalBytes is not null;
    public bool HasDecodedText => !Diagnostics.Any(d => d.Code == "InvalidEncoding");
    public bool CanEdit { get; private set; }
    public bool IsModified { get; private set; }
    public string RawText => HasDecodedText ? text : throw new InvalidOperationException("Input has no safely decoded text; use byte serialization.");

    internal Hl7Message(string text, Hl7ParseOptions options, Encoding encoding, byte[] bom, byte[]? bytes,
        List<Hl7Segment> segments, Hl7Delimiters delimiters, List<Hl7Diagnostic> diagnostics, bool canEdit)
    {
        this.text = text;
        this.options = options;
        this.encoding = encoding;
        this.bom = bom;
        originalBytes = bytes;
        currentBytes = bytes;
        Segments = segments.AsReadOnly();
        Diagnostics = diagnostics.AsReadOnly();
        Delimiters = delimiters;
        CanEdit = canEdit && (encoding.IsSingleByte || encoding.CodePage is 65001 or 1200 or 1201);
    }

    public Hl7Value Get(string path) => Get(Hl7Path.Parse(path));
    public Hl7Value Get(Hl7Path path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var field = FindSegment(path)?.GetField(path.Field);
        if (field is null) return new(null);
        if (path.Repetition is null) return field.Value;
        var repetition = At(field.Repetitions, path.Repetition.Value);
        if (repetition is null) return new(null);
        if (path.Component is null) return repetition.Value;
        var component = At(repetition.Components, path.Component.Value);
        if (component is null) return new(null);
        if (path.Subcomponent is null) return component.Value;
        return At(component.Subcomponents, path.Subcomponent.Value)?.Value ?? new(null);
    }

    public Hl7EscapeResult GetDecoded(string path) => GetDecoded(Hl7Path.Parse(path));
    public Hl7EscapeResult GetDecoded(Hl7Path parsed)
    {
        ArgumentNullException.ThrowIfNull(parsed);
        var raw = Get(parsed).Raw ?? throw new KeyNotFoundException("HL7 value is missing.");
        if (parsed.Segment == "MSH" && parsed.Field <= 2)
            return new(raw, new List<Hl7Diagnostic>().AsReadOnly());
        return Hl7EscapeCodec.Decode(raw, Delimiters, encoding, options.MaxMessageCharacters, options.MaxDiagnostics);
    }

    public void SetText(string path, string value) => SetText(Hl7Path.Parse(path), value);
    public void SetText(Hl7Path path, string value) => Write(path, value, true);
    public void SetNull(string path) => SetNull(Hl7Path.Parse(path));
    public void SetNull(Hl7Path path) => SetRaw(path, "\"\"");
    public void SetRaw(string path, string value) => SetRaw(Hl7Path.Parse(path), value);
    public void SetRaw(Hl7Path path, string value) => Write(path, value, false);

    private void Write(Hl7Path path, string value, bool literal)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(value);
        var literalValue = literal ? value : null;
        if (!CanEdit) throw new InvalidOperationException("Editing is disabled for this message or encoding.");
        if (path.Segment == "MSH" && path.Field <= 2)
            throw new InvalidOperationException("MSH-1 and MSH-2 require explicit delimiter conversion.");
        var segment = FindSegment(path) ?? throw new KeyNotFoundException("Requested segment occurrence is missing.");
        if (!segment.CanEdit) throw new InvalidOperationException("Malformed or ambiguous segments cannot be edited.");
        Hl7Parser.Check(value.Length, options.MaxMessageCharacters, nameof(options.MaxMessageCharacters));
        if (!literal && Get(path).Raw == value) return;

        // Only offsets and at most four separator/count pairs are allocated here.
        var plan = PlanEdit(segment, path);
        var size = literal ? Hl7EscapeCodec.MeasureEncoded(value, Delimiters, encoding) :
            ((long)value.Length, (long)encoding.GetByteCount(value));
        long paddingCharacters = 0, paddingBytes = 0;
        foreach (var (separator, count) in plan.Padding)
        {
            paddingCharacters += count;
            paddingBytes += (long)count * Hl7EscapeCodec.CharacterBytes(separator, encoding);
        }
        var insertedCharacters = paddingCharacters + size.Item1;
        Hl7Parser.Check((long)text.Length - plan.Removed + insertedCharacters, options.MaxMessageCharacters, nameof(options.MaxMessageCharacters));
        Hl7Parser.Check((long)segment.Raw.Length - plan.Removed + insertedCharacters, options.MaxSegmentLength, nameof(options.MaxSegmentLength));
        var currentByteCount = currentBytes?.Length ?? (long)encoding.GetByteCount(text);
        var removedByteCount = encoding.GetByteCount(text.AsSpan(plan.Start, plan.Removed));
        Hl7Parser.Check(currentByteCount - removedByteCount + paddingBytes + size.Item2, options.MaxMessageBytes, nameof(options.MaxMessageBytes));

        // Replacement encoding and padding construction happen only after preflight.
        if (literal) value = Hl7EscapeCodec.Encode(value, Delimiters, options.MaxMessageCharacters, encoding);
        else
        {
            var forbidden = new List<char> { Delimiters.Field };
            if (path.Repetition is not null) forbidden.Add(Delimiters.Repetition);
            if (path.Component is not null) forbidden.Add(Delimiters.Component);
            if (path.Subcomponent is not null && Delimiters.Subcomponent is { } sub) forbidden.Add(sub);
            if (Hl7Tokenizer.ContainsUnescaped(value, forbidden, Delimiters.Escape))
                throw new ArgumentException("Raw value contains a separator outside the selected element; use SetText for literal text.", nameof(value));
            if (Delimiters.Escape is { } escape && value.Count(c => c == escape) % 2 != 0)
                throw new ArgumentException("Raw edits require closed escape sequences.", nameof(value));
        }
        if (Get(path).Raw == value) return;
        string inserted;
        if (plan.Padding.Count == 0) inserted = value;
        else
        {
            var builder = new StringBuilder((int)insertedCharacters);
            foreach (var (separator, count) in plan.Padding) builder.Append(separator, count);
            inserted = builder.Append(value).ToString();
        }
        var start = plan.Start;
        var removed = plan.Removed;
        var old = text.AsSpan(start, removed);
        var prefix = 0;
        while (prefix < old.Length && prefix < inserted.Length && old[prefix] == inserted[prefix]) prefix++;
        if (prefix > 0 && prefix < old.Length && char.IsHighSurrogate(old[prefix - 1]) && char.IsLowSurrogate(old[prefix])) prefix--;
        var suffix = 0;
        while (suffix < old.Length - prefix && suffix < inserted.Length - prefix &&
            old[old.Length - suffix - 1] == inserted[inserted.Length - suffix - 1]) suffix++;
        var suffixStart = old.Length - suffix;
        if (suffix > 0 && suffixStart > 0 && char.IsLowSurrogate(old[suffixStart]) && char.IsHighSurrogate(old[suffixStart - 1])) suffix--;
        start += prefix;
        removed -= prefix + suffix;
        inserted = inserted.Substring(prefix, inserted.Length - prefix - suffix);
        var candidateText = string.Concat(text.AsSpan(0, start), inserted, text.AsSpan(start + removed));
        var candidate = Hl7Parser.ParseText(candidateText, options, encoding, bom, null, true);
        var candidateSegment = candidate.FindSegment(path);
        if (!candidate.CanEdit || candidateSegment is null || !candidateSegment.CanEdit || candidate.Get(path).Raw != value ||
            candidateSegment.Fields.Count != Math.Max(segment.Fields.Count, path.Field) ||
            segment.Fields.Any(field => field.Number != path.Field && candidateSegment.GetField(field.Number)?.Raw != field.Raw))
            throw new InvalidOperationException("Edit would reinterpret existing malformed escapes or neighboring fields.");
        CheckNestedSiblings(segment.GetField(path.Field), candidateSegment.GetField(path.Field)!, path);
        if (literal && candidate.GetDecoded(path).Text != literalValue)
            throw new InvalidOperationException("Declared delimiters cannot represent this literal text unambiguously.");
        byte[]? candidateBytes = null;
        if (currentBytes is not null)
        {
            var prefixLength = bom.Length + encoding.GetByteCount(text.AsSpan(0, start));
            var replacedBytes = encoding.GetByteCount(text.AsSpan(start, removed));
            var replacement = encoding.GetBytes(inserted);
            var resultLength = (long)currentBytes.Length - replacedBytes + replacement.Length;
            Hl7Parser.Check(resultLength, options.MaxMessageBytes, nameof(options.MaxMessageBytes));
            candidateBytes = new byte[(int)resultLength];
            currentBytes.AsSpan(0, prefixLength).CopyTo(candidateBytes);
            replacement.CopyTo(candidateBytes, prefixLength);
            currentBytes.AsSpan(prefixLength + replacedBytes).CopyTo(candidateBytes.AsSpan(prefixLength + replacement.Length));
        }
        text = candidateText;
        currentBytes = candidateBytes;
        Segments = candidate.Segments;
        Diagnostics = candidate.Diagnostics;
        Delimiters = candidate.Delimiters;
        CanEdit = candidate.CanEdit;
        IsModified = true;
    }

    private sealed class EditPlan
    {
        internal int Start;
        internal int Removed;
        internal List<(char Separator, int Count)> Padding { get; } = [];
    }

    private EditPlan PlanEdit(Hl7Segment segment, Hl7Path path)
    {
        var plan = new EditPlan();
        long expansion = 0;
        T? Select<T>(Hl7Element? parent, IReadOnlyList<T> children, int index, char? separator, bool field = false) where T : Hl7Element
        {
            Hl7Parser.Check(index, options.MaxElementsPerContainer, nameof(options.MaxElementsPerContainer));
            expansion += Math.Max(0, index - children.Count);
            Hl7Parser.Check(expansion, options.MaxEditExpansion, nameof(options.MaxEditExpansion));
            var child = At(children, index);
            if (child is not null) { plan.Start = child.Start; plan.Removed = child.Raw.Length; return child; }
            if (parent is not null) { plan.Start = parent.Start + parent.Raw.Length; plan.Removed = 0; }
            var padding = children.Count == 0 && !field ? index - 1 : index - children.Count;
            if (padding > 0)
            {
                if (separator is null) throw new InvalidOperationException("Cannot expand undeclared delimiter levels.");
                plan.Padding.Add((separator.Value, padding));
            }
            return null;
        }
        var field = Select(segment, segment.Fields, path.Field, Delimiters.Field, true);
        if (path.Repetition is not { } r) return plan;
        var repetition = Select(field, field?.Repetitions ?? (IReadOnlyList<Hl7Repetition>)Array.Empty<Hl7Repetition>(), r, Delimiters.Repetition);
        if (path.Component is not { } c) return plan;
        var component = Select(repetition, repetition?.Components ?? (IReadOnlyList<Hl7Component>)Array.Empty<Hl7Component>(), c, Delimiters.Component);
        if (path.Subcomponent is { } s)
            Select(component, component?.Subcomponents ?? (IReadOnlyList<Hl7Subcomponent>)Array.Empty<Hl7Subcomponent>(), s, Delimiters.Subcomponent);
        return plan;
    }

    private static void CheckNestedSiblings(Hl7Field? before, Hl7Field after, Hl7Path path)
    {
        if (path.Repetition is not { } r) return;
        var oldRepetitions = before?.Repetitions ?? (IReadOnlyList<Hl7Repetition>)Array.Empty<Hl7Repetition>();
        EnsureSiblings(oldRepetitions, after.Repetitions, r);
        if (path.Component is not { } c) return;
        var oldComponents = At(oldRepetitions, r)?.Components ?? (IReadOnlyList<Hl7Component>)Array.Empty<Hl7Component>();
        var newRepetition = At(after.Repetitions, r)!;
        EnsureSiblings(oldComponents, newRepetition.Components, c);
        if (path.Subcomponent is { } s)
            EnsureSiblings(At(oldComponents, c)?.Subcomponents ?? (IReadOnlyList<Hl7Subcomponent>)Array.Empty<Hl7Subcomponent>(),
                At(newRepetition.Components, c)!.Subcomponents, s);
    }

    private Hl7Segment? FindSegment(Hl7Path path)
    {
        var occurrence = 0;
        foreach (var segment in Segments)
            if (segment.Identifier == path.Segment && ++occurrence == path.Occurrence) return segment;
        return null;
    }
    private static T? At<T>(IReadOnlyList<T> values, int index) where T : class =>
        index <= values.Count ? values[index - 1] : null;

    private static void EnsureSiblings<T>(IReadOnlyList<T> before, IReadOnlyList<T> after, int selected) where T : Hl7Element
    {
        if (after.Count != Math.Max(before.Count, selected))
            throw new InvalidOperationException("Edit would reinterpret neighboring elements.");
        for (var i = 0; i < after.Count; i++)
            if (i != selected - 1 && after[i].Raw != (i < before.Count ? before[i].Raw : ""))
                throw new InvalidOperationException("Edit would reinterpret neighboring elements.");
    }

    internal byte[] SerializeBytes() => currentBytes is not null ? (byte[])currentBytes.Clone() : encoding.GetBytes(text);
}
