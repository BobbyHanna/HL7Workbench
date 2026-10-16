# Stage 6B Advanced Search & Saved Searches acceptance corrections

Baseline: accepted Stage 6A `dbcd6284eabb01220046109a1f01f2e94626edcd`, branch main.
**Native Windows acceptance passed; Stage 6B is accepted by the user.**
Explicit authorization covers one accepted 44-file milestone commit and normal push to
origin/main, using `feat: add Stage 6B advanced search and saved searches`. No history
rewriting, tag, release, installer or distribution package is authorized. See [semantics](message-search.md),
[ADR 0009](decisions/0009-explicit-saved-search-definitions.md) and [Preferences](desktop-preferences.md).

## Native Windows procedure

### Focused resizable-search and Light-theme retest

Run this new focused pass as well as the retained 17-step functionality procedure below.
Use only the synthetic fixtures below; do not overwrite originals. The 998/510 totals
from the earlier correction pass are historical and are superseded by the fresh results
in Current correction verification.

1. Open Find Messages docked at the bottom, switch to Advanced and create three/four
   group levels. Initial proportions should be about 60% builder / 40% results. Drag
   the horizontal divider until the builder occupies most of the height (and back).
   Both regions scroll independently. Search/Cancel/Clear, mode and Filter Navigator
   stay accessible above them. Focus the divider and try Up/Down arrow resizing.
2. Run a matching query, select a result, enable filtering and record the ordinals/page.
   Drag/keyboard-resize, expand/collapse groups and resize the containing window. Nothing
   should execute or clear/reorder discovery. Check 200-row paging and result navigation.
3. Dock at the side, float, hide/reopen, restore an older layout, and Reset Layout /
   Recover Windows. Try a narrow/short pane: minimums shrink rather than forcing Dock
   larger. Switch to Simple: query controls should be compact, with no reserved 60% area.
   Return to Advanced: its prior proportion returns. Restart and verify it restores again.
   No query or results should restore from the split preference.
4. Open a synthetic HL7 document in Dark. Select text, place the caret and scroll. Apply
   Light in Preferences. The editor itself must become white with dark value text;
   inspect segment names, all delimiter categories, escape delimiter accents, line
   numbers, caret, text selection and structural selection. Check current-line styling
   only if enabled. Escape bodies have no separate syntax category. Inspect custom
   delimiters too, and large/source-backed previews, omission markers, virtualized blocks
   and Full Source Viewer. No source read/reopen should be caused by a theme change.
5. Inspect Light main/floating windows, tool headers, ribbon tabs/commands (temporary and
   pinned), Find Messages, root/child groups, Search Library, result selections, Navigator,
   Inspector, menus/context menus, tooltips, Preferences and confirmation dialogs. Look
   for readable normal/secondary/disabled text, boundaries, hover/pressed/selected/focus
   states and errors/empty states explained by text. Light should have distinct white
   working surfaces and a pale canvas rather than broad undifferentiated gray areas.
6. Make an unsaved synthetic edit, create undo/redo state, select text and scroll. Switch
   Light/Dark repeatedly without closing the tab. Confirm exact draft text, dirty state,
   undo/redo, caret, selection and scroll stay intact; no search/script runs. Dark should
   retain the established HL7 canvas and syntax accents. The C# Script Editor keeps its
   existing Stage 5B VS Dark palette; verify its workflows remain functional.
7. Apply Follow System, change Windows light/dark preference while the app is open, and
   verify complete appearance including editor/floating windows when Avalonia reports
   the OS change. Explicit Light/Dark should override the OS. Restart each preference
   once, verify persistence, then restore the user's preferred app/OS settings.
8. Recheck nested evaluation, PV1 segment existence without field numbers, field/component/
   subcomponent presence, negative/repetition behavior, saved-search migration/CRUD, Copy
   Path, cancellation, navigation/filtering and fixture hashes using the retained steps.

No user-defined theme editor, profiles, color pickers or theme import/export is implemented.
The resource boundary is prepared for that later milestone, with no new dependency packages.

Use synthetic data only for acceptance. Preserve existing files and saved definitions.
Launch the verified Release build:

```powershell
Set-Location C:\Dev\HL7Workbench
Get-FileHash samples/synthetic-only/stage5b-repeated-patients.hl7 -Algorithm SHA256
dotnet run --project C:\Dev\HL7Workbench\src\HL7Workbench.Desktop -c Release --no-build --no-restore
```

