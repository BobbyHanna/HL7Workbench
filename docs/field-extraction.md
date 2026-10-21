# Field Extraction

Open **View → Field Extraction**, or choose **Extract Fields** on an HL7 document.
The document action selects that document explicitly. The tool's source selector and
**Use active document** choose the source for the next run; changing tabs does not
redirect existing results. Choose **Current Message** or **Whole Document**, then
**Extract**. Edit Mode may be off. Extraction never edits or saves source HL7.

## Columns and paths

The immutable **Interface Overview** preset contains these columns, in order:

| Label | Path |
|---|---|
| Message Type | `MSH-9` |
| Message Control ID | `MSH-10` |
| Patient Identifier | `PID-3` |
| Patient Name | `PID-5` |
| Visit Number | `PV1-19` |
| Universal Service ID | `OBR-4` |

Choose **Duplicate** to make an editable user copy. Expand **Columns and matching
policy** to add/remove columns, change labels/paths and move columns up/down. At
least one column is required. Labels must be distinct and cannot conflict with CSV
metadata or companion headers (for example, a second label `Patient Name State`).

Paths use the existing Core grammar: `PID-3`, `PID-5.1`, `PID-5.1.1`,
`OBX[2]-5`, `PID-3[2]`, or `OBX[2]-5[3].1.2`. Numbers are one-based; brackets after
the segment select an occurrence, brackets after the field select a repetition.
Components and subcomponents use dots. Explicit `[1]` remains explicit when saved.
Custom message delimiters come from the message, not from the path spelling.

**Decode HL7 escapes** uses Core's existing text escape rules. Turn it off to retain
raw HL7 escapes. MSH-1/2 are literal delimiter fields. Unsupported/invalid escapes
remain diagnosed; extraction does not decode or render binary attachments. An opaque
or ambiguous structure is labeled `Unstructured` rather than presented as verified.

## First Match and Expand Matches

**First Match** produces one row per captured source message, choosing the first
occurrence/repetition unless the path explicitly selects another.

**Expand Matches** enumerates each column's candidates by segment occurrence and
then repetition. Omitted indexes expand; explicit indexes stay fixed, including
`[1]`. Row n aligns candidate n from each column. A column with a single candidate
is repeated on every row. A shorter multi-candidate column produces
`MissingAlignment` after its candidates are exhausted. For example, columns with
three and two candidates produce three rows, aligned `(1,1)`, `(2,2)`, `(3,missing)`.
They do not produce six combinations. This ordinal policy does not infer clinical
OBR/OBX groups or other relationships. Each cell displays/exports its actual path.

## Values, results and source identity

Cells retain the following distinctions:

| State/status | Meaning |
|---|---|
| `Value` / `Complete` | Complete selected text, raw or decoded according to the run |
| `Empty` | The selected position exists with no characters |
| `ExplicitNull` | The selected position contains HL7 `""` |
| `MissingSegment`, `MissingField`, `MissingRepetition`, `MissingComponent`, `MissingSubcomponent` | That structural position is absent |
| `MissingAlignment` | This expanded column has exhausted its candidates |
| `Unavailable` / `Unreadable` | Message decoding/scanning could not establish the value |
| `ValueLimit`, `Unstructured`, `DecodeDiagnostic` status | A limit or structural/escape diagnostic needs review; an empty value cell does not mean a verified empty HL7 value |

The virtualized table shows 100-row pages, with at most 256 characters per cell.
`… [preview]` is presentation only; CSV reads complete stored values. CR, LF and tab
characters appear as visible symbols in the table. Select a row and double-click,
press Enter or choose **Go to source**. Navigation uses its captured stable message
identity, even after insertion changes logical positions. The Identity column is an
opaque application-session identity, not an HL7 patient or control identifier.

Source document number, captured content revision and current/stale/closed status
remain visible. Source edits cancel an in-flight extraction. Completed results become
stale and remain available for review/export until rerun or tool closure. Navigation
can still resolve a surviving message in the same open source. Closing/replacing a
document cannot redirect its old rows to a new tab. Opening another tab does not
refresh results; explicitly select the source and rerun. Cancellation/failure retains
the previous completed result and never publishes a partial replacement.

## Filter, sort and summary

Expand **Filter and sort results**, select a column/operator and choose **Apply
filter/sort**. Enter in the comparison input also applies it. Contains and exact match
are literal, ordinal case-insensitive comparisons. Separate missing, empty, HL7-null
and diagnosed/unavailable operators avoid conflating states. Numeric comparisons use
invariant-culture decimal numbers (decimal point `.`, optional sign/exponent, no
thousands separators). Diagnosed values do not participate in text/numeric filters.
Comparison literals are limited to 1,024 characters and stay in the current tool
session; do not put patient values into configuration names or column labels.

