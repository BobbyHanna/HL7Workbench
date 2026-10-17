# Stage 7: Message Comparison acceptance

Current scope: Stage 7 implementation, UX/paste/settings refinement and native default-view correction,
from the main baseline d7ad3e12ae63d8f42f1a26a743803ac081d5e6eb.
**Native Windows acceptance passed, as confirmed by the user on 2026-10-16**, including
the UX refinements and Side-by-Side default. The user authorized one accepted Stage 7
commit, `feat: add Stage 7 HL7-aware message comparison`, and a normal push to origin/main.
This supersedes the earlier pending-acceptance restrictions recorded below. No further
features, unrelated refactors, tags, releases, installers or distribution packages are authorized.

## What is implemented

New comparison sessions default to Side-by-Side: both empty HL7 panes are immediately
visible and editable for temporary text, including with no documents open. Differences
remains available through the checked view toggle as a full-area grouped summary:
type/path/structural description and wrapped before/after values, with bounded details.
Both presentations preserve the same result, selection and loaded panes. Source panes
scroll independently; their divider resizes. Switching views preserves their scroll/caret
state without recapturing or reparsing sources.

Compact A/B source controls provide Open Message, Original Message and editable
temporary Pasted Text. Direct pane typing/paste, Differences' Paste HL7 into A/B buttons
and Ctrl+V on a source control work with no documents open. Pasted typing debounces 250 ms; cancellation,
revision/generation checks and coordinate selection retention prevent stale results.
Switching source kinds/modes/themes/reparenting preserves each side's pasted draft.

Document/original sources stay read-only. Pasted text is never a main-document edit
or save. Compare With Original still captures the active unsaved draft against initially
loaded content; filtered original ordinals, cross-document choices and source lifetime
leases are retained. Source closure clears selected document inputs safely; inactive
document closure keeps independent pasted results.

Options contains ignore controls and secondary utilities. Copy menu/context actions
work in Differences and read complete original ranges within their size limit.
The count marks active ignore options. Existing HL7 syntax/difference palette follows
Light/Dark. The Script Editor's separate VS Dark palette remains deferred.

Settings contains Preferences only. The redundant Start in Edit Mode menu entry,
click handler and menu-only synchronization are removed. Preferences → General,
the same JSON value, persistence, startup default and existing-document behavior remain.

## Representation and changed files

- Core: the existing Hl7Comparison engine is unchanged by this refinement.
- Workspace: ComparisonInput owns two independent session-only source slots/drafts;
  ComparisonSource captures document/original or immutable pasted text. Pasted sources
  need no Workbench document, disk snapshot or decoded spool. MessageComparison reuses
  the shared scanner and retains existing document read leases, bounded reads and cleanup.
  MessageDocument.Comparison supplies source labels reflecting unsaved working state.
- Desktop: ComparisonViewModel shares result/selection/presentation and debounces input;
  ComparisonSourcePicker supplies compact kind/document/paste flyouts; ComparisonView
  presents the two modes. Exact clipboard insertion avoids native editor newline conversion.
  Existing shell/Dock integration is retained. MainWindow markup/Editing/Shell lose only
  the redundant settings menu wiring.
- Tests: all existing comparison and permanent stress tests retained. New
  PastedComparisonTests and ComparisonRefinementTests cover input/state/clipboard/settings/
  rendering. Existing comparison UI callbacks now return awaited Task<bool> results.
- Documentation: README, PROJECT_CONTEXT, architecture, desktop-preferences,
  message-comparison, this checklist and ADR 0010 describe the current scope.
  The existing permanent synthetic generator remains available.

No dependencies/lock files, parser/edit limits, scripting worker contracts or existing
synthetic fixture bytes changed. See [user guide](message-comparison.md) and
[ADR 0010](decisions/0010-read-only-message-comparison.md).

## Direct clipboard samples — invented data only

Copy only the HL7 lines below. LF from Markdown is supported; no Save or temporary file
is needed. All identifiers, patients, timestamps and observations are independently invented.

Message A:

~~~text
MSH|^~\&|SYN|TEST|DEST|TEST|20261016090000||ORU^R01|SYN-PASTE-A|T|2.5
PID|1||SYN-001^^^TEST^MR||Example^Ada
OBX|1|TX|SYN-A||ALPHA
OBX|2|TX|SYN-B||BETA
OBX|3|TX|SYN-C||GAMMA
~~~

