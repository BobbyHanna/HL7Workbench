# Stage 3 final workspace layout polish

Native Windows acceptance was explicitly granted on October 14, 2026, for the complete
Stage 3 milestone, including Activity Log and final workspace refinements. The user
authorized verification and one local milestone commit. No push, publication, tag or
release is authorized. No Stage 4 engine,
placeholder command, transport feature, crash reporting or new docking architecture
is introduced.

## New default arrangement

`WorkspaceDockFactory.CreateLayout` now uses a vertical proportional split with
an upper content area (73%) and bottom Activity Log (27%). The upper area splits
documents (78%) from a right tool column (22%). Segment Navigator occupies the upper
half of that column and Field Inspector the lower half, in independent tool docks.
These are initial relative proportions, without fixed panel pixel sizes. All normal
Dock resizing, floating, redocking, closing and tab grouping remains available.

Activity Log is visible in a fresh/reset workspace. Message Browser is hidden, with
its registered stable identity and View-menu command retained for saved layouts and
explicit opening. Full Source Viewer remains optional, hidden and uninitialized.
Document tabs and the fixed message navigation controls are unchanged.

The shell's existing `Auto,*,Auto` grid still gives command rows their natural height
and the workspace the remaining area. No toolbar height assumptions, toolbar redesign
or future-feature placeholders are added.

## Saved layouts and Activity Log lifecycle

The new arrangement applies only when no valid layout is available or the user invokes
Reset Layout. Existing valid layouts are reconstructed from their saved graph,
preserving tab groups/order, proportions, floating positions and hidden preferences.
An older visible Message Browser remains visible; an explicitly hidden Activity Log
remains hidden, without constructing its feature model. A visible Activity Log,
including a floating one, is restored in place.

`ToolDefinition.RestoreVisibility` separates optional content creation from saved
visibility. Activity Log opts in; Full Source Viewer does not. Thus Activity Log can
release its projection on close while honoring visible/hidden saved state. Its
session-only, PHI-safe service, 2,000-entry retention, filtering, clear/copy and
source-free metadata remain unchanged. Closing/reopening retains current-session
history; restarting begins fresh history. There is no disk logging or crash sink.

The layout path, version-2 schema, version-1 compatibility, all tool IDs (including
the Diagnostics/Activity Log alias), closed-placement recovery and structural-tool
migration are retained. Saved layout files are neither deleted nor rewritten to force
the defaults. Save Layout continues to store a user's current arrangement explicitly;
normal application shutdown retains the existing save behavior.

## Explicit Full Source Viewer context — verified behavior and discrepancy

Full Source Viewer is explicitly opened for a source and scope, rather than following
the active document. Switching document tabs does not retarget or close it. Explicitly
requesting another source/scope replaces the session. Closing the owner document
invalidates it; closing the tool releases the session and its view resources. No Pin
or Follow control is added, and no source content is restored from layout metadata.

**Same-file message navigation currently closes the session.** This differs from the
full snapshot-inspector behavior described in the layout request. `RawInspectorSession`
subscribes to its owner's `ActiveContent` change and disposes; its reads/export borrow
the document's current message resources. The existing Workspace regression explicitly
asserts this invalidation. Maintaining the original message across same-file navigation
would require changing source ownership/lifetime, not just a Dock preference.

After this discrepancy was reported, the user explicitly chose **“Leave source code
unchanged; document the discrepancy.”** The source-backed reading/export implementation
and original memory constraints are therefore unchanged. Retention across same-file
message changes is deferred; it must not be claimed as implemented or tested. The
explicit-context design should remain intentional in future development, without
silently turning the viewer into a live active-document mirror.

## Verification

- Locked dependency restore passed.
- Release build passed with **zero warnings and zero errors**.
- Complete suite: **478 passed**, zero failed/skipped (Core 216, Workspace 189,
  Desktop 73). All 470 preceding cases remain, with eight additional layout/lifecycle
  cases and focused updates to scenarios whose default arrangement changed.
- Workspace stress suite: **191 passed**, zero failed/skipped, including generated
  256/512 MiB attachments and the existing allocation/retained-memory constraints.
- `git diff --check` passed. The accepted milestone's parent is
  `3e0f9890a9eeadf2e5d733165309f0554ccc232c` on `main`. One local commit is authorized
  with subject `feat: complete Stage 3 multi-message workspace and source inspection`.
  See local Git history for the resulting commit identity.

Tests cover the proportional default graph, positive rendered geometry at two window
sizes, independent tools sharing selection, visible Activity Log, hidden Message Browser,
unchanged lazy source tool, version-1/2 hidden preferences and legacy left-browser/
bottom-tab arrangements, customized tab order/floating bounds, reset behavior,
duplicate-tool prevention and the actual Full Source Viewer lifecycle above.
Earlier tests that explicitly require two visible Browser/Diagnostics tools now open
Browser in their setup; tab-group scenarios explicitly arrange those tabs. Their
geometry, lifecycle, synchronization and persistence assertions remain in place.

Implementation changes are limited to Desktop `WorkspaceDockFactory` and
`MainWindow.axaml.cs`, Workspace `ToolWindowRegistry`, the new
`DefaultWorkspaceLayoutTests`, and focused fixture/assertion updates in
`DockingTests`, `RefinedDockingTests` and `ActivityLogViewTests`. Context, architecture,
docking ADR and Stage 3 acceptance/performance documents describe the revised policy.
Core, source-backed reading/export, Activity Log retention/privacy code and the
shell's toolbar layout were not changed in this pass.

## Native Windows acceptance checklist — explicitly accepted October 14, 2026

Launch the verified Release output with the opt-out variables from `PROJECT_CONTEXT.md`:

```powershell
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

1. Start with the existing saved layout. Confirm its groups, visibility, proportions
   and floating geometry are preserved. A saved closed Activity Log must remain closed;
   an old visible Message Browser must remain available in its saved group.
2. Use Window → Reset Layout / Recover Windows. Confirm a wide document region on
   the left, Navigator upper-right, Field Inspector lower-right and Activity Log
   across the bottom. Message Browser and Full Source Viewer must be hidden.
3. Resize the window and panel splitters at normal and mixed DPI. Verify both right
   tools remain usable and independent; float/redock/close/reopen each tool. Change
   tab groups, save/restart and verify those choices survive rather than defaulting.
4. Open synthetic ordinary and large-attachment files. Verify highlighting, raw
   hover/double-click, shared structural selection, bounded value previews, document
   tabs and message navigation remain functional in the new arrangement.
5. Verify Activity Log's session history, real warning/error indicator, severity
   filters, clear/copy, tail following and older-entry review. Close it, perform
   operations, reopen and confirm retained history and remembered placement.
6. Open Full Source Viewer for one message/value. Switch document tabs: original
   source stays selected. Explicitly request another scope: it replaces the session.
   Navigate messages within the owner file: current code closes the viewer, per the
   clarified limitation. Close the owner or tool and verify cleanup. Check original-
   byte export and lazy restart behavior without introducing source retention.
7. Save/restart with Activity Log visible/floating, then closed. Respect each saved
   preference, with fresh session history after restart. Exercise shutdown and monitor
   recovery as before. No duplicate tools or stale source content should appear.

Headless tests establish model and visual-tree behavior. Native drag gestures,
OS-window placement, monitor recovery and mixed DPI rely on platform-specific manual
verification. Native acceptance was supplied by the user; automated results do not
independently certify every supplemental platform configuration.
