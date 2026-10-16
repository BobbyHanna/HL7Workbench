# Project context

HL7 Workbench is an independent, local-first HL7 v2 development tool. Windows is
the initial desktop platform. No runtime telemetry, cloud features, or HL7 content logging.
All committed examples and tests must be synthetic.

## Current milestone and review boundary

Stage 6A message search/filter, including the final Find Messages UX cleanup, passed
native Windows acceptance and was explicitly accepted by the user on 2026-10-16.
The user authorized one milestone commit on main from the accepted/pushed Stage 5B
baseline 0497180b3799c5075f9fbc9dd6d778b48e68d279, followed by a normal push to origin/main.
Commit message: `feat: add Stage 6A message search and filtering`.
No additional features, source/test changes, history rewriting, force-push, tag, release,
installer or distribution package is authorized. Earlier pending-acceptance/Git statements
below record historical verification checkpoints and are superseded by this authorization.

Find Messages is a dockable/floating optional tool with Text / HL7 Field literal search,
case sensitivity, Contains/Equals, progress/cancel/clear and 200-row virtualized pages.
HL7 Tools → Find Messages, View → Find Messages and Ctrl+F share one open/reveal/focus
command; none executes a search. The redundant Focus Search ribbon entry is removed;
Clear Search / Restore Navigator and the tool's Search/Clear buttons remain. The original
`tool.search-results` ID preserves saved layouts and window recovery. Ctrl+F focuses the
value input from main/floating roots. No new top-level menu, settings or query language.
Field search uses Core's shared scanner over all matching segments/repetitions, respecting
MSH conventions, raw values, custom delimiters and malformed-but-readable input.

Workspace borrows the existing snapshot/index and immutable bounded draft strings;
scans run off the UI thread with one message's text/structure resident. A content-only
SearchRevision distinguishes edits from navigation; edits/undo/redo, switching, closure
or replacement cancel/clear stale work and filters. Disposal drains borrowed readers
before snapshot cleanup. Results retain bounded metadata only (no snippets/patient values)
up to the existing 100,000-message ceiling; headers are capped and control IDs remain
session-only. Filter Navigator restricts the existing file toolbar to original matching
ordinals without copying/removing messages or changing source/dirty state.

AGENTS.md now requires permanent regression-test/fixture/script retention and cleanup of
owned disposable artifacts only, preserving user files, useful failure diagnostics and
ignored build/publish outputs. See message-search.md, ADR 0008 and
stage6a-search-acceptance.md for semantics, bounds, known limits and exact native steps.

Stage 6A verification on 2026-10-16 passed locked restore, the Release solution build
(0 warnings/errors), 60 focused search/UI cases, all 862 automated cases and all 387
Workspace stress cases. Stress covers 50,000 result rows/paging, zero matches, in-flight
cancellation and a 32 MiB field. A test-only negative-GC-delta assertion was corrected
without relaxing either upper memory limit; focused, full and stress runs passed again.
Worker/package/isolation boundaries are unchanged, so published-worker checks were not
repeated. All 23 changed/new files were reviewed; whitespace/conflict checks and the
synthetic fixture hash passed. Existing local artifacts/build outputs/user CSV remain;
no Stage 6A disposable probes were created. At that checkpoint, main stayed at 0497180,
nothing staged. The user subsequently confirmed both functional and final Find Messages
UX native acceptance. Search/Workspace behavior is unchanged by that cleanup.

The final UX cleanup passed locked restore, a zero-warning/error Release rebuild, 33
focused Desktop cases (6 search + 27 shell/ribbon/editor) and all 862 automated cases
(0 failed/skipped). It retains the original layout ID, removes the redundant focus alias
and binds View/ribbon/Ctrl+F to the same open/focus command without automatic execution.
Tests use the production Dock theme/template to check actual revealed input focus and
verify existing results/filter state survives focus. Workspace source/tests are unchanged,
so the prior 387-case stress pass was retained without another stress run. Thirteen files
were updated within the existing 23-file Stage 6A working tree; whitespace/conflict and
fixture-hash checks passed. No new disposable artifacts, staged files or Git operations.

Final pre-commit review confirmed the expected 13 modified tracked / 10 new files and
empty index on main at the baseline. All 17 changed C#/markup files match the existing
verified Release portable-PDB source checksums (zero mismatches); the synthetic fixture
hash is unchanged. Source/tests were not edited and expensive checks were not rerun.
Only these existing milestone documents were updated to record final acceptance and
Git authorization; the accepted 23-file inventory remains intact.

### Accepted Stage 5B checkpoint and historical authorization

