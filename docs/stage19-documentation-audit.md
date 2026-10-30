# Stage 19A: documentation audit

Audit date: 2026-10-30. Repository baseline: `63ffd0ce4e9afa2815eee27e846e015fb62b438f`
(`CL fix`), following `b1271a8` (`Stage 18 acceptance`). The working tree was clean
at the start. The owner confirms Stage 18 and the subsequent SQL test cleanup fix
are committed and green in GitHub Actions. That confirmation is recorded here;
this audit did not independently retrieve a new Actions run.

This is an assessment and proposed plan, not the new documentation set. No existing
manual, application behavior, Help integration, package, or protected beta artifact
has been changed. Stage 19B has not begun.

## Findings and recommended priorities

The application has considerably more functionality than its entry-point docs
consistently describe. Useful instructions, architectural contracts and permanent
test evidence already exist. The main problem is authority and organization:
current guidance is interleaved with milestone approvals, superseded designs,
historical test totals and pending-acceptance statements.

1. **Correct misleading claims first in Stage 19B.** SQL and batch/load transmission
   are implemented; scripts are trusted C# with account permissions; schema commands
   live in the HL7 Tools ribbon. General privacy language must distinguish local
   processing from intentional networking and script privileges.
2. **Create two task-oriented entry points.** A user should be able to open a
   synthetic file, inspect/edit/save it and choose an analysis workflow without
   reading milestone history. A developer needs a current project/dependency map,
   reproducible build/test commands and explicit ownership/preservation contracts.
3. **Retain authoritative references and historical evidence.** Keep the Stage 18
   generated API reference/catalog, ADRs, reusable examples, tests and acceptance
   records. Link to them selectively; do not turn milestone logs into the user manual.
4. **Bundle a curated offline Help catalog after content review.** Use the existing
   native tool/resource conventions. Preserve the Script API reference and its
   verified runtime contract pipeline. No browser service, network search, WebView
   or new rendering dependency is necessary for the recommended initial scope.

## Audit method and evidence boundaries

- Inventoried root Markdown, all `docs/**/*.md`, both synthetic-sample READMEs,
  non-Markdown documentation resources and relevant build/deployment tooling.
  Before this report there were **106 Markdown documents** in that inventory,
  approximately **206,127 whitespace-delimited words**. This excludes dependency
  license copies, protected packaged copies, `bin/`, `obj/` and ignored artifacts.
  The number measures volume, not current user-manual coverage.
- Read repository guidance, current and historical sections of
  [project context](PROJECT_CONTEXT.md), the feature guides and relevant ADRs;
  inspected headings/status claims across the milestone corpus. Historical test
  totals were treated as evidence for their original checkpoint, not current totals.
- Cross-checked UI labels and routes in
  [MainWindow.axaml](../src/HL7Workbench.Desktop/MainWindow.axaml),
  [shell bindings](../src/HL7Workbench.Desktop/MainWindow.Shell.cs),
  [UtilityRibbon](../src/HL7Workbench.Desktop/Viewer/UtilityRibbon.cs), Preferences,
  feature models/services and the permanent tests referenced below. Test references
  identify executable coverage; they do not imply every test was rerun in this audit.
- Checked the generated Script API Markdown against its authoritative JSON with
  `build/WriteScriptApiReference.py --check`. Inspected local documentation link
  targets; the initial simple scanner's sole apparent missing target was the
  timestamp syntax in an inline code span in the FHIR mapping, not a broken link.
- Compared all protected files against the retained SHA-256 manifest
  `artifacts/stage17/protected-before.json` before and after the audit. Results are
  recorded under Verification below. Protected content was hashed, not rewritten.

This audit verifies availability and contracts from source/tests. It is not a new
native Windows visual/accessibility acceptance, performance benchmark, legal review
or distribution approval. No new full-suite pass count is claimed for a Markdown-only
change.

## Documentation inventory

### Entry points, policy and architecture

| Material | Present purpose and assessment | Proposed treatment |
| --- | --- | --- |
| [README](../README.md) | Setup, large capability overview, milestone history, test guidance and old roadmap mixed together; about 3,773 words. Several direct contradictions listed below. | Short product/start-here/build entry point linking user and developer indexes. |
| [AGENTS](../AGENTS.md) | Current repository boundaries, preservation/privacy rules, test retention and cleanup policy. | Preserve as contributor/agent authority; link from developer contribution guidance. Do not present as end-user help. |
| [PROJECT_CONTEXT](PROJECT_CONTEXT.md) | About 22,955 words of reverse-chronological implementation/verification context plus older project summaries. Valuable history, unreliable as a single current-status page. | Later add a concise current-state index and clearly label historical gates; retain original evidence. |
| [Original specification](../HL7_Workbench_Specification.md) | Product vision and initial milestone plan, including unimplemented requirements and superseded scripting/solution designs. | Explicitly historical requirements/backlog input, not current feature documentation. |
| [Starter prompt](../Codex_Starter_Prompt.md) | Milestone 0 assignment and initial constraints. | Historical bootstrap material; exclude from bundled user Help. |
| [Architecture](architecture.md) | About 5,416 words of chronological additions. Strong individual contracts but stale project/status statements remain. | Build a current architecture guide; retain linked milestone rationale separately. |
| [ADRs 0001–0021](decisions/) | Foundation, parser, Dock, editing, scripting, search, comparison, generation, extraction, FHIR, transport, beta, SQL and validation decisions. Amendments explicitly supersede parts of earlier decisions. | Preserve IDs/paths and decision history; add a current decision map with supersession scope. ADR 0007 governs current C# lifecycle. |
| [Responsive tool layout](responsive-tool-layout.md) | Practical native control composition, finite measurement, event/lifetime and Dock ownership guidance. | Reuse as developer UI extension guidance. |

### Current feature and API references

| Area | Existing files | Assessment |
| --- | --- | --- |
| Parser and coordinates | [parser-api](parser-api.md), [hl7-paths](hl7-paths.md) | Strong lossless/encoding/limit contracts. Explicitly distinguish per-message Core APIs from Workspace file/framing features. |
| Search | [message-search](message-search.md) | Detailed semantics and saved-search lifecycle, but contains old authorization/menu statements. Needs a short tutorial and semantics reference split. |
| Comparison | [message-comparison](message-comparison.md) | Useful task flow and limitations; retain pasted/current/original distinctions and source identity behavior. |
| Extraction | [field-extraction](field-extraction.md) | Good workflow/state/alignment/CSV reference; needs discovery from a user index and a worked synthetic example. |
| SQL | [sql-query-ui](sql-query-ui.md), [sql-hl7-workflows](sql-hl7-workflows.md), [sql-query](sql-query.md) | Complementary UI, workflow/function and schema/status material. Names/entry points do not establish which to read first; consolidate navigation, avoid duplicating relation/function lists. |
| Scripting | [script-api](script-api.md), [script-api-reference](script-api-reference.md), [catalog JSON](script-api-reference.json) | Workflow/lifecycle narrative plus generated member reference and authoritative reviewed content. Keep this pipeline; generated Markdown is not an independently edited manual. |
| Synthetic HL7 | [stage16-synthetic-generation](stage16-synthetic-generation.md), [stage8d acceptance](stage8d-synthetic-generator-acceptance.md) | Current behavior is present but buried among 16/16A–D refinements and superseded defaults. Needs a single current workflow separate from history. |
| Schema validation | [stage17 guide](stage17-hl7-schema-validation.md), [ADR 0021](decisions/0021-explicit-inline-schema-validation.md), [profile examples](examples/) | Current ribbon workflow, exact scope, formats, provenance and limits exist. A simpler apply/import/review tutorial is missing. |
| FHIR | [fhir-patient-workspace](fhir-patient-workspace.md), [synthea-fhir-import](synthea-fhir-import.md), [mapping](fhir-to-hl7v2-mapping.md) | Current UI overview, deeper import/history guide and versioned mapping. Preserve explicit mapping exclusions and different seed semantics. |
| Networking | [tcp-mllp](tcp-mllp.md), [batch-transmission](batch-transmission.md), [load-testing](load-testing.md) | Separate task guides already reflect three different review/authorization, ACK, cancellation and reporting workflows. Keep that separation. |
| Preferences and saves | [desktop-preferences](desktop-preferences.md), [save-and-export-safety](save-and-export-safety.md) | Mix user settings, architectural contracts and historical UI changes. Some descriptions are stale; safety details should support focused user/developer pages. |
| Privacy | [data-handling](data-handling.md) | Good factual baseline; supplement with a discoverable storage/retention matrix and troubleshooting instructions. No new EULA or compliance claim. |

