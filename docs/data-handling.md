# Data handling and user responsibility

This is an informational product description, not a new EULA or a compliance
certification. The repository has no established data-handling EULA. Existing
software licensing is unchanged. Any future binding agreement needs its own review.

Use data you are authorized to access and handle it according to your obligations.
Import does not establish lawful authority or verify that records are synthetic.
There is no claim of HIPAA compliance, guaranteed de-identification, complete
clinical fidelity or certified interface conformance.

## What the application does

- **FHIR Patient Workspace:** reads explicitly selected local R4 Bundle files under
  file/path/resource limits. It projects bounded fields in memory, resolves supported
  references locally and never executes transaction requests or fetches references.
  Closing or clearing releases the session. No patient database is created by default.
- **HL7/SQL:** local source snapshots, captured query sources and result journals can
  contain sensitive data in temporary local storage. Their owning documents/results
  release storage after pending work drains. Temporary storage is not an encryption
  or secure-erasure guarantee.
- **Persistence:** explicit HL7 saves and CSV/report exports write user-selected data.
  Preferences and placement metadata persist separately. Explicitly saved scripts,
  searches and SQL definitions can contain sensitive literal text; they are not
  automatically verified or encrypted. Query history and imported FHIR records are
  not automatically saved with layouts.
- **Logging:** ordinary application activity uses fixed/count-only diagnostics rather
  than message contents, patient identifiers or clinical values. Reports/exported
  messages are separate user-requested output, not a promise that exports lack PHI.
- **Networking:** FHIR import and SQL execution are local and have no automatic
  uploads, remote queries, cloud dependency or runtime telemetry. Separate TCP/MLLP
  tools intentionally connect/transmit through their explicit review workflows.
  Trusted C# scripts execute with the user's account permissions and can use file or
  network APIs; they are not a data-isolation sandbox.

## Help integration

**Help → About → Data handling and privacy** now contains a concise factual summary
of these behaviors and user responsibility. This replaces repeated workspace
acknowledgments/banners; it is not a mandatory consent dialog or new contract.
The FHIR workspace keeps meaningful mapping, clinical, validation and resource-limit
feedback. Removing its synthetic-data acknowledgment does not remove import
validation, path restrictions, cancellation, cleanup or explicit network safeguards.

For a future legal/help review, retain the distinction between responsibility
(“Use data you are authorized to access”) and implementation facts above. Do not
turn provenance hints such as Synthea identifiers into a claim of verified safety.
