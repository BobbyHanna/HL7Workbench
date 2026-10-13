# Milestone 2 docking integration gate

Scope: synthetic placeholder documents only. Full raw/structured viewer, local file
opening, and concatenated-message navigation await explicit continuation approval.
AvaloniaEdit is approved for the later viewer phase and is not installed at this gate.

## Run

Set the three telemetry opt-out variables as documented in README, then:

```powershell
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

The default layout has two document tabs, Message Browser at left, and Diagnostics
below the documents. ORU intentionally contains an unsupported escape and missing
header value so Diagnostics has visible synthetic data. ADT has no diagnostics.
Closing document tabs removes them from Message Browser; restarting opens the two
synthetic documents again. Reset Layout resets tool geometry without reopening
closed documents.

## Manual acceptance passed (2026-10-13)

The user retested the previously failing scenarios and confirmed the docking gate
passes. Rapid document-tab switching works. Inactive, active, and final document
tabs can be closed. Both tool windows can float at the same time, dock and refloat,
and remain well behaved when the main window closes. Automatic layout restoration
and Reset Layout work. The earlier failures are resolved; the docking foundation is
complete. The supplemental checks below were not part of this acceptance report.

## Supplemental desktop checklist

- [ ] Both placeholder document tabs render; clicking them changes Diagnostics title/content.
- [ ] Message Browser selects the corresponding tab. Tool focus retains the active message.
- [ ] Close a document; Browser removes it and Diagnostics follows the remaining document.
- [ ] Drag a tool title/tab to left, right, top, and bottom using docking guides.
- [ ] Reorder tabs/group tools together; tools cannot become document tabs.
- [ ] Resize tool panes with splitters and resize the main window without losing controls.
- [ ] Float each tool (drag out or context menu); resize its native window and dock it back.
- [ ] Close/hide each tool, then reopen it through View. Verify content and active context.
- [ ] Hide both tools, save/restore, then reopen both tools through View.
- [ ] Save Layout, rearrange, Restore Layout: groups, split orientation/proportions,
  tool visibility, active document, and floating bounds return.
- [ ] Restart: saved tool layout restores and the synthetic documents are available.
- [ ] Float a tool on another monitor, save, disconnect that monitor, and restart:
  its title bar remains reachable. Reset Layout / Recover Windows provides a fallback.
- [ ] Move floating tools between monitors with different DPI, including negative
  screen coordinates; confirm pointer guides, bounds, text scaling, and restoration.
- [ ] Test keyboard tab navigation and application scaling. Close the main window:
  no floating process/window remains.
- [ ] With the app closed, place invalid JSON in the layout file and restart:
  defaults appear with recoverable status feedback. Restore a valid layout afterward.

## Persistence and known limits

`%LOCALAPPDATA%/HL7Workbench/workspace-layout.json` contains an application-owned
version-1 schema. It stores dock tree, tab order, proportions, tool visibility,
active document ID, and floating normal-window geometry. It does not contain HL7
payloads, paths to message files, imported values, scripts, or feature view models.
Save occurs explicitly and on main-window closing; replacement uses a temporary file.
Invalid or excessive layouts fall back to defaults. Unknown identities are omitted.
Current documents come from the session, not from the layout file.

Floating positions use screen pixels; sizes use Avalonia device-independent units.
Recovery keeps a title-bar anchor inside an available work area; unavailable anchors
move to the first available screen. Full mixed-DPI behavior requires manual validation.
Maximized/full-screen state and auto-hide/pinning are not persisted in this initial
gate (pinning is disabled). Native tool floating is enabled; document floating is
disabled to retain the central document workspace. No automatic file-session restore.
Hidden tools reopened after layout restoration use an available tool group, or a
new right-hand group if none exists; exact pre-hide placement is not persisted.

## Automated evidence

Workspace tests cover document lifecycle, lazy registry identity, UI independence,
bounded metadata serialization, rejected layouts, and reachable/off-screen anchors.
Desktop tests exercise the actual Dock factory/model with a deterministic host
substitute: separate document/tool content, active context, hide/reopen, reopening
after restore, moving tool groups, floating geometry, splitter proportions, closed
documents, repeated switching, simultaneous tool floating, floating-window shutdown
requests, and feature-model unsubscription. These do not prove native pointer gestures,
OS window behavior, rendered appearance, multi-monitor behavior, or DPI correctness.
The core docking acceptance gate passed manual desktop review on 2026-10-13.
Supplemental checks above remain useful for future platform-specific verification.

## Stabilization investigation (2026-10-13)

- **Closing the main window left floating windows alive:** the app had no explicit
  Avalonia `OnMainWindowClose` shutdown mode or Dock floating-window shutdown setting.
  Startup now enables Dock's owner and close-on-main-window-close settings; the
  desktop lifetime explicitly shuts down with the main window. Native confirmation
  remains part of the manual checklist.
- **Closing and switching documents:** current fake-host integration tests did not
  reproduce the process disappearance. A later DEBUG log captured the fault in
  `DockContentTemplate`: document rows dereferenced `d!.Title` and diagnostic rows
  dereferenced `d!.Severity` while Avalonia was refreshing/recycling item content
  synchronously during collection removal or document switching. The `!` only
  suppresses nullable analysis. Avalonia's generic `FuncDataTemplate<T>` considers
  null valid data for reference types and can pass it to the callback. Neither
  `DocumentManager.Close` nor the feature view models dispose/clear those row objects;
  null transitional template data was the cause. The callbacks now reject null in
  `Match` and render an empty row if `Build` is nevertheless called directly with null.
  Outer Dock content also handles null, unset, and unexpected data without casting it.
  Focused tests construct and invoke the actual Avalonia templates and cover switching
  plus inactive, active, and final document removal.
- **Floating a second tool:** a new Dock-model regression floats both registered
  tools, docks one back into the other tool group, and requests exit for both fake
  hosts; it passes. This does not reproduce native-window creation or OS ownership.
  The exact native failure remains unconfirmed pending desktop retest and diagnostics.
- **Diagnostics:** DEBUG builds append lifecycle markers, exception type, and stack
  trace (without exception messages or HL7 values) to
  `%LOCALAPPDATA%/HL7Workbench/development-diagnostics.log`; they do not mark
  dispatcher exceptions handled. Release builds omit these handlers.

An upstream Dock issue documented a detached visual during floating-window drag in
Dock 12/Avalonia 12 and was addressed by a merged Dock change before the currently
locked 12.1.0.7 package. It covers a related drag/docking sequence, not this exact
reported failure; do not treat it as the established root cause. See the
[Dock report](https://github.com/wieslawsoltes/Dock/issues/1136) and
[merged fix](https://github.com/wieslawsoltes/Dock/pull/1137).
