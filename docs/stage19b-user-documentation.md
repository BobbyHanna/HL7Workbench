# Stage 19B: user documentation and navigation

Date: 2026-10-30. Baseline: `63ffd0ce4e9afa2815eee27e846e015fb62b438f`
(`CL fix`, following Stage 18 acceptance). The completed Stage 19A audit was already
present as an untracked file and is preserved unchanged. This stage changes only
documentation; no Stage 19C developer guide or Stage 19D Help code is implemented.

## Entry points and scope

[README](../README.md) is now a concise project introduction, current feature/trust/
release summary, source setup and eight-project map. [Documentation home](index.md)
separates user tasks, existing developer references and historical release/evidence
material. [User home](user/index.md) links all tasks and compares Find Messages,
Field Extraction, SQL and scripting.

Current-source instructions are distinguished from the separately frozen, older
private-preview artifact. Original specification/bootstrap instructions and dated
milestone gates remain historical, not current capability or distribution authority.
No file has been moved. Existing ADRs/reports/examples and protected copies are retained.

## Files created and updated

Created user documentation:

- [Installation](user/installation.md)
- [Getting started](user/getting-started.md)
- [Workspace](user/workspace.md)
- [Inspect and edit](user/inspect-and-edit.md)
- [Search and filter](user/search-and-filter.md)
- [Compare messages](user/compare-messages.md)
- [Extract fields](user/extract-fields.md)
- [Query with SQL](user/query-with-sql.md)
- [Run and manage scripts](user/run-and-manage-scripts.md)
- [Generate synthetic HL7](user/generate-synthetic-hl7.md)
- [Validate with profiles](user/validate-with-profiles.md)
- [Import FHIR and generate HL7](user/import-fhir-and-generate-hl7.md)
- [Test with MLLP](user/test-with-mllp.md)
- [Batch and load tests](user/batch-and-load-tests.md)
- [Preferences and shortcuts](user/preferences-and-shortcuts.md)
- [Privacy and storage](user/privacy-and-storage.md)
- [Troubleshooting and limits](user/troubleshooting.md)

Also created `docs/index.md`, `docs/user/index.md` and this record: **20 new files**.
Updated **four existing files**: `README.md`, `docs/desktop-preferences.md`,
`docs/message-search.md` and `docs/tcp-mllp.md`. The latter three are living guides;
their stale current menu/network/schema-status statements were corrected without
rewriting historical milestone reports. Total Stage 19B delta: **24 documentation
paths**, in addition to the unchanged pre-existing Stage 19A audit.

## Audit findings addressed

| Stage 19A finding | Stage 19B disposition |
| --- | --- |
| A01 | User docs/README use HL7 Tools → Schema Validation. Actual Preferences wording is deferred below. |
| A02–A03 | README now identifies implemented SQL/batch/load and intentional networking, separating socket write/ACK/application outcomes and trusted-script access. |
| A04 | Trusted C# model is explicit; specification/bootstrap are labeled historical at entry points. Original requirement/evidence files are unchanged. |
| A05 | Living Preferences/search docs include Help; transport is implemented; palette customization is distinguished from active validation profiles. |
| A06 | README accurately maps eight production/five test projects. Full developer architecture remains Stage 19C. |
| A07 | Current task/README guidance avoids stale architecture claims. Consolidating historical architecture/context is deferred to Stage 19C; no historical records rewritten. |
| A08 | One user procedure covers styles, standalone v1 compatibility, realistic/scenario v2 and operation-scoped automatic/explicit replay. |
| A09 | Entry points label historical authorization/status/test-count evidence; original records remain preserved. |
| A10 | Current batch/message/coverage limits are discoverable; vision-only requirements are not marketed as implemented. |
| A11 | README links adapted metadata attribution. Package/license inventory consolidation and any legal review remain Stage 19C/separate approval. |
| A12 | Source and older accepted-preview baselines are separated. Preview manuals/tool mappings/artifacts are unchanged; no new release is promised. |
| A13 | User index compares analysis workflows; guides distinguish path semantics, pages/full actions and value states. |
| A14 | Editing guide distinguishes original source, working draft, saved output and captures; source-viewer invalidation/encoding/save failures are covered. |
| A15 | User entry points, 17 tasks, storage matrix, focus-specific keys, limits and symptom-based recovery are present. |
| A16 | Developer lifecycle consolidation remains Stage 19C; user recovery avoids arbitrary-delay or directory-deletion recipes. |
| A17 | Script guide uses the existing tested catalog starter and links generated member reference; no duplicate signature catalog. |
| A18 | Documentation is repository Markdown; current Help capabilities are stated accurately. General offline Help remains Stage 19D. |

