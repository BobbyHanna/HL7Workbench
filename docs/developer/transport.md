# TCP/MLLP, sequential batches and controlled load

Core interprets header/ACK semantics; Transport frames bytes and owns sockets;
Workspace captures sources, reviews immutable targets and tracks outcomes; Desktop
supplies explicit controls/confirmation and lifecycle ownership. Neither ScriptHost
nor QueryHost is a network broker. See ADRs
[0016](../decisions/0016-interactive-mllp.md),
[0017](../decisions/0017-controlled-batch-transmission.md) and
[0018](../decisions/0018-controlled-load-testing.md).

## Framing and interactive exchange

[MllpFraming](../../src/HL7Workbench.Transport/MllpFraming.cs) is incremental over
arbitrary socket chunks. Reads may split or coalesce frames; MLLP envelope bytes
are not HL7 delimiters or file index boundaries. Capture uses authoritative working
text/original payload, never a preview. It excludes outer file framing and adds one
wire envelope. Strict UTF-8/ASCII conversion is explicit and disclosed; no replacement
characters, silent MSH-18 rewrite or framing repair is allowed.

[TransportEndpoint](../../src/HL7Workbench.Transport/TransportEndpoint.cs) validates
metadata and resolves one reviewed address. TLS is unavailable and fails without
plaintext fallback; private IP/Test flags are not authorization. Interactive Connect
checks a socket, while a separately reviewed one-message send uses a fresh connection
to the pinned destination. Each exchange disconnects; there is no automatic reconnect,
address fallback or retry. Non-loopback sends/listener exposure have explicit review.

[MllpConnection](../../src/HL7Workbench.Transport/MllpConnection.cs) and
[Hl7Acknowledgment](../../src/HL7Workbench.Core/Hl7Acknowledgment.cs) distinguish
write/commit/application phases. A complete local write does not prove delivery or
application acceptance. Correlation checks MSA-2/control ID plus required ACK/header/
routing semantics, not an `AA` substring. Enhanced CA/CE/CR and application AA/AE/AR
remain separate; suppressed/conditional silence is not positive evidence. A complete
write plus validated correlated AA establishes the implemented application-ACK
outcome, not clinical correctness. Write-started failure/cancel/timeout can leave
processing uncertain and never triggers an automatic resend.

[MllpListener](../../src/HL7Workbench.Transport/MllpListener.cs) bounds clients,
deadlines/frame bytes and retained receipts/payloads. Stop closes acceptance and
cancels/drains clients; simultaneous Stop/Dispose must share completion rather
than revive a disposed listener. Test ACK policy is synthetic receiver behavior,
not a production engine or proof of clinical processing. Opening history payloads/
ACKs explicitly creates independent documents; arrival does not open/save tabs.

## Immutable batch and load ownership

[BatchTransmission](../../src/HL7Workbench.Workspace/BatchTransmission.cs) prepares
a bounded source selection/revision and resolved endpoint/options without opening
a socket. Review/Start authorization is bound to that prepared identity. Source/
selection/configuration changes before Start invalidate preparation; after Start,
the run owns immutable captures. Sequential fresh-connection exchanges honor ACK
policy and monotonic post-exchange pacing. Pause gates the next attempt after the
active exchange; Resume cannot resend completed/uncertain positions.

[LoadTesting](../../src/HL7Workbench.Workspace/LoadTesting.cs) reuses capture and
production transport with one global admission-rate gate and bounded slots. Stalls
do not accumulate catch-up permits. Replay explicitly cycles unchanged captured
identities; it is not a failure retry. Concurrency may reorder clinical events and
completion rows. Duration/attempt/source/safety stops end admission and drain/cancel
active work according to the stop reason. A target rate is a ceiling, not promised
throughput or receiver safety.

Detailed completion retention and aggregate/latency sample accounting are separate.
Do not add uncertainty to mutually exclusive classifications or treat first-1,000
rows as the full aggregate population. Reports use approved metadata columns;
payloads/control identifiers are not added to ordinary activity/CSV summaries.
Exact explicitly opened/exported HL7 has a separate sensitive-content contract.

Tool close/reset and app shutdown coordinate owning model/session drains; replacing
a Dock view does not cancel/restart an authorized run. Await final model observation
as well as engine execution before asserting visible results/export state. New runs
always require fresh review, with no scheduling/restart resume, pooling or automatic
rate escalation. See [limits](../reference/limits.md#transport-and-testing).

## Regression and native coverage

Use synthetic loopback endpoints, controlled schedulers/signals and precise write
phase evidence. Cover fragmented/coalesced frames, multiple ACK phases, mismatched
IDs/delimiters, strict encodings, no-ACK/conditional silence, cancellation during
write/read, stop/dispose races, pre-start changes, immutable running captures and
no duplicate admission. Do not replace lifecycle barriers with arbitrary delays.

Evidence: [framing](../../tests/HL7Workbench.Workspace.Tests/TransportFramingTests.cs),
[network](../../tests/HL7Workbench.Workspace.Tests/TransportNetworkTests.cs),
[ACK semantics](../../tests/HL7Workbench.Core.Tests/TransportAcknowledgmentTests.cs),
[batch](../../tests/HL7Workbench.Workspace.Tests/BatchTransmissionTests.cs),
[load](../../tests/HL7Workbench.Workspace.Tests/LoadTestingTests.cs) and
[Desktop lifecycle](../../tests/HL7Workbench.Desktop.Tests/LoadTestingViewTests.cs).
Actual firewall, OS picker, network policy and native-window behavior require
Windows acceptance with authorized synthetic destinations. User guides:
[single/listener](../user/test-with-mllp.md), [batch/load](../user/batch-and-load-tests.md).
