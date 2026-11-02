# Stage 21D — Internal tester handoff preparation

Prepared **2026-11-02**. **Technically staged for owner review; NOT APPROVED FOR
DISTRIBUTION or internal software use.** No transfer, upload, tester contact or
company-wide rollout occurred. This copy/documentation task does not approve rights
or supersede any frozen AWAITING-OWNER / NON-DISTRIBUTABLE label.

## Local proposed staging folder and exact files

```text
C:\Dev\HL7Workbench\artifacts\stage21d-internal-preview\
```

The folder is ignored by Git and contains exactly these **10 files**:

| Relative staged file | Purpose |
| --- | --- |
| MessageWorkbench-0.1.0-beta-8f5c43cc-win-x64-AWAITING-OWNER.zip | Unchanged byte-for-byte Candidate 02 copy; original filename retained |
| CANDIDATE-SHA256SUMS.txt | Exact SHA-256 and original archive filename |
| NOT-APPROVED-FOR-DISTRIBUTION.txt | Folder-wide owner-review-only status marker |
| START-HERE.md | Short internal evaluation cover note and links |
| RELEASE-STATUS-AND-DECISIONS.md | Concise technical status, pending human decisions, named-recipient/approval fields |
| TESTER-QUICK-CHECKLIST.md | Eight approachable proposed tasks, not completed acceptance tests |
| Tester-Docs/README.md | Unchanged Stage 21C tester README |
| Tester-Docs/LIMITATIONS.md | Unchanged restrictions, trust/privacy and unsigned-build guidance |
| Tester-Docs/FEEDBACK.md | Unchanged manual privacy-conscious reporting template |
| Tester-Docs/TEST-SCENARIOS.md | Unchanged lightweight scenarios; optional advanced work |

The four tester documents stay together; sibling links work without a repository
checkout. Root cover/checklist/decision-sheet links resolve within the staged folder.
The application ZIP was **copied**, not rebuilt, renamed, extracted or repacked.
No combined outer ZIP, installer or new release was created.

## Candidate identity and frozen evidence

- Candidate 02: **0.1.0-beta / Build 8f5c43cc**, source
  `8f5c43cc568c7777be41305e6eb8a0792e3b5c62`.
- Original remains in `artifacts/stage21b2/8f5c43cc-owner-candidate-02/`.
- Original and copied archive size: **133,101,099 bytes**.
- Original and copied archive SHA-256:

  ```text
  ab71b803e927d3def44090de826968e932c077769dd352afb8f4152e5a819044
  ```

Before copying, verified both frozen evidence inventories against Stage 21C's
record: **42 Candidate 01 files and 49 Candidate 02 files**, with no additions,
deletions or hash changes. Candidate 01's archive still has SHA-256
`ad69f7a74c1a232436325f42ec028574d7648fb985be315e888f1aff5423d05b`.
The original reports, worksheets, checksums, manifests and evidence are unchanged.
No source candidate directory was reused as an output target.

The unchanged copy retains **795 archive entries**, 40 dependency records,
**74 notice files**, three exact covered-source files, runtime assets, offline Help,
provenance and the integrity manifest. All entry hashes/CRC and the 794-file
SHA256SUMS scope match the original frozen inventory. Covered source remains
v251-model.json, NOTICE.md and MPL-2.0.txt under covered-source/. Nothing was
removed to make the review folder simpler.

## Intended scope and unresolved approvals

The user now authorizes preparing this **local proposed folder** for approximately
two named trusted colleagues at the owner's consulting firm. Their actual names
are **not recorded**, and remain blank on the decision sheet. This updates the
earlier assembly-plan stage only; it grants no permission to transfer, contact
colleagues, use internally, roll out company-wide or forward the application.

The cover explicitly prohibits PHI, client data, production systems/use and
unreviewed C# scripts; records unsigned/incomplete-native status and no automatic
telemetry; and preserves recipients' independent third-party license rights.
No legal agreement, employer approval or new application permission is invented.

Carried forward without declaring resolution:

1. Adapted nHapi MPL-2.0 preferred-source-form sufficiency and whether additional
   covered source/generator material is required.
