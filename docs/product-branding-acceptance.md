# Message Workbench product branding acceptance

Status: complete automated gate and native Windows branding acceptance **passed**.
The user authorized one local checkpoint, `Branding: Rename product to Message Workbench`.
All 16 final-file hashes and eight source/test hashes matched the completed gate
before this acceptance-only documentation update. No implementation/test changed.
Earlier pending/stop statements below are historical. Stage 14 is separately authorized
after the checkpoint and must remain uncommitted. No push/tag/distribution/publication.

## Accepted checkpoint and scope

Stage 13B native acceptance passed all 16 user-reported checks. Its single authorized
local commit is **738e3d7697fb7b2d2d8e668fe7b02edbddea28f2**, message
`Stage 13B: Add controlled MLLP load testing`, parent
`45e7cdbad9a32cdaebb7834ae0026b10914622aa`. Before staging, all 29 final-file hashes
matched the completed gate, as did 20 implementation/test hashes, protected assets,
artwork and the protected TEST fixture. Four acceptance-only Markdown files were
updated; all 29 staged blobs/inventory were reviewed before one local commit.
Immediate post-commit index/tree were clean. No completed gate was unnecessarily
repeated for that unchanged implementation. Evidence: artifacts/stage13b-checkpoint.

The official final public name is **Message Workbench**. The main title/startup
status, Help → About, About contents, restored floating-window titles and Desktop
Product/AssemblyTitle/Description metadata use the public name. README, project
context, specification/starter and appropriate documentation introduce the broader
existing healthcare interoperability scope. There is no separate splash screen or
tracked screenshot with obsolete public branding. Future packaging documentation
uses the public name while preserving technical paths; no packaging was performed.

The solution/projects/assemblies/namespaces/repository, executable filename and
resource URIs remain HL7Workbench. Configuration directories, preferences keys,
endpoint profiles, script libraries and placement schemas are unchanged. Original
vocabulary authoring attribution is preserved. The approved MW source PNG,
master PNG and ICO remain byte-identical. No Core, Workspace, Scripting, ScriptHost,
Transport implementation, dependency/lockfile, clinical fixture or protocol changed.

Four permanent Desktop cases cover metadata vs technical identity, legacy settings/
profiles and launch/relaunch, both themed title/Help/About/icon/keyboard paths,
single About instance and restored floating geometry/tool identity. Existing three
icon cases and shell/menu coverage remain. The initial focused run identified the
SDK's AssemblyTitle property (Title alone does not set the assembly title) and two
test setup errors: key-up sent to an already closed modal, and optional-tool restore
without visibility retention. These were corrected without weakening assertions;
diagnostics and the passing seven-case retest remain in artifacts/product-branding.

## Reference and provenance findings

The [complete audit](product-reference-audit.md) records current cleanup, all matching
commit hashes/paths, additional tree refs, retained audit references and limitations.
Four named references in project-owned specification/starter documents were replaced
with neutral wording while preserving independent-implementation/proprietary-reuse
restrictions. Historical references remain intact. No obvious suspicious provenance
or licensing evidence was found within the limited review; this is not legal clearance
or proof of authorship. Third-party notices/dependency licensing requirements remain.
The protected TEST fixture was hash-checked only and excluded from content searches.

## Final verification

Evidence: ignored artifacts/product-branding. Results are reconciled against
the exact eight frozen implementation/test files; only result documentation changed
after the final build. All focused/stress cases have zero errors/failures/skips/unrun.

| Check | Result |
| --- | --- |
| Locked restore | Passed; all projects up to date (`final-restore.log`) |
| Release warnings-as-errors build | Passed; **0 warnings/errors**, 2.95s (`final-build.log`) |
| Branding/icon focused retest | 7 passed; zero errors/failures/skips/unrun |
| Focused Core ACK/FHIR | **109 passed**, 1.020s (`final-core.log`, `.xml`) |
| Focused Workspace/Transport/FHIR/preferences | **192 passed**, 31.222s (`final-workspace.log`, `.xml`) |
| Focused Desktop Stage 11–13B/branding/icon | **71 passed**, 14.079s (`final-desktop.log`, `.xml`) |
| Shell menu/ribbon checks | **3 passed**, 0.875s (`final-shell.log`, `.xml`) |
| Full solution | **1,953 passed**, 0 failures/skips, **3m 21.144s** (`final-solution-tests.log`) |
| Complete unfiltered Workspace stress | **935 passed**, 0 errors/failures/skips/unrun, **310.593s**; `HL7WORKBENCH_STRESS=1` scoped/restored (`final-workspace-stress.log`, `.xml`) |
| Both existing Release ScriptHost lifecycles/isolation | Passed (`final-scripthost-smoke.log`); existing Release layout, no publish command |
| Script API reference sync | Passed (`final-reference-sync.log`) |
| Frozen/protected/all-fixture/artwork integrity | Passed: **8** frozen implementation/test paths, **13** protected hashes, **2** icon derivative hashes and **16** fixture/artwork Git blobs; TEST hash unchanged (`final-integrity.log`) |
| Current/history reference audit and whitespace | Passed: **462** current files searched (TEST excluded); only the audit record retains named references. **24** commits + **3** additional trees, **865** historical blobs searched / **1** protected blob excluded. Tracked + all **5** new-file whitespace checks clean. HEAD unchanged; empty index. |

