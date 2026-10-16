# ADR 0007: Unified C# lifecycle

Status: Accepted; final native Windows acceptance confirmed on 2026-10-16. Supersedes ADRs
0005/0006 only for script kinds, lifecycle authoring, script schema and attribution
kind metadata. Their preservation, transaction, bounded streaming, privacy, worker
containment and tool-lifetime contracts remain in force. Earlier designs are history.

## Verified Roslyn mechanism

A local probe against the pinned Roslyn 5.9.0 assemblies confirmed that top-level
script methods bind across a fixed continuation to delegates sharing the original
submission instance. Initialization runs once, shared variables persist, Main locals
reset, helper calls work, and another invocation gets fresh state. No new dependency.
The temporary probe was removed after verification; UnifiedLifecycleTests and the
published-worker smoke preserve executable coverage of these findings.

Compile the user submission once per job. Inspect its top-level method declarations
and Roslyn method symbols: exactly one instance, synchronous, nongeneric,
parameterless void Main; optional Complete has the same signature. Compiler errors
retain original locations. Missing/ambiguous/unsupported entry points fail before
initialization. Nested types/helpers do not count as entry points. No regex discovery.

One fixed host-owned continuation binds `new System.Action[] { Main, Complete }`
(omitting Complete when absent), with its own non-user source identity. Both
submissions are compiled before Ready. Invoke this runner once under the initialization
deadline, retain the bound delegates, and call them for each message / completion.
There is no per-message recompilation or growing continuation chain. Only host code
wires delegates; scripts author ordinary C# methods. No script-kind inference.

## Public contract and phases

Every scope creates a fresh worker and one ScriptBatch context. Initialize once;
Main once per captured target in source order; Complete once on success, never after
a processing failure. Current message has one target and follows the identical
lifecycle. Globals: Message, Batch, Cancellation. Message is dynamically resolved
only while Main runs; access in initialization/Complete fails LifecyclePhase.
Batch remains an advanced facilities object, never a script kind.

Reports are defined in initialization; rows can be emitted in any active phase.
Defer/Hold accept only the current Message in Main; Emit accepts this run's held
copies only in Complete. Exclude, exact-byte preservation, source-ordinal validation,
framing restrictions, queues, journal/export transactions and all existing bounds
remain. Arbitrary trusted C# state is contained through the existing Windows Job
Object, not object-level accounting. No generation or source mutation is added.

## Deliberate incompatibility and storage

The user explicitly discarded compatibility with development Message/Batch syntax.
Remove ScriptMode and callback-registration properties. Definition API/protocol move
to version 2; mismatches are refused. Library version 3 stores ID/name/source and
a migration-warning flag for retained obsolete duplicates, without kind metadata.
Version-1/2 records remain on disk and open as editable obsolete entries. Run is
disabled until an explicit Save adopts version 3; the author must rewrite to Main.
No automatic syntax conversion or execution of an obsolete record. Invalid new bodies
fail compiler/entry-point validation before initialization. Unsupported/corrupt records
retain the existing safe failure behavior. No library deletion or unrelated preference
migration. Save remains atomic/conflict checked; duplication preserves obsolete status
until explicit adoption, preventing an accidental bypass of the migration decision.

## Desktop

One New Script action; no kind labels, type menu or kind metadata. Apply to: selects
Current message / Whole document. Library drafts, immutable examples, shortcuts,
fonts, highlighting, narrow scrolling, docking/floating and placement remain.
Last-successful-run attribution captures execution-time script display name and scope
only, and updates only with successful report replacement. No persistent report history
or source/row values in metadata/logs. Bundled simple transformation and conservative
synthetic-identity examples use Main/Complete; the latter is not clinical de-identification.
