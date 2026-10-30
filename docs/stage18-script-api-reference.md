# Stage 18 — integrated Script API reference

Stage 17 acceptance is committed at `6a0afb18ee3cf583c3222794e055504bc51ccf98`.
The owner confirms its Actions run is green. Stage 18 started with a clean working
tree and 2,384 matching protected-beta SHA-256 hashes. This milestone changes
reference content and editor assistance, not script execution semantics. No commit,
push, package, publication or Stage 19 is authorized. Stop for native Windows
acceptance after the verification report.

## Inventory and authority

`HL7Workbench.ScriptHost/UnifiedScript.cs` compiles API-2 submissions with
`ScriptGlobals`, the Scripting assembly, and its existing imports. ScriptHost's
Main/Complete discovery, compiler options, protocol, worker containment and
publication remain unchanged. ADRs 0005–0007 and 0013 continue to govern execution.

The catalog combines two sources, rather than introducing a second API list:

- `docs/script-api-reference.json` is the embedded, reviewed topic map and behavior
  source. It contains descriptions, examples, parameter/return explanations, explicit
  null behavior, errors, mutation, mode restrictions, related IDs and signature
  snapshots. Existing topic IDs and all six editable starters remain available.
- `Workspace/ScriptApiContracts.cs` reads declared public metadata from the same
  Scripting assembly referenced by ScriptHost. It derives constructor/method/property/
  constant/enum signatures, nullable annotations, parameter names, defaults,
  `params`/`out` modifiers, static flags, getter/setter/init accessors and return
  types. It invokes no getters, constructors or scripts. Generated record methods
  and inherited .NET members retain ordinary C# semantics and are outside host
  operations.

There are **25 supported facade/helper types, 186 declared supported contracts,
6 public host-infrastructure types with 29 contracts, and 52 reference topics**.
The supported types are:

| Area | Types |
| --- | --- |
| Globals and HL7 | `ScriptGlobals`, `ScriptMessage`, `ScriptSegment`, `ScriptValue`, `ValueState` |
| Lifecycle/output/reporting | `ScriptBatch`, `ScriptDeferredMessage`, `ScriptCsvReport`, `ScriptScope`, `ScriptPhase`, `ScriptDiagnostic`, `ScriptRunException`, `ScriptLimits`, `ScriptOutputFileName` |
| Synthetic identity | `SyntheticIdentityLibrary`, `SyntheticIdentity` |
| De-identification | `DeidentificationWorkflow`, `DeidentificationOptions`, `DeidentificationPreset`, `DeidentificationSummary`, `DeidentificationCategory`, `DeidentificationCategoryCount`, `DeidentificationFinding`, `DeidentificationSegment`, `DeidentificationReason` |

`ScriptDeferredMessage` is an opaque run-owned handle with no public constructor or
members. Type lookup links its producing/consuming Hold/Emit topics. It does not
invent a message-construction API.

Public host types (`ScriptDefinition`, `ScriptWorkerOptions`, `ScriptWorkerSession`,
`ScriptMessageResult`, `ScriptProgress`, `ScriptReportChunk`) are **physically
nameable by trusted C#**, because they are public in the referenced assembly. They
are not supported authoring facilities or a security boundary. Their metadata is
included in a separate, explicitly unsupported topic; no runnable authoring
example, starter or completion is offered. Internal Core models, worker lifecycle
hooks and protocol implementation methods are not promoted into supported APIs.

`ScriptApiReference` checks the supported member multiset, including overload
counts, against the actual runtime inventory. Permanent tests additionally
classify every exported Scripting type and require checked-in signature snapshots
to equal the generated signatures. A new binding cannot silently evade review.
Nullable annotations are C# caller contracts, not promises that every helper
performs a runtime null check; the behavior sections identify important exceptions.

## Machine-readable catalog and maintenance

`ScriptApiReference.ExportJson()` returns the complete offline catalog, with
`ApiVersion=2`, reference `Version=1`, and `ContractVersion=1`. It includes separate
supported and unsupported infrastructure contracts, structured parameter metadata,
reviewed behavior and complete examples. **Copy API catalog JSON** in the reference
copies this exact document. It contains no script draft, message body, saved script,
source filename, document identity or user reference query.

The permanent `ScriptApiCatalogTests` export the same document beside their test
outputs as `tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/script-api-catalog.json`.
The Stage 18 verification copy is retained under ignored `artifacts/stage18/`.

When runtime bindings change:

1. Review the real public surface and the actual host binding/lifecycle first.
   Classify new exported types and add/update topic member keys in the JSON. Do
   not classify a public host helper as an internal or supported operation merely
   to satisfy a test.
