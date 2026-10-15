# Stage 5A final CI investigation and cleanup

Investigation date: 2026-10-15. Stage 5A and its final Script Editor UX have passed
the user's native Windows acceptance. At this checkpoint, all changes were uncommitted
and Git actions were unauthorized. The user subsequently accepted the final tooltip
discovery/dismissal correction and authorized one Stage 5A commit and normal push
to origin/main; see PROJECT_CONTEXT.md for the current review boundary.

This report records the 621-test CI-investigation checkpoint. Subsequent native
testing found that the normal small-message RAW path was still unguarded, and a
layout-dependent weakness remained in the presentation guard. See
`stage5a-native-tooltip-dismissal.md` for the superseding correction and verification.
The file inventory below is the historical checkpoint snapshot, not current status.

## Historical evidence

Authenticated GitHub Actions job logs were available and inspected:

| Run | Commit | Result | Runner image revision |
| --- | --- | --- | --- |
| [Build and test #6](https://github.com/BobbyHanna/HL7Workbench/actions/runs/38684480100) | `c18efc4` | Build: zero warnings/errors; tests: 526 passed, 1 failed | `20260925.250.1` |
| [Build and test #7](https://github.com/BobbyHanna/HL7Workbench/actions/runs/38685418494) | `025a804` | Build: zero warnings/errors; tests: 527 passed | `20261007.264.1` |

Both used Windows Server 2025 (`10.0.26100`), runner 2.337.0, the
`windows-2025-vs2026` image family and SDK 10.0.402. The succeeding commit changes
only README, architecture and dependency documentation. The workflow and affected
production/test code were identical between those commits. They were also unchanged
in the Stage 5A working tree before this investigation's correction.

The workflow runs locked restore, Release build and the complete suite on
`windows-latest`, using `actions/setup-dotnet` with `10.0.x`. It has no test retries,
skips or failure suppression. The two runner images differ, so an environmental
influence is possible; their difference alone does not establish causation.

The actual failed assertion is `Assert.False(ToolTip.GetIsOpen(textView))` in
`LargePresentationPreservesPathsSelectionAndFollowingStructure`, historical line
110. It checks tooltip dismissal after a double-click in the abbreviated,
source-backed large-message editor. The other stack location, line 80, is the
headless UI dispatch boundary. This assertion does not compare parsed HL7 values,
source bytes or structural ranges.

## Reproduction, cause and correction

Twelve separate focused runs of the original test passed before the correction,
with `CI=true`, `GITHUB_ACTIONS=true`, `DOTNET_NOLOGO=1` and the workflow's telemetry
settings. Twelve more passed after it. Local SDK is 10.0.401; this is a CI-like
environment, not a recreation of either hosted runner image.

The production hover callback trusts the button state carried by AvaloniaEdit's
earlier pointer-move event. Closing the tooltip on pointer press did not invalidate
a late hover notification carrying that older, unpressed state. Such a notification
can reopen the tooltip even after mouse release. The assertion is valid; the
behavior under that event ordering is a production tooltip-dismissal defect.

Two deterministic regression cases deliver an earlier move's hover notification
through the real TextView routed event, first while the button is held and then
after release. Both failed before the production correction with Expected False /
Actual True, and both pass afterward. They also check that fresh unpressed movement
restores normal hover and preserves the selected structural path. They add no waits
and do not alter or weaken the original test.

The initial `PresentationView` correction retained click dismissal until fresh,
unpressed pointer movement to another editor-relative position. Reattachment reset
that view-local state. This was superseded after native acceptance failed. No Core,
source/display mapping, scripting execution, worker protocol or transaction behavior
is changed. The correction is confined to the affected presentation editor.

**Confidence:** high that this is a real, reproduced production event-order defect;
moderate that it caused run #6 specifically. Historical logs contain the assertion
and environment, but no pointer/timer trace, so that precise historical ordering
cannot be proved. No OS-specific defect or runner-image regression was established.
It was not already corrected by the succeeding documentation commit or Stage 5A.

## Temporary artifacts and pending-change review

`artifacts/semantic-probe/Program.cs` and its project were a temporary compiler-symbol
timing investigation, with local build-output HintPaths. They were absent from the
solution and had no production, test, build or documentation references. The entire
verified probe directory was removed: 62 files, 17,687,181 bytes, including its own
bin/obj and lock outputs. No other matching probe outputs remained. Unrelated
artifacts and legitimate uncommitted Stage 5A work were retained.

Generated binaries, test results, publish output, local investigation logs and
temporary synthetic data are ignored. No artifacts are tracked. Pending additions
are source, project/lock/build files, tests, documentation and one reviewed synthetic
HL7 sample. No generated binary, local script record or preferences file is in the
pending additions. Script Library/preferences remain under LocalApplicationData;
the user's local records were not read, changed or removed. No Core file or workflow
is changed. A stale build-target comment was corrected to reflect the already
accepted compiler-only Desktop reference; deployment logic is unchanged.

## Final verification

| Check | Result |
| --- | --- |
| Locked restore | Passed |
| Release solution build | Passed; 0 warnings, 0 errors |
| Complete automated suite | 621 passed, 0 failed, 0 skipped |
| Core / Workspace / Desktop / Scripting | 211 / 272 / 110 / 28 |
| Workspace with `HL7WORKBENCH_STRESS=1` | 274 passed, 0 failed, 0 skipped |
| Framework-dependent folder publish | Passed to ignored `artifacts/stage5a-publish` |
| Published worker smoke | Compile, transform synthetic input, finish, clean exit passed |
| Publish dependency checks | Compiler-only assemblies at Desktop root; scripting runtime in ScriptHost |
| `git diff --check` | Passed |
| Untracked text review | All 36 files checked for trailing whitespace/conflict markers; passed |

The two new deterministic tooltip cases account for the increase from 619 to 621.
The original tooltip test passed all 12 post-fix focused repetitions. No assertion
was weakened, test skipped or arbitrary wait added. The initial build was blocked
by the running application locking its executable; after the user closed it,
the final Release build completed cleanly. The user's application was not terminated.

The new tooltip guard is verified with headless routed-input regressions. A native
spot-check can hover the abbreviated large-message RAW editor, double-click, confirm
dismissal, then move and hover again to confirm normal restoration. This specific
post-investigation correction has not been manually exercised by the user. The
previously accepted Stage 5A Script Editor behavior and scope remain unchanged.

No known implementation or verification blocker remains. Exact historical event
ordering remains uncertain; a new hosted CI run has not been dispatched. Explicit
user authorization is still required for any Git commit or later publication action.

## Complete Git working-tree inventory

Branch: `main`, HEAD `025a804`, tracking `origin/main`. No staged changes, tracked
deletions, renames or conflict entries. There are 27 modified tracked files and 36
untracked files. The inventory below is literal `git status --short
--untracked-files=all` output; paths are relative to `C:\Dev\HL7Workbench`.

```
 M Directory.Packages.props
 M HL7Workbench.sln
 M README.md
 M docs/PROJECT_CONTEXT.md
 M docs/architecture.md
 M docs/dependencies.md
 M src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs
 M src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
 M src/HL7Workbench.Desktop/HL7Workbench.Desktop.csproj
 M src/HL7Workbench.Desktop/MainWindow.Editing.cs
 M src/HL7Workbench.Desktop/MainWindow.axaml
 M src/HL7Workbench.Desktop/MainWindow.axaml.cs
 M src/HL7Workbench.Desktop/Viewer/PresentationView.cs
 M src/HL7Workbench.Desktop/packages.lock.json
 M src/HL7Workbench.Workspace/ActivityLog.cs
 M src/HL7Workbench.Workspace/HL7Workbench.Workspace.csproj
 M src/HL7Workbench.Workspace/MessageDocument.Editing.cs
 M src/HL7Workbench.Workspace/MessageDocument.cs
 M src/HL7Workbench.Workspace/MessageFileIndex.cs
 M src/HL7Workbench.Workspace/SourceSnapshot.cs
 M src/HL7Workbench.Workspace/packages.lock.json
 M tests/HL7Workbench.Desktop.Tests/HL7Workbench.Desktop.Tests.csproj
 M tests/HL7Workbench.Desktop.Tests/Stage3ViewTests.cs
 M tests/HL7Workbench.Desktop.Tests/packages.lock.json
 M tests/HL7Workbench.Workspace.Tests/HL7Workbench.Workspace.Tests.csproj
 M tests/HL7Workbench.Workspace.Tests/Stage3LifecycleTests.cs
 M tests/HL7Workbench.Workspace.Tests/packages.lock.json
?? build/ScriptHost.targets
?? docs/decisions/0005-message-scripting-foundation.md
?? docs/stage5a-final-ci-investigation.md
?? docs/stage5a-script-editor-visual-acceptance.md
?? docs/stage5a-scripting-acceptance.md
?? samples/synthetic-only/stage5a-scripting.hl7
?? src/HL7Workbench.Desktop/ViewModels/ScriptEditorViewModel.cs
?? src/HL7Workbench.Desktop/Viewer/ScriptEditorPalette.cs
?? src/HL7Workbench.Desktop/Viewer/ScriptEditorView.cs
?? src/HL7Workbench.Desktop/Viewer/ScriptSemanticHighlighting.cs
?? src/HL7Workbench.Desktop/Viewer/ScriptSymbolClassifier.cs
?? src/HL7Workbench.ScriptHost/HL7Workbench.ScriptHost.csproj
?? src/HL7Workbench.ScriptHost/Program.cs
?? src/HL7Workbench.ScriptHost/packages.lock.json
?? src/HL7Workbench.Scripting/HL7Workbench.Scripting.csproj
?? src/HL7Workbench.Scripting/ScriptContracts.cs
?? src/HL7Workbench.Scripting/ScriptMessage.cs
?? src/HL7Workbench.Scripting/ScriptProtocol.cs
?? src/HL7Workbench.Scripting/ScriptWorkerSession.cs
?? src/HL7Workbench.Scripting/WorkerLifetime.cs
?? src/HL7Workbench.Scripting/packages.lock.json
?? src/HL7Workbench.Workspace/BoundedOutputStream.cs
?? src/HL7Workbench.Workspace/MessageDocument.Scripting.cs
?? src/HL7Workbench.Workspace/ScriptEditorPreferences.cs
?? src/HL7Workbench.Workspace/ScriptEditorSession.cs
?? src/HL7Workbench.Workspace/ScriptExecution.cs
?? src/HL7Workbench.Workspace/ScriptLibrary.cs
?? tests/HL7Workbench.Desktop.Tests/ScriptEditorPolishTests.cs
?? tests/HL7Workbench.Desktop.Tests/ScriptEditorTests.cs
?? tests/HL7Workbench.Desktop.Tests/ScriptSymbolHighlightingTests.cs
?? tests/HL7Workbench.Scripting.Tests/HL7Workbench.Scripting.Tests.csproj
?? tests/HL7Workbench.Scripting.Tests/WorkerTests.cs
?? tests/HL7Workbench.Scripting.Tests/packages.lock.json
?? tests/HL7Workbench.Workspace.Tests/ScriptEditorPreferencesTests.cs
?? tests/HL7Workbench.Workspace.Tests/ScriptExecutionTests.cs
?? tests/HL7Workbench.Workspace.Tests/ScriptLibraryTests.cs
```
