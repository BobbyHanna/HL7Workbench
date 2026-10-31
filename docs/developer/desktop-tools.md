# Desktop tools, commands and Dock lifetimes

Avalonia controls and Dock types stay in Desktop. Workspace exposes neutral
documents/sessions and application-owned layout DTOs, not framework models.
Feature view models do not need docking inheritance. Follow
[ADR 0003](../decisions/0003-dockable-workspace.md) and the
[responsive composition guide](../responsive-tool-layout.md).

## Tool and view ownership

[ToolWindowRegistry](../../src/HL7Workbench.Workspace/ToolWindowRegistry.cs) registers
stable ToolDefinitions with Optional, ReleaseOnClose and RestoreVisibility policies.
[WorkspaceDockFactory](../../src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs)
owns Dock identities, active-document wiring, close consent, placement and recovery.
[DockContentTemplate](../../src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs)
and its content host can replace a view while retaining the same feature model.
Do not stop a run or clear drafts simply because a control detached.

Tool close may retain an editor/reference model or release a source/result session,
depending on its existing policy. Release invokes IDisposable, which can initiate
asynchronous drains; registry removal alone is not completion. The owning shell/model
must track its work as described in [workspace lifecycle](workspace-lifecycle.md).
Subscribe when needed and unsubscribe on detach/dispose; do not accumulate handlers
across template reconstruction. Explicit open actions request focus; restoration
must not steal keyboard focus or start source/network work.

## Commands and active context

MainWindow markup/shell bind existing UiCommands into File/Settings/View/Help and
the utility ribbon. Use one command/model for all entry points, with meaningful
CanExecute and accessible names/tooltips. Dock active document changes update the
shared DocumentManager; tool focus retains the active document unless a workflow
explicitly chooses another source. Floating windows are separate routed-input roots.

The schema command coordinator captures active document, MessageId and revision
around the existing selector and rejects stale choices. Import/Manage reuse one
library interface even without an open document. Raw editor controls retain only
read-only validation status/inline marks, not a second management surface.
Preserve F8/Shift+F8, Script Editor F1/completion/signatures, and independent text-input
history. [DocumentHistoryShortcuts](../../src/HL7Workbench.Desktop/DocumentHistoryShortcuts.cs)
prevents document undo from overriding SQL/C#/TextBox histories.

## Layout, theme and bounded presentation

Use finite Grid viewports, Auto command/status rows, star editor/list rows,
virtualized collections and native wrapping action bars. Feature controls measure
their own docked/floating DIP viewport, not MainWindow width. Do not repair a tool
with fixed-position offsets or by overwriting the user's Dock ratios. Default sizing
is armed once after finite measurement and guarded by root identity so a queued
callback cannot overwrite restored geometry.

Keep authored values in the model and authoritative journals/sources, not abbreviated
cells. Map original intervals explicitly before drawing squiggles or selecting raw
text. Dynamic resources inherit effective Light/Dark themes in popup/floating hosts;
code editor palette behavior is its own established contract.

Layouts serialize IDs/geometry/visibility only. Do not add filenames, message text,
queries, imported patients, results or network authority. Existing version-1/2 layout
compatibility and stable IDs (including Diagnostics→Activity Log and old Synthetic
Patients/Search Results IDs) must survive visible-name changes.

## Inspector projection and content fonts

The same InspectionPanes/MessageInspection controls own segment-occurrence selection
and lazy Core children. InspectorProjection filters represented empty rows and adds
only bounded fields from the explicitly applied profile. Absent rows have no range;
the scanner's authoritative Fields collection is unchanged. Available datatype
metadata enriches structural labels without inventing clinical names. Expansion and
blank visibility are session-only path state, restored without retaining offsets.
Stale snapshots also reject the blank toggle and pointer-release actions.

InspectionActionContext captures document/message/revision/selected-node identity.
Copy uses the shared bounded ReadTextAsync path for both materialized and source-backed
messages, then rechecks identity. Search reuses MessageSearchSession over the entire
active file with unindexed ANY-occurrence/repetition semantics; advanced raw equality
can match represented empty values. Extraction uses the existing retained draft and
unsaved prompt, rechecking eligibility before replacing the draft. Docked/floating
routed requests reach the same MainWindow coordinator. Discovery/extraction remains
read-only. The bulk review below adds bounded unique analysis and confirmed independent
output. Edit Value uses the guarded staged session described below.

StructuredValueEditSession captures document, MessageId, content/search revisions,
inspection/node identity, canonical path and exact message-local range. Eligibility
requires the existing complete-text Edit Mode, a materialized Core node and safe
structure. A native dialog owned by the originating inspector window stages raw
input. Core SetRaw validates an isolated bounded model and splices its span; one
ReplaceWorkingText call publishes through the existing document history. The active
Core model is never mutated behind Workspace. Absent nodes cannot expand structure;
MSH-1/2 stay read-only. No clinical/schema gate is added.

