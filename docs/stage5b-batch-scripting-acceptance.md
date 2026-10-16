# Stage 5B unified C# scripting acceptance

The original split Message/Batch functionality passed native Windows acceptance.
The user authorized a unified redesign before commit and explicitly dropped syntax/
kind-metadata compatibility with disposable development scripts. **Final native Windows
acceptance passed on 2026-10-16**, including the save/dialog corrections. The user
authorized one milestone commit and normal push to origin/main; no tag/release or
history rewriting. Earlier pending checkpoints below are retained as verification history.

[ADR 0007](decisions/0007-unified-csharp-scripting.md) supersedes the lifecycle/type
decisions in historical ADRs 0005/0006. [Script API](script-api.md) documents phases,
examples, migration, bounds and export guarantees.

The desktop finishing pass adds a utility ribbon, Preferences, inline rename/delete
and contextual reports. The final permanent menu is File | Settings | View; Home holds
all layout actions, and the Scripts ribbon holds every former Scripts menu command. Use the current
[ten-step UI acceptance checklist](stage5b-ui-finishing-acceptance.md) alongside the
execution/safety checks below. Report selection/export now live in Reports / Export CSV,
available from Scripts ribbon → Reports and editor ⋯; they are no longer permanent toolbar controls.

## Verification

The final save-dialog/confirmation correction supersedes the earlier no-overwrite rule.
See [save/export safety and native retest](save-and-export-safety.md) and the current
UI checklist for its verification results and remaining acceptance gate.
Its final locked restore, zero-warning/error Release build, 80 focused checks, all 802
automated cases, all 333 Workspace stress cases, refreshed folder publish and full
Windows PowerShell 5.1 worker/hash/dependency verification passed. Diff checks passed;
no staging/commit/push/tag/release. The checkpoints below precede these final corrections.

Final menu cleanup verification: locked restore, zero-warning/error Release build,
ten focused checks and all 769 automated cases passed. Stress/publish were not rerun for
this menu-only change. The UI checklist records remaining native checks and command coverage.

Earlier UI finishing verification: 762 automated / 320 Workspace stress cases passed,
locked restore and zero-warning/error Release build passed, folder publish and full
worker/hash/dependency verification passed in Windows PowerShell 5.1 and PowerShell 7.
See the [finishing checklist](stage5b-ui-finishing-acceptance.md) for coverage and files.

Prior unified checkpoint on 2026-10-15 (before UI finishing) passed:

- Locked restore and Release build: zero warnings/errors.
- Full suite: 743 passed, zero failed/skipped. There are 23 new unified lifecycle
  cases; obsolete kind-only workflows were replaced. Preservation, worker safety,
  reports, ownership, migration, dirty transitions and native-tool coverage remain.
- Workspace stress: 306 passed, zero failed/skipped, including 8,192 stateful messages,
  the 4,096-message stream and 256/512 MiB fixtures.
- Folder publish and published worker smoke: identical script with one/two targets,
  shared state, CSV/completion, Hold/Emit, clean exit, worker hashes and dependency isolation.
- `git diff --check` and full inspection of 19 modified tracked / 16 untracked files.
  Nothing staged; main remains at 6dfa9f4. No commit/push/tag/release.

The ignored temporary `artifacts/unified-probe/` project was removed after review;
permanent lifecycle tests and publish smoke cover its findings. The existing synthetic
CSV export was retained. Prior checkpoints were 714 automated / 304 stress cases for
original Stage 5B, then 722 / 306 for split-editor polish. Native unified acceptance
is still pending.

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
$env:HL7WORKBENCH_STRESS = '1'
dotnet test --project tests/HL7Workbench.Workspace.Tests/HL7Workbench.Workspace.Tests.csproj -c Release --no-build --no-restore
Remove-Item Env:HL7WORKBENCH_STRESS
dotnet publish src/HL7Workbench.Desktop/HL7Workbench.Desktop.csproj -c Release --no-build --no-restore -o artifacts/stage5b-publish
./build/VerifyScriptPublish.ps1
git diff --check
git status --short --untracked-files=all
```

## Native Windows steps

Run the freshly built app from C:\Dev\HL7Workbench:

```powershell
Set-Location C:\Dev\HL7Workbench
New-Item -ItemType Directory -Force test-data/generated
Get-FileHash samples/synthetic-only/stage5b-repeated-patients.hl7 -Algorithm SHA256
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

Use only reviewed synthetic fixtures. Save generated output under test-data/generated.
The fixture SHA256 is `8ABFC4D2F05E9FBB47D9E6716BFEBCBAF2092BFE6B1EE693FC9AB3C3ADFA8086`.
Before each Run, select the original fixture's document tab: a successful run selects
its new output. Compare the source hash again in a second terminal or after app exit.

1. **Unified tool.** View → Script Editor remains a dockable/floating tool. Scripts ribbon → New (editor New)
   creates a Main starter without a type choice. No kind prefixes or Change Script Type
   action remain. Apply to: offers Current message / Whole document. Check narrow
   toolbar scrolling, float/dock/hide/reopen, layout restore, keyboard focus, Ctrl+S,
   script-name editing, lexical/semantic colors, font preferences/shortcuts and tooltip
   discovery/dismissal. Layouts/preferences must preserve their existing behavior.