Original fixture SHA-256: `8ABFC4D2F05E9FBB47D9E6716BFEBCBAF2092BFE6B1EE693FC9AB3C3ADFA8086`.
For nested/existence/Copy Path checks, create one disposable synthetic input in a unique
location using a separate PowerShell window:

```powershell
$nativeInput = Join-Path $env:TEMP ('hl7-stage6b-native-' + [Guid]::NewGuid().ToString('N') + '.hl7')
$nativeMessages = @(
'MSH|^~\&|SYN|TEST|DEST|TEST|20261016000000||ADT^A01|SYN-N1|T|2.5',
'PID|1||SYN-001^MR|', 'PV1||I', 'OBX|1|NM|||1',
'MSH|^~\&|SYN|TEST|DEST|TEST|20261016000000||ADT^A01|SYN-N2|T|2.5',
'PID|1||SYN-002&A^MR~SYN-003&B', 'OBX|1|ST|||SYN-TEXT',
'MSH|^~\&|SYN|TEST|DEST|TEST|20261016000000||ORU^R01|SYN-N3|T|2.5',
'PID|1||SYN-003', 'OBX|1|NM|||3',
'MSH|^~\&|SYN|TEST|DEST|TEST|20261016000000||ORU^R01|SYN-N4|T|2.5',
'PID|1||SYN-004^MR', 'PV1|', 'OBX|1|TX|||SYN-OTHER'
)
[IO.File]::WriteAllText($nativeInput, ($nativeMessages -join "`r") + "`r", [Text.UTF8Encoding]::new($false))
$nativeInput
Get-FileHash $nativeInput -Algorithm SHA256
```

1. **Simple regression:** Open the original three-message fixture. Ctrl+F, View → Find
   Messages and HL7 Tools → Find Messages reveal/focus the same tool without scanning.
   Text SYN-001 and Field PID-3.1 Equals SYN-001 return original ordinals 1/3. Check case,
   Contains, single selection, double-click/Enter navigation and no automatic Edit Mode.
2. **Nested OR/AND:** Open the new four-message input. In Advanced, set root ANY with
   Segment PV1 Exists plus a child ALL containing Field MSH-9.1 Equals ADT and Field
   PID-3.1 Equals SYN-001. Only ordinals **1/4** match. One row per message; summary/count
   refer to satisfied evaluated leaves, not exhaustive occurrences or patient snippets.
3. **More combinations/levels:** Build root ALL, child ANY of PID-3.1 Contains SYN-001
   and PID-3.1 Contains SYN-002, plus Segment OBX Exists: **1/2**. Build root ALL with
   MSH-9.1 Equals ORU plus child ANY of OBX-2 Equals NM/ST: **3**. Add a third/fourth group
   level; the fifth level is unavailable. Root removal is unavailable. Empty groups and
   invalid rows show validation and never match all. Check the global 16-leaf/16-group
   limits, Add/Remove controls, clear group borders and indentation. Collapse a child
   after searching: query, results and filter stay unchanged. Editing structure/operator
   clears stale discovery without automatically running. Expand and use Tab/Enter.
4. **Segment existence:** Select **HL7 Segment**, path PV1, Exists: **1/4**; Does Not
   Exist: **2/3**. No field number/value/case setting is required. PID Exists: all four.
   Bare PV1 in **HL7 Field** instead gives guidance to choose Segment. Try malformed
   identifiers and the retained custom-delimiter fixtures; no silent repair occurs.
5. **Field/component existence:** HL7 Field PV1-1 Exists: **1/4**, including present-empty
   fields; PID-4 Exists: **1**. PID-99 Exists returns none; Does Not Exist returns all.
   PID-3.2 Does Not Exist: **3**. PID-3.1.2 Exists: **2**. Value/case controls disappear.
   Check missing versus empty components and repetitions, including custom delimiters.
6. **Negatives:** PID-3.1 Does Not Equal SYN-001: **2/3/4**; Does Not Contain SYN-003:
   **1/4** (a match in any repetition defeats the negative). Missing PID-99 negative
   string comparisons return none. Values/whitespace are literal; no numeric coercion.
7. **Filtering:** Filter a 1/4 result. Original ordinals remain authoritative; excluded
   current messages are labeled. First/Next/Previous/direct navigation and inspection
   work normally. Clear restores all navigation and retains the authored query. Editing
   a group/condition or switching Simple/Advanced restores navigation without scanning.
