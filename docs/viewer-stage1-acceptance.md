# Milestone 2, Stage 1: read-only raw viewer

Stage 1 passed user manual acceptance on 2026-10-14. Stage 2 requires separate
authorization and has not started. No editing, saving messages, navigation, inspector, scripting,
or transport is included.

## Manual acceptance passed (2026-10-14)

The user confirmed all eight categories passed: file opening, document tabs,
syntax highlighting, read-only behavior, scrolling, malformed messages, docking,
and persistence and shutdown. This records the user's category-level acceptance;
the detailed checklist below remains available for regression testing.

## Future viewer requirements (not implemented)

- Keep the raw HL7 viewer above a Segment Navigator in the lower-left pane and
  a Field Inspector in the lower-right pane, with adjustable splitters.
- For combined multi-message files, place a fixed navigation toolbar at the
  bottom of the raw-message pane with first, previous, next, last, and message
  position/count controls.
- Prioritize combined single-file batches over directory-wide processing.
- Preserve version-independent structural parsing and future version/profile
  extensibility; semantic definitions remain separate from raw rendering.

These requirements are documented for future work only. Do not start Stage 2
without explicit user authorization.

## Architecture and preservation

Workspace's `MessageFileReader` reads bounded streams as bytes, then runs the
existing Core parser off the UI thread. Documents are published on the desktop
context only after a complete parse. Each open has an opaque random session ID;
filename collisions receive numeric suffixes, including repeated opens of the
same file. Opening does not rebuild the dock tree or disturb floating tools.
Closing removes the workspace document and docking adapter references.

Desktop uses Avalonia `StorageProvider.OpenFilePickerAsync`, permits multi-select,
offers HL7/text/data filters and All Files, and creates AvaloniaEdit document
content through the existing Dock content template. There are no startup example
tabs. Canceling the picker leaves the workspace unchanged. Read failures and Core
limit exceptions produce count-only status; exception messages and imported
values are never logged. Invalid encodings open a tab with a static explanatory
notice and retain original bytes; they are never shown as an empty message.

The editor receives `RawText` directly, without serialization, trimming, escape
decoding, or newline replacement. Core retains bytes, selected encoding, and BOM;
the BOM is encoding metadata rather than a displayed character. The editor is
read-only and its editing pipeline rejects insertions and deletions. The editor
buffer is independent of the Core message; it is never a save source.

Core's new `Hl7Element.CharacterOffset` exposes existing UTF-16 coordinates without
changing parsing. Desktop builds sorted highlighting spans from those coordinates
and child boundaries. Thus custom MSH delimiters, omitted older encoding
characters, paired escapes, and unmatched-escape recovery follow Core. Opaque
malformed lines use ordinary value color. Missing/invalid MSH uses Core's
provisional standard delimiters; multiple MSH files follow Core's first-header
interpretation and diagnostics. Highlighting adds no document characters.

AvaloniaEdit's `DocumentColorizingTransformer` applies spans only to visible lines,
with binary lookup. Ordinary values are light gray on a dark canvas; segment IDs
blue, fields gold, components green, repetitions orange, subcomponents purple,
and escape markers salmon. This offset-based presentation map can support later
selection overlays/navigation without adding parsing to UI code.

`MessageDocument.DeclaredVersion` exposes the raw MSH-12 declaration for a future
version/profile resolver. Such a resolver must remain separate from structural
parsing and return unavailable definitions for unknown declarations. No field
names, standards catalog, version rejection, or semantic certification is added.

## Known limits

- One file is one bounded Core parse. Concatenated messages and framing are
  retained and diagnosed, without per-message splitting/navigation.
- BOM-less input uses strict UTF-8. No legacy encoding selector or MSH-18 guessing.
- Core defaults apply: 16 MiB input, 8 Mi UTF-16 characters, plus structural limits.
  Parsing is eager; open documents collectively consume memory. No session-wide
  memory budget is imposed yet. Parser cancellation prevents publication but
  cannot interrupt a parse already executing within its bounds.
- Layout saves contain IDs/geometry only. Files are not reopened on restart.
- The viewer has a fixed dark palette. Native behavior passed the user's Stage 1
  acceptance; supplemental monitor and mixed-DPI checks remain platform-specific.

## Exact manual acceptance steps

Automated verification: locked restore passed; Release build passed with zero
warnings/errors; all 263 tests passed with zero skips; `git diff --check` passed.
Tests cover byte/BOM and mixed-terminator retention across five Unicode encodings,
empty/BOM-only/malformed/undecodable inputs, byte bounds and cancellation, duplicate
titles, actual editor selection and rejected text input, delimiter-aware spans,
and dynamic docking insertion after closing all documents and restoring layout.
Existing parser/docking regressions remain included. Headless tests validate
editor behavior without proving native picker, clipboard, or rendered appearance.

From the repository directory, run:

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
$env:AVALONIA_TELEMETRY_OPTOUT='1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT='1'
dotnet run --project src/HL7Workbench.Desktop -c Release --no-build --no-restore
```

1. Confirm an empty central workspace with Message Browser and Diagnostics. Use
   File → Open File, then Cancel; no tab should appear.
2. Open `samples/synthetic-only/adt-a01.hl7`. Confirm raw message text, line
   numbers, distinct delimiter/segment colors, and no character substitutions.
3. Open `oru-r01.hl7` and `oru-custom.hl7` together. Switch tabs rapidly and select
   them through Message Browser. Diagnostics must follow the selected tab; tool
   focus must retain it. In the custom sample confirm `*`, `$`, `%`, `!`, `@`
   receive their respective delimiter colors.
4. Reopen `adt-a01.hl7` twice; verify distinct tabs ending in `(2)` and `(3)`.
   Also open synthetic files with the same basename from two local directories.
5. Select with mouse and Shift+arrow keys; use Ctrl+A then Ctrl+C and paste into
   an external scratch editor. Confirm copying works. Try typing, Delete,
   Backspace, Ctrl+V, Ctrl+X, and dropping text: the viewer must remain unchanged.
6. Resize the window and panes. Open a synthetic long-line/many-line file; scroll
   horizontally and vertically, select across lines, and copy again.
7. Using temporary synthetic files, open an empty file, BOM-only file, malformed
   line followed by `PID|1`, mixed CR/LF/CRLF input, and a file lacking a final
   terminator. All decodable characters must remain visible/selectable; no crash.
8. Open UTF-8 BOM, UTF-16 LE/BE BOM, and UTF-32 BOM synthetic files. Non-ASCII
   synthetic text must display correctly. Open bytes `FF 81`: a text-unavailable
   notice and `InvalidEncoding` diagnostic must appear.
9. Use All Files to open a synthetic file with an unrelated extension. Try a
   file larger than 16 MiB and an unreadable/deleted selection where practical;
   status must report failure without imported values. Existing tabs remain usable.
10. Close inactive, active, and final tabs, then open another file. Float both
    tools, open more files, dock/refloat, Save/Restore Layout, and Reset Layout.
    Open tabs must survive layout operations. Close the main window while both
    tools float; all windows must exit. Restart: layout restores, files do not.

Stage 1 integration acceptance passed as recorded above. Retain this checklist
for future regressions; Stage 2 still requires separate authorization.
