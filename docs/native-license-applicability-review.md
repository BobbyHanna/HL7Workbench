# Native license applicability review — private Windows preview

Review date: 2026-10-23. Scope: the existing v0.1.0-beta Windows x64 private-preview
ZIP, not other versions, platforms or hypothetical native builds. This is a technical
applicability and notice review, not legal clearance or distribution authorization.
The owner has accepted PRIVATE-PREVIEW.txt for limited coworker evaluation; that
wording and every release artifact remain unchanged.

## Checkpoint follow-up — attribution supplemented

The owner subsequently authorized one local checkpoint and a new private-preview
package, reusing accepted binaries with separate packaging/binary provenance.
The original investigation below describes the preserved 582-file ZIP, not the new
checkpoint candidate. Its identified attribution remedy is now implemented:

- [Pinned HarfBuzz COPYING](../build/third-party/harfbuzz-harfbuzz-COPYING) includes
  Matthias Clasen's 2022 credit and the complete upstream copyright/license text.
- [Pinned Skia LICENSE](../build/third-party/mono-skia-LICENSE) supplies exact current
  BSD wording. [Wuffs LICENSE](../build/third-party/google-wuffs-LICENSE) and a
  [verbatim copyright/header excerpt](../build/third-party/google-wuffs-COPYRIGHT.txt)
  identify Wuffs 0.3.3 and Apache-2.0. The excerpt is not an invented NOTICE file.
- [The upstream manifest](../build/third-party/upstream-licenses.json) records exact
  revisions, source/stored SHA-256 hashes and the excerpt's source line selection.
  The package inventory carries those references and hashes, verified during assembly.
  All 61 original notices are preserved; these four supplementary texts bring the
  collection to 65 files.

Recommendation after the package gate: **ready for owner/legal review**. No additional
specific licensing question remains identified within this bounded investigation.
This is not independent legal certification. Exact final artifact/hash/verification
results are recorded in `artifacts/private-preview/final-checkpoint/FINAL-REVIEW.md`;
recipient/transfer/distribution approval remains separate. No application or native
binary changes are authorized or needed.

## Executive summary and recommendation

**The older Netscape/Mozilla GIF implementation is excluded from the pinned Windows
source/build examined.** Its notice is historical material in a shared, outdated
SkiaSharp/HarfBuzzSharp notice bundle. Current GIF decoding uses Wuffs 0.3.3 under
Apache-2.0, compiled into libSkiaSharp.dll. HarfBuzzSharp builds a separate font-shaping
and subsetting library, without a GIF decoder. The retained old notice does not make
the application GPL-covered or establish a source-publication/relinking obligation
for these binaries.

**Original recommendation (addressed by the checkpoint follow-up above): notice
remediation before owner/legal review.** A substantiated attribution gap was found: the native HarfBuzz
notice contains an older copyright list, omitting credits in the pinned source that
the Windows project compiles. Add the pinned HarfBuzz COPYING as a supplementary
notice, preserving all existing notices. This does not require opening the owner's
application source, modifying binaries or changing dependencies. At investigation
time, no remediation/repackaging was authorized; the later authorization is recorded
above. The original GIF copyleft question does not require a
dependency replacement or an application license change.

## Original examined artifact and versions

- Accepted application/binary commit: `af06e576a0a761f092db6ea7047b488570c0ee06`.
- Examined ZIP: `artifacts/private-preview/candidate-1/CANDIDATE/MessageWorkbench-v0.1.0-beta-win-x64.zip`.
- ZIP SHA-256: `614F73FFC5B24C7AF0A1AE1BAC58D91CFBF726D64CED876DE98278E3578FD676`.
- Size: 95,870,680 bytes; 582 files; 61 original third-party notice files; 35
  runtime/package entries. The prior accepted application ZIP is also preserved.
- SkiaSharp and SkiaSharp.NativeAssets.Win32: **3.119.4**.
- HarfBuzzSharp and HarfBuzzSharp.NativeAssets.Win32: **8.3.1.3** (not the newer
  HarfBuzzSharp version mentioned in the SkiaSharp 3.119.4 source tree).
