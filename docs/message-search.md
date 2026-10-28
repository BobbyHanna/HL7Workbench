# Find Messages and navigator filtering (Stages 6A–6B)

Stage 8B.4 extends this pipeline to inserted logical messages and stable identities;
see [compatibility acceptance](stage8b4-compatibility-acceptance.md). Earlier milestone
Git authorizations below are historical; current 8B work remains intentionally uncommitted.

Stage 6A, including the final Find Messages naming/open-focus cleanup, passed native
Windows acceptance on 2026-10-16. The user explicitly authorized the accepted milestone's
single commit and normal push to main; no tag, release, installer or distribution package.
Stage 6B passed native Windows acceptance from `dbcd6284eabb01220046109a1f01f2e94626edcd`.
The user accepted the milestone and authorized one commit and normal push to origin/main.
No tag, release, installer, distribution package or additional implementation is authorized.

## Opening and using search

Use **View → HL7 Inspection → Find Messages**, **HL7 Tools → Search → Find Messages**, or **Ctrl+F**.
All three use the same command: open/reveal the retained dockable/floating Find Messages
tool and focus/select its search-value input, including from floating tool windows.
Opening/focusing never executes a search or clears existing results/filter state. The
redundant Focus Search ribbon command is removed. The permanent menus remain File |
Settings | View. The ribbon keeps its existing collapsed/pinned overlay behavior and theme resources.

The persisted tool identity remains `tool.search-results`, preserving layouts saved with
the former Search Results title. Existing floating/hidden placement, View recovery and
Reset Layout still work; only the displayed tool/command name changes.

In **Simple**, choose **Text** or **HL7 Field**, enter a literal value, optionally enable **Case sensitive**,
then Search or Enter in the value input. Field mode shows a separate **Field path** input
and **Contains / Equals** selector. Cancel stops the active scan. Clear, including the
ribbon's **Clear Search / Restore Navigator**, clears results and restores full navigation
while retaining the authored query and saved definitions. This Stage 6B change prevents
Clear from unexpectedly discarding a reusable definition. Query-control edits (including
Simple inputs, conditions, ALL/ANY or Simple/Advanced switching) cancel/clear old results
and filters immediately; rerun explicitly. Opening/focusing, switching modes, selecting
a saved item or loading it never runs a search. There is no automatic query history.

Rows show captured logical message position, message type (MSH-9), control ID (MSH-10), first match
location when available, and count. Single-click selects; double-click or Enter follows
the retained MessageId in the existing document. Original ordinal is separate nullable
provenance. Stale/unavailable results never select a neighbor at the old position.
Existing raw/segment/field inspectors
update through normal document navigation, without enabling Edit Mode or creating a tab.
Control IDs are session-only and may be sensitive. No patient values or snippets appear
in rows. Headers are capped at 48/96 UTF-16 units, with an explicit ellipsis if shortened;
these display labels are not authoritative source values.

## Advanced visual queries (Stage 6B)

Choose **Advanced** to edit a Boolean tree with 1–16 leaf conditions. The first switch copies the current
Simple criterion; later switches retain each mode's session draft. Each row has a target,
applicable path/operator/value/case controls and Remove. Add Condition is disabled at 16.
Each group has an ALL (AND) or ANY (OR) selector, Add Condition and Add Group. Child
groups also have Remove Group. The root is always present and cannot be removed.
Indentation, borders and level labels show the hierarchy. Groups can be collapsed;
this changes presentation only, preserving results, filters and query meaning.
Limits are **16 conditions, 16 groups including the root, and depth 4 including the root**.
Add controls enforce these limits globally. Empty groups are invalid, including ANY
groups; removing the last condition never becomes an implicit match-all. Inline errors
identify invalid conditions and empty groups. Tab navigates controls; group headers
support standard Expander keyboard interaction. Structural/operator edits invalidate
old results/filtering without executing a search.
Enter in a value input runs Search; Ctrl+F focuses the first applicable value (or path
for existence-only conditions). A horizontal splitter separates the scrolling builder/
library from the results. Advanced starts near **60% builder / 40% results** and may give
most of the tool to query construction. Drag the divider or focus it and use arrow keys.
Search/Cancel/Clear, mode and Filter Navigator remain above both scrolling regions.
Result pages and their footer remain separate. Minimum heights shrink with short panes
instead of forcing the outer Dock larger. Simple uses compact automatic height and hides
the splitter; returning to Advanced restores its independent preferred proportion.
Resizing or expanding/collapsing groups never runs/invalidates a search or clears filters.
The Advanced fraction is session-retained and saved on completed drag/keyboard gestures,
not on every pointer delta, in existing editor-preferences.json as version-1 SearchLayout.
It contains only a fraction, default .6, accepted range .1–.9; unsupported/out-of-range
values fall back safely. Docking/floating/hiding/recovery keep tool.search-results and
older layouts; source values/results/document details never enter this setting.
Fluent resources apply
in docked/floating light/dark windows; native DPI/IME behavior still needs acceptance.

