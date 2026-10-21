using System.Collections.ObjectModel;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace HL7Workbench.Core;

public enum FhirMappingWarning { IneligibleAdt, UnsupportedEvent, UnsupportedValue, UnsupportedCodeSystem, UnsupportedStatus,
    UnresolvedOrConflictingLink, MissingOrInvalidClinicalTime, OmittedDetail, QualifiedIdentity, GeneratedTransportMetadata,
    UnassociatedClinicalResource, UnsupportedResourceType }
public enum FhirGenerationScope { SelectedPatient, EntireImport, SelectedEvents }
public enum FhirGenerationLimit { MessageCount, TotalCharacters, MessageCharacters, SegmentCharacters, MappedFieldCharacters }
public enum FhirAdtIneligibility { UnsupportedClass, UnsupportedStatus, MissingCompleteStart, ReversedPeriod, MissingDischargeEvidence }
public sealed record FhirEventSelection(int ResourceKey, FhirEventKind Kind);
public sealed record FhirGenerationRequest
{
    // Preserve existing programmatic targeted requests; the workspace/UI defaults
    // explicitly to SelectedPatient without changing the advanced request contract.
    public FhirGenerationScope Scope { get; init; } = FhirGenerationScope.SelectedEvents;
    public int PatientKey { get; init; }
    public IReadOnlyList<int> Encounters { get; init; } = [];
    public IReadOnlyList<FhirEventSelection> Events { get; init; } = [];
    public bool Adt { get; init; } = true;
    public bool Oru { get; init; } = true;
    public uint Seed { get; init; } = 42;
    public DateTimeOffset TransportTimestamp { get; init; } = SyntheticGenerationOptions.DefaultTimestamp;
}
/// <summary>Only counts and opaque captured identity, never source identifiers/names/clinical values.</summary>
public sealed record FhirGenerationReport(int MappingVersion, Guid SessionId, string SourceFingerprint, uint Seed,
    DateTimeOffset TransportTimestamp, FhirImportSummary Imported, int AdtA04, int AdtA01, int AdtA03, int OruR01,
    int Observations, IReadOnlyDictionary<FhirMappingWarning, int> Warnings)
{
    public int Messages => AdtA04 + AdtA01 + AdtA03 + OruR01;
    public FhirGenerationScope Scope { get; init; }
    public int PatientsIncluded { get; init; }
    public int EncountersConsidered { get; init; }
    public int SkippedResources { get; init; }
    public int IneligibleEvents => Warnings.GetValueOrDefault(FhirMappingWarning.IneligibleAdt);
    public IReadOnlyDictionary<FhirAdtIneligibility, int> IneligibleReasons { get; init; } = new ReadOnlyDictionary<FhirAdtIneligibility, int>(new Dictionary<FhirAdtIneligibility, int>());
    public long MeasuredCharacters { get; init; }
    public bool CharacterMeasurementComplete { get; init; }
    public IReadOnlyList<FhirGenerationLimit> Limits { get; init; } = [];
    public bool IsPreview { get; init; } = true;
    public bool CanGenerate => Messages > 0 && Limits.Count == 0;
    public int GeneratedMessages => IsPreview ? 0 : Messages;
    public static string ScopeLabel(FhirGenerationScope scope) => scope switch
    {
        FhirGenerationScope.SelectedPatient => "Selected Patient",
        FhirGenerationScope.EntireImport => "Entire Import",
        FhirGenerationScope.SelectedEvents => "Selected Events (advanced)",
        _ => throw new ArgumentOutOfRangeException(nameof(scope))
    };
    public const string Notice = "Review required. Synthetic/test import is user-confirmed, not verified PHI-free. Complete history includes supported mappings only; clinical completeness is not claimed. Mapping is partial and may be lossy; no universal FHIR conversion, clinical or interface conformance certification. Later output edits are not reassessed.";
}
public sealed class FhirMappedBatch
{
    public IReadOnlyList<string> Messages { get; }
    public FhirGenerationReport Report { get; }
    public int TotalCharacters { get; }
    internal FhirMappedBatch(string[] messages, FhirGenerationReport report)
    { Messages = Array.AsReadOnly(messages); Report = report; TotalCharacters = messages.Sum(m => m.Length); }
    public byte[] GetUtf8Bytes() => new UTF8Encoding(false, true).GetBytes(string.Concat(Messages));
}

