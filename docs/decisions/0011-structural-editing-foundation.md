# ADR 0011: Stable logical messages and validated streaming publication

Current checkpoint status, 2026-10-17: the user confirmed native acceptance of all
Stage 8B work and Script Output Naming and authorized one local commit only.
Earlier pending/no-commit status below is historical. No push, tag, publish, release,
Stage 8C or Stage 8D is authorized.

Status: Stage 8B.1–8B.3 and the window-root keyboard correction are native accepted
and intentionally uncommitted. Stage 8B.4 implementation was subsequently authorized
on 2026-10-17; its native acceptance is pending. No staging/commit/push/publication.
Stage 8C and 8D are not authorized.

## Identity, provenance and order

Workspace keeps the immutable SourceSnapshot and MessageFileIndex. A
LogicalMessageSequence contains only LogicalMessage metadata and an ID-to-position
dictionary. MessageId is a strongly typed opaque session token. Allocation uses a
process-wide monotonic counter to prevent accidental cross-document aliases;
resolution and ownership remain document-scoped. IDs are never HL7 identifiers,
offsets or positions and never enter HL7, layout, settings or sidecars.

LogicalMessage distinguishes File, Memory and Inserted origins. Only File has an
original SourceOrdinal. Original file ranges still come from IndexedMessage;
insertions have no fabricated original ordinal/range. Bounded in-memory originals
retain their initial bytes/text independently of the current active parse.

MessageNumber is explicitly the current one-based logical position (zero when
empty). GetMessageId, GetLogicalPosition and NavigateAsync(MessageId) make the
distinction explicit. Cached decoded sources remain keyed by original source
ordinal. Original relative order cannot change in this checkpoint.

The 100,000-message ceiling includes insertions. Flat sequence lookup is O(1),
ID lookup expected O(1), and insertion/removal O(N + K). A batch shifts the range
once. Keystrokes and ordinary target resolution do not traverse the sequence.
Source text is never decoded to construct or insert sequence metadata.

## Working state and history

Buffers are keyed by MessageId. One document history orders text deltas and batch
insertions chronologically; EditableMessageBuffer retains bounded text, strict
encoding and surrogate-safe splicing without a second local history.

Undo/Redo works with Edit Mode off or a source-backed active message and always
selects the affected message. Undo insertion selects the previous surviving entry,
or the first survivor when there is no previous entry, or a valid empty state.
Redo selects the first restored entry and restores the same IDs/payloads/position.
Navigation adds no history. New mutations drop the entire redo branch and release
its detached inserted buffers. Save is not a history barrier.

The existing 1 Mi UTF-16 complete-editor limit, 16 Ki line limit, 16,777,216 retained
character budget (32 MiB of character storage), and 4,096 history-entry budget
remain. A buffer reserves original text plus two largest-revision strings;
document history accounts both removed and inserted delta strings. Live and
detached redo-owned buffers and prepared batch costs are checked before publication.
One insertion batch is one history entry. No eviction of required history/drafts.
Encoded original buffers, models, reader captures and metadata remain additional
bounded overhead, as in ADR 0004.

Dirty state is exact text comparison against successful save points plus cached
logical-order comparison against a saved ID array. History position is not a save
point. Text dirty-count changes are incremental; O(N) saved-order comparison occurs
only on structural changes and save, not each keystroke. Save points include live
membership, so undoing a saved insertion becomes dirty and redo can return to clean.

## Streaming composition and validation

MessageSavePlan freezes metadata and bounded buffer references while IsSaving
blocks document mutations/navigation. Concurrent editing during publication is
deferred: it would require additional versioned history/save-point reservations.
All long I/O/indexing/verification runs off the UI thread, supports cancellation
before publication, and exposes generation-guarded progress and measured phases.

The plan copies the complete original source partition, replacing only changed
original payloads and inserting new payloads/envelopes. Adjacent original copies
coalesce and share one 64 KiB buffer. Encoded replacement arrays are bounded to a
single message at a time. No full document string/array or second disk snapshot
is produced.

In each gap before an original, retained non-message regions are copied first,
then pending insertions, then the original message. Final insertions precede
retained trailing regions. A file BOM stays first, including BOM-only documents.
New envelopes inherit the next original's framing, or the last original's framing
for terminal insertion; in-memory/empty documents use unframed payloads. Existing
terminators/delimiters/envelopes never change to accommodate an insertion.

