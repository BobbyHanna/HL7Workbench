using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Scripting;

public enum ValueState { Missing, Empty, ExplicitNull, Value }
public sealed record ScriptValue(ValueState State, string? Raw);

/// <summary>One invocation's bounded message. Paths are Core's one-based coordinates; no UI or source sessions escape.</summary>
public sealed class ScriptMessage
{
    private Hl7Message message;
    private readonly CancellationToken cancellation;
    internal string? LastPath { get; private set; }
    internal bool Excluded { get; private set; }
    internal ScriptMessage(Hl7Message message, CancellationToken cancellation) { this.message = message; this.cancellation = cancellation; }

    public bool CanEdit => message.CanEdit;
    /// <summary>Whether Core reported any structural/encoding diagnostic. Conservative scripts can refuse malformed input before editing.</summary>
    public bool HasDiagnostics => message.Diagnostics.Count != 0;
    internal byte[] Serialize() => Hl7Serializer.Serialize(message);
    internal Hl7Message Model => message;
    internal sealed record LiteralEdit(int Offset, int Length, string Text);
    /// <summary>Workflow-only batch of literal leaf replacements from one frozen Core model.
    /// Copies untouched encoded spans and validates once before replacing this facade's model.</summary>
    internal void ApplyLiteralEdits(IReadOnlyList<LiteralEdit> edits)
    {
        cancellation.ThrowIfCancellationRequested(); LastPath = null;
        if (!CanEdit || HasDiagnostics) throw Failure("EditRejected");
        if (edits.Count == 0) return;
        var original = Serialize(); var encoding = message.Encoding; var text = message.RawText;
        var byteCursor = message.ByteOrderMark.Length; var charCursor = 0;
        using var output = new MemoryStream(); output.Write(original.AsSpan(0, byteCursor));
        foreach (var edit in edits.OrderBy(e => e.Offset))
        {
            cancellation.ThrowIfCancellationRequested();
            if (edit.Offset < charCursor || edit.Length < 0 || edit.Offset > text.Length - edit.Length) throw Failure("EditRejected");
            var untouched = encoding.GetByteCount(text.AsSpan(charCursor, edit.Offset - charCursor));
            output.Write(original.AsSpan(byteCursor, untouched)); byteCursor += untouched;
            byteCursor += encoding.GetByteCount(text.AsSpan(edit.Offset, edit.Length)); charCursor = edit.Offset + edit.Length;
            var escaped = Hl7EscapeCodec.Encode(edit.Text, message.Delimiters, ScriptLimits.MessageCharacters, encoding);
            var bytes = encoding.GetBytes(escaped); output.Write(bytes);
            if (output.Length > ScriptLimits.MessageBytes) throw Failure("InputLimit");
        }
        output.Write(original.AsSpan(byteCursor));
        if (output.Length > ScriptLimits.MessageBytes) throw Failure("InputLimit");
        var candidate = Parse(output.ToArray(), encoding.CodePage);
        if (!candidate.CanEdit || candidate.Diagnostics.Count != 0) throw Failure("EditRejected");
        cancellation.ThrowIfCancellationRequested(); message = candidate;
    }
    public ScriptValue Read(string path)
    {
        var value = message.Get(ParsePath(path));
        return new((ValueState)value.State, value.Raw);
    }
    /// <summary>Explicit decoded text. Missing/null values and unresolved escapes are errors, never normalized values.</summary>
    public string Text(string path)
    {
        var parsed = ParsePath(path); var value = message.Get(parsed);
        if (value.State is Hl7ValueState.Missing or Hl7ValueState.ExplicitNull) throw Failure("MissingValue");
        var decoded = message.GetDecoded(parsed);
        if (decoded.Diagnostics.Count != 0) throw Failure("DecodeDiagnostic");
        return decoded.Text;
    }
    public void SetText(string path, string value) => Edit(path, p => message.SetText(p, value));
    public void SetRaw(string path, string value) => Edit(path, p => message.SetRaw(p, value));
    public void SetNull(string path) => Edit(path, message.SetNull);
    /// <summary>Clears an existing value without removing its position. Missing targets are no-ops.</summary>
    public void Clear(string path)
    {
        var parsed = ParsePath(path);
        if (message.Get(parsed).State != Hl7ValueState.Missing) Edit(path, p => message.SetRaw(p, ""));
    }
    public void Exclude() { cancellation.ThrowIfCancellationRequested(); Excluded = true; }
    public IReadOnlyList<ScriptSegment> Segments(string identifier)
    {
        cancellation.ThrowIfCancellationRequested();
        _ = new Hl7Path(identifier, 1);
        return Enumerable.Range(1, message.Segments.Count(s => s.Identifier == identifier))
            .Select(n => new ScriptSegment(this, identifier, n)).ToArray();
    }
    private Hl7Path ParsePath(string path)
    {
        cancellation.ThrowIfCancellationRequested(); LastPath = null;
        try { var parsed = Hl7Path.Parse(path); LastPath = parsed.ToString(); return parsed; }
        catch (Exception e) when (e is ArgumentException or FormatException) { throw Failure("InvalidPath"); }
    }
    private void Edit(string path, Action<Hl7Path> edit)
    {
        var parsed = ParsePath(path);
        try { edit(parsed); }
        catch (Hl7LimitException) { throw Failure("InputLimit"); }
        catch (EncoderFallbackException) { throw Failure("InvalidEncoding"); }
        catch (Exception e) when (e is InvalidOperationException or ArgumentException or KeyNotFoundException) { throw Failure("EditRejected"); }
    }
    private ScriptRunException Failure(string code) => new(new ScriptDiagnostic(ScriptPhase.Execution, code, Path: LastPath));