Message B:

~~~text
MSH|^~\&|SYN|TEST|DEST|TEST|20261016100000||ORU^R01|SYN-PASTE-B|T|2.5
PID|1||SYN-001^^^TEST^MR||Example^Bea
OBX|1|TX|SYN-A||ALPHA
OBX|99|TX|SYN-NEW||INSERTED
OBX|2|TX|SYN-B||BETA
OBX|3|TX|SYN-C||GAMMA-CHANGED
~~~

With identical final-terminator treatment these produce five changes: MSH-7, MSH-10,
PID-5.2, one added middle OBX and the final OBX-5. Different copied final newlines
may add an explicit terminator difference; source text is intentionally preserved.

For document-backed/filter workflows the permanent generator creates A, B and a three-
message batch (UTF-8 without BOM, CR terminators) in ignored test-data/generated/stage7.
It refuses existing filenames; choose a new -OutputDirectory if needed.

~~~powershell
& ./samples/synthetic-only/New-Stage7ComparisonSamples.ps1
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
~~~

## Native Windows acceptance — passed, user confirmed 2026-10-16

The user reports completion of Stage 7 native acceptance, including its refinements and
Side-by-Side default. The retained checklist below documents the acceptance workflow;
headless/render checks remain separate automated evidence.

1. Launch with no documents open. Open HL7 Tools → Message Comparison. Verify Side-by-Side
   is visibly selected and both empty, editable A/B panes are immediately accessible.
   Type in either pane without visiting Differences; clear that temporary text.
2. Copy Message A above and paste into pane A. Copy B and paste into pane B.
   Alternatively keyboard-focus each A/B source control and press Ctrl+V. Confirm results
   appear after about 250 ms, with distinct temporary Pasted Text labels and no documents
   created. Exercise CR, LF, CRLF and a normal trailing newline without silent conversion.
3. Toggle to Differences; confirm its active state and Paste HL7 into A/B empty-state
   controls remain available when inputs are empty. With results, confirm type/path/before/after rows are readable, grouped by occurrence, and identify
   the middle OBX addition without cascading later OBX changes. Select the PID change.
   Use Previous/Next and F7/Shift+F7, including wraparound.
4. Open A's source control and edit Ada to Cara in Pasted Text. Confirm recomputation,
   unchanged B, retained typing focus/caret and selected coordinates where still present.
   Replace with clipboard text, clear it, then restore it. The other side remains intact.
5. Switch Differences ↔ Side-by-Side repeatedly. Verify the same selected difference,
   count, source choices and pasted drafts, with no new comparison/loading cycle. In
   Side-by-Side confirm syntax/highlighting, independent scrolling and usable pane sizes.
6. Select a difference in Differences and switch to Side-by-Side: both sources locate the
   relevant ranges. Added/removed content has one absent side and nearest same-name context
   where possible. Drag the divider and resize it with keyboard arrows.
7. Open a generated synthetic document. In one source control choose Open Message, document
   and original ordinal; keep the other side Pasted Text. Verify document source is read-only,
   with the correct document/ordinal label. Original Message instead uses initially loaded text.
8. In a main Workbench document enable Edit Mode and make an unsaved edit. Use ribbon/raw-
   editor Compare With Original. Confirm original A versus current unsaved B and no Save,
   dirty-state or undo/redo changes caused by comparison. Undo/redo clears stale results;
   Refresh captures current revisions.
9. Switch a side from Pasted Text to Open Message, Original Message and back. Verify the
   pasted draft is preserved and never silently overwritten. Repeat across modes and active
   document changes. With a search filter active, compare original ordinals 1 and 2 in the
   generated combined file; visible filtered row position must not replace the ordinal.
10. In Options toggle MSH-7, MSH-10, trailing empty fields and terminator ignores. Counts
    recompute and ignore options enabled appears unobtrusively. Unrelated changes remain.
    Comparing a source against itself says No differences found.
11. Use Copy menu and row context menu in Differences for Path, Before, After and selected
    Summary. Paste into disposable local scratch text and verify complete values, occurrence
    paths and missing/empty behavior. Expand long selected details; they are labeled previews.
    Oversized copies/pastes must give feedback and retain prior drafts, never truncate.
