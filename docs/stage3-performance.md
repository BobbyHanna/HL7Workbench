# Stage 3 performance and verification

Measured on October 14, 2026, Windows 10.0.26100, .NET 10.0.12, x64,
16 logical processors, Release configuration. CPU model/storage performance
were not calibrated. These are individual observations with normal filesystem
caching and variable writeback contention, not p95 claims or cold-cache guarantees.

All inputs are generated synthetic ORUs with ED/Base64 attachments, following fields,
following segments, and a second message. Encoded size is ASCII Base64 text; source
file size adds 229 bytes. No complete attachment string/array is used by the generator.
Fixtures and raw JSON measurements live under ignored `artifacts/stage3/`.
The performance table records the consolidated UX refinement observations, before
the subsequent native-acceptance fixes. New test runs regenerate the ignored JSON
files and may have different timings. See `stage3-workspace-layout-acceptance.md` for
the current verification and granted native Windows acceptance status.

## Measured stages

Times are milliseconds; retained managed deltas are MiB above the
process baseline for the direct capture/index/decode/scan/preview pipeline.

| Encoded MiB | Capture | Index | UTF-16 spool | Structure scan | Session open | Cached return | Projection | Retained managed |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| 20 | 17.5 | 36.1 | 25.7 | 39.4 | 122.3 | 36.9 | 0.077 | 0.175 |
| 50 | 33.1 | 89.4 | 66.5 | 99.4 | 297.2 | 105.2 | 0.088 | 0.177 |
| 100 | 59.5 | 154.6 | 185.4 | 199.0 | 864.5 | 197.4 | 0.078 | 0.192 |
| 256 | 170.3 | 451.1 | 366.7 | 583.8 | 5663.9 | 548.5 | 0.113 | 0.288 |
| 512 | 368.3 | 984.4 | 10497.5 | 1172.1 | 37561.2 | 1138.1 | 0.068 | 0.409 |

Snapshot, indexing, decoding and scanning are timed separately. Session open is
a separate run through the actual document service and includes all preparation.
Returning reuses one decoded spool and rescans structure without retaining old models.
This explains why open and return timings are not the sum of the first four columns.

Disk writeback varied substantially: exploratory pre-cache runs included a 36-second
256 MiB return, motivating the bounded one-spool cache. Refinement stress runs also
observed a **52.5-second 512 MiB spool write** and, in a separate run, a **37.6-second
512 MiB session open**. The table reports the most recent observations per size.
The recorded stress results demonstrate variable initial decoding/open times. Structure scan throughput
alone exceeded the provisional 25 MiB/s target in these observations; full opening
latency is constrained by temporary storage and is not guaranteed by that result.

Direct bounded preview/window retrieval was sub-millisecond in these observations.
These calls exclude layout and publication. Abbreviation reduces the generated
20/50 MiB messages to **735** displayed UTF-16 units and 100/256/512 MiB messages to
**736** units, including following fields/segments. Projection creates no payload-
sized string. The source pipeline allocation/retention checks now include projection
and reading its full bounded presentation.

For a 100 MiB attachment plus four encoded characters, Avalonia.Headless measured
**30.5 ms** to first abbreviated layout and **19.3 ms** to structural navigation/tail
layout, displaying **736** units. This follows complete file preparation and excludes
initial capture/decode latency. The compact main preview soft-wraps; ordinary message
wrapping is unchanged. A separate two-million-character many-small-value UI test
exercises virtualized blocks, bounded editor lengths and final-segment navigation.
Native GPU/font/clipboard behavior remains unverified.

An exact **20 MiB source-value export** took **14.3 ms**, allocating approximately
**0.146 MiB** managed memory. It streams 64 KiB buffers and never decodes attachments.
Thirty warm small-file openings observed **2.11 ms mean, 1.97 ms median, 3.23 ms max**.
Those messages retain the materialized route with no abbreviation projection. This
checks the route and records current latency; it is not a controlled before/after
benchmark. Original ordinary-message regressions remain intact.

GC retained-memory checks use a full collection; total allocated bytes are also
recorded. Peak managed/private deltas are sampled every 10 ms and can miss transient
peaks. They are not working-set/RSS measurements and exclude OS file-cache size.
The direct source pipeline asserts less than 16 MiB total managed allocation for
each fixed-structure attachment, and less than 64 MiB retained managed growth.
Three repeated message round trips assert less than 8 MiB retained growth; recorded
round-trip retained deltas were approximately zero. Every closed document's owned
snapshot directory was removed. The models/checkpoint counts, not decoded attachment
strings, account for the modest increase with payload size.

## Automated verification

- Original Stage 2 baseline: 356 tests; no original cases removed or weakened.
- Normal complete suite after workspace layout polish: **478 passed, zero failed/skipped**
  (Core 216, Workspace 189, Desktop 73).
- Optional Workspace stress suite: **191 passed, zero failed/skipped**, including
  256/512 MiB generated attachments. The normal Workspace subset has 189 cases.
- Locked restore passed. Release build: **zero warnings, zero errors**.
- New tests include long coordinates beyond int.MaxValue using a virtual source,
  tiny capture reads, multibyte/surrogate cache boundaries, exact byte partitions,
  10,000 messages, malformed framing, batch wrappers, provisional input, strict
  failures, source deletion, crash-cleanup eligibility, cancellation/close races,
  structural budgets, bounded copying, diagnostics, real glyph hover/double-click,
  mapped abbreviation/markers and clipboard semantics, virtualized navigation,
  complete raw attachment-tail access, exact export in five encodings, lazy tool
  disposal, tabbed structural tools and remembered closed/floating placement.

No p95 UI-input/hover latency, cancellation-deadline, cold-cache benchmark, mixed-DPI
or native cross-platform result is claimed. The user explicitly granted acceptance
for the complete Stage 3 milestone on October 14, 2026; see
`stage3-workspace-layout-acceptance.md`. One local milestone commit is authorized;
push, publication, tagging and release remain unauthorized.
