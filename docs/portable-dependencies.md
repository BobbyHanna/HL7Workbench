# Portable Windows x64 dependency and license review

Stage 14 local candidate review, 2026-10-22. Actual published dependency manifests:
35 runtime/package entries. No legal clearance or distribution authorization is implied.
The [checkpoint distribution review](beta-distribution-review.md) distinguishes
technical notice-retention findings from unresolved embedded-component applicability,
application terms, unsigned trust and owner/employer approval requirements.

All notice paths below are relative to `ThirdPartyNotices/<Package>/<Version>/` in
the candidate. Every available package-bundled notice and pinned upstream text is
retained. `dependency-licenses.json` records exact notice paths, copyrights, repositories,
versions, package scopes and redistribution-obligation notes.

| Package | Version | Primary license | Notice/redistribution review |
| --- | --- | --- | --- |
| Avalonia.Angle.Windows.Natives | 2.1.27548.20260419 | File: LICENSE (BSD-style ANGLE) | Keep full conditions/disclaimer; no endorsement; additional native terms. |
| Avalonia.AvaloniaEdit | 12.0.0 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Avalonia.Desktop | 12.1.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Avalonia.FreeDesktop.AtSpi | 12.1.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Avalonia.FreeDesktop | 12.1.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Avalonia.HarfBuzz | 12.1.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Avalonia.Native | 12.1.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Avalonia.Remote.Protocol | 12.1.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Avalonia.Skia | 12.1.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Avalonia.Themes.Fluent | 12.1.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Avalonia.Win32 | 12.1.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Avalonia.X11 | 12.1.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Avalonia | 12.1.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| CommunityToolkit.Mvvm | 8.4.0 | MIT | Keep license/copyright and all bundled/upstream notices; review component scope. |
| Dock.Avalonia.Themes.Fluent | 12.1.0.7 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Dock.Avalonia | 12.1.0.7 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Dock.Controls.DeferredContentControl | 12.1.0.7 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Dock.Controls.ProportionalStackPanel | 12.1.0.7 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Dock.Controls.Recycling.Model | 12.1.0.7 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Dock.Controls.Recycling | 12.1.0.7 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Dock.MarkupExtension | 12.1.0.7 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Dock.Model.Mvvm | 12.1.0.7 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Dock.Model | 12.1.0.7 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Dock.Settings | 12.1.0.7 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| HarfBuzzSharp.NativeAssets.Win32 | 8.3.1.3 | MIT | Keep license/copyright and all bundled/upstream notices; review component scope. |
| HarfBuzzSharp | 8.3.1.3 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| MicroCom.Runtime | 0.11.6 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Microsoft.CodeAnalysis.CSharp.Scripting | 5.9.0 | MIT | Keep license/copyright and all bundled/upstream notices; review component scope. |
| Microsoft.CodeAnalysis.CSharp | 5.9.0 | MIT | Keep license/copyright and all bundled/upstream notices; review component scope. |
| Microsoft.CodeAnalysis.Common | 5.9.0 | MIT | Keep license/copyright and all bundled/upstream notices; review component scope. |
| Microsoft.CodeAnalysis.Scripting.Common | 5.9.0 | MIT | Keep license/copyright and all bundled/upstream notices; review component scope. |
| Microsoft.NETCore.App.Runtime.win-x64 | 10.0.12 | MIT | Keep license/copyright and all bundled/upstream notices; review component scope. |
| SkiaSharp.NativeAssets.Win32 | 3.119.4 | MIT | Keep license/copyright and all bundled/upstream notices; review component scope. |
| SkiaSharp | 3.119.4 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |
| Tmds.DBus.Protocol | 0.94.1 | MIT | Keep MIT copyright/permission/disclaimer and any complete upstream notices. |

The top-level MIT declaration does not replace bundled third-party terms. The
ANGLE, native rendering, .NET runtime, Roslyn and Toolkit notice bundles include
component-specific conditions. Broad upstream notices mention licenses/components
that may not apply to every selected Windows binary; confirm applicability and any
source/relinking obligations with a qualified human reviewer before distribution.
No proprietary runtime dependency or paid component was added. Source/asset origin
is not independently established by a package metadata scan.

The owner confirmed rights to redistribute the unchanged approved MW artwork.
Application beta usage terms and distribution authorization remain unapproved.
Build/test packages and non-Windows native packages are outside this candidate.
The complete normal dependency graph remains locked for the solution gate.

See [portable procedure](portable-release.md), [existing dependency inventory](dependencies.md),
[ADR 0019](decisions/0019-portable-beta-readiness.md) and the actual included notice
texts for detail. Pin updates must repeat this review; unknown primary licenses or
missing/changed notice texts fail candidate assembly.
