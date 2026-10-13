# Architecture

The solution has three projects targeting `net10.0`:
Desktop -> Core, and Core.Tests -> Core. Core has no NuGet dependencies.
The desktop contains only application startup, a theme, and a static window.
Milestone 1 adds the parser/model, paths, editing, escapes, diagnostics, and
serialization to Core, with synthetic tests in Core.Tests. No desktop integration
or other milestone functionality has been added.

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