2. Review behavior, errors, null semantics, phase/scope restrictions, mutations,
   examples and related links. Keep the existing library record schema and stable
   topic/starter IDs unless a separate change explicitly authorizes migration.
3. Build Release and run the Workspace catalog tests. Their runtime export is
   available even when the snapshot assertion fails, for a reviewable diff.
4. Refresh snapshots and Markdown from that reviewed export:

   ```powershell
   python build/WriteScriptApiReference.py --sync-contracts tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/script-api-catalog.json
   python build/WriteScriptApiReference.py --check
   ```

   The synchronizer refuses differing API versions, supported-type classification,
   topic inventories or member classifications. It changes signatures only, not
   reviewed behavior or examples. Rebuild afterward so the embedded resource uses
   the refreshed snapshot.
5. Run catalog, real-worker example, Script Library, reference and assistance
   tests. Exact signatures/types/defaults and real example results must pass before
   the regular solution verification gate.

No extra package, language server, reflection over arbitrary user assemblies,
external documentation service or network dependency is used.

## Reference UI and navigation

Open the existing optional **Script API Reference** Dock tool from:

- Help → Script API Reference.
- Scripts ribbon → Help → API Reference.
- Script Editor → API Reference beside the Script Library controls.
- F1 in the Script Editor on a supported symbol.

The established tool ID and retained model are reused. It docks, floats, closes,
reopens and restores placement using the same architecture as other optional tools.
Search/category/selection/back history survive normal view reconstruction within
the session; query/source content is not serialized in layouts. The history remains
bounded to 64 locations, including context and related-topic navigation.

Literal case-insensitive AND search includes names, signatures, parameters,
descriptions, keywords, errors and behavior. Categories group actual facilities;
the unsupported public host boundary is clearly separate. The detail pane shows
exact signatures, parameters, returns, null/missing behavior, mutations, errors,
execution modes, coverage/limits, runnable examples and related topics. Copying
signatures/examples uses the normal clipboard; unavailable clipboard access has an
inline fallback. Unsupported host topics disable example copying and have no
editable starter. Existing starter creation uses the same dirty-draft consent and
save gates as before.

The view reuses Fluent themes, accessible control names, selectable/read-only text,
native scrolling, responsive tool surfaces, wrapping action bars and narrow/wide
pane layouts. Long signatures/examples scroll horizontally rather than silently
truncating their authoring text. Related buttons wrap at narrow widths.

## Context-sensitive assistance

The existing compiler-only symbol environment now serves coloring and assistance.
F1 resolves actual symbols, including inferred segment/workflow variables, aliases,
chained receivers, properties, helper types and lifecycle entry declarations. User
types with matching spelling do not masquerade as facade APIs. Comments, strings,
unknown members and ordinary .NET APIs produce a clear reference result rather
than executing code or selecting a similarly spelled API. The standard
`Cancellation.ThrowIfCancellationRequested()` helper links to the globals topic.
Invalid/ambiguous calls show supported candidate contracts without choosing an
invented overload. Type lookup can present several relevant topics.

**Ctrl+Space** explicitly opens AvaloniaEdit's existing completion window with
catalog-backed receiver members or supported type/global names. Its descriptions
include exact signatures and reviewed behavior. No automatic typing popup is
introduced. Opening completion changes no code; explicitly choosing an item
replaces only the completion prefix using the editor's normal undo transaction.
Read-only scripts are checked both before opening and before insertion.

**Ctrl+Shift+Space** explicitly shows invocation signatures without moving focus or
editing source. Escape, edits and view detach dismiss transient help. It is not a
full active-argument language service. No automatic code-hover popup was added;
documentation is exposed in the explicit completion-description infrastructure.

Analysis is bounded to the existing 65,536-character script limit, a three-second
request deadline and a small shared assistance gate. Edits/detach cancel work;
document/revision/caret checks reject stale results. Detached/completed requests
release source/syntax/model references. Only application/runtime metadata references
are cached. There is no emit, runner invocation, analyzer loading, directive
resolution, network lookup or script-source logging during assistance. F1 timeout
or lookup failure falls back to the offline reference with a fixed safe message.

## Execution modes and examples

Both Apply to scopes run the same existing lifecycle: initialization once, Main
once per captured input in order, then optional Complete after successful Main
processing. Current message selects one target; Whole document selects live logical
messages under the established capture rules. `Batch` is a facility, not a separate
script kind. Source-backed previews are never execution values.

