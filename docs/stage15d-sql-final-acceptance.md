# Stage 15D — SQL performance, integration and final acceptance

Date: 2026-10-28. Starting checkpoint: clean `main` at
`fc802a8d8ee2afaa6b6ec251162d59cf9196f449`. The owner reports successful native
use of the preceding SQL/UI stages. This pass does not reclassify that report as
agent-performed native testing. No commit, push, packaging or Stage 16 is authorized.

## Executive summary and recommendation

Recommendation: **CONDITIONAL PASS**, with the automated gate passed and the native
checklist/explicit verification qualifications remaining. Measured metadata workflows scale through 100,000 messages,
large OBX metadata remains lazy, and complete identity results support source-order
extraction. One malformed-IPC defect was reproduced and corrected. No authorizer
bypass or reason to replace/optimize the established engine was found.

Conditions are explicit: native Windows interaction remains owner verification;
some high hard limits and mid-operation cancellation races were inspected rather
than forced dynamically. This is not exhaustive proof of every possible workload
or a claim of a general-purpose security sandbox. The evidence matrix below
separates those limits from tested boundaries.

## Architecture and lifecycle reviewed

Reviewed ADR 0020, architecture/schema/UI/workflow/responsive guides, 15A–15C
acceptance records, Querying/QueryHost, Workspace capture/journal/output code,
Desktop models/view and retained tests. The short initial audit is retained under
`artifacts/stage15d/initial-audit.txt`.

1. Workspace freezes logical order, identities, drafts and encoding on the owning
   context. It copies source bytes to independently owned snapshots off-context,
   then releases original leases. Completed results deliberately survive later
   source closure/editing; they cannot silently follow another active document.
2. Querying launches QueryHost without a shell. Only the random pipe identifier
   enters arguments. Current-user IPC uses bounded, versioned JSON frames.
   stdout/stderr are drained without retaining content. Windows Job Object memory
   and kill-on-close policy is assigned before Initialize permits SQLite work.
3. QueryHost alone loads SQLite. A private in-memory database, trusted typed import,
   prepare-only Plan and permanently sealed Execute preserve the process boundary.
   The deny-by-default authorizer precedes preparation and stays through stepping.
   User SQL never runs in Desktop or Workspace.
4. Only referenced relations are projected. Metadata queries decode bounded MSH
   headers. Structural queries still scan every scoped message before executing
   predicates; fixed read-only adapters request complete bounded values lazily.
   Core remains the path/scanner/escape authority.
5. Complete bounded results become an owned journal plus offsets. UI reads at most
   100 previews; typed stable sort retains a bounded permutation and one key column.
   Result leases defer cleanup until readers drain. Replacement/close/disposal cancel
   work and release journals, sources and decoded spools.
6. CSV and extraction use all complete rows, not a page. CSV stages atomically.
   Extraction validates provenance and every captured pair, deduplicates in source
   order, copies exact spans, then reindexes before publishing one independent tab.

No schema, dependency, native library, parser, UI layout or source mutation semantics
changed. The production patch is confined to `QueryProtocol.ReadAsync`.

## Environment, fixtures and measurement method

Windows `10.0.26200`, X64, .NET `10.0.12`, 16 logical processors. The runtime reported
33,286,213,632 bytes available to GC; this is **not** measured free physical RAM.
Measurements use isolated Release outputs and synthetic generated fixtures; no large
fixture binary is committed. Query tests run serially with unrelated test suites
stopped. Ordinary application/background OS activity and filesystem caches are not
controlled, so these are observations rather than latency guarantees.

`SqlPerformanceTests` is explicitly opt-in. It records fixture/open time separately,
first progress-phase timestamps, total wall time, SQL/complete transfer duration,
Core decode/value/import counters, encoded journal bytes and owned temporary storage.
It samples parent/new-worker working sets every 25 ms. Samples can miss short peaks;
they are neither committed-memory limits nor private-byte measurements. Parent RSS
also reflects prior allocations/GC and retained results. Export/extraction timing
entries do not collect a separate memory sample. Worker startup has five samples;
the 50-message aggregate has three. Large runs have one sample each.

