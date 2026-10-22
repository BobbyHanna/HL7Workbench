# Stage 14 — usability, stability and portable release readiness

Status: implemented; complete automated gate **passed** (2026-10-22). The user reports
portable application and self-contained scripting success in a separate work VM and
authorizes **one local Stage 14 checkpoint and fresh verified clean-source ZIP**.
Distribution/human license/beta-terms review remains open. No push/tag/upload/GitHub
release, amendment/history rewrite, further milestone or publication is authorized.

## Checkpoint and final ZIP preparation authorization

The authorized single commit is `Stage 14: Prepare portable beta release`, based on
branding checkpoint 84fdc22925173250fad132d0d4b4c6768d3c68e0. All application code,
tests, locks, notices and protected assets are reconciled to the completed gate below.
Post-gate changes are documentation and packaging only: required recipient README,
provenance input inclusion, missing-document regression assertions, concise notes,
proposed application-only beta terms and a resolved/unresolved distribution review.
No established application/runtime behavior is changed by this preparation.

Checkpoint integrity, scoped retest rationale, exact staged inventory/blob audit and
commit hash are recorded in artifacts/stage14-checkpoint/. The fresh ZIP's locked
portable restore, zero-warning Release build, packaging tests, application/worker
lifecycle evidence, metadata/content/notice checks, clean-source provenance, integrity,
deterministic-repeat and SHA-256 results are recorded after the commit under
artifacts/stage14-release-preparation/. Old dirty candidates remain historical local
evidence and are superseded for final review, not silently replaced.

The earlier full **1,961 solution / 935 Workspace stress** gate applies to unchanged
application inputs; packaging/document changes require focused packaging verification,
not repetition of the complete unchanged solution/stress suite. The user VM report
does not assert every native matrix item or acceptance of the exact rebuilt ZIP.
See [recipient README](beta-readme.md), [release notes](release-notes-template.md),
[distribution review](beta-distribution-review.md) and [proposed beta terms](beta-usage-terms-proposed.md).
Notice applicability, owner terms, employer trust and separate exact-artifact/recipient
approval block distribution. The detailed earlier authorization/inventory statements
below describe the historical pre-checkpoint gate.

## Accepted baseline

Branding passed user-confirmed native Windows acceptance. Its single authorized local
checkpoint is **84fdc22925173250fad132d0d4b4c6768d3c68e0**, message
`Branding: Rename product to Message Workbench`, parent
`738e3d7697fb7b2d2d8e668fe7b02edbddea28f2`. All 16 final hashes, eight implementation/
test hashes, protected assets and persistence identities matched the completed
1,953 solution / 935 stress gate. Two acceptance-only Markdown files were updated;
all 16 staged blobs/inventory matched audited files before the one commit. The
immediate post-commit tree/index were clean. Evidence: artifacts/branding-checkpoint.
No completed gate was needlessly repeated for unchanged accepted branding.

## Issue inventory and resolutions

| ID / severity | Observed problem / review finding | Resolution and evidence |
| --- | --- | --- |
| 14-01 High | Default worker DLL launch required installed dotnet, incompatible with portable self-contained use. | Prefer only its adjacent apphost; DLL-only development fallback retained. Private pipe, limits, Windows job/process-tree ownership and transactional publication unchanged. Permanent selection/integration/cancellation coverage plus actual candidate worker lifecycles. |
| 14-02 High | SDK project-reference publishing propagated worker apphost/configuration into Desktop root; Build glob could also scoop up unrelated RID/publish trees. | Nonprivate worker reference; explicit root/satellite copying; candidate publishes processes separately with independent runtimes. Candidate fails worker execution leaks and missing runtime/metadata. |
| 14-03 High | Semantic/unsupported/oversized saved layouts throw InvalidDataException, outside the existing startup recovery filter. | Catch the specific failure and safely restore default workspace; preserve invalid metadata until explicit/normal layout save. Four permanent startup cases verify no data disclosure or network-tool initialization. |
| 14-04 Medium | Fixed workspace-layout.json.tmp could collide across instances and leave failed temporary metadata behind. | Unique per-attempt metadata, atomic move and best-effort cleanup of only that attempt. Failure regression preserves pre-existing .tmp/current workspace. |
| 14-05 Low / hardening | Save handler displayed arbitrary InvalidOperationException.Message, even though current workflow conflicts use fixed wording. | Fixed actionable status retaining working text; no imported exception text exposed. Existing save/source/conflict and worker/activity privacy suites retained. No demonstrated PHI incident is claimed. |
| 14-06 Medium | Public beta version was absent; SDK default FileVersion follows fixed binding AssemblyVersion. | Explicit product/informational 0.1.0-beta, file 0.1.0.0; About displays beta. Binding version remains 1.0.0.0 and existing technical identities/settings paths stay fixed. Metadata regression caught/corrected the initial file-version mapping. |
| 14-07 Medium | No reproducible reviewed self-contained candidate/notice/provenance/exclusion gate. | Locked win-x64 restore and separate publishes; allowlisted candidate assembly, complete notices, checksum manifests, source-input provenance, deterministic ZIP and streamed ZIP/directory roundtrip verification. Permanent packaging failure/determinism/license tests. |
| 14-08 Review gate | Clean-machine/DPI/accessibility/OS trust and first distribution rights/usage terms are not established by automated tests. | Explicit native checklist and human review below; candidate remains unsigned/local/not for distribution. Owner confirmed artwork rights. No release-readiness claim bypasses these gates. |