The reference JSON and `build/WriteScriptApiReference.py` are part of the documentation
system, as are the runtime contract reader and real-worker example tests. Stage 18
provides 52 topics, 25 supported facade/helper types and 186 supported declared
contracts; six public infrastructure types with 29 contracts are separately
classified as unsupported for script authoring. There are six editable starters.
These are current catalog inventories, not an invitation to invent additional APIs.

### Milestone and acceptance corpus

The following groups account for all milestone/incident Markdown in the inventory.
They are retained engineering evidence, not redundant files to delete:

| Group | Files and content | Documentation use |
| --- | --- | --- |
| Viewer/Dock | `docking-acceptance.md`; `viewer-stage1-acceptance.md`, `viewer-stage2-acceptance.md`, `viewer-stage3-acceptance.md`, `viewer-stage3-ux-acceptance.md`; `stage3-windows-acceptance-fixes.md`, `stage3-performance.md`, `stage3-activity-log-acceptance.md`, `stage3-workspace-layout-acceptance.md` | Source preservation, large-file behavior, native Dock limits and the Full Source Viewer navigation limitation. |
| Editing | `stage4-editable-document-acceptance.md`, `stage4-raw-editing-crash.md`, `stage4-inspection-continuity.md` | Materialization/history limits, routed editor input safety and current/stale inspection. |
| Scripts/editor | `stage5a-scripting-acceptance.md`, `stage5a-script-editor-visual-acceptance.md`, `stage5a-native-tooltip-dismissal.md`, `stage5a-final-ci-investigation.md`, `stage5b-batch-scripting-acceptance.md`, `stage5b-ui-finishing-acceptance.md`, `script-output-naming-acceptance.md`, `stage9c-script-developer-experience-acceptance.md`, `stage18-script-api-reference.md` | Lifecycle evolution, naming, dialogs, semantic assistance, reference authority and native checks. |
| Search/comparison | `stage6a-search-acceptance.md`, `stage6b-advanced-search-acceptance.md`, `stage7-message-comparison-acceptance.md` | Synthetic examples, semantics and native procedures. |
| Structure/generation | `stage8b-structural-editing-acceptance.md`, `stage8b4-compatibility-acceptance.md`, `stage8c-message-insertion-acceptance.md`, `stage8d-synthetic-generator-acceptance.md`, `stage16-synthetic-generation.md` | Stable logical IDs, insertion/save/history transaction and versioned generation contracts. |
| De-identification | `stage9-deidentification-acceptance.md`, `deidentification-report-fix-acceptance.md` | Field-coverage matrix, residual risks and output-owned Review Required reports. |
| Extraction/FHIR | `stage10-extraction-analysis-acceptance.md`, `stage11-synthea-fhir-acceptance.md`, `stage15ui-fhir-workspace-acceptance.md`, `fhir-generation-ux-acceptance.md` | Extraction sessions, mapping/history scope, direct generation and revised workspace. |
| Transport | `stage12-tcp-mllp-acceptance.md`, `stage13a-batch-transmission-acceptance.md`, `stage13b-load-testing-acceptance.md` | Correlation, uncertain sends, pacing/rate bounds and native procedures. |
| Release/branding | `stage14-release-readiness-acceptance.md`, `product-branding-acceptance.md` | Accepted binary/provenance gates, no-debug-path corrections and native branding checks. |
| SQL/layout/validation | `stage15a-sql-engine-acceptance.md`, `stage15b-hl7-query-acceptance.md`, `stage15c-sql-ui-acceptance.md`, `stage15d-sql-final-acceptance.md`, `stage15ui-acceptance.md`, `stage17-hl7-schema-validation.md` | Enforcement and measured limits, complete output actions, responsive/native layout and profile scope. |

### Distribution, provenance and synthetic supporting material

- Release documents: [portable-release](portable-release.md),
  [portable-dependencies](portable-dependencies.md),
  [beta-user-guide](beta-user-guide.md), [beta-readme](beta-readme.md),
  [beta-distribution-review](beta-distribution-review.md),
  [withdrawn terms stub](beta-usage-terms-proposed.md),
  [release-notes template](release-notes-template.md), and [PRIVATE-PREVIEW.txt](PRIVATE-PREVIEW.txt).
  These describe a specific older accepted private preview. They are not proof that
  a current source build has been packaged or authorized for distribution.
- Provenance/licensing: [dependencies](dependencies.md),
  [native-license review](native-license-applicability-review.md),
  [branding](branding.md), [product-reference audit](product-reference-audit.md),
  [validation metadata notice](../src/HL7Workbench.Core/ValidationCatalog/NOTICE.md)
  and the `build/third-party/` notice/license files. Preserve review boundaries and
  upstream attribution. The Stage 17 adapted metadata has MPL-2.0 obligations
  separate from the earlier portable package inventory.
- [Synthetic sample guide](../samples/synthetic-only/README.md) and
  [FHIR fixture guide](../samples/synthetic-only/fhir-r4/README.md), HL7 samples,
  two authored FHIR Bundles and the comparison fixture generator supply reusable
  examples. `stage5b-repeated-patients_TEST.hl7` is pre-existing; this audit neither
  deletes it nor assumes it is disposable because of its name.
- `docs/examples/` contains the native profile authoring schema, synthetic JSON and
  XML profiles and a synthetic message. Keep these as permanent fixtures.
- Build/reference infrastructure includes `global.json`, central build/package
  properties, committed lock files, `.github/workflows/build.yml`,
  `build/WriteScriptApiReference.py`, `build/update-validation-catalog.py`, worker
  copy targets, portable build/verification tools and their permanent tests.
  This is part of developer documentation coverage, not application Help content.

## Verified feature inventory

“Implemented” here means a production command/service exists and associated
permanent automated coverage was inspected. It does not expand the supported scope
or certify arbitrary HL7/FHIR input. Paths below identify the implementation and
test evidence to use when writing later guides.

