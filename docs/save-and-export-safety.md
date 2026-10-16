# Local Save As, exports and confirmations

Final Stage 5B native Windows acceptance, including save-dialog replacement and compact
confirmations, was explicitly confirmed by the user on 2026-10-16. One milestone commit
and normal push to origin/main are authorized; no tag/release or history rewriting.
Earlier verification notes below describe the pre-acceptance checkpoint.

**Existing files may be replaced only after explicit user authorization, using safe
replacement semantics.** Ordinary Save to an associated HL7 document is an established
operation and does not request fresh overwrite consent.

## Native Save File inventory

There are exactly three picker workflows. All now use Desktop's SaveFilePicker adapter.

| Workflow | Before correction | Current policy and implementation |
| --- | --- | --- |
| File Save As; first Save of an unassociated document | ShowOverwritePrompt=true; MessageDocument.SaveAsync used File.Replace for existing files, without version validation | Same picker and streaming serializer; version-scoped consent and shared safe publication |
| Script-generated HL7 Save / Save As | Same implementation as ordinary HL7; no separate output saver | Same normal document workflow; only the selected output gets a SavePath/save point |
| Reports → Export selected CSV | Picker prompted, but ScriptReports refused every existing destination | Confirmed overwrite permitted; report identity, attribution, escaping, formula protection and read leases retained |
| Original source/value export (Full Source Viewer and source context menus) | Picker prompted; RawInspectorSession used overwrite-enabled File.Move, without version validation or durable flush | Same exact source ranges; shared durable staging/replacement; original source, associated document and owned snapshot remain forbidden destinations |

Ordinary File Save/Ctrl+S uses the existing associated path without opening a picker.
Its document gates, identity, save points, dirty state, undo and streaming Core boundary/
encoding checks remain. It now checks for changes occurring during that save operation.
Reconciliation of external changes made before Save begins remains the earlier Stage 4
limitation; this change does not introduce an external-file merge/reload workflow.

Script Library records, preferences, workspace layout and worker staging use no native
Save File picker. Their persistence and scripting APIs are unchanged. Scripts gain no
filesystem export API. Host-side string-path export/save overloads permit creation only;
replacement requires a FileSaveDestination obtained after explicit consent. Associated
Save captures its destination at operation start under the existing document save gate.

## Windows picker evidence and provider policy

