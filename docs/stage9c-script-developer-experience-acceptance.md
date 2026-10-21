# Stage 9C — Script Developer Experience

## Native acceptance and checkpoint authorization (2026-10-21)

The user confirmed successful native Windows acceptance of Stage 9C. The exact
28-path inventory and accepted implementation hashes match the prior final record.
Earlier pending/no-commit statements below are historical. One local checkpoint is
authorized after a fresh complete gate, with message
`feat: add Stage 9C scripting API reference and templates`, parent
ea897decdbd79d067d13552e972ab456885a61d6. The excluded _TEST.hl7 remains unchanged
and untracked. After checkpoint verification succeeds, Stage 10 extraction/analysis
is authorized and must remain unstaged/uncommitted for its own native acceptance.
No push, tag, package, deploy, release or publication is authorized.

Fresh checkpoint gate passed on 2026-10-21: locked restore; Release zero warnings/
errors (1.72s); **1,544 solution tests** (2m 49.456s); **733 complete Workspace stress
tests** (5m 04.044s), no failures/skips; both ScriptHost lifecycles against the existing
Release layout; generated-reference synchronization; eight exact fixture blobs,
all 13 protected hashes and whitespace. Original artwork and excluded _TEST.hl7
are unchanged. Only this record and PROJECT_CONTEXT changed since the accepted
manifest. Evidence is retained under ignored artifacts/stage9c-checkpoint-20261021.

Implemented under the sequential Stage 9 checkpoint/Stage 9C authorization.
**Native Windows acceptance is pending. Stage 9C remains unstaged and uncommitted.**
No push, tag, package, deploy, release, publication or Stage 10 is authorized.

## Accepted Stage 9 checkpoint

Local commit: `ea897decdbd79d067d13552e972ab456885a61d6`.
Message: `feat: add Stage 9 de-identification and application branding`.
Parent: `5eebc767eae5f241ac4c7a02c4275ae0305b76fa`.
It contains exactly the accepted 41 Stage 9/branding paths. Post-commit tracked tree
and index were clean; the excluded user HL7 was the sole untracked file.

Fresh checkpoint gate passed: locked restore, Release zero warnings/errors,
1,504 solution tests (2m 34.383s), 718 complete Workspace stress tests (4m 37.853s),
both real ScriptHost smoke lifecycles, four icon-processing tests, eight exact
fixture blobs, all 13 protected hashes and whitespace. No failures/skips. Evidence:
ignored artifacts/stage9-checkpoint. Original artwork and excluded HL7 are unchanged.

## Implementation and API audit

- One versioned source, docs/script-api-reference.json (reference 1, scripting API 2),
  supplies 50 in-app topics and six complete starter examples. Workspace embeds its
  exact bytes. build/WriteScriptApiReference.py generates the readable repository
  reference; `--check` rejects drift. Application help needs no Python or network.
- All 143 supported members across 22 authoring types are explicitly classified against exported types in
  HL7Workbench.Scripting. Declared public constructors, properties, methods, enum
  values and constants have signatures, descriptions, parameters/returns where
  applicable, complete examples and limitations. Reflection coverage preserves
  overload counts; it rejects undocumented/new public types/members. Inherited .NET
  and compiler-generated record operations are ordinary C# features, not host commands.
  Parent transport/orchestration types are explicitly classified outside the facade.
- The catalog explains synchronous Main/Complete, per-run state, Current/Whole
  scope, job ordinals rather than persistent document identity, cancellation,
  one-based paths, all four value states, raw/decoded reads, controlled mutations,
  segment occurrences, repetitions/custom separators, exclusions, held/deferred
  copies, output naming, CSV/schema/formula protections, safe diagnostics,
  atomic independent output, stale-source attribution, worker/resource limits and
  trusted-code security restrictions. It invents no navigation/enumeration API.
- Help → Script API Reference and the visible editor/library API Reference button
  open the same optional Dock tool. Literal case-insensitive AND search is bounded
  to 256 characters; category navigation and 64-entry Back history are session-only.
  Layout persistence contains placement metadata, not queries/examples/history.
  Details use standard themed selectable/read-only controls, normal clipboard
  copy and horizontal/vertical scrolling; narrow hosts stack topics above details.
- Six starter drafts cover guarded PID read/write, host-driven message iteration,
  editing only an existing second repetition, independent output with a suggested
  filename, one count-only CSV row, and a bounded synthetic identity library. They
  are reference examples, not additional built-in library presets. Create editable
  script uses the existing Save/Discard/Cancel and revision checks; it creates an
  unsaved user draft without overwriting a saved script. Save/reopen/Run use existing
  library/worker paths. Built-in/My Scripts labels make ownership explicit.
