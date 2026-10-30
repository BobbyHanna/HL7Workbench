# Desktop menus, ribbon and Preferences

The permanent menu bar is **File | Settings | View | Help**. Scripts and Window dropdowns
are removed. View groups inspection, data/query, connectivity/testing and report tools,
with FHIR Patient Workspace and Activity Log also available. Help opens Script API
Reference and About/privacy. File → Save/Save As still save HL7
documents. Script saving is explicitly Save Script in the editor and Save in the ribbon.
View → Data & Query → Script Editor and Scripts ribbon → Open Script Editor reveal/focus the retained
dockable/floating tool. The Scripts ribbon and editor use the same commands and model.
Home → Workspace provides Save Layout, Restore Layout and Reset Layout / Recover Windows.
All ten former Scripts menu commands are available in the Scripts ribbon, even when
the editor is hidden; scope and Reports / Export CSV remain there too.

HL7 Tools → Schema Validation provides Apply/Change Schema, Remove Schema,
Import Schema, Manage Schemas and Previous/Next Issue (Shift+F8/F8). Application
and removal target the active message, including floating editors. Import/Manage
reuse the local profile dialog and work without an open document. The raw editor
retains compact read-only profile/counts/state status and inline diagnostics;
editing, message navigation and validation status wrap in separate layout rows.

