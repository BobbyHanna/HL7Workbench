# Find messages and filter navigation

Use Find Messages to locate messages in one active document without transforming
them. Prerequisite: an open HL7 file; this example uses
[adt-a01.hl7](../../samples/synthetic-only/adt-a01.hl7).

## Find the synthetic identifier

1. Activate the document, then press **Ctrl+F**, choose **View → HL7 Inspection →
   Find Messages**, or **HL7 Tools → Search → Find Messages**.
2. In **Simple**, choose **HL7 Field**, enter path `PID-3.1`, choose **Equals**
   and enter `SYN-MRN1`. Leave case sensitivity off for this example.
3. Choose **Search** or press Enter in the value input.
4. Double-click the matching row, or select it and press Enter, to navigate the
   existing document. Enable **Filter Navigator** to limit its navigation to matches.
5. Choose **Clear**, or **HL7 Tools → Search → Clear Search / Restore Navigator**,
   to restore all navigation while retaining the authored query.

Expected result: this fixture matches once. Rows identify matching messages rather
than copying patient values/snippets into a report. Filtering never deletes messages.
An excluded current message can remain selected until you navigate to a match.

## Combine conditions or save a definition

1. Switch to **Advanced** for nested ALL/ANY groups. For example, combine
   `MSH-9.1 Equals ADT` with `PID-3.1 Equals SYN-MRN1` under ALL.
2. Use HL7 Segment with Exists/Does Not Exist for segment presence, rather than
   typing a bare segment into a field path. Run explicitly after editing conditions.
3. Expand **Search Library**, name a valid definition and choose **Save Search**.
   Select and **Load Search** to reuse it; loading never runs it. Update/Rename
   operate on the loaded saved record; Delete requires confirmation.

Saved names and literal values are plaintext local JSON and may be sensitive.
There is no automatic query history. Loading/searching does not save a definition.

## Cancellation, freshness and limits

**Cancel** stops scanning without publishing partial results/filtering. Query edits,
source edits/undo/redo/insertion and tab switching invalidate old results/filtering;
rerun for the desired document/revision. Opening the tool alone does not execute.

Search is literal, not regex. It compares raw selected text without HL7 escape
decoding or numeric conversion. Bracket indexes are unsupported; all applicable
segment occurrences and repetitions are examined. Missing values differ from
present nonmatching values, especially for negative conditions. Skipped/unreadable
messages reduce coverage and are counted; zero matches is not proof of complete absence.

Advanced queries allow 16 conditions, 16 groups and depth four. Results have 200-row
pages; the navigator filter uses the complete retained matching identity set.
For exact indexed extraction or full CSV, [choose another analysis workflow](index.md#choose-an-analysis-workflow).
See the [search semantics/library reference](../message-search.md).
