# Security and data-handling boundaries

Use the [user storage/retention matrix](../user/privacy-and-storage.md),
[format reference](../reference/storage-and-formats.md) and factual
[data-handling statement](../data-handling.md) together. Local-first is not an
encryption, anonymity, compliance or clinical-conformance guarantee.

## Threat and privilege boundaries

| Boundary | Implemented protection | Do not claim |
| --- | --- | --- |
| HL7/FHIR/profile input | Strict decoding, bounded grammar/import/evaluation, explicit unsupported states, no remote reference fetch | That readable input is clinically correct, synthetic, safe to share or universally conformant. |
| ScriptHost | Bounded versioned pipes, deadlines, Windows process/job containment and cleanup; parent-controlled publication | An OS sandbox, filesystem/network denial, same-user isolation or rollback of arbitrary trusted-code side effects. |
| QueryHost | Closed typed schema, deny-by-default SQLite authorizer, sealed imports, native/process/result budgets | External database access, SQL writes, arbitrary extensions or a universal hostile-process sandbox. |
| Save/export | Explicit destination/replace consent, captured identity/hash, staged flush/validation and atomic supported publication | Portable filesystem compare-and-swap against all external name races or secure erasure. |
| TCP/MLLP | Explicit reviewed destination/selection, strict framing/encoding, correlated ACK outcomes, bounded authorized runs | TLS/authentication, automatic retry/reconnect, receiver safety or clinical acceptance from a socket write. |

Desktop's compiler-only assistance binds known symbols without running scripts;
that does not make executing the same trusted source safe. Imported data and saved
definitions are user-controlled content, not trusted simply because they are local.
The SQL authorizer is an engine boundary, distinct from pipe/resource containment.
Windows Job Object checks have actual Windows tests; equivalent enforced process
memory containment on other platforms is not established.

## Privacy by data flow

Source snapshots, decoded spools, captured inputs and result/report journals can
contain sensitive plaintext. They use owned temporary local storage with leases
and drain-based cleanup. Permissions inherit device/account policy; abnormal
termination and best-effort deletion can leave files. Same-account processes,
administrators, backups and OS security/indexing software are outside an application
secure-erasure guarantee. Do not add patient identifiers to directory names or
log source paths to diagnose ownership bugs.

Preferences/layouts must not restore clinical content or network authority. Explicit
scripts, searches, SQL and imported profiles can contain sensitive literal text.
Read-only analysis is not a no-persistence promise: its captures/journals may use
disk, and explicit CSV/HL7 saves persist user-selected values. Clipboard readers,
history and synchronization are controlled outside Workbench.

Activity Log and ordinary error/status reporting use fixed codes/counts/approved
metadata instead of message contents, patient IDs, paths, query literals or raw
exception/native error text. Bounded control-ID/header display in a feature result
is not permission to log it. Whitelisted transport summaries and clinical CSV
exports have different policies. Trusted C# can still write/transmit content itself;
discarding its console text is not access control.

De-identification presets remain conservative review aids, including unassessed
free text/attachments/Z/container content. Synthetic generator catalogs can coincide
with real people/places. FHIR provenance hints do not verify synthetic origin.
Do not infer anonymity from placeholder names, hashes, local processing or reports.

## Review a change

1. Identify every source, capture, new retained field, temporary file, persisted
   value, clipboard action, worker message and network destination it touches.
2. Bound allocation/parse depth/work/time and output before accepting untrusted
   input. Keep nullable annotations separate from actual protocol validation.
3. Preserve ownership/revision/consent checks across async dialogs and cancellation;
   reject stale publication rather than silently retargeting it.
4. Test failure/cancel/close paths, malformed peer objects, aliases/replacements,
   unsafe encoding and fixed diagnostics using synthetic fixtures only.
5. Review [licenses/resources](../dependencies.md), especially adapted validation
   metadata, before new redistribution. Build success is not legal clearance or
   permission to regenerate accepted artifacts.

Keep existing [protected preview](../portable-release.md) bytes/notices/archives
unchanged. No automatic upload, cloud dependency, account or runtime telemetry is
introduced by current workflows; intentional transport and arbitrary trusted-script
networking remain explicit exceptions to a blanket “no network” description.

Evidence: [ActivityLogTests](../../tests/HL7Workbench.Workspace.Tests/ActivityLogTests.cs),
[FileSaveDestinationTests](../../tests/HL7Workbench.Workspace.Tests/FileSaveDestinationTests.cs),
[query hardening](../../tests/HL7Workbench.Querying.Tests/QueryHardeningTests.cs),
[profile import security](../../tests/HL7Workbench.Core.Tests/Hl7ProfileImportTests.cs),
[de-identification review tests](../../tests/HL7Workbench.Scripting.Tests/DeidentificationReviewTests.cs)
and [release recovery tests](../../tests/HL7Workbench.Desktop.Tests/ReleaseReadinessTests.cs).
