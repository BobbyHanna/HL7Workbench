# Stage 21C — Private preview handoff preparation

Prepared **2026-11-02**. **Preparation only; not released, legally cleared or
approved for distribution.** Approximately two invitation-only testers are intended;
no testers were contacted and no application or handoff bundle was sent or generated.

## 1. Completed work and source baseline

Prepared the requested acceptance summary, exact-artifact technical rights inventory,
four concise tester guides and owner approval checklist. Application/build/dependency
behavior remains unchanged. Both candidates and their existing evidence are immutable.

Initial branch: main; HEAD `8f5c43cc568c7777be41305e6eb8a0792e3b5c62`.
Tracked working tree/index: clean. The only initial untracked file was the completed
`docs/stage21b2-candidate02-refresh.md`; it is preserved byte-for-byte. All **725**
tracked input hashes are recorded for final comparison. Earlier instructions in
milestone records remain historical; the current user request authorizes these
Stage 21C documents, not distribution or application work.

## 2. Candidate and protected-file integrity

| Artifact | Frozen identity / current verification |
| --- | --- |
| Candidate 02 ZIP | `artifacts/stage21b2/8f5c43cc-owner-candidate-02/MessageWorkbench-0.1.0-beta-8f5c43cc-win-x64-AWAITING-OWNER.zip` |
| C2 source / version | 8f5c43cc568c7777be41305e6eb8a0792e3b5c62 / 0.1.0-beta, Build 8f5c43cc |
| C2 SHA-256 | `ab71b803e927d3def44090de826968e932c077769dd352afb8f4152e5a819044` |
| C2 archive and evidence | 795 archive entries; all manifest/frozen hashes, CRC and 49 current evidence-file hashes recorded and preserved |
| C1 SHA-256 | `ad69f7a74c1a232436325f42ec028574d7648fb985be315e888f1aff5423d05b` |
| C1 evidence | All 42 files match the earlier frozen inventory; original report unchanged |
| Protected historical files | All 2,384 SHA-256 hashes match the original inventory; zero changed, missing or added |
| Historical reports/worksheets | Original C1/C2 completion reports and native worksheets are not rewritten |

Existing C2 file/dependency/provenance copies match the actual ZIP. Per-file
verification-tool hashes and orchestration identities match its existing records.
Stage 21C records a new full before/after inventory **outside** both candidate
directories. No existing manifest/checksum/evidence is regenerated or overwritten.

## 3. Clean-machine evidence classification

**Owner-reported clean Windows VM smoke test: passed at a general level.**
Successful launch and the general “major features appeared to work” statement are
the owner's explicit observations. Exact Windows build/VM/date/account privileges,
installed-software inventory, VM-copy checksum/About observation and individual
actions/results are **not recorded**. Do not fill those from build targets or prior
developer-host automation.

The [acceptance summary](stage21c-clean-vm-smoke-acceptance.md) distinguishes that
attestation from automated verification, unconfirmed individual actions and the
explicitly uncompleted full worksheet. SQL, scripting, TCP, FHIR, bulk editing, DPI
and shutdown are not individually declared passed. All original **68** definitions
remain **Not tested**; zero rows are automatically promoted. Unknown actions are
not falsely described as known failures or definitely unperformed.

