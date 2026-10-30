# Message Workbench

Message Workbench is an independent, local-first Windows desktop development tool
for HL7 v2 inspection, bounded editing, analysis, trusted C# scripting, synthetic
generation, local FHIR R4 import/partial conversion and TCP/MLLP testing.
It uses .NET 10, Avalonia and Dock. `HL7Workbench` remains the internal assembly,
namespace and per-user settings identity.

Start with [the user guide](docs/user/index.md),
[the first synthetic-file walkthrough](docs/user/getting-started.md) or
[the documentation index](docs/index.md). Existing
[developer guides](docs/developer/index.md) cover architecture, build/test,
parser invariants, lifecycles, scripting, SQL and validation. Shared
[limits/storage references](docs/reference/index.md) link their source definitions;
**Help → Documentation** opens the curated user/shared guides locally without internet
or repository access. Script API links reuse the existing reference tool.

## Implemented capabilities

- View combined HL7 files, navigate messages, inspect fields/segments and original
  source bytes; edit supported complete messages with undo/redo and safe Save/Save As.
- Find/filter messages, compare pasted/current/original messages, extract tables/CSV,
  and run local read-only SQLite over current or selected open HL7 documents.
- Manage trusted C# scripts and run them against Current message or Whole document,
  with independent outputs, reports and searchable offline Script API help.
- Generate standalone or connected synthetic ADT/ORM/ORU scenarios with realistic
  or placeholder styles, automatic seeds and explicit reproducible replay.
- Explicitly apply scoped v2.5.1 schemas/custom profiles from
  **HL7 Tools → Schema Validation**, with inline findings and Incomplete coverage.
- Import supported local FHIR R4 Bundles, browse patients/history and generate a
  partial versioned HL7 mapping into independent documents.
- Test single TCP/MLLP exchanges, receive test traffic, or authorize bounded
  sequential batch and controlled load runs. Arrange docked/floating tools and
  retain placement/preferences separately from content.

See [task selection](docs/user/index.md#choose-an-analysis-workflow) and
[supported limits](docs/user/troubleshooting.md#limits-and-coverage).
No TLS, automatic retry/reconnect, scheduling, remote FHIR integration, external
SQL database connectivity or universal conformance is advertised.

## Privacy and trust

There is no automatic cloud upload, account requirement or runtime telemetry.
TCP/MLLP tools intentionally communicate through explicit review workflows.
C# scripts execute with the user's account permissions and can access files or
networks; worker containment is **not an operating-system security sandbox**.
Socket write completion does not establish application acceptance.

Temporary snapshots/journals and explicitly saved definitions/exports can contain
sensitive plaintext. De-identification aids require review and do not guarantee
anonymization or compliance. See [privacy and storage](docs/user/privacy-and-storage.md).
Use only reviewed synthetic data in repository examples/tests and feedback.

## Release status

These docs describe the current source application. The previously accepted
Windows x64 v0.1.0-beta private preview is a separate, older binary/provenance
baseline. It does not include every later source feature. No new current-source
package, public release or distribution approval is implied. Follow
[installation/version scope](docs/user/installation.md) and the exact approved
artifact's [recipient guide](docs/beta-user-guide.md).

## Build and run from source

Install the .NET 10 SDK: floor 10.0.401 with `latestFeature` roll-forward in
[global.json](global.json). Initial dependency restore needs NuGet access.
From the repository root in PowerShell:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln --configuration Release --no-restore
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
dotnet run --project src/HL7Workbench.Desktop --configuration Release --no-build --no-restore
```

Keep deployed ScriptHost/ and QueryHost/ output intact. Tests use Microsoft Testing
Platform via xUnit, as selected in global.json. [Windows CI](.github/workflows/build.yml)
uses locked restore and Release build/test. Native clipboard/IME, monitor/DPI and
window-manager checks supplement headless tests. Extra large Workspace cases use
`HL7WORKBENCH_STRESS=1`; run opt-ins deliberately rather than treating them as
ordinary CI failures/skips. See [repository guidance](AGENTS.md) and
[current test/lifecycle context](docs/PROJECT_CONTEXT.md).

## Projects and dependencies

| Production project | Responsibility |
| --- | --- |
| Core | UI/file/network-independent HL7 parser/scanner, edits/serializer, generation, FHIR mapping and profile validation. |
| Workspace | UI-independent snapshots, documents/history, captures/publication, tools' sessions and local definitions/preferences/layout DTOs. |
| Desktop | Avalonia/AvaloniaEdit shell, native controls/pickers and Dock integration. |
| Scripting | Supported script facade/contracts and deterministic helper policies; no UI. |
| ScriptHost | Separate trusted-C# execution process and worker protocol. |
| Querying | UI/SQLite-independent query contracts and worker client. |
| QueryHost | Separate read-only SQL process; owns native SQLite and enforcement. |
| Transport | UI-independent incremental MLLP framing, TCP/listener and exchange lifecycles. |

Five test projects cover Core, Workspace, Desktop, Scripting and Querying.
Package versions are centralized in [Directory.Packages.props](Directory.Packages.props)
with committed lock files. See [dependency inventory](docs/dependencies.md),
[architecture/ADRs](docs/index.md#developer-references) and
[validation metadata attribution](src/HL7Workbench.Core/ValidationCatalog/NOTICE.md).
Keep Core free of UI and Dock, Workspace independent of Avalonia/Dock, and Dock
adapters in Desktop. Do not change locks or dependencies without deliberate review.

## Documentation and repository data

[Documentation home](docs/index.md) separates current tasks from developer references
and dated milestone evidence. The [initial specification](HL7_Workbench_Specification.md)
and [starter prompt](Codex_Starter_Prompt.md) are historical planning, not current
feature scope or security guarantees. Historical reports and accepted artifacts
retain their established paths.

[Reviewed synthetic fixtures](samples/synthetic-only/README.md) support walkthroughs
and regression tests. Never commit PHI, secrets or proprietary third-party materials.