12. Paste A twice into one side. Verify an explicit multiple-MSH/single-message explanation,
    with the whole draft retained. Try empty, incomplete, malformed and custom-delimiter
    messages: useful diagnostics, retained characters and no crash or hidden first-message
    selection. Cancel/retry and rapid typing must not publish stale rows.
13. Change Light/Dark while open, including a floating comparison. Confirm legible row/syntax/
    highlight/selection colors, retained inputs/results/selection and natural keyboard focus.
    Resize at normal and docked sizes; the toolbar wraps and source names truncate with
    tooltips rather than overflowing horizontally. Select Differences, dock/float/resize,
    hide/reveal without closing, and return to document size: keep the active presentation,
    inputs, ignore options, result and selected difference. Repeat in Side-by-Side.
    Close/reopen or Reset Layout: pasted drafts/results are intentionally cleared and the
    genuinely new session defaults to Side-by-Side. Layout
    persistence stores placement only. Verify native monitor/DPI/keyboard behavior explicitly.
14. Open Settings: Start in Edit Mode is absent and Preferences remains. Preferences → General
    exposes Open documents in Edit Mode by default. Apply true, open a new document and
    confirm Edit Mode; existing documents keep their prior mode. Restart and confirm the
    value persists and still applies to new documents. Cancel abandons unapplied changes.
15. Close an underlying selected document during comparison. Results/selection invalidate;
    pasted drafts survive. Verify comparison never changed original bytes, document content,
    unsaved edits or undo history, never implicitly saved, and never invoked a script worker
    or decoded an attachment. Test a large synthetic document's labeled Earlier/Later windows.

Headless/Skia checks are supporting evidence; **they do not establish native acceptance**.

## Limits and deliberately deferred work

- Pasted text: 1 Mi UTF-16 characters per side, 16 Ki per line; rejected edits/pastes retain
  the entire previous draft. Unicode clipboard strings have no original encoded-byte claim.
  Temporary editors are session-only; no pasted history or main-document undo is added.
- Native acceptance included a user-reported single approximately 100 MiB HL7 message
  containing an embedded Base64 PDF. Comparison paste correctly rejected it under the
  existing 1 Mi UTF-16 limit. Large embedded-payload comparison is a known limitation;
  rejection must retain the previous draft without truncation. This correction changes
  no memory or paste limits. Streaming, hashing and ED-aware comparison are potential
  future enhancements, deliberately unimplemented here; no attachment is decoded/rendered.
- One pasted message per side. Multiple MSH messages are explicitly rejected with the text
  retained. Selecting an individual message from a pasted batch is deferred.
- Row previews: at most 256 characters with a visible preview label; selected detail preview:
  4,096 characters per side. Complete-value/summary copy: 1 Mi characters. Copy never uses
  abbreviated presentation fragments.
- Document-backed windows: 64 Ki characters with overlap and safe surrogate boundaries.
  Pasted editors display their complete bounded draft. Source/display/file-byte coordinates
  remain distinct. An absent side has no invented value span.
- Core alignment remains deterministic 64-segment / 32-repetition lookahead, without Set-ID
  keys. Distant reorderings/changed duplicate runs can produce additional differences.
  Insertions beside changed duplicates without an unchanged anchor may also align as
  additional modifications.
  Scanner budgets and the 20,000-difference cap fail explicitly without partial results.
- Supported escape equivalence uses each source's encoding for leaves up to 64 Ki; larger
  leaves compare raw. Encoding/BOM-only differences are outside decoded structural comparison.
  Malformed recovery coordinates are provisional. No version/profile field-description
  catalog or clinical correspondence algorithm is introduced.
- No merge/apply, arbitrary external-file picker, batch comparison, persistent comparison/
  pasted-text history, automatic save, source export, network/cloud processing or attachment
  decoding. Internal view splits and selections are not persisted. Script Editor VS Dark
  palette changes remain deferred.

## Automated verification commands

Run full and stress suites sequentially: existing stress helpers share generated filenames.

