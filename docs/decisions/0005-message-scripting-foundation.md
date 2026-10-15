# ADR 0005: Message-script worker foundation

Status: Stage 5A native Windows functional and visual acceptance completed by the
user, including the final Script Editor and tooltip discovery/dismissal refinements.
The accepted milestone is authorized for one commit and normal push on main.

## Decision

Keep Core authoritative and dependency-free. Add a UI-free Scripting assembly for
the public facade, safe diagnostics, protocol, and worker client. Only the separate
ScriptHost executable references Roslyn C# scripting 5.9.0. The editor-only compiler
reference described in the refinement amendment below does not execute scripts.
Workspace owns captured
inputs, staging, Script Library records and draft state; Desktop supplies the tool
view and Dock adapters. The worker and Roslyn dependencies deploy in `ScriptHost/`
beside the desktop application, including ordinary framework-dependent publish.

Each job starts a new worker, compiles once, and invokes a fresh globals/submission
state for each targeted message. Message/Batch/Generate remain explicit mode values,
but Batch and Generate are refused before launch. No continuation chain, lifecycle
callbacks, shared-state service, arbitrary message emission, or generation is exposed.

The worker executes trusted local C# with the user's permissions. It is **not a
security sandbox**. Blocking `#r`, `#load`, and source-location directives keeps the
host contract and diagnostics predictable; it does not prevent reflection, filesystem,
network or process access through trusted code. Only application-managed artifacts
participate in rollback. Arbitrary external side effects cannot be rolled back.

## Runtime and privacy

One job at a time; bounded length-prefixed JSON over a current-user named pipe, one
outstanding message request, and discarded fixed-buffer stdout/stderr drains. Scripts
and message values never enter command-line arguments or Activity Log. Diagnostics
carry phases, compiler/error codes, script line/column, source message ordinal and
canonical HL7 paths. Imported exception messages and compiler message text are omitted.
Double-clicking a diagnostic navigates to its script line.

Cancellation reaches worker host calls through a separate asynchronous request reader.
After a 200 ms grace period the parent terminates and reaps noncooperative workers.
Windows Job Objects enforce a 768 MiB process/job committed-memory limit and kill
descendants when the job handle closes. Process-tree termination is also attempted.
Windows is the acceptance platform; other operating systems do not currently have
equivalent OS-enforced memory containment. This is containment, not restricted trust.

Defaults: 30-second startup, 60-second compilation, 10-second message operation,
10-minute total job, 65,536 script UTF-16 units, 100 diagnostics, 12 MiB IPC frames,
8 MiB encoded payloads, 1,048,576 decoded message UTF-16 units and 16,384 units per
segment. Existing Core structure/edit limits still apply. Captured input and staged
output each have a 1 GiB default disk limit; limits may not exceed the 16 GiB snapshot
ceiling. Input/output indexes retain their existing 100,000-message limit.

## Capture, preservation and publication

Capture streams original snapshot bytes plus validated working overlays into a
private owned snapshot. Edits, saves and navigation are briefly frozen during capture
under the existing document gate; the accepted viewer source-session lifecycle is
unchanged. After capture, source editing/navigation may continue independently.
Current-message jobs apply only the targeted working overlay, so an unrelated invalid
draft does not block that scope. Whole-document jobs include all working overlays.
The current implementation captures the entire backing file even for current scope;
the capture quota therefore applies to that file, not just the selected message.
Closing the source cancels the job. Capture/staging ownership drains before disposal.

Never use presentation fragments or bounded previews as execution values. Unsupported
targeted sizes, invalid encodings, ambiguous/incomplete framing and interior BOMs fail
explicitly. No large-message materialization fallback or attachment decoding is added.
Unchanged supported payloads retain their exact bytes. Controlled changes use Core;
MSH delimiters remain read-only. Parent-side payload checks and the authoritative file
indexer validate final payload intervals and framing, including wide-encoding collisions.

