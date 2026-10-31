# Privacy, storage and retention

Use data you are authorized to access and destinations you are authorized to test.
Message Workbench is a local-first development tool, not a clinical decision system,
compliance certification or guarantee of de-identification. No account/cloud service
or automatic upload is required; there is no runtime telemetry. TCP/MLLP intentionally
transmits through explicit workflows, and trusted C# can use your account's permissions.

Open **Help → About Message Workbench… → Data handling and privacy** for the
in-app summary. This page describes storage facts, not a new EULA.

## Storage and retention

Default configuration root is `%LOCALAPPDATA%\HL7Workbench`. The public product name
does not rename that existing technical data identity. Custom configuration roots
can use different paths. Temporary storage is separate and may contain plaintext.

| Data | Location/lifetime | When it changes or is released |
| --- | --- | --- |
| Source HL7/JSON files | User-selected filesystem paths | Opening/importing reads them. Only an explicitly authorized Save/export can write its allowed destination; retain your own originals/backups. |
| Original HL7 snapshots, decoded spools, captured sources and result/report journals | Application-owned temporary local storage; can contain entire messages/clinical values | Owners cancel/drain readers/work before cleanup on close/replacement. Abnormal termination can leave files for bounded later abandoned-file cleanup. No encryption or secure-erasure guarantee. |
| Working/generated HL7 documents | In-session draft/history plus owned snapshots as needed | Save/Save As explicitly persists output. Close prompts protect dirty work; layout does not save/recover document contents. |
| Exported HL7, CSV and reports | Explicitly selected destinations | Persist until the user manages them. May contain sensitive data; application cleanup does not remove these exports. CSV quoting/formula protection is not anonymization. |
| User scripts | `Scripts/` | Explicit Save Script persists name/source; can include sensitive literals. User copies are independent of built-ins. Delete requires the library workflow. |
| Saved searches | `Searches/` | Explicit saves store names/query literals as unencrypted JSON. Searching/loading alone does not autosave history. |
| Saved SQL definitions | `SqlQueries/` | Explicit saves store names/SQL, including literals. Session query history is memory-only and ends at application exit. |
| Extraction definitions | `Extractions/` | Explicit saves retain paths/labels/preferences, not extracted rows or comparison literals. Names/labels are user text; avoid sensitive values there. |
| Imported validation profiles | `ValidationProfiles/` | Explicit import/reimport/remove persists local definitions and bounded provenance metadata. Profile literals/labels may be sensitive. Applied message selections/findings remain session-only. |
| Preferences/layout/endpoint profiles | `editor-preferences.json`, `script-editor-preferences.json`, `workspace-layout.json`, `transport-endpoints.json` | Persist settings, placement or whitelisted endpoint metadata; not clinical contents, scripts, live connections or network authority. |
| Search/extraction/SQL/comparison results and FHIR import | Session state, with owned temp journals where applicable | Tool-specific close/clear/reset rules release captures. Completed SQL results can outlive source closure until their owner closes. Explicit result documents remain independent. |
| Activity Log | Bounded application-session memory | Fixed codes/counts, not payload/path/exception text; ends at application exit. It is not a durable audit trail. |
| Network attempts/receipts | Endpoint plus bounded tool-owned history | Explicit connect/send/listen/start workflows only. Close/stop cancels/drains, but cannot retract bytes already delivered. Receiver storage is outside Workbench control. |
| Clipboard | OS/platform clipboard after explicit copy | Other readers/history/sync are outside application control. Do not assume copied sensitive values stay local to this process. |

Files inherit account/device storage protections; the application does not encrypt
its libraries/temp files. Same-account processes, administrators, backups and security/
indexing software may access them. Imported records are not verified as synthetic.
De-identification reports are Review Required; residual/unassessed content may identify people.

## Review what you will retain or share

1. Prefer [reviewed synthetic fixtures](../../samples/synthetic-only/README.md)
   for learning and reproduction. Never attach real HL7/JSON/dumps to feedback.
2. Before Save/export, inspect the destination, report type, source revision and
   scope. Full-result export can include more rows than the visible page.
3. Review saved script/search/SQL/profile literals and exported content. A safe
   application log does not imply safe arbitrary output or trusted-script side effects.
4. Save valuable work and back up originals/configuration before a deliberate reset,
   upgrade or manual metadata repair. Close tools/documents normally and allow drains.

Expected result: you know which outputs persist and which sessions are temporary.
Canceling a picker avoids publication; deleting/resetting layout metadata does not
erase exports or retract transmissions. Do not indiscriminately delete active
snapshots or the entire settings folder to solve a resource problem.

See [data-handling facts](../data-handling.md), [save/export safety](../save-and-export-safety.md),
[scripting trust](run-and-manage-scripts.md), [network uncertainty](test-with-mllp.md)
and [safe troubleshooting](troubleshooting.md).

## Bulk analysis and result ownership

Unique-value analysis and bulk preview privately capture the complete current
dataset in plaintext temporary storage, including drafts and out-of-scope messages.
Distinct values and staged Find/Replacement literals are bounded session memory;
they are not saved into preferences, layouts or a library. Review closure cancels
and drains work before capture cleanup. Abnormal termination/best-effort cleanup
can leave temporary data; this is not encryption or secure erasure.

Confirmed mutation opens an independent unsaved complete result. Its owner retains
its snapshot until document cleanup; closing the source/review does not remove an
explicitly opened result. Save As persists it only through the existing explicit
workflow. Analysis is read-only with respect to the source, not a no-storage promise.
