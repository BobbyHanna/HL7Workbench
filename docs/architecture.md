# Architecture

## Stage 16 coherent synthetic scenarios

Stage 16D shares the Stage 16A ordered demographic catalogs and deterministic
profile/provider/facility/location logic in internal Core SyntheticDemographics.
Both dialog modes default Realistic Synthetic and expose Obvious Placeholders.
Standalone v2 renders the unchanged v1 clinical template, replaces only existing
demographic fields using bounded Core-parsed materialized ranges, and verifies the
complete result through Core's parser/validator/serializer. Explicit placeholder
style retains original standalone v1 bytes; scenario-v2 realistic/placeholder bytes
are unchanged. Old style-less standalone Core options/JSON retain legacy meaning;
new dialog configurations select realistic explicitly. No preset store exists.
Unset versus explicit transport values stay distinct until the operation's single
automatic seed is resolved after preflight. Normalization captures replay settings.
The same dialog exposes transport/patient overrides and style/version reporting;
all content enters the existing prospective publication/history transaction.
Core remains independent of Workspace/UI/Dock. No new templates, fields, dataset,
dependency, publication route or network service is added. See the
[Stage 16 guide/compatibility boundaries](stage16-synthetic-generation.md).

Core adds separately versioned immutable scenario options, bounded structured
overrides and fictional patient/encounter/order contexts. It shares version 1's
specified PRNG/segment construction helpers without changing standalone golden
bytes. A small ordered clinical event sequence renders the existing supported
families plus ADT transfer/discharge, and validates one message at a time through
the established parser/serializer. Quantity/date preflight and targeted shared-ID/
timeline checks precede publication; no schema engine or clinical inference enters
Core. Workspace's existing captured-target generation transaction accepts the new
closed Core batch through the same prospective-save/index/exact-byte proof and
single insertion/history authority.

Generate Messages adds a mode selector, exact estimate and collapsed scenario
customization. A count-only SyntheticGenerationReport belongs to MessageDocument,
is attached before new-document open and notifies same-tab insertions. The existing
Generation Report tool/indicator project it alongside unchanged FHIR report contracts.
Report views follow active documents and release subscriptions on disposal; layout
still persists placement only. There are no new packages, Dock identities, editors,
external services or script-execution hooks. See [ADR 0012 amendment](decisions/0012-deterministic-synthetic-generation.md)
and [Stage 16 guide/acceptance](stage16-synthetic-generation.md).

## Responsive tool content (Stage 15UI)

The FHIR workspace follow-up replaces the old browser/form arrangement with
master-detail content and a collapsed generation section. It preserves the tool's
serialized identity. Minimal Core/Workspace no-acknowledgment overloads reuse import
validation, while Boolean compatibility overloads retain their old guard. Desktop
arms fresh/reset proportions after finite Dock measurement and uses root-identity
checks to preserve saved layouts. See [follow-up acceptance](stage15ui-fhir-workspace-acceptance.md)
for evidence and the native-review boundary.

Desktop composes ordinary feature controls with native wrapping action bars,
responsive labeled forms and finite-measured scrolling Grid surfaces. Individual
tool dimensions drive layout in docked and floating hosts; Dock continues to own
chrome, placement and its existing persistence schema. Nested ToolContentGrid
compositions expose vertical overflow without giving editors/lists infinite height.
SQL and Script Desktop models own a session-only editor-document/undo handoff for
Dock template replacement; disposal releases it. Core/Workspace behavior and
dependencies are unchanged. See [responsive tool layout](responsive-tool-layout.md),
the [ADR 0003 amendment](decisions/0003-dockable-workspace.md), and
[Stage 15UI acceptance](stage15ui-acceptance.md) for composition, audit boundaries
and the separate native visual acceptance checklist.

## Stage 15A/15B/15C local SQL workflow

