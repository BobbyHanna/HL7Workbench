# Stage 21B.1 — Portable distribution preparation

## 1. Executive summary and baseline

Prepared current-source three-process deployment and technical redistribution checks
for a future **synthetic-only**, approximately two-person Windows x64 preview.
No application ZIP, distributable candidate, upload, publication or transfer was created.
Temporary isolated publishes were explicitly labeled non-distributable and removed
after verification. Existing historical artifacts and the Stage 21A report are preserved.

Baseline is `main` at `cd71c482ec62351e0bf9b650a2f6bbb6a5d68c46`. The only initial
untracked file was the completed Stage 21A audit. Its deployment findings remained
accurate: the legacy builder covered two executables, QueryHost lacked current portable
deployment, and current redistribution material was outside the old inventory.
No authenticated Actions evidence was available; latest status remains **Unknown**.
New changes are local/uncommitted, so these results are local verification, not CI claims.

R01's three-process deployment and R02's technical notice/source-delivery preparation
are implemented. R05 build identification, R09 first-run guidance and R10 manual
feedback are addressed. Human redistribution clearance, exact-artifact clean-machine
acceptance and owner distribution approval remain explicit blockers.

The final real publish check passed for a **795-file verification tree**, including
both worker lifecycles in an actually write-denied spaces/Unicode path, unrelated
working directory and disabled installed-runtime lookup. This does not establish
native clean-machine Desktop/GPU/Defender acceptance. Feature engines are unchanged.

## 2. Exact implementation files changed

36 files relative to the preserved pre-implementation state: 23 tracked updates and
13 new files. The separately pre-existing untracked Stage 21A report is not part of
this implementation delta and must remain unchanged.

```text
.gitattributes
.github/workflows/build.yml
build/BuildPortableCandidate.ps1
build/QueryHost.targets
build/ScriptHost.targets
build/VerifyPortableWorkers.ps1
build/portable_release.py
build/test_portable_preparation.py
build/test_portable_release.py
build/test_release_compilation.py
build/third-party/ericsink-SQLitePCL.raw-LICENSE.TXT
build/third-party/ericsink-SQLitePCL.raw-NOTICE.TXT
build/third-party/upstream-licenses.json
docs/PROJECT_CONTEXT.md
docs/dependencies.md
docs/developer/build-and-test.md
docs/help-topics.json
docs/portable-release.md
docs/preview-readme.md
docs/private-preview-feedback.md
docs/redistribution-review-checklist.md
docs/stage21b1-portable-distribution-preparation.md
docs/user/getting-started.md
docs/user/index.md
docs/user/installation.md
docs/user/troubleshooting.md
src/HL7Workbench.Desktop/Documentation/catalog.json
src/HL7Workbench.Desktop/Viewer/AboutWindow.cs
src/HL7Workbench.Desktop/Viewer/BuildIdentity.cs
src/HL7Workbench.Desktop/packages.win-x64.lock.json
src/HL7Workbench.QueryHost/packages.win-x64.lock.json
src/HL7Workbench.Querying/packages.win-x64.lock.json
src/HL7Workbench.Workspace/packages.win-x64.lock.json
tests/HL7Workbench.Desktop.Tests/BuildIdentityTests.cs
tests/HL7Workbench.Querying.Tests/PortableWorkerTests.cs
tests/HL7Workbench.Scripting.Tests/PortableWorkerTests.cs
```

No parser, editing/serialization, SQL execution, scripting execution, transport,
bulk-operation, storage-model or validation-resource content change is included.
No production project/dependency/version was added. Ordinary lockfiles and the other
four existing RID lockfiles remain unchanged.

## 3. Three-process publishing architecture

The existing [builder](../build/BuildPortableCandidate.ps1) now requires
**-VerificationOnly** before doing any work. It uses the existing
[portable_release.py](../build/portable_release.py) assembler/validator rather than a
second release pipeline. PowerShell 7.2+, the selected SDK and Python 3.11+ standard
library are build-machine requirements only.

