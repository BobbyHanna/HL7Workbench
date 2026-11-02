# Message Workbench — synthetic-only private technical preview

This document is prepared for an eventual owner-approved artifact. Stage 21B.1
verification trees are NON-DISTRIBUTABLE TEST OUTPUTS, not a preview to share.
No new download, release, transfer or distribution approval is implied.

After receiving the exact approved Windows x64 ZIP and a trusted checksum from its
owner, verify SHA-256, extract **all files**, and start **MessageWorkbench.exe**.
Keep the complete **ScriptHost/** and **QueryHost/** directories and all notices.
Do not run inside the ZIP or copy only the executable. No installed SDK/runtime,
Python, SQLite server, Docker, WebGateway or InterSystems is expected for ordinary use.
Use a supported Windows 11 x64 release; other Windows targets require explicit acceptance.

Open **Help → About Message Workbench…** for Version and short Build. Machine-readable
BUILD-PROVENANCE.json identifies the source, build inputs, target and dependency
inventory. SHA256SUMS.txt covers extraction files; it is not a publisher signature.
The approved transfer must also identify the archive checksum and unsigned/signed status.

Follow **USER-GUIDE.md**: File → Generate Messages… creates input without repository
fixtures or internet. Help → Documentation and Script API Reference work locally.
Bulk Apply opens a separate complete Bulk Result tab; Save As is an explicit step.

Use **only synthetic data** and authorized loopback/test systems. This experimental
developer utility is not approved for production or clinical decisions. Validation,
FHIR mapping and de-identification have limits and do not establish clinical conformance
or anonymity. No HIPAA-compliance claim is made.

Temporary snapshots/journals can contain complete plaintext content. Clipboard history
and synchronization are outside application control. Saved definitions and explicit
exports may contain sensitive text. Trusted C# executes with your account's permissions;
worker containment is not a security sandbox. TCP/MLLP has no TLS/authentication and
socket write completion is not application acceptance. Bulk output retains all input
messages, including those outside the mutation scope. No automatic telemetry, update
checks, account registration, uploads or feedback transmission is introduced.

Use **FEEDBACK.md** privately with the person who supplied the approved preview.
Never send PHI, real messages, credentials, endpoint secrets, production filenames,
temporary storage or raw crash dumps; inspect screenshots first. Save desired work
before exiting; unsaved drafts are not recovered by restoring layout.

Retain PRIVATE-PREVIEW.txt, ThirdPartyNotices/ and covered-source/. Third-party rights
are preserved; the MPL source delivery applies to the adapted validation resource,
not the entire application. REDISTRIBUTION-REVIEW.md records decisions requiring human
approval. Automated preparation does not provide legal clearance or authorize transfer.
