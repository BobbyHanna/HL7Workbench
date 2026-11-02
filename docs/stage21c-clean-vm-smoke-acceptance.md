# Stage 21C — Clean-VM smoke acceptance record

Record prepared **2026-11-02**. Actual VM test date: **not recorded**.

**Owner-reported clean Windows VM smoke test: passed at a general level.**

The owner reports using Candidate 02 on a clean Windows VM, successfully launching
the application and exercising major features that appeared to work. The owner
explicitly did **not** complete the full 68-check native worksheet. This is an
owner attestation, not a newly observed or reproduced native test by Codex.

## Artifact and evidence boundaries

| Item | Evidence / classification |
| --- | --- |
| Candidate | 02; version 0.1.0-beta, intended About Build 8f5c43cc |
| Source | `8f5c43cc568c7777be41305e6eb8a0792e3b5c62` |
| Local archive | `artifacts/stage21b2/8f5c43cc-owner-candidate-02/MessageWorkbench-0.1.0-beta-8f5c43cc-win-x64-AWAITING-OWNER.zip` |
| Archive SHA-256 | `ab71b803e927d3def44090de826968e932c077769dd352afb8f4152e5a819044` |
| Windows VM and successful launch | Explicitly confirmed by owner, general smoke pass |
| Major features appeared to work | Explicit general statement; exact features/actions/results not recorded |
| Windows edition/version/build/patch, architecture | Not recorded; win-x64 is the artifact target, not independently confirmed VM inventory |
| VM product/configuration, resources, profile freshness | Not recorded |
| Test date, duration, user privileges | Not recorded |
| Absence of SDK/runtime/developer software | Not individually recorded; do not infer a complete software inventory from “clean” |
| Hash of the VM copy / About screen observation | Not recorded; tooling verifies the frozen local ZIP, not the unrecorded VM copy |
| Full 68-check worksheet completion | Explicitly not performed |
| Codex native VM execution in Stage 21C | Not performed |

## Automated evidence, separately classified

The [Candidate 02 report](stage21b2-candidate02-refresh.md) records automated
verification on the source revision above: 2,941 regular .NET passes, zero failures,
14 expected skips; 84 portable, two compiler and 12 documentation test passes;
zero-warning/error Release and locked restores; exact ZIP/extraction checks and
published-worker smoke/cancellation tests. These are **historical automated results**,
not new Stage 21C application tests or individual native acceptance passes.
Successful [Actions run 41465370655](https://github.com/BobbyHanna/HL7Workbench/actions/runs/41465370655)
is tied to this revision by the owner's previously supplied GitHub CLI evidence.

Stage 21C independently rechecks frozen bytes, manifest/evidence consistency,
source/resource/notice correspondence and documentation. It does not rebuild or
rerun the application. Worker tests on a developer host are distinct from native
GUI, security, display and lifecycle observations on a clean VM.

## Individual native coverage is still unconfirmed

SQL, C# scripting, TCP/MLLP, FHIR import, bulk editing/publication, source preservation,
DPI, floating windows, clipboard, multi-instance behavior, shutdown and temporary
cleanup are **not individually confirmed** by the phrase “major features.” They
may have been exercised, but no per-action evidence is supplied. Do not label them
failed or say they definitely were not performed; their formal status is unverified.

The original Candidate 02 `NATIVE-ACCEPTANCE.md` remains frozen: **68 definitions,
all 68 Not tested; zero rows newly marked Pass**. A successful launch in an unknown
path/profile does not satisfy compound rows for spaces, Unicode, read-only folders,
unrelated working directory or a verified fresh standard-user environment. This
separate summary adds narrative evidence without rewriting that worksheet.

## Short owner-confirmation section

Record these details in a new, separately approved acceptance addendum if available;
do not change the frozen original evidence during this preparation:

1. Actual test date, Windows edition/build/x64, VM/configuration and account privileges.
2. Confirmation of no separately installed modern .NET/SDK/developer dependencies,
   and how the fresh profile/environment was established.
3. VM ZIP checksum and displayed Version/Build, extraction path/permissions and launch method.
4. Exact features/actions exercised, outcomes and any failures, mapped to worksheet IDs.
5. Specific Avery→Morgan result/source agreement, SQL/script execution/cancellation,
   dirty close, worker shutdown and temporary-storage observations.
6. Which remaining checks must be completed before invitations, and which named gaps
   the owner would explicitly accept for this restricted synthetic-only preview.

General smoke success does not complete native acceptance, approve legal rights,
authorize distribution or change the frozen candidate's status. See the
[owner approval checklist](stage21c-owner-release-approval.md).
