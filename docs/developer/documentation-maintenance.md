# Documentation authority and maintenance

Documentation is part of a feature's change scope. Review instructions against the
actual command bindings, settings defaults, lifecycle implementation and permanent
tests. Do not infer current behavior from an old milestone title or a successful
build alone. User entry point: [user guide](../user/index.md); developer entry point:
[developer index](index.md); historical evidence: [history index](../history-index.md).

## Authority and stable paths

- Runtime/public contracts and tested source behavior establish what is implemented.
  Accepted ADR amendments explain intentional architecture/compatibility decisions.
  Resolve conflicts explicitly; do not silently expand a historical requirement.
- User task guides describe current workflows. Developer guides explain owners and
  invariants. Shared [limits](../reference/limits.md) and
  [storage/formats](../reference/storage-and-formats.md) link definitions, rather
  than replacing validation code or promising unbounded behavior.
- The Script API catalog JSON contains reviewed behavior/examples; runtime metadata
  provides exact supported signatures, and Markdown is generated. Keep that pipeline
  authoritative and stable topic/starter IDs intact.
- Historical reports, audits and PROJECT_CONTEXT sections retain dated verification
  and authorization. Never replace their old totals with a new run's totals or treat
  pending/forbidden-stage wording as permanent current product scope.
- Existing ADR/reference/example paths and preview-document build mappings remain
  stable. A future move needs incoming-link/resource/test/tooling review and explicit
  migration; do not mass-move or delete accepted evidence as cleanup.

## Feature-to-documentation ownership

The contributor changing a feature owns the documentation review. This map assigns
areas, not new organizational roles or an extension API.

