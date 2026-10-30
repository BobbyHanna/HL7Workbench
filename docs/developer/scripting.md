# Trusted C# scripting and API documentation

The authoring contract is the existing Scripting facade, not the entire public Core
or host surface. [ADR 0007](../decisions/0007-unified-csharp-scripting.md) supersedes
the earlier script-kind/lifecycle design while retaining applicable preservation,
transaction, containment and reporting decisions in ADRs 0005/0006. User tasks live
in [Run and manage scripts](../user/run-and-manage-scripts.md).

## Execution pipeline

Workspace captures logical input identity/order and immutable working/original
content before background processing. [ScriptExecution](../../src/HL7Workbench.Workspace/ScriptExecution.cs)
coordinates bounded staging/publication; [ScriptWorkerSession](../../src/HL7Workbench.Scripting/ScriptWorkerSession.cs)
launches a fresh worker and exchanges versioned bounded pipe frames.
[UnifiedScript](../../src/HL7Workbench.ScriptHost/UnifiedScript.cs) compiles the
submission once and discovers entry points from Roslyn syntax/symbols, not regex.
One fixed continuation binds delegates to the same submission instance.

Initialization runs once. Required synchronous, nongeneric parameterless `void Main()`
runs once per captured message in source order. Optional `void Complete()` runs once
after all Main calls succeed, never after a processing failure. Current message is
one target using the same lifecycle as Whole document. Top-level state persists
within the run, Main locals restart, and another Run has fresh state. The Message
global is only available in Main; Batch/Cancellation and each operation's phase
restrictions follow the generated reference. Hold/Defer/Emit work on run-owned
handles, not arbitrary externally constructed messages.

Parent-owned report/output journals and final indexing/publication prevent partial
application results after failure/cancellation. Successful outputs are independent
unsaved documents; unchanged originals retain their preservation contract. This
transaction does not roll back arbitrary filesystem/network side effects of trusted
C#. Scripts execute with account privileges; private pipes, deadlines, bounded
frames, process-tree cleanup and Windows Job Objects are **not an OS security sandbox**.
Do not describe public host helpers as an access-control boundary merely because
they are unsupported authoring facilities.

## Facade, library and reports

[ScriptMessage](../../src/HL7Workbench.Scripting/ScriptMessage.cs) wraps controlled
Core reads/writes. [ScriptBatch](../../src/HL7Workbench.Scripting/ScriptBatch.cs)
owns exclusion, bounded reports/deferred output and naming decisions. The facade
does not expose a generic from-scratch message constructor; dedicated generation is
a separate Core/Workspace workflow. [ScriptLimits](../../src/HL7Workbench.Scripting/ScriptContracts.cs)
and [the workflow reference](../script-api.md) define phase/resource boundaries.

Library version 3 stores independent user IDs/name/source and migration status.
Built-ins are immutable and duplicable; obsolete v1/2 records remain reviewable
but require explicit rewrite/save before Run. Do not automatically translate or
execute old syntax, overwrite user copies during updates, or relabel retained
reports when switching/renaming scripts. Report export retains the producing run
and blocks journal replacement during the native picker/write.

De-identification helpers use bundled deterministic vocabularies and run-local
maps. Supported field substitution and timestamp shifting do not establish complete
PHI removal. Output reports retain Review Required and explicit residual/unassessed
risks. See [ADR 0013](../decisions/0013-script-deidentification-aids.md) before changing policy.

## Generated API catalog authority

Keep [docs/script-api-reference.json](../script-api-reference.json) as the reviewed
topic/behavior/example source. Workspace embeds it verbatim;
[ScriptApiContracts](../../src/HL7Workbench.Workspace/ScriptApiContracts.cs) derives
exact declared signatures from the same Scripting assembly referenced by ScriptHost.
The catalog classifies supported facade/helper types separately from public but
unsupported orchestration types. Runtime inventory tests check overloads,
nullability/defaults/modifiers and snapshot signatures; they do not execute getters.

The [Markdown reference](../script-api-reference.md) is generated, never manually
edited as another member list. Existing topic/starter IDs remain stable. F1,
explicit completion/signature help and reference navigation reuse compiler-only
binding in Desktop without running code or loading an arbitrary user assembly.
Do not turn facade completion into a new general language-service dependency.

When a contract changes, review classifications, behavior/errors/null/mutation/
phase semantics and complete examples first. Run Workspace catalog tests to obtain
their runtime export, then synchronize reviewed signatures and render:

```powershell
python build/WriteScriptApiReference.py --sync-contracts tests/HL7Workbench.Workspace.Tests/bin/Release/net10.0/script-api-catalog.json
python build/WriteScriptApiReference.py --check
```

The export path assumes normal Release output. Rebuild after changing embedded
JSON, then run catalog and real-worker example/assistance tests. See
[Stage 18 maintenance](../stage18-script-api-reference.md#machine-readable-catalog-and-maintenance)
for guards; do not sync an unrelated/stale export or use it to invent classifications.

Evidence: [lifecycle tests](../../tests/HL7Workbench.Scripting.Tests/UnifiedLifecycleTests.cs),
[real-worker examples](../../tests/HL7Workbench.Scripting.Tests/ScriptReferenceExampleTests.cs),
[catalog inventory](../../tests/HL7Workbench.Workspace.Tests/ScriptApiCatalogTests.cs),
[execution/publication](../../tests/HL7Workbench.Workspace.Tests/ScriptExecutionTests.cs)
and [editor input/help](../../tests/HL7Workbench.Desktop.Tests/ScriptReferenceNativeInputTests.cs).
