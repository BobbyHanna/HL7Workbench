# Stage 12: interactive TCP/MLLP acceptance

User acceptance update: Stage 12, including the native send correction below, passed
native Windows acceptance and was committed by the user as
`e575ebe47921f33b3431d32bf26a5fc87aee4a0a` (`MLLP functionality`). Earlier pending
status and mandatory-stop statements below record the pre-acceptance verification.
Stage 13A passed separate native acceptance and has one local checkpoint authorized.
The user checkpoint also tracks the formerly excluded `_TEST.hl7`;
its bytes are preserved and it is not opened or changed by Stage 13A.

Status: initial native Windows acceptance **failed**; the send correction has
passed the complete automated gate. Native Windows retesting remains pending.
Stage 12 remains unstaged and uncommitted. No push,
tag, packaging, deployment, release, publication or Stage 13 is authorized.

## Native send failure: confirmed reproduction and correction

The native report used the app's own loopback listener, UTF-8/positive test ACK,
and a 504-byte message selected from 11. Confirm returned immediate Disconnected
with WriteStarted, while the listener timed out with zero completed payload.

The production path was traced end to end:

1. Desktop connects **before** capture/review and awaited a human dialog. The
   listener starts its per-client idle/frame deadline immediately on accept. The
   UI default is 10 seconds (the service-only default used in most tests is 30).
2. Workspace correctly captures one complete authoritative payload and strict
   encoding; the reproduced capture is exactly 504 bytes, with unchanged bytes.
   Core framing correctly creates `0B + 504 bytes + 1C 0D`, 507 wire bytes.
3. While confirmation remains open, the listener's idle deadline can close that
   checked connection. Sender IsConnected tracked an owned client handle, not a
   guarantee of peer liveness. It then reused the expired socket on confirmation.
4. The production write **was awaited**. The defect was stale connection lifetime,
   not a missing await/Flush. The exception was reduced to generic Disconnected,
   WriteStarted was set before awaiting WriteAsync, and every outbound history row
   was labeled Sent. That did not prove complete transmission.
5. The listener's timeout rows always used an empty Payload array. Their `0 bytes`
   meant zero **completed HL7 payload bytes**, and did not measure socket input or
   buffered partial payload. Original native evidence cannot establish those counts.
6. No complete message reaches ACK construction on that expired connection, hence
   no AA. Connect/capture/send cancellation sources are scoped correctly; production
   regression also proves cancellation/disposal of an already-completed connect token
   does not cancel a subsequent exchange. There was no automatic retry.

The permanent Desktop regression holds confirmation until the **actual** production
listener emits its idle timeout, then confirms a 504-byte message from an 11-message
source. Against the original implementation it failed with actual **Disconnected**
instead of ApplicationAccepted (`artifacts/stage12-native-send/repro-before.*`). No
guessed sleep advances the case; it waits for the observed listener deadline.

The review now explicitly authorizes retiring the check socket, establishing one
fresh connection to the same reviewed/pinned address, and sending one message.
There is no write attempt on the check socket, no failed-attempt retry, no alternate
address and no unsolicited reconnect. Connection/write/response cancellation and
timeouts remain bounded. Normal shutdown follows the awaited complete frame write
and required ACK phase(s). NetworkStream is unbuffered; adding Flush would not fix
the stale socket or establish receipt. [Microsoft documentation](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream.flush?view=net-10.0).

Results now distinguish WriteStarted, WriteCompleted, received response/ACK frames,
correlated application acceptance, safe failure phase and SocketError enum. A local
write completion alone is never application acceptance. History says Outbound,
with explicit not-attempted/incomplete/completed write evidence. Canceled/failed
operations never become successful delivery; partial writes retain uncertainty.
Listener rows separately show completed payload, connection-total socket bytes and
partial payload bytes, including partial headers/end-block-without-CR timeouts.
No raw exception text, stack, payload or identifiers enter Activity Log.

Correction paths (relative to the 39-path initial Stage 12 baseline):

