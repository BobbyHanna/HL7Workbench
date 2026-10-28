# SQL Querying — implementation status

Stage 15A/15B provide isolated execution, live-document capture, selective indexing,
path functions, bounded journals and outputs. Stage 15C exposes these through
**View → SQL Query**, with a docked editor/results workflow and explicitly saved
named queries. Start with [the user guide](sql-query-ui.md). Stage 15D remains gated.
See [HL7 workflows and function behavior](sql-hl7-workflows.md) and
[15B verification](stage15b-hl7-query-acceptance.md).

The engine accepts read-only SQLite result statements over six closed relations.
Synthetic live-document and relational tests exercise examples such as:

```sql
SELECT MessageNumber, MessageType, TriggerEvent, MessageControlId
FROM Messages
WHERE MessageType = 'ADT' AND TriggerEvent = 'A01'
ORDER BY MessageNumber;

SELECT m.DocumentId, m.MessageId, f.DecodedValue AS PatientId
FROM Messages m
JOIN Fields f ON f.DocumentId = m.DocumentId AND f.MessageId = m.MessageId
WHERE f.SegmentName = 'PID' AND f.FieldNumber = 3
  AND f.RepetitionNumber = 1;

SELECT TriggerEvent, count(*) AS Messages
FROM Messages
GROUP BY TriggerEvent
ORDER BY Messages DESC;
```

The schema is declared in `src/HL7Workbench.Querying/QuerySchema.cs`:

* Documents: DocumentId, DocumentNumber, Revision.
* Messages: DocumentId, MessageId, MessageNumber, MessageType, TriggerEvent,
  MessageControlId, Hl7Version, SendingApplication, SendingFacility,
  ReceivingApplication, ReceivingFacility, MessageTimestamp, HasDiagnostics,
  DiagnosticStatus.
* Segments: DocumentId, MessageId, SegmentId, SegmentName, SegmentOccurrence,
  SegmentOrdinal, Status.
* Fields: DocumentId, MessageId, SegmentId, SegmentName, SegmentOccurrence,
  FieldNumber, RepetitionNumber, RawValue, DecodedValue, ValueState, ValueStatus,
  ValueLength.
* Components: Fields coordinates plus ComponentNumber, with the same value columns.
* Subcomponents: Fields coordinates plus ComponentNumber and SubcomponentNumber,
  with the same value columns.

All identifiers/ordinals/lengths/revisions and HasDiagnostics use SQLite INTEGER;
other columns use TEXT. HasDiagnostics is nullable to represent incomplete diagnostic
assessment. Identity/coordinate/status columns are required; values and header
metadata are nullable. Keys are document-qualified. The engine validates types and
relational keys; Workspace produces authoritative HL7 states and coordinates from
Core's shared scanner. Fields has one row per actual field repetition; Components
and Subcomponents add their corresponding one-based coordinates. Absent nodes have
no row. SegmentId is the message-local ordinal; SegmentOccurrence counts each name.
ValueLength is raw UTF-16 units, not bytes. Header metadata comes from MSH using each
message's delimiters and captured encoding. It is not profile validation.

One statement is allowed. Internal imports and user parameters are bound, never
interpolated. Result scalars retain SQLite null/integer/real/text/blob types. Null
and empty text differ. Queries exceeding result/materialization limits fail with
no partial result; values are never silently shortened. The 65,536-character text
limit concerns explicitly materialized values, not the length of a source HL7 field.

See [ADR 0020](decisions/0020-isolated-read-only-sql.md) for limits, allowed functions,
read-only enforcement, privacy and later-pass decisions. SQL failures show
fixed diagnostic categories instead of SQLite messages that can contain sensitive
identifiers or query literals. Query execution is local and creates no disk database.