FileSaveDestination has a narrow internal validated-write path. After staged
output is flushed, MessageFileIndex.BuildStaged borrows its read handle and shares
the original byte-indexing implementation. No alternative boundary grammar is added.
Validation compares every planned message's projected SourceRange, PayloadRange
and framing in logical order, plus every retained non-message region and reason.
Equal message counts or MSH-10 values cannot establish membership.

A bounded second validation pass compares each staged piece against its actual
original bytes or encoded bounded payload. This verifies the ID-to-payload mapping
and retained opaque bytes and computes SHA-256 for the associated save baseline.
It does not decode any attachment. The cost is additional sequential I/O, explicitly
measured rather than claimed free.

Only then does the existing same-directory File.Replace/File.Move publication
commit. There is no delete/copy fallback. Saved order, text save points, destination
and successful metrics advance only after publication. Original snapshots/indexes
never rebase; saved offsets are not original offsets. Reopen creates fresh IDs.
The legacy single bounded in-memory-original save retains its pre-existing ability
to write malformed/non-indexable content; strict logical membership validation is
required whenever that document contains insertions.

## Incomplete drafts and unsupported boundaries

Blank or malformed inserted drafts can be retained, navigated and undone.
Multiple-MSH input is rejected through Core's shared scanner instead of splitting
one draft silently. Save requires a nonempty recoverable MSH start for an insertion,
strict encoding, existing parse budgets and exact staged boundary recovery. Missing
clinical/profile fields are diagnostics, not mandatory generation/conformance rules.

Known unsupported original containers reject insertion before mutation: provisional
headerless messages, batch wrappers, incomplete/ambiguous MLLP, interior BOM boundary
diagnostics, and unexpected MLLP endings. Arbitrary unclassified-only input is not
treated as an empty file. Existing unchanged saves retain such inputs byte-exactly.
Ordinary concatenation and complete MLLP are supported where validation proves them.
Unsafe unterminated joins are rejected during save; neither original nor inserted
text receives an implicit terminator or repair. Drafts remain available for explicit
correction/undo. A second staged guard also rejects provisional reclassification.
Unsupported edit encodings remain eligible for unchanged copying, not insertion.

## External destination changes

SourceSnapshot hashes bytes during capture and retains the opened file handle's
identity before/after capture. It does not recapture a newer pathname as the open
baseline. Explicit local-path adapters must match captured content/location.
Partial FileStream captures do not silently acquire a whole-file save destination.

Associated Save checks the expected identity/hash from open or the last publication,
then retains the existing publication-time location/identity/hash checks. Unexpected
changes fail safely; Save As to another destination remains available. Publication
retains the validated output hash, never an external replacement's contents.

Windows ReplaceFile requires closing replacement-file readers. The validated staging
lease is write-denying until the final identity-checked handoff. The publisher then
observes the published identity without adopting another Windows file ID. Failure
to observe a just-published name does not pretend publication failed: the validated
hash and conservative prior identity cause later associated saves to fail safely.

No portable atomic compare-and-replace exists against noncooperating external
name swaps. Final name-check/replacement and post-publication observation windows
remain. Windows provides file-ID checks; other platforms retain weaker metadata/hash
identity. No automatic merge, source reload, universal metadata retention or
power-loss durability guarantee is introduced.

## Stage 8B.1–8B.3 temporary compatibility boundary (historical)

The following checkpoint boundary is superseded by the Stage 8B.4 integration below.

Full Search/Comparison/scripting migration is Stage 8B.4. These original-ordinal
operations are guarded while live membership differs from the initial document,
including after Save. Search/filter results cancel/clear on structural mutations.
Comparison results invalidate; new document selection/capture refuses structural
documents. Script capture fails before worker launch. Undoing all insertions
restores original ordinal compatibility; reopening saved output establishes a new
compatible original index. Saved query schemas and worker protocol are unchanged.

Narrow safety adapters are included now: original-buffer ordinal lookup, correct
original range exports/active boundary diagnostics, ID-based view reuse/context-menu
checks, existing Undo controls/shortcuts, and publication progress. Inserted entries
have no Original Message/raw-byte-export source. Unchanged originals can still export
their real original ranges after position shifts. Source read leases/draining and
the native editor undo-group safety fix remain intact.

No Add Message UI, Next append, generator, general deletion/reordering,
large-message editing, comparison-limit change, new dependency, Dock schema,
packaging, publication or Git action belongs to this checkpoint.

