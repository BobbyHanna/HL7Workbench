# Stage 10 — Field Extraction and Analysis

## Native acceptance and checkpoint authorization — 2026-10-21

The user confirmed successful Stage 10 native Windows acceptance and authorized
one exact 33-path local checkpoint after a fresh established gate, message
`feat: add Stage 10 HL7 field extraction and analysis`, expected parent
867016c3d1eab969c4a1853994eedb95621ad4f0. Earlier pending/no-commit statements
below are historical. Preserve the excluded untracked HL7 and all protected assets.
After a clean checkpoint, Stage 11 Synthea/FHIR integration is authorized but must
remain unstaged/uncommitted and stop for native acceptance. No push, tag, package,
deploy, release or publication. Checkpoint evidence: artifacts/stage10-checkpoint-20261021.

Fresh checkpoint gate passed: locked restore; Release 0 warnings/errors (1.40s);
1,602 solution tests (2m 51.567s), 761 complete Workspace stress tests (4m 45.043s),
no failures/skips; both ScriptHost Release-layout lifecycles, API-reference sync,
eight exact fixture blobs, all 13 protected hashes, unchanged icon derivatives and
tracked/new text whitespace. Accepted implementation/test hashes are unchanged;
only native-acceptance/authorization records changed before the checkpoint. Its
commit hash and post-commit inventory are recorded in the checkpoint evidence.

Implemented under the sequential checkpoint/Stage 10 authorization of 2026-10-21.
Stage 10 awaits native Windows acceptance. All Stage 10 paths remain unstaged and
uncommitted. No push, tag, package, deploy, release, publication or Stage 11.

## Accepted Stage 9C checkpoint

Exactly one local commit was created: **867016c3d1eab969c4a1853994eedb95621ad4f0**,
`feat: add Stage 9C scripting API reference and templates`, parent
ea897decdbd79d067d13552e972ab456885a61d6. Its exact accepted 28-path inventory was
audited before staging and after commit. The post-commit tracked tree and index were
clean; the excluded untracked user HL7 was preserved. Fresh verification passed
locked restore, zero-warning Release build, **1,544 solution tests**, **733 complete
Workspace stress tests**, both ScriptHost lifecycles, generated-reference sync,
eight exact fixture blobs, all 13 protected hashes and whitespace. No external Git
operation occurred. Evidence: ignored artifacts/stage9c-checkpoint-20261021.

## Implementation and requirement audit

- One ordinary optional Field Extraction Dock tool, opened by View or the explicit
  document action; one open authority and explicit source selection/Use active
  document. Current Message and Whole Document use the existing immutable capture,
  lazy indexed snapshots and draft references. No source bytes, edits, undo history,
  save points or navigation change during extraction/filtering/export.
- Core validates paths and scans structural coordinates. Explicit segment occurrence,
  repetition, component and subcomponent intent survives saving, including `[1]`.
  Custom delimiters and raw/decoded escapes follow Core; opaque/ambiguous structures,
  invalid escapes, unreadable bytes and oversized values are diagnosed explicitly.
  Missing segment/field/repetition/component/subcomponent, empty, HL7 null and value
  remain distinct. Attachments are not decoded/rendered.
- First Match emits one row/message. Expand Matches uses ordinal zip, scalar
  broadcast and explicit MissingAlignment rather than a Cartesian product. Every
  selected cell retains its actual path; clinical groups are not inferred. This
  tradeoff was documented before implementation in [ADR 0014](decisions/0014-read-only-field-extraction.md).
- Private disk-backed authoritative result journal, bounded row/identity metadata,
  100-row virtualized pages and 256-character presentation previews. Background
  filtering/sorting reads stored values, with bounded keys/cancellation and stable
  source-order ties. Literal case-insensitive contains/exact, separate state filters,
  invariant decimal comparisons and single-column text/numeric sorts are supported.
- Captured document/revision and live stale/closed status are always shown. Stable
  MessageIds resolve rows after logical insertion; tab switches do not retarget them.
  Source edits/closure cancel an in-flight scan. Prior completed results survive a
  canceled/failed replacement. Replaced/closed document instances cannot inherit old
  rows; stale surviving messages can still be navigated in their original open source.