2. **Same script, either scope.** Open the three-message fixture. Duplicate the simple
   bundled transformation or paste:

   ```csharp
   void Main()
   {
       Message.SetText("PID-5.1", "SYNTHETIC");
   }
   ```

   Current message produces one dirty independent output; Whole document produces three.
   Reuse identical source text for both. Source file/hash, draft, undo and save destination
   stay untouched. Test supported unsaved source edits in each scope. Output requires its
   own Save As and normal close prompts.

3. **Shared state, helpers and completion.** New Script, paste:

   ```csharp
   int count = 0;
   var report = Batch.Report("summary", "Messages");
   string Name(int value) => "SYNTHETIC" + value;
   void Main()
   {
       count++;
       Message.SetText("PID-5.2", Name(count));
   }
   void Complete()
   {
       report.Row(count.ToString());
   }
   ```

   Whole document gives names SYNTHETIC1/2/3 and one summary row 3. Another Run restarts
   at 1. Current scope gives SYNTHETIC1 and report 1. Initialize/report headers occur once;
   completion produces exactly one row. Local variables declared inside Main reset per call.

4. **Conservative patient example.** Run the immutable consistent-patient example on
   Whole document: messages 1 and 3 share SYN-P000001 / SYNTHETIC^PATIENT000001;
   message 2 gets SYN-P000002. Summary CSV is exactly:

   ```csv
   "Patients","Messages"
   "2","3"
   ```

   Missing/null/empty identifiers, PID repetitions, multiple/missing PID or diagnosed
   malformed input must fail the entire run. Different raw assigning authorities map
   separately. Source mappings never enter report/logs. This is not complete clinical
   de-identification or a HIPAA guarantee.

5. **Library and obsolete records.** Save/reopen/rename/duplicate unified scripts;
   exercise Save/Discard/Cancel and dialog dismissal with dirty script/HL7 drafts.
   Bundled examples stay immutable until Duplicate. Existing development version-1/2
   records appear as Needs update, retain their original source/files and cannot Run.
   Rewrite to Main/Complete, then Save Script explicitly; identity/name persist and the
   warning clears. Duplicate of an obsolete entry retains its warning until Save.
   Unrelated font/layout/application settings must remain intact. Do not delete the library.

6. **Ownership and export.** After successful reports, open Scripts / ribbon / ⋯ → Reports /
   Export CSV. The window and action tooltip identify the producing script's execution-time name and scope,
   with no kind. Switch/rename scripts and reconstruct the tool: origin/reports persist.
   Export to a new file, then replace a disposable existing file after explicit picker
   confirmation. Decline/cancel must preserve existing bytes. Changed destinations must
   fail safely and require fresh selection; failed exports retain the completed reports.
   Cancel the picker or use an unavailable destination, then retry. No partial destination.
   A later successful report run replaces origin; success with no reports clears it.

7. **Entry diagnostics and phase rules.** Try missing Main, overloaded Main, int Main,
   async void Main, Task Main and parameterized Complete. All fail before initialization,
   with fixed compiler/entry codes and original locations. Double-click a diagnostic.
   Message.Read in top-level initialization or Complete fails LifecyclePhase rather than
   resolving a stale message. Helper methods called by Main must resolve the current message.

8. **Rollback/cancellation/crash.** Run:

   ```csharp
   var report = Batch.Report("unpublished", "Count");
   void Main() { report.Row("1"); Message.SetText("PID-5.2", "SYNTHETIC"); }
   void Complete() { throw new Exception("SYNTHETIC-PRIVATE"); }
   ```

   No new document/reports; previous successful report origin remains. Raw exception text
   must not enter diagnostics/Activity Log. Move the throw into Main: Complete must not
   run. Replace a body with while(true) {} and Cancel, close the source/tool or wait for
   the ten-second operation timeout. Replace a body with Environment.Exit(17): app survives,
   worker exits, staging rolls back. A subsequent normal run succeeds.

9. **Deferred output and bytes.** Run void Main() { Batch.Defer(Message); Message.Exclude(); }
   on the plain fixture: each appears once in source order. Without Exclude, normal output
   precedes the deferred copies. Try the Hold/Emit completion-decision example in the API:
   its first two selected messages are emitted in reverse order. Repeat with synthetic
   MLLP envelopes. Unsafe wrappers/missing terminators fail explicitly. A no-op Main
   round-trips exact bytes/BOM/terminators across supported encodings. Oversized source-backed
   previews fail, never become abbreviated output. Recheck raw editing/undo, navigation,
   inspection/source viewer, document saves and floating-tool shutdown.
   For an exact-byte check, run `void Main() { }` on the original fixture with Whole
   document, Save As `test-data/generated/unified-noop.hl7` and compare its SHA256
   with the source above. Choose a fresh destination for each export/save check.

10. **CSV escaping and safety.** Define two columns; in Complete append
    report.Row("患者,\"synthetic\"\r\nline", null) and report.Row("=1+1", "").
    Export and inspect as text: quotes doubled, multiline cell quoted, Unicode preserved,
    null/empty quoted empty, formula prefixed with an apostrophe. No viewer/evaluation is added.

## Limits and acceptance boundary

Main/Complete are synchronous; background side effects cannot be rolled back. There is
one transient last-successful report set, no persistent history. Exports are atomic per
new destination, not across files and app documents. Per-operation report buffering is
256 KiB; stream rows during Main. Windows Job Object containment is not a security sandbox.
Deferred output requires original extractable framing and existing quotas; no source-free
generation, structural insertion/removal, cross-document or network scripting is included.
Record native results and obtain separate explicit commit authorization before Git changes.
