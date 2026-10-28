# Stage 15C — Docked SQL Query workflow

Status: implemented for owner review after provisional acceptance of 15B. Stop
before 15D. No commit, push, release packaging or accepted-beta artifact change is
authorized. Native Windows UX acceptance remains manual.

## Implementation and architectural boundary

View → SQL Query opens one optional dockable/floating tool with AvaloniaEdit,
theme-aware lexical SQL highlighting, Ctrl+Enter, Run/Cancel, current/selected
document scope, operation progress, SQL/transfer and total duration, row count,
session history and a named-query toolbar. The default workspace remains unchanged.
There is no new editor/grid package or dependency version. SQLite remains the sole
SQL parser/executor and all Stage 15A enforcement/resource guarantees remain in force.

Desktop view models own one tool's draft, cancellation, journal and selection.
Workspace owns saved definitions and the small journal extensions: stable typed
sorting via bounded permutation, authoritative per-row source resolution, SQL duration
projection and complete ordered CSV export. Core, QueryHost, schema, path syntax and
the 15B extraction algorithm are unchanged. Desktop/tests deploy the existing worker
under QueryHost/ via the existing target; no accepted distribution is rebuilt.

The view renders at most 100 bounded previews using the established virtualized
ListBox/grid approach. Whole-result header sorting never fills controls with all
rows. Long/multiline column aliases use 80-unit single-line presentation labels,
with original column metadata retained for CSV. Page-local cell/rectangle/row selection supports full-value quoted TSV copy;
NULL/empty are explicit. Inspect full value is deliberate and bounded. CSV validates
and exports every row in the presentation permutation. Matching extraction ignores
page/selection/sort and retains complete authoritative source-order deduplication and
all byte/framing/encoding checks. Source navigation is deliberate (button or real
data-cell double-click), validates the captured pair/revision/current membership,
and verifies the existing identity at the frozen position before navigation.

Current scope pins sources at Run. Source closure/revision changes cancel participating
running queries; completed captures remain independently usable and display staleness.
The result status attributes captured document numbers and marks when the SQL draft
differs from its producing query. A new run clears the old result before execution,
so failed runs cannot masquerade as success. No content/filename/SQL logging is added.

Saved definitions whitelist Version/Id/Name/Sql in GUID-named JSON under SqlQueries/.
The existing library conventions provide bounded strict Unicode reads, link rejection,
revision/duplicate-name checks, a library lock and FileSaveDestination atomic
publication. A visible disclosure warns that names/SQL literals may be sensitive
and stored unencrypted. Save is explicit, rename preserves stored SQL, delete confirms,
and new/open/history/close handle unsaved changes. No unsaved SQL/history/results/
document metadata is added to preferences/layout. History is application-session-only
and survives tool reopen. Closing/resetting/restoring the tool drains cancellation
and releases results, while layout restores placement with clean content. Shutdown
does not consume SQL discard consent before later document prompts succeed.
Save conflicts at the direct close/shutdown boundary are caught explicitly: closure
is canceled, external definitions and the dirty SQL draft are retained, and no
storage exception escapes the async window event.

See [user guide](sql-query-ui.md), [schema](sql-query.md),
[Workspace contracts](sql-hl7-workflows.md) and [ADR amendment](decisions/0020-isolated-read-only-sql.md).

## Automated verification

Added 33 permanent cases:

* 21 Desktop model/headless/integration cases: menu/lazy default layout, Ctrl+Enter,
  real worker execution, SQL highlighting, current/selected scope and tab switching,
  all-row sorting/paging, typed NULL/empty display, rectangle copy to the real
  headless clipboard, full-value inspection, real mouse double-click navigation,
  stale-source refusal, complete 130-message CSV/extraction and original preservation,
  Cancel/source-close cleanup, fixed privacy-safe errors, named-query toolbar and
  persistence lifecycle, unsaved/cancel/version guards, close/reopen/floating/layout
  serialization, active-operation draining, history/copy/paste caps, producing-query
  attribution and compact/wide light/dark rendering/virtualization.
* 10 Workspace library cases: explicit whitelist/reload/update/rename/delete,
  unsaved-SQL preservation on rename, duplicates/stale revisions, six corruption
  variants, strict Unicode/text/record limits, pre-cancellation and bounded entry count.
* Two journal cases added to SqlQueryTests: complete stable typed sort (including
  integer precision above 2^53), page permutation and CSV validation/cancellation;
  authoritative row identity vs computed aliases.

All 25 original 15B Workspace SQL cases and all 98 worker/protocol cases are retained.
The existing shell test's exact menu inventory is extended with the authorized SQL
entry; every previous entry/order and its exact equality assertion remain checked.
No test is skipped, filtered out of the final gate or weakened. Initial build/test
failures remain in evidence, including absent built-in SQL highlighting (fixed by
the small local definition), stale membership notification before document disposal
(fixed), and the menu inventory update. New test-harness corrections exercise actual
commands, realize virtualized rows before mouse input, and require nonblank rendered
frames rather than accepting a fake headless drawing frame.

Verification on 2026-10-27:

