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
