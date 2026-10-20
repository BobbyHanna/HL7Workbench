using HL7Workbench.Core;
using HL7Workbench.Scripting;

namespace HL7Workbench.Workspace;

public sealed record ScriptReportInfo(string Name, int Rows, long Bytes);

/// <summary>Successful job's sealed report journal. Explicit export is independent of document publication.
/// Dispose requests cleanup; active exports retain the snapshot until their reads drain.</summary>
public sealed class ScriptReports : IDisposable
{
    private readonly SourceSnapshot snapshot;
    private readonly IReadOnlyDictionary<string, List<ByteRange>> ranges;
    private readonly object gate = new();
    private int readers;
    private bool disposed;
    public IReadOnlyList<ScriptReportInfo> Items { get; }
    internal ScriptReports(SourceSnapshot snapshot, ScriptReportBuilder builder)
    { this.snapshot = snapshot; ranges = builder.Ranges; Items = builder.Items; }

    // A bounded projection of this same sealed journal, not a second report store.
    internal byte[]? ReadSmallReport(string name, int maximum)
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!ranges.TryGetValue(name, out var intervals) || intervals.Sum(r => r.Length) > maximum) return null;
            using var bytes = new MemoryStream();
            foreach (var interval in intervals) bytes.Write(snapshot.ReadBytes(interval, maximum));
            return bytes.ToArray();
        }
    }

    /// <summary>Exports to a new file only; a path alone never authorizes replacement.</summary>
    public Task ExportAsync(string name, string destination, CancellationToken token = default) => ExportAsync(name, FileSaveDestination.NewFile(destination), token);
    /// <summary>Exports using explicit, version-scoped destination consent.</summary>
    public async Task ExportAsync(string name, FileSaveDestination destination, CancellationToken token = default)
    {
        lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); readers++; }
        try
        {
            if (!ranges.TryGetValue(name, out var intervals)) throw new ArgumentException("Unknown report.", nameof(name));
            if (destination.IsWithinDirectory(snapshot.DirectoryPath)) throw new IOException("Export cannot replace owned report storage.");
            await destination.WriteAsync(async (output, cancellation) =>
            {
                foreach (var range in intervals) await ScriptExecution.CopyAsync(snapshot, range, output, cancellation).ConfigureAwait(false);
            }, token).ConfigureAwait(false);
        }
        finally
        {
            lock (gate) { if (--readers == 0 && disposed) snapshot.Dispose(); }
        }
    }
    public void Dispose()
    { lock (gate) { if (disposed) return; disposed = true; if (readers == 0) snapshot.Dispose(); } }
}

/// <summary>Streams interleaved report rows to one bounded snapshot; only bounded range metadata stays in memory.</summary>
internal sealed class ScriptReportBuilder
{
    internal Dictionary<string, List<ByteRange>> Ranges { get; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ScriptReportInfo> metadata = new(StringComparer.OrdinalIgnoreCase);
    private long bytes;
    private int rows;
    internal IReadOnlyList<ScriptReportInfo> Items => metadata.Values.ToArray();
    internal async Task AppendAsync(Stream stream, IReadOnlyList<ScriptReportChunk> chunks, CancellationToken token)
    {
        long operationBytes = 0;
        foreach (var chunk in chunks)
        {
            operationBytes += chunk.Bytes.Length;
            bytes += chunk.Bytes.Length;
            if (operationBytes > ScriptLimits.ReportOperationBytes || bytes > ScriptLimits.ReportBytes ||
                !ScriptBatch.ValidReportName(chunk.Name)) throw Failure("ReportLimit");
            if (chunk.Header)
            {
                if (Ranges.Count >= ScriptLimits.Reports || Ranges.ContainsKey(chunk.Name)) throw Failure("ReportSchema");
                Ranges.Add(chunk.Name, []); metadata.Add(chunk.Name, new(chunk.Name, 0, 0));
            }
            else if (++rows > ScriptLimits.ReportRows) throw Failure("ReportLimit");
            if (!Ranges.TryGetValue(chunk.Name, out var intervals)) throw Failure("ReportSchema");
            var range = new ByteRange(stream.Length, chunk.Bytes.Length);
            if (intervals.Count > 0 && intervals[^1].End == range.Offset)
                intervals[^1] = new(intervals[^1].Offset, intervals[^1].Length + range.Length);
            else intervals.Add(range);
            await stream.WriteAsync(chunk.Bytes, token).ConfigureAwait(false);
            var info = metadata[chunk.Name]; metadata[chunk.Name] = info with { Rows = info.Rows + (chunk.Header ? 0 : 1), Bytes = info.Bytes + chunk.Bytes.Length };
        }
    }
    private static ScriptRunException Failure(string code) => new(new ScriptDiagnostic(ScriptPhase.Output, code));
}
