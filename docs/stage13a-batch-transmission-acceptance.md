# Stage 13A: controlled batch MLLP transmission acceptance

Checkpoint completed after explicit user native acceptance (all eight checks):
**45e7cdbad9a32cdaebb7834ae0026b10914622aa**, `Stage 13A: Add controlled batch MLLP transmission`,
parent e575ebe47921f33b3431d32bf26a5fc87aee4a0a. The exact accepted 37 paths, source/test
hashes and staged blobs were verified, one local commit created, and clean post-commit
tree/index confirmed. No repeated completed gate, amendment, push or publication.
Stage 13B is now separately authorized and remains uncommitted pending native review.
Earlier pre-checkpoint status/inventory/stop statements below are historical evidence.

Status: complete automated gate passed; the user explicitly confirmed **all eight
native Windows acceptance checks passed**, including Generation Report UI polish.
Exactly one local checkpoint is authorized with message
`Stage 13A: Add controlled batch MLLP transmission`. All 37 final verified paths
matched the completed gate hashes before acceptance-only documentation updates;
all 27 source/test hashes still match. No repeated implementation or test gate is
necessary. After this checkpoint, Stage 13B is separately authorized and must stay
unstaged/uncommitted. Earlier pending/stop statements below are historical records.
Verification reconciled on 2026-10-22.
Stage 13A remains unstaged/uncommitted. No amendment, push, tag, package, deployment,
publication or release is authorized. Stop after report polish for explicit native
acceptance. The single local checkpoint is conditional on that acceptance; Stage
13B cannot begin until the checkpoint is authorized and created.

## Final UI polish (Part A)

The existing FileDocumentView placed an expanded, 180-pixel scrollable report below
RAW navigation, consuming document space even when the user was not reviewing it.
The report itself was already immutable and document-owned before publication.
That ownership and the existing full formatter are preserved. RAW now shows only
one compact metadata-derived row and View report button; nonreport documents hide
the row. The normal View menu reopens the optional Generation Report Dock tool.

The tool projects the selected generated document's report, clears for other tabs,
and retains all mapping/import/output/exclusion/warning/limitation details in a
scrollable selectable layout. Closing/reconstructing/resetting it disposes only
its projection/subscription. The document retains the report. Dock/floating geometry
and visibility use the existing placement DTO; no report data enters layouts, HL7
files or restart persistence. Later edits are not reassessed and the original
generation counts remain unchanged, as the existing report notice explains.

Focused shell tests caught an explicit focus notification arriving before Dock
attached the new view. The model now retains only that explicit pending request
until attachment, and the view consumes it after layout. Passive tab/navigation/
edit refreshes never request focus. Initial harness/save-destination diagnostics
are retained alongside the successful focused results, without weakening save consent.

Permanent headless regressions cover compact height/metadata/hidden state, complete
long warning reports and scrolling, live light/dark changes, actual window indicator
and menu open/focus without duplicate tools, generated/nonreport tab switching,
document closure, tool close/reopen, detach/reconstruction, dock/float/resized layout
restore and reset, original report identity across navigation/edit/Undo/Redo/Save,
and exact generated UTF-8 bytes before and after those operations. Existing Stage
11 UI tests now assert the docked projection plus compact indicator, retaining
mapping/privacy/history coverage. A combined Desktop run also exposed a pre-existing
batch-test timing assumption: cancellation can finish before async window close
reaches Closed/model disposal. That test now waits for actual window closure before
checking disposal, retaining its exact receipt/client/Disposed/no-restart assertions.
No production batch/transport behavior changed. Stage 12 and Workspace batch tests
remain unchanged; diagnostic details are in boundary-diagnostics.txt.

### Part A verification (2026-10-22)

Evidence: ignored artifacts/stage13a-report-polish. Initial failing harness/focus
logs and the reviewed shutdown-boundary diagnostic are retained. No permanent
coverage removed; no disposable payload exports or probes added to the source tree.