On 2026-10-16 the user confirmed final native Windows acceptance of Stage 5B, including
the safe overwrite and compact-dialog corrections, and explicitly authorized one
milestone commit and a normal push to existing origin/main from baseline 6dfa9f4.
Commit message: `feat: complete Stage 5B unified scripting and desktop tooling`.
No amend, rebase, force-push, tag or release is authorized. Stop if origin/main advanced.
The accepted verification remains the zero-warning/error Release build, 802 automated
cases, 333 Workspace stress cases, published-worker smoke/hashes/dependency isolation
and native Windows acceptance. Final review makes only acceptance-documentation,
generated-export ignore and fixture attribute changes; implementation/test code remains
the verified version.
The known synthetic-summary.csv native export stays local and ignored; the reviewed
synthetic HL7 fixture is included. Earlier pending-acceptance/Git restrictions below
describe historical checkpoints and are superseded by this explicit authorization.
Final review checked all 48 changed C# files against existing Release portable-PDB
source checksums (zero mismatches); MainWindow markup predates that verified build.
The published-worker checks passed again under Windows PowerShell 5.1. Expensive
automated/stress runs were not repeated because implementation/tests are unchanged.
The commit inventory is 64 reviewed files, with generated outputs excluded. A targeted
Git binary attribute preserves the synthetic fixture's protocol CR terminators and
exact reviewed bytes; its documented SHA-256 is unchanged.

### Earlier final save/dialog correction checkpoint

The user completed native Stage 5B functional checks for unified scripting, Library,
reports, ribbon, Preferences and document workflows. Two final acceptance corrections
remain for native retest: confirmed Save File replacement and compact confirmations.
All three picker workflows (shared ordinary/generated HL7 Save As, CSV export and exact
source export) now use one Desktop authorization adapter and Workspace FileSaveDestination.
The installed Avalonia 12.1.4 Windows backend was inspected: ShowOverwritePrompt=true
retains Windows' default overwrite consent, so no second application prompt is shown.
Other providers require application consent for existing local files. A path alone
allows creation only. Complete same-directory staged writes are flushed/closed, then
identity/metadata/hash and location are checked before File.Replace; collisions/changes
fail safely. Associated Save needs no repeat prompt. Existing streaming/byte/undo/script
contracts remain, with no worker/API or persistence changes. Native-confirmation and
final check-to-replace races are documented rather than claimed impossible.

Unsaved HL7/script, Delete Script and fallback Replace confirmations share a content-sized
420-pixel layout, wrapping, aligned buttons and safe Cancel/Escape/window-close behavior.
See `save-and-export-safety.md` for the full before/after inventory, provider evidence,
filesystem limits and exact native retest. The finishing checklist records verification.
Final locked restore/Release build passed with zero warnings/errors; 80 focused checks
and all 802 automated cases passed, zero failed/skipped (33 added). Refreshed folder
publish and full Windows PowerShell 5.1 worker/hash/dependency checks passed. No mocked
picker result is claimed as native confirmation/visual/DPI acceptance.
Workspace stress was rerun for the shared file-publication changes: all 333 passed.
Diff/whitespace/conflict checks passed across all 63 modified/untracked files. The
ignored investigation probe was removed; the synthetic fixture hash is unchanged.
Main stays at 6dfa9f4, with 28 modified tracked / 35 untracked and nothing staged.
Do not stage, commit, push, tag or release; explicit final acceptance and Git authorization
are still required. Earlier checkpoints below are historical.

### Earlier final-menu checkpoint

The user confirmed the corrected Stage 5B ribbon looks substantially better and switches
tabs with one click. The final menu cleanup leaves **File | Settings | View**, removing
Scripts and Window dropdowns. All ten former Scripts entries retain their shared command
instances in the Scripts ribbon; Open Script Editor was added there. Home → Workspace
retains Save/Restore Layout and now exposes Reset Layout / Recover Windows. All six tool
visibility commands stay in View. Scope, Review Output, Reports / Export CSV, keyboard
shortcuts and docked/floating editor controls are retained. Ribbon tabs, accepted styles,
theme bindings, input routing, pinning and persistence are unchanged. No runtime, library,
export, Dock, HL7 or schema implementation changes belong to this cleanup.

Final menu verification on 2026-10-15: locked restore passed; Release build passed with
zero warnings/errors; all ten focused menu/ribbon/shortcut cases and all 769 automated
cases passed (zero failed/skipped). The large-file stress suite was not rerun for this
menu-only change; its earlier successful result remains historical. See the complete
command inventory and remaining native steps in `stage5b-ui-finishing-acceptance.md`.
Stage 5B remains uncommitted on main; nothing may be staged, committed, pushed, tagged
or released without separate explicit authorization. Full native acceptance is pending.

### Earlier ribbon correction checkpoint