| Target | Operators |
| --- | --- |
| Message Text | Contains; Does Not Contain |
| HL7 Field | Contains; Equals; Does Not Contain; Does Not Equal; Exists; Does Not Exist |
| HL7 Segment | Exists; Does Not Exist |

Use the same field/component/subcomponent paths as Simple. Segment paths are exactly three uppercase
ASCII letters/digits, as validated by Core (for example PID, PV1, OBX); path inputs may
have surrounding whitespace, but literal values are never trimmed. Explicit occurrence/
repetition brackets remain unsupported; every applicable occurrence
and repetition is examined. Values are required/nonblank only for string comparisons.
Exists/Does Not Exist hide value/case controls and ignore their inactive values.

Choose **Match ALL (AND)** or **Match ANY (OR)** independently in each group. There is
no per-condition connector or expression language. The entire bounded tree is validated
and deeply copied before scanning. Children are evaluated in their displayed order:
ALL stops at the first false child; ANY at the first true child. Limits are also guarded
during evaluation. A needed
structural scan that exceeds Core's budget skips/counts that message. An unneeded
structural condition is not evaluated, and optional header failure still cannot hide
a valid literal match. Skipped counts are coverage limits, not negative results.

Examples (parentheses indicate child groups):

- `PV1 Exists OR (MSH-9.1 Equals ADT AND PID-3.1 Equals SYN-001)`.
- `(PID-3.1 Contains SYN-001 OR PID-3.1 Contains SYN-002) AND OBX Exists`.
- `MSH-9.1 Equals ORU AND (OBX-2 Equals NM OR OBX-2 Equals ST)`.

There is still one row per matching message. In Simple, Count retains the Stage 6A
occurrence/whole-value count below. In Advanced, Count is the number of satisfied
evaluated leaf conditions, including true leaves in an evaluated branch that ultimately
failed before another OR branch succeeded. Short-circuited leaves are not counted.
The UI displays a bounded "N satisfied evaluated conditions" summary. This is not an
exhaustive occurrence count or a full proof/trace. A primary safe location and summary contain no
query values; message/result counts are never multiplied by the number of conditions.

### Presence and negative comparisons

Contains/Equals mean **any** applicable repetition/component satisfies the comparison.
Does Not Contain/Does Not Equal require structural presence and **no** applicable value
satisfying that positive comparison. A nonmatching repetition cannot negate a match in
another repetition or another segment.

| Field/component state | Contains/Equals | Negative string comparison | Exists | Does Not Exist |
| --- | --- | --- | --- | --- |
| Missing | false | false | false | true |
| Present, empty | false for nonblank query | true for nonblank query | true | false |
| Any applicable value matches | true | false | true | false |
| Present, none match | false | true | true | false |

Empty first components/repetitions count as present where Core exposes their nodes;
missing higher components do not. Explicit-null `""` remains a present raw string.
Bare segment identifiers count as segments; opaque malformed lines with an identifier
preview and no exposed structure do not. No normalization or repair is performed.

Choose **HL7 Segment** for `PV1 Exists` or `PV1 Does Not Exist`; no field number is needed.
Choose **HL7 Field** for `PV1-1 Exists` or `PID-3.1 Does Not Exist`. A bare `PV1` in Field
mode shows feedback to choose Segment; it is not reinterpreted as an arbitrary field.
Switching a segment target to Field preserves its identifier and starts at field 1.

