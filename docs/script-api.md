# Unified C# scripting API

## Offline reference and starter examples (Stage 9C)

Open **Help → Script API Reference** or the visible **API Reference** button beside
the Script Library controls. The dockable/modeless tool works without a document or
network. Search class/member/property names or keywords, select a category, inspect
signatures/parameters/returns/limits and use Back to restore previous navigation.
Copy signature or Copy example uses the normal clipboard; paste examples into an
editable script with Ctrl+V. Built-ins remain read-only until Duplicate Script.

Search `starter` for six complete examples: guarded PID read/write, host-driven
message iteration, an existing field repetition, independent output, a count-only
bounded CSV and the existing synthetic identity helper. **Create editable script**
opens an unsaved My Scripts draft after the normal Save/Discard/Cancel prompt. Save
Script, reopen and Run in either Apply to scope. This adds no duplicate built-in
presets; duplicate Patient Deidentifier to customize its complete supported policy.
User copies keep independent IDs/source through application/built-in updates.

The authoritative [versioned JSON](script-api-reference.json) is embedded verbatim
in Workspace and drives in-app descriptions, search and starter source. The
[readable reference](script-api-reference.md) is generated from it, including every
supported declared public facade/helper member. Edit JSON, run
`python build/WriteScriptApiReference.py`, and check with `--check`. The permanent
tests classify every public scripting type, compare supported member inventories
(including overload counts) and compile/execute every distinct complete example
through the real worker. Generated record/inherited .NET members and parent transport
types are explicitly outside host authoring operations. The sections below explain
the existing workflows and transaction contracts; member details live in the catalog.

The existing public SyntheticIdentityLibrary and DeidentificationWorkflow already
work in authored scripts. No additional facade, execution capability, dependency,
engine/editor rewrite or completion system is introduced. Helpers use bundled data
and execution-local mappings without IO/network. Trusted arbitrary C# still has the
user's permissions; ScriptHost containment is not an OS security sandbox. See the
[Stage 9C acceptance record](stage9c-script-developer-experience-acceptance.md).

Stage 9 adds three reviewable/duplicable Script Library presets: **Patient Deidentifier**,
**Identifier Pseudonymizer**, **Timestamp Shifter**. Each initializes a run-local
`DeidentificationWorkflow(new DeidentificationOptions { Preset = ..., Seed = 42 }, Cancellation)`,
calls `workflow.Apply(Message)` in Main and emits `workflow.Summary.Cells` into one
CSV row in Complete using `DeidentificationSummary.Columns`. Optional ShiftDays
overrides the seed-derived common displacement; MaximumMappings can lower its budget.
Existing Batch output naming supplies static Review Required suggestions, never a path.
Duplicate the immutable built-in to configure it; saved user scripts are not replaced.

These are de-identification aids, not anonymity or compliance guarantees. Reports
always say **Review required** and explicitly flag unassessed container bytes/nested
metadata and residual clinical content. Narrow identifier/timestamp presets retain
other sensitive content. No mapping persists outside the worker. Read the
[coverage, limits and residual risks](stage9-deidentification-acceptance.md) before
execution or sharing. Existing worker isolation/rollback and stale-source attribution
apply without a new protocol or scripting engine. See [ADR 0013](decisions/0013-script-deidentification-aids.md).

Native acceptance corrections introduce Patient policy 2 and bundled vocabulary 1:
`SyntheticIdentityLibrary(seed).Get(opaqueIdentityKey, Cancellation)` supplies a
deterministic fictional complete profile, independently of original demographics.
Patient names/addresses/contacts/DOB/sex and supported ages are substituted; both
narrow presets retain policy-1 behavior. Missing/empty/null components remain so.
`Summary.Categories` supplies transformed/suppressed leaf counts. A successful fixed
review row is projected into an expanded **Transformation Report** in each output
document, independent of later editor runs. It tracks captured revision and stale/
closed source without rendering arbitrary CSV cells or retaining the source owner.
See the acceptance matrix for supported demographics/age codes and residual risks.

View → Data & Query → Script Editor is a dockable/floating tool. Scripts ribbon → New (or editor **New**) creates an editable
`void Main()` starter. Name it, Save Script, choose **Apply to: Current message /
Whole document**, and Run. The identical script works in either scope. There are
no Message/Batch script types. Duplicate makes editable copies of immutable examples
or the current draft; Ctrl+S saves the script, independently of the HL7 document.
The Scripts ribbon and editor ⋯ share commands. The permanent menu is File | Settings |
View | Help; Scripts ribbon → Open Script Editor also reveals/focuses the retained tool. Click the
current name to rename; Enter/focus loss confirms a valid draft name, Escape cancels,
and Save Script persists it with current code. Delete Script requires saved user
ownership, dirty-draft handling and confirmation; immutable examples are protected.

## Lifecycle and shared state

Each Run creates a fresh worker/context and compiles the user submission once.
Top-level statements initialize once. The host calls Main once per selected message,
in captured logical input order, then optional Complete once after all Main calls succeed. A
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
| Batch.MessageNumber | 0 | One-based job-local input ordinal | 0 |
| Batch.SetOutputFileName(name) | Suggest filename | Suggest filename | Suggest filename |
| Batch.Report(name, columns) | Define schema | Unavailable | Unavailable |
| report.Row(values) | Append | Append | Append |
| Batch.Hold(Message), Batch.Defer(Message) | Unavailable | Current message only | Unavailable |
| Batch.Emit(heldCopy) | Unavailable | Unavailable | Own run's held copies |
| Cancellation | Available | Available | Available |

