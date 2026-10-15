# Native RAW tooltip discovery and dismissal

2026-10-15. The user reported a tooltip returning after double-click with the
pointer stationary in the **normal small-message main RAW editor**. Previous
621-test verification did not cover that path's late hover lifecycle. Stage 5A
and its Script Editor remain accepted. The user subsequently confirmed reliable
dismissal but reported difficult hover activation with the shared pixel guard.
The user has confirmed native Windows acceptance of the current discovery and
dismissal correction and authorized one Stage 5A milestone commit and normal push
to origin/main. No tag, release or history rewrite is authorized.

## Build identity and launch

No `HL7Workbench.Desktop` process was running during the initial check, so its
previous launch path could not be established. At that time, the local Release
Desktop DLL and `artifacts/stage5a-publish` DLL were byte-identical (SHA-256
`A480BF2F68DCE04B528F535BF746F6E68CF7C012EEB6786C5D9249ABEB08935A`). Both contained
the previous presentation-only guard. An older copy elsewhere cannot be ruled out,
but stale local publish output is not needed to explain the confirmed normal-view
defect. The corrected build has a different hash.

The startup project is `src/HL7Workbench.Desktop/HL7Workbench.Desktop.csproj`,
configuration **Release**, target **net10.0**. ScriptHost is a child worker, not
the application startup project. No repository launch profile overrides this.

After saving drafts and closing any running app, launch the working-tree build:

```powershell
Set-Location C:\Dev\HL7Workbench
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
if ($LASTEXITCODE -ne 0) { throw 'Restore failed' }
dotnet build HL7Workbench.sln -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
& 'C:\Dev\HL7Workbench\src\HL7Workbench.Desktop\bin\Release\net10.0\HL7Workbench.Desktop.exe'
```

Alternatively, after the successful build, use:

```powershell
dotnet run --project C:\Dev\HL7Workbench\src\HL7Workbench.Desktop\HL7Workbench.Desktop.csproj -c Release --no-build --no-restore
```

Check the actual executable path with:

```powershell
Get-Process HL7Workbench.Desktop | Select-Object Id, Path, StartTime
```

A normal build updates `bin/Release/net10.0`, **not** an existing publish folder
or installed/shortcut copy. To refresh the folder explicitly:

```powershell
dotnet publish src/HL7Workbench.Desktop/HL7Workbench.Desktop.csproj -c Release --no-build --no-restore -o artifacts/stage5a-publish
```

Launching that folder uses `C:\Dev\HL7Workbench\artifacts\stage5a-publish\HL7Workbench.Desktop.exe`.
Rebuilding/republishing does not replace code already loaded in a running process.

## Complete field-tooltip lifecycle

| Path | Open path and former dismissal behavior |
| --- | --- |
| Normal/materialized main RAW | `MessageInspectionView.RawPointerHover` resolves a Core-derived structural node and explicitly opens TextView's tooltip. Press only closed it; no dismissal latch existed. |
| Abbreviated/virtualized large-message RAW | `PresentationView.Setup` resolves mapped source offsets or display-only omission guidance. The earlier latch used TextView-relative coordinates and reset on reattachment. |
| Full Source Viewer | `FullRawInspectorView` resolves the visible window's message-local source offset. Press only closed the tooltip; no dismissal latch existed. |

The initial implementations used AvaloniaEdit's hover notifications. Its installed source revision
[PointerHoverLogic](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Rendering/PointerHoverLogic.cs)
starts/restarts a 400 ms Background dispatcher timer on pointer enter or sufficient
movement and retains that event's position/button state. It does not subscribe to
pointer presses. An enter notification can therefore schedule another hover even
without physical mouse movement, and a retained event can still describe released
buttons. Our correction adds no timer or delay.

