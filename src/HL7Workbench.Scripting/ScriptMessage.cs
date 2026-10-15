using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Scripting;

public enum ValueState { Missing, Empty, ExplicitNull, Value }
public sealed record ScriptValue(ValueState State, string? Raw);

/// <summary>One invocation's bounded message. Paths are Core's one-based coordinates; no UI or source sessions escape.</summary>
public sealed class ScriptMessage
{
    private readonly Hl7Message message;
    private readonly CancellationToken cancellation;
    internal string? LastPath { get; private set; }
    internal bool Excluded { get; private set; }
    internal ScriptMessage(Hl7Message message, CancellationToken cancellation) { this.message = message; this.cancellation = cancellation; }

    public bool CanEdit => message.CanEdit;
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
    public ScriptMessage Message { get; }
    public CancellationToken Cancellation { get; }
    internal ScriptGlobals(ScriptMessage message, CancellationToken cancellation) { Message = message; Cancellation = cancellation; }
}