Stage 8B.4 binds Current scope to the selected stable MessageId; Whole document
captures every live logical entry and its required current draft, including insertions.
Current scope uses original-file fallbacks for inactive originals and explicitly omits
inactive insertions from its input map. An invalid unrelated draft therefore cannot
block a valid current target; the selected target is never omitted. Capture still
streams the backing originals, so the input-storage quota applies even to Current scope.
No presentation preview is an execution value. Unsupported sizes/encodings, invalid
required payloads and unsafe framing fail explicitly. Source text/history/save identity
remain untouched. Successful output is a new independent unsaved document.

Batch.MessageNumber is meaningful only within that immutable job. Without insertions
it retains the earlier ordinal behavior; it is never a persistent document identity.
Workspace ScriptInputMapping resolves it to MessageId, captured logical position and
optional original ordinal. Output attribution also covers deferred/excluded ordering.
After capture, edits, insertion and structural Undo/Redo preserve completed independent
output and reports. ScriptExecutionResult.Input retains the captured document/revision
and mapping; SourceChangedDuringRun records whether that source revision changed before
publication. The Script Editor and retained report attribution explicitly show source
document number, captured revision and "Stale source" when applicable, including after view reconstruction
or a later failed run. This is producing-run attribution, not a live freshness check.
Newer source work is retained. Navigation and active-tab switches never retarget a
captured job; a tab switch alone does not mark unchanged input stale. Source closure,
cancellation and real failures still prevent publication. InputChanged applies to a
queued source revision conflict before capture, when no immutable input exists yet.
This preserves the independent-captured-output policy explicitly selected by the user. See
[8B.4 acceptance](stage8b4-compatibility-acceptance.md); worker protocol/API version 2,
isolation, limits and library schema remain unchanged.

## Suggested output filenames

Each successful independent output starts with the script name captured at Run,
without the editor's unsaved marker. For example, Patient Deidentifier produces
`Patient Deidentifier.hl7`. Later library selection or renaming cannot relabel that
run. An unavailable or unusable name falls back to `Script Result.hl7`.

An optional override is available in initialization, Main and Complete:

```csharp
void Main() { }
void Complete()
{
    Batch.SetOutputFileName("Deidentified_Patients.hl7");
}
```

The last valid suggestion wins. Invalid suggestions are ignored without failing the
script or erasing an earlier valid suggestion. If none was valid, the captured script
name/default is used. A fixed, nonfatal notice in successful run/report attribution
indicates ignored suggestions; invalid values themselves are not echoed or logged.

The same deterministic Windows policy applies on every host, without filesystem or
locale lookup. A suggestion must be a filename, limited to **128 UTF-16 units including
the extension**. `.hl7` is appended when absent, and an existing case-insensitive `.HL7`
ending is standardized to `.hl7`. Other extensions are retained as part of the stem:
`audit.v2` becomes `audit.v2.hl7`. Valid Unicode is retained without normalization.

Paths/separators, drive/UNC syntax, `.`/`..`, an empty basename, Windows-invalid
`<>:"/\|?*`, control/format characters, unpaired surrogates, leading whitespace,
trailing whitespace/dots and overlong values are refused. Device names are rejected
case-insensitively before the first extension, including CON, PRN, AUX, NUL, CONIN$,
CONOUT$, COM1–9/LPT1–9 and their superscript 1/2/3 variants. No truncation or path
extraction silently turns an invalid path into a valid suggestion.

The validated filename is result metadata and the output document's Title, used by
Save As. Duplicate outputs retain separate identities and the same suggested filename;
only unsaved tab/display labels gain `(2)`, `(3)`, etc. The dirty `*` remains presentation
only. Saving follows existing behavior: the selected actual filename becomes the
document title and future Save/Save As identity. No filename is embedded in HL7 bytes.

This API never saves, selects a directory, changes the source document/destination,
or bypasses validation/isolation. Stale-source output, captured identity/revision,
job ordinal mapping, report ownership and cancellation/failure rollback remain intact.
API and frame protocol stay at version 2; optional Finished-frame OutputNaming metadata
is version 1. Missing metadata uses the captured default; unknown versions or unsafe
imported names are ignored with the same notice. Older frame readers ignore the
additive field. Script Library persistence is unchanged.

See [output naming acceptance](script-output-naming-acceptance.md) for permanent
coverage, verification and native Windows checks.

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
The parent resolves job-local ordinals through the captured ID map and validates
payloads, envelopes and all final indexed ranges.

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
Diagnostics contain fixed codes, original script line/column, job-local ordinal and
canonical paths, never exception text or imported values. Parent mapping/presentation
identifies captured logical position and original ordinal separately where available.

The worker runs trusted code with user permissions, **not a security sandbox**.
External reference/source/line directives remain blocked. No network API, telemetry,
cloud dependency, third-party parser, report viewer, Generate/templates, cross-document
input, source mutation, structural insertion/removal, IDE or debugger is added.
