# User guide

Message Workbench opens and analyzes local HL7 v2 data, edits bounded messages,
runs trusted C# scripts, generates synthetic HL7, validates against explicit profiles,
imports supported local FHIR R4 Bundles and tests TCP/MLLP interfaces.
Use authorized data and test destinations. Start with the repository's
[reviewed synthetic fixtures](../../samples/synthetic-only/README.md).

## Start here

1. [Install or run the application](installation.md).
2. [Open, inspect, edit and save a synthetic file](getting-started.md).
3. [Arrange tools and recover hidden windows](workspace.md).
4. Read [privacy/storage](privacy-and-storage.md) and [limits/recovery](troubleshooting.md).

## Choose an analysis workflow

| Your goal | Choose | Output and source behavior |
| --- | --- | --- |
| Find messages containing text or matching field/segment conditions | [Find Messages](search-and-filter.md) | Navigates/filters the existing document; no transformed output. Literal search and bounded ALL/ANY groups. |
| Make a configurable field table and CSV | [Field Extraction](extract-fields.md) | Captured values, state distinctions, filters and sorting; exports the complete applied result selection across pages. |
| Join/group/count data across current or selected open documents | [SQL Query](query-with-sql.md) | Local read-only SQLite; complete-result CSV or independent matching-message document. No external database connection or SQL writes. |
| Change messages or apply a stateful custom policy | [Trusted C# scripting](run-and-manage-scripts.md) | Independent unsaved output and optional reports; account permissions apply. Review code before Run. |

Search paths examine all applicable occurrences/repetitions and do not accept
bracket indexes. Extraction and SQL/Core path functions have their own explicit
index rules. Follow each guide rather than transferring one tool's semantics to another.

## Work with messages

- [Inspect, edit, insert messages and preserve source](inspect-and-edit.md).
- [Compare pasted, original and working messages](compare-messages.md).
- [Generate standalone or connected synthetic HL7 and replay seeds](generate-synthetic-hl7.md).
- [Apply schemas and manage custom profiles](validate-with-profiles.md).
- [Import local FHIR and generate supported HL7](import-fhir-and-generate-hl7.md).

## Test interfaces

- [Send one message or receive with the MLLP listener](test-with-mllp.md).
- [Run a reviewed sequential batch or controlled load test](batch-and-load-tests.md).

Opening a tool does not authorize a transmission. Socket write completion, ACK
receipt and application acceptance are different outcomes.

## Reference and recovery

- [Preferences and shortcuts, including focus behavior](preferences-and-shortcuts.md).
- [Storage/retention matrix](privacy-and-storage.md#storage-and-retention).
- [Limits and coverage](troubleshooting.md#limits-and-coverage).
- [Troubleshooting](troubleshooting.md) and [generated Script API Reference](../script-api-reference.md).
- [Documentation home and developer references](../index.md).

These guides cover current source functionality, not a new release approval.
[Installation](installation.md) distinguishes that source tree from the older frozen preview.
