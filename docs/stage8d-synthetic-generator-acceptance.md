# Stage 8D — deterministic synthetic HL7 generator

Native Windows acceptance was confirmed by the user on 2026-10-20. Exactly one
local checkpoint is authorized with message `feat: add Stage 8D synthetic HL7 message generator`,
after a fresh verification gate. The approved icon source is excluded from this
checkpoint and retained for the later icon phase. Earlier pending/no-commit notes
below are historical. The Stage 9/icon work must remain uncommitted; no external
Git operation or publication is authorized.

The fresh 2026-10-20 checkpoint gate passed locked restore, Release with zero
warnings/errors (1.88 s), all 1,416 solution tests (145.094 s), all 698 complete
Workspace stress tests (251.846 s), both ScriptHost Release-layout smoke lifecycles,
eight original fixture blob hashes, excluded-user-file/icon-source integrity and
tracked/new-file whitespace checks. Exactly the 35 accepted Stage 8D paths are
checkpoint candidates; application and permanent-test bytes match the native-accepted
manifest. Only the five native acceptance/authorization records changed in preparation.
The checkpoint hash and post-commit gate are recorded separately after creation
under ignored artifacts/stage8d-checkpoint-20261020.

Implemented under the sequential Stage 8C checkpoint + Stage 8D authorization.
Native Windows acceptance is **pending**. Stop after automated verification; Stage 8D
must remain uncommitted. No push, tag, release, packaging, deployment or publication.

## Stage 8C checkpoint gate

Local commit: `ddc18ae63df7ddb31ba55ee35c3c6feffd0d3891`

Message: `feat: add Stage 8C message insertion UI`

Exactly 30 accepted files were included, with parent
`16ebc6d2a2a9d36ddfb20c766976c18990846bb2`. Post-commit HEAD/content inventory matched,
tracked tree was clean, index empty, and the sole untracked `_TEST.hl7` unchanged.
Locked restore, Release (zero warnings/errors), 1,327 solution tests, 656 complete
Workspace stress tests, both ScriptHost smoke lifecycles, all eight fixture blobs
and whitespace checks passed before commitment. The mandatory gate is recorded in
ignored `artifacts/stage8c-checkpoint/phase1-gate.txt`; all gate conditions passed
before any Stage 8D changes. No second checkpoint commit is authorized.

## User workflow and contract

Home → Generate Messages or File → Generate Messages opens one compact styled dialog.
It exposes type, count, seed, base timestamp, prefix, new/active destination, four
insertion positions, Generate and Cancel. The active destination is captured when
opening the dialog; later tab switches cannot redirect it. Preview displays type,
count, destination and the expected control-ID range without generating message bodies.

Defaults: ADT^A01; 1 message; seed 42 (also used for blank seed);
`2030-01-01T00:00:00+00:00`; prefix SYN; new unsaved document. Timestamp requires
seconds and an explicit offset or Z, normalizes to UTC, and is restricted to years
1900–9998. Prefix trims/uppercases to 1–12 ASCII A–Z/0–9/hyphen/underscore characters.
No clock/source-derived defaults are used. The seed is an unsigned 32-bit integer.

| Type | Example content |
| --- | --- |
| ADT^A01 | EVN/PID/PV1; inpatient admission, synthetic ward/room/bed |
| ADT^A04 | EVN/PID/PV1; outpatient registration, no bed assignment |
| ADT^A08 | EVN/PID/PV1; earlier visit start, updated fictional address and reserved email |
| ORM^O01 | PID/PV1/ORC/OBR/NTE; new scheduled order with matching placer/filler identifiers |
| ORU^R01 | PID/PV1/ORC/OBR/three OBX; final numeric glucose/sodium and synthetic comment, units/status/time relationships |

All MSH segments identify test processing, v2.5.1, synthetic sender/receiver and
facilities, fixed-derived timestamps and predictable control IDs. Names, identifiers,
addresses and providers are unmistakably fictional. Email uses example.invalid;
there are no real-looking random phone contacts. No imported patient, external
dataset, network service, telemetry or content logging participates.

Generator version 1 fixes its explicit xorshift32 PRNG and construction order.
Identical normalized configuration gives identical UTF-8 bytes without BOM and
canonical CR segment endings, independent of culture, machine and wall clock.
IDs have format `PREFIX-SEEDHEX-EVENT-INDEX`, e.g. `SYN-0000002A-A01-0001`.
They are unique within a batch and repeat for identical requests; they are not
Workspace MessageIds and do not claim cross-batch uniqueness. Future generator
versions may change bytes. See [ADR 0012](decisions/0012-deterministic-synthetic-generation.md).

