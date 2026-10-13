# HL7 Developer Workbench — Product & Engineering Specification

**Status:** Initial specification / implementation-ready v0.1  
**Purpose:** Personal, local-first HL7 v2 developer workbench for healthcare interface development and testing.  
**Primary platform:** Windows desktop; cross-platform architecture where practical.  
**Implementation:** C# / .NET (current supported LTS), Avalonia UI, xUnit or equivalent.  
**Audience:** Developer using Codex in VS Code.  
**Development rule:** Implement one milestone at a time, with tests and user review before proceeding.

## 1. Vision and boundaries

Create an independent HL7 v2 message viewer, editor, analysis, scripting, synthetic test-data generator, and MLLP testing tool. The application should be especially useful for InterSystems IRIS / HealthShare interface development. It is not initially intended as a commercial HL7Spy competitor. Use documented HL7 conventions and independently written code. Do not copy or depend on proprietary HL7Spy code, resources, or license-protected functionality.

### Design principles
- **Local-first and offline:** Core features function without an account, cloud service, or internet access.
- **PHI-aware:** No telemetry, remote upload, or external API calls with message contents by default. Never log raw PHI unnecessarily.
- **Lossless where possible:** Preserve raw message bytes, line endings, field separators, unknown segments, Z-segments, and unexpected values during view and save. Any normalization is opt-in.
- **Flexible, not over-strict:** Support nonconformant real-world messages; distinguish parsing from validation.
- **Scriptable from the start:** Both template mutation and message creation from scratch are essential use cases.
- **Reusable libraries:** HL7 parser, generation, scripts, and MLLP transport must not depend on UI classes.
- **Safe defaults:** Synthetic data only in sample files/tests; MLLP load tests require explicit targets and rate limits.

## 2. Target workflows

### A. Inspect and edit
Open one or many HL7 v2 messages from files, navigate between messages, inspect segments and fields with HL7 path coordinates, edit values, compare messages, and save without inadvertent changes.

### B. Template-based generation and de-identification
Load one or more existing messages, clone them, replace identifiers and demographics with synthetic values, preserve patient/encounter relationships across related messages, and export generated messages. A dedicated de-identification workflow must identify high-risk unstructured content (NTE, OBX, Z-segments, attachments) and **never claim full de-identification based only on PID replacement**. Provide explicit warnings, a configurable allowlist/scrub strategy, and a review report. Do not ship a 'guaranteed HIPAA compliant' label.

### C. From-scratch generation
Build synthetic HL7 v2 messages without requiring a template, including configurable ADT, ORM, and ORU scenarios; use a seeded generator and reusable synthetic patient and encounter objects. Support linked longitudinal sequences (e.g., A01 → A08 → A03).

### D. Throughput testing
Generate large batches and optionally transmit them over MLLP to an explicitly configured test endpoint. Measure sent messages, ACKs (AA/AE/AR where applicable), timeouts, retries, throughput, and latency percentiles. Do not silently retry non-idempotent messages. Include stop/cancel and bounded resource usage.

### E. Synthea integration
Import locally generated Synthea FHIR R4 bundles (first choice) and optionally CSV later; map synthetic patients, encounters, conditions, orders, observations, and procedures into selected HL7 v2 templates. Maintain consistent IDs and clinical relationships. Synthea does not directly guarantee correct HL7 v2 event mapping: define and test mapping assumptions. Running Synthea as a managed local subprocess is a later enhancement, not a prerequisite for import.

## 3. Functional requirements

### 3.1 HL7 parser and model (P0)
- Support ER7 HL7 v2 messages and common versions 2.3–2.8.x, starting with general delimiter-driven parsing.
- Detect segment boundaries for CR, LF, and CRLF inputs; preserve original formatting for unmodified messages.
- Parse MSH-1 field separator and MSH-2 encoding characters; handle the special indexing of MSH fields correctly.
- Address segment occurrence, field, repetition, component, and subcomponent (e.g. `PID[1]-3[2].1.1`); define a canonical internal path format and support friendly aliases.
- Preserve empty/trailing fields, repetitions, Z-segments, and unknown message types.
- Decode/encode standard HL7 escapes where supported, without corrupting raw content; surface unsupported escapes rather than silently rewriting them.
- Separate raw representation, parsed structure, and validation diagnostics.
- Unit tests for MSH indexing, delimiters, escape sequences, repeated fields, missing segments, unusual Z-segments, and round-trip integrity.

### 3.2 Desktop viewer/editor (P0)
- Open single-message files and concatenated multi-message files.
- Message list with type, control ID, timestamp, and optional patient ID (masking option).
- Raw syntax-colored message view; structured segment/field tree; selected-field details and editable value.
- Find text, navigate by HL7 path, copy value/path, undo/redo edits, save as, and dirty-file indicators.
- Responsive UI for large files; load/index incrementally rather than holding unnecessary duplicated strings.
- Clear error feedback without losing original data.

