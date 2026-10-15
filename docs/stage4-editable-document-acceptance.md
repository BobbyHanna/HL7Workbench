# Stage 4 editable document foundation

Stage 3 baseline: `3045bafa0790806817e0bb83932b4b92b8ab0181`.
The user explicitly granted native Windows acceptance on 2026-10-15 and authorized
final verification and one local milestone commit:
`feat: complete Stage 4 editable HL7 document foundation`.
No amend, push, tag, publication or release is authorized.

The native raw-editing crash reported on 2026-10-15 has a focused fix and routed-input
regression. See [exception evidence and native retest](stage4-raw-editing-crash.md).
The user accepted the crash fix; the earlier automated run alone did not certify editing.

The editing-continuity fix keeps Dock/embedded inspection panes mounted while parsing,
labels prior snapshots and blocks stale structural actions without disabled-control
styling. The user accepted the inspection-flicker fixes. See
[state contract and native retest](stage4-inspection-continuity.md). Current coordinate
APIs remain separate from the retained presentation; no stale ranges highlight raw text.

See [ADR 0004](decisions/0004-editable-document-foundation.md) for the assessment,
alternatives, limits, ownership, original/working/saved distinction and deferred work.

## Implemented

- Per-document Edit Mode checkbox beside Undo/Redo in the raw pane; read-only default.
- Settings → Start in Edit Mode, persisted separately in `editor-preferences.json`
  beside application layout preferences. Changes apply only to newly opened tabs.
- Bounded direct raw editing, document-owned delta undo/redo, modified-tab `*`,
  navigation retention, Core refresh after a 180 ms debounce and explicitly labeled
  previous inspection during refresh with stale coordinate actions disabled.
  Ctrl+Z/Ctrl+Y/Ctrl+Shift+Z use document history.
- File → Save / Save As and Ctrl+S / Ctrl+Shift+S. Save As selects a local destination;
  subsequent Save targets it. Edits remain in memory if publication fails.
- Save/Discard/Cancel for dirty tab/application closure. Escape/X dismissal cancels.
- Streaming byte-preserving save, unchanged-file copying including unsupported/
  undecodable encodings, and safe temporary-file publication/replacement.
- Source-backed messages retain abbreviation, bounded viewing and source ownership.
  A visible explanation describes their read-only state. Original-byte exports are
  disabled for edited messages because working offsets no longer describe originals.

## Automated verification

`EditableDocumentTests` covers preference persistence/isolation; exact dirty/save
points; undo/redo branches; navigation; delayed parsing and fresh Core coordinates;
UTF-8/UTF-16 LE/BE, Unicode surrogate pairs, custom separators, mixed CR/LF/CRLF,
BOM, MLLP and batch-wrapper retention; repeated saves and Save As; missing/locked
destinations, cancellation, draft limits and strict encoding; boundary refusals;
unchanged UTF-32/undecodable copying; close decisions/stale consent; pending read/save
coordination and disposal; and a 64 MiB streamed save with an allocation budget.

`EditableViewerTests` uses actual headless AvaloniaEdit input, edit toggles,
structural selection, keyboard undo/redo, view reconstruction, multi-message UI
navigation and real Dock close events/title updates. Existing tests remain intact.
These tests do not certify native clipboard, dialogs, fonts or window-manager behavior.

Initial verification on 2026-10-15: locked restore passed; Release build passed with
zero warnings/errors; all 518 tests passed (211 Core, 226 Workspace, 81 Desktop).
All 228 Workspace stress tests passed with `HL7WORKBENCH_STRESS=1`, including
256/512 MiB cases. The 40 new editing tests include byte-index verification of
UTF-16 framing collisions, privacy, bounded history and streaming save. All existing
tests remain intact. `git diff --check` passed; native acceptance was pending then.
After the raw-editing crash fix and awaited editing-test corrections, all 519 tests
passed (211 Core, 226 Workspace, 82 Desktop); all 228 Workspace stress tests passed.
Locked restore, zero-warning/error Release build and `git diff --check` passed again.
After inspection-continuity changes, all 525 tests and all 232 Workspace stress tests
passed, including the 256/512 MiB cases. Locked restore, the zero-warning/error Release
build and `git diff --check` passed. The final pending-state styling fix brought the
suite to 527 passing tests (211 Core, 230 Workspace, 86 Desktop); all 232 Workspace
stress tests passed. The user subsequently granted native Windows acceptance.
Run from repository root with all telemetry opt-outs set to `1`:

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
$env:HL7WORKBENCH_STRESS = '1'
dotnet test tests/HL7Workbench.Workspace.Tests/HL7Workbench.Workspace.Tests.csproj -c Release --no-build --no-restore
git diff --check
```

Stress mode includes the existing 256/512 MiB synthetic source-inspection cases.
Unset `HL7WORKBENCH_STRESS` afterward for ordinary test runs.

## Native Windows acceptance checklist

The user explicitly granted native Windows acceptance on 2026-10-15, including
the raw-editing crash and inspection-flicker fixes. This checklist is retained for
future regression testing.

Use synthetic files only. Start the Release application:

```powershell
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

1. **Default and preference.** Open two small files. Verify both start read-only.
   Enable Edit Mode on only one. Switch tabs and verify each state. Turn Settings →
   Start in Edit Mode on, open a third file and verify only that new tab starts in
   Edit Mode. Restart and verify the preference persists; restore it to Off.
2. **Typing and inspection.** Edit a PID value's length, components and repetitions.
   Verify caret, selections and scrolling remain usable. During refresh, tools retain
   labeled previous data with stable selection colors, values, expanded rows and
   scroll positions; stale structural actions are blocked without disabled styling.
   After refresh, Navigator/Field Inspector/hover/double-click use the new offsets.
   Test Enter, Backspace, selection replacement, Cut/Paste and Unicode emoji. Verify
   untouched mixed endings remain exact after saving. Confirm no typing stalls.
