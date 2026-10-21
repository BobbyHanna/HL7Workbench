# FHIR R4 → HL7 v2.5.1 mapping, version 1

Implemented by `FhirR4Importer`, `FhirClinicalTime` and `FhirGenerationPlan` in Core;
Workspace handles local input and atomic new-document publication. This specification
describes a narrow synthetic test workflow, not lossless conversion, a complete R4
validator, clinical advice or interface certification. See [ADR 0015](decisions/0015-local-synthetic-fhir-integration.md)
and the [user guide](synthea-fhir-import.md) for ceilings and operation.

## Source, references and identity

Accept UTF-8 JSON collection/transaction Bundles, with optional UTF-8 BOM. Explicit
non-4.0.1 version declarations, known incompatible profiles/shapes, duplicate JSON
properties, malformed projected types and conflicting choices fail the whole import.
Most instances do not declare their FHIR version; absence is not proof of R4 or of
Synthea origin. Recognize Patient, Encounter, Condition, Observation, DiagnosticReport,
Organization and Practitioner. Other types retain bounded identity metadata and are
counted unsupported; their contents are not interpreted. No transaction is sent.

Each resource retains input digest/number, Bundle entry, type/id, fullUrl, content
digest, optional container and a session-local integer key. Exact duplicate type/id
or fullUrl inside a Bundle is rejected. Across files, only identical shared
Organization/Practitioner type/id/fullUrl/content can deduplicate. Clinical resources
never coalesce just because their JSON matches: relative links can have different
scope. Names do not determine identity. Ambiguous aliases and affected links are
reported separately.

Resolve case-sensitive exact fullUrl, REST-base-qualified relative references,
Bundle-local Type/id (offline fallback for UUID Bundles), unique global Type/id and
owner-scoped `#id`/`#`. A REST source never falls back to another server's Type/id.
HTTP fullUrls with a recognized resource suffix must agree with resource type/id.
Never fetch links. Conditional/query, history, logical identifier-only and unsupported
reference forms are counted, not resolved heuristically. Repeated clinical fullUrls
remain ambiguous, including when reached through a relative alias. Declared target
type must agree. Contained resources require their own explicit patient subject.

Patient association follows only resolved Patient subjects. A clinical encounter
must resolve to an Encounter with that same patient; otherwise the clinical record
is quarantined from generation. Report results must be non-quarantined Observations
with that same patient and, if the report has an encounter, explicitly that encounter.
Missing/ambiguous/conflicting links never confer membership. Resolution uses bounded
passes, not recursive traversal; cycles and unsupported `derivedFrom` do not infer
clinical ownership. Unassociated records remain in import counts.

PID-3 uses the first nonempty Synthea-system identifier, otherwise the first MR-typed
identifier, otherwise Patient.id. It never chooses a first arbitrary SSN. CX.4 is
`SF` plus 16 hex SHA-256 characters of the source identifier system; CX.5 is PI.
Duplicate identifier/system pairs across patients fall back to Patient.id with
source-qualified CX.4 and a QualifiedIdentity warning. Visit PV1-19 uses Encounter.id,
CX.4 derived from unambiguous fullUrl (otherwise input digest/type/id), CX.5 VN.
Conflicting visit fullUrls with independently resolved contained patients also use
input qualification. Source patient/visit IDs remain consistent across ADT/ORU and
seed changes; shortened hash qualifiers are deterministic test identifiers, not a
global identity registry or mathematical collision guarantee.

## Event selection and timestamps

Stage 11 native-acceptance improvement adds complete selection scopes without new
clinical mappings. **Selected Patient** (UI default) considers all that patient's
supported encounters, reports and observations, including observations without an
encounter. **Entire Import** does so for every imported patient. Neither requires
manual events. **Selected Events (advanced)** retains the bounded targeted request
contract; programmatic FhirGenerationRequest defaults to SelectedEvents for backward
compatibility, while Workspace explicitly defaults to SelectedPatient. Scope changes
retain manual selections, invalidate preview, and never add inferred associations.

