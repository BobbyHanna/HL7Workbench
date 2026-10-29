# ADR 0012: Deterministic synthetic generation and prospective batch validation

## Stage 16D amendment — shared realistic standalone projection (2026-10-29)

Both dialog modes explicitly default Realistic Synthetic. Standalone v2 projects
only existing demographic/transport/identifier fields from the unchanged v1
renderer using bounded Core-parsed ranges and complete parser/validator/serializer
verification. This avoids maintaining a second standalone clinical template and
keeps every original field boundary, segment order and clinical-stream draw.
Obvious Placeholders dispatches directly to original v1 rendering. Scenario v2
bytes for both styles are unchanged after moving the ordered catalogs and realistic
profile/provider/facility/location logic to internal SyntheticDemographics.

Old standalone Core options/JSON without a style retain v1 meaning; there is no
persisted generator preset store to migrate. New dialog configurations explicitly
select realistic. Standalone realistic transport and patient-prefix overrides reuse
existing identifier validation; unsupported legacy overrides fail explicitly.
Scenario clinical overrides are unchanged. Distinct identifier domains and shared
provider/facility contexts do not add HL7 fields or change clinical examples.
Workspace keeps automatic drafts unresolved through preflight until the single
actual seed is selected, then normalizes transport defaults. Both styles use the
same existing publication/history transaction, report tool, version/style metadata
and Copy seed action. No additional seed provider, UI window, Dock identity,
persistence schema, external data or dependency is introduced. Prior 16A–C statements
that standalone supports placeholders only are historical. Native acceptance remains
pending; no Git publication, package or Stage 17 work is authorized.

## Stage 16C amendment — shared standalone/scenario automatic seeds (2026-10-29)

Both dialog modes now start with a blank optional seed. Standalone GenerateAsync
opts into the same existing post-preflight cryptographic provider and publication
pipeline as scenarios. Resolution happens once per operation for either flow; explicit
uint values bypass it. Core options/algorithms/versions/templates/identifiers remain
unchanged, including standalone v1 bytes for explicit 0/42/other supported seeds.
Programmatic default behavior remains deterministic unless automaticSeed is requested.
The existing report/Copy seed action already supports standalone; it now records
the actual automatic standalone seed too. Separate drafts retain manually typed
values and never receive resolved automatic seeds. Preview does not fabricate seed-42
control IDs. Negative seeds remain unsupported by uint. No new randomization system,
dependency, persistence schema, output limit or transaction change is introduced.
Historical 16B standalone default/blank-42 notes below are superseded for the UI.
Native acceptance remains pending; no Git publication, package or Stage 17 is authorized.

## Stage 16B amendment — operation-scoped seed selection (2026-10-29)

Blank scenario UI seeds opt into one Workspace cryptographic uint draw, after
configuration/target preflight and before generation. Preview/validation consume no
randomness. The existing Core engine receives an explicit resolved seed; algorithms,
versions and goldens are unchanged. Explicit seeds (including 0 and 42) bypass the
provider. Standalone v1 retains its separate seed draft and default/blank 42 behavior.
Automatic drafts remain unnormalized until resolution, preventing seed-42 preview
defaults from freezing the wrong facility metadata. Result options/report contain
the same actual seed, with automatic/explicit provenance. The existing report adds
Copy seed using the platform clipboard; no content/report persistence changes.
Cancellation after resolution does not redraw; a later user retry is a new operation.
An optional coordinator Func<uint> allows collision-free deterministic tests; the
production default uses RandomNumberGenerator.Fill. No clock/GUID/randomness enters
Core, and there is no fallback seed or second generator. No publication, packaging,
Git operation or Stage 17 work is authorized by this change.

## Stage 16A amendment — realistic demographics (2026-10-28)

The owner authorizes realistic synthetic demographics in the existing scenario
engine. Scenario generator version 2 defaults new configurations to Realistic
Synthetic; Obvious Placeholders pins the previous scenario-v1 bytes. Standalone
generator version 1 and all original presets/public contracts are unchanged.
The engine uses small original ordered vocabulary and separate deterministic PRNG
domains for profiles/providers/facilities/locations, retaining clinical-stream draws
across styles. Existing patient/visit/order context and rendering/publication remain
the sole authority. Profile text uses Core's existing escape codec. No real-person
dataset, service, dependency on Scripting or second generator is introduced.

