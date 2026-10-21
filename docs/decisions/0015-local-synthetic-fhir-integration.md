# ADR 0015: Session-only synthetic FHIR import and explicit v2 mapping

Status: Stage 11 authorized 2026-10-21 after the successful Stage 10 checkpoint
1b04e1849b2ba3c03d135cf324bae28b8af4cba8. These decisions precede implementation.
Implemented under Stage 11; user native Windows acceptance confirmed 2026-10-21,
including complete-history improvements. One exact local checkpoint is authorized
before Stage 12. Earlier pending/no-commit notes are historical.

Use a narrow System.Text.Json R4 projection, not a new FHIR SDK, database or server.
The seven supported types are Patient, Encounter, Condition, Observation,
DiagnosticReport, Organization and Practitioner. Unknown resource types are counted
and omitted, not interpreted. Collection and transaction Bundles are supported;
transaction requests are never executed. R4-compatible instances do not prove their
producer/version or synthetic status. Require explicit user synthetic/test-data
confirmation on every import; display that JSON may contain real PHI. A Synthea
Patient identifier is a provenance hint, never proof of safety. Reject explicit
non-R4 versions and known incompatible shapes. No network, Synthea installation,
automatic download, narrative rendering or attachment decoding participates.

Workspace selects local files or a top-level folder and parses one bounded file at
a time off-context. Core projects immutable bounded fields, not retained raw JSON.
Ceilings: 64 files, 16 MiB/file, 64 MiB total, 50,000 resources, 2,048 patients,
65,536 UTF-16 units/JSON string, depth 32, 2,000,000 JSON nodes, 200,000 references,
1,024 links/resource, 8 Mi retained projected characters and 100,000 timeline events.
Reject an entire replacement on malformed data or budget failure; retain the old
session. Session clearing/closure cancels pending work and prevents late publication.
No imported data, identifiers, file paths or search literals enter settings/layouts.
Only user-selected generated HL7 may leave the session through normal explicit Save.

Each resource has input/bundle/entry/contained provenance and an opaque session key.
Resolve exact fullUrl locally, REST-base-qualified relative references, bundle-local
Type/id (a documented offline fallback for UUID bundles), unique cross-input Type/id
and container-scoped #id/#. Never fetch a reference. Version/history, conditional and
logical-identifier-only references are unsupported. Duplicate IDs/fullURLs inside a
bundle reject import. Across inputs, identical shared Organization/Practitioner
type/id/fullUrl/content can deduplicate. Clinical records retain input provenance:
even identical JSON can carry relative links with different scope. Repeated clinical
fullUrls remain ambiguous, including when reached by relative reference. Report
ambiguous alias counts separately from affected references. Do not merge by names.
Clinical records need an explicit unambiguous Patient subject. Encounter links must
resolve to that same patient's encounter; conflicting records are quarantined from
generation. Containment never implies a subject. Resolve links in bounded passes,
without recursive graph traversal; cycles cannot confer patient membership.

Timeline events retain the actual timestamp text and precision: encounter start/end,
condition onset/recording, observation effective/issued, report effective/issued.
Missing/invalid dates remain explicit; partial dates are labeled and ordered only
at their available precision, with deterministic provenance ties. Do not invent
clinical timestamps. ADT requires complete, offset-qualified event timestamps and
supported encounter evidence. A01: inpatient IMP, in-progress/finished, valid start.
A04: supported non-inpatient class, in-progress/finished, valid start. A03: finished
inpatient, valid end not before start. Other cases are ineligible with fixed reasons.

Version-1 mapping uses source patient/visit identity with deterministic qualified
transport identifiers, not names. Identifiers are stable across selected ADT/ORU and
seed changes. Duplicate visit fullUrls with independently resolved contained subjects
are qualified by captured input rather than reusing a conflicting visit authority.
MSH-10 combines seed, selected-source fingerprint and ordinal, unique
within each output and deliberately repeated for identical requests. Fixed explicit
transport time defaults to the existing generator's 2030 UTC default; it is labeled
generated metadata, never substituted for missing clinical times. Selection/order,
source content and seed determine bytes, independent of clocks and tab changes.

Generate one ORU per explicitly linked eligible DiagnosticReport with compatible
observations, and standalone ORUs for selected observations not covered by those
reports. Never group by matching names/codes or proximity. Quantity/string/boolean/
CodeableConcept values have explicit safe mappings; missing/unsupported values or
statuses are skipped with count-only warnings, never normal results. Known coding
systems and UCUM map explicitly; unknown systems are unsupported rather than relabeled
as known. No claim of universal conversion, full clinical fidelity or certification.

Core returns a closed validated mapped batch under existing Stage 8D ceilings (1,000
messages, 4 Mi total UTF-16, 1 Mi/message, 16 Ki/line). Workspace reuses the existing
prepared insertion/prospective save/index/byte-proof publication, with a narrow second
Core-batch overload. Output stays private until complete, then opens as one new
unsaved document. Never modify existing HL7. No partial document on failure/cancel.
Attach a document-owned count-only Generation Report before publication, preserving
captured session identity/fingerprint, resource/event/warning counts and limitations
independently of tool lifetime. Later output edits do not recompute that report.

Synthetic Patients is an optional release-on-close Dock tool with bounded patient,
encounter and timeline pages and standard themed controls. Closing/resetting releases
the imported session; layout remembers placement only. Core/Workspace remain UI/Dock
independent. Existing Stage 8D, ScriptHost, history, save and extraction contracts stay.

Stage 11 native-acceptance improvement: default Selected Patient and optional Entire
Import expand supported eligibility through the same candidate/mapping authority;
Selected Events preserves the advanced workflow and selections across scope changes.
Combined output groups stable patient identities, then clinical time/provenance ties.
Global ordinals keep control IDs unique. Preview counts complete eligibility but
retains at most 1,000 candidates, then measures/discards one message at a time under
the existing output ceilings; no truncation/oversized output buffer. Scoped exclusions,
ADT ineligibility reasons, output limits and preview-versus-generated totals remain
count-only. Separate per-patient documents are deferred because the publisher has
no atomic group-of-tabs rollback; reuse its one-output boundary without redesign.

References checked 2026-10-21: [Synthea R4 exporter](https://github.com/synthetichealth/synthea/blob/master/src/main/java/org/mitre/synthea/export/FhirR4.java),
[R4 Bundle](https://hl7.org/fhir/R4/bundle.html), [R4 references](https://hl7.org/fhir/R4/references.html),
[R4 Encounter](https://hl7.org/fhir/R4/encounter.html), [R4 Observation](https://hl7.org/fhir/R4/observation.html),
[v2.5.1 ADT](https://hl7.eu/HL7v2x/v251/std251/ch03.html) and
[v2.5.1 results](https://hl7.eu/HL7v2x/v251/std251/ch07.html).
