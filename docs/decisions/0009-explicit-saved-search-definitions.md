# ADR 0009: Explicit saved query definitions over the existing search pipeline

Status: Accepted with Stage 6B native Windows acceptance. The user explicitly authorized
the accepted milestone commit and normal push to origin/main; no tag, release or installer.

## Context

ADR 0008 made search results and query state session-only. Stage 6B needs reusable
queries without storing document-specific state or silently retaining potentially
sensitive query values. The existing snapshot/draft capture, scanner, revision guards,
paging and navigator filter already provide the required discovery lifecycle.

## Decision

Extend MessageSearchQuery with versioned Simple/Advanced input, a maximum of 16 immutable
condition snapshots in an ordered Boolean tree (ALL/ANY per group). Root depth is one;
bound the whole tree to 16 leaves, 16 groups and four levels. Reject empty/invalid groups,
including unvisited branches, before scanning and deep-copy the validated tree. Guard
limits during recursive evaluation too. Adapt Simple into one effective condition
inside the same MessageSearch scan; preserve its original match counts. Parse paths
once and lazily scan structure once per message, reusing bounded decoded text. Do not
introduce a second file scanner, result database, expression parser or worker dependency.

Advanced negative field comparisons require presence and the absence of any matching
value across all applicable segments/repetitions. Existence tests distinguish missing
from present-empty. Short-circuit evaluation means Advanced counts refer to satisfied
evaluated conditions, with one bounded summary and one row per matching original ordinal.
Query edits invalidate the entire result/filter state, like content edits, without
altering document bytes. Clear retains the authored query; loading never runs it.

Add a separate Search Library under the existing local application configuration root.
Persist one versioned GUID-named JSON record per definition, whitelisting effective
query inputs only. Names never become paths. Current result metadata, control IDs,
document identities/paths/contents, ordinals, inactive drafts and query history are
excluded. Query values themselves may contain sensitive data: Save Search and Update
are explicit actions with a nonmodal local unencrypted-storage disclosure. There is no
repeated Save/Update confirmation, PHI prohibition or implicit autosave.
This intentionally extends ADR 0008's input retention only; results remain ephemeral.

Reuse FileSaveDestination's staged/flush/identity/hash/replace publication boundary,
with a library lock and expected record revisions following Script Library conventions.
Rename reloads and preserves the stored query, so it cannot adopt an unsaved draft
without explicit Update. Schema/query version 2 stores ordered child groups/conditions.
Version-1 flat definitions migrate in memory to an equivalent root, preserving names,
order and matching semantics. List/Load never rewrite; explicit Update/Rename publishes
version 2 against the original file revision. Reject duplicate names, unsafe names, entry links, oversized
records, missing/unknown schema fields and unsupported versions. Skip corrupt entries
independently, retain them for review, and expose fixed errors/counts without raw values.

## Consequences

The library is bounded (256 records, 128 KiB each), local, and independently recoverable
when one file is corrupt. Stored literals are not encrypted; users can save sensitive
definitions after the local-storage disclosure. Errors/logs never contain those values.
Filesystem publication/deletion is not a universal transaction against noncooperating
external writers. Native confirmation, DPI, keyboard and floating-window behavior still
require acceptance. Layout/settings remain placement/preferences only, retaining the
accepted `tool.search-results` identity and shared Find Messages open/focus command.

Inspector Copy Path reuses Core-backed inspection coordinates, omitting unsupported
occurrence/repetition indices. Discovery now supports subcomponents so copied paths are
accepted. Appearance persists a theme enum in existing preferences and maps directly
to Avalonia Default/Light/Dark, inheriting OS notifications and floating-window resources.
HL7 rendering now resolves a small immutable editor palette through Avalonia dynamic
resources, with semantic application-surface resources over Fluent. Theme changes only
redraw existing lines. This is an extension boundary for later custom palettes, without
profile editing/storage, a second theme framework, scanner or clipboard service.
The Advanced query/results split is independent version-1 placement metadata inside
existing preferences. It stores one bounded fraction on completed gestures, never query/
result/document state; Simple remains automatic/compact. Dock layouts and the tool ID
remain unchanged. See desktop-preferences.md for resolution and persistence details.

Regex, saved-query interchange/sync, automatic history, transformations
and scripting against results remain outside Stage 6B.
