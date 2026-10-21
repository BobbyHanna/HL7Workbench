namespace HL7Workbench.Core;

public sealed partial class FhirGenerationPlan
{
    /// <summary>Captures complete or targeted eligibility without publishing or retaining an oversized output.</summary>
    public static FhirGenerationPlan Create(FhirImportSession source, FhirGenerationRequest request, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(request.Scope) || !request.Adt && !request.Oru)
            throw new ArgumentException("Choose a generation scope and ADT, ORU or both.");
        if (request.Scope != FhirGenerationScope.EntireImport && source.Resource(request.PatientKey).Kind != FhirResourceKind.Patient)
            throw new ArgumentException("Select a patient for this scope.");
        var complete = request.Scope != FhirGenerationScope.SelectedEvents;
        if (!complete && (request.Encounters.Count > 256 || request.Events.Count > 256 || request.Encounters.Count + request.Events.Count == 0))
            throw new ArgumentException("Select up to 256 encounters and 256 timeline events.");
        var metadata = new SyntheticGenerationOptions { BaseTimestamp = request.TransportTimestamp }.Normalize();
        request = request with
        {
            TransportTimestamp = metadata.BaseTimestamp,
            Encounters = complete ? [] : Array.AsReadOnly(request.Encounters.Distinct().ToArray()),
            Events = complete ? [] : Array.AsReadOnly(request.Events.Distinct().ToArray())
        };
        var patients = request.Scope == FhirGenerationScope.EntireImport ? source.Patients : new[] { source.Resource(request.PatientKey) };
        // One source-bounded lookup prevents rescanning the whole import per patient.
        var byPatient = source.Resources.ToLookup(r => source.PatientOf(r.Key));
        var ambiguousUrls = source.Resources.Where(r => r.FullUrl.Length > 0).GroupBy(r => r.FullUrl, StringComparer.Ordinal)
            .Where(g => g.Skip(1).Any()).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        var candidates = new List<Candidate>(); var counts = new int[4]; var observationCount = 0;
        var consideredEncounters = new HashSet<int>(); var skipped = new HashSet<int>();
        var mappedResources = new HashSet<int>(); var usedEncounters = new HashSet<int>();
        var warnings = new Dictionary<FhirMappingWarning, int>();
        var ineligibleReasons = new Dictionary<FhirAdtIneligibility, int>();
        void Warn(FhirMappingWarning kind, int count = 1) { if (count > 0) warnings[kind] = warnings.GetValueOrDefault(kind) + count; }
        foreach (var patient in patients)
        {
            token.ThrowIfCancellationRequested();
            var resources = byPatient[patient.Key];
            consideredEncounters.UnionWith(complete ? resources.Where(r => r.Kind == FhirResourceKind.Encounter).Select(r => r.Key) :
                request.Encounters.Concat(request.Events.Select(e => source.EncounterOf(e.ResourceKey)).OfType<int>()));
            foreach (var candidate in SelectCandidates(source, request with { PatientKey = patient.Key }, complete, resources, warnings, ineligibleReasons, skipped, token))
            {
                token.ThrowIfCancellationRequested();
                counts[candidate.Event switch { "A04" => 0, "A01" => 1, "A03" => 2, _ => 3 }]++;
                observationCount += candidate.Observations.Length;
                // Count all eligible events, but never retain a partial oversized plan.
                // At most 1,000 candidates and source-bounded result references live here.
                if (counts.Sum() <= SyntheticMessageGenerator.MaximumCount) candidates.Add(candidate);
                else candidates.Clear();
                if (!candidate.Time.IsValid) Warn(FhirMappingWarning.MissingOrInvalidClinicalTime);
                Warn(FhirMappingWarning.OmittedDetail, candidate.Observations.Sum(k => source.Resource(k).OmittedParts));
                if (candidate.Observations.Any(k => !Time(source.Resource(k), FhirEventKind.ObservationEffective).IsValid)) Warn(FhirMappingWarning.MissingOrInvalidClinicalTime);
                mappedResources.UnionWith(candidate.Observations);
                if (source.Resource(candidate.Resource).Kind == FhirResourceKind.DiagnosticReport) mappedResources.Add(candidate.Resource);
                if (candidate.Encounter is { } encounter) usedEncounters.Add(encounter);
            }
        }
        if (request.Scope == FhirGenerationScope.EntireImport)
            foreach (var resource in source.Resources)
            {
                token.ThrowIfCancellationRequested();
                if (resource.Kind == FhirResourceKind.Encounter) consideredEncounters.Add(resource.Key);
                if (resource.Kind == FhirResourceKind.Unsupported)
                { skipped.Add(resource.Key); Warn(FhirMappingWarning.UnsupportedResourceType); }
                else if (resource.Kind is FhirResourceKind.Encounter or FhirResourceKind.Condition or FhirResourceKind.Observation or FhirResourceKind.DiagnosticReport && source.PatientOf(resource.Key) is null)
                { skipped.Add(resource.Key); Warn(FhirMappingWarning.UnassociatedClinicalResource); }
            }
        foreach (var key in mappedResources)
        {
            token.ThrowIfCancellationRequested(); var resource = source.Resource(key);
            int LostCodes(FhirConcept? concept) => concept is null ? 0 : Math.Max(0, concept.Codings.Count - 2) + concept.Codings.Count(c => c.System.Length == 0 || CodingSystem(c.System).Length == 0);
            Warn(FhirMappingWarning.OmittedDetail, LostCodes(resource.Code) + LostCodes(resource.Value?.Concept));
        }
        foreach (var key in usedEncounters)
        {
            token.ThrowIfCancellationRequested(); var encounter = source.Resource(key);
            if (encounter.Encounter?.ClassSystem is not ("http://terminology.hl7.org/CodeSystem/v3-ActCode" or "http://hl7.org/fhir/v3/ActCode")) Warn(FhirMappingWarning.OmittedDetail);
            if (ambiguousUrls.Contains(encounter.FullUrl)) Warn(FhirMappingWarning.QualifiedIdentity);
        }
        // Group patients by stable source identity, then use actual/coarse clinical
        // chronology and stable provenance/event ties within each patient. No names.
        var ordered = candidates.OrderBy(c => Stable(source.Resource(c.Patient)), StringComparer.Ordinal)
            .ThenBy(c => c.Time.Order).ThenBy(c => Stable(source.Resource(c.Resource)), StringComparer.Ordinal)
            .ThenBy(c => c.Event, StringComparer.Ordinal).ToArray();
        token.ThrowIfCancellationRequested();
        return new(source, request, ordered, counts, observationCount, patients.Count, consideredEncounters.Count, skipped.Count, warnings, ineligibleReasons, token);
    }
}
