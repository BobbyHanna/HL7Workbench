# v0.1.0-beta private-preview distribution review

Current scope: personal/internal development and evaluation by the owner and a
small trusted coworker group, compiled ZIP only, no commercial sale or public owner-
repository access, no guaranteed support. The user reports automated and Windows VM
acceptance of commit af06e576a0a761f092db6ea7047b488570c0ee06. The owner authorizes one
local documentation/notice/packaging checkpoint and a fresh candidate for final review;
no upload, publication or distribution is authorized. This is a technical review
of the selected artifact and original notices, not legal clearance or employer approval.

Follow-up native applicability investigation: the owner has accepted the short preview
wording. The pinned Windows builds exclude the historical Netscape GIF reader and use
Apache-2.0 Wuffs instead. The identified HarfBuzz attribution gap is supplemented; see
[the native applicability review](native-license-applicability-review.md). This task
preserves both earlier ZIPs and does not authorize sharing.

## Application notice and packaging

[PRIVATE-PREVIEW.txt](PRIVATE-PREVIEW.txt) replaces the former proposed lengthy
application agreement. The old draft is withdrawn, with a reference stub retained
in the repository; neither that stub nor a second agreement is packaged. The notice
restricts redistribution of the owner's application only and expressly preserves
third-party rights. No licensor/company identity, jurisdiction, term, liability cap,
sale or public release is invented.

The accepted ZIP never contained the proposed agreement. The final candidate uses
committed recipient documents, approved PRIVATE-PREVIEW.txt and supplementary notices.
It preserves all accepted executable/runtime bytes and every original third-party
file. The owner explicitly chose binary reuse: recompilation changes the SDK's Git
revision stamp. Provenance records the new packaging commit with dirty=false and the
original binary commit/archive hash separately, after checking compilation inputs and
all runtime hashes. No application source, build settings or dependency versions change.
The minimal packaging update requires the notice and verifies pinned notice hashes.
Retain the candidate marker until final owner permission; preserve all older evidence.

## Confirmed facts and obligations

- The actual two-process Windows artifact has 35 package/runtime entries: 34 primary
  MIT declarations and ANGLE's BSD-style file license. Primary package declarations
  are not substitutes for embedded-component terms. Every original copyright/
  license/notice text and runtime dependency manifest is retained byte-for-byte;
  the license inventory gains supplementary notice references and hashes.
