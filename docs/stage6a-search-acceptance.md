# Stage 6A search acceptance and verification

**Stage 6A accepted on 2026-10-16:** the user confirmed native Windows acceptance,
including the final Find Messages naming/open-focus cleanup, and explicitly authorized
one commit on main followed by a normal push to origin/main. Accepted baseline:
`0497180b3799c5075f9fbc9dd6d778b48e68d279`.
Commit message: `feat: add Stage 6A message search and filtering`.
No source/test changes, history rewriting, force-push, tag, release, installer or
distribution package is authorized. Stage 5B regression tests remain retained.
See [search semantics and limits](message-search.md) and [ADR 0008](decisions/0008-read-only-message-search.md).

## Completed final UX native acceptance

The user confirmed this procedure passed. Retain it for future regression checks;
launch the Release build with the command below, using only synthetic input.

1. Verify the tool title, View entry and HL7 Tools ribbon command all say **Find Messages**.
   **Focus Search** is absent from the ribbon. **Clear Search / Restore Navigator** remains.
2. Hide the tool. Open it separately through HL7 Tools → Find Messages, View → Find Messages
   and Ctrl+F. Each reveals the same tool and focuses/selects the search-value input without
   starting a scan. Check the updated tooltip/accessibility label and collapsed/pinned ribbon.
3. Run a search with the tool's **Search** button. Move focus away and invoke each entry
   again: the existing query/results/filter remain, with no new scan. Verify the tool's
   **Clear** button and retained ribbon Clear command still restore the full navigator.
4. Float, hide/reopen and dock the tool; Ctrl+F from a floating window still focuses its
   value input. Restore a layout saved before the rename: the old `tool.search-results`
   identity reopens as Find Messages. Recheck Reset Layout / Recover Windows and both themes.
5. Confirm single-click ribbon tab switching and normal result navigation still work.
   No save, source mutation or automatic search occurs when opening/focusing the tool.

## Full feature acceptance reference

The user has completed this functional procedure and the focused UX procedure above.
Retain both for regression/reproduction; no Stage 6A native acceptance step is pending.

Use only reviewed synthetic input and disposable generated data. Record the original
fixture hash before opening and compare it after all searches and unsaved-edit tests:

```powershell
Set-Location C:\Dev\HL7Workbench
Get-FileHash samples/synthetic-only/stage5b-repeated-patients.hl7 -Algorithm SHA256
dotnet run --project C:\Dev\HL7Workbench\src\HL7Workbench.Desktop -c Release --no-build --no-restore
```

Expected SHA-256: `8ABFC4D2F05E9FBB47D9E6716BFEBCBAF2092BFE6B1EE693FC9AB3C3ADFA8086`.

1. Open that three-message fixture. From collapsed **HL7 Tools → Search**, open Find
   Messages. Verify View → Find Messages also works and there are still only three
   permanent menus. Press **Ctrl+F** from document/code/floating tools: the value input
   receives focus without invoking a document/script save or changing Edit Mode.
2. Text search `SYN-001`: ordinals **1 and 3**, one match each. Select one row with a
   single click: only row selection changes. Double-click or Enter navigates the existing
   source document to its original ordinal, updating raw viewer, navigator and inspector.
3. HL7 Field search **PID-3.1 Equals SYN-001**, then Contains `001`: ordinals 1 and 3.
   Search **PID-5.1 Equals example**: two matches by default; enable Case sensitive:
   zero. Correct the case: two. Check invalid paths, blank values and no-match feedback.
   Confirm field path and search value are visually distinct; rows contain no patient values.
4. Enable Filter Navigator for ordinals 1 and 3. The file toolbar's selector contains
   those original ordinals only; First/Previous/Go/Next/Last obey the filter. Keep message
   2 active before filtering and verify it remains visible with an excluded indicator
   until explicit matching navigation. Disable filtering: all three messages return.
   Clear also restores all messages. A zero-match filter must not remove document content.
5. With valid results/filter, enable the existing Edit Mode and change a supported raw
   value to `UPDATEDSYN` without Save. Results/filter clear immediately with a changed
   notice; the draft and undo remain. Rerun: search sees UPDATEDSYN. Undo/redo invalidates
   again. Do not save over the fixture. Ordinary Save/Save As regression checks use only
   disposable copies and retain the accepted Stage 5B confirmation/safety semantics.
6. Search one document, switch to another and return: results/filter must not carry over.
   Close/reopen the source and rerun. Closing or editing during an active scan must not
   publish stale rows or leave a filtered navigator. Cancel must leave no partial filter.
7. Generate/open the large synthetic file below. Search `SYN-` for high matches, inspect
   200-row pages and later original ordinals, navigate a row, and test zero matches.
   Start another broad search and Cancel while scanning; scrolling/input remain responsive.
   Rerun after cancellation and verify the new completed result set replaces the canceled run.