~~~powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
dotnet test --project tests/HL7Workbench.Core.Tests -c Release --no-build --no-restore -- --filter-class '*ComparisonTests'
dotnet test --project tests/HL7Workbench.Workspace.Tests -c Release --no-build --no-restore -- --filter-class '*ComparisonTests'
dotnet test --project tests/HL7Workbench.Desktop.Tests -c Release --no-build --no-restore -- --filter-class 'HL7Workbench.Desktop.Tests.ComparisonTests'
dotnet test --project tests/HL7Workbench.Desktop.Tests -c Release --no-build --no-restore -- --filter-class '*ComparisonRefinementTests'
$env:HL7WORKBENCH_STRESS = '1'
dotnet test --project tests/HL7Workbench.Workspace.Tests/HL7Workbench.Workspace.Tests.csproj -c Release --no-build --no-restore
Remove-Item Env:HL7WORKBENCH_STRESS
git diff --check
~~~

Optional isolated Skia rendering uses its own process because Avalonia caches fonts/backends:

~~~powershell
$env:HL7WORKBENCH_COMPARISON_RENDER = '1'
& tests/HL7Workbench.Desktop.Tests/bin/Release/net10.0/HL7Workbench.Desktop.Tests.exe -method '*PresentationFitsNormalAndNarrowWindowsAndRetainsStateAcrossThemes' -noLogo
& tests/HL7Workbench.Desktop.Tests/bin/Release/net10.0/HL7Workbench.Desktop.Tests.exe -method '*EmptyWorkspaceMakesPasteDiscoverableAndScrollableAtDockedSize' -noLogo
Remove-Item Env:HL7WORKBENCH_COMPARISON_RENDER
~~~

The 10 synthetic-only previews in ignored artifacts/comparison-visual were inspected:
both modes at 1000×700 and 600×450 in Light/Dark, plus two empty-state previews.
These previews are automated evidence. Native acceptance is separately user confirmed above.

Historical refinement verification on 2026-10-16 (before native acceptance):

| Check | Result |
| --- | --- |
| Locked restore | Passed; no lock-file changes |
| Release solution build | Passed; 0 warnings, 0 errors |
| Full solution suite | 1,121 passed; 0 failed/skipped; 1m 11s |
| Focused Core comparison | 43 passed; 0 failed/skipped |
| Focused Workspace comparison/paste | 33 passed; 0 failed/skipped |
| Focused Desktop original comparison | 7 passed; 0 failed/skipped |
| Focused Desktop refinement | 20 passed; 0 failed/skipped |
| Focused Stage 7 total | 103 passed; 37 new ordinary refinement cases |
| Isolated Skia render checks | 10 passed; 0 errors/failures/skips |
| Final sequential Workspace stress | 553 passed; 0 failed/skipped; 2m 06s |
| Existing HL7 fixture bytes | All 8 unchanged against HEAD blobs |
| Whitespace/conflict/secrets review | Passed across 32 changed/new files |
| Native Windows acceptance | Pending user execution |

## Investigation and retained coverage

### Native default-view correction verification — 2026-10-16

This focused correction changes only the session's initial presentation in
ComparisonViewModel; the existing View and Dock lifecycle already retain presentation
on an open model. Permanent tests now verify immediate empty-pane visibility/focus,
direct exact clipboard paste and typing without entering Differences, the checked toggle,
unchanged source captures/results/selected difference/options/pending/load tasks on view
switches, independent nonzero scroll offsets and caret retention, and both presentation
choices through hide/reveal/float/resize/dock. True close/reopen and layout restoration
create a new empty Side-by-Side session. No existing test was removed or weakened.

Files changed for this correction (within the existing uncommitted Stage 7 inventory):

- src/HL7Workbench.Desktop/ViewModels/ComparisonViewModel.cs
- tests/HL7Workbench.Desktop.Tests/ComparisonTests.cs
- tests/HL7Workbench.Desktop.Tests/ComparisonRefinementTests.cs
- README.md
- docs/PROJECT_CONTEXT.md
- docs/message-comparison.md
- docs/decisions/0010-read-only-message-comparison.md
- docs/stage7-message-comparison-acceptance.md

| Check | Correction result |
| --- | --- |
| Locked restore | Passed; no lock-file changes |
| Release solution build | Passed; 0 warnings, 0 errors |
| Full solution regressions | 1,124 passed; 0 failed/skipped; 1m 22.941s |
| Focused Core / Workspace | 43 / 33 passed; 0 failed/skipped |
| Focused Desktop comparison / refinement | 9 / 21 passed; 0 failed/skipped |
| Focused Stage 7 total | 106 passed; 3 additional permanent cases |
| Isolated Skia rendering | 10 passed; 0 errors/failures/skips; updated empty Light/Dark previews inspected |
| Existing HL7 fixture bytes | All 8 unchanged against HEAD blobs |
| Tracked/new whitespace and conflict checks | Passed across 32 files |
| Git | Same main baseline; 16 modified tracked / 16 untracked; nothing staged |
| Native Windows acceptance | Pending continued user execution |

