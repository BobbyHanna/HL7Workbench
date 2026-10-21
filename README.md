# HL7 Workbench

HL7 Workbench is an independent, local-first HL7 v2 desktop viewer and editor
built with C#/.NET 10 and Avalonia. Windows is the initial desktop platform.
Viewer/editor Stages 1–4 are complete and have passed native Windows acceptance.

## Implemented capabilities

Stage 8D synthetic generation passed user native Windows acceptance on 2026-10-20.
Earlier pending-acceptance notes below are retained as historical implementation records.
The approved M/W application icon is integrated; see [branding](docs/branding.md)
for the unchanged source, reproducible derivatives and native Windows checks.
Patient Deidentifier now substitutes realistic fictional demographics from a bundled
deterministic identity library. Each transformed output exposes its own expanded
**Transformation Report — Review required**; narrow Identifier/Timestamp presets
keep their existing purposes. See [Stage 9 acceptance](docs/stage9-deidentification-acceptance.md)
for coverage, privacy limits and native retest steps. These corrections passed user
native Windows acceptance on 2026-10-20; earlier pending notes are historical.

- **Local files and multi-message navigation:** Open files in document tabs and
  navigate concatenated or MLLP-framed messages with First, Previous, Next, Last,
  and direct message-number controls.
- **Structural inspection:** Delimiter-aware syntax highlighting, Segment
  Navigator, and Field Inspector expose fields, repetitions, components, and
  subcomponents. Raw-editor hover shows structural paths; double-click reveals
  the corresponding structure. Field Inspector → Copy Path copies a search-compatible
  segment/field/component/subcomponent coordinate without its value.
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
  Settings → Preferences → General edit-mode preference applies only to newly opened documents.
- **Undo/redo and dirty tracking:** Document-owned edit history and modified-tab
  indicators survive message navigation and view reconstruction. Turning Edit
  Mode off retains unsaved work. Inspection stays visible while edits are parsed;
  previous inspection data is labeled and stale structural actions are paused.
- **Save and Save As:** Publish edits from a temporary file while preserving
  untouched original bytes, encoding/BOM, framing, and unedited line endings.
  Save As sets the destination for later saves. Dirty tab and application closure
  offer Save, Discard, or Cancel; canceled or failed saves retain working edits.

### Editing scope and limits

**Generate Messages** in Home or File opens the local synthetic HL7 v2.5.1 generator.
Choose ADT^A01 (admission), ADT^A04 (registration), ADT^A08 (update), ORM^O01 (order)
or ORU^R01 (results). Defaults create one new unsaved ADT^A01 with seed 42,
timestamp `2030-01-01T00:00:00+00:00` and prefix `SYN`. An empty seed also means 42.
Use a timestamp with seconds and an explicit offset (or Z); it normalizes to UTC.
Prefixes normalize to uppercase ASCII letters/digits/hyphens/underscores, 1–12 characters.
The preview shows configuration and the expected control-ID range without generating
message bodies. Data is conspicuously fictional, generated entirely locally, and
never derived from the active patient or an external dataset.

Choose a new independent unsaved tab, or the document captured when the dialog opened
and Before Current/After Current/Beginning/End. Generation enables that document's
Edit Mode and selects/focuses the first generated message. One Ctrl+Z removes the
whole batch; one Ctrl+Y restores the same identities/order, also with Edit Mode off.
Save As uses the normal workflow; generation never saves automatically.

Requests allow 1–1,000 messages and at most 4 Mi UTF-16 units, within existing editor,
document and history budgets. Generation/validation run in the background; Cancel
or closing the busy dialog cancels before publication. Existing-document insertion
first proves the prospective save in private temporary storage. This needs temporary
disk space for the whole prospective file and can take time on large sources. Unsafe
joins, ambiguous/batch containers, incomplete drafts or exhausted budgets reject the
whole request. Complete/undo the draft, choose a safe position, or use a new document.
No source bytes are repaired or rewritten.

