# Responsive dockable tools

The [FHIR Patient Workspace follow-up](fhir-patient-workspace.md) uses a compact
master-detail body and collapsed generation section instead of the earlier full-width
Synthetic Patients browser. Its empty state hides lists entirely. Menu-based import
actions remain real buttons in an accessible flyout; an active Cancel stays directly
visible. Internal browser/tab minimums are finite, not whole-tool minimum dimensions.
The original composition guidance below still applies. New hierarchy tests exercise
the complete bottom dock and floating roots, not only an isolated feature Grid.

Fresh/reset Dock sizing is armed once after a finite viewport exists. Root identity
checks prevent a queued default callback from overwriting saved-layout restoration;
ordinary resizing never reapplies the default ratios. Keep this ownership rule when
adding new startup paths. See DefaultDockSizing.cs and FhirWorkspaceTests.cs.

Stage 15UI uses small Desktop-only helpers over native Avalonia controls. Tool
content measures its own DIP viewport; it never uses MainWindow width. Dock owns
headers, tabs, placement and floating chrome. Core and Workspace remain unchanged.
The existing themes, editor palettes and bounded source/value presenters still apply.

## Composition

Use a Grid with Auto rows for controls/status and star rows for dense content.
Call `ResponsiveToolSurface.Attach` once during construction, after adding children.
It moves those existing controls into its content Grid; it does not clone them.
An optional primary bar stays above the body. Other controls and status messages
scroll vertically when their natural height plus content minimums exceed the tool.

```csharp
using Avalonia.Controls;
using HL7Workbench.Desktop.Viewer;

var run = new Button { Content = "Run" };
var cancel = new Button { Content = "Cancel" };
var filter = new TextBox();
var rows = new ListBox(); // Use a virtualizing items panel for large collections.
var status = new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap };
var tool = new Grid { RowDefinitions = new("Auto,*,Auto") };
tool.Children.Add(new ResponsiveFormField("Filter", filter));
Grid.SetRow(rows, 1); tool.Children.Add(rows);
Grid.SetRow(status, 2); tool.Children.Add(status);
var surface = ResponsiveToolSurface.Attach(tool,
    new AdaptiveActionBar(run, cancel), (1, ToolLayoutMetrics.ListHeight));
```

The row indexes passed to Attach refer to the original Grid. An existing direct
child can be promoted as the primary bar. Afterwards, inspect or change those row
definitions through `surface.Content`, not the outer tool's new wrapper rows.
Keep tool margins on the outer control. Do not change the Dock template or default
workspace proportions to repair feature content.

## Actions and forms

`AdaptiveActionBar` is a native horizontal WrapPanel. It keeps logical/tab order,
adds the standard gap once per child and limits each child's width to the available
line. Text labels wrap. Its original min/max widths are restored when constraints
grow; no action is cloned, silently hidden or moved between parents on resize.
Keep Cancel/Stop first when urgent access matters. Disabled actions remain visible.
Use existing tooltips to explain unavailable actions. Avoid large indivisible action
groups; a child containing its own fixed-width descendants must also adapt.

`ResponsiveFormField(string label, Control input)` associates a wrapping label with
its input through Avalonia automation properties. At 600+ DIP the label occupies
180 DIP beside the input; below that width the same controls stack. The input has
no forced width or minimum. For a compact field inside an action bar, give the
field wrapper a preferred Width; the bar constrains it at narrower sizes.

```csharp
var mode = new ComboBox { ItemsSource = new[] { "Current", "All" } };
var options = new AdaptiveActionBar(
    new ResponsiveFormField("Scope", mode) { Width = 220 },
    new Button { Content = "Preview generation" });
```

Guidelines, in DIP: 600+ full, 400–599 compact, 250–399 stacked, below 250 minimal
accessible scrolling. These are not four mandatory template modes. Actual measured
control needs determine wrapping. Central `ToolLayoutMetrics` defines 6 DIP gaps,
10 DIP section gaps, 150 DIP editor, 120 DIP list, 100 DIP inspector-tree minimums,
and a 270 DIP clinical browser minimum including tabs/wrapped page controls.
Cache breakpoint state before replacing row/column definitions; do not reset
user-adjusted splitters on every SizeChanged event.

## Scrolling and dense content

An ordinary outer ScrollViewer measures vertically at infinity, which can turn
Grid star rows into Auto rows, expand editors and defeat list virtualization.
`ResponsiveToolSurface` instead measures its content against a finite height budget
from the actual tool. Its native vertical ScrollViewer reports the complete overflow
extent. Horizontal toolbar scrolling is disabled.

