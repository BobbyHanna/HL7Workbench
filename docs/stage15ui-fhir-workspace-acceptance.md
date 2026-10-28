# Stage 15UI follow-up — FHIR Patient Workspace

2026-10-28 amendment: the owner reports native Windows acceptance of this redesign.
The subsequent optional-preview/Seed-help polish has its own
[acceptance report](fhir-generation-ux-acceptance.md) and still needs native review.
Its current workflow supersedes preview-first instructions below; prior automated
and native evidence remains historical rather than being reclassified.

Source redesign for owner/native review, 2026-10-27. This supersedes the earlier
Synthetic Patients layout and acknowledgment UI. Stop before Stage 15D/16. No
commit, push, packaging or accepted-beta modification is authorized.

## Findings and implementation

The prior full-width patient/timeline browser claimed a large star region even
when empty. Import/search/selection, permanent warnings and generation Auto rows
competed with that region. A short dock then required navigating a long form under
a dominant table. The shared finite-scrolling helpers prevented unbounded lists but
did not provide a suitable workspace information hierarchy.

The replacement has three areas:

1. A small **Import FHIR** flyout, authoritative patient/resource counts, **More
   actions** and a directly visible active **Cancel**.
2. A patient master list and a larger detail area with **Overview**, **Encounters**
   and **Timeline**. At narrow widths the same controls stack; a wide divider is
   resizable. Empty/no-match/no-selection/no-clinical-row states are distinct.
3. A collapsed **Generate HL7** section holding the existing scope, ADT/ORU, seed,
   transport time, output behavior, clear selection, preview, generation and clinical
   limitation feedback. Collapse does not invalidate settings or preview.

Controls are constructed once per view, finite list pages remain bounded to 100,
and the existing Source/Workspace model remains authoritative. Patient and clinical
selections, settings, chosen detail tab, inspected resource identity and expanded
sections survive view replacement through session-only Desktop state. No imported
records enter layout/settings persistence. The existing session cleanup and late
publication guards remain.

The visible name is **FHIR Patient Workspace** in the View menu, Dock tool title,
picker/help descriptions and generation-report guidance. Internal classes, menu
control IDs, handlers and `tool.synthetic-patients` remain compatible. Saved layouts
restore placement and visibility using that same identity and display the new title.

## Acknowledgment and privacy boundary

The checkbox/banner and the Desktop confirmation dependency are removed. This is
explicitly authorized by the owner. Core, Workspace file import and the session
offer no-acknowledgment overloads which reuse the existing parser/validation and
publication bodies. They do not pass a fabricated `true` confirmation value.
Boolean compatibility overloads retain their original guard and asynchronous Core
failure contract. Their existing regression tests remain.

No resource, local-path, JSON/R4 shape, reference, atomic-replacement, cancellation
or output limit was removed. UNC/network/reparse-path protection remains. Patient-free
and malformed replacements still fail and retain a prior valid session. Generated
HL7 still contains selected source data. The report notice drops the now-false
user-confirmed synthetic-provenance claim while retaining clinical/mapping limitations.

The repository had no data-handling EULA or About privacy section. The existing
About window now has a nonmandatory, factual **Data handling and privacy** expander;
`docs/data-handling.md` documents responsibility, in-memory FHIR sessions, temporary
HL7/SQL storage, explicit saves/exports, potentially sensitive saved definitions,
privacy-safe ordinary logs, TCP/MLLP and trusted-script file/network permissions.
Software licensing is unchanged. No binding EULA, HIPAA claim, synthetic-data
verification, de-identification guarantee or new acknowledgment dialog was added.

## Default docking correction and evidence limits

Default model proportions already existed, but were assigned during construction
before a finite Dock viewport was available. `DefaultDockSizing` now arms one
fresh/reset initialization after positive Dock bounds and posts at Loaded priority.
It reconciles the actual displayed root to 73% upper / 27% bottom, and 78% document /
22% right inspection. It removes its size handler after the first usable viewport;
normal resize never reapplies defaults. Closing/resetting cancels pending callbacks.

Both displayed-root and factory-root identity checks prevent a queued default
callback from overwriting a saved-layout restore during Window.Opened. Existing
saved/user proportions remain authoritative. Startup, recovery and Reset Layout use
the same default path; saved restoration retains its separate path.

