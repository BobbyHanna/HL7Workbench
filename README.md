# HL7 Workbench

HL7 Workbench is an independent, local-first HL7 v2 desktop viewer and editor
built with C#/.NET 10 and Avalonia. Windows is the initial desktop platform.
Viewer/editor Stages 1–4 are complete and have passed native Windows acceptance.

## Implemented capabilities

- **Local files and multi-message navigation:** Open files in document tabs and
  navigate concatenated or MLLP-framed messages with First, Previous, Next, Last,
  and direct message-number controls.
- **Structural inspection:** Delimiter-aware syntax highlighting, Segment
  Navigator, and Field Inspector expose fields, repetitions, components, and
  subcomponents. Raw-editor hover shows structural paths; double-click reveals
  the corresponding structure.
- **Source-backed large-message handling:** Immutable file snapshots, bounded
  previews, and virtualized display blocks support inspection without loading
  entire large payloads into editor strings. The optional Full Source Viewer
  provides contiguous source windows and exact original-byte export. Attachments
  are not decoded or rendered.
- **Dockable workspace:** Resize, move, float, hide, and reopen tools. Saved layouts
  retain placement and geometry without storing message contents.
- **Activity Log:** Review application-session operations and structured
  diagnostics, with severity filtering and status counts. Log entries exclude
  message contents, patient identifiers, filenames, paths, and exception text.
- **Per-document Edit Mode:** Documents start read-only by default. Enable editing
  for a supported message without changing other documents. A persistent
  Settings → Start in Edit Mode preference applies only to newly opened documents.
- **Undo/redo and dirty tracking:** Document-owned edit history and modified-tab
  indicators survive message navigation and view reconstruction. Turning Edit
  Mode off retains unsaved work. Inspection stays visible while edits are parsed;
  previous inspection data is labeled and stale structural actions are paused.
- **Save and Save As:** Publish edits from a temporary file while preserving
  untouched original bytes, encoding/BOM, framing, and unedited line endings.
  Save As sets the destination for later saves. Dirty tab and application closure
  offer Save, Discard, or Cancel; canceled or failed saves retain working edits.

### Editing scope and limits

Raw editing is bounded to complete messages within the existing editor limits:
1,048,576 UTF-16 units per message and 16,384 per line, with bounded document
history. Large/source-backed messages remain read-only; supported smaller messages
in the same file can still be edited. Temporarily malformed drafts are retained;
inspection and saving enforce the existing structural and encoding limits.

Saving preserves unchanged file regions and refuses unsupported message-boundary
or transport-framing restructuring. Original-source inspection/export for an edited
message requires reopening the saved file. See the
[editable-document decision](docs/decisions/0004-editable-document-foundation.md)
for supported encodings, budgets, and save guarantees. Structural inspection and
basic diagnostics do not provide HL7 version/profile conformance validation.

## Planned capabilities

Scripting, SQL-style querying, and TCP/MLLP sending, receiving, and transport testing
are planned and are not implemented. File-based MLLP framing support is available
for inspection and preservation; it does not provide network transport. Synthetic
message generation and additional analysis tools also remain future work.

## Windows setup

Install the .NET 10 SDK (10.0.401 or a newer compatible .NET 10 feature band),
VS Code, and Microsoft's C# extension. No additional .NET workloads or paid
Avalonia tooling are required. Open this folder in VS Code.
[global.json](global.json) specifies the SDK floor and feature-band roll-forward.

Run from the repository root in PowerShell:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln --configuration Release --no-restore
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
dotnet run --project src/HL7Workbench.Desktop --configuration Release --no-build --no-restore
```

The final command opens the workspace. Use File → Open File to select local files,
then enable Edit Mode in the raw pane when needed. File → Save / Save As and
Ctrl+S / Ctrl+Shift+S publish edits; Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z use document history.

Dependency restore needs NuGet access. The application has no runtime network
features, cloud integration, or telemetry. Avalonia's transitive build telemetry
assets are excluded; the environment opt-outs above are also configured in CI.

## Projects and dependencies

- `HL7Workbench.Core`: UI-independent parser, message model, paths, controlled
  edits, escapes, diagnostics, and lossless serialization. No file I/O or NuGet
  dependencies.
- `HL7Workbench.Workspace`: UI-independent file snapshots/indexing, document and
  inspection state, edit history, saving, preferences, tool registry, and bounded
  layout metadata. No Avalonia or Dock dependencies.
- `HL7Workbench.Desktop`: Avalonia/AvaloniaEdit UI and Dock shell, file pickers,
  editing commands, and close prompts. Dock adapters remain isolated from Core
  and Workspace.
- The corresponding Core, Workspace, and Desktop test projects cover synthetic
  parser, preservation, resource-limit, lifecycle, editor-input, and Dock regressions.

Versions are centralized in [Directory.Packages.props](Directory.Packages.props);
committed package lock files record the resolved graph. To intentionally update
dependencies, edit the central versions, then run:

```powershell
dotnet restore HL7Workbench.sln --force-evaluate
dotnet build HL7Workbench.sln --configuration Release --no-restore
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
```

Review all version and lock-file changes before accepting an update.
[Windows CI](.github/workflows/build.yml) restores in locked mode and builds/tests
Release. Tests use the Microsoft Testing Platform runner selected in `global.json`,
supplied transitively by xUnit. See the [dependency inventory](docs/dependencies.md).

## Testing

The setup commands run the full automated suite. For the additional Workspace
stress cases, use the same telemetry opt-outs and completed Release build:

```powershell
$env:HL7WORKBENCH_STRESS = '1'
dotnet test tests/HL7Workbench.Workspace.Tests/HL7Workbench.Workspace.Tests.csproj --configuration Release --no-build --no-restore
Remove-Item Env:HL7WORKBENCH_STRESS
git diff --check
```

Stress mode includes synthetic 256/512 MiB source-backed cases. Headless tests
exercise real AvaloniaEdit input and Dock integration; native clipboard/IME,
window-manager, monitor, and DPI behavior also require manual regression checks.
The [Stage 4 acceptance report](docs/stage4-editable-document-acceptance.md) records
the accepted milestone's automated results and native Windows checklist.

## Documentation and repository data

- [Architecture](docs/architecture.md) and [project context](docs/PROJECT_CONTEXT.md)
- [Parser API](docs/parser-api.md) and [path grammar](docs/hl7-paths.md)
- [Stage 1 acceptance](docs/viewer-stage1-acceptance.md) and
  [Stage 2 acceptance](docs/viewer-stage2-acceptance.md)
- [Stage 3 navigation and source inspection](docs/viewer-stage3-acceptance.md),
  [source-viewer UX](docs/viewer-stage3-ux-acceptance.md), and
  [large-file measurements](docs/stage3-performance.md)
- [Activity Log](docs/stage3-activity-log-acceptance.md) and
  [workspace layout](docs/stage3-workspace-layout-acceptance.md)
- [Stage 4 editing and saving](docs/stage4-editable-document-acceptance.md) and
  [inspection continuity](docs/stage4-inspection-continuity.md)
- [Foundation decision](docs/decisions/0001-foundation.md) and
  [docking acceptance](docs/docking-acceptance.md)

Only reviewed synthetic fixtures belong in [samples/synthetic-only](samples/synthetic-only/).
Never commit real patient data (PHI), secrets, or third-party proprietary materials.
