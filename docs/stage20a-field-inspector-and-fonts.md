# Stage 20A — Field Inspector and content fonts

This stage extends the existing Field Inspector; it adds no structured editor,
bulk mutation, SQL engine, scripting API or network workflow. No commit, push,
packaging or publication is authorized. Automated verification is recorded below;
native Windows acceptance remains pending.

## Inspector architecture and selection

MessageInspection retains the shared Core scanner/materialized projections and
message-local UTF-16 ranges. InspectorProjection exposes fields for the exact
selected segment occurrence, filters blank rows and adds only fields declared by
the explicitly applied schema. These absent rows have no source range. Underlying
scanner collections, byte mappings, parser limits and serialization are unchanged.

Tree rows show path, structural description/known datatype, value state and a
bounded encoded preview. Repetition/component/subcomponent children stay lazy.
Selected fields expand by default; other expansion paths and blank visibility are
session state. Refresh restores canonical identities rather than stale offsets;
switching segments/documents clears unrelated expansion. Empty fields retain their
real zero-length span, explicit null stays distinct, and MSH/custom delimiters use
Core's existing numbering/splitting. Large values retain bounded chunk/source access.

Current schema metadata does not contain friendly field/segment/component names.
The inspector therefore uses honest structural labels with available datatypes,
without inventing names for custom segments or importing another metadata library.
Schema management and validation remain explicit existing workflows.

Pending/stale structure keeps its previous enabled presentation while rejecting
selection, expansion input, blank-toggle rebinding and source actions. Root and
child collections remain stable when visibility has not changed, preserving
realized controls and scroll position during typing. Inline diagnostics, hover,
raw editing/history and source publication remain owned by their existing layers.

## Context actions

| Action | Implementation and boundary |
| --- | --- |
| Copy Value | ReadTextAsync supports small materialized and large source-backed messages; exact encoded characters only, bounded to 131,072 UTF-16 units. Rechecks captured selection/revision before clipboard publication. Oversized/missing values are unavailable rather than silently abbreviated. |
| Copy HL7 Path | Core occurrence/repetition-specific location of the actual row. |
| Copy Path | Preserved reusable unindexed search path, explicitly distinguished in its tooltip. |
| Find Occurrences… | Existing whole-active-file search, structural presence at that path, all occurrences/repetitions, including unsaved work. |
| Filter by This Value… | Existing search/filter with case-sensitive raw equality and complete retained matching identities. Values fit the existing 1,024-character bound. A whole repeated field is unavailable until a repetition is selected. Advanced equality now admits exact empty/whitespace literals; absent positions remain missing. |
| Extract Field to Table… | Existing configuration/results tool, unsaved-draft consent, indexed selected path, raw values and Expand Matches. Whole fields expand repetitions; explicit indexes remain fixed. Review scope and run explicitly. Stale modal returns do not replace the draft. |
| List Unique Values… | Deferred to Stage 20C; no menu entry. Existing SQL relations support explicit grouping, but this stage does not add an inspector-to-query workflow with reviewed raw/state/repetition grouping and SQL-draft consent. |

InspectionActionContext captures document, MessageId, revision and selected node.
MainWindow handles the same routed intent from docked and floating roots. Search
also guards its active source after asynchronous reads; extraction rechecks the
captured target before replacing a draft. Menus disable stale/unavailable actions;
source contents and patient identifiers are never logged.

## Shared font sizing

Appearance exposes Default Content Font Size with a 14 DIP reference default,
12–32 DIP range (approximately 9–24 points), explicit units and Reset Content Size.
Apply/OK atomically saves first and updates live tools; failed saves leave the
previous preference active. Invalid values clamp or fall back safely. Known feature
keys, not filenames/document IDs, index optional persistent overrides.

DockContentTemplate installs ContentFontScope on ordinary content. Native inherited
font properties and scoped dynamic resources preserve authored relative typography
at the default. There are no transforms, recreated documents or text edits for zoom.
Ctrl+Plus/Minus saves only the focused feature's override; Ctrl+0 removes it so the
feature follows the global default. A content tooltip exposes size and shortcuts.
All raw HL7 tabs share the raw-viewer key. Floating content uses the same settings.

| Covered content | Details |
| --- | --- |
| Raw HL7 and Full Source Viewer | Existing AvaloniaEdit controls, including virtualized presentation blocks, caret/selection, syntax and native scrolling. |
| Field Inspector and Segment Navigator | Native TreeView/ListBox rows, bounded previews and values. |
| Browser, Find Messages and Activity Log | Inherited content fonts, including virtualized result rows. |
| SQL | Editor, result cells and complete-value display; execution/budgets unchanged. |
| Script Editor | Existing code font adapter, output/status/diagnostic text; code remains bounded 9–24 points. Existing wheel zoom stays code-only. Legacy explicit settings migrate once, clamped to shared bounds; original legacy files remain. |
| Documentation and Script API Reference | Selectable prose, tables, headings, code and search/navigation content. |
| Extraction and Comparison | Native cells/details/editors; extraction's one-line maximum-height clipping is removed. |
| FHIR patients, reports, transport/batch/load tools | Registered content inherits shared sizing; no execution/network action starts on resize. |
| Excluded | Main ribbon/menu/window chrome; independent modal configuration, export and About dialogs retain normal sizing. |

