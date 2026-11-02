# Stage 21B.1 CI hotfix — Windows short-path aliases

Date: **2026-11-02**. Scope: release-tooling path handling and regression coverage.
This is a correction to Stage 21B.1, not a new milestone. No application behavior,
dependency version, candidate payload, provenance schema or release permission changes.
**Local verification does not establish a successful GitHub Actions run. CI confirmation
is pending; no commit or push is authorized/performed.**

## Baseline and root cause

HEAD is main at `933bd6675b6baa66be1a2aa3f48acfad988e783e`. Initial tracked changes
and index were empty. The existing untracked Stage 21B.2 candidate report was retained
unchanged. Candidate 01 and its adjacent evidence were inventoried separately before
the hotfix; all 2,384 protected historical hashes matched their original inventory.

`checked_files()` first resolves its root through the filesystem. On Windows,
resolving an existing 8.3 alias returns the long path. Its traversal consequently
yields long paths, while callers previously computed `path.relative_to(original_root)`
against the unnormalized short-path argument. `Path.relative_to()` checks lexical
components, not filesystem identity. Thus a contained file failed with the reported
long `runneradmin` versus short `RUNNER~1` mismatch before integrity/license assertions
could execute. This was neither an external file nor a reason to suppress containment.

The condition was **reproduced locally with genuine Windows 8.3 aliases** from
GetShortPathNameW. Before the fix, the retained missing/altered-file regression run
through a short temporary parent produced **1 failed test / 5 assertion failures,
including subtests**, because `Incomplete` and `integrity` assertions instead received
the unrelated path-conversion error. Its diagnostic is retained in ignored hotfix
evidence. The complete reported Actions 30-error/15-failure run was not reenacted;
the common cause was demonstrated directly without changing test expectations.

## Precise correction and containment boundaries

[portable_release.py](../build/portable_release.py) adds one helper,
`relative_to_root(path, root)`, which resolves **both existing operands** with
`resolve(strict=True)` and then applies `relative_to()` to the canonical paths.
This gives a filesystem-aware canonical relative name and explicit containment.
Missing inputs remain errors; external paths still raise ValueError. No exception
is broadly caught, no path is accepted by string-prefix matching, and no policy or
manifest is relaxed.

Ten filesystem-relative conversions now use this helper:

- `checked_files()` containment and private-build-path diagnostic naming.
- `copy_publish()` source-relative names.
- `copy_notices()` package-relative names and both packaged/upstream notice references.
- `validate_legacy_tree()` inventory keys and forbidden path-component checks.
- `accepted_binary_provenance()` published-input identity keys.
- `validate_tree()` current-format inventory keys.

The existing symlink/junction checks execute **before canonicalizing traversed
inputs** and remain unchanged. Canonical containment is an additional independent
check, not permission to follow a link into or out of the tree. Root handling has
not acquired a new link policy. Existing CLI output containment and stage input/output
separation already resolve their operands; they remain unchanged. Constructed fixture
paths are compared within their own consistent representation and need no rewrite.

The validators still own archive entry names and all existing byte, checksum,
provenance, license, native-resource and complete-worker assertions. Canonical names
retain actual long filesystem filenames instead of leaking an 8.3 spelling into
inventories. No source bytes, ZIP compression/order/timestamps, license text,
dependency graph or application configuration are rewritten by this fix.

## Regression coverage

[test_portable_paths.py](../build/test_portable_paths.py) adds:

- Eight focused checks for canonical equivalent paths, true external/prefix-sibling
  paths, escaping parent traversal, missing inputs, escaped traversal results,
  manifest traversal rejection, symlink/junction guards, actual Windows short/long
  names in both directions and an actual external junction. Some checks cover
  multiple related cases through subtests.
- A real Windows short-temp-root fixture matrix that reruns **all 14 existing legacy
  and 24 existing current tests**, inheriting their assertions without weakening them.
  This covers missing/changed notices, invalid provenance, altered bytes, incomplete
  worker/runtime/native deployments, deterministic fixture archives, private paths,
  strict licenses, rollback and accepted-binary identity checks in the CI environment
  that previously prevented those assertions from being reached.

