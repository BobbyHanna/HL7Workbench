# Install or run Message Workbench

Use Windows as the supported acceptance platform. Choose either an approved
portable artifact supplied by its owner or a local source build. These are
different version baselines; this documentation does not offer a new public download.
Current-source portable preparation is verification-only; no new recipient artifact
is approved. When an approved current build is supplied, follow the bundled generated-data
walkthrough without repository fixtures and record Version/Build from About.

## Use an approved portable preview

Prerequisite: obtain the exact owner-approved Windows x64 preview and its checksum
through an approved channel. The existing private preview predates SQL, Stage 16–18
and these guides. Do not use its version label to infer current-source features.

1. Verify the supplied archive's SHA-256 against the accompanying trusted value
   with PowerShell `Get-FileHash`.
2. Extract the entire ZIP to a local folder on Windows 10/11 x64. Do not run inside
   the archive or copy only the executable.
3. Keep all supplied runtime/worker folders and notices together. Launch
   **MessageWorkbench.exe** from the extracted folder.
4. Open **Help → About Message Workbench…** to inspect the public version; consult
   that artifact's provenance and recipient guide for its exact feature baseline.

The accepted preview is self-contained: no SDK, separately installed runtime or
administrator installation is required. Its [recipient guide](../beta-user-guide.md)
and [release procedure](../portable-release.md) describe that specific artifact.
Moving its application folder does not remove per-user settings/scripts.

## Run the current source tree

Prerequisites: the repository, a compatible .NET 10 SDK (floor 10.0.401,
`latestFeature` roll-forward), PowerShell and initial NuGet access. An editor such
as VS Code is optional. No extra .NET workload or paid Avalonia tooling is required.

1. Open PowerShell in the repository root.
2. Follow the exact [restore/build/run commands](../../README.md#build-and-run-from-source).
3. Let the build deploy both **ScriptHost/** and **QueryHost/** alongside Desktop
   output. Do not manually copy a subset of the build output.
4. Launch with the documented `dotnet run` command, then use **File → Open File…**.

Expected result: the local desktop workspace opens. Build/restore can need network
access; ordinary file analysis is local. TCP/MLLP intentionally communicates after
review, and trusted C# scripts can use your account's file/network permissions.

## If startup fails

Retain the complete output and record version/status codes with synthetic reproduction
steps. Check SDK selection with `dotnet --info`, file access and worker-folder
completeness. Do not download loose DLLs or disable security software. An invalid
layout can fall back safely; [workspace recovery](workspace.md#recover-the-workspace)
does not restore unsaved message contents. See [troubleshooting](troubleshooting.md).

No new installer, signing, updater, package or public distribution is approved by
this source documentation. Preserve originals and [back up valuable saved work](privacy-and-storage.md).
