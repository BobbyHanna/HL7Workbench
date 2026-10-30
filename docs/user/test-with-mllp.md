# Send one message or receive with MLLP

Use **View → Connectivity & Testing → Connection Manager / MLLP Listener**, or
the **TCP/MLLP** ribbon. Prerequisites: authorized synthetic test messages and an
authorized test endpoint. Opening/restoring these tools does not connect or listen.
Plain TCP is unencrypted/unauthenticated; TLS is unavailable and fails closed.

## Try a local one-message exchange

1. Open [adt-a01.hl7](../../samples/synthetic-only/adt-a01.hl7).
2. In **MLLP Listener**, configure literal loopback bind `127.0.0.1`, an available
   test port such as `2575`, UTF-8 strict and **Positive test ACK**. Choose
   **Start listener**. If the port is in use, stop/reconfigure to an available one.
3. In **Connection Manager**, create a **New profile** for `127.0.0.1` and that
   port, Plain TCP and UTF-8 strict. **Save profile** retains endpoint metadata only.
4. Choose **Connect** to check/pin the resolved destination. Keep the ADT document
   active; leave message position blank to use its current message.
5. Choose **Review and send one message…**. Check resolved IP/port, encoding,
   source/message reference, payload bytes and message count one. Confirm only the
   intended send. Non-loopback sends need additional explicit confirmation.
6. Inspect sender and listener history. Use **Open message** or **Open ACK 1/2**
   deliberately to inspect retained exact payloads in independent unsaved tabs.

Expected result: one fresh connection sends one MLLP-framed payload. With the
positive loopback test ACK and eligible fixture, the sender can report a correlated
application acknowledgment. A listener-generated test response is not evidence
of clinical processing by a real application.

The checked Connect socket is not reused for transmission. Each reviewed send uses
a fresh connection and disconnects afterward; another interactive exchange needs
explicit Connect/review again. There is no automatic reconnect or retry.

## Interpret outcomes before resending

| Evidence | What it establishes |
| --- | --- |
| Socket connected | A TCP connection was established, not that HL7 was accepted. |
| Complete local MLLP write | The full frame was written locally, not peer processing or application acceptance. |
| ACK received | A frame arrived; it still needs structural/control-ID/routing validation. |
| Correlated CA | Enhanced commit acknowledgment, not application acceptance. |
| Complete write plus validated correlated AA | Application acknowledgment under the implemented exchange rules. |
| Timeout/cancel after write started | Delivery/processing can be uncertain; do not infer nondelivery. |

Original AA/AE/AR and enhanced commit/application phases are separate. Suppressed
or conditional ACK silence is not success evidence. Review exact codes/status and
the receiving test system before deciding on another explicit send.

## Stop and recover

Use **Disconnect / Cancel** to stop preparation/connect/exchange, or **Stop listener**
to close acceptance and drain clients. Stop before changing listener options.
Closing/resetting a tool stops its work and releases its ephemeral history;
explicitly opened payload/ACK documents remain independent. Cancellation cannot
retract data already sent. **Clear history** releases retained receipts when needed;
it does not undo network activity.

Non-loopback/wildcard binds expose a listener and require confirmation; no firewall
rule is created. Strict UTF-8/ASCII capture rejects unrepresentable or malformed/
oversized payloads. Supported explicit encoding conversion is disclosed, not silently
treated as exact source bytes. Listener resources/history are bounded; this is not
a durable message archive or production interface engine.

See [exact transport/ACK/storage limits](../tcp-mllp.md),
[batch/load workflows](batch-and-load-tests.md) and [privacy/storage](privacy-and-storage.md).