| Facility | Initialization | Main | Complete |
| --- | --- | --- | --- |
| `Message` and its field/segment operations | Unavailable | Current captured target | Unavailable |
| `Batch.MessageNumber` | 0 | One-based captured job ordinal | 0 |
| `Batch.Report` | Define schema | Unavailable | Unavailable |
| `report.Row` | Append | Append | Append |
| `Batch.Hold` / `Batch.Defer` | Unavailable | Current message only | Unavailable |
| `Batch.Emit` | Unavailable | Unavailable | Own held copies only |
| `Batch.SetOutputFileName` | Suggest | Suggest | Suggest |
| `Cancellation` | Available | Available | Available |

Successful Run publishes a **new independent unsaved output document**. There is
no modify-in-place execution mode. `Message.SetText` transforms the worker copy,
not the open source editor/history/file. `SetOutputFileName` suggests only a safe
filename; it neither selects a directory nor creates/saves/renames a file. HL7
publication requires explicit Save/Save As. CSV reports require explicit export.
Failure/cancellation prevents managed staged publication; arbitrary trusted C#
side effects outside the application remain outside rollback.

Examples cover field states/read/decoded text, guarded writes, segment iteration,
field repetitions, filtering with Exclude, shared multi-message counts, hold/defer/
emit, synthetic identity/de-identification, filename suggestions and error handling.
The error-handling example catches a recoverable MissingValue read and records a
fixed diagnostic code; catching an exception does not undo earlier successful
mutations. Do not suppress cancellation or continue after workflow/budget failures.
All 18 distinct runnable examples execute through the real worker. Representative outcomes
(transformed field, excluded second message, exact count CSV, handled error code and
filename) have explicit assertions. Existing Workspace tests save/reopen starters,
run both scopes, verify independent outputs/source bytes and preserve all bundled
and user library records. De-identification remains an aid requiring review, not
an anonymity or conformance certification.

## Known limitations

- Completion/help cover the documented facade, not arbitrary .NET/user types,
  extension-method discovery, full IntelliSense, automatic code-hover or automatic
  parameter/active-argument tracking. No editor replacement is warranted.
- Requests on very incomplete/pathological source may fail to resolve or time out;
  search remains available. No code execution is used to improve a guess.
- Signature metadata describes public contracts, not every possible runtime misuse
  of non-nullable records/helpers. Original limits, unsafe framing restrictions,
  worker failure behavior and unsupported encodings remain authoritative.
- Public host infrastructure is inspectable but unsupported; inherited/generated
  .NET methods are not promoted to host APIs. Trusted C# is still not an OS sandbox.
- State retention is session-only. Native popup placement, real monitor DPI changes
  and detailed keyboard/clipboard interaction require Windows acceptance.

## Verification and changed files

Both locked restores passed, including the isolated artifacts-directory restore.
Release build passed with **0 warnings / 0 errors**. Focused suites passed
**Scripting 94 / 0 / 0**, **Workspace 111 / 0 / 0**, **Desktop 107 / 0 / 0**
(passed/failed/skipped). These include the new coverage plus original lifecycle,
de-identification, library, execution, native copy/paste, editor and responsive-tool
regressions. These focused runs overlap the additional targeted checks below.

The final exact command `dotnet test HL7Workbench.sln --configuration Release
--no-build --no-restore` passed **2,713 / 0 / 14**, **2,727 total**, in
**4m 47.968s**. All five projects passed. The same 14 opt-in skip identities and
their classification sources are unchanged. After the final suite, all **2,384
protected beta SHA-256 hashes** matched: **0 changed / 0 missing / 0 added files**.
Runtime/test/build-tool source hashes match the snapshot used for this run;
only the documentation and ignored verification records were finalized afterward.

The first full solution run exposed an existing validation-disposal race: completion
could remove a task while `HashSet.ToArray()` copied its disposal snapshot, leaving
a null array slot. The three collection accesses are now synchronized. No validation
rules, script runtime behavior or editing/save semantics changed. One permanent
regression performs 384 reapplications across 32 closes. Post-fix checks passed
**Workspace 34 / 0 / 0** (validation lifecycle/catalog/reference) and **Desktop
37 / 0 / 0** (reference/assistance/native input). These overlap the focused suites;
they are separate runs, not additional unique-case totals. The integration test
also discards its own temporary dirty script before closing its shell, avoiding an
unanswered save prompt in test cleanup.

Added **46 permanent regular cases**: **5 Scripting / 10 Workspace / 31 Desktop**.
Existing tests remain; ribbon inventory assertions now include the new eleventh
command/Help group, preserving all ten previous commands and their exact shared
instances, operation gates, icon/size/contrast and light/dark/pinned/popup checks.
The catalog snapshot test initially identified the intentionally unsynchronized
public-host signature placeholder; the verified export/synchronizer resolved it.
The test runner rejected an unsupported internal-wildcard filter with zero tests;
explicit class filters were used for the recorded successful runs. These reviewed
diagnostics did not suppress or replace any test coverage.

