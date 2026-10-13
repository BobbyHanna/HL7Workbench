# Architecture

Milestone 0 establishes three projects targeting `net10.0`:
Desktop -> Core, and Core.Tests -> Core. Core has no NuGet dependencies.
The desktop currently contains only application startup, a theme, and a static
window. There are no view models, service containers, parser types, or domain APIs.

Future desktop behavior will use MVVM. Introduce interfaces and dependency
injection when a concrete testing or substitution need exists. Generation,
scripting, transport, Synthea, and CLI projects will be added in their milestones.
Core libraries must remain UI-independent and cross-platform.

The application has no telemetry, cloud integration, or runtime network features.
No logging of message contents is configured. Samples and tests must be synthetic.
Git ignore rules are a convenience, not proof that data is safe to commit.

## Proposed Milestone 1 (not implemented)

Retain original message bytes, encoding information, and segment terminators
separately from delimiter-driven parsed structure and validation diagnostics.
Define an explicit supported encoding policy before promising byte preservation.
Handle MSH-1/MSH-2 and MSH field indexing explicitly. Preserve empty and trailing
values, unknown segments, repetitions, and unsupported escapes. Parsing should
tolerate unexpected content; validation reports it separately.

Define canonical paths such as `PID[1]-3[2].1.1`, with documented occurrence and
repetition indexing and friendly aliases. Unmodified serialization returns the
original bytes; edits must preserve unaffected content. Normalization is opt-in.

Synthetic tests should cover CR/LF/CRLF and mixed terminators, BOM/encoding policy,
custom separators, MSH indexing, repeated fields/components/subcomponents,
empty/trailing fields, unknown/Z-segments, missing segments, supported and
unsupported escapes, path reads/writes, and exact unmodified byte round trips.
Document unsupported cases and use failing tests to resolve ambiguous behavior.
No parser or fixtures for these cases are included in Milestone 0.
