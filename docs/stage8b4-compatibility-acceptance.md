# Stage 8B.4 — structural editing compatibility acceptance

Checkpoint status, 2026-10-17: the user confirmed native Windows acceptance of this
integration, the structural foundation, keyboard correction and Script Output Naming.
One local checkpoint commit is authorized. Earlier pending/no-commit statements
below are historical; the native checklist remains a permanent retest reference.
See PROJECT_CONTEXT.md for final checkpoint verification. No push, tag, publish,
release or Stage 8C/8D work is authorized.

Stage 8B.4 only, authorized 2026-10-17. Stage 8B.1–8B.3 and the native root
Undo/Redo correction are user accepted and intentionally uncommitted. They were
preserved before this work. Baseline: main at
8808929e844cb925fbdb11635e0b55740e5ec160; nothing staged. No Stage 8C insertion UI,
Next append, Stage 8D generation, dependency, packaging or publication is included.
Native 8B.4 acceptance and separate Git authorization remain pending.

## Implemented checkpoints and contracts

### 8B.4.1 — identity and capture

All retained document-backed feature selections/results use MessageId. Logical
position is presentation; original ordinal and original snapshot ranges remain
provenance. IDs never come from HL7 identifiers/content and are never persisted.
Saving does not rebase originals or create provenance for an insertion; reopening
creates a new document, IDs and original index.

Search captures copy logical metadata and immutable bounded draft references before
background scanning. Unchanged originals retain snapshot/index references and are
decoded lazily. Existing source read leases, cancellation generations and disposal
draining remain. Script capture uses its existing mutation lock/work gate and a
sealed independent snapshot. No file-wide decoded string is introduced.

### 8B.4.2 — search and filtering

Inserted current drafts and shifted edited originals participate in the same
literal/field/advanced scanner. A MessageSearchMatch carries MessageId, captured
logical MessageNumber and nullable OriginalOrdinal. Result activation resolves the
ID; it never navigates to the old captured number. Removed/stale results cannot
retarget the message now at that position.

NavigationMatchIds plus an ID membership set are the filter authority. The legacy
NavigationMatches property remains a display-only list of current logical positions.
The existing numeric navigator/dropdown resolve a position to its current ID and
check ID membership before navigating. Structural mutation clears the filter, so
no valid display projection survives a rank change. No Next-arrow append is added.

Insertion, structural Undo/Redo and text changes increment SearchRevision, cancel
affected work and clear results/filters. Session generation/revision checks reject
late progress/completion. Authored queries and saved definitions remain intact.
Navigation and successful Save alone do not invalidate valid search/filter results.
Nested ALL/ANY, existence semantics, custom delimiters, paging and library schemas
remain unchanged. Blank/incomplete inserted drafts remain discoverable according
to the same query/structural rules; discovery does not certify script/save eligibility.

### 8B.4.3 — comparison

ComparisonSource/ComparisonInput retain selected MessageIds. Their legacy Ordinal
accessor now means current logical position, zero if unavailable; OriginalOrdinal
is separate. The source picker labels logical position and preserves the selected
ID during refresh/coercion. Position entry is an explicit new selection.

Undo of an insertion leaves its comparison source selected but unavailable, without
clamping to another message. Redo resolves that same ID; old results remain stale
until a new comparison completes. Insertion elsewhere updates position labels and
invalidates content-bound results without changing the selected identity.

Inserted/current, inserted/inserted, same-/cross-document and original/edited
comparisons work. Original is always the initial source, including after Save.
An inserted original-side request gives explicit guidance; Save does not enable it.
Pasted-only comparisons remain independent of unrelated document changes. Existing
bounded readers, 64 Ki windows, clipboard/copy, scanner and difference limits remain.
The deferred approximately 100 MiB embedded-payload comparison problem is unchanged.

### 8B.4.4 — scripting

Capture reuses BuildSavePlan/MessageSavePlan: original byte partitions, bounded
payload substitution, insertion envelopes and staged-index membership/range proof.
Save still performs its complete staged byte verification/hash/publication path.
Scripting no longer has a separate original-ordinal-only composition loop.

