# Architecture

The solution targets `net10.0`: Desktop -> Workspace -> Core (Desktop also directly
references Core). Core, Workspace, and Desktop each have corresponding test projects.
Core has no NuGet dependencies. Workspace has no UI or docking dependencies.
Desktop now contains the Milestone 2 docking foundation with synthetic documents.
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
