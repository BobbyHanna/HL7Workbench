# ADR 0010: Read-only structural comparison over selected-message sources

Status: Accepted for Stage 7; native Windows acceptance confirmed by the user on 2026-10-16.

## Decision

Use Core's shared `Hl7StructureScanner` ranges over two caller-owned immutable
`IHl7TextSource` instances. Core opens no files, owns no UI or Dock state, and adds no
dependencies. Compare segment, field, repetition, component and subcomponent structure.
Keep MSH-1/MSH-2 literal. Descend only through meaningful splits; collapse implicit
singleton chains to the containing field/repetition/component. Missing, present empty
and explicit null remain distinct. Supported escape equivalence uses each side's
encoding and reports SourceSpelling rather than hiding different raw spellings.
Unknown/diagnosed escapes remain raw. Leaves over 64 Ki characters use raw equality;
no attachment format is decoded or rendered.

Align ordered segments using deterministic 64-entry lookahead for an exact content
fingerprint anchor; choose the shortest displacement, preferring additions on ties.
Fingerprints include structural shape and raw leaf contents, omit MSH declarations,
ignore opted-out fields and omit opted-out trailing empty fields. They select anchors
only: every aligned pair is checked against actual source ranges. When exact anchors
are absent and segment names differ, bounded name anchors preserve following modified
segments. Otherwise pair in order as modifications. Field repetitions use the same
strategy with a 32-entry window. Identical duplicates match earliest-first. Set IDs are
never keys. This gives O(text + nodes + segments × 64 + repetitions × 32) work and
linear bounded structural storage rather than an unbounded edit-distance matrix.

Outside the lookahead, alignment may produce additional modifications. It is not a
globally minimal diff or a clinical matching algorithm. This explicit tradeoff keeps
worst-case behavior predictable and cancellation frequent without a heavy dependency.
Scanner budgets still apply (100,000 segments / 250,000 elements per side by default).
Fail explicitly at 20,000 differences; do not publish truncated results. Difference
values are capped at 256 characters and marked abbreviated; ranges remain authoritative.
Both sides retain their own segment occurrence and nested coordinates, including when
insertion changes the occurrence numbers of a later modified segment.

## Capture and lifetime

Stage 7 refinement adds two UI-independent Workspace `ComparisonInput` slots. Each
retains a selected source kind, original document ordinal/choice and independent
temporary pasted draft. Switching kind does not replace the inactive draft. Immutable
`ComparisonSource` captures represent either a document/original or literal pasted
text; pasted captures do not construct Workbench documents, files or decoded spools.
They use the existing complete-editor budgets (1 Mi UTF-16 units / 16 Ki per line),
rejecting oversized replacements atomically. No clipboard newline conversion, trimming,
framing repair or source normalization occurs.

The shared scanner remains the only HL7 grammar. Multiple-MSH pasted input is rejected
with a single-message explanation and its draft retained; no first-message truncation.
Empty/whitespace-only input has useful guidance; malformed/incomplete nonempty sources
retain original text and parser diagnostics. Paste uses UTF-8 only as the HL7 escape
codec's encoding context; Unicode clipboard text has no claimed original encoded bytes.

Workspace captures exactly the selected original ordinal, independent of filtering,
active message, parsing debounce and window coordinates. An edited message uses an
immutable current draft string; Original uses its initial text. Untouched file messages
borrow the existing original snapshot/index and privately decode just their payload.
In-memory documents retain an immutable reference to their initially loaded text.
Save/Save As never rebase the original side.

Snapshot read leases share the existing search-reader disposal drain. Construction
links caller cancellation, both document lifetimes and both content-change tokens.
Even pre-canceled tasks execute their release/finally path. Result-owned spools use a
serialized bounded-read gate; disposal releases them and leases after pending reads
finish. A content revision invalidates results; generation guards reject superseded
background completions. Supported Workspace edits/undo/redo are the revision boundary;
external direct mutation of the mutable Core model is not an application editing API.
Input-slot changes cancel/clear stale work and debounce 250 ms before recapturing.
Selection is matched by both sides' structural coordinates where possible. Explicit
Refresh and ignore changes can run immediately. Inactive document choices are removed
on document closure without discarding pasted drafts or invalidating unrelated pasted
results. Closing the workspace clears both drafts and unsubscribes all input/document
events. No automatic clipboard read, storage or history is introduced.

## Desktop and privacy

An optional release-on-close tool starts in the document group, can dock/float and
retains source selections only while open. Dock stays in Desktop/Docking. Floating
directly from a document group can supply Dock's Unset alignment; serialize a Bottom
recovery edge in existing version-2 placement metadata rather than widening the schema.
On restart the tool restores no content and remains lazy; reopening a prior tool/floating
placement reconstructs empty feature state. Default document-tab placement reopens at
document size. Internal splitter sizes remain session/view-only.

Reuse AvaloniaEdit, Core-coordinate syntax coloring, the existing dynamic HL7 palette
and clipboard provider. New sessions default to Side-by-Side, giving both source panes
the content area with a resizable divider and immediately usable empty pasted editors.
Differences remains a virtualized grouped summary with wrapped, capped row previews and
optional 4,096-character selected details. The checked toggle retains its session value
through docking, floating, resizing and hide/reveal; real close/reset releases the session.
They share one result,
selection and loaded pane state; a presentation toggle triggers no comparison/read.
A/B source controls expose secondary kind/document/ordinal/paste controls in flyouts.
Empty-state paste buttons and Ctrl+V on a source control work in Differences directly.
Copy and ignore controls use compact menus; ignore state remains visible beside the count.

For document sources, two independent 64 Ki-character windows show exact contiguous
decoded text, labeled with their global UTF-16 origin. Pasted panes are editable complete
bounded strings; document/original panes remain read-only. Native paste is intercepted
for these temporary editors and inserts exact clipboard characters through TextDocument
so AvaloniaEdit cannot normalize CR/LF/CRLF. Clipboard reads check the input revision
before publishing to prevent an asynchronous paste overwriting newer typing. Background
result publication redraws syntax without moving a focused pasted editor's caret.
Navigator selection
explicitly positions both panes; a missing side shows nearest same-name segment context
where available, without inventing a missing value span. Complete-value copy reads source
ranges and refuses values/summaries above 1 Mi characters. It never substitutes a preview.

The redundant Settings → Start in Edit Mode menu item and its menu-only synchronization
are removed. Preferences → General continues to persist the same setting and apply it
to subsequently opened documents. No migration or preference reset is needed.

No source text, values, results, paths, ordinals or history are added to settings, layout,
Activity Log or telemetry. Read-only comparison does not navigate, save, change dirty
state, modify history or invoke the scripting worker. Encoding/BOM byte differences alone
are outside this decoded structural comparison. No merge, external-file browser, batch
comparison, arbitrary ignore expressions or comparison export/history is introduced.
