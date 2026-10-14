# Milestone 2, Stage 2: Segment Navigator and Field Inspector

Stage 1 remains manually accepted. The user confirmed all ten original Stage 2
categories passed on 2026-10-14, followed by eight of nine UX refinement checks.
The remaining requirement has been clarified: **hover tooltips belong to the
upper RAW HL7 editor, not Field Inspector paths**. The existing inspector tips
worked natively; the earlier tests were performed over raw text. Diagnostic
reports identifying AvaloniaEdit TextView agree with that misunderstanding.
No inspector-native root cause was established. **Viewer Stage 2 is formally
accepted as of October 14, 2026.** The user authorized final verification and
commit/push to main. No additional features, multi-message navigation or Viewer
Stage 3 are authorized.

## Final native Windows acceptance — PASSED (October 14, 2026)

| Check | Result |
| --- | --- |
| Raw editor hover tooltips | PASSED |
| Structural paths, including repetitions, subcomponents and custom delimiters | PASSED |
| MSH numbering and edge cases | PASSED |
| Tooltip behavior, dismissal and selection/caret preservation | PASSED |
| Double-click navigation regression | PASSED |
| General regression: copying, tabs and docking | PASSED |

All six checks passed in the native Windows application. This completes the
Stage 2 manual acceptance gate, including the corrected RAW editor hover target.

## Architecture and current changes

- Workspace `MessageInspection` owns ordered segment entries, lazy canonical
  field/repetition/component/subcomponent projections, ranges and document selection.
  Core supplies all parsed structure and UTF-16 character offsets. Core is unchanged.
- `ResolveOffset` is a non-mutating raw-character lookup shared with double-click
  resolution. Binary searches find segments, fields and nested values. Field
  projections are cached lazily per visited segment; nested projections reuse existing
  caches. Hover neither reparses text nor rebuilds the inspector tree.
- Desktop `MessageInspectionView` uses ordinary document-internal grids: raw editor
  above approximately 65/35, Segment Navigator/Field Inspector below approximately
  25/75, with draggable horizontal and vertical splitters. These are not Dock tools.
- AvaloniaEdit TextView PointerHover/PointerHoverStopped events supply hover timing.
  GetPositionFloor maps the glyph under the pointer to a document offset, adding
  the current scroll offset. Nearest-caret rounding is avoided because it can report
  a neighboring value on the right half of a glyph or delimiter.
- The TextView owns a conventional Avalonia ToolTip placed near the pointer.
  Desktop opens/closes it using coordinate-specific editor hover events; automatic
  control-level tooltip timing is disabled locally on that TextView. No custom
  popup, dependency, global style or standards catalog is introduced.
- `InspectionPathToolTip` formats canonical path, segment/occurrence, field and
  applicable repetition/component/subcomponent indices only. No raw values, value
  states, semantic names or version-specific descriptions appear in the tip.
- Inspector tooltip bindings and selected-detail tips are removed. Temporary
  Tooltip Test Button, diagnostic panel/probe, F8 handling and their tests are removed.
  Obsolete inspector-hover tests are replaced with raw-editor hover tests.

Hover never selects nodes, moves the caret, scrolls the editor, expands tree rows,
or replaces text selection. Tips close on hover stop, pointer exit, press, scrolling
and view detach. Handlers unsubscribe on detach and subscribe on reconstruction.
Tests use production styles and Dock document templates, but headless results do
not certify native Windows visibility.

## Structure, selection and boundary rules

Navigator entries preserve Core order, repeated/unknown identifiers and malformed
or blank lines. Labels include sequence and identifier occurrence. Segment selection
highlights its raw range without the terminator. Inspector selection highlights exact
Core field/nested ranges. Document selection survives tab/view changes and is not
serialized in layout JSON.

`InspectionNode.Children` retains canonical implicit wrappers. `VisibleChildren`
suppresses singleton wrappers not introduced by actual Core delimiter splits.
A simple value is a field leaf; `SMITH^JOHN` exposes components directly; actual
repetitions and subcomponents retain meaningful levels. Empty siblings remain
visible when delimiters create them. Escaped delimiters and literal MSH encoding
characters never create structure independently of Core.

Compact inspector labels can omit implicit repetition `[1]`, e.g. `PID[1]-5.2`.
Raw hover reports Core's canonical spelling `PID[1]-5[1].2`. Segment occurrence
is explicit even for occurrence 1, so repeated segments are unambiguous.

Raw hover uses half-open value ranges:

- Values resolve to the deepest meaningful delimiter-created node. A simple OBX
  value stays `OBX[occurrence]-N`, without fabricated singleton component levels.
- Component, repetition and subcomponent separators resolve to their containing
  structural parent, not a neighboring value. Escaped separators remain inside
  their parsed value according to Core.
- Ordinary field separators, IDs, line endings, opaque malformed content,
  outside-document positions and empty canvas past line ends show no tip.
- Empty fields/components have no glyph to hover; no empty or missing path is
  invented on an adjacent separator. Use inspector lookup for absent fields.
- MSH-1 is the special literal separator field. Every character of MSH-2 resolves
  to that literal encoding field. MSH-3 and subsequent fields keep correct numbering.
- Whitespace inside a parsed field belongs to its value. Blank lines and unused
  editor space have no path. Explicit-null quotes remain literal value characters;
  value state is available in the inspector.
- Custom and provisional delimiter behavior follows Core. Unknown versions and
  custom segments remain inspectable; no semantic validation is invented.