- Patient Deidentifier and all other built-ins remain immutable and keep their IDs,
  versions and source. Duplicate/Rename/Edit/Save/Reopen/Run uses independent user
  IDs; application-owned updates never rewrite user records. No library schema or
  implicit migration changes. Existing synthetic identity helpers were already
  public and worker-accessible, so no new facade or capability was necessary.
  Repeated real-worker executions verify deterministic fresh-run profiles.
- Core, save semantics, message identity, worker protocol, report journal/publication,
  output naming behavior and native editor transaction/keyboard handling are unchanged.
  The existing ScriptBatch.MessageNumber XML comment now accurately states its
  job-local semantics. No dependency, editor rewrite, IntelliSense/LSP or new engine.

## Permanent regression coverage

Scripting tests compare the actual supported public member inventory, require every
topic's documentation fields, and compile/execute all 18 distinct complete examples
from the shared catalog in real workers (50 topics reuse appropriate examples).
They drain actual report/deferred responses and retain existing worker/lifecycle,
output naming, security-directive, cancellation and de-identification coverage.

Workspace tests cover offline embedded search/categories, query bounds, all six
starters saved/executed in both scopes, independent unsaved publication and unchanged
source bytes, count reports, existing repetitions, output naming and deterministic
helper use. They exercise dirty/newer-draft consent plus complete Patient built-in
duplicate/rename/edit/save/reopen/run and all built-in ownership guards.

Headless Desktop tests cover Help/editor discovery, retained model/detached and
reconstructed views, layout restoration without persisted help content, search,
categories, Back bounds, exact signature/example clipboard bytes, native Ctrl+V into
the ordinary script editor and native local Ctrl+Z, starter command/dirty gating,
light/dark standard-control resources and narrow/wide layouts. The native-input
regression uses a fresh child process because AvaloniaEdit's shared default key
bindings are dispatcher-affine, following the existing permanent regression pattern.
Existing shell/toolbar tests now include the authorized Help menu/API button and
retain all previous tool/shortcut assertions. No test or protected fixture was removed.

## Verification

Verified on 2026-10-20 with .NET SDK 10.0.401/runtime 10.0.12, Release configuration.

| Check | Result |
| --- | --- |
| Locked solution restore | Pass; package locks unchanged |
| Release build (`--no-restore -warnaserror`) | Pass; 0 warnings, 0 errors; 2.35s |
| Focused Scripting | 123 passed; 23.060s |
| Focused Workspace/library | 38 passed; 15.744s |
| Focused Desktop | 50 passed; 21.259s |
| Full solution | 1,544 passed; 2m 53.028s |
| Complete Workspace stress suite | 733 passed; 5m 05.767s |
| ScriptHost Current + Whole smoke lifecycles | Pass against existing Release layout; no publish operation |
| Generated Markdown synchronization | Pass; exact authoritative JSON source |
| Eight tracked HL7 fixtures | Pass; exact no-filter Git blobs |
| Original artwork, excluded _TEST.hl7 and protected regressions | All 13 recorded hashes pass; ICO/master unchanged too |
| Git and new-file whitespace | Pass; all 28 milestone text files |

All final gate tests have no failures, skips or errors. The 40 new permanent cases
account for the increase from 1,504 to 1,544 solution tests (19 Scripting, 15 Workspace,
6 Desktop) and from 718 to 733 complete Workspace stress tests. Earlier focused harness failures/
build diagnostics remain in ignored artifacts/stage9c for review; they are not
discarded by cleanup. No permanent regression coverage was removed or weakened.

## Exact Stage 9C changed-file inventory

28 milestone paths: 17 tracked modifications and 11 new files. No file is staged.
The additional untracked samples/synthetic-only/stage5b-repeated-patients_TEST.hl7
is excluded, unchanged and inspected only by hash.

```text
README.md
build/WriteScriptApiReference.py
docs/PROJECT_CONTEXT.md
docs/script-api-reference.json
docs/script-api-reference.md
docs/script-api.md
docs/stage9c-script-developer-experience-acceptance.md
src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/MainWindow.Shell.cs
src/HL7Workbench.Desktop/MainWindow.axaml
src/HL7Workbench.Desktop/MainWindow.axaml.cs
src/HL7Workbench.Desktop/ViewModels/ScriptApiReferenceViewModel.cs
src/HL7Workbench.Desktop/ViewModels/ScriptEditorViewModel.cs
src/HL7Workbench.Desktop/Viewer/ScriptApiReferenceView.cs
src/HL7Workbench.Desktop/Viewer/ScriptEditorView.cs
src/HL7Workbench.Scripting/ScriptBatch.cs
src/HL7Workbench.Workspace/HL7Workbench.Workspace.csproj
src/HL7Workbench.Workspace/ScriptApiReference.cs
src/HL7Workbench.Workspace/ScriptEditorSession.cs
tests/HL7Workbench.Desktop.Tests/ScriptApiReferenceTests.cs
tests/HL7Workbench.Desktop.Tests/ScriptEditorWorkflowTests.cs
tests/HL7Workbench.Desktop.Tests/ScriptReferenceNativeInputTests.cs
tests/HL7Workbench.Desktop.Tests/SearchToolTests.cs
tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs
tests/HL7Workbench.Scripting.Tests/HL7Workbench.Scripting.Tests.csproj
tests/HL7Workbench.Scripting.Tests/ScriptReferenceTests.cs
tests/HL7Workbench.Workspace.Tests/ScriptReferenceTests.cs
```

