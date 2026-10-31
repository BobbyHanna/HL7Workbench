# Extract fields into a table and CSV

Use **View → Data & Query → Field Extraction**, or **Extract Fields** in an HL7
document. Prerequisite: open [adt-a01.hl7](../../samples/synthetic-only/adt-a01.hl7)
or [oru-r01.hl7](../../samples/synthetic-only/oru-r01.hl7). Extraction reads captured
values without modifying the source.

## Create and export a result

1. Choose the source, or **Use active document**, and select **Current Message**
   or **Whole Document**. The document action explicitly selects its document.
2. Load the built-in **Interface Overview** if needed. It includes message type,
   control ID, patient identifier/name, visit number and universal service ID.
3. Choose **Extract**. Inspect the result and **Extraction Summary**, including
   missing/empty/null/diagnosed values and source freshness.
4. Expand **Filter and sort results**, select a column/operator and optional sort,
   then **Apply filter/sort**. A header click also sorts all stored rows.
5. Choose **Export selected rows to CSV…**, select a new destination and review
   any replacement confirmation.

Expected result: a separate CSV of the entire applied filtered/sorted result
selection, across all pages. Highlighting a table row is for navigation and does
not limit the export. The HL7 file, drafts and history stay unchanged.

## Customize columns or expand repeats

1. **Duplicate** the immutable preset; name the editable configuration.
2. In **Columns and matching policy**, add/reorder paths such as `PID-3.1`,
   `OBX-5` or `OBX[2]-5[1].1`; explicit indexes stay fixed.
3. Choose **First Match** for one row per message, or **Expand Matches (ordinal
   alignment)** to enumerate omitted occurrence/repetition indexes. Extract again.
4. **Save** the configuration for reuse; select and **Load** it later. New,
   Duplicate, Rename and Delete manage user definitions separately from the built-in.

Expanded matches align candidate n across columns, broadcast single candidates
and label exhausted columns MissingAlignment. They do not infer OBR/OBX clinical
groups or create Cartesian combinations. Decode HL7 escapes is an explicit choice;
attachments are never decoded/rendered by that option.

## Cancellation, freshness and limits

Field Inspector's **Extract Field to Table…** prepares a new draft in this same
tool after the existing unsaved-configuration prompt. It prepopulates the selected
occurrence-specific path, turns escape decoding off and selects Expand Matches.
Explicit repetition/component indexes remain fixed; a whole field expands its
repetitions. Review Current Message/Whole Document and choose Extract explicitly.
Changing selection or source while the prompt is open cancels preparation. It does
not run a second extraction engine or modify HL7.

**Cancel** also covers filtering, sorting, paging and exporting. Failed/canceled
replacement retains the previous completed result; source edits cancel scanning
and make completed results stale. Changing tabs does not redirect an existing
capture. **Go to source** navigates a surviving stable identity; closed/replaced
sources cannot retarget old rows.

The table shows 100-row pages and abbreviated cells; CSV reads complete stored
values, subject to explicit extraction/value limits. CSV can contain sensitive
data and applies spreadsheet formula protection; it is not exact original HL7.
Saved configurations omit comparison literals/source/results; closing/resetting
releases results and literals while retaining the configuration draft in-session.

See [detailed extraction states, alignment and bounds](../field-extraction.md),
[save/export safety](../save-and-export-safety.md) and [privacy/storage](privacy-and-storage.md).

## Unique-value and mutation workflows

Use Field Inspector's List Unique Values for bounded raw distinct grouping and
occurrence/message counts. Continue using this extraction tool for a configurable
CSV projection and its existing safe export workflow; the unique-value dialog has
no separate CSV writer. Whole-field bulk actions compare the complete field,
whereas extraction can expand its repetitions. Search & Replace / Assign Value
produce an independent complete HL7 dataset, not extraction rows. Select that
result document as a source here if a subsequent CSV is needed.
See [bulk scopes and preservation](inspect-and-edit.md#analyze-unique-values-and-create-a-bulk-result).
