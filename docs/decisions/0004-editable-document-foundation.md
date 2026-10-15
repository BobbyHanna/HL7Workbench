# ADR 0004: Bounded editable document working state

Status: Accepted. The user explicitly granted native Windows Stage 4 acceptance on
2026-10-15, including the raw-editing crash and inspection-flicker fixes, and
authorized final verification and one local milestone commit. No push, tag,
publication or release is authorized.

## Assessment

The existing raw control is AvaloniaEdit. Complete ordinary messages use a
`TextDocument`, Core-derived highlight ranges and Workspace inspection selection.
Stage 3 large messages instead use `MessagePresentation`, explicitly mapped
abbreviations and virtualized blocks. Those display strings cannot be save sources.
The existing control can enable writes for complete messages without replacement.

`MessageDocument` owns an immutable `SourceSnapshot`, an immutable complete byte
index, active parsed/structural state, and at most one decoded source spool.
Navigation previously discarded the materialized model. Dock reconstructs views,
so working text/history must belong to the document rather than the view or editor.
Optional Full Source Viewer sessions borrow document access and end on content
changes or closure. Their ownership contract remains in place.

Options considered:

| Approach | Benefit | Cost / decision |
| --- | --- | --- |
| Bounded message buffers | Reuses the existing parser/control and byte ranges; straightforward undo and transactional save | Eager bounded parse and bounded retained working text; selected for this slice |
| Disk copy-on-write overlays | Scales total edited data independently of RAM | Needs overlay/history storage, cancellation, cleanup and crash-recovery ownership; deferred |
| Piece table / rope over source | Efficient large edits and history | Needs decoded/encoded piece boundaries, incremental inspection and editor integration; disproportionate to the first slice |
| New editor library | Could supply alternative storage | Unnecessary replacement/dependency; rejected for this slice |

## Decision

Keep Core APIs/limits and Stage 3 snapshots, indexing, abbreviation and source-spool
ownership intact. Add Workspace-owned bounded working buffers, delta history,
save points, Edit Mode and saving. No UI/Dock types enter Workspace or Core.

Manual raw editing is distinct from Core's controlled field writes: it can retain
temporarily malformed text and explicit delimiter/segment edits. It uses Core
parsing for every structural projection; it does not relax `SetRaw`/`SetText`
contracts or interpret omission markers as source. Limits and strict encoding
failures retain the draft and remove current inspection coordinates, rather than
publish stale offsets. Explicitly labeled historical presentation is retained as
described in the continuity amendment below.

Every document starts read-only unless application preferences request Edit Mode.
The preference is applied once at open and persisted separately from layout metadata.
Edit Mode is session state; turning it off keeps working text/history/dirty state.

The complete editor retains Stage 3's 1,048,576 UTF-16 character message limit
and 16,384-character line limit. No source-backed message is materialized to edit.
Drafts may temporarily exceed the line/structural budget but remain bounded by
the complete-text budget; inspection and saving then fail safely until repaired.
UTF-8, UTF-16 LE/BE and available single-byte encodings support edits. UTF-32 and
undecodable files support unchanged copying and existing inspection only.

Retained edit text is capped at 16,777,216 UTF-16 units (32 MiB of character data)
and 4,096 delta-history entries **per document**. Accounting reserves original
text, working/save-point strings at the largest retained revision size, and both
sides of undo/redo deltas. This bounds inactive-message edit retention separately
from file size and avoids silently evicting undo history or unsaved text. Original
encoded buffers, models, editor storage and object overhead are additional. The
limits are engineering budgets, not HL7 validity limits. Crossing a budget refuses
the new edit atomically; Undo remains available. Saving and reopening the saved
document starts a fresh history. Limits and rejection/undo behavior are tested.

Text changes immediately invalidate use of old structural coordinates for selection/
highlighting and close borrowed source sessions. Parsing runs after a 180 ms debounce, off the UI thread, under
the existing per-document work gate and two-scan global concurrency limit. Revision
checks suppress obsolete results. The editor remains mounted while typing; active
inspectors reacquire the new Core projection. Navigation reloads retained drafts
without keeping inactive parsed models. Undo/redo remains available across tab/view
reconstruction, message navigation and save points. Dirty means exact working text
differs from the last successful save, rather than Core's historical `IsModified`.

