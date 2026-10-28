# ADR 0020: Isolated, bounded read-only SQLite querying

Status: Stages 15A/15B provisionally accepted; Stage 15C implemented for review.
Stage 15D remains gated. The original engine decision and later amendments are
retained below; this is not a new release.

## Original Stage 15A decision and boundaries

Use SQLite's parser, planner and execution engine, not a custom SQL parser. A new
UI-independent `HL7Workbench.Querying` assembly owns closed relational DTOs, bounded
IPC and a worker client. `HL7Workbench.QueryHost` alone loads SQLitePCLRaw 3.0.5 and
the lock-resolved native SQLite 3.53.4. The thin raw provider exposes authorization,
preparation tails, progress callbacks and SQLite limits without adding a second
ADO.NET abstraction. There is no dependency from Core, Workspace, Desktop, Transport
or Scripting to the new projects in 15A. Tests copy a framework-dependent QueryHost
tree into their own output; no Desktop deployment or portable packaging is added.

Each session starts a fresh worker and private `:memory:` database. It accepts only
typed rows for the closed Documents/Messages/Segments/Fields/Components/Subcomponents
schema; import identifiers are application constants and values are bound parameters.
STRICT tables, primary keys and foreign keys reject inconsistent relational input.
15A supplies synthetic relational fixtures, not a live HL7 importer. Core remains
the only HL7 grammar/path/escape authority for 15B.

The parent assigns a Windows Job Object before sending Initialize. The worker cannot
start SQLite work until that request. The job has 512 MiB process/job committed-memory
limits and kill-on-close. The narrow lifetime implementation mirrors the established
ScriptHost policy without refactoring accepted scripting code. Native SQLite resides
only in QueryHost. SQLite's process-global hard heap limit therefore cannot affect
Desktop or another feature. Other platforms lack equivalent enforced OS memory
containment; Windows is the acceptance platform. This is not a general security sandbox.

## Enforcement

Trusted schema/import setup precedes user SQL. Initialization enables defensive mode,
disables trusted schema, triggers, views and extension loading, sets temp storage to
MEMORY, caps native heap at 128 MiB and database pages at 64 MiB, and verifies those
settings. The worker never opens an external database or accepts a connection string.
The first Execute disposes import statements, enables query_only and permanently
seals import before installing a deny-by-default authorizer.

The authorizer is installed **before** preparing any user statement and remains
installed through stepping/repreparation. Only SELECT/recursive SELECT, reads of
documented columns in the six closed main relations, and an explicit set of pure
built-in functions are permitted. SQLite's empty-column/schema-omitted count(*)
authorization is accepted only for those same closed relations. Implicit rowid,
sqlite_schema, PRAGMA/table-valued PRAGMAs, transaction control, ATTACH/DETACH, DDL,
DML, extension loading and unapproved functions are denied. Virtual table preparation
is also disabled with SQLITE_PREPARE_NO_VTAB. There are no application SQL functions
or user-loadable extensions in 15A.

sqlite3_stmt_readonly and a positive result-column count provide additional checks;
neither replaces the authorizer. SQLite itself consumes statement tails (including
comments and semicolons). A second statement is rejected before the first is stepped.
Named parameters are required exactly once; anonymous/numbered parameters, missing,
extra and duplicate bindings fail. Parameters and import values are length-delimited;
embedded NUL, quotes, newlines and Unicode are preserved without SQL interpolation.

Function names currently allowed are abs, avg, coalesce, count, glob, ifnull, iif,
if, instr, length, like, lower, ltrim, max, min, nullif, replace, round, rtrim,
substr, substring, sum, total, trim, typeof, upper, unicode, char, hex, unhex,
quote, group_concat, string_agg, concat, concat_ws, date, datetime, time, julianday,
strftime, unixepoch, timediff, row_number, rank, dense_rank, percent_rank, cume_dist,
ntile, lag, lead, first_value, last_value and nth_value. Availability still depends
on SQLite's own signatures. printf/format and randomblob/zeroblob are deliberately
not author-approved. Standard SQL operators, joins, aggregates, CTEs, window clauses,
DISTINCT, GROUP BY, ORDER BY and LIMIT use SQLite semantics.

The independent request reader delivers cancellation while SQLite is executing.
A progress callback checks cancellation, the query clock and a VM-work budget.
The parent independently bounds startup, each exchange, the entire query across
pages, and idle/active session lifetime. Cancellation has at most 200 ms attempted
cooperation before kill/reap. The parent terminates failed workers, discards all
locally accumulated pages, and never publishes a partial result. Disposal is
idempotent; a session deadline also reaps idle workers.

Diagnostics are fixed QueryError codes and descriptions. SQLite errmsg, SQL,
parameter names/values, aliases and exception messages are not sent as diagnostics
or logged. stdout/stderr are drained and discarded. SQL and values travel only over
a current-user named pipe, never process arguments or environment variables. DTO
ToString methods omit payloads. No database/results/history are written to disk.