| Capability and actual user workflow | Implemented behavior and important boundary | Implementation and test references |
| --- | --- | --- |
| Open HL7 and navigate combined files | File → Open File opens immutable snapshots; indexed concatenated/MLLP-framed messages have First/Previous/Next/Last and direct position controls. Tolerant recovery retains unclassified/malformed bytes. No implicit repair or MSH-18 encoding guess. | [MessageFileReader](../src/HL7Workbench.Workspace/MessageFileReader.cs), [MessageFileIndex](../src/HL7Workbench.Workspace/MessageFileIndex.cs), [file tests](../tests/HL7Workbench.Workspace.Tests/MessageFileReaderTests.cs), [Stage 3 source tests](../tests/HL7Workbench.Workspace.Tests/Stage3SourceTests.cs). |
| Raw viewing and large messages | Syntax coloring, bounded previews/virtualized blocks and original-byte Full Source Viewer/export. Attachments are not implicitly decoded/rendered. Full Source Viewer retains an explicitly chosen source across unrelated tab activation; same-file message navigation ends its borrowed session. | [presentation](../src/HL7Workbench.Workspace/MessagePresentation.cs), [RawInspectorSession](../src/HL7Workbench.Workspace/RawInspectorSession.cs), [source view](../src/HL7Workbench.Desktop/Viewer/FullRawInspectorView.cs), [raw-inspector tests](../tests/HL7Workbench.Workspace.Tests/RawInspectorTests.cs), [presentation tests](../tests/HL7Workbench.Workspace.Tests/PresentationTests.cs). |
| Field Inspector and Segment Navigator | Document-owned structural selection, repeated/custom/opaque segments, field/repetition/component/subcomponent inspection, Copy Path, raw-hover coordinates and double-click reveal. These are inspectors; direct field-grid editing is not implemented. Copy Path for Find Messages intentionally omits occurrence/repetition restrictions. | [MessageInspection](../src/HL7Workbench.Workspace/MessageInspection.cs), [InspectionPanes](../src/HL7Workbench.Desktop/Viewer/InspectionPanes.cs), [inspection tests](../tests/HL7Workbench.Workspace.Tests/MessageInspectionTests.cs), [refinement tests](../tests/HL7Workbench.Desktop.Tests/InspectionRefinementViewTests.cs). |
| Edit, history and save | Per-document Edit Mode, bounded raw working text, document-wide undo/redo and dirty-close Save/Discard/Cancel. Edit Mode off retains drafts/history. Same-directory staged publication preserves untouched bytes, BOM, encoding, framing and terminators; invalid boundaries/unencodable edits fail atomically. Save does not redefine the original snapshot. | [editing](../src/HL7Workbench.Workspace/MessageDocument.Editing.cs), [saving](../src/HL7Workbench.Workspace/MessageDocument.Saving.cs), [FileSaveDestination](../src/HL7Workbench.Workspace/FileSaveDestination.cs), [editable-document tests](../tests/HL7Workbench.Workspace.Tests/EditableDocumentTests.cs), [native-input regression](../tests/HL7Workbench.Desktop.Tests/RawEditingCrashTests.cs). |
| Add generated or blank messages | Home/File/raw navigator Add Message inserts at Before/After Current/Beginning/End. Unfiltered Next at EOF can append/reuse a blank draft. Stable MessageId and logical order survive undo/redo; blank/unsafe drafts block Save. No general segment insertion/deletion UI is implied. | [sequence](../src/HL7Workbench.Workspace/MessageDocument.Sequence.cs), [insertion workflow](../src/HL7Workbench.Desktop/Viewer/MessageInsertionWorkflow.cs), [structural tests](../tests/HL7Workbench.Workspace.Tests/StructuralEditingTests.cs), [insertion input tests](../tests/HL7Workbench.Desktop.Tests/MessageInsertionNativeInputTests.cs). |
| Find Messages and filter navigation | Ctrl+F/View → HL7 Inspection; literal text or field comparisons; bounded nested ALL/ANY groups, segment/field presence and negative comparisons; explicit Search Library operations. Results/filter state are session-owned. No regex, replace or occurrence-indexed search paths. | [search](../src/HL7Workbench.Workspace/MessageSearch.cs), [advanced query](../src/HL7Workbench.Workspace/MessageSearchQuery.Advanced.cs), [SearchLibrary](../src/HL7Workbench.Workspace/SearchLibrary.cs), [advanced tests](../tests/HL7Workbench.Workspace.Tests/AdvancedSearchTests.cs), [Desktop search tests](../tests/HL7Workbench.Desktop.Tests/AdvancedSearchUiTests.cs). |
| Message Comparison | HL7 Tools → Message Comparison accepts two temporary pasted messages or captured current/original document messages. Side-by-Side/Differences, ignore settings and F7/Shift+F7 navigation. Compare With Original preserves source/history. Comparison is read-only for document-backed sources and does not merge. | [MessageComparison](../src/HL7Workbench.Workspace/MessageComparison.cs), [comparison view](../src/HL7Workbench.Desktop/Viewer/ComparisonView.cs), [pasted tests](../tests/HL7Workbench.Workspace.Tests/PastedComparisonTests.cs), [comparison tests](../tests/HL7Workbench.Workspace.Tests/ComparisonTests.cs). |
| Field Extraction | View → Data & Query, or document Extract Fields; current/whole document, ordered columns, explicit Core occurrence paths, first/ordinal-expanded matches, state/text/numeric filters, full-result sorting and all-selected-result CSV. Expansion zips candidates/broadcasts scalars, not clinical grouping or Cartesian joins. | [FieldExtraction](../src/HL7Workbench.Workspace/FieldExtraction.cs), [ExtractionResults](../src/HL7Workbench.Workspace/ExtractionResults.cs), [field tests](../tests/HL7Workbench.Workspace.Tests/FieldExtractionTests.cs), [view tests](../tests/HL7Workbench.Desktop.Tests/FieldExtractionViewTests.cs). |
| SQL Query | View → Data & Query → SQL Query; current/selected open documents, Ctrl+Enter, one read-only SQLite statement over six closed relations; lazy HL7 value/raw/state/status functions, joins/aggregates, typed complete results and bounded pages. No external database/server, arbitrary SQL writes or UI parameter panel. | [SqlQuery](../src/HL7Workbench.Workspace/SqlQuery.cs), [schema](../src/HL7Workbench.Querying/QuerySchema.cs), [engine](../src/HL7Workbench.QueryHost/QueryEngine.cs), [worker tests](../tests/HL7Workbench.Querying.Tests/QueryWorkerTests.cs), [hardening tests](../tests/HL7Workbench.Querying.Tests/QueryHardeningTests.cs). |
| SQL outputs, library and lifecycle | Copy/inspect complete bounded scalars, stable all-row sort, CSV of all result rows and deduplicated matching-message extraction across all pages. Extraction produces an independent unsaved document from captured bytes. Explicit named SQL storage and bounded session history are distinct. Desktop cancels running operations on source close/change; completed captures remain independently usable and stale-labeled. | [results](../src/HL7Workbench.Workspace/SqlQueryResults.cs), [extraction](../src/HL7Workbench.Workspace/SqlQueryResults.Extraction.cs), [library](../src/HL7Workbench.Workspace/SqlQueryLibrary.cs), [SQL model](../src/HL7Workbench.Desktop/ViewModels/SqlQueryViewModel.cs), [SQL tests](../tests/HL7Workbench.Workspace.Tests/SqlQueryTests.cs), [Desktop regression including cleanup](../tests/HL7Workbench.Desktop.Tests/SqlQueryToolTests.cs). |
| Trusted C# scripting and Script Library | New/duplicate/rename/save/delete, Current message/Whole document; initialize once, Main per captured message, optional Complete once on success, fresh worker per Run. Output/report publication is all-or-nothing and independent of sources. Not JavaScript and not an OS security sandbox; arbitrary trusted C# has account privileges. | [UnifiedScript](../src/HL7Workbench.ScriptHost/UnifiedScript.cs), [ScriptMessage](../src/HL7Workbench.Scripting/ScriptMessage.cs), [ScriptBatch](../src/HL7Workbench.Scripting/ScriptBatch.cs), [ScriptExecution](../src/HL7Workbench.Workspace/ScriptExecution.cs), [lifecycle tests](../tests/HL7Workbench.Scripting.Tests/UnifiedLifecycleTests.cs), [library tests](../tests/HL7Workbench.Workspace.Tests/ScriptLibraryTests.cs). |
| Script authoring help | Help/Scripts/editor API Reference, F1 context lookup, explicit Ctrl+Space facade completion and Ctrl+Shift+Space signatures; related topics/back navigation, copy catalog/examples and six editable starters. Lookup compiles/binds symbols without running code. Built-ins require duplication to edit. | [catalog loader](../src/HL7Workbench.Workspace/ScriptApiReference.cs), [contract reader](../src/HL7Workbench.Workspace/ScriptApiContracts.cs), [assistance](../src/HL7Workbench.Desktop/Viewer/ScriptEditorAssistance.cs), [catalog tests](../tests/HL7Workbench.Workspace.Tests/ScriptApiCatalogTests.cs), [real-worker example tests](../tests/HL7Workbench.Scripting.Tests/ScriptReferenceExampleTests.cs), [native-input tests](../tests/HL7Workbench.Desktop.Tests/ScriptReferenceNativeInputTests.cs). |
| De-identification aids | Patient Deidentifier, Identifier Pseudonymizer and Timestamp Shifter presets use run-local deterministic mappings. Output-owned reports retain Review Required and residual risks. Narrow presets retain unrelated identifying content; no anonymization/compliance guarantee. | [workflow](../src/HL7Workbench.Scripting/DeidentificationWorkflow.cs), [built-ins](../src/HL7Workbench.Workspace/ScriptLibrary.Deidentification.cs), [report](../src/HL7Workbench.Workspace/TransformationReport.cs), [execution tests](../tests/HL7Workbench.Scripting.Tests/DeidentificationTests.cs), [report tests](../tests/HL7Workbench.Workspace.Tests/DeidentificationReportTests.cs). |
| Standalone synthetic HL7 | Generate Messages creates ADT A01/A04/A08, ORM O01 or ORU R01. Realistic Synthetic defaults to version 2; Obvious Placeholders retains original version-1 explicit-seed bytes. New document or atomic insertion into captured target; report/Copy seed. No new template/dataset implied. | [options](../src/HL7Workbench.Core/SyntheticGenerationOptions.cs), [generator](../src/HL7Workbench.Core/SyntheticMessageGenerator.cs), [realistic projection](../src/HL7Workbench.Core/SyntheticMessageGenerator.Realistic.cs), [standalone demographics tests](../tests/HL7Workbench.Core.Tests/StandaloneDemographicTests.cs), [standalone seed tests](../tests/HL7Workbench.Workspace.Tests/StandaloneSeedTests.cs). |
| Connected synthetic scenarios and automatic seeds | Admission to discharge, Order to result, Combined encounter; linked patients/encounters/orders/results with optional transfer and bounded overrides. Both UI modes start with blank seed; one cryptographic uint is resolved after preflight per Generate. Explicit 0/42/uint boundaries replay with unchanged settings/style/version/time. Draft fields remain separate and unresolved; Core/programmatic defaults retain compatibility. | [scenario engine](../src/HL7Workbench.Core/SyntheticScenarioGenerator.cs), [coordinator](../src/HL7Workbench.Workspace/SyntheticGeneration.cs), [dialog model](../src/HL7Workbench.Desktop/ViewModels/SyntheticGenerationViewModel.cs), [scenario tests](../tests/HL7Workbench.Core.Tests/SyntheticScenarioTests.cs), [seed tests](../tests/HL7Workbench.Workspace.Tests/SyntheticSeedTests.cs), [report tests](../tests/HL7Workbench.Desktop.Tests/GenerationReportToolTests.cs). |
| Explicit schema validation | HL7 Tools → Schema Validation: Apply/Change, Remove, Import and Manage reuse one selector/library. Per-message application; no validation on file open. Edit-triggered revalidation, cancellation/revision guards, inline marks/hover/F8 and read-only status. Built-ins cover seven v2.5.1 triggers/five structures; Incomplete is meaningful, not certification. | [catalog](../src/HL7Workbench.Core/Hl7ValidationCatalog.cs), [validator](../src/HL7Workbench.Core/Hl7SchemaValidator.cs), [document lifecycle](../src/HL7Workbench.Workspace/MessageDocument.Validation.cs), [commands](../src/HL7Workbench.Desktop/Viewer/SchemaValidationCommands.cs), [schema tests](../tests/HL7Workbench.Core.Tests/Hl7SchemaValidationTests.cs), [lifecycle tests](../tests/HL7Workbench.Workspace.Tests/SchemaValidationLifecycleTests.cs), [ribbon tests](../tests/HL7Workbench.Desktop.Tests/SchemaRibbonTests.cs). |
| Custom profiles | Strict bounded native JSON and supported conformance XML subset, narrowing-only inheritance, explicitly placed Z segments and atomic local import/reimport/removal. Unsupported rules are rejected or explicitly incomplete; no arbitrary executable rules/network resolution. | [importer](../src/HL7Workbench.Core/Hl7ProfileImporter.cs), [resolver](../src/HL7Workbench.Core/Hl7ProfileResolver.cs), [library](../src/HL7Workbench.Workspace/ValidationProfileLibrary.cs), [import tests](../tests/HL7Workbench.Core.Tests/Hl7ProfileImportTests.cs), [library tests](../tests/HL7Workbench.Workspace.Tests/ValidationProfileLibraryTests.cs). |
| Local FHIR import and HL7 mapping | File → Import Patient FHIR / View → FHIR Patient Workspace; local R4 Bundle files/top-level folder, patient search/details/encounters/timeline. Selected Patient/Entire Import/Selected Events → partial versioned ADT A04/A01/A03 and ORU R01 mapping; independent combined output/report. Preview optional; no acknowledgment gate or remote fetch. FHIR seed changes transport IDs, not imported demographics. | [import boundary](../src/HL7Workbench.Workspace/FhirPatientImport.cs), [importer](../src/HL7Workbench.Core/FhirR4Importer.cs), [mapping plan](../src/HL7Workbench.Core/FhirGeneration.cs), [publisher](../src/HL7Workbench.Workspace/FhirGenerationPublisher.cs), [direct-generation tests](../tests/HL7Workbench.Workspace.Tests/FhirDirectGenerationTests.cs), [workspace tests](../tests/HL7Workbench.Desktop.Tests/FhirWorkspaceTests.cs). |
| Interactive TCP/MLLP | Connection Manager and MLLP Listener from View/ribbon; explicit connect/send/listen, strict incremental framing, exact capture, original/enhanced ACK correlation and bounded test responses. Endpoint metadata persists, live state/payloads do not. Plain TCP; TLS unavailable/fails closed. No automatic reconnect/retry. | [Transport](../src/HL7Workbench.Transport/), [TransportSession](../src/HL7Workbench.Workspace/TransportSession.cs), [profiles](../src/HL7Workbench.Workspace/TransportProfiles.cs), [network tests](../tests/HL7Workbench.Workspace.Tests/TransportNetworkTests.cs), [tool tests](../tests/HL7Workbench.Desktop.Tests/TransportToolTests.cs). |
| Batch and controlled load transmission | Separate explicit prepare/review/authorize workflows. Batch is sequential/paced with pause/resume/cancel; load uses global rate/concurrency, duration/attempt/safety stops and bounded metrics. ACK/application acceptance differs from write completion. No automatic retry, scheduling or restart resume. | [batch](../src/HL7Workbench.Workspace/BatchTransmission.cs), [load](../src/HL7Workbench.Workspace/LoadTesting.cs), [batch tests](../tests/HL7Workbench.Workspace.Tests/BatchTransmissionTests.cs), [load tests](../tests/HL7Workbench.Workspace.Tests/LoadTestingTests.cs), [view tests](../tests/HL7Workbench.Desktop.Tests/LoadTestingViewTests.cs). |
| Docking, layouts and preferences | Dock/float/hide/reopen tools, save/restore/reset/recover layout and version-1/2 placement compatibility. Layout is not document/session recovery. General edit default/ribbon visibility, Follow System/Light/Dark, explicit validation defaults and search split preference; script editor font settings are separate. | [factory](../src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs), [layout DTO](../src/HL7Workbench.Workspace/WorkspaceLayout.cs), [preferences](../src/HL7Workbench.Workspace/EditorPreferences.cs), [PreferencesWindow](../src/HL7Workbench.Desktop/Viewer/PreferencesWindow.cs), [default layout tests](../tests/HL7Workbench.Desktop.Tests/DefaultWorkspaceLayoutTests.cs), [preference tests](../tests/HL7Workbench.Workspace.Tests/ApplicationPreferencesTests.cs). |
| Activity, privacy and file/export handling | Bounded application-session Activity Log uses fixed/count-only metadata, not payload/path/exception text. Explicit CSV/HL7 exports may contain sensitive values. Temp snapshots/journals are plaintext local storage; cleanup is ownership/drain-based, not secure erasure. About exposes a privacy summary. | [ActivityLog](../src/HL7Workbench.Workspace/ActivityLog.cs), [SourceSnapshot](../src/HL7Workbench.Workspace/SourceSnapshot.cs), [save boundary](../src/HL7Workbench.Workspace/FileSaveDestination.cs), [About](../src/HL7Workbench.Desktop/Viewer/AboutWindow.cs), [activity tests](../tests/HL7Workbench.Workspace.Tests/ActivityLogTests.cs), [save tests](../tests/HL7Workbench.Workspace.Tests/FileSaveDestinationTests.cs). |