### 3.3 Scripting and bulk generation (P0/P1)
- Provide a sandboxed scripting API for reading/writing HL7 paths, cloning messages, generating synthetic values, outputting messages, and iterating in batches.
- Choose an embedded engine only after evaluating maintenance, isolation, execution limits, and API surface. JavaScript is preferred for ergonomics, not mandated.
- Example conceptual API (not fixed syntax): `loadTemplate`, `clone`, `get(path)`, `set(path, value)`, `newMessage`, `synthetic.patient(seed)`, `emit`, `generate(count)`.
- Seeded reproducibility, progress updates, cancellation, configurable output size and message count, and streaming file output.
- Include runnable examples: 1,000 A01 clones with distinct IDs; 100 synthetic patients with encounter lifecycles; randomized ORU values.
- Script permissions must default to no arbitrary filesystem/network/process access; expose narrowly scoped application APIs.

### 3.4 PHI-safe transformation (P1)
- Separate de-identification workflows from ordinary scripting.
- Configurable transformations for identifiers, names, DOB/date shifting, addresses, phone/email, provider identifiers, encounter numbers, free text, and site-specific Z-segments.
- Deterministic token mapping for cross-message referential integrity; avoid embedding original values in outputs or reports.
- Fail closed or flag for manual review when unknown fields, attachments, or unstructured text may contain PHI.
- Generate an audit summary listing transformed field categories, skipped fields, unresolved risks, and configuration used, without disclosing original PHI.
- Explicit warning that automatic de-identification is not a compliance guarantee; require validation before sharing data.

### 3.5 Search, comparison, and analysis (P1)
- Filter messages by HL7 path and value, substring, regex with timeout, message type, and control ID.
- Field-value frequency counts, null/missing analysis, and CSV export.
- Side-by-side structural message diff with optional ignored fields (e.g., MSH-7, MSH-10).
- SQL-like query syntax is a later feature; start with a well-defined filter/query API.

### 3.6 MLLP transport and load test (P1)
- MLLP TCP sender and receiver, with proper framing (`0x0B`, payload, `0x1C 0x0D`).
- Configurable host/port, timeouts, TLS only where endpoint support is available, bounded concurrency, rate limits, and backpressure.
- Parse ACK control IDs and codes, correlate responses to requests, record latency and failure reasons.
- Dashboard: messages sent/second, successes, rejects, timeouts, p50/p95/p99 latency, elapsed time.
- Safety: visible destination, explicit start confirmation for bulk sends, cancel, conservative default rate; test environment warning.

### 3.7 Synthea (P2)
- Import FHIR R4 Bundle JSON from disk; support Synthea-generated bundles as test fixtures.
- Build canonical internal synthetic Patient / Encounter / Observation / Condition / Medication / Procedure objects.
- Generate at least ADT A01/A08/A03 and ORU R01 where source data supports it; document mapping gaps.
- Support deterministic identifiers and coherent encounter references.
- Optional scenario controls: duplicate identities, typos, swapped demographics, intentionally conflicting records, with a separate ground-truth manifest for MPI testing.
- Never assume Synthea's FHIR history maps one-to-one to actual HL7 v2 event timing.

### 3.8 IRIS / HealthShare integration (P3, deferred)
- Investigate safe, supported connection methods and permissions before implementing.
- Initial goal: import/export messages through supported interfaces, not direct writes to production internals.
- Keep any production connectivity read-only by default; no PHI sent outside approved environments.

## 4. Proposed solution architecture

```text
HL7Workbench.sln
  src/
    HL7Workbench.Core/          # HL7 model, parser, serializer, paths
    HL7Workbench.Generation/    # templates, synthetic identities, scenarios
    HL7Workbench.Scripting/     # restricted script runtime + APIs
    HL7Workbench.Transport/     # MLLP client/server, ACKs, metrics
    HL7Workbench.Synthea/       # FHIR import + HL7 mapping
    HL7Workbench.Desktop/       # Avalonia MVVM UI
    HL7Workbench.Cli/           # optional automation CLI, later
  tests/
    HL7Workbench.Core.Tests/
    HL7Workbench.Generation.Tests/
    HL7Workbench.Transport.Tests/
    HL7Workbench.Synthea.Tests/
  samples/
    synthetic-only/
  docs/
    architecture.md
    scripting-api.md
    privacy-and-security.md
```

Use interfaces and dependency injection only where they improve testability. Prefer straightforward implementations over premature abstractions. Pin package versions; record dependency licenses. Do not add paid dependencies without approval.

