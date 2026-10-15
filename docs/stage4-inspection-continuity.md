# Stage 4: inspection continuity while editing

The user explicitly granted native Windows Stage 4 acceptance on 2026-10-15,
including inspection continuity and the pending-state styling fix, and authorized
final verification and one local milestone commit. No push, tag or release is authorized.

## Root cause and event path

The blank tools were a state/publication and view-lifecycle problem, not simply
slow debouncing. The original path was:

1. AvaloniaEdit `TextChanged` called `MessageDocument.ReplaceWorkingText`.
2. `WorkingChanged` published a draft, set `message` and `inspection` to null,
   cleared current diagnostics and raised `ActiveContent`.
3. `DocumentManager` forwarded the notification. `InspectionToolViewModel` notified
   its `Document` property even though document identity had not changed.
4. `InspectionToolHost.Refresh` unconditionally cleared `Content`. With both current
   parse/inspection null, it installed "No inspectable active message."
5. After debouncing/parsing, another notification created new `InspectionPanes`.
   The embedded viewer likewise substituted an empty projection and replaced its
   panes. Repeated input restarted the cycle, losing controls/scroll state.

The shared view models themselves were not recreated. Their hosts rebuilt the
interface on both ends of each parse; the 180 ms delay prolonged the blank state.

## State and safety contract

`DocumentInspectionState` separates these states. `ActiveInspection` continues to
mean current coordinates and never aliases historical presentation data.

| State | Tool presentation | Structural actions |
| --- | --- | --- |
| Current | Core projection for current text, including tolerant/provisional structure | Enabled where structure permits |
| Pending | Previous parse, explicitly labeled updating/previous parse | Blocked by model and input guards |
| Stale | Previous parse, explicitly labeled failed/previous parse | Blocked by model and input guards |
| Empty | Empty-working-message status and empty rows in existing controls | Disabled |
| Unavailable | No usable projection, such as genuine message loading | Disabled |

`InspectionSnapshot` supplies the current projection or one previous bounded
materialized projection. An edit marks the previous `MessageInspection` non-current:
its resolve/selection/field-navigation methods refuse stale operations. The editor
independently gates hover, reverse navigation and range highlighting. Pending/stale
values are labeled visual history and are never mapped onto modified raw text.
`MaterializedMessage`/`ActiveInspection` stay null until a current parse is ready;
syntax and structural range colors pause while current coordinates are unavailable.

Successful tolerant Core parses replace historical data even when HL7 is malformed;
current structural inspection does not imply profile validity. A limit/encoding
failure retains the draft and labeled prior projection. Empty working text has an
immediate distinct state. Only the latest revision publishes. Navigation clears
historical state before loading another message; obsolete work cannot retarget it.

Only one prior projection of the active bounded editable message is retained. It is
released after successful parse publication, navigation or disposal. This bounded model
overhead is separate from the existing edit-character/history budget. There is no
new inactive-message cache, model history or materialization of source-backed data.

## Stable controls and parsing decision

Dock and embedded hosts retain their `InspectionPanes` while editing. A fixed-height
status identifies current/pending/stale/empty data without resizing the interface.
Rows remain while pending; new projections bind into the same Navigator, TreeView,
value controls, splitters and scroll hosts.

Native video review identified a remaining visual flicker: setting the Navigator
and Inspector `IsEnabled` to false during Pending/Stale activated Fluent disabled
styles on their descendants, dimming selected rows and values until parsing finished.
These controls now remain enabled with their existing brushes and opacity. The
fixed-height status label identifies previous data. Tunnel handlers reject stale
row pointer/double-click and structural keyboard input before selection/expansion;
selection-change handlers also restore the snapshot selection for programmatic
changes. Lookup and value-navigation handlers check currency before accessing
coordinates. Scrollbars and wheel scrolling remain available. No debounce,
editor/undo, parser, dependency or source-backed limit changes were needed.

Workspace restores selection by segment occurrence/canonical path, never old offsets.
Deleted identities are dropped. Desktop restores realized expanded branches by path
and both scroll offsets where the new extent permits. Field-number input and pane
geometry remain. Explicit navigation reveals its branch; framework auto-scroll on
rebinding is disabled to prevent viewport jumps. Unrealized off-screen expansion
state and changed/deleted structure are best effort.

