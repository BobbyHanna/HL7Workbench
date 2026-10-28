using HL7Workbench.Querying;

namespace HL7Workbench.Workspace;

public sealed partial class SqlQueryResults
{
    /// <summary>Validates one complete journal row against the captured identity registry. Never infers identity from position or control IDs.</summary>
    public SqlSourceIdentity? SourceForRow(int index)
    {
        using var lease = ReadLease();
        if (IdentityColumns() is not { } columns) return null;
        var row = ReadCore(index); var d = row.Cells[columns.Document]; var m = row.Cells[columns.Message];
        return d.Kind == QueryValueKind.Text && m.Kind == QueryValueKind.Integer && sources.Identities.TryGetValue((d.Text!, m.Integer), out var source)
            ? new(source.Document.Id, source.Document.Revision, source.Message.Id, source.Message.Number) : null;
    }
    /// <summary>Builds a bounded stable presentation permutation over all journal rows. Values stay within the original complete-result budgets.
    /// Text uses ordinal comparison; use SQL ORDER BY when a SQLite collation is required. Extraction remains in captured source order.</summary>
    public async Task<int[]> SortOrderAsync(int column, bool descending, CancellationToken token = default)
    {
        using var lease = ReadLease(); using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, closing.Token);
        if (column < 0 || column >= Columns.Count) throw new ArgumentOutOfRangeException(nameof(column));
        return await Task.Run(() =>
        {
            var keys = new QueryCell[RowCount]; var order = new int[RowCount];
            for (var i = 0; i < RowCount; i++) { linked.Token.ThrowIfCancellationRequested(); keys[i] = ReadCore(i).Cells[column]; order[i] = i; }
            var comparisons = 0;
            try { Array.Sort(order, (a, b) =>
            {
                if (++comparisons % 1024 == 0) linked.Token.ThrowIfCancellationRequested();
                var c = Compare(keys[a], keys[b]); if (descending) c = -Math.Sign(c);
                return c == 0 ? a.CompareTo(b) : c;
            }); }
            catch (InvalidOperationException error) when (error.InnerException is OperationCanceledException) { linked.Token.ThrowIfCancellationRequested(); throw; }
            linked.Token.ThrowIfCancellationRequested(); return order;
        }, linked.Token);
    }
    private static int Compare(QueryCell a, QueryCell b)
    {
        static int Rank(QueryValueKind kind) => kind switch { QueryValueKind.Null => 0, QueryValueKind.Integer or QueryValueKind.Real => 1, QueryValueKind.Text => 2, _ => 3 };
        var c = Rank(a.Kind).CompareTo(Rank(b.Kind)); if (c != 0) return c;
        if (a.Kind == QueryValueKind.Null) return 0;
        if (a.Kind == QueryValueKind.Text) return StringComparer.Ordinal.Compare(a.Text, b.Text);
        if (a.Kind == QueryValueKind.Blob) return a.Blob.AsSpan().SequenceCompareTo(b.Blob);
        if (a.Kind == QueryValueKind.Integer && b.Kind == QueryValueKind.Integer) return a.Integer.CompareTo(b.Integer);
        if (a.Kind == QueryValueKind.Real && b.Kind == QueryValueKind.Real) return a.Real.CompareTo(b.Real);
        // Comparing via double alone would collapse distinct integers above 2^53.
        static int IntegerReal(long integer, double real)
        {
            if (real >= 9223372036854775808d) return -1;
            if (real < long.MinValue) return 1;
            var truncated = (long)real; var comparison = integer.CompareTo(truncated);
            return comparison != 0 ? comparison : ((double)integer).CompareTo(real);
        }
        return a.Kind == QueryValueKind.Integer ? IntegerReal(a.Integer, b.Real) : -IntegerReal(b.Integer, a.Real);
    }
    /// <summary>Reads a bounded presentation page using a previously validated complete-row permutation.</summary>
    public async Task<IReadOnlyList<SqlPreviewRow>> OrderedPageAsync(int page, IReadOnlyList<int>? order, CancellationToken token = default)
    {
        if (order is null) return await PageAsync(page, token);
        using var lease = ReadLease(); using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, closing.Token);
        if (order.Count != RowCount || page < 0 || page >= Math.Max(1, (RowCount + 99) / 100)) throw new ArgumentOutOfRangeException(nameof(page));
        var indices = order.Skip(page * 100).Take(100).ToArray();
        return await Task.Run(() => (IReadOnlyList<SqlPreviewRow>)indices.Select(index =>
        {
            linked.Token.ThrowIfCancellationRequested();
            return new SqlPreviewRow(index + 1, ReadCore(index).Cells.Select(cell =>
            {
                var text = Display(cell); var count = Math.Min(text.Length, 256);
                if (count < text.Length && count > 0 && char.IsHighSurrogate(text[count - 1])) count--;
                return new SqlPreviewCell(cell.Kind, text[..count], count < text.Length);
            }).ToArray());
        }).ToArray(), linked.Token);
    }
}
