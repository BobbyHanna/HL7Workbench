# ADR 0002: Bounded, lossless ER7 parsing and edits

Status: Accepted for Milestone 1 by user approval.

## Decision

Use an independently written, delimiter-driven Core implementation with no new
NuGet packages or UI/file/network dependencies. Public read-only nodes represent
segments, fields, repetitions, components, and subcomponents. Paths provide a
stable scripting/editing API with one-based coordinates and explicit raw versus
escaped-text writes. Missing, empty, and explicit-null values stay distinct.

Retain original bytes, encoding/BOM information, raw spellings, and per-segment
terminators. Unmodified byte serialization is exact even for malformed content.
Supported edited encodings splice changed spans into retained bytes. Strict
encoding failures disable unsafe interpretation or reject edits, never replace
characters. String input retains characters but cannot claim original bytes.

Handle MSH-1/MSH-2 literally. Freeze delimiter definitions in this milestone.
Parsing tolerates missing headers and opaque malformed lines with diagnostics;
ambiguous delimiters or multiple MSH headers disable editing. Keep validation
separate and minimal rather than pretending to certify version/profile conformance.
Unsupported escapes are retained with diagnostics. Unclosed escapes do not
suppress subsequent separators; synthetic regression tests fix this interpretation.

Bound bytes, characters, line lengths/counts, elements, diagnostics, and edit
padding. Hard size/structure failures throw a typed exception instead of returning
unbounded partial structures. Diagnostic storage saturates with a suppression
notice. Edits preflight character and byte budgets using an offset/padding plan
before constructing replacement strings. Candidate edits are checked before publication,
including a check that new escapes cannot reinterpret malformed neighbors.

## Consequences

Copies, eager hierarchy construction, and reparsing each edit use more memory/CPU
than a lazy or incremental parser; configured limits make that cost bounded and
the implementation reviewable. Message objects are not thread-safe; node snapshots
must be reacquired after mutation. Unsupported edit encodings remain inspectable
and recoverable as original bytes. BOM-less input defaults to UTF-8 rather than
guessing MSH-18 or legacy code pages.

The API accepts individual in-memory messages so future streaming readers can
own framing, file I/O, and batch bounds without changing Core's responsibilities.
See [API contracts and limitations](../parser-api.md) and
[path grammar](../hl7-paths.md). Desktop integration remains Milestone 2.

## Hardening decisions

Represent omitted older MSH-2 escape/subcomponent characters as nullable characters
instead of inventing defaults. An explicit Unicode encoding's matching BOM takes
precedence over automatic longest-BOM detection. Encode configured truncation
characters with P in literal writes. Literal two-quote values use encoding-aware
hexadecimal escapes; intentional null writes use SetNull or SetRaw.

Escape pairs spanning raw structural delimiters have ambiguous coordinates and
make their segment read-only, including unknown local escapes. This favors safe
preservation over permissive edits. Safety flags remain available even when
diagnostic descriptions are suppressed. Six independently authored synthetic
fixtures cover ADT, ORM, ORU, ACK, custom delimiters, and ambiguous escapes;
tests verify both structure and exact edited bytes.
