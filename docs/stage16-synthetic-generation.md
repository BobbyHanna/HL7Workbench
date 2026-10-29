# Stage 16: connected synthetic HL7 generation

## Stage 16D — realistic standalone demographics (2026-10-29)

New **Standalone messages** and scenario dialog configurations both select
**Realistic Synthetic** and start with a blank automatic Seed. The demographic
selector is available in both modes. Standalone ADT A01/A04/A08, ORM O01 and ORU R01
use **generator v2** for realistic output; **Obvious Placeholders** selects the
original standalone **v1** algorithm. Scenario generator v2 retains both existing
realistic output and original placeholder bytes. Earlier milestone descriptions
below are historical; this section supersedes their standalone-only restrictions.

### Construction and compatibility

The internal Core `SyntheticDemographics` component owns the existing Stage 16A
ordered name/street/locality/facility catalogs and deterministic profile/provider/
location logic. Both engines consume it; no second demographic implementation,
dataset, dependency or randomization system was added. The refactor retains
scenario PRNG domains, ordering, clinical draws and golden bytes.

The standalone v2 path renders the **unchanged v1 template and clinical stream**,
then projects existing demographic/identifier fields using Core-parsed materialized
field ranges. It validates the complete bounded result with Core's parser/validator
and serializes through Core. Segment order/count, field boundaries, CR terminators,
v2.5.1, processing ID T, clinical values, panels, units and timestamps remain the
same. No facility-name segment or extra fields are added. Curated facility codes
are shared by MSH, PL, CX and EI authorities; their human-readable catalog labels
remain available to configuration hints. Provider identity is shared deliberately
between EVN/PV1, with a distinct ordering provider shared by ORC/OBR.

Realistic output uses plausible fictional American-style adult profiles, structured
XPN/XAD/XTN values, optional reserved 555-0100..0199 example phones, synthetic PRN
provider IDs and routine MEDSURG/OUTPATIENT locations. MR, VN, compact SYN2 message,
PO, FO, PRV and facility namespaces are distinct. MRNs are independent of MSH-10;
MSH-10 is unique within a batch and at most 20 characters. Seed replay and cross-batch
identifier collisions are intentional; IDs are not globally unique. No SSNs or NPIs
are generated. Names, facility labels and marked synthetic addresses can coincide
with real entities; the address marker is not a reserved postal namespace.
Literal components use Core's escape codec; overrides cannot inject HL7 separators.

There is **no persisted generator preset/configuration store** in this repository;
the dialog remains session-local. For old serialized/programmatic Core standalone
options, **absence of DemographicStyle still means ObviousPlaceholders** and the
explicit programmatic seed default remains 42. New dialog configurations explicitly
choose RealisticSynthetic. Old JSON inputs and newly serialized explicit styles are
regression tested. To reproduce old standalone bytes, select Obvious Placeholders
and reuse the same seed, type, count, prefix and base timestamp. The original
renderer, observation helper and xorshift implementation are unchanged.

### Seeds, overrides, reports and publication

Stage 16B/C behavior is preserved: blank Seed resolves **one** cryptographic uint
after configuration/target preflight and before generation. Preview/open/validation
never select a seed. Repeated Generate operations draw afresh; explicit 0, 42 and
all uint values bypass selection and reproduce identical complete settings/version.
Negative seeds remain unsupported. Automatic drafts retain unset seed-dependent
facility defaults until the actual seed is known. A normalized configuration pins
those defaults for replay; explicitly supplied codes always win, even old defaults.

Advanced options expose applications/facilities and patient ID prefix for realistic
standalone messages as well as scenarios. They validate before seed selection.
Standalone placeholder v1 never supported these overrides; non-default transport
or patient-prefix overrides produce a validation error asking the user to select
Realistic Synthetic or clear overrides, rather than being ignored. Scenario clinical
overrides/panels retain their meaning and visibility; standalone clinical templates
remain fixed. Switching modes/styles retains editable drafts; automatically resolved
values never populate Seed or override fields.

The existing Generation Report includes selected style, generator version, actual
seed and automatic/explicit provenance for both modes. **Copy seed** uses the same
platform clipboard action; paste it into Seed with the same complete configuration
to replay. Reports remain session-only count summaries without patient values.
Cancellation, failed generation and staged verification do not publish partial
batches or replace the previous report. A user retry starts a fresh operation;
there is no internal seed redraw. The same prospective-save/index/exact-byte proof,
publication transaction and single undo/history operation remain authoritative.

### Stage 16D verification and modified files

Both normal and independent-artifacts locked restores passed. Release build passed
with **0 warnings / 0 errors**. Focused Core generation: **132 passed / 0 failed /
0 skipped**; Workspace: **133 / 0 / 0**; Desktop: **108 / 0 / 0**. Added **58 permanent
ordinary cases** (39 Core, 11 Workspace, 8 Desktop). Existing legacy seed tests now
explicitly select placeholders where appropriate; original byte/golden assertions
remain. Old report assertions now expect explicit standalone style metadata.
The exact full regular command `dotnet test HL7Workbench.sln --configuration
Release --no-build --no-restore` passed **2,547 / 0 / 14** (2,561 total) in
**4m 47.113s**. All five test projects passed; the 14 opt-in identities are unchanged.

The new coverage exercises all five realistic types, demographic variation/formats,
provider/facility and independent identifier domains, unchanged clinical/template
fields, 0/42/uint-max and JSON compatibility, exact replay, automatic defaults and
single seed draws, advanced override validation, escaped literal components,
culture independence, maximum batches, cancellation at four generation/publication
phases, atomic insertion/history/report retention, mode/style drafts and Copy seed.

22 Stage 16D delta paths (17 updated existing files / 5 new permanent files):

