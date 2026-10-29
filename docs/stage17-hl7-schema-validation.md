# Stage 17: explicit, inline HL7 schema validation

## Architecture and activation

Stage 17 extends the accepted Stage 16 checkpoint `9686f5259f9c1a8b99cc4631dfae40731b410284`.
Core owns immutable declarative profiles, resolution/import, validation and source
coordinates. Workspace owns the local profile library and per-message application,
revision guards, cancellation and disposal. Desktop alone owns AvaloniaEdit
decoration, hover/navigation, the selection/library dialog and preferences.
No dependency, second HL7 parser, cloud service, telemetry or transmission was added.

Opening a file does **not** validate it. In **HL7 Tools → Schema Validation**,
choose **Apply/Change Schema**, review a built-in/imported profile and choose
**Apply Schema**. **Remove Schema** clears only the active message's selection,
diagnostics and future refresh. **Import Schema** opens the existing JSON/XML
import workflow; **Manage Schemas** opens/reuses the same local profile library
for review, reimport and removal. Import and management are available with no open
document and never apply a profile. There is no second management or results tool.
Apply requires a prepared active message; Remove requires an applied schema;
Previous/Next Issue require retained findings. Commands track the active document
and message across docked and floating hosts. A selector result is rejected if its
target document/message or content revision changed while it was open.

The raw editor has separate Editing, Message navigation and read-only Validation
status rows. Native wrapping keeps controls within the available width; profile
names use ellipsis with the full name in the tooltip/accessibility description.
At narrow widths, status stacks its profile above counts/state; at wider widths it
shares one line. Error/warning counts and coverage/state remain visible while
navigating issues. Long issue explanations stay in hover/accessibility text.
Schema and issue-navigation buttons are absent from the raw editor toolbar.
Use **Previous Issue / Next Issue** in the ribbon or **Shift+F8 / F8** in the editor;
inline squiggles and hover explanations are unchanged. Ribbon icons, Fluent themes,
pin/popup behavior and keyboard navigation use the existing shell conventions.

Application is session-only, keyed by Workspace `MessageId`, never a patient ID,
message number, source offset or MSH-10. Navigating to another message does not
transfer the selection. Returning to a previously applied message revalidates it.
Selections/diagnostics never enter source, Save/Save As, layouts or sidecars.
The session limit is 128 explicitly applied message selections per document.

## Built-in coverage and provenance

Only **HL7 v2.5.1** profiles are supported in this release. Future versions can
provide another immutable catalog without changing the evaluator or document model.

| Message/trigger | Message structure |
| --- | --- |
| ADT A01, A04, A08 | ADT_A01 |
| ADT A02 | ADT_A02 |
| ADT A03 | ADT_A03 |
| ORM O01 clinical OBR orders | ORM_O01 |
| ORU R01 | ORU_R01 |

The seven built-ins contain the five complete referenced message/group hierarchies,
39 segment definitions with all their field declarations, and 50 referenced
composite layouts. Ordered groups, optional/repeated segments and group cardinality
are represented as data. This is **not** a catalog of all HL7 messages.

