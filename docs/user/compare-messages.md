# Compare original, working or pasted messages

Use **HL7 Tools → Comparison → Message Comparison**. No document is required for
pasted comparison. For the draft workflow below, open
[adt-a01.hl7](../../samples/synthetic-only/adt-a01.hl7) and make the synthetic name
edit in [getting started](getting-started.md), leaving it unsaved.

## Compare a draft with its original

1. Activate the edited message.
2. Choose **HL7 Tools → Comparison → Compare With Original** or the raw editor's
   **Compare With Original** context-menu command.
3. Inspect **Side-by-Side** to see original A and current draft B. Switch to
   **Differences** for grouped changes; the `Avery` → `Rowan` edit should appear.
4. Use Next/Previous or **F7 / Shift+F7** with comparison focus to move through differences.
5. Review ignore/copy settings in the comparison menus. Copy is explicit; comparison
   never applies a merge or changes an HL7 document/history.

Expected result: the initial source stays distinct from the draft, even after Save.
An inserted message has no original during that open session, including after Save;
reopen saved output to establish one.

## Compare pasted text or other open messages

1. Open **Message Comparison**. New sessions start in **Side-by-Side**.
2. Paste one complete synthetic message into each empty A/B pane. Alternatively,
   use each source control's **Open Message**, **Original Message** or **Pasted Text**.
3. Let the debounced comparison refresh, then inspect differences. Each side retains
   its pasted draft when you switch source kinds during the session.

Document-backed panes are read-only. Pasted panes are temporary editors: their exact
characters/line endings are retained, not saved to an existing document. Multiple
MSH messages receive a single-message explanation with the draft retained.

## Recovery and limits

Source edits/closure invalidate affected comparison results; reselect/recompare
current sources instead of interpreting stale positions as a different message.
Large document messages use bounded source windows. Pasted input uses the complete
editor limits of 1 Mi UTF-16 units/message and 16 Ki/line.

Closing/reopening comparison or Reset Layout starts a new session and releases its
pasted text/results; there is no persisted comparison history. Resizing/floating
the same session retains its drafts. Copy valuable temporary text explicitly before closing.
No comparison action saves or transmits content.

See [comparison behavior and copy limits](../message-comparison.md),
[architecture decision](../decisions/0010-read-only-message-comparison.md) and
[storage/privacy](privacy-and-storage.md).