| Setting | All three executable publishes |
| --- | --- |
| Projects | Desktop, ScriptHost, QueryHost |
| Target / RID / configuration | net10.0 / win-x64 / Release |
| Runtime | Self-contained, pinned Microsoft.NETCore.App 10.0.12 |
| Single file / trimming / AOT / ReadyToRun | All explicitly disabled |
| Privacy/build identity | Existing deterministic PathMap, CI build setting and no-symbol policy preserved; private build-path guard remains enabled |
| Intermediate/output ownership | New isolated --artifacts-path plus separate publish directories beneath a newly owned verification root |

Separate ScriptHost/ and QueryHost/ directories retain their own complete runtime,
managed dependencies, resources and apphosts. QueryHost retains native e_sqlite3.dll.
The renamed Desktop apphost remains bound to HL7Workbench.Desktop.dll. Application
assembly/settings identities are unchanged.

[ScriptHost.targets](../build/ScriptHost.targets) suppresses development-tree copying
when separate publication is selected. [QueryHost.targets](../build/QueryHost.targets)
adds RID-aware roots, excludes nested unrelated build/publish trees, suppresses copying
for separate publication and rejects a self-contained Desktop publish that does not
declare separate QueryHost ownership. The orchestrator explicitly publishes all three.
An ordinary Desktop build continues deploying its ordinary development worker trees.

Production resolver code is unchanged: a DLL worker path selects only its adjacent
native apphost when present; arguments use the existing separate argument list.
The development DLL/dotnet fallback remains, but the current portable validator refuses
missing apphosts/configs/dependencies. There is no fallback to a different development
output, arbitrary binary search path or new runtime service.

## 4. RID restore and lockfile strategy

Added packages.win-x64.lock.json to Querying and QueryHost. Desktop and Workspace's
existing RID locks gained only the missing query project-reference entries. The
other RID locks and all normal locks retain their original bytes. No package version
was changed for convenience or otherwise.

One explicit --force-evaluate restore into owned metadata outputs generated/reconciled
the necessary locks. Its diff was reviewed: existing changes are project references,
not package upgrades. Permanent tests compare every production ordinary/RID package
version and content hash and require all eight RID lockfiles.

