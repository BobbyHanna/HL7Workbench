# Stage 5A Script Editor visual acceptance

The user completed Stage 5A's native functional checklist. This focused polish pass
addresses code contrast, code font/zoom and cramped layout. The user has confirmed
final native visual acceptance, including point sizes and contextual symbol colors.
The user has authorized one accepted Stage 5A milestone commit and normal push on
main. No tag, release or history rewrite is authorized.

## Changes and boundaries

- `ScriptEditorPalette` centralizes VS Dark-inspired lexical and editor colors.
  Default/unresolved identifiers, properties and punctuation use `#D4D4D4`; keywords
  `#C586C0`, built-in type-keyword categories `#4EC9B0`, strings `#CE9178`, comments
  `#6A9955`, numbers `#B5CEA8`, background `#1E1E1E`. Caret, line numbers, selection
  and current-line colors are explicit. A Roslyn compiler-only overlay colors resolved
  methods `#DCDCAA` and types `#4EC9B0`, including segment/chained/extension calls.
  Properties and direct delegate-variable invocations remain neutral. Unresolved or
  ambiguous symbols are not guessed. Strings/comments use compiler syntax context.
- New default code font is **12 pt**, with 9–24 pt limits. The footer displays actual
  whole points (e.g. `12 pt`); −/+, Ctrl+wheel, Ctrl++/− and Ctrl+NumPad Add/Subtract
  all step exactly 1 pt. Ctrl+0 or the point-label button resets to 12 pt. Shortcuts
  operate in code only. Ordinary wheel scrolling remains.
- Font size is saved immediately to `%LOCALAPPDATA%/HL7Workbench/script-editor-preferences.json`.
  Failed/corrupt settings keep the editor usable and show a safe status. The setting
  contains no script/source data. V1 saved DIP sizes migrate to their nearest whole
  point without overwriting the file on read; previous UI-generated sizes retain their
  selected point size. Next change saves V2 FontSizePoints. Edit-mode settings and
  workspace layout stay separate.
- Two compact toolbar rows reserve code height. At narrow widths, horizontal
  scrollbars keep all actions reachable without stacking more rows. Source context
  is shown when wide and always available on the scope tooltip. Status has an
  ellipsis and full tooltip. Library/save errors remain visible in status.
- Empty diagnostics use no space; errors expose a collapsed Diagnostics header.
  Expand to read/navigate, collapse to recover code space. Existing dock proportions
  are retained, so users can still adjust splitters to their preferred code height.

Full-suite verification also exposed a pre-existing snapshot cleanup race: another
capture can dispose a directory after enumeration but before its metadata lookup.
The metadata check now sits inside the existing best-effort I/O guard. A concurrent
capture/dispose regression test reproduced the failure before the fix. Snapshot
ownership, cleanup eligibility and publication transactions are unchanged.

The symbol pass adds the existing pinned Microsoft.CodeAnalysis.CSharp/Common 5.9.0
compiler packages to Desktop. This explicitly amends the initial worker-only Roslyn
loading policy; only the worker loads scripting/execution packages. A prototype
resolved the requested cases in approximately 430 ms cold and 110 ms for 50,000
characters in this environment, without Workspaces/LSP infrastructure. Those timings
are observations, not guarantees. Analysis is debounced 200 ms, runs off the UI thread,
and shares one active binding slot across views with a three-second cooperative
cancellation budget. Edits immediately clear old ranges; revision/document checks
discard late results. Closing/reconstructing a view cancels its analysis. Fixed
framework/facade metadata is shared; script trees/models/strings are not cached or
logged. Pending/error/canceled/unresolved regions retain readable lexical colors.

No Core, worker, protocol, facade, output transaction or Stage 5B changes. This is
limited symbol coloring, not a complete semantic IDE classifier. Dynamic/ambiguous
calls and symbols outside the fixed metadata set may remain neutral.

## Files in this polish pass

| Files | Purpose |
| --- | --- |
| `src/HL7Workbench.Desktop/Viewer/ScriptEditorPalette.cs` | Central token/chrome palette, existing lexer adapter |
| `src/HL7Workbench.Desktop/Viewer/ScriptSymbolClassifier.cs` | Compiler-only, request-scoped token/symbol classification |
| `src/HL7Workbench.Desktop/Viewer/ScriptSemanticHighlighting.cs` | Debounce/cancel/revision guard, view-owned overlay |
| `Directory.Packages.props`, Desktop project/lock and Desktop.Tests lock | Pin/deploy compiler-only 5.9.0 reference and test visibility |
| `src/HL7Workbench.Desktop/Viewer/ScriptEditorView.cs` | Compact toolbars/status, collapsible diagnostics, code-only zoom routing |
| `src/HL7Workbench.Desktop/ViewModels/ScriptEditorViewModel.cs` | Retained font state, persistence and safe settings errors |
| `src/HL7Workbench.Desktop/MainWindow.axaml.cs` | Compose the persistent preferences path |
| `src/HL7Workbench.Workspace/ScriptEditorPreferences.cs` | Bounded/versioned font settings, atomic save |
| `src/HL7Workbench.Workspace/ScriptEditorSession.cs` | Explicit library-error state so compact status keeps failures visible |
| `src/HL7Workbench.Workspace/SourceSnapshot.cs` | Guard metadata lookup against a concurrently removed directory |
| `tests/HL7Workbench.Desktop.Tests/ScriptEditorPolishTests.cs` | Eight color/layout/input/persistence/diagnostics cases |
| `tests/HL7Workbench.Desktop.Tests/ScriptSymbolHighlightingTests.cs` | Seven symbol/incomplete-code/late-result/lifecycle cases |
| `tests/HL7Workbench.Workspace.Tests/ScriptEditorPreferencesTests.cs` | Nine settings/migration cases |
| `tests/HL7Workbench.Workspace.Tests/Stage3LifecycleTests.cs` | Concurrent snapshot cleanup regression |
| `README.md`, `docs/PROJECT_CONTEXT.md`, `docs/architecture.md`, `docs/decisions/0005-message-scripting-foundation.md`, `docs/stage5a-scripting-acceptance.md`, this checklist | Acceptance status and polish behavior |

