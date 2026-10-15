# Architecture

The solution targets `net10.0`: Desktop -> Workspace -> Core (Desktop also directly
references Core). Core, Workspace, and Desktop each have corresponding test projects.
Core has no NuGet dependencies. Workspace has no UI or docking dependencies.
Desktop contains the accepted Milestone 2 docking foundation and the Stage 1
read-only raw viewer, manually accepted on 2026-10-14.
Milestone 1 adds the parser/model, paths, editing, escapes, diagnostics, and
serialization to Core, with synthetic tests in Core.Tests. No desktop integration
or other milestone functionality was added during Milestone 1.

Future desktop behavior will use MVVM. Introduce interfaces and dependency
injection when a concrete testing or substitution need exists. Generation,
scripting, transport, Synthea, and CLI projects will be added in their milestones.
Core libraries must remain UI-independent and cross-platform.

The application has no telemetry, cloud integration, or runtime network features.
No logging of message contents is configured. Samples and tests must be synthetic.
Git ignore rules are a convenience, not proof that data is safe to commit.

## Milestone 1: Parser and round-trip

`Hl7Parser` accepts an in-memory string or byte span representing one message.
There is no file I/O, transport framing, or multi-message reader. Future readers
can supply one bounded message at a time. The parser is delimiter-driven and has
no version dictionary or external dependencies.

Raw segment/element spelling and exact segment terminators are retained alongside
the hierarchy. Missing values, present empty values, and the explicit null marker
remain distinct. MSH-1/MSH-2 are special literal fields. Unknown segments have
ordinary structure; malformed lines are retained as opaque content. Parsing
diagnostics describe recovery/ambiguity; `Hl7Validator` performs separate minimal
header checks without rewriting anything.

`Hl7Message` owns original bytes, the selected encoding/BOM, read-only model
snapshots, and controlled path edits. Edits build and parse a candidate before
publishing it; failed edits leave the previous message intact. The serializer
returns retained bytes when unchanged. Supported edited byte messages splice
only the changed span while preserving untouched bytes. There is no normalization.

Resource limits bound message bytes/characters, segment count/length, total and
per-container elements, diagnostics, and edit padding. Size/structure/expansion
violations throw `Hl7LimitException`; diagnostic storage instead saturates with
a suppression notice. Edits preflight all size budgets before materializing
replacement strings. Ambiguous escapes lock only the affected segment, with
editability tracked independently of diagnostic storage.

See [parser API and limits](parser-api.md), [path grammar](hl7-paths.md), and
[ADR 0002](decisions/0002-lossless-parser.md) for contracts and limitations.

## Milestone 2: Docking foundation (review gate)

Dock.Avalonia wrappers live in Desktop/Docking, with ordinary feature content in
their Context. Workspace provides document lifecycle and active-message context,
the lazy tool registry, metadata-only layout schema, limits, and recovery policy.
Feature view models do not inherit Dock classes. Tool focus retains the selected
document; closing its tab switches to another open document or clears context.

Message Browser selects the synthetic tabs; Diagnostics follows their real parser
and minimal header diagnostics. Dock supplies draggable/resizable/floating tools.
Layout persistence and View menu reopening are application-coordinated. See
[ADR 0003](decisions/0003-dockable-workspace.md) and the
[desktop checklist](docking-acceptance.md). Native UI/monitor checks are pending.

Milestone 2 remains read-only. After explicit continuation approval the document view will have a
full-width raw pane above segment navigation and hierarchy inspection. Future file
sessions preserve source bytes/framing, with bounded concatenated-message navigation.
Editing, history, message saving, and dirty state are deferred to Milestone 3.
Reusable script storage and Script Editor/Library tools are future services; search,
comparison, generation, de-identification, MLLP, Synthea, and IRIS remain deferred.

