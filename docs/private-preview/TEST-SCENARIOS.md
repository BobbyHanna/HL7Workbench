# Suggested private preview tasks

These are lightweight feedback exercises, not the formal 68-check acceptance
worksheet. They are proposed outcomes, **not recorded passes**. Use one instance,
synthetic input and [LIMITATIONS](LIMITATIONS.md). No repository or developer tools
are needed. Report useful observations with [FEEDBACK](FEEDBACK.md).

| # / goal | Basic steps | Expected observable outcome / useful feedback |
| --- | --- | --- |
| 1. First launch | Verify the approved checksum, Extract All, start MessageWorkbench.exe; Help → About Message Workbench… | Version 0.1.0-beta / Build 8f5c43cc. Record Windows/security blocks or unclear instructions without bypassing protections. |
| 2. Generate synthetic input | File → Generate Messages…; standalone ADT^A01, Realistic Synthetic, 30 messages, new document; record the actual seed/settings | New unsaved 30-message tab and Generation Report. Describe confusing defaults or report wording; seed 42 plus identical settings supports replay. |
| 3. Save, open and navigate | File → Save As… to a synthetic filename; File → Open File…; use First/Previous/Next/Last and Go 17 | Correct message index/count and content; saved file reopens. Report wrong navigation, lost content or unclear active tab. |
| 4. Inspect structure | View → HL7 Inspection → Field Inspector / Segment Navigator; select PID, expand field 5/repetition/component; copy a value/path | Inspector and raw view refer to the same message and existing value. Describe unclear labels, selection or long-value presentation. |
| 5. Edit and save | Enable owning document's Edit Mode; Inspector Edit Value… on an existing component; Undo, Redo; Save As a separate file and reopen | Both views agree after parsing; saved output contains the change. Report history, focus, encoding or dirty-state surprises. Preserve the input file. |
| 6. Bulk replacement | Select existing PID-5.2 in the Inspector; Search & Replace…; keep the prefilled Find value, enter PreviewSynthetic as Replace with; Active file → Analyze / Preview; review, confirm and Apply all to new document | Preview leaves input unchanged. One active Bulk Result.hl7 has only reviewed replacements, all 30 messages remain, and input views/history stay unchanged. Report counts, absent inputs or raw/Inspector mismatches. |
| 7. SQL filtering | View → Data & Query → SQL Query; Current document; run the query below on the 30-message document, then Go to source on a row | Up to the first three messages, with correct source navigation. Report query/status clarity, paging or stale-source behavior; no external database is used. |
| 8. Trusted C# | View → Data & Query → Script Editor; New, replace starter with the reviewed example below; Apply to: Current message → Run; Review Output when available | Independent output changes the selected message's given name, input stays unchanged. Report compilation/run/output/cancel behavior; never paste unreviewed code. |
| 9. Schema validation | On an ADT message: HL7 Tools → Schema Validation → Apply/Change Schema; choose HL7 2.5.1 ADT^A01 → Apply Schema; inspect findings, then Remove Schema | Inline schema/count/coverage state follows the message; no source edit on application/removal. Record findings and usability, not a clinical-validity claim. |
| 10. Offline Help | Help → Documentation; search a task, follow local topics; open Help → Script API Reference | Useful local navigation/search without a download. Report missing explanations, broken topic navigation or unreadable layout. |
| 11. Close and reopen | Save useful output; close dirty work and exercise Save / Discard / Cancel carefully; exit, then reopen the app and saved synthetic file | Cancel retains work; explicit save persists it. Layout does not recover unsaved contents. Report prompts, lingering processes or unexpected plaintext files without attaching them. |
| 12. Optional advanced work | Choose either local synthetic FHIR or explicit loopback MLLP as described below | Record the exact chosen branch and observed outcome; leave unattempted work untested. No production systems, external endpoints or developer installation. |

SQL for task 7:

```sql
SELECT DocumentId, MessageId, MessageNumber
FROM Messages
WHERE MessageNumber <= 3
ORDER BY MessageNumber;
```

Reviewed synthetic C# for task 8 (it changes only PID-5.2 in the processed message):

```csharp
void Main() { Message.SetText("PID-5.2", "PreviewSynthetic"); }
```

Optional task 12 branches:

- **FHIR:** only if the owner supplies an independently reviewed synthetic R4 Bundle:
  File → Import Patient FHIR…; in the FHIR Patient Workspace choose Import FHIR ▾ →
  Import FHIR Bundles…. Browse a patient, then Generate HL7 with an explicit scope;
  inspect the new output/report. Record unsupported records and relationship/mapping
  limits; do not fetch a production FHIR server or assume arbitrary JSON is synthetic.
- **MLLP:** View → Connectivity & Testing → MLLP Listener / Connection Manager.
  Configure literal 127.0.0.1 and an available test port in both, Positive test ACK
  in the listener; Start listener, Connect and Review and send one message… after
  checking the intended synthetic message. Inspect correlated outcomes, disconnect
  and stop the listener. Report ACK/status ambiguity. Never bind a public interface
  or infer clinical processing from a test ACK; no TLS is provided.
