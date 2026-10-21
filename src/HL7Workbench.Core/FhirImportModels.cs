namespace HL7Workbench.Core;

/// <summary>Defensive import budgets; callers may lower them, never raise the application ceilings.</summary>
public sealed record FhirImportLimits
{
    public int Files { get; init; } = 64;
    public int FileBytes { get; init; } = 16 * 1024 * 1024;
    public long TotalBytes { get; init; } = 64L * 1024 * 1024;
    public int Resources { get; init; } = 50_000;
    public int Patients { get; init; } = 2048;
    public int StringCharacters { get; init; } = 65_536;
    public int ProjectedCharacters { get; init; } = 8 * 1024 * 1024;
    public int JsonNodes { get; init; } = 2_000_000;
    public int References { get; init; } = 200_000;
    public int TimelineEvents { get; init; } = 100_000;
    public int Depth { get; init; } = 32;
    internal void Validate()
    {
        if (Files is < 1 or > 64 || FileBytes is < 1 or > 16 * 1024 * 1024 || TotalBytes is < 1 or > 64L * 1024 * 1024 ||
            Resources is < 1 or > 50_000 || Patients is < 1 or > 2048 || StringCharacters is < 1 or > 65_536 ||
            ProjectedCharacters is < 1 or > 8 * 1024 * 1024 || JsonNodes is < 1 or > 2_000_000 ||
            References is < 1 or > 200_000 || TimelineEvents is < 1 or > 100_000 || Depth is < 1 or > 32)
            throw new ArgumentException("Unsupported synthetic FHIR import limits.");
    }
}

public enum FhirResourceKind { Unsupported, Patient, Encounter, Condition, Observation, DiagnosticReport, Organization, Practitioner }
public enum FhirLinkRole { Subject, Encounter, Result, ServiceProvider, Practitioner, AttendingPractitioner, ManagingOrganization }
public enum FhirResolution { Resolved, Unresolved, Ambiguous, Conflicting, Unsupported }
public enum FhirEventKind { EncounterStart, EncounterEnd, ConditionOnset, ConditionRecorded, ObservationEffective, ObservationEffectiveEnd, ObservationIssued, ReportEffective, ReportEffectiveEnd, ReportIssued }
public enum FhirValueKind { Missing, Quantity, String, Boolean, CodeableConcept, Unsupported }

public sealed record FhirCoding(string Code, string Display, string System)
{ public override string ToString() => "FHIR coding"; }
public sealed record FhirConcept(string Text, IReadOnlyList<FhirCoding> Codings)
{ public override string ToString() => "FHIR concept"; }
public sealed record FhirPerson(string Family, IReadOnlyList<string> Given, string BirthDate, string Gender,
    string Identifier, string IdentifierSystem, IReadOnlyList<string> AddressLines, string City, string State, string PostalCode, string Country)
{
    public string DisplayName => string.Join(' ', Given.Concat(new[] { Family }).Where(s => s.Length > 0));
    public override string ToString() => "FHIR person";
}
public sealed record FhirEncounter(string Status, string ClassCode, string ClassSystem)
{ public override string ToString() => "FHIR encounter"; }
public sealed record FhirValue(FhirValueKind Kind, string Text, string Comparator, string Unit, string UnitCode, string UnitSystem, FhirConcept? Concept = null)
{ public override string ToString() => "FHIR observation value"; }
public sealed record FhirLink(FhirLinkRole Role, string Reference, string DeclaredType = "")
{ public override string ToString() => "FHIR reference"; }
public sealed record FhirResolvedLink(FhirLinkRole Role, FhirResolution Resolution, int? Target);
public sealed record FhirTimedEvent(FhirEventKind Kind, FhirClinicalTime Time);

/// <summary>Bounded immutable projection, never raw JSON. Key is session-local; provenance is explicit.</summary>
public sealed record FhirResource(int Key, FhirResourceKind Kind, string ResourceType, string Id, string FullUrl, int InputNumber,
    string InputDigest, int EntryNumber, int? Container, string ContentDigest, FhirPerson? Person, FhirEncounter? Encounter,
    FhirConcept? Code, string Status, FhirValue? Value, string ReferenceRange, IReadOnlyList<string> Interpretations,
    IReadOnlyList<FhirLink> Links, IReadOnlyList<FhirTimedEvent> Times, string OrganizationName, bool SyntheaIdentifier,
    int OmittedParts = 0)
{ public override string ToString() => "Imported FHIR resource"; }