### Limits that need a single discoverable reference

These are different layers, not interchangeable advertised file-size limits:

- Raw editing: complete message up to 1,048,576 UTF-16 units and 16,384 per line;
  16,777,216 retained characters (32 MiB UTF-16 storage) and 4,096 history entries
  per document. Source-backed viewing is a separate bounded path.
- Logical file sequence: at most 100,000 messages. Generation: at most 1,000 messages
  and 4 Mi UTF-16 units per batch, plus editor/document/publication limits.
- Search: at most 16 conditions, 16 groups, depth four; 200-row UI pages. Extraction
  and SQL use 100-row pages; complete result actions are not page-only actions.
- SQL: configured budgets have hard caps; the native database, materialized scalar,
  result, snapshot and extracted-output limits are distinct. Preserve the authoritative
  definitions in [QueryContracts](../src/HL7Workbench.Querying/QueryContracts.cs)
  and [SqlQueryModels](../src/HL7Workbench.Workspace/SqlQueryModels.cs), not a guessed
  “SQLite supports any file size” statement.
- Validation: 128 applied selections per document; profile/rule/value/diagnostic
  budgets and coverage gaps can yield Incomplete/Unavailable. An error-free built-in
  result still carries base-model coverage notices.
- Controlled load: concurrency up to eight, global target up to 20 messages/second,
  duration up to 300 seconds, 5,000 attempts and first 1,000 detailed results, with
  separate bounded aggregate/latency statistics. These ceilings do not promise receiver safety.

