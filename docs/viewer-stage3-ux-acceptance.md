# Viewer Stage 3 consolidated UX refinement

The user explicitly granted native Windows acceptance for the complete Stage 3
milestone, including Activity Log and final layout polish, on October 14, 2026.
Verification and one local milestone commit are authorized; push, publication,
tagging and release are not authorized. The Stage 3 source/index/scanner implementation
and all original Stage 2 test cases are retained.
The initial native Windows pass found issues; see `stage3-windows-acceptance-fixes.md`
for the accepted cached-template fix. Activity Log is included in final acceptance;
see `stage3-activity-log-acceptance.md` for that refinement and
`stage3-workspace-layout-acceptance.md` for the latest layout policy and checklist.

## Phase 1: readable main presentation

Workspace `MessagePresentation` traverses Core structural ranges, folds the smallest
leaf value strictly above **16,384 UTF-16 units**, and keeps a scalar-safe **256-unit
prefix and 256-unit suffix**. Datatype and segment names do not determine folding.
Actual delimiters, following siblings, fields, terminators and segments survive.
Closed escapes use the shared scanner's structure, including custom delimiters and
literal MSH fields. Large opaque segments receive an explicit opaque-source marker.
If structural scanning exceeds its existing budgets, a clearly identified whole-
source preview is used; no partial model is claimed to be complete.

`DisplayFragment` distinguishes retained source text from synthetic markers. Each
fragment has a displayed UTF-16 interval; retained fragments have original decoded
intervals. A marker has **no source characters** and separately identifies the whole
original target range and its exact message-relative byte range. Interval searches
translate hover, selection and navigation, without per-character maps or rescanning
omitted payloads. `ContentVersion` rejects intents from stale context menus/dialogs.
Diagnostic locations remain original source coordinates, rather than preview positions.

Desktop `PresentationView` uses one AvaloniaEdit document when the abbreviated text
is at most **1 Mi UTF-16 units** with bounded lines. This compact preview soft-wraps
so following fields are visible; ordinary small-message wrapping is unchanged.
Larger presentations or lines
use Avalonia's virtualized ListBox with bounded **16 Ki-unit editor blocks** and
natural vertical scrolling. Blocks read only their visible ranges; no paging toolbar
or fake newline is inserted into source. Long logical lines may continue across
separate display blocks. Each block has independent horizontal scrolling and text
selection. Cross-block drag selection and global original line numbering are not
provided by this fallback; the Full Source Viewer supplies contiguous source access.
These block boundaries are UI boundaries, not HL7 segment boundaries.

File sessions use the ordinary materialized viewer only below the 1 Mi-unit document
and 16 Ki-unit longest-line display gates, while preserving Core's original public
parser/edit/serialization limits. Ordinary small messages keep their exact full text,
raw hover and double-click behavior. The main **First/Previous/Next/Last Text** toolbar
is removed. First/Previous/Next/Last **Message** navigation remains below raw content,
with no MSH metadata and appropriately disabled single-message controls.

Syntax coloring and selection coloring map retained fragments individually. Markers
use a distinct muted, italic style and background; marker hover identifies a display-
only omission. Double-clicking a marker selects its structural target intentionally.
Ordinary Copy is rejected across an omission, rather than silently joining source
fragments. **Copy Visible Preview** explicitly copies presentation text, including
markers. Clipboard operations retain the 1 Mi-unit cap.

## Phase 2: optional Full Source Viewer and exact export

`RawInspectorSession` borrows the owning document's snapshot/spool. It has no second
snapshot, parser or full-value string. `FullRawInspectorView` is a **tool**, opened
by **View Full Value**, **View Full Message Source**, or View → Full Source Viewer.
Labels identify the scope/path and original message ordinal.