8. **Save/restart/load:** Expand Search Library. Confirm the nonmodal local unencrypted
   storage disclosure is readable. Save a synthetic nested definition, restart, then
   Load it: same tree/name/values, no auto-search/results. Run explicitly against either
   synthetic document. Switching or selecting library items never scans or saves history.
9. **Migration:** If a synthetic version-1 flat definition exists from the earlier build,
   load it and confirm identical ALL/ANY semantics, order/name/literals/case. List/Load
   must leave its bytes/version unchanged. Explicit Update or Rename writes schema/query
   2 under the same GUID against its original revision. Automated tests cover both ALL
   and ANY plus Simple migration. Do not alter unknown existing user definitions.
10. **Library CRUD/disclosure:** Save and Update act explicitly without repeated modals;
    there is no PHI prohibition. Edit alone does not change the saved definition. Rename
    preserves its stored query even when the builder differs. Duplicate/unsafe names
    cannot overwrite other records. Delete defaults to Cancel; confirming removes only
    that definition and retains the current query/results/source. Optional: a uniquely
    named disposable corrupt JSON entry is isolated with a count; other entries load.
11. **Copy Path:** In message 2, use Field Inspector. Right-click its selected PID heading
    to Copy Path → PID. Right-click field 3 → PID-3, first component → PID-3.1, and its
    second subcomponent → PID-3.1.2. Paste into the corresponding Find Messages target;
    copied text has no values or occurrence/repetition brackets and searches all applicable
    occurrences/repetitions. Test keyboard context menu on a selected row. Missing,
    nonstructural and stale nodes disable Copy Path. No Search This Field action exists.
12. **Theme selection:** Settings → Preferences → Appearance. Apply Light, then Dark:
    main window, Find Messages, ribbon temporary/pinned content, menus, context menus,
    dialogs, floating tools and selection/disabled states update with readable contrast.
    No restart is needed. OK/restart preserves selection. Cancel abandons unapplied edits;
    existing edit/ribbon/schema preferences retain their behavior.
13. **Follow System:** Apply Follow System (default for old preference files). Change
    Windows light/dark preference while running; verify updates where the platform supports
    Avalonia notifications, including floating tools. Explicit Light/Dark must override
    the OS. Return the OS/app preference to the user's desired setting after testing.
14. **Docking/narrow layout:** Float/dock/hide/reopen/recover Find Messages. Test Ctrl+F in
    main/floating roots, collapsed groups, existence-only queries, 400px-wide/short panes,
    scrolling, DPI and keyboard focus. Inputs/errors must remain reachable. Layout contains
    placement only and retains `tool.search-results`; Reset Layout and older layouts work.
    Ribbon single-click tabs, pinning and temporary overlay viewport remain unchanged.
15. **Drafts/invalidation:** Edit a disposable draft without Save. Results/filter invalidate;
    rerun sees the draft. Undo/redo, switch/close/reopen and superseded runs clear stale work.
    Saving/searching/loading does not rewrite untouched original bytes or trigger scripts.
16. **Large input/cancellation/paging:** Use the existing optional 50,000-message synthetic
    generator in the Stage 6A checklist with a unique ignored destination. Try nested
    high/zero-match queries, 200-row pages and late original ordinals. Cancel during a scan;
    no partial/stale filter remains and UI input stays responsive. Retain useful failure
    diagnostics; tests also cover 32 MiB fields and memory bounds.
17. **Preservation/Stage 5B:** Discard acceptance drafts; compare both input hashes. Spot-check
    scripting/reports, safe Save As/CSV overwrite and confirmation cancellation on disposable
    copies. Record native results. Clean only the uniquely owned successful acceptance
    input/exports, retaining failures, permanent fixtures/tests and unknown user files.

If no synthetic legacy entry is available for step 9, close the application and create
one uniquely named fixture in the default library (adjust only for a custom configuration
root). Restart, Load it, compare its hash before/after Load, then explicitly Update it.
For the four-message input this ANY definition returns **1/2/4**. After Update its Version
and QueryVersion become 2; the GUID/name and matches remain. Remove this owned record
through Delete Search after successful acceptance; do not alter other library files.