### Implemented versus roadmap or explicitly unsupported

Do not preserve the old specification's milestone headings as the current roadmap.
The following remain outside the verified feature set:

| Item | Current status / distinction |
| --- | --- |
| TLS/authenticated transport, automatic resend/reconnect, scheduling | Not implemented. Controlled batch/load is implemented, with explicit fresh-run review. |
| External SQL database connections, arbitrary SQL writes/extensions, SQL parameter UI | Not supported. Workspace supports typed parameters; the Desktop editor has no parameter-entry panel. |
| Regex/replace and indexed occurrence selectors in Find Messages | Not supported by that UI. Core paths and extraction/SQL APIs have different selection semantics. |
| Structured field-grid mutation, general segment insertion/deletion, unbounded large-message editing, delimiter normalization | Not implemented by the current editor/facade. Whole-message insertion and bounded raw edits are implemented. |
| JavaScript runtime or restricted-permission script sandbox | Superseded initial design. Current engine is trusted C# in a bounded worker. |
| Arbitrary from-scratch construction through the scripting facade | Not a supported facade operation. Dedicated standalone/scenario generation exists; `ScriptDeferredMessage` is a run-owned handle, not a constructor API. |
| Universal HL7 profile/terminology support, schema validation for all versions, automatic file-open validation | Not implemented; seven scoped v2.5.1 built-ins and explicit custom profiles only. Parsing is delimiter-driven and broader than schema coverage. |
| Universal/bidirectional FHIR conversion, remote FHIR server/transaction execution, CSV patient import, integrated Synthea subprocess | Not implemented. Current workflow imports local supported R4 Bundles and performs a partial one-way HL7 mapping. |
| Per-patient multi-document FHIR publication and MPI ground-truth scenarios | Deferred; current FHIR publisher opens one atomic combined document. |
| IRIS/HealthShare-specific connector, CLI, plugin marketplace | Roadmap/specification concepts, not current solution projects or supported extension APIs. |
| Guaranteed de-identification, certified conformance, clinical decision support, secure erasure | No such guarantee. Review Required/Incomplete and preservation limits must remain explicit. |
| Cloud accounts/sync, runtime telemetry, automatic updater, signed installer/current public release | No implemented account/cloud/updater workflow or new release authorization established. Existing private-preview artifacts are a separately frozen baseline. |

## Documentation gaps and inaccuracies

Priority P1 means fix before describing the current application to users; P2 means
resolve during the relevant 19B/19C guide work. References describe the audited baseline;
they are not instructions to edit these files during Stage 19A.