The full solution increases by exactly four Desktop cases from the accepted 1,949
to 1,953; no existing permanent coverage was removed. Complete Workspace stress
remains 935 cases. Source/test hashes, all protected assets and fixture/artwork blobs
match after stress. Existing ScriptHost verifier output uses the word "Published"
historically; it was invoked against the existing Release directory and performs
verification, not a publish operation. No package/deploy/publish command was run.
The final 16 source/test/doc hashes are retained in post-verification-file-hashes.json.

## Known limits and retained material

Native title bars, Windows taskbar/Alt+Tab/DPI, OS file metadata presentation, focus,
saved layouts on a real existing installation and live receiver behavior still need
native acceptance. Headless tests verify resource/metadata/placement compatibility
but cannot replace these checks. OS product metadata does not rename the executable.
The existing logo has not been regenerated or modified. No settings migration is
needed; historical technical names remain visible in file paths and API namespaces.

Audit coverage is local reachable history, with the explicit protected TEST content
exclusion. The audit record itself intentionally names the searched third-party
product so findings are reviewable. Old vocabulary authoring attribution is retained
as provenance. Existing diagnostics and newly owned ignored logs/XML/hash manifests/
audit script are retained for review, with no payload exports or new project assets.

## Native Windows acceptance checklist

1. Launch the existing Release `HL7Workbench.Desktop.exe`. Verify **Message Workbench**
   in the main title, startup status and Help → About. About describes the existing
   workflows; Escape/Close dismisses it, repeated opening uses one modal instance.
2. Inspect menus, tooltips, dialogs and reports for consistent public branding and
   absence of third-party product branding. Technical filenames/API namespaces may
   remain HL7Workbench. Inspect executable Properties product/title metadata.
3. Check unchanged approved logo on main/dialog/floating windows, taskbar and Alt+Tab,
   light/dark themes and representative DPI/multiple-monitor configurations.
4. Launch against existing configuration: preferences/theme, endpoint profiles,
   saved scripts/searches/extractions and docking layout remain available. Float,
   resize, save/restore layout, close/relaunch and verify placement/tool identities.
5. Open synthetic HL7; navigate/view/inspect/search/extract, edit, Undo/Redo across
   messages/tabs and save/reopen to scratch. Run a local trusted script and inspect
   retained output/reports; confirm existing save/overwrite behavior.
6. Import synthetic FHIR, select a patient/history, generate an independent HL7
   document and verify document-associated Generation Report/docking/navigation.
7. Recheck Stage 12 single-send with delayed confirmation against loopback Positive
   Test ACK; confirm full write, exact receipt and correlated AA. No automatic retry.
8. Recheck Stage 13A sequential batch pause/resume/cancel and privacy-safe export.
   Recheck Stage 13B attestation/review/exact-target authorization, global rate and
   concurrency bounds, correlated ACK metrics/latency, Stop/drain and safe reports.
   Opening/restoring tools must never initiate traffic; keep existing listener bounds.
9. Switch light/dark themes with About and tools open. Shut down normally, relaunch
   and recheck settings/layout/logo; no background sender/listener resumes.
10. Confirm protected source/fixtures/artwork/TEST hash remain exact. Record native
    acceptance before any separate branding commit authorization.

## Changed-file inventory

16 paths: 11 modified tracked files and 5 new files. All branding/audit changes remain
unstaged/uncommitted; HEAD stays at the accepted Stage 13B checkpoint. No protected
fixture, artwork, TEST file, dependency lock or unrelated file is included. The only
sample-directory edit is its synthetic FHIR README introduction, not clinical data.

```text
 M Codex_Starter_Prompt.md
 M HL7_Workbench_Specification.md
 M README.md
 M docs/PROJECT_CONTEXT.md
 M docs/branding.md
 M samples/synthetic-only/fhir-r4/README.md
 M src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
 M src/HL7Workbench.Desktop/HL7Workbench.Desktop.csproj
 M src/HL7Workbench.Desktop/MainWindow.Shell.cs
 M src/HL7Workbench.Desktop/MainWindow.axaml
 M tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs
?? docs/product-branding-acceptance.md
?? docs/product-reference-audit.md
?? src/HL7Workbench.Desktop/ProductBranding.cs
?? src/HL7Workbench.Desktop/Viewer/AboutWindow.cs
?? tests/HL7Workbench.Desktop.Tests/ProductBrandingTests.cs
```

No files are excluded as unrelated/uncommitted work. The protected formerly excluded
TEST fixture is tracked in the existing Stage 12 baseline, remains unchanged and was
never staged or opened here. Existing and newly owned ignored verification/audit
evidence remains available under artifacts; nothing uncertain was deleted.

**Stop for native Windows branding acceptance. Leave these 16 paths unstaged and
uncommitted; no additional commit, amendment, push, tag, package or publication.**
