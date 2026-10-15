# Stage 3 Windows acceptance fixes

The initial Stage 3 native Windows pass did **not** pass. The user subsequently
confirmed these fixes passed native acceptance when authorizing Activity Log polish.
The complete milestone received explicit native Windows acceptance on October 14,
2026; see `stage3-workspace-layout-acceptance.md` for the final layout and verification
record. One local milestone commit is authorized; push, publication, tagging and
release are not authorized.

## Blank docked viewer: reproduced cause and fix

`DockContentTemplate.Build` previously dispatched once on `dockable.Context`.
Optional tools start with null Context and replace their model after each close.
Dock can retain the same dockable/template control across those replacements. That
leaves a cached empty control or a `FullRawInspectorHost` bound to the disposed old
model. Floating rebuilds the template against the current Context, explaining why
source content appeared after a docking transition.

The new deterministic regression builds/attaches a template while Context is null,
then replaces Context without reattachment. It failed before the fix with no source
view in the visual tree. It now passes through repeated open/null/reopen cycles,
including populated bounded editor content and nonzero measured view bounds.

`DockToolContentHost` now observes Context changes for optional/releasable tools,
detaches old feature content and constructs a host for the current model. It also
reads the current Context when attached and removes subscriptions/content on detach.
`FullRawInspectorHost` continues to observe session changes within the same model.
Source loading still begins on actual visual attachment, uses the document read
gate, and cancels/version-checks work on replacement or detachment. Editor measurement
is handled by normal Avalonia layout. No arbitrary delay, forced floating, duplicate
snapshot, eager viewer creation or docking-framework modification is used.

Model/headless tests exercise actual Dock activation/reopening/redocking and feature
reattachment in a second headless visual root. Native window creation, drag gestures
and OS rendering still require the checklist below; the fake host cannot certify them.

## Naming and scope commands

All user-facing references now use **Full Source Viewer**, including the tab, tool
header, View menu, documentation and accessibility names. Internal class names and
the stable `tool.full-raw-inspector` ID remain for compatibility with saved layouts.

- **View Full Value** opens only the original structural value, labels its HL7 path,
  and starts at that value's beginning. Chunk offsets are relative to that scope.
- **View Full Message Source** opens the entire indexed message payload, including
  every segment, from message offset zero. It does not reuse the selected-value range.

Both actions replace the existing session when the tool is open. Replacement cancels
old reads, disposes the old session and refreshes the header/editor. The header names
the active scope/message and its original message-relative UTF-16 range; status shows
scope/message positions, lengths and original byte intervals. Chunk size remains
16 Ki UTF-16 units. Original-value export retains exact snapshot bytes and excludes
neighboring delimiters, markers and attachment decoding. Existing encoding, framing,
source-ownership and export contracts are unchanged.

## Default tools and value navigation

A fresh/reset workspace opens **Segment Navigator and Field Inspector as bottom
tool tabs**, alongside Diagnostics. Full Source Viewer remains hidden with no feature
model, editor, session or source-window buffers until requested.

When restoring an older layout, absent Navigator/Field Inspector identities are added
to an available bottom tool group. A layout that explicitly hides either identity
continues to hide it. Existing tab groups/geometry and the layout schema/path are
preserved; no saved layout file is deleted or forcibly reset.

The Field Inspector hides its entire chunk-navigation row for no selection, missing
values, and values of **4,096 UTF-16 units or fewer**. Larger values retain First,
Previous, Next, Last and direct-offset access, labeled **Selected value chunks**.
The row remains available on the last chunk of a large value so earlier content
stays reachable. Position feedback reports the selected value's offset and total length.

Tooltips now distinguish message navigation, field-number lookup, selected-value
chunks, Full Source Viewer scope navigation, original-byte export, source-region
byte pages, source-view commands and application-owned layout/menu controls. No
third-party Dock templates were modified merely to add tooltips.

## Verification

- Complete suite: **451 passed**, zero failed/skipped (Core 216, Workspace 178,
  Desktop 57), retaining the original 356-case baseline.
- Optional Workspace stress suite: **180 passed**, including 256/512 MiB attachments.
- Locked restore and Release build passed with **zero warnings and zero errors**.
- `git diff --check` passed.
- Five focused acceptance regressions cover cached null/replaced contexts,
  production open order, close/reopen, headless floating/reattachment/redocking,
  repeated independent value/message commands, default/migrated/explicitly closed
  tools, short/large/missing value rows, bounded previews and tooltip presence.

Initial verification used the isolated ignored `artifacts/refinement-build` output
because the running manually tested application locked the standard Release executable.
After that application was closed, the standard locked restore and Release build
also passed with zero warnings/errors, and all 451 tests passed against the standard
output with zero failures/skips. The standard Release executable is ready for native
acceptance. No tests or build warnings were suppressed.

## Preceding native Windows checklist — passed before Activity Log polish

The standard Release output has been rebuilt and verified. Launch with:

```powershell
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

1. Use Reset Layout (or start without saved layout). Confirm Navigator and Field
   Inspector tabs are open; select each to see its content. Full Source Viewer must
   remain closed. Restore a layout with Field Inspector explicitly closed and verify
   that choice is respected; a legacy layout lacking its identity must introduce it.
2. Open `artifacts/stage3/synthetic-100MiB-104857600.hl7`. With the viewer docked,
   right-click the OBX-5 omission marker → **View Full Value**. Header, path, range,
   original text and navigation must appear immediately without a floating transition.
3. Move between chunks and jump to the last eight value characters (`VEFJTA==`).
   Switch to **View Full Message Source**: it must start at MSH and allow navigation
   through OBX-6 and the following NTE. Repeat both commands several times while open.
4. Float, resize and redock Full Source Viewer; verify source content throughout.
   Close/reopen while docked repeatedly, including while another tool tab is selected.
   Check View → Full Source Viewer and an ordinary single-message file too.
5. Select short, empty, missing and exactly 4,096-unit values: no value-chunk row.
   Select enormous OBX/NTE/component values: the row returns with meaningful totals.
   Exercise all chunk buttons and offsets; their tooltips must identify value chunks.
6. Verify message buttons describe messages, field lookup describes the selected
   segment, and viewer/export/layout actions describe their actual scopes. Check
   consistent Full Source Viewer naming and useful accessibility labels.
7. Export both a value and a whole message payload and compare original bytes/ranges.
   Repeat custom-delimiter and UTF-8/16/32 cases. No markers or decoded PDF/image data
   should enter output; cancellation and original-file protection must still work.
8. Rapidly change scopes/messages, close/reopen tools, close the bound document and
   switch other documents. No stale content or labels may appear. Bound message/
   document closure must close the viewer; reopening must retain preferred placement.
9. Save/restart with docked and floating placements. Full Source Viewer must remain
   uninitialized until explicitly opened, then show current requested source. Check
   mixed DPI, monitor recovery and shutdown with floating tools as before.

Native acceptance is now explicitly granted for the complete milestone. The starting
parent is `3e0f9890a9eeadf2e5d733165309f0554ccc232c`; one local Stage 3 commit is
authorized. No push, publication, tag or release is authorized.
