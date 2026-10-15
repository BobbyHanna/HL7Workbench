# Viewer Stage 3: file navigation and large-message inspection

The complete Stage 3 milestone, including Activity Log and final layout polish,
received explicit native Windows acceptance on October 14, 2026; see
`stage3-workspace-layout-acceptance.md`. One local milestone commit is authorized.
Push, publication, tagging, release, message editing/saving, scripting, search and
attachment rendering are outside this authorization.
Original-source export is now implemented by the consolidated UX refinement; see
`viewer-stage3-ux-acceptance.md` for current interaction and tool-lifecycle acceptance.
The baseline was clean `main` at `3e0f9890a9eeadf2e5d733165309f0554ccc232c`.
All 356 original tests remain present. See `stage3-performance.md` for measured results.

## Project responsibilities

Core `Hl7StructureScanner` owns the shared grammar. `Hl7Parser` materializes its
ranges into the existing `Hl7Message` hierarchy; public Raw/Get/edit/serializer
contracts retain complete values and their original limits. `Hl7SourceEncoding`
centralizes existing strict BOM precedence. Source-backed `Hl7Structure` contains
immutable ranges, structure, bounded previews, and long-coordinate diagnostics.
The scanner consumes `IHl7TextSource`; it does not open files or know Avalonia.

Workspace owns `SourceSnapshot`, `MessageFileIndex`, `DecodedMessageSource`, and
the stable `MessageDocument` file session. Only the active message owns a structural
model. One decoded spool may be cached per document to avoid rewriting an enormous
attachment when returning from an ordinary message; cached spools retain no old
structural hierarchy or UI projections. Work is serialized by the document gate.
Generation checks and cancellation prevent stale publication. Closing asynchronously
drains work before releasing sources; `Disposal` makes completion observable.
At most two file-session message scans run concurrently across documents.

Desktop retains one document tab per file. `FileDocumentView` owns the fixed message
toolbar; `MessageInspectionView` renders ordinary text or mapped abbreviated
presentations. Segment Navigator/Field Inspector are now tabbed tools over shared
inspection projections. The lazy Full Source Viewer provides contiguous windows.
Structured diagnostics remain on documents; Activity Log records newly discovered
warnings/errors without routine navigation events. Existing Dock framework types remain
isolated in Desktop; layout metadata now supports version-2 remembered placement.

## Capture, encoding, and byte accounting

- Capture streams into owned temporary storage using 64 KiB buffers. Never reopen
  the original source pathname for navigation. Publish only after complete capture,
  complete indexing, and initial-message preparation.
- Seekable streams are checked for length changes. File streams additionally check
  last-write time before/after capture. Same-size changes with preserved timestamps,
  or changes invisible to a stream provider, cannot be detected reliably. This is
  an immutable capture of the bytes read, not an atomic filesystem snapshot.
- Temporary GUID directories hold exclusive lease files. Closing releases them.
  Startup capture removes only old (>1 day), GUID-named, non-symlink directories
  whose existing lease can be opened exclusively. Active and unrelated directories
  are retained. Unix child directories are created with owner-only permissions;
  Windows storage inherits the current user's temporary-directory permissions.
- Each original byte belongs to one ordered source region. File-level BOM bytes
  have their own metadata region. A message's complete source extent is distinct
  from its payload: MLLP envelopes are retained outside the parser payload.
- Inherit one encoding per file: supported longest BOM detection, otherwise strict
  UTF-8. No MSH-18 inference, speculative detection, or replacement characters.
  Interior BOMs are retained and diagnosed; they never select another file encoding.
- Snapshot ranges are global bytes. `StructureNode.ByteRange` and decoded-source
  mapping are message-local bytes. `CharacterRange` is message-local UTF-16,
  excluding the file BOM/framing. Editor offsets are bounded local integers.
  UTF-8/32 use sparse checkpoints; UTF-16 code units and surrogate pairs remain exact.
