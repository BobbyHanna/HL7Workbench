# Stage 3 final polish — Activity Log

The complete Stage 3 milestone, including Activity Log and final workspace polish,
received explicit native Windows acceptance on October 14, 2026. The user authorized
verification and one local milestone commit; push, publication, tagging and release
remain unauthorized. Subsequent workspace polish changes its initial visibility and
saved-visibility policy; see `stage3-workspace-layout-acceptance.md` for the accepted
layout and verification record.

## Notification root cause

`MainWindow.OpenFile` appended “See Diagnostics for retained input warnings” to every
completed picker batch, without checking diagnostic counts or severity. The old pane
projected only the active document's diagnostics and title; its title alone did not
establish that any warning existed. The suffix was therefore misleading for clean
input. Actual parser, validator, index and decoding diagnostics remain intact.

Successful clean opens now say `Opened 1 file(s) read-only.` Actual retained warning/
error counts and failed-file counts extend that short summary when applicable.
Informational diagnostics do not increase warning counts. Open progress remains
transient; queued progress from completed files cannot replace the final summary.
A status-bar button shows warning/error counts in retained session history and
opens Activity Log. Information does not show that indicator or open the tool.

## Implementation and event boundaries

- Workspace `ActivityLog` owns the in-memory chronological history. `ActivityEntry`
  contains immutable timestamp, sequence, severity, enum codes/categories and numeric
  operation/document/message/location metadata. The session service belongs to
  `DocumentManager`; the view is independent of collection lifetime.
- `MessageFileReader` records attempts, indexing completion, successful publication,
  cancellation and failures. The shell begins an attempt before opening the storage
  stream so provider failures also receive a safe operation identity.
- Document-owned `DocumentActivity` projects file and indexed boundary diagnostics
  once, then newly encountered active-message structural diagnostics. It keeps
  message-local UTF-16 offsets as `long`, snapshot byte offsets separately, and the
  three known header-field coordinates where available. Original structured
  diagnostics remain on Core models, file index entries and `ActiveDiagnostics`.
- `RawInspectorSession` records export start, completion with actual byte count,
  failure and cancellation, preserving the initiating message number even when
  document navigation cancels the session. The export implementation and original-
  byte publication guarantees remain unchanged.
- The shell records initialization, Full Source Viewer opening, picker/platform
  errors and layout failures. Source-backed views report significant I/O failures
  once per document. Opening a save picker and dismissing it without starting an
  export does not add export activity.

There are no routine events for message changes, field selection, tabs, docking,
scrolling, hover, chunk navigation, progress callbacks or background refreshes.
New actual warnings discovered during lazy message inspection are still recorded.

## Privacy, retention and lifecycle

The logging API accepts typed operations and numeric metadata, with no free-text
message parameter. Diagnostic conversion maps known codes to an enum and generates
its own explanations. Imported diagnostic descriptions, arbitrary paths/codes,
source snippets and exception messages/stack traces are never retained. Unknown
diagnostic codes become `Unknown`; the full original diagnostic stays with its
owning document. Only the fixed `MSH-9`, `MSH-10`, `MSH-12` diagnostic paths are
projected. Exceptions are classified by type only.

Entries use application-generated `Document N` identities, never filenames or full
paths. They hold no document, model, snapshot, source buffer or exception reference.
There is no disk sink, telemetry or external transmission. Copy Selected Entries is
an explicit clipboard action containing the same safe displayed metadata.

The queue retains the latest **2,000 entries**, evicting oldest entries. Document
diagnostic identities are deduplicated independently of eviction/Clear Log, so a
revisit does not refill the log with the same warnings. That identity set is capped
at **8,192 per open document**. On exhaustion one warning explains that additional
diagnostic activity is omitted; underlying document diagnostics are unchanged.
Entries and UI collections remain bounded even for pathological input.

`ActivityLogViewModel` projects at most 2,000 entries and coalesces notification
bursts on the UI thread. Closing the tool disposes/unsubscribes the projection;
reopening constructs a new projection over retained history. The virtualized
`ActivityLogView` detaches subscriptions when reparented. A new application session
starts a new history. Clear Log does not clear parser diagnostics or reset safe
document identities.

## UI and layout compatibility

View → Activity Log opens a chronological list with local timestamp, severity,
description and safe context. All/Information/Warning/Error filtering, Clear Log,
Copy Selected Entries and Ctrl+C (Cmd+C on macOS) are available. Selection supports
normal multiple selection. Rows do not wrap; horizontal scrolling exposes long
metadata. The list follows new entries only when already at its bottom. Reviewing
older entries preserves the visible row/scroll position, including oldest-entry
eviction when the anchor entry remains retained. If that entry itself is evicted,
the surviving list necessarily becomes the available review range.

