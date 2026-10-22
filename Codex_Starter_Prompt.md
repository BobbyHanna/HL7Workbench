# Codex starter prompt — Message Workbench

You are implementing Message Workbench described in `HL7_Workbench_Specification.md` in this repository. The filename and `HL7Workbench` technical identifiers are retained for compatibility.

Read the full specification first. This is an independent, local-first C#/.NET desktop application for HL7 v2 inspection, editing, scripting, synthetic test-message generation, MLLP testing, and Synthea/FHIR import. Do not inspect or reuse proprietary third-party binaries, code, assets, or resources. Use only synthetic test fixtures.

**Your assignment right now is Milestone 0 only.**

1. Inspect the repository and report its current state.
2. Recommend the current supported .NET LTS SDK, Avalonia version, test framework, and minimal dependency set. Explain tradeoffs briefly.
3. Create the solution structure, project references, build/test setup, README, and initial architecture decision record.
4. Add a minimal passing smoke test.
5. Run `dotnet build` and `dotnet test`, fix any failures, and report results.
6. Provide a short proposed design and test plan for Milestone 1, **but do not implement Milestone 1 until I approve**.

Constraints: keep core libraries UI-independent; no cloud services, telemetry, external network calls at runtime, real PHI, or unnecessary dependencies. Keep changes understandable for a developer learning C#/.NET. Summarize every new project and why it exists.
