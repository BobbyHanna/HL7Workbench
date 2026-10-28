# Use SQL Query

Stage 15C adds SQL Query as an optional dockable tool, now under **View → Data & Query → SQL Query**. The normal default
layout is unchanged. You can dock or float the tool and resize its editor/results
split. A saved layout can reopen the tool, but always with a fresh draft and no
restored results. All execution stays local; no database server is required.

## Run against current or selected documents

1. Open synthetic HL7 data, then choose View → Data & Query → SQL Query.
2. Use Current document, or choose Selected open documents and check the sources.
3. Enter one read-only query and press **Ctrl+Enter** or **Run**. Ctrl+S explicitly
   saves the named SQL query when focus is in this tool; it does not save the HL7.
4. Watch capture/index/execute progress. **Cancel** stops the active operation. No
   partial result is published on failure or cancellation.

The current document is pinned when Run starts. Switching tabs afterward does not
retarget a running query or its completed results. Closing/editing a participating
source during execution cancels the query. Completed results retain captured bytes
after source edits/closure and are labeled stale. Their captured document numbers
remain visible; changing SQL marks results as belonging to the earlier completed
query. Run again to replace them. Starting a new run clears the previous result so
a failed query cannot appear to have succeeded with old rows.

Status includes returned row count, SQL/preparation/transfer duration and total
capture/index/query duration. Error messages are fixed categories; they do not echo
SQL literals, native errors, source values or filenames.

## Queries to try

```sql
-- ADT A01 admissions, with authoritative identities for output/navigation.
SELECT DocumentId, MessageId, MessageNumber, MessageControlId
FROM Messages
WHERE MessageType = 'ADT' AND TriggerEvent = 'A01'
ORDER BY MessageNumber;

-- Current-document convenience paths (only available in single-document scope).
SELECT DocumentId, MessageId, MessageNumber,
       HL7_VALUE(MessageId, 'PID-3.1') AS PatientId,
       HL7_VALUE(MessageId, 'PID-5.1') AS FamilyName,
       HL7_VALUE(MessageId, 'PV1-2') AS PatientClass
FROM Messages;

-- A synthetic patient literal. Explicitly saving this SQL also saves the literal.
SELECT DocumentId, MessageId, MessageNumber
FROM Messages
WHERE HL7_VALUE(DocumentId, MessageId, 'PID-3.1') = 'SYN-0001';

SELECT TriggerEvent, count(*) AS Messages
FROM Messages GROUP BY TriggerEvent ORDER BY Messages DESC;

SELECT m.DocumentId, m.MessageId, m.MessageNumber,
       f.SegmentOccurrence, f.RepetitionNumber, f.DecodedValue
FROM Messages m JOIN Fields f
  ON f.DocumentId=m.DocumentId AND f.MessageId=m.MessageId
WHERE f.SegmentName='OBX' AND f.FieldNumber=5
  AND f.DecodedValue LIKE '%synthetic%';

SELECT m.DocumentId, m.MessageId, m.MessageNumber
FROM Messages m WHERE NOT EXISTS (SELECT 1 FROM Segments s
  WHERE s.DocumentId=m.DocumentId AND s.MessageId=m.MessageId
    AND s.SegmentName='PID');
```

The editor uses a small theme-aware lexical highlighter through AvaloniaEdit;
highlighting is not SQL validation. SQLite remains the parser/executor. The UI has
no parameter-entry panel in this pass: use synthetic literals in the editor, or
the Workspace parameter API described in [HL7 workflows](sql-hl7-workflows.md).
See [schema/functions reference](sql-query.md) for supported relations and Core paths.

## Inspect, sort, copy and navigate

Only 100 result rows reach the controls at a time; Previous/Next read the journal.
Long cells show bounded previews with an explicit ellipsis. SQL NULL appears as
`(SQL NULL)` in italics; present empty text appears as `(empty TEXT)`. Unavailable
requested values fail the query explicitly; they are not empty/null placeholders.
Use HL7_STATE/HL7_STATUS and ValueLength when investigating source conditions.
Long/multiline column labels also use bounded single-line headers with an explicit
ellipsis; original column names remain intact in SQL metadata and CSV export.

Click a cell to select it. Shift-click selects a rectangle within the page;
Ctrl-click adds cells. Click # to select that row. Copy selection or Ctrl+C in
results copies **complete bounded values**, not the displayed ellipsis, as quoted
tab-separated text. Quotes/newlines are escaped; SQL NULL has an explicit unquoted
marker and empty TEXT is `""`. Copy is limited to 1 Mi UTF-16 units and refuses
oversize selections; use CSV for complete large reports. Paging/sorting clears the
selection to avoid applying it to different rows.