Whole-document scope includes every live logical entry and required current draft
in order. Invalid required payloads fail before worker launch; none are silently
skipped. Current-message scope targets the selected stable ID. Inactive original
drafts use their immutable original fallback, preserving current-scope isolation.
Inactive insertions are explicitly omitted from that job's input map because they
have no original fallback; this avoids parsing/depending on unrelated invalid
insertions. The selected insertion is always required. The existing backing-file
capture/storage quota still applies, and worker payload/line/message limits remain.

ScriptInputMapping records document ID/revision and each immutable job ordinal's
MessageId, captured logical position and optional original ordinal. Job ordinals
can therefore differ from displayed logical positions in current scope. Worker
ordinals never identify a live document message. Deferred/excluded output records
each new output ID's originating input ID/job ordinal. Failure diagnostics retain
the same bounded parent-only mapping; the UI distinguishes captured message,
job ordinal and original ordinal. Double-click still navigates to the script line.

The user explicitly selected "Keep independent captured output with explicit
stale-source attribution." After input capture, source edits, insertion and structural
Undo/Redo preserve completed independent output. ScriptExecutionResult.Input retains
the captured source document/revision and immutable ordinal map; SourceChangedDuringRun
records a source-revision difference immediately before publication. Source identities,
captured positions and output attribution never resolve through newer live positions,
even when Undo removes a selected insertion. Newer source work is retained.

The Script Editor shows source document number, captured revision and an explicit "Stale source" label for
the last successful output. Reports share that producing-run attribution. It remains
visible through a later failure, script selection changes and view reconstruction;
it describes the capture-to-publication interval, not subsequent live source changes.
Source navigation or tab switching never retargets the job, and a tab switch alone
does not mark unchanged input stale. Output still opens as a separate unsaved document.
Source closure, explicit cancellation, timeout and actual worker/validation failure
still prevent publication and roll back private HL7/report artifacts. Source edits do
not mask a real worker failure. A queued job whose revision changed before capture
fails with InputChanged because it has no older sealed input yet. No source-apply or
automatic source overwrite path is introduced.

No ScriptHost lifecycle, IPC DTO/schema/version, Windows Job Object, timeout/memory
limit or Script Library schema changed. The only Scripting-facade change adds the
fixed InputChanged description. Workspace mapping metadata stays out of the worker
protocol and contains no message text or patient values.

### 8B.4.5 — Inspector, diagnostics and provenance

The existing ID-based file view reuse/selection contract is retained. Inspector
content follows the selected entry after insertion/Undo/Redo, including incomplete
drafts. Message-specific Activity diagnostics deduplicate by ID, retaining recorded
logical position, original ordinal and original coordinates separately. A position
shift cannot create a duplicate attributed to another message. Existing unsupported
boundary containers remain protected; the tests do not bypass those guards merely
to manufacture a shifted boundary-warning case.

RawInspectorSession records its ID/provenance. Shifted originals export their own
unchanged snapshot bytes; inserted sources have no original range and cannot export
a neighbor. Existing edit/navigation/close invalidation and work/read draining remain.
DocumentHistoryShortcuts and its main/floating root registration, native input
protections and all earlier history tests are unchanged from the accepted baseline.

## Changed files relative to the accepted uncommitted starting tree

The retained baseline manifest distinguishes this work from the earlier foundation
changes; Git's whole diff still includes both milestones. Paths are repository-relative.

