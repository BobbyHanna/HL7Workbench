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

See ../docking-acceptance.md for the review gate and ../PROJECT_CONTEXT.md for orientation.