The user-facing Diagnostics tool is replaced in production. Its shipped identity
`tool.diagnostics` is retained as `ActivityLogId`, avoiding an identity migration or
duplicate tool. Layout format stays version 2 with version 1 support. Existing
placement records are used; older visible Diagnostics slots without such records
are captured before reconstructing the graph, including floating geometry. Activity
Log is now visible in fresh/reset workspaces. Restore respects its saved visible/hidden
state, including prior Diagnostics placement; a saved hidden log stays uninitialized.
Opening it restores remembered placement. Navigator, Field Inspector, Full Source Viewer and documents retain their
existing lifecycles. The old diagnostic projection remains available for API and
regression compatibility; it is not registered in the production shell.

## Verification

- Locked dependency restore passed.
- Release build passed with **zero warnings and zero errors**.
- Complete suite: **470 passed**, zero failed/skipped (Core 216, Workspace 189,
  Desktop 65), preserving the preceding 451 cases and original 356-case baseline.
- Workspace stress suite: **191 passed**, zero failed/skipped, including generated
  256/512 MiB attachments and existing allocation/retained-memory assertions.
- `git diff --check` passed. HEAD remains
  `3e0f9890a9eeadf2e5d733165309f0554ccc232c` on `main`, with no staged changes.
- Retention tests exercise 10,000 operations, including concurrent producers, and
  verify exactly the newest 2,000 remain. Privacy tests verify retained entry fields
  are scalar values and reject synthetic patient-like names, paths, field text,
  imported diagnostic text and arbitrary exception text from displayed/copied history.

Nineteen new test cases cover the logging service, operations and UI.
Focused tests cover chronological/concurrent ordering, 2,000-entry eviction,
session identity, typed privacy boundaries, synthetic patient-like filenames and
exception text, source/export privacy, deduplication and its cap, document isolation,
real index-budget rejection, encoding/structural limits, informational diagnostics,
status text, filtering, clearing, clipboard contents, virtualized tail following,
scroll anchoring, lazy close/reopen and version-1/2 docked/floating legacy placements.
Original tests are preserved without removing or weakening cases.

## Native Windows acceptance checklist

Use the opt-out variables from `PROJECT_CONTEXT.md`, then launch the verified output:

```powershell
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

1. Start with the existing saved layout and with Reset Layout. Existing saved visibility
   must be respected. Reset shows Activity Log below documents and the two independent
   right-side inspection tools. Confirm the renamed tab, initialization entry and readable
   timestamps/severities. Check old docked and floating Diagnostics layouts too.
2. Open clean synthetic HL7 containing MSH-9, MSH-10 and MSH-12. Confirm concise success
   status with no warning instruction. Verify the attempt/index/success history has
   generated document numbers and no filenames, full paths or message previews.
3. Open synthetic malformed/unclassified input, invalid encoding and a file that
   cannot be read. Confirm actual warning/error history and counts. Click the
   status-bar indicator to activate Activity Log. Information alone must not show
   an urgent indicator or automatically open the tool.
4. Navigate multiple messages/documents and inspect fields/chunks repeatedly. Newly
   discovered structural warnings may appear once; repeated visits, tab switches,
   hover and scrolling must not produce activity spam. Underlying raw/structural
   inspection must remain functional.
5. Exercise each severity filter, Ctrl/Shift selection, Ctrl+C and Copy Selected
   Entries. Verify chronological clipboard output contains only safe metadata.
   Clear Log; confirm history/indicator clear but document inspection still works.
6. At the list bottom, generate more events: it should follow the newest entry.
   Scroll up, generate more events: the reviewed row/position should remain stable.
   Check horizontal scrolling, keyboard navigation and readable appearance at DPI
   settings used for normal acceptance.
7. Open Full Source Viewer; export whole-message and value scopes, cancel an export
   in progress and try a protected original/snapshot destination. Check safe states,
   byte counts and error classification, with no output paths or payload data.
   Preserve existing exact-byte export and large-value behavior.
8. Close Activity Log, perform operations, reopen: retained history must return in
   its remembered location. Float/redock and repeat. Save/restart with it visible:
   it must honor saved visible/hidden state with fresh session history. Opening a
   previously closed log should use remembered placement. Check shutdown with other floating tools.

Native OS rendering, clipboard integration with other applications, drag gestures,
mixed DPI and monitor recovery remain manual checks. The headless tests establish
model and visual-tree behavior, not native-window acceptance. No new HL7 standard/
profile validation or diagnostic-to-source navigation is implemented. Inactive
messages are not parsed eagerly; structural warnings are discovered when inspected.
