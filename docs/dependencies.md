# Dependency inventory

Direct package choices were checked against official NuGet package metadata.
Restore and compilation are the compatibility checks for the complete graph.
All packages below are open source and permissively licensed; no paid components
or optional Avalonia commercial tooling are included.

| Package | Version | Purpose | License |
| --- | --- | --- | --- |
| Avalonia | 12.1.4 | UI framework | MIT |
| Avalonia.Desktop | 12.1.4 | Desktop platform integration | MIT |
| Avalonia.Themes.Fluent | 12.1.4 | Window theme | MIT |
| Avalonia.AvaloniaEdit | 12.0.0 | Read-only raw message editor | MIT |
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

Milestone 2 Dock packages require Avalonia >=12.1.3 and include net10.0 assets;
the lock graph retains Avalonia 12.1.4. Dock brings DeferredContentControl,
ProportionalStackPanel, Recycling, Recycling.Model, MarkupExtension, Model, Settings (all 12.1.0.7),
and CommunityToolkit.Mvvm 8.4.0 transitively. Their restored nuspec metadata declares
MIT. No Dock serializer, ReactiveUI, Prism, or DI container is added. AvaloniaEdit
12.0.0 is now integrated for the authorized Stage 1 viewer. Headless is confined
to Desktop.Tests to verify actual editor read-only behavior without native hosts.
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
