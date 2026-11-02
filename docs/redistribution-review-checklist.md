# Redistribution Review Checklist

Stage 21B.1 checks technical presence, declarations, bytes and provenance only.
These unchecked decisions remain **release blockers** for an eventual artifact;
the preparation scripts cannot provide legal clearance or distribution approval.

- [ ] Owner/qualified reviewer approves the exact runtime/package/native dependency
  inventory for Desktop, ScriptHost and QueryHost, including MIT, ANGLE/component
  terms, SQLitePCLRaw Apache-2.0 license/NOTICE, native SQLite's public-domain declaration,
  and .NET/Roslyn/HarfBuzz/Skia/Wuffs notices. Retain every applicable original notice.
- [ ] Reviewer confirms adapted nHapi metadata's MPL-2.0 applicability and the actual
  recipient/organization context. The exact modified v251-model.json, its change/source
  NOTICE and MPL text are delivered in covered-source/, with hashes in provenance.
  An upstream link alone is not substituted for the adapted source. Do not imply the
  independent application's entire source becomes MPL-licensed.
- [ ] Owner confirms application/artwork rights, employer requirements and permitted
  recipients/transfer method. PRIVATE-PREVIEW.txt must preserve all third-party rights.
  No unrestricted application redistribution or new trademark clearance is inferred.
- [ ] Reviewer reconciles actual selected binaries and component-notice scope. A
  package's primary SPDX identifier alone does not settle native/component obligations.
- [ ] Exact-artifact clean-machine/native acceptance, current CI result, supported OS,
  source/build identity, input/dependency hashes and archive checksum are recorded.
  Preparation logs and a build on a development PC are not that acceptance.
- [ ] Owner approves synthetic-only recipient guidance, feedback/contact channel,
  signing/unsigned status and authenticated checksum delivery before any transfer.

If any obligation remains unresolved, retain the blocker and seek review. Never
weaken a fail-closed notice/resource check to create an artifact. No commit, final
ZIP, upload, publication or distribution is authorized by this checklist.