- MIT components require copyright/permission notice retention. ANGLE's binary
  conditions require its copyright, conditions and disclaimer, and prohibit implied
  endorsement. See the pinned [SkiaSharp MIT license](https://raw.githubusercontent.com/mono/SkiaSharp/f568ac94dd768ef9a2f593537cfde2dd0d348ef5/LICENSE.txt)
  and [ANGLE license](https://raw.githubusercontent.com/AvaloniaUI/angle/1c89805903c1482166356d3b950d474973180e61/LICENSE).
- The shipped SkiaSharp/HarfBuzzSharp native bundles retain additional Apache,
  BSD-style, font and other component notices. The pinned Windows recipes do not
  include FreeType; its preserved notice offers FTL or GPL for builds that do.
  FTL is a BSD-style option with credit conditions, not an automatic GPL requirement.
  [FreeType's own license explanation](https://freetype.org/license.html) confirms the
  alternatives; this review does not silently elect a license for uncertain code.
- Both pinned runtime and all native/managed libraries are unchanged. The .NET
  runtime has its own component notices beyond MIT; the selected package's complete
  original notice text governs retention, not a replacement fetched from another tag.
  [.NET upstream explains this distinction](https://raw.githubusercontent.com/dotnet/runtime/v10.0.12/THIRD-PARTY-NOTICES.TXT).

## Specific uncertain or nonapplicable items

| Item | Evidence and bounded conclusion |
| --- | --- |
| Historical GIF code | The identical broad native bundles retain the old Netscape 1998 reader's MPL-1.1 OR GPL-2.0-or-later OR LGPL-2.1-or-later block. The pinned Windows trees/DEPS/build rules exclude that reader and libgifcodec. Wuffs supplies GIF decoding; HarfBuzzSharp compiles font shaping/subsetting without GIF. No old-GIF source/relink/application-relicensing obligation is established for this artifact. The conclusion uses source/build evidence, not absent strings. |
| Replacement GIF decoder | Wuffs 0.3.3 is Apache-2.0, statically incorporated into libSkiaSharp.dll. Full license terms already occur in the original bundle; the pinned Wuffs mirror has no NOTICE file. Explicit Wuffs identification would improve clarity but absence of a Wuffs-named notice alone does not establish a binary license-copy failure. |
| HarfBuzz attribution | The original native bundle omits current 8.3.1 credits, including Matthias Clasen (2022) in compiled/exported paint code. The exact pinned COPYING is now added as a supplement, retaining the original bundle. Old MIT requires attribution, not application source publication. |
| FreeType and Skia | The pinned Windows defaults exclude FreeType, and HarfBuzz's optional integration is not enabled. Keep its FTL/GPL alternatives regardless. Skia's historical BSD notice remains; the supplement adds the pinned current root LICENSE. Wuffs' pinned license/header is explicit. The broad cgmanifest names a different Skia revision from the actual submodule pin; the review follows the actual pins. |
| Roslyn VsSDK notices | The broad pinned Roslyn notice lists Microsoft.VsSDK.CompatibilityAnalyzer with mixed licenses including LGPL and Microsoft.VSSDK.BuildTools. Neither is a package entry or named standalone file in this 35-entry artifact. Those SDK/tool entries are not established runtime obligations here; absence from the graph does not independently prove absence of all vendored code. Preserve the complete notice and its existing third-party reverse-engineering/source-access statements. |
| Toolkit notices | The Toolkit notice explicitly includes Common/Deferred, notification helpers and UnitTests as well as MVVM-related material. Only CommunityToolkit.Mvvm is a package entry here. A UnitTests notice does not establish that test code ships. No LGPL text was found in this Toolkit notice; the earlier generic grouping was not evidence of Toolkit copyleft. Retain the entire original file. |
| Platform/build-only assets | No standalone non-Windows native, test-runner or VsSDK analyzer package is shipped. Managed cross-platform libraries such as Avalonia.X11 remain in the untrimmed graph; do not claim all platform-related notices are inapplicable or remove them. |

Evidence comes from the actual packaged dependency-licenses.json, both .deps.json
files and original notice files, corroborated by primary upstream license sources.
The package integrity scan is not an independent native rebuild or complete source
audit. There is no confirmed requirement from this review to open-source the owner's
application. The identified attribution remedy is now implemented; the historical
GIF implementation is excluded. No additional specific licensing question remains
identified within this bounded review. Exact pins, evidence,
source/build limitations and primary references are in the native applicability review.

## Before private sharing

1. The owner has accepted PRIVATE-PREVIEW.txt. Exact ZIP/checksum, limited recipients
   and private transfer method still require owner review. No sharing is performed.
2. Verify the final package includes all 61 original notices plus the four pinned
   HarfBuzz/Skia/Wuffs supplements and their source/hash manifest entries. The new
   clean-checkpoint ZIP has its own checksum and separate binary-source provenance.
3. Confirm rights to share the owner's application and follow workplace security/data
   rules when used at work. Artwork permission was previously confirmed. No employer
   approval is presumed; the application/worker remain unsigned.
4. Verify the new archive checksum and recipient documents. Byte-identical binaries
   preserve the accepted application payload; review the new documentation/provenance
   rather than claiming the old checksum identifies this different archive.

Private/noncommercial use does not, by itself, establish that third-party conditions
are waived. No extra agreement or invented legal-clearance certificate is required
by this preparation. Attribution supplements address the identified gap; owner review
of the exact ZIP, recipients and transfer method remains outstanding.