Fixtures: minimal MSH/PID/OBX messages with alternating A01/A04 and intentionally
duplicate control IDs; patient/value ordinals are deterministic. Counts are 50,
4,000, 10,000 and 100,000. Oversized messages contain 1/16/64 MiB of base64-like `Q`
text in OBX-5. The malformed timing fixture has 1,000 messages with Z-segments,
unstructured lines, empty fields and invalid escapes. Normal tests separately cover
custom delimiters, repeated fields/segments, subcomponents, invalid bytes, BOMs,
UTF-16 variants, wrappers and mixed terminators.

## Baseline and final runtime observations

The baseline ran before the production fix. Raw JSON/logs are retained under
`artifacts/stage15d/baseline/` and `measured/`. No performance optimization was made;
differences between series must not be presented as improvements.

| Workload | Baseline | Final observation |
| --- | ---: | ---: |
| 50-message metadata aggregate, median of 3 | 416 ms | 401 ms |
| 10,000-message metadata aggregate | 1,560 ms | 1,314 ms |
| 100,000-message metadata aggregate | 3,514 ms | 3,204 ms |
| 100,000 identity rows, complete transfer/journal | 3,902 ms | 4,134 ms |
| 100,000-row local sort and final page | 294 ms | 303 ms |
| 100,000-row CSV | 362 ms | 341 ms |
| 100,000 matching messages extracted | not measured | 1,084 ms |
| 4,000-message Fields join with values | not measured | 5,750 ms |
| 10,000-message segment count | 4,857 ms | 4,609 ms |
| 10,000-message path predicate, one matching row | not measured | 5,766 ms |
| 1,000 malformed messages, structure count | not measured | 870 ms |
| Source-RPC barrier cancellation | 212 ms | 215 ms |

The 100,000-row encoded journal was 21,698,896 bytes. The largest sampled worker
RSS in that final case was approximately 84 MiB; parent approximately 181 MiB.
Five isolated worker starts had a 362 ms median (354–420 ms range).
Metadata counters showed zero body decoding/value reads. The 50-message series
included a 992 ms outlier; its other two observations were 381/401 ms. Warm/cold
cache and scheduling variation is visible and not hidden by a speedup claim.

| OBX-5 size | Metadata | Locate raw length | Reject full value | Exact extraction |
| --- | ---: | ---: | ---: | ---: |
| 1 MiB | 427 ms | 683 ms | 450 ms | 8 ms |
| 16 MiB | 400 ms | 453 ms | 589 ms | 105 ms |
| 64 MiB | 406 ms | 716 ms | 960 ms | 391 ms |

Metadata queries decoded zero body characters for all three sizes. Locating field
structure still decodes a bounded spool, not an enormous grid/string value; it
made zero value RPCs. Requests for the whole value, `substr(DecodedValue,1,32)` and
a predicate on it all failed MaterializationLimit without partial results.
`substr` is not a way around materialization policy. Sampled worker RSS for these
queries was about 34–38 MiB, parent about 103–111 MiB. All three extraction outputs
were compared chunk-by-chunk with the original source bytes.

### Practical scaling limit found

The 10,000-message Fields join failed **ImportLimit**, after about 4.9 seconds,
despite a narrow final projection. Full-scope field metadata reaches the existing
64 MiB accounted import budget before this fixture finishes. There are 240,001
projected document/message/segment/field rows if fully populated; accounting is
not the physical database size. This is an enforced ceiling, not silent truncation.
The baseline test initially assumed success and failed; that evidence is retained.
The final test expects the existing cap, then verifies a fresh path-query workflow
can succeed. A 4,000-message Fields join succeeds. No cap was raised or query
predicate parser invented. For narrow lookups, Messages plus HL7_VALUE avoids
importing the entire Fields relation, while retaining value/read/deadline limits.

## Correctness and exact-byte extraction

