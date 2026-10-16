# Unified C# scripting API

View → Script Editor is a dockable/floating tool. Scripts ribbon → New (or editor **New**) creates an editable
`void Main()` starter. Name it, Save Script, choose **Apply to: Current message /
Whole document**, and Run. The identical script works in either scope. There are
no Message/Batch script types. Duplicate makes editable copies of immutable examples
or the current draft; Ctrl+S saves the script, independently of the HL7 document.
The Scripts ribbon and editor ⋯ share commands. The permanent menu is File | Settings |
View; Scripts ribbon → Open Script Editor also reveals/focuses the retained tool. Click the
current name to rename; Enter/focus loss confirms a valid draft name, Escape cancels,
and Save Script persists it with current code. Delete Script requires saved user
ownership, dirty-draft handling and confirmation; immutable examples are protected.

## Lifecycle and shared state

Each Run creates a fresh worker/context and compiles the user submission once.
Top-level statements initialize once. The host calls Main once per selected message,
in source order, then optional Complete once after all Main calls succeed. A
one-message run follows exactly the same lifecycle. Processing failure skips Complete.
Every new Run starts with fresh state; nothing is shared between workers.

```csharp
void Main()
{
    Message.SetText("PID-5.1", "SYNTHETIC");
}
```

Top-level variables persist across Main calls; Main locals start fresh each call.
Helpers are ordinary C# methods and resolve Message to the current Main invocation:

```csharp
int count = 0;
var summary = Batch.Report("summary", "Messages");
string SyntheticName() => "SYNTHETIC";

void Main()
{
    int local = 0; // A fresh local on each call.
    local++;
    Message.SetText("PID-5.1", SyntheticName());
    count++;
}

void Complete()
{
    summary.Row(count.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
```

For the three-message synthetic fixture, Current message reports 1; Whole document
reports 3. Initialization/report schema creation occur once in either scope. Return
from Main leaves its normal output included; use Message.Exclude() to omit it.

Define exactly one top-level instance, synchronous, nongeneric, parameterless
`void Main()`. Complete is optional and has the same signature. Expression bodies
and helper methods are supported. Entry-point overloads, duplicate declarations,
async/Task-returning entries, static methods, parameters and non-void returns fail
before initialization. Nested classes' methods do not count as entries. Ordinary
C# compiler errors take precedence and retain source locations.

Roslyn binds method symbols and one fixed host-owned continuation to delegates
retaining the initialized user submission. The host invokes those delegates directly;
it does not recompile, rerun initialization or grow a continuation chain per message.
Do not register callbacks. Do not launch background work or async-void helpers with
the host facade. Trusted C# external side effects cannot participate in rollback.

## Globals and phase rules

Globals are Message, Batch and Cancellation. Batch is an advanced facilities object,
not a script kind. Message always resolves to the current Main message and throws
a fixed LifecyclePhase diagnostic during initialization/Complete. It is not the last
message of the run.

| Facility | Initialization | Main | Complete |
| --- | --- | --- | --- |
| Message | Unavailable | Current selected message | Unavailable |
| Batch.MessageNumber | 0 | Original one-based source ordinal | 0 |
| Batch.Report(name, columns) | Define schema | Unavailable | Unavailable |
| report.Row(values) | Append | Append | Append |
| Batch.Hold(Message), Batch.Defer(Message) | Unavailable | Current message only | Unavailable |
| Batch.Emit(heldCopy) | Unavailable | Unavailable | Own run's held copies |
| Cancellation | Available | Available | Available |

Current scope captures the chosen message's supported unsaved working edit; Whole
document captures all supported working edits. Capture still snapshots the entire
backing file, so its input-storage quota applies even to Current scope. No presentation
preview is an execution value. Unsupported complete-message sizes/encodings and unsafe
framing fail explicitly. Source text, undo and save identity remain untouched.
Successful output is always a new independent unsaved document.

## Message facade

Core one-based [paths](hl7-paths.md) apply. No alternative HL7 parser is introduced.

| Member | Contract |
| --- | --- |
| Read(path) | ScriptValue: Missing/Empty/ExplicitNull/Value and original Raw spelling |
| Text(path) | Explicit decoded text; fails on missing/null/unresolved escape diagnostics |
| SetText(path, text) | Core-escaped literal text using original encoding/delimiters |
| SetRaw(path, raw) | Validated raw spelling within the selected scope |
| SetNull(path) | Explicit HL7 null marker |
| Clear(path) | Empty an existing value; missing is a no-op, never structural removal |
| Exclude() | Omit normal output |
| Segments("PID") | Occurrence views with relative Read/Text/SetText/SetRaw/SetNull/Clear |
| CanEdit | Core editability, not clinical validity/conformance |
| HasDiagnostics | Any Core diagnostic; conservative scripts can refuse diagnosed input |

Unchanged messages preserve exact original bytes, BOM, terminators and spelling where
supported. Controlled changes use Core; delimiters stay read-only. No implicit attachment
decoding, normalization, insertion/removal or generation.

## Deferred output and completion decisions

Defer copies the current transformed message for FIFO emission after Complete.
Normal output is included separately unless Exclude is called:

```csharp
void Main()
{
    Batch.Defer(Message);
    Message.Exclude();
}
```

Hold captures an opaque immutable copy for a final decision using complete run state:

```csharp
var held = new List<ScriptDeferredMessage>();
void Main()
{
    if (held.Count < 2) held.Add(Batch.Hold(Message));
    Message.Exclude();
}
void Complete()
{
    for (int i = held.Count - 1; i >= 0; i--) Batch.Emit(held[i]);
}
```

