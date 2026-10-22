# v0.1.0-beta distribution review

Stage 14 checkpoint/release preparation, 2026-10-22. **Distribution is not authorized.**
This records a technical license/notice review and proposed approval steps, not an
independent provenance audit or legal opinion. Final artifact evidence is retained
under artifacts/stage14-checkpoint/ and artifacts/stage14-release-preparation/.

## Resolved for candidate preparation

- The owner authorized one local Stage 14 commit and a fresh verified ZIP from that
  clean revision, without push/tag/upload/publication. The user reports the earlier
  portable app and self-contained scripting worked in a separate work VM.
- Message Workbench is the product name. The existing MW logo is approved; its
  informal nickname was an inside joke with no brand/company/publisher association.
  Artwork ownership or redistribution permission was confirmed; hashes are preserved.
- Actual published deps manifests identify 35 runtime/package entries: 34 primary MIT
  declarations and ANGLE's BSD-style file license. Runtime is pinned to .NET 10.0.12.
  Both processes include their self-contained runtime. No new dependency is added.
- The candidate retains every available package-bundled notice and 11 pinned upstream
  texts from eight repositories. Bundled texts retain bytes; upstream formatting-only
  normalization retains all words and distinct raw/stored hashes. Missing/unreviewed
  primary licenses or altered pinned text fail assembly. See portable-dependencies.md
  and the generated dependency-licenses.json for package/version/notice locations.
- MIT notice retention and ANGLE's binary notice/non-endorsement conditions are
  addressed by shipping their full texts. Primary references:
  [Toolkit MIT license](https://raw.githubusercontent.com/CommunityToolkit/dotnet/638b41dad30dffabb123a39aa38eabc7e3721371/License.md),
  [ANGLE license](https://raw.githubusercontent.com/AvaloniaUI/angle/1c89805903c1482166356d3b950d474973180e61/LICENSE).
  No endorsement or blanket legal clearance is claimed.
- Test/build-only dependencies and non-Windows native assets are excluded from the
  selected publish trees. Broad Roslyn upstream notices mention test/build tools
  (including VsSDK.CompatibilityAnalyzer); these are not separate packages/files in
  the 35-entry Windows runtime artifact. This does not prove absence of vendored code.

## Unresolved — block coworker distribution until reviewed

| Item | Evidence / remaining decision |
| --- | --- |
| Native/runtime notice scope | SkiaSharp/HarfBuzzSharp native and .NET runtime package notices, plus Roslyn/Toolkit upstream bundles, list component-specific Apache/BSD/font and conditional copyleft terms. Map relevant fragments to the exact shipped binaries and confirm notice completeness and any applicable source/relinking obligations. A primary MIT declaration or absence of a separately named dependency does not settle embedded-code applicability. Preserve full notices meanwhile. |
| Application rights and beta terms | Artwork permission is confirmed, but legal licensor/contact, rights to application contributions, proposed application-only terms, recipients, duration and liability/law decisions need owner/human approval. See beta-usage-terms-proposed.md; it is not an effective license. |
| Organizational security and trust | Application/worker are unsigned. Windows may warn or block them. Owner/employer IT must approve the unsigned pilot or choose signing; do not bypass employer controls or disable protections. |
| Exact rebuilt artifact | Verify the new clean-source ZIP/checksum and launch/script it on the intended approved Windows environment. The prior work-VM report applies to the earlier candidate; Windows 10/11, read-only-folder, DPI/accessibility matrix items not explicitly reported remain open. |
| Data/use scope | Confirm synthetic/non-production use, private feedback channel, authorized MLLP endpoints and agreed load limits. Do not claim HIPAA certification, independent security audit or production clinical approval. |
| Separate permission | Owner must explicitly approve the exact checksum and limited recipients/channel before anyone shares it. No public repository, upload, release, push or tag is authorized. |

## Distribution procedure after separate approval

1. Resolve the blockers above and approve the proposed terms, exact source revision,
   archive SHA-256, recipient list and employer-approved private transfer method.
2. Launch and exercise the exact unchanged archive in the approved environment;
   retain the reviewed checksum and approval record. Do not remove the candidate
   marker or alter signed-off contents manually. If changed labeling/signing is
   requested, authorize and verify a new artifact/checksum first.
3. Transfer the entire approved ZIP, its .sha256 sidecar and approved terms through
   that channel only. Give recipients README.md/RELEASE-NOTES.md instructions and
   the private feedback contact; retain all notices and runtime files.
4. Recipients verify SHA-256, extract fully, and launch MessageWorkbench.exe as a
   standard user. If security policy blocks it, stop and involve IT; no bypass advice.
5. Keep release/recipient records without patient data. Any wider redistribution,
   upload, public release, signing rebuild or later update requires new approval.
