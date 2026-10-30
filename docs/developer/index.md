# Developer documentation

These guides describe the current source architecture, not a promise that later
features exist in the frozen private-preview ZIP. Read [repository guidance](../../AGENTS.md)
before making changes. [Project context](../PROJECT_CONTEXT.md) and the
[history index](../history-index.md) retain dated milestone evidence; the current
implementation and accepted ADR amendments resolve superseded design statements.

## Start a change

1. [Build and test](build-and-test.md) with the selected SDK and locked dependencies.
2. Read the [eight-project/process map](architecture.md).
3. Check [parser invariants](parser-invariants.md) and
   [workspace ownership/lifetimes](workspace-lifecycle.md) before handling content.
4. Choose an existing [extension boundary](extension-points.md); add meaningful
   synthetic regression coverage and review the [documentation ownership map](documentation-maintenance.md#feature-to-documentation-ownership).

## Feature architecture

- [Desktop tools and Dock ownership](desktop-tools.md)
- [Trusted scripting and generated API reference](scripting.md)
- [Isolated read-only SQL](sql.md)
- [Explicit schema validation and profile imports](validation.md)
- [Versioned synthetic generation and FHIR mapping](generation-and-fhir.md)
- [TCP/MLLP, batch and load testing](transport.md)
- [Security and data handling](security-and-data-handling.md)

## Shared references and maintenance

- [Limits and coordinate units](../reference/limits.md)
- [Storage, formats and compatibility](../reference/storage-and-formats.md)
- [Documentation maintenance and authority](documentation-maintenance.md)
- [ADRs](../decisions/), [generated Script API Reference](../script-api-reference.md)
  and [parser API](../parser-api.md)
- [User tasks](../user/index.md) and [documentation home](../index.md)

There is no supported plugin ABI, remote query service or generic scripting sandbox.
The feature guides distinguish existing seams from possible future work. General
offline Help is implemented in Desktop from curated Markdown; see
[Stage 19D implementation and pending native acceptance](../stage19d-offline-documentation-help.md).
