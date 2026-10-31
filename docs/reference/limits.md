# Limits, units and coverage

Values below describe the audited source baseline, not universal file/throughput
claims. A character means a **UTF-16 code unit** unless stated otherwise; bytes,
characters, nodes, rows, logical messages and display decorations are different
budgets. KiB/MiB/GiB mean powers of 1024. Defaults are not automatically hard caps.
Review the linked owner and its validation whenever a value changes.

## Parser, snapshots and editor

| Owner | Current value | Meaning |
| --- | --- | --- |
| Hl7ParseOptions | Default 16 MiB input bytes / 8 Mi characters / 1 Mi characters per segment | Materialized per-message Core options, not the Desktop edit limit or global file capacity. |
| Hl7ParseOptions | Default 100,000 segments / 250,000 elements / 10,000 per container | Structural allocation budgets. |
| Hl7ParseOptions | Default 1,024 expansion / 1,024 diagnostics | Per controlled-edit padding and retained diagnostics. Saturated diagnostics do not remove safety flags. |
| SourceSnapshot | Maximum 16 GiB capture | Independent disk-space/index/tool budgets still apply. |
| MessageFileIndex | Maximum 100,000 indexed messages / 200,000 source regions | Complete retained byte partition, not one huge editor string. |
| MessagePresentation/editor | Complete editor 1,048,576 characters / line 16,384 | Large messages use bounded source-backed presentation. Raw drafts can exceed safe line/structure limits while retained; parsing/save must fail safely until corrected. |
| MessageDocument editing | 16,777,216 retained characters / 4,096 history entries per document | 32 MiB UTF-16 character storage plus additional bounded models/metadata/bytes; not total process memory. |
| DecodedMessageSource | Read window cap 128 Ki characters | Bounded access to a strict spool with explicit original-byte checkpoints. |

Owners: [Hl7ParseOptions](../../src/HL7Workbench.Core/Hl7ParseOptions.cs),
[SourceSnapshot](../../src/HL7Workbench.Workspace/SourceSnapshot.cs),
[MessageFileIndex](../../src/HL7Workbench.Workspace/MessageFileIndex.cs),
[MessagePresentation](../../src/HL7Workbench.Workspace/MessagePresentation.cs),
[editing](../../src/HL7Workbench.Workspace/MessageDocument.Editing.cs),
[decoded source](../../src/HL7Workbench.Workspace/DecodedMessageSource.cs).
Core source-backed scanner and literal-decoder bounds also apply; use
[parser invariants](../developer/parser-invariants.md) before converting ranges.

## Search, comparison and extraction

| Feature | Current bounds | Interpretation |
| --- | --- | --- |
| Find Messages | Term 1,024 characters; Advanced 16 conditions/16 groups/depth four; pages 200 | Literal comparisons, no regex/indexed occurrence UI. One retained row per matching logical message, at most the file message ceiling. |
| Pasted comparison | Complete-editor message/line limits | One message per side. Document-backed large sources use bounded windows; pasted text is session-only. |
| Extraction | 1–32 columns; defaults/hard caps 1,000,000 rows / 4,096 expanded rows per message | Ordinal alignment, not clinical groups/Cartesian combinations. Inspect explicit truncated/limit summary. |
| Extraction values/journal | 65,536 characters per value; 16 MiB encoded row; 1 GiB journal; 4 GiB export | These are distinct ceilings. A value/diagnostic state is not a silently empty original. |
| Extraction presentation | Pages 100; preview 256 characters; distinct tracking 1,024; sort cache 4 MiB | Page/preview limits do not restrict complete applied-result CSV to the visible cells. |

Owners: [search](../../src/HL7Workbench.Workspace/MessageSearch.cs),
[advanced conditions](../../src/HL7Workbench.Workspace/MessageSearchQuery.Advanced.cs),
[comparison input](../../src/HL7Workbench.Workspace/ComparisonInput.cs),
[extraction options](../../src/HL7Workbench.Workspace/ExtractionModels.cs).
Some extraction row/expansion limits retain bounded results with an explicit
truncated summary; do not equate that policy with SQL/generation's no-partial-result
failure policy. See [extraction semantics](../field-extraction.md).