```text
src/HL7Workbench.Desktop/ViewModels/TransportToolViewModel.cs
src/HL7Workbench.Desktop/Viewer/TransportToolView.cs
src/HL7Workbench.Transport/MllpConnection.cs
src/HL7Workbench.Transport/MllpFraming.cs
src/HL7Workbench.Transport/MllpListener.cs
src/HL7Workbench.Workspace/TransportSession.cs
tests/HL7Workbench.Desktop.Tests/TransportToolTests.cs
tests/HL7Workbench.Workspace.Tests/TransportNetworkTests.cs
docs/PROJECT_CONTEXT.md
docs/decisions/0016-interactive-mllp.md
docs/tcp-mllp.md
docs/stage12-tcp-mllp-acceptance.md
```

Permanent coverage includes the delayed 504-byte production sender/listener exchange,
exact payload/507 wire bytes, correlated AA, closure, completed-connect token lifetime,
cancel/close during review, a failed confirmed connection with no write/retry,
cancel/listener-stop after completed write while ACK is pending, expired raw socket
classification, and separate socket/partial/completed byte counts. All previous tests
remain. The initial immediate-approval tests never exercised the human idle interval.

### Final correction verification

Evidence was reconciled on 2026-10-22 under `artifacts/stage12-native-send`.
The complete stress process finished while the chat was interrupted; its finished
log and XML agree. Completed restore/build/focused/full-suite/smoke checks and the
completed stress run were preserved without unnecessary reruns. Final integrity
checks were repeated after recovery. Earlier counts below describe the initial
implementation, not this correction.

| Check | Actual result | Evidence file |
| --- | --- | --- |
| Locked restore | Passed; all projects up to date | `final-restore.log` |
| Release build with warnings as errors | Passed; **0 warnings, 0 errors**, 1.61s | `final-build.log` |
| Focused Core transport/ACK | **41 passed**, 0 errors/failures/skips/unrun, 0.145s | `final-core.log`, `.xml` |
| Focused Workspace/transport | **70 passed**, 0 errors/failures/skips/unrun, 8.345s | `final-workspace.log`, `.xml` |
| Focused Desktop transport | **12 passed**, 0 errors/failures/skips/unrun, 3.297s | `final-desktop.log`, `.xml` |
| Full solution | **1,828 passed**, 0 failures/skips, **2m 58.698s** | `final-solution-tests.log` |
| Complete Workspace suite with `HL7WORKBENCH_STRESS=1` | **854 passed**, 0 errors/failures/skips/unrun, **298.318s** | `final-workspace-stress.log`, `.xml` |
| Both existing Release-layout ScriptHost lifecycles | Passed: current/whole Main, shared state, CSV/deferred output/Complete, clean exit, worker hashes and execution-dependency isolation | `final-scripthost-smoke.log` |
| API reference synchronization | Passed against the authoritative embedded JSON | `final-reference-sync.log` |
| Post-stress integrity | **33** frozen implementation/project/test hashes, **13** protected hashes, **8** original HL7 Git blobs, source artwork/PNG/ICO and excluded HL7 hash unchanged | `final-integrity.log`, `final-implementation-hashes.json` |
| Git and inventory | `git diff --check` passed; **39** Stage 12 paths; empty index; unchanged Stage 11 HEAD | `final-integrity.log`, `changed-files.txt`, `final-git-status.log` |

The stress command ran the entire Workspace project, without a test filter:

```powershell
$env:HL7WORKBENCH_STRESS = '1' # Scoped to the run; prior environment restored afterward.
dotnet run --project tests/HL7Workbench.Workspace.Tests -c Release --no-build --no-restore -- -result-xml artifacts/stage12-native-send/final-workspace-stress.xml
```

The frozen 33-file manifest still matches the tested bytes; no implementation or
test was changed during recovery. The correction changes eight source/test paths
and four documentation paths already within the 39-path Stage 12 inventory.
Git status remains **18 modified tracked paths and 21 new Stage 12 paths**, plus
the one pre-existing excluded `_TEST.hl7`, unchanged and never opened/staged.
HEAD remains `af717a66608d3277402bd6696cd4f50712c7ecf5`; no Git mutation,
package, publication or Stage 13 work was performed. Ignored gate logs/XML/hash
manifests, including the original failing regression, are intentionally retained.
All permanent coverage and prior gate evidence remain intact. Headless and
loopback evidence does not grant native Windows acceptance.