Double-click handlers run on TextArea's tunnel route, select the deepest structural
node and mark the event handled. Selection may scroll/redraw the editor and update
the docked inspector; it does not replace the normal RAW view. Layout/popup changes
can cause hit testing/enter notifications. Repeated hover notifications formerly
reopened the unguarded normal/full-source tooltip. In the presentation path, a
same-window-position move after a layout shift changed the TextView-relative
coordinate and prematurely cleared its guard. The new tests reproduce these
event orders, but no native pointer trace was captured from the user's earlier run.

Avalonia's installed
[ToolTipService](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Avalonia.Controls/ToolTipService.cs)
can schedule automatic tooltip opens from raw move events. `ServiceEnabled=false`
is retained on these TextViews. Initially AvaloniaEdit supplied timing; the current
controller owns field scheduling and the open request. The separate
[ToolTip opening event](https://github.com/AvaloniaUI/Avalonia/blob/2d79ba55fae1b1e71c69f93a668080c9029e6a31/src/Avalonia.Controls/ToolTip.cs)
allows a pending/independent open request to be vetoed while dismissed.

Scroll, stale inspection and detach close tips and clear their content. Leaving
the actual displayed target also clears it; movement inside the same field does
not. Detach releases current pointer eligibility. Keyboard/caret/inspection
updates do not schedule tooltips. Copy-limit/export-failure notifications use editor
tooltips separately; their behavior and ordinary toolbar tooltips are unchanged.

## Confirmed discovery regression and current correction

The former shared lifecycle required an AvaloniaEdit retained event's screen pixel
to equal the latest move's pixel exactly. AvaloniaEdit tolerates movement within a
two-pixel box without updating that retained event. A one-pixel move could therefore
reject the eventual hover despite staying inside the same value. Beyond that box,
AvaloniaEdit restarted its timer even when moving between characters of one field.
Its hover-stop notifications also closed an already-visible tooltip. These policies
explain the difficult activation; they were not a requirement to hover glyph ink.

The current controller separates three responsibilities:

- **Acquisition:** hit the full rendered text row/character cell with floor-based
  coordinates, then resolve the existing Core-backed structural ranges. Spaces
  inside real values count; identifiers, unsupported/opaque regions, missing values,
  field delimiters except MSH-1, and unrelated whitespace do not acquire a field.
  The shared hit test explicitly rejects blank space after each rendered row,
  including wrapped rows. No semantic field names or values are invented.
- **Scheduling:** use the existing normal 400 ms interval, once per structural
  `(segment, occurrence, field)` identity. Movement within that field preserves
  the deadline and any open popup; the latest component/repetition path supplies
  the displayed metadata. Crossing fields clears old content immediately and
  starts the new field's delay. A generation check rejects canceled/late callbacks;
  the target is revalidated before opening. AvaloniaEdit's independent hover and
  hover-stop events no longer open or close field tooltips. No arbitrary extra
  grace period, periodic polling, LSP or parser dependency is introduced.
- **Dismissal:** tunnel presses close/cancel before inspection affects layout.
  Actual button state is retained separately from potentially older move events.
  Click, scroll, source replacement and detach suppression survives stationary
  pointer/layout/enter/exit notifications. Subsequent unpressed physical movement
  rearms acquisition. Layout revalidation can close obsolete content but never
  schedules a dismissed popup. The Avalonia opening event remains a final veto.

Screen coordinates are transient view state only. They are neither persisted nor
logged. Event subscriptions belong to the editor/control tree, not a document or
global service. Each view owns at most one one-shot pending callback and one target;
detach cancels it and clears metadata. Acquisition uses cached structural projections,
without reparsing. The existing Core navigation/mapping code is unchanged.
No Core, Workspace, worker, protocol, script, output transaction or
Stage 5B functionality changes.

Changed production files: `FieldToolTipLifecycle.cs`, `MessageInspectionView.cs`,
`PresentationView.cs`, `FullRawInspectorView.cs`, all under the Desktop Viewer folder.
`FileDocumentView.cs` and `MessageViewer.cs` pass an internal scheduling seam through
to those views so tests use a deterministic UI-thread clock. Public constructors
retain their existing signatures; production uses DispatcherTimer.RunOnce.
Tests are in `FieldToolTipDiscoveryTests.cs`, `FieldToolTipLifecycleTests.cs` and
`Stage3ViewTests.cs`, with the test-only `ManualHoverScheduler`. Existing assertions
are retained; clock advancement replaces direct synthetic hover as the open trigger.

## Verification

Six synthetic routed-input cases cover all three real views with/without a layout
shift: hover, double-click, released buttons, repeated enter/exit/hover at the same
physical position, same-position move, independent open veto, another field,
obsolete callback, move away/return, selection preservation and detached cleanup.
All six failed before the initial dismissal correction and pass afterward. One controller
case covers movement while held, release, reattachment and subsequent legitimate
unpressed movement. No waits were added. The earlier two delayed-hover regression
cases and three existing timed RAW-tooltip cases also pass. Eight additional cases
protect natural movement: all three views with standard/custom delimiters, one-pixel
and multi-character movement before a single 400 ms deadline, full character-cell
edges and value spaces, component path changes without a timer reset, field switches,
obsolete callbacks, unrelated whitespace, dismissal/layout/return, and wrapped/final
row bounds. Tests advance the scheduler without sleeps and explicitly check that
399 ms does not open while 400 ms does.

| Check | Result |
| --- | --- |
| Locked restore | Passed |
| Release solution build | Passed, 0 warnings/errors |
| Complete automated suite | 636 passed, 0 failed/skipped |
| Core / Workspace / Desktop / Scripting | 211 / 272 / 125 / 28 |
| Workspace stress (`HL7WORKBENCH_STRESS=1`) | 274 passed, 0 failed/skipped |
| Framework-dependent folder publish | Passed; refreshed `artifacts/stage5a-publish` |
| Published worker smoke | Compile, synthetic transform, finish and clean exit passed |
| Deployment checks | Release/publish DLL hashes match; scripting runtime remains in ScriptHost |
| `git diff --check` and untracked text review | Passed |

The current Release/publish Desktop DLL SHA-256 is
`20355A5E9EC2ADF3C76278C4D2D97F7FAF7556FCDA8AADA51E68DE619AC99F9C`.
No dependencies were added. The pre-commit review covered 31 modified tracked files
and 41 untracked source, test, project/lock, build, documentation and synthetic sample
files. The user subsequently authorized the accepted milestone's commit and push.

## Native acceptance checklist

1. Launch the explicit Release executable above and verify its process path. Open
   only `samples/synthetic-only/stage5a-scripting.hl7` or another synthetic fixture.
2. In normal small-message RAW, move naturally over PID-5. Wiggle slightly and
   slide between letters within the value: the normal hover should appear after
   about 400 ms from entering that field, without pixel-perfect positioning or
   repeated delay resets. Try the upper/lower parts and right half of character
   cells, and spaces inside a synthetic multi-word value.
3. Slide across components/repetitions of PID-5: retain the hover and show the
   accurate latest path. Move to PID-3: old content disappears immediately and the
   new field appears after its normal delay. No value/path from PID-5 may linger.
   Blank space past the row, separators without values, and space below the message
   must not acquire a fabricated field.
4. Double-click the field and leave the mouse still beyond the hover interval:
   the tooltip stays closed and inspection/selection remain correct. Scroll, resize,
   update the inspector/layout, or rerender while stationary: no spontaneous reopen.
   Move away and return to restore normal hover. Single-click/drag/release stays
   dismissed until subsequent unpressed movement.
5. Repeat discovery and dismissal in an abbreviated/wrapped large-message RAW
   view and Full Source Viewer, including horizontally scrolled text. Actual visible
   values use accurate paths; omission markers show only their existing source-view
   guidance. Wrapped-row trailing space must not become a hit on the following field.
6. Confirm normal selection, double-click navigation, editing/undo, copying and
   Script Editor focus/commands still work. No local scripts or drafts are discarded.

Headless regressions do not establish native popup/OS hit-testing behavior. The
user completed native Windows acceptance, including this discovery/dismissal checklist.