The exact owner-reported native initial-sizing symptom was **not independently
reproduced**: fresh headless geometry was already correct. The pre-measure timing
condition was confirmed in code, and the fix makes post-measure default ownership
explicit. Automated hierarchy tests verify actual ratios, close/reopen, repeated
window resizing, reset and a deliberately saved 60/40 layout surviving startup and
queued-default races. Native verification of the original symptom remains required.

## Automated verification

Permanent new coverage consists of ten `FhirWorkspaceTests` cases and one Core
no-acknowledgment/validation/cancellation case. Existing FHIR workflow, history,
responsive and menu suites were adapted to the expressly changed UI. No permanent
tests, fixtures or meaningful behavior assertions were removed. Dispatcher-based
async workflows return awaited Tasks, and rendered captures reject blank frames.

- Existing eight patient-view workflows retain file import, search, patient/encounter/
  timeline selection, preview/generation, stale-row rejection, clear/close/reset and
  placement-only persistence coverage, now without the checkbox.
- New cases cover folder import/nonrecursive discovery, active Cancel/late picker
  completion, missing optional demographics, empty clinical collections, rejection
  of patient-free replacements, no-match search, and setting/preview preservation.
- All five tool sizes (1200×800, 900×600, 600×400, 350×300, 250×250 DIP) exercise
  real arranged controls, popup actions, scrolling, section boundaries and finite
  lists. Three 1.0/1.25/1.5 transform cases exercise repeated width transitions.
- Full DockControl hierarchy coverage runs the workflow in a short bottom dock,
  right dock, floating roots, redocking and close. Floating geometry uses a fake
  native owner plus actual headless Windows; it does not establish native chrome.
- Core/Workspace checks compare fingerprints and generated bytes, preserve source
  files, exercise no-acknowledgment limits/cancellation and retain legacy guard tests.

| Individual tool DIP | Empty/import/browser | Details/generation/repeated resize |
|---|---|---|
| 1200 × 800 | Pass | Pass |
| 900 × 600 | Pass | Pass |
| 600 × 400 | Pass | Pass |
| 350 × 300 | Pass, scrolling/stacked | Pass, scrolling/stacked |
| 250 × 250 | Pass, scrolling/stacked | Pass, scrolling/stacked |

The generic responsive suite has 23 cases; the new FHIR hierarchy/state suite has
10. Opt-in Skia captures show the actual complete Dock hierarchy and isolated
feature layouts. Representative wide overview, compact empty, bottom-docked and
floating generation frames were inspected. Blank early captures exposed a test
dispatcher-overload issue, which was corrected before accepting rendered evidence.
Earlier diagnostic failures remain available under ignored artifacts.

Evidence directory: `artifacts/stage15ui-fhir/`.

| Verification | Result / evidence |
|---|---|
| Normal and isolated locked restores | Passed; `restore-final-normal.log`, `restore-final-isolated.log`. |
| Final Release solution build, warnings as errors | Passed, 0 warnings/errors; `build-final-cleanup.log`. |
| Complete five-suite run | 2,163 passed, 0 failed/skipped, 7m 03.6s; `full-complete.log`. |
| Final FHIR hierarchy/state run | 10 passed, 0 failed/skipped; `fhir-complete.log`. |
| Final generic responsive run | 23 passed, 0 failed/skipped; `matrix-complete.log`. |
| Final Desktop rerun | 438 passed, 0 failed/skipped, 2m 17.9s; `desktop-complete.log`, after detached demographic-text cleanup. |
| Protected beta | All 2,384 files match baseline hashes, no added/missing files; `protected-preview-check.json`. |

After the complete run, final review added explicit clearing of detached Overview
text and strengthened the reconstruction test to contain selected demographics.
The final Desktop suite reruns against that change; the other four test DLLs remain
byte-identical to the successful full run. The initial full run's obsolete menu-title
expectation was corrected to the explicitly requested name; no menu order/function
coverage was weakened. Earlier failures remain in the evidence directory.

41 nonblank headless Skia frames are retained in `rendered/`, including full bottom/
right Dock hierarchies, floating windows, empty states, overview and generation.
These are rendered layout evidence, not native Windows acceptance. Test runtime
trees are copied byte-identically from isolated output to ignored
`bin/Stage15UI/net10.0` directories for existing branding tests' path assumptions.
Accepted private-preview outputs and normal running Release outputs were not rebuilt.

## Native Windows review — outstanding

A disposable isolated native check was compiled with a separate configuration,
but Computer Use app approval timed out before launch. No native window was
inspected and no native acceptance is claimed. The unused probe and repair
intermediate were removed after verifying their owned paths. No user configuration
or saved layout was changed by that attempt.

