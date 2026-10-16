# Stage 5B desktop finishing acceptance

Final native Windows acceptance passed on 2026-10-16, as explicitly confirmed by the
user, including safe replacement and compact confirmations. The user authorized one
Stage 5B milestone commit and normal push to origin/main. No tag, release, force-push
or history rewriting is authorized. Earlier pending checkpoints below are historical;
the native checks are user-confirmed, not inferred from headless tests.
See [preferences and UI contracts](desktop-preferences.md).

## Final Save File and confirmation correction

The user completed native functional testing of unified scripting, Script Library,
reports, ribbon, Preferences and document workflows. The final
[save/export and compact confirmation retest](save-and-export-safety.md#native-windows-retest)
also passed native Windows acceptance on 2026-10-16.
That document inventories every picker, the installed Windows provider's actual consent
behavior, shared transactional publication, file identity/hash checks and platform limits.

Existing files may be replaced only after explicit user authorization, using safe
replacement semantics. Windows' native overwrite prompt is used without a redundant
application prompt; unverified providers require a compact application confirmation.
HL7 ordinary/generated documents share Save As; ordinary associated Save still needs no
picker. CSV and exact source exports use the same safe writer. Source-export exclusions,
document identity/undo, report attribution/escaping/selection and scripting APIs remain.
Unsaved document/script and Delete Script dialogs use wrapped content-sized layouts
around 420 pixels wide, with existing decisions and safe dismissal behavior.

The new tests mock picker results to verify application policy, not native Windows
prompt behavior. The native retest must confirm replacement/decline/cancel, source bytes,
generated output, compact long-name dialogs, keyboard dismissal and DPI/theme behavior.

Verification on 2026-10-15: locked restore, zero-warning/error Release build, 80 focused
cases and all 802 automated cases passed, zero failed/skipped (33 added). Folder publish
was refreshed; full published-worker smoke, Release hashes and dependency isolation
passed in Windows PowerShell 5.1. The [correction report](save-and-export-safety.md)
records the exact 28 changed files, test commands and native/provider limitations.
Workspace stress was rerun for the shared file writer and passed all 333 cases.
Diff/whitespace/conflict checks passed for all 63 modified/untracked files. Main remains
at 6dfa9f4, with 28 modified tracked / 35 untracked and nothing staged. The investigation
probe was removed; the reviewed synthetic fixture and prior Stage 5B work are retained.

## Final menu cleanup

The user confirmed the corrected ribbon looks substantially better and switches tabs
correctly with one click. The final permanent menu is **File | Settings | View**.
Ribbon tabs remain **Home | Scripts | HL7 Tools | TCP/MLLP**. This cleanup changes only
menu entries and command exposure, retaining existing shared implementations, shortcuts,
enabled states, ribbon visuals/input routing and persistence. Nothing is staged.

Complete inventory of the removed dropdowns and their retained access:

| Former menu command | Current access |
| --- | --- |
| Scripts → New Script | Scripts ribbon → Script Library → New; editor New |
| Scripts → Open Script Editor | Scripts ribbon → Script Library → Open Script Editor (added); View → Script Editor |
| Scripts → Save Script | Scripts ribbon → Script Library → Save; editor Save Script / Ctrl+S |
| Scripts → Duplicate Script | Scripts ribbon → Script Library → Duplicate; editor ⋯ |
| Scripts → Rename Script | Scripts ribbon → Script Library → Rename; editor name / ⋯ |
| Scripts → Delete Script | Scripts ribbon → Script Library → Delete; editor ⋯ |
| Scripts → Run Script | Scripts ribbon → Execution → Run; editor Run |
| Scripts → Cancel Script | Scripts ribbon → Execution → Cancel; editor Cancel |
| Scripts → Review Output | Scripts ribbon → Results → Review Output; editor ⋯ |
| Scripts → Reports / Export CSV | Scripts ribbon → Results → Reports (opens existing export dialog); editor ⋯ |
| Window → Save Layout | Home ribbon → Workspace → Save Layout |
| Window → Restore Layout | Home ribbon → Workspace → Restore Layout |
| Window → Reset Layout / Recover Windows | Home ribbon → Workspace → Reset Layout / Recover Windows (added) |

Apply to: Current message / Whole document remains in Scripts → Execution and the
editor. The removed Window dropdown contained exactly the three layout actions above;
all six tool visibility controls remain in View: Message Browser, Activity Log, Segment
Navigator, Field Inspector, Script Editor and Full Source Viewer. The shared command
implementations and keyboard handlers are retained. ScriptCommandMenu remains as the
former-menu inventory used to check complete command identity coverage in tests.

Verification on 2026-10-15 passed: locked restore, Release build with zero warnings/errors,
ten focused menu/ribbon/shortcut checks, and all 769 automated cases (zero failed/skipped).
Focused coverage includes the three-menu shell, all former Scripts command identities,
command gates/scope synchronization, actual collapsed-ribbon Open Editor and all three
layout actions, script Ctrl+S isolation, first-click tab switching, dismissal, preferences
and both theme/expansion modes. Full-suite docking/floating coverage also passed.
The existing layout contract restores optional Script Editor placement when reopened;
the integration check verifies the retained model after that reopen. Large-file stress
and publish/worker verification were not rerun for this menu-only change; earlier results
below are historical. Diff checks passed.

Native menu acceptance:

1. Launch the Release build using the commands below. Verify exactly File, Settings,
   View in the permanent menu; no Scripts/Window dropdowns. Verify all four ribbon tabs.
2. Starting collapsed, hide the editor, then use Scripts ribbon → Open Script Editor; it must
   reveal/focus the same retained docked/floating model. New must also reveal it. Reach
   every command in the inventory, using horizontal scrolling at narrow widths. Use
   Escape/outside dismissal and single-click tab switching; Dock splitters must stay put.
3. From collapsed Home, Save Layout, move/hide/float tools, Restore Layout, then Reset
   Layout / Recover Windows. Verify the existing placement/recovery behavior. Reopen the
   Script Editor after restoration; its draft is retained. Check all six View controls.
4. Check script gates with a bundled example, saved user draft, no source, active run,
   and retained reports. Match ribbon/editor availability and both scope selectors. Check
   Ctrl+S in code saves only the script; File Save / Ctrl+S in the document saves HL7,
   and Ctrl+Shift+S retains Save As. Repeat essential editor controls docked and floating.
5. Recheck pin/unpin, Preferences synchronization and restart behavior. Ribbon stays
   collapsed by default unless the saved preference specifies expansion. Accepted theme,
   icon/button styling and first-click tab switching must remain unchanged.

Full Stage 5B acceptance below and separate explicit Git authorization remain pending.

Cleanup files: src/HL7Workbench.Desktop/MainWindow.axaml, MainWindow.Shell.cs,
Viewer/UtilityRibbon.cs; tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs,
RibbonCorrectionTests.cs; README.md; docs/PROJECT_CONTEXT.md, desktop-preferences.md,
script-api.md, stage5b-batch-scripting-acceptance.md and this checklist. Existing Stage 5B
files remain uncommitted. Main stays
at 6dfa9f4, with 23 modified tracked / 28 untracked files and nothing staged.

## Earlier ribbon defect correction

Initial native Windows ribbon acceptance failed: theme background/contrast, command
hierarchy and two-click tab switching were defects. The correction uses dynamic Fluent
resources (no white fallback), legible group headings/dividers, vector glyphs, consistent
30/64-pixel command controls and an accent Run tile. Scripts is grouped as Script Library
(New/Save/Duplicate/Rename/Delete, now also Open Script Editor), Execution (scope/Run/Cancel),
and Results (Review/Reports).
Home/HL7 Tools share the presentation; Sender/Listener remain disabled Planned controls.

The light-dismiss overlay now passes input only to the tab/pin strip. A different tab
receives its first pointer press without dismissal; a stable popup content host changes
commands in place. Clicking the selected tab toggles temporary visibility. Outside clicks
remain consumed/dismissed, Escape dismisses, and pinned switching stays expanded. No
timers, double-click handling, preferences/runtime changes or Dock resize workaround.

Native retest explicitly requires:

1. In **dark and light** themes, inspect **pinned and temporary** ribbon surfaces. Verify
   background/foreground, group headings, dividers and disabled controls are readable.
   Check hover, held press, selected tabs and keyboard focus; no white surface in dark mode.
2. Starting collapsed, click Home, Scripts, HL7 Tools and TCP/MLLP in sequence with **one
   click per tab**. Content must change immediately, with no intervening dismissal. Reverse
   direction and repeat. Click the selected tab to close/reopen; click outside and Escape
   to dismiss. No double-clicks or waiting should be necessary.
3. Pin via the first click while temporary commands are open. Switch tabs once each:
   ribbon stays expanded. Unpin: current tab remains selected and commands collapse.
   Check Preferences synchronization and restart persistence in both modes.
4. Check compact Script Library / Execution / Results grouping, consistent icon/button
   sizes, prominent Run, readable Cancel/Results disabled states and no wasted vertical
   area. Home/HL7 Tools must use the same system. At narrow widths use horizontal scrolling.
5. While switching temporary tabs, watch Dock/editor bounds and splitters: no movement.
   Compare editor/ribbon availability with a bundled example, saved user script, no
   source, an active run and a report-producing run. Existing commands remain authoritative.

Four Skia-rendered previews are retained under ignored artifacts/ribbon-visual/ (dark/light,
pinned/overlay). They support layout/contrast review and do not establish native acceptance.
Six new automated cases cover real pointer transitions, command state, theme inheritance,
live theme changes, heading contrast and compact layout. The previous ribbon test remains.

Correction verification on 2026-10-15 passed: locked restore; zero-warning/error Release
build; seven focused ribbon cases; four isolated Skia render cases; all 768 automated
cases (zero failed/skipped). The four dark/light pinned/overlay previews were visually
inspected. Folder publish was refreshed and full worker smoke/hash/dependency checks
passed in Windows PowerShell 5.1. Diff and all 51 modified/untracked file checks passed.
No staging/commit/push/tag/release; main remains at 6dfa9f4 with 23 modified tracked and
28 untracked files. Temporary investigation/comparison files were removed; previews
remain ignored. Native Windows acceptance is still pending.

Optional rendering runs in its own filtered process because Avalonia caches drawing-backend
typefaces globally. All theme/layout assertions also run in the regular suite:

```powershell
$env:HL7WORKBENCH_RIBBON_RENDER = '1'
& tests/HL7Workbench.Desktop.Tests/bin/Release/net10.0/HL7Workbench.Desktop.Tests.exe -method '*RibbonUsesWindowThemeInBothModesAndRendersCompactReadableGroups' -noLogo
Remove-Item Env:HL7WORKBENCH_RIBBON_RENDER
```

Correction files: src/HL7Workbench.Desktop/Viewer/UtilityRibbon.cs;
tests/HL7Workbench.Desktop.Tests/RibbonCorrectionTests.cs and ShellFinishingTests.cs;
docs/PROJECT_CONTEXT.md, desktop-preferences.md and this checklist. MainWindow.Shell,
shared commands, settings/library persistence and scripting/runtime files are unchanged.

## Earlier finishing checkpoint (2026-10-15, before ribbon correction)

- Locked restore and final Release build passed with zero warnings/errors.
- All 762 automated cases passed, zero failed/skipped; 19 new cases cover this pass.
  Focused editor/polish checks passed 28 cases and settings/library checks passed 29.
  The final suite also covers notification when closing an inactive output document.
- Workspace stress passed 320 cases, zero failed/skipped, including 8,192 stateful
  messages, 4,096-message streaming and 256/512 MiB fixtures.
- Folder publish passed. Full VerifyScriptPublish.ps1 passed under Windows PowerShell
  5.1.26100.9444 and PowerShell 7: one-/multi-message lifecycles, state, CSV, completion,
  Hold/Emit, clean exit, all Release/publish worker hashes and dependency isolation.
- Diff/whitespace/conflict-marker checks passed for all 50 modified/untracked files.
  Main stays at 6dfa9f4; 23 modified tracked / 27 untracked, nothing staged.

One rebuild overlapped stress and retried a locked Windows DLL copy. After stress
completed, the final sequential build passed without warnings and the full suite
passed again. No verification checks were skipped/weakened. Use the comprehensive
commands in the [lifecycle checklist](stage5b-batch-scripting-acceptance.md) to reproduce.

## Native Windows checklist

Use only the reviewed synthetic fixture. From Windows PowerShell:

```powershell
Set-Location C:\Dev\HL7Workbench
New-Item -ItemType Directory -Force test-data/generated
Get-FileHash samples/synthetic-only/stage5b-repeated-patients.hl7 -Algorithm SHA256
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

Expected fixture SHA256: `8ABFC4D2F05E9FBB47D9E6716BFEBCBAF2092BFE6B1EE693FC9AB3C3ADFA8086`.
Recheck it after testing. Use fresh generated filenames. Before every Run reselect
the original source tab; successful execution activates the independent output.

1. **Ribbon.** Default preferences show only tabs beneath the permanent menu. Select
   each tab: Home has existing file/layout/preferences actions, Scripts has library/
   execution/results groups, HL7 Tools has existing inspection tools. Temporary commands
   overlay content without moving Dock splitters or reducing editor height. Click outside
   or Escape to dismiss. Use Left/Right/Home/End, Down and Tab to reach commands. Pin,
   switch tabs, unpin: selection stays predictable. Pin/restart verifies expansion;
   unpin/restart verifies collapse. Check normal/high DPI and narrow main-window scrolling.

2. **Permanent menus and Scripts ribbon.** Check File | Settings | View only; all six View
   tool commands remain. Hide Script Editor; the Scripts ribbon tab remains visible. New
   opens/focuses a Main draft. Open Script Editor reveals the same model. Verify Save,
   Duplicate, Rename, Delete, Run, Cancel, Review Output and Reports states agree with
   editor. Bundled rename/save/delete and idle Cancel are disabled. Without an HL7
   source, Run is disabled. File → Save saves HL7; Ctrl+S in code saves only the script.

3. **Compact docked/floating editor.** Dock, float, resize near 260–320 pixels, hide,
   reopen and restore a saved layout. Exactly two toolbar rows remain, with New,
   Save Script, ⋯, Apply to:, Run/Cancel. Duplicate, Review and reports/export are in ⋯.
   Scroll narrow rows to reach all controls; code remains dominant. Check focus after
   New/Open, font/color/zoom shortcuts, typing, selection and undo.

4. **Inline rename.** Duplicate an example. Click the displayed name, type a name, Enter:
   name confirms without changing ID/code. Save Script/reopen: it persists. Escape cancels.
   A valid name confirms on focus loss; whitespace-only input leaves the prior name with
   feedback. Confirmation leaves unsaved code intact/dirty until explicit Save. Bundled
   names cannot be edited. Selecting a library item must not activate rename.

5. **Delete.** Save a disposable user script. Delete via ribbon/⋯: confirmation
   identifies it; Cancel/dismiss keeps it. With dirty code, Cancel the first prompt;
   then try Discard followed by Cancel in deletion confirmation: draft remains in both
   cases. Try Save before confirming and verify the saved draft. Finally confirm Delete:
   record disappears, simple bundled example is selected, unrelated records remain.
   Bundled/never-saved scripts cannot be deleted. A disposable Needs update record retains
   its Run protection when renamed/duplicated until explicit Save; no automatic adoption.

6. **Dirty transitions.** Type unsaved code/name and switch library items, New, hide/close
   tool and close the app. Save/Discard/Cancel/dismiss behave consistently. Cancel loses
   no code or working HL7 edits. Repeat from a floating editor using ribbon
   actions. Decisions/pickers/runs block conflicting management/execution. Existing source
   undo and save identity remain intact.

7. **Reports.** Open the fixture and run bundled consistent synthetic patients on Whole
   document. Scripts ribbon → Reports (also editor ⋯ → Reports / Export CSV) lists synthetic-summary,
   producing name and Whole document scope. Export to a new file; text is exactly:

   ```csv
   "Patients","Messages"
   "2","3"
   ```

   Confirm replacement of a disposable existing destination, then decline and cancel:
   existing bytes remain on cancellation. Try an unavailable/changed destination and
   retry after fresh selection: no partial replacement or lost reports. Switch,
   rename and delete a disposable producing script: origin retains execution-time name/
   scope; output documents and exported CSV remain. Define two reports via Batch.Report
   to verify individual selection/export. Success replaces results (no reports clears
   them); failure retains them.

8. **Preferences.** Settings → Preferences opens a separate window. General edits are
   drafts until Apply/OK. Apply stays open; Cancel discards only changes since Apply;
   OK saves/closes. Ribbon choice and pin/unpin agree and persist after restart. Enable
   Open documents in Edit Mode by default; existing documents retain their mode. Open a
   new supported file: Edit Mode. Disable/open another: read-only. Recheck eligibility,
   dirty/save protections and the retained Settings checkbox.

9. **HL7 settings.** Preferences → HL7 explains declared MSH-12 versus independent target
   schema and interpretation mode. Schema/target controls are disabled, explicitly pending
   an engine; no fabricated version catalog or conformance/conflict results appear. General
   preferences cannot modify MSH-12/source bytes. Recheck source hash and inspection.

10. **Unified execution/regressions.** Run identical source below first on Current message
    and then Whole document; reports show 1 and 3, output has 1 and 3 messages. Another
    run resets state. Source stays untouched; output needs its own Save As. Retest late
    failure/cancel, deferred/Hold/Emit, CSV escaping/formula protection and exact-byte
    no-op using the [unified lifecycle checklist](stage5b-batch-scripting-acceptance.md).
    TCP/MLLP only shows disabled Planned Sender/Listener and opens no networking UI.

    ```csharp
    int count = 0;
    var report = Batch.Report("summary", "Messages");
    void Main() { count++; Message.SetText("PID-5.2", "SYNTHETIC" + count); }
    void Complete() { report.Row(count.ToString()); }
    ```

Record native results before requesting separate explicit Git authorization.

## Files changed by this finishing pass

Earlier uncommitted Stage 5B files remain; this pass changes the following files only:

| Area | Files |
| --- | --- |
| Shell | src/HL7Workbench.Desktop/MainWindow.axaml; MainWindow.axaml.cs; MainWindow.Editing.cs; new MainWindow.Shell.cs |
| Shared script commands | src/HL7Workbench.Desktop/ViewModels/ScriptEditorViewModel.cs; new UiCommand.cs |
| Views | src/HL7Workbench.Desktop/Viewer/ScriptEditorView.cs; new UtilityRibbon.cs, ScriptCommandMenu.cs, PreferencesWindow.cs, ScriptReportsWindow.cs |
| Workspace | src/HL7Workbench.Workspace/EditorPreferences.cs, ScriptLibrary.cs, ScriptEditorSession.cs; new ApplicationPreferences.cs |
| Desktop tests | tests/HL7Workbench.Desktop.Tests/ScriptEditorTests.cs, BatchEditorTests.cs, ScriptEditorWorkflowTests.cs; new ShellFinishingTests.cs |
| Workspace tests | tests/HL7Workbench.Workspace.Tests/ScriptLibraryTests.cs; new ApplicationPreferencesTests.cs |
| Documentation | README.md; docs/PROJECT_CONTEXT.md, architecture.md, script-api.md, stage5b-batch-scripting-acceptance.md; new desktop-preferences.md and this checklist |

Core, Scripting/ScriptHost runtime, dependency declarations, lock files, publish
verification helper, synthetic fixtures and the existing exported CSV are unchanged
by this UI pass. Native acceptance remains pending.
