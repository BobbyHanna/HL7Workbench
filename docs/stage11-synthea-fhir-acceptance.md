# Stage 11: Synthea/FHIR integration acceptance

Status: **user native Windows acceptance confirmed on 2026-10-21**, including the
complete-history improvements. The user authorized exactly one local 44-path
checkpoint, message `Stage 11: Add Synthea FHIR import and HL7 generation`, parent
1b04e1849b2ba3c03d135cf324bae28b8af4cba8. Audit matched every accepted file hash,
1,705 solution / 784 Workspace stress evidence, zero-warning build and integrity
checks; index empty before staging. Only acceptance/authorization records changed
for this checkpoint. Earlier pending/no-commit restrictions below are historical.
Stage 12 is authorized only after a successful checkpoint and must stay uncommitted;
no push, tag, packaging, deployment, publication or Stage 13 is authorized.

## Accepted checkpoint and resumed work

Stage 10 was accepted natively and checkpointed once on 2026-10-21 as
**1b04e1849b2ba3c03d135cf324bae28b8af4cba8**,
`feat: add Stage 10 HL7 field extraction and analysis`, parent
867016c3d1eab969c4a1853994eedb95621ad4f0. Its exact 33-path checkpoint gate passed
1,602 solution and 761 complete Workspace stress tests, locked restore, zero-warning
Release build, both ScriptHost lifecycles and protected-integrity checks. That commit
has not been amended/recreated. Evidence: artifacts/stage10-checkpoint-20261021.

The usage-limit interruption was recovered by reading the actual diff, guidance,
ADR and logs; historical test counts were not treated as current proof. Existing
importer/mapping/publication work was completed rather than replaced. The dockable
UI, reports, lifecycle handling, permanent tests and documentation are now present.

## Stage 11 acceptance improvement: complete supported histories

Native acceptance requested complete histories as the primary workflow. This is a
Stage 11 acceptance improvement, with the existing importer, mapping version, identity
rules and atomic single-document publisher retained.

- Selected Patient is the Workspace/UI default: selecting a patient is sufficient
  to Preview their supported history, including eligible observations without an
  encounter. Entire Import includes every imported patient without manual selection.
- Selected Events (advanced) preserves the existing targeted workflow. Switching
  scopes retains the current patient's manual encounter/timeline selections and
  invalidates preview. Changing patient/session still clears those selections.
- Combined document is the only output organization. Separate patient documents
  are deferred: individually publishing tabs would lack atomic group rollback and
  could leave partial batches on failure/cancel. No publication redesign was added.
- One candidate authority deduplicates reachability and repeated result links;
  distinct report groups retain their explicit results. Patients sort by stable
  source identity/content, then clinical time/resource/event ties within patient;
  missing times remain last. One global ordinal guarantees per-output MSH-10 uniqueness.
- Preview distinguishes eligible/estimated output from successfully generated totals,
  shows patients/encounters, scoped skipped resources, unsupported/link warnings,
  ineligible ADT reasons and output-limit states. Actual failures use fixed failure
  status; cancellation is separate. Neither publishes a successful document/report.
- Eligibility counts cover the complete scope. Retention stops at 1,000 candidates;
  an over-count plan retains no partial sequence and cannot generate. Otherwise
  preview renders/discards one message at a time for escaped-size measurement,
  stopping at existing ceilings. Incomplete measurements are labeled lower bounds/
  unavailable. No truncation, new mappings, inferred clinical events or partial output.

Permanent additions: 14 Core scope/size/ordering/report cases, 10 Workspace scope/
publication/cancellation/save/extraction cases, 4 headless Desktop scope/theme/state
cases, and one shared fictional graph builder (including a 10-patient history).
The 2,000-encounter regression checks complete 4,000-event counts beyond the output
cap; a source-wide ambiguity lookup avoids repeated full-import scans per encounter.
All previous coverage remains. The 10-patient test graph produces 40 messages;
the user's separately imported 10-patient dataset may have different eligible counts.