The post-beta SQL engine is a separate, review-gated source milestone. Querying is
a SQLite/UI-independent transport and typed relational contract assembly; QueryHost
alone references SQLitePCLRaw and its pinned native SQLite library. Workspace now
captures logical live documents and projects Core structural ranges. Desktop exposes
one optional dockable SQL editor/results tool and deploys the existing isolated
worker under QueryHost/. Workspace owns explicitly saved named SQL definitions;
layout persists placement only. Core/schema/native enforcement are unchanged in
15C. Each worker owns a private
in-memory database, Windows memory containment, native heap/page limits, a
deny-by-default authorizer and bounded cancellation/deadline/result transfer.
Prepare-only authorizer-protected planning discovers relations before import, without
stepping. Execute permanently seals import before preparation/execution. Three fixed
read-only field adapters request values lazily over bounded RPC; hidden metadata
backing tables are never authorized by view/CTE names. MSH-only queries avoid decoding
unused OBX values. No custom SQL parser, arbitrary import DDL, extension, disk database
or external connection is exposed. Complete journals support paged previews, CSV
and separately bounded all-row identity extraction into independent documents.
Source bytes/framing remain authoritative; unsafe outputs fail without publication. See
[ADR 0020](decisions/0020-isolated-read-only-sql.md) and
[15C acceptance](stage15c-sql-ui-acceptance.md),
[SQL UI guide](sql-query-ui.md) and [HL7 workflow reference](sql-hl7-workflows.md).
Stage 15D preserves these boundaries after measured stabilization. The protocol now
rejects malformed nested JSON shapes with fixed errors. No engine rewrite, new SQL
capability or limit increase is introduced. See [15D acceptance](stage15d-sql-final-acceptance.md)
for runtime measurements, exact-byte/resource tests and conditional native/limit
verification. Accepted preview artifacts remain unchanged.

## Stage 14 portable beta readiness

See [ADR 0019](decisions/0019-portable-beta-readiness.md). A local candidate assembles
two directory-based self-contained win-x64 processes with separate runtimes. The
renamed native apphost retains HL7Workbench.Desktop.dll binding; no technical or
persistence rename. ScriptWorkerSession prefers an adjacent apphost without changing
protocol/containment. Existing build copying owns the worker boundary; the project
reference is nonprivate to avoid root apphost/runtimeconfig leakage. Core/Workspace/
Transport behavior and saved-data schemas remain unchanged. Unique layout temporary
metadata/recovery and fixed save status address concrete stability/privacy problems.
Version/portable content/license/provenance verification supplements existing suites.
All artifacts remain local candidates pending native/human review and separate release
authorization. No automatic recovery of clinical contents, network access or updater.

## Stage 13B controlled load testing

Stage 13A is checkpointed as 45e7cdbad9a32cdaebb7834ae0026b10914622aa after all eight
native acceptance checks. Stage 13B remains uncommitted for separate native review.
LoadTestingSession reuses batch preparation and production transport, with one
global monotonic dispatcher and at most eight active exchanges, bounded unchanged
replay, duration/attempt limits and runtime stop thresholds. It retains exact
aggregates and bounded timing samples separately from first-1,000 completion rows.
Transport only adds phase timing metadata. Desktop supplies Dock/review/pickers,
throttled presentation and cancel/drain ownership; no second protocol stack, Core
UI dependency, ScriptHost privilege, package or new persistence participates.
Reports use whitelist metadata and existing Core CSV/atomic save mechanisms.
See [ADR 0018](decisions/0018-controlled-load-testing.md), [guide](load-testing.md)
and [acceptance](stage13b-load-testing-acceptance.md). Earlier milestone states below
are historical; sequential batch and single-send contracts remain intact.

## Stage 13A controlled batch transmission

Stage 12 passed native acceptance and was committed by the user at
e575ebe47921f33b3431d32bf26a5fc87aee4a0a. Stage 13A and its report polish passed
native acceptance; the single local checkpoint is authorized. Earlier pending
Stage 12/13A statements below are historical.

Workspace owns a bounded immutable selection and an explicitly authorized sequential
batch. It reuses authoritative Stage 12 captures and production MllpConnection/Core
ACK handling, with one connection per message and an ACK-gated, monotonic, bounded
post-exchange pacing/pause scheduler. A source change before Start requires fresh
preparation; afterward it cannot change captured payloads. No retry/concurrency/load/
schedule is added. Typed result metadata is separate from bounded retained ACK bytes;
explicit reports reuse Core CSV escaping and version-consented atomic file publication.
Desktop supplies only lazy themed Dock controls, confirmations, viewer/picker actions
and task-draining ownership on close/reset/application shutdown. Layouts persist no
network state or authorization. Core, ScriptHost, document/save identities and package
dependencies are unchanged. A null-safe listener client-endpoint snapshot corrects
the disposal race demonstrated by batch integration tests; Stage 12 single-send
protocol and confirmation behavior remain intact. See [ADR 0017](decisions/0017-controlled-batch-transmission.md),
[guide](batch-transmission.md) and [acceptance](stage13a-batch-transmission-acceptance.md).