## Workflow and reliability review

| Area | Review outcome / retained behavior |
| --- | --- |
| Navigation/focus/keyboard | Existing active-document window-root Undo/Redo, independent text-input ownership, Ctrl+F, editor/script shortcuts and cancellation remain. About remains keyboard dismissible. No shortcut remapping or focus-stealing redesign. |
| Docking/empty states/themes | Existing optional/lazy tools, placement-only restore, no restored clinical contents/network authorization, off-screen recovery and contextual empty states remain. Correct corrupt-layout recovery and beta/About messages are the targeted usability changes. |
| Startup/shutdown/unsaved work | Per-user settings retained. Existing dirty-script/extraction/document prompts and revision checks remain; transport cancellation/drain precedes closure. Existing cancellation/late confirmation/save rollback/close regressions remain permanent. |
| Long messages/background work | Existing snapshots, bounded decoded caches/editor windows, owned read leases, cancellation, extraction/report journals and deferred disposal remain. No implicit attachment decoding or whole-value claims from abbreviated previews. Complete stress coverage retained. |
| File failures/corruption | Existing atomic source/export consent/version guards retained. Layout recovery now handles semantic and oversized files; failed layout publication is isolated. Corrupt settings retain safe defaults until explicit save. |
| Restart after abnormal exit | No automatic PHI recovery, auto-send/listen or background load resume added. Placement/preferences may restore; plaintext temporary snapshots can remain until bounded abandoned-file cleanup on a later operation. This residual risk is disclosed. |
| DPI/accessibility | Current themed controls/keyboard routes and headless layout tests retained. Real screen reader, native dialogs, DPI/multi-monitor and OS focus behavior require native acceptance, not speculative UI redesign. |

## Privacy and safety review

No runtime cloud service, account, telemetry, update polling, automatic upload or new
network path was added. Source/runtime review keeps network activity in explicitly
authorized transport workflows (trusted user scripts remain a separate permission
boundary). Stage 12 confirmation/write/ACK/uncertainty, Stage 13A sequential batch,
Stage 13B synthetic attestation/exact-target authority/rate/concurrency/stops remain.
TLS is unavailable/fail-closed; plain TCP is not encrypted/authenticated. No retry.

Activity uses fixed metadata/codes, not payloads/patient IDs/arbitrary exception text.
Worker stdout/stderr is drained/discarded; diagnostic coordinates/codes are sanitized.
Release omits development stack logging; candidate excludes symbols and optional
createdump. OS/runtime/admin-configured crash dumps, debugging, clipboard history/
sync, backups and same-user script/file access are outside this application's control.
Script/extraction reports intentionally contain requested values; transport reports
have separate whitelist rules. Export/clipboard use remains explicit, not PHI-proof.

Settings and user-created definitions remain in existing per-user paths. Snapshots
and journals use bounded user temporary storage with inherited Windows permissions;
no application-level encryption, secure erasure or custom cross-user ACL assurance.
Crashes can leave plaintext. Script jobs are resource-contained, not sandboxed:
trusted scripts can access files/network as the user. Guide and residual risks are in
[beta guide](beta-user-guide.md). No real patient data was introduced for verification.

## Candidate and licensing review

Target archive: MessageWorkbench-v0.1.0-beta-win-x64.zip. Only the packaged apphost
copy is renamed MessageWorkbench.exe; the embedded HL7Workbench.Desktop.dll binding,
assembly/resource/config identities and AssemblyVersion remain unchanged. Each
process has its own pinned .NET 10.0.12 runtime. No install/admin/runtime download,
single-file unpack cache or extraction-folder write dependency is introduced.

