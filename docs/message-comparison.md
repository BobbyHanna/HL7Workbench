# Message Comparison

Open **HL7 Tools → Message Comparison**. No document needs to be open to compare
pasted HL7. The workspace starts at document size and supports existing docking,
floating and layout recovery.

## Paste two messages

New sessions open in **Side-by-Side**, with both empty editable panes visible even
when no documents are open. Paste each message directly into its A/B pane. You can
also use **Paste HL7 into A/B** in Differences, or focus an A/B source control and
press **Ctrl+V** to paste/replace that side. Clipboard access happens only after
your explicit action. Results appear after a 250 ms pause.

Click an A/B source control to edit its temporary **Pasted Text**, paste/replace,
or clear it. Ctrl+V and Shift+Insert in the temporary editor insert exact clipboard
characters at the current selection; CR, LF and CRLF are preserved. Typed edits
also update comparison after a short pause. In **Side-by-Side**, pasted source
panes are editable directly. Background updates retain keyboard focus and the caret.

Each side owns a separate draft. Switching source types, presentation modes,
themes, docking or floating keeps both drafts during the same session. Selecting
another source does not overwrite a pasted draft. Close/reopen or Reset Layout
starts a new comparison session in Side-by-Side and releases pasted text/results.
Docking, floating, resizing and hiding/revealing the existing session keep its selected
view. The checked view toggle shows which presentation is active. Only placement metadata
survives restart; comparison content and the selected view are not stored in preferences.

Pasted text is temporary: it creates no Workbench document, is not saved, and never
changes an open document or its undo/redo history. Use one message per side.
Multiple MSH messages produce an explicit single-message explanation, with the full
pasted draft retained. Empty input has instructions; incomplete/malformed nonempty
input is retained and compared with parser diagnostics. No text is silently trimmed,
normalized or repaired.

## Choose document sources

Each A/B control offers:

- **Open Message**: choose an open document and original message ordinal. Unsaved
  edits are captured even while ordinary inspection is refreshing; the label identifies
  an Unsaved draft when applicable.
- **Original Message**: choose an open document and original ordinal to capture its
  initially loaded content. Save/Save As does not redefine this original.
- **Pasted Text**: return to that side's preserved temporary draft.

Use active message in the source control chooses the active document's actual original
ordinal. Find Messages filters never turn visible row positions into source ordinals.
You may compare two ordinals in one document, different documents, or pasted text
against a current/original document message. Selecting sources never navigates a file.

**Compare With Original** on the ribbon/raw-editor context menu immediately selects
the active message's original (A) and current draft (B). Existing pasted drafts remain
available by switching either side back to Pasted Text.

Document/original source panes remain **read-only**. Editing them is never enabled by
paste support. Edits/undo/redo in a selected source document clear stale results with a
message; use Compare / Refresh again. Closing a selected document clears that selection.
Closing an inactive document choice preserves pasted drafts and unrelated pasted results.

## Review differences

Choose **Differences** to review grouped changes. Related rows have segment/occurrence headings, followed
by type, explicit HL7 paths, structural description, and wrapped before/after values.
Added/Removed/Modified/DelimiterDefinition/Terminator/SourceSpelling are text labels.
Missing and empty values remain distinct. SourceSpelling identifies supported equivalent
decoded text with different source spellings. Semantic field names are not invented:
the existing parser has no version/profile description catalog.

Select a row, use Previous/Next or F7/Shift+F7. Navigation wraps and preserves explicit
occurrences, for example OBX[3]-5 → OBX[4]-5 after insertion. Selected difference details
expand to a bounded, scrollable 4,096-character preview per value. Large row previews
are capped at 256 characters, have a visible Preview label and cannot overwhelm the list.

**Side-by-Side** uses the content area for the two syntax-highlighted sources, with
independent scrolling and a resizable divider. Keyboard-focus the divider and use arrows
to resize. The selected difference is located/highlighted on both sides. An absent side
has no invented value span; nearby same-name context is shown where available.

Both modes share the same comparison, selection and loaded panes. Switching modes
does not parse, recapture or read messages again. Light/Dark changes redraw existing
content and preserve source choices, pasted drafts and selection.

## Compact menus and copy

**Options** contains the four opt-in ignores, Compare active with original, and Cancel.
All ignores default off. Ignore whole MSH-7, whole MSH-10, trailing present-empty fields,
or segment terminator spelling. Changes recompute and exclude ignored changes from the
active count. An unobtrusive ignore options enabled indicator appears beside the count.
Interior empty values, explicit nulls and unrelated fields remain significant.

Use **Copy** or the difference-row context menu for HL7 Path, Before Value, After Value
and the selected Difference Summary. These work directly in Differences. Complete values
come from authoritative source ranges, never row/detail previews. Missing copies empty
text; summaries explicitly mark missing. Values/summaries over 1 Mi UTF-16 characters are
refused with feedback. Nothing is copied automatically.

Compare / Refresh runs explicitly when needed; pasted typing is debounced automatically.
Cancel clears superseded/in-flight work. Results matching under the options say
**No differences found.** No stale completion replaces a newer comparison.

## Limits and privacy

Pasted sources use existing complete-editor limits: **1 Mi UTF-16 characters per side,
16 Ki per line**. Oversized pastes/edits are rejected atomically and retain the previous
draft; nothing is silently truncated. Pasted text is complete in its editable pane.
Document-backed large sources use labeled **64 Ki-character windows**, with Earlier/Later
source controls and overlap. Window boundaries do not split surrogate pairs.

Large embedded-payload comparison remains limited: a user-reported approximately
100 MiB single message containing a Base64 PDF was correctly rejected by the paste
limit during native acceptance. Streaming, hashing and ED-aware comparison are potential
future work; this UI correction introduces none of them or any higher memory limits.

Core's existing scanner budgets and **20,000-difference cap** remain unchanged.
Alignment uses deterministic 64-segment / 32-repetition lookahead, with Set IDs treated
as ordinary values. Distant reorders or long runs of modified duplicates can generate
extra changes. Escape equivalence is limited to 64 Ki-character leaves; larger leaves
compare raw. Insertions next to changed duplicates without an unchanged anchor may
also align as additional modifications. Encoding/BOM-only byte differences are outside
this decoded structural diff.
Diagnostics mark recovered coordinates as provisional. Attachments are not decoded.

No pasted text, comparison results/history, document identities or values enter layout,
settings, logs, telemetry or cloud services. Only placement metadata persists.
There is no merge/apply, automatic save, arbitrary external-file picker or batch compare.

Settings now contains **Preferences** only. Preferences → General still exposes the
persisted Open documents in Edit Mode by default setting; existing documents keep their
own mode/history. The separate C# Script Editor VS Dark palette remains deferred.

See [Stage 7 acceptance](stage7-message-comparison-acceptance.md) for direct-paste synthetic
examples, exact verification commands and the user-confirmed native Windows acceptance checklist.
