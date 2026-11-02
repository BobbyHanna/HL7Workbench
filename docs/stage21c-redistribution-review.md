# Stage 21C — Candidate 02 technical redistribution review

Prepared **2026-11-02**. Scope: the exact frozen **Candidate 02**, Windows x64,
version 0.1.0-beta / Build 8f5c43cc, source
`8f5c43cc568c7777be41305e6eb8a0792e3b5c62`.
ZIP SHA-256: `ab71b803e927d3def44090de826968e932c077769dd352afb8f4152e5a819044`.

**Technical inventory and review preparation, not legal clearance or distribution
approval.** No candidate, notice, license, source, dependency or build tool is changed.
Retain the complete archive. A package name or primary SPDX declaration alone does
not establish every embedded component/resource right.

## 1. Evidence and status key

- **T — Technically verified:** described files, declared terms, hashes or source
  correspondence checked. This does not mean legally approved.
- **O — Requires owner confirmation:** application/artwork/employer rights, actual
  recipients/organization context, scope, channel and approval.
- **L — Requires legal interpretation:** applicability/source-form/native-component/
  jurisdiction or notice questions for an accountable reviewer.
- **B — Missing or potentially blocking:** an identified missing obligation or an
  unresolved question that must block sharing if the reviewer cannot resolve it.

Every component needs owner sign-off before sharing; T rows are not exceptions.
No confirmed absent required notice/source file was found by these checks. The
potential notice-clarity/source-form and rights questions in section 7 remain open.

Primary evidence, in precedence order:

1. **E1:** Candidate ZIP's three published .deps.json graphs, SHA256SUMS.txt,
   dependency-licenses.json, THIRD-PARTY-NOTICES.md and BUILD-PROVENANCE.json.
   Exactly **795 entries**, **40 dependency records**, **74 notice files**.
2. **E2:** All referenced original/pinned notice files under ThirdPartyNotices/,
   including each record's noticeSha256/upstreamNotices. Every file's bytes match
   the ZIP manifest and frozen inventory; nothing is replaced by a web link.
3. **E3:** All eight committed packages.win-x64.lock.json files and exact cached
   NuGet .nuspec/.nupkg data for the listed versions. Declarations match E1;
   **39 package content-hash records** match locks. The remaining .NET runtime pack
   is pinned to 10.0.12 by build configuration/provenance, not a normal lock entry.
4. **E4:** **688 actual shipped runtime/native/localized asset copies** match exact
   members of the corresponding cached packages, using selected RID/locale paths.
   The .NET pack's published basenames are mapped to its explicit win-x64 native
   or net10.0 library directories, not an arbitrary basename search.
5. **E5:** Exact repository resources, project embedding declarations, pinned
   upstream source cache and existing [native applicability investigation](native-license-applicability-review.md).
   The latter is reused only for native bytes/versions that are unchanged.

