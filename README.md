# HL7 Workbench

A local-first C#/.NET 10 desktop workbench for HL7 v2 development.
Milestone 0 provides an application shell and build/test foundation only.
Message parsing, editing, generation, scripting, and transport are not implemented.

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

The final command opens a static HL7 Workbench window. Dependency restore needs
NuGet access; the application itself has no network functionality. Avalonia's
transitive build telemetry assets are excluded, with the environment opt-out
also documented here and configured in CI.

## Projects

- `HL7Workbench.Core`: empty, UI-independent library reserved for the HL7 model.
- `HL7Workbench.Desktop`: Avalonia application shell referencing Core.
- `HL7Workbench.Core.Tests`: xUnit smoke test for Core loading and UI independence.

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

Milestone 1 requires separate approval; its proposed design and tests are recorded
in the architecture document.