Automated expected values are authored independently of SQL projection output:
literal MSH-1/2, field repetitions/components/subcomponents, decoded escapes,
empty/explicit-null/missing states, Z-segments and malformed segment statuses.
Fixture ordinals independently determine group counts, filtered values and order.
Native column provenance and captured document/message pairs remain navigation
authority; displayed row positions/control IDs do not substitute.

New tests sort/page 205 messages, select noncontiguous odd messages and compare the
assembled output to independently constructed fixture bytes after both source and
unrelated document closure. UTF-8 BOM, UTF-16LE/BE and mixed CR/LF/CRLF are covered.
Existing tests retain MLLP framing, invalid encoding bytes, duplicate control IDs,
multi-document identity, working edits, no-match handling and refusal of unsafe
wrappers/mixed encodings/unterminated joins. Large-value extraction and the 100,000
message matching cap are exercised by explicit tests.

This proves message-span/container behavior under the documented contract, not
whole-original-file identity after selecting a subset. Existing BOM/container rules
remain: unsafe outer regions/wrappers are refused, never silently discarded/repaired.

## Security and worker reliability findings

Existing and new real-worker tests reject DML/DDL, transactional operations,
ATTACH/DETACH, forbidden PRAGMAs/table-valued PRAGMAs, schema/private-table access,
extension/file functions, arbitrary modules and disallowed functions. New attacks
include mixed case, comments, CTEs, brackets/quotes, nested calls, RETURNING,
EXPLAIN ATTACH and valid-looking SELECTs with disallowed functionality. Reused
execution authorizers and source-backed mode are tested; existing planning attacks
remain retained. No SQL prefix filter grants authority.

Crash/cancellation tests use source-callback completion barriers rather than sleeps
to force an in-flight RPC. Worker termination/IPC disconnect yields a fixed failure,
not a partial result; a fresh worker succeeds. Existing native-heap exhaustion,
query/operation/session deadlines, source closure and tool-disposal tests remain.
Five startup/disposal measurements also assert worker exit. Windows kernel readback
checks actual job membership, both 512 MiB limits and kill-on-close flags.

### Defect reproduced and fixed

Nullable annotations did not enforce JSON shapes. An explicit-null `Plan.Tables`
caused NullReferenceException in the parser, and several nested null shapes passed
the decoder to later validation. Six regression cases captured the old behavior.
ReadAsync now rejects null nested collections/items and invalid table enums with
fixed ProtocolError. Frame/depth limits, wire version and valid semantics remain
unchanged. This is reliability hardening; no authorization escape was demonstrated.

The worker still runs as the same OS user, inherits the host environment and is
not an OS capability sandbox. Current-user IPC does not defend against a malicious
process already running as that user. No claim is made about native-engine exploits,
secure erasure, OS paging/crash dumps or equivalent non-Windows memory containment.

## Resource-limit evidence matrix

Constants come from QueryLimits/QueryBudgets/QueryWorkerOptions, SqlQueryOptions,
QueryEngine and SqlQueryResults, not guessed workload targets. Accounting caps are
not exact heap/RSS budgets. Every configurable budget is tested against attempts
to raise it. Small exact-boundary tests lower configurable budgets; that is separate
from reaching a default hard maximum.