| Area | Repository-relative paths |
| --- | --- |
| Shared Core and options | `src/HL7Workbench.Core/SyntheticDemographics.cs` (new), `SyntheticGenerationOptions.cs`, `SyntheticMessageGenerator.Realistic.cs` (new), `SyntheticMessageGenerator.cs`, `SyntheticScenarioGenerator.Demographics.cs`, `SyntheticScenarioGenerator.cs`, `SyntheticScenarioOptions.cs` |
| Workspace | `src/HL7Workbench.Workspace/SyntheticGeneration.cs` |
| Desktop | `src/HL7Workbench.Desktop/ViewModels/SyntheticGenerationViewModel.cs`, `src/HL7Workbench.Desktop/Viewer/SyntheticGenerationDialog.cs` |
| New regression files | `tests/HL7Workbench.Core.Tests/StandaloneDemographicTests.cs`, `tests/HL7Workbench.Workspace.Tests/StandaloneDemographicTests.cs`, `tests/HL7Workbench.Desktop.Tests/StandaloneDemographicViewTests.cs` |
| Updated regression files | `tests/HL7Workbench.Workspace.Tests/StandaloneSeedTests.cs`; `tests/HL7Workbench.Desktop.Tests/StandaloneSeedViewTests.cs`, `SyntheticDemographicViewTests.cs`, `SyntheticGenerationViewTests.cs` |
| Documentation | `README.md`, `docs/PROJECT_CONTEXT.md`, `docs/architecture.md`, `docs/decisions/0012-deterministic-synthetic-generation.md`, `docs/stage16-synthetic-generation.md` |

Source audit confirms the original standalone renderer/observation and PRNG/helper
bodies match HEAD; all existing standalone/scenario goldens pass. All 2,384 protected
beta files match their pre-16D SHA-256 hashes, with no missing/additional files.
The same 14 opt-in identities/classification sources are retained. Final evidence,
baseline/delta hashes, restore metadata and reviewed failure diagnostics are retained
under ignored `artifacts/stage16d`. Six redundant intermediate logs were removed;
distinct failure diagnostics and final verification logs remain. No probes or
HL7/CSV exports were created. Combined Stage 16–16D status is 36 unstaged paths
(17 modified tracked / 19 new), with an empty index and unchanged main/HEAD
`a5a982d878c6dcf10b136fd26d6b65536918cb99`. Prior work outside this stage's delta
is retained. Limits remain unsigned seeds, possible random/cross-batch collisions,
session-only reports, adult synthetic profiles and complete-settings/version replay.

### Native Windows acceptance pending

Generate every standalone type with default realistic style and blank Seed twice;
inspect profiles, provider/order links, actual report seeds and v2 style metadata.
Copy and explicitly reuse a seed with identical settings; compare saved bytes.
Repeat explicit 0/42, then select Obvious Placeholders and compare prior v1 bytes.
Switch modes/styles with blank and explicit seed drafts, exercise advanced overrides
and their validation, Cancel/retry during generation/publication, existing-document
insert/undo/redo and report retention. Check keyboard/F1 help, scrolling, Copy seed,
clipboard/focus and narrow/high-DPI layouts in both themes. Native visual/clipboard
acceptance remains an owner gate. No commit, push, publish, package or Stage 17.

## Stage 16C — automatic seeds for all HL7 generation (historical 16C behavior)

Both **Standalone messages** (all five v1 types) and **scenario generation** now
start with an optional **blank Seed**. Each valid Generate operation with a blank
field uses the same Stage 16B Workspace cryptographic provider, exactly once after
configuration/destination preflight and before generation. Preview, dialog opening,
validation and internal generator calls consume no randomness. A repeated Generate
with a blank field starts a new operation with a fresh draw; random collisions are
possible in the finite uint space, so this is not a global uniqueness guarantee.

Explicit decimal seeds **0 through 4294967295**, including **42**, remain exact
deterministic inputs. Negative values are unsupported by the existing unsigned seed
type. All five standalone v1 templates, field formatting, identifier algorithms and
placeholder demographics are unchanged. Use explicit 42 to reproduce the former
default output; blank intentionally no longer means 42. Core v1/v2 algorithms,
versions and goldens remain unchanged; no second randomization system is introduced.

Generation Report shows the actual resolved seed and automatic/explicit provenance
for standalone and scenarios. **Copy seed** copies that value; paste it into Seed
with the same message type/scenario, count, prefix, base timestamp and other settings
to reproduce the bytes. Neither a successful operation nor cancellation writes its
resolved seed into the editable field. Standalone and scenario drafts remain separate:
their initial values are blank, manually entered values survive switching modes,
and automatic operation results do not become either mode's explicit draft.

Cancellation after selection never redraws within that operation. Canceled/failed
publication preserves prior content/history/report; a later user retry selects one
new seed. Invalid configuration, invalid destinations and pre-cancellation consume
none. The existing atomic publication and output limits stand. Secure selection
failure does not fall back to 42; supply an explicit seed and retry.

Core options continue to carry explicit uint seeds (programmatic default 42), so
direct deterministic callers keep their existing contract. Both Workspace methods
support `automaticSeed: true`: `GenerateAsync` for standalone and
`GenerateScenariosAsync` for scenarios. The UI selects that flag only for blank
fields. The original constructor provider parameter remains source-compatible and
serves both flows. Scenario drafts still resolve seed-dependent automatic facility
defaults only after the actual seed is known.

Native Windows acceptance: generate each standalone type twice with blank Seed;
inspect actual reported seeds, copy one and regenerate with identical settings,
then compare saved bytes. Repeat explicit 0/42, Cancel/retry, existing-document
insertion/undo/redo, and standalone/scenario switching with blank and explicit
drafts. Check Copy seed and scrolling/keyboard behavior in both themes and narrow/
high-DPI layouts. Native acceptance remains pending; there is no commit, push,
packaging or Stage 17 work.

### Stage 16C verification and changed files

Verification on 2026-10-29: normal/independent locked restores passed; Release
build passed with **0 warnings / 0 errors**. Focused Core generation: **93 passed /
0 failed / 0 skipped**; Workspace: **122 / 0 / 0**; Desktop: **100 / 0 / 0**.
The exact regular command `dotnet test HL7Workbench.sln --configuration Release
--no-build --no-restore` passed **2,489 / 0 / 14** (2,503 total), in **4m 56.459s**.
The same 14 opt-in test identities are skipped; none reclassified or removed.
All 2,384 protected beta hashes are unchanged. The four Core generator files match
the pre-16C source hashes; standalone options have no tracked diff. All previous
goldens pass. Whitespace/conflict checks pass, including new files.

Added **43 permanent ordinary cases** (30 Workspace, 13 Desktop), covering all
five types with controlled automatic seeds, every type × explicit 0/42/uint-max,
byte-exact saved replay, actual report seeds/provenance, standalone clipboard copy,
preflight refusal without draws, post-resolution/staged cancellation and retry,
duplicate Generate, mode drafts, unsupported negatives and overflow. Existing
known-seed assertions now explicitly supply 42 or assert the new blank UI behavior;
their explicit-seed and compatibility checks remain. No test/fixture deletion or
unrelated weakening; focused counts overlap the full suite.