`ToolContentGrid` retains native Grid row/column calculation but exposes its
unclipped vertical desired extent. Avalonia's default finite measure would otherwise
hide overflow from the outer scroller. This also propagates newly expanded Auto
details. Use ToolContentGrid for **nested** Auto/star compositions that may overflow,
as in Synthetic Patients' browser/details and Field Inspector's lookup/tree/value
composition. It respects explicit Height and min/max height settings. It is not a
new general layout engine or an unbounded list host.

Set **row** minimums as well as any control minimums. A large child MinHeight alone
does not reserve its star row when Auto details compete for the same finite height;
it can instead overflow adjacent sections. Synthetic Patients sets the nested
browser row's minimum to `ToolLayoutMetrics.BrowserHeight`. The surface remeasures
at the complete finite extent before arranging, so nested star allocations update.
Test section boundaries as well as individual action bars; rendered inspection
caught a browser/details overlap that bar-only assertions could not detect.

Give star rows practical dense-content minimums through Attach. Keep editors,
virtualized lists and results in those finite rows; do not put them in an unbounded
vertical StackPanel. SQL has a 150 DIP editor and 160 DIP results-region minimum.
The horizontal SQL data-table scroller is intentional: a complete relational table
may be wider than its viewport. Result paging, scalar materialization limits,
bounded previews and explicit full-value inspection remain authoritative.

Primary bars have a vertical fallback only for unusually short/narrow hosts,
capped at half the tool height and at 160 DIP (28 DIP floor). The body remains
reachable. Wheel/keyboard scrolling and native BringIntoView are retained. Separate
inner scrolling is appropriate for genuinely separate domains such as a transport
configuration form, exchange history, editor text or a virtualized list. Avoid
arbitrary nested vertical scrollers around every small section.

## State and lifecycle

Resize changes measurement/attached positions; it never rebuilds editors, lists,
models, sessions or result pages. Keep model subscriptions in matching attach/detach
handlers. The responsive helpers subscribe only within their own control tree;
they do not observe the main window, static resize events or workspace services.

Dock can build a new view before detaching the old view. Desktop's internal
`ToolEditorState` transfers the live AvaloniaEdit TextDocument/undo stack and
selection between SQL/Script views over the same model. It has only a weak editor
owner reference. A detached editor releases document handlers by switching to an
empty document under synchronization suppression. A late detach cannot overwrite
the replacement's selection. A different script identity/source invalidates the
handoff. Disposal clears the retained state; nothing is serialized to layout or disk.
Closing a release-on-close tool still follows its original session cleanup rules.

## Testing

`ResponsiveToolTests` hosts individual tools at 1200×800, 900×600, 600×400,
350×300 and 250×250 DIP inside a larger fixed-size window with separate header/tab
sentinels. It measures real controls, checks action rectangles/overlap, navigates
through actual scroll viewports, runs synthetic import/generation and SQL queries,
checks finite list realization, and verifies selection/undo/page/session preservation.
Additional cases exercise height-only changes, expanded details, large-value chunks,
wheel/PageDown/focus scrolling, repeated threshold crossings and Dock ownership changes.

Scaling cases use LayoutTransformControl at 1.0/1.25/1.5. These stress layout but
**do not establish Windows per-monitor DPI correctness**. Floating ownership uses
Dock's fake native host plus actual headless Windows for feature measurement.

Optional nonblank Skia frames:

```powershell
$env:HL7WORKBENCH_RESPONSIVE_RENDER_DIRECTORY = 'C:/Dev/HL7Workbench/artifacts/stage15ui/rendered'
dotnet test --test-modules tests/HL7Workbench.Desktop.Tests/bin/Stage15UI/net10.0/HL7Workbench.Desktop.Tests.dll --filter-class '*ResponsiveToolTests'
Remove-Item Env:HL7WORKBENCH_RESPONSIVE_RENDER_DIRECTORY
```

These are rendered headless frames, not native Windows acceptance. Follow the
[acceptance checklist](stage15ui-acceptance.md) for real docking, floating chrome,
focus indicators, pickers, clipboard, maximized/restored windows and display scaling.

## Common mistakes

- Fixed-width horizontal StackPanels or horizontal toolbar scrolling hide actions.
- Many Auto rows above a tiny star row can leave a zero-height editor or list.
- A large minimum on the whole tool can extend it under Dock tabs.
- A plain nested finite Grid may hide its overflowing Auto rows from a parent scroller.
- Basing breakpoints on the application window misses narrow individual docks.
- Rebuilding controls on resize loses selection/undo and can duplicate subscriptions.
- Bounded preview text remains presentation only; layout must not change export,
  source identity, source-byte ownership, encoding, SQL or synthetic-data semantics.
