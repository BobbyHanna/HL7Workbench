# ADR 0006: Stateful batch scripting and staged CSV reports

Historical decision: [ADR 0007](0007-unified-csharp-scripting.md) supersedes the
split kinds, callback registration and version-2 library schema below. The bounded
report/deferred-output and transaction decisions remain. Final unified native acceptance
was confirmed on 2026-10-16; the user authorized one milestone commit and normal push
to origin/main, with no tag/release or history rewriting.

Status: Accepted historical design. The final unified Stage 5B implementation under
ADR 0007 passed native Windows acceptance on 2026-10-16.
Supersedes ADR 0005's Batch/CSV deferral only. No architectural conflict requires a
redesign. Core, the source-capture gate, payload validation and snapshot transactions
remain authoritative. No new dependencies or second scripting engine are needed.

## Extension points and authoring

ScriptMode.Batch uses the existing fresh-per-job Roslyn worker. The submission runs
once (Initialize), captures ordinary C# locals, and registers synchronous callbacks:

```csharp
var count = 0;
var report = Batch.Report("summary", "Messages");
Batch.Process = message => { count++; };
Batch.Complete = () => report.Row(count.ToString());
```

Process is required; Complete is optional. Process receives the existing Core-backed
ScriptMessage facade, once per selected message, in original source order. Batch's
MessageNumber exposes the original one-based ordinal during Process, zero otherwise.
Read/Text/SetText/SetRaw/SetNull/Clear/Exclude/Segments retain Stage 5A semantics.
Message scripts keep fresh submission state per message. Generate remains rejected.
Async callbacks are not supported: do not assign async lambdas to these Action
callbacks or launch background work. Host calls belong to the synchronous lifecycle.

Batch.Defer(message) copies the current transformed message into a bounded queue;
copies are emitted in queue order after Complete. For decisions requiring final
state, Batch.Hold(message) returns an opaque immutable copy; Batch.Emit(copy) queues
it during Complete. Only the owning context may emit it. Exclude separately for delayed-only
output. There is no arbitrary source-free emission or cloning API. The parent uses
the retained source ordinal to validate payloads and reproduce original envelopes.
Deferred output/exclusion require extractable plain-message partitions or complete
unambiguous MLLP envelopes. Batch wrappers cannot retain stale counts. Adjacent
payloads still must index to exactly the expected boundaries; nothing inserts or
normalizes separators, bytes, encodings, terminators or BOMs.

## Worker and bounds

Compile once, then initialize under the operation timeout. Process each message with
one outstanding request; Complete uses the same timeout. Cancellation retains the
independent worker reader, 200 ms grace and forced tree termination. Windows Job
Objects enforce the existing 768 MiB committed-memory ceiling. Arbitrary captured
C# object graphs cannot be counted reliably; this is process containment, not an
object accounting promise or security sandbox. Trusted scripts have user permissions.

Limits: 100,000 input messages; 128 retained copies / 16 MiB combined and 128 queued
emissions / 16 MiB combined (repeated Emit calls also count); eight named
reports; 64 columns/report; 16,384 UTF-16 units/cell; 100,000 data rows and 16 MiB
UTF-8 CSV globally; 256 KiB CSV per Initialize/Process/Complete operation. The small
operation buffer drains at each response. Summaries larger than this must emit rows
during Process. Headers count toward bytes but not data rows. Existing 65,536-unit
script, 8 MiB payload, 12 MiB IPC, 1 Mi-unit message, 16 Ki-unit segment, 1 GiB input
and output staging defaults, 10-second operations, 60-second compilation, 30-second
startup and 10-minute total timeout remain. Input is streamed one message at a time.
Parent report staging checks count/byte/name/schema bounds independently.

## CSV and transactions

Report(name, columns) defines schema during initialization; Row(string?[]) enforces
column count/order. Names are ASCII letters/digits/underscore/hyphen, 1–64 characters,
case-insensitively unique. UTF-8 without BOM, CRLF records, every cell quoted, embedded
quotes doubled. Null and empty both produce an empty quoted cell. Unicode is strict.
Formula policy: prefix an apostrophe to cells whose first non-whitespace character
is =, +, - or @, or whose first character is tab/CR/LF. Applies to headers too.
This deliberately changes spreadsheet-risky strings, including negative numbers;
there is no evaluation or unsafe opt-out.

Workspace streams interleaved chunks to one private bounded snapshot journal with
per-report range metadata. Output and reports remain private through Complete,
deferred drain, authoritative output indexing and pending-document construction.
Failure/cancellation/crash disposes both artifacts. No completed partial output.
Successful output is an independent unsaved document; source and undo remain intact.

Publishing an app document and arbitrary filesystem files cannot form one atomic
transaction. Publish the validated HL7 document first and retain sealed reports in
the editor session. User explicitly exports a selected report using a native picker.
Final Stage 5B acceptance amendment: export writes/flushes a same-directory temporary
file, then permits replacement only after explicit user authorization and destination
version checks. A path alone stays creation-only. This supersedes the original unconditional
no-overwrite policy; see [save/export safety](../save-and-export-safety.md) for provider,
atomic replacement and race limitations. Failed/canceled
export leaves the document and staged reports available for retry, and no partial
destination. Multiple exports are independent. Retain the last successful reports
until a subsequent successful run or editor-model disposal; failed runs retain them.
Active exports keep a read lease until drained. Normal close of the tool retains its
session under the existing dock lifetime policy; application shutdown releases it.
Snapshots use existing abandonment cleanup; deletion is not secure erasure.

## Library, Desktop and privacy

Version-2 library records include Message/Batch kind. Version-1 entries open as
Message without rewriting on load. Save atomically upgrades; duplicate/draft kind
survives reconstruction with existing save/conflict/close behavior. Bundled examples
are immutable. New offers Message/Batch creation; the secondary Script actions →
Change Script Type menu changes draft metadata deliberately without rewriting source.
Library selection shows the active draft's kind even before saving. Scope, Run/Cancel/
progress and report selector/export extend the same tool, retaining docking, font and
coloring behavior. Report attribution is captured from script name/kind/scope at
execution start and replaced only on success, alongside the reports. Last successful
run labels/tooltips distinguish it from current script selection. Attribution is
session-only and never added to logs, storage or layouts; no report history is added.

The synthetic identity example uses the complete raw PID-3 composite (including
assigning authority) as an opaque in-worker key, only after requiring exactly one
editable PID and a nonempty PID-3.1, no repeated PID-3 and no Core diagnostics.
Missing/ambiguous identities or diagnosed malformed input fail the whole run.
Opaque spelling can distinguish semantically equivalent identifiers; no clinical
correlation is claimed. Sequential unique synthetic IDs/names avoid collisions in
the assigned output set. Reports contain counts only. The mapping never enters IPC
or storage. Other identifying fields remain; this is not clinical de-identification
and provides no HIPAA guarantee. No source values, row contents, exception text or
mapping keys enter diagnostics/logs. Explicitly requested CSV is user-owned data;
trusted scripts can deliberately report PHI or perform external side effects, which
the application transaction cannot undo. No network/cloud/telemetry is introduced.
