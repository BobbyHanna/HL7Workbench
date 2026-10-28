using HL7Workbench.Querying;

namespace HL7Workbench.Workspace;

/// <summary>Workspace limits supplement, and never raise, the isolated SQL worker's hard caps.</summary>
public sealed record SqlQueryOptions
{
    public string WorkerPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll");
    public QueryBudgets Budgets { get; init; } = new();
    public QueryWorkerOptions Worker { get; init; } = new();
    public int MaximumDocuments { get; init; } = 16;
    public long MaximumSourceBytes { get; init; } = 1024L * 1024 * 1024;
    public long MaximumOutputBytes { get; init; } = 1024L * 1024 * 1024;
    public long MaximumCsvBytes { get; init; } = 256L * 1024 * 1024;
    public int MaximumMatchingMessages { get; init; } = 100_000;
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromMinutes(10);
    public string? StorageRoot { get; init; }
    internal void Validate()
    {
        if (MaximumDocuments is < 1 or > 16 || MaximumSourceBytes is < 1 or > 1024L * 1024 * 1024 ||
            MaximumOutputBytes is < 1 or > 1024L * 1024 * 1024 || MaximumCsvBytes is < 1 or > 256L * 1024 * 1024 ||
            MaximumMatchingMessages is < 1 or > 100_000 || TotalTimeout <= TimeSpan.Zero || TotalTimeout > TimeSpan.FromMinutes(10))
            throw new ArgumentOutOfRangeException(nameof(SqlQueryOptions));
    }
}
public sealed record SqlQueryProgress(string Phase, int Completed, int Total);
public sealed record SqlQueryMetrics(long HeaderCharacters, long BodyDecodedCharacters, long ValueRequests, int StructuredMessages, int ImportedRows);
public sealed record SqlSourceIdentity(string DocumentId, long Revision, long MessageId, int MessageNumber)
{ public override string ToString() => "Captured SQL source identity"; }
public sealed record SqlPreviewCell(QueryValueKind Kind, string Preview, bool Abbreviated)
{ public override string ToString() => "SQL presentation cell"; }
public sealed record SqlPreviewRow(int RowNumber, IReadOnlyList<SqlPreviewCell> Cells)
{ public override string ToString() => "SQL presentation row"; }
