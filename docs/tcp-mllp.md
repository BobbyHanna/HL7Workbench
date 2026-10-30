# Interactive TCP/MLLP testing

Interactive exchange/listener, [Batch Transmission](batch-transmission.md) and
[Controlled Load Testing](load-testing.md) are implemented separate workflows.
The single-send contract below is unchanged by batch/load support. TLS,
scheduling, automatic reconnect and automatic retries remain unsupported.
For a short synthetic loopback task, start with the [current user guide](user/test-with-mllp.md).
Historical acceptance/checkpoint evidence remains in the Stage 12/13 reports.

Open **View → Connectivity & Testing → Connection Manager** and **View → Connectivity & Testing → MLLP Listener**, or the TCP/MLLP
ribbon tab. Both are optional dockable tools. Opening/restoring the application
does not connect, start listening or send. Closing a tool stops its network work
and releases its ephemeral payload history; replacing a Dock view retains the
live tool session. Explicitly opened message/ACK documents remain independent.

## Connect and send one message

1. Configure a friendly name, hostname/IP, port, plain TCP security, connection
   timeout, response timeout and UTF-8 or ASCII. **Save profile** persists this
   metadata locally. Select an existing profile, or use **New profile**. Disconnect
   before changing the configured destination. Notes must contain no patient data
   or secrets. The optional **Test endpoint** designation is descriptive only.
2. Choose **Connect** to check the endpoint. A hostname resolves once to one address,
   which is displayed and pinned for the reviewed send. The checked socket may idle
   out while you read; it is never used to transmit HL7. There is no address fallback. Non-loopback
   connections require confirmation, including private/link-local networks. Private
   addressing does not prove nonproduction. Plain TCP is unencrypted.
3. Activate an HL7 document. Leave message position blank for its active message,
   or enter one position from its complete message sequence. This supports ordinary,
   edited, inserted, script-produced and Stage 11 generated messages. Edit Mode can
   be off; the editor need not have focus. No whole-document batch is transmitted.
4. Choose **Review and send one message…**. Review host, resolved address, port,
   security, encoding, document/message reference, byte count and **message count: 1**.
   The review reports explicit encoding conversion. Confirm explicitly authorizes
   closing the checked socket, opening one fresh connection to the reviewed IP/port,
   and sending exactly one message. A separate
   non-loopback send confirmation is required each time. A closed, edited or
   structurally changed source, or a switch to another document during review,
   prevents that send. There is no scheduled/background sending.
5. Inspect the session result. Each exchange disconnects, including success, so a
   late/unsolicited ACK cannot become another request's response. Reconnect explicitly
   for the next interactive exchange. Only a new explicit reviewed send can open
   its next transmission connection. There is no reconnect/retry following failure.

The capture reads complete authoritative working text or original indexed payload
bytes. It never sends an abbreviated editor preview or a whole batch container.
Original delimiters and segment terminators are retained. Outer file MLLP framing
is excluded; one `0B payload 1C 0D` envelope is added only at the network boundary.
UTF-8/ASCII are strict: unrepresentable characters fail instead of becoming `?`.
Changing encoding explicitly transcodes characters; it does not rewrite MSH-18 or
claim that a receiver honors that encoding. UTF-16 source documents require an
explicit supported wire encoding. A BOM inside the selected original payload is
retained when encoding matches; a file-level BOM outside that range is not sent.
Header and structural eligibility are checked without clinical schema validation.
Unsupported/malformed headers, reserved framing bytes and oversized messages fail.

## Read ACK results

The request's MSH-10 is correlated with the reply's MSA-2. The reply needs a readable
MSH, ACK message type, its own nonempty distinct MSH-10, matching MSH-12, exactly one
MSA and a recognized code. Supplied application/facility routing identifiers must
mirror the request. Correlation uses Core escape decoding when delimiter alphabets
differ; unknown/local escapes are never guessed. An `MSA|AA` substring is insufficient.