## Search Library and explicit local persistence

Expand **Search Library** inside Find Messages. Enter a valid query and Search name,
then **Save Search** to create a new definition. **Save** and **Update** are explicit
actions without repetitive modal confirmations. A persistent nonmodal disclosure explains
that names and query values are stored locally as unencrypted JSON and may contain
sensitive data. Searching patient identifiers is supported; saving such definitions is
allowed. Searching, editing, opening and clearing never
write query history. This is separate from the Script Library.

Select a definition and **Load Search** to replace the current query without executing
it. Run Search explicitly against any active document. Load first to enable Update,
Rename and Delete. **Update** changes the loaded record's query under its existing name.
For **Rename**, edit Search name then click Rename; it preserves the stored query even
when the current builder draft differs. **Save Search** always creates a new record;
case-insensitive duplicate names fail without replacement. **Delete** requires a separate
confirmation and leaves the current query/results and source documents intact.

Default directory: `%LOCALAPPDATA%\HL7Workbench\Searches` (alongside existing settings
and Scripts; custom configuration roots use their own Searches subdirectory). Records
are local JSON, not encrypted storage. Each GUID-named file
contains only schema/query version 2, identity/name, Simple/Advanced mode, an ordered
Boolean root tree and effective conditions/targets/operators/paths/values/case settings. Inactive Simple/
Advanced draft inputs, results, ordinals, control IDs, source paths and message contents
are not serialized. Values supplied explicitly as query inputs are the user's responsibility.
No definitions are bundled with patient identifiers, and none are automatically selected
or executed on application restart.

Version-1 flat ALL/ANY definitions load as an equivalent root group, retaining names,
leaf order, operators, exact literal values and case settings. Loading/listing does not
rewrite files. The original byte hash remains the revision for stale-write protection.
Explicit Update/Rename writes schema 2 through the existing safe replacement path.
Malformed/deep/empty/over-limit trees are rejected as whole records; no conditions are
silently dropped. Group expansion is session-only and is not part of the persisted query.

Limits: 256 files and 128 KiB per record; names are 1–120 characters without leading/
trailing spaces, path/control characters or `.`/`..`. Names never form filenames. Unknown
fields, missing schema fields, invalid operators/paths and unsupported versions are
rejected. Corrupt entries are counted/skipped independently and retained for review;
valid entries remain usable. Unrelated JSON files reserve capacity but are not rewritten.
An over-capacity directory requires manual review before it can be listed; no file is deleted.
Unpaired Unicode surrogates are rejected on save rather than silently changing a literal.

Writes reuse FileSaveDestination: same-directory staging, durable flush/close, captured
identity/hash checks, creation-only collision handling and atomic supported replacement.
The library uses a cooperating-instance lock plus complete-record revision hashes for
update/rename/delete. Stale operations fail safely and require Load again; the current
builder draft stays intact. Entry symlinks are rejected, error/status text excludes query
values and source paths, and cleanup removes only owned staging files. Filesystem races
with noncooperating writers remain subject to the shared publication API's documented
limits; delete's final version-check/name-removal interval is not a filesystem transaction.

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

Accepted paths are Core's uppercase `SEG-field`, `SEG-field.component`, or
`SEG-field.component.subcomponent`, for example MSH-9, PID-3, PID-3.1, PID-3.1.2,
PID-5.1, PV1-2 and OBX-5. Invalid paths get fixed explanatory
feedback. Explicit occurrence/repetition brackets remain unsupported;
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

## Field Inspector Copy Path and application appearance

Right-click a current Field Inspector row and choose **Copy Path** to copy its field,
component or subcomponent coordinate. Right-click the selected segment heading above
the inspector tree to copy its segment identifier. Keyboard context-menu invocation on
a selected tree row uses that row. Only structural paths are copied, never values.
Missing/nonstructural/stale nodes disable the action. Occurrence/repetition brackets
are intentionally omitted because Find Messages searches all applicable occurrences;
the copied path is a discovery target, not an exact repeated-value address. A copied
segment path belongs in Advanced → HL7 Segment; other paths work in Simple Field or
Advanced HL7 Field. No Search This Field command is added.

