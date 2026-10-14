# Project context

HL7 Workbench is an independent, local-first HL7 v2 development tool. Windows is
the initial desktop platform. No runtime telemetry, cloud features, or message logging.
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
malformed messages, docking, and persistence and shutdown. Stage 2 has not started
and requires separate explicit authorization.
See `docs/viewer-stage1-acceptance.md` for architecture, limits, and exact steps.
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

## Future viewer requirements (not implemented)

- Raw HL7 viewer above a lower-left Segment Navigator and lower-right Field
  Inspector, with adjustable splitters.
- Combined multi-message files use a fixed toolbar at the bottom of the raw-message
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

Core parses one bounded message at a time. Byte input preserves encoding/BOM;
MSH-18 does not select encoding. Messages are mutable/not thread-safe; reacquire
model snapshots after edits. Never use display text as a save source.

## Important documentation

- `HL7_Workbench_Specification.md`: product vision; milestone amendments govern scope.
- `docs/architecture.md`, `docs/decisions/`: architecture and accepted decisions.
- `docs/parser-api.md`, `docs/hl7-paths.md`: preservation, limits, paths, and edit rules.
- `docs/dependencies.md`: versions/licenses.
- `docs/docking-acceptance.md`: passed docking gate, supplemental checklist, and limitations.
- `README.md`: setup/run commands; `samples/synthetic-only/`: synthetic fixtures.
