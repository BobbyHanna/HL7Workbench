# Message Workbench v0.1.0-beta — release candidate notes

**Local review candidate; not approved for distribution. Unsigned.**
Windows 10/11 x64 portable self-contained ZIP. Extract everything and launch
MessageWorkbench.exe. The separately deployed scripting worker needs no installed
.NET runtime. Source revision and clean-state evidence are in BUILD-PROVENANCE.json;
the adjacent ZIP SHA-256 identifies the exact reviewed archive.

Included capabilities: lossless HL7 v2 inspection/editing, navigation/search,
extraction, trusted C# scripting and de-identification aids, synthetic generation,
FHIR R4 conversion/Generation Report, explicit single/sequential batch MLLP sends,
and bounded controlled load testing. Official name: Message Workbench; approved MW
artwork and existing technical identities/settings paths are preserved.

Stage 14 improves corrupt-layout startup recovery, atomic layout metadata cleanup,
safe save-conflict messages, beta version display, portable worker deployment and
package/runtime/license integrity checks. The user reports the earlier portable
candidate and self-contained scripting worked in a separate work VM; the rebuilt
archive still needs its exact-artifact review. No broader native matrix is implied.

Known limitations: unsigned beta; no installer/updater; plaintext temporary content
and TCP/MLLP; scripts are not sandboxed; de-identification is not a PHI-free guarantee.
Use synthetic data and authorized test endpoints only. No clinical/production
approval, HIPAA certification or independent security audit is claimed. Third-party
notice applicability, owner beta terms, employer security review and separate limited
distribution authorization remain pending. Do not bypass Windows/employer controls.

The accepted de-identification update replaces the expanded inline report with a
dockable **De-identification Report** (View menu or the output's View report button).
It adds structured transformation/suppression/review counts, value-free field paths
and reasons, execution/source attribution and independent source/output stale labels.
Both summary and bounded findings CSVs remain explicitly exportable from Script
Editor for its last successful run. Execution completion, specific findings and
general limitations are shown separately; no flagged findings is not a privacy
certification. Existing transformations, conservative review findings and safeguards
are preserved. The user reports all nine native checks passed for this fix; the
replacement ZIP still requires exact-ZIP VM acceptance and distribution authorization.

See README.md for launch/privacy/MLLP precautions and private bug reporting;
USER-GUIDE.md has details. Preserve all runtime files and third-party notices.
