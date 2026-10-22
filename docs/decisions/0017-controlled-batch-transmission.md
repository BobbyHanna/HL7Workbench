# ADR 0017: Controlled sequential batch transmission

Status: Stage 13A batch and final Generation Report UI polish passed native Windows
acceptance; the user confirmed all eight checks and authorized one local checkpoint.
See the acceptance record for exact results and retained evidence.

Stage 12 was accepted natively and committed by the user as
`e575ebe47921f33b3431d32bf26a5fc87aee4a0a` (`MLLP functionality`). Stage 13A
is ready for its authorized single local checkpoint. Stage 13B may begin only after
that checkpoint, and must remain unstaged/uncommitted. Publication is not authorized.

Workspace owns a bounded immutable prepared selection, source revision, pinned
resolved endpoint/options and an explicitly authorized run. It reuses the Stage 12
capture API, strict encoding, production MllpConnection, framing and Core ACK policy.
No second protocol stack, new package, save/message identity change or ScriptHost
privilege is introduced. Default stop requires application acceptance, not merely
write completion or CA. Continue is an explicit reviewed policy and never a retry.

Each message owns one connection and must complete its ACK phase(s) before the next
message can start. A monotonic cancellation-aware scheduler enforces a 250–60,000 ms
post-exchange interval (default 1,000). Pause gates the next attempt, allowing the
active exchange to finish; cancel propagates to active transport and drains it.
An immutable run cannot change destination while paused. Reconfiguration requires
cancel/drain and fresh preparation/review/confirmation. No restart resume exists.

Before Start, a stale/closed/inactive source or changed draft requires preparation and
review again. After Start, edits/closure/tab switches cannot retarget the authorized
capture. Dock view lifetime does not own the run; the release-on-close model owns
it, is retained until drain completes, and application shutdown awaits those drains.
Layouts contain placement only, never payloads, selections, results or authorization.

Bounded result rows retain exact phases, actual write evidence and uncertainty;
16 MiB ACK-byte retention is separate from outcome/frame metadata. Reports use the
shared Core CSV encoder and existing version-consented atomic publication. Only
whitelisted typed metadata is exported; free-text source/profile/HL7/exception fields
are excluded. Protected source destinations remain guarded after source closure.
No durable payload journal, telemetry, background/unattended or load mode is added.

The batch integration tests exposed a Stage 12 listener inspection race: TcpClient
can clear its Client property during disposal before dictionary removal. A null-safe
endpoint snapshot fixes that demonstrated defect without changing single-send wire,
ACK or confirmation behavior. Initial failing logs remain under artifacts/stage13a.
Windows can buffer a whole 1 MiB write before a peer event; a small internal exchange
test seam exercises an explicitly incomplete-write cancellation boundary. Production
and real loopback tests always use MllpConnection; no Desktop substitution path exists.

See the [batch guide](../batch-transmission.md) and
[acceptance record](../stage13a-batch-transmission-acceptance.md) for exact bounds,
report columns, verification evidence and remaining native checks.