These are useful structurally coherent test examples, not complete clinical or
implementation-guide conformance validation. Five types and v2.5.1 only; no schema
designer, custom templates, PHI-based generation or new scripting APIs.

## Publication, resource limits and save behavior

Generation runs off the UI thread with per-message cancellation. Limits are
1,000 messages and 4 Mi UTF-16 units per request, plus existing 1 Mi/message,
16 Ki/line, 100,000 logical document messages, 16 Mi retained UTF-16 units/32 MiB,
and 4,096 history operations. Limits reject the whole request without truncation.
The 4 Mi ceiling is defensive; current closed examples are substantially smaller.

Existing-document output uses Before Current/After Current/Beginning/End over the
logical sequence. One prepared batch, one prospective metadata composition and one
publication splice produce one history operation. First generated ID is selected;
Edit Mode activates for that document only. One Undo removes the whole batch; one
Redo restores the same IDs/order. Existing IDs and original snapshot ranges remain.
Generated entries have Inserted provenance and no invented original ordinal/range.
Mutation invalidates search/filter state while retaining authored query definitions.

Before publication, a transient preparation lock blocks editing/history/navigation,
save, script capture and closure of the destination. An owned private temporary file
streams the prospective save; the existing index and byte-proof pipeline verifies
membership, framing, gaps and source pieces. No original decoding/reconstruction,
target saving, save-point advancement or source-byte repair occurs. This needs
temporary disk space proportional to the prospective output and streaming I/O;
large requests are cancellable throughout. Source snapshots drain work before disposal.

Unsupported batch/provisional/ambiguous framing, unsafe joins, incomplete unrelated
drafts, unrepresentable encoding or exhausted capacity/history reject atomically.
Feedback suggests completing/undoing drafts, a safe position, a reduced count, or a
new document. Pre-publication failure/cancel preserves live IDs/order/text/selection,
dirty/save state, revision and Redo. Safe MLLP and mixed message delimiters use the
existing streaming save policy; source terminators/bytes are retained exactly.

New output stays private until complete, then opens independently as, for example,
`Synthetic ADT A01.hl7 *`. Duplicate unsaved titles get display suffixes. It has no
filesystem destination and follows normal Save As and unsaved-close prompts. Undo
may remove its entire batch to an empty document. Saving does not fabricate original
provenance; reopening the saved output establishes a new original session.

The existing window-root history authority and clipboard/rejected-input transaction
fixes remain intact. Result focus waits for the actual Dock host to mount after
modal dismissal, then uses the existing guarded insertion focus path. Ordinary
history keys retain their focus-independent active-document routing; independent
text inputs retain native commands. No new keyboard handler is installed.

## Automated verification

Final verification, 2026-10-18:

| Check | Result |
| --- | --- |
| Locked restore | Passed; dependencies/lock files unchanged |
| Release build | Passed; zero warnings/errors; 2.14 s |
| Focused Core generator | 27 passed; zero errors/failures/skips/unrun; 0.451 s |
| Focused Workspace structural/generator/compatibility | 132 passed; zero errors/failures/skips/unrun; 55.161 s |
| Focused Desktop generator/insertion/history/clipboard/compatibility | 65 passed; zero errors/failures/skips/unrun; 32.128 s |
| Desktop ribbon/script-editor correction retest (overlaps above) | 52 passed; zero errors/failures/skips/unrun; 21.823 s |
| Full solution, final source | 1,416 passed; zero failed/skipped; 136.785 s |
| Complete Workspace with HL7WORKBENCH_STRESS=1 | 698 passed; zero errors/failures/skips/unrun; 267.057 s |
| Both ScriptHost smoke lifecycles | Passed against the existing Release build layout, including dependency/hash isolation; no publish command used |
| Eight tracked fixtures | Original no-filter Git blob hashes and protected SHA-256 hashes unchanged |
| Excluded local _TEST.hl7 | SHA-256 unchanged; remains untracked and unstaged |
| Native shortcut/paste/crash-test files | Protected SHA-256 hashes unchanged |
| Whitespace | git diff --check plus new-file whitespace/conflict checks passed |
| Git scope | main/HEAD remains the Stage 8C checkpoint; empty index; 35 unstaged Stage 8D paths plus unchanged excluded file |

The new permanent coverage adds 89 cases: 27 Core, 42 Workspace and 20 Desktop.
No baseline cases were removed. Counts rise from 1,327 solution/656 Workspace to
1,416 solution/698 Workspace. Focused runs overlap and must not be summed.
Useful initial/failure/final diagnostics and exact inventories/hashes remain under
ignored artifacts/stage8d; shared generated stress fixtures remain ignored/reusable.
All permanent prior tests and all eight fixture blobs remain protected; the excluded
local `_TEST.hl7` was checked by hash only.