Select one cell and choose **Inspect full value** to deliberately read its complete
bounded scalar. The expandable read-only inspector shows scalar kind and size.
This does not decode attachments. Native materialization limits still apply.

Click a column header to sort **all** journal rows, ascending then descending.
Sorting is stable, typed (NULL, numeric, text, blob) and uses ordinal text comparison.
Use SQL ORDER BY for SQLite collations/expressions. Only a bounded permutation and
one column's keys are retained; the full result is not loaded into controls.

**Go to source**, or double-click a data cell, deliberately navigates when exactly
one selected row resolves to an authoritative captured DocumentId/MessageId pair
in an unchanged, still-open source. Selection alone does not navigate. Computed
aliases, aggregates without identity, multiple selected rows, closed or changed
sources disable navigation. The original content/dirty state is never modified.

## Export and open messages

**Export all results to CSV…** writes every complete journal row, across all pages,
in the current presentation sort order. It preserves full bounded values, uses
UTF-8 without BOM, Core quoting/newline/formula protection and [Type] companion
columns distinguishing NULL/empty text. Existing-file replacement requires consent.
Source files/aliases are protected and publication is atomic.

**Open Matching Messages in New Document** is a separate action. It ignores visible
page and selected cells, reads **all** authoritative identity rows, deduplicates and
preserves captured source order. Include original DocumentId/MessageId columns;
otherwise the disabled action explains that a separate identity-selection query
is required. Explicit SQL LIMIT intentionally restricts the selected set. Result
and extraction budgets apply; overflow never opens a partial subset.

Output is independent, unsaved and dirty, using original byte/framing ranges where
available. Originals remain unchanged. Captured stale results still extract captured
bytes, not later source edits. Mixed encodings, wrappers/unmapped outer regions,
ambiguous framing/interior BOM, unencodable drafts and unsafe concatenation are
refused. The fixed error text explains these checks; no silent repair/transcoding.

## Named definitions and session history

Enter a query name and choose **Save query** to create/update its definition. Choose
a saved entry and **Open** to load it without executing. **New** starts a clean
default draft. To rename the currently loaded saved definition, change the name
and choose **Rename saved**; this preserves its stored SQL, leaving SQL edits dirty
until Save query. Delete asks for confirmation. Unsaved changes prompt Save,
Discard or Cancel before replacement/close. Cancel or failed saving keeps the draft.

Explicit saves store names and SQL as unencrypted GUID-named JSON under
`%LOCALAPPDATA%\HL7Workbench\SqlQueries` (or the configured application directory).
**Names and SQL literals may contain sensitive data.** The tool discloses this before
saving; there is no autosave. Revision conflicts preserve external changes and the
current draft. Unsupported/corrupt records are skipped and retained for review.
The library permits 256 definitions, 120-character names and 65,536-character SQL,
with a 512 KiB encoded record cap. No document IDs, source paths, results or history
are stored as definition metadata.

Session history holds at most 32 executed queries/256 Ki UTF-16 SQL units in memory,
including outcomes/row counts. It survives closing/reopening this tool until the
application exits. Open history loads a new draft after unsaved-change handling;
it does not execute or save it. Unsaved editor text, selection, sort, full values and
results are not restored by layout or preferences. Tool closure cancels/drains work
and releases result snapshots. Existing temporary Workspace journals/spools can
contain sensitive data and follow the [15B ownership/cleanup rules](sql-hl7-workflows.md).

## Practical limits and CSV interpretation

Metadata queries can cover large message batches without decoding unused OBX
payloads. A structural Fields query still populates all scoped field metadata
before SQL predicates run: a small final result does not guarantee a small import.
The measured 10,000-message Fields workload reaches the accounted import limit;
4,000 succeeds. For narrow lookups, use the existing Messages/HL7_VALUE examples
when appropriate. All scanning, value and deadline limits still apply.

`substr(DecodedValue,1,32)` does not bypass a large field's materialization limit.
Inspect ValueLength/HL7_STATUS first or use bounded source-inspection tools.

CSV is spreadsheet-guarded presentation. Risky leading characters receive an
apostrophe even after numeric-to-display conversion (for example `-42`), and type
companion columns retain the SQL kind. Ordinary CSV quoting is not formula
protection. Full typed journal values and exact-byte HL7 extraction retain their
separate contracts; CSV is not an unmodified raw-value interchange format.

See [Stage 15C UI verification](stage15c-sql-ui-acceptance.md) and the
[Stage 15D measurements, limits and native checklist](stage15d-sql-final-acceptance.md).