Ignored verification logs, XML results and hash manifests are deliberately retained
under artifacts/stage9c. Earlier verification/stress fixtures remain. Only the owned
one-off catalog authoring probe is disposable; permanent source, generator, tests and
fixtures are retained. No build-output directory or pre-existing user file is deleted.

Final Git state: HEAD is ea897decdbd79d067d13552e972ab456885a61d6; the index is empty.
The 28 Stage 9C paths above remain unstaged (17 modified tracked, 11 new). The
additional excluded _TEST.hl7 remains unchanged/untracked. Final implementation/test
hashes match the verified build; documentation-only measured-result updates followed
testing. No Stage 9C commit, push, tag, packaging, deployment or publication occurred.

## Known limits and native Windows acceptance

This is API documentation and starter assistance, not completion/IntelliSense, a
debugger, a new privacy policy or a guarantee that a custom script is safe. Users
remain responsible for trusted code and reviewing transformed data. ScriptHost is
process/resource containment, not an OS sandbox; arbitrary trusted C# can have user
filesystem/network side effects. The documented bundled helpers themselves use no
IO/network or persistent mappings. Example field replacements are demonstrations,
not complete de-identification. The fixed opaque key in the identity starter shares
one profile deliberately; use the full Patient built-in for supported qualified
identity resolution and conservative sensitive-content handling.

Search is a bounded literal AND search, without fuzzy ranking, syntax analysis,
network help or external links. Back history/query/selection survive Dock view
reconstruction in the same session; application restart retains placement only.
Clipboard availability, native rendering/focus, floating windows and DPI remain
manual acceptance checks. Generated examples are complete scripts; replace an
editable draft with them or create a starter rather than appending duplicate Main.

Run the existing Release Desktop build without a debugger; use synthetic inputs only:

1. Open Help → Script API Reference with and without an HL7 document. Verify it is
   modeless, searchable and does not block normal document work.
2. Open View → Script Editor and click its visible API Reference button. Confirm it
   reveals the same tool; use the toolbar's horizontal scroll if the host is narrow.
3. Search `Read`, `Text`, `SetText`, `SetRaw` and `subcomponent`. Choose Read and write
   and Segments and paths categories. Confirm actual signatures, parameter/return
   explanations, custom delimiter/repetition examples and limits; exercise Back.
4. Copy a signature, then a complete example. Duplicate a built-in or create a new
   editable draft; select its code and Ctrl+V. Verify exact clipboard text, normal
   script-local Undo/Redo and no changes to the active HL7 document's undo history.
5. Select Patient Deidentifier in the library. Verify read-only source. Choose
   Duplicate Script, confirm My Scripts ownership, rename it, change the seed, Save
   Script, close/reopen the tool and application and reopen the saved copy. The
   built-in source/name must remain unchanged.
6. Run that copy against synthetic input in Current and Whole scopes. Verify normal
   independent unsaved output, output-associated Transformation Report and CSV export.
7. Search `starter`, choose one of the six examples and Create editable script.
   Save, reopen and run it in both scopes. Verify host-driven counting (1 in Current,
   all captured messages in Whole), existing-second-repetition behavior and static
   output naming. Try Save/Discard/Cancel with a dirty draft before another starter.
8. Inspect the synthetic helper example and its explicit opaque-key/identity/privacy
   limits. Repeat with identical seed/input/order and compare saved output bytes.
9. Disconnect network access and reopen/search/copy/create/run the same examples.
   Reference content and synthetic vocabulary must remain available offline.
10. Cancel a long-running trusted synthetic script; verify no partial output/report,
    source/draft retention and successful subsequent runs. Check an invalid script
    produces fixed diagnostics and a report-producing script retains export behavior.
11. Switch light/dark themes. Dock, float, resize narrowly/widely, close/reopen and
    save/restore the workspace. Verify readable text/controls, scrollable long code,
    retained session search/selection, usable copy buttons and normal keyboard focus.
12. Verify small icon/title/taskbar appearance and the previously accepted Stage 9
    output workflows as regression checks. Never open or export the excluded user HL7.

**Stop for native Windows acceptance. Stage 9C is not staged or committed.**