An encounter selection requests its supported ADT start/end and associated ORU data.
Timeline selections add specific resources/events. Condition events are browse-only;
unsupported selections are counted. Selecting effective/issued rows of the same
Observation or Report produces one resource candidate, not repeated messages for
each timestamp. Candidate chronology uses actual encounter start/end or effective
time; issued is not substituted for missing effective time. Combined output groups
patients by stable source identity/content (fullUrl or input/type/id, plus resource
content digest), then sorts each patient's messages by clinical time and stable
resource identity/content/event ties. Missing dates remain last within a patient.
OBX order uses effective time and stable source ties. File selection, display names,
search/filter order and active tab do not change this order. Patient/visit identifiers
are identical across complete/targeted scopes for the same source records.

| Event | Required source evidence | Ineligible/lossy cases |
| --- | --- | --- |
| ADT A01 | Encounter class IMP in known v3 ActCode system; in-progress/finished; complete start with offset | Planned/unknown classes/statuses, missing/partial start, end before start |
| ADT A04 | AMB, EMER, OBSENC, HH or VR in that system; in-progress/finished; complete start | Not every non-inpatient class is supported; no fabricated admission |
| ADT A03 | Finished IMP; complete valid start/end; end ≥ start | No discharge for ambulatory visits, ongoing stays or absent end |
| ORU R01 report | Supported Report status/code and ≥1 explicitly linked eligible result | Bad result links/results omitted with counts; no inferred panels |
| ORU R01 standalone | Selected supported Observation not covered by selected report groups | No artificial encounter or panel association |

Year/month/day retain source precision in HL7 TS. Complete dateTimes require ASCII
`yyyy-MM-ddTHH:mm:ss[.fraction](Z|±hh:mm)` and at most four fractional digits (v2.5.1
TS precision). Higher precision, leap seconds, invalid dates and missing offsets are
explicitly unsupported/invalid; they are never rounded into a clinical timestamp.
Dates sort coarsely at their available precision; the sorting coordinate is never
emitted as an invented instant. Missing/invalid dates sort last and fields remain
empty with count warnings. Inconsistent period ends are omitted and counted.

## Direct source mappings

All literal text uses Core HL7 escaping; controls become explicit UTF-8 hex escapes.
No literal can inject a segment, repetition, component or HL7 null token. Canonical
output is UTF-8 without BOM with CR segment terminators and standard delimiters.