The Markdown check passes. JSON hashing is canonical UTF-8/LF so Windows checkout
line endings do not stale the generated reference; HL7 handling is unaffected.

**27 changed files: 20 updated tracked / 7 new.**

| Area | Files (repository-relative) |
| --- | --- |
| Documentation/tooling | `README.md`, `build/WriteScriptApiReference.py`, `docs/PROJECT_CONTEXT.md`, `docs/script-api-reference.json`, `docs/script-api-reference.md`, `docs/script-api.md`; new `docs/stage18-script-api-reference.md` |
| Workspace | `src/HL7Workbench.Workspace/ScriptApiReference.cs`, `MessageDocument.Validation.cs` (task-drain synchronization); new `src/HL7Workbench.Workspace/ScriptApiContracts.cs` |
| Desktop shell/model | `src/HL7Workbench.Desktop/HL7Workbench.Desktop.csproj` (existing compiler-reference comment only), `MainWindow.Shell.cs`, `ViewModels/ScriptApiReferenceViewModel.cs`, `ViewModels/ScriptEditorViewModel.cs` |
| Desktop views/assistance | `src/HL7Workbench.Desktop/Viewer/ScriptApiReferenceView.cs`, `ScriptEditorView.cs`, `ScriptSymbolClassifier.cs`, `UtilityRibbon.cs`; new `ScriptApiLookup.cs`, `ScriptEditorAssistance.cs` |
| Tests | `tests/HL7Workbench.Desktop.Tests/RibbonCorrectionTests.cs`, `ScriptApiReferenceTests.cs`; new `ScriptEditorAssistanceTests.cs`; `tests/HL7Workbench.Scripting.Tests/ScriptReferenceTests.cs`; new `ScriptReferenceExampleTests.cs`; `tests/HL7Workbench.Workspace.Tests/SchemaValidationLifecycleTests.cs`; new `ScriptApiCatalogTests.cs` |

Verification evidence is retained under ignored `artifacts/stage18`: build/restore/
focused/full logs, the complete runtime-derived JSON catalog, source/file manifests,
protected hash audits, reviewed failure diagnostics and isolated restore metadata.
The generated test-bin catalog is an intentional reusable verification output;
no disposable HL7/CSV exports or probes need cleanup. Permanent tests/examples
remain. AGENTS.md is unchanged because its existing architecture, retention and
verification guidance already applies.

Main/HEAD remains `6a0afb18ee3cf583c3222794e055504bc51ccf98`. The index is empty;
all 27 paths are unstaged. Core, Scripting and ScriptHost sources, script execution/
library storage, package references/locks and protected beta contents are unchanged.
The only incidental runtime-lifecycle correction is the documented Workspace
validation task-drain synchronization. Native Windows acceptance remains pending.
No commit, push, package, publication or Stage 19 work occurred.

## Native Windows acceptance checklist

- Open reference from Help, Scripts ribbon and Script Editor, with and without an
  HL7 document. Confirm one optional tool rather than a document tab/window app.
- Dock/float/close/reopen/restore the tool while searching/selecting; verify useful
  state and bounded Back navigation survive without leaking queries into layouts.
- Search exact names, parameter names, keywords and error codes. Check nullable
  `TryNormalize`, optional identity cancellation, params CSV rows, init properties,
  enum/constant values and unsupported public host metadata against copied JSON.
- Copy a signature, example and full catalog. Paste a supported example into an
  editable draft, undo, save/reopen and run both scopes. Verify existing user scripts
  and immutable bundled examples are intact; dirty drafts use normal consent.
- F1 on Message.SetText, inferred segment reads, aliases, workflow properties,
  opaque handles and lifecycle methods. Check unknown/user-shadowed symbols,
  comments/strings and ambiguous calls; confirm source, undo and execution state
  remain unchanged.
- Invoke Ctrl+Space explicitly, inspect documentation, choose a completion and undo.
  Confirm no typing popups and no edits to read-only examples. Inspect
  Ctrl+Shift+Space inside invocation arguments and Escape dismissal.
- Repeat in light/dark, narrow docked panels (about 250–400 DIPs), floating and
  maximized windows at 100/150/200% Windows scaling, including monitor transitions.
  Check long signatures, descriptions, related buttons, keyboard reachability and
  completion/signature popup placement for clipping/overlap.
- Type rapidly, move the caret, switch scripts, float/redock and close tools during
  analysis; confirm stale results do not navigate or edit a newer draft.
- Run the provided synthetic read/write/filter/count/error/de-identification examples.
  Verify output is independent and unsaved, names are suggestions, CSV export is
  explicit, and source bytes/edit history remain unchanged by script execution.

Stop for owner acceptance. Do not commit, push, package, publish or start Stage 19.