| Check | Result |
| --- | --- |
| Normal and isolated locked solution restores | Passed |
| Final isolated Release solution build, warnings as errors | Passed, 0 warnings / 0 errors, 2.91s |
| Complete Core/Desktop/Querying/Scripting/Workspace regression | 2,129 passed, 0 failed/skipped, 7m 03.246s |
| Final focused SQL UI suite, including header and close-conflict guards | 21 passed, 0 failed/skipped, 11.881s |
| Final complete Desktop suite after those guards | 405 passed, 0 failed/skipped, 2m 13.434s |
| Workspace SQL workflows / saved library focused suites | 27 / 10 passed, 0 failed/skipped |
| Non-Desktop test assemblies after full run | All four byte-identical to the full-suite build |
| Runtime copies | All five DLLs match the final isolated Release outputs |
| Synthetic visual review | Four nonblank Skia screenshots, compact/wide × light/dark |
| Protected private-preview inventory | All 2,384 files byte-identical; no added/removed files |
| Tracked/new-file whitespace | Clean |
| Git | 22 modified tracked / 44 new files across 15A–15C; empty index; HEAD f2a5cec unchanged |

After the complete run, final review bounded multiline/long column headers and
contained close-time save conflicts. Only three Desktop source/test paths changed;
the other four test DLLs stayed byte-identical. The final complete Desktop suite was
rerun separately against these last guards, rather than
rerunning unchanged Core/Workspace/Querying/Scripting modules. Initial failures and
superseded successful logs remain distinguishable in artifacts/stage15c/.
The 15C delta covers 31 paths: 22 newly changed paths relative to the 15B checkpoint
(including 13 new files), plus updates to nine of its already changed paths. The
66-path working tree also includes the uncommitted, provisionally accepted 15A/15B
work; it is not all newly introduced by this pass.

