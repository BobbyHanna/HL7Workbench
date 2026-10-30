# Isolated read-only SQL architecture

Querying owns typed contracts/client/protocol; QueryHost alone owns native SQLite.
Workspace captures/interprets HL7 using Core, and Desktop supplies the SQL tool.
[ADR 0020](../decisions/0020-isolated-read-only-sql.md) and its amendments govern
enforcement. The user/schema/function references remain
[SQL tasks](../user/query-with-sql.md), [relations](../sql-query.md) and
[HL7 functions/output rules](../sql-hl7-workflows.md); do not fork another function catalog.

## Capture, plan, execute and publish

1. [SqlQuery](../../src/HL7Workbench.Workspace/SqlQuery.cs) validates source membership,
   captures selected open documents in document order and freezes logical IDs,
   revisions and drafts on the owning context. A canceled capture releases leases.
2. [SqlCapturedSources](../../src/HL7Workbench.Workspace/SqlCapturedSources.cs)
   independently retains bounded authoritative bytes/drafts. After capture, the
   document borrow can end; the worker receives source identities, not file paths.
3. The worker's authorizer-protected prepare-only plan discovers required closed
   relations without stepping user SQL. Workspace selectively imports header/
   structural metadata. Header-only queries avoid decoding unused large OBX values.
4. Execution permanently seals imports. Three fixed field adapters and bounded
   source-value RPC resolve requested values through Core coordinates. Hidden
   metadata backing storage is not made readable by a user alias/CTE name.
5. Only a complete successful bounded result becomes a parent-owned journal.
   SQL failures/cancellation publish no partial replacement. Desktop observes
   source changes/closure and cancels running work; completed captures can remain
   stale-labeled and independently exportable.

The six relations are declared by [QuerySchema](../../src/HL7Workbench.Querying/QuerySchema.cs).
Document-qualified keys and typed imports use bound values, not interpolated DDL.
Missing nodes have no row; raw/state/status/decoded value semantics are separate.
Requested unavailable/over-limit values fail explicitly rather than masquerading
as null/empty/truncated data. Desktop currently has no parameter-entry panel;
Workspace accepts validated typed parameters.

## Enforcement and budgets

[QueryEngine](../../src/HL7Workbench.QueryHost/QueryEngine.cs) creates one private
`:memory:` database per worker. It enables defensive behavior, disables extension
loading/trusted schema/triggers and uses bounded memory/page/temp policies. A
deny-by-default authorizer is installed before any user prepare and remains active
through stepping/repreparation. Only documented relation reads, SELECT/recursive
SELECT and whitelisted functions are allowed; implicit rowid, schema/PRAGMA access,
ATTACH, transactions, DDL/DML and unapproved functions are denied.

SQLite itself parses statement tails. Reject a second statement before stepping
the first; do not add a home-grown SQL parser. Read-only/positive-result-column
checks supplement, not replace, authorization. Planning cannot execute arbitrary
code, and user Execute cannot reopen the import phase.

Native heap/page caps, Windows process/job memory containment, VM progress/time
checks, bounded protocol/scalars/results and Workspace source/output caps are
different limits. A small final WHERE/LIMIT does not mean a small structural import;
substr does not bypass the materialized input scalar bound. See
[shared limits](../reference/limits.md#sql-worker-and-workspace) and
[QueryContracts](../../src/HL7Workbench.Querying/QueryContracts.cs).

[QueryWorkerSession](../../src/HL7Workbench.Querying/QueryWorkerSession.cs) assigns
containment before initialization. StartupTimeout covers pipe connection through
Ready, OperationTimeout applies afterward, and SessionTimeout is absolute lifetime
including startup/active/idle time, not a resettable inactivity timer. Tests should
control the deadline clock instead of arranging a ready worker with a one-second
real lifetime. Protocol deserialization must reject malformed nested objects with
fixed errors; nullable annotations alone do not validate a peer frame.

## Results, complete actions and cleanup

[SqlQueryResults](../../src/HL7Workbench.Workspace/SqlQueryResults.cs) owns a complete
journal plus captures and read leases. Controls receive 100-row abbreviated pages;
complete bounded copy/inspection, stable typed sorting and CSV read the journal.
CSV uses spreadsheet guards/type companions and is not exact typed/raw interchange.
Matching-message extraction reads all authoritative identity rows, deduplicates and
retains captured source order; it rejects unsafe encoding/framing/outer-region joins
rather than silently repairing output. Identity provenance must not be inferred
from a computed alias or displayed ordinal.

Results can outlive source closure, but navigation needs an unchanged open source.
Extraction publishes an independently owned snapshot/document; it is not a child
view whose lifetime ends with the query model. [Fixture cleanup](workspace-lifecycle.md#sql-fixture-cleanup-race)
must drain both model and all document owners before deleting their shared test root.
Result Dispose defers cleanup until outstanding read leases release.

History is bounded session memory; named SQL is explicit unencrypted storage, not
an external database connection. Diagnostics use fixed categories/startup phase
metadata, not SQLite messages, SQL literals or result values. Other processes under
the same account remain outside this resource isolation guarantee.

Evidence: [worker queries](../../tests/HL7Workbench.Querying.Tests/QueryWorkerTests.cs),
[protocol hardening](../../tests/HL7Workbench.Querying.Tests/QueryProtocolTests.cs),
[startup clocks](../../tests/HL7Workbench.Querying.Tests/QueryStartupTests.cs),
[containment/resource checks](../../tests/HL7Workbench.Querying.Tests/QueryContainmentTests.cs),
[captured outputs](../../tests/HL7Workbench.Workspace.Tests/SqlQueryTests.cs),
[Desktop/all-page teardown](../../tests/HL7Workbench.Desktop.Tests/SqlQueryToolTests.cs).
