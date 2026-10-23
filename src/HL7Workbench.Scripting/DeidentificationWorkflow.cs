using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Scripting;

/// <summary>Versioned, run-local field-aware pseudonymization/de-identification aid.
/// Output always requires review; a reproducible seed is not an anonymity secret.</summary>
public sealed partial class DeidentificationWorkflow
{
    private readonly DeidentificationOptions options;
    private readonly CancellationToken cancellation;
    private readonly byte[] key;
    private readonly Dictionary<string, Patient> patients = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> issued = new(StringComparer.Ordinal);
    private readonly SyntheticIdentityLibrary? synthetic;
    private readonly long[] transformedCategories = new long[Enum.GetValues<DeidentificationCategory>().Length], suppressedCategories = new long[Enum.GetValues<DeidentificationCategory>().Length];
    private long messages, missing, ambiguous, replaced, suppressed, shifted, unsupportedTimes, unknown, highRisk, retained, operations;
    private long retainedReview, omittedFindings;
    private readonly List<DeidentificationFinding> findings = [];
    private int segmentNumber, segmentOccurrence, fieldNumber;
    private DeidentificationSegment reportingSegment;
    public IReadOnlyList<DeidentificationFinding> Findings => findings.AsReadOnly();
    private sealed class Patient(string tag, string? birthEvidence)
    { internal string Tag { get; } = tag; internal string? BirthEvidence { get; set; } = birthEvidence; }
    private sealed record Context(string Tag, bool Unresolved);
    public int ShiftDays { get; }
    public DeidentificationSummary Summary => new(messages, missing, ambiguous, replaced, suppressed, shifted, unsupportedTimes, unknown, highRisk, retained)
    {
        Preset = options.Preset, Version = PolicyVersion, RetainedValuesRequiringReview = retainedReview, OmittedFindings = omittedFindings,
        Categories = Array.AsReadOnly(Enum.GetValues<DeidentificationCategory>().Select(c => new DeidentificationCategoryCount(c, transformedCategories[(int)c], suppressedCategories[(int)c])).ToArray())
    };
    private int PolicyVersion => options.Preset == DeidentificationPreset.PatientDeidentifier ? DeidentificationOptions.PatientImplementationVersion : DeidentificationOptions.ImplementationVersion;
    public DeidentificationWorkflow(DeidentificationOptions options, CancellationToken cancellation = default)
    {
        ArgumentNullException.ThrowIfNull(options); options.Validate(); this.options = options; this.cancellation = cancellation;
        key = Encoding.UTF8.GetBytes("HL7Workbench-Deidentification-v1:" + options.Seed.ToString(CultureInfo.InvariantCulture));
        if (options.Preset == DeidentificationPreset.PatientDeidentifier) synthetic = new(options.Seed, options.MaximumMappings);
        var derived = (int)(options.Seed % 731) - 365;
        ShiftDays = options.ShiftDays ?? (derived == 0 ? 1 : derived);
    }
    /// <summary>Plans from Core's frozen hierarchy, then atomically replaces only this worker invocation's message.
    /// A failure invalidates the run; the built-in presets let the existing worker journal discard all output.</summary>
    public void Apply(ScriptMessage message)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!message.CanEdit || message.HasDiagnostics) throw Failure("EditRejected");
        if (messages >= ScriptLimits.BatchMessages) throw Failure("BatchLimit");
        var model = message.Model;
        var context = options.Preset == DeidentificationPreset.TimestampShifter ? new Context("", false) : ResolvePatient(model);
        var edits = new List<ScriptMessage.LiteralEdit>();
        segmentNumber = 0;
        var occurrences = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var segment in model.Segments)
        {
            cancellation.ThrowIfCancellationRequested();
            segmentNumber++; fieldNumber = 0;
            reportingSegment = KnownSegments.Contains(segment.Identifier) ? Enum.Parse<DeidentificationSegment>(segment.Identifier) : DeidentificationSegment.Unknown;
            occurrences.TryGetValue(segment.Identifier, out var occurrence); segmentOccurrence = occurrence + 1; occurrences[segment.Identifier] = segmentOccurrence;
            if (!KnownSegments.Contains(segment.Identifier)) { unknown++; Finding(DeidentificationReason.UnknownSegment); }
            foreach (var field in segment.Fields)
            {
                fieldNumber = field.Number;
                if (segment.Identifier == "MSH" && field.Number <= 2) continue;
                if (field.Value.State != Hl7ValueState.Value) continue;
                var beforeReplaced = replaced; var beforeSuppressed = suppressed;
                Transform(model, segment, field, context, edits);
                var category = Category(segment, field);
                transformedCategories[(int)category] += replaced - beforeReplaced;
                suppressedCategories[(int)category] += suppressed - beforeSuppressed;
            }
        }
        cancellation.ThrowIfCancellationRequested(); message.ApplyLiteralEdits(edits); messages++;
    }
    private Context ResolvePatient(Hl7Message model)
    {
        var segments = model.Segments.Where(s => s.Identifier == "PID").ToArray();
        if (segments.Length != 1) { if (segments.Length == 0) missing++; else ambiguous++; IdentityFinding(segments.Length == 0); return Unresolved(); }
        var pid = segments[0]; var identifiers = pid.GetField(3)?.Repetitions ?? [];
        var aliases = identifiers.Where(r => r.Value.State == Hl7ValueState.Value).Select(r => QualifiedIdentity(model, r, "patient", 4, 5))
            .Where(k => k is not null).Cast<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (aliases.Length == 0) { missing++; IdentityFinding(true); return Unresolved(); }
        var birthRaw = pid.GetField(7)?.Value;
        var birth = birthRaw is { State: Hl7ValueState.Value, Raw: { } value } ? Digest("birth-evidence", value) : null;
        var known = aliases.Select(a => patients.GetValueOrDefault(a)).OfType<Patient>().DistinctBy(p => p.Tag).ToArray();
        if (known.Length > 1 || known.Length == 1 && birth is not null && known[0].BirthEvidence is { } old && old != birth)
        { ambiguous++; IdentityFinding(false); return Unresolved(); }
        var patient = known.FirstOrDefault() ?? new Patient(Issue("patient", aliases[0]), birth);
        patient.BirthEvidence ??= birth;
        if (patients.Count + aliases.Count(a => !patients.ContainsKey(a)) > options.MaximumMappings) throw Failure("TransformationLimit");
        foreach (var alias in aliases) patients.TryAdd(alias, patient);
        return new(patient.Tag, false);
    }
    private Context Unresolved() => new(Digest("unresolved-message", messages.ToString(CultureInfo.InvariantCulture))[..24], true);
    private void IdentityFinding(bool absent) => AddFinding(new(checked((int)messages + 1), 0, DeidentificationSegment.PID, 0, 3,
        absent ? DeidentificationReason.MissingIdentity : DeidentificationReason.AmbiguousIdentity));
    private void Finding(DeidentificationReason reason) => AddFinding(new(checked((int)messages + 1), segmentNumber, reportingSegment, segmentOccurrence, fieldNumber, reason));
    private void AddFinding(DeidentificationFinding finding)
    { if (findings.Count < DeidentificationFinding.MaximumFindings) findings.Add(finding); else omittedFindings++; }
    private void Risk(DeidentificationReason reason) { highRisk++; Finding(reason); }
    private void Retain(bool review, DeidentificationReason reason = DeidentificationReason.RetainedClinicalValue)
    { retained++; if (review) { retainedReview++; Finding(reason); } }
    private string? QualifiedIdentity(Hl7Message model, Hl7Repetition rep, string domain, int authorityComponent, int typeComponent = 0)
    {
        var id = ComponentText(model, rep, 1);
        var authority = ComponentText(model, rep, authorityComponent);
        if (domain == "order")
        {
            var universal = ComponentText(model, rep, 3);
            if (authority is null && universal is null) return null;
            authority = Digest("EI-authority", authority ?? "", universal ?? "", ComponentText(model, rep, 4) ?? "");
        }
        if (string.IsNullOrWhiteSpace(id) || string.IsNullOrWhiteSpace(authority)) return null;
        return Digest(domain, id, authority, typeComponent > 0 ? ComponentText(model, rep, typeComponent) ?? "" : "");
    }
    private static string? ComponentText(Hl7Message model, Hl7Repetition rep, int number)
    {
        if (number > rep.Components.Count || rep.Components[number - 1].Value.State != Hl7ValueState.Value) return null;
        var component = rep.Components[number - 1];
        if (!component.Subcomponents.Any(p => p.Value.State == Hl7ValueState.Value && !string.IsNullOrWhiteSpace(p.Raw))) return null;
        var values = new List<string>();
        foreach (var part in component.Subcomponents)
        {
            if (model.Delimiters.Truncation is { } truncation && part.Raw.Contains(truncation)) return null;
            if (part.Raw.Length > 1024) throw Failure("TransformationLimit");
            var decoded = Hl7EscapeCodec.Decode(part.Raw, model.Delimiters, model.Encoding, 1024);
            if (decoded.Diagnostics.Count != 0) return null;
            // Length framing distinguishes composite boundaries from literal '&'
            // values decoded through escapes, and preserves null/empty distinctions.
            values.Add(((int)part.Value.State).ToString(CultureInfo.InvariantCulture) + ":" + decoded.Text.Length.ToString(CultureInfo.InvariantCulture) + ":" + decoded.Text);
        }
        return string.Concat(values);
    }
    private string Digest(string domain, params string[] values)
    {
        cancellation.ThrowIfCancellationRequested(); using var data = new MemoryStream();
        using (var writer = new BinaryWriter(data, Encoding.UTF8, leaveOpen: true))
        { writer.Write(PolicyVersion); writer.Write(domain); foreach (var value in values) writer.Write(value); }
        return Convert.ToHexString(HMACSHA256.HashData(key, data.GetBuffer().AsSpan(0, checked((int)data.Length))));
    }
    private string Issue(string domain, params string[] values)
    {
        var digest = Digest(domain, values); var result = "SYN-" + digest[..24];
        if (issued.TryGetValue(result, out var previous)) { if (previous != digest) throw Failure("EditRejected"); return result; }
        if (issued.Count >= options.MaximumMappings) throw Failure("TransformationLimit");
        issued.Add(result, digest); return result;
    }
    private void Write(Hl7Element value, string text, List<ScriptMessage.LiteralEdit> edits, bool suppress = false)
    {
        if (value.Value.State != Hl7ValueState.Value) return;
        cancellation.ThrowIfCancellationRequested();
        if (++operations > 2_000_000 || edits.Count >= 16_384) throw Failure("TransformationLimit");
        edits.Add(new(value.CharacterOffset, value.Raw.Length, text));
        if (suppress) suppressed++; else replaced++;
    }
    private void Suppress(Hl7Field field, List<ScriptMessage.LiteralEdit> edits)
    {
        foreach (var rep in field.Repetitions.Where(r => r.Value.State == Hl7ValueState.Value))
        foreach (var comp in rep.Components.Where(c => c.Value.State == Hl7ValueState.Value))
        foreach (var leaf in comp.Subcomponents) Write(leaf, "REDACTED", edits, suppress: true);
    }
    private static ScriptRunException Failure(string code) => new(new ScriptDiagnostic(ScriptPhase.Execution, code));
}
