# Stage 13B: controlled MLLP load testing acceptance

Status: complete final automated gate **passed** (2026-10-22). The user explicitly
confirmed **all 16 native Windows acceptance checks passed** and authorized one local
checkpoint: `Stage 13B: Add controlled MLLP load testing`. The exact 29-path final
manifest, 20 implementation/test hashes, protected assets and empty index were
rechecked before these acceptance-only Markdown updates. No code/test changed and
completed verification was not repeated. Earlier pending/stop statements below are
historical records. Branding is a separate, uncommitted follow-up. No push, tag,
package, deploy, publish or release is authorized.

## Checkpoint and scope

The user confirmed all eight Stage 13A native checks passed and authorized the one
local checkpoint `45e7cdbad9a32cdaebb7834ae0026b10914622aa`, message
`Stage 13A: Add controlled batch MLLP transmission`, parent
`e575ebe47921f33b3431d32bf26a5fc87aee4a0a`. All 37 accepted paths matched the final
gate hashes before acceptance-only Markdown updates; all 27 source/test hashes and
protected assets matched. The staged inventory/blobs were reviewed and matched
exactly, then one commit was created. The post-commit tree/index were clean. Evidence
is retained in ignored artifacts/stage13a-checkpoint; no completed tests were rerun
unnecessarily. That accepted gate passed 1,884 solution and 890 Workspace stress cases.

Stage 13B adds a separate explicitly authorized load session using existing batch
preparation, transport, captures, profiles and ACK policy. One global monotonic
dispatcher, 1–8 slots, 0.1–20/s target, 1–300 seconds, 1–5,000 attempts, conservative
runtime stop thresholds, explicit unchanged replay, exact aggregate/latency metrics,
bounded retained metadata and atomic safe reports are implemented. Desktop supplies
the optional themed Dock tool, synthetic attestation, source selection, exact-target
review/authorization, additional non-loopback consent and explicit exports.

No Stage 12/13A sender/capture/framing/ACK policy is replaced. Transport only gains
optional completed-write and complete-frame timing metadata. Core and ScriptHost
remain unchanged; no package, lockfile, persistence schema, data or artwork change.
The earlier listener disposal-race correction remains exactly in the checkpoint.
See [guide](load-testing.md) and [ADR 0018](decisions/0018-controlled-load-testing.md)
for bounds, scheduling, measurement definitions, report schema and lifecycle.

## Regression coverage and findings

Permanent synthetic-only tests cover hard bounds/defaults, one global rate gate and
no accumulated permits, eight held concurrent slots, no duplicate Start, stale source/
tab/configuration/attestation rejection, pinned destination and non-loopback consent,
TLS fail-closed, real production loopback exact payloads, AA/AE/AR/CA/CE/CR/NE modes,
missing/mismatched ACKs, out-of-order completion with source identity retained,
unchanged replay at concurrency one/three, duration and attempt/source limits, stop
thresholds, canceled/incomplete-write and real ACK-wait uncertainty, socket draining,
exact rates/percentile sample rules, 1,005 completions with only 1,000 retained rows,
fresh repeated runs, Stage 11 output/report preservation, privacy/formula/atomic/source
export guards, themes, short-dock Stop reachability, detach/reconstruction, full
scrollable position review, layout/dock/float/reset/close/application shutdown.

Initial build diagnostics and all failed checks are retained under artifacts/stage13b.
The first Workspace run caught failed TLS preparation being rethrown on disposal;
typed expected preparation failures are now consumed during owned drain. The initial
retention test used many real tiny timer waits; deterministic exchange work now advances
the test TimeProvider while the public bounds and 1,005 exact assertions remain intact.
An initial Desktop fixture had an invalid header; correcting it also exposed the need
to handle InvalidDataException explicitly (it is not IOException). A permanent malformed
source regression verifies safe fixed status and clean disposal. A dedicated scrollable
load consent dialog keeps the full bounded position list and Cancel/Confirm reachable,
without changing Stage 12/13A dialogs. No tests are removed or assertions weakened.

## Final verification

Evidence: ignored artifacts/stage13b. Final gate results were reconciled against
the frozen source/test bytes. Useful diagnostic logs/XML/hash manifests are retained; no generated
payload exports or disposable probes are included in the source tree.

