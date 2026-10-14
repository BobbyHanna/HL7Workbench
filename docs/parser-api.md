# Parser API, preservation, and limits

All types are in `HL7Workbench.Core`. Parsing is independent of Avalonia, files,
network services, and terminology dictionaries. No dependencies were added.

```csharp
using HL7Workbench.Core;

// Entirely synthetic in-memory example; callers/readers own file I/O.
var message = Hl7Parser.Parse(
    "MSH|^~\\&|SYN|TEST|APP|TEST|20261013120000||ADT^A01|SYN-1|T|2.5\r" +
    "PID|1||SYN-001^^^TEST^MR||Example^Ada\r");

Hl7Value identifier = message.Get("PID-3.1");
message.SetText("PID-5.2", "Synthetic|Name");
message.SetRaw(new Hl7Path("MSH", 10), "SYN-2");
message.SetNull("PID-5.4"); // Intentional null, distinct from empty text.
string er7 = Hl7Serializer.SerializeText(message);
byte[] output = Hl7Serializer.Serialize(message);
var validation = Hl7Validator.Validate(message);
```

`Get`/`GetDecoded`/`SetRaw`/`SetText`/`SetNull` accept string or typed paths.
`GetDecoded` returns text plus escape diagnostics and throws for a missing value.
`Get` exposes raw content and its missing/empty/null/value state. See
[path grammar](hl7-paths.md) for selection rules. Supported escape decoding is
explicit: F/S/R/T/E, P when a truncation character is declared, and hexadecimal
X byte sequences using the selected encoding.
Unsupported formatting, local, character-set, and malformed escapes remain
verbatim with diagnostics. Decoding never changes the message. MSH encoding
characters are literal and are not passed through the escape decoder.

## Encoding and byte preservation

- Byte input is defensively copied. UTF-8, UTF-16 LE/BE, and UTF-32 LE/BE BOMs
  select encoding; otherwise use the caller's encoding or strict UTF-8.
- An explicitly supplied Unicode encoding's matching canonical BOM is checked
  first, including when that Encoding instance is configured not to emit a BOM.
  Explicit UTF-16 LE interprets `FF FE 00 00` as its two-byte BOM followed by NUL.
  Without an explicit matching encoding, automatic detection uses the longest
  recognized BOM first and interprets that prefix as UTF-32 LE.
  A supplied encoding conflicting with another recognized BOM remains an
  argument error; unrelated BOMs are not silently stripped or converted.
  No code-page package is registered automatically; callers supply available
  encodings. MSH-18 does not select or convert byte encoding.
- Strict decoder/encoder fallbacks prevent silent replacement. Undecodable bytes
  yield `InvalidEncoding`, no structure, and `HasDecodedText == false`.
  Byte serialization still returns the original bytes. Text access and edits
  are disabled, so invalid data is never represented as an empty message.
- Every accepted unmodified byte input serializes byte-for-byte, including
  malformed lines, mixed endings, blank lines, trailing delimiters, unsupported
  escapes, BOM-only data, and absent final terminators.
- Edited byte serialization supports UTF-8, UTF-16 LE/BE, and single-byte
  encodings. Changed spans are encoded strictly; unchanged bytes are copied.
  Splice boundaries never split UTF-16 surrogate pairs. Unrepresentable edits
  fail atomically.
- Other decodable encodings, including UTF-32, support inspection and unchanged
  byte serialization; `UnsupportedEditEncoding` explains why editing is disabled.
- String input has no original-byte guarantee or implicit BOM. It must be
  encodable with strict fallback, retains exact characters, and serializes using
  the selected encoding without adding a preamble. `HasOriginalBytes` is false.
- `OriginalBytes`, `ByteOrderMark`, serialization results, and `Encoding` return
  defensive copies. `IsModified` records successful non-no-op edits; reverting
  an edit does not automatically reset the flag.

## Recovery and validation

The first MSH supplies delimiters. MSH-2 may contain two through five characters:
component/repetition are required; escape/subcomponent may be omitted, as allowed
by older versions, and optional truncation occupies position five. Omitted optional
characters are null and do not split data. Declared delimiters must be distinct
printable non-whitespace ASCII characters. Truncation spelling is retained;
literal text writes escape it with P. Profile-dependent truncation validation is
not implemented. Missing MSH uses standard delimiters with `MissingMsh`.
Invalid definitions use provisional standard-delimiter structure and disable
editing. Multiple MSH headers are retained and diagnosed, with editing disabled;
the caller must separate messages before parsing.

Well-formed escape pairs suppress structural splitting inside the token, even
for unsupported local escapes. An unmatched escape remains literal and does not
hide subsequent structural delimiters or cross a segment terminator. This is a
documented recovery interpretation, not a repair of the source. If an escape pair
spans structural delimiters, the segment receives `AmbiguousEscape` and
`CanEdit == false`. Parsed coordinates are provisional; original content is
preserved. This conservative rule also applies to locally defined escapes with
raw delimiters, whose semantics Core cannot establish.

