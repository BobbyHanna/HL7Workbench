# Repository guidance

- Read `docs/PROJECT_CONTEXT.md` for significant work and consult relevant ADRs/API docs.
- Keep Core free of UI and Dock; Workspace UI-independent; isolate Dock types in Desktop. Follow accepted ADRs and avoid unnecessary abstractions or dependencies.
- Preserve lossless HL7 behavior: never silently normalize malformed input; retain original bytes, encodings, delimiters, and terminators. Use Core parser APIs rather than parsing in the UI.
- Source-backed inspection uses Core's shared structural scanner. Keep snapshot-global bytes, message-local bytes/UTF-16 ranges, and editor-window coordinates distinct; never treat bounded previews as complete values.
- Keep original materialized Core APIs and edit/serialization limits intact. Viewer documents own temporary snapshots and bounded decoded caches; release them after pending work drains. Do not decode/render attachments implicitly.
- Abbreviated presentation fragments and omission markers are not authoritative source. Map display intervals explicitly; export original ranges from snapshots. Optional source-backed tools must release sessions on close; persist placement metadata only.
- Use synthetic HL7 in tests/examples. Never commit PHI or log message contents or patient identifiers.
- Use XML documentation for public APIs where it helps callers.
- Explain non-obvious logic, HL7-specific rules, invariants, and lifecycle decisions. Prefer comments that explain why over comments that narrate what each line does.
- Avoid redundant comments and keep existing comments accurate when behavior changes.
- Add relevant tests and run the applicable locked restore, Release build, and test suite. Do not commit or push without explicit approval; avoid unrelated changes.
- Report uncertainty and behavior that still needs manual verification.

## Permanent test retention and temporary cleanup

- Preserve unit, integration, regression and stress tests, reusable synthetic fixtures,
  supporting test infrastructure and documented verification scripts across milestones.
  Keep useful failure diagnostics until reviewed; milestone completion is not a reason
  to delete regression coverage.
- After verification, remove only clearly owned disposable probes, one-off harnesses,
  generated HL7/CSV exports, scratch files and unnecessary intermediate reports created
  for that work. Prefer designated ignored directories for disposable artifacts.
- Never delete pre-existing user files, tracked fixtures or permanent tests as cleanup.
  Avoid broad destructive commands and do not routinely delete ignored bin/, obj/ or
  publish outputs. Preserve uncertain artifacts and report them rather than guessing.
- Inspect Git status and relevant generated/untracked files after verification, and
  report intentionally retained temporary artifacts. Cleanup must not hide failures,
  suppress warnings or discard unexpected changes.