| Limit / enforcement | Dynamic evidence and remaining boundary |
| --- | --- |
| SQL 65,536 UTF-16; parent + worker | Below/at/above submission tests; embedded NUL/invalid Unicode retained tests |
| 64 parameters / columns | 63/64/65 parameter-column queries; named binding/policy tests |
| Scalar 65,536 UTF-16 / 256 KiB blob; 512 KiB row | Text at/over retained; blob below/at/over; seven/eight maximal text cells; oversized source values refuse |
| Import 250,000 rows / 64 MiB accounted | Actual row-cap stress; exact lowered byte boundaries; 10k Fields reaches default byte cap; recovery verified |
| Results 100,000 rows / 16 MiB accounted | 99,999/100,000/100,001 stress; exact lowered 97-byte header+row boundaries; partial pages discarded |
| IPC 2 MiB / 100 page rows / 512 KiB page target | Below/at/above encoded input frames; capped outgoing serializer; multi-page results; malformed nested shapes |
| Source RPC 100,000 reads | Actual ceiling stress, worker reap and fresh recovery; not merely a lowered option |
| Startup/exchange/query 30s; session/total 10m | Lowered query, exchange and idle-session deadlines dynamic; startup/whole-run starvation not independently injected |
| SQLite heap 128 MiB / DB pages 64 MiB | Native-heap exhaustion retained; page-cap setup verified by runtime; exact DB-page edge not separately forced |
| Windows job 512 MiB process/job | Actual kernel configuration/membership readback and reap; deliberately did not allocate >512 MiB solely to force OS termination |
| Scope 16 docs / 100,000 messages | 15/16/17 documents; 100k scoped messages succeeds; 100k+1 multi-document scope refuses |
| Capture 1 GiB / frozen drafts 16 Mi UTF-16 | Exact lowered source-byte boundaries and 64 MiB fixture; literal 1 GiB edge and SQL-specific draft edge not allocated |
| Journal 128 MiB encoded | 21.7 MB observed; lower scalar/result ceilings dominate approved workloads; literal 128 MiB edge not forced |
| Output 1 GiB / 100k unique matches | Exact lowered output/matching boundaries; 64 MiB exact output and 100k matches; 1 GiB edge not allocated |
| CSV 256 MiB | Exact lowered byte boundaries, all-row 100k export, atomic failure; literal 256 MiB edge not forced |
| Native expression depth 32, compound terms 16, args 16, compiled ops 100k, progress callbacks 100k, LIKE bytes 4096, attached DBs/threads/trigger depth 0 | Setup inspected; retained deep-expression/native-length/VM-budget and policy rejection tests; not every individual native threshold exhaustively bracketed |
| UI 100 rows / 256-char preview / 1 Mi clipboard; history/library bounds | Existing complete Desktop/Workspace library tests retained; no UI collections made unbounded |

The last-column limitations prevent unconditional acceptance of an assertion that
**every default cap** has been tested below/at/over. Dominating limits, particularly
result accounting ahead of journal/CSV ceilings, are not bypassed just to force a
downstream cap. The report does not mark inspected boundaries as dynamically passed.

## Cancellation and cleanup evidence

| Phase | Evidence / qualification |
| --- | --- |
| Capture | Synchronized callback after document 1 prevents capture of document 2; cleanup and retry; per-copy token checks inspected |
| Startup | Pre-start/parent timeout plumbing inspected; no controlled stalled-startup executable used |
| Projection | Retained inline Indexing cancellation and snapshot cleanup; full scanner/spool token checks inspected |
| Execution / source RPC | Real recursive cancellation/deadlines; controlled RPC cancellation/crash; typical measured cancel 215 ms |
| Page / sort | Pre-canceled calls dynamically reject; per-row/comparison token checks inspected; no artificial mid-sort I/O barrier added |
| CSV / extraction | Pre-cancellation, quota failure and atomic no-publication tests; per-row/chunk tokens inspected; precise mid-write cancellation race remains unforced |
| Dispose / closure | Retained Desktop active-operation drain/source-close tests, new captured-source closure extraction, no partial stale-success result |

The 200 ms grace is attempted cooperation before kill/reap, not a universal latency
guarantee: process exit and stream drain have additional bounded waits. Synchronous
logical freeze, one full-row read and clipboard construction use bounded completion;
they are not independently interruptible in the middle. No sleep-based timing probe
was added to fake deterministic cancellation coverage.

## CSV, privacy and diagnostics

CSV exports every row in the validated complete presentation permutation, regardless
of current page. Literal UTF-8 expected bytes cover quotes, commas, CR/LF, Unicode,
NULL/empty type companions and all formula prefixes including whitespace/control
prefixes. Ordinary quoting alone is not formula protection.

