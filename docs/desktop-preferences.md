# Desktop menus, ribbon and Preferences

The permanent menu bar is **File | Settings | View**. Scripts and Window dropdowns
are removed. View retains Message Browser, Activity Log, Segment Navigator, Field
Inspector, Script Editor and Full Source Viewer. File → Save/Save As still save HL7
documents. Script saving is explicitly Save Script in the editor and Save in the ribbon.
View → Script Editor and Scripts ribbon → Open Script Editor reveal/focus the retained
dockable/floating tool. The Scripts ribbon and editor use the same commands and model.
Home → Workspace provides Save Layout, Restore Layout and Reset Layout / Recover Windows.
All ten former Scripts menu commands are available in the Scripts ribbon, even when
the editor is hidden; scope and Reports / Export CSV remain there too.

The utility ribbon starts collapsed, with Home, Scripts, HL7 Tools and TCP/MLLP tabs.
A selected tab opens compact grouped commands in a light-dismiss overlay, preserving
the dock viewport. Click outside or press Escape to dismiss. Left/Right/Home/End select
tabs; Down enters commands; Tab navigates controls. Pin ribbon persists expansion;
Unpin persists collapse. The selected tab stays the same when switching modes; it is
session-only and starts at Home on restart. Pinned expansion deliberately uses workspace
height. Horizontal scrolling keeps command groups accessible in a narrow main window.
TCP/MLLP Sender and Listener are disabled and labeled Planned; no networking is added.

Ribbon correction: the tab/pin strip is excluded from overlay dismissal, so another
tab changes content on its first click and the selected tab toggles temporary content.
A stable popup host switches commands without closing/reopening. Fluent resources bind
dynamically to the attached window's dark/light variant, including changes while open.
Script Library, Execution and Results use aligned icon controls, subtle dividers and an
accent Run tile. Standard Fluent control themes retain hover/press/focus/disabled states.

Settings → Preferences opens a dedicated window with General/HL7 category navigation.
Apply saves without closing; OK saves and closes only on success; Cancel/close abandons
changes since the last Apply. General contains two fully functional preferences:

- Open documents in Edit Mode by default: Off for new installations. Only subsequently
  opened documents use the preference. Existing documents retain mode, drafts and undo;
  all existing edit eligibility and save protections apply. The existing Settings →
  Start in Edit Mode checkbox uses the same state.
- Ribbon visibility: Collapsed by default, or Expanded. Pin/unpin and Preferences use
  the same persisted value. A failed save retains the previous active preference.

## Declared version and future schema choices

| Concept | Meaning | Current capability |
| --- | --- | --- |
| Declared HL7 version | Each message's original MSH-12 | Read from Core's structural message model |
| Selected target schema | A user's independent target for inspection, potentially different from MSH-12 | Reserved metadata; no supplied schema catalog |
| Interpretation mode | Use message-declared schema or selected target schema | Reserved enum; both choices shown in a disabled control |

Inspection uses the lossless delimiter-based Core scanner/parser, with structural/
encoding diagnostics. It has no target-schema interpreter, profile validator or
version-conflict highlighting. The HL7 category explains the distinction and disables
schema controls. It does not invent supported versions or imply validation is active.
SchemaMode and nullable TargetSchema are future settings metadata only; no current
processing path consumes them. A future interpreter may intentionally inspect mixed-
declaration batches against a target; it must never implicitly rewrite MSH-12,
normalize values or alter original bytes.

## Persistence and failure handling

General/schema metadata use the existing per-user
`%LOCALAPPDATA%\HL7Workbench\editor-preferences.json`, separate from Scripts,
workspace-layout.json and script-editor-preferences.json (code font preferences).
Old files containing only StartInEditMode retain it; missing fields use defaults.
Unknown JSON fields in a valid record survive subsequent saves. Unknown enum values
fall back to declared mode. Missing files cause no write; malformed/oversized records
use safe defaults with fixed feedback and remain untouched until explicit Apply/pin.

Records remain bounded to 4 KiB. Save writes and flushes a unique same-directory
temporary file, then atomically moves over the destination. Shared in-memory state
changes only after saving succeeds. Failure retains the prior state and removes the
temporary file. Preferences contain no message/script source or report rows; errors
do not log paths or imported exception text. Separate app instances retain the existing
last-writer-wins preference behavior, without cross-instance merging.

## Script management and reports

The editor has two horizontally scrollable toolbar rows. Management contains library,
click-to-rename name, New, Save Script and ⋯. Execution contains Apply to:, scope,
Run, Cancel and compact state; full feedback/zoom remain in the footer. Duplicate,
Rename, Delete Script, Review Output and Reports / Export CSV are in ⋯ and the Scripts
ribbon. There is no permanent report selector/export or Duplicate button.

Click the current name or invoke Rename Script: Enter or valid focus loss confirms
the draft name, Escape cancels. Invalid names retain the previous name and show feedback.
Names use the existing 1–120-character nonblank/no-control-character rule. Confirmation
does not save code implicitly: Save Script persists name and code together, preserving
identity and unsaved content. Bundled examples require Duplicate before editing.

Delete applies only to saved user records. Dirty scripts offer Save/Discard/Cancel;
then a dialog identifies the saved script and requires explicit Delete Script. Discard
is deferred until deletion succeeds: canceling confirmation, a newer draft, external
record changes or I/O failure retain the draft. Deletion uses the library lock/revision
check and selects the bundled simple example as a predictable fallback. It never deletes
output documents/exports or relabels retained reports. Obsolete records keep their
existing upgrade protections, including while being renamed or duplicated.

Reports / Export CSV opens a contextual window listing names, counts and byte sizes
from the last successful run, with producing name/scope attribution. Export selected
CSV uses the existing picker and version-checked transactional replacement only after
explicit user authorization; unauthorized paths remain creation-only. The journal is retained and
Run/management are blocked throughout picking/export, preventing replacement with a
different run's identically named report. Switching, renaming or deleting scripts keeps
reports/origin. Failure/cancellation keeps them for retry; later successful runs replace
or clear them. There is no report viewer/history or new script filesystem API.

See [native acceptance](stage5b-ui-finishing-acceptance.md) and [script API](script-api.md).