- Immutable duplicable Interface Overview (MSH-9, MSH-10, PID-3, PID-5, PV1-19, OBR-4),
  separate user configuration CRUD, editable/reorderable labeled columns, canonical
  selectors, mode/decoding/filter/sort preferences. Validated versioned bounded JSON,
  optimistic revisions, atomic writes, duplicate-name/header guards and corruption
  handling preserve drafts/newer files. Comparison literals/source/results never
  enter saved definitions. User-authored names/labels should contain configuration
  descriptions, not patient data.
- Complete applied filtered/sorted selection exported across all pages, independent
  of highlighted navigation row. Shared pure Core CSV formatting preserves existing
  scripting policy: UTF-8 without BOM, CRLF, quoted/escaped cells, formula guard.
  Five captured-source metadata columns plus value/state/status/path companions per
  configured column preserve value-state fidelity and order. Existing atomic file
  publication/consent/version checks prevent partial files; source aliases (including
  Windows hard links), open-source destinations and owned storage are protected.
- Bounded count-only summary describes the completed extraction before filtering:
  examined/matched messages, stored rows, per-column missing/empty/null/diagnosed and
  bounded distinct values, unreadable/expansion-limited counts, truncation, last
  operation cancellation and source freshness. No extracted values or comparison
  literals are logged or serialized into layouts. Closing/resetting the tool drains
  result leases and clears session literals; a separate configuration draft survives
  same-session reopening and participates in application shutdown Save/Discard/Cancel.

No new dependency, alternate HL7 parser, document-processing engine, worker protocol,
Dock layout schema or message identity/save architecture was introduced. ScriptBatch
only shares the existing CSV record policy through Core; its API/bounds/lifecycle are
unchanged. Native editor history routing and transaction-safety files are unchanged.

## Permanent regression coverage and verification

Synthetic-only additions: **22 Core**, **28 Workspace/library**, **8 Desktop** cases.
They cover selectors/coordinates/states/custom escapes, deterministic expansion and
limits, immutable captures, stale/closed/replaced sources, stable navigation, literal/
state/numeric filter/sort, all-page/full-value/formula/state CSV, cancellation/atomic
export/quota/source aliases, result read-lease drainage, immutable/CRUD/corrupt/conflict
configurations, privacy-safe schema/stringification, bounded values/metadata, actual
View/document integration, virtualized paging, Enter navigation, light/dark narrow/
wide layouts and docked/floating placement restoration without retained results.
Existing test coverage is retained. The exact existing View-menu inventory assertion
now includes Field Extraction; no prior assertions or tests were removed.

Measured on Windows, .NET SDK 10.0.401 / runtime 10.0.12, Release:

| Check | Result |
|---|---|
| Locked restore | Passed; all lock files retained |
| Release build, `-warnaserror` | Passed; 0 warnings, 0 errors (0.78s) |
| Focused Core | 22 passed (0.152s) |
| Focused Workspace/library | 28 passed (1.219s) |
| Focused Desktop plus existing affected workflows | 86 passed (36.969s) |
| Shared-CSV/API/real-worker Scripting | 63 passed (20.544s) |
| Full solution | **1,602 passed**, 0 failed/skipped (2m 59.818s) |
| Complete Workspace with stress enabled | **761 passed**, 0 errors/failed/skipped/not-run (5m 12.293s) |
| ScriptHost Current and Whole lifecycles | Both passed against existing Release layout |
| Generated API-reference synchronization | Passed |
| Fixture and protected integrity | 8 exact tracked fixture blobs; all 13 protected hashes; unchanged ICO/master derivatives |
| Whitespace | `git diff --check` and all 18 new text files passed |

The focused Scripting run includes all 18 executable reference examples and the
143-member public inventory. The full suite includes existing search, scripting,
save, comparison, de-identification and native-editor transaction/history regressions.
No test was weakened or removed. Build/test logs, focused/stress XML and verified
implementation/test input hashes are retained under ignored artifacts/stage10.

