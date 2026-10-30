# Stage 19C: developer documentation

Date: 2026-10-30. HEAD remains `63ffd0ce4e9afa2815eee27e846e015fb62b438f`
(`CL fix`, after Stage 18 acceptance). Stage 19A/19B documentation was already
present as uncommitted work. This pass preserves that work and adds current
developer/shared references without changing application behavior or starting Stage 19D.

## Deliverables and change scope

Created **19 Markdown files**:

- [Developer index](developer/index.md) and 13 guides:
  [build/test](developer/build-and-test.md), [architecture](developer/architecture.md),
  [parser invariants](developer/parser-invariants.md), [workspace lifecycle](developer/workspace-lifecycle.md),
  [Desktop tools](developer/desktop-tools.md), [scripting](developer/scripting.md),
  [SQL](developer/sql.md), [validation](developer/validation.md),
  [generation/FHIR](developer/generation-and-fhir.md), [transport](developer/transport.md),
  [security/data](developer/security-and-data-handling.md), [extension points](developer/extension-points.md)
  and [documentation maintenance](developer/documentation-maintenance.md).
- [Shared reference index](reference/index.md), [limits](reference/limits.md)
  and [storage/formats](reference/storage-and-formats.md).
- [Historical index](history-index.md) and this verification record.

Updated **four existing documentation files** relative to this stage's starting
working tree: `README.md`, `docs/index.md`, `docs/architecture.md` and
`docs/dependencies.md`. The README/home now route to the developer/shared references;
the old architecture has a historical-context preface, and the dependency inventory
distinguishes adapted MPL-2.0 validation metadata from runtime packages.
Stage 19C delta: **23 documentation paths**. No files were moved or deleted.

Existing Stage 19A/19B reports and all 18 files under `docs/user/` remain unchanged.
Older milestone/incident reports, ADRs, examples, generated API resources, AGENTS,
PROJECT_CONTEXT, production/test/build/config sources and protected artifacts are
preserved. No general Help catalog, renderer, new package or build integration is added.

## Audit findings resolved or clarified

| Finding | Stage 19C outcome |
| --- | --- |
| A06 | Current eight-project dependency/process map, five test projects and separate build-only host deployment edges are documented against project/target XML. |
| A07 | New present-tense architecture is the entry point; the old chronological architecture is explicitly historical. Existing evidence is not rewritten. |
| A09 | History index and documentation authority distinguish checkpoint approvals/totals from current scope. PROJECT_CONTEXT and old reports remain unchanged. |
| A10/A13/A14 | Shared references name layered budgets/units, format versions, capture/publication states and per-tool policies instead of a single advertised capacity. |
| A11 | Adapted nHapi metadata/MPL-2.0 attribution is explicit, separate from permissive package choices and frozen-preview inventories. No legal clearance claimed. |
| A12 | Build/deployment versus approved older artifact provenance remains separate; no package is rebuilt. |
| A16 | Lifecycle guide documents borrowed leases, revision/cancel guards, publication commit points and awaited disposal, with a dedicated SQL teardown race explanation and permanent tests. |
| A17 | Runtime-derived API/catalog authority, stable topic IDs and exact maintenance commands are documented without another signature inventory. |
| A18 | Existing Script API Help is preserved; general offline Help and two UI text fixes remain Stage 19D. |

The user-facing A01–A05 corrections made in Stage 19B remain intact. New developer
guidance does not reintroduce JavaScript/sandbox, SQL-write/external-DB, TLS/retry,
universal schema/FHIR or guaranteed de-identification claims.

## Verification method and results

Commands/examples were checked against `global.json`, central build/package
properties, `.github/workflows/build.yml`, project/target XML, existing opt-in
test attributes and the existing SQL verification instructions. Source/test links
identify implementation evidence; they are not a claim that those tests ran again.
Numeric/format statements were reviewed against the owning definitions and their
validation, including defaults versus hard bounds and Script/Query protocol versions.

The SQL lifecycle explanation follows `OwnedSqlScope` and gated-reader regressions
in the committed Desktop test source. It explicitly retains already-closed
documents and distinguishes initiating Dispose from its asynchronous completion.
No sleep/retry, new cleanup suppression or production change is proposed.

Completed checks:

