# Troubleshooting and supported limits

Start with the tool's status, source freshness and Activity Log. Prerequisites for
reproduction: note public version/provenance, operation, Windows version, theme and
scaling; use synthetic input. Do not include patient values, files, screenshots,
dumps or paths containing sensitive information in feedback.

## Recover safely

1. Cancel the active operation using its visible control and let it drain. Stop
   network tools explicitly; cancellation after a write starts can leave delivery uncertain.
2. Keep unsaved drafts. Record fixed error/status codes and the chosen scope/settings.
3. Check the relevant case below. Retry only after correcting the cause; network
   resend requires new review and a decision based on receiver evidence.
4. Reproduce with [reviewed synthetic fixtures](../../samples/synthetic-only/README.md)
   before sharing steps. Preserve existing originals/configuration and useful diagnostics.

| Symptom | Check and safe next step |
| --- | --- |
| Application or worker unavailable | Keep the whole approved extraction/build, including ScriptHost and (for current source) QueryHost. Check SDK/build for source runs. Do not download loose DLLs or disable security software. See [installation](installation.md). |
| Hidden/off-screen tool | Reopen through View; Restore Layout or Reset Layout / Recover Windows from Home → Workspace. Save valuable tool drafts first; never delete all settings/documents as a shortcut. |
| Edit Mode on but no editor access | Message may exceed complete editor/line limits, lack safe decoding or use unsupported editing encoding. Inspect through source-backed tools; choose a smaller supported message. |
| Inspector temporarily shows previous data | Wait for debounced parsing. Pending/Stale labels prevent old coordinates from being used as current structure. Malformed drafts remain retained. |
| Inspector field missing from the tree | Enable Show Blank Fields. Absent schema fields have no source span; unknown/custom segments have structural labels rather than invented clinical definitions. |
| Inspector Copy Value or Filter unavailable | Select a current row. Copy requires a complete value within 128 Ki characters; filtering requires at most 1,024 characters and one repetition. Use source viewing/export for oversized originals. |
| Inspector Edit Value unavailable | Enable the owning document's Edit Mode. Require a current, complete editable message and an existing span. Absent nodes, MSH-1/MSH-2, ambiguous structure and source-backed messages stay unavailable. |
| Inspector replacement rejected or target changed | Review raw encoding, closed escapes, separators allowed at the selected level, control characters and bounds. A changed document/message/selection or raw edit invalidates the staged target; Cancel and reopen. Rejected input creates no undo entry. |
| Content font does not follow the global setting | Focus that tool and press Ctrl+0 to remove its saved local override. Apply Appearance changes; modal configuration dialogs and ribbon/menu chrome keep normal sizing. |
| Ctrl+Wheel zooms a different tool than keyboard zoom | Wheel zoom follows the pointer; Ctrl+Plus/Minus/0 follows keyboard focus. Move over the intended content. Menus, command controls, dialogs and horizontal/modified scrolling are excluded. |
| Save refused or failed | Keep draft/history. Check incomplete inserted messages, unsafe framing/boundaries, strict encoding, access, disk space and external destination changes. Complete/undo the draft or choose a safe Save As destination. |
| Full Source Viewer unavailable or closes | Edited/inserted messages lack usable original ranges. Save/reopen for a new original; same-file message navigation ends the borrowed viewer session. Reopen for the intended message. |
| Search finds nothing | Clear navigator filters, check active document/path/case/raw escape spelling and skipped counts. Search has no bracket indexes, regex or numeric coercion; rerun after edits/tab switches. |
| CSV has more rows than the page/highlight | SQL exports all journal rows; extraction exports its complete applied selection. Highlighting is not an export filter. Check scope before writing. |
| Long cell is abbreviated | Table ellipses are previews. SQL offers Inspect full value/copy within bounds; CSV reads complete stored values. A diagnosed/limited value is not a verified empty value. |
| SQL limit or unavailable value | Try a narrower source or Messages/header query. Structural imports can hit budgets before WHERE/LIMIT; substr does not bypass materialization. No partial result is published. |
| SQL Go to source disabled | Require authoritative document/message identity and an unchanged open source. Stale captures can still export/extract captured bytes; they cannot navigate a replacement document. |
| Script fails or is read-only | Duplicate a built-in before editing; check Main/Complete phase rules, scope and diagnostics in the [API reference](../script-api-reference.md). Run only trusted code; arbitrary side effects are not rolled back. |
| Generator repeats data | Explicit seed/settings/style/version/base time intentionally replay. Blank synthetic seed gets a fresh operation seed; Copy seed exposes the actual value. FHIR seed only affects transport IDs. |
| Validation has zero errors but Incomplete | Coverage/budget gaps remain. Built-ins are scoped v2.5.1 models, not conformance certification. Select/reapply the appropriate profile and review notices. |
| Profile import/update fails | Use the supported JSON/XML subset, valid inheritance and current saved revision. Keep existing definitions and review status; import does not automatically apply. |
| FHIR generation disabled/omits events | Check scope, explicit selections, eligible dates/statuses, resolved relationships and preview limits. Empty Selected Events never falls back to all records. |
| MLLP write completes but no accepted outcome | Inspect ACK phase/correlation, timeout and receiver state. TCP completion/CA/ACK silence are not application AA. Do not automatically retry uncertain sends. |
| Listener rejects/retains fewer receipts | Review its separate client/history/byte caps and wire/framing status. Clear history explicitly for an authorized longer test, rather than removing bounds. |