The [portable procedure](portable-release.md) and [35-entry license review](portable-dependencies.md)
record exact package versions, primary license declarations, notices, obligations and
remaining scope questions. There are 34 primary MIT declarations and one ANGLE file
license with BSD-style terms. Native/.NET/Roslyn/Toolkit component bundles retain
additional terms; broad notices mentioning conditional/copyleft terms require human
applicability review and are not evidence that every listed component ships here.
Package-bundled notices retain exact bytes. Eleven pinned upstream license/notice
texts from eight repositories retain all words/copyrights; LF/trailing-whitespace
formatting only, with raw/stored hashes and original diagnostic bytes retained.
Unknown primary licenses, missing notices or pinned-text changes fail assembly.

The user confirmed approved MW artwork ownership or redistribution permission on
2026-10-22. Artwork/source/icon hashes are unchanged. This attestation and package
metadata do not amount to independent legal clearance. Human notice-scope review,
application beta usage terms, native acceptance, signing/trust decision and separate
distribution authorization remain. No license or distribution grant is invented.

Initial diagnostic candidate: 580 files, both worker lifecycles passed with PATH and
DOTNET_ROOT unable to select an installed dotnet. A direct PE-import review found
33 native modules whose direct imports resolve to bundled libraries or Windows API/
system libraries; delay-load/dynamic imports and real Windows 10/11 startup still
require clean-machine testing. Initial candidate/probes/logs are clearly local and
retained as diagnostic evidence; they are not the final approved beta.

## Permanent regression coverage / verification findings

New .NET coverage: six Desktop cases (metadata, four corrupt startup variants,
failed atomic layout metadata) and two Scripting apphost-selection cases. Existing
branding, source/save, large-file, script privacy/cancellation/job containment, theme,
dock/close, FHIR and MLLP suites remain. Packaging tests cover deterministic ZIPs,
exclusions, no overwrite/links, integrity, runtime/worker/assembly identity, missing/
unknown licensing, altered pinned notices and archive roundtrip tampering.

Initial packaging execution was blocked by sandbox-owned temporary-directory access;
the same tests passed with normal owned-temp access. Diagnostics are retained, not
hidden. Initial Desktop metadata regression caught FileVersion inheriting 1.0.0.0;
explicit 0.1.0.0 corrects it without weakening the assertion. Initial notice whitespace
checks caught genuine upstream formatting; all license words were retained while
normalizing LF/trailing whitespace, and source/stored hashes distinguish the change.

## Final automated gate

Evidence: ignored artifacts/stage14. Final source/test/build/notice/lock hashes were
reconciled after the complete gate. No tests/fixtures are removed and no generated
candidate/logs are source-controlled. Native acceptance remains pending regardless
of automated results.

| Check | Result |
| --- | --- |
| Locked ordinary solution restore | Passed (`final-restore.log`) |
| Zero-warning Release build | Passed; **0 warnings/errors**, 8.48s (`final-build.log`) |
| Focused Core ACK/FHIR | **109 passed**, 1.191s (`final-core.log`, `.xml`) |
| Focused Workspace/Transport/FHIR/settings/activity privacy | **203 passed**, 32.005s (`final-workspace.log`, `.xml`) |
| Focused Scripting apphost/lifecycle/privacy/cancellation | **74 passed**, 26.375s (`final-scripting.log`, `.xml`) |
| Focused Desktop Stage 11–14/branding/docking | **102 passed**, 15.852s (`final-desktop.log`, `.xml`) |
| Shell menu/ribbon regressions | **3 passed**, 1.137s (`final-shell.log`, `.xml`) |
| Full solution | **1,961 passed**, 0 failures/skips, **3m 34.276s** (`final-solution-tests.log`) |
| Complete unfiltered Workspace stress | **935 passed**, 0 errors/failures/skips/unrun, **334.985s**; HL7WORKBENCH_STRESS=1 scoped/restored (`final-workspace-stress.log`, `.xml`) |
| Both normal Release ScriptHost lifecycles/isolation | Passed, including deployed hashes and satellite files (`final-scripthost-smoke.log`) |
| Script API synchronization | Passed (`final-reference-sync.log`) |
| Permanent packaging suite | **10 passed**, 0 failures/errors, 0.813s (`final-packaging-tests.log`) |
| Final locked portable candidate / both native worker lifecycles | Passed; 580 files, 35 package/runtime entries, public metadata/technical identity checks and both self-contained current/whole lifecycles (`final-candidate-build.log`) |
| ZIP roundtrip/deterministic repeat/checksums/contents/notices | Passed; byte-identical repeat ZIP and streamed CRC/SHA-256 roundtrip (`final-candidate-repeat.log`) |
| Protected fixtures/artwork/TEST/frozen hashes and whitespace | Passed: **13** protected hashes, **33** frozen code/build/notice/lock/curated-doc paths, **2** icon derivatives, **17** fixture/artwork Git blobs; **11** upstream source/stored hashes and complete notice words; all candidate input hashes; tracked + **29** new-file whitespace (`final-integrity.log`) |

