# Project context

Message Workbench is the official, final public product name: an independent,
local-first healthcare interoperability development tool for HL7 v2, scripting,
synthetic generation, FHIR R4 conversion and TCP/MLLP testing. Windows is
the initial desktop platform. No runtime telemetry, cloud features, or HL7 content logging.
All committed examples and tests must be synthetic. The existing MW logo is the
approved artwork; its former informal nickname was an inside joke with no brand,
publisher, company or product status.

## Grouped View menu (2026-10-28)

After the owner checkpointed Stage 15D at a99fb2f, View groups the existing commands
under HL7 Inspection, Data & Query, Connectivity & Testing and Reports. FHIR Patient
Workspace and Activity Log remain directly accessible below one separator. Group
headers have no tool-opening command; leaf handlers, shortcuts, names and docking
identities are retained. No default/persisted layout, SQL or generation behavior
changes. This navigation-only cleanup does not authorize Stage 16 or a commit/push.
Verification: locked restores, Release build (0 warnings/errors), 32 focused shell
tests and all 2,283 regular regressions passed; 14 opt-in stress cases were skipped.
All 16 leaf attributes and 2,384 protected preview hashes are unchanged. Native
Windows submenu/keyboard/visual checks remain manual.

## Stage 15D — SQL stabilization and final acceptance (2026-10-28)

The owner checkpointed/pushed 15A–15C and UI follow-ups at fc802a8, then authorized
15D stabilization only. Earlier stop-before-15D notes are historical. The isolated
SQLite/Workspace/Desktop boundaries, schema and limits are preserved. A reproduced
malformed JSON Plan.Tables=null exception and nested null items now fail at the
protocol boundary with fixed ProtocolError. No policy bypass, new SQL capability,
dependency, UI redesign or speculative performance rewrite was introduced.

Explicit synthetic Release measurements cover 50/4,000/10,000/100,000 messages,
1/16/64 MiB OBX values and malformed data. Metadata retains zero body decoding;
100k aggregate/complete identity results measured about 3.2/4.1s. The representative
10k Fields join hits the existing accounted import budget; 4k succeeds. Predicates
do not push through full-scope relation import. Large-value substr/filter attempts
still refuse materialization rather than returning partial data. CSV retains its
spreadsheet-guarded presentation semantics, including numeric display strings;
exact typed values and original HL7 bytes remain separate contracts.

New deterministic tests cover adversarial syntax, malformed IPC, exact lower/at/over
budgets, actual worker Job Object settings, source identity/byte extraction and
recovery. Expensive measurements/cap tests are explicit opt-ins, separately reported
from ordinary CI. See docs/stage15d-sql-final-acceptance.md for counts, measured
memory/timing methodology, resource/cancellation evidence and native checklist.
Acceptance is conditional on those explicit native/unforced-boundary qualifications.
Final gate: both locked restores and Release build passed (0 warnings/errors).
All 2,282 ordinary regressions passed; 14 explicit skips were all executed separately
and passed (9 performance + 5 actual-cap cases). Focused Querying 158, Workspace
SQL/hardening/library 27/32/10 and Desktop SQL 22 passed. All 2,384 protected preview
hashes remain unchanged. There are 13 Stage 15D paths: 8 modified / 5 untracked,
empty index; main/HEAD fc802a8 unchanged. Native verification was not performed.
No commit, push, packaging, protected-preview change or Stage 16 is authorized.

## FHIR generation UX polish (2026-10-28)

The owner reports native Windows acceptance of the preceding FHIR workspace
redesign. This focused follow-up still needs its own native acceptance. Generate
HL7 now prepares eligibility/output size internally through the same Workspace
operation as optional Preview, then uses the existing atomic validated publisher.
Cache reuse requires the session revision and seed/type/timestamp settings. No
Core mapping, output ceiling or selection policy changed. Selected Events with
cleared manual selections remains blocked; full-history scopes retain defaults.

Generate is primary. Seed and Transport Timestamp live in initially collapsed
Advanced Options with session state retained across resizing/docking. Desktop has
exactly two configurable Seed inputs: FHIR affects MSH-10 only; Generate Messages
affects test-name suffixes, birth dates, ORU numeric values and derived identifiers.
Both label/input pairs have specific themed tooltips and accessible help text.
F1 is not advertised after native review found it unreliable; the existing handler
is retained. No new dependency, persistence, telemetry or tooltip framework.
See docs/fhir-generation-ux-acceptance.md for verification and manual checks.
Final verification passed: normal/isolated locked restores, Release solution build
with warnings as errors (0 warnings/errors), and all 2,188 tests (0 failures/skips),
including 25 new direct-generation/UI cases. All five requested tool viewport sizes
pass headless checks. All 2,384 protected preview hashes remain unchanged. This
polish changes 15 paths; combined Git state is main/f2a5cec, 59 modified tracked /
58 untracked files, empty index. Native hover/keyboard/docking review remains pending.
Preserve uncommitted prior work; no commit/push/package, beta artifact changes,
Stage 15D or Stage 16 is authorized by this task.

## Stage 15UI follow-up — FHIR Patient Workspace (2026-10-27)

Native owner testing found UX defects after the original automated UI gate. The
authorized follow-up renames the visible tool to **FHIR Patient Workspace**, while
preserving `tool.synthetic-patients`, internal class names and placement-only layout
compatibility. The toolbar now has an Import FHIR flyout, authoritative session
counts, a secondary actions menu and a directly visible active Cancel. Empty imports
show a compact prompt instead of a blank table. Loaded data uses patient master-detail
browsing with Overview/Encounters/Timeline; narrow panes stack. Generate HL7 is
collapsed by default and retains configuration/preview across collapse and docking.

The owner explicitly removed the acknowledgment checkbox and persistent confidentiality
banner. New no-acknowledgment overloads in Core/Workspace reuse the exact bounded
validation/import implementation; legacy Boolean overloads retain their old guard
for compatibility. The Desktop model uses the new paths, rather than passing a
fictitious acknowledgment. No parser, clinical interpretation, mapping or publication
semantics changed. The count-only generation notice no longer claims user-confirmed
synthetic provenance; meaningful clinical limitations remain. About now includes an
informational data-handling section. No new EULA, legal guarantee or compliance claim
was introduced; see docs/data-handling.md.

DefaultDockSizing arms a single fresh/reset initialization after finite Dock bounds
exist. Root-identity checks let saved-layout startup restoration win. Ordinary resize
and deliberately adjusted/saved proportions are untouched. Target defaults remain
73% upper / 27% bottom and 78% document / 22% right inspection. Automated full-hierarchy
checks cover startup, reopen, resize, reset and custom saved ratios. The exact
reported native initial-sizing symptom could not be independently established:
the isolated native launch was blocked by a Computer Use app-approval timeout.
Do not claim native acceptance from the passing headless/Skia evidence.

See docs/fhir-patient-workspace.md and docs/stage15ui-fhir-workspace-acceptance.md for
current workflows, evidence, limitations and the owner native checklist. Earlier
15UI/Stage 11 notes below are historical where superseded. Preserve all uncommitted
15A–15UI work. Stop for native review; no Stage 15D/16, commit, push, packaging or
protected-beta modification is authorized.

Follow-up verification: normal/isolated locked restores and final Release build
passed with 0 warnings/errors. All 2,163 tests passed; the final 438 Desktop cases
passed again after detached demographic-text cleanup. The other four test DLLs
remain unchanged from the full run. All 23 responsive and 10 FHIR hierarchy/state
cases passed; 41 nonblank Skia frames are retained. All 2,384 protected preview files
match their baseline. The follow-up changes 31 paths; the combined worktree remains
on main at f2a5cec, index empty, 57 modified tracked / 55 new files. No commit, push,
packaging, accepted-artifact rebuild or later-milestone work occurred.

## Stage 15UI — responsive tool infrastructure (2026-10-27)

The owner authorized completing the interrupted 15UI milestone, preserving all
uncommitted 15A–15C work. Stop before 15D; no commit, push, packaging or accepted-beta
artifact modification is authorized. Desktop now uses small native-control helpers
in Viewer/ResponsiveTools.cs: centralized DIP metrics, AdaptiveActionBar,
ResponsiveFormField, ToolContentGrid and ResponsiveToolSurface. Individual tool
viewports drive wrapping/stacking. Finite Grid measurement preserves editor/list
viewports while exposing complete vertical overflow, including nested expanded
details. Essential bars have a bounded vertical fallback in very small hosts.
Dock headers/tabs/default layout/schema remain unchanged. No new dependency or
Core/Workspace change belongs to this milestone.

Synthetic Patients/FHIR Import, SQL Query, Script Editor/Library, Field Inspector,
Segment Navigator, Activity Log, Field Extraction, Full Source Viewer, transport,
batch transmission, load testing and Script API Reference adopt the shared patterns.
Search/Comparison action bars and de-identification report wrapping receive small
safe fixes; their complex split compositions retain existing persistence and are
documented as remaining extreme-height work. Layout does not alter source snapshots,
bounded previews, SQL enforcement/results/extraction or synthetic generation.

Resizing preserves controls and model state. Desktop's internal ToolEditorState
also retains SQL/Script editor documents, undo and selection when Dock replaces a
template, including attach-before-detach ordering; detached editors release document
handlers, owners are weak references, and model disposal clears retained state.
No editor content is persisted by this infrastructure.
Activity Log retains only opaque selected entry sequence numbers and its filter
within the current model when Dock replaces the view; disposal/clear prunes them.

See docs/responsive-tool-layout.md, ADR 0003's amendment and
docs/stage15ui-acceptance.md for the audit, matrix, verification evidence and native
Windows checklist. Headless measurement/Skia and emulated 1.0/1.25/1.5 transforms
are explicitly distinct from real native docking, chrome and per-monitor DPI
acceptance. Verification totals and artifact/Git checks are recorded in the
acceptance report; native visual acceptance remains an owner review step.

Verification: normal/isolated locked restores passed; final isolated Release solution
build passed with 0 warnings/errors. All five suites passed 2,152 tests. All 428
Desktop tests were then rerun successfully after the final clinical layout and
Activity Log state corrections; the other four test DLLs are unchanged. All 23
responsive cases also passed with nonblank Skia captures. Representative frames
were inspected; actual native Windows/DPI acceptance remains pending. All 2,384
protected beta files match their baseline. Git remains on main at f2a5cec, index
empty, 42 modified tracked / 49 new files across 15A–15C plus 15UI. The 29-file
15UI inventory and retained ignored verification evidence are in the acceptance report.

## Stage 15C — Docked SQL Query workflow (2026-10-27)

The owner provisionally accepted 15B and authorized **15C only**. Stop before 15D;
no commit, push, packaging or accepted-beta artifact modification is authorized.
View → SQL Query opens a lazy dockable/floating editor with integrated paged results.
The default layout is unchanged. AvaloniaEdit plus a small theme-aware lexical SQL
definition provides multiline editing/highlighting, Ctrl+Enter execution and Ctrl+S
explicit query saving. Desktop copies the existing contained worker to QueryHost/;
no engine/schema redesign, Core change or new dependency/package is introduced.

Current scope pins the active document on Run; selected scope freezes checked open
documents. Capture/index/execute stays off-thread with progress/Cancel. Closure or
revision changes cancel participating running queries; completed results retain
independent captured bytes and show staleness. Captured document numbers and draft
vs producing-query attribution remain visible. Starting a run clears old results;
failure cannot present them as a new success. Status reports SQL/transfer and total
capture/index/query duration plus complete row count.

Results render at most 100 journal rows with 256-character previews. Cell/row/rectangle
selection supports full-value quoted TSV copy (1 Mi UTF-16 cap), typed NULL/empty
presentation and explicit full scalar inspection. Stable typed sorting reads one
column and retains a bounded row permutation; text order is ordinal, not a claim
about SQLite collations. CSV validates/exports the complete permutation across all
pages. Go to source/double-click verifies authoritative identities and unchanged
open revisions; selection alone never navigates or changes source content.
Matching-message extraction still consumes the complete original journal, deduplicates
in source order and honors all 15B byte/framing/encoding/output checks.

Explicit named definitions live under the application configuration's SqlQueries/
directory, following existing GUID JSON, bounded read, link rejection, library lock,
revision conflict and atomic publication conventions. Persist Version/Id/Name/Sql
only: up to 256 records, 120-character names, 65,536 SQL units, 512 KiB encoded record.
Names/SQL literals may be sensitive and unencrypted; disclosure is visible before
saving. No autosaved editor text, history, results, source data or document metadata.
Session history survives tool reopen in memory only (32 entries/256 Ki SQL units).
New/open/delete/close guard unsaved drafts; delete confirms; rename preserves stored
SQL. Application shutdown retains discard consent until other close prompts succeed.
Tool closure drains cancellation and disposes journal/sources; layout stores placement
only and can restore visible tools with fresh content. SQL editors reject oversize
paste before document mutation and retain deferred rollback for programmatic overflow,
preserving the accepted AvaloniaEdit undo-transaction invariant.

See docs/sql-query-ui.md for the user guide, ADR 0020's 15C amendment, and
docs/stage15c-sql-ui-acceptance.md for verification, limitations, file inventory and
native checks. Evidence/initial failures/rendered synthetic screenshots are retained
under ignored artifacts/stage15c/. This is a source milestone pending review; 15D
and a rebuilt release are not implied.

Verification: normal/isolated locked restores passed; final isolated Release build
passed with 0 warnings/errors. The complete run passed all 2,129 tests with no
failures/skips (33 added 15C cases). Final review added bounded alias headers and
caught close-time save conflicts; all 405 Desktop tests were rerun successfully
against those guards. The other four test DLLs remain byte-identical to the complete
run. Four nonblank light/dark compact/wide Skia frames were inspected. All 2,384
protected preview files remain byte-identical. Git contains 22 modified tracked /
44 new files across 15A–15C, index empty, HEAD f2a5cec unchanged. No commit, push,
package, accepted-artifact rebuild or 15D work was performed. Native Windows
docking/DPI/picker/clipboard checks remain in the owner checklist.

## Stage 15B — historical relational/output gate (2026-10-27)

The owner initially provisionally accepted 15A and authorized **15B only**. The
subsequent 15C authorization/amendment is above. No accepted-beta change is authorized.
Workspace now references managed Querying contracts; Core stays unchanged and
SQLite-free. Desktop has no SQL menu/tool/grid or worker deployment yet. Saved-query
persistence and UI belong to 15C. See docs/sql-query.md, docs/sql-hl7-workflows.md,
docs/stage15b-hl7-query-acceptance.md and the 15B amendment in ADR 0020.

Focused 15A review retained authorizer-before-prepare/reprepare, permanent Execute
sealing, worker cancellation/crash cleanup and native/parent resource caps. Empty
BLOB binding was corrected. A prepare-only, authorizer-protected Plan phase can
discover relations before import; it never steps and cannot reopen sealed input.
Source-backed field relations use three fixed read-only SQLite adapters, private
metadata backing tables and bounded parent value RPC. No view/CTE context grants
authority (adversarial tests caught and rejected that approach). Trusted schema,
views/triggers/extensions stay disabled. No new dependency/version in 15B.

Live logical state is frozen on the document context, copied to owned snapshots off
thread, then live leases release. Metadata-only queries decode just bounded MSH
headers, never unused OBX bodies. Referenced structural relations scan complete
messages with Core; value columns materialize only on request and fail explicitly
on oversized/unresolved decoded values. Missing, empty, null and errors remain
distinct. Document-qualified HL7_VALUE/RAW/STATE/STATUS reuse Core paths; two-argument
overloads exist only for one pinned document. Multi-document identity never relies
on control/patient IDs or display positions.

Complete bounded results become an owned journal with 100-row/256-character previews
and explicit full-row reads. CSV streams all rows through Core encoding and existing
atomic publication/source guards. Separate identity-selection queries or complete
identity-bearing results deduplicate all rows, preserve source order and copy exact
source/framing ranges into an independent unsaved document. No visible-page-only
extraction or hidden truncation exists. Unsafe encoding/wrappers/framing/joins fail
before publication; originals remain unchanged.

Workspace caps: 16 documents, 100,000 scoped messages, 1 GiB captured bytes, 16 Mi
UTF-16 frozen drafts, one decoded spool/model, 128 MiB encoded journal, 1 GiB output,
100,000 matching messages, 256 MiB CSV and 10-minute overall run. Existing SQL caps
remain (16 MiB/100,000 results, 250,000 metadata rows/64 MiB import, 65,536 explicitly
materialized text units, 30s execution, 512 MiB Windows job). Source RPC is capped
at 100,000 reads. Temporary snapshots/journals can contain sensitive content and use
existing lease/cleanup rules; no query/history/results setting or raw disk database.
Structural scans are relation-selective, not message-predicate-selective; metadata
queries still copy source bytes. Convenience segment views remain deferred.

Verification: normal/isolated locked restores passed; isolated Release build passed
with 0 warnings/errors; all 2,096 regression tests passed (0 failed/skipped), including
47 added 15B cases. All 2,384 protected preview files remain byte-identical. Full gate
and native-review checklist are in docs/stage15b-hl7-query-acceptance.md. Evidence and
initial failure diagnostics remain under ignored artifacts/stage15b/. Git has 13
modified tracked / 31 new files across 15A+15B, with an empty index. This is a source
milestone awaiting review, not a release or native SQL UI acceptance.

