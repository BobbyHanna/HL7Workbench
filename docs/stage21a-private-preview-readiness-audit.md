# Stage 21A — Private preview readiness audit

Audit date: **2026-11-01**. Audience: approximately two trusted Windows HL7/interface
developers. Scope: current committed source, with a proposed future portable ZIP.
This report is the only repository change. No remediation, publish, ZIP creation,
distribution, dependency update or application-behavior change was performed.

## 1. Executive summary and recommendation

**Not ready to distribute the current source through the existing portable builder.**
The source application has substantial automated safety coverage and passed this
audit's regular verification. The distribution machinery remains explicitly scoped
to an older accepted preview. It does not yet establish a complete current-source
three-process deployment or its notices. Closing the three P1 findings would support
a **small, synthetic-only technical preview**, subject to owner approval of the exact
artifact and clean-machine results. This is not a recommendation for production or
real patient data.

| Priority | Findings | Meaning here |
| --- | ---: | --- |
| P0 | 0 | No confirmed critical vulnerability, source-corruption or serious data-loss defect found. This is not proof of their absence. |
| P1 | 3 | Current packaging completeness, current redistribution review, and exact-artifact/native acceptance gates. |
| P2 | 7 | CI coverage, build identification, sensitive storage, shutdown verification, multiple instances, onboarding and feedback. |
| P3 | 2 | Signing automation and expanded performance/soak evidence that can follow the initial bounded preview. |

Top five risks are **R01** (missing current query-worker deployment), **R02** (new
redistribution obligations outside the old notice pipeline), **R03** (no current
clean-machine/native artifact evidence), **R06** (plaintext storage and device-policy
boundaries), and **R07** (native shutdown/cleanup completion not established).

Stage 20C is committed, including both acceptance fixes. The Inspector/raw mismatch
was a real document-presentation defect; its regression tests now pass. Latest
GitHub Actions status for the audited commit is **Unknown**, not inferred from local
tests or earlier green runs. No current portable deployment success is claimed.

## 2. Scope, methodology and evidence quality

The developer review inspected project/lock graphs, startup and storage owners,
publication/cleanup paths, worker launching, privacy boundaries, tests, CI and the
historical release assembler. The tester review followed the documented routes from
receipt/extraction through synthetic input, inspection, editing, bulk output, Help
and feedback. No native UI session, clean VM, network capture, ACL experiment,
malware analysis or multi-hour soak was performed in this audit.

Evidence labels used below:

- **Verified defect/gap:** directly established in source/configuration or existing
  documentation. A release-tooling gap does not imply a current runtime bug.
- **Demonstrated risk:** an implemented behavior has a concrete exposure or limitation.
- **Potential risk / manual validation:** plausible from the lifecycle/environment,
  with no observed failure; explicitly not a confirmed defect.
- **Usability concern / missing documentation:** affects a tester's ability to finish
  a task or report it. **Deferred enhancement** is useful but not essential.

Orientation included [AGENTS.md](../AGENTS.md), [project context](PROJECT_CONTEXT.md),
[README](../README.md), the [current architecture](developer/architecture.md),
[build/test guide](developer/build-and-test.md), [workspace lifecycle](developer/workspace-lifecycle.md),
[security guide](developer/security-and-data-handling.md), [storage reference](reference/storage-and-formats.md),
[deployment procedure](portable-release.md), [release decision](decisions/0019-portable-beta-readiness.md),
[distribution review](beta-distribution-review.md), [dependency inventory](dependencies.md),
[native-license review](native-license-applicability-review.md), and the
[maintenance map](developer/documentation-maintenance.md).
The [Stage 19 audit](stage19-documentation-audit.md), [19B](stage19b-user-documentation.md),
[19C](stage19c-developer-documentation.md), [19D](stage19d-offline-documentation-help.md),
[20A](stage20a-field-inspector-and-fonts.md), [20B](stage20b-structured-field-editing.md)
and [20C](stage20c-bulk-field-operations.md) were reviewed as historical evidence,
not substitutes for current execution or native acceptance.

Source references are relative to this report; line numbers describe the audited
commit and may drift. No report contains real patient data or invented test results.

## 3. Verified environment and repository baseline

| Item | Observed value |
| --- | --- |
| Branch / HEAD | `main`, `cd71c482ec62351e0bf9b650a2f6bbb6a5d68c46` — “Search and replace fix” |
| Initial status | Clean working tree and empty index. Stage 20C files and publication tests are tracked in HEAD. |
| Earlier checkpoint | `a0f486d` follows `aeee55c`; current HEAD is later than both. No assumption that their packages contain current features. |
| SDK / MSBuild | .NET SDK 10.0.401; MSBuild 18.9.11 |
| Host | Windows version 10.0.26200, RID `win-x64`, x64; host runtime 10.0.12 |
| Target / runner | `net10.0`; Microsoft.Testing.Platform selected by [global.json](../global.json) |
| SDK policy | 10.0.401 floor, `latestFeature`, prerelease disabled |
| Public version / binding | VersionPrefix 0.1.0, suffix beta; AssemblyVersion 1.0.0.0, FileVersion 0.1.0.0 |
| Built product metadata | `Message Workbench`, ProductVersion `0.1.0-beta+cd71c482ec62351e0bf9b650a2f6bbb6a5d68c46` |
| Actions evidence | No `gh` executable available. Requests to the repository Actions endpoint and commit-filtered run API were inaccessible through the available web tool. Latest run result **Unknown**. No authenticated run evidence was obtained. |

Version properties come from [Directory.Build.props](../Directory.Build.props).
The source apphost is `HL7Workbench.Desktop.exe`; the older assembler renames its
copy to `MessageWorkbench.exe` without changing its assembly binding. The shared
`0.1.0-beta` label alone cannot identify a feature baseline (R05).

### Architecture and deployment boundaries

| Production project | Responsibility and ordinary dependencies |
| --- | --- |
| Core | Lossless parser/scanner, structural edits, generation, FHIR mapping and validation; no project/package dependency on UI, Dock, network or files. |
| Workspace | Documents, snapshots, history, captures, libraries, publication and feature sessions; references Core, Scripting, Querying and Transport. UI-independent. |
| Desktop | Avalonia/AvaloniaEdit, native dialogs, shell commands and Dock adapters; references Core/Workspace and compiler-only Roslyn. |
| Scripting | Trusted-script facade, protocol/client and helpers; references Core. |
| ScriptHost | Separate Roslyn execution process; references Scripting. |
| Querying | Typed SQL contracts/protocol/client; no SQLite/UI/Core assembly dependency. |
| QueryHost | Separate SQLite process and authorizer; references Querying and owns native SQLite. |
| Transport | TCP/MLLP framing, exchange/listener lifecycles; references Core. |

These relationships were checked against all eight `src/*/*.csproj` files. Desktop
imports [ScriptHost.targets](../build/ScriptHost.targets) and
[QueryHost.targets](../build/QueryHost.targets) for build ordering/copying, rather
than loading worker execution engines as UI assemblies. The two workers are lazy:
ordinary launch/file inspection does not require a running worker service.
There is no implemented general plugin marketplace/system. Supported customization
is trusted C#, saved queries/searches/extractions, profiles and existing preferences.

No WebGateway, InterSystems server, Docker, Python, NuGet access or development SDK
is referenced by the ordinary application startup path. Python/.NET SDK/NuGet are
build/audit prerequisites, not proposed recipient prerequisites. Transport needs an
explicitly chosen reachable test peer; FHIR uses local supported R4 files. Scripts
may themselves introduce arbitrary file/network requirements.

## 4. Findings organized by priority

Complexity S/M/L estimates the bounded follow-up, not a commitment or authorization.
Only R01–R03 are unconditional blockers to distributing a new current-source ZIP.
P2 work can use the stated workaround for the two-person synthetic preview; real
patient data has a separate, stricter gate in section 8.

### R01 — P1: legacy portable tooling does not deploy the current query process

