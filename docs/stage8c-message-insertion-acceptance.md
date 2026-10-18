# Stage 8C — message insertion UI acceptance

Stage 8C is implemented from main at
`16ebc6d2a2a9d36ddfb20c766976c18990846bb2` (the native-accepted Stage 8B checkpoint).
On 2026-10-18 the user confirmed successful native Windows Stage 8C acceptance and
authorized exactly one local checkpoint commit containing only this accepted work:
`feat: add Stage 8C message insertion UI`. This supersedes earlier pending/no-commit
statements below. Final checkpoint verification is recorded separately below.
Stage 8D may begin only after the mandatory post-commit gate passes; it is excluded
from this commit. No push, tag, package, deploy, release or publication is authorized.
The native checklist remains a permanent regression reference.

## User behavior

- **Next Message (`>`)** navigates normally among existing messages. At unfiltered
  logical EOF it inserts one blank draft through `MessageDocument.InsertMessage(End)`.
  The same navigator command handles pointer and keyboard activation; no second
  navigation implementation or new global keyboard shortcut is introduced.
- A current trailing inserted draft containing only Unicode whitespace, including
  CR, LF and tab, is reused by Next. This reads the document's retained working text,
  including pending drafts and Edit Mode off, rather than an editor preview or parse.
  Any non-whitespace text, including incomplete `MSH`, permits another append. This
  guard is not an HL7 validity check. Explicit Add Message may deliberately add
  multiple blank drafts; they still need completion or Undo before safe publication.
- With a filter active, Next navigates matching IDs only and never appends, including
  at actual logical EOF or with zero matches. Clear the filter before EOF append.
  Explicit Add Message is a deliberate logical insertion; it invalidates stale
  discovery/filter results and retains the authored query under the accepted 8B.4 policy.
- **Add Message** is available in Home → Add Message, File → Add Message and the
  existing navigator. Before Current/After Current use the selected logical ID;
  Beginning/End use the entire current logical order. Original file ordinals are
  never insertion coordinates. Beginning/End create the first draft for a valid
  empty/BOM-only file; Before/After are disabled with no current message. Opaque
  unclassified content is not treated as an empty document.
- Successful insertion selects the new stable ID, updates position/count, activates
  session Edit Mode and focuses the blank HL7 editor after menus dismiss. The saved
  Start in Edit Mode preference stays unchanged. Merely navigating does not enable
  Edit Mode. A rejected insertion preserves selection, history, dirty state and focus.
- Capability checks disable choices at the 100,000-message limit, during preparation,
  saving/capture, for unsupported source boundaries/encodings and at existing history
  limits. The document API retains its authoritative atomic rejection checks. Feedback
  is inline or in the disabled control's tooltip, without keystroke warning dialogs.

## History, paste and lifecycle

Insertions reuse the accepted chronological document history. Undo removes the
insertion; Redo restores the same ID. Paste/edit is a later history operation, so
Undo first reverses it, then insertion. New mutation invalidates Redo. Save points
retain exact membership/text semantics across Undo/Redo. The accepted window-root
Ctrl+Z/Ctrl+Y authority is unchanged, including focus loss, Inspector/Navigator,
Edit Mode off, multiple tabs and floating tools. Independent text inputs retain
their own commands. No local Undo/Redo route or input-time undo-stack clearing is added.

Editable in-memory originals now use the same stable document host as file-backed
tabs. Otherwise insertion/history could select a different ID while the old single
inspection view stayed mounted. View reconstruction and message changes use the
existing identity-based replacement. Low-level inspection tests now construct the
active inspection control explicitly, retaining every original behavioral assertion.
The Dock integration test verifies the production host.