## Automated verification

The results below record the completed refinement checkpoint. See
`stage5a-final-ci-investigation.md` for subsequent tooltip regression coverage
and final verification after cleanup.

Locked restore passed. Release build: zero warnings, zero errors. Complete suite:
**619 passed, zero failed/skipped** (211 Core, 272 Workspace, 108 Desktop, 28 Scripting).
The final point-size/contextual-color refinement adds 13 cases beyond the previous
606-test polish pass: eight Desktop and five preference-migration cases. Folder
publishing also verifies compiler assemblies at the desktop root and scripting
runtime assemblies confined to ScriptHost. `git diff --check` passed.
Workspace stress passed all **274 cases** with `HL7WORKBENCH_STRESS=1`, including
the 256/512 MiB fixtures and 4,096-message scripting stream.

## Native Windows visual checks

Launch from `C:\Dev\HL7Workbench`:

```powershell
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

Use only `samples/synthetic-only/stage5a-scripting.hl7` and synthetic script text.
For the contextual-color check, a duplicated/new script can contain:

```csharp
Message.SetText("PID-5.1", "SYNTHETIC");
foreach (var pid in Message.Segments("PID"))
    pid.SetText("5.2", "EXAMPLE");
Message.Segments("PID").First().Clear("19");
var raw = Message.Read("PID-3").Raw;
Action callback = () => { };
callback();
```

1. **Docked/floating layout:** View → Script Editor; duplicate the bundled example.
   Check a wide, shallow tool below the HL7 viewer, then a narrow floating tool.
   Resize and scroll both toolbars to Save/Rename and Review Output. Code should
   retain useful space, with no clipped actions. Float/dock/reconstruct the tool:
   the draft remains. Verify mixed DPI if available. Existing layout is not reset.
2. **Colors and diagnostics:** inspect comments, `if`, `string`, numbers, strings,
   method calls, variables and punctuation. Resolved calls become light yellow;
   types teal, properties/delegate-variable calls neutral. Check `Message.SetText(...)`,
   `pid.SetText(...)` in a foreach, and `Message.Segments("PID").First().Clear("19")`.
   Type an incomplete call, then repair it and quickly replace code/open another
   script: no older symbol colors should appear over newer text. No dark
   text disappears against the code background. Check line numbers, caret, selection
   and current line. Run `var invalid = ;`, expand Diagnostics, double-click its entry
   to navigate, then collapse. Confirm diagnostics and status are readable in the
   operating system's current application theme.
3. **Zoom:** hover code and use Ctrl+wheel both ways, Ctrl++/−, numpad Add/Subtract,
   Ctrl+0 and footer buttons. Verify exact whole-point labels/1 pt steps and limits;
   the point label/Ctrl+0 resets to 12 pt. A saved prior choice must survive the upgrade.
   Ordinary wheel still scrolls. Select text, move the caret, edit and undo/redo at
   different zoom levels; line numbers and caret must stay aligned. Shortcuts in
   the HL7 editor or script-name box must not zoom the script editor.
4. **Save routing/drafts:** make both an HL7 edit and a script edit. Ctrl+S in code
   saves only the script. Ctrl+S in the HL7 editor retains its normal behavior.
   Recheck script rename, duplicate and Save/Discard/Cancel on tool close/switch;
   dismissed/canceled dialogs preserve drafts. Current/whole execution and output
   review retain the functionally accepted behavior.
5. **Restart:** choose a non-default zoom, save any wanted script changes and exit
   normally. Restart and reopen Script Editor: the chosen font size returns without
   changing the HL7 font, edit-mode preference or dock placement.

Headless tests verify token brushes, narrow layout measurements, routed input,
draft/undo preservation, diagnostic navigation and font persistence. Native rendering,
font rasterization, OS wheel behavior and mixed-DPI floating hosts are covered by
the native checklist rather than headless verification. The user has confirmed
completion of native visual acceptance; supplemental hardware configurations remain
environment-dependent.
