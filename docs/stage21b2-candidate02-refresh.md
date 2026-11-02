# Stage 21B.2 — Candidate 02 refresh

Verification date: **2026-11-02**. **Local owner-controlled verification only;
AWAITING OWNER ACCEPTANCE / NON-DISTRIBUTABLE / UNSIGNED.** Candidate 02 supersedes
Candidate 01 for native acceptance purposes only. No distribution is authorized.

## 1. Executive summary

Created one new current-source Windows x64 candidate from the committed hotfix
revision tied to successful Actions run **41465370655** by owner-verified GitHub
CLI evidence. All local prerequisites, exact-archive checks, published-worker smokes
and comparison checks pass. The ZIP contains **795 files**, measured from the
validated archive rather than assumed from Candidate 01.

Fresh regular tests: **2,941 passed, 0 failed, 14 expected skips**. Release:
**0 warnings, 0 errors**. The exact portable Actions command passes **84/84**,
including real Windows short-path coverage. Candidate 01's **42 files**, its
worksheet/evidence and original completion report remain unchanged; all **2,384**
protected historical hashes match. The new worksheet preserves all **68** native
checks, each still **Not tested**. Native acceptance, rights/legal review and
explicit sharing approval remain outstanding.

## 2. Exact source baseline and CI evidence

