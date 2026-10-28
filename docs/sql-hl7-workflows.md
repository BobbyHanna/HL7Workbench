# Querying loaded HL7 and producing outputs

Stage 15B exposes independently testable Workspace APIs. Stage 15C adds
[the docked SQL Query UI and saved definitions](sql-query-ui.md). This page describes
the underlying source, function and complete-output contracts, shared by the UI.
See [schema reference](sql-query.md) and [ADR 0020](decisions/0020-isolated-read-only-sql.md).

## Scope and captured data

Call `SqlQuery.RunAsync(sql, selectedDocuments, parameters, progress, token)` on the
document-owning context. Omitted scope uses the active document. Explicit scope uses
selected open documents in workspace order. Logical order, stable IDs, working edits,
original source bytes and encodings are frozen. Copying, indexing and execution run
off the caller thread. Later edits/closure cannot retarget completed results;
`IsCurrent` reports original revision currency. Dispose `SqlQueryResults` when done.

DocumentId plus MessageId is the authoritative captured key. Join on both, plus
SegmentId when addressing individual segments. Control IDs/patient IDs can repeat;
MessageNumber is a logical display position, not identity.

Metadata-only message queries decode just the first header line. They do not decode
OBX bodies or import field text. HasDiagnostics=1 means diagnostics were observed;
NULL means incomplete assessment. DiagnosticStatus is HeaderOnly, Complete,
Unreadable or HeaderLimit. Only a full structural assessment permits HasDiagnostics=0.

Structural references scan all scoped messages under existing Core budgets. Malformed
lines have Segments.Status=Unstructured; valid segments use Complete. No invented
fields make malformed input appear valid. Undecodable structural input fails.
Fields/Components/Subcomponents import coordinates, raw lengths and structural states,
then materialize RawValue/DecodedValue/ValueStatus only when SQLite requests them.
Metadata equality constraints can narrow those lazy reads. SQL evaluation order is
not guaranteed: a value may be requested before another predicate runs.

RawValue preserves spelling. DecodedValue uses Core escapes and actual delimiters.
ValueState distinguishes Value, Empty and ExplicitNull; missing nodes have no row.
Explicit null has raw `""` and decoded SQL NULL. Empty text remains `''`.
ValueStatus reports Complete, DecodeDiagnostic, ValueLimit, Unstructured or Unreadable;
the internal import placeholder NotMaterialized never substitutes for a requested value.
Unresolved decoded escapes fail the query; RawValue and ValueStatus permit inspection.
Oversized requested values fail, never silently become NULL, shortened text or nonmatches.
Common-segment convenience views are deferred; general relations and paths apply to
ADT, ORU and other message types.

## Path functions

```
HL7_VALUE(DocumentId TEXT, MessageId INTEGER, path TEXT) -> decoded TEXT or NULL
HL7_RAW(DocumentId TEXT, MessageId INTEGER, path TEXT)   -> raw TEXT or NULL
HL7_STATE(DocumentId TEXT, MessageId INTEGER, path TEXT) -> state TEXT
HL7_STATUS(DocumentId TEXT, MessageId INTEGER, path TEXT)-> status TEXT
```

Functions reuse [Core paths](hl7-paths.md), including `PID-3[2].1.2` and
`OBX[2]-5[1].1`. Omitted occurrence/repetition selects the first; use relations to
expand all occurrences. MSH-1/2 are literal fields. Invalid paths fail InvalidPath;
unknown captured pairs fail SourceUnavailable. MSH-only reads use bounded headers
where possible. Other paths need a full scan with at most one cached decoded spool.

| Condition | VALUE | RAW | STATE | STATUS |
| --- | --- | --- | --- | --- |
| Value | Decoded text | Exact text | Value | Complete |
| Empty | Empty text | Empty text | Empty | Complete |
| Explicit null | SQL NULL | Two double-quote characters | ExplicitNull | Complete |
| Missing | SQL NULL | SQL NULL | Missing | MissingSegment/Field/Repetition/Component/Subcomponent |
| Unresolved escape | Query fails | Exact text | Structural state | DecodeDiagnostic |
| Value exceeds cap | Query fails | Query fails | Structural state | ValueLimit |
| Undecodable bytes | Query fails | Query fails | Query fails | Unreadable |

Unstructured selection reports Unstructured/Unavailable and refuses decoded values.
Exactly one pinned document also registers `(MessageId, path)` overloads. They never
follow a changing active tab and are absent in multi-document scope:

```sql
SELECT MessageNumber, HL7_VALUE(MessageId, 'PID-3.1') AS PatientId FROM Messages;
```

## Practical examples

The [schema guide](sql-query.md) shows ADT A01 selection and trigger-event aggregation.
Use named parameters rather than interpolating patient values:

```sql
-- @patientId: new QueryParameter("@patientId", QueryCell.From("SYN-0001"))
SELECT DocumentId, MessageId, MessageNumber
FROM Messages
WHERE HL7_VALUE(DocumentId, MessageId, 'PID-3.1') = @patientId;

SELECT m.DocumentId, m.MessageId, f.SegmentOccurrence, f.DecodedValue
FROM Messages m JOIN Fields f
  ON f.DocumentId=m.DocumentId AND f.MessageId=m.MessageId
WHERE f.SegmentName='OBX' AND f.FieldNumber=5
  AND f.DecodedValue LIKE '%synthetic%';

SELECT m.DocumentId, m.MessageId, m.MessageNumber
FROM Messages m WHERE NOT EXISTS (SELECT 1 FROM Segments s
  WHERE s.DocumentId=m.DocumentId AND s.MessageId=m.MessageId
    AND s.SegmentName='PID');
```

## Complete outputs, independent of visible pages

`PageAsync(page, token)` returns 100 rows with 256-character previews and explicit
scalar kinds/abbreviation flags. `ReadRow(index)` deliberately returns a complete
bounded row for full-value inspection. UI controls are deferred.

`await result.ExportCsvAsync(destination, token)` exports **every journal row** and
full values behind previews. Core's CSV encoder uses UTF-8 without BOM, commas,
quoted fields/escaped quotes, CRLF records and the established apostrophe guard for
formula-leading text. Embedded newlines and Unicode survive. Each value has a [Type]
companion column, so SQL NULL and empty TEXT remain distinct. The presentation-order
overload accepts only a complete unique permutation, so UI sorting never creates a
partial export. Existing destinations
require FileSaveDestination overwrite consent. Source files/aliases and owned storage
are protected. Atomic publication leaves no partial CSV on cancellation/quota failure.

For provenance-bearing columns named DocumentId and MessageId, check
`CanIdentifyMessages`, then call `result.OpenMatchingMessagesAsync(manager, token)`.
Native column origins are checked; every typed pair is validated against the captured
registry. Deduplicate all result rows and preserve captured document/message order.
No result selection automatically opens/navigates a message.

For aggregates or reports without identity columns, run a **separate identity query**:

```csharp
var query = new SqlQuery(documentManager, options);
var output = await query.OpenMatchingMessagesAsync(
    "SELECT DocumentId,MessageId FROM Messages WHERE MessageType='ADT' AND TriggerEvent='A01'",
    selectedDocuments, token: cancellationToken);
```

Both paths consume complete bounded results, never visible pages alone. All SQL
result caps still apply; the separate operation also has matching-message/output
caps. Any overflow fails without publishing a document. Explicit SQL LIMIT restricts
the selection intentionally; remove it for all matches. No matches opens nothing.

Byte ranges, terminators and framing are copied; changed drafts use strict captured
encoding. The file indexer validates the assembled bytes before an independent,
dirty, unsaved SQL Matches.hl7 document opens. Originals remain unchanged. Mixed
encodings, batch/file wrappers or unmapped outer regions, interior BOM/ambiguous
framing, unencodable drafts and unsafe unterminated joins are refused rather than guessed.

## Privacy, resources and limitations

SQLite has no disk database. Workspace temporary captured bytes, decoded spools and
journals **can contain sensitive data**; they use existing snapshot ownership and
exclusive leases. Dispose results to cancel/drain readers and delete owned storage.
Crash leftovers follow existing abandoned-snapshot cleanup. No query text, history,
results or raw cache is persisted as a setting. This is not secure erasure or a
general-purpose security sandbox. SQL/value text is never in ordinary logs or args.

Defaults can be lowered, not raised:

| Resource | Cap |
| --- | --- |
| Scope | 16 documents; 100,000 messages; 1 GiB captured bytes; 16 Mi UTF-16 frozen drafts |
| Structure/cache | One decoded spool/model; Core 250,000 elements, 100,000 segments, 1,024 diagnostics per message |
| Import / source reads | 250,000 metadata rows / 64 MiB accounted; 100,000 source reads |
| Explicit scalar / row | 65,536 UTF-16 units / 256 KiB; 512 KiB accounted row |
| Results | 64 columns; 100,000 rows; 16 MiB accounted; 128 MiB encoded journal |
| Presentation | 100 rows/page; 256-character previews; full bounded ReadRow |
| Extraction / CSV | 100,000 unique messages / 1 GiB output; 256 MiB CSV |
| Deadlines | 30s SQL/exchange; 10m capture-through-journal and worker session |
| Worker | 128 MiB SQLite heap; 64 MiB database pages; 512 MiB Windows job |

Capturing still copies source bytes even for metadata queries. A UTF-16 decoded spool
may occupy approximately twice source bytes on disk; source/output/journal coexist.
Structural queries scan all messages before SQL execution; message predicates are
not pushed through Core scanning. Repeated uncached path reads can rescan messages
and hit deadlines. Initial results are bounded in parent memory before journaling.
Accounting ceilings do not promise exact RSS/disk usage. Narrow scope/select columns
and inspect ValueLength/HL7_STATUS before requesting large values.