The new real clipboard regression demonstrated a correctness defect in the default
AvaloniaEdit Paste path: it normalized mixed newlines (and can expand tabs). See the
[pinned dependency implementation](https://github.com/AvaloniaUI/AvaloniaEdit/blob/86fdebec4cff7affb0e14c7885df71d28edce777/src/AvaloniaEdit/Editing/EditingCommandHandler.cs#L475).
The HL7 editor now owns a per-instance Paste command binding ahead of the default.
It retains clipboard text through the public selection replacement API inside the
existing document update transaction. Typing, IME and other inputs retain their
existing handlers. No script/search/independent editor binding changes.

Clipboard retrieval does not hold an open editor transaction across its await.
Before applying, it checks message ID, content generation, selection/caret, edit
availability, visibility, request generation and attached-view lifetime. A newer
request, mutation, navigation, hidden/detached/reconstructed view or mode change
cannot retarget or overwrite newer work. Oversized input is rejected before editor
mutation; existing multiple-message/draft budget validation remains authoritative.
Native Ctrl+V is tested in a fresh child process because AvaloniaEdit's default
static key bindings belong to the creating UI dispatcher, matching the established
raw-input test harness. The child uses real defaults, without replacing key maps.

The rejected multi-message-paste regression also exposed an existing input restore
defect: assigning TextEditor.Text calls UndoStack.ClearAll inside an open input
group. Both restoration and document-driven synchronization now replace text through
TextDocument instead. Local history remains disabled before input; no stack clearing
occurs during a native transaction. The permanent rejection case verifies unchanged
working state, no added history, same insertion ID and later successful paste/Undo.
The existing three top-level menus are preserved; no new top-level menu is needed.

## Save and compatibility

No blank content is manufactured, auto-saved or appended to the original file.
Blank/invalid inserted drafts retain the accepted save restrictions. A missing
recoverable MSH start now identifies the offending logical message in Save feedback.
Failed validation preserves all working text, dirty state and the destination.
Complete the draft or undo its insertion. There is no partial destination replacement.

The existing validated streaming plan, staging/index membership proof, byte checks,
associated-destination identity protections and atomic publication remain. BOMs,
original regions, supported MLLP framing, custom delimiters and mixed inserted
terminators are preserved where the established boundary validation proves safety.
Unsupported batch/provisional/ambiguous containers remain unchanged and cannot insert.
An unterminated unsafe join is rejected rather than silently repaired.

Search results/filter membership, comparison selections, script captures and Inspector
continue using the 8B.4 stable-ID contracts. An undone insertion is unavailable to
comparison, never retargeted; Redo restores that ID. Inserted messages have no original
provenance, even after Save. Current/whole script scopes validate their selected/live
drafts under existing policy. Independent captured output survives later source
insertion with explicit stale-source attribution. Close prompts see unsaved insertions.

## Bounds and permanent coverage

The 100,000-message logical limit, 1 Mi UTF-16 complete-editor limit, 16 Ki line limit,
32 MiB retained edit/history character-storage budget and 4,096 history-entry limit
remain. Original sources stay lazy; insertion performs only the accepted bounded
metadata splice and allocates its blank draft/view. Command readiness reads metadata
and retained budgets; immutable source-boundary metadata is checked once and cached.
No synchronous source-file scan, document-wide decoded string, new Core parser,
dependency or architectural ADR is introduced. Active-manager notifications now
include save/capture availability so shell commands update their busy state.

New permanent coverage lives in:

- `MessageInsertionViewTests.cs` and `.Compatibility.cs`: navigation/guard, four
  positions, zero-message states, focus/edit preference, structural/text history,
  filter EOF, safe rejected saves, BOM/MLLP/custom delimiters, Save/reopen/save points,
  external changes, close policy, stable search/comparison/Inspector/script behavior,
  99,999/100,000-message bounds, delayed/oversized/multi-message paste and a source-backed
  16 MiB insertion/streaming-save byte-hash proof.
- `MessageInsertionNativeInputTests.cs`: real pointer Next, no-editor-click Ctrl+V,
  exact mixed terminators/tabs and consecutive native Undo/Redo of paste/insertion.
- `MessageInsertionShellTests.cs`: shared menu/ribbon commands, active-tab scoping,
  collapsed/pinned flyout dismissal and editable focus.
- Workspace `InsertionReadinessTests.cs`: pending draft semantics, empty/BOM-only
  sources, save-time readiness notification, offending message and atomic rejection.

All prior tests, including native transaction safety and root keyboard routing,
remain. No assertions are weakened or permanent fixtures removed. None of the eight
tracked HL7 fixtures are edited. The pre-existing untracked
`samples/synthetic-only/stage5b-repeated-patients_TEST.hl7` is hashed only and untouched.

## Automated verification

The accepted baseline was 1,283 full-suite tests and 646 Workspace stress tests.
The new coverage adds 34 Desktop and 10 Workspace cases; every previous case remains.

| Check | Result |
| --- | --- |
| Locked restore | Passed |
| Release build | Zero warnings/errors; 1.87 s |
| Focused Workspace | 90 passed; zero errors/failures/skips/not-run; 42.741 s |
| Focused Desktop | 78 passed; zero errors/failures/skips/not-run; 29.166 s |
| Full solution | 1,327 passed; zero failures/skips; 133.723 s |
| ScriptHost Release-layout smoke | Both current/whole lifecycles passed, including naming, hashes and isolation |
| Complete Workspace stress | 656 passed; zero errors/failures/skips/not-run; 254.793 s |
| Fixture integrity | All eight original HL7 blobs unchanged; excluded user file SHA-256 unchanged |
| Whitespace/conflict checks | Passed, including untracked milestone files |

Telemetry opt-outs apply in each verification shell. Full/focused Workspace and
stress suites run sequentially because some existing generated fixture paths are shared.

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
dotnet tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/HL7Workbench.Workspace.Tests.dll -class HL7Workbench.Workspace.Tests.InsertionReadinessTests -class HL7Workbench.Workspace.Tests.StructuralEditingTests -class HL7Workbench.Workspace.Tests.StructuralSaveTests -class HL7Workbench.Workspace.Tests.StructuralCompatibilityTests -noLogo -noColor -result-xml artifacts/stage8c/focused-workspace.xml
dotnet tests/HL7Workbench.Desktop.Tests/bin/Release/net10.0/HL7Workbench.Desktop.Tests.dll -class HL7Workbench.Desktop.Tests.MessageInsertionViewTests -class HL7Workbench.Desktop.Tests.MessageInsertionNativeInputTests -class HL7Workbench.Desktop.Tests.MessageInsertionShellTests -class HL7Workbench.Desktop.Tests.StructuralHistoryViewTests -class HL7Workbench.Desktop.Tests.RawEditingCrashTests -class HL7Workbench.Desktop.Tests.EditableViewerTests -class HL7Workbench.Desktop.Tests.Stage3ViewTests -class HL7Workbench.Desktop.Tests.SearchToolTests -class HL7Workbench.Desktop.Tests.ComparisonTests -class HL7Workbench.Desktop.Tests.ViewerTests -noLogo -noColor -result-xml artifacts/stage8c/focused-desktop.xml
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
& .\build\VerifyScriptPublish.ps1 -PublishDirectory src/HL7Workbench.Desktop/bin/Release/net10.0
$env:HL7WORKBENCH_STRESS = '1'
dotnet tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/HL7Workbench.Workspace.Tests.dll -showLiveOutput -noLogo -noColor -result-xml artifacts/stage8c/workspace-stress.xml
Remove-Item Env:HL7WORKBENCH_STRESS
git diff --check
```

The smoke checks the existing Release layout; it creates no publish/package artifact.
Useful initial failure diagnostics, final logs/XML, hashes and inventories remain
under ignored `artifacts/stage8c`. Pre-existing build/stress artifacts are preserved.
Only test-owned temporary GUID directories are cleaned by their owning tests.

No cases are excluded from these final runs. Opt-in Workspace stress was enabled
for the complete stress run, including all existing 100/256/512 MiB insertion,
byte-preserving streaming saves and lazy capture cases plus maximum-count history.
Observed large-save allocations remained bounded (386,328–423,384 bytes in the
reported 100–512 MiB first-save cases); these observations are not timing guarantees.
The new headless source-backed insertion case separately enforces an 8 MiB operation
allocation ceiling for its 16 MiB source and hashes the entire retained original prefix.

Final scope: 30 intended paths, comprising 12 implementation, 14 permanent test/setup
and four documentation paths (21 modified tracked, nine new). The exact inventory
and final status are retained in `artifacts/stage8c/changed-files.txt` and
`artifacts/stage8c/final-status.txt`. Core, dependency files, scripting API/protocol/
worker and the accepted root history router are unchanged. Git HEAD remains
`16ebc6d2a2a9d36ddfb20c766976c18990846bb2`, the index is empty and the excluded
untracked user HL7 is unchanged. Nothing is staged or committed.

## Native acceptance and checkpoint retest — 2026-10-18

The user confirmed successful native Windows Stage 8C acceptance and authorized one
local checkpoint. The complete audit matched the 30 intended files and saved hashes;
the pre-existing user `_TEST.hl7` remained untouched and untracked. No generated output,
log, local configuration, credential or unrelated file belongs in the checkpoint.
Only this document, PROJECT_CONTEXT.md, architecture.md and README.md received
acceptance/authorization notes during checkpoint preparation; source/tests did not change.

Fresh final verification passed: locked restore; Release build with zero warnings/
errors (1.11 s); all 1,327 solution tests (zero failed/skipped, 122.899 s); complete
Workspace stress (656 passed, zero errors/failures/skips/not-run, 213.233 s); both
ScriptHost smoke lifecycles against the existing Release layout; all eight tracked
fixture blob hashes; excluded user file SHA-256; and whitespace/conflict checks.
Logs, XML, explicit allowlist and hashes are retained in ignored
`artifacts/stage8c-checkpoint`. No packaging or publication is performed by the smoke.
The commit carrying this record contains only accepted Stage 8C work. Its Git hash
and the post-commit gate are recorded after creation. Stage 8D begins only after the
clean tracked tree/empty index/unchanged excluded file gate passes.

## Native Windows acceptance — permanent retest steps

Run the normal Release desktop app without a debugger. Use only reviewed synthetic
files and a new synthetic output destination. Do not overwrite the tracked fixture
or the excluded `_TEST.hl7`.

From `C:\Dev\HL7Workbench` in PowerShell:

```powershell
& .\src\HL7Workbench.Desktop\bin\Release\net10.0\HL7Workbench.Desktop.exe
```

1. Open `samples/synthetic-only/stage5b-repeated-patients.hl7`. Disable Edit Mode.
   Use `>|` to select the final message and note its position/count N. Previous/Next
   within existing messages must only navigate, with Edit Mode staying off.
2. At message N, click `>`. Expect message N+1 of N+1, a blank editable HL7 surface,
   dirty tab and Edit Mode on. Without clicking the editor, paste the synthetic HL7
   below. It must appear immediately. First also try `>` again while still blank:
   count/ID/selection must stay N+1 and paste must still need no editor click.
3. With content present, click `>` again. Expect a new blank at N+2. Press Ctrl+Z
   repeatedly without mouse interaction: remove N+2, reverse the prior paste, then
   remove N+1. Ctrl+Y restores each in order, with the same selection/numbering.
   Repeat with Edit Mode off and after focusing Segment Navigator/Field Inspector.
   Undo the final extra blank before continuing to Save.
4. Exercise Home → Add Message and File → Add Message and the navigator Add Message
   menu. Try Before Current, After Current, Beginning and End. Each must select a
   blank at the stated logical position, increment count once and accept input
   immediately. Undo each test insertion before trying the next. Repeat after an
   earlier completed insertion to verify shifted logical positions. Try both pinned
   and collapsed Home; the flyout must dismiss and leave editable focus.
5. Keep an inserted blank/incomplete draft and choose File → Save As to a new path.
   Expect a refusal naming its logical message, a dirty tab and retained edits. If
   replacing an explicitly selected disposable existing output, its bytes must remain
   unchanged. Closing the dirty tab must offer Save/Discard/Cancel; Cancel keeps it.
6. Complete every inserted draft with one valid synthetic message. Include a final
   segment terminator (press Enter at the end if needed). Save As a new local file,
   verify the tab becomes clean, close and reopen the saved copy. Verify count,
   selected content, custom delimiters if used, and original messages. Undo/Redo
   around a successful Save must mark dirty/clean according to actual contents.
7. Find Messages: search text `MSH`, enable Filter Navigator, navigate to the last
   match and click `>`. No message is added, including at logical EOF. Also try a
   query with zero matches. Clear the filter; Next at actual EOF may insert. While
   filtered, explicit Add Message clears stale results/filter but retains the query.
   Refresh search for `SYN-8C-001` after pasting; activate its result and verify the
   correct inserted message after additional position-shifting insertions.
8. Inspect the pasted PID/NTE via Segment Navigator/Field Inspector. Values/paths
   must refresh; a blank or partial draft must not expose stale values from the prior
   message. In Message Comparison choose the insertion as a source. Undo/Redo its
   insertion: unavailable must not become an unrelated message, and Redo restores
   availability. Original source for an insertion must remain unavailable after Save.
9. Create a new trusted synthetic script `void Main() { }` in Script Editor. Run it
   on a valid insertion with Current message, then Whole document. Outputs must be
   independent and include the expected captured logical messages. Select a blank
   insertion and rerun: controlled input failure, no partial output. A whole run with
   any live blank must fail under the existing validation policy. Do not alter bundled
   scripts. For stale capture, use `void Main() { System.Threading.Thread.Sleep(1000); }`,
   run, then insert in the source once capture finishes; completed output must show
   the accepted stale-source attribution and retain its captured input.
10. Open a second synthetic tab; insert and undo in each. Menu/ribbon/history must
    affect the active document. Float Navigator and test Ctrl+Z/Ctrl+Y there. Type in
    Find Messages and Script Editor, then undo there: their own text history must
    work without changing HL7 membership. Switch tabs or change selection while a
    clipboard operation is pending if reproducible; it must not paste into a new target.
11. Open the retained generated fixture
    `artifacts/stage3/synthetic-100MiB-104857600.hl7`, navigate to its second/final
    message, insert, paste a small complete message and Save As a disposable copy. UI must remain
    usable, large original payloads retained, and no attachment decoded. At 100,000
    logical messages (if a reviewed fixture is available), Add choices/EOF append must
    be unavailable with understandable feedback; existing navigation still works.
    The permanent Desktop cases already verify 99,999/100,000 messages without a
    native harness. Do not create large test data in a tracked fixture or user source.
12. Check Settings → Preferences → General: the saved Start in Edit Mode value must
    be unchanged by insertion. Native clipboard/IME, mouse focus, floating menus,
    monitor/DPI and popup behavior remain manual acceptance responsibilities.

```hl7
MSH|^~\&|SYN-8C|SYN|SYN|SYN|20261018000000||ADT^A01|SYN-8C-001|T|2.5
PID|1||SYN-8C-001||Synthetic^Example
NTE|1||Synthetic insertion acceptance
```

## Limitations and stop

No generator, template, automatic MSH, deletion/reordering UI, new scripting API,
large-message editor expansion, boundary repair or automatic Save is included.
Unsupported source containers and unsafe terminator joins remain rejected. A
non-whitespace partial draft is meaningful for the runaway guard only, and can
still block Save/whole scripting. Existing filesystem publication races and resource
limits remain as documented in ADR 0011. No performance timing is a guarantee.

Stop for native Windows acceptance. Do not stage, commit, push, tag, release,
package, publish or begin Stage 8D.
