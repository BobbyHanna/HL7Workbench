# FHIR generation UX polish — acceptance

Focused follow-up, 2026-10-28. The owner reports native acceptance of the preceding
FHIR Patient Workspace redesign. Native acceptance of this polish remains separate.
No Stage 15D/16, commit, push, packaging or protected-beta changes belong to this work.

## Cause and implementation

Only `SyntheticPatientsSession.PreviewAsync` previously built the prepared Core
plan. Its prepared-only Generate overload and the view's `Preview.CanGenerate`
button gate consequently made Preview mandatory. The Desktop now passes current
settings to this new Workspace overload:

```csharp
Task GenerateAsync(uint seed, bool adt, bool oru,
    DateTimeOffset transportTimestamp, CancellationToken token = default)
```

It shares preparation with `PreviewAsync(uint, bool, bool, DateTimeOffset,
CancellationToken)`. Core `FhirGenerationPlan.Create` still owns eligibility,
mapping and measured-size rules and runs through `Task.Run`. A single busy/cancel
operation covers preparation and publication, with phase status on the existing
surface. Preview publishes nothing. Generate checks eligibility/limits, then calls
the unchanged `FhirGenerationPublisher.PublishAsync`. Its staged validation,
source/current-capture check, one-document open and failure cleanup remain intact.
The original `GenerateAsync(CancellationToken)` preserves the prepared-only API
for existing callers; the Desktop uses the settings overload.

Cached plans require both authoritative session revision and seed/ADT/ORU/timestamp
settings. Import/replacement, patient/scope/event/encounter changes and relevant
filter removal invalidate revision. Desktop settings changes invalidate immediately;
the scalar key also protects non-UI callers. Clear/close cancel and invalidate.
There is no new persisted cache or copied patient data in settings.

Default Selected Patient and Entire Import keep their full supported-history rules.
Selected Events keeps explicit manual selection. Clearing it blocks this scope;
there is no empty-to-all fallback. Switching scopes preserves manual selections;
switching patients/importing clears them under the existing policy.

Generate is the primary accent action. Optional Preview stays next to it. Invalid
seed/time disables both actions with help/status. Known no-eligible/over-limit plans
disable Generate until inputs change; warnings that allow output remain in the
count-only preview/Generation Report, without mandatory acknowledgment.

All existing ceilings remain: 1,000 messages, 4 Mi total UTF-16 units, 1 Mi/message,
16 Ki/segment and mapped-field limits, plus import/insertion/source budgets.
Preparation retains bounded candidates and renders/discards measured messages;
it does not bypass the publisher or weaken cancellation/source immutability.

## Seed audit and accessibility

Searching all Desktop C#/AXAML identified exactly two configurable seed inputs.
Read-only report labels and Script API example strings are not configuration fields.

| Input | Verified implementation and help |
| --- | --- |
| FHIR Patient Workspace | `FhirGenerationPlan.Render` in `FhirGeneration.Mapping.cs` builds MSH-10 from seed + selected-source fingerprint + ordinal. No PRNG is used. Only transport IDs change; demographics, clinical values, patient/visit IDs and order stay unchanged. Identical source/settings/mapping version support repeatable bytes and repeat transport IDs across batches. |
| Generate Messages | `SyntheticMessageGenerator` uses its fixed xorshift32 for birth dates, test given-name suffixes and numeric ORU values. `SyntheticGenerationOptions.ControlId` includes seed, deriving patient/visit/order IDs. Fixed facilities/providers/address templates and configured transport times stay unchanged. Same normalized settings reproduce bytes within generator version 1; blank seed means 42. |

Example FHIR help: “Controls generated message-control IDs (MSH-10). The same seed,
source and settings produce repeatable IDs. Changing the seed may change transport IDs;
patient/visit IDs, clinical values and event order remain unchanged.”

Both label/input pairs use native themed Avalonia ToolTips with wrapping and a
400-DIP maximum text width, plus AutomationProperties.HelpText. Existing F1/Escape
handlers set the open flag, and blur/detach closes it. Native owner review found F1
unreliable, so the final cleanup removes its advertisement from both descriptions
and guides while retaining handlers/accessibility. Headless flag checks do not prove
native popup visibility. Local handlers create no global subscription. No new
tooltip framework/dependency was introduced.

FHIR's initially collapsed Advanced Options contains Seed and Transport Timestamp.
The existing responsive fields/surface provide scrolling in short/narrow tools;
editors/lists are not put in an unbounded viewport. Values and expansion live in
the current model and survive collapse, resize and Dock view replacement.

## Automated verification

New permanent coverage: 14 Workspace direct-generation cases and 11 Desktop cases.
They cover direct output without Preview, exact byte equality with prepared/repeated
generation, settings/revision invalidation, selections/clear, no eligible output,
message-count/character limits, bad configuration, nonblocking warning availability,
source immutability, count-only logs, cancellation/clear/conflict and busy recovery.
Desktop checks both seeds' help/accessibility and F1/Escape, both themes, direct
button routing, optional preview and retained Advanced Options after remount.
Existing publisher phase-cancellation, insertion/byte-proof, FHIR and workspace
regressions remain retained and run in the full suite.