Reproduction commands (telemetry opt-outs apply as in the existing repository gate):

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore -warnaserror
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
$env:HL7WORKBENCH_STRESS = '1'
dotnet run --project tests/HL7Workbench.Workspace.Tests -c Release --no-build --no-restore -- -result-xml artifacts/stage10/final-workspace-stress.xml
Remove-Item Env:HL7WORKBENCH_STRESS
./build/VerifyScriptPublish.ps1 -PublishDirectory src/HL7Workbench.Desktop/bin/Release/net10.0
python -B build/WriteScriptApiReference.py --check
git diff --check
```

VerifyScriptPublish checks worker lifecycle/shared state/CSV/completion/deferred
output/clean exit and dependency/hash isolation in the existing build directory;
it does not invoke publishing or packaging. Focused commands/class selections and
outcomes are recorded in the corresponding final-focused logs/XML.

Final integrity recheck confirms implementation/test hashes are identical to the
verified build. HEAD remains 867016c3d1eab969c4a1853994eedb95621ad4f0; the index is
empty. Exactly 33 Stage 10 paths remain unstaged (15 tracked modifications, 18 new),
plus the unchanged excluded untracked HL7. All eight fixture blobs, 13 protected
hashes, original artwork, ICO/master derivatives and final text whitespace match.
Only measured-result documentation updates followed testing. No Stage 10 commit or
external Git/publication operation occurred.

## Exact Stage 10 changed-file inventory

**33 paths: 15 modified tracked files and 18 new files.** No Stage 10 path is staged.

Modified tracked (15):

```text
README.md
docs/PROJECT_CONTEXT.md
docs/architecture.md
src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/MainWindow.Editing.cs
src/HL7Workbench.Desktop/MainWindow.axaml
src/HL7Workbench.Desktop/MainWindow.axaml.cs
src/HL7Workbench.Desktop/Viewer/ConfirmationDialog.cs
src/HL7Workbench.Desktop/Viewer/FileDocumentView.cs
src/HL7Workbench.Desktop/Viewer/ViewerRequests.cs
src/HL7Workbench.Scripting/ScriptBatch.cs
src/HL7Workbench.Workspace/FileSaveDestination.cs
src/HL7Workbench.Workspace/MessageDocument.Capture.cs
tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs
```

New (18):

```text
docs/decisions/0014-read-only-field-extraction.md
docs/field-extraction.md
docs/stage10-extraction-analysis-acceptance.md
src/HL7Workbench.Core/CsvRecordEncoder.cs
src/HL7Workbench.Core/Hl7FieldSelection.cs
src/HL7Workbench.Desktop/MainWindow.Extraction.cs
src/HL7Workbench.Desktop/ViewModels/FieldExtractionViewModel.cs
src/HL7Workbench.Desktop/Viewer/FieldExtractionView.cs
src/HL7Workbench.Workspace/ExtractionConfigurationSession.cs
src/HL7Workbench.Workspace/ExtractionLibrary.cs
src/HL7Workbench.Workspace/ExtractionModels.cs
src/HL7Workbench.Workspace/ExtractionResults.cs
src/HL7Workbench.Workspace/FieldExtraction.cs
src/HL7Workbench.Workspace/FieldExtractionSession.cs
tests/HL7Workbench.Core.Tests/FieldSelectionTests.cs
tests/HL7Workbench.Desktop.Tests/FieldExtractionViewTests.cs
tests/HL7Workbench.Workspace.Tests/ExtractionLibraryTests.cs
tests/HL7Workbench.Workspace.Tests/FieldExtractionTests.cs
```

The additional untracked samples/synthetic-only/stage5b-repeated-patients_TEST.hl7
is explicitly excluded and unchanged; its contents were not opened. No fixture,
artwork, local configuration or generated build output is included. Ignored Stage 10
build/test logs, XML, input-hash manifest and changed-file inventory are intentionally
retained, including reviewed narrow-layout/floating-placement failure diagnostics.
No permanent tests, existing user files or build-output directories were deleted.

## Known limits

See the [user guide](field-extraction.md) for full path/state/CSV rules. Ceilings:
32 columns, 4,096 rows/message, 1,000,000 stored rows, 1 GiB journal, 65,536 UTF-16
units/value, 16 MiB encoded row, 4 MiB sort keys, 1,024 distinct hashes/column,
4 GiB CSV and 256 user definitions of at most 128 KiB each. Values beyond limits
are unavailable; row/storage/expansion limits are explicitly truncated. Distinct
counts saturate with a lower-bound label. CSV cannot recover excluded rows/values.

Expansion is deterministic ordinal alignment rather than a clinical group join.
Filtering supports one selected column/operator at a time; sorting supports one
selected column with stable ties. Comparison numbers use invariant decimal range,
not locale-specific numbers or an analytics engine. Large uncached sorts can be slow.
Full result values are disk-backed; bounded per-row/message metadata and indexes
scale with row count. The 25,000-row stress and 16 MiB selected-large-value case
exercise these bounds; they do not prove every maximum combination on every device.

The table is a bounded preview; complete stored values are available through explicit
CSV. Formula protection intentionally prefixes dangerous CSV strings, including
negative-looking numbers, following the existing policy. This is read-only extraction,
not de-identification, a PHI classifier or a conformance guarantee. Temporary journals
follow existing local snapshot cleanup without encryption/secure-erasure guarantees.
Saved layouts restore placement, not results. Native file dialogs, OS focus/DPI,
floating windows and spreadsheet rendering still need manual acceptance.

## Native Windows acceptance — pending

Use the existing Release Desktop build without a debugger and synthetic inputs only.
Never open the excluded _TEST.hl7. Use scratch export destinations outside source
fixtures. No packaging or publication is needed.

1. Open **View → Field Extraction** without a document, then with one. Verify one
   modeless dock tool, standard keyboard focus and disabled Extract without a source.
2. Open samples/synthetic-only/stage5b-repeated-patients.hl7. Use its **Extract fields**
   action and verify explicit source selection. Switch tabs and confirm results do not
   silently follow; exercise **Use active document** and both scope choices.
3. Load **Interface Overview**, choose Whole Document and Extract with Edit Mode off.
   Confirm one row/message, source/revision/current label, pager and summary.
4. Inspect MSH/PID values against the source; use the synthetic ORM/ORU fixtures or
   generator for PV1/OBR positions. Verify absent fields versus empty and HL7 `""`
   in a separately edited synthetic draft, without saving over fixtures.
5. Double-click a row, then use selected-row Enter and **Go to source**. Verify the
   original document/message activates. Switch tabs and navigate again; insert a
   synthetic message before a target and verify its stable identity still resolves.
6. Duplicate the preset, add labeled `PID-5.1` and `PID-5.1.1` columns, reorder/remove
   columns, Extract and inspect paths/states. Try an invalid path and duplicate label;
   expect a fixed error with the prior completed result and source retained.
7. Use synthetic repetitions such as `PID-3` and repeated OBX segments; add
   `PID-3[2]`, `OBX[2]-5` and explicit `[1]` selectors. Verify actual cell paths,
   custom delimiters and decoded/raw escapes, including a diagnosed unsupported escape.
8. Compare First Match and Expand Matches. Verify ordinal alignment, scalar broadcast,
   MissingAlignment and absence of Cartesian multiplication. Review limit/truncation
   labels when exercising a generated high-match scratch input.
9. Apply contains/exact, missing/empty/null and numeric filters; press Enter in the
   literal input. Sort using a header and numeric/descending controls. Change pages,
   verify all selected rows follow the same order and reset with No filter/Source order.
10. Save/rename a user copy, Duplicate it, edit its columns, Load/reopen it across tool
    closure and application restart. Verify built-in immutability, Save/Discard/Cancel,
    user-copy independence and absence of saved comparison literals/source/results.
11. Export a filtered/sorted selection exceeding one page. Inspect UTF-8 CSV headers,
    all-page count/order, state/status/path companions, quotes/newlines and guarded
    formula-like synthetic values. Cancel export/decline replacement; confirm no partial
    output and original destination retained. Source HL7 must remain unchanged.
12. Edit a source message, verify stale labeling and captured results remain; rerun
    and verify refreshed values/revision. Close/reopen the source and confirm old rows
    cannot navigate to the replacement. Review/export retained captured results safely.
13. Cancel a long extraction, filter/sort and export; verify clean recovery, no partial
    replacement/publication and successful rerun. Close/reset the tool during work;
    reopen to an intact configuration draft with released results/literals.
14. Use a large synthetic document/message; navigate/switch tabs while extracting,
    inspect bounded pages/previews and summary, cancel and rerun. Confirm responsiveness
    and no source dirty/history changes caused by extraction/filter/sort/export.
15. Dock/float, resize narrow/wide, switch light/dark themes and restore layout. Verify
    table/toolbars scroll, controls/summary remain readable, Enter navigation works and
    text inputs retain normal local editing/Undo/Redo. Check native DPI/window placement.
16. Recheck Search, message comparison, Save/Undo/Redo, Script Editor/API Reference and
    both-scope scripting/de-identification with output reports. Confirm icon appearance
    remains intact. No extraction values or comparison literals should enter Activity Log.

**Stop for Stage 10 native Windows acceptance. Do not stage or commit Stage 10.**
