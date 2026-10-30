# Stage 19D: offline Documentation Help

Baseline: `63ffd0ce4e9afa2815eee27e846e015fb62b438f` with the completed, uncommitted
Stage 19A–19C documentation preserved. This stage adds native Help integration,
its permanent regression coverage and the two approved UI wording corrections.
No public distribution, package, commit or push is performed. Native Windows
acceptance remains pending.

## Implementation and scope

**Help → Documentation** reveals an optional native Avalonia tool with stable
identity `tool.documentation`. It uses ToolWindowRegistry/DockContentTemplate and
the existing layout DTO. It can dock/float/redock/close/reopen, including with no
HL7 document. Contents/topic results, headings, Back, selectable prose, bounded
tables and read-only copyable code use standard themed controls and accessible names.

The model contains immutable documentation metadata and session navigation only.
It has no MessageDocument/snapshot, SQL, transport, log or script-execution service.
Views subscribe on attachment and unsubscribe on detachment. Float/template replacement
retains the model; close/reset releases it. Reopen/application restore starts with
empty search/history and Contents. Only placement/visibility is serialized.

No WebView, browser/Markdown engine or new runtime package is added. The existing
Script API Reference, About, F1, Ctrl+Space and Ctrl+Shift+Space paths are preserved.
An API link reveals the existing reference tool; it neither duplicates its catalog
nor executes a starter/script. Documentation code copying is explicit clipboard
interaction, never code execution.

### Canonical Markdown and deterministic resources

`docs/help-topics.json` explicitly curates **20 topics**: all 18 files in `docs/user/`
(index plus 17 guides) and shared `limits.md`/`storage-and-formats.md`. It assigns
stable topic IDs/keywords. Developer/acceptance/history/private-preview files,
fixtures and unrelated repository content are not bundled as additional topics.

`build/WriteDocumentationCatalog.py` compiles that allowlist into
`src/HL7Workbench.Desktop/Documentation/catalog.json`, embedded as
`HL7Workbench.Documentation.json`. Output order, normalized-LF source digests and
UTF-8 JSON are deterministic; no machine path/time or runtime repository dependency
is included. Markdown remains canonical: do not hand-edit the generated resource.
Regenerate/check using the documented maintenance commands. CI checks this catalog,
compiler tests and the unchanged generated Script API reference.

Supported markup is explicit: one H1 title, headings through H6, soft-wrapped
paragraphs, flat dash/numbered lists, quoted paragraphs, strong/emphasis/inline code,
ordinary links, fenced code (including an explicitly indented list example), and
pipe tables with unaligned separators. Escaped punctuation and pipes in code/table
cells are preserved. Unsupported constructs fail generation: nested lists/emphasis,
images, HTML/entities, reference links, indented code, hard breaks, strikethrough,
alternative fences/alignment and malformed/unclosed markup. Meaningful content is
not silently stripped to satisfy the renderer. Extending syntax requires compiler,
rendering and preservation tests rather than weakening rejection.

The compiler validates IDs, source allowlist/path bounds, link targets/headings,
duplicate-heading anchors, table widths and resource/inline limits. Runtime loads
and validates only the embedded resource, then exposes read-only nested collections.
It never opens a Markdown file from the deployed build's working directory.

### Links and offline limits

Bundled-topic links resolve stable topic IDs and anchors. Heading selection and
Back navigate locally without changing document selection or stealing focus.
Links to the script reference/workflow/catalog route to the existing API tool.

Other repository references retain their original label and relative location,
clearly marked **repository / not bundled**. Selecting one displays its location;
it does not read that file, launch a filesystem handler or fetch a replacement.
External HTTPS references, if present in future approved text, are similarly inert
and labelled; no automatic browser/network opening is implemented. This preserves
the reference without expanding the approved content bundle. Some deeper format,
fixture, release and developer material therefore requires a separate reviewed
checkout; Help does not claim every linked repository file is included offline.

### Search and lifecycle bounds

- Embedded resource maximum **2 MiB**, at most **64 topics / 512 blocks per topic**.
  Tables are at most 256 rows/16 columns; inline/span budgets are validated.
- Search is local literal ordinal case-insensitive AND over approved title,
  keywords/headings/body only. Query length is at most **256 UTF-16 units**, with
  at most **16 whitespace-delimited terms**; excessive terms give explicit feedback.
  Results are bounded by the topic limit; snippets are 160 characters plus ellipsis.
- Navigation history holds at most **64 locations**, in memory only. Search queries,
  selected topics/anchors and history are never added to preferences/layout.