## Stage 15A — historical engine verification (2026-10-27)

The owner initially approved the four-pass post-beta Stage 15 plan and **15A only**:
an isolated SQLite worker, deny-by-default authorization, cancellation, resource
limits and automated tests. Stop for review before 15B. No commit, push, packaging,
publication or change/rebuild of accepted private-preview artifacts is authorized.
The earlier release checkpoint restrictions below describe that separate release.

New Querying contains UI/SQLite-free contracts and bounded current-user pipe/client
ownership. QueryHost alone loads pinned SQLitePCLRaw 3.0.5/native SQLite 3.53.4 into
a private in-memory database, with a 512 MiB Windows job, 128 MiB native heap,
64 MiB database, authorizer-before-prepare, disabled extensions/virtual tables,
query_only, statement-tail validation, typed parameterized import and fixed
privacy-safe errors. Complete bounded results or failure; no partial publication.
No existing Core/Workspace/Desktop/Scripting/Transport implementation is changed.
No live HL7 importer, SQL Dock UI, path functions, query persistence, CSV or matching
message extraction is implemented in 15A. See ADR 0020, docs/sql-query.md and
docs/stage15a-sql-engine-acceptance.md for exact status, tests and limits.

Final 15A verification: locked restores passed; isolated Release solution build
passed with 0 warnings/errors; all 76 new cases and all 2,049 complete regression
cases passed, with 0 failures/skips. All 2,384 protected private-preview files
remain byte-identical. The running app locked normal Release outputs, so fresh
isolated binaries and byte-identical compatibility-path test copies were used;
no test assertion was changed. Evidence and initial failure logs remain under
artifacts/stage15a/. Current 23 paths (5 modified / 18 new) stay unstaged and
uncommitted at that gate. The subsequent 15B authorization and amendment are above.

Carry forward: the 65,536-character cap is explicitly a materialization budget,
never an HL7 field-size assumption. Metadata queries must avoid decoding unused
large OBX values. Referenced unavailable values must fail explicitly rather than
silently become NULL/nonmatches. Matching-message extraction must operate on its
own complete bounded identity selection, not visible/truncated report rows.
Document-qualified functions remain authoritative; plan a convenient pinned
Current document scope and consider single-document-only overloads in 15B.

## Final private-preview checkpoint

The accepted binary checkpoint is af06e576a0a761f092db6ea7047b488570c0ee06. The user
reports its ZIP passed automated and native Windows VM acceptance. The owner has
accepted PRIVATE-PREVIEW.txt for a personal developer project: no sale/public release,
compiled ZIP only, no owner-repository access, limited trusted coworker development/
evaluation, authorized data/systems, no clinical or production use or guaranteed support.
Third-party rights remain intact. No company or blanket employer approval is implied.

The owner authorizes one local documentation/notice/necessary-packaging checkpoint
and a new reproducible ZIP, followed by final owner review. The owner explicitly
selected reuse of the accepted binaries: recompilation embeds a new Git revision in
AssemblyInformationalVersion. Packaging therefore records the new committed-source
revision and dirty=false separately from the original binary revision/archive hash,
and rejects changed compilation inputs or runtime bytes. No application/build
configuration, dependency, fixture, icon, branding or product-version changes.

