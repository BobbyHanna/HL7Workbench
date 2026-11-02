# Preview limitations and safe testing

This invitation-only preview is experimental. Native acceptance is incomplete;
the owner's general clean-VM smoke report is not an individual pass for every feature.
Use it for technical feedback, never clinical decisions or production workflows.

- **Synthetic HL7 only.** No real patient data, production PHI, copied clinical
  records, private FHIR exports or production systems. Import does not verify that
  input is synthetic; generation/de-identification do not guarantee anonymity.
- **Review every C# script.** Trusted scripts are executable code with your account's
  file/network permissions. Worker budgets and cleanup are not an OS security sandbox.
  Cancel cannot undo arbitrary script side effects. Do not run unreviewed code.
- **Isolated transport only.** TCP/MLLP is unencrypted and unauthenticated; no TLS
  protection should be assumed. Use explicit authorized loopback testing. Never
  expose listeners to untrusted networks or use real endpoints/addresses. A socket
  write or test ACK does not establish clinical processing or safe delivery.
- **Plaintext can persist.** Snapshots, captures and journals may contain full input
  in local temporary storage. Saved definitions, logs/excerpts, clipboard history,
  screenshots and exports deserve review. Normal cleanup is best effort; forced
  termination can leave files. There is no encryption or secure-deletion guarantee.
- **Save deliberately.** Use Save As to keep independent outputs. Unsaved work is
  not recovered by restoring layout. Cancel/failure does not authorize editing a
  partial preview or manually changing temporary storage.
- **One instance at a time.** Avoid simultaneous instances for this preview;
  shared preferences/layout writes and native lifecycle behavior need further review.
- **Unsigned Windows build.** SmartScreen/Defender may warn or block launch. Do not
  disable or bypass security protections; stop and report the exact safe category.
  A SHA-256 checksum verifies byte identity, not publisher identity or malware safety.
- **Coverage is partial.** Schema validation, FHIR mapping, generation, editing and
  large-file tools have different limits. Incomplete coverage, truncated presentation
  and stale results must not be treated as complete source or conformance proof.

The application is local-first and does not need an account/cloud service. Feedback
is manual; no automatic crash upload or telemetry service is added. Explicit transport
and arbitrary trusted-script networking remain possible. Do not forward the owner's
application without permission; preserve independent third-party license rights.
See [README](README.md) and [FEEDBACK](FEEDBACK.md).
