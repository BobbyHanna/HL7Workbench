# Stage 15UI — responsive tool infrastructure acceptance

The subsequent [FHIR Patient Workspace redesign](stage15ui-fhir-workspace-acceptance.md)
supersedes this report's Synthetic Patients layout/confirmation and default sizing
details. The original evidence below is retained; native testing subsequently found
UX defects despite the automated pass.

Source implementation for review, 2026-10-27. Stop before Stage 15D. No commit,
push, release packaging or accepted-beta rebuild is authorized. Native visual
acceptance is **not** inferred from automated tests.

## Starting point and completed work

Resumed the existing uncommitted 15A–15C and partial 15UI work rather than recreating
or reverting it. The initial helpers and Synthetic Patients/SQL migrations compiled,
and their original workflow suites passed. The new layout matrix was unfinished:
11 of 14 cases failed. Activity and several additional migrations were in progress;
developer/acceptance documentation was missing.

Root causes were horizontal fixed-width action strips, whole-tool minimum widths,
too many Auto sections starving star content, and nested finite Grids whose clipped
DesiredSize concealed overflow. A browser control minimum without a matching star-row
minimum also allowed expanded details to overlap it. Wrapping alone did not make lower controls reachable.
An unbounded outer scroller would instead expand editor/list content and defeat
virtualization. Dock template replacement also exposed editor selection/undo loss,
including replacement attachment before old-view detachment.

Completed Desktop-only infrastructure:

- `ToolLayoutMetrics`: DIP spacing, width guidelines and practical content minimums.
- `AdaptiveActionBar`: native wrapping, bounded child widths, wrapping action text,
  stable logical order, no action cloning/hiding or horizontal toolbar clipping.
- `ResponsiveFormField`: associated label/input, aligned wide and stacked compact
  forms without reconstructing inputs.
- `ToolContentGrid`: native row sizing with an explicit vertical overflow extent,
  including nested/expanded Auto content.
- `ResponsiveToolSurface.Attach`: one-time composition, finite editor/list
  measurement, vertical overflow and optional bounded primary actions.
- Internal Desktop `ToolEditorState`: session-only TextDocument/undo/selection
  handoff, weak editor owner and disposal cleanup. No new persistence.

No dependency, Core, Workspace, SQL enforcement, source identity/extraction, FHIR
parsing or generation change was made in 15UI. Existing layout persistence and
default Dock arrangement remain intact.

## Tool audit

| Tool | Changes / boundary |
|---|---|
| Synthetic Patients / FHIR Import | Pinned adaptive import/cancel/clear, wrapping warning/confirmation, search, pagers and generation options; finite tabbed lists; nested details and preview/status overflow reachable. Session/privacy/output semantics retained. |
| SQL Query | Pinned Run/Cancel; adaptive saved queries/scope/history/result actions; finite editor/results; full-value and status overflow. Horizontal data-table scrolling remains deliberate. Existing paging/sorting/export/extraction unchanged. |
| Script Editor / Library | Adaptive library/execution controls, finite editor, wrapping feedback, reachable overflow menu and zoom actions; selection and undo retained across resize/template handoff. |
| Field Inspector / Segment Navigator | Wrapped labels, adaptive lookup/chunk controls, finite tree/list, scrolling details; combined document panes stack below 600 DIP. Structural selection and bounded values retained. |
| Activity Log | Adaptive filters/actions, finite virtualized log and vertical tool overflow; session filter/selected sequence IDs survive view replacement; existing safe copy, retention anchoring and attach/detach behavior retained. |
| Field Extraction | Adaptive library/filter/output bars, finite results, full status visibility. |
| Full Source Viewer | Adaptive range navigation and finite editor with vertical tool overflow; original source-session disposal retained. |
| Connection Manager / MLLP Listener | Responsive labeled forms, adaptive essential actions, finite configuration/history regions. No network actions are invoked by layout tests. |
| Batch Transmission / Load Testing | Responsive forms and adaptive primary bars, Cancel/Stop first, finite form/results and reachable status/metrics. Existing configuration/history inner scrolling is intentional. |
| Script API Reference | Adaptive search/actions, cached width transition, finite topic/detail panes; existing bounded code examples unchanged. |
| Find Messages | Shared adaptive action/library/page bars only. Existing responsive persisted builder/results split retained. A complete extreme-height composition migration is deferred. |
| Message Comparison | Shared adaptive action bar only. Existing source chooser, side-by-side editors and persisted split proportions retained. Very small source-choice/short split layouts need a dedicated follow-up. |
| Generation / De-identification Reports | Existing wrapping vertical report scrollers retained; category heading/labels now wrap. |
| Browser / legacy Diagnostics | Existing finite list and wrapping diagnostic text need no composition replacement. |