## Stage 8B.4: shared identity and logical captures

Document-backed feature selections carry MessageId. Current logical positions are
display coordinates; original ordinals and original byte ranges remain provenance.
Capture occurs synchronously on the document's owning context before background work.
Captured metadata arrays and immutable bounded draft strings never enumerate live
mutable sequence/buffer collections on reader threads. Untouched originals retain
references to the immutable snapshot/index and decode only when scanned/read.

SearchCapture owns the existing snapshot-read lease and captures every live logical
entry, ID, position, optional original ordinal and optional bounded text. Search results
retain one bounded row per matching ID. Navigation resolves IDs, never captured ranks.
NavigationMatchIds plus a membership set are authoritative; NavigationMatches remains
a compatibility display projection of current logical positions. Membership changes
invalidate both, so a valid filter cannot retain obsolete positions. SearchRevision
changes cancel in-flight work, clear results/filters and reject queued completions.
Navigation, inspection selection and successful Save alone leave that revision alone.
Authored query and saved definitions remain independent; no persisted schema changes.

ComparisonSource/ComparisonInput retain IDs, including an undone insertion. Their
legacy Ordinal accessor reports current logical position (zero when unavailable),
while OriginalOrdinal is separate. A position control is an explicit new selection;
refresh/coercion never replaces a retained ID. Source capture resolves the ID first,
then uses its bounded current text or original source ordinal. Inserted Original
requests fail explicitly, including after Save. Redo restores availability of the
same ID, but never makes an old result current. Pasted-only sources remain independent.
Existing scan/presentation/copy limits and lease/read draining remain unchanged.

Script capture is mutation-locked under the existing work gate. It reuses BuildSavePlan
and MessageSavePlan's projected byte partition and re-indexed membership validation;
there is no second original-ordinal composition implementation. Whole scope captures
all live messages and current drafts in logical order. Current scope captures all
originals (inactive originals use their immutable original fallback) and only the
selected insertion, if any. Other insertions are explicitly absent from this job's
input map: they have no original fallback and must not make unrelated current scope
depend on invalid drafts. The selected target is never omitted. The existing whole
backing-file capture quota still applies. Neither scope materializes large untouched
originals just to establish identity; targeted worker size limits remain unchanged.

ScriptInputMapping records document ID/revision and job ordinal -> MessageId, captured
logical position and optional original ordinal. Worker ordinals are job-local only.
Output attribution records each new output ID's originating input ID/job ordinal,
including exclusion/deferred order. Parent-only exception context carries the same
bounded mapping for diagnostics, avoiding Workspace identities in the worker protocol.
Desktop labels captured logical position and job/original ordinal separately; script
line navigation remains script-line navigation. No source text is retained by the map.

The user explicitly selected independent captured output with stale-source attribution,
preserving ADR 0005's snapshot behavior. After capture, source edits, insertion or
structural Undo/Redo do not cancel a job or change its captured order/identities.
ScriptExecutionResult.SourceChangedDuringRun records a revision difference immediately
before publication; Input retains the captured document ID/revision and ordinal map.
The Script Editor retains a visible label with source document number, captured
revision and stale-source status for the last successful output, including across
later failure or view reconstruction. Reports share
that producing-run attribution. This flag describes the capture-to-publication interval,
not a live comparison after publication. No source text is retained by the metadata.
Navigation or switching active documents never retargets the source or worker ordinals;
a tab switch alone does not mark unchanged input stale. Output opens as a separate
unsaved document; no source-apply path exists. Source closure, explicit cancellation,
timeouts and actual worker/validation failures still prevent publication and roll back
private artifacts. Later edits do not mask actual worker errors as revision conflicts.
Before a queued job has captured input, a changed revision is refused with InputChanged;
there is no older snapshot to run. No DTO/protocol schema, worker lifecycle, time/memory
limit or Windows Job Object protection changes.

Activity diagnostics deduplicate by stable ID and source coordinates, retaining their
recorded logical position and optional original ordinal separately. Raw inspector
sessions retain that provenance too; existing edit/navigation/close invalidation and
exact original-byte export rules remain. Inserted sources never acquire original bytes.

The accepted document-wide root Undo/Redo authority is unchanged. No Stage 8C/8D UI,
large-message editor/comparison expansion, dependency or persistence change is included.