Stage 1 file reading and session document creation live in Workspace; Avalonia
file picking, editor configuration, and Core-coordinate highlighting live in
Desktop. No version dictionary is required to render a structurally compatible
message. See [Stage 1 acceptance](viewer-stage1-acceptance.md).

Stage 2 adds `MessageInspection` on each Workspace document: UI-independent segment
entries, lazy field/repetition/component/subcomponent projections, raw ranges, and
session-only selection. Desktop renders these in document-internal Avalonia grids
with splitters, not Dock tool windows. Selection requests raw navigation; raw caret
changes update structural context without replacing user text selections. Attached
views subscribe and detached views unsubscribe, allowing Dock content reconstruction
without leaking views or losing document selection. No Core parser or dependency
changes are needed. See [Stage 2 acceptance](viewer-stage2-acceptance.md).

Stage 2 UX refinements preserve the full canonical hierarchy alongside lazy
`VisibleChildren` projections of meaningful delimiter splits. Canonical parent
chains map suppressed selections onto visible ancestors without losing identity.
Workspace resolves raw offsets into deepest meaningful structural ranges, reusing
lazy cached projections without changing selection for hover. Double-click requests
selection explicitly. Desktop owns AvaloniaEdit glyph-floor hover hit-testing,
scroll-offset mapping, ancestor reveal and conventional raw-editor ToolTips.
Tooltips consume `InspectionPathInfo` and display only canonical coordinates, allowing
future independent version/profile metadata without a hardcoded standards catalog.

## Viewer Stage 3 (native Windows acceptance granted October 14, 2026)

File documents now own streamed immutable snapshots and complete byte partitions.
Workspace indexes concatenated/MLLP messages without creating parsed models for each
entry. Only the active message has structural projections. Core's shared
`Hl7StructureScanner` supplies ranges to both the original materialized parser and
source-backed inspection; no second HL7 grammar is maintained in the UI.

Workspace strictly decodes active source-backed messages to an owned UTF-16 spool,
with sparse original-byte checkpoints and a bounded character cache. One decoded
spool may survive navigation as a cache, without old structural models. Desktop
uses AvaloniaEdit and the existing structural controls; main previews, optional raw
inspector windows and field previews are bounded, with selection and hit-testing
mapped to message-global UTF-16 coordinates.
Original byte ranges remain separate and never derive from editor text.

The consolidated UX refinement replaces main-pane raw paging with Workspace's
`MessagePresentation`: structural leaf abbreviation and explicit source/display
interval mappings. Desktop renders compact wrapped previews or virtualized bounded
editor blocks. Original bytes/text remain unchanged. Hover and structural selection
map through retained fragments; synthetic markers have no source-character offsets.

`RawInspectorSession` and `FullRawInspectorView` provide optional contiguous source
windows and streamed exact-byte export. The document retains snapshot/spool ownership;
the optional tool borrows access and closes on bound message/document changes.
Its context is explicit: document-tab switching does not retarget it. Same-file
message navigation still invalidates it, contrary to the requested full snapshot
retention. The user explicitly deferred that source-ownership change during layout
polish; see `stage3-workspace-layout-acceptance.md` for the verified limitation.
`InspectionPanes` supplies the shared structural controls both to standalone document
tests and production dock-tool adapters. Tool focus retains active-document context.
The existing registry gains optional/release-on-close flags; layout schema version 2
stores stable groups and closed-tool placement. It accepts version 1 and never restores
optional source content at startup. See `viewer-stage3-ux-acceptance.md` for resource
bounds, export semantics and accepted lifecycle policy.

Optional/releasable tool wrappers retain stable Dock identities while feature Context
objects change on close/reopen. `DockToolContentHost` observes that replacement at
the cached template boundary, detaches old content and builds current feature hosts.
Visual-tree attach/detach controls subscriptions and bounded reads; no timing delay
or forced docking transition is needed to initialize Full Source Viewer. Layout
restore adds Navigator/Field Inspector only when their identities are absent from
old metadata, preserving explicit user-hidden state and existing geometry.

