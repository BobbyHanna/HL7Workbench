# Apply a schema and review inline findings

Use **HL7 Tools → Schema Validation**. Prerequisite for application: a prepared
active message. Opening a file never applies a schema or starts schema validation.
Tolerant structural parsing/diagnostics remain separate from schema evaluation.

## Apply and navigate findings

1. Open [adt-a01.hl7](../../samples/synthetic-only/adt-a01.hl7) and keep its
   message active. The fixture exercises syntax/preservation, not guaranteed profile validity.
2. Choose **HL7 Tools → Schema Validation → Apply/Change Schema**.
3. Select **HL7 2.5.1 ADT^A01**, review its details and choose **Apply Schema**.
   Auto-detect uses MSH-9/MSH-12 for matching built-ins; custom profiles require selection.
4. Review the raw editor's compact profile/error/warning/coverage/state indicator.
   Hover inline marks for explanations. Use **F8 / Shift+F8** with editor focus or
   the ribbon's **Next Issue / Previous Issue** to navigate retained findings.
5. Edit only if desired. Once applied, validation refreshes after edits; outdated
   results are canceled/rejected. Choose **Remove Schema** to clear the active
   message's selection/findings without changing text.

Expected result: findings appear inline, not in a separate results tool. The base
model can show **Incomplete** even with zero errors, because unsupported conditional,
terminology and datatype coverage is explicit. This does not certify conformance,
clinical validity or suitability for a receiver.

Application belongs to one stable message identity in this session. Moving to
another message/document does not transfer it. Returning revalidates a previously
applied selection. Save/layout/restart do not persist applied selections or findings.

## Import and manage a custom profile

1. Choose **HL7 Tools → Schema Validation → Import Schema**. This works without
   an open message and opens the existing local profile workflow.
2. Choose the reviewed synthetic [native JSON example](../examples/stage17-synthetic-hie.json)
   or [supported XML example](../examples/stage17-synthetic-profile.xml). Review
   details/status. Importing alone does not apply a profile.
3. Use **Apply/Change Schema** to explicitly apply the imported choice. The JSON
   HIE example adds local requirements beyond the base; unrelated fixtures may
   correctly fail them. [Its synthetic message](../examples/stage17-synthetic-message.hl7)
   supplies a more targeted demonstration; base coverage notices still apply.
4. Use **Manage Schemas** to reopen the same library. **Reimport selected…**
   updates an imported definition; **Remove imported profile** removes it from the
   library. Built-ins are protected. Library removal differs from Remove Schema
   on the active message and can invalidate dependent selections.

JSON supports narrowing inheritance and explicitly placed Z segments; XML supports
a strict subset of conformance profiles. Unsupported constructs/rules are rejected
or explicitly incomplete, not silently certified. Imported profiles are local files;
there is no remote resolution or arbitrary executable rule support.

## Cancel, defaults and limits

Cancel the selector to leave application unchanged. If the message/document/revision
changes while it is open, close/reopen and apply to the intended active message.
Failed imports retain existing valid definitions. A profile update/removal may
require explicit reapplication/resolution; do not trust old findings.

**Settings → Preferences → Validation** selects only the initial picker choice,
not automatic validation. Actual schema actions are on the ribbon. Built-ins cover
only v2.5.1 ADT A01/A04/A08/
A02/A03, ORM O01 clinical orders and ORU R01, with explicit gaps. At most 128
message selections per document and other evaluator/import budgets apply.

See [formats, source attribution, coverage and limits](../stage17-hl7-schema-validation.md)
and [native JSON authoring schema](../examples/stage17-profile.schema.json).