Current improvement gate evidence: **artifacts/stage11-history/**. Earlier Stage 11
logs remain under artifacts/stage11 and are historical baseline evidence.

| Current gate | Result |
| --- | --- |
| Locked restore | Passed; final-restore.log |
| Release build, warnings as errors | Passed, 0 warnings/errors; final-indexed-build.log |
| Focused Core | 68 passed, 0 failures/skips, 0.998s; final-indexed-core.log/xml |
| Focused Workspace | 23 passed, 0 failures/skips, 4.048s; final-indexed-workspace.log/xml |
| Focused Desktop | 32 passed, 0 failures/skips, 7.423s; final-indexed-desktop.log/xml; includes existing synthetic generator coverage |
| Full solution | 1,705 passed, 0 failures/skips, 2m 51.607s; final-indexed-solution-tests.log |
| Complete Workspace stress suite | 784 passed, 0 errors/failures/skips/not-run, 290.253s; final-indexed-workspace-stress.log/xml; HL7WORKBENCH_STRESS=1, no class filter |
| Both ScriptHost smoke lifecycles / API-reference sync | Passed against final existing Release layout, no publish; final-indexed-scripthost-smoke.log / final-reference-sync.log |
| Fixtures, protected hashes, excluded HL7, artwork/icons | Passed all eight tracked fixture blobs, 13 protected hashes and original/derived icon checks after stress; excluded file hash-only; final-post-check.log |
| Whitespace / exact inventory / implementation hashes | Passed 44 text paths/local links/inventory and git diff --check; all 37 final-indexed implementation/test/fixture hashes match after stress; final-text-check.log / final-diff-check.log / final-post-check.log |

## Requirement audit and implementation

| Requirement | Implementation / permanent evidence |
| --- | --- |
| Local synthetic-only R4 import | File/View share one optional tool; per-picker attestation/warning; collection/transaction JSON, multi-file/top-level folder; local read-only path guard, no network/server/download/dependency |
| Supported projections | Patient, Encounter, Condition, Observation, DiagnosticReport, Organization, Practitioner; unsupported metadata/counts; immutable bounded projections, no retained raw JSON/narrative/attachment rendering |
| Reference identity | Exact fullUrl, REST-relative, Bundle-local/unique session Type/id, contained scope; duplicate/ambiguous/conflicting states; own subject and consistent encounter required; no name-based association |
| Bounded browser | 100-row virtualized patient/encounter/timeline pages; literal search; explicit selected patient; details, date/reference states; selections persist across pages within limits |
| Timeline | Actual source text/precision and event kind; coarse partial-date ordering, deterministic ties; missing/invalid/reversed periods explicit, no inferred dates |
| ADT / ORU | Evidence-based A04/A01/A03, typed conservative ORU, actual report-result groups, stable qualified PID/visit IDs, unique per-output MSH-10; version-1 mapping specification |
| Safe publication | Background captured plan; closed validated Core batch; existing prospective insertion/save/index/byte proof on private new document; only complete current output opens; normal history/save/extraction |
| Privacy / lifetime | Count-only document-owned report; fixed diagnostics; layout placement only; failed import retains previous session; Clear/close/reset cancels and releases; stale views/pickers cannot retarget replacement |
| Regression boundaries | Existing generator, de-identification, scripting, keyboard/history, save, extraction and branding coverage retained; no worker isolation or package changes |

Resumed audit corrections include restricting deduplication to identical shared
Organization/Practitioner metadata (clinical relative-reference scope must survive),
refusing repeated clinical fullUrl aliases even through relative references, REST
type/id/base checks, exact decimal rendering, source-safe controls/HL7 literals,
conditional range omission, value/dataAbsentReason conflicts, bounded candidate and
segment construction, cancellation within large groups, and coding-loss warnings for
both Observation and DiagnosticReport concepts. Ambiguous visit fullUrls whose
contained subjects independently resolve use source-qualified visit authorities.
Detached/stale UI objects cannot select reused integer keys in a replacement import.
File ceilings are checked before discovery/enumeration, including overflow inputs.

Permanent tests cover malformed/version/shape cases and all configurable import
budgets; fullUrl/relative/contained/cross-input relationships; clinical dedup scope;
patient conflicts; precision/missing dates; ADT eligibility; all four value types,
exact decimals/comparators, codes/units/statuses/ranges; report grouping; seed/ID
determinism; literal injection; new-document history/save/extraction/report privacy;
four publication cancellation phases and retry; no existing-document mutation;
failed/canceled replacement; offline path rejection; stream disposal; 10,000-observation
stress/cycles; themes/narrow layout; real File/View registration; dock/float/reset;
view/picker lifetimes; 205-patient paging and stale object selection.

## Prior Stage 11 verification evidence

These retained results describe the initial 39-path Stage 11 baseline, before the
complete-history acceptance improvement. Logs/XML remain in ignored
**artifacts/stage11/**, including diagnostic logs. Use the current indexed gate above
and artifacts/stage11-history for the final implementation. No permanent tests or
fixture infrastructure were removed.

| Gate | Current result |
| --- | --- |
| Locked restore | Passed; final-restore.log |
| Release build, warnings as errors | Passed, 0 warnings / 0 errors; final-build-retest.log |
| Focused Core FHIR tests | 54 passed, 0 failed/skipped, 0.235s; final-core.log/xml |
| Focused Workspace FHIR tests | 13 passed, 0 failed/skipped, 2.038s; final-workspace.log/xml |
| Focused headless Desktop tests | 8 passed, 0 failed/skipped, 1.965s; final-desktop.log/xml |
| Full solution suite | 1,677 passed, 0 failed/skipped, 3m 09.482s; final-solution-retest.log |
| Complete Workspace stress suite | 774 passed, 0 errors/failures/skips/not-run, 352.997s; final-workspace-stress.log/xml; HL7WORKBENCH_STRESS=1, no class filter |
| Both ScriptHost lifecycles, existing Release layout | Passed current/whole Main, shared state, CSV, completion, defer, clean exit, hashes and dependency isolation; final-scripthost-smoke.log; no publish command |
| Generated Script API reference synchronization | Passed; final-reference-sync.log |
| Eight fixture blobs / all 13 protected hashes | Passed exact blob/SHA comparisons; final-integrity.log |
| Original icon / derivatives / excluded HL7 | Passed hash-only comparison; excluded file never viewed/staged |
| Text whitespace / git diff --check | Passed all 39 text paths, local documentation links and Git check; final-text-check.log / final-diff-check.log |

An earlier resumed full suite passed 1,676 cases with no failures/skips before the
last report-coding warning regression was added. Its log is retained as
final-solution-tests.log; that successful 1,677-case baseline rerun superseded it.
The 32 baseline implementation/test/fixture hashes matched its pre-gate manifest. No native
acceptance is claimed by headless tests. Test diagnostics use authored synthetic data.

Reproduce from the repository root with .NET, Avalonia and test telemetry opt-outs as
in the established verification environment:

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore -warnaserror
dotnet run --project tests/HL7Workbench.Core.Tests -c Release --no-build --no-restore -- -class '*Fhir*'
dotnet run --project tests/HL7Workbench.Workspace.Tests -c Release --no-build --no-restore -- -class '*Fhir*'
dotnet run --project tests/HL7Workbench.Desktop.Tests -c Release --no-build --no-restore -- -class '*Synthetic*'
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
try {
    $env:HL7WORKBENCH_STRESS = '1'
    dotnet run --project tests/HL7Workbench.Workspace.Tests -c Release --no-build --no-restore
} finally { Remove-Item Env:HL7WORKBENCH_STRESS -ErrorAction SilentlyContinue }
./build/VerifyScriptPublish.ps1 -PublishDirectory src/HL7Workbench.Desktop/bin/Release/net10.0
python -B build/WriteScriptApiReference.py --check
git diff --check
```

The smoke script verifies the existing Release directory; it does not run publish.
Integrity comparisons additionally use the eight tracked fixture blobs and the
13-entry protected manifest from artifacts/stage10-checkpoint-20261021, with explicit
original/derived icon hashes and
artifacts/stage11-history/final-indexed-implementation-hashes.json (37 paths).

## Exact Stage 11 inventory

Verified current inventory: **44 Stage 11 paths: 14 modified tracked, 30 new**, plus
the separately excluded unchanged untracked HL7. HEAD remains the Stage 10 checkpoint;
the Git index is empty. No Stage 11 file has been staged or committed.

Modified tracked paths:

```text
README.md
docs/PROJECT_CONTEXT.md
docs/architecture.md
src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/MainWindow.axaml
src/HL7Workbench.Desktop/MainWindow.axaml.cs
src/HL7Workbench.Desktop/Viewer/FileDocumentView.cs
src/HL7Workbench.Workspace/MessageDocument.Generation.cs
src/HL7Workbench.Workspace/MessageDocument.cs
tests/HL7Workbench.Core.Tests/HL7Workbench.Core.Tests.csproj
tests/HL7Workbench.Desktop.Tests/HL7Workbench.Desktop.Tests.csproj
tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs
tests/HL7Workbench.Workspace.Tests/HL7Workbench.Workspace.Tests.csproj
```

New paths:

```text
docs/decisions/0015-local-synthetic-fhir-integration.md
docs/fhir-to-hl7v2-mapping.md
docs/stage11-synthea-fhir-acceptance.md
docs/synthea-fhir-import.md
samples/synthetic-only/fhir-r4/README.md
samples/synthetic-only/fhir-r4/patient-alpha.json
samples/synthetic-only/fhir-r4/patient-beta.json
src/HL7Workbench.Core/FhirClinicalTime.cs
src/HL7Workbench.Core/FhirGeneration.Mapping.cs
src/HL7Workbench.Core/FhirGeneration.Selection.cs
src/HL7Workbench.Core/FhirGeneration.cs
src/HL7Workbench.Core/FhirImportModels.cs
src/HL7Workbench.Core/FhirR4Importer.Projection.cs
src/HL7Workbench.Core/FhirR4Importer.Relationships.cs
src/HL7Workbench.Core/FhirR4Importer.cs
src/HL7Workbench.Desktop/MainWindow.SyntheticPatients.cs
src/HL7Workbench.Desktop/ViewModels/SyntheticPatientsViewModel.cs
src/HL7Workbench.Desktop/Viewer/GenerationReportView.cs
src/HL7Workbench.Desktop/Viewer/SyntheticPatientsView.cs
src/HL7Workbench.Workspace/FhirGenerationPublisher.cs
src/HL7Workbench.Workspace/FhirPatientImport.cs
src/HL7Workbench.Workspace/SyntheticPatientsSession.cs
tests/HL7Workbench.Core.Tests/FhirGenerationTests.cs
tests/HL7Workbench.Core.Tests/FhirHistoryScopeTests.cs
tests/HL7Workbench.Core.Tests/FhirImportTests.cs
tests/HL7Workbench.Desktop.Tests/SyntheticPatientsViewTests.cs
tests/HL7Workbench.Desktop.Tests/SyntheticHistoryScopeViewTests.cs
tests/HL7Workbench.Workspace.Tests/FhirHistoryWorkflowTests.cs
tests/HL7Workbench.Workspace.Tests/FhirWorkflowTests.cs
tests/Shared/FhirSyntheticHistory.cs
```

Excluded, never opened/staged: samples/synthetic-only/stage5b-repeated-patients_TEST.hl7.
No lock file/dependency, worker permission, source artwork, original HL7 fixture or
protected native editing regression file changes. Ignored verification logs/manifests
are intentionally retained for review; no bin/obj or uncertain user artifacts removed.

## Limits and caveats

See the [user guide](synthea-fhir-import.md#limits-and-session-lifetime) for all
ceilings and the [mapping](fhir-to-hl7v2-mapping.md) for field-level policies. This is
an explicitly partial projection, not all Synthea/FHIR versions, every FHIR element,
clinical validation or universal v2 conversion. Newly authored fixtures approximate
the documented Synthea R4 packaging; no Synthea engine or external dataset was
installed/run/downloaded for this milestone. Native testing with separately produced
small R4 Bundles remains part of acceptance.

Patient synthetic status is user-confirmed, not PHI detection. Generated HL7 includes
selected source demographics/clinical values; a safe count-only report does not
de-identify that output. Unsupported/unassociated data is counted rather than guessed.
Some omitted fields have aggregate counts; the report is not an exhaustive per-element
loss ledger. Partial dates are not instants; >4 fractional timestamp digits and leap
seconds are unsupported. No derived abnormal flags, unit conversion, attachments,
components, diagnosis messages, locations/beds or terminology service.

Requests intentionally reproduce control IDs for the same source/options; uniqueness
is within output, not a global registry. Qualified source identities use shortened
hashes and source byte changes may change fallback authorities. Reports are immutable
session attribution, survive tool closure, do not reassess later edits, and are not
automatically persisted with HL7. Import/session clearing is not secure erasure of
source files, generated documents, local snapshots or OS caches. Stress coverage
proves tested shapes/bounds, not every ceiling combination on every Windows device.

## Complete-history native Windows acceptance — pending

Use the existing Release executable outside a debugger, known-synthetic local input
and scratch Save/CSV destinations. Never open the excluded `_TEST.hl7`.

1. Import the existing local 10-patient synthetic FHIR dataset after confirmation.
   Verify the patient count; record the actual preview counts rather than assuming
   every resource produces a message. The authored automated 10-patient graph is
   a regression fixture builder, not a claim about this separately generated dataset.
2. Select a patient without selecting any encounter or timeline event. Verify scope
   defaults to Selected Patient, with a visible explanation and enabled Preview.
3. Preview their complete supported history. Review patient/encounter/event totals,
   mapping warnings and output size. Generate HL7 into one new unsaved combined tab.
4. Inspect eligible ADT and ORU messages against the source history. No admission,
   discharge, diagnosis, result, date or relationship may be fabricated. Results
   outside included reports should be standalone ORUs when supported.
5. Compare PID-3 and PV1-19 across ADT/ORU for the same patient/visit, including another
   scope. Verify unique MSH-10 within the entire output and normal editor navigation.
6. Switch to Entire Import. Clear patient search/selection if desired; neither is
   required for Preview. Verify all 10 imported patients are considered and review
   exact eligible totals, skipped resources and ineligible categories.
7. Generate the combined document. Compare message/OBX totals with Preview and its
   associated Generation Report. Patient groups and event ordering must be stable;
   existing HL7 tabs/source JSON must stay unchanged. If that dataset exceeds a
   ceiling, expect refusal with no partial tab; narrow the mode/patient/events or
   import a smaller known-synthetic subset rather than expecting truncation.
8. Repeat with the same source, scope, seed and transport time. Compare bytes through
   normal comparison/scratch saves. Change seed: control IDs change, PID/visit IDs stay.
9. Switch to Selected Events; select encounters/timeline events and generate targeted
   output. Switch to other scopes and back: manual selections must remain. Repeated
   selections/report links must not duplicate candidates or OBX within one report.
10. Review skipped/unsupported/link warnings and specific ineligible ADT reasons in
    Preview and Generation Report. Check a synthetic no-eligible history and a malformed
    scratch import: zero eligibility differs from an actual fixed failure status.
11. Run Field Extraction on the combined document; inspect PID/PV1/OBR/OBX. Save As to
    scratch and recheck history/Undo/Redo, report reconstruction and tool clearing.
12. Exercise preview/generation cancellation, over-1,000-message and over-4-Mi UTF-16
    requests, then narrow scope/retry. No partial output. Dock/float/reconstruct/reset,
    resize, light/dark themes, native dialogs/DPI/focus, offline use and existing
    generator/scripting/de-identification/search/save workflows must remain sound.

## Original Stage 11 native checks — retained, still pending

For retained steps that target particular encounters/timeline events, first choose
**Selected Events (advanced)**. The primary complete-history scopes are checked above.

Run the existing Release Desktop executable outside a debugger. Use only authored
fixtures or separately obtained known-synthetic R4 data. Keep source files unchanged;
Save/CSV to scratch destinations. Never open the excluded `_TEST.hl7`. No packaging,
installation, commit or publication is needed.

1. Open **View → Synthetic Patients** with no imported data. Verify warning,
   confirmation requirement, empty pages and disabled generation. File's import action
   must reveal the same single tool.
2. Confirm synthetic/test input and select `samples/synthetic-only/fhir-r4/patient-alpha.json`.
   Check import counts and confirmation reset. Repeat with a small independently
   generated Synthea FHIR R4 Bundle; unsupported shapes must report safely.
3. Import the `fhir-r4` folder or both sample files. Expect two distinct same-display-name
   patients, shared provider metadata deduplication and the cross-input result link.
4. Search SYN-ALPHA/SYN-BETA/name using Find and Enter. Browse pages of a larger
   synthetic import; filtering out the selected patient must clear event selection.
5. Select Alpha; inspect the inpatient encounter, condition, observation types and
   diagnostic report in timeline/details. Verify resolved provider links and source
   date text; no narrative/attachment rendering.
6. Check start/end, effective/issued and condition events in chronological order.
   Partial/missing/invalid dates must be labeled, never replaced by transport time.
7. Review unsupported resource/value and date/reference counts. Use scratch synthetic
   malformed/missing/conflicting examples; failure must retain the completed session
   without revealing source content in Activity Log.
8. Select Alpha's encounter, choose ADT only and Preview/Generate. Expect A01 then
   A03. Beta's ambulatory encounter produces A04 without a fabricated A01/A03.
9. Generate ORU only, then both. Alpha's report creates one OBR with its five eligible
   OBX results; missing/unsupported values stay absent with warnings. Select an
   individual observation to check standalone ORU behavior. Folder import also adds
   Alpha's cross-input standalone result: expect two ORUs and four messages in both
   mode, versus one ORU/three messages with Alpha's file alone.
10. Compare PID-3/PV1-19 across generated ADT/ORU for one patient/visit; ensure the
    other patient/visit stays distinct. Do not rely on identical display names.
11. Verify MSH-10 is distinct for every message in one output and MSH-11 is T.
12. Navigate/edit generated output in RAW/Inspector/Navigator. Undo/Redo the batch
    without mouse clicks, including Edit Mode off and tab switches. Save As to scratch
    through normal consent; existing HL7 tabs/source JSON must remain unchanged.
13. Run Interface Overview Field Extraction against generated output; inspect PID,
    PV1, OBR/OBX and export a synthetic CSV to scratch. Exercise Search/comparison.
14. Inspect expanded **Generation Report — Review required**. Verify counts, mapping
    1, seed, opaque source/session identity and limitations; no patient values/paths.
    Reconstruct/switch document views and close the import tool: report must survive.
15. Reimport the same source, select the same events/options/seed and generate again.
    Compare bytes with Message Comparison or scratch saves. Change seed: control IDs
    change while patient/visit IDs remain stable.
16. Cancel a sufficiently large import/preview/generation, then retry. No partial new
    document/session may appear. Clear/close/reset during work or an outstanding
    picker must prevent late publication/import; completed prior output remains.
17. Clear session; verify patient/search/detail/selection/preview data disappears.
    Close/reopen/restart with saved layout: placement may remain, patient data must not.
18. Disconnect network; repeat local import/generation. Dock/float/reopen, resize narrow
    and wide, change light/dark themes and check keyboard, scrollbars, DPI, native
    dialogs/focus, Activity Log privacy and unchanged icon. Recheck Stage 8D generator,
    scripting/de-identification, extraction, save and window-root Undo/Redo.

**Stop for user review/native Stage 11 acceptance. Do not stage, commit, amend,
push, tag, package, deploy, publish, release or start Stage 12.**
