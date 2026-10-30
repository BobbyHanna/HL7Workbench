# Supported extension boundaries

These are internal source-level seams for reviewed changes, not a public plugin
ABI or dynamic discovery framework. Prefer extending an existing owner/contract
over introducing another parser, tool manager, random provider or persistence layer.
Read [architecture](architecture.md), [lifecycle](workspace-lifecycle.md) and the
feature ADR before modifying a seam.

| Change | Existing seam | Required invariants and coverage |
| --- | --- | --- |
| Add a structural consumer | Core shared scanner/path/escape APIs; Workspace captured source/read leases | Preserve coordinate origins, value states, malformed bytes, budgets and cancel/drain; never parse a bounded preview as complete input. |
| Add a Desktop tool/action | ToolDefinition/registry, shell UiCommand, existing Dock factory/template | Stable ID, correct active/captured target, CanExecute, explicit focus, finite layout, detach/close ownership, placement-only persistence and headless/native checks. |
| Add a script operation/helper | Reviewed Scripting facade plus ScriptHost lifecycle/parent publication | Phase restrictions, bounds, trusted-code privileges and rollback boundaries; update generated catalog classification/signatures/behavior/examples and real-worker tests. |
| Add a library preset/starter | Existing immutable Script Library or catalog starter with stable ID | User copies remain independent; duplicate/edit/save/reopen behavior and migration rules; no parallel signature catalog. |
| Add an analysis workflow | Existing capture/session/result-journal/publication APIs | Stable message identity, complete values versus previews, stale source policy, source-alias export protections and full-result actions across pages. |
| Change SQL relations/functions | Querying typed schema, QueryHost authorizer/field adapters, Workspace Core projection | Review closed imports/protocol/budgets/provenance; deny new access by default, keep engine in worker, test malformed and adversarial statements, update schema/function references. |
| Add profile coverage | Immutable Core catalog/import/resolver/evaluator and existing local library | Narrowing/coverage semantics, exact ranges, budgets/security, attribution, revalidation/cancellation and existing picker/ribbon. Not another profile manager. |
| Add synthetic options/version | Existing deterministic Core engines/shared demographic helpers and Workspace coordinator | Explicit compatibility/version decision, unchanged prior goldens, one operation seed, estimates/limits and atomic history/publication. |
| Add FHIR mapping | Existing bounded importer/projections/plan and publisher | Source-qualified identity, explicit relationships/precision, counted omissions, versioned eligibility, current-preparation checks and no partial output. |
| Extend transport | Transport framing/ACK engine and Workspace prepared/reviewed sessions | No hidden send/retry/fallback; exact reviewed endpoint/source, uncertainty, monotonic bounds and stop/dispose drains; explicit security/compatibility decision for new wire modes. |
| Add settings/formats | Existing neutral DTO/library/versioned publication owner | Bounded schema validation, safe defaults or migration, preserved unknown compatible fields where supported, dirty/conflict handling and no clinical data in layouts. |

Some seams require a coordinated source/protocol/format change; “extension point”
does not mean adding a new operation is already authorized. Preserve original APIs
and limits unless the specific change calls for reviewed compatibility work.
Do not add abstraction/dependency layers solely to make these seams look uniform.

## Not implemented as extension systems

There is no runtime plugin marketplace/loader, general external SQL connector,
IRIS/HealthShare integration module, managed Synthea launcher, generic CLI, arbitrary
schema code hook or unrestricted message-construction facade. Proposed concepts in
the original specification are not supported APIs. TLS or additional schema/FHIR
versions need design/security/provenance work; a disabled enum/menu possibility is
not a working feature.

## Review checklist

1. Name the owner and authoritative source/API/format it extends.
2. Identify source versus working/captured/saved output and every coordinate conversion.
3. Prove budgets, cancellation/stale-result rejection and cleanup with controlled
   gates rather than timing sleeps; retain useful regression tests/fixtures.
4. Keep commands/menu/ribbon focus behavior consistent and user data out of metadata/logs.
5. Update the [feature-to-documentation map](documentation-maintenance.md#feature-to-documentation-ownership),
   authoritative references and appropriate native checklist before acceptance.

Examples of existing regression patterns are
[schema targeting](../../tests/HL7Workbench.Desktop.Tests/SchemaRibbonTests.cs),
[seed injection](../../tests/HL7Workbench.Workspace.Tests/SyntheticSeedTests.cs),
[capture/export leases](../../tests/HL7Workbench.Workspace.Tests/FieldExtractionTests.cs),
[SQL teardown](../../tests/HL7Workbench.Desktop.Tests/SqlQueryToolTests.cs) and
[script inventory](../../tests/HL7Workbench.Workspace.Tests/ScriptApiCatalogTests.cs).
