# Find Messages and navigator filtering (Stage 6A)

Stage 6A, including the final Find Messages naming/open-focus cleanup, passed native
Windows acceptance on 2026-10-16. The user explicitly authorized the accepted milestone's
single commit and normal push to main; no tag, release, installer or distribution package.
Baseline: `0497180b3799c5075f9fbc9dd6d778b48e68d279`.

## Opening and using search

Use **View → Find Messages**, **HL7 Tools → Search → Find Messages**, or **Ctrl+F**.
All three use the same command: open/reveal the retained dockable/floating Find Messages
tool and focus/select its search-value input, including from floating tool windows.
Opening/focusing never executes a search or clears existing results/filter state. The
redundant Focus Search ribbon command is removed. The permanent menus remain File |
Settings | View. The ribbon keeps its existing collapsed/pinned overlay behavior and theme resources.

The persisted tool identity remains `tool.search-results`, preserving layouts saved with
the former Search Results title. Existing floating/hidden placement, View recovery and
Reset Layout still work; only the displayed tool/command name changes.

Choose **Text** or **HL7 Field**, enter a literal value, optionally enable **Case sensitive**,
then Search or Enter in the value input. Field mode shows a separate **Field path** input
and **Contains / Equals** selector. Cancel stops the active scan. Clear, including the
ribbon's **Clear Search / Restore Navigator**, clears results/value and restores full
navigation. Editing query controls does not automatically run a new search; displayed
rows belong to the last completed search. No query history or saved searches are stored.

Rows show original message ordinal, message type (MSH-9), control ID (MSH-10), first match
location when available, and count. Single-click selects; double-click or Enter follows
the original ordinal in the existing document. Existing raw/segment/field inspectors
update through normal document navigation, without enabling Edit Mode or creating a tab.
Control IDs are session-only and may be sensitive. No patient values or snippets appear
in rows. Headers are capped at 48/96 UTF-16 units, with an explicit ellipsis if shortened;
these display labels are not authoritative source values.

## Text semantics

- Literal ordinal case-insensitive comparison by default; Case sensitive uses ordinal
  case-sensitive comparison. No regex, culture folding, numeric conversion or normalization.
- Search each message's original decoded raw text, excluding its BOM/outer transport or
  batch regions. Untouched delimiters, whitespace, escapes, CR/LF, custom segments and
  malformed-but-readable text are preserved. HL7 escapes are not decoded into new values.
- Count non-overlapping occurrences. For example, `zz` in `zzzz` counts twice and `zzz`
  counts once. A matching message appears once, with its total occurrence count.
- Reject empty/whitespace-only values explicitly; do not interpret them as "all messages".
  Nonblank literals can include whitespace/terminators through the API/pasted value.
  Search terms are limited to 1,024 UTF-16 characters. Non-ASCII/supplementary characters
  and matches crossing internal read-window boundaries use the same ordinal semantics.

## Field/component semantics

Accepted paths are Core's uppercase `SEG-field` or `SEG-field.component`, for example
MSH-9, PID-3, PID-3.1, PID-5.1, PV1-2 and OBX-5. Invalid paths get fixed explanatory
feedback. Explicit occurrence/repetition brackets and subcomponents are outside Stage 6A;
all applicable segment occurrences and field repetitions are searched automatically.

Field-level search evaluates each individual repetition's raw value; it never joins
repetitions with a separator. Component-level search evaluates that component in every
repetition. Contains counts non-overlapping literal occurrences within those values;
Equals counts matching whole values, with the chosen ordinal case setting. Numeric-looking
values stay strings (`001` differs from `1`); stored whitespace is never trimmed.

Core's per-message delimiters and escape-aware structure are authoritative. MSH-1 is
the field separator and MSH-2 the literal encoding-character field, not repetitions
split at its own encoding characters. Missing/opaque segments, absent fields/components
or repetitions do not match. Explicit-null text is raw `""`, not a missing value.
Malformed-but-readable messages are searched where Core exposes structure. Invalid
encodings or exhausted structural budgets are skipped and visibly counted, so zero
matches with skipped messages does not claim complete coverage. Text search still finds
raw literals when only structural metadata exceeds budget.

## Revisions, unsaved edits and cancellation

Search uses the active document's original index plus immutable references to its current
Workspace drafts. It includes unsaved/malformed draft text without requiring Save, waiting
for inspection or silently discarding edits. Untouched messages are not reconstructed.
Search captures a content-only revision, distinct from viewer navigation. Following a
result or saving unchanged text does not invalidate it. Editing/undo/redo invalidates
the entire result set, cancels pending work and restores the full navigator with a
"Document changed" notice. Rerun explicitly to search the new revision.

Switching tabs cancels/clears search and filtering; returning does not reuse stale results.
Closing/reloading/replacing a document invalidates old results, even for the same document
ID. Canceled or superseded work cannot publish partial/stale results or a partial filter.
The source read lease drains before document snapshot cleanup. Search does not modify
save identity, history, original source bytes, script state or report/export behavior.

## Filter Navigator

The existing message navigator is the per-file First/Previous/number/Go/Next/Last toolbar.
Filter Navigator adds a virtualized matching-ordinal selector there and restricts those
controls to matches. Original ordinals are always shown; they are never renumbered.
The count distinguishes total messages from matching messages. An excluded current
message stays selected and is clearly marked; First/Next/Previous or the selector can
choose a match. Go to an excluded ordinal explains how to clear the filter. Zero-result
filters have no navigation targets. Disabling, clearing, editing, switching or closing
restores the full navigator. No document/message is removed or copied.

## Bounds, performance and limits

Scans and structural inspection run off the UI thread, one message at a time, with
cancellation checks through decoding, window matching and Core scanning. First-message,
throttled and final progress contain counts only. Results collect incrementally in a
bounded list and publish on successful completion. Previous/Next page exposes at most
200 rows to the virtualized results control; the matching-ordinal selector is virtualized.
No control is created for every document message.

The existing index limits documents to 100,000 classified messages. Results likewise
contain at most 100,000 rows with capped headers/coordinate/count, rather than full text
or snippets; paging bounds UI state while the complete ordinal list supports filters.
Result memory is bounded by this ceiling (worst-case headers plus row/list overhead,
roughly tens of MiB). Draft references reuse the existing 32 MiB text/history budget.
Payloads over 128 KiB reuse the disk-backed decoded source with bounded resident caches.
Large field structure remains subject to existing Core scanner budgets; skipped messages
are reported, not silently interpreted from partial structure. Disk capacity/strict
decoding remain existing source-backed inspection limitations. No database or new package.

Known limits: no field subcomponents/explicit indexed paths, regex/Boolean queries,
saved searches/history, bulk transformations, replace, search-result scripting or
incremental per-message edit reindexing. These belong to future Stage 6B/6C planning,
not implemented behavior. Only supported Workspace edit/undo/redo workflows signal
content revisions; callers must not mutate a document's materialized Core object behind
the Workspace model and expect UI notifications.

See [ADR 0008](decisions/0008-read-only-message-search.md) and the
[native acceptance and verification checklist](stage6a-search-acceptance.md).