14 Stage 16C delta paths (12 updated existing files / 2 new permanent test files):

| Area | Repository-relative paths |
| --- | --- |
| Coordinator/UI | `src/HL7Workbench.Workspace/SyntheticGeneration.cs`; `src/HL7Workbench.Desktop/ViewModels/SyntheticGenerationViewModel.cs`, `Viewer/SyntheticGenerationDialog.cs` |
| New tests | `tests/HL7Workbench.Workspace.Tests/StandaloneSeedTests.cs`, `tests/HL7Workbench.Desktop.Tests/StandaloneSeedViewTests.cs` |
| Updated tests | `tests/HL7Workbench.Workspace.Tests/SyntheticSeedTests.cs`; `tests/HL7Workbench.Desktop.Tests/SyntheticGenerationViewTests.cs`, `SyntheticDemographicViewTests.cs`, `SyntheticSeedViewTests.cs`, `FhirGenerationUxTests.cs` |
| Documentation | `README.md`, `docs/PROJECT_CONTEXT.md`, `docs/decisions/0012-deterministic-synthetic-generation.md`, `docs/stage16-synthetic-generation.md` |

Combined Stage 16–16C state: main/HEAD a5a982d unchanged, empty index, 30 unstaged
paths (16 modified tracked / 14 new). Final logs, baseline/delta source hashes and
protected/Core/stress audits remain under ignored artifacts/stage16c; independent
restore metadata is retained. Redundant initial success logs were removed. No
disposable probes or generated HL7/CSV exports remain. Limits: uint seeds exclude
negatives, random draws can collide, reports are session-only, and exact replay
requires the same complete settings/version. Native clipboard/focus/high-DPI and
visual acceptance remain pending. No commit, push, publish, package or Stage 17.

## Stage 16B — automatic scenario seeds (historical 16B behavior)

Stage 16C above extends this initial scenario-only behavior to standalone generation.

The scenario Seed field is optional and **blank by default**. Each valid Generate
action with a blank seed selects one unpredictable unsigned 32-bit value using
`RandomNumberGenerator.Fill`, once at the Workspace operation boundary after
configuration/destination preflight. That resolved value is passed explicitly into
the unchanged deterministic Core engine and retained in result options and the
Generation Report. There is no random selection per patient, message, preview,
normalization or internal renderer call. The seed space is finite; random draws
are not a global uniqueness guarantee.

An explicit decimal seed is used exactly, including **0**, **42** and **4294967295**.
With the same quantities, base time, demographic style, overrides and generator
version it reproduces the same bytes. The report displays the actual seed and
whether it was automatic or explicit. Use **Copy seed**, then paste that value
into Seed with the same settings to reproduce an automatic batch. The complete
report also remains selectable for manual copying if the clipboard is unavailable.

The UI keeps separate scenario/standalone seed drafts. Standalone v1 still starts
at 42 and uses 42 when blank; switching modes does not silently carry its default
42 into a new scenario. Scenario previews say "automatic (chosen on Generate)";
seed-dependent automatic facility hints remain unresolved until execution. Explicit
application/facility overrides still take precedence. The UI passes unnormalized
scenario drafts so provisional seed-42 validation cannot freeze automatic facilities.

Validation/preview never requests a seed. Rejected preflight and pre-cancellation
consume none. Once selected, a seed belongs to that one operation through generation
and atomic publication. Cancellation after selection discards the pending batch and
retains existing content/history/report. A subsequent Generate retry is a new
operation with one new draw; duplicate Generate while busy does not start another.
No implicit retry, fallback to 42, timestamp seeding or random GUID is used. If
secure selection fails, enter an explicit seed and retry. Core scenario version 2,
both style goldens, standalone v1 bytes and the existing transaction remain unchanged.

Programmatic Core options still specify a deterministic seed (default 42). Existing
Workspace calls preserve explicit-seed behavior. Programmatic callers can opt into
automatic selection with `GenerateScenariosAsync(options, automaticSeed: true)`;
the optional coordinator seed provider is a controlled testing seam, defaulting to
the cryptographic provider. It is never invoked for explicit or standalone requests.
Normalization still captures transport defaults; automatic callers should pass a
fresh unresolved draft when they want automatic facility defaults to follow the seed.

Native acceptance: leave scenario Seed blank and generate twice; inspect each actual
reported seed and output. Copy the first seed and regenerate with identical settings;
saved bytes should match the first batch. Repeat with explicit 0 and 42, both styles,
Cancel/retry, mode switching, narrow Light/Dark layouts and report close/reopen.
Standalone blank/default generation must retain its previous bytes. Native clipboard,
keyboard/high-DPI behavior remains an owner acceptance gate.

### Stage 16B verification and changed files

Verification on 2026-10-29: normal and independent locked restores passed; Release
solution build passed with **0 warnings / 0 errors**. Focused Core: **93 passed /
0 failed / 0 skipped**; Workspace: **92 / 0 / 0**; Desktop: **87 / 0 / 0**.
The exact regular command `dotnet test HL7Workbench.sln --configuration Release
--no-build --no-restore` passed **2,446 / 0 / 14** (2,460 total), in **4m 16.336s**.
The 14 skipped test identities match the prior opt-in inventory; none reclassified.
All 2,384 protected beta SHA-256 hashes and the four Core generator source hashes
match the pre-16B baseline. Both existing scenario goldens and standalone v1 tests
pass. Whitespace/conflict checks passed, including new files.

Added 23 permanent ordinary cases (14 Workspace, 9 Desktop), using controlled seed
providers rather than probabilistic collision assertions. They verify one call per
automatic operation, no calls during preview/validation or explicit/standalone runs,
exact 0/42/uint-max use, actual report/options seed agreement, seed-dependent facility
resolution, byte-identical replay, all staged cancellation phases, fresh retries,
duplicate Generate rejection, separate UI drafts, and actual clipboard copy/paste.
Prior demographic tests now explicitly supply 42 where they assert a known seed's
facility rather than assuming a blank scenario still means 42. No tests or fixtures
were removed or weakened; focused counts overlap the full suite.

12 Stage 16B delta paths: 10 prior paths updated, 2 new permanent test files:

