using System.Globalization;
using System.Text.Json;
using HL7Workbench.Core;
using HL7Workbench.Querying;

namespace HL7Workbench.Workspace;

/// <summary>Complete query journal and its independent captured sources. Disposal cancels work and drains read leases before cleanup.</summary>
public sealed partial class SqlQueryResults : IDisposable
{
    private readonly SourceSnapshot journal;
    private readonly long[] offsets;
    private readonly SqlCapturedSources sources;
    private readonly SqlQueryOptions options;
    private readonly object gate = new();
    private readonly CancellationTokenSource closing = new();
    private int readers;
    private bool disposed;
    private readonly QueryColumnOrigin[] origins;
    public IReadOnlyList<string> Columns { get; }
    public int RowCount => offsets.Length;
    public TimeSpan Duration { get; }
    /// <summary>SQL preparation, execution and complete result transfer, excluding source capture/indexing and journal creation.</summary>
    public TimeSpan ExecutionDuration { get; }
    public SqlQueryMetrics Metrics { get; }
    public bool IsCurrent { get { lock (gate) return !disposed && sources.Documents.All(d => d.Original.TryGetTarget(out var original) && original.IsSearchCurrent(d.Revision)); } }
    public IReadOnlyList<SqlSourceIdentity> Sources => sources.Documents.SelectMany(d => d.Messages.Select(m => new SqlSourceIdentity(d.Id, d.Revision, m.Id, m.Number))).ToArray();
    private SqlQueryResults(SourceSnapshot journal, long[] offsets, SqlCapturedSources sources, SqlQueryOptions options,
        string[] columns, QueryColumnOrigin[] origins, SqlQueryMetrics metrics, TimeSpan duration, TimeSpan executionDuration)
    {
        this.journal = journal; this.offsets = offsets; this.sources = sources; this.options = options; this.origins = origins;
        Columns = Array.AsReadOnly(columns); Metrics = metrics; Duration = duration; ExecutionDuration = executionDuration;
    }
    internal static async Task<SqlQueryResults> CreateAsync(QueryResult result, SqlCapturedSources sources, SqlQueryOptions options,
        SqlQueryMetrics metrics, TimeSpan duration, CancellationToken token)
    {
        var offsets = new List<long>();
        var journal = await SourceSnapshot.CreateAsync(async (output, ct) =>
        {
            foreach (var row in result.Rows)
            {
                ct.ThrowIfCancellationRequested(); var bytes = JsonSerializer.SerializeToUtf8Bytes(row);
                if (bytes.Length > QueryLimits.MaximumFrameBytes) throw new QueryException(QueryError.MaterializationLimit);
                offsets.Add(output.Position); await output.WriteAsync(bytes, ct);
            }
        }, 128L * 1024 * 1024, token, options.StorageRoot);
        return new(journal, offsets.ToArray(), sources, options, result.Columns.ToArray(), result.Origins.ToArray(), metrics, duration, result.Duration);
    }
    private IDisposable ReadLease()
    {
        lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); readers++; return new Lease(this); }
    }
    private sealed class Lease(SqlQueryResults owner) : IDisposable
    {
        private SqlQueryResults? current = owner;
        public void Dispose()
        {
            var value = Interlocked.Exchange(ref current, null); if (value is null) return;
            lock (value.gate) { value.readers--; if (value.disposed && value.readers == 0) value.Cleanup(); }
        }
    }
    private QueryRow ReadCore(int index)
    {
        if (index < 0 || index >= RowCount) throw new ArgumentOutOfRangeException(nameof(index));
        var end = index + 1 == RowCount ? journal.Length : offsets[index + 1];
        return JsonSerializer.Deserialize<QueryRow>(journal.ReadBytes(new(offsets[index], end - offsets[index]), QueryLimits.MaximumFrameBytes)) ??
            throw new QueryException(QueryError.ProtocolError);
    }
    /// <summary>Reads one complete bounded row, never presentation abbreviations.</summary>
    public QueryRow ReadRow(int index) { using var lease = ReadLease(); return ReadCore(index); }
    public async Task<IReadOnlyList<SqlPreviewRow>> PageAsync(int page, CancellationToken token = default)
    {
        using var lease = ReadLease(); using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, closing.Token);
        if (page < 0 || page >= Math.Max(1, (RowCount + 99) / 100)) throw new ArgumentOutOfRangeException(nameof(page));
        return await Task.Run(() =>
        {
            var rows = new List<SqlPreviewRow>();
            for (var n = page * 100; n < Math.Min(RowCount, page * 100 + 100); n++)
            {
                linked.Token.ThrowIfCancellationRequested();
                rows.Add(new(n + 1, ReadCore(n).Cells.Select(c =>
                {
                    var text = Display(c); var abbreviated = text.Length > 256;
                    var count = Math.Min(text.Length, 256); if (count < text.Length && count > 0 && char.IsHighSurrogate(text[count - 1])) count--;
                    return new SqlPreviewCell(c.Kind, text[..count], abbreviated);
                }).ToArray()));
            }
            return (IReadOnlyList<SqlPreviewRow>)rows;
        }, linked.Token);
    }
    private static string Display(QueryCell cell) => cell.Kind switch
    {
        QueryValueKind.Null => "(SQL NULL)", QueryValueKind.Text => cell.Text!, QueryValueKind.Integer => cell.Integer.ToString(CultureInfo.InvariantCulture),
        QueryValueKind.Real => cell.Real.ToString("R", CultureInfo.InvariantCulture), QueryValueKind.Blob => Convert.ToHexString(cell.Blob!), _ => ""
    };
    /// <summary>Exports every complete result row, with type companions that distinguish SQL NULL from empty text.</summary>
    public Task ExportCsvAsync(string destination, CancellationToken token = default) => ExportCsvAsync(FileSaveDestination.NewFile(destination), token);
    public async Task ExportCsvAsync(FileSaveDestination destination, CancellationToken token = default)
        => await ExportCsvAsync(destination, null, token);
    /// <summary>Exports every row once in a supplied presentation order. A partial/duplicate permutation is rejected before publication.</summary>
    public async Task ExportCsvAsync(FileSaveDestination destination, IReadOnlyList<int>? order, CancellationToken token = default)
    {
        using var lease = ReadLease();
        var indices = order?.ToArray();
        if (indices is not null && (indices.Length != RowCount || indices.Any(i => i < 0 || i >= RowCount) || indices.Distinct().Count() != RowCount))
            throw new QueryException(QueryError.InvalidRequest);
        var guards = sources.Documents.SelectMany(d => d.ProtectedDestinations.Concat(d.Original.TryGetTarget(out var original) ? original.ExtractionDestinations() : []));
        if (destination.IsWithinDirectory(journal.DirectoryPath) || sources.Documents.Any(d => destination.IsWithinDirectory(d.Snapshot.DirectoryPath) ||
            d.OriginalStorage is not null && destination.IsWithinDirectory(d.OriginalStorage)) || guards.Any(destination.RefersToSameFile))
            throw new IOException("SQL export cannot replace a source or owned snapshot.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, closing.Token);
        await Task.Run(() => destination.WriteAsync(async (output, ct) =>
        {
            long bytes = 0;
            async Task Write(IEnumerable<string?> values)
            {
                var encoded = CsvRecordEncoder.Encode(values); bytes += encoded.Length;
                if (bytes > options.MaximumCsvBytes) throw new QueryException(QueryError.ResultLimit);
                await output.WriteAsync(encoded, ct);
            }
            await Write(Columns.SelectMany(c => new[] { c, c + " [Type]" }));
            for (var i = 0; i < RowCount; i++)
            {
                ct.ThrowIfCancellationRequested();
                await Write(ReadCore(indices?[i] ?? i).Cells.SelectMany(c => new[] { c.Kind == QueryValueKind.Null ? null : Display(c), c.Kind.ToString() }));
            }
        }, linked.Token), linked.Token);
    }
    private void Cleanup() { journal.Dispose(); sources.Dispose(); }
    public void Dispose()
    {
        lock (gate) { if (disposed) return; disposed = true; closing.Cancel(); if (readers == 0) Cleanup(); }
    }
}
