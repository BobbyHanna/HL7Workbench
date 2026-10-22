# ADR 0018: Bounded, explicitly authorized MLLP load testing

Status: Stage 13B implemented; complete automated gate and all 16 user-reported
native Windows acceptance checks passed. One local Stage 13B checkpoint authorized.

Stage 13A passed all eight native acceptance checks and was checkpointed once as
`45e7cdbad9a32cdaebb7834ae0026b10914622aa`, with the authorized message
`Stage 13A: Add controlled batch MLLP transmission`. Its parent is the Stage 12
checkpoint e575ebe47921f33b3431d32bf26a5fc87aee4a0a. The exact 37 accepted paths and
27 implementation/test hashes matched the completed gate before acceptance-only
documentation updates and that checkpoint. No amendment or publication occurred.

Workspace reuses Stage 13A preparation and immutable Stage 12 captures, with the
same 1,000-message/32 MiB preparation ceilings and original byte/encoding/identity
rules. A distinct load session owns bounded orchestration, not another protocol
stack. Every admitted attempt gets one production MllpConnection, including its
existing original/enhanced ACK rules, strict encoding, pinned endpoint and deadlines.
No retry, fallback, pooling, generated identity rewrite or background resume exists.

One dispatcher admits attempts through one monotonic rate gate and a semaphore of
1–8 slots. Admission is at most 20/s (default 1); the next permit is measured from
the actual prior admission, never a historic deadline. A stalled dispatcher cannot
catch up in bursts. Only active tasks are retained; no queue of 5,000 payload copies
or one limiter per worker exists. Source messages are consumed once unless explicit
replay cycles the captured set unchanged. Replay is separate from retry.

Hard bounds are five minutes and 5,000 attempts, default 60 seconds/60 attempts.
Duration includes drain; at its deadline admission stops and active operations are
canceled/drained. Source exhaustion/attempt limit stop admission and drain normally
within that same deadline. Stop also cancels on user request, three consecutive
transport failures, or at least 20% rejection or timeouts after five completions.
Only stricter thresholds can be configured. First stop reason wins; already admitted
operations can complete or become uncertain. Limits are not receiver-safety guarantees.

Preparation resolves exactly one address without opening a socket. Each run requires
synthetic-data attestation, exact configuration/source review and explicit authority
to load-test that target, followed by extra non-loopback consent. Profile names,
private addresses and Test flags confer no authority. Changed source/configuration
invalidates consent; no DNS re-resolution happens at Start. A running snapshot is
independent of later source edits/closure. TLS remains unavailable and fails closed.

Transport results add optional monotonic completed-write and last-complete-ACK timing
metadata without changing framing, ACK parsing, send confirmation or shutdown. Load
orchestration measures connection and end-to-end phases with a monotonic clock.
Attempt, write, ACK-frame, commit and application evidence remain separate. An AA
counts only through existing validated correlated application acceptance. Invalid
complete ACK frames can contribute latency samples without becoming acceptance.

All aggregate counts and at most 5,000 samples per phase are retained; individual
metadata retains only the first 1,000 completions with explicit omitted counts.
Source position/MessageId, attempt order and completion order stay distinct. Raw
payloads and ACK bytes do not enter results. Exact nearest-rank p50/p95/p99 require
2/20/100 samples; insufficient samples are shown explicitly. Rates divide totals
by elapsed run time through final drain, not configured target or summed latencies.

Desktop owns only themed controls, synthetic confirmation, scrollable Cancel-default
dialogs, pickers, four-Hz aggregate refresh and release-on-close ownership. Stop is
first in a fixed toolbar; individual virtualized rows display after completion/Stop.
Closing/reset/shutdown cancels/drains owned work, including review/picker operations.
A canceled unsaved-close prompt can leave stopped results inspectable. Layout DTOs
remember placement only, never source/configuration/results/authorization; restore
cannot send or automatically initialize a load run.

CSV and text summary exports are explicit, whitelist-only and atomic through existing
FileSaveDestination version consent/source protection. Hosts/IPs, source paths/titles,
patient/control IDs, clinical values, credentials and exception text are excluded.
CSV uses Core's shared formula-safe quoting. Summary/omission/uncertainty limitations
remain explicit. Core, ScriptHost isolation, persistence schemas and dependencies
do not change. TimeProvider is framework infrastructure; deterministic test clocks
exercise real scheduler/deadline paths without exposing a production bypass.

See [guide](../load-testing.md) and [acceptance](../stage13b-load-testing-acceptance.md).