| Check | Result |
| --- | --- |
| Locked restore | Passed; all projects up to date (`final-restore.log`) |
| Release build with warnings as errors | Passed; **0 warnings/errors**, 3.31s (`final-build.log`) |
| Focused Core ACK/FHIR | **109 passed**, 1.040s (`final-core.log`, `.xml`) |
| Focused Workspace load/batch/transport/FHIR | **174 passed**, 31.134s (`final-workspace.log`, `.xml`) |
| Focused Desktop Stage 11–13B | **64 passed**, 13.383s (`final-desktop.log`, `.xml`) |
| Shell menu/ribbon regressions | **3 passed**, 0.872s (`final-shell.log`, `.xml`) |
| Full solution | **1,949 passed**, 0 failures/skips, **3m 20.356s** (`final-solution-tests.log`) |
| Complete unfiltered Workspace stress | **935 passed**, 0 errors/failures/skips/unrun, **329.422s**, `HL7WORKBENCH_STRESS=1` scoped/restored (`final-workspace-stress.log`, `.xml`) |
| Both existing Release-layout ScriptHost lifecycles/isolation | Passed (`final-scripthost-smoke.log`); no publish command |
| Script API reference synchronization | Passed (`final-reference-sync.log`) |
| Protected/frozen integrity and whitespace | Passed after stress: **13** protected hashes, **20** frozen implementation/test hashes, **8** original HL7 Git blobs, original artwork + **2** icon derivatives/formerly excluded HL7, tracked + all **13** new-file whitespace checks (`final-integrity.log`) |

All focused cases have zero errors/failures/skips/unrun. The new load tests comprise
45 Workspace and 20 Desktop cases; all prior permanent tests remain. The final 20
implementation/test paths are frozen in final-implementation-hashes.json, including
transport timing metadata and the shared shell regression expectations. No Core,
Scripting, ScriptHost, listener, project/lockfile, protected fixture or artwork changes.
The complete suite and stress run cover these exact source/test bytes; no code/test
changed after the final build/gate. Full solution increases by 65 cases from 1,884
to 1,949, and complete Workspace stress by 45 from 890 to 935. No existing permanent
coverage removed. All 29 final source/test/doc hashes are retained in
post-verification-file-hashes.json; documentation-only result reconciliation follows
the gate. HEAD/parent and empty index remain confirmed, with 16 modified tracked
and 13 new paths, all unstaged/uncommitted. The formerly excluded HL7 was hash-checked
only, preserving D254E8BBBFC98C2E377EC9149F2D46415756E0CD96CE8DED0D9FB8548F5EFFA1.
Only the one authorized Stage 13A Git checkpoint was created; no Stage 13B staging,
commit, amendment, push/tag/package/deploy/publish/release or later milestone work.

## Known limitations

- Target controls global admission, not achieved receiver processing/throughput.
  Concurrency can reorder clinical events. Limits do not guarantee receiver safety.
- The built-in listener independently defaults to four clients, 64 retained receipts
  and 16 MiB. Full admission rejects without AA; longer runs require explicit user
  history clearing or an appropriately configured authorized loopback receiver.
- Replay retains identical control IDs; duplicate rejection is an outcome, not a
  reason to rewrite payloads or retry. Source snapshots remain bounded by Stage 13A.
- First 1,000 completion rows only; omission is explicit and aggregate/latency counts
  remain exact. Up to 5,000 samples per phase are retained; small samples suppress
  unsupported percentiles. No ACK/payload byte retention or raw viewer publication.
- ACK timing includes invalid complete frames; connect timeout is not missing ACK.
  Commit/write completion is not application acceptance. Cancellation/timeout can
  leave delivery uncertain, including after a stop threshold cancels other slots.
- Rate thresholds use completed outcomes and default to five samples; already
  admitted attempts may finish/cancel after a stop. OS scheduling is not real-time.
- Plain TCP only; no TLS fallback, pooling, automatic retry, failover, discovery,
  restart resume, schedules, durable journals, telemetry or cloud dependencies.
- Native Windows DPI, keyboard, dialogs, docking geometry and real receiver behavior
  still require manual acceptance. Automated tests contact only loopback sockets.

## Native Windows acceptance checklist

1. Launch with existing saved layout: no sender connects/sends and no listener starts
   automatically. Stage 12 Connection Manager/Listener and Stage 13A Batch still work.
2. Open View → Controlled Load Testing or TCP/MLLP → Load Testing. Verify idle state,
   loopback default and no active attempts. Preparation requires synthetic attestation.
3. Open local synthetic HL7 or generate a Stage 11 history. Select all/range/explicit
   positions and verify logical identity/order and unchanged generated report/bytes.
4. Start a UTF-8 Positive Test ACK loopback listener (127.0.0.1:2575), save its profile
   in Connection Manager, then reload/select it. Inspect default concurrency 1,
   global target 1/s, duration/attempt cap 60, Replay off and conservative thresholds.
5. Prepare: listener must receive no connection/payload. Review exact hostname, pinned
   IP/port/security, positions, source revision, rate/concurrency/duration/attempt cap,
   replay and stop thresholds. Scroll a long review; Cancel remains reachable/focused.
6. Cancel review/Start and confirm no send. Authorize one low-rate loopback run; verify
   one connection per attempt, exact payloads, AA correlation and clean closure.
7. Check attempts, connections, full writes, ACK frames, commit/application acceptance
   as separate counters. Target and achieved attempt/completion/accepted rates differ
   appropriately; no UI labels claim processing success from write completion.