public sealed record FhirImportSummary(int Files, long Bytes, int ResourceOccurrences, int Resources, int Patients,
    int UnsupportedResources, int DeduplicatedResources, int AmbiguousAliases, int UnresolvedReferences, int AmbiguousReferences,
    int ConflictingReferences, int UnsupportedReferences, int UnassociatedClinicalResources, int InvalidTimestamps,
    int MissingTimestamps, int InconsistentPeriods, int SyntheaIdentifierPatients, IReadOnlyDictionary<FhirResourceKind, int> Counts);
public sealed record FhirTimelineEvent(int ResourceKey, FhirEventKind Kind, FhirClinicalTime Time, int? EncounterKey)
{ public override string ToString() => "FHIR clinical event"; }

/// <summary>One immutable, bounded, session-only import. Names/identifiers never determine relationship membership.</summary>
public sealed class FhirImportSession
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Fingerprint { get; }
    public IReadOnlyList<FhirResource> Resources { get; }
    public IReadOnlyList<FhirResource> Patients { get; }
    public FhirImportSummary Summary { get; }
    private readonly IReadOnlyDictionary<int, int> patients, encounters;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<FhirResolvedLink>> links;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<FhirTimelineEvent>> timelines;
    private readonly IReadOnlyDictionary<int, IReadOnlyList<FhirResource>> encounterIndex;
    private readonly HashSet<int> quarantined;
    internal FhirImportSession(string fingerprint, FhirResource[] resources, FhirImportSummary summary,
        Dictionary<int, int> patients, Dictionary<int, int> encounters, Dictionary<int, IReadOnlyList<FhirResolvedLink>> links,
        Dictionary<int, IReadOnlyList<FhirTimelineEvent>> timelines, HashSet<int> quarantined)
    {
        Fingerprint = fingerprint; Resources = Array.AsReadOnly(resources); Summary = summary;
        Patients = Array.AsReadOnly(resources.Where(r => r.Kind == FhirResourceKind.Patient).ToArray());
        this.patients = new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(patients);
        this.encounters = new System.Collections.ObjectModel.ReadOnlyDictionary<int, int>(encounters);
        this.links = new System.Collections.ObjectModel.ReadOnlyDictionary<int, IReadOnlyList<FhirResolvedLink>>(links);
        this.timelines = new System.Collections.ObjectModel.ReadOnlyDictionary<int, IReadOnlyList<FhirTimelineEvent>>(timelines);
        encounterIndex = resources.Where(r => r.Kind == FhirResourceKind.Encounter && patients.ContainsKey(r.Key)).GroupBy(r => patients[r.Key])
            .ToDictionary(g => g.Key, g => (IReadOnlyList<FhirResource>)Array.AsReadOnly(g.OrderBy(r => r.Times[0].Time.Order).ThenBy(r => r.Id, StringComparer.Ordinal).ToArray()));
        this.quarantined = quarantined;
    }
    public FhirResource Resource(int key) => key > 0 && key <= Resources.Count ? Resources[key - 1] : throw new ArgumentOutOfRangeException(nameof(key));
    public int? PatientOf(int resourceKey) => patients.TryGetValue(resourceKey, out var key) ? key : null;
    public int? EncounterOf(int resourceKey) => encounters.TryGetValue(resourceKey, out var key) ? key : null;
    public IReadOnlyList<FhirResolvedLink> LinksOf(int resourceKey) => links.GetValueOrDefault(resourceKey) ?? [];
    public IReadOnlyList<FhirTimelineEvent> Timeline(int patientKey) => timelines.GetValueOrDefault(patientKey) ?? [];
    public IReadOnlyList<FhirResource> Encounters(int patientKey) => encounterIndex.GetValueOrDefault(patientKey) ?? [];
    public bool IsQuarantined(int resourceKey) => quarantined.Contains(resourceKey);
    public override string ToString() => "Synthetic FHIR import session";
}