## SQL worker and Workspace

| Owner | Current limit/default | Scope |
| --- | --- | --- |
| QueryLimits | SQL 65,536 characters; 64 columns / 64 parameters | Parameters are API bindings, not a Desktop entry panel. |
| QueryLimits | Text scalar 65,536 characters; scalar bytes 256 KiB; row 512 KiB; frame 2 MiB | Materialization/transport, not source-field length. |
| QueryLimits / QueryBudgets | Maximum 250,000 imported rows / 64 MiB accounted import; 100,000 result rows / 16 MiB accounted result | Configured budgets can be lowered. SQL WHERE/LIMIT does not bypass needed structural import. |
| QueryLimits | Native heap 128 MiB; database pages 64 MiB; worker Windows process/job 512 MiB | Different enforced layers, not a single parent-memory limit. |
| QueryBudgets | Query 30,000 ms / 100,000 progress callbacks maximum | Runtime/interruption budget, separate from startup/pipe lifetime. |
| QueryWorkerOptions | Default and maximum 30 s StartupTimeout / 30 s OperationTimeout / 10 min SessionTimeout | Startup covers connection through Ready; session is absolute, includes startup and never resets on activity. |
| QueryLimits | Page ≤100 rows / 512 KiB; ≤100,000 source reads | Value RPC and complete result transfer remain bounded. |
| SqlQueryOptions | ≤16 documents / 1 GiB captured source / 1 GiB matching output / 256 MiB CSV / 100,000 matching messages / 10 min total | Workspace bounds supplement worker bounds; output must also pass exact-byte/framing checks. |

Owners: [QueryContracts](../../src/HL7Workbench.Querying/QueryContracts.cs),
[SqlQueryModels](../../src/HL7Workbench.Workspace/SqlQueryModels.cs),
[QueryEngine](../../src/HL7Workbench.QueryHost/QueryEngine.cs).
[Practical query limitations](../sql-query-ui.md) and
[SQL architecture](../developer/sql.md) distinguish complete results from previews.

## Scripting and API help

| Owner | Current value | Meaning |
| --- | --- | --- |
| ScriptLimits | Source 65,536 characters; message 1,048,576 characters / line 16,384 / encoded bytes 8 MiB | Facade/worker materialization bounds, not arbitrary trusted C# allocation accounting. |
| ScriptLimits | Batch 100,000 messages; deferred 128 messages / 16 MiB | Deferred storage separate from input/output journals. |
| ScriptLimits | Eight reports; 64 columns/report; 16,384 characters/cell; 100,000 rows and 16 MiB across the run's reports; 256 KiB/operation | Row/byte budgets are shared across reports, not multiplied by report count. |
| ScriptWorkerOptions | Defaults startup 30 s / compilation 60 s / message 10 s; memory 768 MiB | Timeout values are defaults; validation requires positive values. Configurable memory is 128 MiB–2 GiB, with platform-specific containment. |
| ScriptProtocol | Maximum frame 12 MiB; version 2 | Internal protocol, not a supported script authoring API. |
| API reference UI | Query ≤256 characters; navigation history 64 | Literal bounded help search; no script execution or message-data search. |

Owners: [ScriptContracts](../../src/HL7Workbench.Scripting/ScriptContracts.cs),
[ScriptBatch accounting](../../src/HL7Workbench.Scripting/ScriptBatch.cs),
[parent report accounting](../../src/HL7Workbench.Workspace/ScriptReports.cs),
[ScriptProtocol](../../src/HL7Workbench.Scripting/ScriptProtocol.cs),
[reference model](../../src/HL7Workbench.Desktop/ViewModels/ScriptApiReferenceViewModel.cs).
Exact public API/phase signatures remain [generated](../script-api-reference.md).

## Generation, FHIR and schema validation

