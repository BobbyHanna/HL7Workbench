# Message Workbench v0.1.0-beta

Personal developer project; no commercial sale or public release. This compiled-ZIP
private preview is for development/evaluation with a few trusted coworkers; access
to the owner's source repository is not included. Support is not guaranteed. See
PRIVATE-PREVIEW.txt. Sharing requires owner permission and compliance with
applicable workplace rules; no employer approval is implied. Message Workbench is a
desktop tool for HL7 v2 inspection/editing, search/extraction, trusted C# scripting, de-identification
aids, synthetic generation, FHIR R4 conversion and controlled TCP/MLLP testing.

## Start

Use Windows 10 or 11 **x64**. Verify the ZIP against its supplied SHA-256, extract
the entire archive, then launch **MessageWorkbench.exe** from the extracted folder.
Keep ScriptHost and all runtime/notice files together. No installer, administrator
rights, SDK or separately installed .NET runtime is required. This beta is unsigned
and may trigger Windows trust warnings. Follow employer IT policy; do not disable
security controls or bypass a block. Ask the owner/IT contact for assistance.

Settings, layout, endpoint profiles and saved definitions use
`%LOCALAPPDATA%\HL7Workbench`. Documents/exports go where you explicitly save them;
temporary snapshots/reports use per-user temporary storage and can survive a crash.
Back up originals and valuable scripts/settings. Do not store data beside the app.

## Use safely

- Use **synthetic/test data**. This beta is not approved for production clinical use,
  HIPAA-certified or independently security-audited. De-identification does not
  guarantee PHI-free output. Do not use identifiable patient data without a separate
  organizational privacy/security approval.
- Temporary content is not encrypted by the app. Review exports and clipboard use;
  OS clipboard history, backups and crash dumps can expose sensitive information.
- Run only trusted scripts. The isolated worker has resource limits but is not a
  security sandbox; scripts can use your file/network permissions.
- MLLP uses plaintext TCP; TLS is unavailable. Send only to explicitly authorized
  test endpoints. Write completion is not receiver acceptance; inspect correlated
  ACKs and uncertain outcomes. Do not automatically resend uncertain messages.
- Load testing requires synthetic data, destination-owner authorization and agreed
  rate/concurrency limits. It can overload a receiver; monitor it and stop on trouble.

Known limitations: beta defects, Windows x64 only, unsigned, no installer/updater,
plaintext temporary storage/MLLP, and no guarantee of clinical correctness. Read
USER-GUIDE.md and RELEASE-NOTES.md. Keep PRIVATE-PREVIEW.txt and every third-party
notice with this folder; the owner's restrictions do not limit third-party rights.

## De-identification reports

After a de-identification preset completes, open **View → De-identification Report**
or choose **View report** in the output document's compact report row. The report is
dockable/floating and follows the selected output document; no expanded report
occupies RAW or Script Editor. It shows transformation/suppression counts and
field-specific review reasons without original sensitive values. Source and output
edits have separate stale labels; the report describes its captured execution.

**Completed — review required** means specific findings need inspection.
**Completed — no flagged findings** means the supported policy flagged none; it does
not certify anonymity, complete de-identification, HIPAA compliance or permission to
disclose. Retained clinical patterns, container bytes and nested metadata can remain
identifying. General limitations stay visible regardless of findings.

Use **Script Editor → Reports / Export CSV** to explicitly export the last successful
run's `deidentification-review` summary and `deidentification-findings` details.
The summary retains its original column order with appended review/omission counts;
details contain fixed structural coordinates/reasons, capped at 1,000 occurrences
with omissions disclosed. Older saved script copies may provide only summary counts;
duplicate the current built-in preset for detailed findings. Review every export.

## Feedback

Find the version under **Help → About Message Workbench**. Report bugs privately to
the person who supplied the approved ZIP, using your organization's approved channel.
Include version, Windows version, steps, expected/actual behavior, display scaling,
and a minimal **synthetic** example. Do not send PHI, private documents, raw exports,
credentials or crash dumps. Include the supplied ZIP checksum/source revision when
available; do not upload the package or repository publicly.
