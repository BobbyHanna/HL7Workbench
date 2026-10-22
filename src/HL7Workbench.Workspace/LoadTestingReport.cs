using System.Globalization;
using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed partial class LoadTestingSession
{
    /// <summary>Explicit version-consented atomic export. Only numeric/typed metadata enters the report.</summary>
    public Task ExportAsync(FileSaveDestination destination, bool summaryOnly = false, CancellationToken token = default)
    {
        PreparedLoadTest run; LoadTestingProgress progress;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (IsActive || prepared is null || state == LoadTestingState.Prepared) throw new InvalidOperationException("Stop and drain before exporting.");
            run = prepared; progress = Progress;
        }
        var guards = run.Batch.ProtectedDestinations.Concat(documents.Documents.SelectMany(d => d.ExtractionDestinations()));
        if (guards.Any(destination.RefersToSameFile) || run.Batch.SourceStorage is { } storage && destination.IsWithinDirectory(storage))
            throw new IOException("Reports cannot overwrite source documents or owned storage.");
        return Task.Run(() => destination.WriteAsync(async (stream, ct) =>
        {
            if (summaryOnly)
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(FormatSummary(run, progress)), ct).ConfigureAwait(false); return;
            }
            var columns = new[] { "RecordType", "RunId", "Scope", "Security", "Encoding", "Concurrency", "TargetPerSecond", "DurationLimitSeconds", "AttemptLimit", "Replay", "ConsecutiveFailureLimit", "RejectionPercent", "TimeoutPercent", "MinimumOutcomeSamples", "DocumentNumber", "SourceRevision", "SourceMessages", "CapturedPayloadBytes",
                "SourcePosition", "Attempt", "CompletionOrder", "Replayed", "StartedUtc", "EndedUtc", "Connected", "CompleteWrite", "AckFrames", "Commit", "Application", "Correlation", "Outcome", "Uncertain", "ConnectMs", "WriteMs", "AckMs", "ExchangeMs",
                "StopReason", "Attempts", "Completed", "Connections", "CompleteWrites", "TotalAckFrames", "CommitAccepted", "CommitRejectedError", "ApplicationAccepted", "ApplicationRejectedError", "MissingMismatched", "Timeouts", "TransportFailures", "Canceled", "UncertainCount", "OmittedResults", "ElapsedSeconds", "AttemptRate", "CompletionRate", "AcceptedRate", "Limitations" }
                .Concat(new[] { "Connect", "Write", "Ack", "Exchange" }.SelectMany(phase => new[] { "Samples", "MinMs", "MaxMs", "AverageMs", "P50Ms", "P95Ms", "P99Ms" }.Select(metric => phase + metric))).ToArray();
            long size = 0;
            async Task Write(IEnumerable<string> cells)
            {
                ct.ThrowIfCancellationRequested(); var bytes = CsvRecordEncoder.Encode(cells); size += bytes.Length;
                if (size > 4 * 1024 * 1024) throw new IOException("Report limit exceeded; no partial report published.");
                await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            }
            Dictionary<string, string> Common(string kind) => new()
            {
                ["RecordType"] = kind, ["RunId"] = run.Id.ToString("D"), ["Scope"] = run.Endpoint.Scope.ToString(), ["Security"] = run.Endpoint.Profile.Security.ToString(), ["Encoding"] = run.Endpoint.Profile.Encoding.ToString(),
                ["Concurrency"] = Number(run.Options.Concurrency), ["TargetPerSecond"] = Number(run.Options.MessagesPerSecond), ["DurationLimitSeconds"] = Number(run.Options.DurationSeconds), ["AttemptLimit"] = Number(run.Options.AttemptLimit), ["Replay"] = run.Options.Replay.ToString(),
                ["ConsecutiveFailureLimit"] = Number(run.Options.ConsecutiveFailureLimit), ["RejectionPercent"] = Number(run.Options.RejectionPercent), ["TimeoutPercent"] = Number(run.Options.TimeoutPercent), ["MinimumOutcomeSamples"] = Number(run.Options.MinimumOutcomeSamples),
                ["DocumentNumber"] = Number(run.DocumentNumber), ["SourceRevision"] = Number(run.Revision), ["SourceMessages"] = Number(run.SourceCount), ["CapturedPayloadBytes"] = Number(run.PayloadBytes)
            };
            await Write(columns);
            foreach (var row in progress.Results)
            {
                var e = row.Result.Exchange!; var values = Common("Exchange");
                values["SourcePosition"] = Number(row.SourcePosition); values["Attempt"] = Number(row.Attempt); values["CompletionOrder"] = Number(row.CompletionOrder);
                values["Replayed"] = row.Replayed.ToString();
                values["StartedUtc"] = row.Started.ToString("O", CultureInfo.InvariantCulture); values["EndedUtc"] = row.Ended.ToString("O", CultureInfo.InvariantCulture);
                values["Connected"] = row.Connected.ToString(); values["CompleteWrite"] = e.WriteCompleted.ToString(); values["AckFrames"] = Number(row.Result.AckFrames);
                values["Commit"] = row.Result.CommitStatus; values["Application"] = row.Result.ApplicationStatus; values["Correlation"] = row.Result.Correlation;
                values["Outcome"] = e.Outcome.ToString(); values["Uncertain"] = row.Result.Uncertain.ToString(); values["ConnectMs"] = Milliseconds(row.ConnectionDuration);
                values["WriteMs"] = Milliseconds(e.WriteDuration); values["AckMs"] = Milliseconds(e.AckRoundTrip); values["ExchangeMs"] = Milliseconds(e.Duration);
                await Write(columns.Select(c => values.GetValueOrDefault(c, "")));
            }
            var total = Common("Summary");
            total["StartedUtc"] = progress.Started?.ToString("O", CultureInfo.InvariantCulture) ?? ""; total["EndedUtc"] = progress.Ended?.ToString("O", CultureInfo.InvariantCulture) ?? "";
            total["StopReason"] = progress.StopReason.ToString(); total["Attempts"] = Number(progress.Attempts); total["Completed"] = Number(progress.Completed);
            total["Connections"] = Number(progress.Connected); total["CompleteWrites"] = Number(progress.CompleteWrites); total["TotalAckFrames"] = Number(progress.AckFrames);
            total["CommitAccepted"] = Number(progress.CommitAccepted); total["CommitRejectedError"] = Number(progress.CommitRejectedError); total["ApplicationAccepted"] = Number(progress.ApplicationAccepted); total["ApplicationRejectedError"] = Number(progress.ApplicationRejectedError);
            total["MissingMismatched"] = Number(progress.MissingOrMismatched); total["Timeouts"] = Number(progress.Timeouts); total["TransportFailures"] = Number(progress.TransportFailures);
            total["Canceled"] = Number(progress.Canceled); total["UncertainCount"] = Number(progress.Uncertain); total["OmittedResults"] = Number(progress.OmittedResults);
            total["ElapsedSeconds"] = Number(progress.Elapsed.TotalSeconds); total["AttemptRate"] = Number(progress.AttemptRate); total["CompletionRate"] = Number(progress.CompletionRate); total["AcceptedRate"] = Number(progress.AcceptedRate);
            total["Limitations"] = "First 1000 completions retained; aggregates include all attempts. Percentiles use nearest rank and require 2/20/100 samples for p50/p95/p99. Uncertain overlaps other outcomes. TCP writes and commit ACKs do not establish application acceptance. Cancellation does not prove nondelivery. Replay keeps original control IDs; concurrency can reorder clinical events. No retry or durable delivery journal.";
            void Stats(string name, LoadLatencyStatistics stats)
            {
                total[name + "Samples"] = Number(stats.Count); total[name + "MinMs"] = Optional(stats.MinimumMs); total[name + "MaxMs"] = Optional(stats.MaximumMs);
                total[name + "AverageMs"] = Optional(stats.AverageMs); total[name + "P50Ms"] = Optional(stats.P50Ms); total[name + "P95Ms"] = Optional(stats.P95Ms); total[name + "P99Ms"] = Optional(stats.P99Ms);
            }
            Stats("Connect", progress.ConnectionLatency); Stats("Write", progress.WriteLatency); Stats("Ack", progress.AckLatency); Stats("Exchange", progress.ExchangeLatency);
            await Write(columns.Select(c => total.GetValueOrDefault(c, "")));
        }, token), token);
    }
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    private static string Number(double value) => value.ToString("0.######", CultureInfo.InvariantCulture);
    private static string Optional(double? value) => value is { } number ? Number(number) : "";
    private static string Milliseconds(TimeSpan? value) => value is { } span ? Number(span.TotalMilliseconds) : "";
    public static string FormatLatency(string label, LoadLatencyStatistics value) =>
        $"{label} ({value.Count} samples): min {Optional(value.MinimumMs)} / max {Optional(value.MaximumMs)} / avg {Optional(value.AverageMs)} ms; p50 {value.P50Ms?.ToString("F2", CultureInfo.InvariantCulture) ?? "insufficient samples"}, p95 {value.P95Ms?.ToString("F2", CultureInfo.InvariantCulture) ?? "insufficient samples"}, p99 {value.P99Ms?.ToString("F2", CultureInfo.InvariantCulture) ?? "insufficient samples"} ms";
    private static string FormatSummary(PreparedLoadTest run, LoadTestingProgress p) =>
        $"Controlled load test summary v1\nRun {run.Id:D}; destination classification {run.Endpoint.Scope}, {run.Endpoint.Profile.Security}, {run.Endpoint.Profile.Encoding}.\n" +
        $"Captured Document {run.DocumentNumber}, revision {run.Revision}; {run.SourceCount} messages; {run.PayloadBytes} bytes. Source positions: {string.Join(", ", run.Positions)}.\n" +
        $"Concurrency {run.Options.Concurrency}; global target {Number(run.Options.MessagesPerSecond)}/s; duration limit {run.Options.DurationSeconds}s; attempt limit {run.Options.AttemptLimit}; replay {run.Options.Replay}.\n" +
        $"Stop thresholds: consecutive transport failures {run.Options.ConsecutiveFailureLimit}; rejection {run.Options.RejectionPercent}%; timeout {run.Options.TimeoutPercent}%; minimum outcomes {run.Options.MinimumOutcomeSamples}.\n" +
        $"UTC {p.Started:O} to {p.Ended:O}; elapsed {Number(p.Elapsed.TotalSeconds)}s; stop {p.StopReason}.\n" +
        $"Attempts {p.Attempts}; completed {p.Completed}; connections {p.Connected}; complete writes {p.CompleteWrites}; ACK frames {p.AckFrames}; commit accepted {p.CommitAccepted}; commit rejected/error {p.CommitRejectedError}.\n" +
        $"Application accepted {p.ApplicationAccepted}; application rejected/error {p.ApplicationRejectedError}; missing/mismatched {p.MissingOrMismatched}; timeouts {p.Timeouts}; transport failures {p.TransportFailures}; canceled {p.Canceled}; uncertain {p.Uncertain}.\n" +
        $"Achieved: attempts {Number(p.AttemptRate)}/s; completed {Number(p.CompletionRate)}/s; application accepted {Number(p.AcceptedRate)}/s.\n" +
        string.Join('\n', new[] { FormatLatency("Connect", p.ConnectionLatency), FormatLatency("Write", p.WriteLatency), FormatLatency("ACK round-trip", p.AckLatency), FormatLatency("End-to-end", p.ExchangeLatency) }) +
        $"\nIndividual results retained {p.Results.Count}; omitted {p.OmittedResults}. Aggregates and bounded latency samples include every completed attempt.\n" +
        "Percentiles: nearest rank; p50/p95/p99 require 2/20/100 samples. Uncertain overlaps other outcomes. Complete TCP writes/commit ACKs are not application acceptance. Cancellation does not prove nondelivery. Replay retains original control IDs and can be rejected as duplicate. Concurrency can reorder clinical events. No retries or durable delivery journal.\n";
}