| Check | Result |
| --- | --- |
| Locked restore | Passed; all projects up to date (`final-restore.log`) |
| Release build, warnings as errors | Passed; **0 warnings/errors**, 3.58s (`final-build.log`) |
| Focused Core FHIR/import/history + ACK | **109 passed**, 1.191s (`final-core.log`, `.xml`) |
| Focused Workspace FHIR publication + TCP/MLLP/batch | **129 passed**, 27.719s (`final-workspace.log`, `.xml`) |
| Focused Desktop report + Stage 11–13A | **44 passed**, 12.369s (`final-desktop.log`, `.xml`) |
| Shell menus and ribbon | **3 passed**, 1.012s (`final-shell.log`, `.xml`) |
| Full solution | **1,884 passed**, 0 failures/skips, 3m 33.082s (`final-solution-tests.log`) |
| Complete Workspace stress | **890 passed**, 0 errors/failures/skips/unrun, **328.532s**, unfiltered `HL7WORKBENCH_STRESS=1` with prior environment restored (`final-workspace-stress.log`, `.xml`) |
| Both existing Release-layout ScriptHost lifecycles/isolation | Passed (`final-scripthost-smoke.log`); verifier only, no publish command |
| Script API reference synchronization | Passed (`final-reference-sync.log`) |
| Protected/frozen integrity and whitespace | Passed after stress: **13** protected + **27** frozen source/test + **4** accepted binary hashes; **8** original HL7 Git blobs; original artwork + **2** icon derivatives/formerly excluded HL7; tracked and all **15** new-file whitespace checks (`final-integrity.log`) |

All focused cases have zero errors/failures/skips/unrun. The complete solution
increased from 1,878 to 1,884 solely through six new report UI cases. The complete
existing Format implementation is unchanged. Core/Workspace/Transport/Workspace.Tests
binaries still match all four accepted batch baseline hashes, confirming no
generation, transport or publication changes. The 27 current implementation/test
paths are frozen in final-implementation-hashes.json. Current inventory is 37 paths
(22 modified tracked, 15 new); 18 of the original 28 accepted paths are byte-identical,
10 received the documented polish/inventory/test-boundary changes, and nine additional
paths contain the report UI, its documentation or retained regression updates.
Final post-stress audit confirms the index is empty and HEAD remains the Stage 12
checkpoint e575ebe47921f33b3431d32bf26a5fc87aee4a0a. The formerly excluded HL7 was
hash-checked only and retains D254E8BBBFC98C2E377EC9149F2D46415756E0CD96CE8DED0D9FB8548F5EFFA1.
No code/test changed after the final build and complete solution/stress runs;
post-verification-file-hashes.json captures all 37 final source/test/doc paths.
Useful ignored logs/XML/hash manifests and the repeatable integrity audit remain
under artifacts/stage13a-report-polish; prior accepted evidence remains intact.
No staging, commit, history mutation, publish operation or Stage 13B work occurred.

### Part A native Windows retest (required before checkpoint)

1. Generate a Selected Patient history and an Entire Import history from the local
   synthetic FHIR samples. Each unsaved tab should have one compact FHIR/message/
   patient row below RAW navigation; no expanded report in the viewer. Counts must
   match the original generation result. Ordinary HL7 and ACK tabs have no row.
2. Click View report, then click it again. Expect one Generation Report tool,
   focused and readable, with complete counts, exclusions, warnings, mapping/seed,
   source/session attribution and limitations. Scroll to the final review notice.
3. Switch between both generated tabs and an ordinary HL7 tab with the tool docked
   and floating. Verify the matching report or clear empty state; no stale content.
4. Resize narrow/wide; dock/float/close/reopen via View → Generation Report. Switch
   light/dark themes, save/restore layout and reset layout. Placement may persist;
   closing/reopening must retain the report for each still-open generated document.
5. Navigate and edit generated HL7; use Ctrl+Z/Y including Edit Mode off and Inspector/
   Navigator focus. Ensure normal navigation/edit focus and unchanged report identity
   and original counts. Save As to scratch; compare generated bytes before edits
   and after undo. Close generated tabs; the tool must clear or follow the next tab.
6. Recheck Stage 12 single-send and a Stage 13A loopback batch, ACK review/export and
   tool/shutdown behavior. Opening/restoring the report must cause no network activity.
7. Restart: placement may restore, but reports/import sessions are not persisted.
   Reopening a saved HL7 file has no generation report, as before this polish.

**Stop for explicit native acceptance of Part A. Do not stage or commit yet, and
do not begin Stage 13B.**