Click a field header to sort ascending, then descending, or select a sort column and
optional numeric ordering. Numeric ordering puts valid numbers before other text
within the value state. Ascending state order is missing, empty, null, diagnosed,
value; descending reverses it. Ties preserve captured row order. Choose **Source
order** and **No filter**, then Apply to reset. Filters and sorting run in the
background over all stored rows, not just a page, and do not reload HL7. **Cancel**
also cancels a pending filter/sort, page read or export.

**Extraction Summary** describes the completed extraction before result filtering:
messages examined/total, messages with any selected structural position, produced
rows, unreadable/expansion-limited messages, missing/empty/null/diagnosed counts per
column and distinct complete nonempty values. Counts apply to stored rows; broadcast
values count once per row. Distinct tracking uses bounded hashes; `>1024` means the
ceiling was exceeded. Review truncation, the last operation's cancellation status and
source freshness. A canceled replacement does not relabel the previous summary.

## Saved configurations

**New**, **Duplicate**, **Rename**, **Save**, **Load** and **Delete** manage **My
Extractions** separately from the built-in. Rename focuses the name input; change the
name and Save. Configurations preserve ordered labels/canonical paths, matching mode,
escape preference, filter column/operator and sort preferences. Literal comparison
values, source selection, scope, extracted rows and patient values are not serialized.
After loading, enter any comparison literal again and Apply to existing results, or
run a fresh extraction. A fresh run starts with all rows in captured source order.

New/Load/Delete and application shutdown protect dirty drafts with Save/Discard/Cancel.
Closing/resetting the tool releases its results and comparison literal but retains its
configuration draft for reopening during the same application session. Save to retain
a user configuration across restart. Workspace layouts contain placement only; results
are not automatically restored. Corrupt definitions are skipped with a fixed status;
invalid paths/names, duplicate names and external file changes cannot overwrite a
newer saved definition. The application-owned preset cannot be overwritten/deleted.

## CSV export

**Export selected rows to CSV…** exports every row in the applied filtered/sorted
selection, across all pages. Row highlighting is for navigation and does not limit
export. Choose a destination and confirm replacement in the normal file picker.
Publication is atomic: cancellation, quota failure or destination conflict leaves
no partial new file and preserves any previous destination. Source-file aliases,
including Windows hard links, and owned snapshot/result storage are refused.

CSV uses UTF-8 without BOM, CRLF record endings, quoted fields and doubled quotes.
Embedded commas, quotes and newlines are preserved. The existing spreadsheet formula
guard prefixes an apostrophe for dangerous initial `=`, `+`, `-`, `@`, tab/CR/LF
patterns, including before numeric-looking negatives. This intentionally changes
those exported cell strings for spreadsheet safety; source and stored values remain
unchanged. Consumers needing original literal text must account for that policy.

The first five columns are Source Document (session number), Captured Revision,
Captured Message (position at capture), Message Identity (session identity) and Match
(expansion ordinal). Each configured column then exports its value, ` State`,
` Status` and ` Path` companions. Empty/null/missing values have empty value cells
with distinct companion states; unavailable/diagnosed values carry explicit status.
Headers retain configured order. Exported captured revision remains authoritative
even if the live source is now stale/closed. CSV may contain sensitive source values;
it is an explicit user export, not a de-identification operation.

## Bounds and lifecycle

| Resource | Desktop ceiling |
|---|---:|
| Extraction columns | 32 |
| Expanded rows per message | 4,096 |
| Stored result rows | 1,000,000 |
| Private result journal | 1 GiB |
| Selected value | 65,536 UTF-16 code units |
| Encoded journal row | 16 MiB |
| Sort-key cache | 4 MiB |
| Distinct hashes per column | 1,024 |
| CSV output | 4 GiB |
| User definitions / definition size | 256 / 128 KiB |

Row/journal limits publish an explicitly truncated result and summary. An oversized
value is unavailable, never a plausible truncated value. CSV exports the stored
selection; it cannot restore values/rows excluded by these limits. Sorting a large
result with uncached keys can be slow; cancellation stays available. Row offsets,
message identity metadata and filtered/sorted integer indexes scale with the bounded
row count; full values remain in a private disk journal. Large source messages use
the existing decoded spool and shared scanner. Memory is bounded, not constant per
document, and existing scanner/edit limits still apply.

The tool owns temporary results and releases them after pending reads/exports drain.
Temporary storage follows the existing local snapshot lifecycle, without an encryption
or secure-erasure guarantee. Values and comparison literals are never added to
application logs, diagnostics, layouts or saved definitions. These controls do not
classify content as PHI-free. Native acceptance is recorded separately in
[Stage 10 acceptance](stage10-extraction-analysis-acceptance.md).
