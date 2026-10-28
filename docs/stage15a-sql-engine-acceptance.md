# Stage 15A — SQL engine and enforcement acceptance

Historical status: the owner provisionally accepted this gate and authorized 15B.
The original verification below is retained. See [15B acceptance](stage15b-hl7-query-acceptance.md)
and ADR 0020's amendment for subsequent behavior; no new release is claimed.

## Scope

New projects: HL7Workbench.Querying, HL7Workbench.QueryHost and
HL7Workbench.Querying.Tests. The existing application implementations and their
test assertions are unchanged. The solution and central dependency catalogue add
the new projects/package, with three new project lock files. No accepted beta
artifact, portable-release manifest or native-notice inventory is changed.

The engine uses the six closed relations documented in [the SQL guide](sql-query.md).
Fixtures supply synthetic relational rows; no private HL7 is read. Live Core
projection, document captures, lazy value planning, HL7 functions, extraction, CSV,
Dock tools and saved-query workflows remain for 15B/15C. The guide and
[ADR 0020](decisions/0020-isolated-read-only-sql.md) make that boundary explicit.

## Automated verification

The permanent real-worker suite covers SELECT, joins, DISTINCT, filtering,
ordering, grouping, aggregation, CTEs, parameters, repeated relational coordinates,
document-qualified keys and duplicate control IDs. Typed null/empty/integer/real/
blob/Unicode values, embedded NUL, quoted/newline text, invalid UTF-8 and nonfinite
results are exercised without coercion or shortening.

Enforcement cases include mutation, DDL, temporary objects, triggers, views,
virtual tables, ATTACH/DETACH, VACUUM INTO, PRAGMA (including EXPLAIN), transaction
control, extensions, file functions, schema/rowid reads, memory-expanding functions,
WITH-prefixed mutation and multiple statements. Safe comments/semicolons use the
actual SQLite preparer. A rejected ATTACH leaves no file. Fresh workers have no
prior data. SQL failures return fixed diagnostics with no original query literals.

Budgets test exact scalar materialization boundaries, metadata describing a
50-million-character unmaterialized value, result row/byte overflow after pages,
input row/byte bounds, duplicate/orphan imports, expression/native-value limits,
VM-work/deadline interruption, actual native heap exhaustion during sorting,
import/query cancellation, idle-session expiry, worker crash, idempotent disposal
and successful fresh sessions after failure. The native-memory case is contained
inside QueryHost; it does not deliberately exhaust the test runner or Desktop.
3,000 synthetic messages cross result pages with exact order/identity assertions.

Protocol tests reject invalid/oversized lengths before allocation, unsupported
versions/kinds/error codes, and oversized outgoing JSON before writing pipe bytes.
They verify fixed payload-free ToString output, hidden apphost launch, metadata-free
arguments and the absence of SQLite references in the parent assembly.

