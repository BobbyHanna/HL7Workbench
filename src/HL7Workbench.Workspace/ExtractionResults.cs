using System.Globalization;
using System.Text.Json;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Sealed private value journal plus bounded offset/identity metadata. Disposal waits for pending readers/export to drain.</summary>
public sealed class ExtractionResults : IDisposable
{
    private readonly SourceSnapshot snapshot;
    private readonly long[] offsets;
    private readonly Dictionary<long, MessageId> identities;
    private readonly WeakReference<MessageDocument> source;
    private readonly FileSaveDestination[] protectedDestinations;
    private readonly string? sourceStorage;
    private FileSaveDestination[] latestDestinations = [];
    private readonly ExtractionOptions options;
    private readonly object gate = new();
    private int readers; private bool disposed;
    public Guid Id { get; } = Guid.NewGuid();
    public string DocumentId { get; }
    public int DocumentNumber { get; }
    public long Revision { get; }
    public ExtractionConfiguration Configuration { get; }
    public ExtractionSummary Summary { get; }
    public int RowCount => offsets.Length;
    public long StoredBytes => snapshot.Length;
    public bool SourceIsOpen => source.TryGetTarget(out var doc) && doc.IsSearchCurrent(doc.SearchRevision);
    public bool IsCurrent => source.TryGetTarget(out var doc) && doc.IsSearchCurrent(Revision);
    public string SourceState => !SourceIsOpen ? "Source closed; captured results retained." : IsCurrent ? "Captured source is current." : "Stale results: source changed; rerun to refresh.";
    public string SourceDescription => $"Document {DocumentNumber} · captured revision {Revision}. {SourceState}";
    public ExtractionView AllRows { get; }
    internal MessageDocument? SourceDocument => source.TryGetTarget(out var doc) ? doc : null;
    internal void RefreshSourceProtection() => latestDestinations = SourceDocument?.ExtractionDestinations() ?? latestDestinations;
    internal void CheckSourceDestination(FileSaveDestination destination, IEnumerable<FileSaveDestination> additional)
    {
        if (additional.Any(destination.RefersToSameFile)) throw new IOException("CSV export cannot replace an open source document.");
    }
    internal MessageId? ResolveMessage(long key) => identities.TryGetValue(key, out var id) ? id : null;
    internal ExtractionResults(SourceSnapshot snapshot, long[] offsets, Dictionary<long, MessageId> identities,
        MessageDocument document, long revision, ExtractionConfiguration configuration, ExtractionSummary summary, ExtractionOptions options)
    {
        this.snapshot = snapshot; this.offsets = offsets; this.identities = identities; this.options = options;
        source = new(document); DocumentId = document.Id; DocumentNumber = document.ActivityNumber; Revision = revision;
        Configuration = configuration; Summary = summary with { Columns = Array.AsReadOnly(summary.Columns.ToArray()) };
        protectedDestinations = document.ExtractionDestinations(); AllRows = new(this, null);
        sourceStorage = document.Snapshot?.DirectoryPath;
    }
    private IDisposable ReadLease()
    {
        lock (gate) { ObjectDisposedException.ThrowIf(disposed, this); readers++; }
        return new Lease(() => { lock (gate) { if (--readers == 0 && disposed) snapshot.Dispose(); } });
    }
    private sealed class Lease(Action release) : IDisposable
    {
        private Action? callback = release;
        public void Dispose() => Interlocked.Exchange(ref callback, null)?.Invoke();
    }
    public ExtractionRow ReadRow(int index)
    { using var lease = ReadLease(); return ReadCore(index); }
    private ExtractionRow ReadCore(int index)
    {
        if (index < 0 || index >= RowCount) throw new ArgumentOutOfRangeException(nameof(index));
        var end = index + 1 == RowCount ? snapshot.Length : offsets[index + 1];
        var row = JsonSerializer.Deserialize<ExtractionRow>(snapshot.ReadBytes(new(offsets[index], end - offsets[index]), ExtractionOptions.MaximumRowBytes))
            ?? throw new InvalidDataException("Extraction row unavailable.");
        if (row.Values.Count != Configuration.Columns.Count || !identities.ContainsKey(row.MessageKey)) throw new InvalidDataException("Invalid extraction row.");
        return row;
    }
    internal Task<IReadOnlyList<ExtractionPreviewRow>> PageAsync(ExtractionView view, int page, CancellationToken token)
    {
        var lease = ReadLease();
        return Task.Run<IReadOnlyList<ExtractionPreviewRow>>(() =>
        {
            using (lease)
            {
                if (page < 0 || page >= view.PageCount) throw new ArgumentOutOfRangeException(nameof(page));
                var result = new List<ExtractionPreviewRow>();
                for (var n = page * ExtractionOptions.PageSize; n < Math.Min(view.Count, (page + 1) * ExtractionOptions.PageSize); n++)
                {
                    token.ThrowIfCancellationRequested(); var index = view.Index(n); var row = ReadCore(index);
                    result.Add(new(Id, index, row.MessageKey, row.CapturedPosition, row.Expansion, row.Values.Select(v =>
                    {
                        var length = Math.Min(ExtractionOptions.PreviewCharacters, v.Text.Length);
                        if (length < v.Text.Length && length > 0 && char.IsHighSurrogate(v.Text[length - 1])) length--;
                        return new ExtractionPreviewCell(v.Text[..length], v.StateLabel, v.Status.ToString(), v.Path, length < v.Text.Length);
                    }).ToArray()));
                }
                return result;
            }
        });
    }
    /// <summary>Filters/sorts only the sealed result journal, with bounded key caching and stable row-order ties.</summary>
    public Task<ExtractionView> CreateViewAsync(ExtractionFilter? filter = null, ExtractionSort? sort = null, CancellationToken token = default)
    {
        filter ??= new(0, ExtractionFilterOperator.None); sort ??= new();
        if (filter.Column < 0 || filter.Column >= Configuration.Columns.Count || !Enum.IsDefined(filter.Operator) ||
            filter.Literal.Length > MessageSearchQuery.MaximumQueryCharacters || sort.Column < -1 || sort.Column >= Configuration.Columns.Count)
            throw new ArgumentException("Choose a supported result filter and sort.");
        if (filter.Operator is ExtractionFilterOperator.NumericGreaterThan or ExtractionFilterOperator.NumericLessThan or ExtractionFilterOperator.NumericEquals && !Numeric(filter.Literal, out _))
            throw new ArgumentException("Use an invariant decimal comparison value.");
        var lease = ReadLease();
        return Task.Run(() =>
        {
            using (lease)
            {
                var indexes = new List<int>();
                for (var n = 0; n < RowCount; n++)
                { token.ThrowIfCancellationRequested(); if (filter.Operator == ExtractionFilterOperator.None || Accepts(ReadCore(n).Values[filter.Column], filter)) indexes.Add(n); }
                var ordered = indexes.ToArray();
                if (sort.Column >= 0)
                {
                    var cache = new KeyCache(this, sort.Column, options.SortCacheBytes);
                    try
                    {
                        Array.Sort(ordered, (a, b) =>
                        {
                            token.ThrowIfCancellationRequested(); var comparison = Compare(cache.Get(a), cache.Get(b), sort.Numeric);
                            return comparison == 0 ? a.CompareTo(b) : sort.Descending ? -Math.Sign(comparison) : Math.Sign(comparison);
                        });
                    }
                    catch (InvalidOperationException error) when (error.InnerException is OperationCanceledException)
                    { throw new OperationCanceledException(token); }
                }
                token.ThrowIfCancellationRequested(); return new ExtractionView(this, ordered);
            }
        });
    }
    private static bool Numeric(string text, out decimal value) => decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    private static bool Accepts(Hl7ExtractedValue value, ExtractionFilter filter)
    {
        if (filter.Operator == ExtractionFilterOperator.Missing) return value.State == Hl7ValueState.Missing && value.Status == Hl7ExtractionStatus.Complete;
        if (filter.Operator == ExtractionFilterOperator.Empty) return value.State == Hl7ValueState.Empty && value.Status == Hl7ExtractionStatus.Complete;
        if (filter.Operator == ExtractionFilterOperator.ExplicitNull) return value.State == Hl7ValueState.ExplicitNull && value.Status == Hl7ExtractionStatus.Complete;
        if (filter.Operator == ExtractionFilterOperator.Unavailable) return value.Status != Hl7ExtractionStatus.Complete;
        if (value.Status != Hl7ExtractionStatus.Complete || value.State is Hl7ValueState.Missing or Hl7ValueState.ExplicitNull) return false;
        if (filter.Operator == ExtractionFilterOperator.Contains) return value.Text.Contains(filter.Literal, StringComparison.OrdinalIgnoreCase);
        if (filter.Operator == ExtractionFilterOperator.Equals) return string.Equals(value.Text, filter.Literal, StringComparison.OrdinalIgnoreCase);
        if (!Numeric(value.Text, out var number) || !Numeric(filter.Literal, out var threshold)) return false;
        return filter.Operator switch { ExtractionFilterOperator.NumericGreaterThan => number > threshold,
            ExtractionFilterOperator.NumericLessThan => number < threshold, ExtractionFilterOperator.NumericEquals => number == threshold, _ => true };
    }
    private static int Compare(Hl7ExtractedValue a, Hl7ExtractedValue b, bool numeric)
    {
        int Rank(Hl7ExtractedValue v) => v.Status != Hl7ExtractionStatus.Complete ? 3 : v.State switch
        { Hl7ValueState.Missing => 0, Hl7ValueState.Empty => 1, Hl7ValueState.ExplicitNull => 2, _ => 4 };
        var rank = Rank(a).CompareTo(Rank(b)); if (rank != 0) return rank;
        if (numeric)
        {
            var an = Numeric(a.Text, out var av); var bn = Numeric(b.Text, out var bv);
            if (an && bn) return av.CompareTo(bv); if (an != bn) return an ? -1 : 1;
        }
        return StringComparer.OrdinalIgnoreCase.Compare(a.Text, b.Text);
    }
    private sealed class KeyCache(ExtractionResults owner, int column, int maximum)
    {
        private sealed record Entry(int Index, Hl7ExtractedValue Value, int Bytes);
        private readonly LinkedList<Entry> lru = new();
        private readonly Dictionary<int, LinkedListNode<Entry>> keys = []; private int bytes;
        internal Hl7ExtractedValue Get(int index)
        {
            if (keys.TryGetValue(index, out var found)) { lru.Remove(found); lru.AddFirst(found); return found.Value.Value; }
            var value = owner.ReadCore(index).Values[column]; var size = value.Text.Length * 2 + 256;
            if (size > maximum) return value;
            while (bytes + size > maximum && lru.Last is { } last) { keys.Remove(last.Value.Index); bytes -= last.Value.Bytes; lru.RemoveLast(); }
            keys.Add(index, lru.AddFirst(new Entry(index, value, size))); bytes += size; return value;
        }
    }
    internal Task ExportAsync(ExtractionView view, FileSaveDestination destination, IProgress<long>? progress, CancellationToken token)
    {
        var guards = protectedDestinations.Concat(latestDestinations).Concat(SourceDocument?.ExtractionDestinations() ?? []);
        if (destination.IsWithinDirectory(snapshot.DirectoryPath) || sourceStorage is not null && destination.IsWithinDirectory(sourceStorage) || guards.Any(destination.RefersToSameFile))
            throw new IOException("CSV export cannot replace source HL7 or owned result storage.");
        var lease = ReadLease();
        return Task.Run(async () =>
        {
            using (lease)
            {
                await destination.WriteAsync(async (output, ct) =>
                {
                    long bytes = 0;
                    async Task Write(IEnumerable<string?> cells)
                    {
                        var encoded = CsvRecordEncoder.Encode(cells); bytes += encoded.Length;
                        if (bytes > options.MaximumExportBytes) throw new IOException("CSV export size limit exceeded; no file published.");
                        await output.WriteAsync(encoded, ct).ConfigureAwait(false);
                    }
                    await Write(new[] { "Source Document", "Captured Revision", "Captured Message", "Message Identity", "Match" }
                        .Concat(Configuration.Columns.SelectMany(c => new[] { c.Label, c.Label + " State", c.Label + " Status", c.Label + " Path" })));
                    for (var n = 0; n < view.Count; n++)
                    {
                        ct.ThrowIfCancellationRequested(); var row = ReadCore(view.Index(n));
                        await Write(new[] { DocumentNumber.ToString(CultureInfo.InvariantCulture), Revision.ToString(CultureInfo.InvariantCulture),
                            row.CapturedPosition.ToString(CultureInfo.InvariantCulture), row.MessageKey.ToString(CultureInfo.InvariantCulture), row.Expansion.ToString(CultureInfo.InvariantCulture) }
                            .Concat(row.Values.SelectMany(v => new[] { v.Text, v.StateLabel, v.Status.ToString(), v.Path })));
                        progress?.Report(n + 1);
                    }
                }, token).ConfigureAwait(false);
            }
        });
    }
    public void Dispose()
    { lock (gate) { if (disposed) return; disposed = true; if (readers == 0) snapshot.Dispose(); } }
}

/// <summary>Immutable filtered/sorted row index; all pages and CSV use this same selection/order.</summary>
public sealed class ExtractionView
{
    private readonly ExtractionResults owner;
    private readonly int[]? rows;
    public int Count => rows?.Length ?? owner.RowCount;
    public int PageCount => Math.Max(1, (Count + ExtractionOptions.PageSize - 1) / ExtractionOptions.PageSize);
    internal ExtractionView(ExtractionResults owner, int[]? rows) { this.owner = owner; this.rows = rows; }
    internal int Index(int ordinal) => rows is null ? ordinal : rows[ordinal];
    public Task<IReadOnlyList<ExtractionPreviewRow>> PageAsync(int page, CancellationToken token = default) => owner.PageAsync(this, page, token);
    public Task ExportAsync(string destination, IProgress<long>? progress = null, CancellationToken token = default) => ExportAsync(FileSaveDestination.NewFile(destination), progress, token);
    public Task ExportAsync(FileSaveDestination destination, IProgress<long>? progress = null, CancellationToken token = default) => owner.ExportAsync(this, destination, progress, token);
}
