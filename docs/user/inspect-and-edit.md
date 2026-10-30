# Inspect, edit and preserve HL7

Use **File → Open File…**, then **View → HL7 Inspection → Segment Navigator /
Field Inspector**. Prerequisite: an open readable message. Small complete messages
can be edited; large source-backed messages remain inspectable without full editor materialization.

## Understand the four kinds of content

| Content | Meaning |
| --- | --- |
| Original source | Immutable snapshot/index of the opened bytes, encoding/BOM and framing. Save does not redefine it. |
| Working draft | Current bounded edits and inserted logical messages, with undo/redo and dirty state. May be temporarily malformed. |
| Saved output | The file published by Save/Save As from unchanged original ranges plus validated draft overlays. Reopen to establish a new original snapshot. |
| Captured output | A tool's retained input revision or independent result document. Later source edits do not silently rewrite that capture; freshness/navigation rules depend on the tool. |

## Inspect and change a value

1. Open [adt-a01.hl7](../../samples/synthetic-only/adt-a01.hl7). Select PID in
   Segment Navigator and inspect field 3's repetitions or field 5's components.
2. Hover raw text for coordinates; double-click a value to reveal the deepest
   displayed structural node. **Copy Path** in Field Inspector copies a search-compatible
   path without a value or occurrence/repetition restriction.
3. Enable **Edit Mode** and edit raw text. The inspector is read-only. Keep
   separators intact unless deliberately testing malformed drafts.
4. Use document **Undo / Redo** to review changes. During reparsing, previous
   inspection stays labeled; stale structural actions are unavailable until refresh.
5. Choose **File → Save** or **Save As…**. Review the destination/overwrite prompt.
   Reopen saved output if you need original-source inspection of the edited message.

Expected result: supported edits change only intended spans; untouched bytes,
terminators, BOM and encoding remain preserved. Viewing malformed data never silently
normalizes it. Missing, empty and HL7 explicit-null (`""`) values remain distinct.

## Insert another message

1. Use **Home → Messages → Add Message**, **File → Add Message**, or the raw
   navigator's Add Message action; select Before Current, After Current, Beginning or End.
2. Complete the blank draft with one safely bounded HL7 message, or use
   [Generate Messages](generate-synthetic-hl7.md) to insert a validated batch.
3. Save when all inserted drafts have valid boundaries, or Undo the insertion.

Unfiltered Next at EOF appends/reuses a trailing blank draft. Filtered Next only
navigates matches. Explicit insertion invalidates search/filter results. One history
operation undoes a batch; Edit Mode off still retains history.

## Full Source Viewer and recovery

Open **View → HL7 Inspection → Full Source Viewer** for bounded contiguous windows
of an unchanged original message and deliberate original-byte export. An omission
marker in an editor preview is never source text. Attachments are not decoded/rendered.
Changing to an unrelated tab does not retarget this explicit viewer; navigating
messages in its owning file ends the session. Reopen it for the desired message.
Edited/inserted messages cannot supply original ranges; reopen their saved output.

Editing requires at most 1 Mi UTF-16 units/message and 16 Ki/line; history is bounded.
Unsupported edit encodings, ambiguous boundaries, framing restructuring and characters
unrepresentable in the source encoding can reject a save. Draft/history and the
destination remain intact on failure/cancellation. Complete/undo an incomplete draft
or save a supported output separately; do not repair source bytes from previews.

See [parser/encoding guarantees](../parser-api.md), [Core path semantics](../hl7-paths.md),
[editing decision](../decisions/0004-editable-document-foundation.md),
[save safety](../save-and-export-safety.md) and [limits](troubleshooting.md#limits-and-coverage).
