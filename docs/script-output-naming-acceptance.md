# Script output naming acceptance

Checkpoint status, 2026-10-17: native Windows acceptance is user confirmed for this
enhancement and all Stage 8B work. One local checkpoint commit is authorized. Earlier
pending/no-commit statements below are historical; retain the native checklist for
regression retesting. See PROJECT_CONTEXT.md for final checkpoint verification.
No push, tag, publish, release or Stage 8C/8D work is authorized.

Authorized scope: suggested filenames for independent script output only. The user
accepted Stage 8B.1–8B.4 and the native Undo/Redo correction, intentionally uncommitted.
Starting main/HEAD: 8808929e844cb925fbdb11635e0b55740e5ec160. The starting tree has 60
Git status entries (39 modified tracked, 20 new milestone files and one user HL7 file).
The ignored artifacts/script-output-naming/baseline manifest and copies preserve that
starting tree; the user HL7 is hashed only. No staging, commit, push, tag, package,
release or publication is authorized. No Stage 8C/8D work is included.

## Behavior and boundaries

The Script Editor captures Session.Name with the script/scope at Run, separately from
DisplayName's dirty marker. Workspace normalizes it immediately, before any job queue
wait. Defaults are `Patient Deidentifier.hl7`, `Normalize Phone Numbers.hl7`, etc.;
an absent/invalid name becomes `Script Result.hl7`. The worker does not need the
library name or access to library persistence.

Batch.SetOutputFileName is advisory in initialization/Main/Complete. The last valid
suggestion wins, including in Complete. Invalid values leave it intact; with no valid
override the captured default wins. A bounded boolean yields a fixed successful-run
notice; neither invalid strings nor filenames enter Activity Log. SuggestedFileName
and IgnoredOutputFileNameSuggestion are captured result/retained attribution metadata.

The shared, filesystem-free Scripting policy caps the normalized name at 128 UTF-16
units including `.hl7`. It appends the extension or standardizes its case, retains
other dots/extensions as stem text, and rejects paths, invalid Windows characters,
reserved devices, controls/formats, unmatched surrogates, leading/trailing whitespace,
trailing dots and empty/overlong names. See script-api.md for the precise policy.
It uses ordinal comparisons, not locale or filesystem state, and never truncates or
extracts a basename from a supplied path.

The worker's existing v2 Finished frame gains optional OutputNaming metadata with
Version=1, a nullable filename and an ignored-suggestion flag. Initialization/Main
frames do not publish naming state. Parent validation repeats the filename policy;
unknown naming versions/invalid imported names are ignored with a notice. Missing
metadata uses the captured default. Older readers ignore this additive field; API 2,
frame version 2, library schema 3, bounds/dependencies and worker lifecycle remain.
The release-worker smoke verifies both current/whole lifecycles, Complete override,
invalid-suggestion retention, reports, deferred output, clean exit and matching hashes.
It runs against the normal Release deployment layout; no publish command is needed.

Workspace assigns the validated name only to the completed, indexed, independent
output. Title is the underlying suggestion; DisplayTitle alone adds duplicate-tab
suffixes and the existing dirty marker. DocumentManager refreshes those suffixes on
open/close/title changes, reserving bare names for saved/source documents. IDs and
suggestions do not change when labels are renumbered. SaveFilePicker.Hl7Options passes
Title to the existing native picker. Actual Save As retains the existing validation,
consent, atomic publication and post-save naming behavior.

Source content/title/Save destination, stable identity, streaming captures, stale-source
publication and report ownership are unchanged. No filename is inserted into HL7 bytes,
no directory is chosen and no automatic save or new filesystem API is added.

## Permanent coverage

- Scripting OutputNamingTests cover normal/Unicode names, invariant comparisons,
  unsafe/path/device/control values, length and surrogate boundaries, repeated
  suggestions, all lifecycle phases, parent validation and optional/versioned v2 frames.
- Workspace ScriptExecutionTests.OutputNaming cover defaults/fallbacks, overrides,
  ignored invalid suggestions, distinct duplicate documents, display-only suffixes,
  source identity/destination, unsaved/Save As transitions, byte preservation, captured
  stale-source mapping/reports and failure/cancellation without publication.
- Desktop ScriptEditorTests.OutputNaming cover execution-time name capture despite
  programmatic rename/selection changes, Complete overrides, visible stale attribution,
  retained report/name metadata, real Dock document titles, duplicate tabs, unsaved
  script markers, actual Save As option construction and save transitions.
- Existing tests are retained, including native raw transaction safety and consecutive
  window-root Undo/Redo. No old assertions are weakened or suppressed.

## Verification

| Check | Result |
| --- | --- |
| Locked restore | Passed |
| Release build | Passed, zero warnings/errors |
| Focused Scripting (entire project) | 148 passed, zero errors/failures/skips; 21.187 s |
| Focused Workspace scripting/compatibility | 98 passed, zero errors/failures/skips; 76.807 s |
| Focused Desktop scripting/compatibility/history/save | 64 passed, zero errors/failures/skips; 35.900 s |
| Worker deployment smoke | Passed both 1- and 2-message lifecycles against Release output |
| Full solution | 1,283 passed, zero failures/skips; 120.245 s |
| Complete Workspace stress | 646 passed, zero errors/failures/skips; 264.521 s |
| Fixture integrity | All eight tracked HL7 blobs match HEAD; user HL7 and protected routing/safety files match baseline hashes |
| Whitespace | git diff --check and all 20 naming paths pass |