3. **History and switching.** Undo/redo using buttons and keyboard. Turn Edit Mode
   off and verify edited text remains while further writes are refused. Navigate
   between two messages, switch tabs and reconstruct views via layout restore/reset;
   verify working text/history remain isolated. Verify dirty `*` follows the save point.
4. **Save and Save As.** Save a copy of UTF-8/UTF-16 LE/BE fixtures with BOM, custom
   delimiters, mixed endings, MLLP and multiple messages. Compare unchanged ranges
   with the original bytes. Reopen saved output and inspect it. Save As again and
   verify later Ctrl+S uses the new destination. Undo across a save and save again.
5. **Failure/cancellation.** Cancel Save As; lock or deny write access to a destination
   and try saving. Verify the destination remains intact, dirty text/history stay,
   a useful status appears and successful retry works. Do not use original PHI files.
6. **Dirty closure.** Close active and inactive dirty tabs using Cancel, X/Escape,
   Discard and Save. Cancel the Save As picker and simulate save failure from a close
   prompt. Repeat while closing the main window with several dirty tabs and floating
   tools. Cancel leaves the workspace usable; Discard requires explicit choice.
7. **Message boundaries.** In the first message of a batch, remove its final ending,
   insert another MSH, or type framing/batch controls. Save must refuse publication
   and retain the draft. Undo, then verify ordinary edits save and adjacent messages
   remain intact. Temporarily exceed a line limit and verify draft repair via Undo.
8. **Large sources and optional viewer.** Open synthetic 100/256/512 MiB attachment
   fixtures. Enable Edit Mode: large message stays read-only with explanation and
   the same abbreviations/source windows. Edit a small adjacent message and save;
   verify UI responsiveness and unchanged attachment bytes. Full Source Viewer
   retains existing tab-switch context, closes on same-file navigation/content edit,
   and releases content on tool/document close. Edited messages explain why original
   source inspection/export requires reopening saved output.
9. **Workspace/privacy regression.** Drag, float, dock, hide/reopen tools and restore
   layout. Verify placement metadata contains no source/path/editor content. Verify
   Activity Log save/open/failure records contain no filenames, paths or raw values.

## Known limits

Source-backed/oversize editing, boundary restructuring, working-source export
mappings, disk-backed history, undo grouping, external-change conflict reconciliation,
autosave/crash recovery, SQL and scripts are deferred. The 32 MiB retained-character
budget plus 4,096 history entries is per document; buffers/models/encoding/editor
overhead and simultaneously open documents add memory. Reaching a budget refuses
new edits; history is not silently evicted. Save/reopen provides a fresh baseline.
Filesystem replacement must be supported; there is no non-atomic fallback.

## Final milestone verification (2026-10-15)

After explicit native Windows acceptance, the required checks were rerun against
the accepted Stage 4 tree:

- Locked restore: passed.
- Full automated suite: 527 passed (211 Core, 230 Workspace, 86 Desktop), zero
  failures or skips.
- Workspace stress with `HL7WORKBENCH_STRESS=1`: 232 passed, zero failures or skips,
  including the 256/512 MiB source-backed cases.
- Release build: zero warnings and zero errors.
- `git diff --check`: passed.

The local milestone commit is authorized with the requested message. Amend, push,
tag, publication and release remain unauthorized.

## Milestone file manifest

The accepted milestone contains these 33 implementation, test and documentation
files. Generated output, local settings, temporary data and private messages are
excluded.

```text
docs/PROJECT_CONTEXT.md
docs/architecture.md
docs/decisions/0004-editable-document-foundation.md
docs/stage4-editable-document-acceptance.md
docs/stage4-inspection-continuity.md
docs/stage4-raw-editing-crash.md
src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/MainWindow.Editing.cs
src/HL7Workbench.Desktop/MainWindow.axaml
src/HL7Workbench.Desktop/MainWindow.axaml.cs
src/HL7Workbench.Desktop/Viewer/FileDocumentView.cs
src/HL7Workbench.Desktop/Viewer/InspectionPanes.cs
src/HL7Workbench.Desktop/Viewer/InspectionToolHost.cs
src/HL7Workbench.Desktop/Viewer/MessageInspectionView.cs
src/HL7Workbench.Desktop/Viewer/MessageViewer.cs
src/HL7Workbench.Desktop/Viewer/ViewerRequests.cs
src/HL7Workbench.Workspace/ActivityLog.cs
src/HL7Workbench.Workspace/DocumentInspectionState.cs
src/HL7Workbench.Workspace/DocumentManager.cs
src/HL7Workbench.Workspace/EditableMessageBuffer.cs
src/HL7Workbench.Workspace/EditorPreferences.cs
src/HL7Workbench.Workspace/MessageDocument.Editing.cs
src/HL7Workbench.Workspace/MessageDocument.cs
src/HL7Workbench.Workspace/MessageFileReader.cs
src/HL7Workbench.Workspace/MessageInspection.cs
src/HL7Workbench.Workspace/RawInspectorSession.cs
src/HL7Workbench.Workspace/UnsavedChanges.cs
tests/HL7Workbench.Desktop.Tests/EditableViewerTests.cs
tests/HL7Workbench.Desktop.Tests/EditingInspectionViewTests.cs
tests/HL7Workbench.Desktop.Tests/RawEditingCrashTests.cs
tests/HL7Workbench.Workspace.Tests/EditableDocumentTests.cs
tests/HL7Workbench.Workspace.Tests/EditingInspectionStateTests.cs
```