| Area | Repository-relative paths |
| --- | --- |
| Workspace | `src/HL7Workbench.Workspace/SyntheticGeneration.cs`, `SyntheticGenerationReport.cs` |
| Desktop | `src/HL7Workbench.Desktop/ViewModels/SyntheticGenerationViewModel.cs`; `Viewer/SyntheticGenerationDialog.cs`, `Viewer/GenerationReportView.cs` |
| Tests | New `tests/HL7Workbench.Workspace.Tests/SyntheticSeedTests.cs`, new `tests/HL7Workbench.Desktop.Tests/SyntheticSeedViewTests.cs`; updated `tests/HL7Workbench.Desktop.Tests/SyntheticDemographicViewTests.cs` |
| Documentation | `README.md`, `docs/PROJECT_CONTEXT.md`, `docs/decisions/0012-deterministic-synthetic-generation.md`, `docs/stage16-synthetic-generation.md` |

Combined Stage 16/16A/16B status: main/HEAD a5a982d unchanged, empty index, 26 unstaged
paths (14 modified tracked / 12 new). Final evidence, baseline/delta hashes, protected/
Core/stress checks and the initial resolved clipboard-namespace compile diagnostic
remain under ignored artifacts/stage16b. Redundant intermediate success/duplicate
logs were removed; independent restore metadata is retained. No disposable probes,
HL7/CSV exports, fixture deletions, packages or protected beta changes were retained.
Native acceptance remains pending. No commit, push, publish, packaging or Stage 17.

## Stage 16A — realistic synthetic demographics

Scenario generator **version 2** adds **Realistic Synthetic**, the default for new
scenario configurations, and **Obvious Placeholders**, which retains the original
scenario-v1 bytes when given the same original inputs. The existing standalone
generator remains version 1 with unchanged defaults, all five message types and
its permanent golden output. The demographic selector applies to scenarios only;
switching back to Standalone messages does not apply a scenario style implicitly.

This extends the existing engine and shared contexts. It adds no generator service,
package, demographic API, source-record input or dependency on Scripting. The
existing Scripting identity library and its versioned output remain unchanged.

### Realistic profiles and safety boundaries

The engine bundles a small original ordered vocabulary of common given/family/
middle names, street words, fictional locality combinations and facility labels.
There is no patient dataset, real address directory, production message template,
name lookup or runtime external service. Profiles are deterministic combinations,
not copied person records. Names are uppercase XPN/XCN components; some patients
have no middle name, an initial or a full middle name. Administrative sex varies
across F/M/O/U. Adult dates of birth produce ages 18–89 at the patient's first
encounter, using calendar arithmetic relative to the explicit base timeline;
demographics stay unchanged across that patient's later encounter messages.

Addresses use a plausible house number/street, city text, a two-letter American
state code, five-digit ZIP formatting (including leading zeros), USA and home type.
XAD/PID-11.2 is always **SYNTHETIC TEST ADDRESS**, the documented marker for this
feature's constructed address namespace. ZIPs and city/state pairings are formatting
examples, not postal validation or deliverability claims. **An arbitrary generated
name, street, city or facility name can coincide with a real person, residence or
institution. The test marker is not an officially reserved postal namespace.**