Both empty/null MSH-15 and MSH-16 select original mode: wait for AA/AE/AR. Enhanced
mode requires AL, NE, SU or ER in **both** fields. Accept and application phases
remain separate: CA/CE/CR describe commit, AA/AE/AR describe application acknowledgment.
AL requests every result, NE suppresses that phase, SU requests success only and ER
requests error/reject only. A successful commit can be suppressed before an application
ACK. A negative commit terminates the exchange. CA alone does not establish application
acceptance. NE/NE reports sent without ACK evidence; conditional silence reports
**ConditionalAckUnobserved**, never application accepted. One deadline covers the
write and all requested phases. See [HL7 control](https://hl7.eu/HL7v2x/v251/std251/ch02.html)
and [acknowledgment choreography](https://hl7.eu/HL7v2x/v29/std29/ch02.html).

History distinguishes connection failure, failed/incomplete write, application accepted/error/rejected, commit accepted/error/
rejected, missing ACK, missing fields, invalid/mismatched/unexpected ACK, framing
error, timeout, cancellation and connection loss, plus the source document/message
reference when available. It retains exact complete ACK
payloads even when correlation fails. Select a row and use **Open ACK 1/2** or
**Open message** to create independent unsaved local HL7 documents in the existing
viewer. Opening does not transmit or save externally.

Outbound rows are attempts, not an assertion that data was sent. They separately
show **MLLP write not attempted**, **MLLP write incomplete; delivery uncertain**, or
**MLLP write completed (local TCP)**, response/ACK frame count and application
acceptance. Only a complete write followed by a validated correlated AA establishes
application acknowledgment. A phase and safe socket error code help distinguish
connection/write/read failures. No raw exception message/stack is retained.
The entire frame's `WriteAsync` is awaited before normal shutdown; NetworkStream is
unbuffered, so adding Flush would provide no additional delivery evidence. See
[Microsoft's NetworkStream documentation](https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.networkstream.flush?view=net-10.0).

**Disconnect / Cancel** cancels preparation/connect/exchange work. Once a write
starts, delivery or processing can be uncertain even if cancellation, timeout or
connection failure follows. Inspect local results and the test system before deciding
whether another explicitly reviewed send is appropriate. No outcome triggers retry.

## Receive and generate test ACKs

The default bind is `127.0.0.1:2575`. Choose a literal bind IP, port, strict wire
encoding, client idle/frame deadline and test ACK policy, then **Start listener**.
No firewall rule is created. Non-loopback/wildcard binds require explicit exposure
confirmation. Connected remote endpoints, receipt time, framing/eligibility status,
prepared test ACK codes and frame duration appear locally. Select a receipt and
**Open message** to inspect its exact bytes. No tabs open automatically on arrival.

Receipt sizes distinguish complete HL7 payload bytes, socket bytes read across that
client connection (including framing/prefetched frames), and buffered partial payload
bytes. A timeout with zero complete payload is not proof of zero socket input. An
idle checked connection may leave a zero/zero timeout row while a person reviews;
the separately confirmed fresh connection must produce the actual received message
and ACK. No ACK is generated for an incomplete frame.

Choose **Stop listener** before changing options, then Start again. Stop closes the
accept socket, cancels and drains clients; restart is supported. Policies are none,
positive, application error/reject (AE/AR), or commit error/reject (CE/CR). Original
mode maps commit negatives to the corresponding application negative. Enhanced mode
honors the requested phase and AL/NE/SU/ER suppression. Envelopes preserve delimiter
rules, mirror routing, use a new timestamp/control ID, echo MSH-10 in MSA-2 and contain
no clinical facts. ACK messages are never ACKed again. Ineligible or undecodable
messages remain inspectable but receive no positive ACK. Rows describe prepared test
ACKs; they do not assert successful delivery or external clinical processing.

Generated ACK control IDs fit the legacy 20-character bound. Their MSH-18 declares
the selected UTF-8/ASCII encoding, generated metadata uses Core escaping, and reply
validation requires timestamp, processing ID and version as well as correlation.

## Limits, storage and security

| Resource | Bound/default |
| --- | --- |
| One payload | 1 MiB maximum, also subject to existing Core structural limits |
| Socket read / prefetched frames | 8 KiB / 16 complete frames |
| Listener clients | 4 default, service ceiling 16 |
| Listener receipts / retained payloads | 64 / 16 MiB defaults; service ceilings 200 / 32 MiB |
| Sender history | 200 exchanges and 16 MiB, evicting oldest rows |
| Connection / ACK deadline | 5 / 10 seconds; ranges 1–60 / 1–120 |
| Listener idle/partial frame deadline | UI defaults to 10 seconds; service default 30; range 1–120 |
| Endpoint profiles | 32, in one 64 KiB version-1 JSON file |

Malformed framing closes its client; there is no silent resynchronization. Receipt
or byte-budget overflow closes the client **without ACKing the overflow frame**;
the refusal counter is visible. Clear history explicitly to release retained bytes.
Listener deadlines bound partial frames as well as idle connections.

Profiles reside beside editor preferences in `transport-endpoints.json`. Only
version, endpoint ID, friendly name, host, port, security/encoding enums, timeouts,
Test endpoint designation and notes are serialized. Unknown properties, invalid
values, oversized files and duplicate IDs are rejected. Writes use a flushed,
same-directory temporary file and atomic replacement. Invalid files are retained;
the UI disables overwriting them until corrected. No socket state, raw messages,
password/private-key property or session history is persisted there. Do not put secrets
in names/notes. Local configuration inherits the user's existing filesystem protections.

Activity Log records fixed operation kinds/counts only, with no endpoint, filename,
payload, patient/control ID, raw exception message or stack. Endpoint and payload
inspection stay within the local transport/document session. There is no telemetry,
cloud dependency or new ScriptHost network API. Existing trusted-script isolation and
warning boundaries are unchanged.

**TLS is deferred.** The TLS profile option cannot connect and cannot fall back to
plaintext. Certificate trust, client certificates/private keys and secure credential
storage need a future complete design. Only general same-socket ACKs are supported;
asynchronous application ACKs on another connection, specialized query responses,
sequence/continuation protocols, arbitrary wire encodings and durable delivery queues
are outside Stage 12. Batch transmission, retries, scheduling, benchmarking and load
testing remain deferred to Stage 13. Native socket/firewall/DNS/docking behavior must
still pass the [Windows acceptance checklist](stage12-tcp-mllp-acceptance.md).
