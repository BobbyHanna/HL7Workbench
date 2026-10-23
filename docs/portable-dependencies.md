# Portable Windows x64 dependency and license review

Private-preview checkpoint review. Actual published dependency manifests:
35 runtime/package entries. No legal clearance or distribution authorization is implied.
The [checkpoint distribution review](beta-distribution-review.md) distinguishes
technical notice-retention/applicability findings from legal certification, unsigned
trust and the remaining owner/artifact/workplace decisions.

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

The top-level MIT declaration does not replace embedded-component conditions.
The private-preview review now distinguishes these concrete cases:

- SkiaSharp/HarfBuzzSharp native bundles retain a historical Netscape GIF notice:
  MPL-1.1 OR GPL-2.0-or-later OR LGPL-2.1-or-later. The pinned Windows source/DEPS/build
  excludes that reader and libgifcodec. GIF decoding uses Apache-2.0 Wuffs 0.3.3,
  statically incorporated into libSkiaSharp.dll; the full Apache terms are retained.
  HarfBuzzSharp's separate font library has no GIF dependency. This resolves the
  identified GIF applicability question without electing GPL/LGPL for the application.
- HarfBuzz's old bundled notice omits current pinned source credits, including
  Matthias Clasen (2022) in compiled/exported paint code. The checkpoint adds the
  exact pinned COPYING without changing the original bundle. Current Skia BSD text,
  Wuffs' Apache-2.0 license and its verbatim copyright/header attribution are also
  supplied. The four supplements bring the preserved 61-file collection to 65 notices.
  No Wuffs NOTICE file is invented, and no new copyleft obligation is asserted.
- The examined Windows defaults exclude FreeType and do not enable HarfBuzz's
  optional FreeType integration. Preserve FTL/GPL alternatives anyway. For builds
  including it, optional GPL wording is not an automatic GPL election; FTL has credit
  requirements.
- Broad Roslyn notices name VsSDK analyzer/build-tool components absent as separate
  packages/files here. This is not proof of a runtime LGPL obligation or proof that
  no vendored component exists. Existing source-access/reverse-engineering text stays.
- Toolkit's broad notices include Common/notification/UnitTests and MVVM material.
  Only the MVVM package ships; no LGPL text was found in its complete notice bundle.
- The .NET runtime retains its complete additional component notices. Non-Windows
  native assets and build/test packages are not standalone shipped entries; some
  managed cross-platform libraries remain. All 35 entries and all notices are kept.

See [the specific evidence, primary sources and remaining questions](beta-distribution-review.md).
See also [the pinned native source/build applicability investigation](native-license-applicability-review.md).
It identifies the historical GIF code as excluded and records the now-implemented
HarfBuzz attribution remedy. The pinned upstream manifest and packaged inventory
record source revisions and source/stored SHA-256 hashes. No application source-
publication requirement or blanket legal clearance is established. The new candidate
has its own checksum; both earlier ZIPs remain unchanged.

The owner confirmed rights to redistribute the unchanged approved MW artwork.
The lengthy proposed application agreement is withdrawn in favor of
[PRIVATE-PREVIEW.txt](PRIVATE-PREVIEW.txt), whose wording the owner has accepted.
Original licenses/notices and all application/dependency versions remain unchanged;
the inventory gains supplementary notices and their exact upstream references.
Private/noncommercial intent does not itself waive third-party conditions.

See [portable procedure](portable-release.md), [existing dependency inventory](dependencies.md),
[ADR 0019](decisions/0019-portable-beta-readiness.md) and the actual included notice
texts for detail. Pin updates must repeat this review; unknown primary licenses or
missing/changed notice texts fail candidate assembly.