- Search does not inspect loaded documents, original source files, snapshots,
  saved scripts, SQL/history, logs or custom profiles. Relative source paths in
  catalog provenance are build metadata, not runtime access capabilities.

The view stacks results/article in narrow space and uses side-by-side panes at
wider widths. Tables/code scroll horizontally inside finite controls; prose wraps.
Headless scale-transform coverage is not proof of real OS high-DPI behavior.

## Approved wording corrections

Preferences HL7/Validation explanations now point to **HL7 Tools → Schema Validation**
and its existing Apply/Change/Manage workflow. The Generation Report View-menu
tooltip now identifies both synthetic HL7 and FHIR reports. Commands, defaults,
profile storage and generation behavior are unchanged. The shell menu regression
still checks exact Help entries, now including Documentation before API Reference/About.

## Automated verification

Normal and independent-artifacts locked restores pass. The final Release build
passes with **zero warnings / zero errors**. The final SDK gate explicitly sets
DOTNET_CLI_TELEMETRY_OPTOUT, AVALONIA_TELEMETRY_OPTOUT and
TESTINGPLATFORM_TELEMETRY_OPTOUT to 1; earlier commands had not inherited them and
were repeated with these repository settings.

| Focused coverage | Passed | Failed | Skipped |
| --- | --- | --- | --- |
| Embedded catalog/model, immutability, search, anchors, metadata rejection | 14 | 0 | 0 |
| Native Help controls, themes/scales, short docks, clipboard, lifecycle and wording | 9 | 0 | 0 |
| Existing Script API Reference | 5 | 0 | 0 |
| Existing reference native-input regression | 1 | 0 | 0 |
| Existing editor assistance | 31 | 0 | 0 |
| Existing schema ribbon | 17 | 0 | 0 |
| Existing Generation Report tool | 6 | 0 | 0 |
| Exact existing shell-menu regression method | 1 | 0 | 0 |
| **Focused C# total** | **84** | **0** | **0** |
| Python compiler regression methods (separate runner) | 10 | 0 | 0 |

The **final full regular solution suite passes 2,739, fails 0 and skips the same
14 expected explicit opt-ins (2,753 total; 4m 53.637s)**. Added 23 permanent C# cases
and 10 Python regression methods. The pass before adding the two short-dock cases
was 2,737/0/14; it is retained as intermediate evidence, not the acceptance count
for the final source. All five regular test projects passed. Stress/performance
opt-ins were not enabled for the regular gate.

Both generated catalog consistency checks pass. The authored-doc scan covers
**147 Markdown files, 1,065 local links and 37 heading-anchor links**, with zero
missing targets/anchors; external URLs are not claimed retrieved. Catalog generation
and tests separately validate the curated graph. The bundle has 86 heading blocks,
127 paragraphs, 148 list items, 15 tables and two code blocks; its 188 links are
60 offline topic links, eight existing API links and 120 labelled repository references.
No unapproved extra text is followed/transcluded into the bundle.

SHA-256 audit against the original `artifacts/stage17/protected-before.json` finds
**2,384 expected / 2,384 present, zero changed, zero missing, zero added**.
`git diff --check` passes; new-file whitespace checks and final source/hash checks
are included with the retained evidence. No package/lock/Core/Workspace/Scripting/
Querying/QueryHost/ScriptHost/Transport source changes were made.

Earlier development attempts are not represented as final passes:
nullable-flow warnings were resolved by annotating the actual validation guard;
a headless text-binding issue was corrected; unsupported nested lists now reject;
and test filters were corrected to the actual class/method names. Final short-dock
review replaced a fixed results height with proportional rows and added explicit
viewport/status separation cases. The compiler
test harness uses uniquely owned ignored workspace scratch space after the sandbox
denied nested writes in its OS temporary directory.

No native application/endpoint acceptance, installer, portable assembly or publishing
was performed. Existing unit/integration tests may use headless UI and synthetic
loopback as part of the regular suite; that is distinct from the manual checks below.

## Changed-file inventory

New files:

- `build/WriteDocumentationCatalog.py`, `build/test_documentation_catalog.py`
- `docs/help-topics.json`, this implementation/native checklist
- `src/HL7Workbench.Desktop/Documentation/DocumentationCatalog.cs` and generated `catalog.json`
- `src/HL7Workbench.Desktop/MainWindow.Documentation.cs`
- `src/HL7Workbench.Desktop/ViewModels/DocumentationViewModel.cs`
- `src/HL7Workbench.Desktop/Viewer/DocumentationView.cs`
- `tests/HL7Workbench.Desktop.Tests/DocumentationTests.cs`, `DocumentationViewTests.cs`

