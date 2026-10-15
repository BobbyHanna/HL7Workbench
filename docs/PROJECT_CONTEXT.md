# Project context

HL7 Workbench is an independent, local-first HL7 v2 development tool. Windows is
the initial desktop platform. No runtime telemetry, cloud features, or HL7 content logging.
All committed examples and tests must be synthetic.

## Current milestone and review boundary

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
No push, publication, tag, release or Stage 4 implementation is authorized.
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
No editing, undo/redo, message saves, dirty-state tracking, or destructive operations
in Milestone 2. These move to Milestone 3, whose revised scope needs later planning.
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
with no cached inactive structural models. Byte input preserves encoding/BOM;
MSH-18 does not select encoding. Messages are mutable/not thread-safe; reacquire
model snapshots after edits. Never use display text as a save source.

## Important documentation

- `HL7_Workbench_Specification.md`: product vision; milestone amendments govern scope.
- `docs/architecture.md`, `docs/decisions/`: architecture and accepted decisions.
- `docs/parser-api.md`, `docs/hl7-paths.md`: preservation, limits, paths, and edit rules.
- `docs/viewer-stage3-acceptance.md`, `docs/stage3-performance.md`: source-backed inspection and measurements.
- `docs/stage3-activity-log-acceptance.md`: Activity Log privacy, history and lifecycle.
- `docs/stage3-workspace-layout-acceptance.md`: accepted final layout, source-lifetime limitation and milestone verification.
- `docs/dependencies.md`: versions/licenses.
- `docs/docking-acceptance.md`: passed docking gate, supplemental checklist, and limitations.
- `README.md`: setup/run commands; `samples/synthetic-only/`: synthetic fixtures.
