# Parser, preservation and coordinate invariants

Core's [Hl7Parser](../../src/HL7Workbench.Core/Hl7Parser.cs) and
[Hl7StructureScanner](../../src/HL7Workbench.Core/Hl7Structure.cs) share the structural
grammar. The materialized API handles one bounded message; Workspace owns files,
framing and large-source access. Use [parser-api](../parser-api.md) and
[path grammar](../hl7-paths.md) for exact public contracts.

## Preservation and malformed input

Unmodified byte input serializes byte-for-byte, including malformed/unknown lines,
Z segments, mixed CR/LF/CRLF, blank lines, trailing separators, unsupported escapes
and missing final terminators. String input preserves characters but cannot claim
original bytes. Never normalize a malformed message to make inspection or saving
easier. Structural diagnostics and schema validation are separate assessments.

MSH-1/MSH-2 define literal delimiters and special numbering. Older omitted optional
escape/subcomponent characters stay absent, not invented. Invalid delimiters,
multiple MSH headers or ambiguous escape structure disable unsafe edits while
retaining source. Unknown well-formed escape pairs suppress splitting within their
token; unmatched escapes remain literal and do not swallow later separators.
Safety/editability flags do not depend on whether diagnostic descriptions were capped.

Missing, present empty, exact explicit-null `""` and ordinary values are distinct.
One-based paths differ from zero-based .NET collection indexers; whole-field selection
differs from first repetition. `SetText` escapes literal delimiters; `SetRaw` must
respect its selected structural scope. Missing fields/nested positions can expand
within budgets; missing segment occurrences are not implicitly created. MSH-1/2
are read-only in controlled writes. Mutable models are not thread-safe; reacquire
node/collection snapshots after editing. Do not mutate a materialized Core model
behind Workspace and expect document notifications/revision guards to run.

## Encoding and BOM

Without an applicable BOM or caller encoding, decoding uses strict UTF-8; MSH-18
does not choose or transcode bytes. Automatic BOM detection prefers the longest
recognized prefix. An explicitly supplied Unicode encoding's matching BOM is
checked first, including encodings configured not to emit a preamble. A conflicting
recognized BOM is an error, not a silent conversion.

Decoder/encoder replacement fallbacks are not allowed. Undecodable bytes remain
serializable as originals but do not become empty text. Edits support UTF-8,
UTF-16 LE/BE and supported single-byte encodings with strict splicing; other decodable
encodings can remain inspectable/read-only. Do not split surrogate pairs at edited
or mapped boundaries. Literal two-quote text and deliberate HL7 null use different
write semantics. [Hl7SourceEncoding](../../src/HL7Workbench.Core/Hl7SourceEncoding.cs)
and [Hl7EscapeCodec](../../src/HL7Workbench.Core/Hl7EscapeCodec.cs) own those rules.

## Coordinate origins

| Coordinate | Origin and use |
| --- | --- |
| Snapshot/file byte range | `MessageFileIndex` SourceRange/PayloadRange and Regions are offsets into the complete immutable snapshot. SourceRange can include framing; PayloadRange identifies payload. File-level BOM is a separate retained region. |
| Original message-local bytes | Structural source ByteRange mappings refer to the original payload origin used by Workspace, including its explicit BOM accounting. Add PayloadRange.Offset only when the mapping is documented as payload-local. |
| Message-local UTF-16 | CharacterRange/model CharacterOffset indexes decoded message text, not bytes. BOM is not a decoded display character. |
| Source-viewer scope offset | Relative to the selected message/value scope; map through its scope/message offsets before resolving structure or export. |
| Editor/display offset | Relative to a current complete editor string or a mapped preview fragment/window. Omission markers and other presentation text have no original range. |
| Logical position / original ordinal / MessageId | Position is current order, original ordinal is nullable provenance, MessageId is stable identity. None is a byte/character offset or MSH-10. |

Do not compute byte positions by multiplying UTF-16 offsets or by encoding a bounded
preview. [DecodedMessageSource](../../src/HL7Workbench.Workspace/DecodedMessageSource.cs)
uses strict spools/checkpoints and exact mappings; byte offsets include explicit
local BOM treatment. [MessagePresentation](../../src/HL7Workbench.Workspace/MessagePresentation.cs)
maps source fragments into abbreviated display intervals. A preview/omission label
is never an authoritative value, save source or export source. Attachment decoding
is never implicit.

## Budgets and regression expectations

Inspector structured edits add no parsing or serialization dialect. Workspace
validates raw replacement through Core SetRaw in an isolated bounded model, verifies
the result equals the captured span splice, then uses document-owned text history.
Existing-node eligibility prevents Core's missing-position expansion from being
invoked by this UI. Field/repetition/component/subcomponent delimiters follow Core's
scope rules; control characters and unsupported editing bounds are rejected before
publication. Original bytes/BOM, snapshot coordinates and save plans remain immutable.

Materialization, structural scanning, literal decoding, editing and presentation
have different budgets; [shared limits](../reference/limits.md) link each owner.
Size/structural failures throw typed limit errors without partial editable models;
diagnostics saturate with a suppression notice. Rejected controlled edits leave
bytes/model/dirty state unchanged. Workspace raw drafts may temporarily be malformed
or exceed the line/structure budget while still bounded in total text; publication
must reject unsafe drafts rather than discard them.

Before changing grammar/ranges, cover custom/older delimiter sets, malformed escape
neighbors, all supported BOM/encoding cases, multibyte/surrogate boundaries, missing/
empty/null, diagnostic saturation and exact untouched/edited bytes. Evidence:
[encoding tests](../../tests/HL7Workbench.Core.Tests/EncodingAndRoundTripTests.cs),
[hardening tests](../../tests/HL7Workbench.Core.Tests/HardeningTests.cs),
[scanner tests](../../tests/HL7Workbench.Core.Tests/StructureScannerTests.cs),
[path/edit tests](../../tests/HL7Workbench.Core.Tests/PathAndEditingTests.cs) and
[source-backed tests](../../tests/HL7Workbench.Workspace.Tests/Stage3SourceTests.cs).
ADRs [0002](../decisions/0002-lossless-parser.md) and
[0011](../decisions/0011-structural-editing-foundation.md) govern preservation/identity.

## Bulk selectors and safety metadata

Bulk selectors traverse Core scanner nodes, preserving selected field versus indexed
repetition/component/subcomponent semantics. Optional expansion changes only segment
occurrences. Exact raw strings plus value state group complete values; preview text
never participates. Present nodes and absent address slots are counted separately.

Hl7Structure.HasUnambiguousMessageStructure exposes the scanner's existing message
header/delimiter safety flag independently of encoding edit support; segment CanEdit
is still required. CanEdit and scanner grammar remain unchanged. Do not infer safety
from capped diagnostics. This permits read-only UTF-32 analysis without enabling
UTF-32 edits. Each changed bounded message is validated by existing Core SetRaw;
nonoverlapping captured ranges must produce exactly the intended span splices.
No absent-node expansion or new parser/serializer is introduced.
