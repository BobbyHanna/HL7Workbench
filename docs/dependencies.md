# Dependency inventory

## Stage 15A/15B/15C query worker (post-beta source milestone)

QueryHost alone adds `SQLitePCLRaw.bundle_e_sqlite3` 3.0.5. Stage 15B adds no package
or version; Workspace references only managed Querying contracts. The fixed lazy
field adapter uses the same e_sqlite3 C ABI because the pinned provider has no module
registration wrapper. Transitive project-reference lock entries were refreshed for
the current beta project versions. No accepted release notice/manifest is rebuilt.
Stage 15C reuses AvaloniaEdit for SQL syntax coloring (a small local lexical
definition), existing virtualized ListBox/grid controls for paged results, and the
same worker-copy target for Desktop/tests. No grid/editor dependency is added.
No accepted portable artifact or release notice inventory is rebuilt. The QueryHost project lock graph
pins `SQLitePCLRaw.config.e_sqlite3`, `SQLitePCLRaw.provider.e_sqlite3` and
`SQLitePCLRaw.core` 3.0.5, and native `SQLite` 3.53.4. Restored nuspecs declare
Apache-2.0 for the four SQLitePCLRaw packages. The SQLite package's bundled
LICENSE.txt states SQLite is public domain and links to sqlite.org/copyright.html.
Preserve required notices in any future distribution; this metadata review is not
legal clearance. No ADO.NET/EF Core or editor/grid package is added.

Only the new QueryHost runtime/test worker folder receives these dependencies.
Querying has no package dependency; existing Desktop/Workspace/Core/Scripting/
Transport graphs are unchanged. Existing accepted beta dependency manifests,
notices, binaries and archives are not regenerated or modified. Future worker
deployment/packaging requires its own reviewed source/dependency inventory.
See [ADR 0020](decisions/0020-isolated-read-only-sql.md).

Stage 14's actual self-contained win-x64 candidate has a separate
[35-entry runtime/license review](portable-dependencies.md), including .NET runtime
10.0.12, complete bundled/pinned upstream notices and outstanding human scope review.
Six packages.win-x64.lock.json files supplement the unchanged normal solution locks.
No library dependency is added beyond the runtime required for self-contained use.
Top-level SPDX declarations do not replace component-specific native/runtime notices.
No legal clearance or distribution authorization is implied by this inventory.

Direct package choices were checked against official NuGet package metadata.
Restore and compilation are the compatibility checks for the complete graph.
All packages below are open source and permissively licensed; no paid components
or optional Avalonia commercial tooling are included.

| Package | Version | Purpose | License |
| --- | --- | --- | --- |
| Microsoft.CodeAnalysis.CSharp.Scripting | 5.9.0 | C# compilation/execution in ScriptHost only | MIT |
| Microsoft.CodeAnalysis.CSharp | 5.9.0 | Compiler-only background symbol binding in Desktop's Script Editor | MIT |
| Avalonia | 12.1.4 | UI framework | MIT |
| Avalonia.Desktop | 12.1.4 | Desktop platform integration | MIT |
| Avalonia.Themes.Fluent | 12.1.4 | Window theme | MIT |
| Avalonia.AvaloniaEdit | 12.0.0 | Raw HL7 viewer and bounded message editor | MIT |
| Avalonia.Headless | 12.1.4 | Actual editor input tests without native windows (tests only) | MIT |
| Avalonia.BuildServices | 11.3.2 | Exclude transitive build telemetry assets | MIT |
| xunit.v3 | 4.0.2 | Test framework | Apache-2.0 |
| Dock.Avalonia | 12.1.0.7 | Docking controls and native hosts | MIT |
| Dock.Model.Mvvm | 12.1.0.7 | Desktop adapter model | MIT |
| Dock.Avalonia.Themes.Fluent | 12.1.0.7 | Docking theme | MIT |

xUnit supplies Microsoft Testing Platform transitively (MIT); `global.json`
selects that runner for .NET 10. The initially proposed VSTest adapter and test
SDK are omitted after compatibility testing identified incompatible runner wiring.

Package lock files contain the complete transitive inventory, including native
rendering dependencies. NuGet package metadata and bundled notices are the
authoritative license records; review them when updating or distributing builds.
No HL7 dictionary or terminology dependency is included.

Stage 5A's ScriptHost lock graph additionally includes Microsoft.CodeAnalysis.Common,
Microsoft.CodeAnalysis.CSharp and Microsoft.CodeAnalysis.Scripting.Common 5.9.0, and
Microsoft.CodeAnalysis.Analyzers 5.9.0-1.26328.17. All five restored nuspecs declare
MIT; preserve notices when distributing. Execution/scripting assemblies deploy in
the worker directory. The final editor refinement also uses CSharp/Common compiler
assemblies in Desktop, with the same pinned version and analyzer dependency. This
is an explicit amendment to the initial worker-only Roslyn loading policy: Desktop
binds symbols without emit/execution; the scripting runtime stays in ScriptHost.
No Workspaces/Features/LSP packages are needed. Core/Workspace/Scripting remain free
of Roslyn references; Scripting itself depends only on Core. No external HL7 library
was added.

Milestone 2 Dock packages require Avalonia >=12.1.3 and include net10.0 assets;
the lock graph retains Avalonia 12.1.4. Dock brings DeferredContentControl,
ProportionalStackPanel, Recycling, Recycling.Model, MarkupExtension, Model, Settings (all 12.1.0.7),
and CommunityToolkit.Mvvm 8.4.0 transitively. Their restored nuspec metadata declares
MIT. No Dock serializer, ReactiveUI, Prism, or DI container is added. AvaloniaEdit
12.0.0 was introduced for the Stage 1 viewer and also supports Stage 4 bounded raw
editing. Headless is confined to Desktop.Tests to verify read-only behavior,
actual editing input, document history and inspection-state regressions without
native hosts.
No TextMate package is needed; highlighting uses Core coordinates.

The restored `.nuspec` metadata was inspected for every resolved package.
Transitive Avalonia packages, MicroCom.Runtime, Tmds.DBus.Protocol, SkiaSharp,
HarfBuzzSharp and their native assets declare MIT. ANGLE's bundled `LICENSE`
contains BSD-style redistribution terms and additional bundled notices.
Transitive xUnit packages/analyzers declare Apache-2.0. Microsoft testing platform,
telemetry/TRX extensions, ApplicationInsights, Bcl.AsyncInterfaces, Win32.Registry,
and System.Security.AccessControl declare MIT. Preserve bundled native notices
when distributing the application. Versions and hashes are in the lock files.

The testing platform includes a telemetry extension transitively. Documented
commands and CI disable it using the explicit testing-platform opt-out, alongside
the .NET CLI opt-out. See [Microsoft's telemetry documentation](https://learn.microsoft.com/dotnet/core/testing/microsoft-testing-platform-telemetry).

Sources: [Avalonia](https://www.nuget.org/packages/Avalonia/12.1.4),
[Desktop](https://www.nuget.org/packages/Avalonia.Desktop/12.1.4),
[Fluent](https://www.nuget.org/packages/Avalonia.Themes.Fluent/12.1.4),
[BuildServices](https://www.nuget.org/packages/Avalonia.BuildServices/11.3.2),
[AvaloniaEdit](https://www.nuget.org/packages/Avalonia.AvaloniaEdit/12.0.0),
[Headless](https://www.nuget.org/packages/Avalonia.Headless/12.1.4),
[xUnit](https://www.nuget.org/packages/xunit.v3/4.0.2).