The isolated `ValidationCatalog/v251-model.json` resource adapts generated model
metadata from [nHapi revision 9027797](https://github.com/nHapiNET/nHapi/tree/9027797b1fd1faebcffd743ac11324499145f29d/src/NHapi.Model.V251).
It records exact upstream source paths/SHA-256 digests and remains **MPL-2.0** under
its [notice](../src/HL7Workbench.Core/ValidationCatalog/NOTICE.md) and
[license](../build/third-party/nHapi-MPL-2.0-LICENSE.txt). The nHapi runtime/parser
is not used. `build/update-validation-catalog.py` explicitly regenerates the pinned
resource; runtime never fetches definitions. Standard prose/specification files are
not bundled. HL7's [licensing information](https://dev3.hl7.org/implement/standards/nocost.cfm)
distinguishes no-cost access from unrestricted redistribution.

Cross-check references include HL7 Europe's published v2.5.1
[control/profile definitions, sections 2.5 and 2.19](https://hl7.eu/HL7v2x/v251/std251/ch02.html),
[patient administration](https://hl7.eu/HL7v2x/v251/std251/ch03.html),
[order entry, especially ORM section 4.4.1](https://hl7.eu/HL7v2x/v251/std251/ch04.html),
and [observation reporting](https://hl7.eu/HL7v2x/v251/std251/ch07.html).
ORM's order-detail alternatives are modeled as a choice, rather than six required
consecutive segments. Symbolic variable maximum length 99999 is not converted into
a fabricated numeric ceiling. Field/component lengths count the retained encoded
characters of one repetition, including component/subcomponent separators.

**Coverage is deliberately explicit.** The base model does not encode every
conditional usage/component requirement, external/user-defined terminology or
advanced datatype semantic. Built-in results therefore carry coverage notices and
display **Incomplete**, even when no errors were found. An imported child retains
its base's notices; importing a profile does not erase unsupported coverage.
Do not interpret this feature as clinical validation or HL7/IG certification.

The Stage 16 generators never claimed schema conformance. Their byte goldens remain
unchanged. In particular, placeholder MSH-10 and some standalone EI order fields can
exceed these v2.5.1 declarations; Stage 17 reports those findings rather than changing
the generated bytes. Specialized supply/pharmacy/diet ORM branch semantics, coding
systems and clinical inference are not certified by the clinical-order example.
The built-in ORM profile explicitly defers messages containing RQD/RQ1/RXO/ODS/ODT:
after parsing and declaration checks, it returns Incomplete without evaluating
structure/content rules. This avoids inventing constraints for unsupported variants;
use an independently specified custom profile for those branches.

## Enforced rules and diagnostic model

The common evaluator checks message compatibility (MSH-9.1/2/3 and MSH-12.1), ordered
segment/group structure, required/prohibited usage, cardinality, required fields,
field repetitions, explicit component/subcomponent requirements, encoded maximum
lengths, supplied value-set membership, supported datatype shape and declarative
conditions. Contextual OBX-5 datatype selection comes from a profile rule referencing
OBX-2, including the corresponding OBX occurrence.

Primitive syntax checks cover NM, SI, DT, DTM and TM, including numeric grammar,
partial date/time precision, calendar validity, fractions and numeric offsets.
ST/TX/FT/ID/IS/TN receive structural, escape, length and explicit value-set checks;
specialized text/telephone formatting is not inferred. Composite layouts constrain
known component/subcomponent positions. GTS, unresolved variable types, deeper
composite semantics, unbound tables and oversized values produce explicit
Incomplete findings. No regex, executable expressions, scripts or external lookup
is supported. A supplied `table` is a binding label; membership requires an explicit
`allowedValues` list. Numeric table labels normalize to four digits.

Each finding has a stable rule ID, Error/Warning/Information severity, bounded
description, optional HL7 path, segment occurrence, exact message-local UTF-16
interval or zero-width insertion anchor, profile ID and rule provenance. Missing
segment/group findings anchor at the next segment or message end. Parsing findings
use `Parse.*`; schema findings use `Structure.*`, `Value.*`, `Type.*`, `Condition.*`
and `Profile.Incompatible`. Coverage and budget notices are separate. Results retain
`Parsed`, `Complete` and engine version 1; malformed/ambiguous source cannot receive
qualified success. Findings are sorted by source position/length/rule/path.

The original materialized parser model and shared structural scanner are the only
ER7 grammar authorities. Source mapping handles MSH numbering, repeated segments,
fields/repetitions/components/subcomponents, empty/null/missing values, custom
delimiters, escapes and CR/LF/CRLF. Original bytes, delimiters and terminators are
never rewritten. Snapshot-global byte positions and display offsets remain distinct
from message-local UTF-16 diagnostic coordinates.

## Native JSON format version 1

Input is UTF-8 JSON (an initial UTF-8 BOM is accepted). Unknown/duplicate properties,
invalid types/enums, null rule collections, unsupported datatypes, excessive nesting
and invalid/conflicting rules are rejected. Property names are camelCase.
The [versioned JSON authoring schema](examples/stage17-profile.schema.json) describes
the format for external editors. Core's strict importer remains authoritative for
cross-field cardinality, path validity, datatype positions, aggregate budgets and
inheritance. The application does not fetch JSON schemas or external references.

| Profile property | Meaning |
| --- | --- |
| `formatVersion` | 1; unsupported versions are rejected |
| `id`, `name`, `profileVersion`, `description` | Stable ID, display/version metadata and optional explanation |
| `hl7Version`, `messageType`, `triggerEvent`, `messageStructure` | Compatibility declaration; current HL7 version is 2.5.1 |
| `baseProfile` | Optional explicit parent ID; no implicit parent discovery |
| `tradingPartner`, `organization`, `source` | Local display metadata; never a fetched URL/reference |
| `unknownZSegments` | `Reject`, `Warn` (default), or `Allow`; undefined content under Warn/Allow remains Incomplete |
| `structure` | Ordered segment/group slots |
| `segments` | Segment definitions and field rules |
| `conditions` | Bounded present/equal → require/prohibit rules |
| `coverageWarnings` | Explicit unsupported/unevaluated coverage notices |
| `unsupportedSegments` | Explicit deferred branch identifiers; presence stops schema evaluation with Incomplete, and inheritance preserves deferrals |

A structure slot has `key`, either `segment` or nonempty `children`, `min` (default
0), `max` (default 1; null means profile-unbounded), optional `choice` for a group,
and optional `provenance`. A choice selects one matching alternative; its siblings
do not become required. New inherited slots require `after` naming an existing
sibling key; placement never comes from unordered segment-name guessing.

A segment has `id`, `fields`, and `allowExtraFields` (default false). A value rule
has one-based `position`, `min`/`max`, `dataType` (default ST), optional `maxLength`,
`table`, `allowedValues`, `children` and `provenance`. Children constrain components
then subcomponents of the declared composite datatype; deeper levels and undeclared
component positions are rejected. Explicit component/subcomponent repetition
cardinalities are rejected because ER7
does not repeat those levels. Max 0 prohibits populated content, including
explicit null; required content cannot be missing, empty or explicit null.
`dataTypePath` optionally names the field/component supplying a supported contextual
datatype for the corresponding segment occurrence. A raw field position is never
interpreted as MSH textual delimiter position.

A condition has `id`, `ifPath`, optional `equalsValue`, `thenPath`, optional
`prohibit` (default false), optional `severity` (Error default) and provenance.
Without equalsValue it tests present/non-null content; otherwise it compares a
decoded literal ordinally. Paths use existing Core syntax, e.g. `PID[2]-3[1].4.1`.
Conditions target the specified occurrence, not an implicit cross-message or
all-occurrences query. They do not run arbitrary boolean expressions or regex.

The fully synthetic [regional HIE example](examples/stage17-synthetic-hie.json)
inherits ADT A01, requires PID-3's local ID and assigning-authority namespace
`SYN-HIE`, constrains PV1-2 to I/O, requires ZPI immediately after PID with a local
partner code, and requires ZPI-2 for an inpatient visit. These exact constraints
are enforced. Its inherited base coverage is still Incomplete.

## Inheritance and Z segments

Resolution follows explicit parent IDs in stable order, with depth/cycle,
missing-parent, duplicate-ID and version/message/structure compatibility checks.
Built-ins are immutable. Child rules must restate compatible cardinality/datatype
for an existing rule: minimum may increase, maximum/length/value set may narrow,
and explicit component constraints may be added. Omitted maximum length, table,
contextual datatype path or empty allowed-values list inherits the base value.
Field/group ordering, datatype identity and conditional rule IDs cannot be replaced.
Closed base field definitions cannot be opened or extended with previously
prohibited fields; choices cannot gain alternatives. Relaxations are rejected,
not silently applied. Each inherited rule retains its originating provenance.

New Z-segment definitions are supported extensions through explicit structure slots
and field/component/value-set definitions. A declared Z segment receives ordinary
placement/cardinality/content checks. Unknown Z policy is explicit; Warn/Allow never
imply validation of unknown content. New inherited definitions do not mutate parents.

## XML subset and security

The importer deliberately supports the HL7 v2.5-era
`HL7v2xConformanceProfile` / `HL7v2xStaticDef` shape (chapter 2.19), declaring
**HL7Version 2.5.1**, ProfileType Constrainable or Implementation. It accepts
unqualified elements or namespace `urn:hl7-org:v2xml`. This is not an importer for
arbitrary XML, HL7 XML messages, all NIST tooling dialects or later profile formats.

Supported constructs: root/static metadata; ER7 encoding declarations; ordered
Segment/SegGroup hierarchies; R/O/RE/X/B usage; Min/Max (`*` unbounded); positional
Field/Component/SubComponent definitions; Datatype/Length/Table; ConstantValue;
plain implementation notes/references. RE knowledge-dependent semantics and narrative
notes/references become explicit coverage notices. Table labels do not fetch values.
Unsupported predicates, conditional C/CE usage, segment flavors with differing
definitions, dynamic definitions, arbitrary extensions, attributes or free rule text
are rejected with an explanation. XML rules use the same evaluator as JSON/built-ins.
The [synthetic XML example](examples/stage17-synthetic-profile.xml) demonstrates this
subset; it is a deliberately narrow custom schema, not a complete base standard.

XML DTDs are prohibited, the resolver is null, entity/external resources are never
opened, and processing instructions are rejected. Bytes, characters, depth and
node count are checked before constructing the bounded DOM. Namespace schema
locations/XInclude are not fetched and unsupported constructs are rejected.
No imported profile can execute code or make network/filesystem reference calls.

## Library and preferences

`%LOCALAPPDATA%/HL7Workbench/ValidationProfiles` contains bounded versioned local
definition records, with hash-derived filenames, import date, source filename label,
profile metadata and revision digest. Profiles are unencrypted local definitions;
they contain no HL7 source, patient values, diagnostics or document identities.
Corrupt/incompatible records are retained and counted as unavailable, not overwritten.
Linked storage is rejected. Directory/entry/aggregate budgets bound startup reads.

Import validates/resolves the candidate before atomic same-directory temp/write/
flush/rename publication. Identifier collisions require explicit reimport with the
reviewed current revision; built-ins cannot be removed or replaced. Reimport must
retain the selected ID, and updates cannot break a currently resolvable descendant.
Failures/cancellation leave prior definitions intact. Removing/updating an applied
profile or its parent clears highlights and marks the selection Unavailable; users
must explicitly apply a current definition. Unrelated imports do not rerun validation.
External disk changes are checked before intentional update/removal; no watcher
silently replaces an applied in-memory definition.

Preferences → **Validation** selects auto-detect or a built-in/imported default.
It persists through the existing atomic preferences store and preserves unknown
future settings. This affects only the initial selection in Apply Schema.
Auto-detect matches MSH-9.1/2/3 and MSH-12.1 against built-ins only, never arbitrarily
selecting a trading-partner profile. Missing/ambiguous matches require selection.
An incompatible explicit profile produces Profile.Incompatible and stops structural
evaluation rather than quietly checking the wrong structure.

## Inline presentation and lifecycle

Errors use red squiggles, warnings amber and information blue; source text colors
are retained. Missing content uses a boundary stroke and an in-editor edge marker.
Hover shows severity/path, stable rule, explanation and profile/rule source, bounded
to four findings/2,048 characters. F8/Shift+F8 and ribbon Previous/Next Issue use deterministic
source order, exact selection/caret anchors and scrolling. Compact status provides
profile, counts and NotApplied/Validating/Valid/IssuesFound/Stale/Incomplete/Unavailable.
“Valid” means only **No issues found under the applied profile**, never certification.

Abbreviated/virtualized raw previews map only their real source fragments. Omission
markers receive no diagnostic source spans. Navigation into omitted content reaches
its preview boundary; inspect the complete value with the existing Full Source
Viewer/export commands. Validation does not decode or render attachments implicitly.

Workspace's existing 180 ms structural edit debounce runs first; highlights become
stale immediately, and evaluation runs off-context under the existing work/scan
leases. Requests capture message identity/content/search revisions; cancellation and
publication guards suppress outdated results. Immutable structure and bounded source
reads reuse the existing spool without a second attachment/message materialization.
Save/Save As do not rerun unchanged content. Undo/redo and message navigation update
the owning state. View detach unsubscribes UI handlers; document disposal cancels and
drains validation before source/spool/snapshot release. Dock re-creation does not own
or duplicate the validation session. Validation emits no patient-bearing Activity Log
entries or exception telemetry.

## Resource budgets and known limits

| Budget | Limit |
| --- | --- |
| Profile input / canonical JSON | 1 MiB each |
| Profile structural depth / rules | 16 / 10,000 |
| Value-rule depth | Field, component, subcomponent only |
| Imported profiles / library bytes | 128 / 16 MiB |
| Applied selections per document | 128 |
| Validation segments / repetitions | 100,000 / 10,000 |
| Evaluation work / elapsed deadline | 1,000,000 steps / 5 seconds |
| Complete inspected value | 4,096 UTF-16 characters |
| Retained diagnostics | 1,000 including suppression/budget notice |
| Visible decorations / tooltip | 200 / 4 findings and 2,048 characters |

Existing Core/parser/editor limits are unchanged. Lower deterministic work/diagnostic
budgets are injectable in Core tests; the deadline is a cooperative backstop, not an
OS process kill. Exhaustion yields Incomplete, never silent success. The elapsed
backstop can produce a shorter incomplete result on a slower machine; completed
diagnostics remain deterministic for identical message/profile/engine version.
Only the explicitly selected message is evaluated, never the whole file.

Limitations include the base-model coverage above, specialized datatype/cross-order
semantics, no XML inheritance/predicate/flavor dialects, no unrestricted expressions,
no authoring editor, no automatic external profile refresh, and session-only applied
selections. Profile values/labels may be sensitive organizational metadata; import
stores definitions locally as disclosed. Native clipboard/focus/high-DPI, rendering
and platform picker acceptance remain manual gates.

## Original Stage 17 verification and changed files (before Stage 17F)

Both required locked restores passed, including an independent artifacts-directory
restore. Release solution build passed with **0 warnings / 0 errors**. Final focused
suites passed Core **207 / 0 / 0**, Workspace **214 / 0 / 0** and Desktop **151 / 0 / 0**
(passed/failed/skipped). These include the new schema/import/security/lifecycle/UI
tests and existing generator/editor/source/preferences/report regressions.
Added **103 permanent ordinary cases**: 75 Core, 17 Workspace and 11 Desktop.
The required exact regular command `dotnet test HL7Workbench.sln --configuration
Release --no-build --no-restore` passed **2,650 / 0 / 14** (2,664 total) in
**4m 49.518s**. All five projects passed. The same 14 opt-in identities and their
classification source files are unchanged. All **2,384 protected private-beta
SHA-256 hashes** match the preflight baseline, with no missing/additional files.
One existing preferences test now checks active validation defaults; its Apply/Cancel
and persistence assertions remain. No tests or opt-in classifications were removed.

The code uses no new package references or lock-file changes. Stage 16 generator
source and goldens remain unchanged. Source/fixture mapping, JSON/XML attack and
budget tests, exact byte preservation, profile rollback/revisions, 2 MiB source-backed
values, Save/Save As, stale-result suppression, two themes and reconstructed editor
views are covered. Both documented profile examples execute against the companion
synthetic message. Latin-1/UTF-16 escape interpretation retains the document's
original encoding, and unencodable drafts remain retained with unavailable validation.
Native rendering/focus/high-DPI/platform pickers remain manual.

43 changed files: **17 updated tracked files / 26 new files**:

| Area | Files (repository-relative) |
| --- | --- |
| Fixture/attribution infrastructure | `.gitattributes`, `.gitignore`, `build/update-validation-catalog.py`, `build/third-party/nHapi-MPL-2.0-LICENSE.txt` |
| Core | `src/HL7Workbench.Core/HL7Workbench.Core.csproj`, `Hl7ValidationProfile.cs`, `Hl7ValidationCatalog.cs`, `Hl7ValidationSourceMap.cs`, `Hl7SchemaValidator.cs`, `Hl7ProfileImporter.cs`, `Hl7ProfileResolver.cs`, `ValidationCatalog/v251-model.json`, `ValidationCatalog/NOTICE.md` |
| Workspace | `src/HL7Workbench.Workspace/DocumentManager.cs`, `EditorPreferences.cs`, `MessageDocument.cs`, `MessageDocument.Validation.cs`, `ValidationProfileLibrary.cs` |
| Desktop | `src/HL7Workbench.Desktop/MainWindow.axaml.cs`, `MainWindow.Shell.cs`, `Viewer/MessageInspectionView.cs`, `Viewer/MessageInspectionView.Validation.cs`, `Viewer/PresentationView.cs`, `Viewer/PreferencesWindow.cs`, `Viewer/SchemaSelectionDialog.cs`, `Viewer/ValidationInlineRenderer.cs` |
| Tests | `tests/HL7Workbench.Core.Tests/HL7Workbench.Core.Tests.csproj`, `Hl7SchemaValidationTests.cs`, `Hl7ProfileImportTests.cs`; `tests/HL7Workbench.Workspace.Tests/ValidationProfileLibraryTests.cs`, `SchemaValidationLifecycleTests.cs`; `tests/HL7Workbench.Desktop.Tests/SchemaValidationViewTests.cs`, `ShellFinishingTests.cs` |
| Documentation/examples | `README.md`, `docs/PROJECT_CONTEXT.md`, `docs/architecture.md`, `docs/desktop-preferences.md`, `docs/decisions/0021-explicit-inline-schema-validation.md`, `docs/stage17-hl7-schema-validation.md`, `docs/examples/stage17-profile.schema.json`, `stage17-synthetic-hie.json`, `stage17-synthetic-profile.xml`, `stage17-synthetic-message.hl7` |

Evidence remains under ignored `artifacts/stage17`: final logs, baseline/change hashes,
protected-file and stress-classification audits, reviewed failure diagnostics,
independent restore metadata and the pinned upstream regeneration cache. The three
documented synthetic examples are permanent regression inputs, not disposable exports.
Twenty-one reviewed redundant intermediate logs were removed. Final runtime/test/
resource hashes match the source snapshot captured for the final suite. Main/HEAD
remains `9686f5259f9c1a8b99cc4631dfae40731b410284`, with an empty index and all
43 changes unstaged. No commit, push, package, publication or Stage 18 work occurred.

## Stage 17F ribbon/layout verification

Stage 17F supersedes the earlier toolbar layout. Core/Workspace validation,
generator algorithms, templates and tests are unchanged by this refinement.
The normal and independent artifacts-directory locked restores passed again after
resuming verification. The final Release solution build passed with **0 warnings /
0 errors**. Focused suites passed **Core 168 / 0 / 0**, **Workspace 95 / 0 / 0** and
**Desktop 132 / 0 / 0** (passed/failed/skipped). These focused filters cover schema,
profile and synthetic generation code, plus ribbon, responsive layout, insertion,
document history and viewer regressions. Their case sets differ from the historical
focused filters above; the full regular solution suite remains the acceptance gate.

The exact full regular command `dotnet test HL7Workbench.sln --configuration Release
--no-build --no-restore` passed **2,667 / 0 / 14**, **2,681 total**, in **5m 03.560s**.
All five projects passed. The 14 skipped cases have exactly the same opt-in
identities as the preceding Stage 17 run, and classification sources are unchanged.
After the final suite, all **2,384 protected beta SHA-256 hashes** matched the
original baseline: **0 changed / 0 missing / 0 additional files**. Runtime, test and
build sources match the snapshot used for this build and suite; only documentation
was finalized afterward. No package references, lock files or generator sources
changed in Stage 17F.

Added **17 permanent Desktop cases** covering ribbon grouping/shared commands,
enabled states, active-document/message targeting, docked/floating editors, modal
cancellation/stale choices, library reuse/import/reimport/removal, long profile names,
keyboard focus, two themes and layouts at 100/150/200% transform scales. Existing
schema-view cases now invoke the shell-owned command flow. No Core/Workspace tests
or opt-in classifications were removed or changed by Stage 17F.

The interrupted verification exposed an incorrect transport-tab test expectation,
which was corrected, and a ribbon test synchronization race: closing the modal
selector schedules schema application asynchronously. The test now awaits the whole
shell command operation before checking findings and enabled states, rather than
awaiting a validation task obtained before application started. The same assertions
remain, and no production validation algorithm changed. The final build also
confirms the interrupted nullable-warning corrections.

Stage 17F changes **15 files** relative to its saved preflight working state:
**13 existing files updated and 2 new files**. Earlier uncommitted Stage 17 work is
retained. The complete working tree has **47 changed paths** relative to HEAD.

| Area | Stage 17F files (repository-relative) |
| --- | --- |
| Shell | `src/HL7Workbench.Desktop/MainWindow.Shell.cs`, `src/HL7Workbench.Desktop/MainWindow.axaml.cs` |
| Editor and commands | `src/HL7Workbench.Desktop/Viewer/FileDocumentView.cs`, `MessageInspectionView.cs`, `MessageInspectionView.Validation.cs`, `SchemaSelectionDialog.cs`, `UtilityRibbon.cs`; new `SchemaValidationCommands.cs` |
| Tests | `tests/HL7Workbench.Desktop.Tests/SchemaValidationViewTests.cs`, `ShellFinishingTests.cs`; new `SchemaRibbonTests.cs` |
| Documentation | `docs/PROJECT_CONTEXT.md`, `docs/desktop-preferences.md`, `docs/decisions/0021-explicit-inline-schema-validation.md`, `docs/stage17-hl7-schema-validation.md` |

The owner manually launched Message Workbench on Windows after the layout/ribbon
changes and reported both working correctly. This records the observed native
inspection, without claiming every detailed checklist item below was exercised.
Headless transform-scale checks do not establish actual per-monitor DPI behavior;
native picker and detailed scaling/focus acceptance can use the checklist below.

Evidence is retained under ignored `artifacts/stage17f`: resumed restore/build/
focused/full logs, preflight and final change manifests, protected-file and skip
audits, verification summary, reviewed interrupted-run diagnostics and independent
restore metadata. These verification artifacts are intentionally retained; no
disposable HL7/CSV exports were created. Main/HEAD remains
`9686f5259f9c1a8b99cc4631dfae40731b410284`, with an empty index and all changes
unstaged. No commit, push, package, publication or Stage 18 work occurred. Work stops
after this verification report; further native checklist coverage remains explicit.

## Native Windows acceptance checklist

- Open standalone/scenario ADT/ORM/ORU, malformed text, custom delimiters and mixed
  multi-message files. Confirm no schema or diagnostics until explicit application.
- In HL7 Tools, inspect the Schema Validation group in pinned and popup modes.
  Use Apply/Change Schema and Remove Schema on different files and messages.
  Confirm disabled states with no document, no applied schema and no findings.
- Apply built-ins; inspect profile/version/coverage notices and legitimate existing
  generator length findings. Confirm original saved bytes remain unchanged.
- Open Manage Schemas with no document; import through Import Schema, cancel and
  retry the native picker, and verify an already-open library is reused. Close the
  dialog during import and confirm no canceled profile is published.
- Import the synthetic HIE JSON; inspect required authority, I/O class, ZPI placement
  and inpatient routing condition. Reimport intentionally, test collisions and remove
  an applied profile/parent. Confirm unavailable status and explicit reapplication.
- Try supported XML, unknown predicates/attributes, DTD/entity attacks and malformed/
  oversized profiles. Confirm clear rejection, no transmission and no library loss.
- Confirm there are no schema or Previous/Next Issue buttons in the raw toolbar.
  Check Editing, Message navigation and Validation status independently at narrow
  dock widths (about 250–400 DIPs), floating sizes and maximized windows. Use a
  160-character profile name: ellipsis/full-name hover, counts, Incomplete and state
  must stay readable without overlaps/clipping. Repeat light/dark at 100/150/200%
  Windows scaling and move windows between monitors with different DPI.
- Inspect red/amber/blue marks and zero-width boundary strokes, hover text and counts
  in light/dark, narrow and high-DPI layouts. Use F8/Shift+F8, mouse selection, normal
  HL7 hover, text editing, undo/redo and Save/Save As without shortcut/focus conflicts.
- Validate a large OBX/ED message; navigate visible and omitted ranges using the
  existing full-source workflow. Ensure no attachment rendering or fake marker spans.
- Tab through the ribbon, use Down/Left/Right/Escape in popup mode and confirm
  picker focus and issue navigation return to the intended editor. Test both ribbon
  Previous/Next Issue and F8/Shift+F8 after docking/floating.
- Switch documents/messages, float/redock/reconstruct the editor, change defaults,
  type rapidly, remove/reapply schemas and close during evaluation. Confirm no stale
  highlights, duplicate work, source-disposal races or persisted applied selections.

No commit, push, package, publication or Stage 18 is part of Stage 17 preparation.
Stop for user acceptance after the required solution verification and hash audit.
