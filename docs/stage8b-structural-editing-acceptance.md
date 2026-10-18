# Stage 8B.1–8B.3: Structural editing foundation acceptance

Checkpoint status, 2026-10-17: the user confirmed native Windows acceptance of all
Stage 8B work, including the keyboard correction, 8B.4 and Script Output Naming,
and authorized one local checkpoint commit only. Earlier pending/no-commit statements
below are historical. See PROJECT_CONTEXT.md for the current checkpoint verification.
No push, tag, publish, release or Stage 8C/8D work is authorized.

This document retains the foundation and keyboard-correction verification records.
Stage 8B.1–8B.3 and the corrected native root Undo/Redo routing are now user accepted,
intentionally uncommitted. Stage 8B.4 was subsequently authorized; its implementation,
verification and pending native checklist are in
[Stage 8B.4 compatibility acceptance](stage8b4-compatibility-acceptance.md).
Stage 8C/8D and all Git/publication actions remain unauthorized.
Baseline: main at 8808929e844cb925fbdb11635e0b55740e5ec160.

## Implemented behavior

- Workspace MessageId/LogicalMessage origins and a metadata-only logical sequence.
- GetMessageId/GetLogicalPosition/ID navigation; logical MessageNumber and count.
- InsertMessage/InsertMessages at BeforeCurrent/AfterCurrent/Beginning/End.
- Atomic bounded batches, blank/incomplete drafts, and stable identity through redo.
- Chronological document Undo/Redo with automatic affected-message navigation,
  including with Edit Mode off or a source-backed current message.
- Exact text/structural save points, including undo across successful saves.
- Partition-based streaming save, single-message bounded encoding, staged re-indexing,
  range/framing/gap checks, and byte verification before replace/move.
- Expected destination versions from open/publication; external-change refusal.
- Mutation-locked asynchronous saving with cancellation tokens and transient progress.
- Narrow safety guards/adapters for remaining original-ordinal features and views.

See ADR 0011 for ownership, complexity, budgets, boundaries and race limitations.
No insertion or generation command is exposed in the product yet. Foundation
insertion is exercised through permanent Workspace and headless Desktop tests.

## Foundation temporary restrictions (superseded by Stage 8B.4)

Search/comparison/script guards described below were replaced by stable-ID integration
in 8B.4. The original-source, unsupported-boundary and payload limits remain in force.

After live structural insertion, search/filter execution, document comparison and
script capture fail safely. Existing results/filter state are invalidated; pasted-only
comparison remains independent. Save/reopen establishes a new original index, or
undo all insertions to restore original membership. Full stable-ID migration belongs
to 8B.4. Saved searches/worker protocol/Dock placement are unchanged.

Inserted messages have no Original Message or original-byte export source in the
open session, even after Save. Unchanged original messages retain exact byte export.
Known provisional/batch/ambiguous-framing containers reject insertion atomically.
Unterminated joins, empty/unrecoverable inserted payloads, encoded framing collisions
and other unexpected reclassification cannot publish. Incomplete MSH-started content
can save only when staged indexing recovers it exactly; no clinical certification.
No automatic terminator, delimiter, wrapper-count or source repair occurs.

Stage 8C owns insertion commands, Next-arrow append, editor focus/refinements and
their native acceptance. Stage 8D owns templates, deterministic data and generation UI.
The Stage 7 large-payload comparison restriction is unchanged.

## Automated verification commands

Run from C:\Dev\HL7Workbench in PowerShell. Full and stress suites must run sequentially
because existing generated fixture names are shared. Telemetry opt-outs apply to each shell.

~~~powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
dotnet test --project tests/HL7Workbench.Workspace.Tests -c Release --no-build --no-restore -- --filter-class 'HL7Workbench.Workspace.Tests.Structural*' --output Detailed
dotnet test --project tests/HL7Workbench.Desktop.Tests -c Release --no-build --no-restore -- --filter-class '*StructuralHistoryViewTests' --output Detailed
$env:HL7WORKBENCH_STRESS = '1'
dotnet test --project tests/HL7Workbench.Workspace.Tests -c Release --no-build --no-restore
$env:HL7WORKBENCH_STRESS = $null
git diff --check
~~~

