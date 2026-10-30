# Generate synthetic HL7 and reproduce a batch

Use **Home → Messages → Generate Messages** or **File → Generate Messages…**.
No input document is needed for a new output. To insert into an existing document,
activate that target before opening the dialog; it is captured when the dialog opens.
Generation is local and does not sample the active patient's data.

## Generate standalone messages

1. Choose **Standalone messages** and a message type: ADT A01/A04/A08, ORM O01
   or ORU R01. For a small first example choose ADT A01, Count 2.
2. Leave **Demographic style → Realistic Synthetic** selected. Keep the initial
   explicit base timestamp, `2030-01-01T00:00:00+00:00`, and prefix `SYN`.
3. Leave **Seed** blank for one new cryptographically selected seed on Generate.
   Review the configuration preview; it does not generate message bodies.
4. Choose a new unsaved document, or the captured target and Before Current,
   After Current, Beginning or End. Choose **Generate**.
5. Open **View → Reports → Generation Report** or the output's report indicator.
   Review the actual seed, style/version and counts. Use **Copy seed** for replay.

Expected result: a complete validated synthetic batch appears in one independent
unsaved document or one atomic target insertion. The target enters Edit Mode and
selects the first generated message; a single Undo removes the inserted batch and
Redo restores the same bytes/order. Nothing is saved or transmitted automatically.

## Generate a connected scenario

1. Open Generate Messages and choose **Admission to discharge**, **Order to result**
   or **Combined encounter**.
2. Choose patient and encounters-per-patient quantities. For order scenarios,
   choose orders per encounter and reports per order. Review the exact message estimate.
3. Choose inpatient admission or outpatient registration where applicable, optional
   inpatient transfer, and the available clinical example. Reports progress from
   preliminary to final by default.
4. Expand **Advanced options** only when overriding supported transport/patient/order
   values. Blank application/facility inputs use automatic defaults; explicit codes win.
5. Generate and review the report. Related events share coherent synthetic
   patient/visit/provider/order identities; this is not clinical inference or certification.

## Replay seeds and choose a style

Paste the report's seed into Seed and repeat **all other settings**, including base
time, style and generator version, to reproduce output bytes. Explicit 0 and 42
are valid; seeds are unsigned integers through 4,294,967,295, so negatives are unsupported.
Each separate blank-seed Generate resolves once after preflight. Validation/preview
does not consume a seed; canceled operations do not publish, and a new retry is a
new operation. Switching modes retains separate editable seed drafts, not resolved seeds.

Realistic Synthetic uses fictional American-style adult profiles, providers and
facilities; standalone and scenario realistic output use version 2. **Obvious
Placeholders** retains original standalone version-1 explicit-seed bytes and the
scenario placeholder behavior. To replay the former standalone default, choose
placeholders and explicit 42. Standalone advanced transport/patient overrides
require realistic style. Identity collisions across replayed batches are intentional;
synthetic names/addresses can coincide with real people/places.

## Cancel or recover

**Cancel**/closing a busy dialog cancels before publication. Invalid options,
unsafe insertion joins, incomplete drafts or exhausted budgets reject the entire
request; complete/undo the draft, choose a safe location or a new output and retry.
Insertion may need temporary disk space for the whole prospective saved file.

The batch ceiling is 1,000 messages / 4 Mi UTF-16 units, plus existing editing,
document and history limits. Generators do not promise schema conformance; real
schema findings can appear without altering compatibility output. There is no
persisted generator preset store or global-ID uniqueness guarantee.

**FHIR generation uses a different seed contract:** it changes transport IDs, not
copied demographics/clinical values. See [FHIR workflow](import-fhir-and-generate-hl7.md).
For exact settings/compatibility and limits see [the Stage 16 reference](../stage16-synthetic-generation.md).