| Property | Recorded evidence |
| --- | --- |
| Branch / full HEAD | main / `8f5c43cc568c7777be41305e6eb8a0792e3b5c62` — CI Fix |
| Initial working tree / index / untracked files | Clean / empty / none; ignored evidence is excluded |
| Version / About Build | 0.1.0-beta / 8f5c43cc |
| Workflow / run | Build and test / [41465370655](https://github.com/BobbyHanna/HL7Workbench/actions/runs/41465370655) |
| Run status / conclusion / headSha | completed / success / `8f5c43cc568c7777be41305e6eb8a0792e3b5c62` |
| Evidence method | Owner supplied confirmed GitHub CLI output; exact command below |
| Committed hotfix | Canonical relative-path helper and Windows alias regression matrix are tracked in HEAD |
| Build-state provenance | dirty=false; same committed revision for all three processes |

```powershell
gh run view 41465370655 --json headSha,conclusion,status,url
```

The owner explicitly confirmed these fields and authorized proceeding with this
exact revision. The automation environment still has no gh executable; web/native
browser access had been unavailable, so this report does not misrepresent the
owner's verified output as an independent remote fetch. The committed workflow has
one **foundation** job; its success follows from the successful single-job run,
not separately downloaded job/step records. Remote per-step test totals were not
obtained; all counts below are from fresh local commands.

HEAD changes exactly four files versus 933bd667: the release helper, new path tests,
hotfix report and previously completed Candidate 01 report. There is **no difference
in src/, Directory.Build.props, Directory.Packages.props or global.json** between
the candidates. The [hotfix](stage21b1-short-path-ci-hotfix.md) is committed, rather
than silently incorporated from a dirty worktree. No commit/push was performed here.

## 3. Candidate 01 preservation

Original source: `933bd6675b6baa66be1a2aa3f48acfad988e783e`.
Original archive:

```text
C:\Dev\HL7Workbench\artifacts\stage21b2\933bd667-owner-candidate-01\MessageWorkbench-0.1.0-beta-933bd667-win-x64-AWAITING-OWNER.zip
SHA-256 ad69f7a74c1a232436325f42ec028574d7648fb985be315e888f1aff5423d05b
```

Before staging, verified the ZIP exists and matches its recorded hash; inventoried
all **42** existing candidate/evidence files and compared them to the earlier hotfix
frozen inventory. This includes its provenance, checksums, native worksheet, scripts,
logs and identity record. Its [original completion report](stage21b2-private-preview-candidate.md)
also matches the frozen SHA-256. The final comparison repeats those checks.
Candidate 01 was never rebuilt, renamed, overwritten, re-manifested, edited or used
as an output directory. Old PE bytes were copied **from its ZIP** into a new owned
Candidate 02 temporary comparison directory for read-only version inspection.

## 4. Immutable Candidate 02 build and configuration

The initial **725 tracked files** were hashed before publishing; all remain exact.
The current provenance inventories **350** relevant source/build/recipient inputs,
with aggregate canonical inventory hash:
`2ef9a5e6d0460139094d0154c95e9dfafcf00873dee484dd3b3fc4925884495f`.
No unexpected tracked/untracked source was present. Existing ignored artifacts are
outside the selected publish trees and Git input inventory, and the validator
rejects unclaimed staging files. No stash/reset/clean or user-file deletion occurred.

Fresh Desktop, ScriptHost and QueryHost publishes use a new isolated artifacts
path under Candidate 02's temporary spaces/Unicode directory. No prior publish or
normal bin/obj bytes are copied as inputs. Shared project references built within
this fresh operation use the same verified source revision.

| Setting | Exact value |
| --- | --- |
| Target / RID / configuration | net10.0 / win-x64 / Release |
| SDK / self-contained runtime | 10.0.401 / Microsoft.NETCore.App 10.0.12 |
| SelfContained | true for all three executable processes |
| Deployment | Multi-file; no installer or external runtime dependency |
| PublishTrimmed / PublishAot / PublishSingleFile / PublishReadyToRun | false / false / false / false |
| DebugType / DebugSymbols / ContinuousIntegrationBuild | None / false / true |
| Restore / publish guards | --locked-mode; packages.win-x64.lock.json; publish -warnaserror |
| Worker targets | PublishScriptHostSeparately=true; PublishQueryHostSeparately=true |

Used the committed [current assembler](../build/portable_release.py) and unchanged
[build recipe](../build/BuildPortableCandidate.ps1), with the established Candidate 01
owner orchestration copied to the new directory and adjusted for the exact revision
and candidate number. The builder's verification-only CLI/cleanup behavior remains
unchanged: its restore/publish configuration is reused; current staging invokes
--scope current --verification-only; its existing deterministic_zip/verify_archive
APIs perform the separately authorized local archive assembly. No legacy fallback,
new production packaging architecture, dependency upgrade, source generation,
application feature or validator exception was introduced.

## 5. Exact Candidate 02 identity and local location

| Property | Value |
| --- | --- |
| Candidate ID | `21B2-8f5c43cc-02-2ef9a5e6d046` |
| Archive size / file entries | **133,101,099 bytes / 795** |
| Native acceptance status | **Not tested**, all 68 rows |
| Apphost signing status | All three **Authenticode NotSigned** |
| Retained Candidate 02 ZIPs | One; disposable second assembly removed after comparison |

```text
C:\Dev\HL7Workbench\artifacts\stage21b2\8f5c43cc-owner-candidate-02\MessageWorkbench-0.1.0-beta-8f5c43cc-win-x64-AWAITING-OWNER.zip
```

Archive SHA-256:

```text
ab71b803e927d3def44090de826968e932c077769dd352afb8f4152e5a819044
```

The adjacent .zip.sha256 and CANDIDATE-IDENTITY.json bind this archive to exact
schema-2 BUILD-PROVENANCE.json, dependency/file inventories and committed build-tool
hashes. Internal provenance retains verificationOnly=true, current-three-process,
dirty=false and artifactChecksums.archive=null, as required by the established
format. The external identity supplies the final ZIP SHA-256 without a circular
self-hash or schema change. Application FileVersion is 0.1.0.0; AssemblyVersion
remains 1.0.0.0. All 14 project apphost/assembly copies have the correct new full
informational revision, verified against Candidate 01's original revision.

## 6. Exact archive validation and three-process completeness

Validated the **actual ZIP**, then its isolated extracted bytes:

- CRC integrity; canonical safe relative entries; no drive/absolute/traversal,
  backslash/NUL, trailing-dot/space or reserved Windows device names.
- No duplicate or Unicode-normalized case-colliding names, encrypted/symlink entries,
  directory/file prefix collisions or unknown files.
- Exact expected file inventory, entry sizes and streamed SHA-256 hashes; full
  extraction round trip and post-smoke revalidation against frozen staging.
- All three x64 PE apphosts/bindings, deps-selected runtime/native/resource assets,
  standalone runtimeconfigs and coreclr/hostfxr/hostpolicy; native SQLite in QueryHost.
- Required assets, exact embedded offline Help and Script API payloads, notices,
  covered source, provenance and checksum scope.
- Strict allowlisting and known checkout/home path scans (UTF-8/UTF-16 spellings);
  no private build paths, fixtures, patient exports, settings, endpoints, logs,
  snapshots, dumps, PDBs or unapproved source in the ZIP. Only the explicit adapted
  covered source is delivered. Existing optional dump/symbol exclusions are retained.

| Process | Actual entry / result |
| --- | --- |
| Desktop | MessageWorkbench.exe → HL7Workbench.Desktop.dll; x64 dependency/resource/runtime checks pass |
| ScriptHost | ScriptHost/HL7Workbench.ScriptHost.exe; independent runtime, execution dependencies and protocol pass |
| QueryHost | QueryHost/HL7Workbench.QueryHost.exe; independent runtime, native e_sqlite3.dll and protocol pass |

All three include Microsoft.NETCore.App **10.0.12**, without installed-framework
references. This dependency/worker evidence does **not** establish native Avalonia
GUI, GPU, Defender, clipboard, accessibility or clean-machine acceptance.

## 7. Dependency notices and covered source

The actual manifests yield **40 unique dependency records** and **74 notice files**,
unchanged byte-for-byte from Candidate 01. Process memberships are Desktop 33,
ScriptHost 5 and QueryHost 6 (shared packages count once globally). All selected
package scopes, original/pinned upstream notice hashes, strict license declarations
and native components pass. QueryHost retains SQLite 3.53.4 and the four SQLitePCLRaw
3.0.5 entries, including Apache license/NOTICE and SQLite's exact package declaration.

Dependency inventory SHA-256:
`1fadcc8a21d9ae1e1f383b7c48643b303cc2ed7e000b896de12e73df6121cd2e`.

Exact adapted nHapi revision: `9027797b1fd1faebcffd743ac11324499145f29d`, MPL-2.0.

| Covered file | SHA-256 |
| --- | --- |
| covered-source/v251-model.json | `99af17a10e7f5ff227121c34cd2bccb1cd875c8156e588f84efc4b6fcdacdc4e` |
| covered-source/NOTICE.md | `ce23986d3d4e04aa2ace65f478db6a7a4304f5b4a97140f2af7e874947c8281e` |
| covered-source/MPL-2.0.txt | `2684de17300e0a434686f1ec7f8af6045207a4b457a3fe04b2b9ce655e7c5d50` |

All three files match repository inputs/provenance and Candidate 01; the delivered
JSON exactly matches embedded bytes in both Core copies. Notice/source delivery is
verified in the ZIP, not replaced with upstream links. Automated completeness is
not legal clearance; see the [human checklist](redistribution-review-checklist.md),
[dependency review](dependencies.md), [historical distribution review](beta-distribution-review.md)
and [native applicability record](native-license-applicability-review.md).

## 8. Published executable smoke tests

Ran the committed worker verifier and copied cancellation/negative-fixture probes
against **Candidate 02's extracted archive bytes**, under a genuine inherited deny
Write/Delete ACL. A write probe was rejected; the ACL was restored afterward.
Child apphosts used System32 as working directory, no SDK on PATH, invalid
DOTNET_ROOT/DOTNET_ROOT_X64 and disabled multilevel lookup.

| Actual candidate-byte check | Result |
| --- | --- |
| ScriptHost startup / trusted synthetic C# | Pass: protocol 2, unified Main current/whole lifecycles, shared state, transformed synthetic output, CSV/completion/deferred emission, zero-exit shutdown |
| QueryHost startup / native SQLite / SQL | Pass: protocol 2, SQLite 3.53.4, three typed synthetic Documents rows, COUNT=3, complete page and zero-exit shutdown |
| Cooperative script cancel | Execution-output barrier, Cancel frame, failure frame and bounded exit 1; no successful partial result |
| Noncooperative script | Execution barrier, Cancel, bounded 200 ms grace then process-tree termination/reap |
| Query cancel | Actual pending SourceRead RPC barrier; withhold reply, Cancel, failure frame and bounded exit 1 |
| Missing-worker handling | Actual candidate parent assemblies return WorkerUnavailable for both deliberately absent paths in an empty owned negative fixture |
| Desktop dependencies / docs / version | Pass: complete x64 deployment, exact embedded Help/API payloads, correct version/source on all project PEs; GUI acceptance remains Not tested |

No arbitrary sleeps were used to establish execution/cancellation success. Missing
worker tests did not remove anything from the candidate. Parent/full UI cancellation
and publication regression results are from the separately built regular .NET suite,
not mislabeled as native candidate UI tests. Compiler/unit fixture trees and fake
ZIPs are separate test evidence. An SDK-equipped development host with hidden lookup
is not a genuinely clean Windows environment.

Precise commands (repository root; all paths below are owned verification outputs):

```powershell
pwsh -NoProfile -File artifacts/stage21b2/8f5c43cc-owner-candidate-02/publish-local.ps1
python -B artifacts/stage21b2/8f5c43cc-owner-candidate-02/assemble-owner-candidate.py
pwsh -NoProfile -File artifacts/stage21b2/8f5c43cc-owner-candidate-02/verify-candidate-smoke.ps1
python -B artifacts/stage21b2/8f5c43cc-owner-candidate-02/compare-candidates.py
pwsh -NoProfile -File artifacts/stage21b2/8f5c43cc-owner-candidate-02/verify-compared-versions.ps1
```

## 9. Deterministic archive assembly

Two assemblies from the **same immutable 795-file inputs** produced identical
archive SHA-256 `ab71b803e927d3def44090de826968e932c077769dd352afb8f4152e5a819044`. Existing sorted entries, fixed timestamps/
attributes and Deflate settings were retained. Toolchain: Python 3.12.14, zlib 1.3.2.
The temporary comparison ZIP was removed, leaving one Candidate 02 ZIP. This is
archive determinism in the stated toolchain, not reproducible compilation across
SDKs/machines or a claim that different source revisions produce identical binaries.

## 10. Fresh regression verification

| Check | Actual result |
| --- | --- |
| Normal locked solution restore | Pass |
| Locked win-x64 restores / fresh publishes | Desktop, ScriptHost, QueryHost all pass; -warnaserror publish |
| Release solution build | Pass; 0 warnings, 0 errors |
| Full regular Microsoft.Testing.Platform suite | **2,941 passed / 0 failed / 14 skipped**, 2,955 total; **5m 25s 652ms** |
| Exact Actions portable command | **84 passed / 0 failed / 0 skipped**, 77.461s; includes all 38 normal cases, 38 real-alias repetitions and eight path checks |
| Real compiler/privacy checks | **2 passed / 0 failed / 0 skipped**, 44.636s |
| Documentation compiler | **12 passed / 0 failed / 0 skipped**, 0.630s |
| Catalog / Script API consistency | Pass, exact generated bytes current |
| Dependency advisory scan | 13 projects enumerated; no vulnerable package entries reported |
| Documentation links / anchors | 162 Markdown files / 1,164 local links including heading anchors; zero errors |
| Whitespace | Git diff check and new-report whitespace pass |
| Historical trees, read-only legacy rules | Four pass: 582 / 582 / 586 / 586 files |
| Protected SHA-256 inventory | All 2,384 match; zero changed, missing or added |
| Candidate 01 / original report | All 42 files plus original report unchanged |
| Committed inputs | All 725 tracked input hashes unchanged |

The 14 expected explicit skips are five QueryResourceStressTests and nine Workspace
SqlPerformanceTests. No opt-in stress branch/native acceptance is claimed. No tests,
expectations or security checks were weakened. The documentation-copy verifier
initially used default Windows decoding for one UTF-8 comparison read; making that
owned helper read explicitly UTF-8 resolved its assertion. All 68 metadata-adjusted
rows then matched exactly. No archive or application fix/replacement resulted.

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

## 11. Candidate 01 versus Candidate 02

| Dimension | Candidate 01 | Candidate 02 / classification |
| --- | --- | --- |
| Source | 933bd6675b6baa66be1a2aa3f48acfad988e783e | 8f5c43cc568c7777be41305e6eb8a0792e3b5c62; expected committed hotfix |
| CI | Recorded Unknown | completed/success, run 41465370655, owner-verified CLI |
| Application version | 0.1.0-beta | Same; About Build changes to 8f5c43cc |
| Archive bytes | 133,101,034 | 133,101,099; expected changed compressed inputs |
| File count | 795 | 795; same exact entry set |
| ZIP SHA-256 | `ad69f7a74c1a232436325f42ec028574d7648fb985be315e888f1aff5423d05b` | `ab71b803e927d3def44090de826968e932c077769dd352afb8f4152e5a819044` |
| Dependencies / notice files | 40 / 74 | Same inventories and bytes |
| Covered source | Three files, hashes in section 7 | All identical |
| Runtime configurations | Three self-contained 10.0.12 configurations | All identical bytes |
| Source input inventory | 349 inputs | 350; modified build/portable_release.py plus added build/test_portable_paths.py |
| Unchanged / changed entries | Baseline | **779 unchanged / 16 expected revision changes** |
| Expected nondeterministic differences | Not applicable | **None identified** |
| Unexpected differences | Not applicable | **None** |

| Apphost | Candidate 01 SHA-256 | Candidate 02 SHA-256 |
| --- | --- | --- |
| MessageWorkbench.exe | `d58df996497ac34b229acc8ac52084f7acfca52bf1d642278181e3dcc8518c0c` | `ef818ec29aec7f4221ad1f72a20b5152c3ece107fd754cc86392a43d156a8704` |
| QueryHost/HL7Workbench.QueryHost.exe | `06906fd7e3785e37a682e70de3292a5e136b4e8f4c5a7a09c6743d90164188dc` | `cd2d90bcea038b6c9c4bbbecbb9f0fa531718ead4d4d10ab04891d9f43ffb012` |
| ScriptHost/HL7Workbench.ScriptHost.exe | `991109f9475c3c94b5722710f92dbd3d90f532c7b0a2cf760df855c7b8021def` | `c9c7241ee8678d3925c1f4cb0353b8e85453e2b93ce8a57e22c5398e88fb6fb6` |

| Changed entry | Classification |
| --- | --- |
| BUILD-PROVENANCE.json | Expected from source revision: commit, build ID and tool/test inputs |
| HL7Workbench.Core.dll | Expected from source revision: new informational-version stamp and compilation identity |
| HL7Workbench.Desktop.dll | Expected from source revision: new informational-version stamp and compilation identity |
| HL7Workbench.Querying.dll | Expected from source revision: new informational-version stamp and compilation identity |
| HL7Workbench.Scripting.dll | Expected from source revision: new informational-version stamp and compilation identity |
| HL7Workbench.Transport.dll | Expected from source revision: new informational-version stamp and compilation identity |
| HL7Workbench.Workspace.dll | Expected from source revision: new informational-version stamp and compilation identity |
| MessageWorkbench.exe | Expected from source revision: new informational-version stamp and compilation identity |
| QueryHost/HL7Workbench.QueryHost.dll | Expected from source revision: new informational-version stamp and compilation identity |
| QueryHost/HL7Workbench.QueryHost.exe | Expected from source revision: new informational-version stamp and compilation identity |
| QueryHost/HL7Workbench.Querying.dll | Expected from source revision: new informational-version stamp and compilation identity |
| SHA256SUMS.txt | Expected from source revision: updated file hashes |
| ScriptHost/HL7Workbench.Core.dll | Expected from source revision: new informational-version stamp and compilation identity |
| ScriptHost/HL7Workbench.ScriptHost.dll | Expected from source revision: new informational-version stamp and compilation identity |
| ScriptHost/HL7Workbench.ScriptHost.exe | Expected from source revision: new informational-version stamp and compilation identity |
| ScriptHost/HL7Workbench.Scripting.dll | Expected from source revision: new informational-version stamp and compilation identity |

All **14** changed project PE copies have the expected old/new ProductVersion full
source stamps and the same FileVersion 0.1.0.0. The unchanged production sources and
build/dependency configuration were confirmed by Git diff; project rebuild metadata
and resulting compilation identity explain these changes. No binary identity was
assumed merely because the application logic was unchanged. All vendor/native/assets/
recipient-document/covered-source entries outside the listed changes are identical.
Complete per-entry old/new hashes and classifications are retained in
CANDIDATE-COMPARISON.json; compared-version-metadata.json records actual PE versions.
There are no unexplained changes blocking native acceptance scheduling.

## 12. Native worksheet — all checks awaiting owner

Exact Candidate 02 worksheet:

```text
C:\Dev\HL7Workbench\artifacts\stage21b2\8f5c43cc-owner-candidate-02\NATIVE-ACCEPTANCE.md
```

Copied the original 68-check worksheet without changing coverage or behaviors.
Only candidate-specific C1→C2 keys, paths, ZIP size/hash, source and About build
metadata were updated. Row-by-row comparison confirms that transformation; **all
68 statuses remain Not tested**. Environment E1 requires a separate patched Windows
11 x64 standard-user VM/machine without modern .NET/developer prerequisites.
A–G still cover install/launch, core viewing/editing, SQL/scripts, all 14 complete
Avery→Morgan regression checks, validation/generation/FHIR/loopback/Help, lifecycle/
privacy/multiple instances, docking, floating tools, themes and DPI. No native check
was premarked from automation. Candidate 01's worksheet remains untouched.

## 13. Practical owner testing instructions

Exact sequential guide (also copied to CANDIDATE-READ-ME-FIRST.txt):

```text
C:\Dev\HL7Workbench\artifacts\stage21b2\8f5c43cc-owner-candidate-02\OWNER-TESTING-GUIDE.md
```

1. Identify the exact ZIP/hash above and independently record the expected value;
   verify the VM copy with built-in Windows PowerShell Get-FileHash. No development
   tools are needed in the VM.
2. Set up the genuinely clean E1 environment; retain Defender/SmartScreen. Record
   security blocks rather than bypassing them.
3. Extract all files into a new spaces/Unicode folder; launch MessageWorkbench.exe.
   Preserve both worker directories, notices, covered source and manifests.
4. Verify About 0.1.0-beta / Build 8f5c43cc. Generate synthetic ADT messages from File
   → Generate Messages; record seed/settings, inspect/edit, Undo/Redo, Save As/reopen.
5. On 30 messages, run SELECT COUNT(*) FROM Messages in the active-file SQL scope,
   navigate results and run the guide's trusted synthetic C# example. Test cancellation.
6. Follow the worksheet's clean Avery baseline and all D01–D14 cases: preview leaves
   the original clean; one active complete Bulk Result shows Morgan in both views;
   original still shows Avery; all 30 messages, current message 17, empty replacement,
   repeated Apply, unchanged sibling fields and docked/floating ownership are checked.
7. Complete every A–G row with Actual/status/notes and reviewed synthetic screenshots.
   Record limitations as Blocked/Not tested. Exercise remaining features using local
   synthetic input and explicit loopback only.
8. Use FEEDBACK.md manually: build/hash, E1 details, row, steps, expected/actual,
   fixed error/status category, restart behavior. No PHI, real messages, credentials,
   production paths/endpoints, raw dumps, snapshots or unreviewed logs/screenshots.

## 14. Remaining acceptance and release gates

Candidate 02 is ready **to undergo owner-controlled native acceptance**, not accepted
or approved for distribution. Complete all 68 C2/E1 checks and review any failures.
The SDK-equipped developer host does not prove native GUI/security/graphics/IME/
clipboard/multi-instance/cleanup behavior on a clean machine.

The CI gate is satisfied by the owner's exact-revision CLI evidence. Remaining human
review covers the exact dependency/native notice inventory, adapted MPL source and
recipient/organization context, application/artwork/employer rights, synthetic-only
scope and feedback contact, unsigned status and trusted checksum/transfer method.
Two trusted recipients are not presumed one internal organization. Automated notice
checks are not legal approval. Retain plaintext storage/best-effort cleanup, trusted
C# account permissions, unencrypted TCP/MLLP and incomplete clinical coverage boundaries.
Do not disable OS protections, infer secure deletion or imply production/clinical,
HIPAA, sandboxing or de-identification certification.

## 15. Final Git status, evidence and cleanup

Only new repository file: **docs/stage21b2-candidate02-refresh.md**, written after
archive creation/verification, so it did not influence clean-source provenance.
No application/build/test/configuration/generated-reference file was modified.
Candidate 01's original report and all candidate/evidence remain byte-identical.

Retained ignored Candidate 02 evidence includes one ZIP, identity and SHA sidecars,
CI/baseline/gates, file/dependency/provenance inventories, full comparison/version
metadata, the 68-row worksheet, owner guide, immutable hashes, reusable copied
orchestration and useful test/smoke logs. The owned bootstrap adapter is retained
at artifacts/stage21b2/bootstrap-candidate02.py. Owned publish/intermediate/staging/
extraction/negative-fixture/old-PE comparison directories and the disposable repeat
ZIP are removed after post-smoke integrity checks and evidence copies. Prior artifacts,
ordinary bin/obj, permanent tests/fixtures and protected files are not cleanup targets.

**No distribution occurred.** No commit, push, upload, publication, private tester
transfer, release, signing, updater, application feature, production data use or
Stage 21C work was performed. Candidate 02 supersedes Candidate 01 only for native
acceptance. **Stop for owner review before any distribution.**