The existing shared encoder deliberately prefixes risky displayed values with an
apostrophe, including a negative numeric scalar after conversion to display text.
It is spreadsheet-guarded **presentation**, not an unmodified raw-value interchange
format. ReadRow retains original typed scalars; HL7 extraction retains original
bytes. No new faithful/safe export mode or silent policy change was introduced.
Spreadsheet imports/settings can still interpret data differently; no universal
formula-execution guarantee is made.

Reviewed error formatting, Activity Log use, process arguments, protocol ToString,
worker failure/stdout/stderr and explicit library persistence. Tests keep synthetic
private markers out of errors/status/ordinary logs. No query text/value/source paths
were added to diagnostics or measurements. Named queries still require explicit
unencrypted persistence; session history/results are not settings. Temporary
snapshots/spools/journals can contain sensitive values and retain existing leased
cleanup/crash recovery, not secure deletion. No network service or telemetry added.

## Desktop integration and native boundary

Existing headless tests cover Ctrl+Enter, Run/Cancel, history/library, selected/current
scope, copy/inspection, dock close/reopen/floating ownership and state retention.
Responsive suites exercise 1200×800, 900×600, 600×400, 350×300 and 250×250 DIP.
The new SQL case navigates the sorted second page after unrelated source closure,
then rejects forbidden SQL and successfully reruns in the same model without stale
rows, sensitive diagnostics or a stuck busy flag. UI layout/controls were not redesigned.

Headless controls and fake Dock hosts are not native Windows chrome, clipboard,
picker, DPI or modal-focus verification. SQL alongside actual FHIR work, inspection
tools and another long-running operation remains in the native checklist.

## Verification gate, reproducibility and files

Final test counts, build, protected-artifact check and Git inventory are recorded
below. Initial failed test logs and raw measurements are retained separately; no
existing assertions were weakened.

| Final check | Actual result |
| --- | --- |
| Normal and isolated locked restores | Passed |
| Release solution build with warnings as errors | Passed, 0 warnings / 0 errors |
| Querying normal suite | 158 passed, 0 failed; 5 explicit cases skipped in this invocation |
| Focused Workspace SQL / hardening / library | 27 / 32 / 10 passed, 0 failed/skipped |
| Focused Desktop SQL | 22 passed, 0 failed/skipped |
| Complete five-module regression | 2,282 passed, 0 failed, 14 explicit skips; total 2,296; 6m 17.151s |
| Explicit performance suite | 9 passed, 0 failed/skipped; 1m 00.511s |
| Explicit actual-cap suite | 5 passed, 0 failed/skipped; 9.127s |
| Protected preview | All 2,384 baseline files match; no added/missing files |
| Whitespace and runtime-copy hashes | Passed; all five tested module DLLs match final isolated build |

The 14 default skips are exactly the separately executed opt-in cases, not remaining
untested cases. This pass adds 93 ordinary cases and 14 explicit cases (107 total).
The SourceRead cap observation is 100,000 reads and ResourceLimit with worker reap;
the final end-to-end measurement was 5.403s including startup/recovery.
Core/Desktop/Querying/Scripting/Workspace full-run durations were respectively
1.466s / 2m 05.045s / 12.361s / 22.055s / 3m 34.950s.

