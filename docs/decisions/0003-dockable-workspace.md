# ADR 0003: Read-only dockable workspace foundation

Status: Architecture approved for Milestone 2; manual docking acceptance passed 2026-10-13.

## Decision

Adopt MIT-licensed Dock.Avalonia 12.1.0.7, Dock.Model.Mvvm, and Dock's Fluent theme
on Avalonia 12.1.4 / net10.0. Establish docking before the viewer and stop for review.
Use native floating tool windows. Documents remain in the central tabbed workspace.
Script Editor and Script Library will be tools, with persistent script storage
independent of the layout and editor view lifecycle.

Workspace owns documents and shared active context plus a stable, lazy tool registry.
Desktop's docking adapter creates framework wrappers with ordinary feature objects
as Context. Core and Workspace have no Dock/Avalonia references. Feature view models
do not inherit Dock types. Use explicit startup composition, without a DI container.

Serialize an application-owned, bounded layout DTO with System.Text.Json; no feature
objects, imported values, filenames, or script bodies. Restore registered identities
and current session documents. Validate geometry and recover unreachable title bars.
Unknown identities are omitted; corrupt/unsupported layouts recover to defaults.

## Consequences

Dock supplies gestures, docking guides, splitters, and native hosts. An adapter
limits coupling but does not eliminate maintenance/API risk. Central versions and
lock files fix the dependency graph. CommunityToolkit.Mvvm arrives transitively
through Dock.Model.Mvvm; application feature models need not depend on it.

This gate uses synthetic placeholders and functional Browser/Diagnostics. Real file
sessions, AvaloniaEdit, raw/structured viewer, reliable selection synchronization,
and bounded concatenated ER7 navigation follow only after explicit continuation
approval. Core stays
unchanged. Editing, undo/redo, saving, dirty state, and destructive operations are
deferred to Milestone 3; its previous scripting scope must be replanned separately.

Initial layout persistence excludes maximized state and pinning; hidden tools may
reopen in an available group rather than their exact pre-hide position. These limits
are explicit, with a reset/recovery command. Native GUI and monitor/DPI acceptance
must be checked manually; model tests cannot establish these behaviors.

Stage 3 consolidated UX amendment (including final polish accepted October 14,
2026): Segment Navigator/Field Inspector become independent tools over their existing
selection model. The optional Full Source Viewer releases content on close and is
not instantiated by restore. Version-2 metadata adds stable group identities and
remembered closed-tool placement/ancestor slots/floating bounds, superseding the
initial pre-hide-position limitation. Version-1 compatibility and all layer boundaries
remain. No new docking framework or broad plugin system is introduced.

Final layout amendment: fresh/reset workspaces show documents on the left (78%),
Navigator above Field Inspector in a right column (22%), and Activity Log across the
bottom (27% height). Browser is hidden by default but retains its ID/menu and saved
layout compatibility. Valid saved graphs and hidden preferences take precedence.
Activity Log opts into saved-visibility restoration while retaining release-on-close;
Full Source Viewer remains lazy and never restores prior content. No schema bump.

Full Source Viewer has explicit source/scope context, rather than following active
document selection. Tab switches retain that context; another explicit request
replaces it. Owning-document closure or tool closure ends the session. Current code
also ends it on same-file message navigation, which conflicts with the requested
fully retained snapshot behavior. The user explicitly chose to leave reading/export
unchanged and document that discrepancy for later ownership work. Do not introduce
a live active-document mirror or claim same-file retention is implemented. See
`../stage3-workspace-layout-acceptance.md` for evidence and the acceptance boundary.

See ../docking-acceptance.md for the review gate and ../PROJECT_CONTEXT.md for orientation.

## Stage 15UI amendment — responsive feature content (2026-10-27)

Follow-up: FHIR Patient Workspace preserves the old tool identity while changing
its visible name and content layout. Fresh/reset MainWindow layout paths now arm
DefaultDockSizing once finite Dock bounds exist; no resize subscription reapplies
defaults. The callback checks both displayed and factory root identity, so Opened
saved-layout restoration cannot be overwritten. Valid user proportions remain
authoritative. See the follow-up acceptance report for the limits of native evidence.

Keep Dock, its layout schema, default proportions, tool registry and placement-only
persistence. Add small native-control helpers in Desktop/Viewer: AdaptiveActionBar,
ResponsiveFormField, ToolContentGrid and ResponsiveToolSurface, with centralized
DIP metrics. Content adapts to its own measured viewport, including floating roots.
Use finite Grid measurement with a visible overflow extent, rather than measuring
editors and virtualized lists at infinity or requiring a large tool minimum.
No dependency, Core or Workspace change is needed.

Dock may replace templates in attach-before-detach order. SQL and Script Desktop
models retain session-only editor documents/undo and selection through an internal
handoff; old editors release their document event handlers. Model disposal clears
that state. No document/query content enters layout persistence. Responsive resizing
itself does not reconstruct controls or reset model selection/results.

Tradeoff: severely constrained tools require vertical travel; primary action bars
can acquire their own bounded vertical fallback. Dense data tables retain deliberate
horizontal scrolling. Existing complex Search/Comparison split persistence is
preserved; their action bars migrate but full extreme-height composition remains
future work. See [developer guide](../responsive-tool-layout.md) and
[acceptance evidence](../stage15ui-acceptance.md). Native Windows visual/DPI acceptance
is separate from automated/headless measurement; Stage 15D is not authorized.