| ID | Priority | Finding with evidence | Required documentation resolution |
| --- | --- | --- | --- |
| A01 | P1 | README's Implemented capabilities puts Apply Schema “in the existing raw editor.” The ribbon and Stage 17 guide place actions under HL7 Tools → Schema Validation; editor retains status/inline findings only. PreferencesWindow's HL7 explanation also still says “in the raw editor.” | Use the real ribbon route in the user guide. Record the Preferences UI wording as a later, separately authorized text-only UI correction; do not change it in this audit. |
| A02 | P1 | README Planned capabilities calls SQL and automated batch/load transmission planned. Earlier transport prose says concurrent load is deferred and later says Stage 12 still needs acceptance. Production commands and tests above exist. | Replace stale current-status claims with the verified scope. Keep genuinely deferred TLS/scheduling separate. |
| A03 | P1 | README Windows setup says the application has “no runtime network features.” TCP/MLLP intentionally networks; trusted scripts can use network APIs. | Say no automatic uploads/cloud dependency/telemetry, with explicit transport and trusted-script exceptions. |
| A04 | P1 | Original specification requires sandboxed scripts/default denial of filesystem/network/process access and prefers JavaScript. ADR 0007, ScriptHost and current privacy/reference docs establish trusted C# and account privileges. | Label original design requirements as historical/superseded; never market worker containment as a security sandbox. |
| A05 | P1 | `desktop-preferences.md` says File/Settings/View only, Sender/Listener disabled/Planned, and future profile storage/validation deferred. `message-search.md` also omits Help in its menu statement. Actual menu has Help, live transport, and Validation preferences. | Establish one menu/settings reference with accurate routes; remove current-tense stale claims when those guides are revised. |
| A06 | P1 | README describes only Core/Workspace/Desktop and three test projects. Solution has eight production projects and five test projects. Specification proposes nonexistent Generation/Synthea/CLI projects. | Publish the actual dependency map, including Scripting/ScriptHost, Querying/QueryHost and Transport, and distinguish process boundaries. |
| A07 | P1 | Architecture still says Core has no schema interpreter/validator and retains several old deferred-feature statements. The Stage 17 section and Core validator contradict those blanket summaries. | Write a present-tense architecture overview; link dated history and explicit ADR supersessions rather than stacking amendments in current instructions. |
| A08 | P2 | README repeats old pending Stage 8D acceptance, version-1-only generation language and mixed generator versions. Stage 16D source/guide establish realistic v2 plus placeholder compatibility. | One seed/style/version/replay procedure, with a compatibility table and no duplicate default claims. Keep FHIR seed behavior separate. |
| A09 | P2 | PROJECT_CONTEXT and Stage 16–18 reports contain “no next stage authorized,” unstaged file counts and pending native statements from their original gates. The owner has since accepted/committed stages. | Label checkpoint evidence with dates/commits; do not treat historical authorizations as permanent product limitations or erase the evidence. Current request governs this audit. |
| A10 | P2 | The initial specification uses a 10,000-message generation gate, while the implemented UI batch limit is 1,000/4 Mi UTF-16 units. Broader schema versions/FHIR message families in the vision exceed current supported mappings. | Put tested limits and scoped mappings in current guides; preserve larger goals as backlog only. |
| A11 | P2 | `dependencies.md` says no HL7 dictionary/terminology dependency and all packages listed are permissive. Stage 17 adds an adapted MPL-2.0 model resource without adding a runtime package. | Add a clear distinction between package inventory and redistributed adapted metadata; link its pinned source/notice/license. Do not infer legal clearance. |
| A12 | P2 | Private-preview docs describe frozen older binaries, a two-process Desktop/ScriptHost package and accepted hashes. Current source also deploys QueryHost and has later features. `build/portable_release.py` maps specific existing doc paths into the preview. | Keep preview docs/version/provenance scoped to that artifact. Future current-build installation/release docs must not imply those later features are in the frozen ZIP. No package rebuild in Stage 19. |
| A13 | P2 | Search, extraction, SQL and Core use related but different path selection rules, value states, page sizes and output semantics. These distinctions are scattered across separate guides. | Add a choose-your-analysis-workflow tutorial and link precise per-tool semantics. Never apply extraction's indexed path rules to Find Messages. |
| A14 | P2 | Save/export guidance is detailed but lacks one easily found answer to “original versus draft versus saved versus captured output.” Source-backed omission markers and Full Source Viewer session invalidation are easily missed. | Add a short preservation/capture model with practical examples; cross-link limits and troubleshooting instead of hiding it in developer notes. |
| A15 | P2 | No general installation/getting-started/workspace index, consolidated shortcuts, troubleshooting decision tree or storage/retention table. Beta guidance is useful but artifact-specific. | Add user entry pages and synthetic tasks with prerequisites, expected outcomes, cancellation/recovery and limitations. |
| A16 | P2 | Developer build/test instructions exist but lifecycle hazards are split across README, incident records and tests. Recent SQL cleanup demonstrates that synchronous Dispose is not necessarily the completion barrier. | Document task/lease ownership and awaited disposal, including already-closed documents; link permanent regressions. No arbitrary-delay cleanup recipes. |
| A17 | P2 | Stage 18 API help is comprehensive and generated; tutorials could inadvertently fork signatures/examples into another manually maintained API list. | Keep catalog JSON/runtime contracts authoritative; tutorials link topic IDs and reuse tested examples. Preserve unsupported-infrastructure classification. |
| A18 | P2 | Existing About privacy help is useful, but Help has no general user guide/search entry. Repository links and historic acceptance logs cannot serve as bundled product Help. | Implement the curated offline approach below only in approved 19D scope. |

No general broken-link cleanup is proposed on the strength of a regex-only scan.
Future validation should understand fenced/inline code, relative paths and heading
anchors, and distinguish actual links from example syntax.

## Proposed documentation structure

Use one repository source of truth for prose; generate only render/index artifacts.
Audience indexes should lead with tasks, not lists of controls or milestone numbers.
The following paths are proposed, not created by this audit:

```text
README.md                              short product/setup entry point
AGENTS.md                              repository work rules (retained)
docs/
  index.md                             audience and task index
  user/
    index.md
    installation.md                    source build vs approved portable artifact
    getting-started.md                 synthetic open -> inspect -> edit -> save
    workspace.md                       navigation, tools, dock/float/recover
    inspect-and-edit.md                 drafts, history, multi-message insertion
    search-and-filter.md                locate messages and restore navigation
    compare-messages.md                 pasted/current/original workflows
    extract-fields.md                   table/filter/sort/full CSV example
    query-with-sql.md                   choose sources -> query -> full outputs
    run-and-manage-scripts.md           duplicate -> save -> run -> inspect report
    generate-synthetic-hl7.md           standalone/scenario/replay
    validate-with-profiles.md           apply -> review -> import/narrow
    import-fhir-and-generate-hl7.md     local import/scopes/mapping limits
    test-with-mllp.md                   single exchange/listener
    batch-and-load-tests.md             separate explicit batch/load tasks
    preferences-and-shortcuts.md       real settings and focus-dependent keys
    privacy-and-storage.md             persistence/retention and trust boundaries
    troubleshooting.md                 symptom -> cause/check -> safe recovery
  developer/
    index.md
    build-and-test.md                   SDK/MTP/locked restores/CI/opt-ins
    architecture.md                     current 8-project + 2-worker map
    parser-invariants.md                original bytes/encoding/range origins
    workspace-lifecycle.md              captures/leases/cancellation/disposal
    desktop-tools.md                    native responsive controls/Dock boundary
    scripting.md                        facade/host phases/catalog maintenance
    sql.md                              schema/worker authorizer/budgets/RPC
    validation.md                       profiles/evaluator/source maps/provenance
    generation-and-fhir.md              deterministic versions/mapping/publication
    transport.md                       framing/ACKs/deadlines/drain
    security-and-data-handling.md       threat boundaries/storage/logging
    extension-points.md                 supported seams and required tests
    documentation-maintenance.md       authority/link/example/help checks
  reference/
    index.md                           links to authoritative API/format references
    limits.md                          layered limits with code provenance
    storage-and-formats.md              persisted vs session data and schemas
  decisions/                           existing 0001-0021 paths retained
  examples/                            permanent synthetic profile fixtures retained
  script-api-reference.json            authoritative reviewed catalog retained
  script-api-reference.md              generated reference retained
  history-index.md                     indexes existing dated milestone records
  stage*.md, viewer-stage*.md, ...      existing paths retained initially
```