An initial full run exposed an existing Home command becoming unreachable after
adding a second large Messages button. The Messages group now uses the established
two-row compact layout; the unchanged shell click/assertion and final full run pass.
New headless coverage also exposed new-tab focus preceding actual Dock mounting;
the result path now waits for the real host and uses the existing guarded focus
helper. Preflight allocation tests removed redundant hierarchy parsing of closed
Core output (about 312 MB initially to 11.6 MB for 1,000 ADT messages) while retaining
strict encoding and complete prospective index/byte verification. A permanent
memory-original regression requires membership proof before its first insertion too.

Observed stress measurements (include bounded active-message parse where awaited):

| Case | Total observed time | Allocated bytes | Retained growth |
| --- | --- | --- | --- |
| 1 generated ADT | 195.451 ms | 537,904 | — |
| 100 generated ADTs | 189.350 ms | 1,641,472 | — |
| 1,000 generated ADTs | 205.770 ms | 11,615,600 | — |
| 1,000 inserted into 100 MiB source | 522.662 ms | 12,071,912 | 1,287,248 bytes |
| 1,000 inserted into 256 MiB source | 4,513.127 ms | 12,959,792 | 1,287,280 bytes |
| 1,000 inserted into 512 MiB source | 1,845.483 ms | 14,397,304 | 1,287,272 bytes |
| 99,000 originals + 1,000 generated | 256.112 ms | 74,693,816 | — |

These are machine/run observations with variable disk costs, not guarantees. Large
tests also save and compare every untouched source byte in bounded chunks. Source
payloads remain lazy; no unrelated original message strings are materialized.

Permanent coverage includes all five structures, exact MSH indexing, separators,
orders/results/timing, CR and round-trip parseability, seed/culture/normalization/golden
determinism, 1/100/1,000 batches, every quota, cancellation/retry, four positions,
one-batch history/save points/IDs, source/framing/encoding safety, invalid drafts,
capacity/retained/history budgets, transient mutation locks and closure, new unsaved
ownership, tab isolation, filter/advanced search, Inspector/Comparison, both script
scopes/CSV and stale attribution, Save As/reopen, UI defaults/validation/preview,
Generate/Cancel/X dismissal, mounted editor focus and Stage 8C EOF/clipboard/history.
Large-source and near-100,000-message tests assert bounded allocations and print
diagnostics. Timings are observations of this machine, never performance guarantees.

## Native Windows acceptance (no debugger)

Run the Release app with `dotnet run --project src/HL7Workbench.Desktop -c Release --no-build`.
Keep acceptance exports in an owned temporary folder or ignored artifacts/stage8d-native;
never replace the eight fixtures or the excluded local `_TEST.hl7`.

1. With no document open, choose Home → Generate Messages (also repeat via File,
   with the ribbon pinned and collapsed). Verify the fixed defaults/identifier preview.
   Generate. Expect one dirty Synthetic ADT A01 tab, Edit Mode on and editor focus
   without a click. The normal Start in Edit Mode preference must remain unchanged.
2. Inspect MSH-1/2/7/9/10/11/12, EVN, fictional PID and inpatient PV1. Use Navigator,
   Field Inspector, hover and RAW selection. Confirm no actual patient/contact data.
3. Generate each A04, A08, ORM O01 and ORU R01 into separate tabs. Verify outpatient
   registration, updated address/contact, coherent order IDs and final OBX values/units.
4. Save As two identical configurations to different owned paths. In PowerShell run
   `(Get-FileHash <first> -Algorithm SHA256).Hash -eq (Get-FileHash <second> -Algorithm SHA256).Hash`.
   Expect True. Change seed to 43, generate/save twice: those match each other and
   differ from seed 42. No current-time default or platform line-ending change.
5. Generate 100, then 1,000 messages; preview must remain lightweight and UI usable.
   Navigate first/middle/last; control IDs/index-derived timestamps must follow order.
6. Open samples/synthetic-only/stage5b-repeated-patients.hl7. Select Message 2, open
   Generate Messages, choose active document, count 3, Before Current. Expect all three
   inserted before it, first generated message selected/focused, original IDs/content
   retained. Turn Edit Mode off; focus Inspector/Navigator; Ctrl+Z once removes the
   whole batch; Ctrl+Y once restores it. Repeat keys without a mouse click and switch
   document tabs to verify correct document scope and no duplicate invocation.