8. Float, dock, hide/reopen Find Messages; value/controls and shared commands remain
   usable. Save/restore layout, Reset Layout / Recover Windows, and reopen through View
   or the ribbon. Placement is restored without serializing terms/results/control IDs.
   Check Ctrl+F in floating windows, light/dark themes, narrow widths, scrolling and DPI.
9. Compare fixture hash again. Search, filtering and unsaved edits did not change original
   bytes. Recheck Stage 5B script execution, reports, safe exports, Ctrl+S, Preferences,
   ribbon pinning/single-click switching and Dock splitters. Record native results before
   requesting separate Git authorization.

Optional large synthetic fixture (new unique destination; creates no tracked artifact):

```powershell
New-Item -ItemType Directory -Force test-data/generated | Out-Null
$searchFixture = Join-Path (Get-Location) ('test-data/generated/stage6a-' + [Guid]::NewGuid().ToString('N') + '.hl7')
$searchStream = [IO.FileStream]::new($searchFixture, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
$searchWriter = [IO.StreamWriter]::new($searchStream, [Text.UTF8Encoding]::new($false))
try {
    for ($searchOrdinal = 1; $searchOrdinal -le 50000; $searchOrdinal++) {
        $searchWriter.Write("MSH|^~\&|SYN|TEST|DEST|TEST|20261016000000||ADT^A01|SYN-$searchOrdinal|T|2.5`rPID|1||SYN-$searchOrdinal||Example^Synthetic`r")
    }
} finally { $searchWriter.Dispose(); $searchStream.Dispose() }
$searchFixture
```

This optional file is owned by whoever runs the command. Keep it while reviewing any
failure, then remove only that known disposable file. Never clean unknown user files.

## Reproducing automated verification