For retained xUnit XML/live benchmark output, the same compiled Workspace assembly
also supports its native runner:

~~~powershell
New-Item -ItemType Directory -Path artifacts/stage8b -Force | Out-Null
$env:HL7WORKBENCH_STRESS = '1'
dotnet tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/HL7Workbench.Workspace.Tests.dll -showLiveOutput -noLogo -noColor -result-xml artifacts/stage8b/workspace-stress.xml
$env:HL7WORKBENCH_STRESS = $null
~~~

StructuralEditingTests covers provenance, foreign IDs, all positions, batches,
atomic failures, a seeded sequence/history oracle, maximum-count metadata, actual
100,000-live-draft stress and history branching. Existing history-budget regressions
remain, with their intentionally approved cross-message/Edit-Mode-off expectations updated.

StructuralSaveTests covers encoding/BOM/custom delimiters/mixed terminators, framing,
gaps, save points, repeated publication, external conflicts, equal-count swapped
payloads, provisional reclassification, unsupported boundaries and failure/cancellation.
Its large-payload cases use LargeSyntheticFile at 100 MiB plus optional 256/512 MiB.
They start on a small prefix, insert on either side without decoding the untouched
large message during insertion, and stream-compare its original/output ranges.
Undo may select the large previous survivor through the existing source-backed
viewer; it never enters the complete editor. Saving does not decode it.

StructuralHistoryViewTests uses awaited headless UI callbacks for actual editor
input/shortcut routing, affected-message Inspector/editor updates, source-backed
Undo, same-position/different-ID view replacement and detached-editor safety.
Existing raw-input transaction and Stage 1–7 regression tests are retained.

## Foundation verification record (before native keyboard correction)

Passing automated tests does not constitute native Windows acceptance.

Final verification on 2026-10-17:

| Check | Result |
| --- | --- |
| Locked solution restore | Passed |
| Release solution build | Passed; zero warnings/errors |
| Full solution | 1,173 passed; zero failed/skipped; 101.669 s |
| Focused Workspace foundation/save | 46 passed; zero errors/failed/skipped; 22.045 s |
| Focused Desktop history/view | 3 passed; zero errors/failed/skipped; 2.779 s |
| Complete Workspace stress | 599 passed; zero errors/failed/skipped; 209.927 s |
| Existing HL7 fixtures | All eight match HEAD blob hashes using --no-filters |
| Whitespace/conflict review | Tracked diff and new files passed |
| Git | main at 8808929e844cb925fbdb11635e0b55740e5ec160; 23 tracked modifications, nine new files, empty staging area |
| Native Windows acceptance | Pending; not replaced by headless tests |

The 49 new ordinary cases account for the increase from the 1,124-case Stage 7
baseline. The stress run included all existing stress cases, 100,000 live drafts,
and 100/256/512 MiB insertion/publication cases. Final full and stress suites ran
sequentially. The final focused run additionally verifies the unchanged original
message's actual exported bytes after its logical position shifts.

Observed on Windows NT 10.0.26100, .NET runtime 10.0.12, SDK 10.0.401, 16 logical
processors. These are single-run measurements with uncontrolled filesystem caches,
not throughput/latency guarantees. Sequence allocation excludes the pre-existing
original index, document saved-ID arrays and inserted payload/buffer objects.

| Sequence entries | Allocated bytes | Construction ms |
| --- | ---: | ---: |
| 1,000 | 55,176 | 0.024 |
| 50,000 | 2,666,400 | 3.150 |
| 100,000 | 5,441,960 (5.19 MiB) | 1.789 |

| Metadata operation (final count 100,000) | Beginning ms | Middle ms | End ms |
| --- | ---: | ---: | ---: |
| Insert one | 0.517 | 0.412 | 0.276 |
| Undo one | 0.347 | 0.176 | 0.002 |
| Redo one | 0.235 | 0.129 | <0.001 |
| Insert batch of 1,000 | 0.546 | 0.556 | 0.301 |
| Undo batch | 0.268 | 0.372 | 0.013 |
| Redo batch | 0.222 | 0.176 | 0.009 |

