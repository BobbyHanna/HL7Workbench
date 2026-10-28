# Stage 15B — HL7 relational model and output workflows

Historical status: the owner provisionally accepted 15B and authorized Stage 15C.
The original 15B implementation/verification is retained below; see
[15C acceptance](stage15c-sql-ui-acceptance.md) for the UI/storage amendment.
No commit, push, release packaging or accepted-beta modification is authorized.

## Implemented boundary and decisions

Workspace captures live logical messages/revisions/drafts on the owning context,
streams independent source snapshots in the background, then releases live leases.
Completed results do not block document close or follow subsequent edits. Core
remains unchanged and is the only HL7 grammar/path/escape authority. Workspace
references managed Querying contracts, never SQLite or Dock. QueryHost owns the
native engine; no new dependency/version was added in this pass.

An authorizer-protected SQLite prepare-only plan discovers referenced relations
before import. It never steps or reopens sealed import. Metadata-only queries
decode bounded MSH headers without decoding/importing unused OBX payloads.
Structural references import coordinates/states/raw lengths from Core, with one
decoded spool/model cache. Three fixed read-only virtual adapters resolve selected
RawValue/DecodedValue/ValueStatus columns over bounded parent RPC. Their private
metadata backing tables are not accessible to user SQL. Public document-qualified
HL7_VALUE/RAW/STATE/STATUS functions reuse Core paths; two-argument convenience
overloads exist only for one pinned document. No alternate HL7 syntax is introduced.

The focused 15A audit retained pre-prepare/reprepare authorizer installation,
permanent Execute sealing, cooperative cancellation plus kill/reap, crash cleanup,
heap/job/native/IPC budgets and complete-or-failure result transfer. It fixed empty
BLOB binding becoming SQL NULL. During 15B adversarial testing, a proposed lazy-view
implementation trusted an authorizer inner-context name that a CTE could impersonate.
It was replaced with the fixed adapter/capability design. Permanent regressions
include direct hidden-table, alias, CTE, module, PRAGMA and file-function attempts.
Trusted schema, views/triggers/extensions remain disabled; no user write callback
or module registration is exposed. See [ADR 0020](decisions/0020-isolated-read-only-sql.md).

Successful complete results become temporary owned journals. Paged previews and
deliberate full-row reads are bounded. CSV streams all rows through Core quoting,
encoding and formula protection with type companions for null/empty distinctions;
existing atomic destination consent and source guards apply. Extraction consumes
all validated identity pairs from a complete result or a separate bounded identity
query. It deduplicates, preserves source order and original bytes/framing, validates
the assembled output with the file indexer and opens an independent unsaved document.
No limit failure publishes a partial result/file/document. Originals stay unchanged.

## Public workflow and schema

See [SQL schema/examples](sql-query.md) and [functions/output workflows](sql-hl7-workflows.md)
for complete columns, states, path signatures and practical ADT/patient/count/OBX/
missing-PID examples. Relevant entry points are:

* `SqlQuery.RunAsync(string, IReadOnlyList<MessageDocument>?, IReadOnlyList<QueryParameter>?, IProgress<SqlQueryProgress>?, CancellationToken)`.
* `SqlQuery.OpenMatchingMessagesAsync(...)` runs a fresh identity query and extraction.
* `SqlQueryResults.PageAsync(int, CancellationToken)`, `ReadRow(int)`,
  `ExportCsvAsync(string|FileSaveDestination, CancellationToken)` and
  `OpenMatchingMessagesAsync(DocumentManager, CancellationToken)`.
* `CanIdentifyMessages`, `IsCurrent`, `Metrics`, `Duration`, `RowCount`, `Columns`
  and `Sources` support later UI without exposing native engine types.

## Automated verification

The permanent 25-case Workspace suite covers 1,500-message aggregation, a 2 MiB
OBX metadata query with zero body decoding, repeated OBX/field/component/subcomponent
joins, custom delimiters/escapes, missing/empty/null states, malformed lines,
unresolved escapes and undecodable body bytes. It covers duplicate control/patient
IDs across documents, pinned current-document functions, collation-sensitive lazy
constraints, large requested-value failure, exact UTF-8/UTF-16LE/BE extraction,
MLLP/malformed bytes, source-order deduplication, all 130 messages across result
pages, edited/closed source independence, unsafe wrapper/encoding/termination
refusal, full-value CSV quoting/Unicode/newlines/types/formula guards, protected
destinations, snapshot/output/CSV/matching/result caps, cancellation during capture
and indexing, storage cleanup and absence of sensitive identifiers in Activity Log.