| Layer | Current scope/ceiling | Owner |
| --- | --- | --- |
| Synthetic/FHIR generated batch | 1,000 messages / 4 Mi characters, plus per-message/line and document/history limits | [SyntheticMessageGenerator](../../src/HL7Workbench.Core/SyntheticMessageGenerator.cs), [FHIR plan](../../src/HL7Workbench.Core/FhirGeneration.cs) |
| Synthetic seed | uint 0–4,294,967,295; blank UI resolves once/operation | [Coordinator](../../src/HL7Workbench.Workspace/SyntheticGeneration.cs); explicit settings/style/version/time required for replay. |
| FHIR import defaults/hard caps | 64 files, 16 MiB/file, 64 MiB total, 50,000 resources, 2,048 patients | [FhirImportLimits](../../src/HL7Workbench.Core/FhirImportModels.cs) |
| FHIR projections | String 65,536 characters; 8 Mi projected characters; 2,000,000 JSON nodes; 200,000 references; 100,000 timeline events; depth 32 | Same import limits; local supported R4 resources/references only. |
| Profile imports/evaluation | 1 MiB profile, nesting 16, 10,000 rules, 128 imported profiles, 1,000 diagnostics, 4,096 inspected value characters, 5 s evaluation | [Hl7ValidationLimits](../../src/HL7Workbench.Core/Hl7ValidationProfile.cs); limits/unsupported checks remain explicit. |
| Applied schemas/library | 128 selected messages/document; 16 MiB imported library | [Document validation](../../src/HL7Workbench.Workspace/MessageDocument.Validation.cs), [library](../../src/HL7Workbench.Workspace/ValidationProfileLibrary.cs) |

Only scoped v2.5.1 built-ins/custom profiles are supported. Base coverage remains
Incomplete even with no errors; FHIR mapping is partial/one-way. Neither generation
nor validation promises clinical or universal interface conformance.

## Transport and testing

| Owner | Current value | Meaning |
| --- | --- | --- |
| MllpFrame | Maximum 1 MiB payload | Strict framing bytes/encoding eligibility; not a whole-document send. |
| ListenerOptions | Defaults four clients / 64 receipts / 16 MiB retained payload | Separate validation ceilings allow at most 16 clients / 200 receipts / 32 MiB; do not mistake configurable maxima for shipped defaults. |
| BatchTransmissionOptions | 1,000 messages / 32 MiB captured payload; default interval 1,000 ms, allowed 250–60,000 ms | Sequential, fresh connection/attempt; ACK policy and cancellation remain separate. |
| LoadTestingOptions | Default concurrency 1 / 1 attempt per second / 60 s / 60 attempts | Replay off by default. |
| LoadTestingOptions | Maximum concurrency eight / 20 attempts per second / 300 s / 5,000 attempts / 1,000 retained completion rows | Global admission rate; exact aggregates and bounded phase samples separate from detailed row retention. |

Owners: [MllpFraming](../../src/HL7Workbench.Transport/MllpFraming.cs),
[listener](../../src/HL7Workbench.Transport/MllpListener.cs),
[batch](../../src/HL7Workbench.Workspace/BatchTransmission.cs),
[load](../../src/HL7Workbench.Workspace/LoadTesting.cs).
Load safety thresholds can be tightened within validation, not disabled/loosened.
No TLS, automatic retry/reconnect/scheduling or receiver-safety guarantee follows
from any resource cap. [Storage-format bounds](storage-and-formats.md) are additional.

## Bulk field operations

[BulkFieldOptions](../../src/HL7Workbench.Workspace/BulkFieldModels.cs) owns separate
hard ceilings (tests may lower them): 1 GiB captured complete source and 1 GiB output;
100,000 scoped messages; 250,000 selected address slots and preview rows; 4,096 distinct
values; 65,536 characters per complete analyzed value; 2,097,152 retained value
characters including authored Find/Replacement. Preview pages contain 100 rows and
abbreviate at 256 characters; complete bounded values remain authoritative.

Mutation additionally uses existing 1,048,576-character complete-message and
16,384-character line/replacement limits, Core structural budgets and 16,777,216
reserved overlay characters (32 MiB of character storage). Metadata, encoded buffers,
scanner models and snapshots are additional bounded overhead. A hard analysis limit
fails without partial results; an invalid proposed message blocks the complete
Apply. Out-of-scope large messages stream unchanged. No UI loosening, truncation or
new large-message editing mode is implied.