Malformed segment identifiers or separator placement produce opaque segments
with zero fields. Their raw spelling/order remains intact. A bare three-character
segment has zero fields; `PID|` has one present empty field. Parsing does not
require message-type, version, or patient segments. `Hl7Validator.Validate`
separately checks empty messages and commonly required MSH-9/MSH-10/MSH-12 values;
it is not full version/profile validation. Diagnostics have static descriptions
and coordinates without imported values. Offsets are zero-based decoded UTF-16
character offsets excluding the BOM, not byte offsets. Edits are also checked
after candidate parsing to ensure the requested value resolves correctly and
other fields retain their structure. A new escape token that would pair with an
existing unterminated escape and reinterpret neighboring content is rejected
atomically; no automatic escape repair is attempted.

## Resource limits

Configure `Hl7ParseOptions` per parse. All limits must be positive except edit
expansion, which may be zero. The options also govern later edits.

| Option | Default | Scope |
| --- | --- | --- |
| `MaxMessageBytes` | 16 MiB | Input bytes including BOM; encoded text and edited output |
| `MaxMessageCharacters` | 8 Mi UTF-16 code units | Decoded message and candidates |
| `MaxSegmentLength` | 1 Mi UTF-16 code units | Each segment excluding its terminator |
| `MaxSegments` | 100,000 | Lines, including retained blank/opaque lines |
| `MaxElements` | 250,000 | Total fields, repetitions, components, subcomponents |
| `MaxElementsPerContainer` | 10,000 | Fields per segment and children per element |
| `MaxEditExpansion` | 1,024 | Sum of missing siblings/placeholders added along a path per edit |
| `MaxDiagnostics` | 1,024 | Stored diagnostics including the suppression notice |

Edits first create a plan of offsets and separator counts (at most four levels).
Projected full-message characters, segment characters, encoded bytes including
the retained BOM, container indices, and expansion budgets are checked before
encoding literal replacements, allocating padding, or constructing candidate
strings. Nested expansion uses the same plan. Hierarchy limits are checked while
building the candidate model before publication.
Raw replacements introducing new structure are additionally bounded by the
total/per-container element limits during candidate parsing. A limit breach
throws `Hl7LimitException` identifying the limit; partial models are not exposed.
Diagnostic storage is capped rather than throwing: when more records arrive,
the last available slot becomes `DiagnosticsSuppressed`. Safety flags, including
segment editability, are independent of the retained diagnostic list. The escape
codec applies the same storage policy. Rejected parse input remains owned by the caller. Failed edits leave bytes,
structure, diagnostics, and dirty state unchanged. Extreme paths are safe for
reads (missing) and rejected on expansion. Escape codec output/storage limits
also apply when it is used independently.

`Hl7Element.CharacterOffset` exposes the retained model's decoded UTF-16 start
coordinate for presentation consumers. It excludes the BOM. Coordinates remain
provisional for malformed/ambiguous recovery; consumers must not infer semantic
validity from them. Reacquire snapshots after any Core edit.

## Current limitations

This is a bounded, eager, in-memory parser, suitable as the per-message component
of future streaming readers; it is not itself a streaming reader. Rebuilding
candidate models favors correctness over allocation/performance optimization.
Message instances are mutable and not thread-safe. Model collections/elements
are immutable snapshots: reacquire them after edits.

No file parsing, MLLP framing, batch messages, automatic delimiter conversion,
segment insertion/deletion, normalization, HL7 dictionary, profile certification,
attachment interpretation, or automatic de-identification is implemented.
Future consumers must enforce their own batch/concurrency limits. Unexpected
version strings and unknown segments are preserved without dictionary checks.

## Hardening compatibility changes

- `Hl7Delimiters.Escape` and `.Subcomponent` are now `char?`. Constructor calls
  supplying characters still compile, but consumers requiring `char` must check
  for absence. Generated constructor/deconstruction signatures change.
- `Hl7Segment.CanEdit` is public, allowing scripts/editors to detect ambiguous
  or opaque segments without depending on a possibly suppressed diagnostic.
- `SetText` guarantees literal two-quote values do not become explicit null.
  Scripts intending deletion should use `SetNull` or `SetRaw` instead.
  Candidate decoded text is checked against the literal input before publication;
  unusual delimiter/code collisions that cannot represent it are refused atomically.
- Diagnostic storage overflow no longer throws `Hl7LimitException`. Structural,
  size, and expansion limits remain hard failures. The suppression notice uses
  a slot within the configured maximum, including when the maximum is one.
- The original three-argument `Hl7EscapeCodec.Encode` overload is retained;
  an encoding-aware overload supports literal quote escapes.
- Non-ASCII declared delimiters are diagnosed as invalid rather than interpreted
  as structural UTF-16 code units; original content remains losslessly recoverable.
