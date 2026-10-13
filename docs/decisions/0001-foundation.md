# ADR 0001: Minimal .NET 10 foundation

Status: Accepted for Milestone 0 by user approval on 2026-10-13.

## Decision

Use .NET 10, Avalonia, and xUnit with only Core, Desktop, and Core.Tests initially.
Use a conventional `.sln` for familiar tooling. Keep package versions centralized
and resolved dependencies locked. `global.json` sets SDK 10.0.401 as the floor
and `latestFeature` roll-forward permits newer .NET 10 feature bands and patches;
it does not select .NET 11 or preview SDKs.

Core is empty until Milestone 1. Desktop references it but provides only a static
window. Test wiring is validated by loading Core and checking that its assembly
references do not include Avalonia. No DI container or MVVM library is needed yet.

Exclude Avalonia.BuildServices assets to suppress its build telemetry task and
also set telemetry opt-out environment variables in CI and documented commands.
The direct BuildServices reference exists only to override the transitive asset
selection; it does not add an application feature.

## Consequences

The foundation is small and accessible to a developer learning C#/.NET.
Future features need separate projects and design decisions as their milestones
start. SDK roll-forward allows machine-to-machine patch differences; CI uses the
latest stable .NET 10 SDK, while NuGet lock files fix the dependency graph.
Restores and hosted CI need network access; application runtime remains offline.

Compatibility validation found xUnit 4.0.2's Microsoft Testing Platform integration
requires the native .NET 10 test runner. Select it in `global.json` and omit the
redundant VSTest adapter and Microsoft.NET.Test.Sdk rather than mixing runners.