Initial native acceptance of the Stage 5B ribbon failed for light command surfaces in
dark mode, weak hierarchy/contrast and two-click tab switching. The authorized defect
correction keeps Popup.Child stable, passes overlay input only through the tab/pin strip,
and switches temporary content on the first click without dismissing it. The selected
tab toggles temporary visibility; outside/Escape dismissal and pinned preferences remain.
Dynamic Fluent resource bindings follow the window's dark/light theme. Compact vector
commands now form Script Library / Execution / Results groups with an accent Run tile;
Home/HL7 Tools use the same layout and networking stays disabled Planned placeholders.
The user subsequently confirmed improved appearance and single-click switching;
remaining native checks are tracked in the checklist and previews alone are not acceptance.
See the explicit ribbon retest in `stage5b-ui-finishing-acceptance.md`. No runtime, API,
library/settings persistence, export safety, Dock or source-byte behavior changes.

Ribbon correction verification on 2026-10-15: locked restore and Release build passed
with zero warnings/errors; seven focused ribbon checks and four isolated Skia rendering
cases passed, followed by all 768 automated cases (zero failed/skipped). Dark/light
pinned/overlay PNGs were inspected and remain in ignored artifacts/ribbon-visual/.
Publish and the complete worker/hash/dependency checks passed under Windows PowerShell
5.1. Diff/whitespace checks and all 51 modified/untracked files passed inspection.
Temporary investigation/comparison files were removed. Main stays at 6dfa9f4, with
23 modified tracked / 28 untracked and nothing staged. This predates the final menu cleanup.

Stage 5B's earlier UI/UX finishing pass added a permanent Scripts menu (now removed), a collapsed-
by-default utility ribbon (Home, Scripts, HL7 Tools, TCP/MLLP), and a separate General/HL7
Preferences window. Temporary commands overlay Dock without changing its viewport;
pin/unpin and Preferences share atomic per-user ribbon persistence. General also reuses
the existing new-document Edit Mode preference and Settings checkbox. Core has no target-
schema interpreter/validator: selected target and interpretation mode are reserved metadata,
distinct from each message's declared MSH-12; controls are disabled with an explanation.
No schema catalog, source mutation, conflict/conformance results or networking is added.

The ribbon/editor share the retained script model's UiCommands and operation gates.
Two compact editor rows contain library/name/New/Save/⋯ and scope/Run/Cancel/state.
Inline rename confirms a valid draft name; Save Script explicitly persists name/code.
Secondary actions include Duplicate, Rename, Delete, Review and Reports / Export CSV.
Delete only targets saved user entries with dirty consent, named confirmation and
lock/revision validation; canceled/failed deletion retains drafts, success selects the
simple bundled example. Generated HL7/CSV and report attribution remain independent.
Contextual reports preserve name/scope and block replacement throughout picker/export.
All unified runtime, containment, CSV and byte-preservation contracts below remain.
See `desktop-preferences.md` and `stage5b-ui-finishing-acceptance.md`. Native Windows
acceptance is pending; no commit/push/tag/release or staging is authorized.

UI finishing verification on 2026-10-15: locked restore and final Release build passed
with zero warnings/errors. All 762 automated cases passed (zero failed/skipped), adding
19 cases for defaults/recovery/forward fields, edit/schema separation, rename/delete,
command state, popup sizing/dismissal/keyboard behavior, Preferences and report retention.
Workspace stress passed all 320 cases, including 8,192 stateful messages, 4,096-message
streaming and the 256/512 MiB fixtures. Folder publish and the complete worker/hash/
dependency checks passed in Windows PowerShell 5.1 and PowerShell 7. A transient build
overlap with stress produced Windows copy-lock warnings; the final sequential build
and full tests passed cleanly. Diff/whitespace and all 50 modified/untracked file checks
passed. Main remains at 6dfa9f4 with 23 modified tracked / 27 untracked files and nothing
staged. The existing synthetic CSV export and earlier Stage 5B work are retained.

Stage 5B now uses unified C# scripting under ADR 0007. Every Run compiles once,
initializes once, calls required synchronous `void Main()` for each selected message
in source order, then optional `void Complete()` once on success. Roslyn syntax and
semantic validation discover entry points; one fixed host continuation binds callbacks
to the initialized submission, retaining shared variables without per-message compilation.
Current message and Whole document use identical source/lifecycle and fresh state per Run.
`Message` resolves only during Main; `Batch` supplies reports, Defer/Hold/Emit and ordinal
access, never a script kind. Existing bounds, lossless capture, working edits, privacy,
Windows worker containment and transactional HL7/report publication remain intact.

The editor has one New Script action and an Apply to: scope selector. Kind labels,
type changes and ScriptMode are removed. Last-successful report attribution captures
script name/scope only. Definition API/protocol are version 2; library schema 3 stores
identity/name/source and an obsolete-record warning flag, with no kind metadata.
Version-1/2 records retain their files/source, display Needs update and cannot Run until
the author rewrites and explicitly saves. Duplication preserves that warning. Settings,
docking, font/color behavior and immutable duplicable examples are preserved.

The unified redesign requires native Windows acceptance. No commit/push/tag/release
is authorized. See `script-api.md`, ADR 0007 and `stage5b-batch-scripting-acceptance.md`
for current contracts, verification and exact acceptance steps.

