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
| Avalonia.BuildServices | 11.3.2 | Exclude transitive build telemetry assets | MIT |
| xunit.v3 | 4.0.2 | Test framework | Apache-2.0 |

xUnit supplies Microsoft Testing Platform transitively (MIT); `global.json`
selects that runner for .NET 10. The initially proposed VSTest adapter and test
SDK are omitted after compatibility testing identified incompatible runner wiring.

Package lock files contain the complete transitive inventory, including native
rendering dependencies. NuGet package metadata and bundled notices are the
authoritative license records; review them when updating or distributing builds.
No HL7 dictionary or terminology dependency is included.

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
[xUnit](https://www.nuget.org/packages/xunit.v3/4.0.2).
