# ADR 0019: Local Windows x64 portable beta candidates

Status: Stage 14 implemented; complete automated gate passed. The user reports portable
application/worker success in a separate work VM and authorizes one local Stage 14
checkpoint plus a fresh verified clean-source candidate. Branding baseline is
84fdc22925173250fad132d0d4b4c6768d3c68e0. Distribution remains unapproved; human review
and exact rebuilt artifact/native acceptance details remain open.

Stage 14 checkpoint is b14b100e399a4eb44a05c98b4844f552f080eb68. Its clean-source
candidate was correctly rejected for absolute CodeView PDB paths in reused Release
DLLs. The follow-up correction is authorized for implementation/verification only,
unstaged/uncommitted. Release path mapping is now established in Directory.Build.props
before the incremental compiler-input cache; normal and portable builds share stable
virtual source/PDB paths. CI-only late mapping, PDB omission and deterministic=true
alone did not cover the warm project-reference reuse sequence. Runtime behavior,
binding identities, worker containment/deployment and privacy guards are unchanged.
The first attempt stopped on Desktop. A fresh isolated build then proved Csc's
mapped reference is overwritten by Avalonia's XAML/Cecil symbol writer, not stale
output. Desktop Release therefore uses DebugType=none / DebugSymbols=false, matching
portable settings; Debug is unchanged. Fresh and warm PE regressions pass without
a CodeView entry. All corrections remain unstaged/uncommitted; see acceptance for
the completed 1,961 solution / 935 stress / 12 packaging-compiler gate and identical
independent portable builds. Candidate provenance records the base commit plus dirty
working inputs; no release/distribution is authorized. No binary rewriting or guard exception.

Use a directory-based self-contained .NET 10 Windows x64 deployment, without trimming,
single-file extraction, ReadyToRun, installer, updater, service or elevated runtime.
Pin runtime 10.0.12 and retain separate packages.win-x64.lock.json files for all six
production projects; the ordinary solution locks/gate remain independent. Build and
runtime telemetry opt-outs and existing architecture/worker boundaries remain.

Only the candidate's native apphost copy is named MessageWorkbench.exe. Its embedded
HL7Workbench.Desktop.dll binding, assemblies, namespaces, resource URIs and per-user
data paths do not change. Public metadata is 0.1.0-beta / file 0.1.0.0; binding
AssemblyVersion remains 1.0.0.0. This avoids a launcher or technical rename. Settings,
profiles/scripts/layouts remain in the existing user directories, not beside binaries.

The candidate builder publishes Desktop and ScriptHost separately. The worker project
reference is nonprivate; explicit build/deployment copying owns the boundary, avoiding
SDK-propagated worker apphost/runtimeconfig files at the Desktop root. Each published
process retains its own runtime. Default DLL worker selection prefers only an adjacent
native apphost; DLL-only development builds retain their dotnet fallback. Named-pipe
privacy, initialization protocol, deadlines, memory/job/process-tree containment and
transactional publication do not change. This is resource containment, not a security
sandbox for untrusted C#.

Assembly is explicit/allowlisted and fail-closed: no PDBs, optional createdump utility,
source, fixtures, clinical data, local configuration, developer diagnostics, symlinks
or arbitrary files. Recipient README, user guide/notes, candidate marker, package inventory/full
notices, build-input hashes and SHA-256 file/archive checksums accompany the build.
Every package/runtime entry from both published deps manifests is reviewed; unknown
primary licenses, missing notices or changed pinned notice text block assembly.
Upstream notices retain every word/copyright, with LF/trailing-whitespace formatting
only; raw source and stored hashes are recorded. Package-bundled notices retain bytes.

ZIP entries have fixed order/timestamps/attributes and fixed compression settings.
Identical staged bytes yield identical ZIP bytes with the same Python/zlib toolchain;
this is not a promise of identical compiler output across SDKs, source paths or future
runtime servicing. Provenance records SDK, runtime, source commit, dirty state and
exact build-input hashes. Existing output directories/archives are never replaced.

Concrete stability fixes are bounded to corrupt-layout exception recovery, unique
owned layout temporary metadata/cleanup and fixed save-conflict wording. No new
clinical feature, PHI recovery, content logging, protocol policy or persistence schema.
Native geometry/accessibility/DPI and clean-machine/read-only-folder execution require
user acceptance. Build success and clean strings do not establish legal clearance.

The owner confirmed artwork ownership/redistribution permission. Full notice bundles
can describe components outside this artifact; their applicability/completeness and
application beta usage terms require human review. All candidates are explicitly local
and not for distribution. Packaging/testing authorization does not authorize release.
See [portable procedure](../portable-release.md) and [acceptance](../stage14-release-readiness-acceptance.md).
