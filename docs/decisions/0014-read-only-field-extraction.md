# ADR 0014: Bounded read-only field extraction

Checkpoint status, 2026-10-21: user native Windows acceptance passed. One exact
33-path Stage 10 local commit is authorized after fresh verification. Earlier
pending status below is historical; Stage 11 is excluded from this checkpoint.

Status: implemented and automated verification passed 2026-10-21 after the Stage 9C checkpoint
867016c3d1eab969c4a1853994eedb95621ad4f0. Stage 10 remains uncommitted pending native
acceptance. No new dependency, source mutation, worker protocol or Dock schema.

Reuse MessageDocument's immutable search capture/read lease, indexed original payloads,
bounded draft references, strict small-message decoding and large-message UTF-16 spool.
Core's shared scanner/path/escape APIs remain the grammar authority. Extraction reads
only requested values after scanning structural coordinates; it never decodes binary
attachments or reserializes source messages. Edits/closure cancel an in-flight scan;
completed results remain associated with the captured document/revision and become
stale/closed explicitly. Stable MessageIds resolve navigation through current logical
positions; missing/closed/replaced targets cannot navigate to another document.

First Match chooses occurrence/repetition 1 unless indexed. Expand Matches enumerates
each column in segment-occurrence then repetition order. The nth candidates align on
row n. A single candidate is broadcast; exhausted multi-candidate columns become an
explicit alignment-missing value. Each cell retains its actual canonical path. This
ordinal zip policy deliberately does not infer clinical relationships between different
segment types. It prevents a Cartesian product; explicit indexes constrain expansion,
including explicit [1]. Preserve that explicitness when canonicalizing saved paths.
The tradeoff is clear coordinates rather than a schema-specific group-join engine.

Use a private sealed SourceSnapshot result journal, not an in-memory value table or
database. Keep only bounded row-offset metadata and optional filtered/sorted integer
indexes resident. Read one complete bounded row at a time; show 100-row pages with
256-character presentation previews. CSV reads authoritative complete journal values,
never the previews. Filtering/sorting run off the UI thread with cancellation over
the journal, without reloading HL7. Sorting uses bounded key caching and stable source
order for ties; large uncached sorts may be slower. No arbitrary value index persists.

Default ceilings: 32 columns; 4,096 expanded rows/message; 1,000,000 stored rows;
1 GiB result journal; 65,536 UTF-16 units/value; 16 MiB encoded row; 4 MiB sort-key
cache; 1,024 distinct hashes/column; 4 GiB CSV. Limits are explicit and bounded.
Oversized/unreadable values have an unavailable status rather than an authoritative
truncated value. Row/journal expansion limits produce an explicit truncated summary.
Canceled runs publish no partial journal and retain a prior completed result, if any.
Snapshots and decoded sources drain before disposal, following existing owners.

CSV uses UTF-8, CRLF, quoting/escaping and the existing formula-protection policy.
A small Core formatter is shared by ScriptBatch and extraction so those rules cannot
drift; existing scripting bounds and behavior stay intact. Each configured column
exports value, state, status and actual path, alongside captured document/revision,
message identity/position and expansion ordinal. Missing/empty/null/unavailable are
unambiguous even when their value cell is empty. Export includes the entire selected
filtered/sorted result, using FileSaveDestination for atomic, consent/version-scoped
publication. Reject destinations that alias source files or owned journal storage.

Saved configurations follow existing bounded GUID-record/optimistic revision/atomic
publication patterns. Persist ordered labels/paths, mode/decoding and filter/sort
preferences. Literal filter values are session-only and never serialized. Results,
source identities/paths, patient values and history are never stored in configurations,
layout, diagnostics or activity logs. The Interface Overview preset is immutable and
duplicable; user copies are independently owned. New labels/names are explicit user
configuration, never copied from extracted values.

Field Extraction is an optional ordinary Dock tool with release-on-close. Its feature
model contains no Dock types; layout stores placement only. Results retain their own
source rather than following tab changes implicitly. View/document actions share one
open authority, and the user can explicitly select Use active document for the next run.
