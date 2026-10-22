# Controlled Batch Transmission

Stage 13A is checkpointed at 45e7cdbad9a32cdaebb7834ae0026b10914622aa after all eight
native acceptance checks. Stage 13B adds a separate [Controlled Load Testing tool](load-testing.md),
with its own authorization and hard bounds. Sequential batch behavior below remains
unchanged; its no-concurrency statement applies to this tool.

Stage 13A adds **View → Batch Transmission**, also available on the TCP/MLLP ribbon.
Use only synthetic messages and explicitly authorized test destinations. This is
an interactive sequential tool. It has no scheduled/startup sending, automatic
retries, persistent resume or concurrent load mode. TLS remains unavailable and
fails closed. Stage 13B load/scheduling functionality is deferred.

## Prepare, review and authorize

1. Generate a Stage 11 synthetic patient history or open a synthetic HL7 document.
2. Save the destination in Connection Manager. In Batch Transmission, reload saved
   endpoints and select one. The fallback endpoint is loopback; an available saved
   loopback profile is preferred over other saved profiles. Nothing connects yet.
3. Choose all messages, an inclusive contiguous range, or comma-separated unique
   positions. Explicit positions are sorted into source order. Positions address the
   complete logical sequence, independently of navigator filters or editor focus.
4. Configure pacing (default 1,000 ms) and ACK policy. **Stop on unsuccessful outcome**
   is the default. Continuing after unsuccessful outcomes requires explicit selection
   and review. Neither policy retries a message.
5. **Prepare** captures selected complete authoritative payloads and their order,
   document identity/revision, endpoint profile, resolved pinned address and options.
   Original bytes/delimiters/terminators follow the Stage 12 contract; explicit strict
   UTF-8/ASCII transcoding is disclosed. No payload preview is treated as source. No
   socket opens. Oversized/ineligible input rejects preparation without truncation.
6. **Review batch** shows the source identity/revision, batch ID, destination hostname,
   pinned IP/port, scope/security/encoding, count, positions, byte count, mode, interval,
   ACK policy and estimated minimum interval time. Larger position lists remain fully
   available in the prepared table. Review is not authorization to start later without
   another confirmation.
7. **Start** explicitly confirms that exact run again. Non-loopback endpoints require
   additional confirmation identifying the exact destination. Private addresses,
   profile names and Test endpoint labels do not prove nonproduction. Pacing does not
   guarantee a destination is protected from overload. Messages may affect receivers.

Edits, closure or another active document before Start make preparation stale/inactive;
activate and prepare the intended source again and review it. Changing the endpoint,
options, selection or batch identity invalidates review. Start never silently recaptures
messages or follows changed DNS. An already running batch continues its authorized
immutable payloads if the source is edited, switched or closed; the source status makes
that distinction explicit. ACK documents opened afterward are independent unsaved tabs.

## Lifecycle, pacing and ACKs

One fresh production `MllpConnection` is used for each message. The full frame write
and the required original/enhanced ACK phases finish before that connection is disposed.
There is no reuse of a pre-review idle socket, destination fallback, or second attempt
for a failed message. Even an explicitly selected Continue policy advances only to the
next selected position, never to the failed one again.

The interval is an extra wait **after each exchange ends** before the next attempt
starts. It applies after accepted, rejected, connection-failed and timed-out attempts.
Allowed range: **250–60,000 ms**. The minimum therefore also bounds transmission starts.
ACK waits/connection time add to the interval, never subtract from it. Review estimates
`(count - 1) × interval`, plus unpredicted connection/ACK time; no delay follows the final
message. Monotonic timing, cancellation and a pause gate enforce scheduling; no guessed
sleep is used to conceal transport races.

Original AA/AE/AR and enhanced CA/CE/CR then AA/AE/AR preserve Stage 12 meaning. Only
a complete local write followed by a validated correlated AA counts as **application
accepted**. CA alone, suppressed ACKs, conditional silence, missing or invalid ACKs and
TCP write completion do not. The default Stop policy stops on every outcome lacking
application acceptance, including CA-only and NE/NE. Continue records that distinct
outcome and advances after pacing. A finished run is not a claim that every receiver
accepted its messages.

**Pause** prevents another attempt starting. An active exchange finishes; state is
PausePending until it reaches the gate, then Paused. Queue and results remain intact.
**Resume** only releases remaining positions in the same authorized batch/destination.
Configuration/preparation is disabled while running or paused; to change destination,
cancel, drain, prepare and authorize a new batch. Completed/uncertain positions never
re-enter the queue. Resume does not re-send anything.