Unset application/facility properties mean style/seed defaults; explicit values
(including old defaults) always take precedence. Normalization captures resolved
transport values. Patient, visit, message, placer/filler, provider and facility
identifiers use distinct local synthetic domains. Adult profiles, marked XAD
addresses, reserved fictional phones and routine coherent locations are test data;
names/addresses/facility labels can coincide with real-world entities. The address
marker is not an officially reserved namespace. Both clinical panels/ranges remain
unchanged. UI/reporting add style metadata without new Dock/persistence identities.
See [Stage 16A construction, compatibility and native acceptance](../stage16-synthetic-generation.md).
Native acceptance is pending; no Git publication, packaging or Stage 17 is authorized.

## Stage 16 amendment — coherent scenarios (2026-10-28)

Implementation authorized by the owner's Stage 16 request; native acceptance pending.
The original standalone version-1 public APIs, defaults and golden bytes below are
preserved. A separate SyntheticScenarioOptions / SyntheticScenarioGenerator version
1 adds bounded admission/discharge, order/result and combined workflows. Shared
fictional patient/visit/provider/order context owns all related identifiers. The
scenario renderer uses the same explicit PRNG and Core segment helpers and validates
each bounded message through Core's parser/serializer. Inpatient A01 and outpatient
A04 are alternative starts, with inpatient-only A02 and an A03 end; all reports are
linked ORM O01 / ORU R01 updates of one collected panel. Supported version is 2.5.1.

Immutable typed shared overrides and an explicit field-target whitelist customize
prefix, order code, result status and first numeric analyte without permitting
individual ID replacement, arbitrary paths or raw string injection. Invalid factors,
multiplied message count, temporal overflow and contradictory/inapplicable options
fail before allocation/publication. The existing output and destination limits stand.
Seeded output has no clock/environment dependency. Scenario MSH-10 is a compact
18-character synthetic seed/ordinal; cross-batch uniqueness is not promised.

Workspace accepts a closed scenario batch into the same prospective streaming proof
and atomic insertion/history operation. No alternate document publication path or
serializer is introduced. The result retains the standalone Options compatibility
property for transport count/seed/date; ScenarioOptions is authoritative for scenario
content. Session-only count reports attach before document open, update on successful
insertion and remain historical through later edits/undo. Canceled/failed operations
retain the previous report and show their terminal status separately. A separate
synthetic report property preserves the existing FHIR report API; the existing
Generation Report tool can display both. No report values enter layout persistence.

Desktop extends Generate Messages with scenario choices and collapsed advanced
settings. The View menu and Dock identities are unchanged. Existing isolated
ScriptExecution operates on captured documents, rather than generating fields; an
implicit generation callback would conflict with its publication/provenance model.
Generation-time scripting is deferred; typed overrides are the current extension
boundary and explicit post-generation scripting retains its existing containment.
No packages, engine, worker privileges or Stage 18 API-reference work are added.
See [guide, limits and acceptance](../stage16-synthetic-generation.md).

Checkpoint status, 2026-10-20: user native Windows acceptance passed. One local
Stage 8D checkpoint is authorized after fresh verification. The prior pending
status below is historical; Stage 9/icon work is excluded from this checkpoint.

Status: implemented for Stage 8D; native Windows acceptance pending. The Stage 8C
checkpoint is ddc18ae63df7ddb31ba55ee35c3c6feffd0d3891. Stage 8D remains uncommitted.

## Decision

Core owns a small closed-format generator for HL7 v2.5.1 ADT A01/A04/A08, ORM O01
and ORU R01. It accepts normalized immutable options and returns an immutable whole
batch. It has no Workspace/UI, source-record, network or configuration dependencies.
The version-1 contract fixes the xorshift32 algorithm (seed XOR 0x9E3779B9, zero
mapped to 0x6D2B79F5; shifts 13, 17, 5), field construction order, invariant number
formatting, UTC whole-second timestamps, and CR segment endings. UTF-8 export has
no BOM. Defaults are seed 42, 2030-01-01T00:00:00+00:00, prefix SYN, A01, count 1.
Same normalized options produce identical bytes within version 1. Future versions
may change output; permanent golden/determinism tests make changes explicit.