- Active strict decoding writes an owned UTF-16 spool. This adds approximately two
  bytes per decoded UTF-16 unit in temporary disk storage. It avoids payload-sized
  managed strings and permits bounded random access. File cache/RSS is not controlled
  by managed-cache limits; disk-full/read errors become explicit failure states.
  Preparing a different large message can temporarily retain the previous cached
  spool alongside the new spool until replacement succeeds.

## Deterministic boundary/recovery policy

Unframed messages begin at the literal MSH prefix only at an actual segment boundary
(start, CR, LF, CRLF), even when their delimiter/header definition is malformed.
MSH substrings inside values do not split messages. Adjacent messages without a
segment boundary remain one ambiguous payload. Interior malformed lines belong to
that payload; concatenated ER7 cannot reliably distinguish trailing garbage from a
malformed final segment.

Raw MLLP `0B ... 1C 0D` envelopes take precedence inside a frame. A nested start
ends the previous incomplete envelope and starts another. EOF ends an incomplete
frame with a warning. Unexpected endings and batch wrapper lines are source regions.
Multiple MSH lines within one frame remain one framed payload and receive Core's
MultipleMessages diagnostic. Empty envelopes remain indexed empty messages.

For UTF-16/32, starts require an encoded MSH/PID prefix or an empty envelope, avoiding
blind interpretation of arbitrary low bytes as framing. Wide-encoding ending
collisions inside a frame use raw framing precedence and report AmbiguousFraming.
Unframed wide-encoding values do not split on an in-value marker-shaped code unit.
Other ambiguous wide-encoding/malformed envelope content stays preserved; no claim
of universally unambiguous framing is made.

FHS/BHS/BTS/FTS lines remain source regions; no semantic batch support is implemented.
With no recognized messages, each non-wrapper source region containing a plausible
uppercase/digit three-character segment with a standard field separator (or bare
identifier) becomes provisional message content. Leading malformed lines remain
opaque and inspectable. Arbitrary content without a plausible segment remains a
zero-message file with byte inspection. Invalid unclassified encoding is explicit.

## Bounds and interaction

- Existing materialized limits remain 16 MiB bytes, 8 Mi UTF-16 units, 1 Mi segment
  units, 100,000 segments, 250,000 elements, 10,000 siblings, and 1,024 diagnostics.
- Source inspection removes message/value size as a materialization requirement;
  structural count limits remain. Escape-token inspection is capped at 1 Mi units
  per token and 100,000 paired tokens per message.
  Exhaustion publishes no partial model as complete; raw text/bytes stay accessible.
- File capture is capped at 16 GiB; index caps are 100,000 messages and 200,000 source
  regions. Coordinate metadata is capped at 1,000,000 checkpoints. These are explicit
  resource failures, separate from original parser limits.
- The active spool has a 32 Ki-unit character cache and sparse checkpoints, not a
  per-character map. Tree previews are at most 100 units; selected previews 4 Ki.
- Ordinary file messages retain complete AvaloniaEdit text below 1 Mi UTF-16 units
  and a 16 Ki-unit longest-line gate. Larger messages use structural abbreviation,
  with a bounded virtualized-block fallback. Display-only markers have explicit
  mappings and never alter source. The optional Full Source Viewer uses contiguous
  16 Ki-unit windows; its line numbers are window-local. See the UX acceptance document.
- First/Previous/Next/Last and direct message numbers are fixed below raw content.
  Counts are final before navigation is enabled. Single-message directions are
  disabled; zero-message controls show 0 / 0. The bar contains no MSH metadata.
- Main text-window paging controls are removed. Full Source Viewer chunk controls
  and bounded Field Inspector previews provide complete positional access. Mapped
  main hover/double-click use Core global paths, including following omitted values.
  Missing values have no invented source range.
- Message changes clear selection and both scroll axes. Text-window changes preserve
  logical selection. Tabs retain their current message and inspection state.
- Copy is limited to 1 Mi UTF-16 units. A rejected copy explains the limit and does
  not silently truncate. Selections across markers require explicit Copy Visible
  Preview. Exact original-value export streams snapshot bytes. Attachments are never
  decoded/rendered.