Stage 10 passed user native Windows acceptance and is checkpointed at
1b04e1849b2ba3c03d135cf324bae28b8af4cba8 (2026-10-21). The user confirmed Stage 11
native acceptance, including complete histories, and authorized one exact 44-path
local checkpoint before Stage 12. Stage 12 stays uncommitted pending separate native
acceptance. Earlier pending statements below are historical. No external Git,
packaging or publication operation is authorized.

## Stage 12 interactive transport

Stage 11 is checkpointed at af717a66608d3277402bd6696cd4f50712c7ecf5. Stage 12 is
implemented for native acceptance and remains uncommitted. Core interprets headers
and ACK semantics, with no socket/framing/UI dependency. The new Transport library
owns strict incremental byte framing and asynchronous TCP/listener lifecycles.
Workspace owns authoritative one-message capture, freshness, profile persistence
and ephemeral bounded history. Desktop coordinates Dock, review and non-loopback
confirmation, using existing unsaved document viewers for exact payload inspection.
Stream framing remains independent of socket reads and leaves room for a future
authenticated TLS stream; TLS connection mode currently fails without fallback.
No parser/save/message-identity/ScriptHost isolation contract changed.
See [ADR 0016](decisions/0016-interactive-mllp.md), [guide](tcp-mllp.md) and
[acceptance](stage12-tcp-mllp-acceptance.md). Earlier deferred-transport statements
below describe their original milestone, not current status.

## Stage 11 local synthetic FHIR workflow

Core's narrow System.Text.Json R4 importer validates bounded collection/transaction
Bundles and retains immutable projections, digests and explicit input/container
provenance. Raw JSON is transient per file; no SDK, new dependency, database or
server participates. Patient/encounter membership follows only explicit resolved
references. Fixed passes reject ambiguity/conflict and never infer from names,
containment or graph traversal. Only identical shared provider metadata can
deduplicate; clinical records retain their source scope. Timeline precision and
missing/invalid dates remain explicit. See [ADR 0015](decisions/0015-local-synthetic-fhir-integration.md).

Workspace's FhirPatientImport enforces local read-only paths and executes bounded
import off-context. SyntheticPatientsSession owns cancellable replacement, indexes,
100-row pages, search and bounded patient/event selections; Clear/close/reset
invalidates captures and releases data. Desktop supplies native pickers with
synthetic-only attestation and an optional release-on-close Synthetic Patients
Dock tool. Placement persists; imported projections, identifiers, searches and
selections do not. Detached view objects cannot retarget a replacement session.

Core's immutable version-1 generation plan previews eligibility and maps actual
source data separately from explicit generated transport metadata. Source-qualified
patient/visit identities remain stable across ADT/ORU; message control IDs combine
seed, captured selection and ordinal. Coding/units/status/value/date restrictions
are explicit and counted. Core returns a closed parser-validated batch under the
existing Stage 8D ceilings. FhirGenerationPublisher adds one narrow batch overload
to the existing prepared insertion/prospective-save/index/exact-byte proof pipeline,
builds a private empty document and opens it only on complete/current success.
Existing documents, original generator contracts and save semantics are unchanged.

An immutable count-only Generation Report is attached before public document open.
FileDocumentView reconstructs a compact provenance/count indicator from the document.
A lazy release-on-close Generation Report Dock tool projects the active document's
report through the existing formatter; nonreport tabs show an empty state. The tool
can float/resize/restore placement and visibility through the existing layout DTO,
without serializing reports. Only explicit open actions request keyboard focus,
including when Dock attaches the view afterward. Thus attribution survives tool
closure and view replacement. No clinical data, paths or
exception text enter reports/log diagnostics. Reports do not reassess later edits
and are not automatically saved alongside HL7. Core/Workspace remain UI/Dock free;
ScriptHost permissions, isolation, packages and Dock layout schema do not change.
The Stage 11 acceptance improvement expands the same candidate selector to Selected
Patient (UI default), Entire Import and advanced Selected Events. Source-bounded
patient lookups avoid per-patient whole-import rescans; exact eligibility counters
continue beyond the output cap while retained candidates never exceed 1,000.
Preview renders/discards one message at a time for size preflight; the existing
closed batch/publication proof remains authoritative. Patient-grouped stable ordering
and one global ordinal retain ID consistency/uniqueness. Scope changes preserve manual
selections. Separate patient documents are deferred until a safe multi-document
transaction exists; no publication or Dock redesign participates here.
See the [mapping](fhir-to-hl7v2-mapping.md), [user guide](synthea-fhir-import.md) and
[acceptance record](stage11-synthea-fhir-acceptance.md).

