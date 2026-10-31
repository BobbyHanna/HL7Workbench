# Stage 20C — Bulk fields and unique values

## Baseline and design

Stage 20B is committed at `a0f486d428327a9b864575c98dff8e451efb1784`.
Its [Build and test run](https://github.com/BobbyHanna/HL7Workbench/actions/runs/41207819267)
completed successfully before implementation. The working tree was clean.

The existing inspector routes three actions to one native review dialog, not a new
dockable tool. Scope is explicitly Current message, Active file, or the complete
current navigator filter. Capture freezes message IDs, source revision and filter
identity. A selected whole field includes its repetitions; an indexed repetition,
component or subcomponent stays at that exact level. Segment occurrence is fixed
by default, with an explicit all-occurrences option. Matching is ordinal exact raw
encoded equality; empty, absent and explicit null remain distinct.

Workspace reuses its validated complete-source capture, Core structural scanner,
Core SetRaw validation, Core byte splicing, existing EditableMessageBuffer cost
accounting, MessageSavePlan and SourceSnapshot. No SQL writes, scripts, new parser or alternative serialization
engine are used. Analysis retains bounded complete distinct values and stable IDs,
not complete decoded messages. Preview retains bounded change details and bounded
message overlays. Sorting/paging never changes the authoritative match set.

Mutation is all-or-nothing over the validated proposals, after explicit preview and
confirmation. Absent targets are skipped and identical assignments are unchanged.
Invalid proposals block Apply. Output streams the entire captured dataset, including
messages outside the scope, wrappers, BOM and untouched bytes. Index/byte proofs and
freshness checks precede the single independent-document publication. Source text,
Edit Mode, dirty state and undo history remain unchanged. Explicit Save As uses the
existing save workflow. Cancellation/failure publishes nothing and disposes staging.

Results are tied to their original document/revision, never the currently active tab.
Filter scope additionally requires the same captured filter. Closing/changing the
source invalidates results. Unique-value navigation/filtering uses existing stable
message IDs and navigator filtering; SQL matching-message output can be selected as
an independent source dataset, and the existing extraction tool remains the CSV path.

## Counting, matching and limits

Grouping uses ordinal complete raw strings plus Hl7ValueState. A whole-field selector
includes its repetitions as one raw value; explicitly selected nested indexes stay
fixed. All-occurrences expands only segment occurrence. With no matching segment,
there is one absent address slot for that message; missing fields/children in existing
occurrences each contribute an absent slot. Absent occurrences have zero present
nodes. Each bucket separately counts selected-slot occurrences, actual present nodes
and distinct message IDs. Summaries distinguish present nodes, selected slots, absent,
skipped/unreadable, unchanged, invalid and proposed targets. Unavailable coordinates
never masquerade as an absent or empty value. Frequency sorting includes absent slots.

Search & Replace uses case-sensitive complete raw equality; no substring, regex,
decoded matching or coercion is offered. Assign skips existing equal values and never
creates structure. Core validates actual custom separators/escapes and strict encoding;
the shared Stage 20B preflight rejects controls/line separators and long replacements.
MSH-1/MSH-2 mutations are unavailable. Original spans are checked for overlap and must
produce exactly the expected splice. Individual selection/exclusion is deferred:
there are no ignored selection checkboxes. Any invalid proposal blocks the whole Apply.

Hard options allow at most 1 GiB each captured source/output, 100,000 scoped messages,
250,000 selected slots/preview rows, 4,096 distinct values, 65,536 characters per
complete analyzed value and 2,097,152 retained value characters. Mutation retains at
most 16,777,216 reserved overlay characters (32 MiB character data), using existing
1 Mi complete-message/16 Ki line/replacement bounds. Metadata, parsed/scanner models,
encoded originals and snapshots are additional bounded costs. Results have 100-row
pages and 256-character previews; complete details remain inspectable. Analysis never
silently truncates/group-abbreviates. Uneditable proposals block Apply. Large untouched
messages stream unchanged; modifying an oversized selected message remains unsupported.

Whole-source private capture has a disk/time cost even for current-message/read-only
analysis. It includes current drafts/insertion order under the existing capture
preflight, so an unrepresentable unrelated draft can prevent complete capture rather
than disappear from output. Pending output is reindexed and byte-proven through the
same MessageSavePlan verifier as Save, with projected membership/framing/gaps and
encoding inherited from the capture. Opaque regions, batch wrappers, BOM and untouched
bytes remain exact. No independent capture lease pins the original document after
analysis. Results cancel/defer snapshot cleanup until active Apply drains; the shell
awaits modal operations and result Disposal. Opened output owns its own snapshot.

The only narrow Core extension exposes the existing scanner's message/header safety
flag separately from editing-encoding support. CanEdit and parsing remain unchanged.
This permits safe read-only UTF-32 analysis while rejecting its mutation; ambiguity
is not inferred from possibly suppressed diagnostic text. Shared save byte verification
now accepts either its original staged file or a sealed snapshot read delegate.

## Initial verification (before native acceptance fix)

Both normal and isolated locked restores pass. The Release build reports
zero warnings and zero errors. Focused results on the final implementation are:

| Run | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Core scanner safety/scanner/path/edit/hardening | 93 | 0 | 0 |
| Workspace bulk/single-edit/save/SQL | 156 | 0 | 0 |
| Desktop bulk/single-edit/wheel/inspector/SQL | 59 | 0 | 0 |
| Desktop completed Help bundle/view | 23 | 0 | 0 |
| Focused total | 331 | 0 | 0 |
| Completed-build full regular solution | 2,898 | 0 | 14 |
| Documentation compiler | 12 | 0 | 0 |

There are 73 new permanent ordinary cases: 3 Core, 61 Workspace and 9 Desktop.
Existing tests, synthetic fixtures and stress opt-ins are preserved. Coverage includes
fixed/all segment occurrences, field/repetition/component/subcomponent targeting,
empty/absent/null counts, full values with identical previews, 250-message filtered
scope across pages, original drafts/insertions, batch/MLLP/opaque bytes, UTF-8 and
UTF-16 LE/BE BOM, strict Latin-1, read-only UTF-32, overlap, stale edits/filter/close,
pre-canceled work, gated cleanup, output/budget failures and retry. Native-control
tests use actual docked/floating routing, review/confirmation/publication, navigation
filtering, focus, pointer zoom and narrow/maximized/large-font Dark layouts.

Successful publication leaves changed-message/replacement/unchanged/skipped/absent
counts visible in the shell after the preview closes. Core's individually spliced
serialized bytes supply each changed payload; a permanent single-byte encoding
alias regression proves intervening untouched bytes survive multiple edits in one
message. The shared save plan accepts these validated bounded payloads while its
existing original-buffer path remains unchanged. The full-run results below refer
to the completed bundle; earlier complete passes remain available as evidence.
The completed-build regular suite contains 2,912 cases, takes 4m 25s 085ms, and
passes all five test projects. Its 14 skipped identities match the Stage 20B
opt-in SQL stress/performance baseline exactly; no exclusions were broadened.

Documentation catalog generation/check, generated Script API consistency and
`python -B -m unittest build/test_documentation_catalog.py` pass. Link/anchor validation
covers 154 Markdown files, 1,078 local links and 42 heading anchors with zero errors.
Whitespace checks include all changed/new text files. No generated API, lock file,
dependency or broad line-ending policy change is made.

The Help bundle was rebuilt after clarifying that bulk-review content explicitly
shares inspector zoom while ordinary modal configuration dialogs remain excluded.
Its 14 DocumentationTests and 9 DocumentationViewTests pass on the completed build.
An unsupported multi-wildcard MTP invocation selected zero cases; it was corrected
to these two single-class runs, with its diagnostic retained separately.

Reproducible .NET commands (with telemetry opt-outs) are:

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet restore HL7Workbench.sln --locked-mode --artifacts-path C:/Dev/HL7Workbench/artifacts/stage20c/restore
dotnet build HL7Workbench.sln --configuration Release --no-restore
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
```

Focused commands use one `--filter-class` pattern per `dotnet test --project` invocation
with Release/no-build/no-restore. Ignored `artifacts/stage20c/` retains exact build,
restore/focused/full/documentation logs, file/input inventories, link/hash audit and
isolated restore outputs. These are review evidence, not packaged releases. No
owned scratch HL7/CSV export or disposable probe remains. Native Windows acceptance
is pending. No commit, push, package or publication was performed.

Final SHA-256 audit against `artifacts/stage17/protected-before.json` confirms all
2,384 protected private-preview files remain present and unchanged, with zero
changed, missing or added files. All 534 captured runtime/build/test input hashes
match the completed build. Git HEAD remains the authorized Stage 20B commit and
the index remains empty. That initial inventory contained 32 files; the acceptance
fix adds one permanent test file. The current Stage 20C inventory is below.

## Native Windows acceptance checklist — pending

Use synthetic fixtures only, in Light/Dark and representative DPI settings.

1. Open a synthetic multi-message file, select PV1-2, and List Unique Values over
   Active file. Verify raw values, empty/absent/null, counts and complete details.
2. Use repeated PID segments and PID-3 repetitions. Compare fixed occurrence versus
   Every occurrence, whole-field grouping versus one repetition, and PID-5.2 grouping.
3. Sort both directions by value/frequency, page results, select a value, navigate,
   and Filter Navigator. Verify all matching IDs, including those beyond a page.
4. Search & Replace PID-5.2 with exact raw input. Verify substring/case differences
   do not match and selected occurrence/repetition/siblings remain distinct.
5. Review source/scope/revision, all summary counts, message identities, paged proposals
   and complete selected values. Verify Apply stays disabled until confirmation.
6. Cancel a staged operation and close the review. Verify source bytes/text/history/
   dirty state/Edit Mode and document membership remain unchanged.
7. Apply a current navigator-filter subset. Verify output retains the complete source
   dataset and order, with only selected messages changed. Clear/replace the filter
   during a preview and verify stale Apply is refused.
8. Assign PV1-2 across selected messages. Verify no-ops, empty and absent targets are
   reported correctly, with no missing structure manufactured.
9. Explicitly Save As and reopen Bulk Result.hl7. Compare original source, untouched
   messages, BOM/encoding, custom delimiters, mixed terminators and batch/MLLP wrappers.
10. Edit/close the source during work; change parameters after preview; switch tabs.
    Verify invalidation/cancellation, fixed source ownership, retry and no partial output.
11. Try malformed/ambiguous input, unsupported encoding, unsafe separators/escapes,
    controls and oversized replacements. Verify honest coverage and blocking failures.
12. Use a large source with small in-scope messages and a large untouched message.
    Cancel capture/analysis/output; verify responsiveness and bounded temporary cleanup.
13. Open SQL Matching Messages as a new dataset, bulk-process it, then query/extract
    the independent result. Verify the original SQL sources remain unchanged.
14. Float/redock Field Inspector and open each action. Check originating modal owner,
    narrow/maximized layouts, large content fonts, Ctrl+Wheel and keyboard focus/order.
15. Review Light/Dark, 100/150/200% DPI/mixed monitors, screen-reader labels and long
    values. Headless control tests do not establish native acceptance.

## Remaining limitations

No per-row selective application, direct SQL-row scope, distinct-table CSV exporter,
substring/regex/decoded matching, absent-node creation, bulk changes to the original
document, or large-message edit model is introduced. Use existing extraction CSV and
explicit SQL matching-message output where appropriate. Existing unsafe source
boundary/encoding/capture constraints remain enforced. Native acceptance is pending.

## Acceptance fix — visible operation inputs

Native Windows acceptance found Search & Replace's inputs missing from view. They
were created with the correct operation-specific labels/controls, and Analyze read
Find.Text/Replacement.Text directly; there was no implicit selected-value search or
Assign-only execution layout. However, the common parameter ScrollViewer was capped
at 120 DIP. Path/explanation/scope/occurrence controls consumed that viewport,
leaving Find/Replacement below its clip. Assign Value shared the same defect.
The previous layout tests checked result bounds rather than input clipping.

Eight new geometric native-control cases reproduced the problem before the fix
at normal/increased fonts and narrow/wide viewports. They now verify complete
on-screen bounds, all ancestor clips, labels and input order before Analyze.

The review now shows compact source/path context, explicit editable Find value and
Replace with for Search & Replace, and New value for Assign. Both replacements start
empty and intentionally empty input clears existing values. Find starts with the
complete selected raw value when safely available within 65,536 characters. Small
materialized nodes use their actual raw value; source-backed reads reuse the bounded
context reader and reject stale context before opening. A missing/oversized value
leaves an editable blank input, never a truncated or hidden implicit term.

ResponsiveToolSurface supplies one whole-review overflow region and a finite
120-DIP results minimum. Essential fields are not behind a short parameter scroller;
commands retain native wrapping/tab order. Unique Values uses the same corrected
composition, keeps scope/occurrence visible, and has no mutation inputs. Existing
zoom checks now reveal the results viewport before sending a pointer gesture,
and check its actual visible bounds. No assertion about source protection was removed.

Analyze captures the actual entered text, including empty replacement. Apply uses
only the resulting reviewed immutable request. Editing either search input or an
assignment discards preview/confirmation and requires reanalysis. Core validation,
copy-on-write byte publication, source history/state, cancellation and stale-source
checks are unchanged. The fix adds 17 permanent Desktop cases; the bulk native
class now has 26 cases, including real text-input events, raw escapes, empty values,
confirmation, invalidation and all three dialog modes.

Both locked restores and the completed Release build pass with zero warnings/errors.
Final acceptance-fix checks are:

| Run | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Focused Core | 54 | 0 | 0 |
| Focused Workspace | 110 | 0 | 0 |
| Focused Desktop | 87 | 0 | 0 |
| Focused total | 251 | 0 | 0 |
| Full regular solution | 2,915 | 0 | 14 |
| Documentation compiler | 12 | 0 | 0 |

The full run has 2,929 cases and takes 4m 25s 336ms. All 14 skip identities match
initial Stage 20C's opt-in SQL cases. Offline Help generation/check, generated
Script API consistency, 154 Markdown files / 1,078 local links / 42 heading-anchor
references, whitespace and all 2,384 original protected SHA-256 hashes pass. There
are zero changed/missing/added protected files. All 535 tested source/build/test
input hashes match the final run. No Core/Workspace engine files changed relative
to the pre-fix Stage 20C state.

Ten files changed for this acceptance fix (nine existing plus one new permanent
test file), with the original Stage 20C work preserved:

```text
docs/PROJECT_CONTEXT.md
docs/developer/desktop-tools.md
docs/stage20c-bulk-field-operations.md
docs/user/inspect-and-edit.md
docs/user/troubleshooting.md
src/HL7Workbench.Desktop/Documentation/catalog.json
src/HL7Workbench.Desktop/MainWindow.Inspection.cs
src/HL7Workbench.Desktop/Viewer/BulkFieldWindow.cs
tests/HL7Workbench.Desktop.Tests/BulkFieldViewTests.Inputs.cs
tests/HL7Workbench.Desktop.Tests/BulkFieldViewTests.cs
```

Ignored `artifacts/stage20c-acceptance-inputs/` retains the eight-failure reproduction,
pre-fix file inventory, completed build/restore/focused/full/documentation logs,
tested input hashes and link/protected-file audits. No disposable probes or HL7/CSV
exports were created. Native Windows reacceptance remains pending.
No commit, push, package or publication. Stop for native Windows reacceptance.

### Native reacceptance checklist — pending

1. Select synthetic PID-5.2 and open Search & Replace. Find value must visibly show
   the selected raw value; Replace with must be visible and empty before Analyze.
2. Enter a different Find value and replacement. Preview must use those exact inputs,
   not the originally selected value. Review scopes/counts and keep source unchanged.
3. Leave Replace with empty, preview, confirm and apply. Reopen the independent
   result and verify the existing target clears while original bytes/history remain.
4. Edit Find or Replace with after preview. Apply/confirmation must reset; rerun
   analysis and verify only the newly reviewed request can publish.
5. Open Assign Value. New value must be visible before Analyze, including when empty.
   Verify both populated assignment and intentional clearing after confirmation.
6. Open Unique Values. Scope/occurrence remain visible with no mutation inputs.
7. Repeat at 14/24/32 DIP, narrow/wide/maximized sizes, Light/Dark and floating/docked
   inspectors. Check labels, focus/tab order, scrolling and result access. Automated
   control tests do not establish this native reacceptance pass.

## Acceptance blocker — result identity and workspace publication

Native acceptance of a synthetic 30-message Avery → Morgan replacement showed
Morgan in Inspector, Avery in the raw viewer, and no result tab. Investigation
confirmed that Apply had produced a complete independent snapshot, then registered
and activated it in DocumentManager. Inspector follows that manager identity.
FileDocumentView instead belongs to an existing Dock document wrapper. The bulk
workflow omitted WorkspaceDockFactory.OpenDocument, so no wrapper/tab was created
and the raw viewer continued displaying the original document. The Inspector was
showing the real result, not a proposed value or modified source. No output-writing
exception was needed to cause the mismatch.

Four new real-shell regression cases reproduced that precise failure before the
fix: 26 existing cases passed, all four failed at the missing result Dock wrapper.
The fix passes the existing Dock opener through a UI-independent publication
callback. Apply selects the first changed output message and exact reviewed path,
registers the complete result, opens/activates its tab, and only then reports success.
The generated Bulk Result.hl7 title remains distinct and unsaved. Scope never drops
out-of-scope messages; no source bytes, history, dirty state or edit mode are changed.

The investigation also found incomplete rollback if a registration/presentation
observer throws. OpenDocument now removes only its newly added wrapper, locator
and title subscription and restores the prior tab. Workspace unregisters the exact
pending document, restores the previous manager identity, and awaits owned cleanup.
The review reports a clear result-opening failure and requires reconfirmation for
retry. Already successful Apply cannot publish again. Busy/closed click guards also
retain the original Completion task across repeated Apply events.

Eight permanent Desktop cases cover full/current scope, complete 30-message byte
equality, untouched NTE and second-PID values, visible raw/Inspector agreement,
switching back to the clean original, first changed message 17, repeated Apply,
post-tab-insertion presentation failure and explicit retry, in docked/floating
inspectors. Three Workspace cases inject registration, activation and presentation
failures and assert withdrawal, drained cleanup, exact bytes and one successful retry.
The prior input-visibility/empty-replacement/cancellation/stale-preview tests remain.

Both locked restores and the Release build pass, with zero warnings/errors.
Focused checks on the completed build pass 57 Core, 113 Workspace and 125 Desktop
cases (295 total), with zero failures/skips. Documentation compiler tests pass all
12; regenerated offline Help, generated Script API consistency, 154 Markdown files /
1,078 local links / 42 heading anchors, and whitespace checks pass. All 2,384 original
protected hashes match, with no changed/missing/added files.

The full regular suite passes 2,926, fails zero and skips the same 14 expected opt-in
SQL cases (2,940 total; 4m 27s 069ms). All 537 captured runtime/build/test input
hashes match the tested build. Compared with the prior acceptance fix, the 11 added
regression cases account for the entire count increase; no tests were removed.
Evidence is retained in ignored artifacts/stage20c-acceptance-publication/, including the four-failure
reproduction, pre-fix file hashes, completed build/restore/focused/documentation logs,
tested input hashes and link/protected audits. No disposable probes or HL7/CSV
exports were created. Native reacceptance is pending; headless UI tests do not
establish native Windows acceptance. No commit, push, package or publication.

This blocker fix changes 15 files relative to the preserved input-visibility fix
(14 existing files and one new permanent test file):

```text
docs/PROJECT_CONTEXT.md
docs/developer/desktop-tools.md
docs/developer/workspace-lifecycle.md
docs/stage20c-bulk-field-operations.md
docs/user/inspect-and-edit.md
docs/user/troubleshooting.md
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/Documentation/catalog.json
src/HL7Workbench.Desktop/MainWindow.Inspection.cs
src/HL7Workbench.Desktop/Viewer/BulkFieldWindow.cs
src/HL7Workbench.Workspace/BulkFieldModels.cs
src/HL7Workbench.Workspace/BulkFieldResults.cs
tests/HL7Workbench.Desktop.Tests/BulkFieldViewTests.Publication.cs
tests/HL7Workbench.Desktop.Tests/BulkFieldViewTests.cs
tests/HL7Workbench.Workspace.Tests/BulkFieldOperationsTests.cs
```

### Native publication reacceptance — pending

1. Open a synthetic 30-message batch with PID-5.2 = Avery. Select that component
   in Inspector, open Search & Replace, keep Active file and the selected PID
   occurrence, and enter Morgan. Analyze must propose 30 changes while both original
   views still show Avery and the source remains clean. Review and confirm.
2. Apply must open exactly one separate Bulk Result.hl7 tab and activate it. Raw
   viewer and Inspector must both show Morgan at the selected path. Navigate all
   30 messages; unrelated PID occurrences, fields and NTE Avery values must remain.
3. Switch to the original tab: both views must show Avery, with no dirty marker or
   added undo entry. Return to the result: both must again show Morgan. Save As only
   when intentionally exporting; no file write is needed for this acceptance check.
4. Repeat Current message on message 17. The new result must open at message 17,
   retain all 30 messages and change only that target. A rapid repeated Apply must
   not produce extra documents. Before confirmation, close/cancel must publish none.
5. Repeat with Inspector docked and floating, narrow/maximized windows, larger fonts
   and Light/Dark themes. Check originating modal ownership, result activation and
   source/result switching. Automated injected failures cover rollback/retry; report
   any real result-opening error with its visible message and workspace state.

## Changed files

24 tracked updates and 11 new files (35 total), including both acceptance fixes:

```text
docs/PROJECT_CONTEXT.md
docs/desktop-preferences.md
docs/developer/desktop-tools.md
docs/developer/parser-invariants.md
docs/developer/security-and-data-handling.md
docs/developer/workspace-lifecycle.md
docs/help-topics.json
docs/reference/limits.md
docs/reference/storage-and-formats.md
docs/stage20c-bulk-field-operations.md
docs/user/extract-fields.md
docs/user/inspect-and-edit.md
docs/user/preferences-and-shortcuts.md
docs/user/privacy-and-storage.md
docs/user/query-with-sql.md
docs/user/search-and-filter.md
docs/user/troubleshooting.md
src/HL7Workbench.Core/Hl7Structure.cs
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/Documentation/catalog.json
src/HL7Workbench.Desktop/MainWindow.Inspection.cs
src/HL7Workbench.Desktop/Viewer/BulkFieldWindow.cs
src/HL7Workbench.Desktop/Viewer/InspectionPanes.cs
src/HL7Workbench.Workspace/BulkFieldModels.cs
src/HL7Workbench.Workspace/BulkFieldOperations.cs
src/HL7Workbench.Workspace/BulkFieldResults.cs
src/HL7Workbench.Workspace/InspectorProjection.cs
src/HL7Workbench.Workspace/MessageSavePlan.cs
src/HL7Workbench.Workspace/StructuredRawValue.cs
src/HL7Workbench.Workspace/StructuredValueEditSession.cs
tests/HL7Workbench.Core.Tests/StructureSafetyTests.cs
tests/HL7Workbench.Desktop.Tests/BulkFieldViewTests.Inputs.cs
tests/HL7Workbench.Desktop.Tests/BulkFieldViewTests.Publication.cs
tests/HL7Workbench.Desktop.Tests/BulkFieldViewTests.cs
tests/HL7Workbench.Workspace.Tests/BulkFieldOperationsTests.cs
```