8. Increase concurrency within 1–8. Check active count stays bounded; note possible
   clinical reordering. Run Replay deliberately; repeated original MSH-10 values and
   clinical data must remain identical. No failed message is automatically retried.
9. For percentile display, use a small captured set with explicit Replay, target 5/s,
   duration 30s, attempt cap 120. Review/authorize again. Expect p95 after 20 applicable
   samples and p99 after 100; smaller samples explicitly say insufficient. Check all
   four phases, min/max/average and elapsed/remaining/stop reason. With the built-in
   listener, explicitly Clear history before 64 receipts/16 MiB fills (for small
   messages, at 40 and 80 receipts). Load counters must remain intact. Its default
   client cap is four; eight concurrent waits need a receiver configured for eight.
10. Use negative ACK modes to observe AE/AR/CE/CR and conservative rejection stop.
    Enhanced AL/AL produces separate CA then AA; CA-only never means application AA.
11. Use no ACK to exercise timeouts and uncertainty. Increase concurrency cautiously
    within bounds, observe active waits, then Stop. No new attempts start; sockets
    drain, canceled/uncertain evidence remains and nothing auto-resumes.
12. Exercise attempt/source exhaustion, short duration and stricter stop thresholds.
    Run configuration cannot be edited while active; Start cannot duplicate a run.
13. Edit/switch/close source or change endpoint/options/selection/attestation after
    review: Start must require fresh preparation/review. During an authorized run,
    later source changes must not alter captured bytes or retarget the destination.
14. Review a non-loopback profile only in an explicitly authorized test environment.
    Cancel its additional consent to verify no connection. A private IP or Test label
    must not suppress the warning. TLS selection must fail closed, with no TCP fallback.
15. Export CSV and text summary to scratch. Verify exact configuration/aggregates,
    stop reason, completion/source order, timing sample rules, uncertainty/omission
    disclosures and absence of patient/control IDs, clinical values, raw HL7/ACKs,
    hostnames/IPs, source paths, credentials and arbitrary exception text. Cancel an
    overwrite/export and verify no partial replacement; source files stay protected.
16. Dock/float/resize, including short/narrow docks: Stop stays reachable. Switch light/
    dark themes, reconstruct views, close/reopen and restore/reset layouts. No run,
    settings, source selection or authorization may survive tool close/restart.
17. Close tool/reset/application while waiting for ACK or a review/picker. Cancellation
    drains before teardown; late confirmation cannot start a run. Cancel a later unsaved
    prompt and verify stopped results remain inspectable until tool closure.
18. Recheck single-send delayed confirmation, sequential batch pause/resume/cancel,
    generated document navigation/editing/Undo/Redo/Save, report association, scripts,
    Activity Log privacy, protected fixtures/artwork and the formerly excluded HL7.

**Stop for native Stage 13B acceptance. Do not stage, commit, amend, push, tag,
package, deploy, publish, release or begin another milestone.**

## Changed-file inventory

29 paths: 16 modified tracked files and 13 new files, all Stage 13B source/tests/docs.
No protected fixture/artwork/excluded HL7, dependency, Core or ScriptHost edits.
The index is empty; HEAD is the single accepted Stage 13A checkpoint.

```text
docs/architecture.md
docs/batch-transmission.md
docs/decisions/0018-controlled-load-testing.md
docs/load-testing.md
docs/PROJECT_CONTEXT.md
docs/stage13a-batch-transmission-acceptance.md
docs/stage13b-load-testing-acceptance.md
docs/tcp-mllp.md
README.md
src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/MainWindow.axaml
src/HL7Workbench.Desktop/MainWindow.axaml.cs
src/HL7Workbench.Desktop/MainWindow.Editing.cs
src/HL7Workbench.Desktop/MainWindow.LoadTesting.cs
src/HL7Workbench.Desktop/MainWindow.Shell.cs
src/HL7Workbench.Desktop/Viewer/LoadTestingConfirmationDialog.cs
src/HL7Workbench.Desktop/Viewer/LoadTestingView.cs
src/HL7Workbench.Desktop/Viewer/UtilityRibbon.cs
src/HL7Workbench.Desktop/ViewModels/LoadTestingViewModel.cs
src/HL7Workbench.Transport/MllpConnection.cs
src/HL7Workbench.Workspace/ActivityLog.cs
src/HL7Workbench.Workspace/LoadTesting.cs
src/HL7Workbench.Workspace/LoadTestingReport.cs
src/HL7Workbench.Workspace/LoadTestingSession.cs
tests/HL7Workbench.Desktop.Tests/LoadTestingViewTests.cs
tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs
tests/HL7Workbench.Workspace.Tests/LoadTestingNetworkTests.cs
tests/HL7Workbench.Workspace.Tests/LoadTestingTests.cs
```
