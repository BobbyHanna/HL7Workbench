# Message Workbench portable release preparation

Target: **v0.1.0-beta**, Windows 10/11 x64, portable self-contained ZIP. Stage 14
is checkpointed as b14b100e399a4eb44a05c98b4844f552f080eb68. The current follow-up
authorizes an unstaged/uncommitted build-path correction and fresh local candidates,
with dirty-source provenance and exact input hashes. No additional commit is authorized.
No installer, administrator rights, installed SDK/runtime, updater, telemetry,
account or automatic network activity is required for normal application use.
Distribution, publication, upload, signing and tags need separate authorization.
This document is a build procedure, not release permission.

## Identity and local build

The intended filename is `MessageWorkbench-v0.1.0-beta-win-x64.zip`, placed beneath a
clearly marked CANDIDATE folder with `CANDIDATE-NOT-FOR-DISTRIBUTION.txt` inside the
archive. The packaged native apphost is **MessageWorkbench.exe**. Renaming that copy
retains its embedded HL7Workbench.Desktop.dll binding; it does not rename assemblies,
namespaces, repository, configuration roots, settings keys or layouts. Windows file
metadata and About show the beta version. AssemblyVersion stays 1.0.0.0 for binding
compatibility. The original approved MW source and derivatives are unchanged.

Build-machine prerequisites: the repository-selected .NET 10 SDK (validated with
10.0.401), PowerShell, Python 3.11+ standard library (no pip packages), and access to
NuGet for the initial restore. Runtime 10.0.12 is pinned. Use a new output directory:

```powershell
./build/BuildPortableCandidate.ps1 -OutputDirectory "$PWD/artifacts/stage14/my-local-candidate" -Python python
```

The script performs locked win-x64 restore using **packages.win-x64.lock.json** in
each production project, then zero-warning self-contained Release publishes of
Desktop and ScriptHost. It never substitutes a framework-dependent Build directory
for a published worker. Trimming, single-file publish, ReadyToRun and debug symbols
are disabled. A nonprivate project reference plus explicit copying prevents worker
execution dependencies from leaking into Desktop. The normal solution lockfiles and
build/test gate remain unchanged; run normal locked restore before normal tests
after RID publishing, since NuGet's intermediate assets file is shared per project.

Release compilation maps this checkout's root to the stable virtual `/_/` prefix
in Directory.Build.props, before incremental compiler inputs are calculated. This
applies to ordinary and portable Release builds, including referenced projects;
it does not change runtime paths or assembly identities. The SDK's late CI-only
SourceRoot mapping and excluding separate PDB files are insufficient: a warm
publish can reuse a normal DLL containing an absolute CodeView PDB reference.
Keep the private-build-path guard enabled for every candidate. Never patch strings
in compiled binaries, exempt a whole assembly or delete caches to mask the defect.

Permanent real-compiler coverage builds the actual Core project in two independent
temporary source roots, inspects PE CodeView records/byte content, compares assembly
hashes and exercises normal-to-CI/no-symbol incremental reuse. Run it with packaging
tests using `python -B -m unittest discover -s build -p 'test_*release*.py' -v`.
This uses the installed repository SDK; it copies only Core sources/build metadata,
without fixtures, Git mutations or modifications to repository bin/obj outputs.

Desktop needs an additional Release-only setting: DebugType=none and
DebugSymbols=false in its project, matching the portable command line. Fresh
pre/post-XAML captures prove Roslyn maps the CodeView filename but Avalonia's
symbol-writing postprocessor recreates a physical PDB reference. Disabling Release
symbols at compilation prevents that rewrite from emitting CodeView; source mapping
alone does not control this later writer. Debug builds retain symbols. The Desktop
regression checks real PE entries before/after XAML compilation in a fresh temporary
checkout and after warm CI/no-symbol reuse. The guard remains unchanged and enabled.
See the acceptance record for the evaluated properties, compiler evidence and gate.

The builder assembles new files only under repository artifacts. Existing output
directories/archives are refused. It makes no recursive deletions and never mutates
the user's installation/settings. No package is uploaded, installed or registered.

## Contents and repeatability

Include: main apphost/assemblies/native runtime, separate ScriptHost and its runtime,
curated README.md, USER-GUIDE.md and RELEASE-NOTES.md, full dependency/license inventory,
ThirdPartyNotices/, BUILD-PROVENANCE.json and SHA256SUMS.txt. The optional runtime
createdump utility and all PDBs are deliberately omitted. Do not include source,
fixtures/HL7/CSV data, development files, credentials, local settings, internal logs,
test infrastructure, arbitrary JSON or links/reparse-point inputs. Legal notices are
the deliberate exception to filename-extension filtering; preserve their contents.

Provenance records product/version/RID, SDK, pinned runtime, source commit, dirty state,
assembly/entry-point identity and hashes of production/build/lock/curated-doc inputs.
A dirty candidate is review evidence, not a releasable source checkpoint. File and
archive checksums detect accidental changes; they are not signatures/authenticity proof.
ZIP entry order, attributes, timestamps and compression settings are fixed. Repeating
assembly from the same staged bytes/toolchain produces identical ZIP bytes; rebuilding
with a different SDK/zlib/runtime is outside that promise. For example:

```powershell
python -B build/portable_release.py --verify artifacts/stage14/my-local-candidate/CANDIDATE/MessageWorkbench
Get-FileHash artifacts/stage14/my-local-candidate/CANDIDATE/MessageWorkbench-v0.1.0-beta-win-x64.zip -Algorithm SHA256
./build/VerifyScriptPublish.ps1 -PublishDirectory artifacts/stage14/my-local-candidate/CANDIDATE/MessageWorkbench -SelfContained
python -B -m unittest discover -s build -p test_portable_release.py -v
```

The SelfContained verifier runs both production worker lifecycles with an unusable
DOTNET_ROOT and PATH limited to Windows System32. It proves the worker needs no
installed dotnet host, not that the full UI has passed clean-machine native testing.
It does not bypass production resource-containment tests, which remain permanent.

## License/attribution review

The two actual win-x64 published deps manifests declare **35 package/runtime entries**:
34 MIT primary declarations plus ANGLE's file license with BSD-style conditions.
The [per-package review](portable-dependencies.md) records versions, obligations and
notice locations. Build/test-only xUnit, headless, telemetry build services/analyzers
and non-Windows native runtime assets are not copied into this candidate. Some
cross-platform managed dependencies remain in the untrimmed Desktop graph; this
does not enable automatic cross-platform services/network activity.

Package nuspecs and bundled license/notice files were inspected; pinned root license
and notice texts from eight upstream repositories are retained in build/third-party.
Source/stored SHA-256 and exact source commit URLs are recorded in upstream-licenses.json.
Only LF/trailing-line whitespace is normalized; all legal text/copyrights remain.
Original fetched bytes remain in ignored verification evidence. All package-bundled
notices, including native and RTF notices, are copied without changing their bytes.
Unknown primary licenses, absent notice text or changed pinned text fail assembly.

Retain MIT copyright/permission/disclaimer texts; ANGLE's BSD-style terms also require
retaining its conditions/disclaimer and avoiding implied endorsement. Native/.NET/
Roslyn/Toolkit notice bundles contain additional component terms, including conditional
Apache/BSD/font/LGPL notices describing broader upstream distributions. Their presence
does not establish that every described component ships here. Human review of scope,
notice completeness and any applicable source/relinking obligations remains required;
the scanner cannot provide legal clearance. Do not delete notices to silence questions.

The owner confirmed ownership or redistribution permission for the approved MW artwork
in the Stage 14 conversation (2026-10-22). No new visual identity or public-source
artwork is introduced. The application's own beta usage terms still require owner
approval; no open-source license or coworker distribution grant is invented here.

## Clean-machine native smoke and proposed later distribution

Use an isolated standard-user Windows 10/11 x64 environment with no SDK or installed
.NET runtime. Verify the archive checksum, extract all files and retain every notice.
Check both a fresh per-user profile and a backed-up existing configuration. Run
MessageWorkbench.exe; verify branding/version/icon, preferences and dock restoration.
Exercise synthetic HL7 open/edit/Undo/Redo/Save As, long-file cancellation, search/
extraction/trusted scripting, FHIR generation/report association and authorized
loopback single/batch/load MLLP. Test a read-only extraction directory with writable
per-user storage. Check no new files appear under extraction; use the verifier to
reconcile hashes. Runtime-created per-user files are expected outside the app folder.

Check startup offline, no automatic listen/connect/update/upload, normal shutdown,
forced termination followed by restart, corrupt metadata and unavailable/readonly
file destinations, screen reader/keyboard/focus, themes, DPI/multiple monitors. No
clinical contents should auto-recover or enter diagnostics. Inspect OS/runtime error
dialogs/Windows Event logs for external crash-collection policy; no PHI in feedback.
See the [complete acceptance record](stage14-release-readiness-acceptance.md) and
[candidate user guide](beta-user-guide.md) for privacy boundaries/troubleshooting.

The earlier authorization permitted the completed checkpoint and clean-source build;
the correction's current candidate is explicitly dirty review evidence, not a release.
After exact-artifact/native and human rights/license/terms review, obtain separate
distribution authorization, reconcile provenance and checksums, consider signing/OS trust requirements,
approve limited recipients and a synthetic-only feedback procedure, and share through
an approved channel. Never repurpose a dirty local candidate as the approved beta.
See [distribution review](beta-distribution-review.md) for resolved questions,
remaining blockers and the later approved-transfer procedure; [proposed beta terms](beta-usage-terms-proposed.md)
are a draft for human approval, not a distribution license.

Technical sources: Microsoft's [self-contained deployment documentation](https://learn.microsoft.com/en-us/dotnet/core/deploying/),
[NuGet lock-file documentation](https://learn.microsoft.com/en-us/nuget/consume-packages/package-references-in-project-files#locking-dependencies)
and [.NET license information](https://github.com/dotnet/core/blob/main/license-information.md).
Actual package declarations and shipped notices take precedence over summary labels.