Verification commands (all three telemetry opt-outs set to 1):

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet restore HL7Workbench.sln --locked-mode --artifacts-path C:/Dev/HL7Workbench/artifacts/stage15c/verification
dotnet build HL7Workbench.sln -c Release --no-restore -warnaserror --artifacts-path C:/Dev/HL7Workbench/artifacts/stage15c/verification -p:_WorkbenchWorkerBuildRoot=C:/Dev/HL7Workbench/artifacts/stage15c/verification/bin/HL7Workbench.ScriptHost/release/ -p:_QueryHostBuildRoot=C:/Dev/HL7Workbench/artifacts/stage15c/verification/bin/HL7Workbench.QueryHost/release/
dotnet test --test-modules 'tests/*/bin/Stage15C/net10.0/*.Tests.dll' --root-directory C:/Dev/HL7Workbench --max-parallel-test-modules 1 --minimum-expected-tests 2129
git diff --check
```

The running app locks normal Release outputs. Fresh isolated Release outputs are
copied byte-for-byte into owned tests/<project>/bin/Stage15C/net10.0 directories,
and Desktop output into src/HL7Workbench.Desktop/bin/Stage15C/net10.0. This retains
existing branding tests' fixed source-root/configuration assumptions without changing
their assertions or rebuilding the accepted preview. Five DLL hashes are recorded.
Modules run sequentially, as in 15B, to avoid timing contention with worker native
memory exhaustion tests. Host execution permission is used; containment is never
disabled. Set HL7WORKBENCH_SQL_RENDER_DIRECTORY to an owned directory when running
SqlQueryToolTests alone in a fresh process to produce four nonblank Skia screenshots.
The normal final regression run does not set that opt-in render variable.

## Limits and known limitations

All 15A/15B SQL/source/result/materialization/extraction caps remain. UI additions:
100 preview rows, 256-character cells, 1 Mi UTF-16 selection-copy cap; history at most
32 entries/256 Ki SQL units; saved library 256 definitions/512 KiB encoded each,
120-character names and 65,536 SQL units. Sort retains an int permutation plus one
bounded column, not all rows in UI controls. Full-value inspection remains within
the existing scalar cap (blobs display full bounded hex). Invalid input never gets
silently shortened. CSV exports all rows and extraction all authoritative identities.

Local text sorting is ordinal; SQL ORDER BY is required for SQLite collations or
expressions. Copy selection is page-local and quoted TSV with explicit NULL; use CSV
for all result pages. Named parameter binding exists in Workspace but this editor
has no parameter-entry panel. The highlighter is lexical, not completion/semantic
validation. Common-segment convenience views remain deferred.

Metadata queries still stream/copy captured bytes; structural queries still scan all
scoped messages before SQL. SQLite evaluation order, materialization limits and
deadline tradeoffs from 15B remain. Unsafe mixed encodings/wrappers/framing/joins
fail extraction without repair. Temporary journals/spools can contain sensitive
data; normal close drains/deletes owned resources and crash leftovers follow existing
snapshot cleanup. This is not secure erasure or a general security sandbox.

Headless layout/mouse/clipboard tests and synthetic render inspection are not native
Windows acceptance. Floating title bars, monitor/DPI recovery, OS clipboard/pickers,
modal confirmation focus and comfortable sizing in the owner's normal workspace
require the manual checklist. No 15D stress/release/packaging gate is claimed.

## Files changed in 15C

* Desktop composition: MainWindow.Sql.cs (new), MainWindow.axaml,
  MainWindow.axaml.cs, MainWindow.Editing.cs, Desktop.csproj worker copy import.
* Docking: WorkspaceDockFactory.cs and DockContentTemplate.cs; one lazy tool ID,
  content dispatch, close confirmation and disposal using existing placement rules.
* New Desktop models: SqlQueryHistory.cs, SqlQueryViewModel.cs,
  SqlQueryViewModel.Library.cs and SqlQueryViewModel.Selection.cs.
* New views: SqlQueryView.cs and SqlHighlighting.cs; ConfirmationDialog.cs adds the
  SQL delete prompt.
* Workspace: new SqlQueryLibrary.cs and SqlQueryResults.Presentation.cs;
  SqlQueryResults.cs adds SQL duration and validated complete CSV permutation.
* Tests: new SqlQueryToolTests.cs and SqlQueryLibraryTests.cs, two new SqlQueryTests
  cases; Desktop.Tests.csproj copies QueryHost; ShellFinishingTests.cs extends its
  exact menu inventory. No package version or Core/Querying/QueryHost source changes.
* Documentation: PROJECT_CONTEXT.md, architecture.md, dependencies.md, ADR 0020,
  sql-query.md, sql-hl7-workflows.md, stage15b-hl7-query-acceptance.md, new
  sql-query-ui.md and this acceptance record.

## Manual Windows UI checklist

Use the fresh isolated Release application under
artifacts/stage15c/verification/bin/HL7Workbench.Desktop/release/, not the already
running older binary. Use synthetic-only files and a disposable output directory.

1. **Menu/layout:** Open View → SQL Query. Check a fresh layout still starts with
   the accepted tools; SQL opens on demand, focuses its editor and leaves the active
   HL7 document selected. Resize the editor/results split and tool horizontally.
2. **Metadata:** Run the default query with Run and Ctrl+Enter. Confirm row count,
   SQL/total durations and complete status. Try a large synthetic OBX body; metadata
   should remain responsive without eager value materialization. Run invalid SQL
   and a mutation; confirm fixed errors, empty new result and preserved originals.
3. **PID/PV1/scope:** Run the guide's PID/PV1 query. Switch current documents, then
   select two sources with duplicate control IDs. Use qualified functions in
   multi-document scope; confirm the two-argument overload is rejected there.
4. **Sort/copy/value:** Sort a column both ways across multiple pages. Select cells,
   a Shift rectangle, additive cells and a row; copy to a text editor/spreadsheet.
   Check NULL vs empty, quotes/newlines, full long values, and oversize copy refusal.
   Inspect a full value explicitly. SQL editor paste beyond 65,536 is rejected.
5. **Navigation:** Selecting a row alone must not navigate. Double-click a data cell
   or use Go to source; verify the exact original message and unchanged dirty state.
   Edit/close its source and verify stale labeling and disabled navigation. Edit SQL
   without Run and verify the previous result's attribution remains clear.
6. **CSV:** Export all results to a disposable CSV. Verify rows beyond page 1 and
   current sort order, UTF-8/quoting/type columns/formula guard. Cancel picking,
   decline overwrite and attempt a source destination; no partial/overwritten source.
7. **Matching messages:** Include original DocumentId/MessageId, duplicate rows and
   navigate to a later page/select one row. Open Matching Messages must still use
   all complete identity rows, deduplicate and open an independent unsaved document
   in source order. Compare bytes/terminators; originals stay unchanged. Check no
   matches, identity-free aggregate, budget and unsafe encoding/wrapper refusal.
8. **Library/privacy:** Read the local-storage disclosure. Create/save/open/edit/
   update/rename/delete a named synthetic query. Rename must not save SQL edits.
   Cancel delete and unsaved prompts; failed save must retain the draft. Session
   history survives tool close/reopen, but restarting restores no history, unsaved
   SQL or results. Only explicit definitions appear in SqlQueries/.
9. **Dock/lifetime:** Float, dock, close and reopen the tool. Confirm dirty drafts,
   results cleanup, fresh content and remembered placement. Save/restore layout;
   inspect JSON for placement only. Check title bars/DPI/monitor recovery, dark/light
   theme readability, clipboard/picker and modal focus in floating windows. Cancel a
   later HL7 close prompt during shutdown; the SQL draft must remain dirty/intact.
10. **Cancellation:** Run a long read-only recursive aggregate (e.g. 1 billion rows)
    and Cancel promptly. The UI remains responsive; cancellation or a work/deadline
    limit publishes no partial result. Also close/edit a source or close the tool
    during capture/index/execute; work drains and reopen starts fresh.

Ignored artifacts/stage15c retains initial/final logs, build/runtime copies, source/
runtime hashes, four synthetic screenshots and protected-preview integrity evidence
for review. Disposable HL7/CSV fixtures are cleaned by tests. Permanent tests and all
pre-existing artifacts are preserved.