**Category/type:** Deployment; verified configuration gap, not a reproduced bad ZIP.
[BuildPortableCandidate.ps1](../build/BuildPortableCandidate.ps1), lines 24–44,
publishes Desktop and ScriptHost only. [QueryHost.targets](../build/QueryHost.targets),
lines 3–12, copies a normal `bin/<Configuration>/net10.0/` tree after Build; it has
no RID-aware self-contained publish target. `portable_release.py`'s
`dependency_inventory` and `validate_tree`, lines 117–136, inventory/require only
Desktop and ScriptHost. Querying and QueryHost also lack the RID lockfiles present
in the other six production projects.

**Impact/verify:** A legacy assembler success cannot establish functioning SQL on
a runtime-free machine. Inspect these paths; after separately authorized remediation,
publish all three executables and execute real SQL with no installed .NET. This audit
did not run the builder or prove a newly created ZIP fails.

**Remediation:** Add reviewed, locked, RID-specific query-worker deployment and
require its apphost/runtime/native SQLite in completeness, integrity and worker smoke
checks. Preserve all older artifacts and their scoped reuse path. **M; automated
coverage: yes**, with clean-machine validation in addition.

### R02 — P1: current redistribution is outside the accepted notice inventory

**Category/type:** Licensing/provenance; demonstrated coverage gap and unresolved
human review, not a finding that the dependencies prohibit distribution.
[dependencies.md](dependencies.md) explicitly distinguishes the old 35-entry preview
from QueryHost and the adapted validation resource. The four SQLitePCLRaw 3.0.5
packages declare Apache-2.0; native SQLite 3.53.4 has a public-domain LICENSE.txt.
[copy_notices](../build/portable_release.py), lines 64–100, accepts only MIT expressions
and the specifically reviewed ANGLE file license. Its two-scope inventory excludes
QueryHost. Core embeds `ValidationCatalog/v251-model.json`; its
[NOTICE](../src/HL7Workbench.Core/ValidationCatalog/NOTICE.md) declares MPL-2.0 and
pinned source/change provenance. That resource is not a NuGet package and is outside
the old package-only inventory.

**Impact/verify:** Reusing the accepted notices would not demonstrate complete notices
or an appropriate covered-source delivery mechanism for the new artifact. Compare
all three eventual deps manifests and embedded resources against the notice manifest.
Review applicability for the actual recipients/organizations and the adapted source,
not only the upstream link or a primary SPDX identifier.

