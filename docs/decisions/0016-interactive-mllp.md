# ADR 0016: Interactive TCP/MLLP

Current milestone status: Stage 12 passed native Windows acceptance and was committed
by the user as `e575ebe47921f33b3431d32bf26a5fc87aee4a0a`. Stage 13A now adds controlled
batch orchestration under [ADR 0017](0017-controlled-batch-transmission.md), while the
single-message contract below is retained. Earlier pending/deferral statements are
historical; concurrent loads, scheduling, automatic retry and Stage 13B stay deferred.

Status: Stage 12 native send correction passed the complete automated gate;
native Windows retest pending. See the Stage 12 acceptance record for exact results.

Stage 11 is checkpointed at af717a66608d3277402bd6696cd4f50712c7ecf5.
Stage 12 remains uncommitted. No Stage 13 work is authorized.

Core interprets HL7 headers and acknowledgment policy using its parser. A separate,
dependency-free Transport project owns incremental MLLP framing, TCP streams,
timeouts, correlation and the bounded listener. Workspace owns profile persistence,
one-message document captures and ephemeral history. Desktop owns dialogs and Dock.

Each send reviews exactly one captured message and a resolved, pinned destination.
The explicit Connect action checks that destination. Confirming Send authorizes
retiring that check socket and opening exactly one fresh connection to the same
pinned address, then writing exactly one message. Human review never consumes the
fresh socket's idle lifetime. This is a user-approved initial transmission, not a
retry after a failed write. A failed connect/write/ACK never causes another attempt.
Non-loopback connection/listener operations require additional explicit consent.
Private addressing and the optional Test endpoint label do not prove nonproduction.
Connections never retry or change destinations. A failed/canceled exchange closes
its socket; delivery after a write starts may be uncertain. Conditional ACK silence
does not establish application acceptance. Original and enhanced ACK phases remain
distinct. Listener ACKs describe test behavior, never clinical processing.

The native failure reproduced when human confirmation outlasted the checked
connection's listener idle deadline. Reusing that closed socket gave immediate
Disconnected; WriteStarted had been mistaken for completion and all timeout rows
showed zero completed payload bytes regardless of actual socket input. Results now
separate write start/completion, response-frame reception, correlated acceptance,
failure phase and safe SocketError enum. Listener rows separate complete payload,
connection-total socket bytes and partial payload. No payload/exception text is logged.

Limits are enforced before payload growth, client admission and receipt admission.
There is no persistent payload journal: retained bytes are bounded session state,
or explicitly opened through the existing document model. Activity Log uses fixed
metadata-only events. Closing a transport tool cancels network work and drains it.

TLS is deferred until certificate trust, identity and private-key management can be
implemented together. TLS profiles cannot connect and never fall back to plain TCP.
No new NuGet dependency, telemetry, worker privilege or ScriptHost API is introduced.
Batch sends, retries, scheduled sends and load testing are deferred to Stage 13.

Protocol references: [HL7 v2 control](https://hl7.eu/HL7v2x/v251/std251/ch02.html)
and [HL7 acknowledgment choreography](https://hl7.eu/HL7v2x/v29/std29/ch02.html).
Both empty MSH-15/16 select original mode; enhanced mode uses AL/NE/SU/ER in
both fields. MSA-2 correlates to the request MSH-10, not the reply's new MSH-10.
