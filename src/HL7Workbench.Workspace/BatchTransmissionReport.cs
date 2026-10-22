using System.Globalization;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed partial class BatchTransmissionSession
{
    /// <summary>Explicit atomic CSV publication of a completed/canceled batch. Excludes source
    /// titles/paths, profile names/notes/hostnames, HL7 identifiers, payloads and exception text.</summary>
    public Task ExportAsync(FileSaveDestination destination, CancellationToken token = default)
    {
        PreparedTransmissionBatch batch; BatchTransmissionProgress progress;
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (IsActive || state is BatchTransmissionState.Empty or BatchTransmissionState.Prepared || prepared is null)
                throw new InvalidOperationException("Finish or cancel a batch before exporting its report.");
            batch = prepared; progress = Progress;
        }
        var guards = batch.ProtectedDestinations.Concat(documents.Documents.SelectMany(d => d.ExtractionDestinations()));
        if (guards.Any(destination.RefersToSameFile) || batch.SourceStorage is { } storage && destination.IsWithinDirectory(storage))
            throw new IOException("Report cannot overwrite an HL7 source or its owned storage.");
        // Capture only whitelist metadata before background writing; neither later UI changes
        // nor tool disposal can retarget the chosen report, destination or batch.
        return Task.Run(() => destination.WriteAsync(async (stream, ct) =>
        {
            long size = 0;
            async Task Write(IEnumerable<string> cells)
            {
                var bytes = CsvRecordEncoder.Encode(cells); size += bytes.Length;
                if (size > 4 * 1024 * 1024) throw new IOException("Report byte limit exceeded; nothing published.");
                await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
            }
            await Write(["RecordType", "BatchId", "DocumentNumber", "SourceRevision", "Position", "ProfileId", "DestinationAddress", "Port", "Scope", "Security", "Encoding", "PacingMs", "Policy", "StartedUtc", "EndedUtc", "DurationMs", "WriteStarted", "WriteCompleted", "AckFrames", "CommitStatus", "ApplicationStatus", "Correlation", "Outcome", "Classification", "FailureStage", "SocketError", "Total", "Attempted", "Accepted", "RejectedError", "TimedOut", "Uncertain", "Canceled", "NotAttempted"]);
            string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
            string[] Common(string kind, string position) => [kind, batch.Id.ToString("D"), Number(batch.DocumentNumber), Number(batch.Revision), position,
                batch.Endpoint.Profile.Id.ToString("D"), batch.Endpoint.Address.ToString(), Number(batch.Endpoint.Profile.Port), batch.Endpoint.Scope.ToString(),
                batch.Endpoint.Profile.Security.ToString(), batch.Endpoint.Profile.Encoding.ToString(), Number(batch.Options.IntervalMilliseconds), batch.Options.Policy.ToString()];
            foreach (var row in progress.Results)
            {
                ct.ThrowIfCancellationRequested(); var e = row.Exchange;
                await Write(Common("Message", Number(row.Position)).Concat(new[] { row.Started?.ToString("O", CultureInfo.InvariantCulture) ?? "", row.Ended?.ToString("O", CultureInfo.InvariantCulture) ?? "",
                    e?.Duration.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture) ?? "", e?.WriteStarted.ToString() ?? "False", e?.WriteCompleted.ToString() ?? "False",
                    Number(row.AckFrames), row.CommitStatus, row.ApplicationStatus, row.Correlation, e?.Outcome.ToString() ?? "NotAttempted", row.Classification.ToString(),
                    e?.FailureStage.ToString() ?? "None", e?.SocketError?.ToString() ?? "" }).Concat(Enumerable.Repeat("", 8)));
            }
            await Write(Common("Summary", "").Concat(Enumerable.Repeat("", 13)).Concat(new[] { Number(progress.Total), Number(progress.Attempted), Number(progress.Accepted),
                Number(progress.RejectedError), Number(progress.TimedOut), Number(progress.Uncertain), Number(progress.Canceled), Number(progress.NotAttempted) }));
        }, token), token);
    }
}