Double-click keeps its accepted behavior: select the deepest meaningful node,
highlight its raw range, expand only its visible ancestor branch and reveal the
tree row. Separators with no value select segment context on double-click, while
hover gives no tip. Ordinary caret movement updates field context without replacing
multi-line selection. Single-click, dragging, clipboard and read-only protections
remain in place.

`Empty`, `ExplicitNull`, `Missing` and `Value` remain distinct in the inspector.
Enter a positive field number and choose **Inspect field** for an absent field:
it has no fabricated raw range. Opaque segments disable lookup. Provisional structure
directs the user to Diagnostics. Neither source text nor bytes change.

## Automated verification

Current validation: locked restore passed; **356 tests passed, zero failed/skipped**;
Release build succeeded with **zero warnings/errors**; `git diff --check` passed.
The total replaces obsolete inspector/diagnostic hover cases with raw-offset and
RAW editor hover coverage. Native Windows acceptance passed on October 14, 2026.

Run locked restore, Release build, the full suite and `git diff --check` as documented
in PROJECT_CONTEXT. Existing parser, docking, selection, copy and read-only tests
remain included. New synthetic tests cover:

- Non-mutating raw-offset lookup, stable cached identities, repeated occurrences,
  repetitions/subcomponents, MSH literals/indexing and custom delimiters.
- Empty/missing boundaries, explicit nulls, whitespace, escapes, malformed input,
  mixed terminators, large repeated messages and document isolation.
- Actual raw-glyph hover through production App styles and Dock, before/after
  reconstruction; no caret, selection or source mutation; coordinate-only tips.
- Right-half glyph/separator hit-testing, empty fields, past-line space, scrolling,
  dismissal and detached-view cleanup. Double-click and splitter tests remain.

## Known limits and future work

- One bounded Core parse per file. Combined-message toolbar/navigation is deferred.
  Prioritize combined single-file batches over directory processing. Its future
  fixed toolbar belongs at the bottom of the raw-message pane: first, previous,
  next, last and message position/count.
- Structural parsing stays version-independent; future version/profile definitions
  remain a separate enrichment service. No standards catalog is implemented.
- Empty ranges cannot be hovered or colored. Missing fields have no raw coordinate.
- After scrolling, move and pause over the desired raw glyph for a fresh tip.
- Splitter sizes and unrelated expanded branches are not persisted across view
  reconstruction/restart. Selected segment/value is session-only and restored.
- Native Windows hover/placement and behavior passed manual acceptance. Supplemental
  monitor/DPI configurations remain platform-specific; automated hover tests use
  Avalonia's headless backend.
- No editing, saving, batch navigation, scripting, transport or Stage 3 is included.

## Native RAW editor acceptance procedure (completed)

Close old application instances. Launch the updated Release executable:

```powershell
& 'C:\Dev\HL7Workbench\src\HL7Workbench.Desktop\bin\Release\net10.0\HL7Workbench.Desktop.exe'
```

The temporary Button and diagnostic panel must be gone. For these hover checks,
**point directly over characters in the upper RAW HL7 editor**, not the lower
Field Inspector. Pause for about one second without clicking.

1. Open `samples/synthetic-only/adt-a01.hl7`. Hover `Example` then `Avery` in raw
   PID-5. Expect `PID[1]-5[1].1` and `PID[1]-5[1].2`, with structural indices only.
   Hover raw `Exampletown` in PID-11 for `PID[1]-11[1].3`.
2. Open `oru-r01.hl7`. Hover raw OBX values across repeated OBX segments. Check
   occurrence-qualified paths and field numbers. Hover nested raw PID-3 values
   to check real repetitions and assigning-authority subcomponents.
3. Open `oru-custom.hl7`. Hover RAW values separated by `$`, `%` and `@`, and
   MSH's field separator/encoding characters. Check custom structure, literal
   MSH-1/MSH-2 and MSH-3 numbering. Hover separator glyphs, including their right
   halves, to verify the containing-parent/no-tip rules.
4. Open a temporary synthetic file with `PID|||||SMITH^JOHN~JONES^MARY&ANN`.
   Hover raw `SMITH`, `JOHN`, `JONES` and `ANN`. Expect components 1/2 in repetition
   1, component 1 in repetition 2, and repetition 2/component 2/subcomponent 2.
5. Try `PID||""|A^&~`, blank lines, a malformed line and whitespace within values.
   Hover empty-field separators, ordinary field separators, line ends and unused
   canvas: no invented path. Hover quotes/whitespace inside actual values: their
   parsed path appears. MSH literal rules remain the exception.
6. Keep a multi-line raw selection or selected inspector node, then hover other RAW
   values. Selection, caret, expansion and scroll must not change. Move away or
   scroll: tips close. Re-hover after horizontal/vertical scrolling.
7. Collapse PID-5 and double-click raw `Avery`, then raw `Exampletown`. Preserve
   expansion, range highlight and tree scrolling. Inspector rows/detail have no
   path tips. Special states and missing-field lookup still work in the inspector.
8. Switch tabs, Save/Restore/Reset Layout, reopen files and test RAW hover again.
   Check splitters, Dock floating tools, copying, closing and shutdown. Typing,
   paste, cut, Delete, Backspace and drops must not modify either read-only pane.

The user completed native testing and accepted Stage 2 on October 14, 2026, with
all six final checks above passing. Retain these steps for future regression testing.
Stop after the authorized final verification and commit/push; do not begin Stage 3.