## Baseline and scope

Actual Git inspection found Stage 12 checkpoint
`e575ebe47921f33b3431d32bf26a5fc87aee4a0a`, message `MLLP functionality`, parent
`af717a66608d3277402bd6696cd4f50712c7ecf5`. The user confirmed native acceptance.
The starting tracked/index/untracked inventory was clean. The checkpoint tracks the
formerly excluded `samples/synthetic-only/stage5b-repeated-patients_TEST.hl7`;
Stage 13A preserves its existing SHA-256 without opening, modifying or staging it.
No Git history or ignore rule is changed to reinterpret that user-created commit.

Workspace implements immutable ordered selections, revision/identity and pinned
endpoint/options; sequential production MllpConnection execution; ACK-gated advancement;
minimum/default pacing; pending/effective pause; resume remaining only; cancel/drain;
bounded phase-specific results and explicit atomic privacy-safe CSV. Desktop adds a
themed lazy dockable tool with source/profile/selection/options, Prepare/Review/Start,
Pause/Resume/Cancel, progress, inspectable ACKs and report export. Closing/reset or
application shutdown cancels/drains owned work. Layout restore never starts a run.

Only a complete write plus correlated AA counts as application accepted. Default
Stop halts for every other outcome, including CA-only/NE/NE. Continue is an explicit
review choice and never retries. Source/configuration changes before Start require
fresh preparation/review; changes after Start cannot alter authorized payloads.
No second MLLP stack, package, Core/parser/save/message identity/ScriptHost redesign.
See [guide](batch-transmission.md) and [ADR 0017](decisions/0017-controlled-batch-transmission.md)
for exact lifecycle, bounds, report schema, privacy and limitations.

## Integration findings and permanent coverage

The first batch Workspace run failed three cases while reading listener Clients:
TcpClient disposal can clear its Client property before the worker's dictionary
entry is removed. `SafeRemote` now tolerates that actual null interval. This narrow
fix preserves Stage 12 wire/ACK/capture/confirmation behavior; its sender/capture
and existing regression test bytes remain intact. The original failing log/XML is
retained as `artifacts/stage13a/workspace-first.*`.

The initial real backpressure test assumed a first-byte event preceded local write
completion. Windows accepted the complete 1 MiB frame first. The regression now
checks actual completion/phase evidence and never claims acceptance/retry. A second
controlled internal exchange-boundary test deterministically cancels an explicitly
incomplete write and verifies Canceled, uncertainty and remaining NotAttempted rows.
The public production constructor always uses MllpConnection, including all real
loopback tests. No substitute transport is exposed through Desktop.

The first complete solution run passed 1,877/1,878 cases; its sole failure was a
legacy ribbon inventory assertion still expecting only the two Stage 12 buttons.
That assertion now includes Batch Transmission and retains the existing geometry,
focus and enabled-state checks. Its focused rerun passed. The initial full-run log
remains `pre-ribbon-adjustment-solution.log`; the complete final gate covers the
updated assertion. No implementation changed for that test-inventory correction.

A later complete run exposed the NE/NE no-ACK test checking listener receipt count
immediately after local sender completion (0 observed versus 1 expected). NE/NE has
no response barrier. The test now waits, within the existing five-second deadline,
for the actual production receipt and retains the same exact count/classification
assertions. Continue-with-no-ACK uses the same receipt barrier. This is observable
synchronization, not a guessed sleep or transport change. The original failure is
retained in `pre-receipt-barrier-solution.log`; focused/full/stress were rerun and
passed for the updated test bytes. The earlier 890-case passing stress evidence is retained under
`pre-receipt-barrier-stress.*`.

Permanent synthetic-only coverage includes all/range/explicit ordering, immutable
selected payloads, edits/closure/tab switches, custom delimiters/CRLF/MLLP envelopes,
Stage 11 generated histories, real exact sequential loopback and correlated AA,
original/enhanced AA/AE/AR/CA/CE/CR/NE semantics, ACK-gated advancement, stop/continue,
default/minimum pacing after successes/failures/timeouts, pause pending/effective,
identity-bound resume without duplicates, cancellation before first send/during
write/ACK wait, failed connection recovery on the next message only, listener stop,
non-loopback/TLS/stale destination/review guards, snapshot/ACK limits, result counts,
safe atomic CSV/overwrite/cancellation/source protection/formula encoding, retained
ACK viewer publication, themes/narrow views/detach/reconstruction and real Dock
close/layout reset/application shutdown. Existing Stage 12 tests are retained.