- **146 authored Markdown documents**, **1,063 local links** and **40 heading-anchor
  links** scanned with code fences/spans excluded and duplicate heading slugs
  handled: **zero missing targets or anchors**. External URLs, vendored notices and
  protected packaged document copies were not scanned as current authored manuals.
- All **13 required developer guides** are linked from the developer index; shared
  index links both references. New guide/reference links include **106 implementation
  links and 55 test links**, all resolving to current files. The architecture, numeric,
  lifecycle and feature claims were additionally reviewed against source as described
  above; file existence alone is not claimed as behavioral verification.
- Architecture verified against project/target XML: **eight production projects,
  ten ordinary compile-reference edges and two build-only host references**. The
  diagram deliberately excludes host-copy edges from runtime assembly references.
- SDK/roll-forward/MTP settings and all **three CI restore/build/test commands**
  match configuration. Focused/explicit/stress procedures match existing test
  classes/attributes/environment gates. These command examples were inspected,
  not executed as fresh test/build results.
- **48 selected numeric constants/defaults** checked against owning definitions;
  accounting semantics reviewed separately. This caught and corrected an initial
  draft's per-report interpretation: 100,000 rows/16 MiB are shared across a run's
  script reports. Defaults versus hard caps, byte/character units and layered limits
  remain explicit.
- Example integrity: no new C#/SQL payload snippet, manually maintained API list,
  synthetic dataset or regenerated fixture. Existing authored examples/catalog/
  signatures remain byte-identical; commands and the compile-dependency diagram
  have configuration checks rather than an invented executable example harness.
- `build/WriteScriptApiReference.py --check` **passed** against the unchanged JSON.
  `git diff --check` **passed**; separate checks of every newly created file found
  no trailing whitespace and confirmed final newlines.
- Protected audit against the original retained
  `artifacts/stage17/protected-before.json`: **2,384 expected / 2,384 present;
  zero changed SHA-256 hashes, zero missing and zero added files**. No replacement
  baseline, artifact regeneration or package write was performed.
- Compared all **652 starting repository files** (tracked plus pre-existing untracked)
  by SHA-256: exactly the four intended documentation updates differ; 19 new
  documentation files were added. All starting user guides, audit/acceptance reports,
  ADRs, examples, production/test/build/config inputs and generated resources remain
  unchanged. HEAD and the empty index are unchanged. No new disposable probe/output
  files were retained; existing ignored evidence was not deleted.

No restore, Release build, solution test suite, native application launch, network
send/listen, catalog regeneration or packaging was run for this documentation-only
stage. No historical suite count is presented as a current result. Checks do not
establish new native Windows/OS-security/legal acceptance.

## Remaining Stage 19D decisions

The Stage 19A [Help recommendation](stage19-documentation-audit.md#in-app-help-recommendation)
remains a proposal. Before implementation, review:

1. **Host/content scope:** native optional Documentation tool, bundled user/shared
   content and navigation into the existing API tool; decide which developer
   material, if any, should ship. Exclude approval/incident history from default product search.
2. **Renderer/resources:** deterministic curated source→bounded content blocks,
   stable topic/asset IDs, faithful code/table/warning rendering and no unapproved
   package/WebView. No user-file loader, embedded script execution or runtime fetch.
3. **Search/lifetime:** literal local documentation-only search, bounded query/history/
   results, placement-only layout data, no retained document/source references or
   patient/query/script-data indexing. Preserve API topic/starter IDs and Script Editor F1.
4. **Accessibility/native acceptance:** keyboard navigation/focus, selectable/copyable
   code, themes, narrow/wide/high-DPI, float/redock/reopen/reset and no-internet tests.
   Code integration will require applicable restores/build/focused/full tests and
   native Windows review; these prose checks do not substitute for that gate.
5. **Two UI wording fixes:** Preferences still locates schema actions in the raw
   editor; Generation Report's View-menu tooltip still describes only FHIR.
   Neither production string was changed in Stage 19C.
6. **External links, screenshot/language and distribution scope:** decide explicit
   external opening policy and any approved synthetic screenshots/localization.
   Bundling Help does not itself approve packaging/publishing a new release or
   modifying protected preview binaries/documents/notices.

No implementation beyond documentation, commit, push, package or publication was
performed. Stop for user review before Stage 19D.