Some profiles omit a phone number. Others use XTN country/area/local components
and only **555-0100 through 555-0199**, the fictional nonworking range documented
by [NANPA](https://nanpa.com/numbering/555-line-numbers). Other 555 numbers are not
assumed fictional. The finite reserved pool can repeat and is not an identity key.
No SSN, verified NPI, real provider credential or externally validated identifier
is generated. Realistic-looking data is not guaranteed disjoint from real-world
identities except where a value space is explicitly reserved, such as these phone
subscriber numbers. Generated values remain test data, not clinical information.

### Providers, facilities and clinical locations

Each encounter owns consistent attending and ordering providers with plausible
names, separate synthetic IDs and roles supplied by PV1-7 vs ORC-12/OBR-16. Their
XCN assigning authority is SYN-PROVIDER and identifier type is PRN; they are not NPIs.
Patient/visit/provider IDs do not depend on message-control ordinals.

Seed-selected fictional facility codes refer to a small named catalogue, including
North Valley Medical Center, Cedar Ridge Community Hospital and Cedar & Pine
Medical Center, with corresponding laboratory endpoints. These labels are invented
examples, not verified unique institution names. Codes carry a SYN prefix and remain
consistent in MSH, location, patient/visit assigning authorities and related orders.
Full ordering facility name/code use ORC-21 XON components 1/10; ADT messages carry
facility codes and the dialog shows their catalogue labels. No later-version MSH
fields or new segments are introduced. Names containing reserved delimiters are
escaped through Core's existing codec. See the standard's
[ORC-21 definition](https://www.hl7.eu/HL7v2x/v251/std251/ch04.html) and
[XPN/XCN/XON/XTN definitions](https://www.hl7.eu/HL7v2x/v251/std251/ch02a.html).

Routine inpatient encounters use MEDSURG with plausible room/bed coordinates. An
optional transfer moves bed A to B in the same ward; PV1-6 retains the prior
location and all subsequent events retain the new bed. Outpatient encounters use
OUTPATIENT locations and cannot request inpatient transfer. Demographic styling
does not invent ICU/ED severity, additional clinical events or new reference ranges.
Both curated panels, their values/ranges/units/statuses and order/result links are
unchanged when switching between demographic styles.

### Defaults, explicit overrides and identifiers

The demographic style is visible with scenario quantities. Advanced application/
facility text boxes start blank: their placeholder text shows effective automatic
defaults. Realistic defaults are EHR, a synthetic named hospital, LAB-INTERFACE and
that catalogue's synthetic laboratory. Placeholder defaults are SYN-GENERATOR,
SYN-FACILITY, SYN-RECEIVER and SYN-DEST.

Any nonblank explicit application/facility code takes precedence, even if its value
equals a previous SYN-* default. It is validated/canonicalized under the existing
ASCII/length rules and propagated consistently. Unknown explicit facilities use
their own code as their label rather than receiving a manufactured institution name.
Normalization captures resolved transport values; changing seed/style on an already
normalized options snapshot retains those captured explicit values. The dialog builds
fresh options from its automatic/explicit drafts, so automatic values follow changes
while explicit values stay fixed. Existing prefix/order-code/status/numeric overrides
retain their validation and shared propagation; no arbitrary field replacement is added.

| Domain in Realistic Synthetic | Content identity / authority |
| --- | --- |
| Patient MRN | SYN + configured patient/default prefix + seed + MR + patient ordinal; CX authority is sending facility, type MR |
| Visit | SYN + default prefix + seed + VN + encounter ordinal; CX authority is sending facility, type VN |
| Message control | SYN2 + seed + global message ordinal, 18 characters, unique within batch |
| Placer order | SYN + default prefix + seed + PO + order ordinal; EI namespace SYN-PLACER |
| Filler order | SYN + default prefix + seed + FO + order ordinal; EI namespace SYN-FILLER |
| Provider | SYN-PRV + seed + encounter ordinal + AT/OR role; XCN authority SYN-PROVIDER, type PRN |
| Facility | SYN-prefixed catalogue code or an explicit configured code |

These are fictional local namespaces, not registered universal identifiers. Prefixes
and ordinals distinguish domains; neither patient nor visit ID is copied from MSH-10.
Global uniqueness across independently generated batches is not promised.

### Determinism and compatibility

Version 2 uses the same specified xorshift32 implementation. Fixed domain-separated
seed streams select patient profiles, providers, facilities and care locations in
explicit array order. There is no clock, GUID, machine state, live terminology or
nondeterministic collection ordering in output. Identical normalized configuration,
seed, base time, demographic style and version yield identical UTF-8 bytes with CR
endings. A new realistic default intentionally differs from scenario version 1;
its own golden hash pins version 2. Future vocabulary/algorithm changes must review
that versioned contract.

Obvious Placeholders retains the original names, addresses, U sex, SYN-WARD/
SYN-CLINIC locations, IDs, SYN1 message-control spelling and PRNG consumption. The
original scenario golden hash remains C6D6324905DEF434F1CF0DEF7FAE3CE2BACAD90DD41CDD4444F9E2BB4B4220E9.
Both styles retain the old clinical stream's three patient draws, so demographic
styling cannot change curated clinical values. Reports identify the current engine
version (2) and the chosen style; legacy byte preservation does not relabel the
current engine as version 1. There is no persisted scenario-preset migration; all
existing standalone version-1 presets/APIs remain unchanged.

### Stage 16A native Windows acceptance additions

Use the original checklist below for publication/history/cancellation. Also verify:

1. Open Home/File → Generate Messages, select each scenario and confirm Realistic
   Synthetic is selected, with unchanged count estimates. Generate and inspect
   plausible names, optional middle names, DOB/sex, marked address and structured
   reserved phone; compare demographics throughout each patient's messages.
2. Inspect attending/ordering provider names, stable local IDs and roles. Confirm
   MEDSURG A→B transfer with prior location, or OUTPATIENT without transfer. Review
   hospital/laboratory codes and the full escaped ORC-21 ordering facility name.
3. Expand Advanced options. Confirm blank fields show automatic code/name defaults.
   Enter custom codes, including the old SYN-FACILITY/SYN-DEST values; change seed
   and style and confirm explicit overrides remain. Clear them to restore automatic
   values. Review patient-prefix/order-code/status/numeric override propagation.
4. Generate Obvious Placeholders with the previous settings and compare a saved
   output hash to prior scenario output. Generate all five standalone types and
   confirm original placeholder output; changing the scenario style must not alter it.
5. Generate/save identical realistic settings twice and compare SHA-256; repeat
   with another seed/base date. Review report version/style and absence of patient
   details; style metadata must survive tool close/remount and document history.
6. Check both themes and keyboard focus at 380×600 and larger sizes: style selector,
   advanced automatic-value hints, scrolling, preview and Generate/Cancel remain
   usable. Check high-DPI/native screen-reader behavior separately.

The marker and reserved phones do not certify clinical appropriateness or authorize
transmission. No automatic transmission, packaging, commit/push or Stage 17 is added.

### Stage 16A verification and changed files — 2026-10-28

| Gate | Result |
| --- | --- |
| Normal and independent locked restores | Passed |
| Release solution build | Passed, 0 warnings / 0 errors |
| Focused Core synthetic generation | 93 passed / 0 failed / 0 skipped |
| Focused Workspace generation / compatibility | 78 passed / 0 failed / 0 skipped |
| Focused Desktop generation / report / FHIR UX | 78 passed / 0 failed / 0 skipped |
| Full regular solution | 2,423 passed / 0 failed / 14 skipped; 2,437 total; 3m 51.447s |
| 1,000-message measurements, both styles | 2 passed / 0 failed / 0 skipped; overlap the Core suite |
| Stress identities | Same 14 opt-in skipped cases; none reclassified or removed |
| Protected private-beta files | All 2,384 SHA-256 hashes unchanged; none added/missing |
| Whitespace / conflict checks | Passed, including new files |

The exact regular command remains
`dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore`.
There are **32 new permanent ordinary cases**: 22 Core, 2 Workspace and 8 Desktop.
They cover both styles, realistic formatting/variation, stable demographics and
provider roles, adult/leap/boundary dates, reserved phones, location/facility/identifier
domains, escaping, seed/culture/golden determinism, preserved panel values, explicit
old-default overrides, byte compatibility, exact save/history, report style and
responsive themed selector behavior. Existing placeholder-specific assertions now
select that explicit style; original golden coverage and all five standalone
types remain. No fixture/test was deleted or weakened. Focused counts overlap.

Observed single-process Core measurements (including parse/serialization checks):

| Style, 1,000 messages | Characters | Cumulative allocated bytes | Generation time |
| --- | ---: | ---: | ---: |
| Realistic Synthetic | 859,510 | 315,546,208 | 213.5 ms |
| Obvious Placeholders | 818,294 | 256,269,368 | 149.3 ms |

These are local observations, not throughput/peak-memory guarantees. They exclude
Workspace staging/UI time. The engine retains bounded strings and only one parsed
message at a time; both cases assert the unchanged count/character limits and a
512 MiB cumulative allocation ceiling. The ordinary stress classifications were
preserved; opt-in SQL actual-cap tests were not executed in this regular run.

Stage 16A updates 14 prior Stage 16 paths and adds 3 paths (17 delta paths):

| Area | Repository-relative paths |
| --- | --- |
| Core | `src/HL7Workbench.Core/SyntheticScenarioOptions.cs`, `SyntheticScenarioGenerator.cs`; new `SyntheticScenarioGenerator.Demographics.cs` |
| Desktop | `src/HL7Workbench.Desktop/ViewModels/SyntheticGenerationViewModel.cs`; `Viewer/SyntheticGenerationDialog.cs`, `Viewer/GenerationReportView.cs` |
| Workspace | `src/HL7Workbench.Workspace/SyntheticGeneration.cs`, `SyntheticGenerationReport.cs` |
| Permanent tests | New `tests/HL7Workbench.Core.Tests/SyntheticDemographicTests.cs`, new `tests/HL7Workbench.Desktop.Tests/SyntheticDemographicViewTests.cs`; updated `tests/HL7Workbench.Core.Tests/SyntheticScenarioTests.cs`, `tests/HL7Workbench.Workspace.Tests/SyntheticScenarioTests.cs` |
| Documentation | `README.md`, `docs/PROJECT_CONTEXT.md`, `docs/architecture.md`, `docs/decisions/0012-deterministic-synthetic-generation.md`, `docs/stage16-synthetic-generation.md` |

Combined Stage 16/16A Git state: **24 unstaged paths: 14 modified tracked / 10 new**,
empty index, `main`/HEAD `a5a982d` unchanged. No changes to Scripting's vocabulary,
standalone generator after the Stage 16 baseline, project/lock files, workflows,
fixtures or beta outputs. Final logs, measurement XML, baseline/delta hashes and
reviewed initial diagnostics remain under ignored `artifacts/stage16a`; independent
restore metadata is retained. Redundant intermediate success/help logs were removed.
The initial new golden-hash capture and obsolete TextBox.Watermark warnings are
retained as diagnostics; the final code uses PlaceholderText with no suppression,
and all final gates pass. No HL7/CSV exports or disposable probe projects remain.

Implementation and automated verification are ready for review. Native visual,
screen-reader, high-DPI and floating-window acceptance is still pending; use the
Stage 16A checklist above and the original workflow checklist below. No commit,
push, publish, packaging or Stage 17 work was performed.

## Phase A assessment and implementation plan

Preflight: clean `main` at a5a982d, with Stage 15D and CI stabilization rounds 1–3.
The 2,384 files under artifacts/private-preview are protected by a SHA-256 inventory
captured before implementation in artifacts/stage16/protected-before.json.

Existing Core version 1 generates standalone ADT A01/A04/A08, ORM O01 and ORU R01
with fixed seed/date/prefix defaults, xorshift32, CR segments and BOM-free UTF-8.
It has no presets, arbitrary field overrides or generation scripting hooks. Keep
its public contract and golden bytes. Workspace already provides background work,
throttled progress, captured-target revision checks and atomic prospective
save/index/byte proof followed by one insertion/history operation. Reuse that
pipeline for new documents and all four insertion positions.

The Generation Report tool currently displays FHIR mapping reports only. Add a
separate count-only synthetic report on MessageDocument, preserving the FHIR public
report property, tool identity, placement-only persistence and formatter behavior.
Reports describe the producing operation, not later edits or undo/redo.

Implementation phases:

1. Core: immutable scenario options/context, curated encounter/order/result events,
   a separately versioned deterministic renderer, targeted invariants and tests.
2. Configuration: checked count/date preflight and structured shared overrides;
   retain the existing 1,000-message / 4 Mi-character ceilings.
3. Workspace/Desktop: use the existing insertion transaction, add scenario choice
   and collapsed advanced settings to Generate Messages, and project synthetic
   counts through the existing Generation Report tool and indicator.
4. Hardening: cancellation/publication/history/encoding tests, theme/narrow UI
   tests, documentation, locked restores, Release build and full regular suite.

No new packages, parser, serializer, document editor, Dock identity or script
engine. The existing ScriptExecution.RunAsync operates on a captured document in
an isolated worker and publishes independent transformed output; it is not a
per-field generation hook. Generation-time scripting is deferred. Generated
documents can use that existing explicit post-generation workflow with unchanged
security, deadlines and resource limits. Typed immutable scenario overrides are
the customization boundary for a future separately designed integration.

## Scope and contracts

The following features are implemented; native Windows acceptance remains a
separate owner gate. No commit, push, package or Stage 17 work is authorized.

### Workflows

| Generate choice | Sequence per encounter |
| --- | --- |
| Standalone messages | Existing ADT A01/A04/A08, ORM O01 or ORU R01, version-1 bytes unchanged |
| Admission to discharge | Inpatient A01, optional A02, A03; outpatient A04, A03 |
| Order to result | One ORM O01 and one or more ORU R01 report updates for each order |
| Combined encounter | Admission/registration, optional inpatient transfer, orders and related reports, discharge |

Registration is an outpatient variation, not an extra event before inpatient
admission. An inpatient transfer is rejected for outpatient and order-only requests.
A01/A04 use ADT_A01, A02 uses ADT_A02 and A03 uses ADT_A03. Order/report structures
are ORM_O01 and ORU_R01. Stage 16 supports **v2.5.1 only**. Targeted structural
choices follow the standard's [patient administration](https://www.hl7.eu/HL7v2x/v251/std251/ch03.html),
[order](https://www.hl7.eu/HL7v2x/v251/std251/ch04.html) and
[observation](https://www.hl7.eu/HL7v2x/v251/std251/ch07.html) definitions; this is
not universal profile/schema certification. No production examples were copied.

Each patient retains one fictional name, birth date, address and medical record
identifier across encounters. Encounters have distinct visit IDs and shared
attending/ordering providers. The first encounter starts at the configured base;
successive encounters/patients occupy successive days. Transfer is at +15 minutes,
orders start at +30 minutes, and all report updates finish before +23-hour discharge.
Location changes on transfer and persists through the remaining messages. PV1-44
retains admission time; only A03 has PV1-45/discharge disposition.

Each order has distinct placer/filler numbers repeated in ORC and OBR in its ORM
and every ORU. OBR-4 service, provider and specimen remain paired. Reports update
the same collected observations, not independent new samples: earlier reports are
preliminary (P); the last defaults to final (F). OBR-25 and OBX-11 agree; ORC-5 is
scheduled (SC), partial (A), then complete (CM). ORC-9 follows each transaction;
collection precedes result reporting. OBX-3/4 identify observations within their
order, OBX-14 is collection time and OBX-19 is analysis/report time.

Two curated examples use explicitly local `99SYN` codes: chemistry (synthetic serum,
glucose and sodium) and blood count (synthetic whole blood, hemoglobin and white
blood count). Numeric values, units, illustrative ranges and flags are paired.
These examples are fictional test data, never clinical decision support.

### Configuration and overrides

Choose a mode in Home/File → Generate Messages. Standalone defaults are one
A01, blank automatic seed, fixed UTC 2030-01-01 and prefix SYN. Explicit 42 retains
the former default bytes. Scenario quantities include
patients, encounters/patient, orders/encounter and reports/order. An ORU report has
two numeric OBX observations. Basic controls include encounter class, optional
transfer, demographic style, seed, base timestamp and identifier prefix. Advanced scenario options
are initially collapsed and retain drafts during resize or mode changes.

| Option | Accepted bounds / behavior |
| --- | --- |
| Patients | 1–1,000, further constrained by whole-batch message count |
| Encounters/patient | 1–100 |
| Orders/encounter | 1–20 for scenarios with orders |
| Reports/order | 1–10, representing preliminary-to-final updates |
| Demographic style | Realistic Synthetic (new scenario default) or Obvious Placeholders (scenario-v1 byte compatibility) |
| Seed | Blank = one cryptographic seed per Generate for standalone and scenarios; explicit unsigned 32-bit values include 0/42; negatives unsupported |
| Base timestamp | Explicit offset/Z, whole-second UTC; base years 1900–9998; entire timeline must fit |
| Prefix / patient prefix override | 1–12 ASCII letters, digits, hyphens or underscores; uppercase |
| Sending/receiving application and facility | Blank UI fields use style/seed defaults; explicit codes contain 1–32 safe identifier characters, uppercase |
| HL7 version | 2.5.1; other versions rejected by Core |
| Clinical example | Chemistry or blood count |
| Local order code override | 1–24 safe identifier characters; changes service code consistently, retaining selected panel/99SYN |
| Last result status | F or P; earlier report updates remain P |
| First analyte override | Invariant decimal from -100,000 to 100,000; repeats through the order's report updates; flags reflect the illustrative range |

Blank optional overrides use the template defaults. Patient identities always
retain a SYN marker, including with a custom prefix. Unsupported delimiters,
control characters and unbounded literals are refused; this is a structured
override model, not raw replacement. Public `SyntheticScenarioOverrides.FromFields`
accepts only `PatientIdentifierPrefix`, `OBR-4.1`, `OBR-25`, `OBX-11` and `OBX-5`.
OBR-25/OBX-11 conflicts fail explicitly. Direct patient/visit/order/control-ID
overrides and arbitrary HL7 paths are rejected. OBR-4.1 and OBX-5 apply to the
selected panel/first analyte; they cannot redefine arbitrary clinical structures.

Core rejects inapplicable nondefault order settings/overrides for ADT-only requests.
The dialog retains hidden drafts when switching modes but constructs a request
using only visible/applicable settings. Transfer plus outpatient remains an explicit
validation error so a requested event cannot be silently dropped.

### Determinism

Scenario generator version 2 shares the established xorshift32 implementation;
standalone generator version 1 and its existing golden bytes are unchanged.
Identical normalized options, seed and generator version produce byte-identical
BOM-free UTF-8 with CR endings. There is no hidden clock, random GUID, machine
identity, imported record or environment dependency in the content. A fixed base
timestamp is the default even when no seed was typed. Dates normalize to UTC with
whole seconds, and numbers use invariant culture.

Patient/visit/order/provider IDs include synthetic prefixes, seed and explicit
ordinals. Scenario MSH-10 uses `SYN2-{seed:X8}-{ordinal:D4}` in Realistic Synthetic,
or the original `SYN1-{seed:X8}-{ordinal:D4}` in Obvious Placeholders (18 characters), within
the v2.5.1 [MSH-10 length](https://www.hl7.eu/HL7v2x/v251/std251/ch02.html).
IDs are unique within the batch; repeated requests intentionally repeat content.
Cross-batch global uniqueness is not promised. Workspace session GUIDs, elapsed
durations and private staging locations never enter the HL7 bytes. Changing
generator version may change output; permanent golden tests pin each contract.

### Limits, output and reports

The exact estimate is patients × encounters × (ADT events + orders × (1 + reports)).
Each factor is validated before multiplication. More than 1,000 messages is
rejected before generation. The existing ceilings remain 4 Mi UTF-16 characters
per batch, 1 Mi per message and 16 Ki per segment, with no truncation. Existing
document count, encoding, edit/history and save limits still apply.

Generation and staged-save proof run in background work, with bounded progress and
cancellation checks. Core retains bounded output strings and validates/discards
one parsed message at a time. The same Workspace prospective-save/index/byte proof
preserves original source bytes/encoding/framing and publishes one history splice.
The proof uses temporary disk proportional to the complete prospective document.
New output opens as an independent dirty unsaved document with Edit Mode on and
the first generated message selected/focused. Existing targets retain all four
insertion positions, one-step undo/redo, normal dirty state and Save/Save As.

Cancel or close the generation dialog to cancel and drain work. Cancellation/failure
before the publication point preserves destination membership, text, selection,
revisions, save points, Redo and any previous report; no partial new tab opens.
Mutation/navigation/closure of a captured target rejects stale publication, while
switching active tabs does not retarget the operation. Atomic publication wins if
it has already completed before a later cancellation request is processed.

The existing Generation Report indicator/tool displays the most recent successful
synthetic batch's scenario, demographic style, counts, event breakdown, seed, base time, durations,
warnings and successful cancellation state. Reports have no patient identifiers,
names, order values, file paths or raw messages. A report is attached before a new
document opens; insertion updates the indicator/tool in place. Later edits and
undo/redo do not rewrite these original batch counts. When a destination also has
FHIR provenance, the existing FHIR report remains available beneath the synthetic
report. Reports survive tool close/remount for the document session, but are neither
saved in layout nor exported alongside HL7. Reopening a saved file has no report.
Rejected/canceled operations are described in dialog status and do not replace a
successful document report. No automatic transmission or external service is added.

### Known limitations and deferred work

These are small local test templates, not a longitudinal clinical simulator or
version/profile validation engine. One chosen clinical panel applies across a
request; report updates repeat two collected observations rather than adding new
specimens. No arbitrary segment injection, generic schema designer, additional
HL7 versions or generation-time script execution is included. The existing isolated
Script Editor can explicitly transform a generated document afterward; its results
have their own provenance and are not re-certified as coherent scenarios. No new
scripting engine, security bypass or API-reference overhaul is introduced.

## Native Windows acceptance checklist

Use the ordinary Release build; keep any manual exports under an owned ignored
artifacts/stage16-native directory. Do not overwrite fixtures or protected previews.

1. Open Generate Messages from Home and File. Generate all five standalone types;
   confirm original defaults, input help, editor focus, Save As and one-step history.
2. Generate inpatient admission/discharge with/without transfer, then outpatient
   registration/discharge. Confirm A01/A02/A03 vs A04/A03, stable PID/PV1 identity,
   old/new transfer location and discharge time. Transfer plus outpatient must refuse.
3. Generate two patients, two encounters each, two orders/encounter and three
   reports/order in Combined encounter. Inspect distinct visits/orders, consistent
   providers and identifiers, P/P/F results, ordered timestamps and final discharge.
4. Try Order to result with both clinical examples and custom facilities/patient
   prefix/order code/status/numeric value. Check ORM/ORU links, correct panel,
   specimen subcomponents, units and numeric flags. Review the report warnings.
5. Save identical seeded configurations twice to owned paths and compare SHA-256.
   Repeat with a changed seed/base date. Identical configurations must match exactly.
6. Try zero, oversized/multiplied quantities, invalid seeds/dates and delimiter
   injection. Estimate/refusal must occur before execution; no partial output.
7. Insert at each position in a synthetic mixed-delimiter/UTF-16/MLLP source, undo
   and redo once, Save As and reopen. Compare untouched source bytes. An incomplete
   draft or unsafe boundary must refuse insertion without changing the source.
8. Generate the 1,000-message ceiling into a new document and a larger existing
   synthetic source. Cancel and dismiss during generation/staged validation;
   confirm responsive UI, drained work, retained Redo/report and no partial tab.
9. Open View report, close/reopen/float/remount the same Generation Report tool,
   switch between generated/plain/FHIR documents, and append another batch to the
   same tab. Counts should follow the active document and latest producing batch;
   later edits/undo must retain original report counts.
10. Check Light/Dark/Follow System, keyboard tab order, Enter/Escape/close behavior,
    screen-reader labels, expanded Advanced options and scrolling at 380×600 and
    larger sizes. Generate/Cancel and preview remain reachable. Confirm the View
    menu categories and existing commands/layout are unchanged.

Headless layout/lifecycle tests do not establish native visual, screen-reader,
OS focus, floating-window or high-DPI acceptance. Record those owner results before
acceptance; packaging, Git publication and the next stage remain unauthorized.

## Initial Stage 16 file inventory (before Stage 16A)

21 Stage 16 paths: 14 modified tracked files and 7 new files. The index remains
empty. No original fixtures, permanent tests, workflows, project/lock files or
protected private-beta artifacts were changed.

| Area | Files (repository-relative) |
| --- | --- |
| Core | `src/HL7Workbench.Core/SyntheticMessageGenerator.cs` (only internal helper visibility); new `SyntheticScenarioOptions.cs`, `SyntheticScenarioGenerator.cs` |
| Workspace | `src/HL7Workbench.Workspace/SyntheticGeneration.cs`, `MessageDocument.cs`, `MessageDocument.Generation.cs`; new `SyntheticGenerationReport.cs` |
| Desktop | `src/HL7Workbench.Desktop/MainWindow.Generation.cs`; `ViewModels/SyntheticGenerationViewModel.cs`, `ViewModels/GenerationReportViewModel.cs`; `Viewer/SyntheticGenerationDialog.cs`, `Viewer/GenerationReportView.cs`, `Viewer/GenerationReportIndicator.cs` |
| Permanent tests | New `tests/HL7Workbench.Core.Tests/SyntheticScenarioTests.cs`, `tests/HL7Workbench.Workspace.Tests/SyntheticScenarioTests.cs`, `tests/HL7Workbench.Desktop.Tests/SyntheticScenarioViewTests.cs` |
| Documentation | `README.md`, `docs/PROJECT_CONTEXT.md`, `docs/architecture.md`, `docs/decisions/0012-deterministic-synthetic-generation.md`; new `docs/stage16-synthetic-generation.md` |

## Initial Stage 16 automated verification — 2026-10-28

| Gate | Result |
| --- | --- |
| Normal locked restore | Passed |
| Independent locked restore to artifacts/stage16/verification | Passed |
| Release solution build | Passed, 0 warnings / 0 errors |
| Focused Core generation | 71 passed / 0 failed / 0 skipped |
| Focused Workspace generation / FHIR publication / compatibility | 76 passed / 0 failed / 0 skipped |
| Focused Desktop generation / report / FHIR UX | 70 passed / 0 failed / 0 skipped |
| Exact regular solution command | 2,391 passed / 0 failed / 14 skipped; 2,405 total; 3m 56.086s |
| Opt-in stress classification comparison | Same 14 skipped test identities as the pre-Stage-16 baseline; none reclassified |
| Protected private-preview SHA-256 comparison | All 2,384 files unchanged, no added/missing files |
| Whitespace / scope | git diff --check passed; 21 Stage 16 paths, empty index, main/HEAD a5a982d unchanged |

The exact full command was:

```powershell
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
```

85 permanent ordinary regressions were added (44 Core, 20 Workspace, 21 Desktop).
Coverage includes all workflow variations, whole-batch/culture/golden determinism,
shared patient/visit/provider/order identity, statuses/locations/specimens and
timestamps, exact/over-count/date/character limits, unsupported/injection/conflicting
overrides, cancellation in generation and each prospective publication phase,
captured-target mutation/closure, save encoding/source-byte preservation, one-step
history, independent unsaved output, same-tab report replacement, FHIR provenance
retention, tool close/remount, shell activation/focus, narrow layout and both themes.
The 1,000-message Core regression also asserts output ceilings and a bounded
allocation budget; it does not claim a measured whole-process peak-memory guarantee.
Existing tests/fixtures were neither deleted nor weakened. Focused counts overlap
the full run and must not be added to it. The 14 opt-in SQL stress/cap cases were
not executed as part of this ordinary suite; their classifications are unchanged.

Final logs, protected/stress inventories, source hashes and reviewed initial failure
diagnostics remain under ignored artifacts/stage16. Redundant intermediate success
logs were removed; the independent restore's owned build metadata is retained.
Initial authoring diagnostics include test-signature/registry compile corrections,
recording the reviewed new golden bytes, and two headless input assertions that
needed the dispatcher to deliver TextChanged before inspecting the model. These
are resolved by the final gates. A runner-help invocation also hit an installed
.NET/MTP help-routing error; the actual required test commands above all succeeded.
No generated HL7/CSV exports or disposable probe projects were retained.

Implementation and automated verification are ready for owner review. Native
Windows/high-DPI/screen-reader/floating-window acceptance was not performed; use the
checklist above. No commit, push, publish, packaging or Stage 17 work was performed.