Headings retain relative sizes. Script legacy sizes near the lower bound can clamp
during migration; Ctrl+0 opts back into the global default. Reset Layout resets
placement, not these separately persisted preferences. Native controls may need
scrolling in short/narrow docks at large fonts; do not treat headless measurement
as Windows DPI/accessibility acceptance.

## Verification

Both normal and isolated locked restores pass. The final Release build has zero
warnings/errors. Final automated results:

| Run | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| Focused Core structural scanner | 5 | 0 | 0 |
| Focused Workspace inspector, fonts and advanced search | 109 | 0 | 0 |
| Focused Desktop inspection, fonts, script layout, insertion/generation and editing | 84 | 0 | 0 |
| Full regular solution, final build | 2,768 | 0 | 14 expected opt-ins |
| Python documentation compiler | 12 | 0 | 0 |

The final full run contains 2,782 cases and takes 4m 22.155s. All five test projects
pass. The 14 skip identities match the retained Stage 19D regular-suite baseline;
no exclusions were added. There are 29 new ordinary C# cases: 20 Workspace and
9 Desktop. Focused counts are separate runs, not additional full-suite cases.
An earlier full pass also had 2,768/0/14; final view-detachment, bounded absent-cache
and expansion-restoration refinements were then rebuilt and reverified with the
focused classes and complete suite.

Documentation generation and both catalog consistency checks pass. The link/anchor
audit covers 152 Markdown files, 1,071 local links and 38 Markdown anchor links with
zero errors. Whitespace checks pass, including new files. All 2,384 protected files
match the original artifacts/stage17/protected-before.json SHA-256 baseline, with
none missing/added/changed. Build/test/runtime inputs match the recorded final
520-file tested hash snapshot. Core, Transport, Querying/QueryHost, Scripting/ScriptHost,
packages, locks and Script API catalogs are unchanged.