### Inspection continuity amendment (accepted 2026-10-15)

Editing now separates Current, Pending, Stale, Empty and Unavailable inspection
states. `ActiveInspection` stays current-only; `InspectionSnapshot` may retain one
bounded previous materialized projection for visual continuity. That projection is
marked non-current, labeled in the tools, and refuses offset/selection operations.
Old coordinates never drive editor highlighting, hover or navigation. Stable Dock
and embedded panes rebind current data without replacing their controls; canonical
selection, realized expansion and viewport are restored where possible. Empty text
has its own state; a tolerant parse is current even when it reports malformed HL7.
Navigation/disposal release the historical projection; edit history and existing
debounce/scan gates retain their established ownership.
See [continuity investigation and native retest](../stage4-inspection-continuity.md).

## Original, working and saved coordinates

The original snapshot and its index never change after Save or Save As. Original
snapshot-global bytes, original message-local bytes, current working UTF-16 offsets,
and editor display coordinates are separate. An edited message's working text has
no original-range mapping. Original-source value exports/Full Source Viewer requests
are disabled for that message; reopening the saved file establishes a new snapshot.
Unchanged messages keep their original-source behavior. Existing sessions close
when the owning content changes, including an edit.

Save freezes manual mutations/navigation, validates bounded drafts, streams the
snapshot in 64 KiB chunks and substitutes only edited payload ranges. Each payload
is serialized by a surrogate-safe common-prefix/suffix splice against its original
encoded bytes. Unchanged original spans, BOM, framing, batch wrappers, unrelated
messages and terminators remain exact. No complete output file string/array exists.
Save As changes the session destination/title; later Save uses that destination.
Undo across saving uses the original overlay plus the retained save point.

Saving refuses new framing controls, interior BOMs, batch wrapper lines, multiple
MSH starts, removal of an existing leading MSH, and removal of the original trailing
CR/LF suffix before a following unframed source region. These edits require future
explicit boundary operations. Drafts are retained for correction/undo. Malformed
HL7 that Core can retain is otherwise permitted; saving is not profile certification.
Edited UTF-16 payloads also pass through the existing byte indexer in a bounded
temporary validation snapshot with inherited encoding/framing context. This catches
code units whose encoded bytes resemble raw MLLP controls; no second boundary grammar
is implemented. Validation snapshots are released before publication.

Publication creates an exclusive random temporary file beside the destination,
flushes data to disk, and then uses `File.Replace` for an existing destination or
`File.Move` for a new one. There is no delete/copy fallback. Snapshot-storage paths
(including resolved symbolic-directory aliases) are rejected. Failures/cancellation
leave save points/history/destination identity unchanged and clean partial output
when possible. A successful publication alone advances save points. Safe typed
Activity Log records contain no imported text, filenames, paths or exceptions.

Tab closure and main-window closure use Save/Discard/Cancel. Cancel, dialog dismissal,
Save As dismissal and failed saves leave documents open. Discard consent is scoped
to the reviewed revision. Concurrent saves block closure. Document disposal still
cancels pending work and releases snapshots after the work gate drains.

## Consequences and deferred work

No third-party dependency, editor replacement, SQL, scripting, attachment decoding,
autosave or crash recovery. Editing exceptionally large/source-backed messages,
disk-backed histories, incremental parsing, explicit boundary editing and working-
to-original source mappings are deferred. Histories are bounded and ungrouped;
typing operations currently undo individually. Save blocks this document's mutations
while streaming; other documents remain usable. External-change reconciliation,
file-identity/metadata preservation guarantees and power-loss durability across
all filesystem types are not implemented. The user explicitly granted native Windows
acceptance, including editing, inspection stability and the crash/flicker fixes.

See [Stage 4 acceptance](../stage4-editable-document-acceptance.md).
