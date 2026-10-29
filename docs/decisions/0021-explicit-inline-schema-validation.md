# ADR 0021: Explicit, bounded inline schema validation

Status: Stage 17 implementation authorized. The owner reports the Stage 17F ribbon
and editor layout working in a native Windows launch; detailed native acceptance
coverage is recorded in the Stage 17 guide.

Core retains the existing lossless parser/scanner as the only ER7 grammar and adds
immutable declarative schema profiles, a bounded evaluator, exact UTF-16 source maps,
strict JSON/XML import and narrowing-only inheritance. Attributed pinned v2.5.1
nHapi model metadata is isolated under MPL-2.0; no nHapi runtime, new package or
standard prose bundle is introduced. Coverage gaps, parsing ambiguity and exhausted
budgets explicitly produce Incomplete. No unconditional compliance claim is made.

Workspace owns the reusable atomic local definition library and session-only
per-MessageId application. Default preferences select an initial picker choice;
opening content never activates validation. Applied profile revisions, stable message
identity and content revisions guard publication. Existing edit debounce and work/
scan leases are reused; disposal drains validation before releasing borrowed sources.
Profile removal/update clears findings and requires explicit resolution. Source,
history, saving and materialization limits remain authoritative and unchanged.

Desktop places selection/library actions in HL7 Tools → Schema Validation and
retains only compact read-only validation status below the raw editor,
view-owned AvaloniaEdit squiggles/boundary marks, shared hover and F8 navigation.
Preview mappings exclude omission markers; no new results tool/Dock identity is
created. Placement is the only layout persistence. No automatic transmission,
arbitrary profile code, unrestricted regex or network resolution is supported.

See [scope, provenance, formats, limits and acceptance](../stage17-hl7-schema-validation.md).
No commit, push, publish, packaging or Stage 18 is authorized by this decision.