Remaining limits: idle check sockets can still time out, and that empty check row
can remain visible; the approved transmission uses a different fresh connection.
Each explicitly confirmed send has one connection attempt and one write attempt.
If that attempt fails, it ends without retry. TLS/batch/asynchronous callback ACK
deferrals and the existing uncertain-delivery/privacy limits are unchanged. The
reproduction establishes the expired-socket defect; native retesting must confirm
the user's environment. New phase/socket/byte counters distinguish other failures.

## Accepted Stage 11 checkpoint

Exactly one local commit was created:
`af717a66608d3277402bd6696cd4f50712c7ecf5`,
`Stage 11: Add Synthea FHIR import and HL7 generation`, parent
`1b04e1849b2ba3c03d135cf324bae28b8af4cba8`.
The 44 paths matched accepted bytes (14 modified, 30 new), 1,705 solution and 784
complete Workspace stress results, locked restore, zero-warning Release build,
both worker lifecycles, API sync and protected integrity evidence. Only acceptance/
authorization records changed for checkpointing. Index and tracked tree were clean
after commit; the excluded `_TEST.hl7` remained untracked and unchanged. Evidence:
`artifacts/stage11-checkpoint-20261021`. No existing commit was amended/recreated.

## Implementation and boundaries

- Dedicated dependency-free Transport library: incremental byte framing, pinned TCP
  connections, bounded listener, explicit deadlines/cancellation and correlated
  original/enhanced ACK exchange. Core owns header/ACK semantic interpretation.
- Workspace captures one complete authoritative message with stable identity and
  revision; no preview, editor focus, Edit Mode or document serialization redesign.
  It owns whitelisted atomic profile persistence and bounded ephemeral sender history.
- Desktop Connection Manager and MLLP Listener are lazy, dockable themed tools. They
  coordinate explicit review/confirmation and open local unsaved message/ACK documents.
  Closing releases network sessions; detached/reconstructed views retain live model
  state. Restoring layout cannot reconnect, listen or send.
- AL/NE/SU/ER and AA/AE/AR/CA/CE/CR retain phase meaning. Conditional silence, commit
  acceptance and TCP write completion never become application acceptance. Every
  exchange closes its connection; reconnect/send are explicit, never retried.
- Loopback defaults, non-loopback confirmations, strict encodings, bounded buffers/
  clients/receipts, metadata-only Activity Log and unchanged ScriptHost boundary.
- TLS deliberately deferred; selecting it fails with no plaintext fallback. General
  same-socket ACKs only; batch/load/retry/scheduling and Stage 13 remain deferred.

See [ADR 0016](decisions/0016-interactive-mllp.md) and the
[transport guide](tcp-mllp.md) for exact bounds, encoding/BOM behavior, storage schema,
ACK eligibility, conditional policy, uncertain delivery and known limitations.

## Initial automated evidence (before the native failure)

Evidence is retained under `artifacts/stage12`; early diagnostic runs are retained
separately from final gate logs. Final locked restore passed. Release build passed
with **zero warnings/errors** (1.01s). Focused tests passed **41 Core** (0.139s),
**64 Workspace/transport** (5.243s), and **8 Desktop** (1.170s), no failures/skips.
The final full solution passed **1,818 tests**, zero failures/skips, 2m 56.578s.
The final complete Workspace stress gate passed **848 cases** in **290.398s**,
zero errors/failures/skips/unrun cases, with `HL7WORKBENCH_STRESS=1` scoped to the
run and restored afterward. The pre-envelope-audit runs passed 1,811 solution and
847 stress cases; those logs are retained with the pre-envelope-audit prefix.
Both final existing Release-layout ScriptHost smoke lifecycles
passed, including current/whole Main, CSV/deferred output/Complete, clean exit, hashes
and execution-dependency isolation; no publish command ran. API-reference sync passed.
All 13 protected hashes, eight original tracked HL7 blobs, source artwork/PNG/ICO and
the excluded HL7 hash passed. `git diff --check` passed. The 33 final
implementation/project/test hashes match the build/test state after stress. HEAD
still points to the Stage 11 checkpoint and the index is empty. No permanent
tests/fixtures were removed. Headless/socket tests
use synthetic data and loopback only; they do not replace native acceptance.

