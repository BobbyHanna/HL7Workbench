# Message Workbench documentation

These guides describe the current source application. The accepted private-preview
ZIP is an older, separately approved binary baseline; a feature in these guides is
not a claim that it exists in that ZIP. See [installation and version scope](user/installation.md).

## For users

Start with [the user guide](user/index.md) or the
[first synthetic-file walkthrough](user/getting-started.md).

- [Install or run from source](user/installation.md).
- [Find the right analysis workflow](user/index.md#choose-an-analysis-workflow).
- [Navigate and recover the workspace](user/workspace.md).
- [Understand privacy and storage](user/privacy-and-storage.md).
- [Troubleshoot errors and check limits](user/troubleshooting.md).

## Developer references

Start with [the developer index](developer/index.md),
[current architecture](developer/architecture.md), [build/test procedures](developer/build-and-test.md)
and [workspace lifetime contracts](developer/workspace-lifecycle.md). Shared
[limits/storage references](reference/index.md) link the owning definitions.
Existing detailed references remain available at their established paths:

- [Build, test and run commands](../README.md#build-and-run-from-source),
  [repository work rules](../AGENTS.md), [project context](PROJECT_CONTEXT.md)
  and [architecture](architecture.md). Architecture/context contain dated milestone
  records; use current implementation and accepted ADR amendments to resolve old claims.
- [Parser preservation/limits](parser-api.md) and [Core path grammar](hl7-paths.md).
- [Scripting lifecycle/workflows](script-api.md), [generated API reference](script-api-reference.md)
  and [reference maintenance](stage18-script-api-reference.md#machine-readable-catalog-and-maintenance).
- [SQL schema](sql-query.md), [HL7 query functions/output rules](sql-hl7-workflows.md)
  and [SQL enforcement ADR](decisions/0020-isolated-read-only-sql.md).
- [Schema formats, coverage and provenance](stage17-hl7-schema-validation.md),
  [FHIR mapping](fhir-to-hl7v2-mapping.md) and [responsive Desktop tools](responsive-tool-layout.md).
- [Architectural decisions](decisions/), [dependencies](dependencies.md) and
  [data handling](data-handling.md).

## Historical evidence and release material

The [historical index](history-index.md) groups milestone/incident reports and ADRs,
retaining their original paths and dated evidence.
Their old test totals, pending-acceptance statements and Git authorizations describe
those checkpoints. They do not grant permission to release today's source tree.

The [Stage 19A audit](stage19-documentation-audit.md) inventories the corpus and
proposes later developer/Help work. The [Stage 19B verification record](stage19b-user-documentation.md)
records the user-documentation pass and deferred UI wording fixes. The
[Stage 19C record](stage19c-developer-documentation.md) covers developer/shared
documentation and the remaining Stage 19D decisions.

For the specific accepted private preview, use its [recipient README](beta-readme.md),
[guide](beta-user-guide.md), [provenance/build procedure](portable-release.md) and
[distribution review](beta-distribution-review.md). These do not approve a new
package or public distribution. The [original specification](../HL7_Workbench_Specification.md)
and [bootstrap prompt](../Codex_Starter_Prompt.md) are historical planning inputs,
not current feature instructions.

Repository Markdown remains canonical. **Help → Documentation** bundles the curated
user/shared guides with native local search; API links reuse Script API Reference.
Repository-only references remain labelled and are not fetched. See the
[Stage 19D implementation/native checklist](stage19d-offline-documentation-help.md).
