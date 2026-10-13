# Repository guidance

- Read `docs/PROJECT_CONTEXT.md` for significant work and consult relevant ADRs/API docs.
- Keep Core free of UI and Dock; Workspace UI-independent; isolate Dock types in Desktop. Follow accepted ADRs and avoid unnecessary abstractions or dependencies.
- Preserve lossless HL7 behavior: never silently normalize malformed input; retain original bytes, encodings, delimiters, and terminators. Use Core parser APIs rather than parsing in the UI.
- Use synthetic HL7 in tests/examples. Never commit PHI or log message contents or patient identifiers.
- Use XML documentation for public APIs where it helps callers.
- Explain non-obvious logic, HL7-specific rules, invariants, and lifecycle decisions. Prefer comments that explain why over comments that narrate what each line does.
- Avoid redundant comments and keep existing comments accurate when behavior changes.
- Add relevant tests and run the applicable locked restore, Release build, and test suite. Do not commit or push without explicit approval; avoid unrelated changes.
- Report uncertainty and behavior that still needs manual verification.