Control IDs combine normalized prefix, eight hexadecimal seed digits, event and
one-based four-digit batch index. They are unique within a batch, intentionally
repeat across identical requests, and are unrelated to Workspace MessageIds.
They do not promise uniqueness across distinct batches with reused configurations.
Patients/providers/facilities/addresses are conspicuously synthetic. Contact data
uses example.invalid. There is no source-derived default, clock-derived content,
uncontrolled random state, real directory of names, telemetry or external service.
Session/temp-storage GUIDs and elapsed-time diagnostics belong to existing
Workspace ownership and do not enter generated content.

Workspace runs Core generation off the caller context. Generation checks cancellation
per message and returns no partial output. Progress delivery is bounded. It captures
the destination and its content/selection version before awaiting; closure, mutation
or navigation during generation refuses insertion rather than moving its anchor.
Changing the active tab does not retarget a captured destination.

The existing batch insertion operation is split into private preparation and
publication steps. Arbitrary insertion callers keep their existing validation.
Only the closed Core batch bypasses redundant hierarchy materialization; its count,
aggregate/message/line bounds and injection-free fields are already enforced.
All destination capacity, encoding, source-boundary and retained/history budgets
are checked before publication. Existing drafts retain their full parse validation.

Before generated publication, Workspace locks mutation with IsPreparingInsertion,
waits for its document work semaphore, composes prospective metadata once, and
streams a private candidate through the established save plan. The shared Core
indexer proves message membership/framing/gap coordinates, and byte comparison proves
all intended pieces. Existing original regions stream without decoding. The
candidate uses an owned leased temporary directory and DeleteOnClose; failure,
cancellation and disposal drain the work gate before snapshot cleanup. No destination,
save point or original snapshot is changed by this proof. The normal user save
pipeline still runs on Save/Save As.

Only after the proof and final cancellation check does one sequence splice publish
all buffers/IDs, select the first generated ID, invalidate discovery state and add
one document history operation. No one-message loop or rollback is used. Undo
removes the complete batch; Redo restores identical session IDs and order. The
existing root keyboard authority and native raw-input transactions are unchanged.

New output starts as a privately owned empty snapshot document, receives the same
inserted-origin batch, finishes its bounded active parse, then opens as independent
unsaved output with a useful suggested title. Its filesystem destination is null;
the existing generated-title and Save As/close workflows apply. It has no fabricated
original message ranges or ordinals. The empty base supports undoing the entire batch.

## Consequences and limits

Requests are bounded to 1,000 messages and 4 Mi UTF-16 units, additionally constrained
by the existing 1 Mi/message, 16 Ki/line, 100,000 logical messages, 16 Mi retained
UTF-16 units (32 MiB), and 4,096 history operations. No silent truncation occurs.
The UI preview calculates identifiers/configuration only; it never generates a batch.

The proof requires temporary disk space proportional to the prospective serialized
document and streaming I/O over it. This deliberately trades disk work for an exact
pre-publication safety result, with bounded RAM and cancellable background work.
Even an unrelated unfinished inserted draft can block this proof: complete/undo it
or generate into a new document. Unsupported batch/provisional/ambiguous containers,
unsafe unterminated joins and unsafe encodings are rejected without repairs.
Safe MLLP boundaries and mixed per-message delimiters use existing save policy.

Examples are coherent synthetic test content, not a full schema designer, clinical
validation engine, or implementation-guide conformance claim. The intended segment
roles/structures were checked against the HL7 v2.5.1 standard's
[ADT](https://hl7.eu/HL7v2x/v251/std251/ch03.html),
[order](https://hl7.eu/HL7v2x/v251/std251/ch04.html) and
[observation](https://www.hl7.eu/HL7v2x/v251/std251/ch07.html) chapters.