Whole-document one-to-one output preserves all surrounding source regions, wrappers,
framing, BOM and terminators. Current-message extraction and filtering are limited to
plain-message partitions or complete unambiguous MLLP envelopes, with only file BOM
metadata outside them. Batch wrappers/unrelated regions cannot silently disappear or
retain stale counts. Filtering such containers is refused. No separator, encoding or
terminator normalization is performed.

Worker processing, output staging and payload validation run off the UI thread.
Progress is throttled to approximately ten updates per second plus phase transitions.
Nothing is published until the complete staged output has been indexed and its expected
message ranges checked. A new document owns the sealed output snapshot and is explicitly
unsaved even when every message was excluded. It has no save destination and uses normal
Save As/close prompts. Script execution never invokes source Save or changes its history.

## Script storage and tool lifetime

Local application data `HL7Workbench/Scripts` holds at most 256 UUID-named, version-1
JSON entries. Each atomic record includes title and source so a rename cannot tear a
save across two files. Names are bounded to 120 characters, source to 65,536 UTF-16
units, and serialized entries to 512 KiB. A library write lease serializes cooperating
instances; complete-record hashes detect stale saves. External noncooperating writers
can still race a filesystem compare/replace; external-edit reconciliation is not a
guaranteed filesystem transaction. Unsupported/corrupt entries are reported without
overwriting them or abandoning the current draft. The format can gain explicit export
or migrate in a later version without changing script IDs.

The bundled synthetic name-replacement example is immutable; Duplicate creates an
editable local copy. Duplicate also saves a current unsaved draft into a new entry
without overwriting its saved original. This example is intentionally not a complete
de-identification policy and provides no HIPAA-compliance guarantee.

Script Editor is an optional dockable/floating tool with an embedded library list,
not a document tab. Its draft session outlives view reconstruction and tool hiding.
New/open/close transitions offer Save/Discard/Cancel; dismissal and failed saves retain
the draft. Shutdown discard consent is consumed only after other close prompts succeed
and revisions still match. Ctrl+S in the tool saves the script, not the HL7 source.
Layouts retain tool placement only. Unsaved drafts are not crash-recovery files.

UX polish/refinement amendment: retain the existing C# lexical engine as immediate
fallback and apply a centralized editor palette without mutating shared definitions.
The user requested contextual colors; a bounded compiler-only Roslyn pass is practical
without Workspaces/LSP, completion or a new process/protocol. Desktop therefore gains
Microsoft.CodeAnalysis.CSharp/Common at the worker's pinned 5.9.0 version, amending
the earlier policy that all Roslyn assemblies load only in the worker. ScriptHost
alone loads the scripting runtime and executes code. Compiler binding against fixed
framework/facade metadata resolves Message, segment-relative/chained/extension calls,
types and method declarations. Delegate variables/properties stay neutral; unresolved
or ambiguous symbols are not guessed. No reference/source resolver, emit, analyzer,
generator, disk source cache, diagnostic forwarding or execution is used.

Analysis is view-owned, debounced 200 ms, serialized across views and performed on a
pool thread with a three-second cooperative cancellation budget. Every edit clears
old ranges immediately; revision and TextDocument identity checks reject stale results.
Detaching cancels and clears the overlay. Only framework/application metadata is
shared; source trees/models are per request and completed request closures release
source references. While pending/canceled/unavailable, readable lexical colors remain.
This is deliberately a small symbol-color pass, not a complete IDE classifier.

Font state uses integer points (9–24, new default/reset 12 pt), converted to Avalonia
DIPs only for rendering. Version-1 DIP preferences migrate in memory to the nearest
whole point; sizes produced by the previous UI retain their chosen whole-point size.
No file is overwritten merely by reading it. Subsequent font changes atomically save
version 2 with FontSizePoints. All controls step one point. Two compact scrollable
toolbar rows and collapsible diagnostics retain user-controlled docking proportions.

## Deferred

Batch callbacks/state, stable synthetic patient identities, CSV reports, generation,
template cloning, structural insertion/removal, cross-document analysis, working-state
application/composite undo, completion/debugging, sandboxing, arbitrary third-party
references, single-file/AOT packaging and disk-backed analysis remain deferred.

See `../stage5a-scripting-acceptance.md` for native Windows steps and checkpoint results.