| Independent tool viewport (DIP) | Automated result |
| --- | --- |
| 1200 × 800 | Pass: Generate and expanded advanced inputs reachable |
| 900 × 600 | Pass |
| 600 × 400 | Pass |
| 350 × 300 | Pass with scrolling |
| 250 × 250 | Pass with scrolling |

Each case repeats resizing through 250/1200/350 widths, verifies finite patient
list bounds and control identity, then remounts with values/expansion/help retained.
These are Avalonia headless geometry/state tests, not native mouse/visual evidence.

Final verification:

| Check | Result |
| --- | --- |
| Normal and isolated locked restores | Passed |
| Final Release solution build with warnings as errors | Passed, 0 warnings / 0 errors |
| Focused new Workspace tests | 14 passed after correcting a new test helper that attempted to Save As to an existing file twice |
| Focused new Desktop tests | 11 passed |
| Full Core/Desktop/Querying/Scripting/Workspace run | 2,188 passed, 0 failed, 0 skipped; 7m 03.861s |
| Protected preview | 2,384 / 2,384 files match baseline; no additions/missing files |
| Git whitespace check | Passed |

Full-suite module durations: Core 1.863s, Desktop 2m 22.097s, Querying 14.372s,
Scripting 28.956s, Workspace 3m 53.039s. All five tested DLL hashes match the final
isolated Release build; no source/test changes followed this complete run.

Ignored diagnostics live under `artifacts/fhir-generation-ux/`: locked restore logs,
`release-build.log`, `full-tests.log`, protected baseline check, verified binary
hashes, change manifest and initial/final Git status. These contain verification
metadata and synthetic-only test output; no generated HL7/CSV export is retained.
The accidentally nested copies created while staging test runtimes were explicitly
removed; normal bin/obj/publish outputs and uncertain preexisting artifacts remain.
Permanent tests are in the test projects; no existing tests were weakened/removed.

Reproduce the full suite from the hash-verified standard-shaped test runtime copies:

```powershell
dotnet test --test-modules 'tests/*/bin/Stage15UI/net10.0/*.Tests.dll' `
  --root-directory C:/Dev/HL7Workbench --max-parallel-test-modules 1 `
  --minimum-expected-tests 2188
```

Build uses `--artifacts-path C:/Dev/HL7Workbench/artifacts/stage15ui/verification`
and the existing `_WorkbenchWorkerBuildRoot` / `_QueryHostBuildRoot` overrides for
that isolated ScriptHost/QueryHost Release output; normal running Release and
protected beta outputs were not rebuilt.

## Files for this polish

- Workspace: `SyntheticPatientsSession.cs`.
- Desktop: `SyntheticPatientsViewModel.cs`, `SyntheticPatientsView.cs`,
  `SyntheticPatientsView.Layout.cs`, `SyntheticGenerationDialog.cs`.
- Tests: `FhirDirectGenerationTests.cs`, `FhirGenerationUxTests.cs`.
- Documentation: this report, PROJECT_CONTEXT, FHIR workspace/import guides,
  ADR 0015, Stage 15UI FHIR acceptance amendment, Stage 8D generation contract
  and README's generator instructions.

Prior uncommitted Stage 15A–15UI changes are retained. No generator source, data
mapping, SQL engine, docking identity, dependency or accepted release artifact changed
as part of this polish.

Final Git: `main` at `f2a5cecc9ee7817c1d4d4ed3098e934c17da2bf6`; 59 modified
tracked paths and 58 untracked files including earlier milestone work, empty index.
This polish changes 15 paths relative to its initial worktree snapshot. No commit,
push, release packaging or later milestone work occurred.

## Native Windows checklist still required

Use the verified isolated executable, not protected previews or a preexisting running
normal Release process:
`C:\Dev\HL7Workbench\src\HL7Workbench.Desktop\bin\Stage15UI\net10.0\HL7Workbench.Desktop.exe`.
The build source is
`C:\Dev\HL7Workbench\artifacts\stage15ui\verification\bin\HL7Workbench.Desktop\release\HL7Workbench.Desktop.exe`.

1. Import synthetic FHIR, select a patient and Generate directly. Expect one new
   independent unsaved document with its Generation Report; source unchanged.
2. Preview separately: no new tab. Generate and compare saved bytes for identical
   inputs. Change seed/types/time/patient/scope/selection and verify recalculation.
3. Selected Events: select targeted events, Generate, clear selection, verify disabled
   action and explanation; Selected Patient/Entire Import still use full histories.
4. Try invalid seed/time, an empty eligible scope and an over-limit fixture. Expect
   explanation and no output; nonblocking omissions still allow the report/output.
5. Cancel preparation/generation on a larger synthetic session. Expect no partial tab,
   restored buttons and retained completed import; retry successfully.
6. Hover Seed labels/inputs in FHIR and Generate Messages, light/dark themes. Tab to
   input; verify native hover placement, readability and screen-reader help. F1 is
   not an advertised help workflow.
7. Advanced Options starts collapsed. Configure both values, collapse/reopen, resize
   bottom/right/floating tools through the matrix and redock. Values/expansion remain,
   controls scroll into reach and patient/timeline lists remain usable.

Known limits: phase status rather than numeric preparation progress; all prior partial
FHIR mapping/output limits remain. Seed help does not promise cross-version identity
or uniqueness across repeated batches. No native Windows or screen-reader testing
was performed by this automated pass.