Unified final verification on 2026-10-15: locked restore passed; Release build had
zero warnings/errors; all 743 tests passed with no failures/skips. The 23 new unified
lifecycle cases cover shared state, helpers, fresh locals/runs, completion, signature
validation and original diagnostic locations. Obsolete kind-only workflows were
replaced while persistence, dirty-state, source preservation, worker safety, CSV,
attribution and tool coverage remain. Workspace stress passed all 306 cases, including
8,192 stateful messages, the 4,096-message stream and 256/512 MiB fixtures. Folder
publish and published one-/multi-message unified worker smoke passed, including CSV,
completion, Hold/Emit, clean exit, all worker file hashes and dependency isolation.
`git diff --check` and all 35 modified/untracked file checks passed. The temporary
ignored `artifacts/unified-probe/` investigation project was removed; permanent
lifecycle tests and publish smoke retain its coverage. The user's synthetic-summary.csv
was retained. Main remains at 6dfa9f4 with 19 modified tracked and 16 untracked files,
nothing staged and no commit/push/tag/release. Stop for native Windows acceptance.

## Historical Stage 5B split scripting and editor polish

Stage 5B stateful Batch scripting passed practical native Windows functional
acceptance. The user requested final Script Editor UX polish; its native Windows
retest is pending. Leave every change uncommitted; no commit/push/tag/release is
authorized. ADR 0006 extends the accepted 5A worker/facade, source capture, library
and dockable editor. Top-level Batch initialization registers synchronous Process
and Complete callbacks sharing captured locals in a fresh worker/job. Message scripts
retain their fresh-per-message behavior. Source working edits are captured, never
mutated. Deferred copies retain source ordinals for parent boundary/envelope checks.
Reports stream through bounded worker buffers into a Workspace snapshot journal;
only the complete validated HL7 output opens. The user then exports selected CSV
reports explicitly, through a flushed temporary file and no-replacement move.
Failures/cancellation clean private artifacts; export failures preserve completed
output and staged reports for retry. Last successful reports live in the editor
session until replacement or model disposal, with read leases for pending exports.
Version-1 scripts read as Message; atomic version-2 saves retain kind. The immutable
identity example keeps at most 10,000 raw PID-3 keys in worker memory, rejects missing/
ambiguous/diagnosed input and reports counts only. It is not complete de-identification.
No new dependencies, Core changes, source mutation or report viewer are introduced.
See `script-api.md`, ADR 0006 and `stage5b-batch-scripting-acceptance.md` for API,
bounds, transaction guarantees, verification and the pending native gate.

Stage 5B verification on 2026-10-15: locked restore and zero-warning/error Release
build passed; all 714 tests passed (211 Core, 302 Workspace, 129 Desktop, 72 Scripting;
78 added). Workspace stress passed 304 cases including 8,192 stateful messages and
repeated patient references, plus existing 256/512 MiB fixtures. Folder publish,
published Message/Batch worker smoke, worker Release/publish hashes and dependency
isolation passed. `git diff --check` and full tracked/untracked inspection passed.
The user subsequently accepted Stage 5B functionality and identified two final
editor UX issues. Creation now uses New → Message/Batch; type changes use the
secondary ⋯ → Change Script Type menu and preserve source. Library selection shows
the actual draft kind, including unsaved/type-changed drafts. Retained CSV reports
have session-only execution-time script name/kind/scope attribution, labeled Last
successful run with origin tooltips. Switching scripts, renaming, cancellation and
failure do not relabel prior reports. Runtime/API/CSV transactions are unchanged.
Final polish native Windows acceptance is pending; all changes remain uncommitted.

Final Script Editor UX polish verification on 2026-10-15: locked restore and a
zero-warning/error Release build passed; all 722 tests passed (211 Core, 304 Workspace,
135 Desktop, 72 Scripting; eight added). Focused editor/library checks passed 25/15
cases. Workspace stress passed 306 cases. Folder publish was refreshed and published
worker Message/Batch smoke, hashes and dependency isolation passed. No worker/API,
Core, CSV transaction, dependency or packaging changes were made by the polish pass.
`git diff --check` and all tracked/untracked file checks passed. The user's existing
synthetic-summary.csv native export was inspected and left untouched. Stop for final
native Windows UX acceptance; no commit/push/tag/release is authorized.

## Accepted Stage 5A baseline and historical checkpoints