All focused cases have zero errors/failures/skips/unrun. The full solution grows by
exactly eight .NET cases from the accepted 1,953 to 1,961, with no prior coverage
removed. Complete Workspace stress remains 935 cases; ten additional standard-library
packaging tests pass. No frozen implementation, test, build, lock, notice or curated
candidate document changed after the final build/gate; only result records were
reconciled. The original 41 final source/test/doc hashes are retained in
post-verification-file-hashes.json. The TEST fixture remains hash-only with
`D254E8BBBFC98C2E377EC9149F2D46415756E0CD96CE8DED0D9FB8548F5EFFA1`.
Normal dependency locks, Core/Workspace/Transport source logic, fixtures/artwork,
solution and CI remain unchanged. HEAD is the branding checkpoint; index empty.

Final local candidate (not for distribution):
`artifacts/stage14/final-candidate/CANDIDATE/MessageWorkbench-v0.1.0-beta-win-x64.zip`,
**95,841,780 bytes**, SHA-256
`41925FE5C47D524F0F39CFE3965A56D060AF8438C162D0394B6C45E7418F3634`.
Its extracted directory is the adjacent MessageWorkbench/ folder. Runtime, all
required files/notices, absence of known private build paths and exact source-input
hashes were checked. Each archive and directory contains an explicit local-candidate
marker and dirty-source provenance. This is not an approved beta distribution.

## Stage 14 artwork identity clarification (2026-10-22)

The user clarified that the former informal artwork nickname was an inside joke,
not a brand, publisher, company or product identity. **Message Workbench** is the
official product name and the existing **MW logo** is the approved artwork. Seven
misleading artwork references across six Markdown files were corrected within
Stage 14; no application, build, packaged document or artwork bytes changed.

A case-insensitive search of tracked and nonignored untracked files (excluding
protected TEST fixture content) found no remaining nickname references or matching
filenames. A byte search including UTF-16 found none in protected assets; visual
inspection of the approved source confirms only the MW logo. All protected hashes,
the 33 frozen gate inputs and candidate input hashes still match. Documentation-only
follow-up checks and the current 43-file inventory/hashes are recorded separately in
artifacts/stage14/branding-clarification-*; the original gate evidence above is retained.
The completed automated suite was not repeated for these wording-only changes.
HEAD remains the accepted branding checkpoint, with an empty index and no history
changes. Native Windows acceptance remains pending.

## Native Windows acceptance checklist

Use synthetic test data and explicitly authorized loopback endpoints only.

1. Startup under a standard Windows 10/11 x64 user; no SDK/runtime/installer/admin on
   a clean machine. Confirm all required files remain together after extraction.
2. Verify Message Workbench, v0.1.0-beta, approved icon on title bars/taskbar/Alt+Tab,
   About and executable Properties; no obsolete public or third-party product branding.
3. Back up existing per-user metadata, launch with existing settings/profiles/scripts/
   searches/extractions/layouts and verify compatibility. No app-folder settings files.
4. Open/edit/navigate/Undo/Redo across messages/tabs and Save/Save As synthetic HL7.
   Confirm lossless unchanged ranges, overwrite/version consent and working-text retention.
5. Open a large synthetic file/message, navigate abbreviated previews, inspect full
   source, cancel operations, and check responsiveness without implicit attachment decoding.
6. Search/extract and run a trusted script in both scopes; inspect report/output
   attribution, privacy-safe diagnostics, cancellation and worker exit. No installed dotnet.
7. Import synthetic FHIR R4, browse timeline/select events and generate a complete history.
8. Dock/float/close/reopen Generation Report; verify document association/navigation.
9. Single MLLP delayed-confirmation send/listen: exact payload, correlated AA, distinct
   write/ACK/application evidence, safe cancel/shutdown; no automatic retry.
10. Sequential batch review/pacing/pause/resume/cancel and privacy-safe report export.
11. Controlled load review/attestation/endpoint consent, global rate/concurrency bounds,
    ACK/latency/uncertainty metrics, Stop/drain and safe exports. No startup sends/listeners.