## 5. Nonfunctional requirements
- **Privacy:** No telemetry or network activity unless initiated by user; no PHI in sample fixtures, crash reports, or default logs.
- **Reliability:** No silent data loss; atomic saves when practical; clear recoverable errors.
- **Performance:** Stream generation; avoid allocating all generated messages in memory. Establish measured baselines on representative hardware before promising throughput numbers.
- **Accessibility:** Keyboard navigation, usable scaling, clear contrast, copyable diagnostic details.
- **Testability:** Unit and integration tests, fixture corpus, deterministic seeded output, parser fuzz/property tests where practical.
- **Portability:** Keep core libraries cross-platform even if initial release is Windows-only.
- **Security:** Treat imported messages and scripts as untrusted; bound input sizes, regex runtime, script execution, file access, and TCP concurrency.

## 6. Milestone plan and acceptance criteria

### Milestone 0 — Foundation
- Create solution, projects, test framework, CI build, README, and architecture decision log.
- Build cleanly on Windows with documented .NET SDK and VS Code setup.
- **Gate:** `dotnet build` and `dotnet test` succeed; no UI feature creep.

### Milestone 1 — Parser and round-trip
- Parse/serialize HL7 v2 ER7 with custom separators, repeated components, and Z-segments.
- Provide path-based read/write API and diagnostics.
- **Gate:** Synthetic fixture tests pass; unmodified messages round-trip byte-for-byte where supported; unsupported cases documented.

### Milestone 2 — Basic desktop viewer
- Open, navigate, inspect, edit, and save HL7 messages.
- **Gate:** User can load a synthetic multi-message file, inspect PID-3 and MSH-10, edit a field, save, and reopen without losing other content.

### Milestone 3 — Scriptable generator (highest-value milestone)
- Add template cloning, from-scratch ADT generation, seeded synthetic demographics, script editor/runner, streaming output, progress, and cancellation.
- **Gate:** Generate 10,000 synthetically populated messages with unique control IDs and consistent patient identifiers; deterministic output with fixed seed; no unbounded memory growth.

### Milestone 4 — Search, diff, and de-identification workflow
- Add field filtering, structural diff, transformation rules, and de-identification review report.
- **Gate:** Can transform a deliberately PHI-like synthetic fixture and correctly flag unsupported free-text/Z-segment risks; no assertion of compliance.

### Milestone 5 — MLLP and load testing
- Implement sender, receiver, ACK handling, rate controls, and metrics.
- **Gate:** Loopback integration tests validate framing, ACK correlation, timeout behavior, and cancellation; no real endpoint required.

### Milestone 6 — Synthea import
- Import local Synthea FHIR R4 fixtures and generate coherent HL7 v2 patient encounter/result sequences.
- **Gate:** Patient/encounter relationships are preserved; mapping assumptions and missing data reported.

### Milestone 7 — Advanced capabilities (backlog)
- SQL-like queries, batch editing, advanced scripting, CSV import, integrated Synthea execution, MPI ground-truth scenario generation, and IRIS connectivity.

## 7. Initial implementation guidance for Codex

1. **Do not build the entire application in one pass.** Begin with Milestone 0 and propose a concise design for Milestone 1.
2. Before adding any dependency, state why it is needed and whether it is open source and permissively licensed.
3. Favor small PR-sized changes with tests, runnable sample commands, and concise change summaries.
4. Keep tests entirely synthetic. Do not use real patient messages, secrets, or the HL7Spy executable as implementation inputs.
5. Ask before making major architectural changes, adding cloud dependencies, or enabling automatic network access.
6. For every milestone, report what works, what is incomplete, how to run it, and the next recommended step.
7. If ambiguity arises in HL7 behavior, write a failing test and document the chosen interpretation rather than guessing silently.

## 8. Explicitly out of scope for first release
- Cloud sync, user accounts, telemetry, or hosted services.
- Full HL7 conformance certification across every version/profile.
- Guaranteed automatic PHI de-identification or HIPAA compliance claims.
- Direct manipulation of live HealthShare production message stores.
- Commercial licensing, payment processing, or plugin marketplace.
- Reproduction of HL7Spy's proprietary source code, assets, or internals.

## 9. Decisions to revisit
- Exact .NET LTS and Avalonia versions at project bootstrap.
- HL7 data dictionary/terminology source and its redistribution license.
- Embedded JavaScript runtime and sandbox strategy.
- Default message file format and handling of MLLP streams versus concatenated ER7 files.
- Which synthetic ADT/ORM/ORU templates to prioritize based on actual interface testing needs.
- Whether Synthea runs externally or is invoked from the desktop app.
