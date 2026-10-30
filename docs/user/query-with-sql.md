# Query loaded HL7 with SQL

Use **View → Data & Query → SQL Query**. Prerequisites: an open document and a
complete build with **QueryHost/** deployed. Queries run locally in an isolated
read-only SQLite worker over captured HL7, not against an external database.

## Run a query and open matching messages

1. Open [adt-a01.hl7](../../samples/synthetic-only/adt-a01.hl7). In SQL Query,
   select **Current document**. Selected open documents is available for multi-file queries.
2. Enter this single statement:

   ```sql
   SELECT DocumentId, MessageId, MessageNumber, MessageControlId
   FROM Messages
   WHERE MessageType = 'ADT' AND TriggerEvent = 'A01'
   ORDER BY MessageNumber;
   ```

3. Choose **Run** or **Ctrl+Enter** with SQL-tool focus. This fixture returns one
   row with control ID `SYN-ADT1`. Source selection is captured at Run, not retargeted
   by later tab activation.
4. Choose **Export all results to CSV…** for a complete report. Header sorting
   changes the order across all journal rows, not only the visible page.
5. Choose **Open Matching Messages in New Document** to extract all authoritative
   identity rows, deduplicate them and preserve captured source order.

Expected result: CSV contains the complete result in presentation order; matching
HL7 opens independently, unsaved and dirty. Original files/documents remain unchanged.
Include original DocumentId/MessageId columns for extraction/navigation; computed
aliases or aggregate-only rows do not establish source identity. Explicit SQL LIMIT
intentionally restricts your result set.

## Inspect values and save a reusable query

The six relations are Documents, Messages, Segments, Fields, Components and
Subcomponents. For example, with the ADT fixture current:

```sql
SELECT DocumentId, MessageId,
       HL7_VALUE(MessageId, 'PID-3.1') AS PatientId
FROM Messages;
```

The synthetic identifier is `SYN-MRN1`. Single-document shorthand functions are not
valid substitutes for document-qualified forms in multi-document scope; see the
[function reference](../sql-hl7-workflows.md).

Select cells and **Copy selection**/Ctrl+C for complete bounded values, or select
one cell and **Inspect full value**. SQL NULL and empty text stay distinct. Table
pages contain 100 rows; ellipses are previews, not stored/exported values.

Enter a query name and **Save query**, or Ctrl+S within the SQL tool. **Open** loads
a saved definition without executing; **New**, **Rename saved** and Delete manage
it with dirty-draft/revision protection. Explicitly saved SQL/names are plaintext
and may contain sensitive literals. Session history is separate and not autosaved.

## Cancel, retry and respect budgets

**Cancel** stops the operation; failed/canceled runs publish no partial result or
output. Starting a new Run clears its preceding result. The Desktop cancels running
work on source close/change; completed captures remain stale-labeled and can still
export/extract captured bytes. Navigation requires an unchanged open source.
Closing the tool releases its results after work drains; explicitly opened outputs
remain independent. Save an unsaved query when prompted.

One read-only statement is allowed. No writes, PRAGMA/ATTACH/extensions, disk database,
external connection or UI parameter-entry panel is supported. Large structural
queries can exceed import limits even when final SQL results are small; a substring
does not bypass scalar materialization limits. Mixed encodings/unsafe joins can
reject matching-message publication rather than silently transcode or truncate it.
Use Messages/header queries for simple counts when possible.

CSV includes type companions and spreadsheet protection; it can contain clinical
values and is not raw typed interchange. See [schema](../sql-query.md),
[complete UI/reference and practical limits](../sql-query-ui.md),
[capture/output rules](../sql-hl7-workflows.md) and [troubleshooting](troubleshooting.md).
