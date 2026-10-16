# ADR 0008: Read-only search over indexed documents and immutable drafts

Status: Accepted with Stage 6A native Windows acceptance on 2026-10-16, including the
final Find Messages UX cleanup. The user authorized the milestone commit and normal push
to main; no history rewriting, tag, release, installer or distribution package.

## Context

The accepted document has an immutable original SourceSnapshot / MessageFileIndex,
bounded Workspace-owned edited-message strings and one active lazy inspection. Its
existing ContentVersion also changes on navigation; using it for search ownership
would invalidate a result whenever the user follows that result. Script capture is a
transactional byte snapshot with save/encoding/boundary validators; applying those
validators to discovery would prevent searching malformed or unencodable drafts.

## Decision

Add a content-only SearchRevision, changed by supported Workspace edits/undo/redo and
invalidated by document disposal. A search takes immutable references to the bounded
draft strings and borrows the original snapshot/index. Untouched messages are read
from original indexed payloads; edited messages use their exact current draft text,
even while inspection is pending. No second document representation, reserialization
or file-wide decoded string is created. Original ordinals stay authoritative, including
when an unsaved draft contains invalid boundary text; discovery does not change save
eligibility or reinterpret that draft as replacement documents.

Run one scan off the UI thread. Strictly decode small payloads in bounded memory;
reuse DecodedMessageSource's private UTF-16 spool for larger messages. Field search
uses Core's shared Hl7StructureScanner. Text matching uses bounded overlapping windows
with ordinal comparison and non-overlapping match positions. Metadata is never a
search source. Structural-budget failure does not hide a literal text match; field
search reports that message as skipped instead of presenting incomplete structure as
complete coverage. Unreadable encodings are skipped with a count, never repaired.

A read lease keeps the document snapshot alive. Closing/editing cancels the scan;
document disposal waits for search readers to drain before deleting original/spool
storage. A run generation rejects queued progress/completion from superseded work.
Document switching clears results and filters instead of keeping a per-document cache.

Store one bounded metadata row per matching ordinal, up to the existing 100,000-message
index ceiling. Bound message type/control-ID display lengths and expose 200-row pages
to a virtualized list. This avoids another PHI-bearing disk result journal or database.
Paging bounds the UI; the complete bounded ordinal set supports navigator filtering.
The tradeoff is up to 100,000 small resident metadata rows, rather than disk paging of
unbounded results. Private decoded spools retain the existing snapshot lifecycle.

Filters are session-only original-ordinal lists on the existing document navigator.
They do not mutate the index, source, document membership or active message. An excluded
current message remains selected with a clear indicator until explicit navigation.
Find Messages is an optional ordinary Dock tool; layout persists its placement only.
Its original `tool.search-results` identity remains compatible with earlier layouts.
View, ribbon and Ctrl+F use one shared open/focus command, without running a search.
Ctrl+F is routed at main/floating window roots to that command. Existing
shell menus, ribbon styling/input routing, Preferences and scripting remain intact.

## Privacy and scope

No query terms, matches, control IDs, filenames or message contents enter search logs,
diagnostics, layout or preferences. Rows contain ordinal, capped MSH-9/MSH-10, a safe
coordinate and count; never snippets or patient field values. Record stringification
excludes terms and headers. All state expires with its active document/search session.

Defer Boolean/regex queries, saved definitions/history, search-and-replace, bulk edits,
script integration, external indexing and databases. Existing materialized Core APIs,
edit/serialization limits and worker/dependency boundaries are unchanged.