Workspace/Core implementations and resource limits are untouched by this correction.
The 553-case stress run above remains historical refinement evidence; it was not rerun
for this Desktop default change. The complete ordinary regression suite was rerun.
An initial added test's direct vertical-offset request did not establish a nonzero
headless viewport; it now navigates to distant source lines, asserts distinct nonzero
offsets, then verifies retention. An initial test nullable warning was fixed; the final
Release build has zero warnings/errors. The 10 ignored synthetic render previews are
retained for review; no disposable HL7/CSV/scratch files were introduced.

### Earlier refinement investigation

The initial Stage 7 checkpoint reported 1,084 ordinary / 536 stress tests. Refinement
found several UI callbacks accidentally selected an async-void Headless Dispatch overload,
so assertions after awaits were not reliable. They now explicitly return awaited
Task<bool> results. All existing test cases remain and their UI assertions execute fully.
The correction exposed test setup mistakes (using DockControl.Factory rather than the
live layout's Factory, raising a routed menu action before attaching the replacement
view, and typing without clearing a selected difference). These were corrected without
removing assertions. Fully awaited narrow-window checks exposed cramped source panes;
hiding irrelevant pasted-source paging and shortening status text fixed the layout.

New permanent coverage adds pasted source handling, exact clipboard line endings,
document/original combinations, source independence, retained inactive drafts,
empty/malformed/multiple-MSH input, bounds, 80 rapid updates, cancellation, selection/
mode/theme/reparenting retention, source closure, cleanup and preference startup/restart.
Existing 6,000-OBX worst-case alignment, 20,001-difference rejection, 8/32 MiB comparison
memory/spool checks and permanent 256/512 MiB stress cases remain.

An exploratory filter with an unsupported interior wildcard selected zero tests; final
focused commands above use supported explicit/suffix filters. Earlier simultaneous full/
stress verification hit a shared generated-file sharing violation; subsequent runs are
sequential. No test or resource ceiling was weakened to hide these findings.
A final rebuild was mistakenly started before an earlier test process had exited,
producing Windows MSB3026/MSB3027/MSB3021 copy-lock diagnostics. The overlapping command
was canceled, test processes drained, and build/full/stress verification was then run
sequentially. Final results supersede that scheduling failure; no assembly-copy warning
was suppressed or reclassified as a passing build.

All eight existing HL7 fixture files are verified against HEAD blobs with git hash-object
--no-filters. The Stage 5B fixture SHA-256 remains
8ABFC4D2F05E9FBB47D9E6716BFEBCBAF2092BFE6B1EE693FC9AB3C3ADFA8086.
Existing artifacts, build/publish outputs, tests and user exports are retained. Initial
generator verification copies were removed; no new disposable HL7/CSV files were created
for this refinement. Synthetic rendering previews are intentionally retained for review.

Finalization authorization: native Windows acceptance is complete and the user authorized
the single Stage 7 commit and normal push described above. Stop after that operation;
Stage 8 requires separate authorization.

Pre-finalization Git inventory: main at d7ad3e12ae63d8f42f1a26a743803ac081d5e6eb,
with 16 modified tracked files and 16 new files (32 total), all unstaged. This includes
the original uncommitted Stage 7 implementation and its refinement. No staging, commit,
push, tag, release or publication was performed. The 10 ignored synthetic rendering
previews are intentionally retained; pre-existing artifacts/build outputs/user exports
remain untouched by cleanup. No new disposable HL7/CSV/scratch files remain.

Git finalization rechecks the expected baseline, live remote, all 32 intended files,
staged scope, whitespace/conflicts, synthetic fixtures and unwanted content/artifacts.
Only acceptance/authorization documentation is updated during finalization; source and
tests remain as verified. The prior locked restore, zero-warning/error Release build,
1,124 full / 106 focused tests, 553-case last stress run and 10 rendering checks are
retained results, not reruns performed solely for finalization. Documented limitations,
including oversized embedded-payload paste rejection, remain unchanged.