- Source byte inspection uses 512-byte pages and strict decoded text only when a
  page is decodable. An arbitrary byte page may begin inside a multibyte sequence;
  hexadecimal access remains available when that page has no safe text rendering.

## Automated commands

```powershell
Set-Location C:\Dev\HL7Workbench
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:AVALONIA_TELEMETRY_OPTOUT='1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT='1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
git diff --check
```

The normal suite generates 20/50/100 MiB synthetic encoded attachments incrementally
under ignored `artifacts/stage3/`. The files remain available for native acceptance;
they are not committed fixtures. Optional stress runs add 256/512 MiB cases:

```powershell
$env:HL7WORKBENCH_STRESS='1'
dotnet test --project tests/HL7Workbench.Workspace.Tests -c Release --no-build --no-restore
Remove-Item Env:HL7WORKBENCH_STRESS
```

## Original native Windows acceptance checklist — passed before Activity Log polish

The current polish checklist is in `stage3-workspace-layout-acceptance.md`.

Close older app instances and run:

```powershell
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

1. Open `samples/synthetic-only/adt-a01.hl7`. Confirm raw text, existing highlighting,
   Navigator/Inspector, empty/null/missing values, read-only protections and copying.
2. Confirm 1 / 1 is visible below raw content with disabled directional buttons.
   Resize panes/window; navigation must remain visible and wrap at narrow widths.
3. Open `artifacts/stage3/synthetic-10000-messages.hl7`. Verify exactly one tab and an accurate count;
   use First, Previous, Next, Last, and direct number entry. Reject invalid/out-of-range
   entries without moving. There must be no MSH metadata in this toolbar.
4. Inspect a value, scroll both axes, and change messages. Selection/caret and both
   scroll axes reset; structured diagnostics describe the new message. Navigate rapidly, cancel
   scans, retry, switch tabs, and close during a scan. No stale content may appear.
5. Open `artifacts/stage3/synthetic-100MiB-104857600.hl7`. The attachment is 100 MiB of
   encoded text; the file contains surrounding structure and a second ACK message.
6. Inspect MSH, PID, OBR and OBX. Expand OBX-5's ED components and select component 5.
   Confirm bounded preview, exact length, First/Previous/Next/Last and offset access.
   Its last eight characters are `VEFJTA==`; no PDF/image rendering occurs.
7. Confirm main abbreviation preserves OBX-6 and NTE, without raw text paging.
   Open Full Source Viewer for the value and use chunks/direct offsets for complete
   access, visible position feedback, and no 100 MiB line handed to the editor.
8. Hover and double-click within a middle inspector chunk. Confirm canonical
   `OBX[1]-5[1].5`, hierarchy reveal, selection intersection and correct highlighting.
   Test custom delimiters and escaped separators across window boundaries.
9. Select OBX-6 (`AFTER-FIELD`) and NTE-3 (`AFTER-SEGMENT`). Raw navigation and inspector
   must reach content after the attachment. Copy visible selections/previews and
   export the exact original value. Complete the consolidated UX acceptance checklist.
10. Navigate to message 2 and back; repeat. In Task Manager observe stabilization,
    while recognizing OS file cache differs from managed memory. Close the document;
    its owned snapshot directory must be removed after outstanding work drains.
11. Open malformed MSH input, opaque leading lines followed by PID, MLLP files,
    mixed/incomplete framing, batch wrappers, arbitrary data and invalid bytes.
    Confirm explicit warnings/failures, source-region hexadecimal paging, and no crash.
12. Repeat accepted Stage 2 raw hover/MSH/custom delimiter/double-click checks, floating
    tools, rapid tab switching, final-tab close/reopen, Save/Restore/Reset Layout and
    shutdown with floating tools. Imported data must not enter layout files.

Supplemental Linux/macOS, mixed-DPI, native clipboard and filesystem lease behavior
are not certified by Windows/headless tests. Do not commit or push before the user
completes native acceptance and gives explicit approval.