An initial combined Workspace class wildcard matched zero tests and was rerun using
the three exact class filters above. That zero-test invocation is retained and is
not counted as a passed check. Initial six protocol regressions and the benchmark's
incorrect initial assumption about the 10k import cap are retained separately too.

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet restore HL7Workbench.sln --locked-mode --artifacts-path C:/Dev/HL7Workbench/artifacts/stage15d/verification
dotnet build HL7Workbench.sln -c Release --no-restore -warnaserror --artifacts-path C:/Dev/HL7Workbench/artifacts/stage15d/verification -p:_WorkbenchWorkerBuildRoot=C:/Dev/HL7Workbench/artifacts/stage15d/verification/bin/HL7Workbench.ScriptHost/release/ -p:_QueryHostBuildRoot=C:/Dev/HL7Workbench/artifacts/stage15d/verification/bin/HL7Workbench.QueryHost/release/
dotnet test --test-modules 'tests/*/bin/Stage15D/net10.0/*.Tests.dll' --root-directory C:/Dev/HL7Workbench --max-parallel-test-modules 1
```

All three CLI/platform/Avalonia telemetry opt-outs are set to 1. Fresh generated
test trees are copied into standard-shaped owned bin/Stage15D/net10.0 folders,
with all five module hashes checked. Desktop's fresh tree is copied similarly;
this preserves existing source-root/branding assertions without modifying them.
No normal running Release or accepted preview output is rebuilt.

Opt-in runs use `--filter-class '*SqlPerformanceTests' --explicit only` on the
Workspace module and `--filter-class '*QueryResourceStressTests' --explicit only`
on Querying. Set `HL7WORKBENCH_SQL_PERF_DIRECTORY` to an owned artifacts directory
to retain count/timing/memory JSON. Default CI deliberately skips these explicit
cases; separate executed stress totals must be reported, not counted as default passes.

Changes: QueryProtocol.cs; new QueryHardeningTests.cs, QueryContainmentTests.cs,
SqlHardeningTests.cs and SqlPerformanceTests.cs; one SqlQueryToolTests.cs case;
this report, PROJECT_CONTEXT, ADR 0020, architecture and SQL schema/workflow/UI guides.
Generated fixtures/exports are removed by test ownership cleanup. Ignored metadata,
build/test logs, initial failures and measurement JSON under artifacts/stage15d are
intentionally retained; no permanent tests or preexisting artifacts are deleted.

Final Git inventory: `main`, HEAD unchanged at
`fc802a8d8ee2afaa6b6ec251162d59cf9196f449`; 8 modified tracked files and 5 untracked
files (13 Stage 15D paths), index empty. The starting checkpoint was clean; none of
the preceding milestone work was reverted. No commit, push, packaging or accepted
artifact modification occurred.

## Native Windows checklist and final decision boundary

Launch the latest verified executable:
`C:\Dev\HL7Workbench\src\HL7Workbench.Desktop\bin\Stage15D\net10.0\HL7Workbench.Desktop.exe`.
It is copied from the isolated build at
`C:\Dev\HL7Workbench\artifacts\stage15d\verification\bin\HL7Workbench.Desktop\release\HL7Workbench.Desktop.exe`.

1. Open a representative synthetic multi-message file and SQL Query.
2. Run the default query with Ctrl+Enter; check complete row count/status.
3. Filter ADT A01 and sort; visit a later result page and deliberately navigate to
   its source, checking message number/content rather than duplicate control IDs.
4. Export CSV from that page; verify all rows, ordering, quoting and type columns.
5. Extract matches into a new unsaved document; originals remain unchanged.
6. Cancel a long recursive query; verify no partial results/stuck busy state, then
   run a simple query successfully. Try invalid SQL and a forbidden operation.
7. Float/dock/resize through narrow/short sizes; preserve editor, selection and page.
8. Inspect/copy a full bounded value, including NULL versus empty; use a large OBX
   metadata query without requesting the entire payload.
9. Save/reopen a synthetic named query and use session history; close/reopen the tool
   and verify the documented fresh draft/results versus retained session history.
10. Use SQL beside Field Inspector, Segment Navigator and FHIR Patient Workspace,
    including while another safe local background operation is active.
11. Confirm ordinary viewer/editor navigation, edits, undo/save and source handling
    remain unaffected. Check native clipboard/pickers and monitor/DPI behavior.

Conditional acceptance is appropriate only with the unforced boundaries above
acknowledged. No verified security/correctness defect is intentionally left open.
Native review and deliberately unexhausted hard maxima are remaining verification
conditions, not silently completed tests. Stop after Stage 15D; no Stage 16 or
release/commit action follows automatically.
