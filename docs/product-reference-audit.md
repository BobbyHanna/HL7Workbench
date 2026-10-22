# Product reference and limited provenance audit

Audit date: 2026-10-22. Public product: **Message Workbench**. Baseline:
`738e3d7697fb7b2d2d8e668fe7b02edbddea28f2` (accepted Stage 13B checkpoint).
This audit and the branding changes remain unstaged/uncommitted for native review.

## Scope and method

The requested third-party reference search covers HL7Spy, HL7 Spy, HL7-Spy and
case/separator variations (including underscores, dots, Unicode dashes and spaced
letters). Searches include current tracked and nonignored untracked file contents
and paths, hidden configuration/workflows, comments/XML, tests, fixtures, resources,
build files and metadata: **462 current files searched**, excluding the protected
TEST content. A second byte-level pass decodes UTF-8, UTF-16LE and
UTF-16BE, including binary files, so encoded fixtures are not silently omitted by
Git's text-only search. No current file/directory name matches were found.

All 24 reachable commits and all locally available refs were inspected. Four
`refs/codex/turn-diffs/...` refs point at three additional distinct trees, also
searched. Total: 27 tree roots, 866 distinct historical blobs; 865 blobs read.
Commit messages, historical paths and local branch/tag/ref names were searched.
No matching commit messages, paths or ref names were found. There are no local
tags; main and the locally cached origin refs are the only branch refs. No fetch,
remote mutation, history rewriting or proprietary application inspection occurred.

**Deliberate coverage exclusion:** the protected `*_TEST.hl7` fixture was never
opened, including its one historical blob. Its contents are outside this audit.
Only its SHA-256 was checked, matching
`D254E8BBBFC98C2E377EC9149F2D46415756E0CD96CE8DED0D9FB8548F5EFFA1`.
Ignored build outputs/logs are not project-owned source; their dependency provenance
is assessed through the existing locked dependency inventory, not by deleting them.
The audit covers locally reachable history, not unreachable objects, remote-only
branches, external systems, deleted unreferenced objects or a full forensic analysis.

Reproducible text searches (exclude the protected file, never open it):

```powershell
git ls-files --cached --others --exclude-standard
git for-each-ref --format='%(refname) %(objecttype) %(objectname)'
git log --all --format='%H %B'
# For every commit from git rev-list --all and each additional tree ref:
git grep -n -I -i -E 'hl7[[:space:]_.-]*spy' <object> -- . ':!*_TEST.hl7'
git ls-tree -r --name-only <object>
# Expanded separator/spelling pass uses rg / git grep -P with:
# h[\s\p{Pd}_.]*l[\s\p{Pd}_.]*7[\s\p{Pd}_.]*s[\s\p{Pd}_.]*p[\s\p{Pd}_.]*y
```

Ignored `artifacts/product-branding` retains the exact read-only encoded audit
script, JSON/blob inventory, 115 historical line occurrences, commit-message/ref
inventories and current-tree search evidence. These are diagnostics, not product
assets. All permanent tests and fixtures are retained.

## Current-tree findings and changes

Before cleanup, four named references existed in two project-owned documents:

| Path/location at baseline | Finding and correction |
| --- | --- |
| `Codex_Starter_Prompt.md:5` | Prohibition on proprietary reuse; replaced vendor name with third-party wording, retaining the prohibition. |
| `HL7_Workbench_Specification.md:12` | Competitor comparison plus independent implementation/proprietary reuse rule; removed comparison, retained rule and neutral interface-development scope. |
| `HL7_Workbench_Specification.md:185` | Prohibition on using a proprietary executable as input; generalized without weakening it. |
| `HL7_Workbench_Specification.md:196` | Excluded proprietary source/assets/internals; generalized the vendor reference. |

There were no named references in source, UI, tests, XML, fixtures, package metadata
or artwork discovered by either pass. No clone/inspiration statement was found.
After cleanup, the only retained current named references are **this audit record**:
they identify the requested search and historical findings, not affiliation or
required third-party attribution. No third-party copyright/license notice was
removed. Protected fixtures and artwork were not edited to clean terminology.

The original vocabulary attribution in `src/HL7Workbench.Scripting/SyntheticIdentities.v1.json`
still says it was authored for HL7 Workbench. That historical authoring statement
is preserved as provenance; it is not public branding and has no third-party product
reference. Technical `HL7Workbench.*` names and paths remain deliberately unchanged.

## Reachable history findings

Eight distinct historical blobs match: two specification versions, one starter
prompt and five README versions. Across all roots there are 115 matching lines:
108 in specification/starter trees (four per root) plus seven README occurrences.
All are independent-implementation restrictions, a commercial comparison in the
specification, or README warnings against proprietary material. None is a legal
attribution/license notice or evidence by itself of copied implementation.

The references originated in `4f9be4a3b1c242e5a88a37ab0284b9007569f7a7`
(specification/starter). `10fdb854342f30a24f377ade412338840cc76cfa` added the README
warning. `025a80465d0ed71453fe72d82b69e23b27d6bee3` generalized that README warning;
the specification/starter references persisted through the accepted checkpoint.
The README wording exists in seven commits:
`10fdb854342f30a24f377ade412338840cc76cfa`,
`599b4524744da13c15a818677f5ef667d9fbd0c7`,
`80c56580839498482a92833acdc9671ed8f8c1b6`,
`0c2e1bae21f8e5b8f7463251713a8cfac970169c`,
`3e0f9890a9eeadf2e5d733165309f0554ccc232c`,
`3045bafa0790806817e0bb83932b4b92b8ab0181`,
`c18efc48b7b8c6d2540d0e93dc302d44c2ef163d`.