2. MPL notice/license-path clarity: the verbatim NOTICE's repository path versus
   the full license actually adjacent at covered-source/MPL-2.0.txt.
3. Native/third-party redistribution applicability for this exact inventory and
   recipient context, not just primary package declarations.
4. Employer IP assignment, application/artwork/documentation ownership, any client
   contract/disclosure obligations, and consulting-firm internal software/security approval.
5. The owner's explicit authorization for the exact ZIP/hash, named recipients,
   scope, accompanying files and trusted transfer/checksum channel.

The decision sheet also leaves unsigned/security handling, acceptance priorities
or explicitly accepted named gaps, and adequate non-superseding status wording
pending. Internal distribution is **not automatically permitted**. No question
requiring artifact remediation was resolved or silently worked around. If review
requires changing Candidate 02, document it and stop for separate authorization;
do not modify the frozen archive. The detailed
[Stage 21C review](stage21c-redistribution-review.md) and
[owner checklist](stage21c-owner-release-approval.md) remain unchanged.

## Tester route and native gaps

The proposed eight-step checklist covers extraction/launch, generated synthetic
HL7, viewing/Field Inspector/editing/saving, SQL and bulk Search & Replace, optional
trusted C#/schema validation, offline Help and manual feedback. It uses existing
UI labels and links to the copied guide set; no development-tool install is required.
Security blocks are reported, not bypassed. One instance is recommended.

Owner-reported clean-Windows-VM smoke passed only at a general level. All **68**
frozen individual worksheet rows remain **Not tested**; SQL, scripts, bulk publication,
transport, FHIR, DPI and shutdown are not newly asserted passed. No runtime test
or native acceptance was performed in Stage 21D. The new short checklist is not
another formal acceptance plan and contains no premarked passes.

## Verification and Git state

| Check | Result |
| --- | --- |
| Source and copied ZIP checksum/size | Match the exact C2 SHA-256 above / 133,101,099 bytes |
| Original archive/evidence preservation | C1 42 and C2 49 files unchanged; original reports/worksheets unchanged |
| Application ZIP contents | 795 entries and full manifest/CRC pass; all 74 notices and three covered-source files retained |
| Tester copies | Four byte-identical readable UTF-8 files; expected sibling links preserved |
| Staged layout and links | Exactly 10 files; seven readable Markdown documents, 22 local links, zero errors |
| Repository Markdown links/anchors | 171 Markdown files / 1,195 local links including heading anchors; zero errors |
| Source/build/dependency inputs | All 725 tracked hashes unchanged; all nine pre-existing untracked documentation hashes unchanged |
| Protected history | All 2,384 SHA-256 hashes unchanged; zero missing or added |
| Whitespace / git diff --check | Pass |
| Builds/full application tests | **Not run**, as requested for documentation-and-copy-only work |

HEAD remains main at `8f5c43cc568c7777be41305e6eb8a0792e3b5c62`; tracked tree/index
have no modifications. The only new repository document from this task is
**docs/stage21d-internal-preview-preparation.md**, uncommitted/untracked. Nine
pre-existing untracked Stage 21B.2/21C documents are retained without edits.
The staging folder and separate verification evidence/scripts are ignored.
No application source/binary, build tooling, dependency version, existing tester
guide or candidate evidence was modified.

Useful copy/hash/link evidence is retained in artifacts/stage21d-verification/
and the owned artifacts/stage21d-prepare.py helper. No disposable HL7/CSV exports
or application extraction were created; no prior artifacts, ordinary bin/obj,
permanent tests or user files were deleted.

## Readiness and next action

**Technically ready for owner review: yes. Authorized for distribution/internal
use: no.** Owner/appropriate firm reviewers must complete the decision sheet,
identify the two permitted colleagues, resolve rights/IP/disclosure/internal-use
questions, decide remaining acceptance scope and explicitly authorize any transfer.
This folder and report do not supersede the inner archive's status labels.

No commit, push, upload, distribution, colleague contact, approval declaration,
application build or feature work occurred. **Stop for owner review; await explicit
approval before any transfer.**