See `viewer-stage3-acceptance.md` for boundary recovery, capture-consistency limits,
resource budgets and manual acceptance; `stage3-performance.md` records measurements.
Stage 3 itself implements no message editing, saving, scripting or attachment rendering.

Stage 3 final polish adds Workspace's typed `ActivityLog` over session operations,
with a bounded 2,000-entry queue and generated document numbers. Fixed descriptions
and allowlisted diagnostic codes prevent imported content, filenames, paths or
exception text from entering history. Document-owned capped deduplication projects
retained index/boundary/active-structure diagnostics without altering Core contracts
or parsing inactive messages. File opening and exact-byte exports record safe states
and counts. Routine selection/navigation/progress is excluded.

Desktop's optional `ActivityLogViewModel`/virtualized `ActivityLogView` supports
severity filtering, clear/copy, tail following and anchored review. Closing releases
the projection but retains session history; restart clears history. The stable old
Diagnostics identity aliases Activity Log, retaining placement. Activity Log now opts
into `ToolDefinition.RestoreVisibility`: saved visible content is constructed in place,
while saved hidden content remains uninitialized. Full Source Viewer continues to omit
content on restore. Legacy slots without placement records retain their reopen geometry.
Status remains immediate operation feedback with actual diagnostic counts and a
lightweight clickable history indicator. No logging framework, disk sink or new
layout version is introduced. See `stage3-activity-log-acceptance.md`.

Final workspace polish changes only fresh/reset defaults: a 78/22 horizontal content
split puts documents left and independent Navigator/Field Inspector docks in an equal
vertical stack right. A 73/27 vertical split puts that content above a full-width
Activity Log. Message Browser remains registered but hidden in the default graph.
All proportions are adjustable, and saved valid graphs are restored without applying
this new arrangement. The shell keeps its natural-height command row and flexible
workspace. See `stage3-workspace-layout-acceptance.md` for current native checks.

## Stage 4 editable document foundation (accepted 2026-10-15)

Workspace now owns bounded raw working buffers and delta histories independently
of editor/Dock view lifetime. Original snapshots/indexes remain immutable. Complete
materialized messages can opt into per-document Edit Mode; source-backed abbreviated
messages remain read-only. Core APIs, edit limits and scanner grammar are unchanged.
Raw drafts may be temporarily malformed; pending/failed parsing removes current
coordinates while retaining one labeled previous bounded projection. Its offset and
selection methods are disabled, as are stale raw highlighting/hover/navigation.
Inspection hosts keep existing panes and bind new data in place, restoring canonical
selection, realized expansion and scroll offsets where possible. Current/Pending/
Stale/Empty/Unavailable are explicit states. Debounced parsing runs under the existing
work/scan gates, with revision checks and drain-before-disposal ownership.

Dirty state compares working text with the last published save point; undo/redo
survives navigation, view reconstruction and saving. A separate application preference
changes the default only when a document opens. Layouts continue to store placement
metadata exclusively. Desktop supplies the Edit Mode toolbar, keyboard commands,
local pickers and unsaved-change dialogs; Dock adapters observe titles/close intents.

Saving freezes this document's manual mutations, validates boundaries and streams
original file ranges plus surrogate-safe encoded payload splices. Same-directory
temporary output is flushed then replaced/moved; failures keep working state and
save identity. Save As retargets future Save without rebasing immutable source ranges.
Edited UTF-16 ranges cannot be used for original-byte exports. Existing optional
source sessions end on edits and retain Stage 3's navigation/closure lifecycle.

See [ADR 0004](decisions/0004-editable-document-foundation.md) for alternatives,
budgets, coordinate contracts and intentionally deferred large-message editing,
source mappings, grouped/disk-backed history, external-change reconciliation,
scripting and SQL. See [Stage 4 acceptance](stage4-editable-document-acceptance.md)
for verification and the native Windows gate.