The pinned native investigation excludes the historical Netscape GIF reader; Wuffs
0.3.3 uses Apache-2.0. Supplement the original 61 notices with pinned HarfBuzz COPYING
(including Matthias Clasen's 2022 credit), current Skia LICENSE and Wuffs license/header
attribution. Do not invent a Wuffs NOTICE file or claim legal clearance. The original
vendor notices, approved preview wording and earlier ZIPs/evidence remain unchanged.
The superseded agreement is not packaged. Minimal permanent packaging changes require
the preview notice, verify pinned notices and enforce exact accepted-binary provenance.

See docs/native-license-applicability-review.md, docs/beta-distribution-review.md and
docs/portable-release.md. Final evidence/commit/archive metadata is recorded under
artifacts/private-preview/final-checkpoint/. No push/tag/upload/distribution, new
version or next milestone is authorized. Earlier statuses below are historical.

## Current beta follow-up — de-identification reporting (2026-10-23)

The user reports all nine native Windows de-identification acceptance checks passed
and authorizes one local checkpoint, `Beta: Improve de-identification reporting and
review findings`, plus a fresh v0.1.0-beta self-contained Windows x64 candidate from
committed source. Preserve the old ZIP/evidence and all safeguards; run the full
normal/portable/packaging/privacy/worker/stress/integrity gate and independent rebuild.
The replacement awaits exact-ZIP VM acceptance and separate distribution permission.
No amendment, push/tag/upload/publication/distribution or new milestone. Packaged
README/release notes are updated before checkpointing so provenance can be clean.
The user explicitly authorized including these two documentation paths with the
original 30 accepted paths in the single checkpoint (32 total); no additional commit.
Earlier pending acceptance and no-commit paragraphs below are historical.

The packaging correction is checkpointed as afe7f6576f67ce25e72caa298adc94694d611d56.
Its clean-source verified ZIP remains unchanged. Native testing of that ZIP found a
large inline Transformation Report and an unconditional Review required status.
The authorized correction adds a dockable De-identification Report, compact document
link, fixed structural findings, separate retained-review counts, neutral completion
statuses, and source/output revision freshness. Transformation/identity/timestamp
policies, isolation, explicit CSV export and safety limits are preserved. The default
synthetic ADT reproduces the reported 28/7/1/8 totals: PV1-7 lacks authority; EVN-1,
PV1-2 and PV1-4 are retained clinical/event codes; five routine fields are not specific
review findings. No disclaimer-only risk is invented and no legitimate finding is hidden.
See docs/deidentification-report-fix-acceptance.md and ADR 0013 for the current gate
and native checklist. Final gate passed: locked restore, Release 0 warnings/errors,
70 Scripting/API / 40 Workspace / 12 Desktop focused cases, 1,973 solution cases and
940 complete unfiltered Workspace stress cases (376.903s), no final failures/skips.
Normal worker Current/Whole smoke, API sync, all 25 frozen code/test/API hashes,
13 protected hashes, 2 icon derivatives, 17 fixture/artwork blobs and tracked/new
whitespace checks pass. The original verified ZIP remains byte-identical. There are
30 pending files (23 modified / 7 new), with an empty index and unchanged HEAD.
Work remains unstaged/uncommitted for user acceptance. No new
candidate, history change, push/tag/upload/distribution or next milestone is authorized.
Earlier authorization/status paragraphs below are historical.

## Stage 14 — usability, stability and portable beta readiness

Current follow-up: the single Stage 14 checkpoint was created as
**b14b100e399a4eb44a05c98b4844f552f080eb68**, `Stage 14: Prepare portable beta release`.
Its final clean-source package attempt stopped before ZIP creation: six DLL copies
contained compiler CodeView records with absolute obj/Release PDB paths. A diagnostic
warm build confirmed CoreCompile skipped despite CI/no-symbol publish settings.
The user authorizes only an unstaged/uncommitted build-path correction and a fresh
local candidate, with all guards enabled. Release mapping now derives this checkout
root from Directory.Build.props and maps it to `/_/` before incremental inputs;
permanent tests compile actual Core sources in two roots and exercise warm reuse.
The first attempt stopped on a remaining Desktop CodeView path. The continuation
isolated it in a fresh build: Csc emitted a mapped virtual PDB reference, then
Avalonia's XAML/Cecil postprocessor recreated the physical PDB filename. Desktop
Release now sets DebugType=none and DebugSymbols=false, matching existing portable
settings; Debug is unchanged. Fresh pre/post-XAML PE captures and the permanent
fresh/warm Desktop regression pass, with no Desktop CodeView entry. All six normal
production DLLs pass the checkout/home byte scan. The complete updated gate passed:
1,961 solution / 935 unfiltered Workspace stress / 12 packaging-compiler tests;
zero-warning Release build, locked solution/portable restores, focused 13 Desktop /
2 worker-launch tests, normal and self-contained current/whole worker lifecycles,
API sync and all integrity checks. Two independently built portable ZIPs are identical:
581 files / 35 runtime-package entries / 95,844,601 bytes, SHA-256
5478697F3E226937CB415D2211571E2EE14F253A9F09AF2F7BCDA146ADAEED4A.
The local candidate is artifacts/stage14-desktop-path-fix/portable-1/CANDIDATE/
MessageWorkbench-v0.1.0-beta-win-x64.zip. Its provenance truthfully records base
b14b100e399a4eb44a05c98b4844f552f080eb68, dirty=true and 254 exact build-input hashes.
Eight correction paths (seven modified tracked / one new test) remain unstaged and
uncommitted; no history change. Exact-artifact native testing and existing licensing,
terms, unsigned trust and separate distribution approval remain open. Current evidence
is under artifacts/stage14-desktop-path-fix/; earlier failed outputs remain intact.
No binary rewriting, developer-path hardcoding, guard exemption or runtime change.
Updated full verification and candidate results are recorded in
docs/stage14-release-readiness-acceptance.md and artifacts/stage14-build-path-fix/.
No additional commit/amendment, push/tag/upload/distribution or further milestone.
Earlier checkpoint authorization, clean-tree and gate statements below are historical.

Current authorization (2026-10-22): the user reports the portable application and
self-contained scripting worker worked in a separate work VM, and authorizes exactly
one local commit, `Stage 14: Prepare portable beta release`, followed by a fresh
clean-source Windows x64 ZIP and verification. No amendment, push/tag, distribution,
upload/publication or next milestone. The new ZIP's source hash/checksum/results are
recorded in ignored artifacts/stage14-release-preparation/ after that checkpoint;
the commit inventory and integrity reconciliation are in artifacts/stage14-checkpoint/.
The earlier full gate still applies to unchanged application code. Only packaging
and recipient/review documentation are extended: required packaged README with input
hash, missing-document regression assertions, concise notes, proposed terms and a
resolved/unresolved notice/trust review. See docs/beta-distribution-review.md.
Human notice applicability, owner terms, employer trust and separate recipient/artifact
approval remain distribution blockers. The reported VM test does not establish every
unreported Windows 10/11/read-only/DPI/accessibility matrix item. Earlier authorization
and inventory statements below are historical records of the pre-checkpoint gate.

Branding passed native Windows acceptance and was checkpointed once as
**84fdc22925173250fad132d0d4b4c6768d3c68e0**, `Branding: Rename product to Message Workbench`,
parent 738e3d7697fb7b2d2d8e668fe7b02edbddea28f2. All 16 accepted hashes matched
the completed 1,953 solution / 935 stress gate before acceptance-only Markdown
updates; all staged blobs were reviewed and immediate post-commit tree/index clean.
Evidence: artifacts/branding-checkpoint. No amendment/history rewriting/publication.

Stage 14 is authorized for implementation and verification only. Target is
v0.1.0-beta, Windows 10/11 x64, portable self-contained ZIP. Only clearly marked local
candidates may be generated for tests; no Stage 14 staging/commit, release/distribution,
push/tag/upload or further milestone is authorized. User confirmed artwork ownership
or redistribution permission; the source PNG and approved derivatives remain exact.

Implemented: startup recovery catches semantic/oversized InvalidDataException layouts;
layout saves use unique owned temporary metadata and cleanup; save-conflict status
is fixed text; public beta/file metadata changes while binding AssemblyVersion remains
1.0.0.0. Worker DLL paths prefer only adjacent apphosts, preserving private pipe/job/
deadline/process-tree isolation and DLL-only development fallback. Nonprivate worker
references prevent SDK apphost/metadata leaks; candidate publishes processes separately.
Core/Workspace/Transport logic, lossless/save/network policy and per-user paths stay intact.

Portable builds use pinned runtime 10.0.12 and six separate RID lockfiles. The local
builder checks contents/runtime/identity/notices, writes exact input provenance and
SHA-256 manifests, then creates a fixed-order/timestamp ZIP. Only its apphost copy is
named MessageWorkbench.exe; embedded DLL/assembly/resource/config identities stay
HL7Workbench. No single-file extraction, trimming, updater, installer or application
write permission in the extraction folder is introduced. License inventory covers
35 actual runtime entries, full bundled/pinned upstream notices; no legal clearance.
Native clean-machine/read-only-folder acceptance and human licensing/beta terms review
remain. See ADR 0019, docs/portable-release.md, docs/portable-dependencies.md and
docs/stage14-release-readiness-acceptance.md for issue inventory/results/privacy risks.
Earlier authorization/status statements below are historical.

Final Stage 14 gate (2026-10-22) passed on **33 frozen code/build/notice/lock/curated-doc
paths**: locked solution/RID restore; Release build **0 warnings/errors** (8.48s);
focused **109 Core / 203 Workspace / 74 Scripting / 102 Desktop / 3 shell**; **1,961
solution cases** (3m 34.276s); **935 complete unfiltered Workspace stress cases**
(334.985s), zero focused/stress errors/failures/skips/unrun; **10 packaging tests**.
Both normal Release and native self-contained current/whole ScriptHost lifecycles,
deployed hashes/isolation and API sync passed. Final marked candidate has 580 files,
35 runtime/package entries and byte-identical deterministic ZIP repeat/CRC/SHA-256
roundtrip. ZIP is 95,841,780 bytes; SHA-256
41925FE5C47D524F0F39CFE3965A56D060AF8438C162D0394B6C45E7418F3634.
All protected/fixture/artwork/TEST hashes, notice text and candidate inputs match.
Only documentation changed after the gate. Current **43 paths (14 modified tracked /
29 new)** remain unstaged/uncommitted; index empty and branding HEAD unchanged.
Evidence and clearly local initial/final candidates: artifacts/stage14. Full native
18-step checklist, licensing/terms/temporary-storage risks and exact inventory are
in the acceptance record. No distribution, Stage 14 commit or further milestone.

## Branding native acceptance complete — local checkpoint authorized

The user confirmed native branding acceptance and authorized one local commit,
`Branding: Rename product to Message Workbench`. All 16 accepted hashes, eight frozen
implementation/test paths, protected fixtures/artwork and persistence identities
matched the completed gate before acceptance-only Markdown updates. Stage 14 is
authorized after that checkpoint for usability/stability and local portable candidate
readiness, but must remain unstaged/uncommitted. No distribution is authorized.
Earlier pending/stop statements below are historical.

## Official product name and accepted Stage 13B checkpoint

Stage 13B passed all 16 user-reported native checks and was checkpointed once as
**738e3d7697fb7b2d2d8e668fe7b02edbddea28f2**, `Stage 13B: Add controlled MLLP load testing`,
parent **45e7cdbad9a32cdaebb7834ae0026b10914622aa**. All 29 staged blobs matched the
audited files; the 20 source/test hashes matched the completed 1,949 solution / 935
stress gate, with acceptance-only Markdown updates. Protected hashes matched and
the immediate post-commit index/tree were clean. Evidence: artifacts/stage13b-checkpoint.

The subsequent branding/reference audit is authorized for implementation/testing,
and must remain unstaged/uncommitted for native acceptance. **Message Workbench**
is public presentation; **HL7Workbench** remains the technical namespace/project/
assembly/repository/configuration identifier. No directory, setting key, saved
script, profile or layout migration is intended. The approved MW source,
PNG and ICO stay byte-identical. See docs/branding.md, docs/product-reference-audit.md
and docs/product-branding-acceptance.md for current findings/results/acceptance.
Earlier milestone authorization and pending statements below are historical.

Final branding gate (2026-10-22) passed on eight frozen implementation/test paths:
locked restore; Release warnings-as-errors build, **0 warnings/errors** (2.95s);
focused **109 Core / 192 Workspace-Transport-preferences / 71 Desktop / 3 shell**;
**1,953 solution cases** (3m 21.144s); **935 complete unfiltered Workspace stress
cases** (310.593s), no errors/failures/skips/unrun in focused/stress runs. Both existing
Release ScriptHost lifecycles/isolation and API sync passed with no publish command.
Final integrity matches 13 protected hashes, two icon derivative hashes, all 16
fixture/artwork blobs and the eight frozen paths; TEST hash only, never opened.
Current audit covers 462 files excluding TEST content; only the audit record retains
the searched third-party name. Historical findings cover 24 commits and three extra
trees (865 blobs read; one TEST blob excluded), with no matching messages/paths/refs
or obvious suspicious provenance evidence. See the audit for exact hashes/limits.
Current state: **16 unstaged paths (11 modified tracked / 5 new)**, empty index,
unchanged accepted Stage 13B HEAD. Logs/XML/hash/audit evidence remains under
artifacts/product-branding. Native branding acceptance is pending; no further Git
mutation, packaging/publication or feature milestone is authorized.

## Stage 13B native acceptance complete — checkpoint authorized

The user confirmed all 16 native Windows checks passed and authorized exactly one
local commit, `Stage 13B: Add controlled MLLP load testing`. The complete 29-path final
manifest and all protected/frozen hashes match the gate below before acceptance-only
Markdown updates. No implementation or test changed. Branding/reference audit is
authorized after that checkpoint and must remain uncommitted. Earlier pending/stop
statements below are historical records.

## Stage 13B controlled load testing — completed automated gate

Stage 13A passed all eight native Windows checks and was checkpointed exactly once
as **45e7cdbad9a32cdaebb7834ae0026b10914622aa**, `Stage 13A: Add controlled batch
MLLP transmission`, parent e575ebe47921f33b3431d32bf26a5fc87aee4a0a. Exact 37-path
hash/inventory and staged-blob checks matched the accepted gate (1,884 solution /
890 stress) before acceptance-only docs and commit; post-commit tree/index clean.
Evidence remains in artifacts/stage13a-checkpoint. No amendment/push/publication.

Stage 13B now reuses batch captures and production MLLP connections for explicitly
reviewed controlled load tests. Workspace owns one global monotonic admission gate,
1–8 active slots, target 0.1–20/s, 1–300 seconds and 1–5,000 attempts; source exhaustion,
duration/attempt limits, runtime failure/rejection/timeout stops, cancel/drain and
explicit unchanged replay. All counts and bounded phase samples remain exact;
only first 1,000 completion rows persist in session, with omissions disclosed.
Optional transport timing fields preserve wire/ACK semantics. Desktop owns a lazy
themed Dock tool, synthetic attestation, scrollable exact-target confirmations,
fixed top Stop, four-Hz live metrics and explicit atomic privacy-safe CSV/text exports.
TLS remains fail-closed. No new package/persistence schema/Core/ScriptHost changes.
All Stage 12/13A and permanent generation/history coverage remains. See ADR 0018,
docs/load-testing.md and docs/stage13b-load-testing-acceptance.md for definitions,
limits, schema, final verification and native checklist; evidence artifacts/stage13b.

Final 2026-10-22 gate passed: locked restore; Release warnings-as-errors build with
zero warnings/errors (3.31s); focused 109 Core / 174 Workspace-Transport / 64 Desktop /
3 shell cases; **1,949 solution tests** (3m 20.356s); **935 complete unfiltered
Workspace stress cases** (329.422s), no failures/skips or focused/stress errors/unrun.
Both existing Release-layout ScriptHost lifecycles/isolation and API sync passed,
without a publish command. Post-stress integrity matches 20 frozen source/test and
13 protected hashes, eight original HL7 blobs, artwork/derivatives/formerly excluded
HL7 and tracked/all-13-new-file whitespace. Current **29 paths (16 modified tracked,
13 new)** are wholly unstaged/uncommitted; empty index, unchanged Stage 13A HEAD.
No code/test changed after the final build/gate; exact inventory, limitations and
18-step native checklist are in the Stage 13B acceptance record. All permanent tests
and useful ignored diagnostics/hash evidence remain. Only the authorized Stage 13A
checkpoint was created; no subsequent Git mutation or later milestone.

Stage 13B must remain unstaged/uncommitted for native Windows acceptance. No further
Git mutation, amendment, push/tag/package/deploy/publish/release or later milestone.
Earlier pending/no-Stage-13B statements below are historical authorization records.

## Stage 13A checkpoint authorization — native acceptance complete

The user explicitly confirmed all eight Stage 13A native Windows acceptance checks
passed, including Generation Report polish, and authorized one local checkpoint:
`Stage 13A: Add controlled batch MLLP transmission`. The exact 37-path inventory and
all final file hashes matched the completed gate before this acceptance-only
documentation update. All 27 implementation/test hashes, protected assets and
Stage 12 baseline e575ebe47921f33b3431d32bf26a5fc87aee4a0a remain verified.
No implementation/test changes require repeating the completed gate. After the
single checkpoint, Stage 13B is authorized under the supplied bounded controlled
load-testing requirements, but must remain unstaged/uncommitted for native acceptance.
No amendment, push/tag/package/deploy/publish/release or later milestone is authorized.
Earlier pending/stop statements below are historical records of their verification.

## Stage 13A report UI polish — accepted verification record

The user confirmed native Windows acceptance of controlled batch transmission.
The final authorized Part A moves the full FHIR Generation Report out of RAW into
an optional dockable Generation Report tool, replacing the expanded panel with one
compact metadata row and View report action. The existing formatter and immutable
document-owned report remain authoritative; no Core/Workspace generation/payload,
identity/save or report persistence changes. The tool follows selected documents,
clears for nonreport tabs, closes/reopens independently, and restores only existing
placement/visibility metadata. Later edits retain original generation attribution.
The final 2026-10-22 gate passed: locked restore; Release 0 warnings/errors (3.58s);
focused 109 Core / 129 Workspace-Transport / 44 Desktop / 3 shell cases; **1,884
solution tests** (3m 33.082s) and **890 complete Workspace stress cases** (328.532s),
zero failures/skips and no focused/stress errors or unrun cases. Both Release-layout
ScriptHost lifecycles/isolation and API sync passed, without a publish command.
Post-stress checks match 27 frozen source/test hashes, 13 protected hashes, eight
original HL7 blobs, artwork/derivatives/formerly excluded HL7 and all tracked/new
whitespace checks. Core/Workspace/Transport/Workspace.Tests binaries remain identical
to the accepted batch baseline; the complete report formatter is unchanged.
The shell focus regression retains an explicit open request until Dock attaches;
passive document updates do not request focus. A retained batch shutdown regression
now waits for actual Closed before asserting disposal, preserving exact delivery/
client/no-restart checks; production batch behavior is unchanged. All permanent
coverage remains. Exact evidence/inventory/native steps: acceptance record and
artifacts/stage13a-report-polish. Current tree: **37 paths (22 modified tracked,
15 new), empty index**, no code/test changes after the final gate. The prior 28
accepted Stage 13A file hashes matched before polish; accepted evidence remains
under artifacts/stage13a. No protected material was changed or excluded HL7 opened.

Stop after Part A verification for explicit user native acceptance of this polish.
No staging/commit now. The one Stage 13A checkpoint is conditional on that future
acceptance; Stage 13B is gated on that checkpoint and has not begun. No push/tag/
package/deploy/publish/release. Stage 12 HEAD remains e575ebe47921f33b3431d32bf26a5fc87aee4a0a.

## Stage 13A controlled batch transmission — accepted baseline before report polish

The user confirmed Stage 12 native Windows acceptance and committed it as
e575ebe47921f33b3431d32bf26a5fc87aee4a0a (`MLLP functionality`). The actual initial
working tree/index were clean. That user-created checkpoint also tracks the formerly
excluded `_TEST.hl7`; preserve its existing bytes and history, without opening it.
Earlier pending Stage 12/no-Stage-13 statements below are historical authorization.
Stage 13A is authorized, but must stay unstaged/uncommitted for native acceptance.
No Stage 13B, push/tag/package/deploy/publish/release is authorized.

Workspace now owns immutable ordered selected payloads, source revision, pinned
endpoint/options and one explicitly authorized sequential run over production
MllpConnection. One message/connection, full ACK gate, default Stop unless correlated
AA; Continue is explicitly reviewed. Default 1,000 ms post-exchange pacing, hard
250–60,000 ms, pending/effective pause, resume remaining only, cancel/drain, bounded
metadata/ACK bytes and whitelist atomic CSV are implemented. A lazy themed Dock tool
owns review/non-loopback confirmations and independent ACK documents. Closing/reset/
application shutdown cancels and drains; placement restore never starts a run.

The first batch tests reproduced a listener endpoint-inspection disposal race
(TcpClient.Client becomes null before dictionary removal); a narrow null-safe
snapshot fixes it. No single-send framing/capture/ACK/confirmation behavior changed.
All Stage 12 sender/capture/regression files remain intact. Real loopback exchanges
plus an internal controlled write-boundary seam test cancellation without assuming
Windows send-buffer timing. Early failure/build logs remain in artifacts/stage13a.
Final verification passed: locked restore; Release warnings-as-errors build with
zero warnings/errors (2.38s); focused 41 Core / 106 Workspace-Transport / 26 Desktop
and one legacy ribbon regression; **1,878 solution tests** (3m 10.673s); **890 complete
Workspace stress cases** (292.458s), zero failures/skips/unrun focused/stress cases.
Both existing Release-layout ScriptHost lifecycles/isolation and API sync passed.
All 19 frozen implementation/test hashes, 13 protected hashes, eight original HL7
blobs, artwork/derivatives/formerly excluded HL7 and tracked/new-file whitespace
passed after stress. The initial full-run ribbon inventory failure is retained;
the exact expectation now includes Batch Transmission with all other checks intact.
A later NE/NE test asserted receipt count before asynchronous listener publication;
it now waits for the actual bounded receipt and retains strict exact assertions.
Full/focused/stress reruns passed for those final test bytes. The final progress
presentation explicitly includes completed/unsuccessful/transport-failure/skipped
aggregates alongside phase-specific outcomes. Production transport behavior was
unchanged by those presentation/test corrections. All diagnostics are retained.
Current inventory: 28 paths (16 modified tracked, 12 new); empty index, unchanged
Stage 12 HEAD. No implementation/test changed after the final build/gate. Ignored
verification evidence is retained in artifacts/stage13a. See the acceptance record
for exact results/inventory and native checklist. Stop for native Windows acceptance.
See docs/batch-transmission.md and ADR 0017 for the lifecycle, report schema, precise
bounds, uncertainty semantics and deferrals. Protected hashes/artwork/original
fixtures and the formerly excluded HL7 must remain unchanged.

## Stage 12 native send correction — automated gate passed; native retest pending

Native acceptance failed: a checked connection could expire at the listener's idle
deadline during human review. A permanent 504-byte/11-message Desktop regression
reproduced the exact Disconnected/zero-payload timeout before correction. Confirm
now explicitly authorizes one fresh connection to the pinned reviewed address and
one write; the checked socket is retired without transmitting. No failed write is
retried. Completion, ACK frames, application acceptance, failure phase/socket enum
and actual socket/partial/complete byte counts are separate. The initial tests
confirmed immediately and missed this interval. Cancellation and shutdown boundary
regressions are retained. Evidence: artifacts/stage12-native-send, including the
pre-fix failure. Eight implementation/test paths plus these docs/guide/ADR changed;
the complete Stage 12 inventory remains 39 paths, all unstaged/uncommitted.
The final correction gate passed: locked restore; Release build with zero warnings/
errors (1.61s); focused 41 Core / 70 Workspace / 12 Desktop; **1,828 solution tests**
(2m 58.698s); **854 complete Workspace stress cases** (298.318s), with no failures/
skips or unrun focused/stress cases. Both Release-layout ScriptHost lifecycles and
isolation, API sync, 33 frozen implementation/project/test hashes, 13 protected
hashes, eight original HL7 blobs, artwork/derivatives/excluded HL7 and whitespace
pass. Recovery on 2026-10-22 found the stress process had completed during the chat
interruption; finished log/XML were reconciled without rerunning completed checks.
Post-stress integrity was repeated. HEAD remains the Stage 11 checkpoint; empty
index, 18 modified / 21 new Stage 12 paths plus the unchanged excluded HL7. See the
acceptance record for exact paths, evidence and delayed-confirmation retest steps.
The prior 1,818 solution / 848 stress gate below is historical and precedes this
native correction. Stop for native Windows retesting. No Git/publication/Stage 13 action.

## Stage 12 initial automated baseline — superseded by native failure above

Stage 11 was checkpointed once as af717a66608d3277402bd6696cd4f50712c7ecf5,
`Stage 11: Add Synthea FHIR import and HL7 generation`, with the exact accepted 44
paths and clean index/tracked tree after commit. Stage 12 transport, Core ACK policy,
Workspace capture/profiles/history and lazy dockable Connection Manager/MLLP Listener
are implemented. The final implementation is frozen at 33 implementation/test/project
hashes in artifacts/stage12/final-implementation-hashes.json. Final locked restore
and zero-warning/error Release build pass; focused 41 Core, 64 Workspace/transport
and 8 Desktop cases pass. Final full solution: **1,818 passed**, zero failures/skips,
2m 56.578s. Complete Workspace stress: **848 passed**, zero errors/failures/skips/
unrun, 290.398s. Both final Release-layout ScriptHost lifecycles/isolation, API sync,
eight HL7 fixture blobs, 13 protected hashes, artwork/derivatives/excluded HL7 and
whitespace pass. The 33 implementation/project/test hashes still match after stress.
Earlier 1,811 solution / 847 stress logs are retained as pre-envelope-audit evidence.
The final-* logs cover ACK metadata/correlation fixes and background send/inspection
parsing. Inventory: 39 Stage 12 paths (18 modified tracked, 21 new), plus the unchanged
excluded untracked HL7; empty index, HEAD remains the Stage 11 checkpoint. No
disposable payload exports/probes were introduced; ignored audit/gate evidence is
intentionally retained. Implementation is complete for native acceptance.
See docs/stage12-tcp-mllp-acceptance.md and ADR 0016 for scope, explicit limits,
conditional ACK behavior, TLS deferral and native steps. No Stage 12 staging/commit,
external Git, package/deploy/release/publication or Stage 13 is authorized. Stop for
native Windows acceptance after the complete gate. Older pending Stage 11 statements
and deferred transport descriptions below are historical.

## Stage 11 native acceptance — checkpoint then Stage 12 authorization

On 2026-10-21 the user confirmed native Windows acceptance of all Stage 11 work,
including complete histories, and authorized one local 44-path checkpoint:
`Stage 11: Add Synthea FHIR import and HL7 generation`, expected parent
1b04e1849b2ba3c03d135cf324bae28b8af4cba8. Audit confirmed all 44 accepted hashes,
14 modified / 30 new paths, empty index, accepted 1,705 solution / 784 Workspace
stress results, zero-warning Release build and protected fixtures/artwork/excluded
HL7. No implementation/test byte changed after verification. Earlier pending/native/
no-commit restrictions below are historical. If checkpoint fails, stop.

After the checkpoint succeeds, proceed to Stage 12: interactive asynchronous bounded
TCP/MLLP connection management, one-message explicit sends, correlated original/
enhanced ACKs, loopback-default listener/test ACKs, safe endpoint profiles and local
session/document inspection. TLS may be explicitly deferred rather than weakened.
Stage 12 must remain unstaged/uncommitted and stop for native Windows acceptance.
No push/tag/package/deploy/release/publication or Stage 13 is authorized.

## Stage 11 acceptance improvement — complete histories, native acceptance pending

The user requested complete-history generation during pending Stage 11 acceptance.
Workspace/UI now default to Selected Patient, with Entire Import and advanced
Selected Events using the same mapping/candidate authority. Switching scopes retains
manual selections. Output stays one combined independent unsaved document; separate
patient documents are deferred because no atomic group-of-tabs rollback exists.
Stable patient grouping/clinical ordering, global MSH-10 ordinals, bounded count/size
preflight and count-only exclusion/ineligible/limit reporting are implemented. No new
clinical mappings, inferred facts, dependency, ScriptHost or publication redesign.

Current evidence: artifacts/stage11-history. Locked restore and the final indexed
Release build passed with zero warnings/errors; final focused 68 Core, 23 Workspace
and 32 Desktop tests passed. A source lookup avoids repeated encounter warning scans;
the 2,000-encounter case verifies complete counts beyond the output ceiling. Both
ScriptHost lifecycles pass against that final Release build. Full solution passed
**1,705 cases** (2m 51.607s) and complete Workspace stress passed **784 cases**
(290.253s), no failures/skips. API-reference sync, protected/fixture/artwork/excluded
hashes, all 37 final-indexed implementation/test/fixture hashes, exact inventory and
whitespace passed after stress. Earlier pre-index logs remain for diagnosis. Inventory
is 44 Stage 11 paths (14 modified tracked, 30 new) plus the unchanged excluded HL7;
no staging/commit is authorized. The earlier verified baseline below is historical.
Exact results/limitations and the 12-step complete-history native retest are in
docs/stage11-synthea-fhir-acceptance.md. Stop for native Windows review; no Stage 12.

## Prior Stage 11 verified baseline — native acceptance remained pending

Stage 10 is checkpointed locally as **1b04e1849b2ba3c03d135cf324bae28b8af4cba8**,
message `feat: add Stage 10 HL7 field extraction and analysis`, with exact 33-path
inventory, clean tracked-tree/empty-index post-check and preserved excluded HL7.
Do not amend or recreate it. Earlier checkpoint/pending instructions below are
historical. Stage 11 must remain unstaged/uncommitted; no push, tag, package, deploy,
release, publication or Stage 12 is authorized. Stop for native Windows acceptance.

Stage 11 implements local read-only, explicitly synthetic-confirmed FHIR R4 Bundle
import, conservative scoped reference resolution, bounded session patient/encounter/
clinical timeline browsing and seeded ADT A04/A01/A03 plus ORU R01 generation into
independent unsaved HL7 documents. The optional release-on-close Synthetic Patients
Dock tool retains placement only; Clear/close/reset cancels captures and releases
clinical state. Document-owned count-only Generation Reports survive tool closure
and view reconstruction. No SDK, dependency, patient database, network service or
ScriptHost permission change. Core/Workspace boundaries and original generator/save
contracts remain. See ADR 0015, docs/synthea-fhir-import.md and
docs/fhir-to-hl7v2-mapping.md.

Resume audit verified the actual working tree rather than trusting historical logs.
Final gate passed 2026-10-21: locked restore; Release 0 warnings/errors; focused
54 Core, 13 Workspace and 8 Desktop tests; **1,677 solution tests** (3m 09.482s)
and **774 complete Workspace stress tests** (352.997s), no failures/skips; both
ScriptHost lifecycles against the existing Release build, API-reference sync, eight
fixture blobs, all 13 protected hashes, original/derived icon integrity and whitespace.
All 32 final implementation/test/fixture hashes remain unchanged. Evidence:
artifacts/stage11; exact 39-path inventory (14 modified, 25 new), limitations and
native checklist: docs/stage11-synthea-fhir-acceptance.md. HEAD remains the Stage 10
checkpoint and index empty. All permanent coverage/fixtures remain; never open or
stage the excluded samples/synthetic-only/stage5b-repeated-patients_TEST.hl7.

## Stage 10 native acceptance — checkpoint then Stage 11 authorization

On 2026-10-21 the user confirmed Stage 10 native Windows acceptance. One exact
33-path local checkpoint is authorized after a fresh established verification gate,
message `feat: add Stage 10 HL7 field extraction and analysis`, expected parent
867016c3d1eab969c4a1853994eedb95621ad4f0. Accepted implementation/test hashes,
protected fixtures/artwork and excluded untracked HL7 match; index empty. Older
pending/no-commit/Stage-11 restrictions below are historical. If checkpoint fails,
stop. Evidence: artifacts/stage10-checkpoint-20261021.

Fresh Stage 10 checkpoint gate passed: locked restore; Release 0 warnings/errors;
1,602 solution tests and 761 complete Workspace stress tests, no failures/skips;
both ScriptHost lifecycles, API-reference synchronization, eight exact fixture blobs,
all 13 protected hashes, unchanged icon derivatives and text whitespace. All accepted
implementation/test bytes match; only native-acceptance records were updated.

After clean tracked-tree/empty-index checkpoint verification, proceed automatically
to Stage 11: local/offline, explicitly synthetic-only FHIR R4 Bundle import, bounded
session patient/timeline browsing and conservative documented ADT/ORU generation
into independent unsaved HL7 documents with count-only Generation Reports. Reuse
existing generator/publication/Dock infrastructure. Stage 11 must remain unstaged
and uncommitted and stop for native Windows acceptance. No push/tag/package/deploy/
release/publication or Stage 12 is authorized.

## Stage 10 implementation — awaiting native Windows acceptance

Stage 9C checkpoint completed as **867016c3d1eab969c4a1853994eedb95621ad4f0**, the
authorized exact 28-path local commit with parent ea897decdbd79d067d13552e972ab456885a61d6.
Post-commit tracked tree/index were clean; the excluded _TEST.hl7 was preserved.
Fresh checkpoint verification passed 1,544 solution and 733 complete Workspace
stress tests, locked restore, zero-warning Release build, both ScriptHost smoke
lifecycles, generated-reference synchronization, eight fixture blobs and all 13
protected hashes. Evidence: artifacts/stage9c-checkpoint-20261021.

Stage 10 adds View/document Field Extraction, editable ordered path configurations,
the immutable duplicable Interface Overview, First Match/ordinal Expand Matches,
explicit value states, captured-revision/stale/closed results and stable navigation.
Core scanner/path/escape APIs and Workspace immutable captures remain authoritative.
Private disk-backed results feed 100-row virtualized pages, cancellable literal/state/
numeric filters, stable sorting, bounded summaries and complete selected-result CSV.
CSV retains formula/state fidelity and uses existing atomic publication/source alias
guards. Saved user definitions retain preferences without source/results/filter
literals; optional tool closure drains storage and retains the configuration draft
only. No source mutation, worker protocol, new dependency or Dock schema change.

Ceilings are explicit: 32 columns, 4,096 rows/message, 1,000,000 result rows, 1 GiB
journal, 65,536 UTF-16 units/value, 4 MiB sort keys, 1,024 distinct hashes/column and
4 GiB CSV. Expansion is ordinal alignment, not a clinical group join; diagnostics
and truncation require review. See docs/field-extraction.md and ADR 0014.

Stage 10 final verification results and the exact inventory are recorded in
docs/stage10-extraction-analysis-acceptance.md. Final gate passed: locked restore;
Release build with 0 warnings/errors; focused 22 Core, 28 Workspace/library,
86 Desktop and 63 shared-CSV/API/real-worker Scripting cases; **1,602 solution tests**
(2m 59.818s), **761 complete Workspace stress tests** (5m 12.293s), no failures/skips;
both ScriptHost lifecycles, generated-reference sync, eight fixture blobs, all 13
protected hashes, unchanged ICO/master and whitespace. Verified implementation/test
hashes are unchanged. Evidence: artifacts/stage10. HEAD remains the Stage 9C
checkpoint, index empty; 33 Stage 10 paths (15 modified, 18 new) plus the unchanged
excluded untracked HL7. All Stage 10 changes must remain
unstaged/uncommitted. Stop for native Windows acceptance; no Stage 10 commit, push,
tag, package, deploy, publication, release or Stage 11 is authorized. Earlier pending
Stage 9C/checkpoint instructions below are historical.

## Stage 9C native acceptance — checkpoint then Stage 10 authorization

On 2026-10-21 the user confirmed Stage 9C native Windows acceptance and authorized
one exact 28-path local checkpoint after a fresh verification gate, message
`feat: add Stage 9C scripting API reference and templates`, expected parent
ea897decdbd79d067d13552e972ab456885a61d6. All accepted hashes match the final manifest;
index empty and excluded _TEST.hl7/protected material unchanged. Earlier pending
Stage 9C/no-Stage-10 statements below are historical. If checkpoint fails, stop.

After its clean tracked-tree/empty-index post-commit gate, proceed automatically to
Stage 10: read-only Field Extraction using existing indexing/scanner/capture, saved
configuration, docking and CSV/export patterns. Include first/expanded matches,
bounded results/filter/sort/summary, stable navigation and stale/closed-source state.
Stage 10 must remain unstaged/uncommitted and stop for native Windows acceptance.
No push/tag/package/deploy/release/publication or Stage 11 is authorized.

Fresh Stage 9C checkpoint gate passed: locked restore; Release zero warnings/errors;
1,544 solution tests, 733 complete Workspace stress tests, no failures/skips; both
ScriptHost lifecycles against the existing Release layout; generated docs sync;
eight exact fixture blobs, all 13 protected hashes and whitespace. The exact 28-path
accepted implementation is unchanged; only native-acceptance records were updated.
Evidence: artifacts/stage9c-checkpoint-20261021.

## Stage 9 native accepted — checkpoint then Stage 9C authorized

Stage 9 checkpoint completed as ea897decdbd79d067d13552e972ab456885a61d6, exact
41-path inventory, expected Stage 8D parent, clean tracked tree and empty index.
The local excluded _TEST.hl7 remains unchanged/untracked. Stage 9C is implemented
and verified, awaiting native Windows acceptance, and remains unstaged/uncommitted.
Its acceptance record contains measured results, the exact inventory and native steps.

Stage 9C adds one offline version-1/API-2 reference catalog (50 topics, six complete
starters) embedded verbatim in Workspace. Generated repository Markdown reuses that
source. Help and the visible Script Editor API Reference button open one optional
dock tool with bounded search/history, category navigation, exact clipboard copy,
theme-aware selectable details and the existing unsaved-draft transition for starter
creation. My Scripts labels clarify independent user copies; existing library schema,
built-ins and persistence behavior remain. Existing public synthetic helpers are
already accessible; no facade, worker protocol/engine changes, dependency or full
completion system is required. Tests classify the public API, execute examples and
templates, retain native clipboard/local undo and verify docking/theme/customization.
See docs/stage9c-script-developer-experience-acceptance.md. No Stage 9C commit, push,
tag, package, deploy, release, publication or Stage 10. Stop for native acceptance.

Final Stage 9C gate passed: locked restore; Release zero warnings/errors; focused
123 Scripting, 38 Workspace/library and 50 Desktop; **1,544 full solution tests**
(2m 53.028s), **733 complete Workspace stress tests** (5m 05.767s), no failures/skips;
both ScriptHost lifecycles against the existing Release layout; generated-reference
synchronization; eight exact fixture blobs, all 13 protected hashes, unchanged
ICO/master and all text whitespace. Evidence is retained in ignored artifacts/stage9c.
HEAD remains the Stage 9 checkpoint; the index is empty, with 28 Stage 9C paths
(17 tracked modifications, 11 new) plus the unchanged excluded untracked HL7.
No new worker facade or completion system. Stop for native acceptance; no Stage 9C
commit or further milestone work is authorized.

On 2026-10-20 the user accepted Stage 9 de-identification, realistic identities,
Transformation Report and optimized icon on native Windows. One exact 41-path local
checkpoint is authorized after a fresh full gate, with message
`feat: add Stage 9 de-identification and application branding`. The expected parent
is 5eebc767eae5f241ac4c7a02c4275ae0305b76fa. Include the unchanged approved source
artwork; preserve/exclude the unchanged untracked _TEST.hl7. Earlier pending/no-commit
statements below are historical. If checkpoint verification/commit fails, stop.

The fresh checkpoint gate passed: locked restore; Release zero warnings/errors;
1,504 solution tests and 718 complete Workspace stress tests (no failures/skips);
both ScriptHost lifecycles against the existing Release layout; four icon-processing
tests; eight exact fixture blobs, all 13 protected hashes and whitespace. Evidence:
artifacts/stage9-checkpoint. Original artwork and excluded _TEST.hl7 are unchanged.

After its clean tracked-tree/empty-index post-commit gate, proceed automatically to
Stage 9C: offline searchable API reference, safe script templates/customization and
editor/Help discovery using the existing Script Library/editor/worker. Stage 9C must
remain unstaged/uncommitted and stop for native Windows acceptance. No push, tag,
package, deploy, release, publication, Stage 10 or full IntelliSense/editor rewrite.

## Stage 9 native acceptance corrections — historical pre-checkpoint record

Native testing requested realistic demographics, discoverable output-associated
reports and clearer small taskbar icons. Patient Deidentifier now uses policy 2 and
an original bundled dictionary 1 through SyntheticIdentityLibrary: independent
fictional names/aliases, addresses, reserved phone/email values, DOB/sex, demographics,
numeric local IDs and supported coherent age observations. Qualified identities
share complete profiles; bounded deterministic probing handles allocation collisions.
Source demographics do not select replacements. The two narrow presets retain
policy 1. Unknown/narrative/ED content remains suppressed/reviewable; no PHI-free or
compliance guarantee. Missing/empty/null source positions remain distinct.

The existing sealed CSV report is projected into an expanded Transformation Report
on each output document. It validates a fixed count-only schema and shows preset,
scope, captured revision, live stale/closed-source state, category counts and privacy
warnings. The document owns the bounded summary and observer disposal, independently
of the editor's last run. Summaries are session-only; later output edits are not
reassessed. Existing CSV export and atomic execution behavior remain unchanged.

Optimized 16/24/32 icon variants enlarge the existing monogram, reduce padding and
simplify tiny bars; 48–256 and the master/source are unchanged. Avalonia now loads
the ICO rather than the 512 PNG so optimized native sizes are available to windows.
Four permanent processing tests complement the three Desktop branding tests.

Correction verification passed: locked restore/zero-warning Release; focused 62
Scripting, 43 Workspace/library, 21 Desktop + four processing tests; **1,504 solution
tests**, **718 complete Workspace stress tests**, both ScriptHost lifecycles against
the Release build layout, eight exact fixture blobs/all 13 protected hashes and
whitespace. No failures/skips/errors. Source artwork/master and larger icon frames
are unchanged; new small variants reproduce byte-identically. There are 41 milestone
paths (17 tracked modifications + 24 untracked), plus the unchanged excluded HL7;
index empty. See the acceptance document for exact inventory and native checks.
Evidence: artifacts/stage9-acceptance-fixes. The initial 1,483/712 gate below is historical.
No staging/commit, push, tag, package, deploy, publish, release or Stage 10 is authorized.
HEAD remains 5eebc767eae5f241ac4c7a02c4275ae0305b76fa. Stop for native Windows retesting.

## Initial Stage 9 implementation — historical pre-correction gate

The Stage 8D checkpoint gate passed and created local commit
5eebc767eae5f241ac4c7a02c4275ae0305b76fa with the exact 35-file accepted inventory.
Tracked tree/index were clean; approved icon source and excluded user HL7 stayed
unchanged/untracked. Fresh gate: locked restore, Release zero warnings/errors,
1,416 full-solution cases, 698 Workspace stress cases, both ScriptHost lifecycles,
eight fixture blobs and protected/whitespace checks. Logs: artifacts/stage8d-checkpoint-20261020.

Stage 9 implements three immutable/duplicable presets through the existing Script
Library and isolated worker, with deterministic authority-scoped run-local identities,
common date shifts, conservative suppression and one count-only Review required report.
Core-backed literal batch edits preserve encoded spans/structure and publish the
validated facade model; worker serialization follows that facade. No protocol/new
engine/dependency or persistent mapping storage is introduced. Reports explicitly
flag unassessed containers/nested data, narrow-preset retained values and residual
privacy risks. No compliance/anonymity guarantee. See ADR 0013 and the Stage 9
acceptance matrix. All Stage 9/icon changes remain unstaged/uncommitted, with no
external Git/publication action or Stage 10 authorized. Stop for native acceptance.

Approved icon integration preserves the original M/W artwork and uses a reproducible
connected-exterior removal, centered square crop and Lanczos resize. The seven-frame
ICO is embedded in the Windows apphost; the shared Avalonia Window style covers main
and secondary top-level windows. Source hash, exact PE frame bytes and headless style
application are tested. Taskbar, Alt+Tab, native title bars, light/dark appearance and
100/125/150/200% DPI remain native acceptance checks. See docs/branding.md.

Final Stage 9/icon gate passed on 2026-10-20: locked restore, Release zero warnings/
errors, focused Scripting 47/Workspace 37/Desktop 21, **1,483 full-solution cases**,
**712 complete Workspace stress cases**, both ScriptHost Current/Whole smoke
lifecycles against the existing Release layout, eight fixture blobs/all 13 protected
hashes, reproducible icon derivatives and whitespace. All tests passed without skips.
No publish/package operation ran. Exact 29-path inventory, measured results and native
checklists are in docs/stage9-deidentification-acceptance.md and docs/branding.md;
evidence is retained in ignored artifacts/stage9. HEAD is still the Stage 8D checkpoint,
the index is empty, and 12 tracked modifications/17 milestone untracked files plus
the unchanged excluded _TEST.hl7 are deliberately uncommitted. Native acceptance is
pending; wait for user authorization before any next milestone or commit.

## Stage 8D checkpoint — native accepted; sequential Stage 9 and icon authorization

On 2026-10-20 the user confirmed native Windows acceptance of Stage 8D and authorized
one local checkpoint, `feat: add Stage 8D synthetic HL7 message generator`, after a
fresh audit/verification gate. All 35 accepted paths match the saved final manifest.
The unchanged local _TEST.hl7 and newly supplied assets/branding/workbench-icon-source.png
are excluded from that checkpoint. The artwork is preserved for the later icon phase.
This supersedes earlier pending/native-acceptance/no-commit statements below.

After successful checkpoint verification/commit and its clean tracked-tree/empty-index
gate, implement Stage 9 de-identification aids through the existing Script Library
and isolated ScriptHost, then integrate derivatives of the approved icon. Do not
begin those phases if the checkpoint fails. Stage 9/icon changes remain unstaged and
uncommitted and stop for native Windows acceptance. No push, tag, release, packaging,
deployment, publication or Stage 10 work is authorized.

Fresh checkpoint verification passed locked restore, Release 0 warnings/errors,
all 1,416 solution tests (145.094 s), all 698 Workspace stress cases (251.846 s),
both ScriptHost smoke lifecycles, eight fixture blobs, protected/excluded/icon-source
hashes and whitespace checks. The exact 35-file inventory and manifests are retained
under ignored artifacts/stage8d-checkpoint-20261020. Application/test bytes remain
identical to the native-accepted Stage 8D manifest; only five documentation records
changed before staging. The hash and post-commit gate are recorded after creation.

## Stage 8D synthetic generator — implemented; native acceptance pending

The mandatory Stage 8C gate passed after local commit
`ddc18ae63df7ddb31ba55ee35c3c6feffd0d3891` (`feat: add Stage 8C message insertion UI`):
exactly 30 accepted paths, clean tracked tree/empty index, unchanged excluded user
file, locked restore, zero-warning Release, 1,327 solution and 656 complete Workspace
stress cases, both ScriptHost lifecycles, eight fixture blobs and whitespace checks.
Its manifests/results remain under ignored artifacts/stage8c-checkpoint.
This latest sequential authorization supersedes older no-8D statements below.

Stage 8D adds Core's deterministic version-1 synthetic HL7 v2.5.1 A01/A04/A08/O01/R01
generator, Workspace background generation/prospective streaming validation/one-batch
publication, and Home/File Generate Messages with compact configuration and identifier
preview. Defaults: new unsaved A01, count 1, seed 42, fixed UTC 2030-01-01 midnight,
prefix SYN. Generated content uses CR, invariant formatting and an explicit PRNG;
all identity/provider/facility data is obviously fictional. No imported data is used.

All four insertion positions use the existing sequence/history and stable session IDs.
Cancellation/failure before publication preserves text, membership, selection, revisions,
save points and Redo. New output has no filesystem destination or original provenance.
Prospective validation uses temporary disk proportional to the entire output but
bounded memory, so unsafe joins/containers/unfinished drafts reject before insertion.
The existing root shortcut, clipboard and rejected-input transaction fixes are retained.
See [ADR 0012](decisions/0012-deterministic-synthetic-generation.md) and
[Stage 8D acceptance](stage8d-synthetic-generator-acceptance.md) for contract, limits,
verification, performance evidence and executable native Windows steps.

Stage 8D is intentionally uncommitted and awaits native Windows acceptance. No further
staging/commit, push, tag, release, package, deploy or publication is authorized.
The excluded samples/synthetic-only/stage5b-repeated-patients_TEST.hl7 remains untouched.

Final Stage 8D verification passed locked restore, Release with zero warnings/errors,
27 focused Core cases, 132 focused Workspace cases, 65 focused Desktop cases plus
a 52-case overlapping ribbon/script-editor retest, all 1,416 solution cases and all
698 complete Workspace stress cases (267.057 s). Both ScriptHost Release-layout smoke
lifecycles, eight fixture blobs, excluded-user-file integrity and whitespace checks
passed. New coverage adds 89 cases (27 Core/42 Workspace/20 Desktop), with none removed.
Stress covered 1/100/1,000 generation, 100,000 live messages and 100/256/512 MiB source
insertion/save byte preservation. The 512 MiB generation case allocated 14,397,304
bytes and retained 1,287,272 bytes; observed costs are not performance guarantees.
Home command overflow was corrected using the existing compact two-row layout, and
new-output focus waits for actual Dock mounting; existing native input/history files
remain byte-identical. The final inventory is 35 unstaged paths (21 modified/14 new),
plus the unchanged excluded user file. Useful reports and reusable generated fixtures
remain ignored. Stop for native Windows Stage 8D acceptance.

## Stage 8C checkpoint — native accepted; sequential Stage 8D authorization

On 2026-10-18 the user confirmed successful native Windows Stage 8C acceptance and
authorized exactly one local commit containing its 30 accepted paths, with message
`feat: add Stage 8C message insertion UI`. This supersedes historical pending/no-commit
instructions below. The pre-commit audit matches the complete saved Stage 8C manifest;
the sole unrelated path remains the unchanged, untracked user _TEST.hl7.
Final checkpoint verification and its inventories are retained under ignored
artifacts/stage8c-checkpoint and recorded in the Stage 8C acceptance document.
The final checkpoint retest passed locked restore, Release build with zero warnings/
errors (1.11 s), all 1,327 solution tests (122.899 s), all 656 Workspace stress tests
(213.233 s), both ScriptHost smoke lifecycles, all eight original fixture blob hashes,
excluded-user-file integrity and whitespace/conflict checks. No tests or fixtures
were removed or weakened. Only the four native acceptance/authorization records
changed during checkpoint preparation; application/test hashes match the accepted
Stage 8C manifest. The commit carrying this record is the Stage 8C checkpoint;
its hash and the mandatory post-commit gate are recorded separately after creation.

Stage 8D implementation is authorized only after the new commit, clean tracked tree,
empty index, unchanged excluded user file and documented verification pass the
mandatory Phase 1 gate. No Stage 8D code belongs in the Stage 8C commit. No Stage 8D
commit, push, tag, package, deploy, release or publication is authorized. After the
gate, implement the deterministic synthetic generator and stop for native acceptance.

## Stage 8C message insertion UI — native accepted; historical implementation record

The user's Stage 8C request authorizes insertion UI/workflow integration from main
at 16ebc6d2a2a9d36ddfb20c766976c18990846bb2. This supersedes older no-8C statements
below. Stage 8B.1–8B.4, root Undo/Redo and Script Output Naming are native accepted
and preserved by that local checkpoint. Stage 8D and all staging, commit, push, tag,
release, packaging and publication remain unauthorized. Stop for native Windows
Stage 8C acceptance after verification.

Add Message appears in Home, File → Add Message and the existing navigator,
with Before Current/After Current/Beginning/End choices over the live logical order.
Unfiltered Next at logical EOF inserts through the existing Workspace API. Next
within a filter never appends. A trailing inserted draft containing only Unicode
whitespace (including CR/LF/tab) is reused based on retained document state, even
before parsing completes or with Edit Mode off. Non-whitespace partial content
permits another append without certifying save eligibility. Explicit insertion
invalidates discovery/filter state while retaining authored queries.

Successful insertion selects the new ID, activates session Edit Mode and focuses
the blank editor after popup dismissal; the persisted startup preference is unchanged.
Stable document hosts now also wrap editable in-memory originals, so inserted siblings
and structural history replace the active view correctly. Existing component tests
construct the active inspection surface explicitly; all previous assertions remain.
The accepted window-root history authority is unchanged. Capability feedback reads
bounded draft budgets and cached immutable index metadata, without source I/O.

The real clipboard regression exposed AvaloniaEdit's newline/tab normalization.
A per-HL7-editor Paste command binding retains clipboard characters, uses the existing
selection/update transaction and Workspace history, and rejects late completions
after changes to the message, revision, selection, edit availability or view lifetime.
Independent inputs retain their own commands. No undo stack is cleared during input.
The rejected-input restore path now replaces document text instead of assigning
TextEditor.Text, whose implicit ClearAll broke open input undo groups. A permanent
multi-message-paste rejection regression verifies atomic recovery and history.
Blank save rejection now identifies the offending logical message; streaming validation,
destination checks, original provenance, limits and serialization policy remain.

The pre-existing untracked samples/synthetic-only/stage5b-repeated-patients_TEST.hl7
is preserved by SHA-256 only. All eight tracked HL7 fixtures remain protected. Baseline
status/hashes and useful verification reports live under ignored artifacts/stage8c.
See stage8c-message-insertion-acceptance.md for final results and native instructions.

Final verification passed: locked restore; Release build with zero warnings/errors;
90 focused Workspace cases; 78 focused Desktop cases; all 1,327 solution cases;
all 656 complete Workspace stress cases (254.793 s); both ScriptHost Release-layout
smoke lifecycles; all eight tracked fixture blob hashes; excluded-user-file SHA-256;
and tracked/untracked whitespace/conflict checks. There are zero errors, failures,
skips or not-run cases in the direct focused/stress runs, and zero failures/skips in
the full solution. This adds 34 Desktop and 10 Workspace cases to the accepted
baseline, with no removed tests. Stress exercised the existing 100/256/512 MiB
streaming-save/lazy-capture and 100,000-message history/mapping cases. New UI coverage
also proves bounded insertion and original-byte hashes in a 16 MiB source-backed file.
Generated shared fixtures and useful initial/final diagnostics remain ignored.

The final scope is 30 paths: 12 implementation, 14 test/setup and four documentation
paths (21 modified tracked and nine new milestone files). All original behavioral
assertions in the seven component-test factory adaptations are byte-identical after
the setup substitution. Core, dependencies, scripting API/protocol/worker and root
history routing are unchanged. Main/HEAD remains the Stage 8B checkpoint; index empty.
The only unrelated path is the unchanged, untracked user _TEST.hl7. No staging,
commit, push, tag, package, release, publication or Stage 8D work was performed.
Stop here for native Windows Stage 8C acceptance.

## Stage 8B checkpoint — native accepted; one local commit authorized

On 2026-10-17 the user confirmed native Windows acceptance of Stage 8B.1–8B.3,
the window-root Undo/Redo correction, Stage 8B.4 compatibility integration and Script
Output Naming. The user authorized staging only this accepted work and creating one
local commit with message `feat: complete Stage 8B structural editing and scripting integration`.
This supersedes the historical pending-acceptance/no-commit instructions below.
No push, tag, publish, release, Stage 8C or Stage 8D is authorized. Stop after the
local checkpoint and its verification.

The pre-commit audit found 46 modified tracked paths and 26 untracked paths on main
at 8808929e844cb925fbdb11635e0b55740e5ec160. All 71 application/test/documentation/
verification paths belong to the accepted work. The pre-existing local
samples/synthetic-only/stage5b-repeated-patients_TEST.hl7 is explicitly excluded and
preserved untouched. Ignored build outputs, logs, artifacts, local configuration
and generated fixtures are not part of the commit. No existing permanent test is
removed. Audit inventories, hashes and final verification logs are retained under
ignored artifacts/stage8b-checkpoint. The commit carrying this record is the local
checkpoint; its hash is reported by Git, not embedded in its own contents.

Final checkpoint verification passed: locked restore; Release build with zero
warnings/errors; all 1,283 solution tests (zero failures/skips); all 646 complete
Workspace stress tests (zero errors/failures/skips/not-run, 292.607 s); both ScriptHost
smoke lifecycles against the existing Release layout; all eight tracked HL7 fixtures
matching the baseline Git blobs; and `git diff --check`. The desktop restart occurred
after restore/build/full-suite completion and before smoke/stress verification. Saved
logs and matching hashes for all 71 audited paths confirmed those completed checks
remained valid; only the unfinished smoke/stress checks were run after restart.
The excluded user HL7 retains its initial SHA-256. No code or test changed during
checkpoint preparation; only current acceptance/authorization records were updated.
Git's existing LF/CRLF conversion advisories do not indicate whitespace-check or
build failures. Verification logs, the explicit accepted-file inventory and audit
hashes remain in ignored artifacts/stage8b-checkpoint; generated outputs stay excluded.

## Script output naming — accepted prior checkpoint

The 2026-10-17 naming request records user acceptance of Stage 8B.1–8B.4 and the
window-root Undo/Redo correction, all intentionally uncommitted. Earlier pending
8B.4 acceptance notes below are historical. Only the naming enhancement is authorized;
no Stage 8C/8D, staging, commit, push, tag, package, release or publication. Main/HEAD
remain 8808929e844cb925fbdb11635e0b55740e5ec160. Preserve the starting 60 status paths
(39 modified tracked, 20 new milestone files, one pre-existing user HL7 file).

Independent output filenames now use the clean script name captured at Run, with
`.hl7`, falling back to Script Result.hl7. Batch.SetOutputFileName is optional during
initialization/Main/Complete; the last valid suggestion wins. Invalid suggestions
are nonfatal, retain earlier valid values/defaults and yield a fixed notice. A shared
Windows policy bounds names to 128 UTF-16 units including extension, refuses paths,
devices/unsafe Unicode/characters and never touches the filesystem. The parent
revalidates optional version-1 OutputNaming metadata on the existing v2 Finished
frame. API/library schema, lifecycle, byte validation, report ownership, stale-source
publication and source Save identity remain unchanged.

Workspace Title retains the underlying suggested filename. Only unsaved generated
DisplayTitles receive duplicate suffixes; Desktop Save As options use Title. After
explicit Save As, existing actual-filename/dirty-state behavior applies. Captured
suggestion/ignored-suggestion metadata remains with successful output/report origin,
independent of later script rename/selection or view reconstruction. No filename is
embedded in HL7 bytes or logged in Activity Log.

New permanent Scripting, Workspace and Desktop naming tests and the existing worker
smoke cover this behavior. The smoke runs against the current normal Release layout,
including worker hashes/dependency isolation, without creating a publish artifact.
The ignored artifacts/script-output-naming baseline/manifest preserves all 60 starting
paths (user HL7 hash only), and useful verification reports remain there. See
docs/script-output-naming-acceptance.md and docs/script-api.md for contracts, exact
final results/file inventory and native acceptance. Stop for native Windows naming
acceptance after automated verification; no additional milestone or Git work follows.

Verification passed: locked restore and Release build with zero warnings/errors;
148 focused Scripting, 98 focused Workspace and 64 focused Desktop cases; all 1,283
solution cases; all 646 complete Workspace stress cases; two worker deployment smoke
lifecycles; tracked fixture integrity and whitespace checks. The naming delta spans 20
paths, adds 69 permanent cases and retains every prior test. The cumulative tree has
46 tracked modifications and 25 new milestone files plus the pre-existing user HL7
(72 status entries), with an empty index and unchanged branch/HEAD. All eight tracked
HL7 blobs, the user HL7 and protected routing/safety files retain their hashes. Exact
commands, results and native acceptance are recorded in the naming acceptance document.

## Stage 8B.4 compatibility integration — accepted prior checkpoint

On 2026-10-17 the user accepted Stage 8B.1–8B.3 and the native window-root Undo/Redo
correction, all intentionally uncommitted, and authorized Stage 8B.4 only. Main/HEAD
remain 8808929e844cb925fbdb11635e0b55740e5ec160. Preserve the existing working tree;
no staging, commit, push, tag, package, release or publication. Do not begin 8C/8D.
The earlier pending-acceptance/8B.4 restrictions below are historical and superseded.

Compatibility now uses stable MessageIds in Search results/filter membership and
Comparison selections. Captures retain logical metadata and bounded immutable draft
references, scanning unchanged originals lazily under existing read leases. Structural
changes/editing cancel and clear stale discovery state without changing authored or
saved queries; navigation and successful Save alone keep valid results. Comparison
retains unavailable insertion IDs across Undo and resolves the same ID on Redo.
Original always means the initial source; inserted messages have no original side,
source ordinal or source bytes during this open session, including after Save.

Script capture reuses the streaming save plan and membership proof, with immutable
job-ordinal-to-ID/provenance mapping and output/diagnostic attribution. Whole scope
includes all logical entries/current drafts. Current scope omits inactive insertions
explicitly from its mapping and uses original fallbacks for inactive originals, so
unrelated invalid drafts do not block a valid target. The selected ID is never omitted.
Per the user's explicit conflict-policy choice, completed independent captured output
is retained after source edits/insertion/history changes, with captured revision and
stale-source attribution in the result, Script Editor and reports. Tab switches and
source navigation never retarget a captured job. Source closure/cancellation still
roll back private artifacts; a queued revision conflict before capture fails safely.
Worker protocol/isolation, limits, libraries, saved-search schema, source ownership
and save semantics remain.
Activity diagnostics and raw inspectors retain stable IDs and original provenance
separately from captured/current positions. DocumentHistoryShortcuts is unchanged.

Final verification passed locked restore, Release build with zero warnings/errors,
all 1,214 solution cases, 85 focused Workspace cases, 41 focused Desktop cases and
633 complete Workspace stress cases (zero failures/skips). New coverage adds 39
cases and retains earlier tests. Stress covers 100,000 logical messages and
100/256/512 MiB lazy search/capture with exact original-byte preservation. At 100,000,
search/capture measured 690.430/41.984 ms; combined retained metadata grew 31,324,128
bytes. Large script capture allocated 245,328–250,208 bytes across 100–512 MiB inputs.
These are single-run observations, not timing guarantees. Full/focused runs also
verify memory-original diagnostic labels, insertion after staged worker output,
captured output despite source edits/history changes, source closure/cancellation,
unmasked worker failure and visible stale attribution through view reconstruction.

All eight tracked HL7 blobs, protected native routing/input-safety source/tests and
the pre-existing user _TEST.hl7 remain unchanged. Whitespace/conflict checks pass;
all 37 starting files remain. Compatibility changes span 41 paths; the complete
uncommitted milestone has 39 tracked modifications and 20 new milestone files,
plus that pre-existing untracked user file. Nothing staged; branch/HEAD unchanged.
The accepted starting-tree snapshot/manifest and useful pass/failure reports are
retained in ignored artifacts/stage8b4; earlier artifacts and build outputs remain.

See docs/stage8b4-compatibility-acceptance.md, ADR 0011 and updated script/search/
comparison API docs for contracts, full inventory, results and native instructions.
Stop for native Windows 8B.4 acceptance. Insertion-specific interactive acceptance
awaits 8C because no production insertion command exists; permanent Workspace and
headless Desktop tests cover those session states. No 8C/8D or Git action is authorized.

## Accepted Stage 8B.1–8B.3 foundation — historical checkpoint

On 2026-10-17 the user authorized Stage 8B.1 (stable identity/logical sequence),
8B.2 (structural transactions/document-wide chronological history), and 8B.3
(validated lossless streaming publication) from main at
8808929e844cb925fbdb11635e0b55740e5ec160. These checkpoints are implemented;
final automated verification is recorded in docs/stage8b-structural-editing-acceptance.md.
Native Windows acceptance and separate Git authorization are pending. Do not stage,
commit, push, tag, package, release or publish. Stop before 8B.4; full search/comparison/
scripting migration, insertion UI/Next append and generation remain unauthorized.

Workspace now owns typed MessageIds, explicit provenance, metadata-only logical
order, ID-keyed bounded buffers and chronological history. Undo/Redo selects its
affected message even with Edit Mode off or a source-backed current message.
Save points compare actual text/membership rather than history position. Saving
freezes mutations, streams a bounded plan, re-indexes the flushed candidate through
the existing boundary implementation, and verifies mapped bytes before replace/move.
Original snapshots/indexes never rebase. Source capture records an identity/hash
baseline; associated Save refuses external changes since open/last publication.
No new dependency, worker protocol, Core API, editor limit or Dock schema change.
See ADR 0011 for ownership, unsupported boundaries and remaining filesystem races.

Original-ordinal search/comparison/script operations fail safely while membership
differs from the original, including after Save. Structural changes clear searches/
filters and invalidate comparisons. Reopen saved output (fresh source/IDs), or undo
all insertions, to restore ordinal compatibility. Unchanged originals retain correct
original-byte export; inserted entries have no original range/comparison source.
Known unsupported provisional/batch/ambiguous containers reject insertion before
mutation; unsafe joins and empty/unrecoverable inserted drafts cannot publish.
Small Desktop adaptations maintain existing Undo/editor/Inspector/count behavior
and guard remaining ordinal source selection; they are not the full 8B.4 migration.

Foundation verification: locked restore and zero-warning/error Release build passed;
all 1,173 solution cases, 46 focused Workspace cases, three focused Desktop cases
and 599 Workspace stress cases passed with no failures/skips. All eight tracked HL7
fixture blobs remain unchanged. The stress run covers 100,000 live drafts and
100/256/512 MiB untouched payload preservation. Maximum sequence metadata allocation
was 5,441,960 bytes; 100,000 actual empty drafts retained 23,685,408 bytes. Measured
save allocations stayed approximately 0.38–0.41 MiB across those payload sizes.
Detailed phase timings, full file inventory, retained ignored reports and exact
native commands are in the acceptance document.

Native acceptance exposed a Desktop keyboard-routing defect: history navigation
replaced the focused RAW view and cleared focus, so the next key missed both
view-local handlers. A platform-input regression reproduced it. The correction
uses one DocumentHistoryShortcuts tunnel authority on main/floating window roots,
resolving the active document for every key. The local handlers are removed;
independent TextBox/script/other editor histories and native input transactions
remain intact. No Workspace/Core, save or identity changes were needed.
Correction verification passed: locked restore, Release build with zero warnings/
errors, nine focused Desktop cases, 83 relevant Workspace cases and all 1,175
solution cases, with no failures/skips. All eight tracked HL7 blobs and staging
remain unchanged; whitespace checks passed. Updated native keyboard instructions
and the exact correction inventory/results are in the acceptance document.
Stop here for native Windows retesting; Stage 8B.4/8C/8D remain unauthorized.
Working tree: 26 tracked modifications and ten new milestone files, plus the
pre-existing untracked stage5b-repeated-patients_TEST.hl7, which is preserved.
Nothing staged; main/HEAD remain at the baseline. No commit, push, tag, package,
release or publication. Earlier ignored artifacts and permanent tests are retained.

### Accepted Stage 7 baseline

Stage 7 Message Comparison has passed native Windows acceptance, including the UX/paste/
settings refinements and Side-by-Side default, as confirmed by the user on 2026-10-16.
The user explicitly authorized one Stage 7 commit on main from the Stage 6B baseline
`d7ad3e12ae63d8f42f1a26a743803ac081d5e6eb`, with message
`feat: add Stage 7 HL7-aware message comparison`, and a normal push to origin/main.
Authorization covers only the accepted 32-file milestone. No new features, unrelated
refactors, history rewriting, tags, releases, installers or distribution packages.
Earlier pending-acceptance/Git restrictions in the historical checkpoints below are
superseded by this authorization. Stop after commit/push; Stage 8 requires separate authorization.

The Stage 7 native UX correction defaults new sessions to Side-by-Side, with both empty
pasted-text panes immediately visible/editable. Differences remains a grouped summary of
the same result/selection through the existing checked view toggle; mode
changes do not recapture or reload sources. A/B controls offer Open Message, Original
Message and editable temporary Pasted Text, with direct pane Ctrl+V and Differences
empty-state paste buttons. Each Workspace ComparisonInput retains its independent pasted draft across
kind/mode changes. Immutable pasted-string captures need no documents or disk snapshots.
Typing debounces 250 ms, cancels superseded work and preserves a selected coordinate
where possible. Exact clipboard insertion avoids AvaloniaEdit's newline conversion.
Pasted inputs have the existing 1 Mi UTF-16 / 16 Ki-line editor bounds; rejection retains
the previous draft. The shared scanner rejects multiple-MSH pasted input explicitly,
rather than silently selecting one message. Malformed/incomplete single-message text
is retained and diagnosed. Source slots, pasted text and results clear on tool closure.
Options and Copy move to compact menus; selected detail previews are bounded to 4,096
characters. Document-backed sources remain read-only, with independent 64 Ki windows;
pasted sources use complete editable bounded text. No source edits, saves or history changes.

Settings now contains Preferences only. The General edit-mode default, its existing
JSON persistence and application to newly opened documents are unchanged. Menu-only
StartInEditMode synchronization/click wiring is removed. The separate Script Editor
VS Dark palette remains deferred.

Core's read-only comparison engine uses the shared structural scanner and message-local
UTF-16 source ranges. Deterministic bounded lookahead aligns repeated segments (64)
and repetitions (32), with exact content fingerprints followed by name anchors for
unmatched different segment names. Both sides retain independent occurrence coordinates;
Set IDs are ordinary values. MSH declarations stay literal, supported escape spelling
differences are distinguished from changed values, and missing/empty/null remain distinct.
Show all is the default; MSH-7, MSH-10, trailing-empty-field and terminator ignores are
explicit and excluded from active counts. Scanner limits remain intact; 20,000 differences
is an explicit failure boundary, with no partial result presented as complete.

Workspace captures only two selected original ordinals from immutable original snapshots
or exact current/original draft strings. Captures share the existing snapshot-read drain
boundary with search, own private decoded spools and release them after bounded reads
drain. No whole-document materialization, navigation, source writes, history changes,
worker changes or new dependencies. Content revision/closure invalidates results;
generation checks reject stale completions. Saving alone does not redefine the original.

Desktop's optional `tool.message-comparison` starts in the document group, can float/dock
and releases all feature state on close/reset. Only placement metadata survives. HL7 Tools
and raw-editor Compare With Original open the same workspace. Independent 64 Ki UTF-16
source windows use the existing syntax/theme palette; navigator selections intentionally
locate both sides, with nearest same-name context when a side is absent. Clipboard actions
read exact ranges, bounded to 1 Mi UTF-16 units, never abbreviated preview values. No
comparison content enters logs/settings/layout/history. See message-comparison.md,
ADR 0010 and stage7-message-comparison-acceptance.md for limits, tests and native checks.

Initial Stage 7 automated verification on 2026-10-16: locked restore and Release build passed
with zero warnings/errors. All 1,084 tests passed (66 new ordinary comparison cases),
and all 536 Workspace stress cases passed, including the additional 32 MiB comparison
variant and existing 256/512 MiB tests. Full and stress suites must run sequentially:
their pre-existing generated fixture filenames are shared. All eight HL7 fixture byte
hashes are unchanged. Tracked/untracked whitespace/conflict checks passed. The permanent
synthetic generator was verified; its three disposable verification outputs were removed.
Existing ignored artifacts/user exports are retained. Working tree: 13 modified tracked,
12 new, nothing staged, still on main at the Stage 6B baseline. Native acceptance and
separate Git authorization remain pending; automated/headless checks do not replace them.
That initial UI checkpoint used several async-void Headless Dispatch callbacks. The
refinement corrects them to awaited Task<bool> callbacks, strengthens their real UI
assertions and reruns all coverage; its final results supersede the initial UI evidence.

Final Stage 7 refinement verification on 2026-10-16 passed locked restore, a Release
solution build with zero warnings/errors, all 1,121 automated cases, all 103 focused
comparison cases (43 Core / 33 Workspace / 27 Desktop), and all 553 Workspace stress
cases including the existing 256/512 MiB checks. The refinement adds 37 ordinary cases.
Ten isolated Skia mode/theme/size/empty-state rendering checks passed and their synthetic
previews were inspected. The final full and stress suites ran sequentially. All eight
existing HL7 fixture blobs/hashes are unchanged; tracked/new whitespace, conflict and
secret-pattern checks passed across 32 files. Git remains on main at the same baseline,
16 modified tracked / 16 new files, nothing staged. The 10 ignored comparison-visual
PNGs are retained for review; no new disposable source/CSV files remain. Documentation,
ADR 0010 and the acceptance checklist describe the current modes, exact paste behavior,
limits and Preferences-only edit default. Native Windows acceptance and Git authorization
remain pending. Do not stage, commit, push, tag, release or publish.

The subsequent native default-view correction on 2026-10-16 initializes Presentation
to SideBySide once per ComparisonViewModel session. Existing Dock/reveal/reparent
operations retain the model's selected view; true close/reset/restart releases it and
reopens empty in Side-by-Side. No engine, paste limit, memory ceiling, layout schema or
dependency changes. The reported approximately 100 MiB message with an embedded Base64
PDF remains outside the 1 Mi UTF-16 paste limit; atomic rejection retains the prior draft.
Large embedded-payload comparison, streaming/hashing and ED-aware handling remain future work.
Locked restore and Release build passed (0 warnings/errors), all 1,124 solution cases
and 106 focused Stage 7 cases passed (43 Core / 33 Workspace / 30 Desktop), with 0
failures/skips. Ten isolated Skia checks passed; the updated empty Light/Dark previews
were inspected. Three new permanent cases add direct default-pane paste/typing and both
presentation choices through hide/reveal/float/resize/dock; existing tests additionally
assert independent scroll/caret, source/options/result/selection retention and fresh defaults.
The earlier 553-case stress result above is retained historical evidence; stress was not
rerun for this Desktop initializer correction. All eight HL7 fixture blobs remain unchanged;
whitespace/conflict checks passed across the same 32 unstaged files. Native acceptance
and separate Git authorization remain pending.

Stage 6B Advanced Search & Saved Searches has passed native Windows acceptance and is
accepted by the user. The user explicitly authorized one milestone commit on main from
Stage 6A baseline dbcd6284eabb01220046109a1f01f2e94626edcd and a normal push to origin/main,
with message `feat: add Stage 6B advanced search and saved searches`. Authorization covers
the accepted 44-file milestone only; no new implementation, history rewriting, tag,
release, installer or distribution package. Any earlier pending-acceptance/Git restrictions
are historical and superseded by this authorization. Stage 7 requires separate authorization.

Find Messages keeps its shared open/reveal/focus command and tool.search-results ID.
Simple retains Stage 6A matching and adds subcomponent paths. Advanced uses an ordered
ALL/ANY Boolean tree: 1–16 leaves, 16 groups and depth 4 including the fixed root.
Every group must be nonempty. Validation, deep snapshots and evaluation guards bound
work; children short-circuit in displayed order. Negative field comparisons require
presence and no matching value across any applicable segment/repetition. Segment targets
accept PV1 without a field; Field targets accept PV1-1 / PID-3.1 / PID-3.1.2.
The existing capture/scanner/session/paging/filter pipeline remains authoritative.
Core/source bytes, worker/packaging and Dock architecture are unchanged. Query edits
cancel/clear stale results and filters; collapse/expand only changes presentation.
Clear retains the authored query. No source values are added to result metadata/logs.

Search Library is separate local versioned JSON in the configuration root's Searches
folder (%LOCALAPPDATA%\HL7Workbench\Searches by default). Explicit Save/Update use a
nonmodal local unencrypted-storage disclosure, with no repeated confirmation or PHI
prohibition. Load never runs, Rename preserves the stored query, and Delete confirms.
Schema/query 2 stores ordered trees. Schema/query 1 flat definitions migrate in memory
without changing names/literals/semantics or rewriting on Load/List; explicit writes
use the original revision hash. GUID filenames, corrupt-entry isolation, bounded strict
schema, revision/lock checks and FileSaveDestination safe publication remain intact.
No history, results, document paths/contents, control IDs or ordinals are persisted.

Field Inspector Copy Path uses existing Core-backed node paths, supports subcomponents
and copies segment identifiers from the selected-segment heading. It omits unsupported
occurrence/repetition addressing, never values; stale/nonstructural nodes are disabled.
Preferences adds Appearance: Follow System (default), Light, Dark, using the existing
Avalonia application theme and preference file. Apply/OK changes all inherited windows
immediately; Cancel abandons unapplied changes. Default receives supported OS theme
notifications; older files default to Follow System. No separate theme system is added.
See message-search.md, ADR 0009, desktop-preferences.md and the Stage 6B acceptance
checklist for semantics, migration, storage, bounds and native verification.

Find Messages now has an internal horizontal splitter in Advanced, defaulting near
60/40 builder/results. Common commands stay outside both scrolling regions; minimums
shrink in short panes. Simple remains compact/automatic. Completed drag/keyboard gestures
save a version-1 SearchLayout fraction (.1–.9, default .6) in existing editor preferences;
continuous deltas/window resize/group expansion cause no writes or search invalidation.
The shared model retains the split through floating/hiding/reset, with no outer Dock
rewrite or tool identity/layout change and no query/results/document details persisted.

The HL7 editor now follows the effective theme too: a small immutable Hl7EditorPalette
resolves via Avalonia dynamic resources for both existing colorizers, structural/text
selections, caret, line numbers, current-line/omission colors and source/virtualized/full
source views. Theme changes redraw existing lines only, preserving draft/history/caret/
selection/scroll and source bytes, with no search/script/reload. Light uses a white editor
and dark syntax accents; Dark preserves the established canvas/accents. Semantic
panel/surface/group/border/accent/validation resources layer over Fluent for consistent
Light working surfaces and inherited floating/menu/dialog themes. Future custom palettes
can replace resources without rewriting renderers; no theme editor, profiles, import/
export, parallel framework, packages or new theme preference fields are implemented.
The C# Script Editor retains its independent Stage 5B VS Dark palette in this pass.

Historical correction verification passed 998 automated / 510 Workspace stress cases.
The current layout/theme pass adds 20 permanent cases: nine Workspace and eleven Desktop.
Fresh locked restore and Release build pass with 0 warnings/errors; 192 focused Workspace
and 88 focused Desktop cases pass. The full suite passes all 1,018 cases and fresh
Workspace stress passes all 519 cases, with no failures/skips. Whitespace/conflict review
passes across all 44 changed/new files; the synthetic fixture hash is unchanged. The
acceptance checklist records exact commands, palette/split behavior, the file inventory
and focused native retest. The user subsequently confirmed native Windows acceptance.
All permanent tests/fixtures and existing ignored artifacts are retained; no new disposable
probes/exports were created. Final pre-commit recovery confirmed main at dbcd628, exactly
23 modified tracked / 21 untracked additions, an empty index, and origin/main at the same
baseline. Source/tests remain unchanged from the verified build; final whitespace checks
and the fixture hash pass. The accepted suites are retained without an unnecessary rerun.
Only documentation acceptance records changed during finalization.

### Accepted Stage 6A checkpoint

Stage 6A message search/filter, including the final Find Messages UX cleanup, passed
native Windows acceptance and was explicitly accepted by the user on 2026-10-16.
The user authorized one milestone commit on main from the accepted/pushed Stage 5B
baseline 0497180b3799c5075f9fbc9dd6d778b48e68d279, followed by a normal push to origin/main.
Commit message: `feat: add Stage 6A message search and filtering`.
No additional features, source/test changes, history rewriting, force-push, tag, release,
installer or distribution package is authorized. Earlier pending-acceptance/Git statements
below record historical verification checkpoints and are superseded by this authorization.

Find Messages is a dockable/floating optional tool with Text / HL7 Field literal search,
case sensitivity, Contains/Equals, progress/cancel/clear and 200-row virtualized pages.
HL7 Tools → Find Messages, View → Find Messages and Ctrl+F share one open/reveal/focus
command; none executes a search. The redundant Focus Search ribbon entry is removed;
Clear Search / Restore Navigator and the tool's Search/Clear buttons remain. The original
`tool.search-results` ID preserves saved layouts and window recovery. Ctrl+F focuses the
value input from main/floating roots. No new top-level menu, settings or query language.
Field search uses Core's shared scanner over all matching segments/repetitions, respecting
MSH conventions, raw values, custom delimiters and malformed-but-readable input.

Workspace borrows the existing snapshot/index and immutable bounded draft strings;
scans run off the UI thread with one message's text/structure resident. A content-only
SearchRevision distinguishes edits from navigation; edits/undo/redo, switching, closure
or replacement cancel/clear stale work and filters. Disposal drains borrowed readers
before snapshot cleanup. Results retain bounded metadata only (no snippets/patient values)
up to the existing 100,000-message ceiling; headers are capped and control IDs remain
session-only. Filter Navigator restricts the existing file toolbar to original matching
ordinals without copying/removing messages or changing source/dirty state.

AGENTS.md now requires permanent regression-test/fixture/script retention and cleanup of
owned disposable artifacts only, preserving user files, useful failure diagnostics and
ignored build/publish outputs. See message-search.md, ADR 0008 and
stage6a-search-acceptance.md for semantics, bounds, known limits and exact native steps.

Stage 6A verification on 2026-10-16 passed locked restore, the Release solution build
(0 warnings/errors), 60 focused search/UI cases, all 862 automated cases and all 387
Workspace stress cases. Stress covers 50,000 result rows/paging, zero matches, in-flight
cancellation and a 32 MiB field. A test-only negative-GC-delta assertion was corrected
without relaxing either upper memory limit; focused, full and stress runs passed again.
Worker/package/isolation boundaries are unchanged, so published-worker checks were not
repeated. All 23 changed/new files were reviewed; whitespace/conflict checks and the
synthetic fixture hash passed. Existing local artifacts/build outputs/user CSV remain;
no Stage 6A disposable probes were created. At that checkpoint, main stayed at 0497180,
nothing staged. The user subsequently confirmed both functional and final Find Messages
UX native acceptance. Search/Workspace behavior is unchanged by that cleanup.

The final UX cleanup passed locked restore, a zero-warning/error Release rebuild, 33
focused Desktop cases (6 search + 27 shell/ribbon/editor) and all 862 automated cases
(0 failed/skipped). It retains the original layout ID, removes the redundant focus alias
and binds View/ribbon/Ctrl+F to the same open/focus command without automatic execution.
Tests use the production Dock theme/template to check actual revealed input focus and
verify existing results/filter state survives focus. Workspace source/tests are unchanged,
so the prior 387-case stress pass was retained without another stress run. Thirteen files
were updated within the existing 23-file Stage 6A working tree; whitespace/conflict and
fixture-hash checks passed. No new disposable artifacts, staged files or Git operations.

Final pre-commit review confirmed the expected 13 modified tracked / 10 new files and
empty index on main at the baseline. All 17 changed C#/markup files match the existing
verified Release portable-PDB source checksums (zero mismatches); the synthetic fixture
hash is unchanged. Source/tests were not edited and expensive checks were not rerun.
Only these existing milestone documents were updated to record final acceptance and
Git authorization; the accepted 23-file inventory remains intact.

### Accepted Stage 5B checkpoint and historical authorization

On 2026-10-16 the user confirmed final native Windows acceptance of Stage 5B, including
the safe overwrite and compact-dialog corrections, and explicitly authorized one
milestone commit and a normal push to existing origin/main from baseline 6dfa9f4.
Commit message: `feat: complete Stage 5B unified scripting and desktop tooling`.
No amend, rebase, force-push, tag or release is authorized. Stop if origin/main advanced.
The accepted verification remains the zero-warning/error Release build, 802 automated
cases, 333 Workspace stress cases, published-worker smoke/hashes/dependency isolation
and native Windows acceptance. Final review makes only acceptance-documentation,
generated-export ignore and fixture attribute changes; implementation/test code remains
the verified version.
The known synthetic-summary.csv native export stays local and ignored; the reviewed
synthetic HL7 fixture is included. Earlier pending-acceptance/Git restrictions below
describe historical checkpoints and are superseded by this explicit authorization.
Final review checked all 48 changed C# files against existing Release portable-PDB
source checksums (zero mismatches); MainWindow markup predates that verified build.
The published-worker checks passed again under Windows PowerShell 5.1. Expensive
automated/stress runs were not repeated because implementation/tests are unchanged.
The commit inventory is 64 reviewed files, with generated outputs excluded. A targeted
Git binary attribute preserves the synthetic fixture's protocol CR terminators and
exact reviewed bytes; its documented SHA-256 is unchanged.

### Earlier final save/dialog correction checkpoint

The user completed native Stage 5B functional checks for unified scripting, Library,
reports, ribbon, Preferences and document workflows. Two final acceptance corrections
remain for native retest: confirmed Save File replacement and compact confirmations.
All three picker workflows (shared ordinary/generated HL7 Save As, CSV export and exact
source export) now use one Desktop authorization adapter and Workspace FileSaveDestination.
The installed Avalonia 12.1.4 Windows backend was inspected: ShowOverwritePrompt=true
retains Windows' default overwrite consent, so no second application prompt is shown.
Other providers require application consent for existing local files. A path alone
allows creation only. Complete same-directory staged writes are flushed/closed, then
identity/metadata/hash and location are checked before File.Replace; collisions/changes
fail safely. Associated Save needs no repeat prompt. Existing streaming/byte/undo/script
contracts remain, with no worker/API or persistence changes. Native-confirmation and
final check-to-replace races are documented rather than claimed impossible.

Unsaved HL7/script, Delete Script and fallback Replace confirmations share a content-sized
420-pixel layout, wrapping, aligned buttons and safe Cancel/Escape/window-close behavior.
See `save-and-export-safety.md` for the full before/after inventory, provider evidence,
filesystem limits and exact native retest. The finishing checklist records verification.
Final locked restore/Release build passed with zero warnings/errors; 80 focused checks
and all 802 automated cases passed, zero failed/skipped (33 added). Refreshed folder
publish and full Windows PowerShell 5.1 worker/hash/dependency checks passed. No mocked
picker result is claimed as native confirmation/visual/DPI acceptance.
Workspace stress was rerun for the shared file-publication changes: all 333 passed.
Diff/whitespace/conflict checks passed across all 63 modified/untracked files. The
ignored investigation probe was removed; the synthetic fixture hash is unchanged.
Main stays at 6dfa9f4, with 28 modified tracked / 35 untracked and nothing staged.
Do not stage, commit, push, tag or release; explicit final acceptance and Git authorization
are still required. Earlier checkpoints below are historical.

### Earlier final-menu checkpoint

The user confirmed the corrected Stage 5B ribbon looks substantially better and switches
tabs with one click. The final menu cleanup leaves **File | Settings | View**, removing
Scripts and Window dropdowns. All ten former Scripts entries retain their shared command
instances in the Scripts ribbon; Open Script Editor was added there. Home → Workspace
retains Save/Restore Layout and now exposes Reset Layout / Recover Windows. All six tool
visibility commands stay in View. Scope, Review Output, Reports / Export CSV, keyboard
shortcuts and docked/floating editor controls are retained. Ribbon tabs, accepted styles,
theme bindings, input routing, pinning and persistence are unchanged. No runtime, library,
export, Dock, HL7 or schema implementation changes belong to this cleanup.

Final menu verification on 2026-10-15: locked restore passed; Release build passed with
zero warnings/errors; all ten focused menu/ribbon/shortcut cases and all 769 automated
cases passed (zero failed/skipped). The large-file stress suite was not rerun for this
menu-only change; its earlier successful result remains historical. See the complete
command inventory and remaining native steps in `stage5b-ui-finishing-acceptance.md`.
Stage 5B remains uncommitted on main; nothing may be staged, committed, pushed, tagged
or released without separate explicit authorization. Full native acceptance is pending.

### Earlier ribbon correction checkpoint

Initial native acceptance of the Stage 5B ribbon failed for light command surfaces in
dark mode, weak hierarchy/contrast and two-click tab switching. The authorized defect
correction keeps Popup.Child stable, passes overlay input only through the tab/pin strip,
and switches temporary content on the first click without dismissing it. The selected
tab toggles temporary visibility; outside/Escape dismissal and pinned preferences remain.
Dynamic Fluent resource bindings follow the window's dark/light theme. Compact vector
commands now form Script Library / Execution / Results groups with an accent Run tile;
Home/HL7 Tools use the same layout and networking stays disabled Planned placeholders.
The user subsequently confirmed improved appearance and single-click switching;
remaining native checks are tracked in the checklist and previews alone are not acceptance.
See the explicit ribbon retest in `stage5b-ui-finishing-acceptance.md`. No runtime, API,
library/settings persistence, export safety, Dock or source-byte behavior changes.

Ribbon correction verification on 2026-10-15: locked restore and Release build passed
with zero warnings/errors; seven focused ribbon checks and four isolated Skia rendering
cases passed, followed by all 768 automated cases (zero failed/skipped). Dark/light
pinned/overlay PNGs were inspected and remain in ignored artifacts/ribbon-visual/.
Publish and the complete worker/hash/dependency checks passed under Windows PowerShell
5.1. Diff/whitespace checks and all 51 modified/untracked files passed inspection.
Temporary investigation/comparison files were removed. Main stays at 6dfa9f4, with
23 modified tracked / 28 untracked and nothing staged. This predates the final menu cleanup.

Stage 5B's earlier UI/UX finishing pass added a permanent Scripts menu (now removed), a collapsed-
by-default utility ribbon (Home, Scripts, HL7 Tools, TCP/MLLP), and a separate General/HL7
Preferences window. Temporary commands overlay Dock without changing its viewport;
pin/unpin and Preferences share atomic per-user ribbon persistence. General also reuses
the existing new-document Edit Mode preference and Settings checkbox. Core has no target-
schema interpreter/validator: selected target and interpretation mode are reserved metadata,
distinct from each message's declared MSH-12; controls are disabled with an explanation.
No schema catalog, source mutation, conflict/conformance results or networking is added.

The ribbon/editor share the retained script model's UiCommands and operation gates.
Two compact editor rows contain library/name/New/Save/⋯ and scope/Run/Cancel/state.
Inline rename confirms a valid draft name; Save Script explicitly persists name/code.
Secondary actions include Duplicate, Rename, Delete, Review and Reports / Export CSV.
Delete only targets saved user entries with dirty consent, named confirmation and
lock/revision validation; canceled/failed deletion retains drafts, success selects the
simple bundled example. Generated HL7/CSV and report attribution remain independent.
Contextual reports preserve name/scope and block replacement throughout picker/export.
All unified runtime, containment, CSV and byte-preservation contracts below remain.
See `desktop-preferences.md` and `stage5b-ui-finishing-acceptance.md`. Native Windows
acceptance is pending; no commit/push/tag/release or staging is authorized.

UI finishing verification on 2026-10-15: locked restore and final Release build passed
with zero warnings/errors. All 762 automated cases passed (zero failed/skipped), adding
19 cases for defaults/recovery/forward fields, edit/schema separation, rename/delete,
command state, popup sizing/dismissal/keyboard behavior, Preferences and report retention.
Workspace stress passed all 320 cases, including 8,192 stateful messages, 4,096-message
streaming and the 256/512 MiB fixtures. Folder publish and the complete worker/hash/
dependency checks passed in Windows PowerShell 5.1 and PowerShell 7. A transient build
overlap with stress produced Windows copy-lock warnings; the final sequential build
and full tests passed cleanly. Diff/whitespace and all 50 modified/untracked file checks
passed. Main remains at 6dfa9f4 with 23 modified tracked / 27 untracked files and nothing
staged. The existing synthetic CSV export and earlier Stage 5B work are retained.

Stage 5B now uses unified C# scripting under ADR 0007. Every Run compiles once,
initializes once, calls required synchronous `void Main()` for each selected message
in source order, then optional `void Complete()` once on success. Roslyn syntax and
semantic validation discover entry points; one fixed host continuation binds callbacks
to the initialized submission, retaining shared variables without per-message compilation.
Current message and Whole document use identical source/lifecycle and fresh state per Run.
`Message` resolves only during Main; `Batch` supplies reports, Defer/Hold/Emit and ordinal
access, never a script kind. Existing bounds, lossless capture, working edits, privacy,
Windows worker containment and transactional HL7/report publication remain intact.

The editor has one New Script action and an Apply to: scope selector. Kind labels,
type changes and ScriptMode are removed. Last-successful report attribution captures
script name/scope only. Definition API/protocol are version 2; library schema 3 stores
identity/name/source and an obsolete-record warning flag, with no kind metadata.
Version-1/2 records retain their files/source, display Needs update and cannot Run until
the author rewrites and explicitly saves. Duplication preserves that warning. Settings,
docking, font/color behavior and immutable duplicable examples are preserved.

The unified redesign requires native Windows acceptance. No commit/push/tag/release
is authorized. See `script-api.md`, ADR 0007 and `stage5b-batch-scripting-acceptance.md`
for current contracts, verification and exact acceptance steps.

Unified final verification on 2026-10-15: locked restore passed; Release build had
zero warnings/errors; all 743 tests passed with no failures/skips. The 23 new unified
lifecycle cases cover shared state, helpers, fresh locals/runs, completion, signature
validation and original diagnostic locations. Obsolete kind-only workflows were
replaced while persistence, dirty-state, source preservation, worker safety, CSV,
attribution and tool coverage remain. Workspace stress passed all 306 cases, including
8,192 stateful messages, the 4,096-message stream and 256/512 MiB fixtures. Folder
publish and published one-/multi-message unified worker smoke passed, including CSV,
completion, Hold/Emit, clean exit, all worker file hashes and dependency isolation.
`git diff --check` and all 35 modified/untracked file checks passed. The temporary
ignored `artifacts/unified-probe/` investigation project was removed; permanent
lifecycle tests and publish smoke retain its coverage. The user's synthetic-summary.csv
was retained. Main remains at 6dfa9f4 with 19 modified tracked and 16 untracked files,
nothing staged and no commit/push/tag/release. Stop for native Windows acceptance.

## Historical Stage 5B split scripting and editor polish

Stage 5B stateful Batch scripting passed practical native Windows functional
acceptance. The user requested final Script Editor UX polish; its native Windows
retest is pending. Leave every change uncommitted; no commit/push/tag/release is
authorized. ADR 0006 extends the accepted 5A worker/facade, source capture, library
and dockable editor. Top-level Batch initialization registers synchronous Process
and Complete callbacks sharing captured locals in a fresh worker/job. Message scripts
retain their fresh-per-message behavior. Source working edits are captured, never
mutated. Deferred copies retain source ordinals for parent boundary/envelope checks.
Reports stream through bounded worker buffers into a Workspace snapshot journal;
only the complete validated HL7 output opens. The user then exports selected CSV
reports explicitly, through a flushed temporary file and no-replacement move.
Failures/cancellation clean private artifacts; export failures preserve completed
output and staged reports for retry. Last successful reports live in the editor
session until replacement or model disposal, with read leases for pending exports.
Version-1 scripts read as Message; atomic version-2 saves retain kind. The immutable
identity example keeps at most 10,000 raw PID-3 keys in worker memory, rejects missing/
ambiguous/diagnosed input and reports counts only. It is not complete de-identification.
No new dependencies, Core changes, source mutation or report viewer are introduced.
See `script-api.md`, ADR 0006 and `stage5b-batch-scripting-acceptance.md` for API,
bounds, transaction guarantees, verification and the pending native gate.

Stage 5B verification on 2026-10-15: locked restore and zero-warning/error Release
build passed; all 714 tests passed (211 Core, 302 Workspace, 129 Desktop, 72 Scripting;
78 added). Workspace stress passed 304 cases including 8,192 stateful messages and
repeated patient references, plus existing 256/512 MiB fixtures. Folder publish,
published Message/Batch worker smoke, worker Release/publish hashes and dependency
isolation passed. `git diff --check` and full tracked/untracked inspection passed.
The user subsequently accepted Stage 5B functionality and identified two final
editor UX issues. Creation now uses New → Message/Batch; type changes use the
secondary ⋯ → Change Script Type menu and preserve source. Library selection shows
the actual draft kind, including unsaved/type-changed drafts. Retained CSV reports
have session-only execution-time script name/kind/scope attribution, labeled Last
successful run with origin tooltips. Switching scripts, renaming, cancellation and
failure do not relabel prior reports. Runtime/API/CSV transactions are unchanged.
Final polish native Windows acceptance is pending; all changes remain uncommitted.

Final Script Editor UX polish verification on 2026-10-15: locked restore and a
zero-warning/error Release build passed; all 722 tests passed (211 Core, 304 Workspace,
135 Desktop, 72 Scripting; eight added). Focused editor/library checks passed 25/15
cases. Workspace stress passed 306 cases. Folder publish was refreshed and published
worker Message/Batch smoke, hashes and dependency isolation passed. No worker/API,
Core, CSV transaction, dependency or packaging changes were made by the polish pass.
`git diff --check` and all tracked/untracked file checks passed. The user's existing
synthetic-summary.csv native export was inspected and left untouched. Stop for final
native Windows UX acceptance; no commit/push/tag/release is authorized.

## Accepted Stage 5A baseline and historical checkpoints

The user authorized Stage 5A implementation only, following the revised Message/
Batch/Generate architecture. Stage 5A implements Message scripts: fresh Roslyn worker
per job, narrow Core facade, current/whole-document execution, captured unsaved edits,
bounded transactional independent output, local Script Library and dockable Script
Editor. The user has completed native Windows functional and visual acceptance,
including the final Script Editor refinements and tooltip discovery/dismissal fixes.
On 2026-10-15 the user authorized one Stage 5A milestone commit on `main` and a
normal push to `origin/main` in the existing private BobbyHanna/HL7Workbench repository.
No history rewrite, tag, release or Stage 5B implementation is authorized.
Subsequent native testing found stationary-pointer tooltip reopening in the normal
RAW editor. A shared dismissal guard prevented reopening, but subsequent native
testing found poor hover discoverability: exact matching of retained event pixels
conflicted with AvaloniaEdit's hover tolerance, and its timer restarted inside fields.
The current Desktop controller separates rendered-row acquisition, one normal 400 ms
deadline per structural field, and screen-coordinate click/scroll dismissal. It uses
the latest structural hit for the precise path and ignores obsolete AvaloniaEdit hover
events. Normal RAW, presentation and Full Source Viewer share this behavior. The user
confirmed native Windows acceptance; see `stage5a-native-tooltip-dismissal.md`.
The complete automated suite passes 636 tests (211 Core, 272 Workspace,
125 Desktop, 28 Scripting), with locked restore and a zero-warning/error Release build.
Workspace stress passes 274 cases. Folder publish, published-worker smoke,
Release/publish hash and dependency-isolation checks, and `git diff --check` pass.
Batch callbacks/state, synthetic identities, CSV, generation/templates, structural
insertion/removal, cross-document analysis, working-state application, IDE/debugging
and untrusted-code sandboxing remain deferred. See ADR 0005 and the Stage 5A checklist.
The worker executes trusted code with user permissions; it is not a security sandbox.

Stage 5A final automated verification on 2026-10-15: locked restore passed; Release
build passed with zero warnings/errors; all 594 tests passed (211 Core, 262 Workspace,
93 Desktop, 28 Scripting), including 67 new cases. Workspace stress passed all 264
cases with `HL7WORKBENCH_STRESS=1`, including the existing 256/512 MiB fixtures and
4,096-message scripting stream. Framework-dependent folder publish and its worker
compile/transform/finish/clean-exit smoke check passed. `git diff --check` passed.
The user subsequently confirmed completion of the native Windows functional
checklist in `docs/stage5a-scripting-acceptance.md`, identifying cramped Script
Editor layout and dark method names for this authorized UX polish pass.

Script Editor refinements keep runtime/Core/protocol/output transactions unchanged.
The centralized VS Dark-inspired palette now has a bounded compiler-only Roslyn
symbol overlay over the immediate AvaloniaEdit lexical fallback. Desktop gains
CSharp/Common 5.9.0 for background symbol binding (explicit amendment to worker-only
Roslyn loading); only ScriptHost uses the scripting runtime/executes code. Analysis
is debounced, cancellable, serialized, revision/document guarded and view-owned.
Resolved methods are light yellow, types teal; unresolved names/properties/delegate
variables stay neutral. No Workspaces/LSP/IDE infrastructure or source cache is added.
Font state uses integer points: new default/reset 12 pt, 1 pt steps, 9–24 pt limits.
V1 DIP preferences retain the nearest whole-point choice without rewriting on load;
subsequent changes atomically save V2 FontSizePoints. Footer displays e.g. `12 pt`.
Ctrl+wheel, Ctrl++/−, numpad Add/Subtract and Ctrl+0 work in code only. Two compact
scrollable toolbar rows and collapsed/absent diagnostics reserve code space. Existing
dock placement is retained. See `docs/stage5a-script-editor-visual-acceptance.md`.

Polish verification: locked restore and Release build passed with zero warnings/errors;
all 606 tests passed (211 Core, 267 Workspace, 100 Desktop, 28 Scripting), with 12 new
cases. Workspace stress passed all 269 cases. `git diff --check` passed. The complete
suite exposed an existing cleanup metadata race under concurrent snapshot disposal;
moving the lookup into its existing best-effort I/O guard fixes it without changing
snapshot ownership/publication. A concurrent-capture regression reproduced the original
failure and passes with the fix. Native visual acceptance was pending at that checkpoint.

Final point-size/contextual-color refinement verification: all 619 tests passed
(211 Core, 272 Workspace, 108 Desktop, 28 Scripting), including 13 new cases beyond
the 606-test polish pass. Workspace stress passed all 274 cases. Locked restore,
zero-warning/zero-error Release build, folder publish/dependency isolation checks
and `git diff --check` passed. Compiler-only CSharp/Common assemblies now ship/load
with Desktop for the requested symbol colors; scripting/execution assemblies remain
in ScriptHost. The user subsequently confirmed final native Windows acceptance.
Git actions were unauthorized at that checkpoint. The current authorization is recorded
above. See `stage5a-final-ci-investigation.md` for
the historical tooltip failure investigation, final verification and pending-file inventory.
That final pass reproduced a late-hover/click-dismissal defect in PresentationView
and added a view-local guard with two deterministic regressions. All 621 tests pass
(211 Core, 272 Workspace, 110 Desktop, 28 Scripting); Workspace stress passes 274.
Locked restore, zero-warning/error Release build, folder publish, deployed-worker
smoke/dependency-isolation checks and `git diff --check` pass. Historical attribution
remains qualified because the CI logs contain no pointer/timer trace.

Milestone 1 is complete: lossless, bounded Core parser with 211 regression tests.
Milestone 2's docking foundation is complete: synthetic placeholder documents,
Message Browser, Diagnostics, persistence, and floating tools. The user passed the
manual acceptance gate on 2026-10-13, including rapid tab switching, closing inactive,
active, and final documents, floating both tools, docking/refloating, main-window
shutdown with floating tools, layout restoration, and Reset Layout.
The user authorized Stage 1 of the read-only viewer. Implementation now opens
bounded local files in AvaloniaEdit document tabs with Core-based highlighting.
Stage 1 passed user manual acceptance on 2026-10-14 in all eight categories:
file opening, document tabs, syntax highlighting, read-only behavior, scrolling,
malformed messages, docking, and persistence and shutdown. The user authorized
Stage 2: Segment Navigator and Field Inspector. The user confirmed all 10 original
manual acceptance categories passed on 2026-10-14. Viewer Stage 2 is completed and
formally accepted on October 14, 2026, including delimiter-driven visible hierarchy,
raw double-click reverse navigation and RAW editor structural path tooltips.
All six final native Windows checks passed: raw hover; structural paths including
repetitions/subcomponents/custom delimiters; MSH numbering/edge cases; tooltip
dismissal and selection/caret preservation; double-click navigation; and copying,
tabs and docking regressions. The user authorized final verification and commit/push
of the accepted Stage 2 changes to main. Viewer Stage 3 implementation was subsequently
explicitly authorized, including large-message source-backed inspection. Stage 3 is
complete and explicitly accepted on October 14, 2026, including its UX refinements,
Windows fixes, Activity Log and final workspace layout. The user authorized milestone
verification and one local commit with the message
`feat: complete Stage 3 multi-message workspace and source inspection`.
No push, publication, tag or release is authorized. Stage 4's editable-document
foundation passed explicit user native Windows acceptance on 2026-10-15, including
the raw-editing crash and inspection-flicker fixes. The user authorized final
verification and one local commit with the message
`feat: complete Stage 4 editable HL7 document foundation`.
See `docs/decisions/0004-editable-document-foundation.md` for the architecture
assessment and `docs/stage4-editable-document-acceptance.md` for scope and native steps.
The clarified hover target is the upper RAW HL7 editor. Earlier inspector tooltip
investigations resulted from testing over raw text and did not establish an inspector bug.
Raw editor hover now uses AvaloniaEdit's hover events and glyph-floor coordinates
with scroll offsets, then Workspace's non-mutating ResolveOffset over cached Core
structural projections. A standard Avalonia tooltip shows only canonical path and
structural indices, without selection/caret changes, raw values, or semantic names.
Inspector tooltips, obsolete tooltip tests, the diagnostic Button/panel/probe and
F8 handling have been removed. Native Windows RAW editor hover acceptance is complete.
Stage 3 now uses immutable streamed file snapshots, a complete byte-oriented message
index, lazy active-message scanning, and bounded source access in the same file tab.
The consolidated UX refinement passed native acceptance: large
values are abbreviated through explicit source/display interval mappings, with
natural scrolling and a virtualized bounded-block fallback. Segment Navigator and
Field Inspector are now dockable tools sharing document-owned selection. Full Source
Viewer is optional and lazy, with contiguous source windows and exact original-
byte export. It retains explicit source context across document-tab switches;
same-file message navigation still invalidates it. The user chose to defer changing
that source-lifetime limitation during layout polish. Closing the tool or its owning
document releases the session. Version-2 layout JSON remembers placement without restoring source
content or instantiating the inspector. Version-1 layouts remain supported.
See `docs/viewer-stage3-ux-acceptance.md` for policy, lifecycle and native checks.
The first native Windows pass found a blank docked optional viewer and usability
issues; the subsequent fixes passed the user's native acceptance. Those fixes track Dock
Context replacement in cached templates, use consistent Full Source Viewer naming,
distinguish value/message scopes, hide unnecessary value-chunk controls and add
control tooltips. Old layouts introduce absent structural-tool identities without
overriding explicitly closed tools. See `docs/stage3-windows-acceptance-fixes.md`
for the reproduced root cause and regression evidence.
Activity Log now replaces the user-facing Diagnostics tool with a typed, privacy-
conscious application-session history, capped at 2,000 entries. It records meaningful
operations and real structured diagnostics without filenames, paths, source text or
exception messages. It is visible in fresh/reset workspaces, honors saved visibility,
releases its projection on close and preserves the shipped
`tool.diagnostics` placement identity. Status shows current operations and actual
warning/error counts; a clickable indicator opens retained activity. Parser/index
diagnostics remain intact; routine navigation does not add duplicate warnings.
See `docs/stage3-activity-log-acceptance.md` for lifecycle, privacy boundaries,
resource bounds and compatibility. The latest default arrangement is a 78% document
area on the left, independent Navigator/Field Inspector docks stacked in the 22%
right column, and Activity Log across the bottom (27% height). Message Browser is
hidden in new defaults but retained for saved layouts and explicit opening. Valid
saved layouts keep their groups, visibility, proportions and floating geometry.
See `docs/stage3-workspace-layout-acceptance.md` for the current native checklist,
layout policies and the explicitly deferred source-viewer lifetime discrepancy.
See `docs/viewer-stage3-acceptance.md` for current architecture, limits and manual steps,
and `docs/stage3-performance.md` for measured results.
See `docs/viewer-stage1-acceptance.md` for architecture, limits, and exact steps.
See `docs/viewer-stage2-acceptance.md` for current changes and manual acceptance.
Dock model tests use a fake host for native-window operations; supplemental monitor,
mixed-DPI, and OS-level behavior still benefit from platform-specific verification.
Milestone 2 / Viewer Stage 3 provide the accepted read-only baseline. Accepted Stage 4 adds
bounded raw editing, history, dirty state and document saving; scripting/SQL remain deferred.
Do not commit or push without user authorization.

## Projects and boundaries

- `src/HL7Workbench.Core`: dependency-free parser, paths, escapes, diagnostics,
  controlled edits, byte-preserving serializer and header/ACK semantics. No UI, files,
  network I/O or network framing.
- `src/HL7Workbench.Transport`: bounded incremental MLLP, TCP connections/listener,
  explicit deadlines and request/response exchange. No UI, Dock or persistent payloads.
- `src/HL7Workbench.Workspace`: document lifecycle/active context, lazy tool registry,
  bounded application-owned layout DTO and screen recovery policy. No Avalonia/Dock.
- `src/HL7Workbench.Desktop`: Avalonia shell. Dock types stay in `Docking/` and shell
  wiring; feature models in `ViewModels/` have no docking inheritance.
- `tests/HL7Workbench.{Core,Workspace,Desktop}.Tests`: synthetic regression and
  coordination tests. Dock tests use the real model with a fake host, not native GUI.

DocumentManager provides the shared active-message context. Active document changes
come from Dock active-tab changes; focusing a tool retains the selected document.
Closing a document selects another open document or clears context. Feature view models
unsubscribe on disposal. Future Script Editor/Library are tools with independent script
storage; generation, scripting, transport, Synthea, and IRIS services remain UI-free.
Layout files contain IDs/geometry only, never messages, filenames, or scripts.

## Viewer layout and future requirements

- Stage 2 implements the raw HL7 viewer above a lower-left Segment Navigator and
  lower-right Field Inspector, with adjustable internal splitters. Document-owned
  structural selection survives tab/view changes; it is not serialized in layouts.
  Stage 3's production shell promotes these same controls into independent Dock tools;
  the latest defaults stack them on the right above a full-width bottom Activity Log;
  the embedded-pane composition remains available for component regression tests.
- Inspection keeps canonical paths while suppressing implicit visual wrappers.
  Double-click raw text to reveal the deepest visible structural node. Hover RAW
  editor values for canonical coordinates only; inspector rows have no tooltips.
- Stage 3 combined multi-message files use a fixed toolbar at the bottom of the raw-message
  pane: first, previous, next, last, and message position/count.
- Prioritize combined single-file batches over directory-wide processing.
- Keep structural parsing version-independent and preserve future version/profile
  extensibility, with semantic definitions separate from parsing and rendering.

## Conventions and validation

.NET 10; SDK floor 10.0.401 with latestFeature roll-forward. Avalonia 12.1.4,
Dock 12.1.0.7, xUnit 4.0.2 / Microsoft Testing Platform. Central package versions
and committed lock files. Preserve compiled bindings and BuildServices exclusion.
In PowerShell set `DOTNET_CLI_TELEMETRY_OPTOUT`, `AVALONIA_TELEMETRY_OPTOUT`, and
`TESTINGPLATFORM_TELEMETRY_OPTOUT` to `1` before commands:

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln -c Release --no-restore
dotnet test HL7Workbench.sln -c Release --no-build --no-restore
git diff --check
```

Core's materialized parser still parses one bounded message at a time; its shared
range scanner also supports source-backed inspection without payload-sized strings.
Workspace owns original snapshots plus at most one cached decoded spool per document,
with no cached inactive structural models. Stage 4 additionally retains bounded
per-message working text/delta history: complete messages only (1 Mi UTF-16 units,
16 Ki per line), 32 MiB character-storage budget / 4,096 history entries per document.
Read-only is still the default; a separate persistent Start in Edit Mode preference
applies once at open. Edit Mode off retains dirty working state. Parsing is debounced,
version-checked and uses the existing global scan gate. Save streams snapshot bytes
plus changed payload overlays and publishes a flushed same-directory temporary file.
Original snapshot/index ranges remain immutable across Save/Save As; edited working
offsets are not original-byte coordinates. Original-source tools are unavailable for
edited messages until saved output is reopened. Close prompts preserve drafts on
Cancel, dismissal or save failure. Native Windows Stage 4 acceptance was explicitly
granted by the user on 2026-10-15.
Byte input preserves encoding/BOM;
MSH-18 does not select encoding. Messages are mutable/not thread-safe; reacquire
model snapshots after edits. Never use display text as a save source.

Initial Stage 4 automated verification on 2026-10-15: locked restore succeeded;
Release build succeeded with zero warnings/errors; the complete suite passed
518 tests (211 Core, 226 Workspace, 81 Desktop), including 40 new editing tests.
The Workspace stress run with `HL7WORKBENCH_STRESS=1` passed all 228 tests,
including the existing 256/512 MiB cases. `git diff --check` passed. A new 64 MiB
streaming-save test verifies unchanged prefix bytes and a sub-16 MiB allocation
budget. UTF-16 encoded framing collisions are checked with the existing indexer.
These automated/headless results did not replace native acceptance. At that point,
HEAD remained the Stage 3 baseline; no commit, amend, push, tag, publication or release
had been performed.

Native Stage 4 acceptance on 2026-10-15 found a critical raw-editing crash. It was
reproduced through real AvaloniaEdit routed input: `InvalidOperationException`,
"No undo group should be open at this point", from `UndoStack.ClearAll()` inside
`MessageInspectionView.WorkingTextChanged`. The fix configures zero native history
retention before input and removes transaction-time clears; Workspace history stays
authoritative. The three Stage 4 editing tests now use awaited `Task<bool>` UI
callbacks instead of accidentally selecting `async void`; a new routed-input/Dock
regression covers typing, deletion, paste, selection, history and fresh inspection.
See `docs/stage4-raw-editing-crash.md` for evidence and native retest steps.
Post-fix verification: locked restore passed; Release build had zero warnings/errors;
all 519 tests passed (211 Core, 226 Workspace, 82 Desktop). The Workspace stress
run passed all 228 tests, including 256/512 MiB cases. `git diff --check` passed.
At that point native acceptance and commit authorization were still pending.

The subsequent native editing UX fix preserves stable inspection tools. Workspace
now separates Current/Pending/Stale/Empty/Unavailable and retains one labeled,
non-current bounded inspection snapshot while parsing. `ActiveInspection` remains
current-only; old snapshot resolution/selection and offset-dependent UI actions are
blocked. Pending/Stale controls retain enabled styling; event guards reject stale
structural input while a fixed-height label identifies previous inspection data.
Dock/embedded panes stay mounted and rebind refreshed projections with
canonical selection, realized expansion and viewport restoration where possible.
Tolerant malformed text remains inspectable; empty text has its own state. Existing
debounce/limits, source-backed lifetimes, undo-group safety and dirty/Edit Mode
ownership remain unchanged. See `docs/stage4-inspection-continuity.md` for the event
trace, state contract, tests and native retest. The user subsequently accepted these
fixes as part of native Windows Stage 4 acceptance.
Final milestone verification on 2026-10-15: locked restore passed; Release build had
zero warnings/errors; all 527 tests passed (211 Core, 230 Workspace, 86 Desktop),
including eight continuity cases and extended native-input regression assertions.
All 232 Workspace stress tests passed, including 256/512 MiB cases; `git diff --check`
passed. The user authorized the final verification and one local Stage 4 milestone
commit. Push, amend, tag, publication and release remain unauthorized.

## Important documentation

- `HL7_Workbench_Specification.md`: product vision; milestone amendments govern scope.
- `docs/architecture.md`, `docs/decisions/`: architecture and accepted decisions.
- `docs/stage9-deidentification-acceptance.md`: field coverage, privacy limitations, verification and native checks.
- `docs/branding.md`: approved artwork, reproducible PNG/ICO derivatives and native icon checks.
- `docs/parser-api.md`, `docs/hl7-paths.md`: preservation, limits, paths, and edit rules.
- `docs/viewer-stage3-acceptance.md`, `docs/stage3-performance.md`: source-backed inspection and measurements.
- `docs/stage3-activity-log-acceptance.md`: Activity Log privacy, history and lifecycle.
- `docs/stage3-workspace-layout-acceptance.md`: accepted final layout, source-lifetime limitation and milestone verification.
- `docs/dependencies.md`: versions/licenses.
- `docs/decisions/0004-editable-document-foundation.md`: Stage 4 assessment and working-state/save contracts.
- `docs/stage4-editable-document-acceptance.md`: implemented editing, deferred scope and native Windows checklist.
- `docs/stage4-raw-editing-crash.md`: captured editor exception, fix, regression and native retest.
- `docs/stage4-inspection-continuity.md`: stable inspection snapshots, stale-action guards and native retest.
- `docs/docking-acceptance.md`: passed docking gate, supplemental checklist, and limitations.
- `README.md`: setup/run commands; `samples/synthetic-only/`: synthetic fixtures.