- Other sanity-check versions: .NET runtime **10.0.12**, Roslyn **5.9.0**,
  CommunityToolkit.Mvvm **8.4.0**, Avalonia.Angle.Windows.Natives
  **2.1.27548.20260419**. The complete unchanged inventory is in
  [portable-dependencies.md](portable-dependencies.md) and the packaged manifests.

Both native DLLs match their cached NuGet packages' `runtimes/win-x64/native/`
members byte-for-byte. Each downloaded archive's SHA-512 matches its cache hash;
NuGet's separately recorded content hash matches the locked Desktop graph. Both PE machine types are x64
(`0x8664`). No native DLL with a GIF/Wuffs/FreeType name is separately shipped.

| Packaged binary | Size | SHA-256 | Package-declared source revision |
| --- | --- | --- | --- |
| libSkiaSharp.dll | 11,628,896 bytes | `7dec3ba900ab353491e6446f0083739924c6f8dd668832e2f09d38ebffdbbe1c` | mono/SkiaSharp `f568ac94dd768ef9a2f593537cfde2dd0d348ef5` |
| libHarfBuzzSharp.dll | 1,816,088 bytes | `145ade963eba427027d5e1381db5d51a0ef9e98e9fef9efbfa6074ca20da246f` | mono/SkiaSharp `2888c737ad016d584c74525e2d35db5097ea8576` |

## Notice-to-binary evidence

Status meanings: **Confirmed** is supported by the actual package and pinned
source/build evidence; **Likely** is a bounded inference with a missing link;
**Unresolved** lacks sufficient evidence; **Not applicable** has positive exclusion
evidence for this artifact. These labels are technical findings, not legal opinions.

| Notice/component | Feature and exact evidence | Binary/linkage and disposition |
| --- | --- | --- |
| Old `gif` notice | Both native packages include identical 2,725-line THIRD-PARTY-NOTICES.txt files. Lines 311-355 identify Netscape Communications Corporation, copyright 1998, with Chris Saari and Apple Computer contributions. The block matches historical `third_party/gif/SkGifImageReader.cpp` after whitespace normalization. It describes GIF decoding, not encoding or font processing. | **Not applicable** to this pinned Windows build: both Skia trees and their DEPS lack the old reader, SkGifCodec sources and libgifcodec dependency. No source inclusion path to that implementation remains. Retain the notice anyway. |
| Wuffs 0.3.3 | Skia DEPS pins the release-C mirror at `e3f919ccfe3ef542cfc983a82146070258fb57f8`. The generated C file identifies original Wuffs revision `00d5e35865a2f2718f4bb2596adaaa54bd639bbe`, copyright 2017 The Wuffs Authors, and Apache-2.0. | **Confirmed** replacement GIF decoder in libSkiaSharp.dll: `skia_use_wuffs=true`; the wuffs target explicitly compiles BASE/GIF/LZW modules and SkWuffsCodec. Wuffs is statically incorporated, not a separate DLL. Matching GIF/LZW status strings in the PE corroborate this source/build chain. |
| Skia codec integration | Skia's root BSD-style license and the Google-authored SkWuffsCodec integration apply separately from Wuffs' generated C license. SkCodec registers SkGifDecoder when SK_HAS_WUFFS_LIBRARY is defined; the exported C codec API reaches SkCodec. | **Confirmed** decoder reachable through the native codec API. This establishes capability; the application need not open GIF files for binary redistribution conditions to matter. |
| HarfBuzz 8.3.1 | Both Skia DEPS pin HarfBuzz at `2b3631a866b3077d9d675caa4ec9010b342b5a7c`. The Windows vcxproj compiles `harfbuzz-subset.cc`, an amalgamation of shaping/subsetting sources. | **Confirmed** code incorporated into libHarfBuzzSharp.dll, with 509 exported functions and only KERNEL32.dll as a regular import. It neither imports libSkiaSharp nor compiles a GIF implementation. The copied GIF notice does not describe this font library's compiled feature set. |
| FreeType | Skia's default enables FreeType on Android/Fuchsia/Linux/Wasm, not Windows; the Windows build does not override that default. HarfBuzz's Windows project does not define HAVE_FREETYPE; its optional FreeType integration is guarded by that macro. | **Not applicable** as a dependency of these examined native build paths. No FreeType import/export or separate FreeType DLL was found, as corroboration only. Keep the complete FTL/GPL alternative notice; this is not a claim about other SkiaSharp platforms. |