## Accepted batch verification baseline (before Part A polish)

Evidence lives in ignored `artifacts/stage13a`; early diagnostic failures/build logs
are retained separately from final gate logs. No payload exports or disposable
probes are retained in the source tree. No permanent coverage removed.

| Check | Final evidence/result |
| --- | --- |
| Locked restore | Passed, all projects up to date (`final-restore.log`) |
| Release build with warnings as errors | Passed, **0 warnings/errors**, 2.38s (`final-build.log`) |
| Focused Core ACK | **41 passed**, 0 errors/failures/skips/unrun, 0.196s (`final-core.log`, `.xml`) |
| Focused Workspace and Transport | **106 passed**, 0 errors/failures/skips/unrun, 23.862s (`final-workspace.log`, `.xml`) |
| Focused Desktop single-send/batch | **26 passed**, 0 errors/failures/skips/unrun, 4.547s (`final-desktop.log`, `.xml`) |
| Corrected legacy ribbon regression | **1 passed**, 0 errors/failures/skips/unrun, 0.648s (`final-ribbon.log`, `.xml`) |
| Full solution | **1,878 passed**, 0 failures/skips, **3m 10.673s** (`final-solution-tests.log`) |
| Complete Workspace stress | **890 passed**, 0 errors/failures/skips/unrun, **292.458s**, unfiltered with `HL7WORKBENCH_STRESS=1` scoped/restored (`final-workspace-stress.log`, `.xml`) |
| Both Release-layout ScriptHost lifecycles | Passed: current/whole Main, shared state, CSV/deferred output/Complete, clean exit, worker hashes and execution-dependency isolation (`final-scripthost-smoke.log`); no publish command |
| API reference synchronization | Passed (`final-reference-sync.log`) |
| Integrity and Git | Passed after the final stress run: **19** frozen code/test hashes, **13** protected hashes, **8** original HL7 Git blobs, original artwork/PNG/ICO/formerly excluded HL7 unchanged; all **12** new-file whitespace checks and `git diff --check`; empty index, unchanged Stage 12 HEAD (`final-integrity.log`, `final-git-status.log`) |

The 19 implementation/test files are frozen in `final-implementation-hashes.json`.
All pre-existing Stage 12 sender/capture/framing/test hashes match the checkpoint;
only the demonstrated listener inspection race changed. A final presentation-only
audit added explicit completed/unsuccessful/transport-failure/skipped aggregates.
The Desktop tests exercise the updated labels on accepted and canceled results.
Core, Transport, Workspace and Workspace.Tests binaries remained **byte-identical**
to the completed stress run after rebuilding Desktop; that historical evidence is in
`verified-workspace-binaries.json` and `pre-receipt-barrier-binary-integrity.log`.
The later receipt-barrier correction changes Workspace.Tests only; production Core/
Transport/Workspace binary hashes remain unchanged. The final full/stress reruns
covered the updated tests and Desktop counters. Current binary hashes are retained
in `final-workspace-binary-hashes.json`; no code/test changed after the final build,
focused checks, full solution and stress runs. The full solution
increased from the accepted 1,828 to 1,878 cases: **36 new Workspace** and **14 new
Desktop** cases, with all existing tests retained. The legacy shell inventories and
expectations now include the authorized batch menu/ribbon action; their other
assertions remain. No tests were weakened to bypass a transport/lifecycle failure.

The complete Workspace stress command ran without a filter:

```powershell
$env:HL7WORKBENCH_STRESS = '1' # Scoped to the run; prior environment restored afterward.
dotnet run --project tests/HL7Workbench.Workspace.Tests -c Release --no-build --no-restore -- -result-xml artifacts/stage13a/final-workspace-stress.xml
```