## Earlier architecture records

Stage 9C passed native Windows acceptance and is checkpointed locally as
867016c3d1eab969c4a1853994eedb95621ad4f0. Stage 10 implements read-only extraction;
its accepted changes are included in that Stage 10 checkpoint. Older milestone
authorization/pending statements below are historical.

Field Extraction reuses immutable MessageDocument search captures/read leases,
indexed payload access, large-message decoded spools and Core's structural scanner,
path and escape APIs. Core selection preserves explicit occurrence/repetition intent;
ordinal alignment broadcasts scalar columns without Cartesian multiplication or
clinical group inference. Complete bounded values enter a private sealed snapshot
journal; only row offsets, stable identity metadata, selected integer indexes and
bounded sort keys remain resident. Pages carry abbreviated presentation cells.
Filtering/sorting and CSV read authoritative journal values in cancellable background
operations. Source edits cancel scanning and mark completed results stale; document
closure/replacement cannot retarget stable message navigation.

Workspace owns extraction/configuration/result lifetimes without UI/Dock types.
Desktop supplies an ordinary optional release-on-close tool and retains only its
configuration draft separately; saved layouts contain placement, not source/results
or comparison literals. User configuration records contain selectors/preferences
only, with bounded validation, optimistic revisions and existing atomic destination
publication. CSV uses the existing file consent/version/atomic write boundary and
rejects source/storage aliases. A pure Core CSV record encoder is shared with
ScriptBatch to retain identical escaping/formula policy; scripting bounds, worker,
save semantics, message identity, Dock schema and dependencies do not change.
See [ADR 0014](decisions/0014-read-only-field-extraction.md), the
[user guide](field-extraction.md) and [acceptance record](stage10-extraction-analysis-acceptance.md).

Stage 9 and optimized branding passed user native Windows acceptance on 2026-10-20.
One local checkpoint is authorized after fresh verification; earlier pending notes
below are historical. Stage 9C follows only after that checkpoint succeeds and must
remain uncommitted, using the existing editor, Script Library and isolated worker.

Stage 8D is checkpointed at 5eebc767eae5f241ac4c7a02c4275ae0305b76fa after all gates.
Stage 9 adds run-local deterministic de-identification aids in the existing Scripting
facade/Script Library/isolated worker. Core supplies hierarchy/ranges, literal escaping
and final validation; the facade applies one bounded encoded-span replacement batch.
The worker serializes the validated facade model. Parent capture, journals, reports,
output naming, source isolation, cancellation and stale attribution remain unchanged.
No parallel engine, protocol version, dependency or cross-session mapping store is added.
See [ADR 0013](decisions/0013-script-deidentification-aids.md) and
[Stage 9 acceptance](stage9-deidentification-acceptance.md). Native acceptance is pending;
Stage 9 and approved-icon integration remain uncommitted.

Native acceptance corrections add an embedded, original versioned vocabulary and
execution-local SyntheticIdentityLibrary under Scripting. Patient policy 2 receives
opaque identity contexts; source demographics never select the generated profiles.
Narrow presets retain policy 1. The Workspace TransformationReport is a bounded
validated count-only projection of the existing sealed CSV journal, associated before
output publication. Its output document owns the summary and observer disposal;
weak source tracking supplies live revision freshness. Desktop FileDocumentView
mounts the clearly labeled expanded report; optional view replacement does not own
or relabel it. No alternate worker, reporting engine, save sidecar or Dock dependency
enters Workspace. The window icon now uses the same optimized seven-frame ICO as
the executable, enabling the native backend to choose small variants.

As of 2026-10-20, Stage 8D has passed user native Windows acceptance and one local
checkpoint is authorized after the fresh verification gate. Older pending statements
are historical. Stage 9 and approved-icon integration follow only after that gate;
they must remain uncommitted and stop for native acceptance. No external Git or
publication operation is authorized. The icon source is excluded from Stage 8D.