NuGet has two hash representations: raw cached .nupkg SHA-512 matches its .sha512
file; normalized contentHash in .nupkg.metadata matches the committed lock. Signed
package content hashes differ from whole-ZIP hashes by design; see NuGet's
[metadata format](https://github.com/NuGet/Home/wiki/Nupkg-Metadata-File) and
[reader implementation](https://github.com/NuGet/NuGet.Client/blob/dev/src/NuGet.Core/NuGet.Packaging/PackageArchiveReader.cs).
The first audit probe wrongly equated them; correcting that comparison retained
both independent checks. This is not a new certificate-chain/signature audit.

Detailed asset/member paths, package hashes, direct-project classification and
notice references are retained locally in the ignored
`artifacts/stage21c-handoff-preparation/package-technical-inventory.json`.
This is audit evidence, not a proposed recipient bundle. No NuGet packages or
application files were downloaded, restored, rebuilt or modified for this inventory.

## 2. Component ledger — all 40 selected dependencies

Scopes: **D** Desktop, **S** ScriptHost, **Q** QueryHost. Direct/transitive comes
from project lock records, not naming. Runtime-pack status is explicit. A resource
DLL name can occur in multiple locales/scopes; the copy count includes those actual
files. Exact complete paths are in E1/E4. A metapackage can legitimately have no
own selected payload while its dependencies provide the runtime.

For each row, **N = ThirdPartyNotices/&lt;component&gt;/&lt;version&gt;/**; append the
listed relative notice paths to obtain the exact location inside Candidate 02.
E1–E4 apply to every row. Rights/obligation rules:

- **M:** MIT declaration plus delivered copyright/permission/disclaimer text;
  retain those texts with the binaries. Do not assume unrelated embedded terms vanish.
- **N:** Additional component/native/runtime terms in the retained complete bundles
  and supplements; reconcile their applicable scope, rather than treating MIT as
  a replacement for those texts. See section 3.
- **B:** ANGLE's actual file license: BSD-style source/binary notice retention,
  conditions/disclaimer and non-endorsement provisions; both package and pinned text delivered.
- **A:** Apache-2.0: retain license and applicable NOTICE/attribution, and identify
  modifications if made. Shipped upstream package assets matched unchanged bytes.
  The exact LICENSE/NOTICE pair is delivered for each SQLitePCLRaw record.
  See [Apache section 4](https://www.apache.org/licenses/LICENSE-2.0).
- **S:** Actual SQLite package LICENSE.txt declares public domain. Preserve the
  exact declaration; jurisdiction/title policy is for the reviewer, not inferred
  from the SQLite name. [SQLite's own statement](https://www.sqlite.org/copyright.html)
  distinguishes deliverables from build scripts and organizational legal needs.

| Component / version | Graph / shipped scope | Declared license / rule | Redistributed payload | Delivered notices under N | Review |
| --- | --- | --- | --- | --- | --- |
| Avalonia.Angle.Windows.Natives **2.1.27548.20260419** | transitive; D | file: LICENSE; **B** | `av_libglesv2.dll`; 1 selected copies | `package/LICENSE`; `upstream/AvaloniaUI-angle-LICENSE` | T; O + L |
| Avalonia.AvaloniaEdit **12.0.0** | direct: Desktop; D | expression: MIT; **M** | `AvaloniaEdit.dll`, `AvaloniaEdit.resources.dll`; 2 selected copies | `upstream/AvaloniaUI-AvaloniaEdit-LICENSE` | T; O |
| Avalonia.Desktop **12.1.4** | direct: Desktop; D | expression: MIT; **M** | `Avalonia.Desktop.dll`; 1 selected copies | `upstream/AvaloniaUI-Avalonia-NOTICE.md`; `upstream/AvaloniaUI-Avalonia-licence.md` | T; O |
| Avalonia.FreeDesktop.AtSpi **12.1.4** | transitive; D | expression: MIT; **M** | `Avalonia.FreeDesktop.AtSpi.dll`; 1 selected copies | `upstream/AvaloniaUI-Avalonia-NOTICE.md`; `upstream/AvaloniaUI-Avalonia-licence.md` | T; O |
| Avalonia.FreeDesktop **12.1.4** | transitive; D | expression: MIT; **M** | `Avalonia.FreeDesktop.dll`; 1 selected copies | `upstream/AvaloniaUI-Avalonia-NOTICE.md`; `upstream/AvaloniaUI-Avalonia-licence.md` | T; O |
| Avalonia.HarfBuzz **12.1.4** | transitive; D | expression: MIT; **M** | `Avalonia.HarfBuzz.dll`; 1 selected copies | `upstream/AvaloniaUI-Avalonia-NOTICE.md`; `upstream/AvaloniaUI-Avalonia-licence.md` | T; O |
| Avalonia.Native **12.1.4** | transitive; D | expression: MIT; **M** | `Avalonia.Native.dll`; 1 selected copies | `upstream/AvaloniaUI-Avalonia-NOTICE.md`; `upstream/AvaloniaUI-Avalonia-licence.md` | T; O |
| Avalonia.Remote.Protocol **12.1.4** | transitive; D | expression: MIT; **M** | `Avalonia.Remote.Protocol.dll`; 1 selected copies | `upstream/AvaloniaUI-Avalonia-NOTICE.md`; `upstream/AvaloniaUI-Avalonia-licence.md` | T; O |
| Avalonia.Skia **12.1.4** | transitive; D | expression: MIT; **M** | `Avalonia.Skia.dll`; 1 selected copies | `upstream/AvaloniaUI-Avalonia-NOTICE.md`; `upstream/AvaloniaUI-Avalonia-licence.md` | T; O |
| Avalonia.Themes.Fluent **12.1.4** | direct: Desktop; D | expression: MIT; **M** | `Avalonia.Themes.Fluent.dll`; 1 selected copies | `upstream/AvaloniaUI-Avalonia-NOTICE.md`; `upstream/AvaloniaUI-Avalonia-licence.md` | T; O |
| Avalonia.Win32 **12.1.4** | transitive; D | expression: MIT; **M** | `Avalonia.Win32.Automation.dll`, `Avalonia.Win32.dll`; 2 selected copies | `upstream/AvaloniaUI-Avalonia-NOTICE.md`; `upstream/AvaloniaUI-Avalonia-licence.md` | T; O |
| Avalonia.X11 **12.1.4** | transitive; D | expression: MIT; **M** | `Avalonia.X11.dll`; 1 selected copies | `upstream/AvaloniaUI-Avalonia-NOTICE.md`; `upstream/AvaloniaUI-Avalonia-licence.md` | T; O |
| Avalonia **12.1.4** | direct: Desktop; D | expression: MIT; **M** | `Avalonia.Base.dll`, `Avalonia.Controls.dll`, `Avalonia.DesignerSupport.dll`, `Avalonia.Dialogs.dll`, `Avalonia.Markup.Xaml.dll`, `Avalonia.Markup.dll`, `Avalonia.Metal.dll`, `Avalonia.MicroCom.dll`, `Avalonia.OpenGL.dll`, `Avalonia.Vulkan.dll`, `Avalonia.dll`; 11 selected copies | `upstream/AvaloniaUI-Avalonia-NOTICE.md`; `upstream/AvaloniaUI-Avalonia-licence.md` | T; O |
| CommunityToolkit.Mvvm **8.4.0** | transitive; D | expression: MIT; **M** | `CommunityToolkit.Mvvm.dll`; 1 selected copies | `package/License.md`; `package/ThirdPartyNotices.txt`; `upstream/CommunityToolkit-dotnet-License.md`; `upstream/CommunityToolkit-dotnet-ThirdPartyNotices.txt` | T; O |
| Dock.Avalonia.Themes.Fluent **12.1.0.7** | direct: Desktop; D | expression: MIT; **M** | `Dock.Avalonia.Themes.Fluent.dll`; 1 selected copies | `upstream/wieslawsoltes-Dock-LICENSE.TXT` | T; O |
| Dock.Avalonia **12.1.0.7** | direct: Desktop; D | expression: MIT; **M** | `Dock.Avalonia.dll`; 1 selected copies | `upstream/wieslawsoltes-Dock-LICENSE.TXT` | T; O |
| Dock.Controls.DeferredContentControl **12.1.0.7** | transitive; D | expression: MIT; **M** | `Dock.Controls.DeferredContentControl.dll`; 1 selected copies | `upstream/wieslawsoltes-Dock-LICENSE.TXT` | T; O |
| Dock.Controls.ProportionalStackPanel **12.1.0.7** | transitive; D | expression: MIT; **M** | `Dock.Controls.ProportionalStackPanel.dll`; 1 selected copies | `upstream/wieslawsoltes-Dock-LICENSE.TXT` | T; O |
| Dock.Controls.Recycling.Model **12.1.0.7** | transitive; D | expression: MIT; **M** | `Dock.Controls.Recycling.Model.dll`; 1 selected copies | `upstream/wieslawsoltes-Dock-LICENSE.TXT` | T; O |
| Dock.Controls.Recycling **12.1.0.7** | transitive; D | expression: MIT; **M** | `Dock.Controls.Recycling.dll`; 1 selected copies | `upstream/wieslawsoltes-Dock-LICENSE.TXT` | T; O |
| Dock.MarkupExtension **12.1.0.7** | transitive; D | expression: MIT; **M** | `Dock.MarkupExtension.dll`; 1 selected copies | `upstream/wieslawsoltes-Dock-LICENSE.TXT` | T; O |
| Dock.Model.Mvvm **12.1.0.7** | direct: Desktop; D | expression: MIT; **M** | `Dock.Model.Mvvm.dll`; 1 selected copies | `upstream/wieslawsoltes-Dock-LICENSE.TXT` | T; O |
| Dock.Model **12.1.0.7** | transitive; D | expression: MIT; **M** | `Dock.Model.dll`; 1 selected copies | `upstream/wieslawsoltes-Dock-LICENSE.TXT` | T; O |
| Dock.Settings **12.1.0.7** | transitive; D | expression: MIT; **M** | `Dock.Settings.dll`; 1 selected copies | `upstream/wieslawsoltes-Dock-LICENSE.TXT` | T; O |
| HarfBuzzSharp.NativeAssets.Win32 **8.3.1.3** | transitive; D | expression: MIT; **M+N** | `libHarfBuzzSharp.dll`; 1 selected copies | `package/LICENSE.txt`; `package/THIRD-PARTY-NOTICES.txt`; `upstream/harfbuzz-harfbuzz-COPYING` | T; O + L |
| HarfBuzzSharp **8.3.1.3** | transitive; D | expression: MIT; **M** | `HarfBuzzSharp.dll`; 1 selected copies | `package/LICENSE.txt` | T; O |
| MicroCom.Runtime **0.11.6** | transitive; D | expression: MIT; **M** | `MicroCom.Runtime.dll`; 1 selected copies | `upstream/kekekeks-MicroCom-LICENSE` | T; O |
| Microsoft.CodeAnalysis.CSharp.Scripting **5.9.0** | direct: ScriptHost; S | expression: MIT; **M+N** | `Microsoft.CodeAnalysis.CSharp.Scripting.dll`, `Microsoft.CodeAnalysis.CSharp.Scripting.resources.dll`; 14 selected copies | `package/ThirdPartyNotices.rtf`; `upstream/dotnet-roslyn-License.txt`; `upstream/dotnet-roslyn-THIRD-PARTY-NOTICES.txt` | T; O + L |
| Microsoft.CodeAnalysis.CSharp **5.9.0** | direct: Desktop; D/S | expression: MIT; **M+N** | `Microsoft.CodeAnalysis.CSharp.dll`, `Microsoft.CodeAnalysis.CSharp.resources.dll`; 28 selected copies | `package/ThirdPartyNotices.rtf`; `upstream/dotnet-roslyn-License.txt`; `upstream/dotnet-roslyn-THIRD-PARTY-NOTICES.txt` | T; O + L |
| Microsoft.CodeAnalysis.Common **5.9.0** | transitive; D/S | expression: MIT; **M+N** | `Microsoft.CodeAnalysis.dll`, `Microsoft.CodeAnalysis.resources.dll`; 28 selected copies | `package/ThirdPartyNotices.rtf`; `upstream/dotnet-roslyn-License.txt`; `upstream/dotnet-roslyn-THIRD-PARTY-NOTICES.txt` | T; O + L |
| Microsoft.CodeAnalysis.Scripting.Common **5.9.0** | transitive; S | expression: MIT; **M+N** | `Microsoft.CodeAnalysis.Scripting.dll`, `Microsoft.CodeAnalysis.Scripting.resources.dll`; 14 selected copies | `package/ThirdPartyNotices.rtf`; `upstream/dotnet-roslyn-License.txt`; `upstream/dotnet-roslyn-THIRD-PARTY-NOTICES.txt` | T; O + L |
| Microsoft.NETCore.App.Runtime.win-x64 **10.0.12** | pinned runtime pack; D/Q/S | expression: MIT; **M+N** | `BCL and runtime native assets in all three processes`; 558 selected copies | `package/LICENSE.TXT`; `package/THIRD-PARTY-NOTICES.TXT` | T; O + L |
| SQLite **3.53.4** | transitive; Q | file: LICENSE.txt; **S** | `e_sqlite3.dll`; 1 selected copies | `package/LICENSE.txt` | T; O + L |
| SQLitePCLRaw.bundle_e_sqlite3 **3.0.5** | direct: QueryHost; Q | expression: Apache-2.0; **A** | Meta-package; no own selected runtime file; 0 selected copies | `upstream/ericsink-SQLitePCL.raw-LICENSE.TXT`; `upstream/ericsink-SQLitePCL.raw-NOTICE.TXT` | T; O + L |
| SQLitePCLRaw.config.e_sqlite3 **3.0.5** | transitive; Q | expression: Apache-2.0; **A** | `SQLitePCLRaw.batteries_v2.dll`; 1 selected copies | `upstream/ericsink-SQLitePCL.raw-LICENSE.TXT`; `upstream/ericsink-SQLitePCL.raw-NOTICE.TXT` | T; O + L |
| SQLitePCLRaw.core **3.0.5** | transitive; Q | expression: Apache-2.0; **A** | `SQLitePCLRaw.core.dll`; 1 selected copies | `upstream/ericsink-SQLitePCL.raw-LICENSE.TXT`; `upstream/ericsink-SQLitePCL.raw-NOTICE.TXT` | T; O + L |
| SQLitePCLRaw.provider.e_sqlite3 **3.0.5** | transitive; Q | expression: Apache-2.0; **A** | `SQLitePCLRaw.provider.e_sqlite3.dll`; 1 selected copies | `upstream/ericsink-SQLitePCL.raw-LICENSE.TXT`; `upstream/ericsink-SQLitePCL.raw-NOTICE.TXT` | T; O + L |
| SkiaSharp.NativeAssets.Win32 **3.119.4** | transitive; D | expression: MIT; **M+N** | `libSkiaSharp.dll`; 1 selected copies | `package/LICENSE.txt`; `package/THIRD-PARTY-NOTICES.txt`; `upstream/mono-skia-LICENSE`; `upstream/google-wuffs-LICENSE`; `upstream/google-wuffs-COPYRIGHT.txt` | T; O + L |
| SkiaSharp **3.119.4** | transitive; D | expression: MIT; **M** | `SkiaSharp.dll`; 1 selected copies | `package/LICENSE.txt` | T; O |
| Tmds.DBus.Protocol **0.94.1** | transitive; D | expression: MIT; **M** | `Tmds.DBus.Protocol.dll`; 1 selected copies | `upstream/tmds-Tmds.DBus-COPYING` | T; O |


No Avalonia.Headless, xUnit, testing platform, SDK, build-service telemetry package,
analyzer-only dependency, nHapi runtime/parser or unapproved fixture is in the
selected 40-entry deployment inventory. Avalonia.BuildServices is excluded as
configured; do not confuse development dependencies with redistributed runtime files.
Untrimmed cross-platform managed libraries remain shipped and retain their notices.

## 3. Native and embedded-component terms beyond primary declarations

| Component / version or pin | Shipped code / evidence | Obligations delivered / uncertainty / review |
| --- | --- | --- |
| ANGLE 2.1.27548.20260419; upstream 1c89805903c1482166356d3b950d474973180e61 | av_libglesv2.dll; exact cached native member matches; actual package LICENSE inspected | Package and pinned ANGLE licenses preserve copyright, conditions and disclaimer. No endorsement inferred. T; O/L component scope review. |
| .NET runtime 10.0.12 | 558 copies across D/S/Q: BCL plus coreclr, clrjit, hostfxr, hostpolicy, clrgc/clrgcexp, clretwrc, compression native, diagnostics, msquic and related runtime files | Full package LICENSE.TXT and THIRD-PARTY-NOTICES.TXT retained under runtime N. Primary MIT is not a complete component-rights conclusion. Exact pack/member hashes checked; no independent native rebuild. T + L. |
| SkiaSharp native 3.119.4; package source f568ac94dd768ef9a2f593537cfde2dd0d348ef5 | libSkiaSharp.dll; same package version/bytes as historical native review | Complete original notice bundle plus pinned Skia BSD license and Wuffs license/header retained. Native subcomponents require the bundle's terms. T + L. |
| HarfBuzzSharp native 8.3.1.3; package source 2888c737ad016d584c74525e2d35db5097ea8576; HarfBuzz 8.3.1 pin 2b3631a866b3077d9d675caa4ec9010b342b5a7c | libHarfBuzzSharp.dll; unchanged native member | Original bundle plus pinned harfbuzz-harfbuzz-COPYING supplies current credits, including the previously investigated paint-code attribution. No new gap identified; preserve both. T + L. |
| Wuffs 0.3.3; release-C mirror e3f919ccfe3ef542cfc983a82146070258fb57f8; original 00d5e35865a2f2718f4bb2596adaaa54bd639bbe | Statically incorporated in Skia's examined Windows GIF decoder, not a separate DLL | Apache-2.0 text and verbatim header supplement included under Skia native N. The existing investigation found no separate upstream Wuffs NOTICE; no invented one is supplied. T + L. |
| Historical Netscape GIF / FreeType terms in broad native bundles | Existing pinned Windows source/build investigation excludes the old GIF implementation and optional FreeType paths for these same native bytes | Keep the full historical notices anyway. Do not infer GPL/whole-application source duties merely from text in a broad bundle; do not generalize exclusions to other platforms/versions. T for unchanged-byte linkage; L for applicability. |
| Roslyn 5.9.0 components / broad vendor notices | Compiler libraries in D/S; scripting execution libraries only S; localized resource copies included | All three referenced texts per record retained. Broad VsSDK/test/build-component references are not proof those binaries ship, nor proof every vendored component is absent. No Workspaces/Features dependency introduced. T + L. |
| SQLite 3.53.4 and SQLitePCLRaw 3.0.5 | QueryHost/e_sqlite3.dll, core/provider/batteries assemblies; bundle metapackage contributes graph rather than another DLL | Exact native public-domain declaration; pinned Apache LICENSE/NOTICE includes SourceGear/Zumero and other included attribution. No SQLite server/ADO.NET component is assumed. T; O/L public-domain/recipient context. |

The original [native review](native-license-applicability-review.md) explains its
source/build evidence and limitations; strings alone were not an exclusion proof.
C2's native files match cached package members and the unchanged native entries
in the C1/C2 comparison. This audit does not independently rebuild Skia/.NET or
claim a complete legal audit of every vendored line. All full bundled texts remain
available to the reviewer and eventual authorized recipients.

## 4. Adapted nHapi metadata — exact source and notices

| Item | Verified evidence |
| --- | --- |
| Covered material | covered-source/v251-model.json, adapted structural declarations; no nHapi runtime/parser DLL |
| Upstream revision | 9027797b1fd1faebcffd743ac11324499145f29d |
| JSON SHA-256 | `99af17a10e7f5ff227121c34cd2bccb1cd875c8156e588f84efc4b6fcdacdc4e` |
| Source/change NOTICE SHA-256 | `ce23986d3d4e04aa2ace65f478db6a7a4304f5b4a97140f2af7e874947c8281e` |
| Delivered MPL text SHA-256 | `2684de17300e0a434686f1ec7f8af6045207a4b457a3fe04b2b9ce655e7c5d50` |
| Correspondence | Exact delivered JSON occurs in both shipped Core assemblies and matches the repository input/provenance |
| Source provenance | JSON source/sourceSha256 fields identify 111 pinned upstream files; all 111 cached source digests match; cached revision.txt agrees |
| License indication | JSON license field is MPL-2.0; adjacent NOTICE identifies the adapted resource, contributors, source, changes and license |
| Upstream license text | Same text after UTF-8 BOM/EOL normalization as the pinned cached LICENSE; raw upstream SHA differs because formatting differs, not a silent claim of identical upstream bytes |

The [source/change NOTICE](../src/HL7Workbench.Core/ValidationCatalog/NOTICE.md)
records translation to JSON, slot-key disambiguation, timestamp aliases, ORM choice
representation and variable-length handling. The 111 consumed source files were
also scanned for copyright/MPL notice keywords: none were found in that subset;
no missing original per-file notice was thereby identified. This limited check
is not an upstream project-wide rights/standards determination. Public source
fetching was unavailable during this audit; the digest-verified pinned cache was
used without regenerating anything.

MPL sections 3.1–3.4 address covered source availability, notice retention and
larger works. The same-directory readable adapted JSON, NOTICE and full MPL text
are already delivered, not merely offered by upstream URL. A recipient need not
obtain the independent application's entire source to read this covered material.
The independent engine/UI and the MPL resource are distinct source boundaries;
containing an embedded resource does not automatically classify all application
source as MPL. Confirm the actual scope/preferred form with a qualified reviewer.
See [MPL terms](https://www.mozilla.org/en-US/MPL/2.0/) and
[Mozilla's file-level/larger-work explanation](https://www.mozilla.org/en-US/MPL/2.0/FAQ/).

**Notice-location ambiguity:** the verbatim NOTICE refers to the repository path
build/third-party/nHapi-MPL-2.0-LICENSE.txt, which is not a ZIP path. The full text
is actually next to it as **covered-source/MPL-2.0.txt**, with an explicit JSON
license field and packaged README source guidance. This is not an absent license
file, but clarity/sufficiency needs review. New draft recipient instructions name
the actual shipped paths. Do not rewrite the frozen NOTICE to resolve this.

Owner/legal decisions remain whether this readable adapted JSON and surrounding
notices constitute the appropriate preferred Source Code Form for this covered
resource, whether any additional covered generator/source material is required,
and whether the directory notice/source-location wording adequately informs the
actual recipients. The optional [generator](../build/update-validation-catalog.py)
is not invoked or included in the ZIP; absence of the entire upstream parser is
not automatically a source-delivery defect. Standards prose and complete HL7
specifications are not bundled; do not infer standards/trademark or clinical approval.
If the reviewer identifies additional required source/notice material, that becomes
a **release blocker**, with separate remediation authorization required.

## 5. Application resources, fonts, artwork, Help and examples

| Resource / rights basis | Evidence and delivery | Uncertainty / review |
| --- | --- | --- |
| MW artwork / owner-recorded permission | [Branding record](branding.md) records prior owner permission and approved original/derivatives. Exact workbench-icon.png (SHA 5f2e224b01adec16ada35ac51fa5d9d8d63550219e45cba3c1fd6770e17405aa) and workbench.ico (93a8d699259764de4ca9ab0f91354d8c48a09d0460415ee9bda0202bec6a4df4) bytes occur in shipped Desktop DLL; project also embeds ICO in apphost | T for identity; O to affirm external-preview/employer/artwork rights remain applicable. No new trademark grant inferred. |
| Fonts / platform font references | No standalone ttf/otf/woff files in ZIP, no font files in the 40 cached package archives, no font NuGet package. Application code names Consolas/monospace and uses platform defaults; no WithInterFont setup. Targeted font-filename probes in three Avalonia assemblies found none | No separate font distribution identified. A limited resource probe is not proof of every vendor's hidden-resource provenance; component notices still need O/L review. Windows-installed fonts are not copied by this package. |
| Offline Help and API catalog / application-owned documentation | Exact catalog.json and script-api-reference.json payloads match embedded Desktop/Workspace bytes; source and project declarations identify them | T for delivery; O for authorship/external-snippet/standards permissions. No third-party prose reuse is silently presumed licensed. |
| Synthetic identity vocabulary / original repository attribution | Exact SyntheticIdentities.v1.json embedded in Scripting; its Attribution says original fictional vocabulary, no imported dataset | T for declared origin/bytes; O for application rights. Common names can coincide with real people; no anonymization guarantee. |
| Built-in scripts, generation and embedded examples | Application source/library and generated API catalog define the bundled examples; synthetic scope and authored vocabulary documented | O to confirm no unrecorded external snippets/data. No repository-only samples/test fixtures are in the 795-entry ZIP. No new source examples were packaged. |
| Owner application notice | PRIVATE-PREVIEW.txt applies to owner's application and explicitly preserves third-party rights | O/L to confirm intended invitation/employer scope and external terms. It is not a grant to redistribute without permission or a replacement for MPL/Apache/native notices. |

## 6. Technical conclusions and boundaries

**Technically verified:** the frozen 795-file archive, 74 delivered notices and
all per-record hashes; 688 selected asset copies; source/dependency identities;
three exact covered-source files; 111 pinned source digests; embedded catalogs and
application artwork. No confirmed missing mandatory notice/source file is found
within the reviewed scope. Prior full application tests/worker smokes are recorded
in the [Candidate 02 report](stage21b2-candidate02-refresh.md), not rerun here.

**Not certified:** every hidden native/resource code path, a new signature-chain
validation, legal applicability in each jurisdiction, source-code preferred-form
interpretation, application ownership/employer permission or redistribution approval.
The frozen labels remain non-distributable/awaiting owner; technical completeness
cannot clear them. Invitation-only and noncommercial use do not waive conditions.

## 7. Actionable unresolved questions / potentially blocking decisions

| ID / classification | Question and action | Effect before sharing |
| --- | --- | --- |
| O1 — owner | Confirm application, artwork, documentation/snippet and vocabulary rights, employer/security requirements, exact invitees and organization context | Pending; two friends are not assumed internal employees of one organization. |
| L1 — legal | Confirm MPL-covered file boundary and preferred Source Code Form for the adapted JSON; decide whether supplied source/NOTICE/license and provenance are sufficient or additional covered generator material is required | Potentially blocking; record qualified decision. If additional material is required, no frozen-archive edit is authorized. |
| L2 — legal / notice clarity | Review the repository-relative license reference inside covered-source/NOTICE.md versus the actual adjacent MPL-2.0.txt, recipient source instructions and third-party-rights carveout | Potentially blocking clarity question, not an absent license. Approve adequate separate clarification or seek remediation authorization. |
| L3 — legal | Reconcile native/component bundled notices and historical exclusions with these exact unchanged binaries; review any applicable HL7/metadata or trademark scope and SQLite public-domain treatment for the recipients | Pending interpretation; no blanket package-name/SPDX clearance. No specific new missing native notice found. |
| O2 — owner | Confirm actual clean-VM facts, mandatory remaining acceptance scope or explicitly accepted named gaps, unsigned-build decision and safe feedback channel | Pending. General smoke does not pass 68 individual checks. |
| B1 — release status | Frozen NON-DISTRIBUTABLE/AWAITING-OWNER marker and preparation text do not authorize testers to receive it | **Sharing blocked** until explicit owner permission and coherent approved status/handoff instructions. |
| B2 — handoff | Four new tester documents are outside the ZIP | Separate approved accompanying documents are required; no handoff bundle is generated here. |

There is **no unsupported blanket legal clearance** and no confirmed conclusion
that the entire application's proprietary code must become MPL-licensed. Preserve
both candidates; if any obligation is found unsatisfied, stop transfer and report
it rather than changing a ZIP or removing a notice. The
[owner approval checklist](stage21c-owner-release-approval.md) carries these decisions.