The accepted batch inventory before Part A was **28 paths (16 modified tracked, 12 new)**, all unstaged and
uncommitted. `HEAD` still points to the user Stage 12 checkpoint. The formerly
excluded HL7 remains tracked exactly as the user checkpoint left it, with SHA-256
`D254E8BBBFC98C2E377EC9149F2D46415756E0CD96CE8DED0D9FB8548F5EFFA1`.
It was not opened/modified/staged. Original source artwork and both derivatives
retain their protected hashes. No package/lock/existing persistence-schema change, Git mutation, publish
command, Stage 13B work or permanent-test cleanup occurred. Ignored audit logs/XML/
hash manifests, including reviewed failure diagnostics, are intentionally retained.

## Current changed-file inventory (including Part A)

37 paths: 22 modified tracked files and 15 new files, all Stage 13A source/tests/docs.
No protected fixture/artwork/formerly excluded HL7 or package/lock file changes.
Baseline is the user Stage 12 checkpoint above. The index remains empty.

```text
README.md
docs/PROJECT_CONTEXT.md
docs/architecture.md
docs/batch-transmission.md
docs/decisions/0016-interactive-mllp.md
docs/decisions/0017-controlled-batch-transmission.md
docs/stage12-tcp-mllp-acceptance.md
docs/stage13a-batch-transmission-acceptance.md
docs/synthea-fhir-import.md
docs/tcp-mllp.md
src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/MainWindow.BatchTransmission.cs
src/HL7Workbench.Desktop/MainWindow.Editing.cs
src/HL7Workbench.Desktop/MainWindow.Shell.cs
src/HL7Workbench.Desktop/MainWindow.axaml
src/HL7Workbench.Desktop/MainWindow.axaml.cs
src/HL7Workbench.Desktop/ViewModels/BatchTransmissionViewModel.cs
src/HL7Workbench.Desktop/ViewModels/GenerationReportViewModel.cs
src/HL7Workbench.Desktop/Viewer/BatchTransmissionView.cs
src/HL7Workbench.Desktop/Viewer/FileDocumentView.cs
src/HL7Workbench.Desktop/Viewer/GenerationReportIndicator.cs
src/HL7Workbench.Desktop/Viewer/GenerationReportView.cs
src/HL7Workbench.Desktop/Viewer/UtilityRibbon.cs
src/HL7Workbench.Desktop/Viewer/ViewerRequests.cs
src/HL7Workbench.Transport/MllpListener.cs
src/HL7Workbench.Workspace/ActivityLog.cs
src/HL7Workbench.Workspace/BatchTransmission.cs
src/HL7Workbench.Workspace/BatchTransmissionReport.cs
src/HL7Workbench.Workspace/BatchTransmissionSession.cs
src/HL7Workbench.Workspace/Properties/AssemblyInfo.cs
tests/HL7Workbench.Desktop.Tests/BatchTransmissionViewTests.cs
tests/HL7Workbench.Desktop.Tests/GenerationReportToolTests.cs
tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs
tests/HL7Workbench.Desktop.Tests/SyntheticHistoryScopeViewTests.cs
tests/HL7Workbench.Desktop.Tests/SyntheticPatientsViewTests.cs
tests/HL7Workbench.Workspace.Tests/BatchTransmissionTests.cs
```

## Known limitations

- Fixed maximum 1,000 selected positions and 32 MiB aggregate payloads; one message
  remains capped by the Stage 12 1 MiB contract. Preparation rejects, never truncates.
- Existing per-message capture temporarily enumerates bounded source metadata;
  very large logical sources can prepare more slowly. Only selected payloads remain.
- 16 MiB retained ACK bytes; later bytes can be omitted explicitly while outcome,
  frame count/correlation/acceptance metadata remain. Unavailable bytes cannot open.
- Pacing is an additional 250–60,000 ms after each completed attempt, default 1,000.
  It is not an overload guarantee. Review time estimates exclude connection/ACK waits.
- Uncertainty overlaps timeout/cancellation/unknown classifications. TCP completion,
  CA or a finished run is not application acceptance. Cancellation cannot prove
  nondelivery. Reports are safe metadata, not a durable delivery ledger.
- No restart resume, retry, scheduled/unattended run, concurrent load, connection
  pooling, TLS, callback-connection ACKs or Stage 13B. Results are ephemeral and are
  released on tool close. Exports are explicit local files, capped at 4 MiB.
- Headless/socket tests do not replace native Windows acceptance.