/// <summary>Immutable selection/eligibility preview over one captured import; no public source-value diagnostics.</summary>
public sealed partial class FhirGenerationPlan
{
    public const int MappingVersion = 1;
    private sealed record Candidate(int Patient, string Event, int Resource, int? Encounter, int[] Observations, FhirClinicalTime Time);
    private readonly FhirImportSession source;
    private readonly FhirGenerationRequest request;
    private readonly Candidate[] candidates;
    private readonly string selectionFingerprint;
    public FhirGenerationReport Preview { get; }
    private FhirGenerationPlan(FhirImportSession source, FhirGenerationRequest request, Candidate[] candidates,
        int[] counts, int observations, int patients, int encounters, int skipped,
        Dictionary<FhirMappingWarning, int> warnings, Dictionary<FhirAdtIneligibility, int> ineligibleReasons, CancellationToken token)
    {
        this.source = source; this.request = request; this.candidates = candidates;
        // Stream the bounded selection digest: repeated report links must not build
        // a large concatenated string solely to compute transport identifiers.
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        void Hash(string value) { token.ThrowIfCancellationRequested(); hash.AppendData(Encoding.UTF8.GetBytes(value)); }
        Hash(source.Fingerprint + "\n");
        foreach (var candidate in candidates)
        {
            Hash(Stable(source.Resource(candidate.Patient)) + ":" + candidate.Event + ":" + Stable(source.Resource(candidate.Resource)));
            foreach (var key in candidate.Observations) Hash("," + Stable(source.Resource(key)));
            Hash("\n");
        }
        selectionFingerprint = Convert.ToHexString(hash.GetHashAndReset());
        warnings[FhirMappingWarning.GeneratedTransportMetadata] = counts.Sum();
        Preview = new(MappingVersion, source.Id, source.Fingerprint, request.Seed, request.TransportTimestamp, source.Summary,
            counts[0], counts[1], counts[2], counts[3], observations, new ReadOnlyDictionary<FhirMappingWarning, int>(warnings))
        { Scope = request.Scope, PatientsIncluded = patients, EncountersConsidered = encounters, SkippedResources = skipped,
            IneligibleReasons = new ReadOnlyDictionary<FhirAdtIneligibility, int>(ineligibleReasons) };
        Preview = MeasureOutput(Preview, token);
    }
    private FhirGenerationReport MeasureOutput(FhirGenerationReport report, CancellationToken token)
    {
        if (report.Messages > SyntheticMessageGenerator.MaximumCount)
            return report with { Limits = Array.AsReadOnly(new[] { FhirGenerationLimit.MessageCount }) };
        long characters = 0;
        for (var n = 0; n < candidates.Length; n++)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                // Render/discard one message at a time. Nothing is retained or
                // published by preview, and size measurement stops at the ceiling.
                characters += Render(candidates[n], n + 1, token).Length;
                if (characters > SyntheticMessageGenerator.MaximumCharacters)
                    return report with { MeasuredCharacters = characters, CharacterMeasurementComplete = n + 1 == candidates.Length,
                        Limits = Array.AsReadOnly(new[] { FhirGenerationLimit.TotalCharacters }) };
            }
            catch (Hl7LimitException error)
            {
                var limit = error.Limit switch
                {
                    "GeneratedMessageCharacters" => FhirGenerationLimit.MessageCharacters,
                    "GeneratedSegmentCharacters" => FhirGenerationLimit.SegmentCharacters,
                    _ => FhirGenerationLimit.MappedFieldCharacters
                };
                return report with { MeasuredCharacters = characters, Limits = Array.AsReadOnly(new[] { limit }) };
            }
        }
        return report with { MeasuredCharacters = characters, CharacterMeasurementComplete = true };
    }
    private static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static string Stable(FhirResource resource) => (resource.FullUrl.Length > 0 ? resource.FullUrl : resource.InputDigest + "/" + resource.ResourceType + "/" + resource.Id) + "/" + resource.ContentDigest;
    private static FhirClinicalTime Time(FhirResource resource, FhirEventKind kind) => resource.Times.FirstOrDefault(t => t.Kind == kind)?.Time ?? FhirClinicalTime.Parse("");
    public static string AdtEligibility(FhirResource encounter, bool discharge = false)
    {
        if (encounter.Kind != FhirResourceKind.Encounter || encounter.Encounter is null) return "Select an encounter.";
        return AdtFailure(encounter, discharge) switch
        {
            FhirAdtIneligibility.UnsupportedClass => "Unsupported encounter class/system.",
            FhirAdtIneligibility.UnsupportedStatus => "Encounter must be in-progress or finished.",
            FhirAdtIneligibility.MissingCompleteStart => "A complete encounter start with offset is required.",
            FhirAdtIneligibility.ReversedPeriod => "Encounter end precedes start.",
            FhirAdtIneligibility.MissingDischargeEvidence => "A03 requires a finished inpatient encounter and valid end.",
            _ => discharge ? "A03" : encounter.Encounter.ClassCode == "IMP" ? "A01" : "A04"
        };
    }
    private static FhirAdtIneligibility? AdtFailure(FhirResource encounter, bool discharge)
    {
        var data = encounter.Encounter!;
        if (data.ClassSystem is not ("http://terminology.hl7.org/CodeSystem/v3-ActCode" or "http://hl7.org/fhir/v3/ActCode")) return FhirAdtIneligibility.UnsupportedClass;
        if (data.Status is not ("in-progress" or "finished")) return FhirAdtIneligibility.UnsupportedStatus;
        var start = Time(encounter, FhirEventKind.EncounterStart); var end = Time(encounter, FhirEventKind.EncounterEnd);
        if (!start.IsInstant) return FhirAdtIneligibility.MissingCompleteStart;
        if (end.IsInstant && end.Order < start.Order) return FhirAdtIneligibility.ReversedPeriod;
        if (discharge) return data.ClassCode != "IMP" || data.Status != "finished" || !end.IsInstant ? FhirAdtIneligibility.MissingDischargeEvidence : null;
        return data.ClassCode is "IMP" or "AMB" or "EMER" or "OBSENC" or "HH" or "VR" ? null : FhirAdtIneligibility.UnsupportedClass;
    }
    private static IEnumerable<Candidate> SelectCandidates(FhirImportSession source, FhirGenerationRequest request,
        bool complete, IEnumerable<FhirResource> patientResources, Dictionary<FhirMappingWarning, int> warnings,
        Dictionary<FhirAdtIneligibility, int> ineligibleReasons, HashSet<int> skipped, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var selectedEncounters = complete ? patientResources.Where(r => r.Kind == FhirResourceKind.Encounter).Select(r => r.Key).ToHashSet() : request.Encounters.ToHashSet();
        var starts = new HashSet<int>(); var ends = new HashSet<int>();
        var observations = new HashSet<int>(); var reports = new HashSet<int>();
        void Warn(FhirMappingWarning kind, int count = 1) { if (count > 0) warnings[kind] = warnings.GetValueOrDefault(kind) + count; }
        void Skip(int key, FhirMappingWarning reason) { skipped.Add(key); Warn(reason); }
        void Ineligible(FhirResource encounter, bool discharge)
        {
            Warn(FhirMappingWarning.IneligibleAdt);
            var reason = AdtFailure(encounter, discharge)!.Value;
            ineligibleReasons[reason] = ineligibleReasons.GetValueOrDefault(reason) + 1;
        }
        foreach (var key in selectedEncounters)
        {
            var encounter = source.Resource(key);
            if (encounter.Kind != FhirResourceKind.Encounter || source.PatientOf(key) != request.PatientKey) throw new ArgumentException("An encounter does not belong to the selected patient.");
            if (request.Adt) { starts.Add(key); ends.Add(key); }
        }
        foreach (var selected in complete ? [] : request.Events)
        {
            token.ThrowIfCancellationRequested(); var resource = source.Resource(selected.ResourceKey);
            if (source.PatientOf(resource.Key) != request.PatientKey || !resource.Times.Any(t => t.Kind == selected.Kind)) throw new ArgumentException("A selected event is no longer available for this patient.");
            if (request.Adt && selected.Kind == FhirEventKind.EncounterStart) starts.Add(resource.Key);
            else if (request.Adt && selected.Kind == FhirEventKind.EncounterEnd) ends.Add(resource.Key);
            else if (request.Oru && resource.Kind == FhirResourceKind.Observation) observations.Add(resource.Key);
            else if (request.Oru && resource.Kind == FhirResourceKind.DiagnosticReport) reports.Add(resource.Key);
            else Skip(resource.Key, FhirMappingWarning.UnsupportedEvent);
        }
        if (complete)
            foreach (var condition in patientResources.Where(r => r.Kind == FhirResourceKind.Condition))
            { token.ThrowIfCancellationRequested(); Skip(condition.Key, FhirMappingWarning.UnsupportedEvent); }
        if (request.Oru)
            foreach (var resource in patientResources)
                if (complete || source.EncounterOf(resource.Key) is { } encounter && selectedEncounters.Contains(encounter))
                { if (resource.Kind == FhirResourceKind.Observation) observations.Add(resource.Key); if (resource.Kind == FhirResourceKind.DiagnosticReport) reports.Add(resource.Key); }
        var eligibilityCache = new Dictionary<int, (bool Eligible, FhirMappingWarning Warning)>();
        bool Eligible(int key, out FhirMappingWarning warning)
        {
            if (!eligibilityCache.TryGetValue(key, out var value))
            { var eligible = TryObservation(source.Resource(key), out _, out var reason); eligibilityCache.Add(key, value = (eligible, reason)); }
            warning = value.Warning; return value.Eligible;
        }
        foreach (var key in starts.Order())
        {
            token.ThrowIfCancellationRequested();
            var encounter = source.Resource(key); var eligibility = AdtEligibility(encounter);
            if (eligibility is "A01" or "A04") yield return new(request.PatientKey, eligibility, key, key, [], Time(encounter, FhirEventKind.EncounterStart));
            else Ineligible(encounter, false);
        }
        foreach (var key in ends.Order())
        {
            token.ThrowIfCancellationRequested();
            var encounter = source.Resource(key);
            if (AdtEligibility(encounter, true) == "A03") yield return new(request.PatientKey, "A03", key, key, [], Time(encounter, FhirEventKind.EncounterEnd));
            else if (!selectedEncounters.Contains(key) || encounter.Encounter?.ClassCode == "IMP") Ineligible(encounter, true);
        }
        var covered = new HashSet<int>();
        foreach (var key in reports.Order())
        {
            token.ThrowIfCancellationRequested(); var report = source.Resource(key);
            if (source.IsQuarantined(key)) { Skip(key, FhirMappingWarning.UnresolvedOrConflictingLink); continue; }
            if (ReportStatus(report.Status).Length == 0) { Skip(key, FhirMappingWarning.UnsupportedStatus); continue; }
            if (!TryConcept(report.Code, false, out _)) { Skip(key, FhirMappingWarning.UnsupportedCodeSystem); continue; }
            var values = new List<int>();
            foreach (var link in source.LinksOf(key).Where(l => l.Role == FhirLinkRole.Result))
            {
                token.ThrowIfCancellationRequested();
                if (link.Resolution != FhirResolution.Resolved || link.Target is not { } observation) { Warn(FhirMappingWarning.UnresolvedOrConflictingLink); continue; }
                if (Eligible(observation, out var warning)) values.Add(observation); else Skip(observation, warning);
            }
            var unique = values.Distinct().OrderBy(k => Time(source.Resource(k), FhirEventKind.ObservationEffective).Order).ThenBy(k => Stable(source.Resource(k)), StringComparer.Ordinal).ToArray();
            if (unique.Length == 0) { Skip(key, FhirMappingWarning.UnsupportedValue); continue; }
            yield return new(request.PatientKey, "R01", key, source.EncounterOf(key), unique, Time(report, FhirEventKind.ReportEffective));
            covered.UnionWith(unique); Warn(FhirMappingWarning.OmittedDetail, report.OmittedParts);
        }
        foreach (var key in observations.Except(covered).Order())
        {
            token.ThrowIfCancellationRequested(); var observation = source.Resource(key);
            if (source.IsQuarantined(key)) { Skip(key, FhirMappingWarning.UnresolvedOrConflictingLink); continue; }
            if (!Eligible(key, out var warning)) { Skip(key, warning); continue; }
            yield return new(request.PatientKey, "R01", key, source.EncounterOf(key), [key], Time(observation, FhirEventKind.ObservationEffective));
        }
        var patient = source.Resource(request.PatientKey);
        Warn(FhirMappingWarning.OmittedDetail, patient.OmittedParts + (patient.Person?.Family.Length == 0 ? 1 : 0));
        if (patient.Person is { } demographics && FhirClinicalTime.Parse(demographics.BirthDate).Precision is FhirTimePrecision.Missing or FhirTimePrecision.Invalid or FhirTimePrecision.Instant)
            Warn(FhirMappingWarning.MissingOrInvalidClinicalTime);
        if (patient.Person is { Identifier.Length: > 0 } p && source.Patients.Count(r => r.Person?.Identifier == p.Identifier && r.Person?.IdentifierSystem == p.IdentifierSystem) > 1) Warn(FhirMappingWarning.QualifiedIdentity);
    }
    /// <summary>Produces one complete validated batch or throws; no clock, I/O, log or document side effect.</summary>
    public FhirMappedBatch Generate(CancellationToken token = default, IProgress<int>? progress = null)
    {
        token.ThrowIfCancellationRequested();
        if (Preview.Limits.Count > 0) throw new Hl7LimitException("FhirOutput" + Preview.Limits[0]);
        if (candidates.Length == 0) throw new InvalidOperationException("No supported messages for the selected events; review eligibility and warnings.");
        var messages = new string[candidates.Length]; var total = 0;
        for (var n = 0; n < candidates.Length; n++)
        {
            token.ThrowIfCancellationRequested(); var text = Render(candidates[n], n + 1, token);
            if (text.Length > SyntheticMessageGenerator.MaximumMessageCharacters || text.Split('\r').Any(line => line.Length > SyntheticMessageGenerator.MaximumLineCharacters)) throw new Hl7LimitException("GeneratedMessageOrLine");
            total = checked(total + text.Length); if (total > SyntheticMessageGenerator.MaximumCharacters) throw new Hl7LimitException("GeneratedContentCharacters");
            var parsed = Hl7Parser.Parse(text);
            if (!parsed.CanEdit || parsed.Diagnostics.Any(d => d.Severity == Hl7DiagnosticSeverity.Error)) throw new InvalidOperationException("Mapped HL7 validation failed; no output published.");
            messages[n] = text; progress?.Report(n + 1);
        }
        token.ThrowIfCancellationRequested(); return new(messages, Preview with { IsPreview = false });
    }
}
