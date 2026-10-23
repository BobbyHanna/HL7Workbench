# Stage 9 — de-identification assistance workflows

## Current beta follow-up — report and review status (2026-10-23)

Native beta testing found the expanded inline report and an unconditional review
label. The dedicated dockable **De-identification Report** now separates execution
completion, specific findings and general limitations. The original 28 transformed /
7 suppressed / 1 high-risk / 8 retained synthetic ADT case is explained by an
unqualified PV1-7 provider and three retained clinical/event fields (EVN-1, PV1-2,
PV1-4); five retained protocol/sequence fields add no specific review flag. These
are legitimate findings, not evidence of a failed execution. Other inputs must be
reviewed using their own paths; matching totals alone do not identify an input.

The existing summary CSV columns remain in order with two appended counts; a second
bounded findings CSV adds fixed reasons/coordinates without original values. The
Status and general-limitation wording have changed. Source and output edits label
their original report stale. Existing saved script copies can still export summary
counts; copies without findings show details unavailable. See
[the beta correction acceptance record](deidentification-report-fix-acceptance.md)
for current verification, limitations and native checks. Earlier expanded-panel,
one-report and unconditional-status descriptions below are historical.

This correction is unstaged/uncommitted on afe7f6576f67ce25e72caa298adc94694d611d56.
The verified portable ZIP is preserved; no replacement candidate is authorized.
Stop for user acceptance before a checkpoint or packaging.
Final correction verification passed: locked restore; Release 0 warnings/errors;
70 Scripting/API, 40 Workspace and 12 Desktop focused cases; 1,973 solution tests;
940 complete Workspace stress cases (376.903s); normal Current/Whole worker smoke,
API sync and all protected/frozen/ZIP/whitespace checks. No native acceptance claim.

## Native acceptance and checkpoint authorization

The user confirmed successful native Windows acceptance of Stage 9 de-identification,
realistic identities, document-associated Transformation Report and optimized branding
on 2026-10-20. The exact 41-path accepted manifest is unchanged. This supersedes the
historical pending-acceptance/no-commit statements below. One local checkpoint is
authorized after a fresh locked-restore/build/solution/stress/smoke/integrity gate:
`feat: add Stage 9 de-identification and application branding`.
The original artwork stays unchanged and is included; the local _TEST.hl7 stays
unchanged/untracked and excluded. No push/tag/package/release/publication. After the
checkpoint passes, separately authorized Stage 9C may proceed without committing it.

Fresh checkpoint gate (2026-10-20): locked restore passed; Release build passed with
zero warnings/errors; **1,504 solution tests** passed (2m 34.383s); **718 complete
Workspace stress tests** passed (4m 37.853s), with no failures/skips. Both ScriptHost
Current/Whole lifecycles passed against the existing Release layout (no publishing).
Four icon-processing tests, eight exact tracked fixture blobs, all 13 protected
hashes and whitespace checks passed. The original artwork and excluded _TEST.hl7
retain their recorded SHA-256 values. Evidence is retained in ignored
artifacts/stage9-checkpoint. Only native-acceptance documentation changed since the
accepted implementation manifest; the checkpoint contains exactly its 41 paths.

The implementation and correction records below preserve the earlier verification
history. Their pending-acceptance and no-commit statements are superseded by the
native acceptance and checkpoint authorization above.

## Native acceptance corrections — historical implementation record

Native acceptance identified three gaps: masked patient demographics were unsuitable
as realistic synthetic identities, reports were discoverable only through the Script
Editor's last-run CSV export, and the taskbar monogram was undersized. These are
corrected within the same uncommitted milestone. The initial 1,483/712 gate below is
historical and is superseded by the correction gate recorded here after verification.

Patient Deidentifier now uses **policy version 2 / dictionary version 1**. Identifier
Pseudonymizer and Timestamp Shifter keep version-1 transformation semantics. No saved
script source is overwritten. Existing user copies that call the Patient helper use
its current policy; older result bytes are not retroactively modified.

### Synthetic identity library