The earlier cold focused run's first splice was 2.673 ms; distinguish JIT/warmup
variation from the warm measurements above. No blocked/weighted sequence is needed
on this evidence. Actual 100,000 empty inserted drafts retained **23,685,408 bytes
(22.59 MiB)**; insertion/undo/redo measured **22.701 / 5.771 / 3.051 ms**, excluding
the asynchronous inspection wait. Draft buffers are additional to sequence metadata.

| Encoded payload MiB | First publication ms | Approx. total MiB/s | Save allocated bytes | Retained growth bytes |
| --- | ---: | ---: | ---: | ---: |
| 100 | 323.110 | 309 | 399,968 | 3,448 |
| 256 | 780.870 | 328 | 408,688 | 3,096 |
| 512 | 5,400.367 | 95 | 426,816 | 3,288 |

Separate phases from each subsequent associated save:

| Payload MiB | Streaming write ms | Staged byte-index ms | Mapping/byte verification ms |
| --- | ---: | ---: | ---: |
| 100 | 49.741 | 148.887 | 62.691 |
| 256 | 148.528 | 381.054 | 161.546 |
| 512 | 2,193.680 | 762.467 | 326.812 |

Write-phase timing excludes flush and destination-version checks; total publication
includes them. The first-publication total and subsequent-save phases are separate
observations. SHA-256 is computed during staged byte verification, avoiding an extra
candidate hash pass. Source/destination checks and byte verification still add real
sequential I/O. Large-case allocations stayed approximately 0.38–0.41 MiB.

## Foundation file inventory (before native keyboard correction)

All paths below are repository-relative. The scope contains 23 modified tracked
files and nine new files (32 total); nothing is staged.

| Area | Files |
| --- | --- |
| New Workspace foundation | LogicalMessageSequence.cs; MessageDocument.Sequence.cs; MessageDocument.Saving.cs; MessageSavePlan.cs |
| Workspace selection/edit state | MessageDocument.cs; MessageDocument.Editing.cs; EditableMessageBuffer.cs |
| Workspace capture/publication/index | SourceSnapshot.cs; MessageFileReader.cs; FileSaveDestination.cs; MessageFileIndex.cs |
| Temporary Workspace ordinal adapters/guards | MessageDocument.Search.cs; MessageDocument.Comparison.cs; MessageDocument.Scripting.cs; ComparisonInput.cs |
| Original source/diagnostics safety | RawInspectorSession.cs; ActivityLog.cs |
| Test visibility only | src/HL7Workbench.Workspace/HL7Workbench.Workspace.csproj |
| Desktop narrow integration | MainWindow.Editing.cs; ViewModels/ComparisonViewModel.cs; Viewer/ComparisonSourcePicker.cs; Viewer/FileDocumentView.cs; Viewer/MessageInspectionView.cs; Viewer/ViewerRequests.cs |
| New permanent tests | Workspace.Tests/StructuralEditingTests.cs; Workspace.Tests/StructuralSaveTests.cs; Desktop.Tests/StructuralHistoryViewTests.cs |
| Approved expectation updates | tests/HL7Workbench.Workspace.Tests/EditableDocumentTests.cs |
| Documentation | docs/PROJECT_CONTEXT.md; docs/architecture.md; docs/decisions/0011-structural-editing-foundation.md; this acceptance document |

Workspace filenames without a prefix are under src/HL7Workbench.Workspace.
Desktop filenames are under src/HL7Workbench.Desktop; test abbreviations are under
tests/HL7Workbench.*.Tests. No Core, Scripting/ScriptHost, lock file, sample, .gitignore,
dependency version, or packaging file changed.

The initial regression pass exposed Windows ReplaceFile's replacement-reader
restriction. The final validation lease handoff closes those handles before atomic
replacement; existing save/publication tests were rerun successfully. No fallback
copy/delete, weakened assertion or discarded regression was introduced.

Retained ignored verification reports are under artifacts/stage8b: foundation
XML/log, Workspace stress XML/log, initial full-suite log and final full-suite log.
Final focused Workspace and Desktop XML/log files are retained there as well.
Existing artifacts and generated large synthetic fixtures remain. No disposable
probe/harness or manual HL7/CSV export was created for this work.

## Native keyboard routing correction — 2026-10-17

Native acceptance found that a second Ctrl+Z required a RAW editor click after the
first Ctrl+Z navigated to another message. Ctrl+Y had the same routing exposure.
The original automated checks passed because they raised KeyDown directly on
FileDocumentView, bypassing the window's actual keyboard focus route.