## Hard limits and tradeoffs

Budgets can be lowered per session but not raised beyond these caps:

| Resource | Limit |
| --- | --- |
| SQL | 65,536 UTF-16 units; embedded NUL in SQL is rejected |
| Explicitly materialized text | 65,536 UTF-16 units and 256 KiB UTF-8 |
| Materialized blob / row | 256 KiB / 512 KiB accounted bytes |
| Result | 64 columns; 100,000 rows; 16 MiB accounted headers/scalars/row overhead |
| Import | 250,000 rows; 64 MiB accounted input; 128 rows/request |
| Pipe frame | 2 MiB encoded JSON, length checked before allocation |
| Result transfer | At most 100 rows/page; 512 KiB target, at most one row over target |
| Parameters | 64 named values; ASCII alphanumeric/underscore names with @, : or $ prefix |
| Startup / exchange / query / session | 30 seconds / 30 seconds / 30 seconds / 10 minutes |
| SQLite heap / database pages / worker job | 128 MiB / 64 MiB / 512 MiB |
| SQLite value/row expression limit | 1 MiB native length ceiling |
| Expression depth / compound SELECT terms / function arguments | 32 / 16 / 16 |
| Compiled VM operations / progress budget | 100,000 / 100,000 callbacks at 1,000 instructions |
| Attached DBs / trigger depth / worker threads | 0 / 0 / 0 |
| LIKE pattern | 4,096 bytes |

Accounting budgets are not claims about exact managed-object heap consumption;
the Windows process/job limit is the independent backstop. JSON escaping can hit
the frame cap before an otherwise valid input batch reaches its row cap; callers
must use smaller batches. A result crossing any cap fails completely, including
when earlier pages have already crossed IPC. A SQL LIMIT is an intentional query
restriction, not hidden host truncation. Native heap exhaustion/complexity failures
discard that worker; a new session starts fresh.

The 65,536-character cap is an **explicit materialization budget**, not an HL7
field-size or conformance rule. ValueLength can describe much larger source ranges
while RawValue/DecodedValue are absent with ValueStatus=NotMaterialized. No source
field is truncated or normalized. Before live integration in 15B, referenced-value
planning must ensure that a predicate asking for a large unavailable value fails
explicitly rather than silently treating it as SQL NULL/nonmatching. Metadata-only
queries must not decode large OBX values merely to populate unused columns.

15A intentionally bounds the complete returned result in parent memory. 15B will
adapt successful output to owned journals/paged presentation; no live-source/result
cache is introduced here. Temporary source/result storage in later passes must use
existing snapshot ownership, quotas and drain-before-disposal rules.

## Required decisions carried into later passes

* Use authoritative captured DocumentId/MessageId pairs; message-control IDs,
  patient identifiers and displayed row numbers are never extraction authority.
* Plan document-qualified HL7_VALUE/HL7_RAW/HL7_STATE/HL7_STATUS through existing
  Core paths and decoders. Missing, empty, explicit null and unavailable stay
  distinct. These functions are not implemented in 15A.
* Default Current document scope keeps ordinary FROM Messages queries short.
  Evaluate two-argument HL7 function convenience only when exactly one document
  is pinned, refusing that overload in multi-document scope; the three-argument
  document-qualified form remains authoritative.
* Matching-message extraction is a separate bounded operation over a complete
  identity-selection query, never just the visible page or a partial/truncated
  reporting result. Fail without publication when its own identity/output budget
  is exceeded. Deduplicate and preserve captured source order by default. Explain
  that explicit SQL LIMIT limits the selected set.
* Arbitrary aggregates lacking identity cannot extract. Mixed encodings/unsafe
  framing cannot silently transcode, repair or discard opaque regions. Existing
  exact-byte/index/publication safeguards remain mandatory.
* Save named definitions only on explicit user action with sensitive-literal
  disclosure; history stays session-only. Dock placement contains no SQL or results.

## Evidence and references