Document/message/selection/raw edits and closure permanently invalidate a staged
target. The shell awaits the existing EditRefresh and restores the canonical path
only while the committed document/message/revisions remain current. Sessions remove
subscriptions and clear value/document references on disposal; they own no snapshot
lease. See [Stage 20B](../stage20b-structured-field-editing.md) and its permanent
Workspace/Desktop tests, including actual docked/floating dialog transactions.

ContentFontPreferences lives in editor-preferences.json, outside placement DTOs.
ApplicationPreferences applies validated global/feature sizes only after a successful
atomic save. DockContentTemplate installs ContentFontScope on ordinary tool content;
feature keys exclude document identity. Native inherited fonts and scoped resources
scale existing authored typography, including newly virtualized rows. Ctrl+Plus/Minus
is handled at the focused content root before editor input; Ctrl+0 removes the keyed
override. Ctrl+Wheel tunnels through the content root before nested scroll/editor
handlers, chooses the nearest registered scope under the pointer and consumes only
a valid handled zoom. Weak scope registration supports nested/floating controls
without a keyboard-focus target. Ordinary/horizontal/modified scrolling and menu/
command/modal controls remain outside this gesture. The C# standalone fallback is
retained, while shared scopes prevent double handling.

The default reference size is 14 DIP, bounded 12–32; headings/code retain proportional
sizes, with script code's existing 9–24-point bounds. Existing script preference files
migrate once into a clamped shared override and remain on disk. The standalone legacy
view-model API remains compatible. Modal configuration/export/About dialogs and
ribbon/menu chrome keep existing sizing. Native DPI, clipboard, focus and screen-reader
acceptance is tracked in the [Stage 20A checklist](../stage20a-field-inspector-and-fonts.md).

## Tests and manual review

Cover command enablement/target changes, stale modal returns, tool close/reopen,
view replacement during work, keyboard focus, narrow/wide bounds, themes and scale.
Use [SchemaRibbonTests](../../tests/HL7Workbench.Desktop.Tests/SchemaRibbonTests.cs),
[DockContentTemplateTests](../../tests/HL7Workbench.Desktop.Tests/DockContentTemplateTests.cs),
[ResponsiveToolTests](../../tests/HL7Workbench.Desktop.Tests/ResponsiveToolTests.cs),
[default-layout tests](../../tests/HL7Workbench.Desktop.Tests/DefaultWorkspaceLayoutTests.cs)
and [native input regression](../../tests/HL7Workbench.Desktop.Tests/RawEditingCrashTests.cs)
as examples. Headless/fake-host tests do not establish native monitor/IME/clipboard/
screen-reader or mixed-DPI acceptance. General documentation Help follows these
same optional-tool conventions; its implementation, approved wording fixes and
pending native checks are recorded in [Stage 19D](../stage19d-offline-documentation-help.md).

## Bulk inspector review

The three bulk actions reuse the existing routed inspector intent and originating
Window owner, including floating inspectors. BulkFieldWindow is a transient native
review, not a second inspector or registered permanent tool. It captures an explicit
scope, exposes 100-row pages and complete bounded detail, and enables Apply only
after a current valid preview plus confirmation. It shares the field-inspector font
preference and ContentFontScope, including pointer zoom. Essential operation inputs
are native editable label/TextBox pairs before Analyze, with a bounded complete raw
Find prefill from the captured selection. MainWindow rechecks context after any
source-backed read; execution reads the visible inputs. ResponsiveToolSurface
provides whole-review overflow and a finite results minimum instead of a capped
parameter scroller that could hide Find/Replace/New value. Complete detail remains
collapsible. Input text changes discard the preview and confirmation.

Unique navigation/filtering uses MessageId and ApplySearchFilter with the complete
captured bucket membership. Existing SQL matching-message publication can provide
an explicitly extracted independent dataset; there is no SQL update/direct-row
bulk authority. Extraction remains the CSV path. Close/Cancel and async action
completion are owned by the shell; tests await work/result/document drains.

Manager registration alone does not create a Dock document tab. Bulk publication
passes an owning-context presenter to BulkFieldResults.ApplyAsync, calling the
existing WorkspaceDockFactory.OpenDocument before success closes the review.
The prepared document already selects its first changed message/path. Inspector
ownership follows DocumentManager, while FileDocumentView belongs to its Dock
wrapper: omitting presentation can therefore show values from different documents.
OpenDocument removes a newly added wrapper, locator and title subscription if
presentation throws, restoring the prior tab. Workspace then withdraws/disposes the
attempted document and restores the prior manager identity. This is a publication
failure, not a refresh request. Busy/closed click guards retain the original action
completion task and prevent duplicate Apply publication. Permanent real-shell tests
exercise both views, 30-message byte equality, original-tab return, post-insertion
presentation failure/retry and docked/floating inspectors.
