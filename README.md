# HL7 Workbench

A local-first C#/.NET 10 desktop workbench for HL7 v2 development.
Milestone 1 provides an in-memory HL7 v2 parser and editing API alongside the
Milestone 2 docking foundation. Desktop opens local files in read-only AvaloniaEdit
tabs with delimiter-aware HL7 highlighting, Message Browser, and Diagnostics.
Stage 1 passed manual acceptance on 2026-10-14; segment navigation, field inspection, editing,
generation, scripting, and transport are not implemented.

## Windows setup

Install the .NET 10 SDK (10.0.401 or a newer compatible .NET 10 feature band),
VS Code, and Microsoft's C# extension. No additional .NET workloads or paid
Avalonia tooling are required. Open this folder in VS Code.

In PowerShell, before running .NET commands:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln --configuration Release --no-restore
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
dotnet run --project src/HL7Workbench.Desktop
```

The final command opens the viewer workspace; use File → Open File to select local files.
Dependency restore needs
NuGet access; the application itself has no network functionality. Avalonia's
transitive build telemetry assets are excluded, with the environment opt-out
also documented here and configured in CI.

## Projects

- `HL7Workbench.Core`: UI-independent parser, message model, paths, editing, escapes,
  diagnostics, and lossless serialization. No file I/O or NuGet dependencies.
- `HL7Workbench.Workspace`: UI-independent documents/active context, tool registry,
  bounded layout metadata, and screen recovery policy.
- `HL7Workbench.Desktop`: Avalonia/Dock shell; Dock wrappers are isolated in Docking.
- `HL7Workbench.Core.Tests`: synthetic parser/editing/encoding/limit tests and the
  foundation smoke test for UI independence.
- `HL7Workbench.Workspace.Tests` and `HL7Workbench.Desktop.Tests`: workspace and Dock
  model integration checks; native gestures and monitor/DPI behavior need manual review.

Versions are centralized in `Directory.Packages.props`; dependency lock files
record the resolved graph. To intentionally update dependencies, change the
central versions, run `dotnet restore --force-evaluate`, review lock-file changes,
and build/test before accepting the update. Windows CI restores in locked mode
and builds/tests Release. Tests use the Microsoft Testing Platform runner selected
in `global.json`, supplied transitively by xUnit. CI will execute after this work is pushed.

Only reviewed synthetic fixtures belong in `samples/synthetic-only/`.
Never commit real patient data, secrets, or proprietary HL7Spy material.
See [architecture](docs/architecture.md), the [foundation decision](docs/decisions/0001-foundation.md),
and [dependency notes](docs/dependencies.md).

See [parser API](docs/parser-api.md) and [path grammar](docs/hl7-paths.md) for usage,
preservation guarantees, configurable limits, and unsupported cases.
Milestone 2 is read-only. The docking foundation is accepted; raw viewer Stage 1
passed manual acceptance on 2026-10-14. Stage 2 requires separate authorization.
See [Stage 1 acceptance](docs/viewer-stage1-acceptance.md),
[docking acceptance](docs/docking-acceptance.md), and
[project context](docs/PROJECT_CONTEXT.md). No message editing or save commands exist.