```powershell
$legacyId = [Guid]::NewGuid()
$legacyDirectory = Join-Path $env:LOCALAPPDATA 'HL7Workbench\Searches'
[IO.Directory]::CreateDirectory($legacyDirectory) | Out-Null
$legacyPath = Join-Path $legacyDirectory ($legacyId.ToString('N') + '.json')
$legacyRecord = @{
    Version = 1; QueryVersion = 1; Id = $legacyId.ToString()
    Name = 'Synthetic legacy ' + $legacyId.ToString('N'); Mode = 1; Combination = 1
    Conditions = @(
        @{ Target = 2; Operator = 4; Path = 'PV1'; Value = ''; CaseSensitive = $false },
        @{ Target = 1; Operator = 1; Path = 'MSH-9.1'; Value = 'ADT'; CaseSensitive = $true }
    )
}
[IO.File]::WriteAllText($legacyPath, ($legacyRecord | ConvertTo-Json -Depth 8), [Text.UTF8Encoding]::new($false))
Get-FileHash $legacyPath -Algorithm SHA256
```

Default Search Library: `%LOCALAPPDATA%\HL7Workbench\Searches`. Names/values are local
plaintext JSON and can be sensitive; ordinary searches and explicit saved PHI queries are
allowed. No query values enter logs/diagnostics/telemetry. No automatic history, results,
ordinals, control IDs or document paths are saved. Custom configuration roots use Searches
under that root. Definitions are bounded to 256 files / 128 KiB per record. No encryption,
regex, indexed occurrence/repetition addressing, query interchange/sync or result scripting.
The user confirmed completion of native acceptance, including the layout/theme corrections.