`SyntheticIdentities.v1.json` is an embedded, original small vocabulary authored for
this project, not an imported real-person/address dataset. `SyntheticIdentityLibrary`
is independently tested and reused inside the isolated worker. It receives seed and
opaque identity context only; source names, age, sex and geography never select its
demographics. DOB remains ambiguity evidence in identity resolution, never a birth
generation input. Values can coincide by chance; generation does not compare with
original demographics or reroll based on them.

Profiles have plausible given/middle/family names, aliases, fictional Test streets and
localities, units, non-deliverable `ZZ`/`00000` region/postal values, numeric local IDs,
new DOB and administrative sex/marital status. A fixed pool covers DOBs from 1945
through 2013; it uses no clock. Supported multiple-birth/order and alive indicators
become N/1/N, and a present original death date is cleared. Present PID, NK1, GT1 and
IN1 birth/sex demographics are generated independently, including insured DOB rather
than merely shifting it. Missing/empty/explicit-null positions are still preserved;
missing components are not invented to fill out a profile.

Phone subscribers use the [NANPA fictional 555-0100…0199 range](https://www.nanpa.com/numbering/555-line-numbers)
under a fixed test area code; emails use [IANA example.org](https://www.iana.org/help/example-domains).
Addresses are assembled fiction, not selected residential locations. This deliberately
does not produce deliverable postal records. Names and the finite fictional phone
pool can repeat; complete profiles/local IDs remain distinct. These are synthetic
test values, without a real-world identity, telecom or geographic validation claim.

The seed and opaque key choose one of 1,000,003 allocation slots. A collision probes
successive free slots deterministically, without replacing an allocated identity.
Allocation is bounded to 100,000 cached profiles (or the lower configured mapping
budget); exhaustion fails atomically. Street number + street + unit allocation and
email/local ID distinguish profiles even when names/phone numbers repeat. Allocation
order is part of reproducibility: same input order/seed/policy/dictionary gives the
same bytes; arbitrary reordered/subset batches are not an equivalence promise.
No source-to-profile map is persisted outside the execution.

Qualified patient aliases share a complete profile. Account, visit, order, provider,
guarantor and insured IDs use stable scoped contexts. Supported XCN providers receive
consistent generated names and local PRN identifiers. Qualified GT1/IN1 identities
link their demographics to their IDs. Unqualified contacts/insured/guarantors use a
patient/role/occurrence context; this does not infer crosswalks or relationships from
names. Unsupported relationship metadata is suppressed rather than guessed.

Explicit numeric age observations `AGE` with local/absent coding system and LOINC
[21612-7](https://loinc.org/21612-7)/[30525-0](https://loinc.org/30525-0) with `LN` are
recomputed in completed years from synthetic DOB, with `a`/year/UCUM units. The
reference is shifted OBX-14 when present, otherwise shifted MSH-7; absent/unsupported
day precision uses the fixed 2030-01-01 reference. An encounter preceding synthetic
birth produces an empty age and review count. No arbitrary local numeric code is
guessed to mean age. Unrecognized age/demographic observations are a residual risk.

### Document-associated Transformation Report

Every successfully transformed output with a valid fixed-schema review row opens
with an expanded **Transformation Report — Review required** panel in that document's
navigation area. It shows preset, scope, policy, captured revision, stale/closed-source
state, processed messages, transformed/suppressed value counts for names, addresses,
contacts, demographics, identifiers, providers, narratives, attachments and other
content, plus unsupported/retained-content warnings. Counts are leaf-value decisions,
not a certification or a count of unique people.

This is a bounded projection of the **existing sealed ScriptHost report journal**,
not another reporting engine. Parent validation accepts only the fixed schema,
closed preset/version/status/notice, nonnegative counts, matching processed count
and consistent category totals, with at most 8 KiB read. Arbitrary user-script CSV
cells are never rendered in the privacy panel. Existing explicit CSV export behavior
is unchanged; Script Editor → Reports still exports the last successful run.

The small immutable summary belongs to its output document, so later runs/export
disposal, tab changes and view reconstruction do not replace it. Weak source tracking
updates freshness after later edits and labels source closure; producing-run stale
state remains separate. Output close removes observers. No source values, source
filenames, edited script names or mapping keys are displayed. The panel describes
the producing run; later output edits are not reassessed. Summaries are session-only
and are not embedded in Save As files or restored when those files reopen.

### Correction verification

Final correction gate on 2026-10-20, Windows / SDK 10.0.401 / runtime 10.0.12:

| Check | Result |
| --- | --- |
| Locked restore | Passed; package graph unchanged |
| Release `--no-restore -warnaserror` | Passed; **0 warnings/errors**, 1.47 s |
| Focused Scripting | **62 passed**, 0.335 s |
| Focused Workspace/library | **43 passed**, 22.522 s |
| Focused Desktop | **21 passed**, 6.632 s |
| Permanent icon-processing tests | **4 passed**, 0.718 s |
| Full solution | **1,504 passed**, 159.512 s |
| Complete Workspace stress | **718 passed**, 278.497 s |
| ScriptHost Current/Whole smoke | Both passed against existing Release build layout |
| Integrity | Eight no-filter fixture blobs and all 13 protected SHA-256 hashes match |
| Whitespace | `git diff --check` and all new text files passed |

All runs have zero failures/skips/errors. Compared with the initial 1,483/712 gate,
15 Scripting and six Workspace cases are added; earlier Desktop cases are expanded
to cover the document panel, view reconstruction, source closure and the embedded
ICO. All prior permanent tests remain. No limit or test expectation was weakened:
seven old Patient assertions now validate nine-digit synthetic IDs instead of the
superseded SYN-prefix format. Both narrow policies have dedicated regression checks.

Observed focused 1/100/1,000-message runs took 1.570/19.208/140.308 ms and allocated
198,704/19,133,376/191,330,496 bytes cumulatively, including test input/final parsing.
This is not retained memory or an end-to-end throughput promise. Existing worker,
message, output and history limits are unchanged. Identity cache/allocation exhaustion,
cancellation, timeout and report failure tests still publish no partial artifacts.

Both smoke lifecycles used `VerifyScriptPublish.ps1 -PublishDirectory` pointing at
the existing Release layout. Its historical output says "Published"; no publish,
package or deployment command ran. Shared state, CSV/Complete, deferred output,
worker hashes/dependency isolation and clean exits all passed.

HEAD remains `5eebc767eae5f241ac4c7a02c4275ae0305b76fa`, index empty. The exact 41-path
milestone inventory below is unstaged/uncommitted, plus the unchanged excluded HL7.
The original icon hash remains A47EFCAD…3651095 and the excluded HL7 hash remains
D254E8BB…5EFFA1 (full original hashes are retained below/in the integrity manifest).
Original artwork/master and larger frames are preserved; optimized ICO regeneration
is byte-identical. No stale owned test directories or disposable exports remain.
Evidence: ignored `artifacts/stage9-acceptance-fixes`, including final logs/XML,
implementation/file manifests, status, integrity and the revised visual preview.
Only documentation changed after the tested implementation was frozen. **Stop for
native Windows retesting; no staging/commit or external publication is authorized.**

### Native Windows correction retest

1. Review/duplicate Patient Deidentifier; run Current and Whole over repeated
   synthetic patient IDs. Verify realistic names/aliases, addresses, contacts, new
   DOB/sex and numeric MRNs; same qualified patient stays consistent, different
   authorities stay distinct. Change original demographics only in independent
   captures: generated values must remain independent. Change seed and repeat.
2. Check PID/NK1/GT1/IN1 demographics, repetitions and nulls, custom delimiters,
   provider references and supported AGE/LOINC observations. Compare age with
   synthetic DOB at the shifted observation/header date. Unsupported values still
   require review; no attachment should decode or display.
3. Inspect the expanded Transformation Report directly in each output. Run another
   preset and switch tabs: each report must retain its own preset/scope/counts.
   Edit/close the source, detach/reconstruct views, and close outputs; check stale/
   closed state, source isolation, normal prompts and no cross-document attribution.
4. Confirm Identifier/Timestamp presets keep their narrow semantics. Test cancellation,
   a low mapping limit, timeout, stale-source execution, Stage 8C insertion/8D generation
   and Save As/reopen. No partial output/report may publish after failure.
5. Follow [the revised icon checklist](branding.md#verification-and-native-acceptance)
   for title bar, unpinned taskbar, Alt+Tab, secondary/floating windows, themes and DPI.

Stop for native acceptance. No staging/commit, external Git action, package, deployment,
publication or Stage 10 is authorized.

Stage 8D checkpoint: `5eebc767eae5f241ac4c7a02c4275ae0305b76fa`
(`feat: add Stage 8D synthetic HL7 message generator`). Its exact 35-file gate passed:
locked restore, Release zero warnings/errors, 1,416 solution cases, 698 Workspace
stress cases, both ScriptHost lifecycles, fixtures/hashes and whitespace. Post-commit
tracked tree/index were clean; icon source and excluded local _TEST.hl7 were preserved
untracked. Full checkpoint manifests live under ignored artifacts/stage8d-checkpoint-20261020.

## Review and execution

Open View → Script Editor. The existing Script Library now contains Patient
Deidentifier, Identifier Pseudonymizer and Timestamp Shifter, alongside both original
examples and saved user scripts. Built-ins are immutable, reviewable and duplicable.
Duplicate to edit `const uint seed = 42` and optional `ShiftDays = 30`; Save Script
persists configuration/source only. Existing copies are never overwritten by built-ins.

Choose Current message or Whole document, review the source/coverage below, then Run.
The existing isolated ScriptHost compiles once, initializes one workflow, applies it
per Main invocation and writes one bounded report row in Complete. Successful output
opens independently and unsaved, with a static suggested filename containing Review
Required. Existing Save As and closing prompts apply. The source/drafts/history/save
destination are unchanged. Cancellation, timeout, encoding/limit failure or other
worker failure publishes no partial document or reports. Captured output retains the
existing explicit stale-source attribution when newer source edits occur during Run.

Reports → Export CSV produces a count-only `deidentification-review` report. Its first
cell is **Review required**. It includes version, message/missing/ambiguous identity
counts, replaced/suppressed values, shifted/unsupported timestamps, unknown segments,
high-risk/retained field counts and a fixed unassessed-scope notice. It includes no
source IDs, names, dates, filenames, source excerpts, hashes or mapping keys.

## Identity and reproducibility contract

Both policies use domain-separated, length-framed HMAC-SHA256 to resolve identities
with a key derived from the unsigned 32-bit seed. Narrow version-1 tokens remain
SYN-prefixed digest values; Patient version 2 uses the generated local numeric IDs.
Within-run digest token collisions fail rather than merge. The same input bytes, normalized
configuration, execution order and version produce the same bytes. Different seeds
change mappings and the default day shift. Future policy versions may change output.
This is deterministic pseudonymization, **not cryptographic anonymity**; the seed is
not treated as a secret. Repeated tokens reveal linkage and are vulnerable to guessing.

Patient identity requires a nonempty decoded PID-3 identifier and assigning authority
(including its subcomponents), with identifier type included in the key when present.
Repeated aliases declared in one PID can link
that patient across messages. Exact case/whitespace distinctions remain. Names are
never primary keys. Missing authorities/identities, truncated identifiers, multiple
PIDs, contradictory birth evidence or bridges between already distinct groups are
not silently merged. These get explicit counts and deterministic per-message fallback
contexts. The same unqualified bare number is deliberately not correlated across
messages. Input order/subset can affect unresolved/alias contexts; a Current run is
not promised to produce all the same demographics as a different Whole input set.

Supported qualified visit/account/order/provider identities preserve repeated
relationships. EI order scope includes namespace/universal ID/type; CX scope includes
assigning authority/type. Unsupported/unqualified relations are flagged/suppressed or
isolated, with no guess at equivalence. Parent-order composites and institutional
crosswalks are not resolved. Metadata may be replaced with synthetic placeholders;
output needs schema/clinical review before use.

## Field-coverage matrix (Patient version 2, narrow presets version 1)

This compiled policy uses common v2.5.1 field locations, not a schema/profile engine.
Other HL7 versions, local conventions and nested composite metadata require review.
Patient Deidentifier suppresses every nonempty leaf outside its explicit supported
identity/date/clinical rules, except protected MSH delimiters/type/processing/version.
Narrow Identifier and Timestamp presets retain other content and count it for review.
Original missing/empty/explicit-null positions remain; suppression never removes a
field, repetition, component, subcomponent or segment. Unsupported present dates may
be deliberately cleared to empty in Patient mode and are counted as suppressed.

| Segment | Identifier/provider handling | Supported top-level dates | Other Patient-mode treatment |
| --- | --- | --- | --- |
| MSH | 3–6 applications/facilities pseudonymized; 10 message ID | 7 | Preserve 1/2 and 9/11/12; suppress other values |
| EVN | Operator/other unsupported data suppressed | 2/3/6 | Event code retained; other leaves suppressed |
| PID | 2/3/4 qualified IDs; 18 account; 19/20 personal IDs; 5/6/9 realistic names/aliases | New DOB 7; clear death 29; shift 33 | New address 11, phones/emails 13/14, sex 8, marital 16, multiple-birth/order 24/25 and alive 30; unsupported content suppressed |
| PD1 | 4 provider; remaining identifying data suppressed | 13/17/18 | Other leaves suppressed |
| NK1 | Generated contact name 2/address 4/phones 5/6 | New DOB 16; shift 8/9 | New sex 15; unsupported relationships/other leaves suppressed |
| PV1 | 5/19/50 visit; 7/8/9/17/52 providers; 3/6/11/42/43 locations | 44/45 | Patient class/admission code retained for review; other leaves suppressed |
| PV2 | Unsupported identifiers/text suppressed | 8/9 | Other leaves suppressed |
| IN1 | 2/3/36 insurance IDs; 49 qualified insured ID | New insured DOB 18; shift 12/13/30 | New insured name 16/address 19/sex 43; unsupported contacts/employer/other data suppressed |
| IN2 | 1/2/6/8/10 insurance/personal IDs; 25/26/61 qualified IDs | 17 | Other names/addresses/contact/employer/military content suppressed |
| GT1 | 2 qualified guarantor number; generated name 3/address 5/phones 6/7 | New DOB 8; shift 13/14/24/31/32 | New sex 9; unsupported identifying/free content suppressed |
| ORC | 2/3/4 orders; 10/11/12/19 providers | 9/15/27 | Order-control/status retained for review; parent and unsupported fields suppressed |
| OBR | 2/3 same EI order mapping; 18–21 accession-like fields; 10/16/28/32–35 providers | 6/7/8/14/22/36 | Service detail/free text and unsupported values suppressed; service section/result status retained for review |
| OBX | 15/18 organization/equipment; 16 provider | 12/14/19; 5 for DT/DTM/TS | Recognized ages regenerated from synthetic DOB; other numeric NM retained after invariant numeric validation; small UCUM units allowlist; TX/FT/ST/ED/RP/CWE/other values suppressed; type/status/abnormal code retained for review |
| NTE | — | — | All present content except numeric Set ID suppressed, including narratives |
| DG1 | — | 5 | Descriptions/coded diagnoses/other content suppressed |
| AL1 | — | 6 | Allergen/reaction/other content suppressed |
| Unknown/Z | No inferred identity schema | No inferred dates | Present leaves suppressed and unknown/high-risk counts incremented |

Set IDs consisting only of ASCII digits are retained. Preserved codes, numeric results
and clinical patterns are **not certified free of identifiers**. Unsupported typed
metadata may become placeholder values and should not be treated as conformant.
ED data is not decoded/rendered. Large/uneditable messages remain subject to existing
complete-message limits and fail safely rather than using truncated previews.

## Timestamp rules

For clinical timestamps, one fixed calendar-day displacement applies throughout the run. Synthetic
PID/NK1/GT1/IN1 birth dates in Patient mode replace original DOB independently; they
are not part of the clinical interval-preservation promise. Default displacement derives
from seed (seed 42 gives -323 days), with a nonzero fallback; explicit ShiftDays allows
-3,660…+3,660. Supported precision is YYYYMMDD, hour, minute or second, with 1–4
fractional second digits and optional explicit ±HHMM offset (conservatively bounded
to -1200…+1400). Precision, fraction,
offset spelling and absent offset are preserved. Relative intervals among supported
values shift by the same number of days. No local clock/timezone/DST inference is used.

Year/month-only, invalid dates, unsupported composites, overflow, leap seconds and
unsupported offsets cannot be safely shifted without guessing. Patient mode clears
the present unsupported values; Timestamp mode retains them and explicitly increments
Unsupported timestamps. Already missing/empty/null dates stay so. Date-only and unzoned
times still require interpretation; nested dates in XPN/XAD/CX/other composites are
not comprehensively shifted. Narrow presets leave other sensitive fields intact.

## Limits, source preservation and residual risks

No mappings persist between executions. Each run is bounded to 100,000 entries per
identity map (a lower cap is configurable), 1,024 characters per identity leaf,
16,384 mutations/message and 2,000,000 mutations/run. Existing 1 Mi UTF-16/16 Ki line/
8 MiB message, report, output/storage, message-count and worker timeout/memory limits
still apply. Transforms use one controlled leaf-replacement batch and one Core parse,
retaining untouched encoded spans/BOM and all delimiter/terminator structure.

This is a de-identification aid. It is not HIPAA certification, a guarantee of PHI
removal, clinical/profile conformance, or authorization for unrestricted sharing.
Clinical patterns, dates, deterministic linkage, unknown code meanings, assigning
conventions, unsupported versions/composites and misassigned source identifiers can
remain identifying. Qualified-ID consistency cannot prove two records are the same
real person. Re-identification risks require human review and appropriate controls.

Container and opaque bytes outside indexed message payloads follow existing lossless
script execution behavior and can remain unchanged, including source text in wrappers.
The fixed report explicitly flags them as unassessed. Review or remove them through
an appropriate controlled workflow before sharing. No source text is logged by these
presets; worker diagnostics use fixed codes and structural locations only.

## Initial automated gate (before native acceptance corrections)

Final gate on 2026-10-20, .NET SDK 10.0.401 / runtime 10.0.12, Windows:

| Check | Result |
| --- | --- |
| `dotnet restore HL7Workbench.sln --locked-mode` | Passed; locked graph unchanged |
| Release build, `--no-restore -warnaserror` | Passed; zero warnings/errors, 2.77 s |
| Focused Scripting | 47/47 passed, 0.431 s |
| Focused Workspace/Script Library | 37/37 passed, 15.916 s |
| Focused Desktop (presets, branding, script polish/highlighting) | 21/21 passed, 5.690 s |
| Full Release solution, `--no-build --no-restore` | **1,483/1,483 passed**, 151.955 s |
| Complete Workspace with `HL7WORKBENCH_STRESS=1` | **712/712 passed**, 262.745 s |
| ScriptHost Current/Whole smoke lifecycles | Both passed against the existing Release layout |
| Fixture integrity | All eight tracked SHA-256 hashes and no-filter Git blob hashes match HEAD |
| Protected files | All 13 initial hashes match, including excluded _TEST.hl7, original icon, shortcuts/crash/paste regressions |
| Icon assets | Seven exact PNG frames embedded in executable; shared window style passes; regeneration byte-identical |
| Whitespace | `git diff --check` passed; every new text file also checked |

All test runs above had zero failures/skips/errors. There are 67 new full-suite cases
over the 1,416 baseline: 47 Scripting, 14 Workspace, three Desktop preset cases and
three branding cases. Workspace stress increased from 698 to 712. Earlier focused
42/37/33 and icon 3-case passes remain in the logs; the final targeted Desktop run
uses a different class selection, while the full suite retains all prior coverage.

The final audit tightened the conservative -1200 offset boundary, which had allowed
negative offsets with hour 12 and nonzero minutes. New regressions cover that case,
leap seconds, year overflow, full EI universal authorities and unit display text.
The real-worker regression also confirms that transformation-budget failures retain
the fixed `TransformationLimit` diagnostic without source-bearing details.

The earlier real-worker defect was an authority mismatch: a controlled batch replaced
ScriptMessage's validated Core model, but Program serialized its old local Core model.
Program now serializes the facade, matching deferred output and ordinary edits. Six
Current/Whole real-worker preset cases prove transformed bytes are actually published;
failure/cancellation/timeout cases prove atomic cleanup and source isolation.

Observed focused allocations for 1/100/1,000 small synthetic messages were
197,896 / 19,765,128 / 197,664,120 bytes, with 0.474 / 19.903 / 163.916 ms elapsed.
These include test input/final parsing and cumulative allocations, not retained
memory or an end-to-end worker throughput guarantee. No production limit was relaxed.

Both smoke lifecycles use:
`./build/VerifyScriptPublish.ps1 -PublishDirectory src/HL7Workbench.Desktop/bin/Release/net10.0`.
The historical script/output calls the directory "Published", but this gate did not
invoke publish, package or deployment. Dependency isolation, worker hashes, shared
state, CSV/Complete, deferred output and clean exit all passed.

Useful logs/XML results and manifests are retained under ignored `artifacts/stage9`.
The final code/test/asset bytes match the full-suite manifest; only documentation was
completed afterward. Original icon source SHA-256 remains
`A47EFCAD4AC2C7E19CB296A280DFA02B2AABFB11DFE07DCFF9EB4951B3651095`.
The excluded local _TEST.hl7 SHA-256 remains
`D254E8BBBFC98C2E377EC9149F2D46415756E0CD96CE8DED0D9FB8548F5EFFA1`.
No stale owned Desktop test directories or disposable HL7/CSV exports remained.

HEAD remains `5eebc767eae5f241ac4c7a02c4275ae0305b76fa`; the index is empty.
At that initial gate, 29 milestone paths were unstaged/uncommitted, plus the unchanged
excluded HL7. The current corrected inventory below supersedes that count.
No Stage 9/icon commit, push, tag, package, deploy, release, publication or Stage 10.
**Stop for native Windows acceptance.** Icon window-manager checks remain pending;
see [the icon checklist](branding.md#verification-and-native-acceptance).

## Exact milestone inventory (after acceptance corrections)

There are 41 milestone paths: 17 modified tracked files and 24 untracked files,
including the unchanged user-supplied icon source. The index remains empty.
The additional untracked samples/synthetic-only/stage5b-repeated-patients_TEST.hl7
is excluded and unchanged; only its hash was inspected. No permanent test was removed.

Modified tracked files:

```text
README.md
docs/PROJECT_CONTEXT.md
docs/architecture.md
docs/script-api.md
src/HL7Workbench.Desktop/App.axaml
src/HL7Workbench.Desktop/HL7Workbench.Desktop.csproj
src/HL7Workbench.Desktop/Viewer/FileDocumentView.cs
src/HL7Workbench.ScriptHost/Program.cs
src/HL7Workbench.Scripting/HL7Workbench.Scripting.csproj
src/HL7Workbench.Scripting/ScriptContracts.cs
src/HL7Workbench.Scripting/ScriptMessage.cs
src/HL7Workbench.Workspace/MessageDocument.cs
src/HL7Workbench.Workspace/ScriptExecution.cs
src/HL7Workbench.Workspace/ScriptLibrary.cs
src/HL7Workbench.Workspace/ScriptReports.cs
tests/HL7Workbench.Desktop.Tests/ViewerTests.cs
tests/HL7Workbench.Workspace.Tests/ScriptLibraryTests.cs
```

Untracked milestone files:

```text
assets/branding/workbench-icon-source.png
build/CreateBrandingAssets.py
build/TestBrandingAssets.py
docs/branding.md
docs/decisions/0013-script-deidentification-aids.md
docs/stage9-deidentification-acceptance.md
src/HL7Workbench.Desktop/Assets/workbench-icon.png
src/HL7Workbench.Desktop/Assets/workbench.ico
src/HL7Workbench.Desktop/Branding.axaml
src/HL7Workbench.Desktop/Viewer/TransformationReportView.cs
src/HL7Workbench.Scripting/DeidentificationOptions.cs
src/HL7Workbench.Scripting/DeidentificationWorkflow.Demographics.cs
src/HL7Workbench.Scripting/DeidentificationWorkflow.Fields.cs
src/HL7Workbench.Scripting/DeidentificationWorkflow.cs
src/HL7Workbench.Scripting/Hl7TimestampShift.cs
src/HL7Workbench.Scripting/SyntheticIdentities.v1.json
src/HL7Workbench.Scripting/SyntheticIdentityLibrary.cs
src/HL7Workbench.Workspace/ScriptLibrary.Deidentification.cs
src/HL7Workbench.Workspace/TransformationReport.cs
tests/HL7Workbench.Desktop.Tests/BrandingTests.cs
tests/HL7Workbench.Desktop.Tests/DeidentificationViewTests.cs
tests/HL7Workbench.Scripting.Tests/DeidentificationTests.cs
tests/HL7Workbench.Scripting.Tests/SyntheticIdentityTests.cs
tests/HL7Workbench.Workspace.Tests/DeidentificationExecutionTests.cs
```

Only workflow, report association, branding, permanent tests and related documentation
are changed. Core/save/message identity/keyboard/clipboard behavior and package locks
are preserved. Build/test logs, manifests, XML results and the visual preview are
intentionally retained under ignored artifacts/stage9-acceptance-fixes; earlier
Stage 9/8D verification records and reusable stress fixtures are retained. The single
owned Python import bytecode file was removed; no build-output directories were deleted.

## Native Windows acceptance (no debugger)

Run `dotnet run --project src/HL7Workbench.Desktop -c Release --no-build` from the
repository. Use only synthetic inputs; keep exports in an owned ignored folder.

1. Open a Stage 8D-generated ADT batch or samples/synthetic-only/stage5b-repeated-patients.hl7.
   Open Script Editor; verify all three new built-ins and both original examples.
2. Select Patient Deidentifier, read the source and this matrix, then duplicate.
   Change seed, Save Script, close/reopen the editor/application and verify the copy
   persists while the built-in is unchanged. Do not execute unrevised old examples
   as a claim of complete de-identification.
3. Run Current, then Whole on the same controlled source. Expect new unsaved Review
   Required tabs and one count-only review report. Source bytes/drafts/save destination
   must remain untouched; no source identifier appears in output names or reports.
4. Use repeated synthetic PID-3 values with explicit authorities. Verify same-patient
   aliases/names, visit/account/order links and distinct authority-scoped identities.
   Test missing authority, duplicate PIDs, conflicting birth data and alias bridges:
   review counts must identify ambiguity without silently merging patients.
5. Repeat identical input/configuration and Save As separate files; SHA-256 hashes
   must match. Change seed and repeat: changed mappings must also be reproducible.
6. Run Identifier Pseudonymizer and Timestamp Shifter. Confirm their intentionally
   narrow coverage, Review required reports and retained-sensitive-content warning.
   Test partial dates, fractional seconds, offsets, leap days, invalid dates and
   year/month-only input. Supported intervals stay consistent; unsupported values
   are cleared or retained with review counts according to the preset.
7. Inspect custom separators, repeating CX/XPN/contact data, missing/empty/explicit
   nulls and mixed CR/LF endings. Test NTE/TX/FT/ST, unknown Z and small ED payloads:
   Patient mode suppresses content without decoding attachments or flattening structure.
8. Test wrappers/opaque source regions and nested date metadata. The report must
   explicitly mark this scope unassessed; inspect it separately before sharing.
9. Cancel a run, exercise a low mapping/output limit in a duplicated preset and a
   deliberately long-running test script. Expect no partial output/reports and no
   source changes. Edit the source during execution: completed captured output must
   retain explicit stale-source attribution and newer source edits.
10. Insert using Stage 8C, generate siblings using Stage 8D, then run both scopes.
    Verify captured identity/order, search, comparison and source provenance. Save As,
    close/reopen output and verify normal unsaved prompts/independent tabs.
11. Export the review CSV. Expect only fixed metadata/counts and Review required;
    no patient values, mapping keys, dates or source filenames. Keep output under
    review with the limitations above; do not treat it as certified anonymous.