Search/Comparison are explicit audit limitations, not claims of full tiny-tool
acceptance. They were kept within the instruction to document larger work rather
than redesign unrelated split persistence.

## Automated layout matrix

Individual tool content is constrained inside a larger fixed host, with separate
header/tab sentinels. These dimensions are **not just main-window dimensions**.

| Tool content DIP | Synthetic import/search/preview/generate | SQL query/page/full-value/output actions | Script controls/editor/undo |
|---|---|---|---|
| 1200 × 800 | Pass | Pass | Pass |
| 900 × 600 | Pass | Pass | Pass |
| 600 × 400 | Pass | Pass | Pass |
| 350 × 300 | Pass, vertical travel | Pass, vertical travel | Pass, vertical travel |
| 250 × 250 | Pass, vertical travel | Pass, vertical travel | Pass, vertical travel |

23 added responsive cases also cover 1.0/1.25/1.5 **layout-transform emulation**,
180 DIP stress widths, repeated threshold crossing in both directions, height-only
changes, stable extents, non-overlapping action rectangles, actual scroll-viewport
intersection, wheel/PageDown/focus access, finite virtualized realization, expanded
clinical/full-value details, large-value inspector chunk navigation, model selection,
SQL page/journal retention, synthetic session retention and Script undo execution.

A real DockControl test moves Script Editor from bottom to right, floats it using
FakeHostWindow ownership plus a separate headless Window, resizes it, redocks,
closes/reopens and checks retained document/undo/selection. Existing SQL lifecycle
coverage checks release-on-close, independent snapshots, cancellation draining and
placement-only persistence. FakeHostWindow cannot establish native floating chrome,
drag gestures, per-monitor DPI or maximized/restore visual behavior.

Rendered inspection caught the expanded clinical browser/details overlap after an
initial passing matrix. Tests were strengthened with section-level boundary checks,
not just action-bar rectangles. The final correction reserves the browser star row
and remeasures nested content at its complete finite extent. Earlier failures and
superseded verification logs remain available for review.

## Verification evidence

Evidence lives under ignored `artifacts/stage15ui/`; permanent tests remain in the
repository. Earlier failing diagnostics are retained for review. The full verification
run uses a fresh isolated artifacts path, then copies byte-identical runtime trees
to ignored `bin/Stage15UI/net10.0` directories because existing branding tests derive
repository/configuration paths from the standard output layout. Normal running app
outputs and protected preview artifacts are not overwritten.

| Check | Result / evidence |
|---|---|
| Normal locked restore | Passed; `restore-normal.log`. |
| Isolated locked restore | Passed; `restore-final-isolated.log`. |
| Release solution build, warnings as errors | Passed, 0 warnings / 0 errors; `build-complete.log`. |
| Complete five-suite regression run | 2,152 passed, 0 failed/skipped; 6m 59.9s; `full-regression.log`. |
| Final rendered responsive matrix | 23 passed, 0 failed/skipped; `rendered-complete.log`. |
| Final Desktop regression rerun | 428 passed, 0 failed/skipped; 2m 05.5s; `desktop-complete.log`, after the final layout and Activity Log corrections. |
| Protected beta files | All 2,384 files byte-identical; no added/missing files; `protected-preview-check.json`. |
| Whitespace / index | `git diff --check` clean; staging index empty. |

The full five-suite run preceded the final clinical section-boundary and Activity
Log state corrections. Every Desktop test is rerun on those final sources; the
other four test DLLs remain byte-identical to the complete run. Copied runtime
test DLL hashes match the isolated build (`verified-test-dlls.json`). No tests were
removed or weakened: older toolbar-shape assertions were updated to native wrapping
and real viewport/boundary assertions while retaining functional and splitter tests.

23 nonblank synthetic Skia frames were captured in `rendered/`. Representative
wide/narrow Synthetic Patients, dark SQL, light Script Editor and Field Inspector
(including a large value) and Activity Log frames were inspected. The corrected
clinical layout separates browser, pager, expanded details and generation controls;
some content is deliberately outside the current scroll viewport. These frames
establish rendered headless evidence only, not native Windows acceptance.

Reproduce the full suite from the verified standard-shaped runtime directories:

```powershell
dotnet test --test-modules 'tests/*/bin/Stage15UI/net10.0/*.Tests.dll' --root-directory C:/Dev/HL7Workbench --max-parallel-test-modules 1 --minimum-expected-tests 2152
```

Ignored retained artifacts are build/runtime outputs, verification logs (including
useful earlier failures), manifests and synthetic screenshots. No one-off probe
programs or generated HL7/CSV exports were added. Permanent regression tests and
fixtures are preserved. Git remains on `main`, HEAD `f2a5cec`, with 42 modified
tracked files and 49 untracked files across 15A–15C plus 15UI. Stage 15UI accounts
for 29 changed/added paths (`changed-files.json`); only four pre-existing 15A–15C
paths change further: project context, architecture, SQL view and SQL Desktop model.
All other captured 15A–15C source hashes are unchanged. No commit, push, release
package, accepted-beta rebuild or Stage 15D work occurred.

