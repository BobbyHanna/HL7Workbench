# Stage 5A: Message-script foundation acceptance

The user has completed this native Windows functional checklist and the final
Script Editor visual checklist in `stage5a-script-editor-visual-acceptance.md`.
The final tooltip discovery/dismissal checklist has also passed native acceptance.
The user has authorized one Stage 5A milestone commit and normal push to origin/main.
No tag, release or history rewrite is authorized.
See `stage5a-final-ci-investigation.md` for final verification and cleanup results.

## Automated checkpoints

1. Runtime/facade: the initial 26 worker tests passed, including compilation errors,
   safe runtime diagnostics, exact bytes across encodings, controlled writes, exclusion,
   cooperative cancellation, infinite loops, crashes and process cleanup.
2. Workspace: all 249 tests passed at this checkpoint (19 new execution cases), with
   zero-warning Release build. Covers working overlays, scope, MLLP envelopes, rollback,
   quotas, oversized source-backed input, encoded boundary collisions and ownership.
3. Desktop/library: the first complete integration passed 589 tests, followed by 593
   after additional runtime-location, lifetime and memory tests. Final verification
   passed all **594 tests** (211 Core, 262 Workspace, 93 Desktop, 28 Scripting; 67 new
   cases), locked restore, zero-warning/zero-error Release build and `git diff --check`.
   Workspace stress passed all **264 cases**, including the 256/512 MiB fixtures and
   4,096-message scripting stream. Framework-dependent folder publishing passed,
   and the deployed worker compiled, transformed, finished and exited successfully.

These headless tests do not establish native floating-window, focus, monitor/DPI or
Windows dialog behavior. See ADR 0005 for defaults, trust, preservation and deferrals.

## Start

From `C:\Dev\HL7Workbench`, in PowerShell:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

To repeat the additional stress/deployment verification separately:

```powershell
$env:HL7WORKBENCH_STRESS = '1'
dotnet test --project tests/HL7Workbench.Workspace.Tests/HL7Workbench.Workspace.Tests.csproj -c Release --no-build --no-restore
Remove-Item Env:HL7WORKBENCH_STRESS
dotnet publish src/HL7Workbench.Desktop/HL7Workbench.Desktop.csproj -c Release --no-build --no-restore -o artifacts/stage5a-publish
```

Use only `samples/synthetic-only/stage5a-scripting.hl7` or other reviewed synthetic
fixtures. Save native outputs under ignored `test-data/generated/`:

```powershell
New-Item -ItemType Directory -Force test-data/generated
Get-FileHash samples/synthetic-only/stage5a-scripting.hl7 -Algorithm SHA256
```

## Native checks

1. **Tool and focus.** Open the fixture. View → Script Editor opens a tool, not a
   document tab. Float/dock it, resize it, switch HL7 tabs and return. Focusing the
   tool retains the selected document. Scope and source-context labels remain clear.
   Check a narrow floating window and a second monitor/mixed DPI if available.

2. **Library and draft.** The bundled example is read-only. Duplicate it, change the
   name and script, Save Script, switch to the example, and reopen the copy. Rename
   again and save: the entry should change name without duplication. New creates a
   dirty blank draft. Close or switch with edits: test Cancel, dialog dismissal,
   Discard and Save. Reopen/float/reset the tool with a retained draft; text must
   survive. Restart after saving: local scripts persist. Unsaved drafts are not
   restored after a crash.

3. **Save shortcut.** Make both an HL7 edit and a script edit. Focus the script
   editor and press Ctrl+S. Only the script is saved; the HL7 tab stays dirty and no
   HL7 save picker appears. Focus the HL7 editor and verify its existing Save works.

4. **Current message and unsaved input.** Select the first message, enable Edit Mode,
   change the given name without saving, and run this script in Current message scope:

   ```csharp
   Message.SetText("PID-5.1", "SYNTHETIC");
   ```

   Expect one new dirty output document with the synthetic family name and the unsaved
   source given name. The source text, dirty state, undo history and save destination
   are unchanged. Save output through Save As into `test-data/generated/`, then reopen
   it. Closing output before saving offers Save/Discard/Cancel.

