# HL7 path grammar

```text
path         = segment [ "[" index "]" ] "-" index
               [ "[" index "]" ] [ "." index [ "." index ] ]
segment      = three uppercase ASCII letters/digits
index        = positive ASCII decimal integer, at most Int32.MaxValue
```

Paths are case-sensitive, have no whitespace, and are at most 64 characters.
Leading zeroes are accepted and removed by canonical formatting.
Every coordinate uses one-based indexing. The segment occurrence is counted
among segments with the same identifier, in message order. Omitted occurrence
means 1. The number after `-` is the HL7 field number; MSH-1 is the separator,
MSH-2 is the encoding-character field, and MSH-3 is the sending application.
MSH-1/MSH-2 are literal values with one literal hierarchy branch each.

| Path | Selection | Canonical form |
| --- | --- | --- |
| `PID-3` | Entire field 3, including all repetitions | `PID[1]-3` |
| `PID-3[1]` | First repetition of field 3 | `PID[1]-3[1]` |
| `PID-3.1` | First component of first repetition | `PID[1]-3[1].1` |
| `PID-5.2` | Second component of first repetition | `PID[1]-5[1].2` |
| `PID[2]-3[2].1.1` | Second PID, second repetition, first component/subcomponent | Same |
| `OBX-5` | Entire field 5 of first OBX | `OBX[1]-5` |

An omitted repetition means 1 when a component is selected. A subcomponent
requires a component. A field-only path intentionally selects the entire field,
which differs from selecting its first repetition. Public node collection
indexers are ordinary zero-based .NET indexers; path coordinates and
`Hl7Segment.GetField(number)` are one-based.

`Get` returns `Hl7Value` with four states:

- `Missing`: segment/field/repetition/component/subcomponent does not exist;
  `Raw` is null.
- `Empty`: the selected element exists with zero characters; `Raw` is empty.
- `ExplicitNull`: the entire selected raw value is exactly `""` (two quotes).
- `Value`: every other spelling, including whitespace and literal escape tokens.

No state is normalized. An empty field has one empty repetition/component/
subcomponent branch, so its first branch is present and empty; subsequent
branches are missing. A null marker within a composite is explicit null only
when that exact branch is selected. `SetRaw(path, "")` leaves a present empty
element; `SetNull(path)` or `SetRaw(path, "\"\"")` writes explicit null. No removal API is provided
in this milestone.

Writes select the same scope as reads. A whole-field raw write may contain
repetitions/components/subcomponents, a repetition write may contain components,
and a component write may contain subcomponents. Unescaped higher-level delimiters,
CR/LF, and unclosed escapes in replacement values are rejected. `SetText` escapes
literal structural delimiters and configured truncation characters before writing.
When the entire literal value is two quotes, `SetText` writes a hexadecimal
escape in the selected encoding, preserving literal text without a deletion
marker. Without a declared escape character, such literal writes are refused;
deliberate null writes remain available through `SetNull`.

Missing nested elements or fields within an existing structured segment are
expanded using empty placeholders subject to configured limits. Missing segment
occurrences are never created implicitly. MSH-1/MSH-2 are read-only; malformed
opaque or ambiguous-escape segments cannot be edited. `Hl7Segment.CanEdit` exposes
this distinction even when diagnostics are capped. Other valid segments may be edited while
opaque lines are retained. Invalid delimiter definitions or multiple MSH headers
disable editing for the entire message.

Older messages may omit escape/subcomponent characters in MSH-2. These are
represented as null rather than invented defaults. A missing subcomponent
delimiter permits the implicit first subcomponent only; writes requiring a second
subcomponent are refused. Literal delimiter writes requiring escaping are refused
if no escape character was declared.