## Native Windows review checklist — outstanding

Use synthetic fixtures only. Launch a development build, preserving the accepted
private-preview directory and any user's existing session. Save layout metadata
only; do not persist imported content or SQL history through this review.

1. In a restored 900×600 application, open Synthetic Patients. Confirm synthetic
   data, import `samples/synthetic-only/fhir-r4/patient-alpha.json`, search `SYN-ALPHA`,
   select the patient, expand details, preview and generate. Repeat in a narrow
   right dock, short bottom dock and 600×400/350×300/250×250 floating tool. All
   import/search/page/scope/preview/generate/cancel/clear controls must be reachable.
2. Open SQL Query with synthetic HL7. In each dock/float size, edit and select SQL,
   run with Ctrl+Enter, cancel a long query, select scope/history/saved definitions,
   page/sort results, scroll horizontally, inspect a full value, copy a tabular
   selection, export complete CSV, navigate deliberately and open matching messages.
   Confirm originals stay unchanged and truncated/page previews never drive extraction.
3. Type several edits into a Script draft. Select text, resize repeatedly, move
   bottom/right/float/redock and use Undo/Redo. Verify library, API reference,
   rename/menu, Run/Cancel, diagnostics, status and zoom are keyboard reachable.
4. In Field Inspector/Segment Navigator, select a repeated field/component and a
   synthetic large OBX value. Resize/move tools and use chunk controls/copy path.
   Verify selection and chunk offset survive resizing and bounded previews remain
   labeled. Check combined document-pane splitter behavior wide and stacked.
5. Filter Activity Log, select entries and scroll away from the tail. Resize/move,
   append safe synthetic activity and verify selection/filter/anchor/copy. Close and
   reopen tools repeatedly; verify no duplicate updates or stale model callbacks.
6. Check Tab/Shift+Tab, visible focus indicators, wheel, keyboard scrolling, disabled
   action explanations, native pickers, clipboard and light/dark themes. Dock tabs,
   headers and application status bars must remain unobstructed. Check actual
   100%, 125% and 150% Windows scaling, including moving a floating tool between
   monitors. Toggle maximized/restored application state and repeat narrow docking.
7. Spot-check Field Extraction, Full Source Viewer, transport, batch/load tools and
   Script API Reference. Verify Cancel/Stop remains discoverable without triggering
   a real connection or load job. Check acknowledged Search/Comparison limitations.

Native visual acceptance remains pending until these checks are performed on real
Windows windows; headless screenshots do not substitute for it.

## Files changed by Stage 15UI

Paths below are repository-relative for documentation. Existing 15A–15C changes are
retained and are not attributed to this milestone.

- Added `src/HL7Workbench.Desktop/Viewer/ResponsiveTools.cs`,
  `src/HL7Workbench.Desktop/ViewModels/ToolEditorState.cs`,
  `tests/HL7Workbench.Desktop.Tests/ResponsiveToolTests.cs`.
- Updated Desktop Viewer `SyntheticPatientsView.cs`, `SqlQueryView.cs`,
  `ScriptEditorView.cs`, `InspectionPanes.cs`, `ActivityLogView.cs`,
  `FieldExtractionView.cs`, `FullRawInspectorView.cs`, `TransportToolView.cs`,
  `BatchTransmissionView.cs`, `LoadTestingView.cs`, `ScriptApiReferenceView.cs`,
  `SearchResultsView.cs`, `ComparisonView.cs`, `TransformationReportView.cs`.
- Updated Desktop ViewModels `ScriptEditorViewModel.cs`, `SqlQueryViewModel.cs`,
  `ActivityLogViewModel.cs`.
- Updated Desktop tests `ScriptEditorWorkflowTests.cs`, `ScriptEditorPolishTests.cs`,
  `InspectionViewTests.cs`, `WindowsAcceptanceTests.cs`; original behavior and real
  splitter/input assertions remain, with new composition-aware control lookup.
- Added `docs/responsive-tool-layout.md` and this report; updated
  `docs/PROJECT_CONTEXT.md`, `docs/architecture.md`,
  `docs/decisions/0003-dockable-workspace.md`.

## Limitations

Severely constrained hosts require scrolling; not every control can be visible at
once. Primary bars may have a small vertical fallback; tables may scroll horizontally.
Close/reopen still obeys each tool's original release/reset semantics, rather than
retaining sensitive sessions indefinitely. Only Script/SQL editor state is handed
across template replacement; native screen-reader/focus/DPI behavior needs review.
Search/Comparison need separately scoped composition work for the most extreme
heights. No Stage 15D work or broad feature redesign is included.