The user authorized Stage 5A implementation only, following the revised Message/
Batch/Generate architecture. Stage 5A implements Message scripts: fresh Roslyn worker
per job, narrow Core facade, current/whole-document execution, captured unsaved edits,
bounded transactional independent output, local Script Library and dockable Script
Editor. The user has completed native Windows functional and visual acceptance,
including the final Script Editor refinements and tooltip discovery/dismissal fixes.
On 2026-10-15 the user authorized one Stage 5A milestone commit on `main` and a
normal push to `origin/main` in the existing private BobbyHanna/HL7Workbench repository.
No history rewrite, tag, release or Stage 5B implementation is authorized.
Subsequent native testing found stationary-pointer tooltip reopening in the normal
RAW editor. A shared dismissal guard prevented reopening, but subsequent native
testing found poor hover discoverability: exact matching of retained event pixels
conflicted with AvaloniaEdit's hover tolerance, and its timer restarted inside fields.
The current Desktop controller separates rendered-row acquisition, one normal 400 ms
deadline per structural field, and screen-coordinate click/scroll dismissal. It uses
the latest structural hit for the precise path and ignores obsolete AvaloniaEdit hover
events. Normal RAW, presentation and Full Source Viewer share this behavior. The user
confirmed native Windows acceptance; see `stage5a-native-tooltip-dismissal.md`.
The complete automated suite passes 636 tests (211 Core, 272 Workspace,
125 Desktop, 28 Scripting), with locked restore and a zero-warning/error Release build.
Workspace stress passes 274 cases. Folder publish, published-worker smoke,
Release/publish hash and dependency-isolation checks, and `git diff --check` pass.
Batch callbacks/state, synthetic identities, CSV, generation/templates, structural
insertion/removal, cross-document analysis, working-state application, IDE/debugging
and untrusted-code sandboxing remain deferred. See ADR 0005 and the Stage 5A checklist.
The worker executes trusted code with user permissions; it is not a security sandbox.

Stage 5A final automated verification on 2026-10-15: locked restore passed; Release
build passed with zero warnings/errors; all 594 tests passed (211 Core, 262 Workspace,
93 Desktop, 28 Scripting), including 67 new cases. Workspace stress passed all 264
cases with `HL7WORKBENCH_STRESS=1`, including the existing 256/512 MiB fixtures and
4,096-message scripting stream. Framework-dependent folder publish and its worker
compile/transform/finish/clean-exit smoke check passed. `git diff --check` passed.
The user subsequently confirmed completion of the native Windows functional
checklist in `docs/stage5a-scripting-acceptance.md`, identifying cramped Script
Editor layout and dark method names for this authorized UX polish pass.

Script Editor refinements keep runtime/Core/protocol/output transactions unchanged.
The centralized VS Dark-inspired palette now has a bounded compiler-only Roslyn
symbol overlay over the immediate AvaloniaEdit lexical fallback. Desktop gains
CSharp/Common 5.9.0 for background symbol binding (explicit amendment to worker-only
Roslyn loading); only ScriptHost uses the scripting runtime/executes code. Analysis
is debounced, cancellable, serialized, revision/document guarded and view-owned.
Resolved methods are light yellow, types teal; unresolved names/properties/delegate
variables stay neutral. No Workspaces/LSP/IDE infrastructure or source cache is added.
Font state uses integer points: new default/reset 12 pt, 1 pt steps, 9–24 pt limits.
V1 DIP preferences retain the nearest whole-point choice without rewriting on load;
subsequent changes atomically save V2 FontSizePoints. Footer displays e.g. `12 pt`.
Ctrl+wheel, Ctrl++/−, numpad Add/Subtract and Ctrl+0 work in code only. Two compact
scrollable toolbar rows and collapsed/absent diagnostics reserve code space. Existing
dock placement is retained. See `docs/stage5a-script-editor-visual-acceptance.md`.

Polish verification: locked restore and Release build passed with zero warnings/errors;
all 606 tests passed (211 Core, 267 Workspace, 100 Desktop, 28 Scripting), with 12 new
cases. Workspace stress passed all 269 cases. `git diff --check` passed. The complete
suite exposed an existing cleanup metadata race under concurrent snapshot disposal;
moving the lookup into its existing best-effort I/O guard fixes it without changing
snapshot ownership/publication. A concurrent-capture regression reproduced the original
failure and passes with the fix. Native visual acceptance was pending at that checkpoint.

Final point-size/contextual-color refinement verification: all 619 tests passed
(211 Core, 272 Workspace, 108 Desktop, 28 Scripting), including 13 new cases beyond
the 606-test polish pass. Workspace stress passed all 274 cases. Locked restore,
zero-warning/zero-error Release build, folder publish/dependency isolation checks
and `git diff --check` passed. Compiler-only CSharp/Common assemblies now ship/load
with Desktop for the requested symbol colors; scripting/execution assemblies remain
in ScriptHost. The user subsequently confirmed final native Windows acceptance.
Git actions were unauthorized at that checkpoint. The current authorization is recorded
above. See `stage5a-final-ci-investigation.md` for
the historical tooltip failure investigation, final verification and pending-file inventory.
That final pass reproduced a late-hover/click-dismissal defect in PresentationView
and added a view-local guard with two deterministic regressions. All 621 tests pass
(211 Core, 272 Workspace, 110 Desktop, 28 Scripting); Workspace stress passes 274.
Locked restore, zero-warning/error Release build, folder publish, deployed-worker
smoke/dependency-isolation checks and `git diff --check` pass. Historical attribution
remains qualified because the CI logs contain no pointer/timer trace.

