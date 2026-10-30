# Historical documentation and decision index

This index preserves established paths. Milestone reports describe their dated
implementation, measurements, native checklists and approval state; their totals
are not current verification counts. Original “do not begin”/uncommitted/pending
statements retain historical meaning and do not supersede a later user instruction.
Use [user tasks](user/index.md), [current architecture](developer/architecture.md)
and [shared references](reference/index.md) for present behavior.

Some stage-named files also contain the authoritative format/compatibility reference,
especially Stage 16/17/18. Their current reference sections remain linked from the
new guides; indexing them here does not declare those contracts obsolete.

## Milestones and native acceptance

| Area | Retained records |
| --- | --- |
| Dock/viewer foundation | [Docking gate](docking-acceptance.md), [Viewer 1](viewer-stage1-acceptance.md), [Viewer 2](viewer-stage2-acceptance.md), [Viewer 3](viewer-stage3-acceptance.md), [source-viewer UX](viewer-stage3-ux-acceptance.md) |
| Stage 3 polish | [Windows fixes](stage3-windows-acceptance-fixes.md), [performance](stage3-performance.md), [Activity Log](stage3-activity-log-acceptance.md), [workspace layout/source lifetime](stage3-workspace-layout-acceptance.md) |
| Editing | [Stage 4](stage4-editable-document-acceptance.md), [raw-input crash](stage4-raw-editing-crash.md), [inspection continuity](stage4-inspection-continuity.md) |
| Scripting foundation | [Stage 5A](stage5a-scripting-acceptance.md), [editor visual](stage5a-script-editor-visual-acceptance.md), [tooltip discovery/dismissal](stage5a-native-tooltip-dismissal.md), [CI investigation](stage5a-final-ci-investigation.md), [Stage 5B lifecycle](stage5b-batch-scripting-acceptance.md), [UI/save/ribbon finishing](stage5b-ui-finishing-acceptance.md) |
| Search/comparison | [Stage 6A](stage6a-search-acceptance.md), [Stage 6B](stage6b-advanced-search-acceptance.md), [Stage 7](stage7-message-comparison-acceptance.md) |
| Structure/generation | [Stage 8B](stage8b-structural-editing-acceptance.md), [8B.4 compatibility](stage8b4-compatibility-acceptance.md), [8C insertion](stage8c-message-insertion-acceptance.md), [8D generator](stage8d-synthetic-generator-acceptance.md), [script output naming](script-output-naming-acceptance.md) |
| De-identification/editor | [Stage 9](stage9-deidentification-acceptance.md), [report correction](deidentification-report-fix-acceptance.md), [9C developer experience](stage9c-script-developer-experience-acceptance.md) |
| Extraction/FHIR | [Stage 10](stage10-extraction-analysis-acceptance.md), [Stage 11](stage11-synthea-fhir-acceptance.md), [FHIR workspace follow-up](stage15ui-fhir-workspace-acceptance.md), [generation UX](fhir-generation-ux-acceptance.md) |
| Transport | [Stage 12](stage12-tcp-mllp-acceptance.md), [13A batch](stage13a-batch-transmission-acceptance.md), [13B load](stage13b-load-testing-acceptance.md) |
| Release/branding | [Stage 14](stage14-release-readiness-acceptance.md), [product branding](product-branding-acceptance.md), [reference/provenance audit](product-reference-audit.md) |
| SQL/responsive tools | [15A engine](stage15a-sql-engine-acceptance.md), [15B HL7](stage15b-hl7-query-acceptance.md), [15C UI](stage15c-sql-ui-acceptance.md), [15D stabilization](stage15d-sql-final-acceptance.md), [15UI layout](stage15ui-acceptance.md) |
| Generation/validation/reference | [Stage 16 and refinements](stage16-synthetic-generation.md), [Stage 17 including 17F](stage17-hl7-schema-validation.md), [Stage 18](stage18-script-api-reference.md) |
| Documentation | [19A audit](stage19-documentation-audit.md), [19B user docs](stage19b-user-documentation.md), [19C developer docs](stage19c-developer-documentation.md) |

The later SQL fixture cleanup race is documented in the current
[lifecycle guide](developer/workspace-lifecycle.md#sql-fixture-cleanup-race) with
links to its permanent regression tests. No new full-suite result is inferred from
that code reference. [PROJECT_CONTEXT](PROJECT_CONTEXT.md) and the
[legacy architecture chronology](architecture.md) remain additional historical context.

## Decisions and supersession

All ADR paths remain in [decisions/](decisions/):

| Decisions | Responsibility / later amendment |
| --- | --- |
| [0001 foundation](decisions/0001-foundation.md), [0002 parser](decisions/0002-lossless-parser.md) | Original foundation and continuing lossless parsing/editing invariants. |
| [0003 Dock](decisions/0003-dockable-workspace.md), [0004 editing](decisions/0004-editable-document-foundation.md) | Dock boundary and later bounded working state; the initial read-only product scope is historical. |
| [0005 scripts](decisions/0005-message-scripting-foundation.md), [0006 stateful batches](decisions/0006-stateful-batch-scripting.md), [0007 unified lifecycle](decisions/0007-unified-csharp-scripting.md) | 0007 replaces script kinds/lifecycle authoring/schema attribution, not every preservation/transaction rule in 0005/0006. |
| [0008 search](decisions/0008-read-only-message-search.md), [0009 saved search](decisions/0009-explicit-saved-search-definitions.md), [0010 comparison](decisions/0010-read-only-message-comparison.md), [0011 logical structure](decisions/0011-structural-editing-foundation.md) | Stable identities/captures/publication; later compatibility sections govern inserted messages. |
| [0012 generation](decisions/0012-deterministic-synthetic-generation.md), [0013 de-identification](decisions/0013-script-deidentification-aids.md), [0014 extraction](decisions/0014-read-only-field-extraction.md), [0015 FHIR](decisions/0015-local-synthetic-fhir-integration.md) | Retain version/style/seed and later workspace/mapping amendments. |
| [0016 interactive](decisions/0016-interactive-mllp.md), [0017 batch](decisions/0017-controlled-batch-transmission.md), [0018 load](decisions/0018-controlled-load-testing.md) | Separate review/authorization/rate/outcome contracts; later load support does not imply retry/TLS/scheduling. |
| [0019 portable preview](decisions/0019-portable-beta-readiness.md), [0020 SQL](decisions/0020-isolated-read-only-sql.md), [0021 validation](decisions/0021-explicit-inline-schema-validation.md) | Artifact-specific release, worker enforcement/amendments and explicit per-message validation. |

## Frozen release and planning material

[Portable release procedure](portable-release.md), [portable dependencies](portable-dependencies.md),
[native license review](native-license-applicability-review.md),
[distribution review](beta-distribution-review.md), [recipient README](beta-readme.md),
[preview user guide](beta-user-guide.md), [PRIVATE-PREVIEW.txt](PRIVATE-PREVIEW.txt),
[release-notes template](release-notes-template.md), [withdrawn-terms stub](beta-usage-terms-proposed.md)
and [branding source/derivatives](branding.md) retain their exact scope. Their presence
does not approve a new package/signature/recipient or rewrite protected artifact copies.

The [initial specification](../HL7_Workbench_Specification.md) and
[bootstrap prompt](../Codex_Starter_Prompt.md) are historical planning inputs. Do not
infer a JavaScript sandbox, CLI, IRIS connector, universal FHIR mapping or proposed
Generation/Synthea projects from them as current implementation.
