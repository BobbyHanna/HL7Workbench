# Controlled MLLP Load Testing

Open **View → Controlled Load Testing** or **TCP/MLLP → Load Testing**. Use synthetic
messages and an explicitly authorized test destination. The tool starts idle; opening,
reconstructing or restoring it never connects or sends. Single-send and sequential
Batch Transmission retain their separate existing workflows.

## Prepare and authorize

1. Open synthetic HL7 or generate a Stage 11 synthetic patient history. Confirm that
   this source contains synthetic test data. This attestation is not proof of PHI-free
   input. No clinical values are changed to manufacture unique load messages.
2. Save the destination in Connection Manager, then reload/select it. A new tool
   defaults to loopback even when only non-loopback profiles exist. Profile labels
   and private IP addresses do not prove authorization or a nonproduction system.
3. Choose all messages, an inclusive range, or unique explicit positions (sorted into
   source order). Prepare retains at most 1,000 messages/32 MiB, with the existing
   strict 1 MiB/message capture eligibility. Oversized/malformed input rejects rather
   than truncates. Source bytes, delimiters and terminators follow Stage 12 capture;
   any explicit strict encoding conversion is disclosed.
4. Configure limits and stricter stop thresholds. **Replay** is off by default: the
   captured source is consumed once, then source exhaustion ends admission. Replay
   deliberately cycles those same messages until a limit or stop condition. MSH-10,
   patient/visit identities and clinical content remain identical. Duplicate-control-ID
   rejection by a receiver is reported, never silently repaired or retried.
  An explicitly reviewed replay schedule admits new attempts independently of prior
  outcomes until a stop condition; no failed attempt is inserted as a recovery retry.
5. **Prepare** snapshots source identity/revision/positions, options, endpoint profile
   and exactly one resolved address. It opens no socket. **Review** shows hostname,
   pinned IP/port, security/encoding, all source positions, concurrency, target, duration,
   attempt cap, replay and thresholds. The scrollable dialog defaults to Cancel.
6. **Start** requires explicit confirmation that you are authorized to load-test this
   exact destination using the reviewed synthetic source. Non-loopback adds another
   exact-target confirmation; a saved Test flag/private address never suppresses it.
   Review is specific to one prepared identity. Source edits/closure/tab switching,
   selection or configuration changes before Start require preparation/review again.
   Start never follows changed DNS or a different address.

Once started, the run owns its immutable snapshot even if its source is edited,
switched or closed. Changing configuration requires Stop/drain and a newly prepared,
reviewed run. There is no automatic retry, fallback, startup send, scheduling,
restart resume, connection reuse/pooling, external discovery or scanning. TLS remains
unavailable and never falls back to plaintext. Increasing concurrency can reorder
registration/results and clinical events. Limits do not guarantee receiver safety.

## Bounds and stopping

| Setting/resource | Default | Hard bound/meaning |
| --- | --- | --- |
| Concurrency | 1 | 1–8 active exchanges, one fresh connection per attempt |
| Global target | 1 attempt/s | 0.1–20 attempts/s shared by all slots |
| Duration | 60 seconds | 1–300 seconds including final drain |
| Attempt limit | 60 | 1–5,000 admitted attempts |
| Replay | Off | Explicit cycling of the captured set, never rewriting IDs |
| Consecutive transport failures | 3 | May tighten to 1–2; evaluated in completion order |
| Rejection threshold | 20% | May tighten to 1–19%; application and commit negatives |
| Timeout threshold | 20% | May tighten to 1–19%; connection/response timeout outcomes |
| Minimum completed samples for rate stops | 5 | May tighten to 1–4 |
| Captured set | Selected messages | Maximum 1,000 positions / 32 MiB; no truncation |
| Retained individual results | First 1,000 completions | Later rows omitted explicitly; every aggregate remains exact |
| Latency samples | Every applicable phase | At most 5,000 per phase, no approximate histogram |
| Exports | Explicit local action | CSV capped at 4 MiB; text summary bounded by typed metadata/1,000 positions |
| UI updates | Four Hz plus actions/completion | Aggregate refresh; virtualized result rows shown after drain |

One dispatcher waits for a slot and the next global admission permit. The next
deadline starts from actual prior admission, so stalls cannot accumulate catch-up
bursts. A target is a ceiling on admission, not a promised achieved rate. Slow
connects/ACKs, low concurrency and scheduling overhead can reduce throughput. No
automatic adjustment increases rate or concurrency to compensate.

The built-in Stage 12 listener has separate protective defaults: four concurrent
clients, 64 retained receipts and 16 MiB retained payloads. It rejects admission
without a positive test ACK when full; this is not evidence of a sender retry or
successful application processing. For a longer small-message loopback run,
explicitly clear listener history before its receipt/byte budget fills (for example
at 40 and 80 receipts in a 120-attempt run). Clearing listener history does not reset
the load tool's aggregates. Do not silently raise/disable listener bounds. Observing
eight simultaneous waits requires an authorized receiver configured to admit eight;
the built-in default can reject clients above four.