For the final UX-only cleanup, run locked restore, Release build, the two Desktop class
filters, the full suite and repository checks. Workspace focused/stress commands below
remain the full-feature workflow; repeat stress only when relevant Workspace behavior changes.

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
& tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/HL7Workbench.Workspace.Tests.exe -class '*MessageSearchTests' -noLogo
& tests/HL7Workbench.Desktop.Tests/bin/Release/net10.0/HL7Workbench.Desktop.Tests.exe -class '*SearchToolTests' -noLogo
& tests/HL7Workbench.Desktop.Tests/bin/Release/net10.0/HL7Workbench.Desktop.Tests.exe -class '*ScriptEditorTests' -noLogo
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
$env:HL7WORKBENCH_STRESS = '1'
dotnet test --project tests/HL7Workbench.Workspace.Tests/HL7Workbench.Workspace.Tests.csproj -c Release --no-build --no-restore
Remove-Item Env:HL7WORKBENCH_STRESS
git diff --check
git status --short
```

Permanent search cases cover literal/case/non-overlap, raw custom delimiters/terminators,
non-ASCII and chunk boundaries, repetitions/segments/components/MSH, missing and malformed
structure, privacy-safe display, original ordinals/paging, unsaved edits/save/navigation,
switch/close/reload/invalidation, cancellation/supersession and snapshot drain. Desktop
cases cover real pointer/Enter navigation, immediate input, modes/commands/cancel/filter,
both themes, Ctrl+F roots, virtual pages, original navigator and floating/layout recovery.
Stress adds 50,000 high/zero-match messages, in-flight cancellation and a 32 MiB field.

### Initial implementation verification (2026-10-16)

| Check | Final result |
| --- | --- |
| Locked dependency restore | Passed; dependency and lock files unchanged |
| Release solution build | Passed; 0 warnings, 0 errors |
| Focused search tests | 54 Workspace + 6 Desktop passed; Workspace also run with stress enabled |
| Full automated suite | 862 passed, 0 failed, 0 skipped (repeated after the test correction below) |
| Full Workspace stress suite | 387 passed, 0 failed, 0 skipped; 2m 15s |
| Search stress bounds | 50,000 rows / 250 pages; high/zero matches and cancellation; 32 MiB field; both retained-memory upper limits passed |
| Source preservation | Reviewed synthetic fixture SHA-256 unchanged |
| Repository review | 23 files: 13 modified tracked / 10 untracked; no staged changes; whitespace/conflict checks passed |
| Native Windows UI | Functional and final naming/open-focus UX acceptance passed, confirmed by the user |

These results cover the initial implementation before the final naming/open-focus cleanup.
Main remains at the baseline above. No staging, commit, push, tag or release was performed.

### Final UX cleanup verification (2026-10-16)

Locked restore and the Release solution build passed (0 warnings/errors). All 33 focused
Desktop cases passed: 6 SearchToolTests and 27 shell/ribbon/ScriptEditorTests, including
single-click switching, themes, shared command identities, accessible names/tooltips,
actual value-input focus after reveal, retained Search/Clear, and legacy layout recovery.
The full solution suite passed again: 862 succeeded, 0 failed, 0 skipped (1m 11s).
An initially extended focus assertion exposed that the lightweight test application did
not load Dock's theme/template. The permanent test now loads the production Dock composition
and verifies the revealed view's focus; no application behavior or assertion was bypassed.

Workspace source/tests and synthetic fixtures are byte-identical to the pre-cleanup
working tree. Search execution, filtering, paging, cancellation, revisions and navigation
are unchanged. Workspace stress was not repeated; the previous 387-case pass remains
the applicable evidence. Diff/whitespace/conflict checks passed across all 23 Stage 6A
changed/new files. This cleanup touched the following 13 existing working-tree files:

| Area | Files changed by this cleanup |
| --- | --- |
| Shell | src/HL7Workbench.Desktop/MainWindow.axaml; src/HL7Workbench.Desktop/MainWindow.axaml.cs; src/HL7Workbench.Desktop/MainWindow.Shell.cs |
| Ribbon/model/layout identity comment | src/HL7Workbench.Desktop/Viewer/UtilityRibbon.cs; src/HL7Workbench.Desktop/ViewModels/SearchViewModel.cs; src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs |
| Permanent UI tests | tests/HL7Workbench.Desktop.Tests/SearchToolTests.cs; tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs |
| Documentation | README.md; docs/PROJECT_CONTEXT.md; docs/message-search.md; docs/stage6a-search-acceptance.md; docs/decisions/0008-read-only-message-search.md |

No new disposable artifacts were created. Existing user files, ignored build/publish
outputs and retained benchmark artifacts remain. Main is unchanged at 0497180 with
13 modified tracked / 10 untracked Stage 6A files and nothing staged at that checkpoint.
The final native acceptance/Git authorization at the top supersedes that historical boundary.

### Final pre-commit review (2026-10-16)

The working tree matched the accepted inventory exactly: 13 modified tracked and 10 new
untracked files, no staged changes, on main at the accepted baseline. All 17 changed
C#/markup files matched the verified Release portable-PDB source checksums; no source
or test edits were needed. Existing successful verification remains: locked restore,
zero-warning/error Release build, 33 focused UI tests, 862 full-suite tests and the
387-case Workspace stress pass before the UI-only cleanup. Those expensive checks were
not rerun. Native Windows acceptance is the user's confirmed pass.

Final review updated acceptance status only in the existing milestone documents. The
23-file inventory is unchanged, source/tests are unchanged, and the synthetic fixture
hash still matches. Diff/whitespace and conflict-marker checks passed. Local ignored
artifacts, build/publish outputs and the user's CSV remain excluded from the commit.

Worker/dependency/package declarations, ScriptHost and Scripting are unchanged. Repeating
published-worker checks is unnecessary unless those boundaries change. This does not
claim that a stale publish folder contains Stage 6A; native testing uses the Release run.

The first stress run exposed a test-only assertion defect: a collected-memory delta of
-3,974,480 bytes failed an incorrectly imposed zero lower bound. Retained-memory growth
is allowed to be negative when earlier fixture state is collected. The permanent test
now keeps results live through measurement and retains both upper limits (80 MiB for
50,000 metadata rows, 16 MiB for the 32 MiB field search). All 54 focused Workspace cases
passed with stress enabled after that correction; no application check was skipped.

## Cleanup and files

The root AGENTS policy now permanently preserves unit/integration/regression/stress tests,
reusable synthetic fixtures and supported verification scripts, while allowing only owned
disposable artifacts to be removed after review. Pre-existing user files, ignored outputs
and useful failure diagnostics must be preserved. No broad cleanup or routine bin/obj
deletion is permitted. The reusable Stage 5B fixture is unchanged; tests create private
synthetic temporary sources and release their snapshots/directories after draining work.

No Stage 6A probe project, one-off harness or disposable repository fixture was created.
Pre-existing ignored artifacts (including the reused/refreshed stage3 stress fixtures and
benchmark reports), bin/obj/publish outputs and the user's synthetic-summary.csv remain
local. Their reuse is not authorization to delete them. Permanent tests and fixtures remain.

| Area | Files |
| --- | --- |
| Policy/context | AGENTS.md; docs/PROJECT_CONTEXT.md; README.md |
| Workspace | new MessageDocument.Search.cs, MessageSearch.cs, MessageSearchSession.cs; MessageDocument.cs, MessageDocument.Editing.cs |
| Desktop | MainWindow.axaml, MainWindow.axaml.cs, MainWindow.Shell.cs; Docking/WorkspaceDockFactory.cs, DockContentTemplate.cs; Viewer/FileDocumentView.cs, UtilityRibbon.cs; new ViewModels/SearchViewModel.cs and Viewer/SearchResultsView.cs |
| Permanent tests | new Workspace.Tests/MessageSearchTests.cs and Desktop.Tests/SearchToolTests.cs; Desktop.Tests/ShellFinishingTests.cs (new View/ribbon command inventory) |
| Documentation | new docs/message-search.md, this checklist and docs/decisions/0008-read-only-message-search.md |