Verification commands (all telemetry opt-outs set to 1):

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore -warnaserror
dotnet test --project tests/HL7Workbench.Querying.Tests/HL7Workbench.Querying.Tests.csproj -c Release --no-build --no-restore
dotnet test --solution HL7Workbench.sln -c Release --no-build --no-restore
git diff --check
```

The running application held normal Release Desktop DLLs open, so the final
warning-as-error solution build used `--artifacts-path
C:/Dev/HL7Workbench/artifacts/stage15a/verification` with worker copy roots pointed
at that tree. Initial alternate-path tests exposed four unchanged branding tests'
fixed five-parent source-root/configuration assumptions. To exercise them without
changing assertions or closing the application, the freshly built outputs were
copied to owned ignored `tests/<project>/bin/Stage15A/net10.0` runtime folders and
`src/HL7Workbench.Desktop/bin/Stage15A/net10.0`. Test DLL hashes match the verified
Release outputs byte for byte. The final complete run uses:

```powershell
dotnet test --test-modules 'tests/*/bin/Stage15A/net10.0/*.Tests.dll' --root-directory C:/Dev/HL7Workbench --max-parallel-test-modules 1 --minimum-expected-tests 2049
```

The first parallel regression run also failed one unchanged load-rate timing
assertion under contention. Its exact focused rerun passed; the complete final
run serializes test modules, retaining every test and assertion. Initial failed
build/test logs remain alongside the final gate; none are represented as passing.

The managed sandbox initially blocked NuGet DNS/build child processes. Normal
restores/builds/tests use the approved host execution permission; no worker
containment check was weakened or bypassed. Initial failure logs remain under
ignored artifacts/stage15a/. They include a corrected lifetime-token diagnostic
classification bug and SQLite's schema-omitted count(*) authorization edge case.

Final gate (2026-10-27):

| Check | Result |
| --- | --- |
| Normal and isolated locked solution restores | Passed; existing lock files unchanged |
| Isolated Release solution build with warnings as errors | Passed, 0 warnings / 0 errors, 8.01 seconds |
| New SQL engine/protocol tests | All 76 passed; final module duration 14.541 seconds |
| Complete unchanged regression suite plus new SQL tests | All 2,049 passed, 0 failures / 0 skips, 6m 31.269s |
| Runtime-path compatibility copies | All five test assemblies byte-identical to isolated Release outputs |
| New-project source/project/lock hashes after testing | Matched verified build inputs |
| Accepted private-preview inventory | All 2,384 files retained byte-identically; no added/removed files |
| Tracked and new-file whitespace | Passed |
| Git | 5 modified tracked files / 18 new files, empty index; no commit/push/package |

Evidence is intentionally retained in ignored artifacts/stage15a/, including
initial failures, locked restore/build/final test logs, source hashes and preview
integrity manifests. Owned isolated verification and bin/Stage15A outputs are
retained; no pre-existing artifacts, fixture, regression test or user file was
deleted. Standard Release output is not the final verification location because
the running application's DLLs remain locked. No accepted preview was rebuilt.

For an isolated rebuild, use the same artifacts path for restore/build and pass
`-p:_WorkbenchWorkerBuildRoot=C:/Dev/HL7Workbench/artifacts/stage15a/verification/bin/HL7Workbench.ScriptHost/release/`
and
`-p:_QueryHostBuildRoot=C:/Dev/HL7Workbench/artifacts/stage15a/verification/bin/HL7Workbench.QueryHost/release/`
to the build. These override worker-copy roots only; they do not change the code
or accepted application deployment. Copy each generated test `release/` tree to
its owned `tests/<project>/bin/Stage15A/net10.0/` folder, and the Desktop `release/`
tree to `src/HL7Workbench.Desktop/bin/Stage15A/net10.0/`, before the module command
above. That preserves existing source-root and native executable assertions.

## Resource contract and known limitations

See ADR 0020 for the full immutable hard-cap table. Key limits are 512 MiB Windows
job, 128 MiB SQLite heap, 64 MiB database/import, 250,000 imported rows, 100,000
result rows, 16 MiB accounted result, 64 columns, 65,536 text UTF-16 units per
explicitly materialized value, 2 MiB frames, 30-second query and 10-minute session.
Results are complete or fail. Large source fields are not assumed invalid because
they exceed a materialization budget. No truncation/export/extraction from partial
results exists. Accounted byte limits are not exact process working-set promises.

This is Windows containment, not a general-purpose security sandbox. Other OSes
do not yet enforce an equivalent job memory cap. Runtime data is entirely in
memory; normal OS paging/crash-dump policies remain outside the application.
Queries intentionally support a documented function allowlist rather than every
SQLite extension/function. Multiple statements, anonymous parameters and schema
introspection are unsupported. Failure invalidates a session; callers start fresh.
Incoming batches can hit encoded frame limits before their row count cap. No
self-contained QueryHost distribution or native UI acceptance is part of 15A.

## Owner review checklist

1. Review ADR 0020, package lock graph and the no-live-HL7/no-UI boundary.
2. Review authorizer installation before prepare and its function/relation allowlist.
3. Confirm worker-side limits plus independent parent deadlines and process cleanup.
4. Review all-or-failure result semantics and 65,536-character materialization wording.
5. Confirm the separate bounded extraction and convenient Current document design
   commitments for 15B; neither has been implemented prematurely.
6. Review final test/build logs and unchanged accepted preview hashes.
7. Authorize 15B separately if satisfied. No commit/push/package is implied.
