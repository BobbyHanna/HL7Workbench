# Navigate and arrange the workspace

Use document tabs for HL7 content and dockable tools for inspection, analysis and
testing. Prerequisite: a running application; an HL7 document is needed only for
actions that operate on messages. Open tools through **View** or the ribbon.

## Inspect a document and arrange tools

1. Activate a document tab. **View → HL7 Inspection** opens Segment Navigator,
   Field Inspector, Message Browser, Full Source Viewer or Find Messages.
2. Use the raw editor's **First / Previous / Next / Last**, message-number input
   and **Go** to move through the file. Enter in the number input also goes to
   that logical message. A search filter changes navigation targets, not file contents.
3. Drag a tool header/tab to dock or float it; resize Dock splitters to give the
   editor or result table more room. Focusing a tool normally retains the active
   HL7 document; an explicit source selector may choose a different source for that tool.
4. Click **Home**, **Scripts**, **HL7 Tools** or **TCP/MLLP** above the workspace.
   The collapsed ribbon opens temporary commands; click outside or press Escape
   to dismiss. **Pin ribbon** retains expansion; **Unpin ribbon** saves collapse.
5. Use **Home → Workspace → Save Layout** to retain placement. Closing/reopening
   a tool and restoring a layout are different from saving its data or result.

Expected result: tools move around the same document model. Tools with captured
results, explicit source sessions or their own source controls follow the rules in
their guides; they do not all retarget when you click another tab.

## Recover the workspace

1. Reopen a hidden tool through **View** or its ribbon command.
2. Use **Home → Workspace → Restore Layout** for saved placement.
3. Use **Home → Workspace → Reset Layout / Recover Windows** when tools are
   off-screen or the arrangement is unusable. Optional tool sessions may close;
   export/save valuable work first and heed dirty-script/query prompts.

Fresh/reset placement gives documents most of the area, inspection tools on the
right and Activity Log at the bottom. Message Browser is available but hidden by
default. Existing saved geometry takes precedence over defaults.

Layouts contain placement/geometry, not message contents, file reopening, scripts,
imported FHIR data, results or network authorization. Reset does not recover unsaved
data. A failed layout save retains feedback; keep a backup of metadata before
deliberate repair, rather than deleting the whole settings directory.

## Activity and help

**View → Activity Log** shows bounded session operations and diagnostics with
fixed/count-only text; individual tool status/result panes contain more specific
outcomes. **Help → Script API Reference** opens offline scripting help. **Help →
About Message Workbench… → Data handling and privacy** summarizes trust boundaries.
**Help → Documentation** opens these user guides and shared limits/storage references
offline, even with no document open. Use Contents, local keyword search, topic headings
and Back; select text or use Copy code without executing an example. The tool can
dock/float/reopen. Close/reset/restart clears its session navigation, while saved layouts
retain placement only. Links to bundled topics navigate locally; Script API links
open the existing reference. Other references are labelled repository/external and
show their location without reading files or opening a network connection.

See [layout behavior and the Full Source Viewer limitation](../stage3-workspace-layout-acceptance.md),
[responsive tools](../responsive-tool-layout.md) and [preferences](preferences-and-shortcuts.md).