The PE also contains no exported old GIF class names, but this absence is **not**
the exclusion proof. Optimized builds omit such names. The proof is the historical
source identification followed by current tree/DEPS exclusion and positive replacement
build rules. Binary strings alone cannot establish source provenance or absence.

## Pinned source and Windows build chain

1. NuGet nuspecs and the unchanged package manifest identify the two different
   SkiaSharp source revisions above. Their Git trees pin `externals/skia` to
   **`7dbfc07dd33181f84e0958afb7ee805c6c769f0b`** for SkiaSharp and
   **`927041a58f130e0dd0562ba86cb4170989ad39e9`** for HarfBuzzSharp.
   See the pinned [SkiaSharp tree](https://api.github.com/repos/mono/SkiaSharp/git/trees/f568ac94dd768ef9a2f593537cfde2dd0d348ef5)
   and [HarfBuzzSharp tree](https://api.github.com/repos/mono/SkiaSharp/git/trees/2888c737ad016d584c74525e2d35db5097ea8576).
   SkiaSharp's cgmanifest names a different Skia revision (`330baf49...`); that
   broad component entry is not substituted for the actual submodule pins.
2. [Windows build.cake](https://github.com/mono/SkiaSharp/blob/f568ac94dd768ef9a2f593537cfde2dd0d348ef5/native/windows/build.cake#L43-L91)
   invokes GN/Ninja's SkiaSharp target with `target_os='win'`, `target_cpu='x64'`,
   `skia_use_harfbuzz=false`, `skia_use_icu=false` and explicit bundled image-library
   choices. The shared helper selects an official Release build with tools disabled.
   The pinned pipeline's x64 job passes `--buildarch=x64`; no Wuffs override is present.
   An optional gnArgs/environment override exists, but the reviewed published build
   recipes do not use it to enable another GIF decoder.
3. [gn/skia.gni](https://github.com/mono/skia/blob/7dbfc07dd33181f84e0958afb7ee805c6c769f0b/gn/skia.gni#L52-L82)
   enables Wuffs. [BUILD.gn](https://github.com/mono/skia/blob/7dbfc07dd33181f84e0958afb7ee805c6c769f0b/BUILD.gn#L1402-L1408)
   adds SkWuffsCodec and the Wuffs dependency to Skia. The
   [Wuffs target](https://github.com/mono/skia/blob/7dbfc07dd33181f84e0958afb7ee805c6c769f0b/third_party/wuffs/BUILD.gn)
   selects BASE/GIF/LZW preprocessing modules from `wuffs-v0.3.c`.
   `third_party.gni` makes a static library for targets with sources; SkiaSharp's
   default native output is a shared library incorporating those dependencies.
4. [DEPS](https://github.com/mono/skia/blob/7dbfc07dd33181f84e0958afb7ee805c6c769f0b/DEPS#L72)
   supplies the exact Wuffs revision. Its
   [generated C header](https://github.com/google/wuffs-mirror-release-c/blob/e3f919ccfe3ef542cfc983a82146070258fb57f8/release/c/wuffs-v0.3.c#L35-L47)
   gives the license and copyright; its
   [root license](https://github.com/google/wuffs-mirror-release-c/blob/e3f919ccfe3ef542cfc983a82146070258fb57f8/LICENSE)
   is Apache-2.0. Do not substitute a different/current Wuffs licensing arrangement.
5. The historical reader's
   [source](https://github.com/mono/skia/blob/19cb22e3d4c32959349fac086949a9eb2eafcdfd/third_party/gif/SkGifImageReader.cpp#L1-L38)
   and header identify the old implementation. Upstream
   [commit 1ec9a28a](https://github.com/mono/skia/commit/1ec9a28a28708b61692811d59c66ca8926cf7176)
   removed those files in 2019, initially using an external libgifcodec. The **current**
   pinned DEPS/build has neither that dependency nor its decoder; removal of the old
   directory alone would not have established exclusion of the external successor.
6. HarfBuzzSharp's pinned
   [Windows project](https://github.com/mono/SkiaSharp/blob/2888c737ad016d584c74525e2d35db5097ea8576/native/windows/libHarfBuzzSharp/libHarfBuzzSharp.vcxproj#L29-L31)
   compiles the
   [HarfBuzz amalgamation](https://github.com/harfbuzz/harfbuzz/blob/2b3631a866b3077d9d675caa4ec9010b342b5a7c/src/harfbuzz-subset.cc).
   It has its own preprocessor settings and no dependency on Skia's image targets.

## License alternatives and actual obligations

| Component/route | Attribution, source, replacement and application implications | Original package before supplementation |
| --- | --- | --- |
| Historical GIF reader | Alternatives are **MPL-1.1 OR GPL-2.0-or-later OR LGPL-2.1-or-later**, not all three cumulatively. The retained block permits a recipient to choose a route unless replaced with a GPL/LGPL-only notice. If this code shipped, the selected route's covered-source/notice or linking conditions would need assessment; MPL's covered-code rules differ from GPL and LGPL. | Excluded here, so no GIF-derived source offer, relinking materials, modification disclosure or application source publication is established. No election of GPL/LGPL is required merely because this notice remains. See [MPL 1.1 sections 3.6-3.7](https://www.mozilla.org/en-US/MPL/1.1/). |
| Wuffs Apache-2.0 | Provide the license; mark modified files if applicable; preserve applicable source notices when distributing source; reproduce an upstream NOTICE if one exists. No source-delivery or relinking requirement, and no requirement to license the owner's separate application under Apache. | Full license terms through section 9 already occur verbatim apart from whitespace in the native bundle. The pinned Wuffs mirror has **no NOTICE file**. No owner modifications to native files. Explicit Wuffs identification/copyright is absent; adding the pinned license/header credit would improve clarity, but lack of a Wuffs-named file alone does not establish an unmet section 4(a) or 4(d) binary requirement. [Apache-2.0 section 4](https://www.apache.org/licenses/LICENSE-2.0). |
| Skia BSD-style | Retain copyright, conditions and disclaimer in binary distribution materials; do not imply endorsement. No covered-source or relinking requirement. | The packaged `skia` section retains Google 2011 copyright and the three BSD conditions/disclaimer. Its older non-endorsement wording names Google; the current root LICENSE says copyright holder. Same named holder, but not an exact current-text match. Adding the pinned root LICENSE is a recommended precision improvement, not evidence of copyleft. |
| HarfBuzz Old MIT | Preserve applicable copyright and permission/disclaimer notices. No mandatory source delivery, relinking facility, modification publication or application source-license change. | **Specific attribution gap:** the older package notice lacks the current pinned COPYING list. `harfbuzz-subset.cc` compiles `hb-paint.cc`, whose header credits **Matthias Clasen (2022)**; the DLL exports `hb_paint_funcs_create`. No packaged notice names Matthias Clasen. Adobe's 2018 CFF source credits are another example; unrelated DNG attribution is not a substitute. Add the exact pinned COPYING as a supplementary file before sharing. |
| FreeType FTL/GPL | If included in another artifact, FTL is an available BSD-style route with credit requirements; optional GPL text does not force that election. | Not included by these Windows paths; original full text remains untouched. See [FreeType licensing](https://freetype.org/license.html). |

The pinned [HarfBuzz COPYING](https://github.com/harfbuzz/harfbuzz/blob/2b3631a866b3077d9d675caa4ec9010b342b5a7c/COPYING)
and directly compiled [hb-paint.cc](https://github.com/harfbuzz/harfbuzz/blob/2b3631a866b3077d9d675caa4ec9010b342b5a7c/src/hb-paint.cc#L1-L24)
support the attribution finding. The missing current credit is a confirmed package
observation; exact sufficiency of a future consolidated notice remains for owner/legal
review. Do not replace or edit the original vendor bundle to repair it. Supplementing
COPYING plus the directly relevant credits is a small, concrete remedy; no new license
agreement or application behavior change is needed.

## Bounded sanity check of the other notice questions

- **.NET 10.0.12:** actual package LICENSE/THIRD-PARTY-NOTICES and both runtime copies
  remain intact. A top-level MIT label does not supersede component terms. No new
  material applicability issue was established in this bounded review.
- **Roslyn 5.9.0:** broad VsSDK analyzer/build-tool entries are absent as standalone
  runtime packages/files. That supports scope limitation, not proof about every
  vendored source fragment. Preserve all existing source-access/reverse-engineering
  statements; no new runtime LGPL obligation was established.
- **CommunityToolkit.Mvvm 8.4.0:** broader Common/notification/UnitTests notice entries
  do not prove those components ship. No LGPL text was identified in that notice.
- **ANGLE:** exact Windows native binaries and the original complete BSD-style notices
  remain. Retain copyright/conditions/disclaimer and non-endorsement; no source/relink
  requirement was established from that license. This is the Avalonia ANGLE package,
  not SkiaSharp's separate optional WinUI ANGLE distribution.
- **SkiaSharp/HarfBuzzSharp/FreeType:** the concrete conclusions above supersede the
  former generic uncertainty about this GIF block; retain all other bundled sections,
  including platform/tool components. This is not a new full dependency source audit.

## Remaining uncertainty and next action

The source mapping is supported by package-declared revisions, Git submodule/DEPS
pins, Windows recipes, exact NuGet binary matches and positive PE evidence. It is not
an independently reproducible rebuild of upstream native packages. Original compiler
command logs, linker maps and a supplier attestation were not available. Hidden vendor
patches or arbitrary out-of-tree build overrides cannot be disproved by this review;
no evidence of them was found. Their hypothetical possibility is not a new blanket
distribution prerequisite, and does not outweigh the positive exclusion/replacement
evidence for the identified old GIF reader.

The **original concrete action was attribution supplementation**, followed by review
of a separately authorized documentation-only candidate and its new checksum. Prefer
the pinned HarfBuzz COPYING (including the compiled-source credits), pinned Skia root
LICENSE for exact wording, and an explicit pinned Wuffs license/header attribution.
Preserve all original texts. Only the HarfBuzz credit gap is demonstrated as a missing
applicable attribution here; the Skia/Wuffs additions improve explicit traceability.
No dependency upgrade, binary modification, source offer for the old GIF code or
relicensing of Message Workbench is recommended by this evidence. The supplementary
texts are now implemented as recorded above; the old ZIP remains unchanged evidence.

The owner's accepted private-preview wording is unchanged. Owner review of the exact
artifact/recipients remains distinct from this technical investigation. No employer
approval, distribution permission or legal-clearance certificate is asserted.

## Verification and retained evidence

- Existing ZIP preserved at its exact SHA-256, with all 582 entries and 61 notice
  files unchanged. Dependency manifests are unchanged. Original accepted ZIPs and
  both private-preview candidates are preserved.
- All 413 frozen application/build/test/fixture/artwork inputs match their existing
  hashes. Protected fixture/artwork hashes and the accepted notice wording match.
- Thirty-two downloaded source blobs cross-check against the pinned Git tree IDs.
  The native DLLs match the locked NuGet x64 members; supporting PE/import/export,
  source/license and hash results are under `artifacts/native-license-review/`.
- Only this new document and two existing review documents are edited by this task;
  the other seven pending private-preview documentation inputs remain byte-identical.
- `git diff --check` and new-file whitespace checks pass. HEAD/index are unchanged;
  no full application tests, restore, build, repackage or release operation was run.

Ignored evidence includes pinned Git/API inventories, retrieved source/license files,
URL/SHA-256 records, artifact/source inspection results and verification helpers. Failed
historical/path guesses returned 404 and are recorded; the substantive pinned files
above were obtained successfully. Downloaded upstream scripts were read, never run.
Prior packaging/worker results remain historical evidence; no rerun is claimed.