Owner checklist, using only repository synthetic fixtures:

The verified development runtime is in
`src/HL7Workbench.Desktop/bin/Stage15UI/net10.0/`. Use that current source build for
review; do not rebuild or launch a modified copy of the protected preview artifacts.

1. Start restored at 900×600 with a fresh configuration; verify approximately
   73/27 vertical and 78/22 horizontal Dock proportions. Maximize/restore repeatedly.
2. Drag splitters deliberately, Save Layout, restart/Restore Layout and verify those
   custom proportions survive. Reset Layout should apply defaults once at the
   current available content size. Repeat with Activity Log hidden and reopened.
3. Open FHIR Patient Workspace in a short bottom dock, narrow right dock, large
   floating window and 600×400/350×300/250×250 floating windows. Headers/tabs must
   remain unobstructed. Verify the empty state has no giant table.
4. Import `samples/synthetic-only/fhir-r4/patient-alpha.json` from the menu with no
   acknowledgment; test folder import, native picker cancel and active Cancel.
   Check counts and the import summary, then Clear Session.
5. Search/select a patient, inspect demographics, encounters and timeline. Exercise
   paging/keyboard selection and clinical details. Verify no-match and incomplete
   optional-data states. No missing clinical values should be invented.
6. Expand Generate HL7; change scope/types/seed/transport timestamp, select advanced
   events, Preview and inspect limitations, collapse/reopen, Generate and inspect
   the independent output/Generation Report. Move/resize/redock during this workflow;
   selections/settings/preview must remain intact. Sources must remain unchanged.
7. Close/reopen/reset the tool: imported session data should clear according to its
   original lifetime rules. Explicit generated documents should remain independent.
8. Check Tab/Shift+Tab, visible focus, flyout navigation, wheel and keyboard scrolling
   in light/dark themes at actual 100%, 125% and 150% Windows scaling, including
   moving floating windows between monitors. Confirm About's informational section
   and absence of persistent workspace confidentiality banners.

## Limits and scope

Very short docks necessarily show a small vertical slice; scroll to search/details/
generation. Lists retain finite internal viewports. The mapping remains partial and
bounded; there is no general FHIR server, patient database, schema validation or
new output mode. Native sizing, gestures, per-monitor DPI, focus/pickers and visual
acceptance remain the review gate. Stage 15D/16 is not included.

## Changed files and repository status

31 paths changed in this follow-up; `artifacts/stage15ui-fhir/changed-files.json`
records the inventory against the pre-task hash baseline.

- Core: `FhirR4Importer.cs`, `FhirGeneration.cs` (compatibility overload / report
  notice only; no projection/mapping algorithm changes).
- Workspace: `FhirPatientImport.cs`, `SyntheticPatientsSession.cs`.
- Desktop: `MainWindow.axaml`, `MainWindow.axaml.cs`,
  `MainWindow.SyntheticPatients.cs`, `ViewModels/SyntheticPatientsViewModel.cs`,
  `Viewer/SyntheticPatientsView.cs`, new `Viewer/SyntheticPatientsView.Layout.cs`,
  `Viewer/GenerationReportView.cs`, `Viewer/AboutWindow.cs`,
  new `Docking/DefaultDockSizing.cs`.
- Tests: Core `FhirImportTests.cs`; Workspace `FhirWorkflowTests.cs`; Desktop
  `SyntheticPatientsViewTests.cs`, `SyntheticHistoryScopeViewTests.cs`,
  `ResponsiveToolTests.cs`, `ShellFinishingTests.cs`, new `FhirWorkspaceTests.cs`.
- Documentation: `README.md`, `docs/PROJECT_CONTEXT.md`, `docs/architecture.md`,
  ADRs 0003/0015, `docs/synthea-fhir-import.md`, `docs/responsive-tool-layout.md`,
  `docs/stage15ui-acceptance.md`, new `docs/fhir-patient-workspace.md`,
  `docs/data-handling.md` and this report.

Git remains on `main`, HEAD `f2a5cec`, index empty: 57 modified tracked files and
55 untracked files across the existing 15A–15UI work and this follow-up. No commit,
push, packaging or protected-beta modification occurred. Existing uncommitted
SQL engine/enforcement and output-workflow source files retain their pre-task hashes.
Ignored evidence retains verification logs, manifests and synthetic screenshots;
permanent tests/fixtures remain. Owned disposable native-probe files and the repair
intermediate were removed without touching pre-existing files or build outputs.