The installed Avalonia.Win32 12.1.4 binary identifies commit
`2d79ba55fae1b1e71c69f93a668080c9029e6a31`. Its IL was inspected: the save options pass
ShowOverwritePrompt to Win32StorageProvider; it starts with IFileDialog's options and
clears FOS_OVERWRITEPROMPT only for an explicit false value. Microsoft documents that
[FOS_OVERWRITEPROMPT is the Save dialog default](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/ne-shobjidl_core-_fileopendialogoptions).
The [Avalonia Windows implementation](https://github.com/AvaloniaUI/Avalonia/blob/main/src/Windows/Avalonia.Win32/Win32StorageProvider.cs)
shows the same option handling and returns a selected filesystem path only after dialog
success; cancellation returns no selection. Declining overwrite stays in the dialog;
canceling it then returns no selection. Selection does not write/truncate the target.

All application picker calls explicitly set ShowOverwritePrompt=true. Only the audited
built-in Avalonia.Win32.Win32StorageProvider in the Avalonia.Win32 assembly, on Windows,
is trusted to provide overwrite consent. There is no second application prompt there.
The generic storage-provider API and SaveFilePickerResult expose no independent consent
or confirmed file-version field. Arbitrary paths/custom providers are not permission.
Other providers require a compact application Replace/Cancel confirmation for existing
local files; the file version is captured before that confirmation and retained afterward.
Nonlocal destinations are refused because their transactional guarantees are unknown.
Reaudit this trust decision when upgrading/replacing the Windows provider.

This was source/binary investigation, not native automation of the Windows picker.
Mocked picker tests verify application policy and do not establish native dialog behavior.

## Publication and concurrency

FileSaveDestination owns the shared publication sequence for HL7, CSV and source export:

1. Capture the authorized existing file's SHA-256, length, creation/write times and,
   on Windows, volume/file identity from GetFileInformationByHandle. Missing destinations
   receive creation-only permission. Hashing streams in bounded 64 KiB buffers off the UI.
2. Stream complete output into a unique same-directory .hl7-save-*.tmp file. Flush both
   managed buffers and the file to disk, then close the writer. Never truncate/delete the target.
3. Revalidate resolved destination location, identity/metadata and content hash immediately
   before publication. On Windows read handles deny in-place writes during validation and
   replacement, while allowing delete sharing required by replacement. An absent/new target
   uses non-overwriting File.Move, so later collisions fail. A removed authorized target fails.
   Final validation/publication is serialized within this process, so two local saves
   cannot both replace the same captured version. Staging remains concurrent.
4. Existing targets use File.Replace with no delete-then-move or destructive fallback.
   Unsupported filesystems, access failures, changed files and sharing conflicts fail safely.
   Temporary files are cleaned after success/failure; cleanup is best effort if the filesystem
   itself refuses deletion. Failed saves retain document identity, history and dirty state;
   failed exports retain the producing report journal and attribution for retry.

The Desktop document workflow rejects simultaneous pickers/saves for the same document
and checks document membership, content version and SavePath after selection. Switching
active tabs cannot redirect an outstanding save. A closed or edited document must retry.
Private document/report snapshot storage remains protected, even with replacement consent.

Limitations: Avalonia exposes no callback at Windows' overwrite-confirmation instant, so
the native version snapshot is taken immediately after the picker returns. A change in
that small gap cannot be compared with the version shown by Windows. Portable file APIs
also expose no atomic compare-and-replace: a rename/name swap can race the final check
and replacement, despite identity/hash checks and Windows write exclusion. Unix metadata
checks are weaker than Windows file IDs and sharing restrictions. Atomic replacement
depends on filesystem/OS support; network or custom storage cannot be promised atomicity
or crash durability. Unsupported replacement fails rather than truncating a destination.
There is no transaction spanning exports and application documents.

## Compact dialogs

Unsaved document, unsaved script, Delete Script and fallback Replace confirmations now
share a 420-pixel width (400–440 constraints), content-sized height, wrapped prompts,
16-pixel padding/spacing and right-aligned buttons. No fixed height can clip long names.
Fluent theme inheritance remains; Cancel initially has focus. Save/Discard/Cancel and
Delete decisions are unchanged. Escape, Cancel and window-close keep safe cancellation.
Preferences and Reports windows are unchanged apart from the report export action.

## Native Windows retest

The user confirmed these final native checks passed on 2026-10-16. Retained below for
reproducing acceptance; automated picker mocks did not establish this native result.

Use disposable synthetic destinations only. Launch the current Release build:

```powershell
Set-Location C:\Dev\HL7Workbench
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

1. Produce a CSV report. Export over an existing disposable CSV; accept Windows' overwrite
   prompt. Verify exact CSV content and no redundant application prompt.
2. Repeat and decline; cancel the picker. Compare original bytes/hash. Cancel the picker
   directly in another attempt; no file changes or temporary leftovers.
3. Edit a synthetic HL7 document and Save As over another disposable file after confirmation.
   Check bytes, title/dirty state and undo; ordinary Save should not prompt again.
4. Run a script, select its generated output and Save As over another disposable HL7 file
   after confirmation. Check the output's identity/save state and the untouched original
   source document/file hash. Repeat decline/cancel and a destination sharing/access failure.
5. Test original-source export to an unrelated existing disposable file after confirmation;
   exporting over its own source or private snapshot must still fail without modification.
6. Inspect Unsaved Changes, Unsaved Script and Delete Script with short and long names.
   Check compact sizing, wrapped text, Save/Discard/Cancel, Delete confirmation, Escape,
   Cancel, window-close and keyboard focus. Repeat dark/light and normal/high DPI.
7. Record native results and finish the existing Stage 5B checklist before requesting
   separate explicit Git authorization.

## Files changed by this correction

Earlier uncommitted Stage 5B work is retained. This correction changes these 28 files:

| Area / repository directory | Files |
| --- | --- |
| src/HL7Workbench.Desktop | MainWindow.Editing.cs; MainWindow.Shell.cs; new DocumentSaveWorkflow.cs |
| src/HL7Workbench.Desktop/ViewModels | ScriptEditorViewModel.cs |
| src/HL7Workbench.Desktop/Viewer | ScriptEditorView.cs; ScriptReportsWindow.cs; ViewerRequests.cs; new ConfirmationDialog.cs and SaveFilePicker.cs |
| src/HL7Workbench.Workspace | MessageDocument.Editing.cs; RawInspectorSession.cs; ScriptReports.cs; new FileSaveDestination.cs |
| tests/HL7Workbench.Desktop.Tests | BatchEditorTests.cs; new SaveAcceptanceTests.cs |
| tests/HL7Workbench.Workspace.Tests | EditableDocumentTests.cs; RawInspectorTests.cs; BatchExecutionTests.cs; new FileSaveDestinationTests.cs |
| Repository root | README.md |
| docs | PROJECT_CONTEXT.md; architecture.md; desktop-preferences.md; script-api.md; stage5b-batch-scripting-acceptance.md; stage5b-ui-finishing-acceptance.md; new save-and-export-safety.md |
| docs/decisions | 0006-stateful-batch-scripting.md (export-policy amendment only) |

No Core, Scripting/ScriptHost runtime, dependency/lock-file, Script Library/preferences
persistence, ribbon appearance/routing or HL7 parsing implementation changes are part
of this correction. The temporary binary-inspection probe is removed after investigation.

## Verification

Final checks on 2026-10-15: locked restore passed; Release build passed with zero
warnings/errors; 80 focused cases passed (20 Desktop policy/dialog, 57 shared save/edit/
source-export and three ordinary/generated HL7/CSV integration cases); all 802 automated
cases passed, zero failed/skipped. There are 33 added cases; existing negative replacement,
escaping, boundary, cancellation, identity and preservation checks are retained.
Workspace stress was rerun because shared Workspace save/export code changed: all 333
cases passed, zero failed/skipped, including existing large-file/streaming/stateful cases.

The Release folder publish was refreshed. Full VerifyScriptPublish.ps1 passed under
Windows PowerShell 5.1.26100.9444: published worker lifecycles/state/CSV/completion/deferred output,
clean exit, Release/publish hashes and dependency isolation. No worker or dependency
implementation changes were required. Native picker/UI retest is pending; it was not
substituted with mocked picker assertions. No new native DPI/visual acceptance is claimed.
Final diff inspection, git diff --check and whitespace/conflict-marker checks passed
for all 63 modified/untracked files. The ignored investigation probe was removed;
the reviewed synthetic fixture hash is unchanged. Git stays on main at 6dfa9f4, with
28 modified tracked files, 35 untracked and nothing staged. No commit/push/tag/release.

Reproduce the build, full suite, stress and publish commands in the
[Stage 5B lifecycle checklist](stage5b-batch-scripting-acceptance.md#verification).
Focused commands (Release executables, no rebuild):

```powershell
& tests/HL7Workbench.Desktop.Tests/bin/Release/net10.0/HL7Workbench.Desktop.Tests.exe -class '*SaveAcceptanceTests' -noLogo
& tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/HL7Workbench.Workspace.Tests.exe -class '*FileSaveDestinationTests' -class '*EditableDocumentTests' -class '*RawInspectorTests' -noLogo
& tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/HL7Workbench.Workspace.Tests.exe -method '*OrdinaryAndGeneratedHl7*' -method '*MultipleInterleavedReports*' -noLogo
& "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File build/VerifyScriptPublish.ps1
```