## Automated verification

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
& tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/HL7Workbench.Workspace.Tests.exe -class '*AdvancedSearchTests' -class '*SearchLibraryTests' -class '*MessageSearchTests' -class '*ApplicationPreferencesTests' -noLogo
& tests/HL7Workbench.Desktop.Tests/bin/Release/net10.0/HL7Workbench.Desktop.Tests.exe -class '*SearchLayoutThemeTests' -class '*AdvancedSearchUiTests' -class '*SearchToolTests' -class '*ScriptEditorTests' -class '*ViewerTests' -class '*InspectionViewTests' -class '*Stage3ViewTests' -class '*RawEditingCrashTests' -noLogo
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
$env:HL7WORKBENCH_STRESS = '1'
dotnet test --project tests/HL7Workbench.Workspace.Tests/HL7Workbench.Workspace.Tests.csproj -c Release --no-build --no-restore
Remove-Item Env:HL7WORKBENCH_STRESS
git diff --check
git status --short
```

The pre-correction checkpoint passed 960 automated / 476 Workspace stress tests.
Those totals describe the earlier flat builder, not acceptance of these corrections.
Current correction results follow from the completed verification run.
Worker/dependency/package/isolation boundaries are unchanged; unrelated published-worker
checks are unnecessary. Headless tests do not substitute for native Windows acceptance.

## Current correction verification

| Check | Result (2026-10-16) |
| --- | --- |
| Locked dependency restore | Passed; no dependency/project/lock changes |
| Release solution build | Passed; 0 warnings, 0 errors |
| Focused Workspace | 192 passed, 0 failed/skipped |
| Focused Desktop | 88 passed, 0 failed/skipped |
| Full solution suite | 1,018 passed, 0 failed/skipped; 1m 31s |
| Full Workspace stress | 519 passed, 0 failed/skipped; 2m 13s |
| Synthetic fixture | Original SHA-256 unchanged |
| Diff/whitespace/conflict checks | Passed; tracked and untracked files checked |
| Native Windows acceptance | Passed; user confirmed acceptance before final Git authorization |

The first corrections added 38 permanent cases beyond the earlier 960: 34 Workspace and four Desktop
(including theory cases). Previous tests remain; only assertions for explicitly changed
contracts are updated (nested builder, schema 2, subcomponent support and nonmodal saves).
Stress covers nested Boolean scans over 50,000 messages in addition to the retained
ALL/ANY, negatives, zero matches, cancellation, paging, 32 MiB fields and memory bounds.
The earlier Desktop disposal review also cleared the root tree's retained input
references; that regression remains included in the current build/focused/full checks.

The resizable-search/theme pass adds 20 more permanent cases (nine Workspace and eleven
Desktop). They cover schema/range/corruption defaults; real drag and keyboard gestures;
deferred persistence; short/narrow panes; mode switching/reparenting/reconstruction;
no discovery invalidation; both syntax delimiter sets and rendered color categories;
live draft/history/caret/selection/viewport preservation; palette substitution; compact/
virtualized source views and Full Source Viewer without reload; semantic resources,
selection and inherited theme propagation; and Light palette contrast. The existing
Dock float/restore/reset test now verifies the persisted split too. All previous tests
and fixtures remain. Fresh results below replace the historical verification baseline.

The current source/tests match the verified Release build and the 1,018-case full run;
later changes only record results in documentation. Fresh Workspace stress passed all
519 cases. Whitespace/diff checks cover all 44 tracked/untracked files, with no conflict
markers and the original synthetic fixture hash unchanged. No worker/isolation/package
dependencies changed, so no unrelated publish checks or installers were produced.
The user subsequently completed native Windows acceptance and explicitly authorized
committing/pushing this accepted milestone. Final recovery found no earlier Stage 6B
commit/staging; main and live origin/main matched the Stage 6A baseline. Source/tests
were unchanged from the verified build, so expensive suites were not rerun for committing.
Final whitespace/inventory/fixture checks passed; only documentation acceptance records
were updated. Automated results above retain their original execution provenance.

## Files and cleanup

The accepted AGENTS.md permanent test-retention/cleanup policy applies. Preserve all
previous regression/stress tests and reusable synthetic fixtures. New tests use private
synthetic sources/config/library directories and dispose owned snapshots/staging files.
No disposable probe projects/harnesses are needed. Existing ignored benchmarks/large
synthetic fixtures, user CSV and bin/obj/publish outputs are intentionally retained.
Reviewed pre-commit inventory: **44 files: 23 modified tracked, 21 untracked additions; nothing staged**.
Paths below are repository-relative; additions are marked new.

| Area | Exact files |
| --- | --- |
| Workspace | `src/HL7Workbench.Workspace/EditorPreferences.cs`; `MessageSearch.cs`; new `InspectionSearchPath.cs`, `MessageSearch.Evaluation.cs`, `MessageSearchQuery.Advanced.cs`, `SearchLibrary.cs`, `SearchPaneLayout.cs` in that same directory |
| Desktop shell | `src/HL7Workbench.Desktop/App.axaml.cs`; `src/HL7Workbench.Desktop/MainWindow.Shell.cs`; `src/HL7Workbench.Desktop/MainWindow.axaml.cs` |
| Desktop models | `src/HL7Workbench.Desktop/ViewModels/SearchViewModel.cs`; new `SearchConditionViewModel.cs`, `SearchGroupViewModel.cs`, `SearchViewModel.Library.cs` in that same directory |
| Desktop views | `src/HL7Workbench.Desktop/Viewer/ConfirmationDialog.cs`, `FullRawInspectorView.cs`, `InspectionPanes.cs`, `MessageInspectionView.cs`, `MessageViewer.cs`, `PreferencesWindow.cs`, `PresentationView.cs`, `SearchResultsView.cs`, `SourceColorizer.cs`; new `ApplicationTheme.cs`, `Hl7EditorPalette.cs`, `Hl7EditorTheme.cs`, `SearchConditionEditor.cs`, `SearchGroupEditor.cs`, `WorkbenchStyles.axaml`, `WorkbenchThemeResources.cs` in that same directory |
| Workspace tests | `tests/HL7Workbench.Workspace.Tests/ApplicationPreferencesTests.cs`, `MessageSearchTests.cs`; new `AdvancedSearchTests.cs`, `SearchLibraryTests.cs` in that same directory |
| Desktop tests | `tests/HL7Workbench.Desktop.Tests/SearchToolTests.cs`, `ViewerTests.cs`; new `AdvancedSearchUiTests.cs`, `SearchLayoutThemeTests.cs` in that same directory |
| Documentation | `README.md`; `docs/PROJECT_CONTEXT.md`, `docs/desktop-preferences.md`, `docs/message-search.md`; new `docs/decisions/0009-explicit-saved-search-definitions.md` and this checklist |

All 31 pre-existing Stage 6B changed/new files were preserved and extended. No binaries,
generated outputs, secrets or patient data are added. Private test directories and owned
staging files drain/clean normally. No new repository probes or disposable exports were
created, so no pre-existing files were deleted. Existing ignored diagnostics, publish/
build outputs, Stage 3 synthetic stress benchmarks and `samples/synthetic-only/synthetic-summary.csv`
are intentionally retained. No permanent tests or reusable fixtures were removed.
The accepted 44 files are authorized for one commit and normal push to origin/main.
No tag, release, installer, distribution package or Stage 7 Git work is authorized.