Expected recovery preserves originals and unsaved work wherever the workflow permits.
An operation that is rejected for unsafe publication must not be forced through by
copying an abbreviated preview or modifying temporary storage.

## Limits and coverage

These layers have separate budgets; large-file viewing does not imply unbounded editing,
SQL materialization or generation. Detailed references are authoritative for all bounds.

| Layer | Important limits/coverage | Details |
| --- | --- | --- |
| Raw editing/history | Complete message ≤1,048,576 UTF-16 units; line ≤16,384; document retained text/history ≤16,777,216 characters (32 MiB UTF-16 storage), 4,096 history entries. UTF-8/UTF-16 LE/BE/single-byte edits supported with strict encoding; other decodable encodings can remain read-only. | [Parser/encoding](../parser-api.md), [working-state decision](../decisions/0004-editable-document-foundation.md) |
| File navigation | Logical sequence ≤100,000 messages; malformed/ambiguous outer regions retained, not automatically repaired. | [Viewer file/source boundaries](../viewer-stage3-acceptance.md) |
| Synthetic/FHIR output | ≤1,000 messages / 4 Mi UTF-16 units per batch plus editor/history/publication limits. No partial limit-driven publication. | [Synthetic reference](../stage16-synthetic-generation.md), [FHIR mapping](../fhir-to-hl7v2-mapping.md) |
| Search | Literal terms ≤1,024 UTF-16 units; Advanced 16 conditions/16 groups/depth four; 200-row pages. No regex/replace/indexed selectors. | [Search semantics](../message-search.md) |
| Extraction | 100-row pages and 256-character display cells; stored value/row/journal budgets remain separate. Ordinal expansion does not infer clinical joins. | [Extraction limits](../field-extraction.md) |
| SQL | 100-row pages; SQL ≤65,536 characters; native database/import/result/value/deadline budgets. One read-only statement, no external DB, SQL writes or UI parameter entry. | [Practical limits](../sql-query-ui.md), [schema/enforcement](../sql-query.md) |
| Scripting | Bounded capture/worker/output/report lifetimes, not restricted account permissions. No generic facade message constructor or security sandbox. | [Runtime limits](../script-api.md), [generated contracts](../script-api-reference.md) |
| Validation | Seven scoped v2.5.1 built-ins; 128 explicitly applied message selections/document; strict profile/import/evaluator budgets. Incomplete coverage is not valid certification. | [Profile formats/coverage](../stage17-hl7-schema-validation.md) |
| FHIR import/mapping | Bounded local R4 Bundles with supported references/resources; partial one-way mapping, no remote fetch/server integration/CSV import. | [Import limits](../synthea-fhir-import.md), [mapping](../fhir-to-hl7v2-mapping.md) |
| Transport/batch/load | Strict supported wire encoding and bounded payload/history. Batch/load capture ≤1,000 messages/32 MiB; load ≤8 concurrent, 20 attempts/s, 300 seconds, 5,000 attempts. No TLS, retry, reconnect, scheduling or receiver-safety guarantee. | [Single/listener](../tcp-mllp.md), [batch](../batch-transmission.md), [load](../load-testing.md) |

No tool guarantees anonymization, secure erasure, clinical fidelity or universal
HL7/FHIR conformance. Preserve and review explicit reports, coverage gaps and
[storage/trust boundaries](privacy-and-storage.md) before sharing or transmitting.
Unresolved native clipboard/IME, screen-reader, monitor and mixed-DPI behavior
should be reported for Windows review; these docs do not establish a fresh native acceptance pass.

## Bulk analysis or Apply is unavailable

Choose an explicit scope and run Analyze / Preview. Current navigator filter needs
an applied current filter, not merely search text or one selected result page.
Source changes/closure or filter replacement make captured results stale. Parameter
changes discard the old preview; analyze again and reconfirm.

Apply requires at least one real proposal, no invalid proposals, and confirmation.
Absent nodes are not created, identical assignments are unchanged, and MSH-1/2
cannot be modified. Raw exact matching is case sensitive and does not search
substrings or other fields. Check segment occurrence/repetition and custom separators.
Unsupported encoding, ambiguous structure, message/line/overlay or capture/output
budgets reject changes rather than normalize data. Cancel/failure publishes no
partial result; the original source and its history remain unchanged.

Successful Apply opens and activates a separate **Bulk Result.hl7** tab at the first
changed message/path. Both its raw viewer and Inspector must show the result; the
original tab must still show the original values in both views without becoming
dirty. A result-opening failure withdraws the attempted document/tab, restores the
original context and reports **No result was retained**. Review and confirm again
to retry. Report any mismatch between the active tab and Inspector; changing a
preview must never silently edit the original.

Find value / Replace with (or Assign Value's New value) appear before Analyze.
The entire review can scroll when its natural form/content exceeds the window,
while results retain a finite viewport; inputs are not hidden in a short separate
parameter scroller. Results are paged and complete-value details expand on demand. Use the
existing extraction CSV or SQL workflows for their separately documented reporting
needs. See [bulk limits and workflow](inspect-and-edit.md#analyze-unique-values-and-create-a-bulk-result).