Each additional tree contains only the same four specification/starter references:

| Tree object | Local refs |
| --- | --- |
| `844c45366b59f088704eca19f3bddd861ba3ba83` | `refs/codex/turn-diffs/captures/1792687684206/db22af86-8f03-478b-b10a-28002c9e2a60/base`; `refs/codex/turn-diffs/checkpoints/b5659d73260ff9e86b764d09652f3211/edc71664c5944f0455af3c8d9ecb7677/1792686506920/3450dd4e-aa8e-493b-bf67-e5841e03f694` |
| `a5ddf2f5f998b1428ff2e49b201e1fe1eac0bc07` | `refs/codex/turn-diffs/checkpoints/0fc2ab515e104d77cd9ec0756acd26de/2abdd9e258166a9aab4cfe3e14a6b06d/1792197666973/b7bc1f84-a7b4-40a5-b3dc-49dc08f6d0b2` |
| `ec95d2042882d7a9a8ad2ac5bbcd888c3a7a4b97` | `refs/codex/turn-diffs/checkpoints/de2da0ad634d7614f878e580d27cf725/c32f20c486f5300d501439867cc3ff6d/1792174502599/38f0b0c3-94bd-4678-a9fd-84955081cbb9` |

Current local branch `main` points at `738e3d7697fb7b2d2d8e668fe7b02edbddea28f2`.
Cached `origin/main` and `origin/HEAD` point at
`e575ebe47921f33b3431d32bf26a5fc87aee4a0a`. No refs or historical objects were changed
by this audit. Historical mentions remain intact; they are now history-only outside
the deliberate audit descriptions here.

## Limited practical provenance review

Production namespaces are all under the retained HL7Workbench Core, Workspace,
Transport, Scripting, ScriptHost and Desktop namespaces. No third-party product
namespace, proprietary HL7 library reference, unexpected checked-in DLL/EXE/archive/
font, copied-source header or unsupported endorsement claim was found. The only
tracked binary artwork is the approved source PNG and its two approved derivatives;
hashes remain exact. Their approval and processing history is documented in
[branding](branding.md), rather than inferred from a string search.

Direct package references/central versions and lock files were inspected against
the existing [dependency inventory](dependencies.md). Intentional Avalonia, Dock,
AvaloniaEdit, Roslyn and test dependencies remain pinned and unchanged, including
their existing third-party notice requirements. Synthetic FHIR examples describe
their project authorship and Synthea-style format; no clinical fixture was replaced.
No obvious suspicious provenance or licensing evidence requiring a stop was found
within this scope. This is not a legal clearance or proof that no code was copied.
The limited search cannot establish original authorship or independently certify
all dependency/asset licenses; distribution still requires the documented notice
review. If contrary evidence appears, preserve it and request human review.

## Complete matching commit inventory

All commits below contain the specification and starter references. README matches
are listed only where present. Paths describe historical contents, not new changes.

| Commit | Matched paths |
| --- | --- |
| `4f9be4a3b1c242e5a88a37ab0284b9007569f7a7` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `10fdb854342f30a24f377ade412338840cc76cfa` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md, README.md |
| `599b4524744da13c15a818677f5ef667d9fbd0c7` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md, README.md |
| `80c56580839498482a92833acdc9671ed8f8c1b6` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md, README.md |
| `0c2e1bae21f8e5b8f7463251713a8cfac970169c` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md, README.md |
| `3e0f9890a9eeadf2e5d733165309f0554ccc232c` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md, README.md |
| `3045bafa0790806817e0bb83932b4b92b8ab0181` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md, README.md |
| `c18efc48b7b8c6d2540d0e93dc302d44c2ef163d` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md, README.md |
| `025a80465d0ed71453fe72d82b69e23b27d6bee3` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `6dfa9f4d9d5c6440273975522fc53cef5b7b2797` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `0497180b3799c5075f9fbc9dd6d778b48e68d279` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `dbcd6284eabb01220046109a1f01f2e94626edcd` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `d7ad3e12ae63d8f42f1a26a743803ac081d5e6eb` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `8808929e844cb925fbdb11635e0b55740e5ec160` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `16ebc6d2a2a9d36ddfb20c766976c18990846bb2` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `ddc18ae63df7ddb31ba55ee35c3c6feffd0d3891` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `5eebc767eae5f241ac4c7a02c4275ae0305b76fa` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `ea897decdbd79d067d13552e972ab456885a61d6` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `867016c3d1eab969c4a1853994eedb95621ad4f0` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `1b04e1849b2ba3c03d135cf324bae28b8af4cba8` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `af717a66608d3277402bd6696cd4f50712c7ecf5` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `e575ebe47921f33b3431d32bf26a5fc87aee4a0a` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `45e7cdbad9a32cdaebb7834ae0026b10914622aa` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
| `738e3d7697fb7b2d2d8e668fe7b02edbddea28f2` | Codex_Starter_Prompt.md, HL7_Workbench_Specification.md |
