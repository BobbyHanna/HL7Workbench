# Build, test and verify a change

Run commands from the repository root in PowerShell. The authoritative inputs are
[global.json](../../global.json), [central build properties](../../Directory.Build.props),
[package versions](../../Directory.Packages.props), project files/committed locks and
[Windows Actions](../../.github/workflows/build.yml). Do not copy a historical
milestone's test totals as current verification.

## SDK and ordinary verification

`global.json` selects .NET SDK **10.0.401**, `latestFeature` roll-forward and no
prerelease SDKs, plus **Microsoft.Testing.Platform**. Projects target `net10.0` with
nullable/implicit usings and package lock generation. The five test projects are
executable xUnit projects; no VSTest adapter is needed. Initial restore requires
NuGet access. Avalonia build telemetry assets are excluded in project configuration.

```powershell
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:AVALONIA_TELEMETRY_OPTOUT = '1'
$env:TESTINGPLATFORM_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
dotnet restore HL7Workbench.sln --locked-mode
dotnet build HL7Workbench.sln --configuration Release --no-restore
dotnet test HL7Workbench.sln --configuration Release --no-build --no-restore
git diff --check
```

These restore/build/test commands match the Actions job on `windows-latest`.
CI also runs the offline compiler tests and both generated-reference checks with
Python (3.11+ standard library; no application runtime dependency). See
[documentation maintenance](documentation-maintenance.md) for those commands.
Run the relevant focused suite before the full regular suite when code changes.
For an independent restore-metadata check, use a separate owned artifacts path:

```powershell
dotnet restore HL7Workbench.sln --locked-mode --artifacts-path artifacts/developer-restore-check
```

This proves that restore configuration, not that the normal output was rebuilt.
Keep normal restore/build outputs consistent. Isolated build/test also needs review
of the worker-copy targets' output-root assumptions; this example only isolates
restore metadata. Do not rewrite locks to make a locked restore pass. Dependency
updates require intentional central-version/lock review.

To launch after a successful normal Release build:

```powershell
dotnet run --project src/HL7Workbench.Desktop --configuration Release --no-build --no-restore
```

Desktop/test build targets deploy separate `ScriptHost/` and `QueryHost/` trees.
Keep those intact. Building the source application is not packaging it or approving
distribution. The older [portable procedure](../portable-release.md) is scoped to
its accepted binaries and separate provenance/license gates.

## Focused and opt-in runs

Use MTP/xUnit arguments after `--`, for example:

```powershell
dotnet test --project tests/HL7Workbench.Desktop.Tests --configuration Release --no-build --no-restore -- --filter-class 'HL7Workbench.Desktop.Tests.SqlQueryToolTests'
```

Do not assume test processes are serialized: the regular solution runner allows
projects to overlap. Desktop and Workspace disable in-process test parallelism
for dispatcher/GC-accounting reasons; other projects retain their defaults.

| Coverage | How it is selected | Interpretation |
| --- | --- | --- |
| Regular solution suite | Ordinary command above, without opt-in flags | Record actual passed/failed/skipped totals and named skip reasons from that run. |
| Large Workspace fixtures | Set `HL7WORKBENCH_STRESS=1`, run the Workspace project, then remove that environment variable | Includes larger 256/512 MiB and other expanded synthetic workloads. Some cases return early/use smaller data outside stress mode; a normal pass does not prove the large branch ran. |
| Explicit SQL performance/resource tests | xUnit `--explicit only` plus the intended class filter | These are excluded from ordinary execution. An output-directory variable alone does not select explicit tests. |
| Windows Job Object checks | Platform-specific tests | Non-Windows skips do not establish equivalent containment elsewhere. |
| Optional rendered diagnostics | Per-test render environment variables | Enable deliberately in an owned ignored directory; they do not replace native acceptance. |

For example, after building, opt into the existing Workspace SQL measurements:

```powershell
dotnet test --project tests/HL7Workbench.Workspace.Tests --configuration Release --no-build --no-restore -- --filter-class '*SqlPerformanceTests' --explicit only
```

`HL7WORKBENCH_SQL_PERF_DIRECTORY` selects where those tests retain measurements.
See [SQL acceptance measurement instructions](../stage15d-sql-final-acceptance.md)
before running expensive worker-cap probes. Do not turn safety limits up for a benchmark.

## Reliable tests and reporting

Await operation completion and, for UI assertions, final model observation/rendering.
Batch/load engine execution can finish before its UI continuation; use the existing
event-based [ObservableCompletion](../../tests/HL7Workbench.Desktop.Tests/ObservableCompletion.cs)
and [controlled observation barrier](../../tests/HL7Workbench.Desktop.Tests/UiObservationBarrier.cs).
For cleanup, await model/document drains before deleting owned directories or
closing the headless dispatcher; see [the SQL race](workspace-lifecycle.md#sql-fixture-cleanup-race).
Do not use arbitrary sleeps/retries to conceal missing completion barriers.

Report exact commands, configuration, counts, opt-ins, warning/error results and
limits. Native clipboard/IME, screen readers, floating windows, monitor recovery,
mixed DPI and OS theme/picker behavior require Windows checks beyond headless tests.
Use the feature's [historical acceptance checklist](../history-index.md) as a starting
point and state what was actually checked now.

Documentation-only work needs link/anchor/reference/example/API-generation and
whitespace checks, not an automatic full-suite rerun. Preserve permanent tests,
fixtures and useful diagnostics; clean only clearly owned disposable probes.
Verify protected private-preview hashes before/after applicable work. Documented
commands are procedures, not evidence that this checkout has passed them; they
do not authorize a commit, package or publication.
