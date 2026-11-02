# Stage 21C — Owner release approval checklist

Prepared **2026-11-02**. **Distribution is not approved.** Approximately two trusted
invitees does not establish internal organizational use or waive third-party rights.
This is a decision record to complete, not a release authorization.

Artifact: Candidate 02, 0.1.0-beta / Build 8f5c43cc; source
`8f5c43cc568c7777be41305e6eb8a0792e3b5c62`.
ZIP SHA-256: `ab71b803e927d3def44090de826968e932c077769dd352afb8f4152e5a819044`.
Do not rebuild/edit/rename either candidate or its evidence to fill this checklist.

Status meanings: **Complete** = specified technical/documentation evidence exists;
**Pending** = accountable decision or work remains; **Blocked** = cannot proceed;
**Not applicable** requires a reason. No gate is currently marked Not applicable.

| Gate | Status | Evidence / remaining action |
| --- | --- | --- |
| Candidate 02 integrity verified | Complete | Exact ZIP, CRC, 795-entry manifest, frozen hashes and existing evidence agree; both candidates preserved. |
| Exact source recorded | Complete | Full source above; no application/build/dependency changes in Stage 21C. |
| CI run verified successful | Complete | Run 41465370655 completed/success at this exact SHA, from owner-verified GitHub CLI evidence recorded in Candidate 02 report. No new remote fetch claimed. |
| Automated results recorded | Complete | Prior Candidate 02: 2,941 regular passes, zero failures, 14 expected skips; 84 portable / 2 compiler / 12 documentation. Stage 21C checks are separately reported. |
| Clean-VM smoke accurately recorded | Complete | Owner-reported general pass; launch confirmed, actual OS/date/account/actions not recorded. See acceptance summary. |
| Remaining native gaps disclosed | Complete | Original 68 definitions untouched; all remain Not tested. No individual SQL/script/TCP/FHIR/bulk/DPI/shutdown pass inferred. |
| Third-party notices technically verified | Complete | Exact 40-record / 74-notice inventory, selected cached assets, declarations and notice hashes checked; not legal clearance. |
| MPL-covered source reviewed | Pending | Exact delivered source/NOTICE/license and embedded correspondence verified. Reviewer must resolve preferred-form/applicability and notice-location questions in redistribution review. |
| Owner/legal redistribution questions resolved | Pending | Confirm application/artwork/employer rights, recipient/organization/jurisdiction context and component terms. No blanket legal clearance. |
| Tester README complete | Complete | Draft docs/private-preview/README.md, actual UI workflows and acceptance limits. |
| Limitations complete | Complete | Synthetic-only, trusted code, transport/storage, unsigned status and one-instance guidance. |
| Feedback template complete | Complete | Manual private process with prohibited data/evidence clearly listed. |
| Suggested scenarios complete | Complete | Twelve lightweight tasks; FHIR/MLLP optional, no developer install required. |
| Recipient materials present or separate handoff requirement documented | Complete (plan only) | Existing ZIP has README/USER-GUIDE/FEEDBACK/PRIVATE-PREVIEW/review/notices/source. Four new tester documents are outside it; separate approved handoff/status clarification is required below. |
| Explicit owner approval to share | Blocked | Not granted. Do not upload, send to invitees, publish or contact testers. |
| Native minimum/risk decision | Pending | Complete selected priority checks or explicitly accept named gaps for this restricted preview. Never convert unperformed checks into Pass. |
| Separate handoff assembly/status note approved | Pending | Owner must approve exact accompanying files, current status wording, recipients and transfer/checksum method before any package is assembled or sent. |

## Required separate handoff, not generated

The frozen ZIP does **not** contain the new four-file tester guide set. It retains
NON-DISTRIBUTABLE/AWAITING-OWNER labels and historical Stage 21B.1 preparation text.
Do not remove, rename or silently reinterpret those files. Before any future transfer,
the owner must approve whether an accompanying explicit permission/status note is
adequate for this exact frozen archive; if a new artifact is required, obtain separate
authorization. Neither technical preparation nor this checklist overrides the labels.

Proposed accompanying material, subject to approval:

- The **unchanged** Candidate 02 ZIP, its independently communicated SHA-256 and
  source/build identification, keeping all notices and covered source inside.
- The four draft files from docs/private-preview/: README.md, LIMITATIONS.md,
  FEEDBACK.md and TEST-SCENARIOS.md. Preserve their sibling links if copied later.
- An owner-authored, approved status/permission note binding the exact ZIP/hash,
  invitation scope, named recipients, no onward application redistribution,
  third-party-rights carveout, synthetic-only restriction and contact/transfer method.
  It must clearly locate covered-source/v251-model.json, NOTICE.md and MPL-2.0.txt.

This is a **file list/plan only**. No new handoff folder, ZIP, upload or transfer is
created by Stage 21C. Preserve third-party source/license rights; “do not forward
the owner's application” must not restrict independently granted MPL/other rights.

## Owner decision record — unfilled

```text
Reviewer / date:
Permitted recipients and organization context:
Rights/legal decisions and evidence:
Remaining native checks completed or explicitly accepted gaps:
Unsigned/security-block decision:
Approved accompanying files and status/permission wording:
Private transfer method and trusted checksum channel:
Explicit permission to assemble a handoff package:
Separate explicit permission to share exact artifact with named recipients:
```

See [smoke acceptance](stage21c-clean-vm-smoke-acceptance.md),
[redistribution review](stage21c-redistribution-review.md),
[tester README](private-preview/README.md) and the
[completion record](stage21c-private-preview-handoff-preparation.md). Stop for owner
review; no automatic next-stage or distribution action follows checklist completion.