Stage 8D follows the completed Stage 8C checkpoint
ddc18ae63df7ddb31ba55ee35c3c6feffd0d3891. The checkpoint gate passed; older pending
authorization statements below are historical. Stage 8D remains uncommitted pending
native Windows acceptance, with no external publication authorization.

Core now produces a bounded immutable synthetic v2.5.1 batch from normalized fixed
options using a specified PRNG and canonical CR endings. Workspace generates in the
background, captures the destination/version, validates a prospective streaming save
under a transient mutation lock, then publishes one existing batch insertion/history
operation. Source bytes are neither decoded nor reconstructed. New output is privately
owned until ready and opens as independent unsaved inserted-origin messages.
Desktop owns only dialog configuration, lightweight identifier preview and post-dialog
activation/focus; it uses the same four logical positions and existing root history.
No dependencies, Dock schema, scripting API, original identity or save semantics change.
See [ADR 0012](decisions/0012-deterministic-synthetic-generation.md) and
[Stage 8D acceptance](stage8d-synthetic-generator-acceptance.md).

Stage 8C passed user native Windows acceptance on 2026-10-18, from the Stage 8B
checkpoint 16ebc6d2a2a9d36ddfb20c766976c18990846bb2. One local Stage 8C commit is
authorized; older pending/no-commit statements below are historical. Stage 8D is
authorized only after its mandatory checkpoint gate, and must remain uncommitted.
No push, tag, package, deploy, release or publication is authorized.
The UI delegates blank insertion to the accepted Workspace API through one Desktop
insertion workflow. File and editable in-memory tabs use the same stable document
host; logical identity drives view replacement. Filtered Next never appends.
Workspace exposes metadata-only insertion readiness and a retained-draft EOF guard;
source-boundary capability is cached because the original index is immutable.
Existing window-root history routing, save plans, identities and ownership remain.
The HL7 surface overrides only Paste, retaining clipboard characters through the
existing selection/update transaction with asynchronous target/lifetime guards.
See [Stage 8C acceptance](stage8c-message-insertion-acceptance.md). No new ADR is
needed: the structural and publication decisions in ADR 0011 still apply.

Stage 8B.1–8B.4, the window-root keyboard correction and Script Output Naming have
passed user native Windows acceptance as of the 2026-10-17 checkpoint request.
The user authorized one local commit only; older pending acceptance notes below
are historical. No Stage 8C/8D or push/tag/publish/release is authorized.

Script output naming adds a captured script-name default and an optional bounded
Batch.SetOutputFileName suggestion. Filename-only validation is shared in Scripting
and repeated by the parent at the worker boundary. An optional version-1 naming field
extends the version-2 Finished frame; lifecycle, payload validation and publication
remain unchanged. Workspace keeps the unsuffixed suggested Title and disambiguates
unsaved generated DisplayTitles; Desktop's existing Save As flow uses Title.
See [naming acceptance](script-output-naming-acceptance.md).

Stage 8B.1–8B.3 adds Workspace stable MessageIds, explicit File/Memory/Inserted
provenance, a bounded metadata-only logical sequence, document-wide chronological
history and structural save points. Original snapshots/indexes and lazy access stay
immutable. Save remains mutation-locked and asynchronous: a bounded plan streams
the original partition plus payloads, then the shared byte indexer and byte mapping
verify staged output before atomic publication. Associated destination baselines
now start at open or the last publication. See [ADR 0011](decisions/0011-structural-editing-foundation.md)
and [Stage 8B acceptance](stage8b-structural-editing-acceptance.md).
Stage 8B.4 replaces the temporary membership guards with stable-ID search results,
ID-based filters and comparison sources, plus immutable logical script captures.
Search copies metadata/bounded draft references and scans unchanged originals lazily.
Comparison retains unavailable selections through Undo and resolves the same ID on
Redo; Original still means the initial source. Script capture reuses the save plan's
partition/membership validation and records job-ordinal-to-ID/provenance mappings.
After capture, edits/history changes preserve independent output with explicit
stale-source attribution and its captured revision. Tab switches never retarget the
job. Closure, cancellation and failures still roll back through existing owners;
queued revision conflicts before capture are refused.
Diagnostics retain stable IDs, captured logical positions and original provenance
separately. Saved-search/library schemas and the isolated worker protocol are unchanged.
The foundation and root keyboard correction have passed native acceptance; 8B.4
native acceptance and Git authorization remain pending. No insertion UI, Next append,
generator, general deletion/reordering, new dependency or Dock schema is added.
See [8B.4 compatibility acceptance](stage8b4-compatibility-acceptance.md).

