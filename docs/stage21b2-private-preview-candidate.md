# Stage 21B.2 — Local private-preview candidate and clean-machine acceptance

Verification date: **2026-11-01** (owner's Windows local date). This report records
one current-source, local Windows x64 candidate. **Awaiting owner acceptance;
non-distributable; synthetic-only; unsigned.** No native clean-machine pass or
redistribution approval is claimed.

## 1. Executive summary

All applicable local preconditions passed. One owner-controlled current-source ZIP
was created and validated, including exact extraction and worker smoke checks.
Two assemblies from the same immutable staged inputs produced identical ZIP bytes;
the disposable comparison ZIP was removed. No application behavior, dependency,
source/build configuration, test expectation or existing documentation was changed.

The regular solution run passed **2,941**, failed **0**, and skipped **14** documented
explicit opt-ins. Release build: **0 warnings, 0 errors**. All **2,384** protected
files retain their baseline SHA-256 values. Current CI is **Unknown**. The **68**
native checks below remain **Not tested**, pending a genuinely separate Windows VM
or machine. Sharing remains blocked by native, CI, owner/legal and transfer gates.

## 2. Source and Git baseline

| Item | Verified value |
| --- | --- |
| Branch | main |
| HEAD | `933bd6675b6baa66be1a2aa3f48acfad988e783e` — Stage 21B.1 |
| Parent | `cd71c482ec62351e0bf9b650a2f6bbb6a5d68c46` |
| Tree | `7f69a57cf70d0cd74f08654739fd97f4c6ff0640` |
| Initial tracked changes / index / untracked files | None; clean working tree and empty index |
| Source state during publishing and assembly | Clean; committed; schema-2 provenance records dirty=false |
| Public version / About Build | 0.1.0-beta / 933bd667 |
| Actual ProductVersion, all three apphosts | `0.1.0-beta+933bd6675b6baa66be1a2aa3f48acfad988e783e` |
| FileVersion / AssemblyVersion | 0.1.0.0 / unchanged 1.0.0.0 |
| Current change | This new report only; written after candidate assembly and validation |

The earlier tested-input inventory was compared against the current checkout:
**546/546 runtime/build/test inputs match byte-for-byte**, without EOL-only
exceptions. Current staging separately records **349** source/build/recipient
inputs and their individual SHA-256 hashes. Candidate identities refer to the
committed source, not to this subsequent uncommitted report. No commit or push was
performed or inferred from the existence of the local commit.

Evidence is retained under
`artifacts/stage21b2/933bd667-owner-candidate-01/`, outside protected historical
outputs. The baseline JSON records the exact empty initial status and change lists.
[Repository guidance](../AGENTS.md), [current context](PROJECT_CONTEXT.md),
[Stage 21A audit](stage21a-private-preview-readiness-audit.md),
[Stage 21B.1 report](stage21b1-portable-distribution-preparation.md),
[portable procedure](portable-release.md), [release ADR](decisions/0019-portable-beta-readiness.md),
[build instructions](developer/build-and-test.md), [storage](reference/storage-and-formats.md),
[security](developer/security-and-data-handling.md), [onboarding](user/getting-started.md),
[privacy](user/privacy-and-storage.md) and [troubleshooting](user/troubleshooting.md)
provided the applicable boundaries. Earlier milestone prohibitions are historical;
the user's Stage 21B.2 request expressly authorizes this single local candidate.

## 3. Stage 21B.1 reconciliation

The actual HEAD commit changes **37 files** against its parent. The Stage 21B.1
completion report lists **36 implementation files**. Their exact set equals the
commit's set after excluding **docs/stage21a-private-preview-readiness-audit.md**,
the previously untracked audit report preserved and committed with Stage 21B.1.
There are **no unexpected files or missing implementation files**. Git identifies
no 38th changed file; the interface's approximate count cannot establish one.
The exact 37-file list and 36-file comparison are in baseline.json.

The previous report's dirty/uncommitted descriptions correctly describe its
historical execution on cd71c482. They are not the current state: Stage 21B.1 is
now committed at 933bd667. Its implemented three-process publishing, strict notice
and covered-source staging, build identification, generated offline Help,
recipient guidance and release/real-compiler regressions are present and passed
fresh checks in this milestone. Historical reports were not rewritten.

## 4. GitHub Actions status

| Commit | Workflow | Run ID | Conclusion / checks |
| --- | --- | --- | --- |
| `933bd6675b6baa66be1a2aa3f48acfad988e783e` | Build and test | Unknown | **Unknown**; authenticated evidence unavailable |

The [workflow](../.github/workflows/build.yml) runs Windows locked restore, Release,
regular tests, documentation/catalog/API and release-preparation/compiler checks.
No gh executable or authenticated Actions-run connector was available; the
commit-filtered API could not be accessed through the available web tool. No run
identifier or success is invented. Local passes do not establish Actions success.
**Exact-revision CI remains a distribution gate.** No push was used to trigger it.

## 5. Owner/legal review status

Technical notice completeness passed; this is **not legal clearance**. The earlier
[distribution review](beta-distribution-review.md) and
[native applicability investigation](native-license-applicability-review.md) concern
their identified historical artifacts. Their conclusions do not independently
approve this newer QueryHost/MPL-bearing package. The current
[redistribution checklist](redistribution-review-checklist.md) remains unchecked.
No identified obligation in that reviewed material prohibits this expressly
requested local owner-controlled verification, subject to applicable rights.

Decisions still requiring the owner/qualified reviewer:

1. Approve the exact 40-record runtime/package/native inventory and complete
   original/supplementary notices, including SQLitePCLRaw Apache terms, SQLite's
   declaration, .NET, Roslyn, ANGLE, Skia, HarfBuzz and Wuffs component scope.
2. Confirm MPL-2.0 applicability and recipient/organization context for the exact
   adapted validation metadata. Deliver modified source, source/change NOTICE and
   MPL text; do not substitute an upstream link or assume the entire independent
   application is MPL-licensed.
3. Confirm application/artwork rights, employer/security requirements, permitted
   recipients and transfer method. Two friends are not presumed internal users of
   one organization. Prior artwork confirmation is not new employer approval.
4. Reconcile native/component notices with the selected binaries; primary SPDX
   declarations alone do not establish every embedded component obligation.
5. Review this exact source/archive/hash, authenticated CI and clean-machine
   acceptance, supported Windows scope and **unsigned** status.
6. Approve synthetic-only instructions, manual feedback contact and an authenticated
   checksum/transfer channel, then explicitly authorize the exact recipient sharing.

There is no clinical, production, anonymity, HIPAA or security-certification claim.

## 6. Exact publishing configuration and tool identity

- net10.0, win-x64, Release, self-contained **each** of Desktop/ScriptHost/QueryHost.
- RuntimeFrameworkVersion **10.0.12**, SDK **10.0.401**, existing locked versions.
- Multi-file; PublishTrimmed=false, PublishAot=false, PublishSingleFile=false,
  PublishReadyToRun=false, DebugType=None, DebugSymbols=false,
  ContinuousIntegrationBuild=true; publish uses -warnaserror.
- Locked RID restores use packages.win-x64.lock.json, SelfContained=true,
  PublishScriptHostSeparately=true and PublishQueryHostSeparately=true, with a
  dedicated --artifacts-path under a spaces/Unicode owned work directory.
- No dependency upgrade, binary rewriting, runtime resolver change or legacy reuse.

The committed [BuildPortableCandidate.ps1](../build/BuildPortableCandidate.ps1)
intentionally deletes its verification tree and creates no ZIP. Its exact existing
restore/publish arguments were reused in the retained ignored publish-local.ps1
owner orchestration, permitting the separately authorized archive stage. The
committed [current assembler](../build/portable_release.py) staged schema 2 with
--scope current --verification-only and all three published inputs. Its existing
deterministic_zip and verify_archive APIs assembled/checked the **current** format;
legacy mode was never used for current bytes. No guard was removed from the CLI.

The sole staged metadata refinement was the generated NON-DISTRIBUTABLE-VERIFICATION.txt
marker: Stage 21B.2, awaiting owner acceptance, synthetic-only, unsigned, no sharing.
Its file checksum was recomputed **before** freezing/assembly. Source recipient
texts and compiled/generated resources were not changed. The outer
CANDIDATE-READ-ME-FIRST.txt explains the earlier Stage 21B.1 wording and actual status.

Build-tool source identity is commit 933bd667. Per-file hashes of the assembler,
publish script, worker verifiers/targets, build properties and global.json are in
CANDIDATE-IDENTITY.json, alongside hashes of the ignored owner orchestration.
Assembler SHA-256: `f0cec9fa8e99232ba41ce0ca24dbc49cf86bccef51b62a67f728722a375d4206`.
No untraceable or fabricated commit metadata was used.

## 7. Candidate archive identity and local path

| Property | Value |
| --- | --- |
| Candidate ID | `21B2-933bd667-01-fa862e0eb69e` |
| Application Build ID | 933bd667 (About); exact source SHA in section 2 |
| Status | **AWAITING OWNER ACCEPTANCE / NON-DISTRIBUTABLE / UNSIGNED** |
| Archive bytes / entries | **133,101,034 / 795** |
| Retained candidate ZIPs | **One**; temporary deterministic comparison removed |
| RID / configuration / SDK / runtime | win-x64 / Release / 10.0.401 / 10.0.12 |
| Source input inventory digest | `fa862e0eb69e5229718fd1dcc54938b8eeb92dec3b71429c0af90f8c5372f5e8` |

Exact local archive:

```text
C:\Dev\HL7Workbench\artifacts\stage21b2\933bd667-owner-candidate-01\MessageWorkbench-0.1.0-beta-933bd667-win-x64-AWAITING-OWNER.zip
```

The adjacent CANDIDATE-IDENTITY.json, .zip.sha256, CANDIDATE-READ-ME-FIRST.txt and
NATIVE-ACCEPTANCE.md bind the owner review to this exact artifact. The ZIP itself
retains established **schema-2 BUILD-PROVENANCE.json**, current-three-process,
verificationOnly=true, dirty=false, exact source/configuration/RID/SDK/runtime and
input/dependency/covered-source hashes. Its artifactChecksums.archive remains null
as required by that format. The **external archive identity record** binds its
SHA-256 to the provenance and file manifest without a circular self-hash or an
unapproved schema change.

## 8. Archive SHA-256 and checksum scope

```text
ad69f7a74c1a232436325f42ec028574d7648fb985be315e888f1aff5423d05b
```

BUILD-PROVENANCE.json SHA-256:
`18c020c23e31780f801a3bad64c9e8c24da80ca937ad8f8f5aad591186da379c`.
SHA256SUMS.txt SHA-256:
`4de0700fb41b62cf9089dc3aa8c51b590dfcae6d0394932bab69bcb3f5e441ac`.
The internal manifest covers **794 files**, excluding only itself; the external
ZIP checksum covers the complete archive including that manifest. Each archive
entry was streamed and compared to its exact staged hash, followed by extraction
and a complete file-set/hash comparison. Post-smoke verification repeated these
comparisons; the archive hash remained unchanged. These are integrity checks,
not a publisher signature or malware/security audit.

## 9. Dependency and notice inventory

The exact selected dependency graphs contain **40 unique package/runtime records**;
process memberships are Desktop **33**, ScriptHost **5**, QueryHost **6**, with
shared packages counted once globally. **74 third-party notice files** are present.
No package-platform superset or historical 35-entry inventory was substituted.

dependency-licenses.json SHA-256:
`1fadcc8a21d9ae1e1f383b7c48643b303cc2ed7e000b896de12e73df6121cd2e`.

All required package and pinned upstream notice bytes/hashes, scopes and declarations
passed the current validator. QueryHost requires SQLite **3.53.4** plus the four
SQLitePCLRaw **3.0.5** entries; Apache license/NOTICE and native SQLite's exact file
license are retained. Native SQLite is present in QueryHost and executed in smoke.
The selected manifests, all notices and covered source are in the actual archive.
The advisory scan enumerated all **13 solution projects**, reporting no vulnerable
package entries; it does not certify native components or future advisory status.

## 10. Covered-source delivery verification

Pinned adapted nHapi source revision:
`9027797b1fd1faebcffd743ac11324499145f29d`; license MPL-2.0.

| Delivered file | SHA-256 |
| --- | --- |
| covered-source/v251-model.json | `99af17a10e7f5ff227121c34cd2bccb1cd875c8156e588f84efc4b6fcdacdc4e` |
| covered-source/NOTICE.md | `ce23986d3d4e04aa2ace65f478db6a7a4304f5b4a97140f2af7e874947c8281e` |
| covered-source/MPL-2.0.txt | `2684de17300e0a434686f1ec7f8af6045207a4b457a3fe04b2b9ce655e7c5d50` |

All three delivered files match repository/provenance inputs byte-for-byte. The
JSON's pinned origin is verified, and its exact uncompressed resource bytes occur
in **both** delivered Core assemblies (Desktop and ScriptHost). Actual ZIP and
extraction hashes prove delivery, not merely a source-side claim. The source/change
NOTICE and full MPL text are present; no remote download is required to obtain
this adapted resource. Applicability/recipient decisions remain human review.

## 11. Three-process completeness and privacy exclusions

| Process | Actual entry point | Checks |
| --- | --- | --- |
| Desktop | MessageWorkbench.exe → HL7Workbench.Desktop.dll | x64 PE/binding, deps-selected assets, self-contained runtimeconfig/coreclr/hostfxr/hostpolicy, compiled Help and API payloads |
| ScriptHost | ScriptHost/HL7Workbench.ScriptHost.exe | x64 PE/binding, complete independent runtime/deps, Roslyn scripting isolation, protocol/execution/exit |
| QueryHost | QueryHost/HL7Workbench.QueryHost.exe | x64 PE/binding, complete independent runtime/deps, native e_sqlite3.dll, protocol/SQL/exit |

All runtimeconfigs include only Microsoft.NETCore.App **10.0.12**, without an
installed-framework reference. Existing adjacent worker paths remain intact.
Authenticode reports **NotSigned** for all three apphosts; ProductVersion and
FileVersion agree with section 2. Desktop compiler-only Roslyn does not acquire
the worker scripting runtime.

Strict allowlisting and selected deps exclude arbitrary fixtures, tests, settings,
endpoint profiles, snapshots, debug logs, PDBs, repository sources, credentials and
dumps. Optional createdump and symbols are deliberately excluded by existing policy;
required native/runtime/resource assets are not omitted. The covered validation
source is the explicit required source exception. Known checkout/home path variants
were scanned in UTF-8/UTF-16 byte spellings; no private build paths were detected.
No real data or production endpoints were introduced. This is not a forensic secret
or malware scan of every dependency.

## 12. Exact archive extraction and automated smoke results

**Archive:** CRC, strict canonical relative paths, Windows reserved/trailing-dot/space
safety, no traversal/drive/backslash/NUL, no symlink/encrypted entries, no duplicate,
Unicode-normalized case collision or file/directory prefix collision. Exact entry
count/set/size/hash, mandatory notices/provenance/runtime/native/covered source,
no unknown files and SHA manifest all passed. The isolated extraction path included
spaces and Unicode. All **795** extraction hashes matched frozen staging before
and after smoke; ZIP SHA-256 remained identical.

**Actual extracted candidate bytes:**

| Check | Result / boundary |
| --- | --- |
| Desktop | Apphost/binding/launch dependency and embedded resource checks pass. Native GUI launch is **Not tested** here. |
| ScriptHost startup and execution | Committed verifier passes unified Main current/whole lifecycles, shared state, synthetic transformation, CSV/completion/deferred output and clean zero exit. |
| QueryHost startup and SQLite | Protocol 2 initialization reports native SQLite 3.53.4; typed three-row synthetic import, SQL COUNT=3, complete page and clean zero exit pass. |
| Cooperative script cancellation | Wait for Main's explicit synthetic output barrier, send Cancel; failure frame and bounded exit code 1, with no successful partial result. |
| Uncooperative script termination | Wait for the execution barrier, send Cancel, bounded 200 ms grace then process-tree kill/reap, matching the parent's stop mechanism. |
| Pending source-read cancellation | Wait for actual QueryHost SourceRead RPC, withhold reply, send Cancel; failure frame and bounded exit code 1 rather than partial page. |
| Missing workers | Load actual candidate parent Scripting/Querying assemblies; explicit empty owned fixture paths both produce WorkerUnavailable. Candidate ZIP/extraction is never damaged. This is API failure evidence, not native error-dialog acceptance. |
| Read-only/unrelated directory | Actual extraction denies account Write/Delete via inherited ACL; a write probe is rejected. Worker apphosts run from System32 with invalid DOTNET_ROOT and no SDK on PATH. ACL restored after checks. |
| Offline Help / Script API / metadata | Exact catalog/API payload bytes embedded in delivered assemblies; matching version/build; all apphosts NotSigned. Help rendering remains native acceptance. |

All cancellation probes use execution/RPC barriers and finite deadlines, with no
arbitrary sleep-based success assertion. Parent/job/full UI cancellation and bulk
publication regressions also passed in the **ordinary solution build**, separately
from these candidate-byte probes. Unit fixture archives and real-compiler temporary
checkouts are **not** this candidate. Hiding installed runtime lookup on this
SDK-equipped host is **not a clean-machine Desktop acceptance**.

Three evidence-harness corrections are recorded rather than hidden: an early
prearchive counter used len on a generator; an early prearchive relative log path
was affected by the verifier's System32 working directory; and a post-smoke
read-only API call needed its absolute root resolved. These were caller/evidence
errors, corrected and rerun. No restore, permanent test, candidate integrity or
worker assertion failed; no candidate bytes were repaired or replaced.

Recipient review: packaged README/USER-GUIDE/PRIVATE-PREVIEW/FEEDBACK/checklist plus
the new generated marker and adjacent READ-ME-FIRST cover complete extraction,
Windows x64, both workers, synthetic-only use, trusted-script account privileges,
plaintext storage/clipboard, unencrypted unauthenticated TCP/MLLP, incomplete
validation, unsaved-work limitations, local Help, manual feedback exclusions,
unsigned status and no production/clinical approval. No telemetry, upload,
account, updater or automatic diagnostics were added.

## 13. Deterministic archive assembly

The committed current assembler created two ZIPs from the **same immutable 795-file
staging tree**, with sorted entries, fixed 1980 timestamps/attributes and fixed
Deflate settings. Both hashes equal section 8. Staged hashes were checked again
before accepting the result. Toolchain: Python **3.12.14**, zlib **1.3.2**.
The comparison ZIP was deleted after proof, leaving **one** retained candidate.
This establishes deterministic archive assembly in this toolchain, **not** identical
compilation across different machines, SDKs or future servicing versions.

## 14. Fresh automated regression results

| Check | Fresh result |
| --- | --- |
| Normal locked solution restore | Pass |
| Locked win-x64 restores | Desktop, ScriptHost, QueryHost: all pass |
| Release solution build | Pass, **0 warnings / 0 errors** |
| Three self-contained publishes | All pass under -warnaserror |
| Full regular Microsoft.Testing.Platform suite | **2,941 passed / 0 failed / 14 skipped**, 2,955 total; **5m 21s 514ms** |
| Portable preparation/integrity unit tests | **38 passed / 0 failed / 0 skipped** (14 legacy, 24 current) |
| Real compiler/path-privacy/warm-reuse tests | **2 passed / 0 failed / 0 skipped** |
| Documentation compiler tests | **12 passed / 0 failed / 0 skipped** |
| Catalog / generated Script API consistency | Pass |
| Markdown local links / heading anchors | Pass; **160 Markdown files, 1,154 local links including heading anchors**, zero errors |
| Dependency advisories | Pass; 13 projects, no vulnerable entries reported |
| Actual historical trees | Four pass; 582 / 582 / 586 / 586 files |
| Exact current ZIP/extraction/manifest/smoke | Pass; 795 files, archive unchanged |
| Protected SHA-256 inventory | 2,384 verified, zero changed/missing/added |
| Whitespace / source-input immutability | Pass; final status recorded below |

The 14 expected explicit skips are **5 QueryResourceStressTests** and
**9 Workspace SqlPerformanceTests**. No opt-in stress branch or native GUI check
is claimed. No test assertions/expected results were changed and no checks were
weakened. Full test names/skip reasons are retained in full-solution.log.

Core commands, using telemetry opt-outs and the repository-selected SDK:

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln --configuration Release --no-restore
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
python -B -m unittest discover -s build -p 'test_portable*.py'
python -B -m unittest discover -s build -p 'test_release_compilation.py'
python -B -m unittest build/test_documentation_catalog.py
python -B build/WriteDocumentationCatalog.py --check
python -B build/WriteScriptApiReference.py --check
dotnet package list --project HL7Workbench.sln --no-restore --vulnerable --include-transitive --format json
git diff --check
```

RID restore/publish arguments are in section 6 and publish-local.ps1. Historical
validation used explicit legacy scope, read-only. The retained verify_preconditions.py
evidence helper performs protected hashes, historical validators and Markdown
path/heading checks; it is owner evidence, not a production extension. Normal
permissions were approved for build/test workers and temp fixtures; no automatic
approval rejection occurred in this milestone.

## 15. Native Windows acceptance worksheet — awaiting owner

This worksheet is awaiting the owner. Every native check starts **Not tested**.
The automated checks in the companion report are separate evidence.

### Candidate C1

- Candidate ID: `21B2-933bd667-01-fa862e0eb69e`.
- Source: `933bd6675b6baa66be1a2aa3f48acfad988e783e`; About Build: `933bd667`.
- ZIP: `MessageWorkbench-0.1.0-beta-933bd667-win-x64-AWAITING-OWNER.zip` (133,101,034 bytes, 795 files).
- SHA-256: `ad69f7a74c1a232436325f42ec028574d7648fb985be315e888f1aff5423d05b`.
- Status: **NON-DISTRIBUTABLE / AWAITING OWNER ACCEPTANCE / UNSIGNED**.
- Local source directory: `C:\Dev\HL7Workbench\artifacts\stage21b2\933bd667-owner-candidate-01`.

### Environment E1 — owner completes before testing

Record tester/date, VM/machine label, Windows 11 edition/build/patch, x64 CPU,
standard user account, fresh profile, screen/monitor/DPI/theme, extraction path,
permissions, working directory and installed-software/environment inventory.
Require a genuinely separate Windows 11 x64 environment with no modern .NET SDK
or separately installed modern .NET runtime, Visual Studio, InterSystems, Docker,
repository checkout, developer variables or required developer services. Built-in
Windows .NET Framework is distinct from the modern .NET 10 dependency under test.
Do not equate hiding PATH on the development machine with this acceptance.
Record any deviation as Blocked; never disable Defender/SmartScreen to force a pass.

Environment details: **owner to complete**. E1/C1 in every row refers to these
recorded environment/candidate keys. Fill Actual, status, notes and an optional
synthetic-only screenshot reference for each row. Status choices: Pass, Fail,
Blocked, Not tested. A failed native check blocks sharing pending review.

### Repeatable Avery baseline

1. Generate 30 standalone ADT^A01 messages, Realistic Synthetic, explicit seed 42,
   in a new document. Record all other settings, style/version and base time.
2. On existing PID[1]-5[1].2, use Assign Value with active-file scope to assign
   Avery to all 30 messages; review and confirm the independent output.
3. In that output, similarly assign PID[1]-5[1].1 to Avery as an unaffected
   sibling-component sentinel. Save the completed output as Synthetic-Avery-30.hl7
   outside the application folder; reopen it as the clean original and close
   earlier preparation tabs. Record its SHA-256 before testing; confirm 30
   messages and both views agree. These are explicit owner-generated synthetic
   files, not additional fixtures in the candidate.
4. Run the D rows against this clean original. Save result copies separately if
   needed for byte comparison. Review the complete 30-message output: family-name
   Avery must remain while only selected given-name Avery becomes Morgan. For
   Current message use Go 17. For empty replacement start again from the original.

### A. Installation and launch

| ID | Environment | Candidate | Expected behavior | Actual | Status | Notes | Screenshot |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A01 | E1 | C1 | Independently verify the ZIP SHA-256 against the owner-recorded value; reject any mismatch. | — | Not tested | — | — |
| A02 | E1 | C1 | Record patched Windows 11 x64 version, standard account, and absence of SDK/runtime/VS/InterSystems/Docker/repository/developer services. | — | Not tested | — | — |
| A03 | E1 | C1 | Record Defender/SmartScreen and Mark-of-the-Web behavior for this unsigned ZIP; retain protections and record any block. | — | Not tested | — | — |
| A04 | E1 | C1 | Extract all 795 files; preserve both workers, notices, covered source and manifests. Do not run from the ZIP. | — | Not tested | — | — |
| A05 | E1 | C1 | Launch from a path containing spaces. | — | Not tested | — | — |
| A06 | E1 | C1 | Launch from a Unicode path, for example a folder named Workbench 雪. | — | Not tested | — | — |
| A07 | E1 | C1 | Launch from a genuinely write-denied application directory; confirm settings/temp data are stored elsewhere. | — | Not tested | — | — |
| A08 | E1 | C1 | Launch with an unrelated working directory, for example via a shortcut with Start in set to the user home. | — | Not tested | — | — |
| A09 | E1 | C1 | Launch in a fresh user profile with no developer environment variables; no installed modern .NET may be borrowed. | — | Not tested | — | — |
| A10 | E1 | C1 | About shows Version 0.1.0-beta and Build 933bd667; retain the full C1 archive identity separately. | — | Not tested | — | — |
| A11 | E1 | C1 | Observe idle startup without automatic application network connections/listeners. Distinguish OS/security traffic; do not enable transport yet. | — | Not tested | — | — |

### B. Core viewing and editing

| ID | Environment | Candidate | Expected behavior | Actual | Status | Notes | Screenshot |
| --- | --- | --- | --- | --- | --- | --- | --- |
| B01 | E1 | C1 | Generate a synthetic 30-message ADT batch in a new document; record seed, style and report settings. | — | Not tested | — | — |
| B02 | E1 | C1 | Navigate first/previous/next/last and Go to message 17; count remains 30. | — | Not tested | — | — |
| B03 | E1 | C1 | Inspect field/repetition/component/subcomponent selections; raw view and Inspector refer to the same message/path. | — | Not tested | — | — |
| B04 | E1 | C1 | Copy synthetic values and canonical paths; clipboard text agrees with complete selected values. | — | Not tested | — | — |
| B05 | E1 | C1 | Enable Edit Mode, edit an existing value, and check raw view/Inspector agreement after parsing. | — | Not tested | — | — |
| B06 | E1 | C1 | Undo and Redo restore exact previous/reapplied values and expected working-text state. | — | Not tested | — | — |
| B07 | E1 | C1 | Save As to an explicitly selected location outside the application folder; cancel a picker without creating a file. | — | Not tested | — | — |
| B08 | E1 | C1 | Reopen saved output and verify message count/values/encoding; preserve and hash the original file before/after. | — | Not tested | — | — |
| B09 | E1 | C1 | Inspect malformed synthetic text without silent repair; retained raw source remains authoritative. | — | Not tested | — | — |

### C. Search, SQL and scripting

| ID | Environment | Candidate | Expected behavior | Actual | Status | Notes | Screenshot |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C01 | E1 | C1 | Search/filter the multi-message synthetic file; navigate results to the correct message and clear the filter. | — | Not tested | — | — |
| C02 | E1 | C1 | List unique PID-5.2 values and reconcile counts with all messages, not only the displayed page. | — | Not tested | — | — |
| C03 | E1 | C1 | Extract fields and export synthetic CSV; review scope, quoting and complete rows. | — | Not tested | — | — |
| C04 | E1 | C1 | Run SELECT COUNT(*) FROM Messages in the active-document SQL scope; expected count is 30. | — | Not tested | — | — |
| C05 | E1 | C1 | Run a message query and Go to source; correct document/message opens. Stale/closed source disables unsafe navigation. | — | Not tested | — | — |
| C06 | E1 | C1 | Run a trusted synthetic script such as void Main() { Message.SetText("PID-5.2", "ScriptSynthetic"); }; independent output opens and the source remains unchanged. | — | Not tested | — | — |
| C07 | E1 | C1 | Demonstrate ScriptHost and QueryHost startup with no installed modern .NET; retain worker/error observations. | — | Not tested | — | — |
| C08 | E1 | C1 | Cancel a long script/query and exercise a rejected SQL or script error; no partial output, orphan worker or unusable UI. | — | Not tested | — | — |

### D. Bulk Avery-to-Morgan regression

| ID | Environment | Candidate | Expected behavior | Actual | Status | Notes | Screenshot |
| --- | --- | --- | --- | --- | --- | --- | --- |
| D01 | E1 | C1 | Prepare the clean 30-message Avery baseline below; original raw view shows Avery at PID[1]-5[1].2. | — | Not tested | — | — |
| D02 | E1 | C1 | Original Inspector shows Avery at the same message/path. | — | Not tested | — | — |
| D03 | E1 | C1 | Enter Find Avery / Replace with Morgan, active-file scope; Preview proposes 30 changes without changing original raw view, Inspector or dirty state. | — | Not tested | — | — |
| D04 | E1 | C1 | Confirm Apply once; exactly one separate Bulk Result.hl7 tab is registered, visible and active. | — | Not tested | — | — |
| D05 | E1 | C1 | Result raw view shows Morgan at the affected component. | — | Not tested | — | — |
| D06 | E1 | C1 | Result Inspector shows Morgan for the same result message/path. | — | Not tested | — | — |
| D07 | E1 | C1 | Result retains all 30 messages, including unchanged content outside any selected scope. | — | Not tested | — | — |
| D08 | E1 | C1 | Only intended PID-5.2 values change; PID-5.1 sentinel, other components, delimiters, terminators and other fields remain identical. | — | Not tested | — | — |
| D09 | E1 | C1 | Original tab remains clean; its saved baseline SHA-256 is unchanged. | — | Not tested | — | — |
| D10 | E1 | C1 | Return to original: both views show Avery; return to result: both views show Morgan. | — | Not tested | — | — |
| D11 | E1 | C1 | Repeat from original at message 17 with Current message scope; only message 17 changes and all 30 remain. | — | Not tested | — | — |
| D12 | E1 | C1 | Use an intentionally empty replacement; Preview/Apply clears only the selected existing components in independent complete output. | — | Not tested | — | — |
| D13 | E1 | C1 | Double-click/repeat Apply while publishing or after success; no duplicate output. Changing an input invalidates the reviewed proposal and requires reanalysis. | — | Not tested | — | — |
| D14 | E1 | C1 | Repeat with docked and floating Inspector, switch tabs and redock; bindings remain aligned. Cancellation produces no result. | — | Not tested | — | — |

### E. Advanced features

| ID | Environment | Candidate | Expected behavior | Actual | Status | Notes | Screenshot |
| --- | --- | --- | --- | --- | --- | --- | --- |
| E01 | E1 | C1 | HL7 Tools → Schema Validation opens selection; explicitly apply/change/remove a profile on the intended message. File open alone does not apply. | — | Not tested | — | — |
| E02 | E1 | C1 | Inspect squiggles, hover/status/incomplete coverage and F8/Shift+F8; edit revalidates without source normalization or cross-document schema leakage. | — | Not tested | — | — |
| E03 | E1 | C1 | Generate standalone/scenario synthetic data with blank seed; Copy seed and replay explicit seed with identical settings. | — | Not tested | — | — |
| E04 | E1 | C1 | Import an independently prepared local synthetic FHIR R4 Bundle; review supported scope/mapping report and generate output without remote fetch. | — | Not tested | — | — |
| E05 | E1 | C1 | Open Help → Documentation offline; search, follow local topic links, Back/Forward and retain useful source-only link explanations. | — | Not tested | — | — |
| E06 | E1 | C1 | Open Script API Reference offline and navigate/search its contracts without running untrusted code. | — | Not tested | — | — |
| E07 | E1 | C1 | Explicitly configure loopback TCP/MLLP only; send/listen/correlate ACK, then Stop. Socket completion is not application acceptance. | — | Not tested | — | — |
| E08 | E1 | C1 | Cancel tools and switch/close documents while operations are active; reject stale results and keep unrelated documents correct. | — | Not tested | — | — |
| E09 | E1 | C1 | Float/redock and reopen tools; correct active owner/session, no duplicated subscriptions, native hosts or leaked captures. | — | Not tested | — | — |

### F. Lifecycle and privacy

| ID | Environment | Candidate | Expected behavior | Actual | Status | Notes | Screenshot |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F01 | E1 | C1 | Close during long-running synthetic script/query/search/bulk/transport work; operations drain and workers exit. | — | Not tested | — | — |
| F02 | E1 | C1 | Close dirty documents and exercise Save/Discard/Cancel; Cancel/dismissal keeps draft/history and does not accidentally close. | — | Not tested | — | — |
| F03 | E1 | C1 | Cancel a Save As picker or fail a safe save destination; retain unsaved work and original file bytes. | — | Not tested | — | — |
| F04 | E1 | C1 | After normal application exit, verify ScriptHost/QueryHost processes are gone. | — | Not tested | — | — |
| F05 | E1 | C1 | Inspect documented synthetic temporary storage after normal close; record cleanup completion and any residual plaintext without claiming secure erasure. | — | Not tested | — | — |
| F06 | E1 | C1 | Force terminate during synthetic work, restart, and record abandoned temporary data and bounded later cleanup behavior. Do not expect unsaved-draft recovery. | — | Not tested | — | — |
| F07 | E1 | C1 | Run two instances with separate synthetic documents; closing one does not remove the other's active leased data or worker. | — | Not tested | — | — |
| F08 | E1 | C1 | Save preferences/layout and restart; metadata persists, clinical content/network authorization do not reopen automatically. Record multi-instance conflicts. | — | Not tested | — | — |
| F09 | E1 | C1 | Compare application-directory inventory/hashes before/after all checks: no application-owned settings, logs, snapshots or dumps beside executables. | — | Not tested | — | — |

### G. UI and display

| ID | Environment | Candidate | Expected behavior | Actual | Status | Notes | Screenshot |
| --- | --- | --- | --- | --- | --- | --- | --- |
| G01 | E1 | C1 | At 100%, 150% and 200% display scaling where feasible, inspect editor, ribbon and dialogs; record unavailable configurations as Blocked with reason. | — | Not tested | — | — |
| G02 | E1 | C1 | Repeat main workflows in Light and Dark themes; labels, focus and diagnostics remain readable. | — | Not tested | — | — |
| G03 | E1 | C1 | Use narrow and maximized windows; inputs, status and Apply remain reachable without overlap or clipping. | — | Not tested | — | — |
| G04 | E1 | C1 | Float/redock tools, change monitors and restore saved placement; retain accessible windows and correct owner. | — | Not tested | — | — |
| G05 | E1 | C1 | Check keyboard focus, Tab/Shift+Tab, modal activation and cancellation from main and floating windows. | — | Not tested | — | — |
| G06 | E1 | C1 | Inspect long synthetic names/values/paths, bounded previews and complete-value actions; no preview is treated as complete source. | — | Not tested | — | — |
| G07 | E1 | C1 | Verify clipboard actions from docked/floating tools and large-font dialogs; review synthetic-only text. | — | Not tested | — | — |
| G08 | E1 | C1 | Navigate/search offline Help and Script API under these display conditions; no download or runtime dependency is required. | — | Not tested | — | — |

### Owner decision after evidence review

Record date, reviewer, failed/blocked rows and limitations. Native acceptance alone
does not approve redistribution: authenticated exact-revision CI evidence, current
inventory/MPL/rights review, recipients, transfer/checksum method and unsigned
status still require explicit owner approval. Do not share this ZIP yet. Submit
only synthetic-only reviewed screenshots/notes; no PHI, credentials, production
paths, dumps or temporary snapshots. Best-effort cleanup is not secure deletion.

## 16. Remaining release blockers and limitations

- **Unknown exact-revision Actions status:** obtain authenticated successful run
  evidence, including run ID and relevant check conclusions for 933bd667.
- **Native acceptance:** complete all 68 E1/C1 rows on a genuinely separate patched
  Windows 11 x64 standard-user environment, with no installed modern .NET or
  developer dependencies. No Desktop GUI/Defender/MOTW/clipboard/IME/DPI/accessibility,
  multi-instance or clean-machine lifecycle claim is made from these worker checks.
- **Human rights/redistribution review:** resolve section 5 and the current checklist;
  selected-notice checks are technical completeness, not legal advice or approval.
- **Unsigned artifact:** obtain an explicit decision on this NotSigned candidate;
  do not bypass OS security checks or present SHA-256 as a signing certificate.
- **Owner sharing gate:** approve exact artifact/hash, recipients, synthetic-only
  scope, contact and private transfer/checksum method. No transfer is authorized now.
- Plaintext/best-effort cleanup, trusted-code permissions, partial clinical coverage
  and unencrypted transport remain documented current boundaries. No unrequested
  performance soak, clinical conformance certification or Stage 21C is included.

A later code/resource/dependency change requires a new separately authorized,
traceable candidate and repeat acceptance; this ZIP must not silently become a
different payload under the same identity. No current failure requires remediation.

## 17. Protected-file integrity, final status and cleanup

Compared all files under artifacts/private-preview against the original
artifacts/stage17/protected-before.json SHA-256 inventory: **2,384 expected and
present; 0 changed, 0 missing, 0 added**, before candidate creation and again after
verification. Historical tree validators remained read-only and passed.

The temporary second ZIP, isolated publish/intermediate outputs, staging tree,
extraction and empty negative fixture are disposable owner-created artifacts;
they are removed only after exact post-smoke checks and evidence copies. Retained
ignored evidence includes the one candidate, checksums/identity, provenance,
dependency and file inventories, immutable hashes, native worksheet, READ-ME-FIRST,
verification scripts, gates and useful logs. Ordinary bin/obj and all prior/user
artifacts/permanent tests remain intact. No protected output was an output target.

Final repository change: **docs/stage21b2-private-preview-candidate.md only**;
PROJECT_CONTEXT.md required no durable architectural/invariant update. It retains
its historically scoped Stage 21B.1 record. Final local links/anchors pass across
160 Markdown files and 1,154 local links. New-report whitespace, Git diff check
and unchanged source input hashes pass; results are retained in final evidence.

## 18. No distribution or subsequent stage

**No distribution occurred.** No commit, push, upload, publication, GitHub release,
private tester transfer, signing, update deployment, production/real-patient-data
use, unrelated feature work or Stage 21C was performed. The candidate remains
local owner-controlled verification material. **Stop for owner review and native
acceptance; explicit owner approval is required before sharing with anyone.**