## Native Windows acceptance checklist

1. Launch normally; verify no connection, listener or batch starts from saved layout.
2. Open **View → Batch Transmission** and the TCP/MLLP ribbon action. Test a narrow
   dock, floating host, resize and both themes; controls/results must remain usable.
3. Import synthetic FHIR and generate a Stage 11 combined patient history. Keep its
   Generation Report open. Select a saved loopback UTF-8 plain-TCP endpoint.
4. Prepare all messages. Verify complete logical count/order, source identity/revision,
   selected payload byte count and no network activity. Try a range and explicit
   nonadjacent positions; duplicate/invalid/over-limit selections must be refused.
5. Check default 1,000 ms and Stop policy. Reject 249/zero; accept 250 and slower
   intervals. Review count/positions, host/pinned IP/port, scope/security/encoding,
   batch/source identity, pacing, ACK policy, minimum time and impact warning.
6. Start the own loopback listener on 127.0.0.1:2575, UTF-8 strict, Positive Test ACK.
   Press Start and explicitly authorize. Leave review open beyond the listener's
   idle timeout as well. Each actual message must use a fresh connection.
7. Verify every exact message arrives once in source order with a completed frame,
   correlated AA, complete local write and clean connection closure. No next attempt
   precedes the active ACK result and post-exchange interval. All accepted counts
   must match; source identities/working state/Generation Report must stay intact.
8. Open individual ACK 1/2 in ordinary unsaved viewer tabs. Compare MSA-2 with the
   selected source MSH-10 locally. Test enhanced AL/AL → CA then AA; CA-only remains
   application unknown, never application accepted.
9. Export CSV explicitly. Inspect per-position rows/summary and counts, UTC times,
   safe phases/categories; confirm no raw HL7, patient/control IDs, clinical values,
   source paths/titles, profile names/notes/hostnames or arbitrary errors. Exercise
   overwrite consent and canceled export; source HL7 must be protected.
10. Repeat negative AE/AR/CE/CR with default Stop: exactly one unsuccessful attempt,
    remaining NotAttempted, no retry. Repeat explicitly reviewed Continue: each next
    position occurs once after pacing; the failed position never re-enters the queue.
11. Test no ACK/conditional silence: timeout/unobserved/uncertain evidence, no false
    application success. Confirm failed connection attempts also respect pacing.
12. Pause during an active ACK wait: PausePending, active result may finish, then
    Paused and no next connection. Resume the same batch: remaining positions only.
    Repeat pause during pacing. Reconfiguration while active/paused must be blocked.
13. Cancel before first send and during ACK wait/large-write activity. Verify distinct
    canceled versus unattempted rows, actual write evidence and uncertainty, no
    automatic retransmission, closed sockets and retained completed results.
14. Edit/close/switch the source after preparation/review and before Start: no send;
    prepare/review again. During a running batch, edit/switch/close the source: the
    remaining payloads must match the explicitly authorized captured revision.
15. Change profile/endpoint/pacing/ACK policy/selection after review and try Start:
    fresh preparation/review required. Change batch identity and try stale Resume.
    No authorization may silently retarget a different destination.
16. Using only an authorized test environment, reject an extra non-loopback warning
    and verify no traffic. Its confirmation must identify the exact destination;
    private IP/Test endpoint must not bypass it. TLS must fail without plaintext.
17. Stop listener during a batch: preserve completed-write/missing-ACK uncertainty
    and Stop/Continue semantics. Restart, then explicitly prepare/review a new run;
    no old message may automatically resume/retry.
18. Detach/reconstruct the live view: preserve one model/run. Close the tool, reset
    layout, and close the application during a pending exchange: cancel and drain
    without orphaned tasks/sockets. Reopen/restore: empty idle tool, never resume.
19. Inspect Activity Log, endpoint JSON and saved layout: typed counts/placement only,
    no payload/identifiers/results/authorization. Check source editing, Ctrl+Z/Y,
    scripts, tabs, generation and existing Stage 12 delayed single-send regression.
20. Verify unchanged protected fixtures/artwork/formerly excluded HL7. Record native
    results and remaining limitations before authorizing any Stage 13A commit.

Stop for user review after the complete automated gate. Do not stage or commit.