| Area | Files |
| --- | --- |
| Shared captured metadata | src/HL7Workbench.Workspace/MessageDocument.Capture.cs (new); MessageDocument.Sequence.cs; MessageDocument.cs |
| Search/filter | src/HL7Workbench.Workspace/MessageDocument.Search.cs; MessageSearch.cs; MessageSearchSession.cs; src/HL7Workbench.Desktop/Viewer/FileDocumentView.cs |
| Comparison | src/HL7Workbench.Workspace/MessageDocument.Comparison.cs; MessageComparison.cs; ComparisonInput.cs; src/HL7Workbench.Desktop/ViewModels/ComparisonViewModel.cs; Viewer/ComparisonSourcePicker.cs |
| Script capture/attribution | src/HL7Workbench.Workspace/MessageDocument.Scripting.cs; MessageDocument.Saving.cs; MessageSavePlan.cs; ScriptExecution.cs; ScriptInputMapping.cs (new) |
| Script diagnostics/output attribution | src/HL7Workbench.Scripting/ScriptContracts.cs; src/HL7Workbench.Desktop/ViewModels/ScriptEditorViewModel.cs; Viewer/ScriptEditorView.cs |
| Original provenance/diagnostics | src/HL7Workbench.Workspace/ActivityLog.cs; RawInspectorSession.cs |
| New permanent Workspace tests | tests/HL7Workbench.Workspace.Tests/StructuralCompatibilityTests.cs; .Search.cs; .Comparison.cs; .Scripting.cs; .Provenance.cs; .Stress.cs |
| New permanent Desktop tests | tests/HL7Workbench.Desktop.Tests/StructuralCompatibilityViewTests.cs |
| Existing regression updates | tests/HL7Workbench.Workspace.Tests/StructuralSaveTests.cs; ScriptExecutionTests.cs; tests/HL7Workbench.Desktop.Tests/ScriptEditorWorkflowTests.cs; BatchEditorTests.cs |
| Documentation | docs/PROJECT_CONTEXT.md; docs/architecture.md; docs/decisions/0011-structural-editing-foundation.md; docs/stage8b-structural-editing-acceptance.md; docs/script-api.md; docs/message-search.md; docs/message-comparison.md; this document |

Workspace/Desktop abbreviations in a row share the explicitly named directory.
The six partial Workspace test files share one StructuralCompatibilityTests class.
All previous tests and tracked synthetic fixtures are retained. The obsolete temporary
guard expectations were replaced with positive identity assertions while retaining
the same original-byte-export and invalidation checks. The script conflict regression
now checks independent captured output, explicit stale attribution and newer-source
retention, preserving the user's chosen policy. No Core, ScriptHost,
package/lock file, dependency version, .gitignore or committed sample changes.

## Verification and reproducible commands

Run from C:\Dev\HL7Workbench. Run full, focused Workspace and stress suites sequentially:
the shared large synthetic fixture generator uses deterministic artifact paths.

~~~powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
dotnet tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/HL7Workbench.Workspace.Tests.dll -class HL7Workbench.Workspace.Tests.StructuralCompatibilityTests -class HL7Workbench.Workspace.Tests.ScriptExecutionTests -showLiveOutput -noLogo -noColor -result-xml artifacts/stage8b4/snapshot-policy-focused-workspace.xml
dotnet tests/HL7Workbench.Desktop.Tests/bin/Release/net10.0/HL7Workbench.Desktop.Tests.dll -class HL7Workbench.Desktop.Tests.StructuralCompatibilityViewTests -class HL7Workbench.Desktop.Tests.StructuralHistoryViewTests -class HL7Workbench.Desktop.Tests.RawEditingCrashTests -class HL7Workbench.Desktop.Tests.EditableViewerTests -class HL7Workbench.Desktop.Tests.ScriptEditorTests -noLogo -noColor -result-xml artifacts/stage8b4/snapshot-policy-focused-desktop.xml
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
$env:HL7WORKBENCH_STRESS = '1'
dotnet tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/HL7Workbench.Workspace.Tests.dll -showLiveOutput -noLogo -noColor -result-xml artifacts/stage8b4/snapshot-policy-workspace-stress.xml
$env:HL7WORKBENCH_STRESS = $null
git diff --check
~~~

Verification on 2026-10-17:

| Check | Result |
| --- | --- |
| Locked solution restore | Passed |
| Release solution build | Passed; zero warnings/errors |
| Focused Workspace compatibility/execution | 85 passed; zero errors/failures/skips; 70.583 s |
| Focused Desktop compatibility/history/native-input/script editor | 41 passed; zero errors/failures/skips; 31.620 s |
| Full solution, final run | 1,214 passed; zero failures/skips; 132.599 s |
| Complete Workspace stress | 633 passed; zero errors/failures/skips; 259.454 s |
| Fixture integrity | All eight tracked HL7 blobs match HEAD using --no-filters |
| Whitespace/conflicts | git diff --check and all 41 compatibility paths passed |
| Accepted native routing/safety | Protected implementation/tests byte-identical to starting working-tree hashes |

Exact git diff --check exited zero with Git's existing LF/CRLF conversion advisories;
there were no whitespace errors. No line-ending normalization was performed.

Thirty-four Workspace and five Desktop cases are added; all earlier cases remain.
The ordinary new large-source case uses 16 MiB and the metadata case uses 1,000
entries. The stress run activates 100/256/512 MiB and 100,000 logical entries,
alongside all existing worker, large-source, byte-preservation and history stress
coverage. Focused/full runs additionally cover captured output after edits/insertion/
Undo/Redo, a selected insertion removed during execution, tab-switch identity, actual
worker failure after source changes, cancellation/closure, memory-original diagnostic
labels and visible retained stale attribution through later failure/reconstruction.

Single-run compatibility measurements (Windows NT 10.0.26100, .NET 10.0.12,
16 logical processors; filesystem/GC caches uncontrolled):

| Payload MiB | Search ms | Current-scope capture ms | Capture allocated bytes |
| --- | ---: | ---: | ---: |
| 16 | 52.742 | 36.694 | 244,112 |
| 100 | 2,924.610 | 873.792 | 245,688 |
| 256 | 8,376.034 | 563.658 | 250,208 |
| 512 | 1,778.519 | 1,142.435 | 245,328 |

Every large case stream-compares the original/captured payload byte ranges and checks
that the active bounded message has not materialized the large original. Allocation
remains approximately 0.23–0.24 MiB as payload size increases; the capture still streams
the backing bytes to disk and re-indexes them. The 256 MiB search is an observed I/O
outlier, not evidence of size-independent timing or a latency guarantee.

At **100,000** logical messages, search took **690.430 ms**, script capture **41.984 ms**;
capture allocated **86,000,080 bytes (82.02 MiB)**. Combined retained growth for search
results/filter/capture metadata was **31,324,128 bytes (29.87 MiB)**, excluding the
pre-existing original index/document state. These are bounded metadata/independent
captured-index costs, not a claim of zero allocation. The retained/allocation budgets
passed. Beginning/middle/end insertions map to the correct IDs and provenance.

The existing foundation stress checks also passed: 100,000 live drafts retained
23,685,552 bytes; insertion/Undo/Redo measured 17.140/6.389/4.202 ms. Original
sequence metadata allocation remains 5,441,960 bytes at 100,000 entries. Existing
100/256/512 MiB lossless Save tests passed, with total Save allocation 426,400 /
429,232 / 412,560 bytes respectively. Metrics are engineering observations only.

Headless checks do not constitute native Windows acceptance. No published-worker
or packaging command is needed or authorized; ordinary Release worker integration
and existing isolation/limit tests remain in the full suite.

Useful reports, reviewed initial test failures, and the initial
working-tree baseline/manifest are retained in ignored artifacts/stage8b4. The UI
test initially searched a collapsed Expander's unrealized rows; it now opens the
panel before checking the rendered identity label, without changing product defaults
or weakening the assertion. During the captured-output policy correction, the new
UI fixture initially tried to edit a read-only bundled script; it now creates an
editable draft before running the report-producing script. Two existing report tests
compared an entire pre-attribution record; they now verify the same script name/scope
plus captured source ID/revision/document number and producing-run freshness. Their
report-export and retention checks remain. Those reviewed failure logs/XML are retained
under snapshot-policy-focused-desktop-initial and -metadata-initial; final passing
policy reports use the snapshot-policy- prefix. Earlier reports remain historical.
Existing artifacts/stage8b, artifacts/stage3 fixtures,
ignored build outputs and the pre-existing untracked stage5b-repeated-patients_TEST.hl7
remain. No disposable native insertion command/export or production harness was added.