| Changed area | User guidance to review | Developer/reference owners and evidence |
| --- | --- | --- |
| Open/view/edit/save, framing/encoding/ranges | [Inspect/edit](../user/inspect-and-edit.md), [recovery](../user/troubleshooting.md) | [Parser](parser-invariants.md), [lifecycle](workspace-lifecycle.md), parser/path APIs, ADRs 0002/0004/0011; Core round-trip and Workspace save tests. |
| Menus/ribbon/shortcuts/focus/layout/theme | [Workspace](../user/workspace.md), [keys/settings](../user/preferences-and-shortcuts.md) | [Desktop tools](desktop-tools.md), [architecture](architecture.md), layout DTO/reference, Desktop command/input/layout tests. |
| Search/extraction/comparison | [Workflow comparison](../user/index.md#choose-an-analysis-workflow), corresponding tasks | [Lifecycle](workspace-lifecycle.md), [limits](../reference/limits.md), existing semantics guides/ADRs 0008–0010/0014, capture/result tests. |
| Script facade/lifecycle/library/presets | [Scripts](../user/run-and-manage-scripts.md), privacy/de-identification warnings | [Scripting](scripting.md), catalog JSON/runtime signatures/generated Markdown, script-api, ADRs 0005–0007/0013, inventory/example/worker tests. |
| SQL schema/functions/worker/results | [SQL](../user/query-with-sql.md), full-result export and recovery | [SQL](sql.md), QuerySchema/QueryContracts, sql-query/sql-hl7-workflows, ADR 0020, authorizer/protocol/capture/all-page tests. |
| Profiles/schema rules/defaults/inline output | [Validation](../user/validate-with-profiles.md), Preferences | [Validation](validation.md), format/schema/examples, metadata notice/license, ADR 0021, evaluator/import/ribbon/lifecycle tests. |
| Synthetic generation/styles/seeds | [Generation](../user/generate-synthetic-hl7.md), report/replay | [Generation/FHIR](generation-and-fhir.md), Stage 16 contracts, ADR 0012, version goldens/seed-provider/publication tests. |
| FHIR import/eligibility/mapping/scopes | [FHIR tasks](../user/import-fhir-and-generate-hl7.md), privacy | [Generation/FHIR](generation-and-fhir.md), mapping reference, ADR 0015, import/history/direct-preview publication tests. |
| Transport/ACK/batch/load/security | [MLLP](../user/test-with-mllp.md), [batch/load](../user/batch-and-load-tests.md) | [Transport](transport.md), existing policy/metric references, ADRs 0016–0018, loopback/cancel/observation tests. |
| Settings/persisted definitions/provenance | [Privacy/storage](../user/privacy-and-storage.md), install/recovery | [Storage/formats](../reference/storage-and-formats.md), exact DTO/library/worker protocol owner, migration/revision/atomic-save tests. |
| Budgets/defaults/dependencies/security boundaries | Limits/troubleshooting, privacy, affected feature tasks | [Limits](../reference/limits.md), [security](security-and-data-handling.md), project/lock/dependency/resource notices, affected resource tests. |
| Build/CI/test classification/release scope | [Installation](../user/installation.md), README | [Build/test](build-and-test.md), global.json/workflow/project targets, dependency/provenance records; no implicit packaging authorization. |

If a change touches multiple rows, review each; changing a tooltip can still mislead
the user about scope even when no runtime logic changes. The two schema/report
wording corrections are included in [Stage 19D](../stage19d-offline-documentation-help.md).

## Verification procedure

1. Inspect Git status and preserve existing work. Record which files belong to the
   current documentation pass; keep historical reports/ADRs and protected preview
   hashes unchanged.
2. Check every relative file link and Markdown heading anchor, including duplicate
   heading slugs and explicit anchors. Exclude fenced/inline code before treating
   example syntax as links. Validate new index reachability and source/test paths;
   do not mark external URLs checked unless actually retrieved.
3. Verify numeric/format statements against their owners, identifying defaults,
   configurable hard caps, coordinate units and platform scope. Compare exact
   current project references/process deployment and command labels/focus handlers.
4. Reuse synthetic fixtures and tested catalog examples. Check copied snippets for
   exact phase/API/type requirements. A parser/mock/syntax check is not real worker
   execution or native acceptance; state the evidence level.
5. Run the existing non-mutating consistency check:

   ```powershell
   python build/WriteScriptApiReference.py --check
   git diff --check
   ```

6. Check new untracked files for trailing whitespace/final newlines separately:
   tracked `git diff --check` alone does not inspect them. Inspect final Git status,
   generated/untracked files and protected SHA-256 hashes. Report any retained
   temporary evidence without deleting permanent fixtures or hiding failures.

Do not refresh API signatures from a stale export just to satisfy a generator. See
[scripting maintenance](scripting.md#generated-api-catalog-authority) for reviewed
synchronization/rebuild/real-worker checks when runtime contracts actually change.
Offline Help uses `docs/help-topics.json` as the explicit input allowlist. After
editing a curated guide or supported markup, run:

```powershell
python build/WriteDocumentationCatalog.py
python build/WriteDocumentationCatalog.py --check
python -B -m unittest build/test_documentation_catalog.py
```

Check in the generated Desktop resource with its canonical Markdown. The compiler
rejects unsupported markup, missing anchors/targets and resource budgets; never
remove a warning/example just to satisfy it. CI checks both catalogs and compiler
regressions. Runtime loads the embedded immutable resource, never repository files.
Only curated user/shared topics are bundled; other repository references remain
labelled locations, and API links reuse the existing reference tool. No parser or
renderer dependency is required. General authored-link scans supplement the curated
compiler checks, as recorded in the stage's verification report.

## Acceptance evidence and future Help

For code changes, run applicable locked restores, Release/focused/full tests and
native acceptance; record exact current counts. For documentation-only changes,
report link/reference/example/API/whitespace/hash checks without pretending an
old full-suite total is current. Keep useful permanent tests and diagnostic scripts
across milestones; remove only clearly owned disposable artifacts.

General offline Help uses approved source text and stable API cross-navigation,
not another manually maintained member list. See [Stage 19D](../stage19d-offline-documentation-help.md)
for host/renderer/search/lifetime bounds and pending native acceptance. Writing docs does not authorize new application
behavior, packaging, publication or modifications to accepted private-preview files.
