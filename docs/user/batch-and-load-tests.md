# Run a reviewed batch or controlled load test

Choose **View → Connectivity & Testing → Batch Transmission / Controlled Load
Testing**, or **TCP/MLLP → Batch Transmission / Load Testing**. Prerequisites:
authorized synthetic messages, a saved Connection Manager endpoint and authority
to test that exact destination. Start with the [loopback setup](test-with-mllp.md).

Batch sends a captured sequence once with pacing and pause/resume. Load testing
adds bounded concurrent attempts, a global target rate, duration/attempt limits
and safety stops. Neither is a scheduled job, automatic retry or production sender.

## Send a small sequential batch

1. Open a small [generated synthetic batch](generate-synthetic-hl7.md) and keep it active.
2. In **Batch Transmission**, **Reload saved endpoints** and select your loopback
   profile. Select All messages, a Contiguous range or Explicit positions (source order).
3. Keep a conservative interval and **Stop on unsuccessful outcome** initially.
   Choose **Prepare**, then **Review batch** for the captured messages, resolved endpoint,
   encoding, pacing and ACK policy.
4. Choose **Start** and authorize only that reviewed batch. Source/selection/endpoint
   changes before Start require preparation/review again.
5. Inspect every outcome. **Pause** stops the next admission after the active exchange;
   **Resume** continues remaining positions in the same authorized run, without resend.
6. After completion/stop/cancel, choose **Export report…** for metadata-only CSV.
   **Open captured message** and **Open ACK 1/2** provide separate explicit inspection.

Expected result: sequential fresh-connection attempts preserve captured source order.
Default Stop ends on any outcome lacking application acceptance, including missing/
suppressed ACK evidence. Continue is an explicit alternative, not a recovery retry.

## Try a bounded load test

1. Use **Controlled Load Testing** with a small synthetic source. Confirm the
   source contains synthetic test data, **Reload saved endpoints** and select the
   saved loopback endpoint.
2. Choose the source selection and conservative settings: concurrency 1, target
   1 attempt/second and a small attempt cap suitable for the receiver. Leave Replay off.
3. Choose **Prepare**, then **Review**. No socket is opened during preparation.
   Review global rate, duration, attempts, replay and stop thresholds.
4. Choose **Start**, confirm authority for that exact destination and complete any
   non-loopback confirmation. A private IP/Test endpoint label is not proof of safety.
5. Review final write/ACK/application, rejection, timeout, uncertainty and latency
   metrics. Use **Export CSV…** or **Export summary…** explicitly to retain reports.

Expected result: the first source/attempt/duration/safety limit ends admission.
Replay deliberately cycles identical captured IDs/content; it does not manufacture
new identities or insert failed attempts as retries. Concurrency can reorder clinical
events/completions. Achieved rate can be lower than the target.

## Stop, retry and respect receiver limits

Use batch **Cancel** or load **Stop / Cancel** to stop admitting attempts and drain
active work. Already admitted exchanges can finish after Stop. Complete local
writes, ACK receipt, commit CA and application AA mean different things; see the
[outcome table](test-with-mllp.md#interpret-outcomes-before-resending). Cancellation
is not proof of nondelivery. A new run needs fresh preparation/review/authorization.
Closing/resetting the tool drains its work; view reconstruction alone does not start another run.

Preparation is bounded to 1,000 messages/32 MiB and strict 1 MiB/message capture.
Load ceilings are eight concurrent exchanges, 20 attempts/second, 300 seconds and
5,000 attempts; thresholds can only be tightened. Detailed results retain the
first 1,000 completions, with separate exact aggregate statistics. Limits do not
guarantee receiver safety. The built-in listener separately defaults to four clients,
64 receipts and 16 MiB retained payloads; small runs avoid filling it. Clear its
history deliberately for longer authorized tests, rather than disabling limits.

Reports omit payload/control-ID values but still identify test endpoints and outcomes;
payload/ACK inspection documents and any HL7 exports can contain sensitive content.
TLS, scheduling, connection pooling, fallback and automatic reconnect/retry are unsupported.
See [batch policies/bounds](../batch-transmission.md) and
[load metrics/limits](../load-testing.md) before increasing traffic.
