# Built-in HL7 v2.5.1 model metadata

`v251-model.json` is an adapted metadata translation of the nHapi project's
generated v2.5.1 message/group/segment/datatype declarations, pinned to revision
`9027797b1fd1faebcffd743ac11324499145f29d`:

https://github.com/nHapiNET/nHapi/tree/9027797b1fd1faebcffd743ac11324499145f29d

This adapted resource is made available under the **Mozilla Public License 2.0**.
The license text is at `build/third-party/nHapi-MPL-2.0-LICENSE.txt`. Upstream
contributors retain their rights; see the upstream repository and source history.
The resource records source paths and SHA-256 digests. The separately authored
validation engine is not an inclusion of the nHapi runtime or parser.

Changes: model declarations translated into declarative JSON; group slot keys
disambiguated; timestamp primitive aliases normalized; the ORM order-detail
alternative represented as a choice per HL7 v2.5.1 section 4.4.1; symbolic variable
length 99999 represented without a fabricated numeric ceiling, per section 2.5.3.2.
The original standards prose and complete HL7 specifications are not bundled.

Regenerate explicitly with `build/update-validation-catalog.py`. Regeneration is a
development operation using the pinned upstream, never an application network call.
See `docs/stage17-hl7-schema-validation.md` for enforced scope and limitations.