**Root cause:** history keys were handled by FileDocumentView's tunnel handler and
MessageInspectionView's RAW TextArea tunnel handler. MainWindow had Save/Search
shortcuts but no document history route. Workspace correctly selected the affected
MessageId. That selection caused FileDocumentView.Refresh to clear Content and
replace MessageInspectionView. Detaching the focused TextArea cleared Avalonia's
focused element; the next physical key targeted the window and traversed neither
document-local handler. A headless platform-input regression reproduced the exact
failure before the fix: the first undo selected message 2, the editor was detached,
focus was null, and the second undo left message 2 selected.

**Fix:** DocumentHistoryShortcuts is the sole history keyboard authority. The shell
registers it once when creating the main window and each floating Dock HostWindow.
Its tunnel handler resolves DocumentManager.ActiveDocument at key time, including
when focus is empty, and consumes one document Undo/Redo per key. It never assigns
focus. Both former view-local history handlers are removed. RAW editor ownership
is checked against the active document; independent TextBox/AvaloniaEdit inputs
retain their native commands, even when their own history is empty. This includes
Search, script code, standalone editors and the message-number input inside the
document view. Existing Ctrl/Meta+Z/Y and Shift+Z redo support remains; unrelated
modifier combinations are left alone. Toolbar commands remain document commands.

The existing pre-input RAW UndoStack.SizeLimit = 0 safeguard is unchanged. No
history clearing occurs inside TextChanged or an AvaloniaEdit input transaction.
No Core/Workspace, identity, navigation, save, layout schema or dependency changes
were needed for this correction.

Permanent regression coverage now sends platform KeyPress/KeyRelease through
Window, rather than injecting history events at FileDocumentView. It checks:

- Two consecutive Undo keys after the first replaces the focused message view;
  two consecutive Redo keys starting on the other message, so the first also
  navigates/replaces the focused view. Edit Mode is off and focus becomes null.
- Editor text, Inspector binding, message position, dirty/history state, source-backed
  Undo, same-position/different-ID replacement and detached-editor write rejection.
- Reconstructing the whole document view without losing or duplicating the root route.
- Actual MainWindow registration, Dock active-tab events, docked Navigator/Inspector
  row focus, a real floating Dock Navigator HostWindow, and independent tab histories.
- Search TextBox, script editor, standalone AvaloniaEdit and the nested navigator
  number TextBox keeping their own Undo/Redo and focus without changing HL7 history.
  Empty independent stacks do not fall through to document history. These native
  text-history checks use the existing isolated raw-input test process/dispatcher.
- Exactly one transaction per key, including multiple same-message history entries,
  and all existing raw typing, paste, delete and undo-group safety checks.

Correction file inventory (in addition to the existing foundation work):

| Area | Files |
| --- | --- |
| Single window routing authority | src/HL7Workbench.Desktop/DocumentHistoryShortcuts.cs (new) |
| Main/floating root registration | src/HL7Workbench.Desktop/MainWindow.Editing.cs; MainWindow.Shell.cs |
| Removed local handlers/RAW ownership | src/HL7Workbench.Desktop/Viewer/FileDocumentView.cs; MessageInspectionView.cs |
| Permanent tests | tests/HL7Workbench.Desktop.Tests/StructuralHistoryViewTests.cs; RawEditingCrashTests.cs; EditableViewerTests.cs |
| Acceptance/context | docs/stage8b-structural-editing-acceptance.md; docs/PROJECT_CONTEXT.md |

Focused correction rerun commands (after the locked restore/Release build above):

~~~powershell
dotnet tests/HL7Workbench.Desktop.Tests/bin/Release/net10.0/HL7Workbench.Desktop.Tests.dll -class HL7Workbench.Desktop.Tests.StructuralHistoryViewTests -class HL7Workbench.Desktop.Tests.RawEditingCrashTests -class HL7Workbench.Desktop.Tests.EditableViewerTests -noLogo -noColor -result-xml artifacts/stage8b/keyboard-focused-desktop.xml
dotnet tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/HL7Workbench.Workspace.Tests.dll -class HL7Workbench.Workspace.Tests.StructuralEditingTests -class HL7Workbench.Workspace.Tests.StructuralSaveTests -class HL7Workbench.Workspace.Tests.EditableDocumentTests -noLogo -noColor -result-xml artifacts/stage8b/keyboard-focused-workspace.xml
~~~

