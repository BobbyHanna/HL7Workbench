# Synthetic fixtures

Only explicitly reviewed, entirely invented HL7 fixtures belong here. Review
every fixture before staging it; the Git ignore exception does not establish
that a file is synthetic. Record its purpose and review in the accompanying
test or change description. Never copy real patient messages into this folder.

The Milestone 1 fixtures were independently authored and reviewed as entirely
synthetic during the hardening pass:

- `adt-a01.hl7`: admission, repeated identifiers/assigning authorities, provider
  subcomponents, explicit-null name suffix, and trailing empty Z-segment fields.
- `orm-o01.hl7`: two order groups with matching ORC/OBR identifiers and notes.
- `oru-r01.hl7`: two result groups, numeric/repeated coded results, formatted notes,
  an ED/Base64 value containing only `SYNTHETIC-ATTACHMENT`, and explicit null.
- `ack-a01.hl7`: synthetic acknowledgement and control-ID correlation.
- `oru-custom.hl7`: independently authored custom-delimiter result groups,
  literal delimiter/truncation escapes, and the same invented attachment.
- `oru-ambiguous.hl7`: deliberately malformed escapes in one result, followed
  by an independently editable result.

These fixtures exercise syntax and preservation, not clinical/profile conformance.
Tests also create CR/LF/CRLF/mixed and UTF-16 variants in memory. Core has no file I/O.