Immediate UI-thread parsing would add latency up to the existing editor/structure
budgets; full parsing after every key adds allocations/contention. The existing
180 ms debounce, background Core parser, revision checks and scan/work gates remain.
Explicitly stale presentation provides continuity without promising fresh offsets.
No incremental grammar, parser/editor dependency, source ownership change or new
Stage 4 feature is needed.

The undo-group crash fix remains intact: native retention is configured before
input; this fix adds no stack clear or editor text replacement. Workspace still
owns dirty state/history/Edit Mode. Saving, byte preservation, large-message limits,
Dock placement and Activity Log privacy remain unchanged.

## Files changed

- Workspace: `DocumentInspectionState.cs`, `MessageDocument.Editing.cs`,
  `MessageDocument.cs`, `MessageInspection.cs`.
- Desktop: `InspectionToolHost.cs`, `InspectionPanes.cs`, `MessageInspectionView.cs`.
- Tests: `EditingInspectionStateTests.cs`, `EditingInspectionViewTests.cs`, and
  additional stable-pending assertions in `RawEditingCrashTests.cs`.
- Documentation: this report, `PROJECT_CONTEXT.md`, `architecture.md`, ADR 0004,
  and `stage4-editable-document-acceptance.md`.

## Regression coverage

Four Workspace tests verify pending/current/stale/empty states, canonical selection,
no stale resolution/selection events, tolerant malformed structure, limit-failure
recovery, rapid edits and pending navigation. Two fully awaited UI tests exercise
actual editor events with production Dock tools and embedded panes: stable controls
and scroll hosts, no inappropriate empty text, paused actions, fresh values/offsets,
retained viewport/expansion/input state, malformed drafts, empty content, undo/redo
and navigation. The isolated platform-keyboard crash regression additionally checks
stable labeled panes during typing, Backspace/Delete, replacement, paste and keyboard
undo/redo. Existing tests remain intact.

Two additional UI regression cases edit through AvaloniaEdit into Pending and Stale
states. They compare realized Navigator/Inspector selected-row brushes and opacity,
effective enabled state, displayed values and status-label height. They exercise
mouse clicks/tree double-clicks, routed navigation keys, programmatic selection and
field lookup, asserting unchanged selection/expansion, no navigation events and no
raw caret movement. Actions resume after the current parse is restored.

## Native Windows retest

Run Release with synthetic fixtures only:

```powershell
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

1. Open an ordinary message with multiple PID components/repetitions and several
   segments. Expand a nested value, scroll both tools and enable Edit Mode. Type
   continuously and slowly with pauses: no "No inspectable active message" flashes
   or replacement of the surrounding controls should occur.
2. While typing, verify the fixed-height previous-parse/updating label and stable
   teal row selection/value presentation, without dimming or layout shifts. Old
   row clicks/double-clicks, keyboard navigation, field lookup and raw hover/double-click
   must not use old offsets. Controls should retain their normal enabled appearance.
   After refresh, verify the current-text label, new values/paths, highlighting,
   navigation, viewport and expanded branches.
3. Exercise Backspace/Delete, replacement, Ctrl+V, rapid input and toolbar/keyboard
   undo/redo. Field-number input and docking geometry should remain stable; no crash.
4. Temporarily break MSH delimiters/segment identifiers: show tolerant/provisional
   structure when available. Exceed a line budget: retain labeled previous data
   with actions disabled. Undo should restore current inspection.
5. Delete all working text: show the distinct empty state, then Undo/type again.
   Navigate another message while parsing and switch documents: old data must not
   become the new message's inspection; Edit Mode/history remain isolated.
6. Recheck a large source-backed attachment and Full Source Viewer. Edit Mode keeps
   it read-only with existing limits/explanation. Edit a small adjacent message and
   repeat the stability/navigation checks.

The user subsequently granted native Windows acceptance for Stage 4, including
the raw-editing crash and inspection-flicker fixes.

## Verification (2026-10-15)

- Locked restore passed.
- Release build passed with zero warnings and zero errors.
- Complete suite: 527 passed, zero failed/skipped (211 Core, 230 Workspace, 86 Desktop).
- Workspace stress with `HL7WORKBENCH_STRESS=1`: 232 passed, zero failed/skipped,
  including the 256/512 MiB source-backed cases.
- `git diff --check` passed. Native acceptance is explicitly granted; final
  verification and one local milestone commit are authorized. No push, tag or release.