The inspector shows unabridged contiguous **16 Ki-unit** windows, without markers.
Previous/Next Chunk and direct **scope-relative UTF-16 offsets** provide bounded
access. Status also shows message-relative UTF-16 intervals, total message length
and exact original message-byte intervals. Direct jumps inside surrogate pairs are
rejected; automatic previous-chunk navigation aligns safely. Hover/double-click use
the same original structural selection and scanner ranges.

**Export Original Value…** and the inspector's **Export Original…** stream exact
snapshot bytes in **64 KiB** buffers. The selected value excludes adjacent delimiters.
Full-message scope means the indexed payload, excluding file-level BOM metadata and
MLLP envelopes; those remain preserved in the owning snapshot/source-region inspector.
No preamble, line ending, text normalization, omission marker or attachment decoding
is added. A string-only legacy document with no authoritative snapshot cannot export
as byte-exact source. Exports are labeled original encoded/source content.

Export writes a unique sibling temporary file and publishes with a final replacement
only after complete success and cancellation checks. Cancellation or write failure
preserves an existing destination and removes temporary output when the filesystem
permits it. Original-file and snapshot targets, including resolvable directory/file
aliases, are rejected. The retained original path is used only for overwrite protection,
never navigation or layout persistence. An external provider without an original path
cannot offer pathname identity checks; snapshot bytes still remain immutable.

Ownership policy: **one optional inspector tool per workspace, bound to one document
and message**. Opening another scope replaces its session. Switching document tabs
does not silently retarget it. Changing its bound message or closing its document
closes the inspector. Closing the inspector cancels reads/exports, releases its model,
editor, buffers, subscriptions and document reference, and leaves the document open.
Dock reparenting/tab deactivation releases view resources without closing the session;
reattachment creates only a bounded window. No previous source content is restored
on restart. Generation checks prevent stale read publication.

The final layout request described retaining source across same-file message
navigation as well. Investigation found the invalidation above in code/tests; the
user explicitly chose to leave reading/export unchanged and document the discrepancy.
Same-file retention remains deferred. Document-tab switching and explicit replacement
are verified and must not become an implicit live active-document mirror.

## Phase 3: tabbed tools and remembered layouts

Dock.Avalonia/Dock.Model.Mvvm **12.1.0.7** remains the framework. Its tool groups,
tab switching/reordering, proportional splits, docking guides, native floating hosts
and redocking are reused. No docking framework migration or custom docking engine.

`InspectionPanes` extracts the existing Segment Navigator/Field Inspector controls.
Desktop tool adapters follow `DocumentManager` and share `MessageInspection` with
the raw viewer. Tool focus does not activate another document. Fresh/reset defaults
put documents left (78%), Navigator/Field Inspector in separate equally stacked right
docks (22%), and visible Activity Log across the bottom (27% height). Browser is
hidden by default. Valid saved arrangements and hidden preferences remain authoritative.
Full Source Viewer remains closed/lazy; Activity Log restores its saved visibility.
View menu entries reopen individual tools. The standalone
embedded-pane composition remains available for existing component regressions.
On restore, structural tools absent from older layouts are introduced without
overriding an explicitly hidden identity. The Field Inspector's selected-value
chunk row is hidden for values fully contained in its 4,096-unit bounded preview;
larger values retain chunk navigation, position feedback and scope-specific tooltips.

`ToolDefinition.Optional` and `ReleaseOnClose` extend the small existing registry.
The optional inspector's editor, model, session and buffers are **not created at
startup**, including when it was visible in a saved layout. Lightweight Dock identity
wrappers and placement metadata are permitted. No broad plugin system is introduced.
Optional/releasable tool content now observes Context replacement across cached Dock
templates; opening and reopening no longer relies on a floating transition to rebuild
the feature host. The native-acceptance fix and regression are documented separately.