| FHIR source | Target | Transformation / required data / omission |
| --- | --- | --- |
| Patient identifier/id | PID-3 CX.1/.4/.5 | Qualification policy above; required resource ID |
| Patient official name, otherwise first | PID-5 XPN.1/.2/.3 | Family, first given, remaining given joined by spaces; no invented name. Up to 8 given strings; other names omitted/count |
| Patient.birthDate | PID-7 | Valid date at actual year/month/day precision; invalid/instant omitted with warning |
| Patient.gender | PID-8 | male M, female F, other O, unknown U; absent/other empty |
| First Patient.address | PID-11 XAD.1/.3/.4/.5/.6 | Up to 8 lines joined by escaped newline; city/state/postal/country; other addresses omitted/count |
| Encounter actual event timestamp | EVN-6 | Start for A01/A04; end for A03; complete offset timestamp required |
| Encounter.class | PV1-2 | Known ActCode: IMP I; EMER E; AMB/OBSENC/HH/VR O; unknown system/code empty in ORU, ineligible ADT |
| Explicit ATND participant → Practitioner | PV1-7 XCN.1/.2/.3 | Practitioner.id/family/first given; only known v3 ParticipationType ATND. No inferred attending |
| Encounter.id | PV1-19 CX.1/.4/.5 | Stable qualified visit identity above |
| Encounter.period | PV1-44/45 | Available valid start/end precision; omit reversed complete end; never use transport time |
| Report.id or standalone Observation.id | OBR-3 EI.1/.2 | Source ID and generated SYN-FHIR namespace; no source order number invented |
| Report.code or standalone Observation.code | OBR-4 CE | Code/display/system and one alternate; text-only concept allowed; unsupported code systems skip candidate |
| Report/Observation effectiveDateTime or period | OBR-7/8 | Actual effective/start and valid non-reversed end; unsupported/missing empty |
| Report/Observation.issued | OBR-22 | Actual issued time; does not become observation analysis time |
| Report/Observation.status | OBR-25 | Status policy below; unsupported skips candidate |
| Observation.code | OBX-3 CE | Code/display/system plus one alternate; required representable concept |
| Observation.value[x] | OBX-2/5 | Typed policy below; missing/unsupported never normal results |
| Quantity.code/unit/system | OBX-6 CE.1/.2/.3 | Code/display and UCUM only for declared `http://unitsofmeasure.org`; absent system stays unqualified; unknown system skips result |
| Single unconditional referenceRange | OBX-7 | Text, or compatible-unit numeric low-high, <=high, >=low; age/appliesTo/type conditions, comparator bounds, multiple/incompatible ranges omitted/count |
| Observation.interpretation | OBX-8 repetitions | Known v3 ObservationInterpretation N/A/H/L/HH/LL/AA/U/D/B/W only; others omitted/count; never derive from range |
| Observation.status | OBX-11 | final F; preliminary P; amended/corrected C; all other statuses skipped/count |
| Observation.effectiveDateTime / period.start | OBX-14 | Actual available valid precision; no substitution from issued/transport |

Report OBR-25 additionally maps partial to A and appended to C. Absent, registered,
cancelled, entered-in-error, unknown and other unsupported statuses skip the report;
eligible separately selected observations can still form standalone ORUs.

| Observation value | OBX-2 | OBX-5 / restrictions |
| --- | --- | --- |
| Quantity | NM | Exact JSON decimal expanded without binary floating point or rounding; ≤128 output characters; exponent -100…100 |
| Quantity with <, <=, >, >= comparator | SN | Comparator in SN.1, exact number SN.2; other comparators unsupported |
| Nonempty String | TX | Escaped literal; empty/null is missing |
| Boolean | CWE | true Y^Yes^HL70136, false N^No^HL70136 |
| CodeableConcept | CWE | Code/display/system, alternate, original text in CWE.9; text-only concept allowed |
| Missing/dataAbsentReason or other value[x] | none | Skipped/count; value plus dataAbsentReason is malformed and rejects import |

Coding namespaces map explicitly: LOINC→LN, SNOMED CT→SCT, UCUM→UCUM,
RxNorm→RXNORM, ICD-10-CM→I10C, ICD-10→I10, CVX→CVX, using their declared canonical
FHIR system URIs. Missing system maps 99LOCAL with an omission warning; unknown
systems are not relabeled as known. Keep the first two representable codings in
source order; extra/unknown coding loss is counted. No terminology lookup, unit
conversion or value/range clinical interpretation is performed.

## Generated metadata, defaults and exclusions

MSH uses fixed synthetic/test routing (SYN-FHIR/SYNTHETIC → TEST-RECEIVER/TEST),
processing ID T and version 2.5.1. MSH-9 structures are ADT_A01 for A01/A04,
ADT_A03 for A03, ORU_R01 for R01. MSH-7 and EVN-2 use the explicit transport time
(default 2030-01-01 UTC, normalized whole seconds), never the clock or a replacement
for clinical time. EVN-1 is the selected event. PID/PV1/OBR/OBX set IDs are generated
sequence numbers. These metadata are disclosed in preview/report.