The SQL engine/protocol suite has 98 cases (76 retained from 15A, 22 added), including
plan/seal lifecycle, lazy-mode bypasses, empty BLOB preservation and cancellation
while waiting for source RPC. Existing native-heap exhaustion, import/query cancel,
worker crash/reap, time/VM/row/byte/materialization tests remain intact.

Final gate on 2026-10-27:

| Check | Result |
| --- | --- |
| Normal and isolated locked solution restores | Passed |
| Isolated Release solution build, warnings as errors | Passed, 0 warnings / 0 errors, 2.07s |
| Focused Workspace SQL tests | 25 passed, 0 failed/skipped, 21.357s |
| Focused complete SQL engine/protocol suite | 98 passed, 0 failed/skipped, 38.198s |
| Full Core/Desktop/Querying/Scripting/Workspace regression | 2,096 passed, 0 failed/skipped, 6m 25.481s |
| Runtime copies | All five test DLL hashes match the fresh isolated Release outputs |
| Accepted preview inventory | All 2,384 files retained byte-identically; no added/removed files |
| Tracked/new-file whitespace | Clean |
| Git | 13 modified tracked / 31 new files across uncommitted 15A+15B; index empty |

The 15B delta is 47 added test cases (22 engine/protocol, 25 Workspace), 21 paths
newly changed relative to the 15A checkpoint, and updates to 13 of its already
changed paths. This is not a claim that all 44 working-tree paths originated in 15B.
No existing test assertions were weakened. A new unsafe-join fixture was corrected
to place an unterminated source before another document in actual source order.
A new invalid-byte fixture was corrected to avoid an accidental interior BOM;
the original refusal was the expected framing safeguard, not lossy decoding.
Initial failures and the view/CTE regression evidence are retained for review.

Verification uses telemetry opt-outs set to 1:

```powershell
dotnet restore HL7Workbench.sln --locked-mode --artifacts-path C:/Dev/HL7Workbench/artifacts/stage15b/verification
dotnet build HL7Workbench.sln -c Release --no-restore -warnaserror --artifacts-path C:/Dev/HL7Workbench/artifacts/stage15b/verification -p:_WorkbenchWorkerBuildRoot=C:/Dev/HL7Workbench/artifacts/stage15b/verification/bin/HL7Workbench.ScriptHost/release/ -p:_QueryHostBuildRoot=C:/Dev/HL7Workbench/artifacts/stage15b/verification/bin/HL7Workbench.QueryHost/release/
dotnet test --test-modules 'tests/*/bin/Stage15B/net10.0/*.Tests.dll' --root-directory C:/Dev/HL7Workbench --max-parallel-test-modules 1 --minimum-expected-tests 2096
git diff --check
```

The running application locks standard Release outputs. The new build therefore
uses an isolated artifacts path. Byte-identical fresh test outputs are copied into
owned ignored tests/<project>/bin/Stage15B/net10.0 directories, and Desktop outputs
into src/HL7Workbench.Desktop/bin/Stage15B/net10.0, to preserve existing branding
tests' fixed source-root/configuration assumptions. Runtime DLL hashes are recorded;
no accepted beta or test assertion changes to accommodate this arrangement.
Test modules run sequentially, as in the 15A gate, to avoid timing contention with
native-memory exhaustion tests. Required host execution permission was used for
restore/build/worker tests; no containment guard was disabled.

## Resource limits and known limitations

Full limits are in the workflow guide and ADR. Defaults: 16 documents, 100,000 scoped
messages, 1 GiB captured bytes, 16 Mi UTF-16 frozen drafts, one decoded spool/model,
250,000 imported rows/64 MiB, 100,000 source reads, 65,536 materialized UTF-16 units,
256 KiB scalar/512 KiB row, 64 columns, 100,000 rows/16 MiB accounted result,
128 MiB encoded journal, 100,000 extracted messages/1 GiB output, 256 MiB CSV,
30s SQL/exchange and 10m whole operation/session. Worker limits remain 128 MiB
SQLite heap, 64 MiB database pages and 512 MiB Windows job. Options only lower caps.