Identical normalized options produce identical canonical UTF-8/CR bytes within
generator version 1; future versions may change output. Control IDs repeat for repeated
configurations and guarantee uniqueness within each batch. These are useful synthetic
examples, without a clinical or implementation-guide conformance claim. Stage 8D
awaits native Windows acceptance; see the [acceptance record](docs/stage8d-synthetic-generator-acceptance.md).

Stage 8C adds **Add Message** in the Home ribbon, File menu and existing message
navigator. Choose Before Current, After Current, Beginning or End; positions always
refer to the current logical sequence. A successful insertion selects a blank draft,
enables Edit Mode for that document and focuses its HL7 editor. The saved Start in
Edit Mode preference stays unchanged. No content is generated or saved automatically.

**Next Message (`>`) at unfiltered document EOF** appends and selects a blank draft.
Repeated Next reuses a trailing inserted draft containing only whitespace/segment
terminators. A non-whitespace partial draft permits another append, but saving still
requires safe HL7 boundaries. Filtered Next stays within matching results and never
appends, even at the last logical message; clear the filter to use EOF append.
Explicit Add Message invalidates the filter/results while retaining the authored query.

Paste into the HL7 editor retains clipboard delimiters, tabs and mixed line endings.
Insertions and edits share document-wide Ctrl+Z/Ctrl+Y history, including with Edit
Mode off. A blank/incomplete inserted draft blocks safe Save and identifies its
logical message number; complete it or undo insertion. Original bytes and the
destination remain unchanged after validation failure. Documents support up to
100,000 logical messages, within the existing draft/history budgets. See the
[Stage 8C acceptance record](docs/stage8c-message-insertion-acceptance.md) for limits,
verification and native Windows retest steps. Stage 8C has passed user native Windows acceptance.

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

## C# scripting (Stages 5A–5B)

**Help → Script API Reference** and the Script Editor's **API Reference** button open
searchable offline API help, copyable complete examples and six editable starters.
Browse [the generated reference](docs/script-api-reference.md) or see
[Stage 9C acceptance](docs/stage9c-script-developer-experience-acceptance.md).
Built-ins are read-only; Duplicate Script creates an independent My Scripts copy to
rename, edit, save, reopen and run. Application updates do not replace those copies.

The Script Library includes **Patient Deidentifier**, **Identifier Pseudonymizer** and
**Timestamp Shifter**. Review a preset, duplicate it to configure seed/day displacement,
and Run against Current or Whole document. Output opens independently and unsaved with
a Review Required filename and bounded count-only CSV report. Sources are retained.
These workflows assist de-identification; they do not certify anonymity, PHI removal,
compliance or unrestricted sharing. Narrow presets and unassessed container/nested
content can retain identifying information. See the [coverage and native checklist](docs/stage9-deidentification-acceptance.md).

Use New Script in the dockable/floating Script Editor and write `void Main()`.
The same source runs against Apply to: Current message or Whole document. A fresh
Roslyn worker initializes shared variables once, calls Main in source order and
calls optional `void Complete()` once on success. Helpers and shared state work
across messages and reset each Run. The worker executes trusted code with the
user's permissions; it is not a security sandbox.

The Script Library preserves names, dirty drafts and immutable duplicable examples.
Old development records retain their source but show Needs update and require an
explicit rewrite/save before Run. `Batch` supplies advanced reports and deferred
output without a separate script type. The consistent synthetic-patient example
maintains identities within a run and reports counts; it is not complete clinical
de-identification. Successful runs open independent unsaved HL7 output, preserving
source documents and unsaved input edits. CSV exports permit explicitly confirmed safe replacement,
and retain the producing script name/scope in the editor even after switching scripts.
Failures and cancellation publish no partial output or reports.

See the [script API](docs/script-api.md), [unified scripting decision](docs/decisions/0007-unified-csharp-scripting.md)
and [native Windows acceptance checklist](docs/stage5b-batch-scripting-acceptance.md).
Stage 5B, including the unified redesign and final save/dialog corrections, passed
native Windows acceptance on 2026-10-16.