Milestone 1 is complete: lossless, bounded Core parser with 211 regression tests.
Milestone 2's docking foundation is complete: synthetic placeholder documents,
Message Browser, Diagnostics, persistence, and floating tools. The user passed the
manual acceptance gate on 2026-10-13, including rapid tab switching, closing inactive,
active, and final documents, floating both tools, docking/refloating, main-window
shutdown with floating tools, layout restoration, and Reset Layout.
The user authorized Stage 1 of the read-only viewer. Implementation now opens
bounded local files in AvaloniaEdit document tabs with Core-based highlighting.
Stage 1 passed user manual acceptance on 2026-10-14 in all eight categories:
file opening, document tabs, syntax highlighting, read-only behavior, scrolling,
malformed messages, docking, and persistence and shutdown. The user authorized
Stage 2: Segment Navigator and Field Inspector. The user confirmed all 10 original
manual acceptance categories passed on 2026-10-14. Viewer Stage 2 is completed and
formally accepted on October 14, 2026, including delimiter-driven visible hierarchy,
raw double-click reverse navigation and RAW editor structural path tooltips.
All six final native Windows checks passed: raw hover; structural paths including
repetitions/subcomponents/custom delimiters; MSH numbering/edge cases; tooltip
dismissal and selection/caret preservation; double-click navigation; and copying,
tabs and docking regressions. The user authorized final verification and commit/push
of the accepted Stage 2 changes to main. Viewer Stage 3 implementation was subsequently
explicitly authorized, including large-message source-backed inspection. Stage 3 is
complete and explicitly accepted on October 14, 2026, including its UX refinements,
Windows fixes, Activity Log and final workspace layout. The user authorized milestone
verification and one local commit with the message
`feat: complete Stage 3 multi-message workspace and source inspection`.
No push, publication, tag or release is authorized. Stage 4's editable-document
foundation passed explicit user native Windows acceptance on 2026-10-15, including
the raw-editing crash and inspection-flicker fixes. The user authorized final
verification and one local commit with the message
`feat: complete Stage 4 editable HL7 document foundation`.
See `docs/decisions/0004-editable-document-foundation.md` for the architecture
assessment and `docs/stage4-editable-document-acceptance.md` for scope and native steps.
The clarified hover target is the upper RAW HL7 editor. Earlier inspector tooltip
investigations resulted from testing over raw text and did not establish an inspector bug.
Raw editor hover now uses AvaloniaEdit's hover events and glyph-floor coordinates
with scroll offsets, then Workspace's non-mutating ResolveOffset over cached Core
structural projections. A standard Avalonia tooltip shows only canonical path and
structural indices, without selection/caret changes, raw values, or semantic names.
Inspector tooltips, obsolete tooltip tests, the diagnostic Button/panel/probe and
F8 handling have been removed. Native Windows RAW editor hover acceptance is complete.
Stage 3 now uses immutable streamed file snapshots, a complete byte-oriented message
index, lazy active-message scanning, and bounded source access in the same file tab.
The consolidated UX refinement passed native acceptance: large
values are abbreviated through explicit source/display interval mappings, with
natural scrolling and a virtualized bounded-block fallback. Segment Navigator and
Field Inspector are now dockable tools sharing document-owned selection. Full Source
Viewer is optional and lazy, with contiguous source windows and exact original-
byte export. It retains explicit source context across document-tab switches;
same-file message navigation still invalidates it. The user chose to defer changing
that source-lifetime limitation during layout polish. Closing the tool or its owning
document releases the session. Version-2 layout JSON remembers placement without restoring source
content or instantiating the inspector. Version-1 layouts remain supported.
See `docs/viewer-stage3-ux-acceptance.md` for policy, lifecycle and native checks.
The first native Windows pass found a blank docked optional viewer and usability
issues; the subsequent fixes passed the user's native acceptance. Those fixes track Dock
Context replacement in cached templates, use consistent Full Source Viewer naming,
distinguish value/message scopes, hide unnecessary value-chunk controls and add
control tooltips. Old layouts introduce absent structural-tool identities without
overriding explicitly closed tools. See `docs/stage3-windows-acceptance-fixes.md`
for the reproduced root cause and regression evidence.
Activity Log now replaces the user-facing Diagnostics tool with a typed, privacy-
conscious application-session history, capped at 2,000 entries. It records meaningful
operations and real structured diagnostics without filenames, paths, source text or
exception messages. It is visible in fresh/reset workspaces, honors saved visibility,
releases its projection on close and preserves the shipped
`tool.diagnostics` placement identity. Status shows current operations and actual
warning/error counts; a clickable indicator opens retained activity. Parser/index
diagnostics remain intact; routine navigation does not add duplicate warnings.
See `docs/stage3-activity-log-acceptance.md` for lifecycle, privacy boundaries,
resource bounds and compatibility. The latest default arrangement is a 78% document
area on the left, independent Navigator/Field Inspector docks stacked in the 22%
right column, and Activity Log across the bottom (27% height). Message Browser is
hidden in new defaults but retained for saved layouts and explicit opening. Valid
saved layouts keep their groups, visibility, proportions and floating geometry.
See `docs/stage3-workspace-layout-acceptance.md` for the current native checklist,
layout policies and the explicitly deferred source-viewer lifetime discrepancy.
See `docs/viewer-stage3-acceptance.md` for current architecture, limits and manual steps,
and `docs/stage3-performance.md` for measured results.
See `docs/viewer-stage1-acceptance.md` for architecture, limits, and exact steps.
See `docs/viewer-stage2-acceptance.md` for current changes and manual acceptance.
Dock model tests use a fake host for native-window operations; supplemental monitor,
mixed-DPI, and OS-level behavior still benefit from platform-specific verification.
Milestone 2 / Viewer Stage 3 provide the accepted read-only baseline. Accepted Stage 4 adds
bounded raw editing, history, dirty state and document saving; scripting/SQL remain deferred.
Do not commit or push without user authorization.

