# Preferences and keyboard shortcuts

Use **Settings → Preferences…** for application settings. No document is required.
Use a synthetic open document when testing edit/navigation keys; shortcuts depend
on which tool or input has focus.

## Change a preference

1. Choose a category: **General**, **HL7**, **Appearance** or **Validation**.
2. In General, choose the default Edit Mode for newly opened documents and
   Collapsed/Expanded ribbon visibility. Existing documents keep their mode/work.
3. In Appearance, select **Follow System**, **Light** or **Dark**. The applied
   application theme updates immediately; no restart is required. The C# code
   editor retains its dedicated dark palette rather than becoming a full light editor.
4. In Appearance, set **Default Content Font Size**, labelled in DIP. The default
   is 14 DIP; 12–32 DIP is approximately 9–24 points. Apply updates open content
   tools immediately, with existing code/heading typography scaled proportionally.
   **Reset Content Size** restores 14; Apply saves it. Ribbon/menu sizing is unchanged.
5. In Validation, choose auto-detection or a default profile for the explicit picker.
   This never enables validation on open or rewrites MSH-12.
6. Choose **Apply** to save without closing, or **OK** to save/close on success.
   **Cancel**/closing discards only changes since the last successful Apply.

Expected result: successfully persisted settings become active. A failed save keeps
the prior active preference and reports feedback. Pin/unpin uses the same ribbon
visibility setting. HL7 explains declared version/interpretation and the explicit
[ribbon workflow](validate-with-profiles.md).

## Keyboard shortcuts

| Keys | Focus/context | Action |
| --- | --- | --- |
| Ctrl+F | Main or floating workbench shell | Reveal Find Messages and focus its applicable input; does not run search. |
| Ctrl+S | HL7 document context | Save HL7 through the existing document workflow. |
| Ctrl+Shift+S | HL7 document context | Save HL7 As. Use the File menu explicitly if focus makes the target unclear. |
| Ctrl+S | Script Editor tool | Save Script, not HL7. |
| Ctrl+S | SQL Query tool | Save the named query, not HL7. |
| Ctrl+Z / Ctrl+Y / Ctrl+Shift+Z | Raw HL7/document context | Document Undo / Redo / Redo, including retained history with Edit Mode off. |
| Ctrl+Z / Ctrl+Y | Independent text input/editor | Its own text history. Search fields, numeric inputs, C# and SQL editors do not become HL7 document history targets. |
| Enter | Message-number input | Go to that logical message. |
| Enter | Find Messages value input | Run Search. In the results list, follows the selected retained message identity. |
| Ctrl+Enter | SQL Query tool | Run query. |
| Ctrl+C | SQL results | Copy selected complete bounded values; paging/sorting clears selection. |
| F7 / Shift+F7 | Message Comparison | Next / Previous difference. |
| F8 / Shift+F8 | Raw editor with retained validation findings | Next / Previous issue. Ribbon issue commands are another entry point. |
| F1 | C# code editor | Context-sensitive Script API help; no execution. |
| Ctrl+Space / Ctrl+Shift+Space | C# code editor | Explicit facade completion / invocation signatures. |
| Ctrl+plus/minus, Ctrl+0 | Focused content tool, docked or floating | Save a local font override / remove it and inherit the global default. Includes raw HL7, inspection, search, SQL, extraction, Help, API reference and other registered content tools. |
| Ctrl+mouse wheel | C# code editor | Existing code-only font zoom; ordinary wheel scrolling elsewhere is unchanged. |
| Enter / Escape | Script rename input | Confirm valid draft name / cancel rename; Save Script persists it. |
| Left/Right/Home/End; Down; Escape | Ribbon tabs/commands | Select tab; enter commands; dismiss temporary ribbon. Tab follows normal focus order. |

Use visible actions when in doubt about focus. Ctrl+S in a script/query tool does
not save the active HL7 document. Avoid relying on a menu gesture to change the
contents of an unrelated tool. Clipboard history/sync is outside Workbench control.

## Other saved UI state and recovery

The Find Messages Advanced builder/results split is separate from Dock placement;
Simple keeps its compact layout. Content font defaults and keyed per-tool overrides
are saved separately from Dock geometry. An existing Script Editor font preference
migrates once into the shared settings, clamped to supported bounds; its default
12-point code appearance is retained. Ctrl+0 deliberately removes that override.
The content tooltip reports the effective reference size and shortcuts. Raw HL7
tabs share one raw-viewer override, rather than saving document-specific settings.
Fonts use native text layout and Windows DPI scaling; no restart is needed.
Modal configuration/export/About dialogs and application chrome keep normal sizing.
Workspace layouts store geometry/visibility, not documents/results/network state.
See [workspace recovery](workspace.md#recover-the-workspace) before resetting.

For exact persistence/failure policies see [desktop preferences](../desktop-preferences.md),
[script editor behavior](../script-api.md) and [storage locations](privacy-and-storage.md#storage-and-retention).
Native IME, clipboard, monitor/DPI and accessibility behavior may need Windows review.
