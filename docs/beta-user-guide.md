# Message Workbench v0.1.0-beta — local candidate guide

This is a local test candidate, not an approved coworker release. Use synthetic data
for acceptance. Native acceptance, owner approval of beta usage terms, human review
of third-party notices and separate distribution authorization are still required.

## Start and preserve your data

Target: Windows 10/11 x64. Extract the entire archive to a local folder, then run
**MessageWorkbench.exe**. Keep ScriptHost and every runtime file beside it. No
installer, administrator rights, SDK or separately installed .NET runtime is needed.
Do not run inside the ZIP. The executable name is a renamed copy of the original
apphost; assembly names and existing data identities stay HL7Workbench.

Application writes do not require access to the extraction folder. Existing settings,
endpoint profiles, scripts, searches, extraction definitions and layout remain under
`%LOCALAPPDATA%\HL7Workbench`. Save documents and exports to locations you explicitly
choose. Moving or replacing the extracted application does not migrate/delete these
per-user files. Back up valuable scripts/settings and originals before testing.

Use File → Open File for HL7, View for inspection/search/extraction/scripting tools,
and Help → About for the public version. Save/Save As retain existing overwrite and
source protections. Ctrl+S saves; Ctrl+F opens Find Messages; document Ctrl+Z/Ctrl+Y
retain their existing text-input and active-document scope. Valid saved dock layouts
remain compatible. Settings/layout errors show safe recovery messages; Reset Layout
recovers hidden/off-screen tools. Corrupt layout metadata is not automatically used
to restore document contents or start network operations.

## Privacy and trust boundaries

There is no account, cloud upload, automatic update, runtime telemetry or automatic
network send/listen. Network actions require explicit endpoint review. Plain TCP/MLLP
is unencrypted and unauthenticated; TLS remains unavailable and fails closed. Use
authorized test destinations. Single-send, sequential batch and controlled load
testing remain separate workflows. Write completion does not prove application
acceptance; inspect correlated ACKs, rejection and uncertainty. Never automatically
resend an uncertain message. Load testing additionally requires synthetic attestation,
exact-target authority, bounded rate/concurrency and a new confirmation per run.

Documents can contain sensitive information. Immutable source snapshots and bounded
script/report/extraction storage use the user's temporary storage; these are **not
encrypted by the application**. Normal close drains operations and releases owned
storage. Abnormal termination can leave plaintext files until bounded abandoned-file
cleanup on a later operation; there is no automatic clinical-content recovery. File
permissions inherit the per-user Windows directories. Other processes under the
same account, administrators, backups and OS indexing/security software may access
files. Use an appropriately secured account/device/storage; secure erasure is not
guaranteed. Do not attach dumps or data files to feedback.

Only run trusted local C# scripts: the separate worker has deadlines, memory bounds,
process-tree cleanup and a private named pipe, but it is **not a security sandbox**.
Trusted scripts can access files/network with your Windows permissions. Script
console output and exception text are discarded/sanitized by the built-in protocol;
a script can still write or transmit data itself. Extraction and script CSV reports
can contain clinical values by design; transmission/load summaries use their own
metadata-only rules. Review each explicit export before sharing. De-identification
aids are conservative development tools, not a guarantee that output is PHI-free.

Copy/paste is explicit. Windows clipboard history/sync and other clipboard readers
are outside the application's control. Avoid copying sensitive values into shared
clipboards. Release builds omit development stack-trace logging and packaged debug
symbols/dump utilities; externally configured OS/runtime crash collection remains
outside application control. Session Activity Log uses fixed codes/counts, not HL7
payloads, patient identifiers or arbitrary exception messages.

## Troubleshooting and feedback

- If launch fails, verify the ZIP's SHA-256 with `Get-FileHash`, extract all files,
  check x64 Windows and keep the candidate in a trusted local folder. Do not download
  missing DLLs from third-party sites or disable security software to make it run.
- Scripting needs the complete ScriptHost folder, including its own native runtime.
  Missing files should cause a safe worker-unavailable result; no fallback downloads.
- Save failures retain working text/history. Check access, disk space, external file
  changes and encoding; review and retry Save As. Canceling a picker does not save.
- Invalid preferences/layouts recover safely. Preserve a copy of metadata before
  deliberately resetting it; do not delete documents or entire per-user folders.
- Long files use snapshots and bounded presentation. Cancel long operations through
  their visible controls; allow active saves and transport cancellation/drain to finish.
- This candidate is unsigned. OS trust/security prompts and actual Windows 10/11,
  DPI/accessibility behavior require native testing; a local smoke check is insufficient.

Report reproducible steps, public version and fixed error/status codes with synthetic
examples only. Include whether Windows is 10/11, x64, theme and display scaling.
Keep `THIRD-PARTY-NOTICES.md`, `dependency-licenses.json`, ThirdPartyNotices/,
BUILD-PROVENANCE.json and SHA256SUMS.txt with the candidate. No redistribution is
authorized by this local test guide.