Layout location is unchanged:
`Environment.SpecialFolder.LocalApplicationData/HL7Workbench/workspace-layout.json`
(Windows: `%LOCALAPPDATA%\HL7Workbench\workspace-layout.json`). The bounded JSON
schema is now **version 2**, with version-1 compatibility. It stores stable tool/group
IDs, tab order/active IDs, split orientation/proportions, floating bounds, and remembered
placement/ancestor slots for closed tools. Optional source-backed tools are omitted
from live restore; their placement survives. Empty optional floating hosts are not
opened. Reopening can recreate a collapsed group or floating host. Unknown identities
are omitted; malformed/incompatible/excessive layouts recover to defaults.

Layout files contain no HL7 content, source filenames, snapshots, controls, feature
models, selections, preview text or prior inspector scope. Save/restore/reset retain
the existing application coordination and temporary-file replacement. Maximized state,
pinning and automatic file-session restore remain outside scope.

## Verification and native acceptance

Run the commands in `viewer-stage3-acceptance.md`, followed by its optional stress
command. The preceding Windows-fix suite had **451 passed** and Workspace stress
**180 passed**, including 256/512 MiB attachments, with no failures/skips and zero
build warnings/errors. See `stage3-activity-log-acceptance.md` for the latest polish
verification and `stage3-performance.md` for observations and measurement limits.

Automated tests cover leaf folding, later siblings/segments, custom delimiters,
opaque/escaped input, threshold boundaries, Unicode/byte mappings, marker hover,
double-click and clipboard guards, virtualized structure navigation, contiguous raw
chunks, original export in five encodings, failures/cancellation, scope/document
lifecycle, tab context/order, sole-group recreation, lazy initialization and floating
placement. Model/headless evidence does not establish native gesture or OS behavior.

Before approval, perform these native Windows checks with synthetic files:

1. Open the 20/50/100 MiB ORUs in `artifacts/stage3/`. Confirm OBX-6 and the following
   NTE are visible and selectable immediately, and message navigation remains intact.
2. Exercise huge TX/NTE/Z values, components/repetitions/subcomponents, multiple large
   values and custom delimiters. Confirm later structure, terminators and highlighting.
3. Hover/double-click prefixes, suffixes, following values and fold boundaries. Markers
   must show omission feedback and never claim a source-character path.
4. Scroll naturally and use Segment Navigator/Field Inspector tabs. Confirm there is
   no main text-paging toolbar. Exercise many-small-value virtualized blocks and direct
   navigation to the final segment; check horizontal scrolling and block selection.
5. Select across a marker: ordinary Copy must explain the omission. Copy Visible Preview
   must be explicitly presentation text. Copy ordinary contiguous HL7 as before.
6. Right-click a marker/value → View Full Value. Check scope label, initial original
   content, Previous/Next Chunk, direct offsets and tail access without decoding PDFs.
7. View Full Message Source and View → Full Source Viewer. Verify ordinary messages too.
   Check read-only editing guards, hover paths, double-click and Unicode offset errors.
8. Export a full value and message payload. Compare exact bytes/hash and lengths with
   the generated source ranges. Test cancellation, overwrite prompts, unwritable output
   and original-file rejection; no partial final output should appear.
9. Rapidly open/close/reopen the inspector, change messages and switch document tabs.
   Bound message changes/closure must close it; another document must stay isolated.
10. Group Navigator, Field Inspector and Full Source Viewer into bottom tabs, reorder,
    close one, float/redock each, resize, and create split groups on left/right/bottom.
    Confirm tool focus retains the selected document and floating hosts render content.
11. Save/restart with the inspector both open and closed. It must remain uninitialized
    until requested, then reopen in its remembered group/order/size or floating position.
    Check a closed sole-tool group, nested splits, unavailable monitors and mixed DPI.
12. Restore old version-1 layout, missing/corrupt JSON, and Reset Layout. Close the main
    window with a floating inspector and pending read/export; check process/resource
    cleanup and that source content never appears in the JSON.

Native Windows acceptance has been explicitly granted. One local milestone commit
is authorized; do not push, publish, tag or release.