This example outputs only the first two selected messages, in reverse order. Copies
retain call-time bytes; later changes do not alter them. Only this run's copies may
be emitted. Repeated Emit calls count as additional bounded output. These facilities
require extractable plain-message partitions or complete unambiguous MLLP envelopes.
Wrapper counts cannot become stale, and missing separators cannot be normalized.
The parent validates source ordinals, payloads, envelopes and all final indexed ranges.

## Bundled synthetic identity example

The immutable consistent-patient example uses a dictionary shared across Main calls
and produces a summary CSV in Complete. It requires exactly one editable PID, a
nonempty decoded PID-3.1, no PID-3 repetitions and no Core diagnostics; failures abort
the whole run. Its opaque full raw PID-3 key includes assigning authority. Distinct raw
spellings stay distinct; no clinical correlation is inferred.

At most 10,000 unique keys receive sequential SYN-P000001 identifiers and
SYNTHETIC^PATIENT000001 names. Selected alternative identifiers/contact fields are
cleared and DOB/sex replaced. Reports contain patient/message counts only. Keys/mappings
stay in worker memory and reset per Run. Other identifying fields may remain:
**not complete clinical de-identification and no HIPAA guarantee**. Use synthetic data
for acceptance and review any broader policy yourself.

## Reports, ownership and export

Define ordered columns once with Batch.Report("summary", "A", "B"); append strings
with report.Row("value", null). Column count is enforced. Names are 1–64 ASCII letters,
digits, underscore or hyphen, case-insensitively unique. Multiple reports may interleave.
Use invariant formatting for numbers.

CSV is strict UTF-8 without BOM, CRLF records, all cells quoted, embedded quotes doubled.
Commas, CR/LF and Unicode are retained. Null and empty both become an empty quoted cell.
Headers use the same rules. An apostrophe prefixes any cell whose first character
after whitespace/control characters is =, +, - or @, or whose first character is
tab/CR/LF. This intentionally changes formula-like strings, including negative numbers.
No formula evaluation or unsafe opt-out is provided.

Output and reports stage privately until initialization, all Main calls, Complete,
deferred draining and authoritative indexing succeed. Failure/cancellation/crash
publishes neither partial output nor new reports. After success the output opens,
and retained reports are available through Scripts ribbon → Reports / editor ⋯ → Reports / Export CSV.
The contextual window shows producing name/scope and individual report selection;
the editor has no permanent report/export toolbar. Picker/export retain the producing
journal and block replacement until finished. Script deletion keeps reports and output.

The reports window and action tooltip identify the execution-time script display name
and scope, independently of current script selection/renaming. Failure/cancellation
retain earlier attribution. A later successful run replaces reports/origin; success
without reports clears them. No report history, rows, source or origin metadata enter
logs/layouts. Export before another successful run or application shutdown; normal
tool hiding/reconstruction retains the session.

Existing files may be replaced only after explicit user authorization, using safe
replacement semantics. Windows' audited native overwrite prompt supplies consent;
other providers require application confirmation. Export flushes a same-directory
temporary file and verifies destination identity/version before replacement. An
unauthorized path is creation-only; collisions/changed destinations fail and require
fresh selection. Failure/cancellation preserves existing content and retained reports.
See [save/export safety](save-and-export-safety.md) for provider/race/filesystem limits.
Each export is independent: no atomic transaction spans arbitrary CSV destinations
and an application document. Staging is temporary, not encrypted storage or secure erasure.

## Library transition

Library version 3 stores name/source/identity and a migration-warning flag, with no
script-kind field. Version-1/2 development records are obsolete: opening/listing never
rewrites/deletes them. They show **Needs update**; Run stays disabled until an explicit
Save adopts the unified format. Rewrite using Main/Complete first. Duplicate retains
the warning until explicit Save, so copying cannot silently execute old semantics.
Adoption is atomic/conflict checked and preserves the ID. Unsupported/corrupt records
still fail safely without abandoning the current draft.

No old Message/Batch syntax compatibility is provided. A saved version-3 body without
a valid Main fails compiler/entry validation before initialization. Other settings,
fonts and layout preferences are preserved. Unsaved drafts are not crash recovery.

## Bounds, cancellation and trust

| Resource | Default/hard cap |
| --- | --- |
| Script | 65,536 UTF-16 units |
| Complete message / segment | 1,048,576 / 16,384 UTF-16 units; 8 MiB payload |
| Input messages / indexed output | 100,000 each |
| Windows process/job committed memory | 768 MiB |
| Retained copies / queued emissions | Each capped at 128 and 16 MiB combined |
| Reports / columns | 8 / 64 per report |
| Cell size | 16,384 UTF-16 units |
| Global report rows / UTF-8 bytes | 100,000 data rows / 16 MiB, headers included in bytes |
| Report buffer per initialization/Main/Complete | 256 KiB, drained after each response |
| IPC | Version 2; 12 MiB frames, one outstanding request |
| Input / HL7 output staging | 1 GiB each, configurable up to snapshot ceiling |
| Startup / compile / each lifecycle operation / total job | 30 sec / 60 sec / 10 sec / 10 min |
| Diagnostics / local library records | 100 / 256 plus immutable examples |

Stream large reports during Main; do not retain every input/row until Complete.
Arbitrary C# state is bounded through process memory, not reliable object accounting.
Windows is the containment/acceptance platform; other OSes lack equivalent enforced
memory limits. Cancellation allows 200 ms cooperation, then kills/reaps the worker tree.
Diagnostics contain fixed codes and original script line/column, source ordinal and
canonical paths, never exception text or imported values.

The worker runs trusted code with user permissions, **not a security sandbox**.
External reference/source/line directives remain blocked. No network API, telemetry,
cloud dependency, third-party parser, report viewer, Generate/templates, cross-document
input, source mutation, structural insertion/removal, IDE or debugger is added.
