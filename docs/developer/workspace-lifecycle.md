# Workspace ownership, cancellation and cleanup

Workspace is UI-independent; Desktop supplies its owning context. Models outlive
replaceable controls. Cancellation requests, operation completion, publication,
UI observation and filesystem cleanup are separate milestones.

## Documents, borrowed sources and captures

[MessageFileReader](../../src/HL7Workbench.Workspace/MessageFileReader.cs) captures an
immutable [SourceSnapshot](../../src/HL7Workbench.Workspace/SourceSnapshot.cs), builds
the byte index and publishes a document only after preparation succeeds. Navigation
does not reopen the user's original pathname. Each document owns its original
snapshot, bounded active inspection and at most one cached decoded spool.

`ReadAsync`/`ReadTextAsync` serialize access through the document work gate.
Search/extraction/script/SQL captures freeze logical metadata and bounded immutable
draft references on the owning context, then borrow original resources through
read leases. Do not enumerate live sequence/buffers from background workers.
Release captures on every path, including pre-canceled requests: a `Task.Run`
delegate that must execute a `finally` must not be skipped merely because its
scheduling token was already canceled. Follow the existing feature implementation.

SQL independently snapshots captured bytes/drafts, then releases the document lease.
Its completed results can outlive the original source and remain stale-labeled;
matching-message extraction creates another independently owned output snapshot.
Other tools may invalidate rather than retain stale results. Full Source Viewer
borrows active message resources and ends on owning-message navigation/closure.
Do not generalize one tool's capture/close semantics to all tools.

## Revisions and publication

`MessageId` survives logical insertion/undo/redo. Current position and original ordinal
are not identity. SearchRevision is content-only: navigation/Save alone does not
mean source text changed. Other operations also guard active selection, content
version, request generation and profile/import lineage as appropriate.

Link lifetime/change/request tokens and reject superseded completions even if an
operation finished before cancellation was observed. Freeze the intended target
before async dialogs/work; recheck ownership/revision at publication. A model
notification or view attachment is not a substitute for those guards.

Edits live in document-owned buffers/history. [MessageSavePlan](../../src/HL7Workbench.Workspace/MessageSavePlan.cs)
streams original partitions plus changed/inserted payloads. It reindexes the flushed
candidate through the shared byte indexer, verifies membership/framing/opaque regions
and proves exact pieces before atomic publication. The [destination](../../src/HL7Workbench.Workspace/FileSaveDestination.cs)
captures replacement consent/version, flushes same-directory staging and refuses
destructive fallback. Publication is the commit point: later cancellation must not
report that already committed output was unpublished. External filesystem name-swap
races remain a documented limitation; this is not universal compare-and-swap.

## Completion barriers

Structured inspector editing uses one short-lived StructuredValueEditSession.
It owns bounded staged value/identity data and subscriptions, with no snapshot
lease or independent undo stack. Apply validates an isolated Core raw splice and
publishes once through ReplaceWorkingText; Cancel and invalid input never publish.
Switching documents/messages, changing selection/raw text, disabling Edit Mode or
closing permanently invalidates the staged target. After a committed edit, the
shell awaits the existing EditRefresh and restores the path only if the committed
message and revisions still match. Disposal clears source-value/document references
and subscriptions. Native modal closure does not replace the document's own drain.

| Owner/operation | Required completion before destroying its resources |
| --- | --- |
| MessageDocument | Call Dispose to cancel/close; await its `Disposal`. That task drains capture readers, validation operations and exclusive work before source/spool/snapshot release. |
| DocumentManager | Dispose/Close initiates document disposal and changes membership; it does not await every document drain. Keep references to all owned documents, including already removed ones. |
| SqlQueryViewModel | Dispose then await `Disposal`, which drains ActionTask and LibraryTask. Keep the creating dispatcher alive while context-dependent continuations finish. |
| SqlQueryResults | Dispose prevents new work/cancels its lifetime. Existing result read leases defer journal/source cleanup until their release. Await the operations you started; do not invent a public Disposal task it does not expose. |
| Batch/load models and listener | Request cancel/stop/dispose and await their existing operation/disposal/stop completion contracts; a detached view is not the owner. |
| UI result assertions | Await model observation and visible state as well as engine execution. `Session.Execution` alone may precede the observer continuation. |

Snapshot deletion is best-effort in production: open-file/permission failures can
leave owned plaintext storage. SourceSnapshot's abandoned cleanup considers only
bounded GUID-named, old, non-reparse directories with an available lease. A drain
is not secure erasure or proof that every OS deletion succeeded.

## SQL fixture cleanup race

The committed regression in [SqlQueryToolTests](../../tests/HL7Workbench.Desktop.Tests/SqlQueryToolTests.cs)
addresses teardown, without weakening SQL/CSV/all-page/source-preservation assertions.
Previously, manager disposal scheduled asynchronous extracted-document snapshot
cleanup; fixture recursive deletion could enumerate `snapshots` while the child
was being removed. `Task.Yield` and an Exists/Delete check did not synchronize it.
The observed `DirectoryNotFoundException` occurred in fixture disposal, after the
test body, not in the query assertion itself.

The fixture now owns a scope which tracks every added document even after Close
removes it. Its order is:

1. Dispose owned SQL models and await model `Disposal` on the creating context.
2. Dispose the manager; await `Disposal` for **all tracked documents**.
3. Unsubscribe tracking, then recursively delete only the fixture-owned root.
4. Tolerate `DirectoryNotFoundException` for an already removed owned directory;
   do not broadly swallow unrelated access/sharing/I/O errors in test cleanup.

`CleanupAwaitsExtractedSnapshotReadersIncludingAlreadyClosedDocuments` holds a real
read with explicit gates and proves cleanup remains pending until release, both
before and after Close. `CleanupToleratesAnAlreadyRemovedOwnedDirectory` covers
idempotent fixture cleanup. No sleep/retry is the completion barrier. This is a
test ownership fix; it does not establish a production parser/SQL defect or justify
changing production cleanup behavior.

Also inspect [document source](../../src/HL7Workbench.Workspace/MessageDocument.cs),
[capture leases](../../src/HL7Workbench.Workspace/MessageDocument.Search.cs),
[manager](../../src/HL7Workbench.Workspace/DocumentManager.cs),
[SQL model drain](../../src/HL7Workbench.Desktop/ViewModels/SqlQueryViewModel.cs),
[validation disposal regression](../../tests/HL7Workbench.Workspace.Tests/SchemaValidationLifecycleTests.cs)
and [extraction read-lease tests](../../tests/HL7Workbench.Workspace.Tests/FieldExtractionTests.cs).
