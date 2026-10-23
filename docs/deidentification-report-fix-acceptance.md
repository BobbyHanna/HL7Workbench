# Beta correction — De-identification Report and review status

Date: 2026-10-23. Base: afe7f6576f67ce25e72caa298adc94694d611d56.
The user reports all nine native Windows acceptance checks passed (2026-10-23).
One local checkpoint and a replacement self-contained candidate from committed source
are now authorized, followed by the complete release gate and exact-ZIP VM acceptance.
No amendment, push, tag, upload, publication, distribution or new milestone is authorized.
Earlier pending/stop and 30-file inventory statements below are historical records
of the implementation gate; checkpoint/package evidence is retained separately under
artifacts/deidentification-checkpoint/.

The user explicitly approved including the two prepared packaged documentation
updates with the original 30 accepted paths in this single **32-file checkpoint**:
docs/beta-readme.md and docs/release-notes-template.md. This resolves the original
30-file limit versus packaged-documentation requirement. Notices and proposed beta
usage terms remain unchanged. Final post-checkpoint gate/package/provenance results
are recorded in the ignored checkpoint evidence, without uncommitted package inputs.

## Root causes and reproduced findings

FileDocumentView mounted an expanded TransformationReportView in the RAW navigation
area. It was not a Dock tool. The Script Editor itself retains only CSV report
actions; it does not own this inline text. The old document report correctly retained
source capture attribution, but did not assess subsequent output edits.

DeidentificationSummary.Status and both report headers were hardcoded to Review
required, independent of execution or findings. The summary CSV contained only
aggregate counts, so neither it nor the UI could distinguish routine retained fields
from retained clinical content or explain individual high-risk occurrences.

The default built-in synthetic ADT reproduces **exactly 1 message / 28 transformed /
7 suppressed / 1 high-risk / 8 retained**. This establishes a matching synthetic
scenario; no screenshot source payload was supplied and matching counts alone cannot
prove it was the same input. For this reproduced scenario:

| Finding or retained group | Paths | Classification |
| --- | --- | --- |
| Unqualified provider | PV1[1]-7 | High risk: assigning authority absent; synthetic replacement cannot establish cross-message identity consistency |
| Retained event/clinical codes | EVN[1]-1, PV1[1]-2, PV1[1]-4 | Three retained values requiring review; event, patient class and admission codes can contribute to identifying clinical patterns |
| Routine protocol/sequence fields | MSH[1]-9, MSH[1]-11, MSH[1]-12, PID[1]-1, PV1[1]-1 | Five retained values without a specific review finding under the supported policy |

EVN-1 remains an event code even if numeric; it is not a Set ID. None of the former
high-risk or retained counters were reduced. No patient transformation was changed.
Suppressed narrative/unsupported content still flags its structural context for review.
Narrow Identifier/Timestamp presets continue to flag content they leave untouched.

## Current behavior

- View → De-identification Report or the compact document View report button opens
  one optional dockable/floating tool. It follows the selected output, clears for a
  plain/source tab, and releases its projection when closed. Reopen/reconstruction
  retain the document-owned original report. Layouts persist placement only.
- Summary separates execution completion, specific review counts, categories and
  general limitations. Status is **Completed — review required** for concrete counts
  or **Completed — no flagged findings** otherwise. Neither label certifies privacy.
- Findings show captured source message positions, HL7 field paths and fixed reasons.
  Unknown source segment names and original values are not rendered. Category counts
  have aligned columns; findings are selectable, wrapped and independently scrollable.
- Each report has a session-local producing execution ID, captured source revision
  and output baseline revision. Source edits/closure and output edits have distinct
  stale labels. Current-message workflow ordinals resolve through the captured target
  ordinal. Later navigation or reordered messages never reinterprets those positions.
- `deidentification-review` CSV preserves existing column names/order and appends
  Retained values requiring review / Omitted findings. Status and unassessed-scope
  wording change intentionally. `deidentification-findings` adds up to 1,000 fixed
  structural rows; omitted occurrences are disclosed while counts remain exact.
- Both CSVs use the existing bounded transactional journal and explicit export UI.
  Missing/invalid detail schemas show details unavailable; they cannot insert arbitrary
  text into this privacy tool. User-script CSV cells remain subject to existing
  explicitly requested exports. The latest successful run owns that export journal;
  document reports survive its disposal and subsequent runs.

## Limits and warnings retained

This is a supported-policy aid, not a PHI detector, anonymity guarantee, clinical
conformance check, HIPAA certification or permission to disclose. Container bytes,
nested metadata and clinical patterns remain unassessed outside the supported rules.
General limitations remain visible even when there are no specific flagged findings.
High-risk counts are occurrences and may overlap other count categories. Findings
are field-level coordinates, not original values or a complete leaf audit/crosswalk.
All original edit, mapping, cancellation, report, worker and publication limits remain.
Saved script copies do not acquire the new findings emission automatically; duplicate
the current preset to obtain it. Existing summary-only scripts show details unavailable.
Reports are session-only and are not persisted in saved HL7 or layout files.
The existing conservative suggested filenames containing Review Required remain;
they are static output names, not the calculated findings status.

## Verification

Evidence is retained in ignored `artifacts/deidentification-report-fix/`.