The initial complete run passed 1,810 tests. Final audit then added a shared,
idempotent listener shutdown drain (preventing a new generation from starting while
old clients drain), a post-connect cancellation check on the owning context, and a
permanent simultaneous Stop/disposal regression. The final gate covers those bytes.
An early closed-port regression assumed Windows would report refusal before its
one-second connect deadline. Windows may instead expire that deadline first; the
test now checks either bounded socket refusal or cancellation, no live connection,
no fallback and no extra listener receipt. The diagnostic XML is retained.

The protocol-reference audit shortened generated MSH-10 values to the legacy
20-character bound, declared the actual generated ACK encoding in MSH-18, escaped
generated metadata through Core, and required reply timestamp/processing correlation.
Additional permanent tests cover these cases. Send-time protocol parsing and local
payload/ACK parsing run off the UI context; cancellation precedes document publication.

## Exact Stage 12 inventory

39 paths: 18 modified tracked, 21 new. The excluded pre-existing
`samples/synthetic-only/stage5b-repeated-patients_TEST.hl7` is an additional untracked
path, preserved by hash and never opened/staged. The index is empty. Owned gate logs,
XML and hash manifests are retained in ignored `artifacts/stage12` and
`artifacts/stage12-native-send`; checkpoint audit evidence remains in
`artifacts/stage11-checkpoint-20261021`. No disposable payload
exports, probes or new clinical fixtures were introduced.

```text
HL7Workbench.sln
README.md
docs/PROJECT_CONTEXT.md
docs/architecture.md
docs/decisions/0016-interactive-mllp.md
docs/stage12-tcp-mllp-acceptance.md
docs/tcp-mllp.md
src/HL7Workbench.Core/Hl7Acknowledgment.cs
src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/MainWindow.Shell.cs
src/HL7Workbench.Desktop/MainWindow.Transport.cs
src/HL7Workbench.Desktop/MainWindow.axaml
src/HL7Workbench.Desktop/MainWindow.axaml.cs
src/HL7Workbench.Desktop/ViewModels/TransportToolViewModel.cs
src/HL7Workbench.Desktop/Viewer/ConfirmationDialog.cs
src/HL7Workbench.Desktop/Viewer/TransportToolView.cs
src/HL7Workbench.Desktop/Viewer/UtilityRibbon.cs
src/HL7Workbench.Desktop/packages.lock.json
src/HL7Workbench.Transport/HL7Workbench.Transport.csproj
src/HL7Workbench.Transport/MllpConnection.cs
src/HL7Workbench.Transport/MllpFraming.cs
src/HL7Workbench.Transport/MllpListener.cs
src/HL7Workbench.Transport/TransportEndpoint.cs
src/HL7Workbench.Transport/packages.lock.json
src/HL7Workbench.Workspace/ActivityLog.cs
src/HL7Workbench.Workspace/HL7Workbench.Workspace.csproj
src/HL7Workbench.Workspace/MessageDocument.Transport.cs
src/HL7Workbench.Workspace/TransportProfiles.cs
src/HL7Workbench.Workspace/TransportSession.cs
src/HL7Workbench.Workspace/packages.lock.json
tests/HL7Workbench.Core.Tests/TransportAcknowledgmentTests.cs
tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs
tests/HL7Workbench.Desktop.Tests/TransportToolTests.cs
tests/HL7Workbench.Desktop.Tests/packages.lock.json
tests/HL7Workbench.Workspace.Tests/TransportFramingTests.cs
tests/HL7Workbench.Workspace.Tests/TransportNetworkTests.cs
tests/HL7Workbench.Workspace.Tests/TransportWorkspaceTests.cs
tests/HL7Workbench.Workspace.Tests/packages.lock.json
```

## Native Windows acceptance