The first duration/attempt/source limit or threshold ends admission. Attempt/source
exhaustion drains active exchanges normally within the duration limit. Duration,
user Stop and safety thresholds cancel/drain active work. **Stop / Cancel** remains
first in the fixed top toolbar, including in short docks. Cancellation cannot prove
that an in-flight message was not received/processed. Previously admitted exchanges
can complete after a stop request and appear as accepted, rejected or uncertain.
Starting again always requires a new preparation/review/authorization.

## Measurement definitions

- **Attempts** count admitted operations, including failures/cancellation before a
  write. **Active** counts admitted operations not yet completed; **Completed** is
  every finished attempt. Original source position/opaque MessageId, attempt ordinal
  and actual completion ordinal are distinct. Concurrent completion may reorder rows.
- **Connections** count successful TCP establishment, including cancellation just
  afterward. **Complete writes** mean local completion of the full MLLP frame, not
  peer processing. Incomplete writes never contribute a completed-write timing sample.
- **ACK frames** count received complete frames, including invalid or mismatched
  frames. Commit CA and CE/CR remain separate from application AA and AE/AR. Only
  validated correlated AA with a complete write counts as application accepted.
  CA-only, NE/NE silence, timeout or a finished run does not establish acceptance.
- **Missing/mismatched** includes missing/invalid/unexpected/mismatched response frames
  and response silence/timeouts after a completed write. A connection timeout is a
  timeout, not a missing ACK. **Transport failures** are connection/write/disconnect/
  framing failures; **Timeouts** include connect/response timeout and conditional
  silence. **Canceled** is distinct. **Uncertain** overlaps interrupted/unconfirmed
  write-started outcomes; do not add it to other totals as an exclusive category.
- **Connection latency** measures attempt connect start to TCP establishment.
  **Write duration** measures the completed frame write. **ACK round-trip** measures
  completed write to the last complete received ACK frame, including invalid frames;
  it excludes no-frame timeouts and is not proof of acceptance. **End-to-end** measures
  admitted exchange work, including capture copy, connect, write, ACK and closure;
  global scheduling waits are excluded from that phase.
- All durations use monotonic clocks. UTC start/end timestamps are attribution only.
  Attempts/s, completed exchanges/s and application-accepted/s each divide their
  total by elapsed run time through final drain. Target and achieved rates are shown
  separately. Remaining duration is shown during a run and zero after it ends.
- Each phase reports sample count, min/max/average and exact nearest-rank percentiles.
  p50 requires 2 samples, p95 requires 20 and p99 requires 100; smaller samples say
  insufficient rather than implying reliable tail estimates. Omitted result rows
  do not remove aggregate or latency samples. Small samples still have min/max/mean.

## Reports, privacy and lifecycle

After Stop/completion, **Export CSV…** writes per-exchange metadata in completion
order plus an exact Summary row. **Export summary…** writes concise configuration,
source counts/positions, UTC start/end, outcomes, stop reason, achieved rates, latency
statistics, omission and uncertainty limitations. Neither export contains raw HL7,
ACK bytes, patient/control identifiers, clinical values, credentials, source titles/
paths, hostnames/IPs or arbitrary exception text. Destination classification,
security/encoding, opaque run ID, document number/revision and numeric positions are
allowed metadata. CSV is UTF-8 without BOM, always quoted, CRLF and protected by
Core's shared formula-injection rules. File publication is atomic, with existing
overwrite-version consent and protection against all open/original source paths.
Canceled/failed exports publish no partial file. Reports are not a delivery journal.

CSV v1 common columns:
`RecordType, RunId, Scope, Security, Encoding, Concurrency, TargetPerSecond,
DurationLimitSeconds, AttemptLimit, Replay, ConsecutiveFailureLimit, RejectionPercent,
TimeoutPercent, MinimumOutcomeSamples, DocumentNumber, SourceRevision, SourceMessages,
CapturedPayloadBytes`.

Exchange columns:
`SourcePosition, Attempt, CompletionOrder, Replayed, StartedUtc, EndedUtc, Connected,
CompleteWrite, AckFrames, Commit, Application, Correlation, Outcome, Uncertain,
ConnectMs, WriteMs, AckMs, ExchangeMs`.

Summary columns:
`StopReason, Attempts, Completed, Connections, CompleteWrites, TotalAckFrames,
CommitAccepted, CommitRejectedError, ApplicationAccepted, ApplicationRejectedError,
MissingMismatched, Timeouts, TransportFailures, Canceled, UncertainCount, OmittedResults,
ElapsedSeconds, AttemptRate, CompletionRate, AcceptedRate, Limitations`, followed by
`Connect`, `Write`, `Ack`, `Exchange` each combined with
`Samples, MinMs, MaxMs, AverageMs, P50Ms, P95Ms, P99Ms` in that order. Inapplicable
cells are empty; insufficient percentile cells are empty with sample counts/rules
disclosed in Limitations. Summary counts include all attempts, even omitted rows.

Detach/reconstruction retains the same live model/run. Close/reset releases it only
after cancellation/drain; application shutdown drains load and batch tools before
normal unsaved-document prompts. If a later prompt cancels shutdown, stopped results
remain inspectable. Closing a tool clears its session. Layouts retain placement only;
source/options/results/attestation/authorization are never serialized and no restart
resume exists. No new dependency, ScriptHost permission, cloud service or telemetry.

Stage 13B is uncommitted pending [native Windows acceptance](stage13b-load-testing-acceptance.md).