Prior [Candidate 02 automated evidence](stage21b2-candidate02-refresh.md): 2,941 .NET
passes, zero failures, 14 expected skips; 84 portable / two compiler / 12 documentation
passes; locked restores and zero-warning/error Release; exact ZIP/extraction and
published-worker checks. These are **historical results**, not reruns in Stage 21C.
Successful [CI run 41465370655](https://github.com/BobbyHanna/HL7Workbench/actions/runs/41465370655)
remains tied to the exact source through the owner's recorded CLI confirmation.

## 4. Technical licensing findings and pending rights decisions

The [redistribution review](stage21c-redistribution-review.md) inventories every
selected direct/transitive/runtime component with version, declared license, actual
payload, delivered notice paths, obligation group, evidence and review classification.

- **40 dependency records, 74 notices, 688 selected shipped asset copies** checked
  against exact cached package members. Thirty-nine package content hashes match
  locks; the remaining .NET runtime pack is explicitly pinned to 10.0.12 and matches
  cached pack assets. Raw package hashes and normalized NuGet content hashes are
  checked separately; no new signature-chain certification is claimed.
- SQLite/SQLitePCLRaw, Avalonia/Dock, runtime, Roslyn, Skia/HarfBuzz/Wuffs and broad
  native component texts are distinguished from primary MIT/Apache/file declarations.
  Known historical native applicability evidence is used only for unchanged native bytes.
- Exact adapted JSON, source/change NOTICE and MPL text are delivered and match
  repository/provenance. The JSON matches both Core copies. All **111** cached pinned
  upstream source digests match its provenance map; the upstream license text is
  equivalent after BOM/EOL normalization, not claimed raw-byte-identical.
- Exact embedded Help/API/vocabulary and approved icon/PNG source bytes are verified.
  No standalone fonts or cached font files were identified; app system-font references
  are distinguished from font redistribution. Hidden vendor resource provenance is
  not certified by a limited string/resource probe.

No confirmed missing mandatory notice/source file was found within the examined
scope. **This is not legal clearance.** Owner/legal decisions remain for application/
artwork/employer rights, actual recipient/organization context, native/component
scope and any relevant metadata/standards/jurisdiction interpretation.

Specific potentially blocking MPL questions: preferred Source Code Form and whether
any additional covered generator material is required; and the verbatim NOTICE's
repository-relative license path versus the actual adjacent covered-source/MPL-2.0.txt.
The license is physically present; this is a clarity/sufficiency question, not an
invented absent file or a declaration that all proprietary application code is MPL.
If the reviewer identifies an unsatisfied obligation, block transfer and seek separate
remediation authorization; do not edit frozen Candidate 02.

Two audit-probe assumptions were corrected transparently: signed NuGet normalized
content hashes are not raw ZIP hashes, and runtimepack deps basenames require the
pack's explicit native/lib paths. Independent hash/member checks pass with the
appropriate representations. No candidate integrity discrepancy was found or bypassed.

## 5. Tester documentation inventory

| New draft | Purpose |
| --- | --- |
| [README](private-preview/README.md) | Portable launch, target/build/checksum, generation, viewing/editing, SQL/scripts, validation and offline Help |
| [LIMITATIONS](private-preview/LIMITATIONS.md) | Synthetic-only/no production, trusted code, transport, plaintext storage, unsigned protection and one-instance guidance |
| [FEEDBACK](private-preview/FEEDBACK.md) | Manual private report template; prohibits PHI, real messages, credentials/secrets, sensitive addresses, dumps and snapshots |
| [TEST-SCENARIOS](private-preview/TEST-SCENARIOS.md) | Twelve small exercises, optional local synthetic FHIR/loopback MLLP; actual UI names and observable outcomes |

These guides need no developer installation or repository fixtures. No automatic
telemetry, crash upload, account, feedback service, installer or feature is added.
They disclose incomplete native acceptance and do not ask users to bypass Windows
security protections. They are drafted here, **not inserted into the frozen ZIP**.

## 6. Separate handoff and owner checklist

The existing ZIP includes README.md, USER-GUIDE.md, FEEDBACK.md, PRIVATE-PREVIEW.txt,
REDISTRIBUTION-REVIEW.md, complete notices and covered source. It does not contain
the newly drafted four-file tester set. It still carries non-distribution/awaiting-owner
labels and historical preparation wording. No document silently overrides those labels.

A **separate, explicitly approved accompanying handoff** is required: unchanged ZIP,
trusted SHA/source/build identity, the four sibling-linked guides, and owner-approved
permission/status/source-location instructions for exact recipients and scope.
Only that proposed file list is documented. **No handoff folder or archive is assembled
or distributed**, and neither candidate is renamed or repackaged.

The [owner checklist](stage21c-owner-release-approval.md) contains 15 requested gates
plus explicit native-minimum/risk and separate-handoff approval decisions:
**12 Complete, four Pending, one Blocked; zero Not applicable**. “Complete” means
technical/documentation evidence for that gate, not overall approval. MPL/rights
decisions, acceptance scope and accompanying-package/status approval remain pending;
explicit permission to share is **Blocked / not granted**. Distribution is not approved.

## 7. Documentation-focused verification

| Check | Stage 21C result |
| --- | --- |
| Documentation compiler | 12 passed, zero failed/skipped |
| Generated offline catalog / Script API consistency | Pass; no generated resource changes |
| Markdown links/heading anchors and referenced paths | 170 Markdown files / 1,193 local links including heading anchors; zero errors |
| Candidate identifier/checksum consistency | Pass for source, build and exact C2 archive; C1 identity remains historical |
| Frozen C1/C2 inventories and original reports | 42 / 49 evidence files unchanged, original reports unchanged |
| Protected SHA-256 inventory | 2,384 expected/present; zero changed, missing or added |
| Source/build/dependency inputs | All 725 tracked hashes unchanged; original untracked C2 report unchanged |
| New-file whitespace / git diff --check | Pass |
| .NET build/full tests/portable assembly | Not run in Stage 21C: documentation-only, no runtime/build changes; prior results distinguished above |

Inspection/verification scripts and JSON evidence are retained only in ignored
artifacts/stage21c-handoff-preparation/ plus artifacts/stage21c-inspect.py. They read
both candidates, existing cached packages and source; no package restoration,
application execution, code generation or candidate extraction/rebuild is needed.
No ordinary bin/obj, permanent tests, uncertain user artifacts or prior evidence
is deleted. Initial probe diagnostics are retained for review.

## 8. Exact changed files and Git status

Stage 21C adds these **eight** documentation files only:

1. docs/stage21c-clean-vm-smoke-acceptance.md
2. docs/stage21c-redistribution-review.md
3. docs/private-preview/README.md
4. docs/private-preview/LIMITATIONS.md
5. docs/private-preview/FEEDBACK.md
6. docs/private-preview/TEST-SCENARIOS.md
7. docs/stage21c-owner-release-approval.md
8. docs/stage21c-private-preview-handoff-preparation.md

All are uncommitted/untracked. The pre-existing untracked
docs/stage21b2-candidate02-refresh.md remains unchanged. Git has no tracked
modifications/index changes; HEAD is still the candidate's source commit. No
application source/binary, build tooling, dependency/lock, existing manual,
candidate ZIP/manifest/evidence or native worksheet is modified.

## 9. Recommended next action and stop boundary

Owner/qualified reviewer should review the technical rights ledger and resolve its
specific MPL/notice/native/ownership questions; supply actual VM facts and per-action
evidence; choose and complete priority native checks or explicitly accept named gaps;
then review the exact accompanying documents/status-note/recipient/transfer plan.
Any authorization to assemble a separate handoff must be explicit, and is distinct
from permission to send the exact artifact to named recipients.

**Stop for owner review and explicit distribution approval.** No commit, push,
upload, tester contact, release, installer, new feature or later milestone occurred.
General clean-VM smoke success and a technically complete notice inventory do not
authorize distribution or certify clinical, security or legal fitness.
