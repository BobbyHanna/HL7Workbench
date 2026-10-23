# Message Workbench v0.1.0-beta — release candidate notes

**Private-preview candidate for owner review. Unsigned; not a public release.**
Windows 10/11 x64 portable self-contained ZIP. Extract everything and launch
MessageWorkbench.exe. The separately deployed scripting worker needs no installed
.NET runtime. BUILD-PROVENANCE.json records the committed packaging revision,
original accepted binary revision/archive and exact input hashes. The adjacent
ZIP SHA-256 identifies this archive. No commercial sale or public repository access
is offered; this personal project is for limited coworker development/evaluation.

Included capabilities: lossless HL7 v2 inspection/editing, navigation/search,
extraction, trusted C# scripting and de-identification aids, synthetic generation,
FHIR R4 conversion/Generation Report, explicit single/sequential batch MLLP sends,
and bounded controlled load testing. Official name: Message Workbench; approved MW
artwork and existing technical identities/settings paths are preserved.

Stage 14 improves corrupt-layout startup recovery, atomic layout metadata cleanup,
safe save-conflict messages, beta version display, portable worker deployment and
package/runtime/license integrity checks. The user reports the accepted application
and scripting passed Windows VM acceptance. This preview reuses those exact binaries,
preserves all original third-party files, and adds private-preview documentation and
pinned attribution supplements. It has a new ZIP checksum for owner review. No
broader native matrix or employer approval is implied.

Known limitations: unsigned beta; no installer/updater; plaintext temporary content
and TCP/MLLP; scripts are not sandboxed; de-identification is not a PHI-free guarantee.
Use synthetic data and authorized test endpoints only. No clinical/production
approval, HIPAA certification or independent security audit is claimed. The concise
PRIVATE-PREVIEW.txt replaces the former proposed application agreement. No support
is guaranteed. Third-party rights and original notices remain intact; pinned HarfBuzz
COPYING, Skia LICENSE and Wuffs license/copyright supplement them. The historical
Netscape GIF reader is excluded from the investigated Windows build; Wuffs 0.3.3 is
Apache-2.0. This technical review is not legal clearance. The owner must approve
this exact ZIP, recipients and transfer method; follow workplace rules. Do not bypass
Windows/employer controls.

The accepted de-identification update replaces the expanded inline report with a
dockable **De-identification Report** (View menu or the output's View report button).
It adds structured transformation/suppression/review counts, value-free field paths
and reasons, execution/source attribution and independent source/output stale labels.
Both summary and bounded findings CSVs remain explicitly exportable from Script
Editor for its last successful run. Execution completion, specific findings and
general limitations are shown separately; no flagged findings is not a privacy
certification. Existing transformations, conservative review findings and safeguards
are preserved. The user reports all nine native checks passed for this fix; the
application binaries are unchanged by byte comparison. The documentation-only
replacement ZIP still requires owner review and permission before private sharing.

See README.md for launch/privacy/MLLP precautions and private bug reporting;
USER-GUIDE.md has details. Preserve all runtime files and third-party notices.
