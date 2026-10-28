# FHIR Patient Workspace: local Synthea/FHIR import

Open **File → Import Patient FHIR…** or **View → FHIR Patient Workspace**.
Both open the same optional dock tool (formerly Synthetic Patients). This is a bounded developer aid,
not a general FHIR converter or clinical conformance product. No Synthea installation,
FHIR server, network connection or new package is required to run Workbench.

## Obtain synthetic input separately

Use the [official Synthea downloads](https://synthetichealth.github.io/synthea/) or
follow its [setup/run instructions](https://github.com/synthetichealth/synthea/wiki/Basic-Setup-and-Running)
outside Workbench. Choose **FHIR R4 JSON patient Bundles**, not STU3/DSTU2, bulk NDJSON,
CSV or a server export. For a separately prepared Synthea checkout, its
[README](https://github.com/synthetichealth/synthea#generate-synthetic-patients)
documents population/seed options and `--exporter.fhir.export=true`. For example,
use its platform-appropriate launcher with `-s 42 -p 2 --exporter.fhir.export=true`.
Keep the exported Bundle JSON files in a local folder; inspect the selected release's
export configuration and output directory. Workbench never downloads or runs Synthea.
External generator versions and options can change the source dataset.

For repository acceptance, use the newly authored, fictional examples under
`samples/synthetic-only/fhir-r4/`. These are Synthea-style examples, not engine output;
their README explains provenance. Do not use or open the excluded `_TEST.hl7`.

## Import and browse

1. Open FHIR Patient Workspace. Import does not require an acknowledgment checkbox.
   See **Help → About → Data handling and privacy** for responsibility/storage behavior;
   imported data is not verified as synthetic.
2. Choose **Import FHIR ▾ → Import FHIR Bundles…** for one/multiple JSON files, or **Import folder…**
   for JSON files directly in a folder. Discovery does not recurse. Collection and
   transaction Bundles are supported; transaction requests are never executed.
3. Review resource, reference, date and unsupported counts. A failed/canceled import
   retains the last completed session; successful import replaces it atomically.
   Sources are read-only. UNC/network drives and reparse-point paths are refused;
   a local copy is required. Cloud-only storage providers are unsupported.
4. Search by source resource ID, patient identifier or display name.
   Press Enter or **Find patients**. Select a patient, then inspect **Encounters** or
   **Clinical timeline**. Same-name patients remain distinct. Pages contain at most
   100 rows; Previous/Next browse the complete bounded result. A selected patient
   remains selected across pages; filtering it out clears its event selection.
5. Select a clinical row and expand **Selected clinical details**. Codes, values,
   source dates, relationship states and provider information are bounded previews.
   Preview labels may abbreviate text; they are not authoritative export values.
   Conditions are browsable but do not produce a diagnosis/event message in mapping 1.

Patient membership requires an explicit unambiguous subject reference. Encounter
links must agree with that patient. Conflicting/unresolved encounters quarantine
clinical records from generation, while their own known patient can still browse
them with a warning. DiagnosticReport results must belong to the same patient and,
when the report has an encounter, explicitly the same encounter. Containment does
not imply a patient. Unassociated records appear in counts rather than under a
guessed patient. See the [mapping specification](fhir-to-hl7v2-mapping.md).

Timeline rows distinguish encounter start/end, condition onset/recording, observation
effective/end/issued and report effective/end/issued. Original timestamp text and
available precision remain visible. Partial dates sort at their available precision
(year/month/day boundaries are ordering coordinates only). Complete timestamps sort
by UTC instant. Missing/invalid dates appear last; inconsistent periods are counted.
No absent date, patient or clinical grouping is inferred.

## Choose a scope and generate (preview optional)

Expand **Generate HL7** to configure output. The **Generation scope** control has three choices:

| Scope | What is included |
| --- | --- |
| **Selected Patient** — default | Select a patient and Generate HL7. All their supported history is considered, with no encounter/event selection required. Includes eligible encounters, reports and observations, including observations without an encounter. |
| **Entire Import** | All imported patients and supported histories, independent of patient search, active patient and manual selections. No patient/event selection required. |
| **Selected Events (advanced)** | The existing targeted encounter/timeline workflow. Select encounters and/or timeline events, accumulating selections across pages. |

Switching scopes preserves manual encounter/timeline selections for the current
patient and invalidates the previous preview. Switching patients or importing/clearing
a session still clears those patient-specific selections, as before. The tool starts
in Selected Patient; an explicitly chosen scope stays selected while browsing.

In Selected Events, click/Space/Ctrl toggles multiple selection. An encounter includes
its eligible ADT events and associated reports/observations; timeline selection adds
the selected resource's supported event. Repeated reachability through encounters,
timeline rows or repeated report links does not duplicate that candidate/result.
A report includes only its explicit compatible result links. Eligible observations
outside included report groups produce standalone ORUs, never an invented panel.
An observation explicitly linked to two distinct reports can occur in each report's
ORU; the reports represent distinct groups under the existing mapping.

Choose **ADT and ORU**, **ADT only** or **ORU only**. In initially collapsed
**Advanced Options**, optionally change the unsigned 32-bit seed
(default 42) and an explicit complete transport timestamp with offset (default
`2030-01-01T00:00:00+00:00`). The transport timestamp normalizes to UTC whole seconds,
years 1900–9998. It is generated message metadata; clinical timestamps are never
filled from it. Choose **Generate HL7** directly; preparation runs in the background
with Cancel available. **Preview generation** optionally reviews counts/limits
without opening a document. Both use the same preparation. Preview shows patients
included, encounters considered, eligible ADT
events by type, ORU/message/OBX totals, skipped/unsupported/ineligible categories,
warnings and output size/limits. Changing scope, patient, selections or options
invalidates the preview. Disabled actions have a visible explanation: import needed,
patient/advanced selection needed, invalid settings, no eligible messages or an output
limit. No-eligible and over-limit previews remain available for review/retry.

Clear Event Selection empties manual selections. In Selected Events this blocks
generation until you select again; it never means “include everything.” Full-history
scopes keep their documented meaning. Advanced values and expansion survive collapse,
resize and docking within this session. Hover Seed's label/input for help;
screen readers receive the same explanation.
This seed changes only MSH-10; copied values, patient/visit IDs and event order remain
unchanged. Identical inputs/settings/mapping version reproduce bytes and deliberately
repeat transport IDs; changing seed does not randomize FHIR.

**Combined document** is the output organization for every scope. Separate documents
by patient are deferred: the current publisher atomically opens one completed
document, with no transaction/rollback for a group of tabs. Looping over patients
could leave an unexpected partially published batch on cancellation/failure. This
acceptance improvement keeps the proven single-output publication boundary.

Entire Import orders patients by stable source identity/content, then each patient's
messages chronologically at available clinical precision. Equal/missing times use
stable resource identity/content and event ties; missing/invalid dates remain last
within that patient. Names, UI/search order and source file selection order do not
drive output ordering. Control IDs use one batch fingerprint and global ordinal,
so they remain unique across all patients in the combined document.

Mapping 1 emits A01 for eligible inpatient starts, A04 for supported non-inpatient
starts and A03 only for eligible finished inpatient discharges. Complete clinical
timestamps and class/status evidence are required. It maps representable Quantity,
String, Boolean and CodeableConcept observations to ORU R01. Missing/unsupported
values, statuses and coding systems are omitted and counted, never normal results.
"Complete history" means all supported mappings, not clinical completeness. Conditions
remain browse-only; provider metadata supplies explicitly supported fields rather
than its own message. Unsupported resources, unassociated records and ineligible
encounter events cannot be turned into messages merely to increase the count.

Generation runs off the UI thread and proves the complete output before opening
one **new unsaved HL7 tab**. It does not insert into or modify existing tabs. Cancel,
failure, Clear, close or reset prevents late/partial publication. Use normal RAW
editing, Inspector/Navigator, Search, comparison, Field Extraction and Save As.
The generated batch is one history action: Undo removes it and Redo restores it.
Save As is explicit; source JSON is never rewritten.

The compact **Generated from FHIR R4** row beneath RAW navigation shows the original
generated message/patient counts. **View report** opens or focuses the dockable
**Generation Report**; **View → Reports → Generation Report** reopens it after closing. The
row is absent on documents without generation metadata. The full scrollable
**Generation Report — Review required** follows the selected document, with an empty
state for other tabs. Dock, float and resize it using the normal workspace controls.
Closing the tool releases its presentation, while the report belongs to the output document.
It records mapping version, opaque captured session/fingerprint, seed/transport time,
scope, included-patient/considered-encounter counts, import/event/reference/date
counts and selected mapping warnings. Preview counts are labeled eligible/estimated;
the published report labels successfully generated messages separately. Ineligible
ADT reasons distinguish class/system, status, incomplete start, reversed period and
missing discharge evidence. Skipped clinical/unsupported resources, unsupported
value/code/status/event occurrences and relationship warnings are separate; occurrence
counts may overlap. Actual failed/canceled operations show fixed status diagnostics
and publish no document/report. The report contains no
patient names, source identifiers, values or filenames. It survives tool closure and
document-view reconstruction during this application session. It is not serialized
into the HL7 file or retained across application restarts, and does not reassess
later manual edits. Saved layouts contain placement/visibility only. The generated HL7 itself
contains the selected source demographics/clinical data. Use only data you are
authorized to handle; review output before explicit saving or transmission.

Identical source bytes, scope/selections, options and mapping version produce identical
UTF-8/CR output, independent of clock and active tabs. Seed changes message control
IDs, not source patient/visit IDs. Control IDs are unique within one output; repeating
the same request deliberately repeats them. No global uniqueness guarantee exists.
Source byte changes can change fingerprints, fallback identifiers and output.

## Limits and session lifetime

| Boundary | Application ceiling |
| --- | --- |
| Files | 64, top-level JSON only |
| Input bytes | 16 MiB/file; 64 MiB/import |
| Resource occurrences / patients | 50,000 / 2,048 |
| JSON | depth 32; 2,000,000 nodes; 256 properties/object; arrays ≤ resource ceiling |
| Strings / projected text | 65,536 UTF-16 units/string; 8 Mi projected units |
| References | 200,000 total; 1,024 projected links/resource; 512-character references/fullUrls |
| Coding / numeric input | 16 codings/concept; 128-character numeric lexemes |
| Timeline / pages | 100,000 events; 100 rows/page |
| Advanced selection / search | 256 encounters plus 256 events; 256 search characters. Complete scopes are bounded by import/output ceilings, not this manual selection cap. |
| Output | 1,000 messages; 4 Mi UTF-16 total; 1 Mi/message; 16 Ki/segment |
| Mapped fields | 4,096 input / 16,384 escaped characters |

Preview counts the entire requested scope without truncation, retaining at most
1,000 message candidates and source-bounded reference/index sets. If the count
exceeds 1,000, it reports the exact count and refuses output without rendering it.
Otherwise it renders/discards one message at a time to measure escaped UTF-16 size,
stopping at the total/field/segment/message ceiling. The report labels an incomplete
measurement as a lower bound or unavailable, never a complete size estimate. Generate
is disabled for these previews; narrow to a patient, message mode or targeted events.
The total ceiling is 4 Mi (4,194,304) UTF-16 units, the existing generator limit.

Ceilings fail the whole import/output rather than silently truncate it. Projected
detail omissions and UI abbreviation are explicitly separate from import failure.
Some representable FHIR fields remain outside this deliberately partial projection.
Large datasets can require multiple separately selected imports/generations. The
stress regression exercises 10,000 observations, not every maximum combination.

**Cancel** stops the current background operation and preserves completed work.
**Clear session**, closing the tool or Reset Layout cancels work, releases imported
projections, search, selections and previews, and prevents an outstanding picker
from later importing. Reopening starts empty, without a new acknowledgment gate.
Previously generated documents remain independent. Moving/floating reconstructs
the view over the live session; layout persistence stores placement only. Restart
does not restore patient data or an imported session.

No raw JSON, narratives or attachments are retained/rendered by the browser. Source
files, OS caches and generated document snapshots follow normal local storage
behavior; clearing is not a secure-erasure promise. Operation diagnostics use fixed
messages/counts without source values, paths or exception text. ScriptHost isolation
and permissions are unchanged. Native dialogs, DPI/focus, floating windows and OS
responsiveness still require the [Stage 11 acceptance checks](stage11-synthea-fhir-acceptance.md).