The enhancement adds 69 registered cases (53 Scripting, 13 Workspace, 3 Desktop),
retaining all 1,214 prior cases. The initial build's six cancellation-analyzer warnings
in the new test helper were corrected by explicit cancellation propagation; no warning
or assertion was suppressed, and the final Release build has zero warnings/errors.
Stress retains the 100,000-message identity/history/mapping and 100/256/512 MiB lazy
capture/repeated lossless-save checks. Timing and filesystem/GC caches are uncontrolled;
the measurements in workspace-stress.log are observations, not performance guarantees.
Git's existing LF/CRLF conversion advisories are retained with the exact diff check;
no whitespace failures or line-ending normalization are introduced.
Useful reports remain in ignored artifacts/script-output-naming. Run full/focused
Workspace/stress sequentially because large fixture paths are shared.

~~~powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
& .\build\VerifyScriptPublish.ps1 -PublishDirectory src/HL7Workbench.Desktop/bin/Release/net10.0
git diff --check
~~~

Naming changes are limited to these paths; the accepted starting tree is distinguished
by artifacts/script-output-naming/baseline/manifest.json and changed-files.txt:

| Area | Files |
| --- | --- |
| Naming policy and facade | src/HL7Workbench.Scripting/ScriptOutputFileName.cs (new); ScriptBatch.cs |
| Worker metadata | src/HL7Workbench.Scripting/ScriptProtocol.cs; ScriptWorkerSession.cs; src/HL7Workbench.ScriptHost/Program.cs |
| Workspace publication/display | src/HL7Workbench.Workspace/ScriptExecution.cs; MessageDocument.Scripting.cs; MessageDocument.Editing.cs; DocumentManager.cs |
| Desktop name capture/picker | src/HL7Workbench.Desktop/ViewModels/ScriptEditorViewModel.cs; Viewer/SaveFilePicker.cs; MainWindow.Editing.cs |
| Permanent tests | tests/HL7Workbench.Scripting.Tests/OutputNamingTests.cs (new); tests/HL7Workbench.Workspace.Tests/ScriptExecutionTests.OutputNaming.cs (new); tests/HL7Workbench.Desktop.Tests/ScriptEditorTests.OutputNaming.cs (new) |
| Worker verification | build/VerifyScriptPublish.ps1 |
| Documentation | docs/PROJECT_CONTEXT.md; docs/script-api.md; docs/architecture.md; this document (new) |

Abbreviated paths in a row share the explicitly named directory. Source Core, package/
lock files, dependency versions, Script Library persistence and .gitignore are unchanged.
MessageDocument.Editing changes only the display-title expression; MainWindow.Editing
reuses the identical Save As options through a testable helper. Their accepted raw-input,
history, shortcut and save transaction behavior remains; protected routing/safety code
and tests are retained. No existing permanent test file is modified by this enhancement.

The naming delta spans **20 files**, including five new permanent files. All 60
starting status paths remain. The cumulative uncommitted tree has **46 modified
tracked files and 25 new milestone files**, plus the pre-existing untracked user HL7
file (72 status entries). Nothing is staged; main/HEAD are unchanged. Baseline copies,
the manifest and useful build/focused/full/smoke/stress reports are intentionally
retained in ignored artifacts/script-output-naming; earlier milestone artifacts and
build outputs remain. No disposable manual HL7 export or probe was added.

## Native Windows acceptance

1. Open a reviewed synthetic fixture. Create two synthetic no-op scripts named Patient
   Deidentifier and Normalize Phone Numbers (`void Main() { }`), and run in Current/Whole scopes.
   Check their initial output titles/tabs and Save As default names; canceling the
   picker keeps dirty, independent output. Source title/destination must stay unchanged.
2. Run the same script twice. Tabs must be distinct (base name, `(2)`), while both Save
   As defaults remain the same unsuffixed `.hl7` name. Save one under a different name;
   its title must follow the actual destination. Verify the other output's data/ID and
   source remain independent when duplicate display labels refresh.
3. Add Batch.SetOutputFileName("Custom_Audit") in Main, then a different valid name
   in Complete. Complete wins and `.hl7` is added. Follow it with an invalid path or
   reserved device suggestion: the last valid name remains, the run succeeds and a
   fixed notice appears. With only invalid overrides, the captured script name wins.
4. Use the three-second synthetic script from the 8B.4 acceptance record; add a custom
   filename in Complete. Edit the source during execution. Output must retain captured
   data/name and explicit Stale source attribution; newer edits must remain. Check report
   attribution and reconstruct the Script Editor. Renaming/selecting another script
   after completion must not relabel prior output/reports. During-run programmatic
   rename/selection is permanently tested; existing busy UI controls remain disabled.
5. Cancel a running script and run one that fails after suggesting a name: neither may
   publish output or replace prior successful reports. Repeat name/Save As checks in
   docked/floating tools and Light/Dark. Recheck accepted source-editor Ctrl+Z/Y routing
   with Edit Mode off and Navigator/Inspector focus, without an intervening mouse click.

Naming metadata/attribution are session-only. Validation is intentionally conservative;
the user can choose another actual filename through existing Save As. Native dialogs,
visual layout and focus still require the user's Windows acceptance. Published artifacts
are not refreshed and installer/package validation is outside this authorization.

Stop after these implementation/automated checks for native Windows acceptance.