Correction verification on 2026-10-17:

| Check | Result |
| --- | --- |
| Locked solution restore | Passed |
| Release solution build | Passed; zero warnings/errors |
| Focused Desktop history/native-input/reconstruction | 9 passed; zero errors/failed/skipped; 14.428 s |
| Relevant Workspace editing/history/structural save | 83 passed; zero errors/failed/skipped; 28.374 s |
| Full automated solution suite | 1,175 passed; zero failed/skipped; 102.101 s |
| git diff --check and new-file whitespace/conflict scan | Passed |
| Existing HL7 fixtures | All eight match HEAD blob hashes using --no-filters |
| Git | main/HEAD unchanged; empty staging area |

The two new Inspector/Navigator theory cases raise the full ordinary count from
1,173 to 1,175. Existing tests were expanded and retained. The foundation stress
record above remains historical; this Desktop-only correction did not rerun the
opt-in stress suite. Focused Desktop and Workspace XML/logs and the full solution
log are retained under artifacts/stage8b/keyboard-*. The initial failing platform
reproduction and test-harness adjustments were reviewed before final passing runs.
No disposable probe/export was created or permanent coverage removed.

The complete milestone working tree now contains 26 tracked modifications and ten
new implementation/documentation/test files (36 files), plus the pre-existing
untracked samples/synthetic-only/stage5b-repeated-patients_TEST.hl7. That user file
is preserved and is outside the correction inventory. Existing ignored artifacts,
bin/obj outputs and earlier verification reports are retained.

The user subsequently accepted this correction through native Windows testing.
Stage 8B.4 was authorized later; see the linked compatibility record. Stage 8C/8D
and Git mutations remain unauthorized.

## Native Windows acceptance and keyboard retest

Start the verified development build; do not publish/package:

~~~powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
~~~

1. Open samples/synthetic-only/stage5b-repeated-patients.hl7.
2. Enable Edit Mode. Make a small clearly synthetic edit in message 1; edit message 2.
   Navigate back to message 1 and turn Edit Mode off.
3. Focus RAW once, then Ctrl+Z must select message 2 and undo its edit. Immediately
   press Ctrl+Z again without clicking or refocusing: it must select message 1 and
   undo its edit. Check raw text, position/count, Inspector and dirty title each time.
   To exercise Redo after navigation too, navigate to message 2 and focus RAW once.
   Ctrl+Y must select/reapply message 1; immediately Ctrl+Y again must select/reapply
   message 2 without clicking or a native undo-group crash.
4. Repeat with toolbar Undo/Redo, document tab switches, visible Navigator/Inspector,
   docked/floating tools, Light and Dark. Check menus/tooltips do not act on stale content.
   Focus Navigator/Inspector rows, keep Edit Mode off, and repeat consecutive keyboard
   Undo/Redo. Confirm only the active document changes, including after tab switches
   and layout/view reconstruction. Focus must not be moved into RAW by the shortcut.
   Type synthetic text in Find Messages, Script Editor and the message-number input;
   their Ctrl+Z/Y must affect only their own input, including when their undo stack is
   empty. Repeat from a source-backed active message with an earlier bounded edit.
5. Save As to a new local synthetic output, change text, Save again, undo across the save,
   Save and reopen. Check exact expected content and destination behavior. Cancel a
   native Save As/close prompt and confirm all drafts/history remain.
6. Open a disposable synthetic copy. Change that copy externally after opening and try
   associated Save. It must refuse; Save As to a different path must remain available.
   Do not perform this check against user data or modify committed fixtures.
7. On an existing large synthetic fixture, keep it source-backed, edit a supported small
   message and save. Confirm responsiveness, source access and editing restoration.
   Save cancellation is available through existing API cancellation tokens; this slice
   does not add a new Cancel Save command.

Insertion-specific native UI acceptance waits for 8C; do not claim manual insertion
acceptance through a command that does not exist. Stage 8B.4's current retest is in
the linked compatibility record; stop before Stage 8C/8D.