12. Keyboard-only navigation, text-input shortcut ownership, tab order, visible focus,
    screen reader names, empty states and confirmation/picker reachability.
13. Dark/light themes, high DPI/multiple monitors, floating/short windows and dialogs.
14. Corrupt/oversized/unsupported layouts and settings, unavailable/read-only source/
    export/settings paths and full disk; fixed safe statuses, no silent document loss.
15. Dirty document/script/extraction close Cancel/Save/Discard, active network/script
    cancellation and normal shutdown/relaunch. Test abnormal termination using synthetic
    data; no automatic PHI recovery/network restart, disclose residual temp files.
16. Clean-machine/read-only extraction folder: exercise UI, saves to user-selected
    locations and scripting; no writes needed beside application files. Verify all
    directory/archive checksums and no missing native/runtime dependency prompt.
17. Startup offline and observe network/process activity: no telemetry, updater or
    unrequested listener/connect/upload. User-trusted scripts retain their own rights.
18. Inspect release contents, provenance, exclusion rules, all third-party notices,
    artwork rights and owner beta usage terms. Review unsigned OS trust prompts and
    remaining license component questions. Obtain separate authorization before release.

## Changed-file inventory and stop

Complete checkpoint inventory: **46 paths (14 modified tracked / 32 new)**, all Stage 14. No protected fixture/artwork/TEST or unrelated file is changed. The exact staged inventory and subsequent clean-tree result are retained in artifacts/stage14-checkpoint/.

```text
 M .gitattributes
 M Directory.Build.props
 M README.md
 M build/ScriptHost.targets
 M build/VerifyScriptPublish.ps1
 M docs/PROJECT_CONTEXT.md
 M docs/architecture.md
 M docs/branding.md
 M docs/dependencies.md
 M docs/product-branding-acceptance.md
 M src/HL7Workbench.Desktop/MainWindow.Editing.cs
 M src/HL7Workbench.Desktop/MainWindow.axaml.cs
 M src/HL7Workbench.Desktop/Viewer/AboutWindow.cs
 M src/HL7Workbench.Scripting/ScriptWorkerSession.cs
?? build/BuildPortableCandidate.ps1
?? build/portable_release.py
?? build/test_portable_release.py
?? build/third-party/AvaloniaUI-Avalonia-NOTICE.md
?? build/third-party/AvaloniaUI-Avalonia-licence.md
?? build/third-party/AvaloniaUI-AvaloniaEdit-LICENSE
?? build/third-party/AvaloniaUI-angle-LICENSE
?? build/third-party/CommunityToolkit-dotnet-License.md
?? build/third-party/CommunityToolkit-dotnet-ThirdPartyNotices.txt
?? build/third-party/dotnet-roslyn-License.txt
?? build/third-party/dotnet-roslyn-THIRD-PARTY-NOTICES.txt
?? build/third-party/kekekeks-MicroCom-LICENSE
?? build/third-party/tmds-Tmds.DBus-COPYING
?? build/third-party/upstream-licenses.json
?? build/third-party/wieslawsoltes-Dock-LICENSE.TXT
?? docs/beta-user-guide.md
?? docs/beta-readme.md
?? docs/beta-distribution-review.md
?? docs/beta-usage-terms-proposed.md
?? docs/decisions/0019-portable-beta-readiness.md
?? docs/portable-dependencies.md
?? docs/portable-release.md
?? docs/release-notes-template.md
?? docs/stage14-release-readiness-acceptance.md
?? src/HL7Workbench.Core/packages.win-x64.lock.json
?? src/HL7Workbench.Desktop/packages.win-x64.lock.json
?? src/HL7Workbench.ScriptHost/packages.win-x64.lock.json
?? src/HL7Workbench.Scripting/packages.win-x64.lock.json
?? src/HL7Workbench.Transport/packages.win-x64.lock.json
?? src/HL7Workbench.Workspace/packages.win-x64.lock.json
?? tests/HL7Workbench.Desktop.Tests/ReleaseReadinessTests.cs
?? tests/HL7Workbench.Scripting.Tests/PortableWorkerTests.cs
```

One local Stage 14 checkpoint and the fresh candidate build are authorized as recorded above. No push/tag/distribution/upload/publication or next milestone. Useful ignored verification logs/XML, raw notice originals, hash/provenance manifests, direct-import diagnostic and initial/final marked candidates remain under artifacts/stage14; no uncertain artifacts or existing build trees were deleted.
