# Stage 20B — Structured field editing and Ctrl+Wheel

Stage 20B extends the existing Field Inspector and shared content-font system.
No new dockable editor, serializer, parser, dependency, SQL/scripting/transport
feature, absent-node creation or bulk operation is introduced. No commit, push,
package or publication is authorized; native Windows acceptance remains pending.

## Baseline authorization

Implementation began on clean main at aeee55c95362a031c4b3bdd3e813fb8a24721cde,
with the committed Stage 20A implementation and recorded local 2,768/0/14 regular
suite. Its [Actions run](https://github.com/BobbyHanna/HL7Workbench/actions/runs/41202729354)
was observed in progress during orientation. The owner explicitly authorized
proceeding using verified local results. This is not a claim of remote CI or native
acceptance. Repository guidance, current developer/user guides, Core edit/path APIs
and ADRs 0004/0011 were reviewed before implementation.

## Structured edit architecture

InspectionPanes adds Edit Value to the existing row context menu. The actual
selected structural node is captured through the existing inspector intent route.
MainWindow uses the same handler from docked and floating roots and owns one
StructuredValueEditSession plus a compact native StructuredValueEditWindow. The
dialog belongs to the originating inspector window and shows the canonical path,
read-only complete current raw value, staged replacement, Apply/Cancel and feedback.

Workspace captures document and MessageId, content/search revisions, inspection/
node identity, exact message-local UTF-16 range, original raw value and path. No
snapshot lease or independent history is created. Switching documents/messages,
selection/raw edits, turning Edit Mode off, close or other loss of edit eligibility
permanently invalidates a staged target; returning to the same path cannot revive it.
Apply rechecks identity before committing. Disposal clears value/document references
and subscriptions; the dialog clears its input controls on close.

Apply validates in an isolated bounded Core model. Existing SetRaw performs the
level-specific separator, closed-escape, encoding, neighboring-element and range
checks. The candidate must equal the exact captured-span splice. One existing
ReplaceWorkingText call publishes the change and creates one document delta-history
entry. Core's active published model is never mutated behind Workspace. This uses
bounded complete-message text already supported by the document buffer; it does
not materialize an entire file or serialize segments from their field models.

The existing debounced EditRefresh handles pending/previous presentation, highlights,
diagnostics, schema revalidation and dirty state. The shell awaits it and restores
the edited path only while the committed document/message/revisions remain current.
An explicit current-node reveal restores raw highlighting even when a longer value
keeps the same logical selection through reparsing; the original SelectNode overload
retains its existing behavior.
The raw viewer keeps its existing caret/layout update path and then highlights the
edited node; exact prior caret/scroll preservation is not promised for every reflow.
Cancel, invalid input and unchanged values create no history entry. Undo/redo uses
the same document stack as raw editing. Save/Save As retains its existing byte-splice,
strict encoding/BOM, streaming verification and atomic publication behavior.

## Eligibility and raw semantics

| Target | Support |
| --- | --- |
| Existing field | Replaces only that field, including explicitly selected repetitions/components within it; allows their separators. |
| Existing repetition | Allows component/subcomponent separators; other repetitions remain unchanged. |
| Existing component | Allows subcomponent separators; sibling components remain unchanged. |
| Existing subcomponent | Rejects unescaped structural separators. |
| Represented empty node | Supported when its actual zero-length range is known. No new delimiter is manufactured. |
| Absent schema field/component | Unavailable; no safe existing span. |
| MSH-1 / MSH-2 | Unavailable; require a separately designed delimiter-conversion workflow. |
| Ambiguous/malformed target, source-backed or oversized message | Unavailable under existing Core/document editing eligibility. Unrelated opaque regions remain retained. |

Input is raw encoded HL7. No escape decoding/re-encoding occurs. Actual declared
delimiters, including custom sets, drive Core validation. Legitimate closed escapes
remain verbatim; unescaped separators above the selected level, unclosed escapes,
segment terminators, tabs, control/Unicode line separators and unrepresentable text
are rejected. Empty and explicit-null remain different. No clinical terminology or
schema conformance prerequisite is added. Other MSH fields use ordinary safe raw
semantics; MSH-18 does not change the actual file encoding.

Existing 1 Mi-character complete-message, 16 Ki-character line, Core structure,
32 MiB retained-character-storage and 4,096-entry document history budgets remain.
The dialog bounds replacement input to the existing line ceiling; remaining segment
text and aggregate/history costs are checked before publication. Oversized source
values are not decoded/materialized just to open this editor.

## Pointer-targeted zoom

ContentFontScope tunnels Ctrl+Wheel before nested ScrollViewer/AvaloniaEdit handlers.
A weak registry selects the nearest supported content scope under the event's
pointer source, independently of keyboard focus; nested scopes zoom only once.
Up/down changes that feature's saved override using Stage 20A's 12–32 DIP reference
bounds and existing atomic preference system. Global defaults stay unchanged.

Coverage follows registered Stage 20A content: raw HL7/Full Source, inspector/
Navigator, SQL editor/results, Documentation/API Reference, Script Editor, Browser,
Find, Activity, extraction/comparison, FHIR/report and connectivity/testing content.
Keyboard Plus/Minus/0 retains focus-based behavior. The standalone C# control keeps
its previous wheel fallback; production shared scopes prevent duplicate handling.

Unmodified wheel scrolling, horizontal/diagonal gestures, extra modifiers, menu/
command controls, selectors, sliders/thumbs, chrome and independent modal dialogs
are excluded. A valid handled zoom consumes the event, including a clamped gesture
at a bound. Failed persistence retains the previous active size. Resizing does not
edit text, add history, execute scripts/SQL/network work or alter source coordinates.

## Verification

Final verification used the Release build containing the longer/shorter replacement
highlight regression fix. No existing test was removed or weakened.

| Check | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Focused Core | 85 | 0 | 0 |
| Focused Workspace | 105 | 0 | 0 |
| Focused Desktop | 40 | 0 | 0 |
| Focused total | 230 | 0 | 0 |
| Full regular solution | 2,825 | 0 | 14 |
| Python documentation compiler | 12 | 0 | 0 |

The final regular run contains 2,839 cases and took 4m 26s 326ms. All 14 skipped
case identities match Stage 20A's opt-in SQL resource/performance cases. The 57 new
permanent cases comprise 38 Workspace structured-edit tests, 10 Desktop edit-dialog/
raw-editor tests and 9 Desktop pointer-wheel tests. Focused coverage also ran existing
Core PathAndEditing/Hardening, Workspace EditableDocument/StructuralSave and Desktop
Inspection/ScriptEditorPolish tests.

Both normal and isolated locked restores pass; Release build reports zero warnings
and zero errors. Commands (with build/test telemetry opt-outs) were:

```powershell
dotnet restore HL7Workbench.sln --locked-mode
dotnet restore HL7Workbench.sln --locked-mode --artifacts-path C:/Dev/HL7Workbench/artifacts/stage20b/restore
dotnet build HL7Workbench.sln --configuration Release --no-restore
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
```

Focused tests used `dotnet test --project tests/HL7Workbench.<Project>.Tests
--configuration Release --no-build --no-restore -- --filter-class '<pattern>'`,
one class pattern per invocation. Documentation compiler tests pass with
`python -B -m unittest build/test_documentation_catalog.py`; the existing catalog
generation workflow and `build/WriteDocumentationCatalog.py --check` pass, as does
`build/WriteScriptApiReference.py --check`. Script API resources remain unchanged.
The generated offline Help catalog retains canonical UTF-8/LF bytes.

Local Markdown verification covers 153 files, 1,073 local links and 38 heading-anchor
references with zero errors. `git diff --check` and explicit checks of final newlines/
trailing whitespace in all 21 changed text files pass. Git's existing autocrlf checkout
advisories are unrelated to the clean zero-warning Release build; no line-ending
policy was broadened.

SHA-256 verification against `artifacts/stage17/protected-before.json` confirms all
2,384 protected private-preview files are present and unchanged, with zero missing,
changed or added files. All 525 captured runtime/build/test input hashes match the
final tested inputs. The HEAD remains the authorized baseline and the index is empty.
No commit, push, package or publication was performed.

During development, native-control regression coverage caught a missing raw highlight
after a longer replacement despite correct model selection. The compatible explicit
reveal overload fixes it; docked longer/floating shorter cases now pass alongside
the final full run. Tests also await outstanding shell actions and document/SQL
cleanup before deleting their owned temporary directories.

Ignored `artifacts/stage20b/` retains verification logs, input/file inventories,
hash/link results and the isolated restore outputs for review. These are diagnostic
evidence, not distributable packages. Automated native-control tests do not constitute
Windows acceptance; the checklist below remains pending.

## Native Windows acceptance checklist — pending

Use synthetic data only. Review both Light/Dark and representative DPI scaling.

1. Open a synthetic ADT file, enable its Edit Mode, select PID field 5 component 2
   and invoke Edit Value. Verify path/repetition context and the exact encoded input.
2. Change Avery to Rowan, Apply, and observe Example^Rowan in raw HL7. Confirm the
   edited node is highlighted and there is no unrelated delimiter/terminator change.
3. Undo once and redo once; verify exact content and dirty state. Cancel another
   staged value and verify it adds no undo entry.
4. Edit a whole field, then one repetition. Review allowed separators and confirm
   other fields, segment occurrences and repetitions remain unchanged.
5. Edit a subcomponent, a represented empty component and a custom-delimiter value.
   Verify closed escapes remain raw rather than decoded/re-encoded.
6. Try an unsafe higher-level separator, newline/control character, unclosed escape
   and unsupported encoding character. Feedback must retain the draft and history.
7. Turn Edit Mode off; inspect normally but verify Edit Value is unavailable.
   Verify absent schema rows, MSH-1/MSH-2 and oversized/source-backed targets stay
   unavailable with an explanation and no manufactured delimiters.
8. Switch documents/messages during a staged edit using another available window.
   Returning must not revive Apply or retarget the same-looking path.
9. Change raw text or selection during staging where another window permits it.
   Reject stale Apply; cancel and reopen against the current parse.
10. Save As and reopen the modified file. Check BOM/encoding, mixed terminators,
    trailing empties, unrelated opaque regions and other messages remain preserved.
11. Ctrl+Wheel up/down over raw text, inspector, SQL editor/results, Documentation
    and C# while another tool has keyboard focus. Only the pointer's scope changes;
    C# must not zoom twice. Test keyboard Plus/Minus/0 afterward.
12. Scroll normally without Ctrl. Check horizontal/diagonal and modified gestures,
    menus, buttons/selectors, modal editor inputs and window chrome do not zoom.
13. Restart and verify per-tool font overrides; Ctrl+0 restores global inheritance.
    Check bounds, source selection/caret, dirty state and native text hit testing.
14. Float/redock the inspector, edit through its owned dialog, and repeat zoom in
    narrow/maximized layouts and 100/150/200% DPI or mixed-monitor arrangements.
    Review focus, clipboard, keyboard and screen-reader behavior.

Native acceptance has not been performed. Bulk assignment, multi-message editing,
creation of absent structure and Stage 20C work remain excluded.

## Changed files

15 updated and 6 new files (21 total):

```text
docs/PROJECT_CONTEXT.md
docs/desktop-preferences.md
docs/developer/desktop-tools.md
docs/developer/parser-invariants.md
docs/developer/workspace-lifecycle.md
docs/stage20b-structured-field-editing.md
docs/user/inspect-and-edit.md
docs/user/preferences-and-shortcuts.md
docs/user/troubleshooting.md
src/HL7Workbench.Desktop/Documentation/catalog.json
src/HL7Workbench.Desktop/MainWindow.Inspection.cs
src/HL7Workbench.Desktop/Viewer/ContentFontScope.cs
src/HL7Workbench.Desktop/Viewer/InspectionPanes.cs
src/HL7Workbench.Desktop/Viewer/ScriptEditorView.cs
src/HL7Workbench.Desktop/Viewer/StructuredValueEditWindow.cs
src/HL7Workbench.Workspace/InspectorProjection.cs
src/HL7Workbench.Workspace/MessageInspection.cs
src/HL7Workbench.Workspace/StructuredValueEditSession.cs
tests/HL7Workbench.Desktop.Tests/ContentFontWheelTests.cs
tests/HL7Workbench.Desktop.Tests/StructuredValueEditingViewTests.cs
tests/HL7Workbench.Workspace.Tests/StructuredValueEditingTests.cs
```