Do not mass-move old files during the first writing pass. Existing relative links,
tests, resource inclusion and release tooling depend on some exact paths. Introduce
the new indexes first; where approved later, retire duplicated living prose through
short compatibility links to the canonical guide. Historical incident/acceptance
reports remain dated records. Keep parser/path references and the generated API
catalog at stable paths initially; decide any migration only after dependency checks.

Each user task should state its goal, prerequisites, shortest successful steps,
observable result, cancellation/recovery path, and material limits. Examples must be
synthetic and small enough for the normal application limits. Screenshots are useful
for orientation, not as the sole source of a command name or procedure. Capture only
synthetic data; include theme/scaling/version and accessible text descriptions.

## Detailed Stage 19B–19D plan

These are proposed gates. Approval of Stage 19A does not start their implementation.

### Stage 19B: user documentation and current navigation

1. Approve the information architecture and artifact/version scope. Establish
   `docs/index.md`, user index and a concise README that routes users/developers.
   Mark specification/bootstrap and checkpoint status as historical without
   rewriting accepted evidence or protected copies.
2. Write installation/getting-started/workspace first. Separate running a source
   build from using the older accepted portable preview. Use actual File/View/ribbon
   routes; explain document versus tool focus and Reset Layout recovery.
3. Write task guides in this order: inspect/edit/save and insertion; search/compare;
   extraction/SQL outputs; scripts/library/reports; synthetic seeds/scenarios;
   schema application/custom profiles; FHIR mapping; single/batch/load transport.
   Reuse existing authoritative limits, worked samples and tested examples.
4. Add the privacy/storage matrix, shortcuts/settings and symptom-based
   troubleshooting. Cover source-backed read-only limits, encoding/save rejection,
   missing worker folders, stale captures, Incomplete validation, repeated explicit
   seeds, ACK uncertainty and canceled operations. Do not suggest automatic resend,
   deletion of entire settings/temp roots or disabling security software.
5. Resolve A01–A05's misleading current guidance and other user-facing duplication
   only in authorized documentation files. Record UI text corrections separately.
   Do not change commands, defaults, persistence, legal terms or packaging.

**Acceptance:** every verified feature above has a discoverable task; real command
routes and focus-specific shortcuts agree with source; synthetic walkthroughs reach
the documented result; generation replay distinguishes FHIR from synthetic seeds;
privacy and limits survive editing. Validate links/anchors and examples. Check both
themes/narrow layouts for any approved screenshots on native Windows. Protected
hashes unchanged; review the content before Stage 19C.

### Stage 19C: developer documentation and maintenance contracts

1. Publish the current project/process dependency map. Core is independent of UI,
   files and network; Workspace is independent of Avalonia/Dock; QueryHost owns
   native SQLite and ScriptHost owns execution; Desktop owns native controls and
   keeps Dock adapters in the existing shell/Docking boundary.
2. Consolidate exact build/run/test instructions from `global.json`, central
   package properties and `.github/workflows/build.yml`: .NET 10 SDK floor
   10.0.401/latestFeature, locked restore, Release/no-restore, MTP test invocation
   and telemetry opt-outs. Describe normal and isolated-artifacts restore checks,
   opt-in stress/performance/platform cases, and native acceptance separately.
   Use historical counts only with their checkpoint; obtain fresh totals when
   an implementation gate actually runs tests.
3. Document the invariants: delimiter-driven scanning, missing/empty/null,
   original bytes/BOM/strict encoding, malformed preservation, Core model
   mutation/reacquisition, snapshot-global bytes versus message-local bytes/UTF-16
   versus editor windows, and explicit preview interval mapping.
4. Document lifecycle ownership with concrete paths through open/edit/capture,
   publication, tool close/reset and application shutdown. Include cancellation
   token linking, stale result/revision rejection, borrowed source leases and the
   `MessageDocument.Disposal` completion barrier. SQL fixture cleanup must await
   models and all opened documents, including removed documents; synchronous
   collection cleanup or `Task.Yield` is not the barrier. Link the committed SQL
   and validation-disposal regression tests.
5. Describe scripting phases/catalog generation and exact API authority, SQL
   authorizer/one-statement/closed-schema/native budgets, profile inheritance/import
   security and model attribution, versioned synthetic/FHIR generation, and transport
   uncertainty/drain. Extension points should identify existing seams, required
   bounded tests and privacy reviews, without promising a plugin ABI.
6. Add a maintenance checklist/feature-to-guide map. Require PRs changing commands,
   contracts, settings, limits or persistence to identify affected guides/examples.
   Preserve ADR supersessions and incident records. Separate dependency/license
   facts from distribution/legal approval.

**Acceptance:** documented commands match CI/project configuration; code/test links
resolve; lifecycle guidance matches the real completion tasks; catalog generator
`--check` and existing API/example checks pass as applicable. New docs checks should
validate meaningful links/contracts, not mirror prose sentence-by-sentence. Run
applicable restores/build/tests if code/build integration changes; no production
behavior changes should be needed for developer prose. Protected hashes unchanged;
review before authorizing Help implementation.

### Stage 19D: approved offline Help implementation and acceptance

1. Approve the Help entry, offline content scope, renderer/resource approach and
   retention policy below. Assign stable topic IDs and build a deterministic curated
   catalog/search text from approved documentation. Fail generation on duplicate
   IDs, missing local assets/targets or unsupported content; never silently omit
   important warnings or examples.
2. Add Help → Documentation using the existing native optional tool conventions.
   Wire Contents/search/topic/back navigation, selectable text, copyable code and
   internal links. Reuse the Script API tool for its topic IDs; preserve F1 in the
   Script Editor and its existing copy/starter/contract functionality.
3. Embed the validated curated catalog/assets and index in application resources.
   Resolve only known topic/asset IDs. Do not load user Markdown, fetch remote
   content, execute embedded HTML/scripts or open arbitrary filesystem paths.
   General documentation search must not scan messages, scripts, paths, history or
   saved definitions. Preserve normal browser/user control for explicitly selected
   external references, if external opening is approved.
4. Add focused catalog/search/navigation and Desktop command/accessibility/layout
   tests. Exercise no-document startup, no internet, internal/API links, unavailable
   topics, close/reopen/float/redock/reset, theme changes, narrow/wide/high-DPI and
   keyboard focus. Verify Help cannot mutate active documents or invoke scripting,
   SQL or transport implicitly.