Final inventory: **41 Stage 8B.4 paths**, distinguished from the accepted uncommitted
baseline by artifacts/stage8b4/baseline/manifest.json and changed-files.txt. The complete
milestone tree contains **39 modified tracked files and 20 new milestone files**,
plus the pre-existing untracked user _TEST.hl7 (60 Git status entries in total).
Main/HEAD remain at the baseline, with an empty staging area. All 37 files present
at the start remain; native routing/safety tests and the user file match their starting
hashes. No commit, push, tag, release, package or publication occurred.

## Remaining limits and native Windows acceptance

No broad post-insertion Search/Comparison/script guard remains. Genuine limits stay:
inserted sources have no initial original counterpart/bytes; unsupported containers,
unsafe joins, empty/unrepresentable required script/save payloads, bounded editor/
worker/scanner/copy limits and deferred large embedded-payload comparison remain.
Current scope's explicit omission of inactive insertions is described above, not a
silent whole-document omission. Save does not redefine originals. Reopen establishes
fresh provenance. No architecture/dependency/persistence deviation was required;
the user's captured-output conflict policy is recorded in ADR 0011.

Start the development build without packaging:

~~~powershell
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
~~~

1. Open samples/synthetic-only/stage5b-repeated-patients.hl7 in two tabs. Search text
   and nested Advanced ALL/ANY/existence conditions, filter and navigate results.
   Confirm only the active document is affected and saved query definitions still load.
   Edit a message: results/filter clear, authored query stays. Rerun; navigation and
   successful Save alone must retain valid results/filter state.
2. Compare Original versus an edited original and sources from both tabs. Navigate
   source messages and switch tabs; selected sources must stay explicit. Save and
   compare Original again: it still shows initially loaded content. Check pasted
   sources remain independent and both comparison presentations remain usable.
3. Run current/whole scripts against ordinary originals, including an inactive invalid
   original draft for current scope. Check logical processing/output and reports.
   While a long-enough run is active, edit/undo source text: successful independent
   output and reports must reflect captured input, show the captured revision and
   "Stale source", and retain newer source work. Switch tabs during a separate run:
   output must still use the captured source; a switch alone must not label it stale.
   Reconstruct the Script Editor and inspect reports after a later failed run: the
   last successful output's stale attribution must remain visible. Explicit Cancel
   or source closure must still publish nothing and retain prior successful reports.
   For a reproducible synthetic run, use the following script (three seconds per
   message, below the existing per-message timeout), then edit the source after
   capture/compilation and before completion:

   ~~~csharp
   var r = Batch.Report("capture", "Job ordinal");
   void Main()
   {
       System.Threading.Thread.Sleep(3000);
       r.Row(Batch.MessageNumber.ToString());
   }
   ~~~

4. Verify Inspector/Copy Path and exact original-byte export for unchanged originals;
   edited-source exports stay unavailable. Repeat the accepted consecutive Ctrl+Z/Y
   sequence with Edit Mode off and no intervening mouse click, including Navigator/
   Inspector and floating-tool focus. Search/script inputs retain their own history.
5. Repeat relevant Search/Comparison/script controls docked/floating, in Light/Dark,
   after view/layout reconstruction and document switches. Check disposal/closure and
   canceled native dialogs retain existing work and do not revive stale results.
6. Insertion-specific states are exercised through the permanent Workspace insertion
   APIs and headless Desktop tests above: inserted search/filter results, selected
   comparison ID removal/restoration, no-original behavior, shifted-original exports,
   inserted/shifted script targets and diagnostic attribution. The product has no
   insertion UI before 8C. Reopening a generated file cannot substitute for testing
   an inserted session identity. Interactive insertion-specific native acceptance is
   explicitly deferred to authorized 8C UI work; no new product command is invented.

Stop after Stage 8B.4 and wait for native acceptance. No Stage 8C/8D, stage, commit,
push, tag, release, package or publish is authorized.