Stage 7 adds a dependency-free Core structural comparison engine, Workspace selected-message
captures/read leases, and an ordinary Desktop feature model with a document-sized Dock
tool adapter. Sources remain immutable and independent of the active/filtered navigator;
content revisions, cancellation and drain-before-disposal protect results and snapshots.
The Stage 7 refinement adds Workspace-owned temporary pasted input slots and immutable
string captures, with debounced Desktop updates. Differences/Side-by-Side are presentations
of one result/selection, not separate comparison sessions. Document sources remain read-only.
No source content/history is persisted. See [ADR 0010](decisions/0010-read-only-message-comparison.md)
and [comparison workflows](message-comparison.md). Native Windows acceptance is user confirmed.

Stage 5A adds Workspace -> Scripting -> Core and a separate ScriptHost -> Scripting
worker executable. ScriptHost alone references Roslyn's scripting/execution packages;
Desktop references the matching compiler-only package for background editor symbol
colors. Core remains dependency-free and no UI/Dock types enter Workspace/Scripting.
The Message-script implementation is
accepted on native Windows, including the final Script Editor UX refinements.
See ADR 0005 and `stage5a-script-editor-visual-acceptance.md`.
Batch/Generate are reserved mode values and rejected in this milestone.

The solution targets `net10.0`: Desktop -> Workspace -> Core (Desktop also directly
references Core). Core, Workspace, and Desktop each have corresponding test projects.
Core has no NuGet dependencies. Workspace has no UI or docking dependencies.
Desktop contains the accepted docking foundation and Viewer/editor Stages 1–4:
multi-message navigation, source-backed inspection, dockable tools, Activity Log,
bounded raw editing, undo/redo, dirty state and Save/Save As. Native Windows
acceptance is complete through Stage 4 (2026-10-15). The sections below record
the architectural evolution; the Stage 4 section describes the editable foundation.
Milestone 1 adds the parser/model, paths, editing, escapes, diagnostics, and
serialization to Core, with synthetic tests in Core.Tests. No desktop integration
or other milestone functionality was added during Milestone 1.

Feature view models remain independent of Dock classes. Introduce interfaces and
dependency injection when a concrete testing or substitution need exists. Generation,
transport, Synthea, and CLI projects remain deferred; Stage 5A's scripting projects
are described in ADR 0005.
Core libraries must remain UI-independent and cross-platform.

The application has no telemetry, cloud integration, or runtime network features.
No logging of message contents is configured. Samples and tests must be synthetic.
Git ignore rules are a convenience, not proof that data is safe to commit.

## Milestone 1: Parser and round-trip

`Hl7Parser` accepts an in-memory string or byte span representing one message.
Core performs no file I/O, transport framing, or multi-message indexing. Workspace
readers supply one bounded message at a time. The parser is delimiter-driven and has
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

## Milestone 2: Docking foundation (accepted 2026-10-13)

Dock.Avalonia wrappers live in Desktop/Docking, with ordinary feature content in
their Context. Workspace provides document lifecycle and active-message context,
the lazy tool registry, metadata-only layout schema, limits, and recovery policy.
Feature view models do not inherit Dock classes. Tool focus retains the selected
document; closing its tab switches to another open document or clears context.

Message Browser selects the synthetic tabs; Diagnostics follows their real parser
and minimal header diagnostics. Dock supplies draggable/resizable/floating tools.
Layout persistence and View menu reopening are application-coordinated. See
[ADR 0003](decisions/0003-dockable-workspace.md) and the
[desktop checklist](docking-acceptance.md). The foundation passed native Windows
acceptance on 2026-10-13.

At this foundation gate the workspace was read-only, and the plan deferred editing,
history, saving and dirty state to Milestone 3. The accepted viewer stages subsequently
added file sessions and structural inspection (Stages 1–2), source-backed multi-message
navigation (Stage 3), and editing/history/saving (Stage 4).
Reusable script storage and Script Editor/Library tools are future services; search,
comparison, generation, de-identification, MLLP, Synthea, and IRIS remain deferred.