Existing-file updates:

```text
.github/workflows/build.yml
README.md
docs/index.md
docs/user/workspace.md
docs/user/validate-with-profiles.md
docs/user/preferences-and-shortcuts.md
docs/developer/index.md
docs/developer/desktop-tools.md
docs/developer/documentation-maintenance.md
docs/developer/build-and-test.md
src/HL7Workbench.Desktop/HL7Workbench.Desktop.csproj
src/HL7Workbench.Desktop/Docking/WorkspaceDockFactory.cs
src/HL7Workbench.Desktop/Docking/DockContentTemplate.cs
src/HL7Workbench.Desktop/MainWindow.axaml
src/HL7Workbench.Desktop/MainWindow.axaml.cs
src/HL7Workbench.Desktop/Viewer/PreferencesWindow.cs
tests/HL7Workbench.Desktop.Tests/ShellFinishingTests.cs
```

Stage 19D therefore has **17 updated / 11 new files (28 paths)**. Stage 19A–19C
reports, earlier historical records and ADRs remain byte-identical to the starting
working tree. The earlier uncommitted work remains present.

The final exact path manifest and verification evidence are retained under ignored
`artifacts/stage19d/` (restore metadata/logs, focused/full test logs, input/path hashes
and final audits). Compiler scratch directories are uniquely owned under ignored
`artifacts/documentation-tests/` and clean up per test; generated Python bytecode
from initial probes was removed. No HL7/CSV exports or disposable probe source was
retained. Existing earlier-stage work is not discarded or folded into a
claim that all working-tree changes originated in Stage 19D.

## Native Windows acceptance checklist — pending

Use a complete current Release build and reviewed synthetic content only. Record
Windows version/build, application/source revision, monitor scaling and theme.
No test below approves packaging or public distribution.

1. **First launch/no document:** open Help → Documentation with no HL7 tab. Confirm
   Contents, all tasks, headings and readable empty/no-results states. API Reference
   and About still open from Help.
2. **Offline/deployment:** run a copied complete build tree from a folder/working
   directory without repository Markdown and with network unavailable. Contents,
   search, tables/code and internal links still work. No reference automatically
   opens a browser, file, worker, endpoint or import dialog.
3. **Search:** try `seed`, `SQLite`, `privacy`, multiple terms/case variations and
   an absent term. Confirm bounded snippets/no-results feedback, topic navigation
   and Back. Try the query length/term bounds.
4. **Links/headings:** jump within a topic and between guides, use the headings
   selector and Back. Repository-only references identify their location and lack
   bundled content without silently fetching anything.
5. **Tables/code:** read the privacy/limits tables at narrow width, scroll them
   horizontally, select prose and copy SQL code to an external plain-text editor.
   Verify exact symbols/escaping/newlines and safety warnings; nothing executes.
6. **API cross-navigation:** follow a Script API link. The existing reference opens;
   its search/examples/contract display remain intact. In Script Editor, verify F1,
   explicit Ctrl+Space and Ctrl+Shift+Space against a trusted synthetic starter.
7. **Keyboard/accessibility:** menu accelerator, Tab/Shift+Tab, list/heading arrow
   navigation, Contents/Back/Copy activation and focus visibility. Check accessible
   names, screen-reader reading order and selectable/copyable content.
8. **Themes/layouts:** Light/Dark and live switching, narrow/wide/maximized main
   windows and a short dock. Verify readable text and finite scroll areas without
   overlaps or clipped actions.
9. **Real DPI/monitors:** check 100/150/200% where available, moving floating Help
   between monitors, off-screen recovery and actual keyboard/caret/scroll behavior.
10. **Dock/lifetime:** float/redock, replace the view, close/reopen. Float/redock keeps
    session state; close/reopen resets it. No duplicated subscriptions, stale content
    or active-document changes occur.
11. **Layout/restart:** save/restore/reset/recover, restart the app with Help visible
    and hidden. Placement/visibility restores appropriately; search/history/selected
    topics do not. Other tool/document drafts follow their existing consent behavior.
12. **Scope/wording:** with a dirty synthetic HL7 tab, open/search Help and confirm
    text/history/selection/dirty/save/schema state unchanged and no execution/network
    tools started. Check both approved Preferences and report-tooltip descriptions.

Stop for owner review of automated results and this native checklist. Do not claim
native acceptance until these platform checks are actually performed and recorded.