Final verification runs normal and isolated locked solution restores, then locked
win-x64 restores for **each** executable graph before --no-restore publishes:

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet restore HL7Workbench.sln --locked-mode --artifacts-path artifacts/stage21b1-verification/isolated-restore
```

The builder's per-project restore adds `-r win-x64`, `--locked-mode`, isolated
`--artifacts-path`, SelfContained=true, RuntimeFrameworkVersion=10.0.12 and
NuGetLockFilePath=packages.win-x64.lock.json. Its explicit separate-worker properties
apply consistently during restore and publish. Recipient machines need no installed
SDK/runtime, Python, Docker, WebGateway, InterSystems or SQLite server for these tools.

## 5. Completeness, worker verification and historical compatibility

The **current-three-process / schema 2** format requires all three x64 apphosts with
their exact assembly binding, deps/runtimeconfig files and pinned includedFrameworks.
Framework-dependent configurations fail. Every selected win-x64 runtime/native/localized
resource asset in each published deps graph must exist. Only the already approved
symbol files and optional createdump.exe are deliberately omitted. An undeclared
DLL, data/configuration file, source/fixture or unapproved covered resource fails validation.

The three-process package inventory must match deps libraries and process scopes exactly.
QueryHost's four pinned SQLitePCLRaw entries and SQLite entry are mandatory. Missing
native SQLite, runtime configuration, transitive DLL, required document, notice or
covered-source file fails even if file checksums are refreshed. Notice and upstream
hash proofs, private-path checks and complete file checksums also remain mandatory.

Current staging produces only NON-DISTRIBUTABLE-VERIFICATION.txt plus the owned test
tree. The current CLI requires --verification-only and --query; it never invokes archive
creation. Failure withdraws only its newly created output tree. Existing output roots
and protected private-preview targets are refused; incoming publishes/logs are not
deleted by the Python stager. The PowerShell owner finally restores the test ACL and
removes its checked, explicitly owned temporary subtree, preserving diagnostic logs.

[VerifyPortableWorkers.ps1](../build/VerifyPortableWorkers.ps1) reuses the existing
ScriptHost lifecycle verifier and adds typed QueryHost import/query/close verification.
The final run established:

- All three actual self-contained publishes and their locked restores succeed.
- The extraction path contains spaces and Unicode. A Windows deny-write/delete ACL
  is applied; an actual attempted write must be denied before smoke tests start.
- Both workers execute from an unrelated System32 working directory with PATH limited
  to System32, unusable DOTNET_ROOT/DOTNET_ROOT_X64, and multilevel lookup disabled.
- ScriptHost passes current/whole Main, shared state, completion, CSV/deferred output
  and clean-exit checks. QueryHost reports SQLite **3.53.4**, imports three synthetic
  Documents rows, returns the complete expected count and closes cleanly.
- Revalidation confirms the 795-file extraction was unchanged by the workers.
- Temporary publish/bin/obj/staging outputs are removed; no application archive exists.

**Legacy is explicit.** The historical validator/assembler preserves its two-process
rules and accepted-binary/input/archive proofs. Current validation defaults to current
scope, never guesses legacy from absent fields, and cannot silently omit QueryHost.
Historical CLI assembly requires --scope legacy plus the accepted archive/hash; it
cannot use arbitrary current sources as the old format. The 14 historical tests retain
their assertions under their actual legacy scope. No historical artifact was rebuilt,
repackaged, overwritten or deleted.
Read-only validation also passes on all four actual protected historical trees:
candidate-1 and candidate-2 each contain 582 verified files; final-checkpoint and
final-checkpoint-rebuild each contain 586. Their original checksums remain valid.

## 6. Dependency and notice inventory

Current notices derive from actual selected published deps for Desktop, ScriptHost
and QueryHost, including shared-runtime scopes. They do not inventory every unrelated
platform in the NuGet cache. Existing MIT, ANGLE, .NET, Roslyn, HarfBuzz, Skia, Wuffs
and other original package/component texts and pinned upstream hashes are preserved.

The four **exact** SQLitePCLRaw package/version pairs at 3.0.5 must declare Apache-2.0.
An arbitrary Apache package, changed version or changed declaration is not admitted.
Native SQLite/3.53.4 must have its exact reviewed package file license declaring public
domain. Unknown expressions/file licenses and absent legal text still fail closed.

SQLitePCLRaw's actual v3.0.5 tag resolves to
`ed046114d5a30534e13294d94d78eb73de896ad4`. Its original upstream license and NOTICE
are retained byte-for-byte with source/revision/raw/stored hashes in
[upstream-licenses.json](../build/third-party/upstream-licenses.json):

| File | SHA-256 |
| --- | --- |
| ericsink-SQLitePCL.raw-LICENSE.TXT | cfc7749b96f63bd31c3c42b5c471bf756814053e847c10f3eb003417bc523d30 |
| ericsink-SQLitePCL.raw-NOTICE.TXT | 5a1ab2f670f86dd2f8d42e40a8459cc278ca6d5225a7e2a5fa3ea0184396a6d9 |

Two narrowly scoped Git attributes preserve those vendor bytes/original whitespace.
Existing upstream records are not rewritten semantically. Current records also contain
notice-file hashes. Repository metadata rejects credentials, arbitrary query URLs and
private/local schemes; the exact public Microsoft redirect used by the four pinned
Windows Skia/HarfBuzz packages is explicitly reviewed, rather than permitting general
query-bearing URLs.

## 7. Exact adapted validation source delivery

No adapted resource, generator algorithm or original attribution text was modified.
Current staging deterministically copies:

| Repository input | Delivered file |
| --- | --- |
| src/HL7Workbench.Core/ValidationCatalog/v251-model.json | covered-source/v251-model.json |
| src/HL7Workbench.Core/ValidationCatalog/NOTICE.md | covered-source/NOTICE.md |
| build/third-party/nHapi-MPL-2.0-LICENSE.txt | covered-source/MPL-2.0.txt |

Provenance records MPL-2.0, upstream revision
`9027797b1fd1faebcffd743ac11324499145f29d`, and exact source/license/notice hashes.
The modified JSON includes its upstream paths/digests and the existing NOTICE explains
translation changes. Staging compares delivered bytes/hashes with the input inventory
and exact uncompressed embedded payload in both Core-carrying assemblies. A permanent
.NET regression separately opens the logical manifest resource and compares it with
the source bytes. Updating only a claimed source hash cannot substitute a different JSON.

This supplies the exact adapted source rather than an upstream link alone. It does
not imply the independently authored application becomes MPL-licensed or resolve
all legal applicability/notice questions automatically.

## 8. Remaining human redistribution decisions

The included [Redistribution Review Checklist](redistribution-review-checklist.md)
remains unchecked until an accountable reviewer/owner approves the actual artifact.
It covers selected native/component obligations, Apache/SQLite notices, MPL source
availability and recipient/organization context, application/artwork/employer rights,
preview wording, exact-artifact acceptance, signing/unsigned status, recipients and
authenticated transfer/checksum delivery. Automation verifies technical presence,
declarations, provenance and bytes; it cannot independently grant legal clearance.
Any unresolved obligation remains a release blocker. No notice guard was relaxed to
make a candidate pass.

## 9. Build identification and provenance

[BuildIdentity](../src/HL7Workbench.Desktop/Viewer/BuildIdentity.cs) reads the actual
AssemblyInformationalVersion. About shows public Version plus an eight-character
Build only for a complete 40/64-hex revision; missing/partial/untrusted metadata displays
unavailable. Arbitrary paths, usernames, credentials and URLs cannot be displayed as
build metadata. The helper is covered by fallback/valid-token and actual About-view tests.

The current local test build correctly identifies base commit **cd71c482**. It is
dirty/uncommitted; that short baseline is not presented as a new committed release.
Machine-readable provenance identifies the dirty state and exact input hashes.
The builder requires published product metadata to match its actual checkout commit;
it does not fabricate source identity. Unknown-metadata fallback remains available in
the UI; untraceable checkout/binary combinations are not automatically staged by the CLI.

Schema 2 provenance includes application version, source commit/build ID, Release,
win-x64, SDK/runtime, relative input hashes, dependency-inventory SHA-256, covered-source
hashes and file-checksum information. Archive checksum is explicitly null because no
archive is created. Absolute developer roots/private repository URLs are not inputs to
the provenance schema. AssemblyVersion, FileVersion, namespaces and persisted settings
identity remain unchanged.

## 10. First-run and offline documentation

[Getting Started](user/getting-started.md) now works with only an approved current
extraction: launch, note Build, generate three synthetic ADT messages, navigate,
inspect components, Edit Value, Undo/Redo, list unique values, preview/confirm structural
replacement, find Bulk Result, return to the unchanged input, Save As/reopen, and open
offline Help. It explains original source versus editable working state, generated
input, independent complete bulk output and explicitly saved file. It requires no
repository-only fixture or download and no longer denies the existing structured edit.

Current recipient inputs include README.md from preview-readme.md, USER-GUIDE.md from
that canonical first task, PRIVATE-PREVIEW.txt, FEEDBACK.md and REDISTRIBUTION-REVIEW.md.
The source index/installation/troubleshooting and Help keywords are consistent with
the new workflow. The established compiler regenerated the embedded catalog; its
strict byte check passes. No onboarding wizard or UI/navigation redesign was added.

## 11. Manual feedback and privacy boundaries

[The template](private-preview-feedback.md) asks for build ID, Windows/architecture,
intent, synthetic type/count, steps, expected/actual behavior, fixed status category,
restart reproducibility and notes. It is an ordinary future ZIP document and requires
manual private transmission by the tester. No Report an Issue action, telemetry,
update check, account, cloud service, diagnostic collector or automatic upload was added.

Guidance explicitly excludes real HL7, PHI, credentials, endpoint secrets, production
filenames, unreviewed screenshots, raw dumps and temporary storage. Activity Log does
not authorize sharing other content. Plaintext snapshots can contain entire messages;
clipboard history/sync are outside application control; trusted C# has user-account
permissions; TCP/MLLP is unencrypted/unauthenticated; bulk output retains out-of-scope
messages; validation/de-identification do not establish clinical conformance, anonymity
or HIPAA compliance. Storage and runtime trust boundaries are unchanged.

## 12. Verification commands and results

Commands use the repository-selected SDK and the three telemetry opt-outs. `python`
below denotes the supplied local Python interpreter; no author-specific installation
path is recorded. All standard-library fixture tests use normal temp-directory
permissions. No source/test expectations were weakened.

| Verification | Completed result |
| --- | --- |
| Normal locked solution restore | Pass |
| Isolated locked solution restore | Pass |
| Locked win-x64 restores / self-contained test publishes | Desktop, ScriptHost and QueryHost all pass with no warnings/errors |
| Final Release solution build | Pass; **0 warnings, 0 errors** |
| Focused .NET checks | **60 passed, 0 failed, 0 skipped**: Desktop 40, Scripting 4, Querying 16 |
| Legacy/current preparation tests | **38 passed, 0 failed**: 14 retained legacy + 24 current |
| Actual historical trees, explicit legacy scope | All four pass read-only validation: 582 / 582 / 586 / 586 files |
| Real compiler/privacy/reuse checks | **2 passed, 0 failed** |
| Full regular solution | **2,941 passed, 0 failed, 14 expected skips**; 2,955 total, **4m 28s 337ms** |
| Documentation compiler | **12 passed, 0 failed** |
| Catalog / Script API consistency | Pass |
| Dependency advisory scan | All 13 projects enumerated; no known vulnerable package entries reported; not a full native-security certification |
| Markdown links/headings / whitespace | **159 Markdown files, 1,136 local links, 43 heading anchors**, zero errors; diff/new-file whitespace checks pass |
| Protected SHA-256 comparison | **2,384 expected/present; 0 changed, 0 missing, 0 added** |

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet restore HL7Workbench.sln --locked-mode --artifacts-path artifacts/stage21b1-verification/isolated-restore
./build/BuildPortableCandidate.ps1 -VerificationOnly -OutputDirectory "$PWD/artifacts/stage21b1-verification/publish-check-final" -Python python
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

Focused MTP runs use Release/no-build/no-restore with one --filter-class pattern
for BuildIdentityTests, ReleaseReadinessTests, DocumentationTests,
DocumentationViewTests, each project's PortableWorkerTests, and QueryStartupTests.
Fifteen new .NET cases and 24 current Python preparation cases were added permanently.
CI adds only the preparation/real-compiler tests; it creates no distributable artifact
or upload. Protected hashes, advisories and full-repository links still require their
documented manual/local gates when unavailable to CI.

Early verification diagnostics are retained rather than hidden: a restricted sandbox
Python attempt failed fixture setup with access-denied errors and was rerun with normal
test permissions; a synthetic fixture needed checksum refresh after setup changed its
fake deps; the programmatic About test needed visual-tree lookup rather than XAML name
scope. Real publishing identified the exact public Skia/HarfBuzz repository redirect
and deps-listed optional dump/native-symbol files. Each was reconciled narrowly with
existing policy; runtime-asset/license requirements were not broadly bypassed. Failing
publish attempts and the final successful attempt all cleaned their owned temporary
binaries, retaining logs for review.

Ignored artifacts/stage21b1-verification retains pre-change/tested input hashes,
restore/build/focused/full/compiler/documentation/advisory results and publish logs/
tested provenance. Ordinary bin/obj outputs and permanent tests are preserved. Legacy
unit tests create only synthetic fake-binary archives in auto-cleaned temporary
fixtures; no actual application archive or candidate was assembled.

## 13. Known limitations

This is developer-machine component verification, not a clean Windows VM/PC desktop
test. Real GPU/native startup, SmartScreen/MOTW, enterprise restrictions, monitor/DPI,
IME/clipboard, global shutdown residue and the complete Stage 20C native workflow still
need exact-artifact acceptance. Explicit stress tests were not selected. Latest Actions
status remains Unknown, and local uncommitted changes have no new Actions run.

Current CLI archive creation is deliberately disabled; future candidate creation needs
separate authorization and an explicit current-format path, never a legacy-mode bypass.
Normal development DLL fallback is preserved but is not accepted as portable completeness.
The UI handles unavailable build metadata safely; verification staging requires
traceable checkout/binary metadata. Native/component licensing and covered-source
applicability require human review despite technically complete staging.

## 14. Proposed Stage 21B.2 acceptance procedure — not started

1. Review these changes/results and the Redistribution Review Checklist. Obtain
   explicit approval before a commit or any candidate/archive operation.
2. Establish current CI for the approved clean source and resolve remaining owner/legal
   decisions. Agree the specific supported Windows x64 recipients/environment.
3. Separately authorize an explicit current-format candidate path/mode and exact build
   identity; do not repurpose a verification tree or invoke legacy mode on current sources.
4. Re-run all three locked publish graphs, complete notices/covered source, input/file/
   archive hashes, source-path guards and worker checks for that exact artifact.
5. On a genuinely separate supported Windows x64 standard-user environment without
   SDK/runtime, verify the intended transfer/extraction, offline start and both workers,
   spaces/Unicode/read-only paths, Defender/MOTW and the repository-free first task.
6. Complete native inspection/edit/save/Help and the 30-message bulk publication
   reacceptance (both views, independent tab, original preserved, docked/floating).
   Exercise cancel/close, startup recovery and pending OS/lifecycle checks.
7. Reconcile artifact/checksum/signature status and feedback/privacy guidance. Obtain
   explicit owner approval of the exact artifact, recipients and transfer before sharing.

## 15. Explicit release blockers

- Human approval of current dependency/component notices and exact adapted-source
  availability/applicability for the actual recipients/organizations.
- Clean committed provenance/current CI and exact-artifact clean-machine/native
  acceptance, including actual OS/native prerequisites and security policy.
- Approved unique artifact identity, checksums/authenticity/signing or unsigned policy,
  recipient scope, synthetic-only guidance and transfer method.
- Separate authorization for commit, candidate/archive creation, publication or
  distribution. No temporary verification output is a substitute for these approvals.

## 16. Protected-file and final scope audit

Compare every protected file's SHA-256 with the original
artifacts/stage17/protected-before.json inventory, and compare the full current file
set for missing/added entries. The completed check confirms **2,384 expected/present,
zero changed/missing/added**. All **546** captured runtime/build/test input hashes
match the completed test run. The original 15 upstream license records and Stage 21A
report also retain their pre-change contents; only the two pinned new notice records
were appended. All 14 full-suite skip identities match the existing opt-in SQL cases.

Final scope is the 36-file implementation delta listed above, plus the unchanged,
pre-existing untracked Stage 21A report. HEAD remains cd71c48, with an empty index.
No .dll/.exe/.pdb/.zip exists in the owned verification evidence tree after cleanup.
Temporary lock-review/isolated-restore metadata were removed after their results were
recorded; publish failures and successful runs retain only logs/markers/provenance.
Ordinary repository build outputs and useful permanent verification infrastructure
remain intact. Vendor legal texts retain their exact original whitespace/bytes under
the narrowly scoped attributes; other changed/new text passes whitespace checks.
NuGet lockfiles retain the generator's native EOF format rather than receiving an
unrelated newline rewrite.

The original Stage 21A report must match its pre-change hash. No protected directory
is an output or cleanup target. No commit, push, final ZIP/candidate, upload,
publication, distribution, Stage 21B.2 or Stage 21C work is performed. Stop for review.
