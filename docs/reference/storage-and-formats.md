# Storage, formats and compatibility

The default configuration root is `%LOCALAPPDATA%\HL7Workbench`; the public product
name does not rename existing technical identifiers. MainWindow may receive a
different configuration directory. Source/original snapshots, drafts, saved output
and captured output are distinct owners/states, not alternate names for one file.
See [user retention matrix](../user/privacy-and-storage.md#storage-and-retention)
and [developer lifecycle](../developer/workspace-lifecycle.md).

## Persisted application metadata and definitions

| Record | Default location / format | Compatibility and sensitivity |
| --- | --- | --- |
| Editor/application preferences | `editor-preferences.json`, bounded 4 KiB | General/theme/schema-default/SearchLayout metadata. Valid unknown fields are retained; invalid enums/split data use documented defaults. Missing record does not cause an implicit write. No message contents. |
| Script code font | `script-editor-preferences.json`, bounded 4 KiB | Current version 2 stores point size; version 1 pixel-size input is handled by the existing migration reader. Separate from app theme/layout. |
| Workspace layout | `workspace-layout.json`, bounded 256 KiB | Reader accepts versions 1/2; application-owned IDs/groups/visibility/geometry/current session placement. No file reopening, script/query text, patient results or network authority. |
| User scripts | `Scripts/<guid>.json`, current library version 3 | Name/source/ID/migration status, not script kind. Obsolete v1/2 records remain on disk/reviewable until explicit rewrite/save; trusted source/literals can be sensitive. |
| Saved searches | `Searches/<guid>.json`, schema/query version 2 | Nested ordered condition tree. Version-1 flat definitions load compatibly; listing/loading does not rewrite them. Names/literals are unencrypted user content. |
| Saved SQL | `SqlQueries/<guid>.json`, version 1 | ID/name/SQL only; no results/document paths/history. Names/literals can be sensitive. 256 records / 512 KiB each, with SQL/string constraints. |
| Extraction configurations | `Extractions/<guid>.json`, version 1 | Ordered column paths/labels, matching/decoding/filter/sort preferences. No filter literal, captured values or source selection. |
| Imported validation profiles | `ValidationProfiles/<hashed-profile-id>.json`, storage version 1 | ImportedAt, bounded source-file label and canonical native-profile JSON bytes. Native profile formatVersion 1; IDs/definitions/constraints can contain user text. Built-in reserved IDs cannot be replaced. |
| Endpoint profiles | `transport-endpoints.json`, version 1, bounded 64 KiB | Whitelisted endpoint/security/encoding/timeouts/notes only; no payload, credentials, live connection or start authority. Never put secrets/PHI in notes. |

Definitions generally use explicit save/import, bounded validation, revision/conflict
checks and owned staging. Library-specific policies differ: cooperating lock files
exist in script/search/extraction/SQL libraries; do not assume a universal cross-
process transaction/merge policy for every store. Corrupt/unsupported records are
retained/skipped or cause fixed recovery feedback according to their reader, never
silently rewritten/deleted during a listing operation.

Owners: [EditorPreferences](../../src/HL7Workbench.Workspace/EditorPreferences.cs),
[code font](../../src/HL7Workbench.Workspace/ScriptEditorPreferences.cs),
[WorkspaceLayout](../../src/HL7Workbench.Workspace/WorkspaceLayout.cs),
[ScriptLibrary](../../src/HL7Workbench.Workspace/ScriptLibrary.cs),
[SearchLibrary](../../src/HL7Workbench.Workspace/SearchLibrary.cs),
[SqlQueryLibrary](../../src/HL7Workbench.Workspace/SqlQueryLibrary.cs),
[ExtractionLibrary](../../src/HL7Workbench.Workspace/ExtractionLibrary.cs),
[ValidationProfileLibrary](../../src/HL7Workbench.Workspace/ValidationProfileLibrary.cs),
[TransportProfiles](../../src/HL7Workbench.Workspace/TransportProfiles.cs).

## Temporary and session-owned content

SourceSnapshot creates GUID directories under a per-user-derived temporary root
or explicitly supplied StorageRoot, with `source.bin` and an exclusive `lease`.
Decoded spools use owned delete-on-close files. Search borrows originals; SQL and
other completed outputs can own independent captures/result journals. These files
may contain sensitive plaintext, regardless of whether a persistent patient database
exists. Close/replacement drains readers/work before best-effort cleanup; old
lease-free abandoned storage can be removed on a later bounded cleanup pass.

Do not serialize imported FHIR sessions, applied schemas/findings, comparison
drafts, extraction/SQL result rows, active connections or run authorizations into
layout metadata. SQL history is bounded memory retained through tool reopen until
app exit, not saved definitions. Generation/transformation reports belong to their
produced documents; later edits do not re-audit their original output. Activity Log
is bounded session metadata, not a durable clinical audit trail.

Original source-file identity/path can be retained in memory for publication guards;
this is not permission to put it in layout or fixed activity diagnostics. Explicitly
opened payload/result documents own independent lifetimes, not just borrowed view state.

## Wire and generated-reference formats

| Format | Current version/authority | Boundary |
| --- | --- | --- |
| Script definition/worker pipe | API/protocol 2 | Internal bounded frames; obsolete library format and execution API version are different concepts. |
| Query pipe | Version 2 | Closed typed schema and bounded source RPC; malformed nested/null peer objects require actual validation. |
| Script reference input | Reference 1 / API 2 | Reviewed catalog embedded verbatim; runtime-derived signatures/classification checked against the same Scripting assembly. |
| Exported API catalog | ContractVersion 1 plus reference/API versions | Machine-readable documentation, not a script execution/persistence protocol. |
| Synthetic output | Standalone placeholder v1 / realistic v2; scenario v2 | Exact replay depends on all normalized settings/style/version/base time and resolved uint seed. No implicit migration of old style-less options. |
| FHIR mapping/report | Mapping version 1 | Partial supported R4→HL7 v2.5.1 projection. FHIR seed affects transport IDs, not imported demographics. |

See [ScriptProtocol](../../src/HL7Workbench.Scripting/ScriptProtocol.cs),
[QueryProtocol](../../src/HL7Workbench.Querying/QueryProtocol.cs),
[reference loader](../../src/HL7Workbench.Workspace/ScriptApiReference.cs),
[synthetic contracts](../stage16-synthetic-generation.md) and [FHIR mapping](../fhir-to-hl7v2-mapping.md).
Do not use a transport version as permission to ignore catalog/library migration.

## Explicit files and publication

HL7 saves retain original encoding/BOM/framing/untouched regions and validate edited/
inserted pieces. CSV outputs are purpose-specific presentations: Core escaping/
spreadsheet guards and state/type companions preserve explicit interpretation,
not exact original HL7 bytes or guaranteed spreadsheet type round-trip. Different
report schemas have their own versions/column order and sensitivity policies.

The [FileSaveDestination](../../src/HL7Workbench.Workspace/FileSaveDestination.cs)
path-only API allows creation, not implicit replacement. Replacement uses captured
consent/version/physical identity with flushed same-directory staging and supported
atomic replace; there is no destructive copy/delete fallback. Read-only exports
reject protected source/storage aliases. External uncooperative final name swaps
remain a filesystem limitation, not a stronger portable CAS promise.

See [save/export safety](../save-and-export-safety.md),
[structural publication ADR](../decisions/0011-structural-editing-foundation.md),
[privacy boundary](../developer/security-and-data-handling.md) and
[limits](limits.md). User-selected exports persist independently of temporary cleanup;
no secure erasure, encryption or automatic recovery of clinical contents is implied.