Settings → Preferences → Appearance offers **Follow System** (default), **Light** and
**Dark**. Apply/OK persists and updates Avalonia's application theme immediately, across
main/floating tools, menus and dialogs; no restart is needed. Cancel abandons unapplied
choices. Follow System uses Avalonia's Default theme and supported OS change notifications.
Older preference files default to Follow System. HL7 editor background, text, syntax,
line numbers, caret, text/structural selections and omission markers now use the effective
variant too, including source-backed/virtualized blocks and Full Source Viewer. Applying
a theme redraws existing lines without replacing documents, history, selection or scroll,
reading source again, restarting search/scripts, or modifying original bytes.
See [desktop preferences](desktop-preferences.md) for palettes and future extension points.

## Revisions, unsaved edits and cancellation

Search captures the active document's logical order/MessageIds, its immutable original
index and references to current bounded Workspace drafts, including inserted messages.
It includes unsaved/malformed draft text without requiring Save, waiting
for inspection or silently discarding edits. Untouched messages are not reconstructed.
Search captures a content-only revision, distinct from viewer navigation. Following a
result or a successful Save alone does not invalidate it. Editing/insertion/Undo/Redo invalidates
the entire result set, cancels pending work and restores the full navigator with a
"Document changed" notice. Rerun explicitly to search the new revision.

Switching tabs cancels/clears search and filtering; returning does not reuse stale results.
Closing/reloading/replacing a document invalidates old results, even for the same document
ID. Canceled or superseded work cannot publish partial/stale results or a partial filter.
The source read lease drains before document snapshot cleanup. Search does not modify
save identity, history, original source bytes, script state or report/export behavior.

## Filter Navigator

The existing message navigator is the per-file First/Previous/number/Go/Next/Last toolbar.
Filter Navigator adds a virtualized matching-position selector there and restricts those
controls by matching MessageIds. Current logical positions are shown; original ordinals
remain provenance. Structural mutation clears the filter before selecting the inserted entry.
The count distinguishes total messages from matching messages. An excluded current
message stays selected and is clearly marked; First/Next/Previous or the selector can
choose a match. Go to an excluded position explains how to clear the filter. Zero-result
filters have no navigation targets. Disabling, clearing, editing, switching or closing
restores the full navigator. No document/message is removed or copied.

## Bounds, performance and limits

Scans and structural inspection run off the UI thread, one message at a time, with
cancellation checks through decoding, window matching and Core scanning. First-message,
throttled and final progress contain counts only. Results collect incrementally in a
bounded list and publish on successful completion. Previous/Next page exposes at most
200 rows to the virtualized results control; the matching-position selector is virtualized.
No control is created for every document message.

The logical document sequence retains the 100,000-message ceiling. Results likewise
contain at most 100,000 rows with capped headers/coordinate/count, rather than full text
or snippets; paging bounds UI state while the complete ID set supports filters.
Result memory is bounded by this ceiling (worst-case headers plus row/list overhead,
roughly tens of MiB). Draft references reuse the existing 32 MiB text/history budget.
Payloads over 128 KiB reuse the disk-backed decoded source with bounded resident caches.
Advanced conditions share that decoded source and a single lazily built structure per
message. Worst-case literal work grows with the bounded condition count; repeated text
conditions may scan the same spool again without decoding/retaining another full message.
Large field structure remains subject to existing Core scanner budgets; skipped messages
are reported, not silently interpreted from partial structure. Disk capacity/strict
decoding remain existing source-backed inspection limitations. No database or new package.

Known limits: no explicit indexed paths, regex,
automatic history, import/export/synchronization of definitions, bulk transformations,
replace, search-result scripting or incremental per-message edit reindexing. These remain
deferred beyond Stage 6B. Only supported Workspace edit/undo/redo workflows signal
content revisions; callers must not mutate a document's materialized Core object behind
the Workspace model and expect UI notifications.

See [ADR 0008](decisions/0008-read-only-message-search.md),
[ADR 0009](decisions/0009-explicit-saved-search-definitions.md), and the
[Stage 6B native acceptance and verification checklist](stage6b-advanced-search-acceptance.md).