The focused file contains **46 cases**: eight checks plus the 38 inherited alias
variants. The complete Actions pattern also retains all 38 original normal-root
cases, giving **84 cases**. Real aliases are requested from the filesystem rather
than fabricated with `~1`. Alias-dependent cases report explicit skips on non-Windows
or volumes without assigned aliases; they do not silently simulate a Windows pass.
The native junction-guard check requires the Path.is_junction API; older Python
versions explicitly skip that check while retaining canonical escape/alias coverage.
The local Windows run exercised actual aliases and junctions, with no such skips.

## Verification

Results are from fresh local commands. No existing test expectation was updated
to obtain a pass. The deliberate pre-fix reproduction is separate from the final
passing regression runs.

| Check | Result |
| --- | --- |
| Pre-fix real alias reproduction | Expected failure: one test, five assertion failures |
| Focused path/alias regression file | 46 passed, 0 failed, 0 skipped |
| Exact Actions portable command | 84 passed, 0 failed, 0 skipped; repeated final run passes in 33.957s |
| Normal locked restore | Pass |
| Locked win-x64 restores, Desktop/ScriptHost/QueryHost | All pass in a separate hotfix artifacts path |
| Release solution build | Pass, 0 warnings, 0 errors |
| Full regular solution | 2,941 passed, 0 failed, 14 expected explicit skips; 2,955 total, 11m 55s 386ms |
| Real-compiler privacy checks | 2 passed, 0 failed, 0 skipped |
| Documentation compiler | 12 passed, 0 failed, 0 skipped |
| Generated Help catalog / Script API consistency | Pass |
| Markdown local links/headings and whitespace | 161 Markdown files / 1,156 local links including anchors, zero errors; whitespace passes |
| Read-only historical portable validation | Four trees pass: 582 / 582 / 586 / 586 files |
| Protected historical SHA-256 inventory | All 2,384 hashes match before and after checks; zero changed/missing/added |
| Frozen Stage 21B.2 candidate 01 | All 42 candidate/evidence files and the pre-existing report are byte-for-byte unchanged |

Commands use repository SDK 10.0.401 and the existing telemetry opt-outs:

```powershell
python -B -m unittest discover -s build -p 'test_portable_paths.py' -v
python -B -m unittest discover -s build -p 'test_portable*.py'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln --configuration Release --no-restore
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
python -B -m unittest discover -s build -p 'test_release_compilation.py'
python -B -m unittest build/test_documentation_catalog.py
python -B build/WriteDocumentationCatalog.py --check
python -B build/WriteScriptApiReference.py --check
git diff --check
```

RID restores additionally use `-r win-x64 --locked-mode`, isolated `--artifacts-path`,
SelfContained=true, RuntimeFrameworkVersion=10.0.12,
NuGetLockFilePath=packages.win-x64.lock.json, PublishScriptHostSeparately=true and
PublishQueryHostSeparately=true. They do not publish or touch candidate 01.

## Changed files, frozen artifact and limits

This hotfix changes only:

1. `build/portable_release.py` — shared canonical relative-path handling.
2. `build/test_portable_paths.py` — permanent filesystem and alias regression matrix.
3. `docs/stage21b1-short-path-ci-hotfix.md` — this correction/verification record.

The pre-existing `docs/stage21b2-private-preview-candidate.md` is still untracked
and is not a hotfix modification. No current documentation catalog or generated
Script API content changes. Useful failure/output/hash evidence remains in the
ignored `artifacts/stage21b1-short-path-hotfix/` directory. Tests clean their own
synthetic temporary fixtures and fake-binary ZIPs; no candidate/release ZIP is
created, rebuilt, replaced or distributed. Ordinary bin/obj and prior artifacts
are preserved. The newly owned isolated RID restore metadata was removed after
successful verification; logs, the deliberate failure diagnostic, test summaries,
frozen inventories and read-only verification helper remain for review.

Candidate 01 remains the previously identified archive:

```text
C:\Dev\HL7Workbench\artifacts\stage21b2\933bd667-owner-candidate-01\MessageWorkbench-0.1.0-beta-933bd667-win-x64-AWAITING-OWNER.zip
SHA-256 ad69f7a74c1a232436325f42ec028574d7648fb985be315e888f1aff5423d05b
```

Its byte identity, adjacent provenance/acceptance/evidence and prior candidate
report are frozen. They are not restamped to describe this later tooling hotfix.
This work does not clear CI, legal, native or sharing gates for that candidate.
**Await review and an actual CI result before declaring the Actions failure fixed.**
No commit, push, upload, release, distribution, dependency update or later milestone
was performed.
