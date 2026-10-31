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
| List Unique Values… | Read-only distinct raw values, occurrence/node/message counts, sorting, paging and navigation filtering. |
| Search & Replace… | Exact raw equality at the selected structural level, followed by preview and confirmation into a new full dataset. |
| Assign Value… | Assign existing nodes in the chosen scope; absent nodes stay absent and identical values are unchanged. |
| Edit Value… | Stages raw encoded text for one existing field, repetition, component or subcomponent. Requires Edit Mode; Apply creates one document undo step. See the editing steps below. |
| Copy Value | Complete raw encoded value within 128 Ki UTF-16 units, without decoding escapes. Oversized values are unavailable; use original-source viewing/export. A preview is never copied as a full value. |
| Copy HL7 Path | Exact location, for example `PID[2]-5[1].2`, including occurrence and repetition where applicable. |
| Copy Path | Reusable search path, for example `PID-5.2`; searches examine all occurrences/repetitions. |
| Find Occurrences… | Runs structural path presence in the entire active file, including unsaved edits. This is not literal-value search. |
| Filter by This Value… | Runs case-sensitive raw equality at the selected structural path and filters navigation using all matching messages. Select one repetition of a repeated whole field first. Values must fit the 1,024-character search bound; represented empty values are supported. |
| Extract Field to Table… | Prepares the existing extraction tool with an occurrence-specific path and raw decoding off. Review scope, then run explicitly. Whole fields expand their repetitions into rows; explicit repetition/component indexes stay fixed. |

Actions reject edited, replaced, closed or changed selections. Search/filter uses
all occurrences and repetitions in one active file; extraction keeps exact indexes.
The bulk actions below use an explicitly reviewed scope and exact selected indexes. See [search/filter](search-and-filter.md) and
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

## Analyze unique values and create a bulk result

Use synthetic multi-message input while learning these workflows. Bulk actions work
with Edit Mode off because they never edit the source document. They do not enable
Edit Mode or execute scripts, SQL writes or network operations.

1. Select `PV1-2` in Field Inspector and right-click **List Unique Values…**.
2. Choose **Current message**, **Active file**, or **Current navigator filter
   (all matches)**. Filter scope requires an applied, current navigator filter;
   it includes every retained matching ID across all Find Messages pages.
3. Review the exact selected path. By default only that segment occurrence is
   analyzed in each message. **Every segment occurrence** explicitly expands
   segment occurrences; it does not expand repetition indexes. Choose Analyze.
4. Review scanned messages, messages containing the path, selected address slots,
   present nodes, absent slots and skipped coverage. Each value shows occurrences,
   actual present-node count and distinct-message count. An absent slot has zero
   present nodes; a message with two matching segment occurrences counts twice
   under occurrences but once under messages.
5. Sort by value or frequency; click again to reverse direction. Pages contain
   100 rows. Long cells are previews; expand **Inspect complete selected values**
   for the complete bounded raw value. Grouping uses complete ordinal values,
   never abbreviations. Empty, absent and raw explicit null (`""`) stay distinct.
6. Select a value and choose **Go to first matching message** or **Filter Navigator
   by selected value**. Filtering uses the complete captured identity set, not
   the visible page or a literal substring search. Clear filtering through the
   existing Find Messages/ribbon workflow.

For **Search & Replace…**, edit **Find value** and **Replace with** above Analyze /
Preview. Find value starts with the selected node's complete raw encoded value when
available within its 65,536-character limit; it is editable and never an implicit
search term. Replace with starts empty. For **Assign Value…**, edit **New value**.
An intentionally empty replacement clears an existing value; it is not an omitted
parameter. Choose Analyze / Preview after reviewing these visible inputs.
The review shows the source, path/scope, captured revision, scanned/affected message
counts, proposals, absent/skipped/unchanged/invalid counts, and paged details with
message occurrence, stable identity, exact path and original/replacement values.
Inspect complete values before confirming. These are encoded HL7 values: `\S\`
is not decoded/re-encoded, and substring, regex, numeric and case-insensitive
matching are not supported. An empty Find value matches represented empty nodes,
not absent ones.

A whole-field row means the complete raw field, including its repetition separators.
A repetition row targets only its index; components and subcomponents retain their
selected indexes. This differs intentionally from the older unindexed Find actions
and from extraction's repetition expansion. Custom MSH separators drive validation.
Allowed lower-level separators follow the single-value editing table above.

Confirm **I reviewed the preview…**, then choose **Apply all to new document**.
There is no per-row exclusion control: this stage applies all validated proposals
or publishes nothing. Any invalid proposal blocks Apply. Missing nodes are listed
and skipped, and identical values are unchanged. A no-op preview cannot publish an
unnecessary output. MSH-1/MSH-2, unsafe delimiters, unclosed escapes, controls,
unrepresentable encoding and ambiguous or oversized edit targets are rejected.

Apply opens and activates an independent, unsaved **Bulk Result.hl7** tab. It selects
the first changed message and reviewed path, so the raw viewer and Field Inspector
show the same result. The result includes the complete captured dataset in its
original order. Scope selects which messages may change; it never drops
out-of-scope messages. Wrappers, BOM/encoding and untouched bytes remain preserved.
The original document's text, dirty state and undo history are unchanged. Switch
back to its tab to inspect the original values in both views. Inspect and explicitly
Save As the result, then reopen it if desired. If the result cannot be opened, a clear
error reports that no result was retained; the original remains active. Review and
confirm again before retrying. Closing/canceling the
review publishes no partial output. A result document remains independent after
closing the review/source.

Changing parameters discards the preceding preview and confirmation. Source edits,
undo/redo, insertion or closure invalidate analysis; changing a captured navigator
filter also invalidates filtered scope. A tab switch alone does not retarget the
immutable capture. Analyze again after any invalidation. Cancellation and resource
failures never produce a partial distinct list or partially published dataset.

The complete dataset is privately captured even for read-only/current-message
analysis, so allow temporary disk space and capture time. Limits are 1 GiB each for
captured source/output, 100,000 scoped messages, 250,000 selected address slots and
preview rows, 4,096 distinct values, 65,536 characters per complete analyzed value,
and 2 Mi retained value characters. Mutation additionally uses the existing 1 Mi
complete-message/16 Ki line limits and 16,777,216 reserved overlay characters
(32 MiB character storage, with metadata/encoded buffers additional). A large file
can stream through with small selected messages; an oversized selected message
cannot become an in-memory edit. A ceiling fails explicitly or marks invalid
proposals; it never groups truncated values or silently broadens editing limits.
