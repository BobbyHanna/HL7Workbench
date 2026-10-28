# FHIR Patient Workspace

Open **View → FHIR Patient Workspace**, or **File → Import Patient FHIR…**.
The former Synthetic Patients tool has a new visible name; its existing
`tool.synthetic-patients` layout identity remains unchanged. Saved placement,
floating bounds and hidden-tool preferences continue to work. Layout files never
restore imported records or search text.

## Import and browse

Choose **Import FHIR ▾ → Import FHIR Bundles…** for local JSON files, or
**Import folder…** for JSON files directly inside a local folder. The empty state
also offers Import FHIR. No synthetic-data checkbox or acknowledgment is required.
The same bounded R4 Bundle validation, local-path checks, atomic replacement and
cancellation behavior still apply. No remote references are fetched.

The toolbar shows the imported session's patient/resource counts. Active operations
show status and a directly visible **Cancel** button. **More actions** contains
**Clear session** and the detailed import summary. Failed or canceled imports retain
the previous completed session; clearing or closing prevents late publication.

The compact patient list shows names and identifiers. Search by name, patient
identifier or resource ID with **Find** or Enter. Pages contain at most 100 records;
Previous/Next appears when there is more than one page. A no-match search displays
a distinct prompt. Filtering out the selected patient clears its clinical selection.

Select a patient to open the detail area:

- **Overview** shows available demographics, identifier authority, address and source
  provenance. Missing optional data is labeled “not supplied.”
- **Encounters** retains encounter class/status, dates, ADT eligibility and selection.
- **Timeline** retains chronological clinical events and source date precision.
  Missing/invalid dates and unresolved/conflicting relationships stay explicit.

Selecting an encounter or timeline row reveals **Selected clinical details** with
the existing bounded code/value/date/reference/provider information. Empty encounter
and timeline collections have their own prompts. A Bundle without a supported
patient is still rejected by existing validation; it does not replace a valid session.

At wide sizes the list and details sit side by side with a resizable divider. At
narrow sizes they stack. Short bottom docks scroll within the tool's bounds; Dock
headers/tabs remain separate. Lists retain finite virtualized viewports. Resizing,
floating and redocking retain the current session, selections and settings.

## Generate HL7

Expand **Generate HL7** when ready. Collapsing it keeps your settings and preview.
Choose scope (**Selected Patient**, **Entire Import** or **Selected Events**)
and ADT/ORU message types, then choose **Generate HL7** directly. Preparation runs
in the background with Cancel available. Generate and Preview use the same eligibility,
output-size and limit calculations before the existing atomic publisher.

Optionally choose **Preview generation**, then inspect **Generation preview and limitations**.
Counts, ineligible/skipped events, relationship warnings and output limits are
preserved. Changing generation inputs invalidates the preview. Disabled actions
explain prerequisites through their tooltips and generation status.

Selected Patient includes complete supported history without manual event selection.
Entire Import includes all patients, independently of search. Selected Events uses
explicit encounter/timeline selections. Clear Event Selection leaves that scope
empty and blocked until you select again; it never falls back to all events.
Full-history scopes retain their normal meaning.

**Advanced Options** (initially collapsed) holds the unsigned seed and explicit
transport timestamp. Collapse, resize and docking retain values and expanded state
in this session. The timestamp is generated metadata; it never fills missing clinical
dates. Seed affects only MSH-10 transport IDs, combining seed, selected-source
fingerprint and ordinal. It does not randomize copied demographics, clinical values,
patient/visit IDs or event order. Identical source, scope, settings and mapping version
support repeatable bytes and deliberately repeat transport IDs across batches.
Hover Seed's label/input for help; screen readers receive the same explanation.

Invalid configuration, missing prerequisites, no eligible messages and exceeded
limits prevent publication with status/tooltips. Nonblocking omissions and warnings
remain available in the preview and generated document's Generation Report without
a mandatory acknowledgment. Generate can safely reuse current preparation only;
source, scope, selection or settings changes invalidate it.

**Generate HL7** opens one independent, combined, unsaved document after the entire
output validates. Original files/documents stay unchanged. Existing mapping and
publication ceilings remain: 1,000 messages / 4 Mi UTF-16 output, with no partial
publication. The generated document retains its count-only Generation Report.
Use normal Save/Save As to keep generated HL7. See the
[mapping specification](fhir-to-hl7v2-mapping.md) and
[detailed import/history reference](synthea-fhir-import.md).

## Session and data handling

Imported projections, search, selections and preview are session-only. Clear Session,
tool close and Reset Layout release that session; generated documents remain
independent. There is no automatic patient database or external transmission from
this workspace. Imported data is not verified as synthetic. Data responsibility,
temporary storage, explicit persistence and separate network/script functionality
are explained in **Help → About → Data handling and privacy** and the
[data-handling reference](data-handling.md).

Fresh/reset layouts initialize approximately 73% upper document area and 27% bottom
tools, with 22% of horizontal space for right inspection tools, using the actual
Dock viewport. Saved layouts and user splitter adjustments take precedence over
defaults and are not reapplied on ordinary resize.