**Cancel** stops scheduling immediately and uses the transport cancellation token for
an active connect/write/response wait. A canceled attempted message remains distinct
from unattempted positions. A write-started cancellation/timeout/disconnect can leave
delivery or processing uncertain; cancellation is not proof of nondelivery. Windows
can accept the complete frame locally before the peer observes its first byte. Results
retain actual WriteStarted/WriteCompleted evidence rather than guessing from timing.

Closing/resetting the tool cancels and drains its owned task and releases captured/ACK
bytes. Application close also awaits batch-tool drains before normal close prompts and
resource teardown. Detached/reconstructed Dock views retain the live model, not another
run. Layout persistence stores placement only; restore/open never restarts a batch.

## Results and privacy-safe CSV

The source/batch ID/revision and exact captured destination apply to all rows. Each row
retains logical position, UTC start/end, elapsed connect/exchange duration, safe outcome,
write start/completion, commit code, application code, correlation status, ACK-frame
count, failure phase and optional SocketError enum. Application and commit negatives
remain distinct. **Open captured message** inspects a selected immutable payload,
including a not-yet-attempted one, in an independent unsaved viewer tab.
**Open ACK 1/2** deliberately publishes retained exact bytes through
the existing lossless HL7 viewer. No raw exception text is displayed/logged/exported.

Progress shows total, attempted (including an active exchange), completed and
unsuccessful completed exchanges, accepted, rejected/error, transport failures,
timed out, uncertain, canceled, not attempted/skipped, active position,
elapsed time including pauses, and average completed exchange time. Uncertain is an
overlapping evidence flag, so it must not be added to the exclusive classification
counts. Unattempted includes unsent positions after Stop/Cancel; it is not a delivery claim.

After completion, Stop or Cancel, **Export report…** explicitly chooses a CSV destination.
The existing picker/version consent and atomic write mechanism protect publication and
reject source-file/owned-storage replacement. Canceled export does not publish a partial
file. CSV is UTF-8 without BOM, CRLF, always quoted, with shared Core formula-injection
protection. It has one row per selected position plus one summary row.

Version-1 columns, in order:

```text
RecordType, BatchId, DocumentNumber, SourceRevision, Position, ProfileId,
DestinationAddress, Port, Scope, Security, Encoding, PacingMs, Policy,
StartedUtc, EndedUtc, DurationMs, WriteStarted, WriteCompleted, AckFrames,
CommitStatus, ApplicationStatus, Correlation, Outcome, Classification,
FailureStage, SocketError, Total, Attempted, Accepted, RejectedError,
TimedOut, Uncertain, Canceled, NotAttempted
```

Message rows leave summary columns empty; Summary leaves message-specific fields empty.
Only opaque run/profile IDs, document number/revision, pinned IP/port, fixed enums/codes,
timestamps and counts are exported. Source title/path/identity strings, friendly profile
names, notes, hostnames, MSH-10/MSA-2, patient identifiers, clinical values, credentials,
payloads and arbitrary exception strings are excluded. Activity Log records typed
start/end counts only. There is no telemetry/cloud service/new ScriptHost API.

## Bounds and limitations

| Resource | Bound and rationale |
| --- | --- |
| Selected positions/results | 1,000, fixed; bounded interactive review and result table |
| Prepared payloads | 32 MiB aggregate; selected messages only, strict refusal rather than truncation |
| One payload | Existing Stage 12 1 MiB eligibility limit |
| Retained complete ACK bytes | 16 MiB; later ACK bytes omitted explicitly once exhausted; all outcome metadata/frame counts retained |
| Live transport | One client/exchange; existing pinned endpoint/connect/ACK deadlines |
| Pacing | 1,000 ms default; 250–60,000 ms after every exchange |
| CSV | 4 MiB; atomic failure if exceeded |
| UI refresh | At most four timer refreshes/sec plus explicit action/completion changes; virtualized 1,000-row list |

Preparation reuses Stage 12 per-message capture, leases and strict eligibility; it does
not serialize/copy the whole document payload. Each capture temporarily enumerates the
existing bounded logical metadata; very large source sequences can therefore make
preparation slower even when a small selection is used. Only selected payloads remain.
The 16 MiB ACK retention budget can make later frames unavailable for opening; this is
shown explicitly, never confused with no ACK received. Reports are metadata-only and
are not a durable delivery ledger. Closing the tool releases results; restart resume,
automatic retry/recovery, TLS, connection pooling, concurrent loads, scheduling,
asynchronous callback ACKs and Stage 13B are deferred from this sequential tool.
Stage 13A native Windows acceptance passed all eight checks, including report polish:
[checklist and verification](stage13a-batch-transmission-acceptance.md).