    internal static Hl7Message Parse(byte[] bytes, int codePage)
    {
        if (bytes.Length > ScriptLimits.MessageBytes) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputLimit"));
        try
        {
            var encoding = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
            if (encoding.GetCharCount(bytes) > ScriptLimits.MessageCharacters + 1)
                throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputLimit"));
            var parsed = Hl7Parser.Parse(bytes, new() { Encoding = encoding, MaxMessageBytes = ScriptLimits.MessageBytes,
                MaxMessageCharacters = ScriptLimits.MessageCharacters, MaxSegmentLength = ScriptLimits.LineCharacters });
            if (!parsed.HasDecodedText) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InvalidEncoding"));
            return parsed;
        }
        catch (Hl7LimitException) { throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputLimit")); }
        catch (Exception e) when (e is DecoderFallbackException or EncoderFallbackException or ArgumentException)
        { throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InvalidEncoding")); }
    }
}

/// <summary>Address view, re-resolved after every controlled write instead of retaining stale Core snapshots.</summary>
public sealed class ScriptSegment
{
    private readonly ScriptMessage message;
    public string Identifier { get; }
    public int Occurrence { get; }
    internal ScriptSegment(ScriptMessage message, string identifier, int occurrence)
    { this.message = message; Identifier = identifier; Occurrence = occurrence; }
    private string Path(string relative) => $"{Identifier}[{Occurrence}]-{relative}";
    public ScriptValue Read(string relativePath) => message.Read(Path(relativePath));
    public string Text(string relativePath) => message.Text(Path(relativePath));
    public void SetText(string relativePath, string value) => message.SetText(Path(relativePath), value);
    public void SetRaw(string relativePath, string value) => message.SetRaw(Path(relativePath), value);
    public void SetNull(string relativePath) => message.SetNull(Path(relativePath));
    public void Clear(string relativePath) => message.Clear(Path(relativePath));
}

public sealed class ScriptGlobals
{
    /// <summary>The current Main invocation's message. Unavailable during initialization and Complete.</summary>
    public ScriptMessage Message => Batch.CurrentMessage;
    public ScriptBatch Batch { get; }
    public CancellationToken Cancellation { get; }
    internal ScriptGlobals(ScriptBatch batch, CancellationToken cancellation) { Batch = batch; Cancellation = cancellation; }
}