1. Launch the Release application normally. Confirm no listener/connection starts.
2. Open View → Connection Manager (also check the TCP/MLLP ribbon action).
3. Create/save a plain TCP profile for `127.0.0.1:2575`, UTF-8; close/reopen the tool
   and verify metadata persists but it stays disconnected. Verify TLS is unavailable.
4. Open MLLP Listener. Start default `127.0.0.1:2575`, positive test ACK. Check state
   and client visibility; stop before changing encoding, port or ACK policy.
5. Open a synthetic ADT fixture, keep Edit Mode off, focus Navigator/Inspector,
   Connect, then review/send. Check destination, one-message count and byte summary.
6. Open the received row. Compare the exact message/delimiters/terminators. For a
   multi-message document, enter one position and confirm only that message arrives.
7. Confirm sender ApplicationAccepted/AA and exact MSA-2 correlation. Optionally use
   synthetic MSH-15=AL/MSH-16=AL and verify CA then AA, two inspectable ACK frames.
8. Open ACK 1/2 in the ordinary viewer. Confirm new unsaved tabs, source independence,
   normal editing/history/save prompts, and no MLLP envelope added to document text.
9. Stop listener, choose AE/AR and repeat; then enhanced CE/CR. Confirm negative
   outcomes, no application success and no retransmission. None/conditional-silence
   modes must show timeout/unobserved rather than application acceptance.
10. Test an unused port and a permitted unreachable test destination/deadline.
    Cancel during response wait; inspect the uncertain-delivery wording. Never use
    a production/unknown interface for acceptance testing.
11. Stop while a client waits; verify clients drain. Start again, reconnect and
    repeat successfully. Close/reopen/reset the tools and check no stale sockets.
12. Wait after any result; verify no background reconnection or automatic resend.
    Reconnect and explicitly review before every additional interactive exchange.
13. Use an authorized non-loopback test host/bind (or cancel the dialogs). Confirm
    separate connection/send and listener exposure warnings. A private IP or Test
    endpoint designation must not suppress the warning or claim nonproduction.
14. Inspect Activity Log and profile/layout JSON: fixed metadata only; no payload,
    control/patient identifiers, raw errors or persisted transport history.
15. Dock/float/resize both tools, replace views, switch light/dark themes, close and
    restore layout. Check editor focus, Ctrl+Z/Y, scripts, document tabs and shutdown.
16. Generate a Stage 11 synthetic patient history. Choose one generated message,
    send it, inspect its ACK and verify Generation Report, document identity and
    unsaved working state remain intact.

## Required native retest for this correction

1. Start UTF-8/Positive Test ACK listener on `127.0.0.1:2575`; use a short configured
   idle limit such as the original 10 seconds. Connect/check the sender endpoint.
2. Choose message 1 of the synthetic 11-message document; verify the 504-byte/one-message
   review. Hold the dialog beyond that idle limit, until the check client times out.
3. Confirm the dialog explicitly says it will connect once and send once. The old
   check timeout may show complete payload 0/socket 0/partial 0; that is separate
   from the fresh transmission. Do not raise timeouts to mask the original case.
4. Confirm the fresh connection receives the exact 504-byte HL7 payload and 507
   socket bytes, prepares AA, and closes cleanly after the sender receives it.
5. Verify Outbound row/status: MLLP write completed (local TCP), one received ACK
   frame, correlated AA/application accepted. Open the ACK and verify MSA-2 matches
   the selected source MSH-10. Compare the received payload without normalization.
6. Repeat with prompt confirmation, Edit Mode off and Navigator focus; then enhanced
   AL/AL (CA then AA), negative ACK mode, no ACK timeout, and explicit cancellation.
7. Cancel the review or close the tool while reviewing: no message is sent. Stop
   the listener after checking/while review is open, then confirm: one bounded
   connection failure with write not attempted, zero ACKs, no acceptance or retry.
8. Stop listener/cancel while waiting for an ACK after an observed complete message:
   write completion stays distinct from missing acceptance. Confirm no retransmit,
   clean Stop/Start, safe Activity Log, docking/themes/editor behavior and unchanged
   protected assets. Finish the original checklist above before granting acceptance.

Stop for user review after verification. Do not stage or commit Stage 12.