**Remediation:** Review and retain Apache/native notices, the MPL text/change notice
and an approved way for recipients to obtain the exact covered resource source.
Preserve native/.NET component notices and owner artwork/preview permissions. Do not
waive the assembler's fail-closed license checks or infer that the entire application
must become MPL. Mozilla explains organization/distribution distinctions and
covered-source availability in its [MPL FAQ](https://www.mozilla.org/en-US/MPL/2.0/FAQ/).
**M; automation: inventory/hash/notice-presence checks yes; rights/applicability no.**

### R03 — P1: current exact-artifact and native acceptance is unverified

**Category/type:** Release validation; potential risk requiring manual validation.
The older VM-accepted artifact predates current SQL/validation/Help/Stage 20 features.
Stage 19D and Stage 20A/B/C reports still contain pending native checklists, including
[the current bulk publication checklist](stage20c-bulk-field-operations.md#native-publication-reacceptance--pending).
This checkout's Actions result is Unknown. A local Release build and headless controls
cannot establish clean Windows launch, native focus/DPI or Defender behavior.

**Impact/verify:** Two testers could receive an incomplete, blocked or confusing
artifact despite green source tests. Follow section 17 on a genuinely separate,
standard-user, SDK/runtime-free Windows environment for the exact future archive;
record its checksum, source/build identity and results. Obtain current Actions evidence
and owner confirmation of any native acceptance completed outside the repository.

**Remediation:** Complete the artifact acceptance gate before transfer; this does not
require inventing a new feature. **M; automation partial**, native/OS checks required.

### R04 — P2: regular CI is not a complete preview release gate

**Category/type:** Verification coverage; verified gap.
[build.yml](../.github/workflows/build.yml) runs locked restore, Release build, regular
solution tests, documentation compiler and both reference checks. It does not run a
current portable worker completeness test, protected-file comparison, all-Markdown
link/anchor audit, dependency advisory scan, notice/artifact checksums or native smoke.
[test_portable_release.py](../build/test_portable_release.py) and
[test_release_compilation.py](../build/test_release_compilation.py) exist but are not
selected by that job and are scoped to their tested inputs.

**Impact/verify:** A green badge can be mistaken for a distribution approval. Read the
workflow and compare it with section 14. **Remediation:** Require the missing gates
manually for each preview initially; propose narrow CI additions only after approval.
Do not claim the old packaging tests cover QueryHost. **S/M; automation mostly yes.**

### R05 — P2: the visible version loses the build identity

**Category/type:** Diagnostics/usability; verified UI limitation.
[AboutWindow.cs](../src/HL7Workbench.Desktop/Viewer/AboutWindow.cs), line 24, displays
InformationalVersion after `Split('+')[0]`. The built DLL contains the current SHA,
but About shows only `0.1.0-beta`, also used by the older preview. FileVersion is
unchanged. **Impact/verify:** Ask a tester which binary they ran; About alone cannot
answer. Compare About's expression with the built DLL's product metadata.

**Remediation:** Give every proposed preview a unique external build ID/checksum and
provenance, with an approved version/build display change if desired. Do not change
assembly/settings identity merely to distinguish builds. Workaround: use a supplied
build ID and trusted archive hash in feedback. **S; automation yes.**

### R06 — P2: plaintext storage needs an explicit device/retention gate

**Category/type:** Privacy; demonstrated storage behavior plus unverified ACL/OS risk.
[SourceSnapshot.cs](../src/HL7Workbench.Workspace/SourceSnapshot.cs), lines 59–86,
creates full plaintext captures under per-user-named temporary roots; Windows
permissions inherit from the actual directory policy. Lines 113–144 perform best-effort
deletion and bounded old/unlocked-directory cleanup. SQL/report journals and bulk
captures reuse this storage. There is no application encryption/secure-erasure promise.
Saved definitions/exports and the clipboard have separate persistent exposure.

**Impact/verify:** Crash residue, redirected/shared TEMP, backups, another same-account
process or screenshots can disclose clinical content. With synthetic data, inspect
effective ACLs under a second standard account, normal close and forced termination,
then inspect later cleanup. No unauthorized ACL read or PHI leak was demonstrated here.

**Remediation:** Synthetic-only preview, approved device/storage policy, deliberate
clipboard/export handling and a tested retention procedure. Assess real-data controls
in section 8 before changing that policy; encryption is not an audit-only implementation
instruction. **M; automation partial**, ACL/OS-policy review required.

### R07 — P2: whole-application shutdown needs native drain evidence

**Category/type:** Lifecycle; potential risk, not observed data loss.
[MainWindow.Editing.cs](../src/HL7Workbench.Desktop/MainWindow.Editing.cs), lines 49–93,
explicitly drains batch/load work and guards save/generation/draft prompts. The
[Closed handler](../src/HL7Workbench.Desktop/MainWindow.axaml.cs), lines 112–130,
disposes models/documents synchronously. [MessageDocument.Dispose](../src/HL7Workbench.Workspace/MessageDocument.cs),
lines 292–309, schedules asynchronous cleanup. SqlQueryViewModel exposes a separate
`Disposal` drain. The fixed [SQL fixture](../tests/HL7Workbench.Desktop.Tests/SqlQueryToolTests.cs)
awaits all tracked document drains, including already closed outputs; synchronous
manager disposal alone was insufficient for that fixture.

**Impact/verify:** It is unverified whether all native process-exit paths give pending
cleanup/worker work time to finish. Test exit during indexing, validation, SQL, trusted
scripting, bulk analysis/output, listener and batch/load, checking workers, handles and
owned storage after exit/restart. Passing fixture drains do not prove native shutdown.

**Remediation:** Establish the evidence first; propose a bounded owning-context shutdown
barrier only if needed. Workaround: cancel/stop tools, let them complete, save desired
outputs and close normally. No sleeps/retries or automatic forced data discard.
**M; automation partial**, native process-exit tests needed.

### R08 — P2: multiple instances share settings without coordinated ownership

**Category/type:** Persistence; potential reliability risk with a demonstrated overwrite
policy. No single-instance mutex/IPC coordination was found in startup. Preferences
use atomic `File.Move(..., overwrite: true)` in
[EditorPreferences.Save](../src/HL7Workbench.Workspace/EditorPreferences.cs), lines 30–43;
layout saves do the same in MainWindow. Unique staging names prevent torn/shared-temp
deletion, but do not prevent one instance replacing another's latest preferences.
Individual definition revision guards and explicit source-save protection are separate.

**Impact/verify:** Two instances can plausibly restore different layouts and overwrite
settings on close. Native two-instance behavior was not executed. Use synthetic work,
change preferences/layout in each, close in opposite orders and inspect the final state.
**Remediation:** Document one instance for the preview; decide on last-writer behavior
or coordination after evidence. Do not infer source corruption. **S/M; automation yes.**

### R09 — P2: first-run Help assumes repository fixtures and older inspector wording

**Category/type:** Onboarding; verified documentation/usability gap.
[getting-started.md](user/getting-started.md), lines 3–18, requires a repository fixture
and says inspectors do not edit values directly. Current context actions include Edit
Value and bulk review, so this wording can mislead. The legacy package deliberately
excludes HL7 fixtures, and offline Help classifies non-bundled repository links rather
than downloading them ([DocumentationTests](../tests/HL7Workbench.Desktop.Tests/DocumentationTests.cs),
`ApiLinksUseExistingReferenceAndOtherReferencesHaveNoExternalAction`).

**Impact/verify:** A ZIP-only, offline tester cannot follow the first walkthrough's
prerequisite unless input is separately supplied. Read that page in a repository-free
extraction. **Remediation:** Start the recipient guide with **File → Generate Messages…**
or deliberately approve a small hashed synthetic sample set; accurately explain
context-menu edits and where Bulk Result opens. Never broadly relax package data
exclusions. **S; automation for links/content yes, new-tester walkthrough manual.**

### R10 — P2: feedback instructions lack a ready-to-use sanitized template

**Category/type:** Missing documentation/diagnostic process.
[PRIVATE-PREVIEW.txt](PRIVATE-PREVIEW.txt) identifies the private supplier as the feedback
contact and excludes PHI/credentials/dumps. Troubleshooting lists useful context, but
there is no complete recipient feedback template and no Report an Issue action in
[MainWindow.axaml](../src/HL7Workbench.Desktop/MainWindow.axaml), lines 48–52.
Release development diagnostics are compiled out in
[App.axaml.cs](../src/HL7Workbench.Desktop/App.axaml.cs); OS crash collection is external.

**Impact/verify:** Testers may send ambiguous version labels, unsanitized screenshots
or full dumps to explain a failure. Inspect the supplied recipient files and Help.
**Remediation:** Use section 13's manual text template and named private contact.
No automated collection/upload is needed for two testers. Workaround: request a
synthetic reproduction before receiving files. **S; automation content checks only.**

### R11 — P3: code signing and trust automation can follow a controlled first preview

**Category/type:** Deferred enhancement; unsigned-delivery/SmartScreen risk remains
subject to R03's manual gate. Historical candidate wording explicitly says unsigned;
the current builder contains no signing stage. Checksums detect changes, not publisher
authenticity. No current artifact's signature or Defender verdict was measured.

**Impact/verify:** MOTW, enterprise policy or reputation may block launch. Test the
actual approved transfer/extraction route; never instruct testers to disable security
software or bypass workplace rules. **Remediation:** Trusted private channel, separately
authenticated hash and explicit unsigned status initially; assess Authenticode/certificate
cost and policy if useful after feedback. **M; signature verification automated,
reputation/enterprise policy manual.**

### R12 — P3: broader native performance and long-session evidence remains limited

**Category/type:** Deferred measurement, not a proven leak or performance defect.
Historical [source-performance](stage3-performance.md) and
[SQL measurements](stage15d-sql-final-acceptance.md) are scoped observations, not current
low-end-PC acceptance. Regular tests omit explicit SQL stress and can select smaller
large-source branches. Stage 20C bounds unique values/previews rather than proving
every near-ceiling workflow responsive on recipient hardware.

**Impact/verify:** Large real-world workloads and multi-hour dock/float/open/close use
may behave differently. Collect synthetic timings, input sizes and resource metadata
without payloads. **Remediation:** Start with bounded realistic synthetic batches;
measure reported bottlenecks, then schedule deliberate soak/stress checks without
raising limits or creating massive fixtures by default. **M; automation partial.**

## 5. Clean Windows deployment and portable ZIP assessment

A self-contained directory ZIP is technically feasible and fits the two-person
audience. It is **not a zero-state application**: writable per-user settings/temp
storage are required for those operations. A self-contained .NET deployment bundles
its runtime, but platform/native dependencies still require verification; see
[Microsoft's deployment overview](https://learn.microsoft.com/en-us/dotnet/core/deploying/).

| Proposed setting | Recommendation / reason |
| --- | --- |
| RID / architecture | `win-x64` only for the initial preview. No x86 or native ARM64 support claim. ARM64 emulation is unverified and unnecessary for the first two recipients. |
| Framework/runtime | `net10.0`, self-contained **for Desktop, ScriptHost and QueryHost separately**. Current legacy recipe pins runtime 10.0.12; reconfirm the serviced patch/lock graph when a new build is approved. Do not silently update it during this audit. |
| Publish shape | Multi-file directory, `PublishSingleFile=false`; preserve adjacent worker apphosts/runtimeconfig/deps/satellites and native DLLs. Worker resolvers use absolute `AppContext.BaseDirectory` paths and adjacent apphosts, not the extraction working directory. |
| Trimming / AOT | `PublishTrimmed=false`, no NativeAOT. Roslyn scripting/dynamic compilation, reflection/resource use and UI dependencies make these separate compatibility projects. |
| Other compiler flags | Release, deterministic mapped source paths; retain existing Desktop Release no-symbol policy, CI build setting and private-path guards. Initially keep `PublishReadyToRun=false` as in the reviewed procedure. |
| OS target | Prefer a currently supported Windows 11 x64 release. Do not advertise all Windows 10 editions merely because the old guide says “10/11.” Verify any requested Windows 10 LTSC/Enterprise target against [Microsoft's current support table](https://learn.microsoft.com/en-us/dotnet/core/install/windows). |
| Recipient prerequisites | Supported patched OS, usable graphics/native rendering stack, writable per-user LocalAppData and TEMP, sufficient disk space, normal extraction preserving all files. No separately installed SDK, .NET Desktop Runtime, SQLite server, Python, Docker, WebGateway or InterSystems dependency expected. Additional VC/native prerequisites on the chosen clean OS remain **Unverified**. |
| Storage | `%LOCALAPPDATA%/HL7Workbench` for configuration/explicit libraries; owned TEMP roots for plaintext snapshots/journals. HL7/CSV/report output only at explicit destinations. Do not place state beside the executable to make it “portable.” |
| Read-only extraction | Expected to work with writable per-user/temp storage because worker/resource paths are app-relative and data paths are per-user. A new current-source read-only-folder launch is **Unverified**. |
| Spaces / Unicode / cwd | Worker ProcessStartInfo uses ArgumentList and absolute worker paths, avoiding shell quoting; OS pickers supply actual paths. Full current-source launch and both workers from spaces/Unicode paths and an unrelated working directory are **Unverified**. |

The single-file alternative complicates native extraction, resource/worker layout
and inspection without a practical benefit for two testers. Multi-file duplicates
runtime files across processes and is larger, but makes ownership and completeness
explicit. Self-contained runtimes need republishing to receive runtime fixes; they
do not inherit an installed runtime's servicing automatically.

Proposed future layout, **not created in this audit**:

```text
MessageWorkbench/
  MessageWorkbench.exe
  HL7Workbench.Desktop.dll + deps/runtimeconfig + managed/native runtime files
  ScriptHost/  (complete self-contained worker)
  QueryHost/   (complete self-contained worker + e_sqlite3 native assets)
  README.md / USER-GUIDE.md / RELEASE-NOTES.md / PRIVATE-PREVIEW.txt
  BUILD-PROVENANCE.json / SHA256SUMS.txt / dependency-licenses.json
  THIRD-PARTY-NOTICES.md / ThirdPartyNotices/
  covered-source/  (exact approved validation resource, notice/license delivery)
```

The covered-source layout is a proposal requiring R02 approval, not a replacement
license determination. No arbitrary patient/sample files, settings, logs, credentials,
build paths, repository checkout or debug dumps should be packaged. Current synthetic
fixtures are inventoried below; including any requires a narrow reviewed allowlist.
Keep candidate markers until exact-artifact/recipient/transfer authorization. Do not
overwrite or repackage the 2,384 protected historical files.

## 6. Startup and application lifecycle assessment

| Condition | Evidence and current conclusion |
| --- | --- |
| Fresh settings/workspace | ApplicationPreferences/EditorPreferences supply defaults; ScriptEditorSession catches unavailable library reads; tools are registered lazily. DefaultWorkspaceLayoutTests exercise fresh tools/layout. Automated evidence exists; no fresh native profile launch here. |
| Corrupt/incompatible settings/layout | ApplicationPreferencesTests cover corrupt/oversized data, unknown fields and failed saves. ReleaseReadinessTests cover corrupt startup layouts without clinical disclosure/network-tool creation. Recoverable fallback, not automatic rewriting of every bad file. |
| Missing/unwritable directories | Save paths create directories explicitly and feature handlers report fixed failures. MainWindow does not require writing the extraction folder at startup. Fully denied/redirected profile/TEMP, disk-full and native file-picker behavior remain manual. |
| Actual startup blockers | Wrong OS/architecture, missing host/runtime/native files, blocked executable or inaccessible required resources can prevent UI startup before feature recovery exists. No current-source package was tested to rule these out. |
| Read/open failure | MainWindow guards opening, cancellation and access/format/size errors; staged documents are not intentionally published partially. Stage3LifecycleTests cover failed/canceled capture and source deletion after capture. |
| Dirty close | DocumentSaveWorkflow and shell use Save/Discard/Cancel with revision checks; a later canceled prompt retains earlier drafts. Saves use explicit destination identity/staging proof, not unconditional replacement. Native modal focus remains manual. |
| Shutdown while active | Batch/load drains, generation/save guards and cancelable feature lifetimes exist. R07 records the whole-process completion gap; no observed current shutdown defect is asserted. |
| Crash/restart | Layout restores placement, not unsaved clinical contents. No autosave/recovery of message drafts was found. Crash loss of unsaved work is an expected limitation; abandoned plaintext cleanup is bounded/best-effort. |
| Multiple instances | Unique staging and snapshot leases protect specific operations; no global instance ownership found. Settings last-writer and native exit behavior require R08 checks. |
| Floating restore | Dock adapters store bounded placement DTOs and recover offscreen geometry; documents stay in the central workspace per ADR 0003. Existing headless/DTO tests do not prove mixed-monitor native recovery. |

The SQL fixture race was fixed by awaiting tracked asynchronous cleanup, including
closed outputs, before recursive deletion. It does not establish current SQL assertion
failure, nor justify claiming that all native shutdown paths drain. No new failure
was repaired or test expectation changed in this audit.

## 7. Core workflow readiness matrix

“Automated” means covered by existing tests included in the current passing full run,
not a newly performed native journey. Runtime package journeys remain gated by R01–R03.

| Journey | Verified implementation/test evidence | Native or release acceptance still needed |
| --- | --- | --- |
| A — launch/open/inspect/navigate/copy/Help | Shared Core scanner and source ownership; Stage3SourceTests/Stage3LifecycleTests; InspectorRedesignViewTests; DefaultWorkspaceLayoutTests; DocumentationTests/DocumentationViewTests. | Clean profile launch, real picker, keyboard/clipboard/history, long values, high DPI and offline Help without repository. |
| B — search/filter/unique/extract/SQL/source navigation | MessageSearch/FieldExtraction sessions; BulkFieldOperationsTests exact grouping/complete filter IDs; SqlQueryToolTests all-page CSV/matching-message/source identity; QueryHardeningTests authorizer/budgets. | QueryHost self-contained deployment, real long-session responsiveness and native focus when navigating results. |
| C — edit/undo/redo/Save As/reopen | PathAndEditingTests, StructuredValueEditingTests, EditableDocumentTests, FileSaveDestinationTests, StructuredValueEditingViewTests; exact bytes, draft retention and stale-target checks. | Actual IME/clipboard, native Save As/read-only destination prompts, reopen on recipient filesystem. Save As preserves the original file; authorized Save can intentionally replace it. |
| D — bulk preview/apply/result/original | BulkFieldViewTests.Publication: 30-message Avery→Morgan, full/current scope, real result Dock wrapper, raw+Inspector agreement, message 17, original clean, failure rollback/retry and repeated Apply; docked/floating cases. BulkFieldOperationsTests prove exact output/untouched regions. | Reaccept the specific native mismatch case on the exact artifact; confirm tab activation, modal ownership and source return with floating/docked tools. Prior bug is fixed in source, native acceptance remains Unverified. |
| E1 — trusted C#/Script Library | ScriptEditorSession/library revision handling, generated API, separate ScriptHost, script worker/report tests; no automatic execution at startup. | Runtime-free apphost, Roslyn dependencies, trusted synthetic starter, cancellation/exit and custom script side effects. |
| E2 — schema validation | SchemaRibbonTests and SchemaValidationLifecycleTests; explicit application, import/library reuse, revision ownership and stale-result rejection. | Native ribbon/dialog focus and custom-library storage. Incomplete coverage is not clinical conformance. |
| E3 — generation/FHIR | SyntheticGenerationViewTests, GenerationReportToolTests and FhirWorkflowTests; replay seeds, guarded complete publication, offline local import and report association. | Generator discoverability, result activation, arbitrary imported records are not presumed synthetic; partial mapping review. |
| E4 — TCP/MLLP/batch/load | TransportNetworkTests real loopback, ACK/cancel/restart/security guards; transport/batch/load UI tests. | Approved loopback/native listener and firewall prompts; non-loopback only with separately authorized test destination. No TLS or production-use claim. |

See the [current user guides](user/index.md), [bulk workflow](user/inspect-and-edit.md#analyze-unique-values-and-create-a-bulk-result),
[SQL guide](user/query-with-sql.md), [validation guide](user/validate-with-profiles.md)
and [transport guide](user/test-with-mllp.md). No command palette, shortcut redesign,
new editor, new parser or additional workflow was introduced by the audit.

## 8. PHI, privacy and security assessment

Source inspection found no application HTTP telemetry client, updater, automatic
diagnostic upload or account/cloud dependency. BuildServices telemetry assets are
excluded; audit/CI commands explicitly opt out of CLI/testing telemetry. This is
source evidence, not an observed packet trace of an eventual package. Security tools,
OS crash reporting and arbitrary trusted scripts remain outside that statement.

| Data pathway | Implemented behavior / exposure |
| --- | --- |
| Activity Log | ActivityLog.cs exposes typed fixed categories/counts/anonymous document ordinals, not a free-text content logger; bounded 2,000 session entries. ActivityLogTests reject imported text/path diagnostics. No ordinary persistent HL7 content log found. |
| Exceptions/crashes | Ordinary feature statuses use fixed categories; worker diagnostics avoid raw source/exception text. DEBUG-only lifecycle logs contain type/stack, omit exception messages. Release has no equivalent crash collector. OS WER/event/dump policy can contain process data; never assume dumps safe. |
| Snapshots/spools/captured input | Entire HL7 files/drafts may be copied to plaintext temp storage, even read-only/current-scope bulk analysis. Names avoid patient identifiers; same-user/ACL/backups/abnormal-exit risks remain R06. |
| SQL values/results/history | Query captures and sealed journals can contain full values/paths represented as clinical columns. Results/history are bounded session state; explicitly saved SQL names/literals persist. No external DB or SQL-file access authority is advertised. |
| Trusted C# | Runs under the user's filesystem/network account rights. Resource/job/pipe containment is not a sandbox. Console suppression and source preservation cannot undo arbitrary file writes/transmissions by trusted code. |
| Clipboard | Explicit copy can contain PHI. OS history/sync/other readers are external; Workbench does not retract it or promise secure clearing. |
| Recent files / preferences / layout | No recent-file list mechanism found. DTOs retain placement/preferences, not restored clinical contents. Source filenames/titles can still be visible in UI. Explicit definitions may embed sensitive literals. |
| Profiles / search / extraction / scripts | Saved names, predicates, source and custom-profile definitions are plaintext local records; separate revision/size constraints are not encryption. |
| Endpoints / credentials | TransportProfiles stores whitelisted endpoint metadata, not payload/session/credentials. Host, name and Notes can reveal infrastructure or contain user-entered secrets; no credential field/vault/TLS authentication exists. Do not put secrets in notes or scripts. |
| Generated/bulk output | Independent complete data can include out-of-scope source messages. Preview limits do not narrow eventual export scope. Unsaved generated snapshots and reports have their own lifetime. |
| HL7/CSV/report exports | Explicit, persistent destination selected by user; all retained rows may export beyond a visible page. Existing quoting/formula safeguards do not anonymize clinical values. Destination consent/staging is distinct from content approval. |
| TCP/MLLP | Explicit destination/review/send or listener start; defaults are loopback, port 2575. Non-loopback requires confirmation. Plain TCP has no TLS/authentication; write completion is not application acceptance. Listener payload/ACK values are session data. |
| FHIR import/mapping | Local bounded supported R4 data; no remote reference fetch. Imported files are not verified synthetic. Patient UI and generated HL7 can disclose all supplied sensitive values despite “synthetic” workflow history. |
| Diagnostics/de-identification/generation reports | Counts/fixed summaries have narrower policies than clinical exports; user script report cells can contain arbitrary content. Review all output, including screenshots and report details. No de-identification guarantee. |
| Updates/third-party components | No automatic app updater found. NuGet scans are development operations. Rendering/native libraries and runtime have their own servicing/OS boundaries; advisory results are not a full binary security assessment. |

### Controls required before any real patient data

Keep initial recipients **synthetic-only**, including feedback and loopback traffic.
Before relaxing that restriction, require all of the following, with accountable review:

1. Organization authorization, purpose, retention/deletion and approved device/location.
2. Effective Windows ACL review for actual LocalAppData/TEMP roots, including redirected
   profiles and a second account; encrypted-device/backup policy as appropriate.
3. Normal close/crash/restart tests of every content owner and documented residual-file
   handling that never deletes another live instance's storage.
4. OS WER/dump/Defender/quarantine, backup, indexing and clipboard history/sync review.
5. No untrusted C#; inspect libraries/report code and any file/network side effects.
6. No unencrypted clinical TCP testing; approved isolated endpoints and protocol/security
   requirements must be established outside the current plain-TCP tool's guarantees.
7. Review saved definitions, endpoint notes, filenames, exports and full bulk/SQL scope;
   transmit no diagnostic dump/screenshot/file without explicit manual inspection.
8. Recheck runtime/native/package advisories and license/source delivery for the actual
   artifact; inventory and authenticate its exact bytes.
9. Train testers on source/working/saved/output identity, unsaved-work loss and partial
   validation/mapping/de-identification coverage.
10. Record privacy/security owner approval. Local-first alone establishes neither
    HIPAA compliance nor general “PHI safe” status.

## 9. Dependency, asset and redistribution inventory

Ordinary production lockfiles contain **47 unique package/version pairs**. That count
includes build-only packages and non-Windows native assets; it is not a claim that all
47 ship in a Windows ZIP. Every restored production nuspec was read for its license
declaration. Exact current published native/runtime selection is **Unverified** because
publishing is prohibited. Package reuse across processes must be inventoried per scope.

### Direct production references

All versions below match [central versions](../Directory.Packages.props) and ordinary
locks. “Yes” means include the selected publish dependency, not copy every NuGet file.

| Package | Version | Purpose / owner | License / redistribution | Bundle / native |
| --- | --- | --- | --- | --- |
| Avalonia | 12.1.4 | Desktop UI | MIT; retain copyright/license/notices | Yes; framework managed layer |
| Avalonia.Desktop | 12.1.4 | Platform integration | MIT; retain notices | Yes; pulls platform/native assets |
| Avalonia.Themes.Fluent | 12.1.4 | Desktop styling | MIT; retain notices | Yes; managed/resources |
| Avalonia.AvaloniaEdit | 12.0.0 | Raw/C#/SQL editors | MIT; retain notices | Yes; managed |
| Dock.Avalonia | 12.1.0.7 | Dock controls/hosts | MIT; retain notices | Yes; managed/UI |
| Dock.Model.Mvvm | 12.1.0.7 | Dock adapter model | MIT; retain notices | Yes; managed |
| Dock.Avalonia.Themes.Fluent | 12.1.0.7 | Dock styling | MIT; retain notices | Yes; resources |
| Microsoft.CodeAnalysis.CSharp | 5.9.0 | Desktop compiler-only symbols/colors | MIT; retain Roslyn/component notices | Yes; managed/satellites; no ScriptHost execution in UI |
| Microsoft.CodeAnalysis.CSharp.Scripting | 5.9.0 | ScriptHost execution | MIT; retain Roslyn/component notices | ScriptHost; managed/satellites |
| SQLitePCLRaw.bundle_e_sqlite3 | 3.0.5 | QueryHost SQLite composition | Apache-2.0; retain license and applicable notices | QueryHost; brings native SQLite |
| Avalonia.BuildServices | 11.3.2 | Explicit build-telemetry suppression reference | MIT metadata | **No**: `ExcludeAssets=all`, `PrivateAssets=all` |

No inspected primary declaration prohibits redistribution outright; license obligations
and owner/recipient permissions still apply. This is a technical inventory, not formal
legal clearance. Existing Apache/MIT labels do not replace bundled component notices.

### Remaining resolved production packages

Grouped rows enumerate the other 36 package IDs; each member has the stated version,
purpose, license and distribution treatment.

| Package IDs | Version | Purpose/license | Future Windows bundle treatment |
| --- | --- | --- | --- |
| Avalonia.FreeDesktop; Avalonia.FreeDesktop.AtSpi; Avalonia.HarfBuzz; Avalonia.Native; Avalonia.Remote.Protocol; Avalonia.Skia; Avalonia.Win32; Avalonia.X11 | 12.1.4 | Platform/rendering/accessibility framework; MIT | Keep selected untrimmed managed graph and required notices. Cross-platform managed assemblies do not establish automatic networking. Native selection follows win-x64 assets. |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | ANGLE graphics; bundled LICENSE with BSD-style conditions and component terms | Required selected Windows native assets; retain full original license/notice texts, including no-endorsement conditions. |
| Dock.Controls.DeferredContentControl; Dock.Controls.ProportionalStackPanel; Dock.Controls.Recycling; Dock.Controls.Recycling.Model; Dock.MarkupExtension; Dock.Model; Dock.Settings | 12.1.0.7 | Dock controls/model/layout; MIT | Selected managed graph; retain notices. |
| CommunityToolkit.Mvvm | 8.4.0 | Dock model support; MIT | Managed runtime; retain notices. |
| MicroCom.Runtime | 0.11.6 | Native interop support; MIT | Managed interop runtime; retain notices. |
| Tmds.DBus.Protocol | 0.94.1 | Cross-platform protocol support; MIT | Retain if present in untrimmed graph; does not imply Windows startup requires a DBus service. |
| Microsoft.CodeAnalysis.Common; Microsoft.CodeAnalysis.Scripting.Common | 5.9.0 | Compiler/scripting support; MIT | Include only correct Desktop/worker scopes with satellites and Roslyn notices. |
| Microsoft.CodeAnalysis.Analyzers | 5.9.0-1.26328.17 | Build analyzers; MIT | No application runtime bundle. |
| HarfBuzzSharp; HarfBuzzSharp.NativeAssets.Win32 | 8.3.1.3 | Text shaping; MIT primary plus native component notices | Managed plus selected Windows native libraries; preserve full HarfBuzz notices. |
| HarfBuzzSharp.NativeAssets.Linux; HarfBuzzSharp.NativeAssets.macOS; HarfBuzzSharp.NativeAssets.WebAssembly | 8.3.1.3 | Other-platform shaping; MIT metadata | Do not copy unrelated RID native payloads into win-x64 ZIP. |
| SkiaSharp; SkiaSharp.NativeAssets.Win32 | 3.119.4 | Rendering; MIT primary plus native component notices | Managed plus selected Windows native libraries; preserve Skia/Wuffs/component notices. |
| SkiaSharp.NativeAssets.Linux; SkiaSharp.NativeAssets.macOS; SkiaSharp.NativeAssets.WebAssembly | 3.119.4 | Other-platform rendering; MIT metadata | Exclude unrelated native payloads. |
| SQLitePCLRaw.config.e_sqlite3; SQLitePCLRaw.core; SQLitePCLRaw.provider.e_sqlite3 | 3.0.5 | SQLite initialization/ABI/provider; Apache-2.0 | QueryHost managed scope; license and applicable NOTICE retention. |
| SQLite | 3.53.4 | Native SQLite engine; bundled LICENSE.txt explicitly declares public domain | QueryHost selected Windows native asset required; retain original provenance/license text. |

Additional distribution material:

| Material | Evidence / treatment |
| --- | --- |
| .NET runtime pack | Old portable locks/recipe pin Microsoft.NETCore.App.Runtime.win-x64 10.0.12, MIT primary with substantial component notices. Include self-contained runtime for each process; inspect actual new runtime-pack assets and notices before approval. Not counted among the ordinary 47. |
| Validation metadata | Core embeds adapted nHapi v2.5.1 declarations pinned to `9027797b1fd1faebcffd743ac11324499145f29d`, with source paths/digests and MPL-2.0 attribution. No nHapi runtime/parser dependency or complete HL7 standard prose. R02 applies to source/notice delivery. |
| Native supplementary notices | `build/third-party/upstream-licenses.json` pins upstream text/digests; historical HarfBuzz/Skia/Wuffs applicability review is retained. Do not remove broad native/font notices merely because some described components may be absent. Reconcile actual selected binaries. |
| Documentation / API assets | Curated Markdown compiles to embedded `Documentation/catalog.json`; Script API behavior/contracts are local resources. Preserve repository-authored content and referenced third-party attribution; no WebView, remote documentation loader or downloaded website is needed. |
| Fonts/icons/images | Desktop Assets contains the approved PNG/ICO derivatives; no separately added font file found there. Owner artwork permission is recorded in the earlier release/branding review. Theme/native component fonts/notices require their existing attribution review; do not claim every notice describes a shipped font. |
| Synthetic samples | `samples/synthetic-only` contains invented HL7 and local synthetic FHIR examples plus authoring scripts/readmes. Current legacy packaging excludes fixtures. Any future selected sample must be reviewed/hashed explicitly, with origin/redistribution confirmed; no patient data or third-party Synthea corpus is assumed approved. |
| Test/build dependencies | Avalonia.Headless 12.1.4 is test-only; xunit.v3 4.0.2 declares Apache-2.0; MTP/support packages are not recipient components. Build/testing telemetry packages are not evidence of application telemetry. Do not ship test outputs/analyzers. |
| Owner application/name | Approved Message Workbench/MW branding and PRIVATE-PREVIEW notice remain the baseline. No new application open-source grant, trademark clearance, employer permission or unrestricted redistribution right is inferred. Public-name clearance is unverified and can be revisited before broader distribution. |

Licensing blocker/unknown: **exact current-artifact notice completeness and adapted
resource source-delivery/recipient review remain unresolved**. No unknown primary
license was found among the 47 inspected production nuspecs; native component scope,
covered-source obligations and exact runtime-pack inventory still need human review.
Do not reuse the old “35 entries / MIT plus ANGLE” label for the new package.

## 10. Performance and stability

| Area | Existing evidence / limits / remaining gap |
| --- | --- |
| Large source/indexing | SourceSnapshot hard capture ceiling is 16 GiB, not a promise that all such inputs are practical. Large source-backed viewing uses bounded decoding/caches and distinct byte/UTF-16/window coordinates; no implicit attachment decode/render. Stage3 lifecycle/source tests cover malformed bytes, source deletion, cancellation and cleanup. |
| Inspector/malformed/large values | Shared Core scanner, bounded virtualized presentation and incomplete/stale labels; exact original exports use source ranges, not omission markers. Native GPU/font/clipboard timing remains unverified. |
| Historical source measurements | Stage3 report observed a 100 MiB attachment's first abbreviated headless layout at 30.5 ms **after preparation**, displaying 736 units; excludes capture/decode/native latency. It is historical evidence, not a 2026-11-01 benchmark. |
| SQL | Stage15D report observed 100,000 metadata aggregate at 3,204 ms and full 100,000-row transfer at 4,134 ms in its final measurement. A 10,000-message Fields join reached the 64 MiB accounted import cap. Those workloads/timings are not fresh tests here. Authorizer/native heap/process/time/result limits remain explicit. |
| Unique/bulk operations | Complete capture even for current scope; 1 GiB source/output, 100,000 scoped messages, 250,000 slots/preview rows, 4,096 distinct values, 65,536 characters/value, 2 Mi retained value characters. Mutations retain existing complete-message/line/history limits and bounded overlays. Exact limits can reject otherwise readable data. |
| Streaming output/cancel | Core serialized overlays plus MessageSavePlan stream complete untouched partitions and verify index/bytes before publication. Cancellation, output limits and stale preview tests prove no partial output publication in those cases. |
| Repeated open/close/resources | Stage3LifecycleTests, worker lifetime tests, SqlQueryToolTests cleanup regression, validation lifecycle and bulk disposal tests give concrete bounded evidence. Full native exit/long-running sessions remain R07/R12. |
| Stress selection | This audit did not set HL7WORKBENCH_STRESS or select xUnit explicit tests. Fourteen regular skips are expected SQL opt-ins. Ordinary passes do not establish the larger optional branches or all hard-cap workloads. |

Start the preview with realistic synthetic tens/hundreds of messages and bounded
larger batches. Ask recipients to report size/count/scope/time and failure category,
not content. There is no need to generate enormous artifacts or raise existing caps
before two testers provide evidence of actual limits that impede their work.

## 11. Documentation and first-run assessment

| Tester question | Current answer / gap |
| --- | --- |
| How do I start? | Installation explains full extraction and MessageWorkbench.exe for the older approved artifact, source commands separately. Needs a new exact-build recipient supplement; no current download is implied. |
| Open a file / navigate messages? | Getting Started, workspace/inspect guides and raw navigation controls explain this; first fixture prerequisite is repository-only (R09). |
| Where is Inspector? | View → HL7 Inspection and default right-hand tools are documented; recovery uses existing layout commands. |
| Edit safely? | Edit Mode, bounded messages, raw/structured edit, Undo/Redo and Save As are covered; wording in the first walkthrough should be reconciled with current context actions. |
| Search & Replace / result location? | Detailed inspect/edit and troubleshooting guides now show visible Find/Replace/New value, exact raw matching, empty replacement, invalidation and separate active Bulk Result tab. |
| Save output / preserve source? | Explicit Save As and four kinds of content are explained. Complete result scope includes unaffected messages; no automatic source overwrite from bulk Apply. |
| Offline Help? | Help → Documentation uses the embedded curated catalog, search/history and existing Script API tool. No repository or internet needed for included topics; repository-only resources are not magically bundled. |
| Experimental/limited features? | Privacy/limits guides disclose trusted scripting, partial mapping/conformance, plaintext storage and TCP uncertainty. Source feature descriptions must not be attributed to the old binary. |
| Report a problem / avoid sharing? | Private supplier contact, synthetic reproduction, PHI/dump warnings exist; build ID and ready template need R05/R10 follow-up. |

The menu/ribbon architecture is established and tested. Do not implement command
palettes, navigation redesigns or shortcut changes before feedback. First-run success
can be improved with a small recipient guide using existing Generate Messages rather
than adding a new first-run wizard. Existing historical reports should remain intact;
record newly completed native acceptance explicitly rather than treating stale “pending”
notes as proof either of a bug or of acceptance.

## 12. Feedback and diagnostics recommendation

For two recipients, use a **manual private text template**, a unique supplied build ID,
and an agreed contact/channel. No automatic transmission, account, support backend,
message capture or dump attachment is warranted. Help → Report an Issue is optional
Stage 21C work only if testers cannot find the template; it should not be a prerequisite
and must never silently collect/transmit data.

```text
Message Workbench preview build ID / archive checksum:
Windows edition/build, x64, display scaling/monitor setup:
Feature, scope and relevant settings (omit sensitive literals/endpoints):
Synthetic input origin, approximate size and message count:
Steps to reproduce:
Expected result:
Actual result and fixed status/error category:
Reproduces after normal restart? / Frequency:
Any unsaved work or running operations? (describe state, not content)
Optional screenshot: synthetic only, manually checked for titles/paths/values.
Reviewed before sharing: no PHI, credentials, real filenames, raw logs or dumps.
```

Activity Log's typed entries can help describe operation/failure/order without source
values; copying a log does not authorize all other tool details/screenshots. Let the
owner review a synthetic reproduction before requesting additional material. Keep
feedback separate from clinical data and record explicit consent for any additional
diagnostics. Do not request blanket “send the whole workspace/temp folder.”

## 13. Verification commands and observed results

Commands ran from the repository root with DOTNET_CLI_TELEMETRY_OPTOUT,
AVALONIA_TELEMETRY_OPTOUT and TESTINGPLATFORM_TELEMETRY_OPTOUT set to `1`.
Python commands used the supplied local Python interpreter with `-B`; `python` below
denotes that executable without recording an author-specific installation path.

| Command/check | Observed result |
| --- | --- |
| `git status --porcelain=v1`, `git rev-parse HEAD`, `git log -5 --oneline` | Initial clean `main` at the full SHA in section 3; current Stage 20C committed. |
| `dotnet --info` | SDK 10.0.401, host 10.0.12, Windows 10.0.26200, win-x64/x64. |
| `dotnet restore HL7Workbench.sln --locked-mode` | Exit 0; solution restore passes; no dependency/lock edits. |
| `dotnet build HL7Workbench.sln --configuration Release --no-restore` | Exit 0; **0 warnings, 0 errors**; 22.35 seconds. |
| `dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore` | Exit 0; **2,926 passed, 0 failed, 14 expected skips**, 2,940 total; **4m 31s 256ms**. All five projects pass; no opt-ins enabled. |
| `python -B -m unittest build/test_documentation_catalog.py` | Exit 0; **12 passed**, zero failures; 0.654 seconds in this run. |
| `python -B build/WriteDocumentationCatalog.py --check` | Exit 0; checked-in bytes match curated Markdown. Check only; no generation. |
| `python -B build/WriteScriptApiReference.py --check` | Exit 0; generated Markdown matches authoritative embedded JSON. |
| `dotnet package list --project HL7Workbench.sln --no-restore --vulnerable --include-transitive --format json` | Exit 0; all 13 projects enumerated using the NuGet v3 source; **no vulnerable package entries reported**. Not a native-binary audit or future guarantee. |
| Advisory command syntax diagnostic | Initial positional `dotnet package list HL7Workbench.sln ...` exited 1 (“Unrecognized command or argument”). This SDK requires `--project`; corrected command above succeeded. No application/test failure was repaired. |
| Protected-file SHA-256 | Python hashlib comparison of every file against existing `artifacts/stage17/protected-before.json`, plus actual tree set equality: **2,384 expected/present, 0 changed, 0 missing, 0 added**. |
| All-Markdown local links/headings and report whitespace | **155 Markdown files, 1,132 local links, 44 heading-anchor references; 0 errors**. New report final newline/trailing-whitespace checks pass. No external-link availability guarantee. |
| `git diff --check` / final status | Exit 0; only this new report is untracked, with no tracked modifications or index changes. |

The 14 skips are five QueryResourceStressTests cases and nine SqlPerformanceTests
cases selected explicitly by xUnit, not newly skipped regressions. Core ran in 7s 062ms,
Desktop 2m 38s 947ms, Querying 16s 267ms, Scripting 28s 230ms and Workspace 4m 30s 704ms.
These are measurements of this audit run, not copied historical totals.

The read-only inline Python check used for the protected inventory is reproducible
from PowerShell as follows (it prints counts only and writes nothing):

```powershell
@'
from pathlib import Path
import hashlib, json
root = Path.cwd()
records = json.loads((root / 'artifacts/stage17/protected-before.json').read_text(encoding='utf-8-sig'))
expected = {Path(row['Path']).resolve(): row['Hash'].lower() for row in records}
actual = {path.resolve() for path in (root / 'artifacts/private-preview').rglob('*') if path.is_file()}
changed = []
for path, digest in expected.items():
    if path in actual:
        with path.open('rb') as stream:
            if hashlib.file_digest(stream, 'sha256').hexdigest() != digest:
                changed.append(path)
result = dict(expected=len(expected), present=len(actual), changed=len(changed),
              missing=len(set(expected) - actual), added=len(actual - set(expected)))
print(json.dumps(result))
assert result == dict(expected=2384, present=2384, changed=0, missing=0, added=0)
'@ | python -B -
```

The protected audit reads original bytes with SHA-256 and compares the full current
set of files, not merely filenames or a second freshly invented baseline. Local-link
validation enumerates Git-tracked/untracked non-ignored Markdown, removes code fences,
checks relative targets and heading slugs using the existing compiler's heading logic.
No permanent checker, fixture, configuration or generated catalog was modified.

Not executed: publish/portable restore, ZIP assembly, packaging tests that construct
archives, clean-machine/native GUI acceptance, explicit resource stress, signing,
external transport, packet capture or real-data tests. Ordinary tests/builds can
produce their normal ignored outputs/temp fixtures; no release artifact, audit scratch
export or separate report/log directory was created. Existing useful diagnostics and
protected binaries remain preserved.

## 14. CI and per-preview release checklist

Current CI is necessary, not sufficient. Before **each separately approved preview**:

1. Record clean committed source SHA; verify current Actions result for that exact SHA.
2. Run locked normal restore, zero-warning/error Release build and the full regular
   suite; record exact pass/fail/skip identities. Do not erase a failing regression.
3. Check offline compiler, strict catalog bytes, Script API reference, all local
   documentation links/anchors and whitespace.
4. Verify all 2,384 protected files against the original SHA-256 inventory before/after.
5. Check serviced runtime/package advisories; review changes rather than auto-updating.
6. Once R01/R02 are remediated and packaging is authorized, run locked win-x64
   self-contained restores/publishes for all three executable graphs, then their
   packaging/compiler/path-privacy/inventory tests. Ordinary build outputs cannot
   substitute for published worker trees.
7. Verify actual native libraries, complete notices/covered source, absence of test
   outputs/settings/PHI/credentials/private build paths, and all three apphosts.
8. Stamp/record source SHA, unique preview ID, SDK/runtime/tool versions, exact input
   hashes and artifact SHA-256. Repeat assembly from identical staged bytes to test
   deterministic archive creation; do not conflate that with arbitrary cross-SDK rebuilds.
9. Execute section 17 on the exact artifact, including worker startup without installed
   .NET, current bulk publication and offline Help. Record OS/DPI/policy limitations.
10. Review recipient docs/feedback rules, signature/unsigned status and separately
    authenticated checksum; obtain owner approval of recipients and transfer.

The protected baseline is local evidence, not currently an Actions step. A future
protected hash gate must use an approved available original baseline rather than
upload protected files casually. Authenticated CI access and artifact signing/checksum
delivery are separate from current read-only audit access. No workflow was modified.

## 15. Proposed Stage 21B scope — approval required

Keep remediation focused on a two-recipient synthetic preview:

- R01: current three-process portable deployment, RID locks, worker completeness and
  no-installed-runtime tests; preserve historical package/binary reuse behavior.
- R02: current package/resource inventory and notice/source-delivery approval, including
  SQLitePCLRaw/SQLite and adapted validation metadata; actual native scopes and owner rights.
- R03: approve a bounded exact-artifact clean-machine/native plan and establish current
  CI/native acceptance evidence. Publishing/testing a candidate needs separate approval.
- R05/R09/R10: choose unique preview identification and a short ZIP-only first task,
  limitations/privacy guidance and manual feedback template using existing commands.
- Validate R06–R08 with controlled synthetic ACL/shutdown/two-instance cases. Fix only
  demonstrated blockers under approved scope; otherwise retain an explicit workaround.

Acceptance should require new actual-artifact provenance, all current tests/checks,
unchanged protected bytes and an owner-reviewed synthetic-only recipient checklist.
This is a proposed work list; none was implemented or packaged in Stage 21A.

## 16. Proposed Stage 21C scope and explicit deferred work

Use early tester feedback to choose small reliability/documentation corrections.
If evidence warrants it, add a bounded shutdown completion barrier, clarified
multi-instance policy, convenient build-ID copying or a **local-only** issue-template
action. A simple text template is sufficient initially; no feedback service or
automatic attachment/telemetry system is recommended.

Defer command palettes, menu/ribbon/navigation redesign, shortcut changes, installers,
auto-updaters, public release/branding campaigns, new plugin systems, broad clinical
conformance, de-identification guarantees, production TCP/TLS promises, new mappings,
single-file/trimming/AOT/ARM64 projects and indiscriminate performance refactors.
Signing/expanded soak work can follow the recipient-policy and measured-needs decision.
Do not treat this audit as authorization for Stage 21B, 21C or Stage 22 implementation.

## 17. Native clean-machine acceptance checklist — not performed

Use a genuinely separate supported Windows 11 x64 VM/PC and standard account, without
an SDK or installed .NET; use a disposable synthetic-only profile. The current audit
host has SDKs/runtimes and cannot satisfy this prerequisite by changing PATH alone.
Run this checklist only for an exact later approved artifact:

1. Record OS edition/build, architecture, account restrictions, GPU/scaling, artifact
   build/source ID and a trusted independently supplied SHA-256. Note unsigned status.
2. Receive through the intended channel, scan normally and observe actual MOTW,
   Defender/SmartScreen or enterprise application-control behavior. Stop if policy
   blocks it; never disable protection as an acceptance step.
3. Extract **all** files into spaces/Unicode paths. Repeat with a read-only extraction
   directory, writable per-user storage and an unrelated process working directory.
   Confirm no settings/temp content is written beside the executable.
4. Launch offline under a fresh profile. Confirm correct version/build, no automatic
   listener/connect/upload/update, useful failure behavior, and no dependency on
   developer services. Verify effective per-user/temp ACLs using synthetic content.
5. Generate a small synthetic batch or open an explicitly supplied reviewed fixture.
   Inspect segments/repetitions/components, long/malformed values and message navigation;
   copy raw/path only to a synthetic scratch destination.
6. Exercise search/filter/unique values, complete extraction CSV and SQL all-page
   results/source navigation. Prove QueryHost runs via its adjacent self-contained
   apphost and selected native SQLite; repeat a trusted synthetic ScriptHost run.
7. Edit synthetic text, Undo/Redo, Save As and reopen; compare original bytes. Check
   cancel, inaccessible destination and collision failures retain source/draft.
8. Reproduce the 30-message Avery→Morgan bulk case: preview leaves original Inspector
   and raw text Avery; Apply opens exactly one distinct active Bulk Result tab; both
   views show Morgan; all 30 messages/unaffected fields remain; original tab remains
   clean and both original views show Avery. Repeat current-message 17, empty replacement,
   input invalidation and rapid repeated Apply, docked/floating Inspector.
9. Apply/remove/import a scoped schema, edit/revalidate and switch documents. Confirm
   per-message ownership and Incomplete coverage, not automatic file-open validation.
10. Check standalone/scenario automatic seeds and explicit replay/Copy seed, plus
    local synthetic FHIR import/partial generation/report association. No remote fetch.
11. Open searchable Help without internet/repository. Follow the recipient's first task,
    inspect API links and unavailable repository references; check keyboard/focus.
12. On authorized loopback only, test send/listen/ACK, cancellation and stop/restart;
    verify no automatic reconnect/resend and no lingering listener after exit.
13. Repeat narrow/maximized, Light/Dark, 100/150/200% scaling, floating/redocking,
    monitor removal/window recovery, clipboard/IME and accessible labels.
14. Cancel/close during each long operation, then shut down. Observe workers/ports,
    document/model drains and temporary-file cleanup. Check Save/Discard/Cancel across
    multiple dirty documents/scripts/definitions; cancel a late prompt and retain work.
15. Force termination with synthetic content, restart and inspect expected unsaved-work
    loss, residual plaintext/old cleanup and placement-only restoration. Verify no clinical
    auto-recovery or unsafe dump-sharing instructions.
16. Test corrupt/incompatible settings, unwritable profile/temp, disk/access failures
    and two simultaneous instances in controlled profiles. Distinguish pre-UI launch
    blockers from recoverable feature failures; never delete existing user settings.
17. Complete the manual feedback template without sending HL7/JSON, credentials, private
    paths or unreviewed screenshots/dumps. Reconcile final extraction/artifact hashes
    and record all deviations before owner distribution approval.

## 18. Open questions, assumptions and audit completion boundary

- **Unknown:** latest Actions conclusion for cd71c48. Obtain authenticated evidence;
  this report does not assert CI failure or success.
- **Unverified:** native acceptance completed outside the current reports, current-source
  clean-machine startup, exact native/VC prerequisites, extraction/path/ACL/Defender
  behavior, signature status of an artifact not built, and global shutdown residue.
- Decide recipient Windows editions/hardware and enterprise policy. Historical broad
  Windows 10/11 wording is insufficient to claim current vendor support on every edition.
- Decide unique preview version/build ID, exact recipients/contact/transfer channel,
  synthetic-only acknowledgment, allowed sample delivery and native acceptance ownership.
- Resolve covered validation source/notices and actual native package scope before
  redistribution. Same-organization and outside-organization recipients may require
  different review; do not assume two trusted developers are one legal entity.
- Decide whether one-instance use is a sufficient initial workaround. Real patient
  data remains out of scope until section 8 is explicitly satisfied.

Only `docs/stage21a-private-preview-readiness-audit.md` is created. Existing production,
tests, locks, workflows, other documentation, generated catalogs and protected artifacts
are preserved. No commit, push, package, publish or distribution. Stop after this audit;
await approval for remediation or Stage 21B.
