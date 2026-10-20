# ADR 0012: Deterministic synthetic generation and prospective batch validation

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
