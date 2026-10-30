# Run and manage trusted C# scripts

Use **View → Data & Query → Script Editor** or **Scripts → Open Script Editor**.
Prerequisites: open synthetic HL7, a complete **ScriptHost/** deployment, and code
you trust. Scripts run as C# with your account permissions. Worker deadlines,
memory limits and cleanup are **not an operating-system security sandbox**;
trusted code can access files, networks and other account capabilities.

## Run a tested starter

1. Open [adt-a01.hl7](../../samples/synthetic-only/adt-a01.hl7).
2. Choose **Help → Script API Reference** (also Scripts → API Reference or the
   editor's API Reference button). Search `starter` and select **Main / Complete
   and execution scopes**, whose starter is named **Count selected messages**.
3. Choose **Create editable script**. Complete any Save/Discard/Cancel prompt for
   the previous draft. The new draft is independent of bundled examples.
4. Name it and choose **Save Script**. Choose **Apply to: Current message**, then **Run**.
5. Open **⋯ → Reports / Export CSV…** (or Scripts → Reports) and export the counts
   report deliberately. The one-message fixture reports one processed message.
6. Choose **Whole document** to process every logical message in captured source
   order. Running against one message follows the same lifecycle.

Expected result: this starter leaves message values unchanged and produces the
count report. Successful runs publish independent unsaved HL7 output; source files,
drafts and history are retained. Review Output opens the produced document when
available. Reports retain their producing script/scope attribution, even after
switching scripts; a later successful run replaces or clears them.

## Understand the lifecycle

- Top-level initialization runs once per Run and may create shared state/reports.
- `void Main()` runs once per captured message. The Message global refers to that
  message only during Main; Main-local variables restart for each call.
- Optional `void Complete()` runs once after successful processing. It is skipped
  after a processing failure. Every new Run uses a fresh worker and state.

Use [the generated API reference](../script-api-reference.md) for exact supported
contracts, phase restrictions and complete examples. F1 in code opens symbol help;
Ctrl+Space explicitly requests facade completion and Ctrl+Shift+Space signatures.
Lookup does not execute scripts. This guide does not define another signature catalog
or promise a general C# language service/from-scratch message constructor.

## Manage scripts and review transformations

1. **New** creates an editable starter. Select a library record to reopen it.
2. Bundled scripts are read-only; **⋯ → Duplicate Script** creates an editable copy.
3. Click the script name or use **Rename Script**; Enter/valid focus loss confirms
   the draft name, Escape cancels. **Save Script** persists name and source together.
4. **Delete Script…** applies only to saved user scripts, with dirty-draft handling
   and explicit confirmation. External revision conflicts preserve the current draft.

Patient Deidentifier, Identifier Pseudonymizer and Timestamp Shifter are reviewable
presets. Duplicate to configure their supported policies. Their outputs/reports
require review; narrow presets leave other identifying content, and even Patient
Deidentifier does not guarantee anonymity or compliance. Read the
[field coverage/residual risks](../stage9-deidentification-acceptance.md).

## Cancellation and recovery

**Cancel** stops a run and prevents partial application outputs/reports; this does
not undo file/network side effects performed by arbitrary trusted C#. Previous
completed reports are retained on failure/cancellation for retry. Read diagnostics,
fix code or inputs and Run again; old library records marked Needs update require
an explicit rewrite/save. Save/Discard/Cancel protects drafts during replacement/close.

Ctrl+S in this tool saves the script, not HL7. Other text inputs have their own undo;
see [focus-dependent shortcuts](preferences-and-shortcuts.md#keyboard-shortcuts).
See [scripting transactions, scope and limits](../script-api.md) and
[privacy/storage](privacy-and-storage.md) before running code or sharing outputs.