## Source and example review

Routes/labels were checked against MainWindow markup and shell command bindings,
UtilityRibbon, PreferencesWindow and the corresponding feature views/models.
Shortcuts were checked against routed handlers in DocumentHistoryShortcuts,
FileDocumentView, SearchResultsView, ScriptEditorView/Assistance, SqlQueryView,
MessageInspectionView.Validation and ComparisonView. Independent text-input undo
and Script/SQL Ctrl+S are kept distinct from document history/saving.

Authoritative references and existing tests were used to check capture/publication,
seed, profile/mapping and trust claims. Examples use only pre-existing reviewed
synthetic fixtures: the ADT/ORU samples, authored FHIR R4 Bundles and Stage 17 profile/
message examples. The script procedure chooses the exact embedded `lifecycle` topic
and its Count selected messages starter rather than copying an untested API snippet.

The two SQL statements use documented relations/functions and current-document scope;
sample expected header/identifier values are checked against `adt-a01.hl7`. Any
SQLite syntax/sample evaluation is a documentation check, not a new QueryHost
security/lifecycle test. Network/GUI procedures have been source-reviewed, not
executed against a live endpoint or reaccepted in native Windows in this stage.

## Verification

Final check results are recorded here after the documentation pass:

- Scanned **127 authored Markdown documents** (root, docs tree and synthetic-sample
  READMEs): **627 local file links and 25 heading-anchor links**, with **zero missing
  targets/anchors**. Fenced/inline code is excluded and duplicate heading slugs are
  handled. This is not an external-URL availability check or a scan of vendored
  license/packaged copies.
- README/user-index navigation and all **17/17 required task guides** passed checks;
  each guide has a numbered procedure. Command routes/focus-specific shortcuts were
  reviewed against source as listed above, not asserted from an old milestone plan.
- **Two SQL examples** passed syntax/sample evaluation with Python SQLite and a
  synthetic HL7_VALUE stub, using expected values checked from the reviewed ADT
  fixture. This does not exercise QueryHost enforcement or constitute a new runtime test.
  The embedded catalog's exact lifecycle topic/title/starter was checked; authored
  FHIR Bundles and profile JSON/XML syntax were checked without changing fixtures.
- `build/WriteScriptApiReference.py --check` passed against the unchanged catalog.
  No second API signature inventory or new C# example was introduced.
- `git diff --check` passed. A separate whitespace/final-newline check included all
  new files, which an unstaged tracked diff alone does not cover. Git emitted only
  its existing checkout-policy LF-to-CRLF notices, with no whitespace errors.
- Protected audit against the retained `artifacts/stage17/protected-before.json`:
  **2,384 expected / 2,384 present; zero changed SHA-256 hashes, zero missing,
  zero added files**. No replacement baseline was generated.
- Final scope is four updated and 20 created documentation files, with an empty
  index and unchanged HEAD. The pre-existing Stage 19A audit, AGENTS and PROJECT_CONTEXT
  hashes match their starting values. No new temporary probe/output files were retained.

No solution tests, restore, Release build, packaging or runtime operation is required
or claimed for this documentation-only change. No production, test or build-tool
source/package/lock file changes were made. Do not reinterpret earlier milestone test
totals as a fresh Stage 19B verification run.

## Remaining review and Stage 19C/19D work

Two known UI wording defects remain intentionally unchanged for Stage 19D:

1. Preferences HL7/Validation explanatory text locates schema application in the
   raw editor; actual actions are in the HL7 Tools ribbon.
2. View → Reports → Generation Report tooltip describes only FHIR, although the
   report tool also supports synthetic HL7 generation.

Stage 19C still needs current developer architecture/build/test/lifecycle/security/
extension-point guides and maintenance authority. Stage 19D still needs approved
general Help resources, native search/navigation integration and its tests/acceptance;
existing Script API Reference/F1 remain unchanged.

Native clipboard/IME, screen-reader, mixed-DPI, floating-window and approved-artifact
walkthroughs require actual Windows review. Loopback instructions assume an available
port and compatible local security policy; these were not exercised here. Existing
profile/mapping/resource limits and reports remain the authoritative behavior if a
particular input is rejected. No new implementation uncertainty justifies changing
production behavior in this stage.

No commit, push, package, publication or Stage 19C/19D work occurred. Stop for user review.