7. Repeat insertion at Beginning, After Current and End. Navigate source messages;
   Save As a separate file/reopen and confirm source bytes/content/order are retained.
   Repeat with a custom-delimiter file and safe MLLP input. An unterminated source
   followed by an End insertion must reject, while safe Beginning insertion may pass.
8. Leave a blank/incomplete inserted draft, then generate into that document. Expect
   whole-request refusal and retained draft/history; complete/undo it or use a new tab.
   Try count 0/1001, invalid seed/prefix/date and a near-capacity document. No truncation
   or partial insertion. Test a full edit/history budget where practical.
9. On a large file-backed source, generate 1,000 into it and immediately Cancel or X
   during background validation. Expect no batch/dirty/revision/selection change and
   retained Redo. Repeat Generate successfully; the source must remain lazy/read-only
   where required. Also cancel new-document generation: no partial output tab.
10. Find a generated control ID via Find Messages; test advanced fields, filter and
    matching navigation. Undo/Redo invalidates stale results. Compare generated messages
    within/across tabs; Compare With Original must report unavailable for inserted data.
11. Run a trusted no-op/report script on Current and Whole document (for example,
    `var r = Batch.Report("ids", "Control ID"); void Main() { r.Row(Message.Read("MSH-10").Raw); }`).
    Review independent output and export CSV. Edit the source during a run and verify
    explicit stale-source attribution remains. Search/script text inputs keep their
    own Undo/Redo behavior and do not mutate the HL7 document.
12. Save As new output, close/reopen, inspect all content and confirm normal subsequent
    saves. Generate a duplicate tab, edit it, switch tabs and close via Save/Discard/
    Cancel; the source and other generated outputs must remain independent.
13. Recheck Stage 8C Next at unfiltered EOF: one blank draft, repeated Next reuses it,
    typing permits another, filtered Next never appends. Paste synthetic HL7 containing
    tabs/mixed CR/LF via Ctrl+V without another click; preserve exact characters.
    Reject multi-message paste, then Undo/Redo: no native transaction crash or data loss.
14. Float Inspector/Navigator or script editor, repeat generation and history, and
    confirm result focus returns to the generated document with no unexpected shortcut
    interception. Use Light/Dark/Follow System and check labels, preview and status
    remain readable at normal Windows scaling.

Record native results separately before authorizing any Stage 8D checkpoint or
further milestone. Stop at this acceptance boundary.

## Changed-file inventory

35 Stage 8D paths: 21 modified tracked files and 14 new milestone files. The
unchanged excluded user file is additional and is not part of this inventory.
No permanent tests, fixtures, source snapshots, native shortcut implementation or
raw clipboard/transaction fixes were deleted. The existing ribbon test action map
adds the new command; all previous assertions remain intact.

| Area | Paths |
| --- | --- |
| Core (3) | src/HL7Workbench.Core/HL7Workbench.Core.csproj; SyntheticGenerationOptions.cs; SyntheticMessageGenerator.cs |
| Workspace (9) | src/HL7Workbench.Workspace/DocumentManager.cs; MessageDocument.cs; MessageDocument.Editing.cs; MessageDocument.Generation.cs; MessageDocument.Saving.cs; MessageDocument.Scripting.cs; MessageDocument.Sequence.cs; SyntheticGeneration.cs; UnsavedChanges.cs |
| Desktop (12) | src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs; DocumentSaveWorkflow.cs; MainWindow.axaml; MainWindow.Editing.cs; MainWindow.Generation.cs; MainWindow.Insertion.cs; MainWindow.Shell.cs; Viewer/FileDocumentView.cs; Viewer/SyntheticGenerationDialog.cs; Viewer/UtilityRibbon.cs; ViewModels/ScriptEditorViewModel.cs; ViewModels/SyntheticGenerationViewModel.cs |
| Tests (6) | tests/HL7Workbench.Core.Tests/SyntheticGenerationTests.cs; tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs; SyntheticGenerationViewTests.cs; tests/HL7Workbench.Workspace.Tests/SyntheticGenerationTests.cs; SyntheticGenerationTests.Compatibility.cs; SyntheticGenerationTests.Stress.cs |
| Documentation (5) | README.md; docs/PROJECT_CONTEXT.md; docs/architecture.md; docs/decisions/0012-deterministic-synthetic-generation.md; docs/stage8d-synthetic-generator-acceptance.md |

The exact sorted path list and final hashes are retained under ignored
artifacts/stage8d. Only the accepted Stage 8C checkpoint was committed; the entire
Stage 8D inventory remains unstaged for native acceptance.
