# Authored synthetic R4 fixtures

These examples were authored for HL7 Workbench. They use Synthea-style transaction/
collection packaging, not a claim that the Synthea engine produced them. Every name,
identifier, address and clinical example is fictional test data. The Synthea-system
identifier deliberately demonstrates a provenance hint that anyone can copy; it is
not proof that arbitrary FHIR JSON is safe or free of real PHI.

Import patient-alpha.json for an inpatient history, condition, four observation value
types, an explicitly linked report and contained result, missing/unsupported values
and an unsupported resource. Import the folder for a second same-display-name patient,
an ambulatory encounter, a cross-input observation link and identical shared provider
metadata. Source files must remain unchanged. Export HL7/CSV only to scratch paths.
