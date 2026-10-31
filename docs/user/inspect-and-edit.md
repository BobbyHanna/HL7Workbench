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
   displayed structural node. **Copy HL7 Path** copies its exact occurrence/repetition
   location; **Copy Path** copies the reusable, unindexed search path.
3. Enable **Edit Mode** to edit raw text or use Field Inspector's **Edit Value…**
   for one existing structural value. Keep separators intact unless deliberately
   testing malformed drafts in the raw editor.
4. Use document **Undo / Redo** to review changes. During reparsing, previous
   inspection stays labeled; stale structural actions are unavailable until refresh.
5. Choose **File → Save** or **Save As…**. Review the destination/overwrite prompt.
   Reopen saved output if you need original-source inspection of the edited message.

Expected result: supported edits change only intended spans; untouched bytes,
terminators, BOM and encoding remain preserved. Viewing malformed data never silently
normalizes it. Missing, empty and HL7 explicit-null (`""`) values remain distinct.

## Navigate the hierarchical Field Inspector

Select a segment in Segment Navigator or raw text. Field Inspector follows that
exact occurrence, such as the second PID, and lists its fields with structural
paths, descriptions and raw encoded previews. Expand a field, repetition, component
or subcomponent using its arrow or the keyboard. The selected field expands by
default; other branches stay compact. Selecting a row reveals its source range
where one exists. Inspection works with Edit Mode off; **Edit Value…** requires
the owning document's Edit Mode and a safely editable existing node.

**Show Blank Fields** includes represented empty fields and fields declared by the
explicitly applied schema. An **Absent** row has no source span; it does not imply
that trailing separators exist. Empty rows have a real zero-length range; explicit
HL7 null remains distinct. With the toggle off, empty rows are hidden except when
needed for the current structural selection. No unlimited trailing fields appear.

Current schema metadata supplies datatypes/constraints rather than friendly field
names. Descriptions therefore use labels such as Field 5 and its known datatype;
custom/unknown definitions fall back to structural labels. Applying a profile is
explicit and separate from inspection. Long values remain bounded previews;
attachments are never decoded automatically.

Right-click the actual row for these actions:

| Action | Result and scope |
| --- | --- |
| Edit Value… | Stages raw encoded text for one existing field, repetition, component or subcomponent. Requires Edit Mode; Apply creates one document undo step. See the editing steps below. |
| Copy Value | Complete raw encoded value within 128 Ki UTF-16 units, without decoding escapes. Oversized values are unavailable; use original-source viewing/export. A preview is never copied as a full value. |
| Copy HL7 Path | Exact location, for example `PID[2]-5[1].2`, including occurrence and repetition where applicable. |
| Copy Path | Reusable search path, for example `PID-5.2`; searches examine all occurrences/repetitions. |
| Find Occurrences… | Runs structural path presence in the entire active file, including unsaved edits. This is not literal-value search. |
| Filter by This Value… | Runs case-sensitive raw equality at the selected structural path and filters navigation using all matching messages. Select one repetition of a repeated whole field first. Values must fit the 1,024-character search bound; represented empty values are supported. |
| Extract Field to Table… | Prepares the existing extraction tool with an occurrence-specific path and raw decoding off. Review scope, then run explicitly. Whole fields expand their repetitions into rows; explicit repetition/component indexes stay fixed. |

Actions reject edited, replaced, closed or changed selections. Search/filter uses
all occurrences and repetitions in one active file; extraction keeps exact indexes.
No unique-value command or bulk mutation is added. See [search/filter](search-and-filter.md) and
[extraction](extract-fields.md) for capture, cancellation and output rules.

## Edit one existing value through the inspector

1. Enable the active document's **Edit Mode** in the raw viewer. The inspector does
   not turn it on automatically. Select PID, expand field 5 and select component 2.
2. Right-click that row and choose **Edit Value…**. Verify its exact path, including
   segment occurrence and repetition, and the read-only current raw encoded value.
3. Enter the replacement, for example change `Avery` to `Rowan`. Input is raw HL7:
   an existing escape such as `\S\` stays encoded; nothing is silently decoded or
   re-encoded. Current and replacement values can scroll horizontally.
4. Choose **Apply**. The selected span changes once, inspection briefly shows its
   pending/previous-parse state, then the edited node is selected and highlighted.
   **Cancel** discards the staged value without changing the document or undo history.
5. Use the normal document **Undo / Redo** and **Save / Save As** workflows. Undo
   restores the previous content exactly; Save is still an explicit publication.

Allowed structural separators depend on the selected level:

| Selected node | Raw separators allowed within its replacement |
| --- | --- |
| Entire field | Repetition, component and subcomponent separators. The replacement explicitly covers the whole field. |
| Repetition | Component and subcomponent separators; other repetitions stay unchanged. |
| Component | Subcomponent separator; sibling components stay unchanged. |
| Subcomponent | No unescaped structural separators. |

The active MSH encoding characters define these separators, including custom sets.
Use a legitimate closed HL7 escape for a literal separator above the selected level.
Segment terminators, tabs, other control/line-separator characters, unclosed escapes,
unrepresentable characters and over-limit replacements are rejected before any edit.
Invalid input leaves the dialog open with feedback and creates no undo entry.
An unchanged value has no edit to Apply. Clinical/profile validity is not required.

An explicitly empty node can be edited when it has a real zero-length span. An
**Absent** schema row cannot create delimiters or missing structure. **MSH-1/MSH-2**,
ambiguous structure and source-backed/oversized messages remain unavailable for this
workflow, with an explanation in the inspector. Other editable MSH fields follow
ordinary raw rules; changing MSH-18 does not transcode the file's actual encoding.

Switching documents/messages, changing selection, turning Edit Mode off, editing
raw text or closing the source invalidates a staged target. Returning to the old
document does not revive it. Cancel and reopen for the intended current node.
The existing complete-message, line, encoding and history bounds still apply.
Unrelated source bytes, delimiters, escapes, empty positions and terminators remain
preserved; malformed content is never silently repaired.

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