Commands use the existing telemetry opt-outs set to 1 for DOTNET_CLI_TELEMETRY_OPTOUT,
AVALONIA_TELEMETRY_OPTOUT and TESTINGPLATFORM_TELEMETRY_OPTOUT:

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet restore HL7Workbench.sln --locked-mode --artifacts-path C:/Dev/HL7Workbench/artifacts/stage20a/restore
dotnet build HL7Workbench.sln --configuration Release --no-restore
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
python -B -m unittest build/test_documentation_catalog.py
python build/WriteDocumentationCatalog.py
python build/WriteDocumentationCatalog.py --check
python build/WriteScriptApiReference.py --check
git diff --check
```

Focused runs use dotnet test --project for each test project, Release/no-build/
no-restore, followed by -- --filter-class with the exact class wildcard. Retained
ignored evidence under artifacts/stage20a includes final and earlier suite logs,
focused/build/catalog logs, isolated restore metadata, file inventories and hash/
link audits. No disposable HL7/CSV exports or one-off harnesses remain.

Development failures were investigated, not excluded: initial read operations used
the source-only reader instead of ReadTextAsync for materialized messages; stale
extraction discard previously applied before eligibility was rechecked; projection
collection identity and blank-toggle rebinding affected stale rows; the headless
application needed the same default font resources as production. Permanent tests
cover these fixes. Existing tests that assumed
all empty fields visible now explicitly exercise Show Blank Fields; their original
source, SQL, generation, insertion and history assertions remain intact.

## Native Windows acceptance checklist — pending

1. Open synthetic ADT/ORU/custom-delimiter messages. Select repeated segment
   occurrences in raw text and Navigator; verify inspector ownership and exact paths.
2. Expand/collapse fields, repetitions, components and subcomponents using pointer
   and arrow keys. Selected fields expand; unrelated fields stay compact. Float,
   redock and reconstruct views; confirm valid selection/expansion survives.
3. Toggle blank fields. Apply a schema explicitly and distinguish absent/empty/null
   rows; missing rows never highlight manufactured source characters. Check unknown
   versions/custom segments and honest fallback descriptions.
4. Right-click a component/subcomponent. Copy raw escaped text and both path forms.
   Check repeated-field filtering restrictions, empty equality and oversized-value
   explanations; use original-source export deliberately for large attachments.
5. Run Find/Filter across more than one result page; review displayed entire-file
   semantics and skipped counts. Clear restores navigation. Prepare extraction,
   review indexed paths/scope and run; check CSV and unchanged source bytes.
6. Edit raw text, switch messages/documents, close during work and cancel analysis.
   Verify previous-parse presentation pauses actions and never uses stale ranges.
7. Apply/reset global content size, then zoom/reset independently in raw HL7, SQL,
   C#, inspector, Navigator, Browser, Find, Activity, extraction/comparison, reports,
   FHIR, transport, Help and API Reference. Check tooltips and restart persistence.
8. Confirm Ctrl+Plus/Minus/0 routes to the focused docked/floating content only;
   C# wheel zoom remains local. Verify selection, caret, syntax colors, dirty state
   and undo/redo survive resizing, and no script/SQL/network work starts.
9. Test Light/Dark, short/narrow/maximized docks and 100/150/200% Windows DPI,
   including moving floating tools between monitors. Check wrapping, scrolling,
   hit testing, focus, screen reader and clipboard behavior.
10. Regression: edit synthetic HL7, undo/redo, Save As, run SQL, open API Reference
    and offline Documentation. Review all output targets; confirm no unexpected edits.

No native manual acceptance is claimed. Structured editing remains Stage 20B;
bulk mutation and advanced unique analysis remain Stage 20C.

## Changed files

43 updated files and 9 new files (52 total), all within Stage 20A:

```text
docs/PROJECT_CONTEXT.md
docs/desktop-preferences.md
docs/developer/desktop-tools.md
docs/message-search.md
docs/reference/storage-and-formats.md
docs/stage20a-field-inspector-and-fonts.md
docs/user/extract-fields.md
docs/user/inspect-and-edit.md
docs/user/preferences-and-shortcuts.md
docs/user/search-and-filter.md
docs/user/troubleshooting.md
src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/Documentation/catalog.json
src/HL7Workbench.Desktop/MainWindow.Inspection.cs
src/HL7Workbench.Desktop/MainWindow.Shell.cs
src/HL7Workbench.Desktop/MainWindow.axaml.cs
src/HL7Workbench.Desktop/ViewModels/ScriptEditorViewModel.cs
src/HL7Workbench.Desktop/ViewModels/SearchViewModel.cs
src/HL7Workbench.Desktop/Viewer/ApplicationTheme.cs
src/HL7Workbench.Desktop/Viewer/ComparisonView.cs
src/HL7Workbench.Desktop/Viewer/ContentFontScope.cs
src/HL7Workbench.Desktop/Viewer/DocumentationView.cs
src/HL7Workbench.Desktop/Viewer/FieldExtractionView.cs
src/HL7Workbench.Desktop/Viewer/InspectionPanes.cs
src/HL7Workbench.Desktop/Viewer/MessageViewer.cs
src/HL7Workbench.Desktop/Viewer/PreferencesWindow.cs
src/HL7Workbench.Desktop/Viewer/PresentationView.cs
src/HL7Workbench.Desktop/Viewer/ScriptApiReferenceView.cs
src/HL7Workbench.Desktop/Viewer/ScriptEditorView.cs
src/HL7Workbench.Desktop/Viewer/SearchResultsView.cs
src/HL7Workbench.Desktop/Viewer/SqlQueryView.cs
src/HL7Workbench.Desktop/Viewer/SyntheticPatientsView.Layout.cs
src/HL7Workbench.Desktop/Viewer/SyntheticPatientsView.cs
src/HL7Workbench.Desktop/Viewer/ViewerRequests.cs
src/HL7Workbench.Workspace/ApplicationPreferences.cs
src/HL7Workbench.Workspace/ContentFontPreferences.cs
src/HL7Workbench.Workspace/EditorPreferences.cs
src/HL7Workbench.Workspace/ExtractionConfigurationSession.cs
src/HL7Workbench.Workspace/InspectorProjection.cs
src/HL7Workbench.Workspace/MessageInspection.cs
src/HL7Workbench.Workspace/MessageSearchQuery.Advanced.cs
tests/HL7Workbench.Desktop.Tests/ContentFontViewTests.cs
tests/HL7Workbench.Desktop.Tests/EditableViewerTests.cs
tests/HL7Workbench.Desktop.Tests/EditingInspectionViewTests.cs
tests/HL7Workbench.Desktop.Tests/InspectionViewTests.cs
tests/HL7Workbench.Desktop.Tests/InspectorRedesignViewTests.cs
tests/HL7Workbench.Desktop.Tests/MessageInsertionViewTests.Compatibility.cs
tests/HL7Workbench.Desktop.Tests/SyntheticGenerationViewTests.cs
tests/HL7Workbench.Workspace.Tests/AdvancedSearchTests.cs
tests/HL7Workbench.Workspace.Tests/ContentFontPreferencesTests.cs
tests/HL7Workbench.Workspace.Tests/InspectorRedesignTests.cs
```
