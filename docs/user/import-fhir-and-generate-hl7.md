# Import local FHIR and generate supported HL7

Use **File → Import Patient FHIR…** or **View → FHIR Patient Workspace**.
Prerequisites: supported local FHIR R4 Bundle JSON, disk access and authorized input.
The [authored alpha/beta fixtures](../../samples/synthetic-only/fhir-r4/README.md)
are synthetic examples; import does not verify that arbitrary records are synthetic.
No Synthea installation or remote FHIR server is required.

## Browse and generate

1. Choose **Import FHIR ▾ → Import FHIR Bundles…** and select
   [patient-alpha.json](../../samples/synthetic-only/fhir-r4/patient-alpha.json).
   **Import folder…** reads JSON directly in a selected local folder, not recursively.
2. Select a patient. Review **Overview**, **Encounters** and **Timeline**; choose
   a row for selected clinical details. Find/Enter searches patient names,
   identifiers or resource IDs. Pages hold at most 100 patients.
3. Expand **Generate HL7**. Select **Selected Patient** for the supported history,
   **Entire Import** for all patients (independent of search), or **Selected Events**
   for explicit encounter/timeline selections. Choose ADT/ORU output as appropriate.
4. Optionally use **Preview generation** and review **Generation preview and
   limitations**, including ineligible/skipped events, relationship warnings and limits.
   **Generate HL7** also performs the required preparation without a manual preview.
5. Inspect the new combined unsaved HL7 document and its **Generation Report**
   through **View → Reports → Generation Report**. Save As explicitly to retain it.

Expected result: one independent document containing eligible supported ADT A04/A01/
A03 and ORU R01 mappings. Counts depend on source dates/statuses/relationships and
selected scope, not simply the number of imported resources. Original JSON stays unchanged.
Missing clinical information is not filled by pretending generated metadata was sourced.

## Understand FHIR seed and mapping limits

**Advanced Options** holds an unsigned seed and explicit transport timestamp.
This seed changes MSH-10 transport IDs using the captured-source fingerprint and
ordinal. It does **not** randomize imported demographics, patient/visit identifiers,
clinical values or event order. The timestamp supplies transport metadata, not missing
clinical dates. This differs from [synthetic HL7 generation's blank random seeds](generate-synthetic-hl7.md).

Mapping is partial and versioned, not universal or bidirectional FHIR conversion.
Supported references are resolved locally; transaction requests are not executed,
URLs are not fetched and remote server integration is unavailable. Local path/file/
resource limits apply; network paths and redirecting filesystem links are rejected.
Review [the mapping contract](../fhir-to-hl7v2-mapping.md) for eligibility/exclusions.

## Cancel, replace or clear

**Cancel** stops active import/preparation/generation. Failed/canceled replacement
retains the previous completed import; stale preparation cannot publish after
source/scope/settings changes. Selected Events with no selection stays blocked; it
does not fall back to all events. Output is capped at 1,000 messages / 4 Mi UTF-16
units and never published as a partial subset after a limit failure.

**More actions → Clear session**, closing the tool or Reset Layout releases imported
projections/search/selections/previews after work drains; generated documents remain
independent. Layouts do not restore patient content. There is no mandatory synthetic
acknowledgment checkbox or patient database. Retain authorized source files separately.

See [current workspace details](../fhir-patient-workspace.md),
[import/history reference](../synthea-fhir-import.md), [mapping](../fhir-to-hl7v2-mapping.md)
and [privacy/storage](privacy-and-storage.md).
