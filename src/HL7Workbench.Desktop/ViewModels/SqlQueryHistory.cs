namespace HL7Workbench.Desktop.ViewModels;

public sealed record SqlQueryHistoryEntry(DateTimeOffset Time, string Sql, string Outcome, int Rows)
{
    public string Label => $"{Time.ToLocalTime():HH:mm:ss} · {Outcome} · {Rows:N0} rows";
    public override string ToString() => "Session SQL history entry";
}
/// <summary>Application-session history shared across tool reopen, never serialized. Both count and retained SQL characters are bounded.</summary>
public sealed class SqlQueryHistory
{
    private readonly List<SqlQueryHistoryEntry> entries = [];
    public IReadOnlyList<SqlQueryHistoryEntry> Entries => entries.ToArray();
    internal void Add(string sql, string outcome, int rows)
    {
        entries.Insert(0, new(DateTimeOffset.UtcNow, sql, outcome, rows));
        while (entries.Count > 32 || entries.Sum(e => e.Sql.Length) > 256 * 1024) entries.RemoveAt(entries.Count - 1);
    }
    public void Clear() => entries.Clear();
}
