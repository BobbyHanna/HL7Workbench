# Explicit schema validation and imported profiles

Schema evaluation is separate from tolerant structural parsing. Core owns immutable
declarative profiles/resolution/import, source maps and a bounded read-only evaluator.
Workspace owns local definitions and per-message applied state; Desktop owns inline
decorations/hover/status and **HL7 Tools → Schema Validation** commands.
[ADR 0021](../decisions/0021-explicit-inline-schema-validation.md) and the
[format/coverage reference](../stage17-hl7-schema-validation.md) govern scope.

## Activation and ownership

Opening a message never applies a schema. Preferences select the initial explicit
picker choice only; detection checks declared MSH-9/MSH-12 against scoped built-ins.
Trading-partner profiles require explicit selection. Import/Manage reuse the same
library dialog and do not apply a definition by themselves.

[MessageDocument.Validation](../../src/HL7Workbench.Workspace/MessageDocument.Validation.cs)
keys selections by stable MessageId, with profile lineage signatures and content/
search revisions. Navigation, pending edit parsing, changes/reapplication/removal
cancel outdated work and guard publication. Applied selections/findings are
session-only, not serialized into source, layouts or Save sidecars. Profile update/
removal invalidates dependent signatures and requires explicit current resolution.

Validation tasks are tracked under a synchronization guard so completion and Close
can consistently snapshot/drain predecessors. Dispose must await them before source
release; an evaluator finishing after cancellation must not republish stale marks.
Desktop's selector additionally guards active document/MessageId/revision around
the modal interaction. Tool focus or a same-number message in another document
cannot retarget it.

## Rules, imports and coverage

[Hl7SchemaValidator](../../src/HL7Workbench.Core/Hl7SchemaValidator.cs) evaluates
supported compatibility/order/group/usage/cardinality/datatype/value/conditional
rules over Core's scanner/parser. Exact message-local UTF-16 source spans come from
[Hl7ValidationSourceMap](../../src/HL7Workbench.Core/Hl7ValidationSourceMap.cs), not
searching display strings. Inline marks map through real preview fragments and
exclude omission labels. F8 navigates retained diagnostics even when decoration
count is bounded separately.

[Hl7ValidationCatalog](../../src/HL7Workbench.Core/Hl7ValidationCatalog.cs) contains
seven v2.5.1 triggers across five structures, not all HL7 versions/messages. Base
conditional/terminology/advanced datatype coverage gaps explicitly yield Incomplete.
Unsupported ORM branches are deferred without inventing clinical constraints.
Zero errors is not complete conformance, clinical validation or certification.

[Hl7ProfileImporter](../../src/HL7Workbench.Core/Hl7ProfileImporter.cs) accepts strict
bounded native JSON and a defined conformance XML subset. XML DTD/external resolution
is disabled; unsupported constructs cannot silently become checked rules. No
arbitrary executable predicates, unrestricted regex or runtime URL fetching is exposed.
[Hl7ProfileResolver](../../src/HL7Workbench.Core/Hl7ProfileResolver.cs) enforces
narrowing-only inheritance, bounded lineage and supported explicitly placed Z segments.
Children do not erase inherited coverage notices. Component positions/cardinality
must match supported datatype semantics, not just well-formed JSON.

[ValidationProfileLibrary](../../src/HL7Workbench.Workspace/ValidationProfileLibrary.cs)
stores validated definitions atomically under local configuration. Built-in IDs
cannot be replaced. Reimport checks reviewed/current revisions and resolvable
descendants; stale/external changes fail without adopting arbitrary content.
Format examples and the authoring schema remain at [docs/examples](../examples/).

## Provenance and extension checks

Adapted pinned nHapi metadata is an embedded JSON resource under **MPL-2.0**; the
nHapi runtime/parser and standards prose are not bundled. Retain the
[metadata notice](../../src/HL7Workbench.Core/ValidationCatalog/NOTICE.md), source
digests and [license](../../build/third-party/nHapi-MPL-2.0-LICENSE.txt).
`build/update-validation-catalog.py` is an explicit developer regeneration tool,
not an application download. New versions/rules need an independently reviewed
catalog/import/coverage design, budget checks and provenance review; absence of a
new NuGet package does not remove resource-license obligations.

Do not change generator byte goldens to silence schema findings; compatibility
output has its own version contract and did not promise profile conformance.
Test exact finding ranges/boundaries, inherited restrictions, malicious/oversized
imports, unencodable drafts, incomplete coverage, rapid cancellation/close and
active-target changes across views.

Evidence: [Core evaluation](../../tests/HL7Workbench.Core.Tests/Hl7SchemaValidationTests.cs),
[import rules](../../tests/HL7Workbench.Core.Tests/Hl7ProfileImportTests.cs),
[library](../../tests/HL7Workbench.Workspace.Tests/ValidationProfileLibraryTests.cs),
[lifecycle](../../tests/HL7Workbench.Workspace.Tests/SchemaValidationLifecycleTests.cs)
and [ribbon/layout targeting](../../tests/HL7Workbench.Desktop.Tests/SchemaRibbonTests.cs).