5. **Whole-document edits and filtering.** Run the duplicated bundled example on
   Whole document: expect three messages with the chosen name fields changed. Then run:

   ```csharp
   if (Message.Read("MSH-9.2").Raw != "A08") Message.Exclude();
   ```

   Expect one retained A08 in an independent document. Run `Message.Exclude();` on
   all messages: an empty unsaved output is still saveable and prompts on close.

6. **No-op bytes.** Use a blank or comment-only script on Whole document, Save As,
   and compare its SHA-256 to the original fixture. Repeat with existing synthetic
   custom-delimiter and UTF-16 fixtures; original files must never change. Compare
   a mixed-terminator fixture without editing it in a normalizing text editor.

7. **Diagnostics and late rollback.** Run `var invalid = ;`: expect a compiler code
   and line/column, with no output. Double-click its diagnostic. Run:

   ```csharp
   Message.SetText("PID-5.2", "SYNTHETIC");
   if (Message.Read("MSH-10").Raw == "SYN-002")
       throw new Exception(Message.Text("PID-3"));
   ```

   Expect no partial document, even though the first message succeeded. The runtime
   diagnostic and Activity Log must not show the exception's identifier value.
   Activity Log records fixed outcomes/counts, never script bodies, names, filenames
   or message content.

8. **Cancel, timeout and crash.** Run `while (true) { }`, press Cancel, and verify UI
   responsiveness, no output, and worker exit in Task Manager. Repeat and wait for
   the ten-second per-message timeout. Run `Environment.Exit(17);`: the worker stops,
   the app survives, and no output appears. A subsequent normal run must work. Only
   worker processes with `HL7Workbench.ScriptHost.dll` in their command line should
   appear, and none should survive job completion/cancellation.

9. **Close/shutdown.** During a run, close the Script Editor or its source document:
   execution drains/cancels and no partial output appears. Try application shutdown
   with a dirty script and a dirty HL7 document: choose Discard for the script, then
   Cancel on the HL7 prompt. The script draft must remain. Finish shutdown with the
   intended decisions and verify no worker remains.

10. **Explicit refusals.** Open an existing synthetic source-backed attachment fixture
    with a line longer than 16,384 characters and run a blank script: receive InputLimit,
    not abbreviated output. Try current extraction/filtering of a batch-wrapped file:
    receive ContainerExtraction. Whole-document no-op transformation preserves its
    wrappers. Incomplete/ambiguous envelopes and interior BOMs are refused explicitly.

11. **Storage and layout.** Save a script, then make its record unavailable/locked or
    change it from another app instance. A stale/failed save must retain the draft;
    Duplicate can save another copy. Save layout, restart, restore/reset layout and
    check dock/floating placement. Layout JSON must contain no script source or library
    paths. The Script Editor stays optional and does not reopen prior source content.

12. **Stage 1–4 regression.** Recheck raw typing/paste/delete/undo/redo, inspection
    continuity, message navigation, Full Source Viewer lifetime, source exports, Save
    As, dirty-close cancellation, existing tools and main-window shutdown with floating
    tools. No implicit source mutation or additional save should occur after scripting.

## API and limitations

Globals: `Message`, `Cancellation`. Core one-based paths are unchanged. `Read` returns
Missing/Empty/ExplicitNull/Value and original raw spelling. `Text` explicitly decodes
and refuses missing/null values or unresolved escape diagnostics. `SetText`, `SetRaw`,
`SetNull`, `Clear`, `Exclude`, and `Segments("PID")` are available; segment views use
relative paths such as `pid.SetText("5.2", "SYNTHETIC")`. Clear is not structural removal.

Only current-message and whole-document Message scripts run. Current scope captures
the entire backing file but executes only the chosen payload. Input/output staging
defaults to 1 GiB each. Batch callbacks/state, consistent patient identities, CSV,
generation/cloning, structural removal, cross-document inputs, working-state output,
full IntelliSense/debugging and untrusted-code sandboxing are not implemented.

The framework-dependent desktop and its worker need an installed .NET 10 runtime/
`dotnet` host. Folder deployment is supported; single-file/AOT/self-contained packaging
has not been established. Windows resource containment is not a security sandbox.