| Check | Actual result |
| --- | --- |
| Locked solution restore | Passed |
| Final Release build with warnings as errors | 0 warnings / 0 errors; 1.72 seconds |
| Focused Scripting, de-identification and public API/worker examples | 70 passed; 20.241 seconds |
| Focused Workspace, CSV/report/source revision and library | 40 passed; 49.062 seconds |
| Focused Desktop de-identification and Generation Report | 12 passed; 15.067 seconds |
| Full solution | 1,973 passed / 0 failed / 0 skipped; 3m 56.013s |
| Complete unfiltered Workspace stress | 940 passed / 0 errors / 0 failed / 0 skipped / 0 not run; 376.903 seconds |
| Normal Release worker smoke | Current and Whole Main lifecycles, shared state, CSV, deferred output, completion, clean exit and dependency isolation passed |
| Embedded API reference synchronization | Passed |
| Protected/integrity and whitespace | Post-stress repeat passed: 13 protected hashes, 2 icon derivatives, 17 fixture/artwork Git blobs, 25 frozen implementation/test/API paths; tracked and all 7 new-file whitespace checks passed |

Twelve new permanent cases cover no findings, legitimate risks, retained review versus
routine retention, generic limitations, exact native-count reproduction, omitted
detail bounds, real-worker safe CSVs, captured Current scope mapping, untrusted
finding rejection, actual shell opening/focus/tab lifecycle, floating placement and
view reconstruction. Existing cases retain byte/encoding/cancellation/atomicity and
source edit-during-run coverage and now also assert output freshness and per-run IDs.
The initial broad run passed 1,972 cases and found one stale menu-inventory expectation;
adding the new menu entry while retaining all old assertions fixed it. Its diagnostics
remain in full-solution-initial-menu-expectation.log. The final full run passed all cases.

The verified ZIP remains byte-identical: 95,843,830 bytes; SHA-256
`B989FD86BFFCDEDA818FAB20E5D552FB38BFB8662F4A0AF26EEF8AF0482C2919`.
No package was built/overwritten. Headless tests exercise the real Avalonia shell/Dock;
they do not establish native Windows visual/DPI or user acceptance.

Final state: 23 modified tracked paths and seven new paths, all unstaged/uncommitted;
empty index, unchanged HEAD. The full solution and complete stress suites apply to
the frozen final implementation/test/API files. Only acceptance/result Markdown was
updated afterward. Native acceptance is pending; no further Git or packaging action.

## Native Windows acceptance checklist

1. Run the current development Release build without replacing the verified ZIP.
   Generate default synthetic ADT, run Patient Deidentifier. Confirm completed status,
   28/7/1/8 totals, three retained-review values and the four specific finding paths above.
2. Confirm no expanded raw report remains in RAW or Script Editor. Open the report
   from its compact row and View menu; repeated opens activate the same tool.
3. Dock, float, resize narrowly, scroll and select/copy report text under both themes
   and normal/high DPI. Verify category alignment, focus, keyboard and readability.
4. Run a minimal qualified synthetic PID message with only protocol/sequence retained
   fields. Expect Completed — no flagged findings plus visible general limitations.
   Add synthetic NTE/unsupported content or an unqualified provider; expect review.
5. Run Current on message 2 and Whole. Verify captured message positions and per-run
   reports, then switch output/source/plain tabs, close/reopen/reconstruct the tool.
6. Edit the source and output independently. Confirm distinct stale labels, original
   immutable findings, and no claim that later text was reassessed. Close the source;
   the output report must remain readable with Source closed attribution.
7. Export both CSVs explicitly from the last run; confirm summary order/appended
   columns, fixed finding coordinates/reasons and absence of original synthetic
   names/IDs/values. Run another script and verify old document reports still exist.
8. Verify cancel/failure publishes no partial output/report and ordinary scripts,
   original Save As, undo/redo and isolated worker behavior remain functional.

Stop for user acceptance before any commit or replacement portable ZIP.

## Changed-file inventory

All 30 paths below belong to this correction (23 modified tracked, seven new).

```text
docs/PROJECT_CONTEXT.md
docs/decisions/0013-script-deidentification-aids.md
docs/deidentification-report-fix-acceptance.md (new)
docs/script-api-reference.json
docs/script-api-reference.md
docs/stage9-deidentification-acceptance.md
src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/MainWindow.axaml
src/HL7Workbench.Desktop/MainWindow.axaml.cs
src/HL7Workbench.Desktop/ViewModels/DeidentificationReportViewModel.cs (new)
src/HL7Workbench.Desktop/Viewer/DeidentificationReportIndicator.cs (new)
src/HL7Workbench.Desktop/Viewer/FileDocumentView.cs
src/HL7Workbench.Desktop/Viewer/TransformationReportView.cs
src/HL7Workbench.Desktop/Viewer/ViewerRequests.cs
src/HL7Workbench.Scripting/DeidentificationFinding.cs (new)
src/HL7Workbench.Scripting/DeidentificationOptions.cs
src/HL7Workbench.Scripting/DeidentificationWorkflow.cs
src/HL7Workbench.Scripting/DeidentificationWorkflow.Demographics.cs
src/HL7Workbench.Scripting/DeidentificationWorkflow.Fields.cs
src/HL7Workbench.Workspace/ScriptExecution.cs
src/HL7Workbench.Workspace/ScriptLibrary.Deidentification.cs
src/HL7Workbench.Workspace/TransformationReport.cs
tests/HL7Workbench.Desktop.Tests/DeidentificationReportToolTests.cs (new)
tests/HL7Workbench.Desktop.Tests/DeidentificationViewTests.cs
tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs
tests/HL7Workbench.Scripting.Tests/DeidentificationReviewTests.cs (new)
tests/HL7Workbench.Scripting.Tests/DeidentificationTests.cs
tests/HL7Workbench.Workspace.Tests/DeidentificationExecutionTests.cs
tests/HL7Workbench.Workspace.Tests/DeidentificationReportTests.cs (new)
```

Owned ignored verification logs, XML results and hash manifests are intentionally
retained under artifacts/deidentification-report-fix/, including the reviewed initial
shell-menu expectation failure. No protected fixture, user file or permanent test
was removed; the excluded TEST file was hash-checked only.
