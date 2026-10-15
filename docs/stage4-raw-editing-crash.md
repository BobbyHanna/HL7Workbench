# Stage 4 raw editing crash: reproduction and fix

Native acceptance reported a crash on ordinary raw editing on 2026-10-15.
Stage 4 was unaccepted when the failure was investigated. The user subsequently
accepted Stage 4 and this fix on 2026-10-15 and authorized one local milestone commit
after final verification. This fix changes only editor history coordination, its
regression coverage and the associated documentation.

## Exception evidence collected before choosing the fix

A minimal synthetic message reproduces the same failure through Avalonia's routed
input and the real AvaloniaEdit control in Release/headless tests:

```csharp
"MSH|^~\\&|SYN\rPID|Alpha^Beta\r"
```

Enable Edit Mode, collapse the selection at `Alpha`, focus the editor's TextArea
and send `window.KeyTextInput("X")`. The first insertion throws before the
debounced parser runs. Both embedded and docked inspection compositions failed;
a file-backed document in the production DockControl layout with Navigator, Field
Inspector and Activity Log also failed. The captured pre-fix exception is:

```text
System.InvalidOperationException: No undo group should be open at this point
   at AvaloniaEdit.Document.UndoStack.ThrowIfUndoGroupOpen()
   at AvaloniaEdit.Document.UndoStack.ClearAll()
   at HL7Workbench.Desktop.Viewer.MessageInspectionView.WorkingTextChanged(Object sender, EventArgs e)
      in MessageInspectionView.cs:126 (pre-fix source)
   at AvaloniaEdit.TextEditor.OnTextChanged(EventArgs e)
   at AvaloniaEdit.TextEditor.OnTextChanged(Object sender, EventArgs e)
```

The failure is an editor transaction invariant, not evidence of a parser race.
`TextChanged` is raised while AvaloniaEdit may still own its input undo group.
Stage 4 called `UndoStack.ClearAll()` from that notification to avoid retaining a
second history. Clearing an open undo group throws, and the exception propagates
through the input event into the UI dispatcher.

Inspection of the surrounding path found synchronous working-state publication,
immediate invalidation of old structural projections and version-checked background
parsing. The reproduction reaches the stack clear during the input transaction,
without requiring background parsing, layout races or cross-thread UI changes.

## Fix

For a document's complete editable pane, set native `UndoStack.SizeLimit = 0`
before enabling input. AvaloniaEdit can finish its own input transactions while
retaining no duplicate native undo history. Remove the explicit stack clears from
`WorkingTextChanged` and document-to-editor synchronization. Workspace's existing
bounded delta history continues to implement Undo/Redo across navigation and view
reconstruction. Parser timing, Core, byte preservation, source-backed presentations,
document Edit Mode and source-viewer ownership are unchanged.

No exception suppression or new catch block was added. No new Activity Log data
or logging path was introduced.

## Why the earlier editing tests missed it

The three Stage 4 tests in `EditableViewerTests` supplied async lambdas without a
return value to `HeadlessUnitTestSession.Dispatch`. Its asynchronous overload
requires `Func<Task<T>>`; those lambdas instead selected `Action` and became
`async void`. The runner did not await their completion or reliably observe their
exceptions. Each callback now returns a result, so the runner awaits `Task<bool>`.
Assertions after parser refresh, navigation and undo now actually execute.

Corrected those tests' previously unexecuted setup/assumptions as well: explicitly
collapse the initial editor selection before insertion, test caret field selection
separately from deepest structural resolution, and count the two application
colorizers without counting AvaloniaEdit's attached internal transformer.

## Focused regression

`RawEditingCrashTests.PlatformInputCompletesEditorTransactionsAndRetainsDocumentHistory`
uses platform-routed text/key input and a fully awaited callback. It covers:

- First insertion into a synthetic ordinary message, both embedded/docked panes.
- Immediate working-state publication and clearing of old inspection ranges.
- Backspace, selected-text replacement, Ctrl+V paste and Delete.
- Rapid typing, Ctrl+Z/Ctrl+Y and document-owned history with empty native history.
- Fresh component offsets and selection after reparsing, and read-only refusal
  when Edit Mode is turned off.
- A file-backed document in the production DockControl with both structural tools
  and Activity Log attached, including their post-edit projections.

All key-command scenarios run in an isolated test process on one headless UI thread. AvaloniaEdit's default
KeyBinding objects are process-wide Avalonia objects; using them across separate
headless dispatchers produces a test-only ownership error. Production has one UI
dispatcher. Isolation also prevents earlier headless cases from establishing those
bindings on an already-disposed dispatcher. The regression does not modify or bypass
production key bindings; child failures and exception traces fail the parent test.

## Verification (2026-10-15)

- Locked restore: passed.
- Release build: passed, zero warnings and zero errors.
- Complete suite: 519 passed, zero failed/skipped (211 Core, 226 Workspace, 82 Desktop).
- Workspace stress suite with `HL7WORKBENCH_STRESS=1`: 228 passed, including existing
  256/512 MiB cases; zero failed/skipped.
- `git diff --check`: passed.
- No commit, amend, push, tag or release was performed during this fix verification;
  native Windows acceptance was pending at that point.

## Native Windows retest

Run the Release app with synthetic fixtures only:

```powershell
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

1. Open a small synthetic file containing the example message. Confirm it starts
   read-only, enable Edit Mode, click within `Alpha` and type a character. Verify
   the application stays open and the modified indicator appears.
2. Type quickly; use Backspace/Delete; select a value and replace it; paste text
   with Ctrl+V. Repeat while Navigator, Field Inspector and Activity Log are open.
3. After refresh, check updated value/path/offsets through Field Inspector, raw
   hover and double-click navigation. Selection and caret movement must remain usable.
4. Undo/redo through toolbar and Ctrl+Z/Ctrl+Y/Ctrl+Shift+Z. Navigate messages and
   switch documents. Verify edits/history survive, and each document keeps its own
   Edit Mode. Turn Edit Mode off: retain the draft and refuse further input.
5. Open a large synthetic attachment message. It must remain source-backed and
   read-only with its existing explanation, abbreviation and Full Source Viewer.
   Edit an ordinary adjacent message and repeat typing/undo checks.
6. Save to a synthetic copy and reopen it. Close dirty tabs using Cancel and verify
   the application remains usable. Repeat with floating structural tools if used.

These tests reproduce the editor exception through real control input, but do not
replace native Windows keyboard/IME/clipboard/window-manager acceptance. The user
subsequently granted native acceptance, including the raw-editing crash fix, and
authorized final verification and one local milestone commit. No push, tag,
publication or release is authorized.