The permanent menu is File | Settings | View | Help. The collapsible Scripts ribbon and compact
editor share commands; Home contains Save/Restore/Reset Layout, including window recovery.
Click the script name to rename; Delete Script protects bundled examples and prompts for
dirty drafts/confirmation. Reports / Export CSV opens contextual retained results.
Settings → Preferences controls new-document Edit Mode, saved ribbon visibility and
Appearance: Follow System (default), Light or Dark, applied without restarting. HL7 editors
and source views follow the effective theme while retaining drafts, undo and selection.
HL7 schema controls are reserved/disabled until an interpretation engine exists; TCP/MLLP
Sender/Listener are disabled Planned placeholders. See [desktop preferences](docs/desktop-preferences.md)
and the [UI finishing checklist](docs/stage5b-ui-finishing-acceptance.md).
See [save/export safety](docs/save-and-export-safety.md) for picker authorization,
destination-version checks, transactional replacement and the final native retest.

## Message search and filtering (Stages 6A–6B)

Use Ctrl+F, View → Find Messages, or HL7 Tools → Find Messages for read-only literal text and
HL7 field/component searches across the current document, including unsaved edits.
Results keep original ordinals, support double-click/Enter navigation and 200-row pages,
and can filter the existing message navigator. Edits, switching and closure invalidate
results safely. Opening/focusing never runs a search. Queries are saved only by explicit
library actions; results stay ephemeral and query values are never logged. Stage 6A,
including the final Find Messages UX cleanup, passed
native Windows acceptance on 2026-10-16.

Stage 6B adds a Simple/Advanced selector, up to 16 visual conditions in nested ALL/ANY
groups (16 groups, four levels including the root),
negative literal comparisons and field/segment existence checks. The separate Search
Library explicitly saves/updates named queries with a nonmodal local unencrypted-storage
disclosure; sensitive queries are allowed. Version-1 flat definitions migrate safely to
version-2 trees on explicit update. Loading never runs
a search. Query edits cancel/clear stale results and filters. Clear retains the query.
Advanced has a draggable query/results splitter (initially 60/40), with its proportion
saved independently of compact Simple mode; resizing never runs or invalidates a search.
Stage 6B passed native Windows acceptance and is accepted; the user authorized its
milestone commit and normal push to main. See
[search semantics](docs/message-search.md) and [Stage 6A acceptance](docs/stage6a-search-acceptance.md).
The [Stage 6B acceptance checklist](docs/stage6b-advanced-search-acceptance.md) covers the
builder, local library, restart persistence and compatibility checks.

## Message comparison (Stage 7)

HL7 Tools → Message Comparison opens a document-sized, dockable/floating workspace.
The default **Side-by-Side** view immediately shows both syntax-highlighted source panes,
ready for temporary pasted text even with no documents open. **Differences** groups
readable before/after changes by segment through the existing view toggle.
Switching views retains results, selection and loaded source panes. Copy and ignore
settings live in compact menus, alongside Next/Previous (F7/Shift+F7) and the count.

With no documents open, paste directly into the A/B panes, or focus the A/B source control
and press Ctrl+V. Results update after a short pause. Each source control offers
Open Message, Original Message and editable temporary Pasted Text; switching kinds
preserves that side's pasted draft. Document-backed sources remain read-only and use
original message ordinals, including with search filters. Compare With Original on
the ribbon/raw-editor context menu compares an unsaved draft with its initially loaded
content. Comparison never changes document content, history or save state.

Pasted text is retained exactly, including line endings, within the existing complete
editor limits (1 Mi UTF-16 characters, 16 Ki per line). Paste one message per side;
multiple MSH messages receive an explicit explanation. Temporary pasted text is cleared
when the comparison workspace closes. Large document-backed messages retain bounded
source windows. Settings now contains Preferences only; its edit-mode default remains
persisted and applies to newly opened documents.

Implementation, automated verification and user-confirmed native Windows acceptance
are complete. See [comparison instructions](docs/message-comparison.md),
[architecture decision](docs/decisions/0010-read-only-message-comparison.md) and the
[Stage 7 acceptance checklist](docs/stage7-message-comparison-acceptance.md), including
the reproducible synthetic sample generator. No comparison content/history is persisted.

## Planned capabilities

Message generation, template cloning, additional batch analysis,
SQL-style querying, and TCP/MLLP transport remain planned. File-based MLLP framing support is available
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
