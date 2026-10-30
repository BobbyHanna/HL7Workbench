# First task: inspect, edit and save a synthetic message

Prerequisites: a running application and the reviewed
[adt-a01.hl7](../../samples/synthetic-only/adt-a01.hl7) fixture in your repository
checkout. It is entirely invented data. Keep this fixture unchanged by saving to
a different destination.

1. Choose **File → Open File…** and select the fixture. Its HL7 appears in a document tab.
2. Open **View → HL7 Inspection → Segment Navigator** and **Field Inspector** if
   hidden. Select PID, then inspect field 5. The sample's patient name is
   `Example^Avery`; PID-3 contains repeated synthetic identifiers.
3. Hover a raw value for its structural path, or double-click it to reveal its
   structure. Inspectors describe existing syntax; they do not edit values directly.
4. Enable **Edit Mode** in the document. In the raw HL7, change `Example^Avery`
   to `Example^Rowan`, leaving delimiters and the remaining fields intact.
5. Wait for inspection to refresh. A pending/previous-inspection label is temporary;
   the document's modified indicator remains until saved. Try **Undo**, then **Redo**.
6. Choose **File → Save As…** and a new filename outside the fixture folder.
   Review any replacement confirmation; use a new file for this walkthrough.
7. Open the saved file separately to inspect its persisted output.

Expected result: the original fixture stays unchanged, the new file contains the
chosen edit, and untouched bytes/formatting are preserved by the save workflow.
The original open document still distinguishes its initial source from its working
and saved state. [Inspect and edit](inspect-and-edit.md#understand-the-four-kinds-of-content)
explains that distinction.

## Recover or cancel

While opening/indexing, **Cancel opening** stops publication. Closing a dirty document
offers **Save / Discard / Cancel**; Cancel retains work. A canceled or rejected save
does not publish a partial replacement and keeps the draft/history. Turning Edit Mode
off does not discard edits. If editing is disabled, check the status and
[editing limits](troubleshooting.md#limits-and-coverage), not just the checkbox.

## Continue with another task

- [Find the synthetic identifier](search-and-filter.md).
- [Compare the draft with its original](compare-messages.md).
- [Extract a table](extract-fields.md) or [query it with SQL](query-with-sql.md).
- [Generate a fresh synthetic batch](generate-synthetic-hl7.md).

See [parser preservation](../parser-api.md) and [save/export safety](../save-and-export-safety.md)
for exact limits. These sample messages exercise syntax/preservation and do not
promise profile conformance or clinical validity.