The utility ribbon starts collapsed, with Home, Scripts, HL7 Tools and TCP/MLLP tabs.
A selected tab opens compact grouped commands in a light-dismiss overlay, preserving
the dock viewport. Click outside or press Escape to dismiss. Left/Right/Home/End select
tabs; Down enters commands; Tab navigates controls. Pin ribbon persists expansion;
Unpin persists collapse. The selected tab stays the same when switching modes; it is
session-only and starts at Home on restart. Pinned expansion deliberately uses workspace
height. Horizontal scrolling keeps command groups accessible in a narrow main window.
TCP/MLLP provides Connection Manager, MLLP Listener, Batch Transmission and Load Testing.
These are implemented explicit test workflows; opening/restoring them never starts
network activity. TLS, automatic retry/reconnect and scheduling are unsupported.
See the [current user guides](user/index.md#test-interfaces).

Ribbon correction: the tab/pin strip is excluded from overlay dismissal, so another
tab changes content on its first click and the selected tab toggles temporary content.
A stable popup host switches commands without closing/reopening. Fluent resources bind
dynamically to the attached window's dark/light variant, including changes while open.
Script Library, Execution and Results use aligned icon controls, subtle dividers and an
accent Run tile. Standard Fluent control themes retain hover/press/focus/disabled states.

Settings → Preferences opens a dedicated window with General/HL7/Appearance/Validation category navigation.
Apply saves without closing; OK saves and closes only on success; Cancel/close abandons
changes since the last Apply. General contains two fully functional preferences:

- Open documents in Edit Mode by default: Off for new installations. Only subsequently
  opened documents use the preference. Existing documents retain mode, drafts and undo;
  all existing edit eligibility and save protections apply. Start in Edit Mode is
  configured in Preferences → General. The redundant Settings
  dropdown checkbox was removed in the Stage 7 refinement; its persisted value and
  new-document startup behavior are unchanged.
- Ribbon visibility: Collapsed by default, or Expanded. Pin/unpin and Preferences use
  the same persisted value. A failed save retains the previous active preference.

Appearance has one mutually exclusive theme selector: **Follow System** (default),
**Light**, **Dark**. Apply/OK saves and changes the existing Avalonia application-wide
RequestedThemeVariant immediately; no restart is required. Follow System uses Default,
including live OS theme notifications where supported. Explicit Light/Dark overrides
the system. Main/docked/floating tools, menus, context menus, dialogs, ribbons and Find
Messages inherit the same resources. Cancel/close discards only unapplied changes.
HL7 editors resolve a shared palette from the effective variant, including source-backed
previews, virtualized blocks and Full Source Viewer. Switching themes repaints existing
visual lines without reopening/replacing documents, discarding edits, resetting history,
moving caret/selection/scroll or starting search/scripts/source reads.

## Editor and application palette foundation

The selected preference remains Follow System/Light/Dark. Avalonia resolves the effective
Light/Dark variant. Dynamic resources then resolve **Hl7EditorPalette** for the existing
materialized and source-backed colorizers. WorkbenchThemeResources/WorkbenchStyles reuse
Fluent control themes with semantic panel/surface/group/border/accent/validation resources.
Light uses a pale application canvas, white editing/result/inspection surfaces and subtly
tinted child groups. Dark keeps existing Fluent surfaces and the established HL7 canvas
and syntax accents. Hover, disabled, selected and focus states retain Fluent behavior;
errors also retain explanatory text. No replacement theme framework or packages are added.

| Supported HL7 color | Light | Dark (preserved) |
| --- | --- | --- |
| Background / default text | #FFFFFF / #20242B | #18212B / #F5F5F5 |
| Segment identifier | #005A9E | DeepSkyBlue |
| Field separator | #825600 | Gold |
| Component separator | #146B35 | LightGreen |
| Subcomponent separator | #7B287D | Plum |
| Repetition separator | #924400 | Orange |
| Escape delimiter accents | #9A3D22 | LightSalmon |
| Line numbers / caret | #626A76 / #20242B | #A0A0A0 / #F5F5F5 |
| Text selection background / foreground | #CCE3FC / #142338 | #264F78 / #F5F5F5 |
| Structural selection background | #DCEAF8 | #384A65 |
| Omission background / text | #F0EEF5 / #62596F | #32303A / #778899 |

These categories reflect actual Core-backed syntax spans. Escape delimiters are accented;
escape bodies are not separately classified/decoded. There is no separate search-highlight
layer; ordinary selection and structural selection keep their established meanings.
Current-line colors are provided if that existing option is enabled; the option is not
newly enabled. Read-only uses the same readable syntax palette; Edit Mode/status still
communicates editability. The C# Script Editor retains its existing independent Stage 5B
VS Dark palette and compiler overlays in this correction pass.

Permanent tests check Light normal/syntax/line-number text and text-selection/omission
contrast at WCAG AA's 4.5:1 threshold, rendered syntax for both delimiter sets, live state
preservation and inherited effective-variant changes. Native DPI, OS notifications,
Fluent selection/hover/focus/disabled contrast and complete window appearance still need
Windows visual acceptance.

A later milestone can supply a named/custom immutable Hl7EditorPalette through the same
WorkbenchHl7EditorPalette dynamic resource and replace semantic application resources per
Light/Dark variant. Renderers already read the resolved palette; they need no new scanner
or rendering logic. A virtualized canvas uses the corresponding WorkbenchEditorBackground
resource. Custom editor color-palette storage, color pickers and palette import/export
remain deferred. This palette extension point is distinct from the implemented
HL7 validation profile library and Preferences → Validation described below.

## Find Messages split position

Advanced has an internal horizontal splitter, approximately 60/40 initially. Builder and
results scroll independently while common commands remain outside both regions. It can
give the builder most of the available height. Simple stays compact with automatic height;
its layout does not overwrite the Advanced fraction. Resizing/group expansion does not
execute/invalidate search or move outer Dock splitters.

Existing editor-preferences.json stores only `SearchLayout: { Version: 1,
AdvancedBuilderFraction: 0.6 }`, not query/results/document state. A finite .1–.9 fraction
is accepted; missing/unsupported/out-of-range values use .6. Malformed records follow the
existing safe-default/feedback policy. Saves occur after completed drag/keyboard gestures,
using the existing flushed temporary-file/atomic-move convention. Continuous deltas and
ordinary window resizing cause no writes. Save failure keeps the chosen split for the
current session and reports failure through existing Preferences feedback; restart uses
the last successfully saved value. Hide/reopen/float/reset retain the shared model's split,
and old placement-only layouts remain compatible with tool.search-results.

## Declared version and explicit schema validation

| Concept | Meaning | Current capability |
| --- | --- | --- |
| Declared HL7 version | Each message's original MSH-12 | Read from Core's structural message model |
| Selected target schema | Initial profile selection when Apply Schema is invoked | Built-in or imported local profile; unavailable IDs are retained explicitly |
| Interpretation mode | Auto-detect a built-in from declarations, or select a default profile | Active choice in Preferences → Validation |

Inspection continues to use the lossless delimiter-based Core scanner/parser.
Stage 17 activates SchemaMode/TargetSchema only as the initial Apply Schema picker
choice. It never validates a newly opened message automatically or rewrites MSH-12,
values or source bytes. Auto-detection matches MSH-9.1/2/3 and MSH-12.1 to built-ins;
trading-partner profiles require explicit selection. Applied state is per message,
session-only, with inline findings and automatic edit refresh after application.
Unsupported coverage remains Incomplete. See the [Stage 17 guide](stage17-hl7-schema-validation.md)
for the local library, import formats, provenance, limits and acceptance checklist.

## Persistence and failure handling

General/schema metadata use the existing per-user
`%LOCALAPPDATA%\HL7Workbench\editor-preferences.json`, separate from Scripts,
workspace-layout.json and script-editor-preferences.json (code font preferences).
Old files containing only StartInEditMode retain it; missing Theme defaults to Follow System.
Unknown JSON fields in a valid record survive subsequent saves. Unknown enum values
fall back to declared schema mode / Follow System. Missing files cause no write; malformed/oversized records
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