MSH-10 is exactly 20 characters: `SF` + seed (8 uppercase hex) + selected-source
fingerprint prefix (6 hex) + one-based message ordinal (4 decimal). Unique within
each ≤1,000-message output; identical requests deliberately repeat IDs. Session GUID
is report attribution only and does not affect output. Fingerprints use actual source
bytes in order-independent digest order; selection fingerprints stream stable patient/
candidate/result identities in final order. The global message ordinal spans all
patients, preserving uniqueness within a combined output. This acceptance correction
changes the selection fingerprint from the initial pending implementation; no
previous Stage 11 control-ID byte contract was natively accepted.
There is no claim of global uniqueness across different datasets/requests.

Conditions, components, effectiveTiming/unsupported choices, medication/procedure
resources, report attachments/conclusions, narratives, identifiers beyond the selected
patient ID, telecom, insurance, locations/beds, additional provider roles and richer
demographics are not converted. Some omitted projected details have counts; the
report is not an element-by-element inventory of every ignored FHIR field. No DG1,
ORM, attachment, waveform, extension or custom-profile conversion is claimed.
Organization/provider metadata can be browsed through explicit resolved links;
organization does not silently become a fabricated MSH facility or patient location.

## Publication and report

Core validates the entire closed batch with existing parser/edit ceilings. Workspace
uses the existing prepared insertion, prospective-save/index and exact-byte proof
on a private empty document. Only after completion/current-capture checks does one
new unsaved tab become visible; no partial output or existing-document mutation.
Generated messages use normal inserted identities/history, editor, extraction and
Save As. Stage 8D contracts and ScriptHost isolation are unchanged. Combined document
is the only output organization: separate per-patient documents are explicitly
deferred because the existing publisher has no atomic multi-document rollback.
No loop of individually published patient outputs is used.

Preview counts all eligible candidates under the import ceilings, but retains at
most 1,000 candidates. Over-count plans retain no partial candidate sequence and
cannot generate. At or below that count, preview renders/discards one message at a
time to measure escaped UTF-16 output, stopping at existing field/segment/message/
4 Mi total ceilings. Counts remain complete; size is exact only when measurement
completes, otherwise a labeled lower bound/unavailable. No whole-output preview buffer,
silent truncation or partial publication. Preview and generation both check cancellation.

The document owns an immutable count-only report with mapping version, captured
session/fingerprint, seed/transport time, imported counts, event/OBX totals, reference
states, scope, patients included, encounters considered, skipped resources, ineligible
ADT events/reasons, skipped/unsupported values, output limits and fixed warning
categories. Preview eligibility/estimated totals are distinct from successfully
generated totals in the published report. Ineligible reasons are bounded enums for
class/system, status, missing complete start, reversed period and discharge evidence.
Whole-import unsupported/unassociated counts are labeled separately from scoped
exclusions; a selected patient cannot be assigned unassociated records for counting.
Entire Import counts those records as skipped without guessing their patient.
Ineligible ADT events are counted separately from skipped non-ADT resources. Warnings
are counts of selected mapping occurrences, not mutually exclusive resource totals;
one source result can contribute to several warnings/report groups. Whole-import
counts include resources outside the current selection. No names, patient IDs,
clinical values, file paths or raw JSON enter the report or operation diagnostics.
Failed operations use fixed failure status; cancellation is separate. Neither publishes
a successful document/report. "Complete history" never claims clinical completeness.
Later manual output edits are not reassessed. The report is session metadata, not
an HL7 segment or a sidecar saved automatically.

Primary specifications checked 2026-10-21: [Synthea R4 exporter](https://github.com/synthetichealth/synthea/blob/master/src/main/java/org/mitre/synthea/export/FhirR4.java),
[R4 Bundle](https://hl7.org/fhir/R4/bundle.html), [references](https://hl7.org/fhir/R4/references.html),
[Encounter](https://hl7.org/fhir/R4/encounter.html), [Observation](https://hl7.org/fhir/R4/observation.html),
[v2.5.1 patient administration](https://hl7.eu/HL7v2x/v251/std251/ch03.html),
[v2.5.1 results](https://hl7.eu/HL7v2x/v251/std251/ch07.html).