The scalar cap is a materialization budget, never a field-size assumption. Explicit
SQL LIMIT intentionally restricts matches. Separate extraction is still subject to
SQL result caps and its own output caps, not an unlimited bypass. Aggregates and
computed/display identities without provenance cannot authorize extraction. Every
returned identity pair must be in the captured registry.

Metadata queries still copy source bytes for independence. Structural queries scan
all scoped messages before execution; no per-message predicate pushdown or persistent
index exists. SQLite chooses lazy value evaluation order. Repeated uncached path reads
can rescan and hit deadlines. Result transfer is bounded in memory before journaling.
Parent Core structures have existing bounded element budgets, not OS job containment.
Snapshots/spools/journals may contain sensitive data temporarily; decoded spools can
use about twice captured bytes and quotas can coexist. Crash leftovers use existing
abandoned-snapshot cleanup; neither secure deletion nor a security sandbox is claimed.

Extraction conservatively refuses mixed encodings, batch/file wrappers/unmapped
outer regions, ambiguous framing/interior BOM, unencodable drafts or unsafe joins.
It never silently transcodes, repairs, drops regions or substitutes preview text.
CSV follows the established guarded-text policy rather than an exact unguarded data
dump. Convenience segment views, result-grid sorting/copy/full-value UX, saved queries,
Desktop worker deployment and native SQL UI acceptance remain for 15C/later review.

## Files changed in this pass

* QueryHost: Program.cs, QueryEngine.cs, new QueryEngine.Sources.cs and
  QueryEngine.FieldModule.cs for planning, source RPC and fixed lazy adapters.
* Querying: QueryContracts.cs, QueryProtocol.cs, QuerySchema.cs and
  QueryWorkerSession.cs for versioned plan/source/provenance contracts and bounds.
* Workspace: new MessageDocument.Sql.cs, SqlCapturedSources.cs, SqlQuery.cs,
  SqlQueryModels.cs, SqlQueryResults.cs and SqlQueryResults.Extraction.cs;
  HL7Workbench.Workspace.csproj adds the managed contract reference.
* Tests/build: new QuerySourceTests.cs, SqlQueryTests.cs and build/QueryHost.targets;
  QueryProtocolTests.cs updates the unsupported-version fixture; Workspace.Tests
  imports the test worker-copy target.
* Lock graphs: Workspace, Desktop, ScriptHost, Workspace.Tests, Desktop.Tests and
  Scripting.Tests reflect the current beta project versions/new managed reference.
  Native package versions remain pinned and unchanged.
* Documentation: PROJECT_CONTEXT.md, architecture.md, dependencies.md, ADR 0020,
  sql-query.md, stage15a-sql-engine-acceptance.md, new sql-hl7-workflows.md and
  this acceptance record. The original 15A evidence remains historical.

## Manual review/verification checklist

1. Review the ADR amendment: Plan cannot step/unseal; adapter-private preparation
   cannot be reached by user SQL or view/CTE names; all callbacks contain exceptions.
2. Run the focused SqlQueryTests and QuerySourceTests from the fresh isolated worker
   output. Inspect the zero-body-decoding metric assertion on a 2 MiB OBX message.
3. Verify document-qualified joins/custom paths and two-argument overload refusal in
   multi-document scope. Compare missing/null/error status behavior to the guide.
4. Inspect the all-page extraction and three-encoding byte-equality assertions; check
   source order, deduplication, later edit/close independence and original preservation.
5. Inspect CSV full-value/type/formula assertions, destination guards and atomic
   failure cases. No generated synthetic exports should remain after tests.
6. Review capture/index/RPC cancellation and worker crash/native OOM tests plus
   cleanup assertions; confirm fixed errors contain no source/SQL literals.
7. Confirm the protected preview inventory/hash result and empty Git index. No new
   native SQL menu or grid is expected in 15B. Native UX review begins only after
   separately approved 15C; the existing application's regression suite still runs.

Ignored artifacts/stage15b retains owned build/runtime copies, initial/final logs,
baseline/source/runtime hashes and protected-preview integrity evidence intentionally.
Permanent tests/fixtures are retained. No pre-existing user artifact is removed.