## Projects and boundaries

- `src/HL7Workbench.Core`: dependency-free parser, paths, escapes, diagnostics,
  controlled edits, and byte-preserving serializer. No UI, files, framing, or transport.
- `src/HL7Workbench.Workspace`: document lifecycle/active context, lazy tool registry,
  bounded application-owned layout DTO and screen recovery policy. No Avalonia/Dock.
- `src/HL7Workbench.Desktop`: Avalonia shell. Dock types stay in `Docking/` and shell
  wiring; feature models in `ViewModels/` have no docking inheritance.
- `tests/HL7Workbench.{Core,Workspace,Desktop}.Tests`: synthetic regression and
  coordination tests. Dock tests use the real model with a fake host, not native GUI.

DocumentManager provides the shared active-message context. Active document changes
come from Dock active-tab changes; focusing a tool retains the selected document.
Closing a document selects another open document or clears context. Feature view models
unsubscribe on disposal. Future Script Editor/Library are tools with independent script
storage; generation, scripting, transport, Synthea, and IRIS services remain UI-free.
Layout files contain IDs/geometry only, never messages, filenames, or scripts.

## Viewer layout and future requirements

- Stage 2 implements the raw HL7 viewer above a lower-left Segment Navigator and
  lower-right Field Inspector, with adjustable internal splitters. Document-owned
  structural selection survives tab/view changes; it is not serialized in layouts.
  Stage 3's production shell promotes these same controls into independent Dock tools;
  the latest defaults stack them on the right above a full-width bottom Activity Log;
  the embedded-pane composition remains available for component regression tests.
- Inspection keeps canonical paths while suppressing implicit visual wrappers.
  Double-click raw text to reveal the deepest visible structural node. Hover RAW
  editor values for canonical coordinates only; inspector rows have no tooltips.
- Stage 3 combined multi-message files use a fixed toolbar at the bottom of the raw-message
  pane: first, previous, next, last, and message position/count.
- Prioritize combined single-file batches over directory-wide processing.
- Keep structural parsing version-independent and preserve future version/profile
  extensibility, with semantic definitions separate from parsing and rendering.

## Conventions and validation