5. Run normal and isolated locked restores, Release build, focused checks and the
   full regular solution suite for code integration; record exact pass/fail/skip
   totals and expected opt-ins. Run `git diff --check`, resource/link checks,
   generated API checks and the protected-file SHA-256 audit. Stop for native
   Windows acceptance/review. Packaging/publication remains a separate decision.

**Acceptance:** the approved manual is discoverable, searchable and readable with
network access absent; loaded binaries do not require the repository/current working
directory to find help; existing Script API entry points continue working; Help
placement is the only layout persistence. No copied PHI, source-backed ownership
changes, new external service, package rebuild or protected artifact modification.

## In-app Help recommendation

### Existing integration to preserve

- The current Help menu contains **Script API Reference** and **About Message
  Workbench**. About includes an expandable Data handling and privacy summary.
  There is no general documentation command, help search or generic Markdown viewer.
- Desktop uses Avalonia resources for `Assets/**` and theme XAML. Workspace embeds
  `docs/script-api-reference.json` as `HL7Workbench.ScriptApiReference.json`.
  [ScriptApiReference](../src/HL7Workbench.Workspace/ScriptApiReference.cs) loads an
  immutable catalog, checks supported contract inventory against the referenced
  Scripting assembly, and performs bounded literal case-insensitive AND search.
- [ScriptApiReferenceViewModel](../src/HL7Workbench.Desktop/ViewModels/ScriptApiReferenceViewModel.cs)
  retains bounded session navigation; [its native view](../src/HL7Workbench.Desktop/Viewer/ScriptApiReferenceView.cs)
  uses standard selectable text, read-only code boxes, responsive panes, explicit
  clipboard actions and accessible names. It already works without a document/network.
- `ToolWindowRegistry`, `WorkspaceDockFactory` and `DockContentTemplate` provide
  optional content creation/placement. The existing `tool.script-api-reference`
  identity, API topic/starter IDs and Help/Scripts/F1 entry points must remain stable.

### Recommended approach and alternatives

Add **Help → Documentation** as a separate native optional tool with Contents,
search, topic content and Back, following the existing reference tool's conventions.
Bundle the user manual, limits/privacy references and links into the existing API
catalog. “Links into” means navigation to the existing API tool, not copying its
member descriptions into a new catalog. A future developer-help subset can be
included if approved; full incident/release/authorization history should stay out
of the default product Help search.

Use reviewed Markdown as the authoring source and generate a versioned, bounded
catalog of headings, prose/list/table/code blocks, known internal links and plain
search text. Render this limited content with native controls. Keep code selectable,
monospaced and copyable; never execute samples. Validate the supported Markdown
subset during generation; an implementation must prove tables, escaping, links and
warnings remain faithful before choosing that subset. If a suitable renderer
requires a new package or WebView, bring that dependency decision back for review
instead of quietly expanding the architecture.

Embed the general help resource in **Desktop**, with a small immutable content/search
model free of Dock types and the view/model in the existing Desktop folders. There
is no reason to put UI help parsing in Core or add a new shared service project.
Reuse existing native layout/theme/accessibility practices; do not generalize the
runtime-derived Script API catalog into an unrelated arbitrary-document loader.
Build tooling should use explicit curated inputs, stable topic IDs, deterministic
ordering and output validation. Record content version/source revision without
embedding absolute developer paths. The help model must not hold MessageDocument
or source snapshot references.

Prefer bounded literal search over title, headings, approved text and keywords;
provide no-results feedback and category/task filters. No embeddings, remote index,
telemetry, regex execution or search of user data is needed. Bound query length,
history and result/snippet sizes; document the chosen values when implemented.
Keep session query/selection outside layouts and preferences. Closing/reconstructing
the view must unsubscribe handlers; optional immutable content can be shared safely.

Opening a local HTML site in the system browser is a possible fallback with fewer
native controls but weaker integrated focus/theme/navigation and a separate search
experience. Linking raw Markdown to an external viewer depends on file associations
and does not meet the searchable in-app goal by itself. An online website may be a
future companion, never the required source of Help. None of these alternatives
has been implemented or packaged here.

## Decisions requiring approval

These are review decisions for subsequent stages; none blocks completion of this audit.

| Decision | Recommendation | Consequence of approval |
| --- | --- | --- |
| Stage sequencing and structure | Approve 19B user docs, 19C developer docs, then 19D Help integration as separate review gates. | Authorizes only the explicitly approved next stage; this audit does not start it. |
| Canonical paths/history | Adopt audience indexes/new task pages; retain existing ADR/reference/milestone paths initially. Later replace duplicated living prose with compatibility links after dependency review. | Avoids breaking tests/resources/release-tool mappings or deleting accepted evidence. |
| User artifact scope | Document current source functionality while clearly scoping frozen private-preview instructions to their accepted binary baseline. | No new download/release promise or implied distribution permission. |
| Help scope/host | Native optional Documentation tool; bundle reviewed user/limits/privacy content, preserve and navigate to the existing Script API reference. | Requires a later Desktop-only integration and resource build/validation work. Decide whether selected developer guides also ship. |
| Rendering/dependencies | Deterministically generated bounded content blocks and native controls, no new runtime dependency initially. | Any new renderer/WebView/package requires separate review if necessary to preserve content fidelity. |
| UI wording defects | Approve a later minimal wording fix for the stale Preferences schema location and the Generation Report View-menu tooltip, which still says FHIR only. | These are production UI text changes, intentionally excluded from 19A. No command behavior changes. |
| Screenshots and languages | English text-first guides, selective native screenshots using reviewed synthetic data. | Agree supported Windows/theme/DPI coverage and whether screenshots are needed before Help integration. Localization/translation is separate scope. |
| Privacy/legal material | Reuse factual data-handling statements and existing notices; preserve withdrawn-terms status. | No new EULA, conformance claim, de-identification certification or licensing clearance. |
| Maintenance gate | Assign guide ownership by feature, retain API generation/example tests and add curated link/catalog checks in the approved integration stage. | Prevents another independent API list; avoids brittle prose-mirroring tests. |

## Verification and remaining limitations

- Initial and final protected audit: **2,384 expected / 2,384 present; zero changed
  SHA-256 hashes, zero missing and zero added files** under `artifacts/private-preview`.
  Comparison uses the pre-existing manifest, not a newly accepted replacement baseline.
- Generated Script API Markdown check passed against the unchanged embedded JSON.
  All 171 local link targets in this report exist. A code-span/fence-aware scan of
  the 107 inventoried Markdown documents (including this report) found 478 local
  file links outside code and no missing file targets; this is not a full heading-
  anchor or external-URL audit. The report adds no generated content/resource and
  does not change package/lock files.
- Final change scope: only `docs/stage19-documentation-audit.md`. Existing manuals,
  production/test/build sources, protected artifacts and staged files are unchanged;
  `git diff --check` passes. No test suite, locked restore or Release build was
  rerun for this prose-only audit. The previously verified SQL-cleanup baseline
  was 2,716 passed / 0 failed / 14 expected skips; that is historical verification,
  not a new Stage 19A run. CI success is owner-reported as stated above.
- Remaining work is the proposed writing/integration and native review, not a
  production implementation defect established by this audit. No fresh UI tour,
  offline packaged Help test, screen-reader audit or independent license review was
  performed. The two identified UI wording defects are deferred for explicit scope approval.
- No commit, push, package, publication or Stage 19B/Stage 19 implementation beyond
  this audit was performed. Stop here for review.