Stage 1 file reading and session document creation live in Workspace; Avalonia
file picking, editor configuration, and Core-coordinate highlighting live in
Desktop. No version dictionary is required to render a structurally compatible
message. See [Stage 1 acceptance](viewer-stage1-acceptance.md).

Stage 2 adds `MessageInspection` on each Workspace document: UI-independent segment
entries, lazy field/repetition/component/subcomponent projections, raw ranges, and
session-only selection. Stage 2 rendered these in document-internal Avalonia grids
with splitters; Stage 3 subsequently promoted them to dockable tools. Selection
requests raw navigation; raw caret changes update structural context without
replacing user text selections. Attached
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
selection methods refuse stale operations; stale raw highlighting/hover/navigation
are also blocked. Controls retain enabled styling while input/event guards prevent
stale structural actions.
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
for verification and recorded native Windows acceptance.

## Unified C# scripting (Stage 5B, native acceptance granted 2026-10-16)

Scripting/ScriptHost use one lifecycle for both execution scopes: compile once,
initialize once, synchronous Main per selected message, optional Complete on success.
Roslyn syntax/semantic entry validation and one fixed continuation bind delegates
to the initialized submission, retaining run-level state with no per-message compilation.
Message resolves dynamically during Main; Batch exposes phase-checked reports,
Defer/Hold/Emit and bounded deferred-copy draining. Core remains
dependency-free and authoritative for structure, controlled writes and serialization.
Workspace reuses captured working snapshots and transactional output validation;
interleaved CSV rows stream into a separate bounded snapshot journal. Both artifacts
roll back until the complete job validates. Completed reports are explicitly exported
after document publication, with flushed temporary files and version-scoped replacement
only after explicit user authorization. HL7 Save As, CSV and original-source exports
share Workspace's FileSaveDestination publication path; ordinary associated Save requires
no repeated confirmation. See [save/export safety](save-and-export-safety.md) for native
provider evidence, destination checks and platform limits.
Export read leases defer snapshot cleanup until readers drain. No cross-resource
atomic transaction is claimed. Desktop provides one New Script action, Apply to: scope
selection and report export with existing docking, font, coloring and draft ownership.
Session-only last-successful-run attribution records script name/scope at start
and changes only with successful report replacement. API/protocol version 2 refuses
obsolete clients. Library schema 3 omits kind and retains a migration warning flag;
version-1/2 records are blocked as obsolete without load-time writes until explicit Save.
See [ADR 0007](decisions/0007-unified-csharp-scripting.md) and [script API](script-api.md).

## Desktop command surfaces and preferences (Stage 5B finishing)

The permanent menu and an Avalonia utility ribbon sit above Dock. Collapsed ribbon
commands use a light-dismiss popup; only deliberately pinned commands consume Dock
height. No new dependency or window-layout schema is introduced. Existing Home/HL7
actions are reused; TCP/MLLP buttons are disabled Planned placeholders without services.
UiCommand adapters on the retained ScriptEditorViewModel serve menu, ribbon and editor.
Decision/export/run state gates every surface. Dock still owns placement only; reopening
or floating reconstructs controls over the same draft and report journal.

The compact editor keeps two rows and moves secondary commands into ⋯. Inline rename
confirms a validated draft name; explicit Save persists name/code atomically. Delete
uses ScriptLibrary's lock/revision guard, dirty consent and a named confirmation;
failure/cancel retains the draft, success selects a bundled fallback. Output documents,
exported files and report provenance are independent. A contextual reports window reuses
the existing export service; journal replacement is blocked throughout the native picker.

Workspace's ApplicationPreferences publishes only successfully persisted EditorPreferences.
The existing per-user JSON gains RibbonExpanded and reserved schema-mode/target metadata,
preserving valid unknown fields. The General/HL7 Preferences window uses Apply/OK/Cancel;
the existing Edit Mode checkbox and pin/unpin share that state. Only newly opened documents
receive the edit default. Core exposes declared MSH-12 but has no schema interpreter or
validator: schema controls are disabled and never drive parsing, writes or diagnostics.
See [settings contracts](desktop-preferences.md) and [native acceptance](stage5b-ui-finishing-acceptance.md).