.NET 10; SDK floor 10.0.401 with latestFeature roll-forward. Avalonia 12.1.4,
Dock 12.1.0.7, xUnit 4.0.2 / Microsoft Testing Platform. Central package versions
and committed lock files. Preserve compiled bindings and BuildServices exclusion.
In PowerShell set `DOTNET_CLI_TELEMETRY_OPTOUT`, `AVALONIA_TELEMETRY_OPTOUT`, and
`TESTINGPLATFORM_TELEMETRY_OPTOUT` to `1` before commands:

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
git diff --check
```

Core's materialized parser still parses one bounded message at a time; its shared
range scanner also supports source-backed inspection without payload-sized strings.
Workspace owns original snapshots plus at most one cached decoded spool per document,
with no cached inactive structural models. Stage 4 additionally retains bounded
per-message working text/delta history: complete messages only (1 Mi UTF-16 units,
16 Ki per line), 32 MiB character-storage budget / 4,096 history entries per document.
Read-only is still the default; a separate persistent Start in Edit Mode preference
applies once at open. Edit Mode off retains dirty working state. Parsing is debounced,
version-checked and uses the existing global scan gate. Save streams snapshot bytes
plus changed payload overlays and publishes a flushed same-directory temporary file.
Original snapshot/index ranges remain immutable across Save/Save As; edited working
offsets are not original-byte coordinates. Original-source tools are unavailable for
edited messages until saved output is reopened. Close prompts preserve drafts on
Cancel, dismissal or save failure. Native Windows Stage 4 acceptance was explicitly
granted by the user on 2026-10-15.
Byte input preserves encoding/BOM;
MSH-18 does not select encoding. Messages are mutable/not thread-safe; reacquire
model snapshots after edits. Never use display text as a save source.

Initial Stage 4 automated verification on 2026-10-15: locked restore succeeded;
Release build succeeded with zero warnings/errors; the complete suite passed
518 tests (211 Core, 226 Workspace, 81 Desktop), including 40 new editing tests.
The Workspace stress run with `HL7WORKBENCH_STRESS=1` passed all 228 tests,
including the existing 256/512 MiB cases. `git diff --check` passed. A new 64 MiB
streaming-save test verifies unchanged prefix bytes and a sub-16 MiB allocation
budget. UTF-16 encoded framing collisions are checked with the existing indexer.
These automated/headless results did not replace native acceptance. At that point,
HEAD remained the Stage 3 baseline; no commit, amend, push, tag, publication or release
had been performed.

Native Stage 4 acceptance on 2026-10-15 found a critical raw-editing crash. It was
reproduced through real AvaloniaEdit routed input: `InvalidOperationException`,
"No undo group should be open at this point", from `UndoStack.ClearAll()` inside
`MessageInspectionView.WorkingTextChanged`. The fix configures zero native history
retention before input and removes transaction-time clears; Workspace history stays
authoritative. The three Stage 4 editing tests now use awaited `Task<bool>` UI
callbacks instead of accidentally selecting `async void`; a new routed-input/Dock
regression covers typing, deletion, paste, selection, history and fresh inspection.
See `docs/stage4-raw-editing-crash.md` for evidence and native retest steps.
Post-fix verification: locked restore passed; Release build had zero warnings/errors;
all 519 tests passed (211 Core, 226 Workspace, 82 Desktop). The Workspace stress
run passed all 228 tests, including 256/512 MiB cases. `git diff --check` passed.
At that point native acceptance and commit authorization were still pending.

The subsequent native editing UX fix preserves stable inspection tools. Workspace
now separates Current/Pending/Stale/Empty/Unavailable and retains one labeled,
non-current bounded inspection snapshot while parsing. `ActiveInspection` remains
current-only; old snapshot resolution/selection and offset-dependent UI actions are
blocked. Pending/Stale controls retain enabled styling; event guards reject stale
structural input while a fixed-height label identifies previous inspection data.
Dock/embedded panes stay mounted and rebind refreshed projections with
canonical selection, realized expansion and viewport restoration where possible.
Tolerant malformed text remains inspectable; empty text has its own state. Existing
debounce/limits, source-backed lifetimes, undo-group safety and dirty/Edit Mode
ownership remain unchanged. See `docs/stage4-inspection-continuity.md` for the event
trace, state contract, tests and native retest. The user subsequently accepted these
fixes as part of native Windows Stage 4 acceptance.
Final milestone verification on 2026-10-15: locked restore passed; Release build had
zero warnings/errors; all 527 tests passed (211 Core, 230 Workspace, 86 Desktop),
including eight continuity cases and extended native-input regression assertions.
All 232 Workspace stress tests passed, including 256/512 MiB cases; `git diff --check`
passed. The user authorized the final verification and one local Stage 4 milestone
commit. Push, amend, tag, publication and release remain unauthorized.

## Important documentation

- `HL7_Workbench_Specification.md`: product vision; milestone amendments govern scope.
- `docs/architecture.md`, `docs/decisions/`: architecture and accepted decisions.
- `docs/parser-api.md`, `docs/hl7-paths.md`: preservation, limits, paths, and edit rules.
- `docs/viewer-stage3-acceptance.md`, `docs/stage3-performance.md`: source-backed inspection and measurements.
- `docs/stage3-activity-log-acceptance.md`: Activity Log privacy, history and lifecycle.
- `docs/stage3-workspace-layout-acceptance.md`: accepted final layout, source-lifetime limitation and milestone verification.
- `docs/dependencies.md`: versions/licenses.
- `docs/decisions/0004-editable-document-foundation.md`: Stage 4 assessment and working-state/save contracts.
- `docs/stage4-editable-document-acceptance.md`: implemented editing, deferred scope and native Windows checklist.
- `docs/stage4-raw-editing-crash.md`: captured editor exception, fix, regression and native retest.
- `docs/stage4-inspection-continuity.md`: stable inspection snapshots, stale-action guards and native retest.
- `docs/docking-acceptance.md`: passed docking gate, supplemental checklist, and limitations.
- `README.md`: setup/run commands; `samples/synthetic-only/`: synthetic fixtures.
