# Versioned generation and FHIR mapping

Core generates bounded closed batches; Workspace captures targets and atomically
publishes documents/insertions; Desktop owns configuration and progress/report
projection. Neither generator runs scripts, transmits output or silently saves it.
[ADR 0012](../decisions/0012-deterministic-synthetic-generation.md) and
[ADR 0015](../decisions/0015-local-synthetic-fhir-integration.md) retain version/lifetime decisions.

## Standalone and connected synthetic HL7

[SyntheticMessageGenerator](../../src/HL7Workbench.Core/SyntheticMessageGenerator.cs)
supports standalone ADT A01/A04/A08, ORM O01 and ORU R01. Obvious Placeholders keeps
original version-1 explicit-seed bytes. The realistic version-2 projection reuses
that clinical template/stream and replaces existing demographic/identifier fields
through bounded Core-parsed ranges, not a second template grammar. Connected
[SyntheticScenarioGenerator](../../src/HL7Workbench.Core/SyntheticScenarioGenerator.cs)
version 2 supports admission/discharge, order/results and combined encounter events,
with shared patient/visit/order identities and explicit variations/overrides.

Both realistic paths share [SyntheticDemographics](../../src/HL7Workbench.Core/SyntheticDemographics.cs)
and existing ordered catalogs/PRNG domains. Changing draw order, catalog order,
normalization, escaping, IDs or clinical timing changes seeded output. Preserve
existing style/version goldens unless an explicitly reviewed version change is
intended. Names/addresses can coincide with real entities; marked synthetic text
is not a reserved postal namespace or global identity guarantee.

[Workspace SyntheticGeneration](../../src/HL7Workbench.Workspace/SyntheticGeneration.cs)
resolves a blank UI seed exactly once after preflight using cryptographic uint
selection (or the injected controllable provider). It then passes the explicit seed
to the unchanged deterministic engine. Explicit 0/42/uint boundaries bypass selection.
Preview does not draw; cancellation publishes nothing; a user retry is a new operation.
Reports retain the actual seed/provenance and Copy seed supports exact replay with
the same settings/style/version/base time. Separate editable mode drafts never
receive automatically resolved values. Old Core/programmatic defaults/absent style
retain compatibility rather than being silently reinterpreted as new UI defaults.

New output remains private until complete insertion/index/save proof succeeds.
Target insertion guards captured document/selection/revision, checks prospective
file boundaries/exact bytes and publishes one history batch. Failure/cancellation
cannot insert a partial set. Generation Report belongs to the output document;
later edits are not a new assessment of the original report.

## Local R4 import and mapping

[FhirPatientImport](../../src/HL7Workbench.Workspace/FhirPatientImport.cs) validates
explicit local JSON/top-level folder paths and bounds discovery. No remote/network
path, recursive directory import or resolving filesystem redirect is accepted.
[FhirR4Importer](../../src/HL7Workbench.Core/FhirR4Importer.cs) handles supported R4
Bundles without a FHIR SDK/service. Raw JSON is transient per input; immutable
bounded projections preserve source-qualified identity, date precision and explicit
reference resolution. Do not infer relationships from names/graph coincidence.
Ambiguous/conflicting/unresolved links remain distinct.

[SyntheticPatientsSession](../../src/HL7Workbench.Workspace/SyntheticPatientsSession.cs)
owns cancelable replacement, pages/search/selections. Failed/canceled replacement
retains the previous completed import; close/clear/reset prevents late publication.
The visible FHIR Patient Workspace retains its existing technical tool ID.

[FhirGenerationPlan](../../src/HL7Workbench.Core/FhirGeneration.cs) mapping version 1
selects eligible ADT A04/A01/A03 and ORU R01 from Selected Patient/Entire Import/
Selected Events. These scopes preserve distinct empty/manual-selection behavior.
Missing dates/statuses/unsupported coding/values are reported, not invented.
The [mapping reference](../fhir-to-hl7v2-mapping.md) is authoritative, not a claim of
universal/bidirectional conversion or clinical completeness.

FHIR's explicit seed affects MSH-10 using source-selection fingerprint and ordinal;
it does not randomize copied demographics, patient/visit IDs, values or event order.
Transport timestamp is generated metadata, not missing clinical time. This is not
the blank automatic synthetic-seed workflow. Direct Generate and optional Preview
share eligibility/size preparation; only current matching preparation can be reused.
[FhirGenerationPublisher](../../src/HL7Workbench.Workspace/FhirGenerationPublisher.cs)
opens one complete independent combined document/report after publication proof.
Per-patient multi-document transactions, managed Synthea execution and remote FHIR
server integration are not implemented.

## Tests and extension checks

Test exact compatibility goldens and all five standalone types, style/seed boundaries,
provider invocation count, cancellation before/inside selection and after generation,
target mutation, history and report ownership. FHIR tests need source scope/collision,
precision/eligibility, unresolved links, all scope modes, stale preview rejection,
direct/preview identical bytes and complete atomic publication.

Evidence: [standalone demographics](../../tests/HL7Workbench.Core.Tests/StandaloneDemographicTests.cs),
[scenario bytes](../../tests/HL7Workbench.Core.Tests/SyntheticScenarioTests.cs),
[seed provider](../../tests/HL7Workbench.Workspace.Tests/SyntheticSeedTests.cs),
[standalone seed](../../tests/HL7Workbench.Workspace.Tests/StandaloneSeedTests.cs),
[FHIR import](../../tests/HL7Workbench.Core.Tests/FhirImportTests.cs),
[FHIR history](../../tests/HL7Workbench.Core.Tests/FhirHistoryScopeTests.cs),
[direct publication](../../tests/HL7Workbench.Workspace.Tests/FhirDirectGenerationTests.cs).
User procedures and exact current settings are in
[synthetic tasks](../user/generate-synthetic-hl7.md) and [FHIR tasks](../user/import-fhir-and-generate-hl7.md).