Permanent worker/protocol tests live in tests/HL7Workbench.Querying.Tests. See
[15A acceptance](../stage15a-sql-engine-acceptance.md) for the gate and limitations.
Upstream rationale: [authorizer](https://sqlite.org/c3ref/set_authorizer.html),
[statement read-only caveats](https://sqlite.org/c3ref/stmt_readonly.html),
[resource guidance](https://sqlite.org/security.html),
[heap-limit scope](https://sqlite.org/c3ref/hard_heap_limit64.html), and
[SQLitePCLRaw](https://github.com/ericsink/SQLitePCL.raw).

## Stage 15B amendment: captured HL7 and selective values

Workspace now references Querying's managed contracts; Core stays unchanged and
free of UI, files, SQLite and Dock. Desktop gains no SQL UI or worker deployment
in this pass. Test outputs alone copy QueryHost through build/QueryHost.targets.
Protocol version 2 adds prepare-only planning, source-read RPC and column origins.
The native package graph is unchanged; no new library is added in 15B.

### Focused 15A review

The authorizer remains installed before every user preparation, tail preparation
and reprepare/step. Execute disposes trusted import statements, enables/verifies
query_only and seals import permanently. The new Plan phase temporarily installs
the same authorizer **before import**, prepares/finalizes without stepping, discovers
referenced public relations, then restores trusted import. It is forbidden once
sealed; an invalid plan discards the worker. This exception to the original
after-import-only preparation rule cannot execute user SQL or reopen sealed input.
Existing real-worker cancellation, crash/reap, VM/heap/resource tests are retained.
An empty-BLOB binding defect was corrected with sqlite3_bind_zeroblob(...,0), with
a regression asserting that BLOB does not become SQL NULL.

Lazy source functions initially evaluated as views during development failed an
adversarial CTE-name test: the authorizer's inner-context name can be user-controlled.
That implementation was replaced before acceptance. No view/CTE name grants trust.

### Fixed read-only adapters

Source-backed Fields, Components and Subcomponents are three application-owned
SQLite virtual tables. Their private STRICT backing tables contain metadata only.
The SQLitePCLRaw version has no module-registration wrapper, so one narrow C ABI
adapter calls the **same pinned e_sqlite3 library**; no SQL parser/executor is added.
Public table reads use normal authorizer rules. Private backing-table reads are
denied to user SQL, including CTE impersonation and alias attempts. The adapter
prepares constant/parameterized SELECT under a synchronous private capability, with
no untrusted evaluation while that capability is set. Schema cannot change after
sealing. Metadata equality pushdown leaves SQLite's recheck enabled and respects
text collations. xColumn alone requests complete bounded source values over RPC.
xUpdate and write/transaction callbacks are absent. Views/triggers/trusted schema
and loadable extensions remain disabled; user module creation/unknown tables remain
denied. SQLITE_PREPARE_NO_VTAB remains on for non-source 15A sessions; only the
source-backed mode permits preparation against the three closed adapters.

HL7_VALUE/RAW/STATE/STATUS are DIRECTONLY and not INNOCUOUS. They accept captured
DocumentId/MessageId/Core path, never filenames or arbitrary SQL/ranges. The parent
owns source resolution and Core parsing/decoding. Exactly one pinned document also
registers two-argument overloads. Both sides enforce 100,000 source requests, scalar
limits and cancellation/deadlines. All native callbacks catch managed exceptions;
fixed error codes cross the boundary, not source/SQL/native exception messages.

See SQLite's [virtual table interface](https://www.sqlite.org/vtab.html),
[authorizer context semantics](https://www.sqlite.org/c3ref/c_alter_table.html) and
[DIRECTONLY guidance](https://www.sqlite.org/c3ref/c_deterministic.html).

### Capture, indexing and outputs

SearchCapture freezes logical messages, draft strings, encoding and revision on the
document-owning context. A background copy creates owned snapshots under a combined
1 GiB quota, then releases live document leases. Completed results survive later
edits/closure without blocking originals. Up to 16 documents/100,000 messages and
16 Mi UTF-16 units of frozen drafts are accepted. MSH metadata reads stop at the
first terminator; unused OBX values are never decoded/imported for metadata-only SQL.
Structural table references use Core's full shared scanner, with one decoded spool
and structural model cached at a time. No source-backed viewer previews are inputs.

All field text is lazy; ValueLength/ValueState remain queryable for large fields.
Decoded escape diagnostics are explicit errors rather than valid-looking partial
values. Missing/empty/explicit null/unreadable states follow Core. DiagnosticStatus
distinguishes incomplete header assessment from full diagnostics. Generic relations
are preferred to segment-specific convenience views in this pass.

Successful SQL results remain bounded to 100,000 rows/16 MiB accounted memory before
an owned journal (128 MiB encoded cap) supports paged previews and full-row reads.
CSV streams the complete journal via Core encoding and existing atomic publication;
typed companion columns distinguish null/empty and formula guards follow existing
reporting policy. The CSV cap is 256 MiB. Source destinations/aliases/storage are
protected. Journals/snapshots/spools contain sensitive data temporarily; they are not
settings/history or a persistent raw cache. Result disposal cancels and drains
readers before cleanup, using existing abandoned-snapshot recovery after crashes.

Extraction can consume a complete provenance-bearing identity result or run a fresh
identity-selection query independent of a report. It validates every typed pair
against the captured registry, deduplicates and preserves source order. SQL limits
are explicit restrictions; no visible-page or hidden truncation is used. Its separate
100,000-message/1 GiB output caps supplement SQL result caps; overflow publishes
nothing. Byte/framing ranges are copied and reindexed before an independent unsaved
document opens. Mixed encodings, wrappers/unmapped outer regions, ambiguous framing,
interior BOM, unencodable drafts and invalid concatenation are refused conservatively.

### Tradeoffs and remaining gates

Metadata queries still copy bounded source bytes to ensure completed-result
independence. Structural scans are relation-selective, not message-predicate-selective;
full structure of all scoped messages is scanned before SQL execution. SQLite
controls value evaluation order. The one-message cache can rescan on repeated joins;
deadlines bound this cost. Parent structure limits are Core's existing budgets,
not an OS memory sandbox. UTF-16 spools can occupy about twice source bytes; source,
output and journal quotas are separate and can coexist. Windows worker containment
and all 15A native/IPC caps remain mandatory.

See [workflow/functions guide](../sql-hl7-workflows.md) and
[15B acceptance](../stage15b-hl7-query-acceptance.md). Dock/editor/grid interactions,
saved queries and explicit sensitive-query persistence disclosure remain for 15C.

## Stage 15C amendment: docked UI and explicit query storage

Add one optional SQL Query tool, combining editor and results rather than introducing
a second Dock owner/session. It follows lazy registry, release-on-close and saved
placement/visibility conventions; fresh layouts remain unchanged. Visible restoration
creates a clean draft and empty result, never serialized feature content. Main-window
and floating-tool close use explicit cancellation/drain and unsaved-draft decisions.
Reset/restore layouts also confirm before releasing a SQL draft. Shutdown checks
content versions and retains discard consent until all document prompts succeed.

Reuse AvaloniaEdit with a small theme-aware lexical SQL definition; no new editor or
grid dependency. SQLite remains the sole SQL parser/planner/executor. A virtualized
ListBox of dynamic fixed-width grid rows displays at most 100 previews. Selection is
page-local, keyed to journal indices after sorting. Stable whole-result sorting
materializes one bounded column and an int permutation; numeric keys preserve integer
precision, text uses ordinal comparison, and SQLite ORDER BY remains available for
collation/expression semantics. CSV accepts only a complete permutation and retains
existing all-row atomic/source-guarded publication. Extraction is deliberately
independent of presentation order/page/selection and retains the 15B implementation.

SourceForRow validates native column origins and typed captured pairs before exposing
an opaque identity/revision. Desktop navigation further verifies document membership,
revision and the existing identity at the frozen logical position, then uses normal
navigation. Displayed row/control/patient IDs never substitute for source authority.
Explicit navigation or data-cell double-click is required. Completed stale captures
still support outputs, with visible producing-document/draft attribution, while
original-source navigation is disabled. Participating source closure/revision changes
cancel running queries. New runs clear old output so failures cannot inherit success.

Full values are read deliberately within existing materialization limits. Clipboard
selection is bounded to 1 Mi UTF-16 units and refuses overflow rather than copying
abbreviations. SQL paste is checked before editor mutation; deferred overflow rollback
never clears AvaloniaEdit undo state inside a native update transaction. Original
Core parsing/editing/source-byte rules are unchanged. No sensitive operation logging
is added; statuses/errors remain fixed and avoid SQL literals/native text/filenames.

Add Workspace SqlQueryLibrary under the existing local configuration root, using
the same explicit-persistence approach as [ADR 0009](0009-explicit-saved-search-definitions.md).
Only Version/Id/Name/Sql are serialized. Names and SQL may contain sensitive data;
a visible unencrypted-storage disclosure precedes Save, without repetitive modal
consent on each explicit save. GUID paths, strict Unicode and size checks, link
rejection, expected hash revisions, a library lock and FileSaveDestination atomic
publication follow existing libraries. Rename reloads stored SQL and cannot adopt
an unsaved draft; delete confirms and checks revisions. Malformed records are skipped
individually and not rewritten by listing/opening. Limits: 256 records, 512 KiB encoded
per record, 120-character names and the existing 65,536-unit SQL budget.

History is a shared application-session object, bounded to 32 entries/256 Ki SQL
units and never serialized. Unsaved editor text, current selections/scope/result
values/full-value inspection and document content are not added to preferences or
layout. Existing temporary result journals/snapshots follow 15B cleanup; no persistent
result database/cache is added. Closing the tool releases its model/results after
pending operations drain, while session history alone can survive reopening.

Desktop now copies QueryHost to its own subdirectory through the existing build
target. There is no native SQLite dependency in Desktop/Workspace and no package
version change, release packaging or accepted-artifact rebuild. The UI currently
has no named-parameter entry panel; Workspace parameter APIs remain available.
Native Windows docking/DPI/clipboard/monitor behavior still requires owner manual
verification. See [15C acceptance](../stage15c-sql-ui-acceptance.md) and
[user guide](../sql-query-ui.md). Stop before 15D.
