# ADR 0013: Run-local, field-aware de-identification aids in ScriptHost

Status: accepted after user native Windows acceptance (2026-10-20).
Stage 8D parent: 5eebc767eae5f241ac4c7a02c4275ae0305b76fa. Included in the accepted
Stage 9 local checkpoint after its verification gate.

Native acceptance amendment: Patient Deidentifier now uses policy 2 with an original,
embedded version-1 vocabulary and a separately testable execution-local
SyntheticIdentityLibrary. Seed + opaque identity context choose independent names,
fictional address/contact details, DOB/sex and supported demographic values. Stable
slot allocation resolves collisions by bounded deterministic probing; cached complete
profiles never reuse an allocated local identifier. Original demographics do not
select replacements. Names/fictional phones can repeat; local IDs/email/address-unit
allocation distinguish complete profiles. No real dataset or persistent crosswalk.
Both narrow presets retain policy-1 behavior. The identity-resolution rules below
remain; contradictory DOB is ambiguity evidence, not a generation parameter.

The existing report journal gains fixed category counts and preset metadata. Before
publishing the output, Workspace validates and projects its one bounded count-only review
row into a document-owned TransformationReport. The small summary outlives the
Script Editor's last-run export journal and tracks source freshness through weak
references and disposable observers. FileDocumentView presents an expanded labeled
report, surviving tab changes/reconstruction. Arbitrary CSV cells/script names/source
filenames are not rendered in it. No new reporting engine, report sidecar, protocol
or source-saving behavior is introduced. See the current acceptance matrix for the
supported patient/contact/guarantor/insured fields and age conventions.

The Script Library adds three immutable, duplicable presets with new reserved IDs:
Patient Deidentifier, Identifier Pseudonymizer, Timestamp Shifter. Existing bundled
examples, stored user records and their schema remain unchanged. Users review source
before Run and duplicate to change seed or day displacement. No mappings are persisted.

One DeidentificationWorkflow is initialized in the existing compiled submission,
Apply runs in Main, and Complete emits one fixed-schema count-only CSV row. Existing
Batch naming supplies static PHI-safe Review Required filenames. Current and Whole
document use the same code. No separate engine, protocol version, execution dependency,
network, automatic save or source mutation is introduced. Isolation still means a
fresh worker/controlled journal with existing limits and cancellation; trusted C# is
not an operating-system security sandbox.

The version-1 policy is deliberately conservative. Patient keys require a nonempty
decoded PID-3 identifier plus assigning authority, including HD subcomponents and
identifier type when present. Inputs are length framed and domain separated before HMAC-SHA256
with a deterministic seed-derived key. The seed is reproducibility configuration,
not an anonymity secret. Output tokens use 96 digest bits and detect a token collision
within the run. Exact case/whitespace distinctions remain; no fuzzy matching or
name-based primary identity is used. Declared repeated aliases can join one known
patient. Conflicting known groups or contradictory nonempty birth evidence are not
merged; unresolved messages receive distinct deterministic run-order contexts.
Unqualified/truncated identities are not guessed across messages. Qualified patient,
account, visit, order and provider references preserve supported relationships.

The workflow reads Core's materialized hierarchy, not display text, and plans literal
leaf replacements against a frozen model. The narrow internal ScriptMessage batch
writer escapes literals through Core, copies untouched encoded spans (including BOM),
checks existing size/line limits with one final Core parse and publishes that facade
model only after validation/cancellation checks. No parser or general schema editor
is added. The worker now serializes the facade authority, as deferred output already
does; ordinary SetText/SetRaw behavior stays intact. This avoids reparsing an entire
message for every name/address leaf while preserving missing/empty/null values,
repetitions, separators, subcomponents and segment terminators.

The Patient preset suppresses unsupported/narrative/Z/ED content at existing leaves;
it retains a small explicit clinical/code allowlist for review. Identifier and
Timestamp presets are narrow transformations and deliberately retain other content,
counting it as requiring review. Every report states Review required and explicitly
flags container bytes, nested metadata and retained clinical content as unassessed.
Outer container/opaque byte regions are outside the message facade and follow the
existing source-preserving execution pipeline. They can still contain PHI. This is
an explicit residual-risk flag, not a claim they were inspected or made safe.

One calendar-day offset applies to every supported timestamp in a run. Full-day to
second precision and up to four fractional digits retain their original precision
and explicit offset; absent offsets stay absent. No clock, timezone database or
locale influences output. Year/month-only, malformed, overflowed, leap-second or
unsupported composites are not guessed: Patient clears present unsupported values;
Timestamp retains them with an explicit count for review. Originally missing/empty/
explicit-null values stay so. Nested timestamp metadata is outside the supported
catalog and must be reviewed. This policy follows the v2.5.1
[DTM/CX definitions](https://hl7.eu/HL7v2x/v251/std251/ch02a.html) and the field locations
in [patient administration](https://hl7.eu/HL7v2x/v251/std251/ch03.html) and
[financial management](https://hl7.eu/HL7v2x/v251/std251/ch06.html).

Budgets are 100,000 distinct mapping entries per map, 1,024 source characters per
identity component, 16,384 edits/message and 2,000,000 mutations/run, within existing
worker/message/report/output/timeout limits. A smaller mapping cap is configurable.
Exhaustion uses a fixed safe diagnostic. Built-in scripts do not catch transformation
failures: the existing worker/journal owner discards all partial output and reports.
Stale-source attribution and independent unsaved publication remain unchanged.

This does not provide cryptographic anonymity, HIPAA certification, guaranteed
PHI removal, clinical/profile conformance, or permission to share output. Dates,
clinical patterns, repeated tokens and unsupported contexts can remain identifiable.
See the acceptance record for the exact coverage matrix and manual review requirements.
