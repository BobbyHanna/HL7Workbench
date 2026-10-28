using System.Text;

namespace HL7Workbench.Querying;

public enum QueryValueKind { Null, Integer, Real, Text, Blob }

/// <summary>A complete materialized SQL scalar. Text is never a viewer preview.</summary>
public sealed record QueryCell(QueryValueKind Kind, long Integer = 0, double Real = 0, string? Text = null, byte[]? Blob = null)
{
    public static QueryCell Null => new(QueryValueKind.Null);
    public static QueryCell From(long value) => new(QueryValueKind.Integer, Integer: value);
    public static QueryCell From(double value) => new(QueryValueKind.Real, Real: value);
    public static QueryCell From(string? value) => value is null ? Null : new(QueryValueKind.Text, Text: value);
    public override string ToString() => $"SQL scalar ({Kind})";
    internal int Size()
    {
        if (!Enum.IsDefined(Kind) || !double.IsFinite(Real) ||
            Kind == QueryValueKind.Text && Text is null || Kind == QueryValueKind.Blob && Blob is null ||
            Kind != QueryValueKind.Text && Text is not null || Kind != QueryValueKind.Blob && Blob is not null)
            throw new QueryException(QueryError.InvalidRequest);
        if (Text?.Length > QueryLimits.MaximumValueCharacters || Blob?.Length > QueryLimits.MaximumValueBytes)
            throw new QueryException(QueryError.MaterializationLimit);
        try { return checked(32 + (Text is null ? 0 : QueryLimits.Utf8.GetByteCount(Text)) + (Blob?.Length ?? 0)); }
        catch (EncoderFallbackException) { throw new QueryException(QueryError.InvalidRequest); }
    }
}

public sealed record QueryRow(QueryCell[] Cells)
{
    public override string ToString() => "SQL row";
    internal int Size()
    {
        if (Cells is null || Cells.Length > QueryLimits.MaximumColumns || Cells.Any(c => c is null))
            throw new QueryException(QueryError.InvalidRequest);
        var size = checked(32 + Cells.Sum(c => c.Size()));
        if (size > QueryLimits.MaximumRowBytes) throw new QueryException(QueryError.MaterializationLimit);
        return size;
    }
}

public sealed record QueryParameter(string Name, QueryCell Value)
{ public override string ToString() => "SQL parameter"; }

/// <summary>Only successful, complete results escape the worker client. A resource limit publishes no partial result.</summary>
public sealed record QueryResult(string[] Columns, IReadOnlyList<QueryRow> Rows, TimeSpan Duration, long MaterializedBytes)
{
    public IReadOnlyList<QueryColumnOrigin> Origins { get; init; } = [];
    public override string ToString() => $"Complete SQL result ({Rows.Count} rows)";
}

public sealed record QueryColumnOrigin(string? Table, string? Column)
{ public override string ToString() => "SQL column provenance"; }
public sealed record QueryPlan(QueryTable[] Tables)
{ public override string ToString() => "SQL read plan"; }
public enum QuerySourceOperation { Value, Raw, State, Status }
/// <summary>A bounded read from a captured, document-qualified source registry; never a filename or arbitrary byte request.</summary>
public sealed record QuerySourceRead(QuerySourceOperation Operation, string DocumentId, long MessageId, string Path)
{
    public override string ToString() => "Captured HL7 value request";
    internal void Validate()
    {
        if (!Enum.IsDefined(Operation) || DocumentId is not { Length: > 0 and <= 128 } || MessageId < 1 || Path is not { Length: > 0 and <= 256 })
            throw new QueryException(QueryError.InvalidRequest);
    }
}

public enum QueryError
{
    InvalidRequest, InvalidSql, InvalidValue, NotAuthorized, MultipleStatements, ParameterMismatch,
    ImportLimit, ImportRejected, MaterializationLimit, ResultLimit, ResourceLimit, InvalidPath, SourceUnavailable,
    Timeout, WorkerUnavailable, WorkerStopped, ProtocolError
}

/// <summary>Fixed diagnostics deliberately exclude SQLite error text, SQL, parameters, names, and result values.</summary>
public sealed class QueryException(QueryError code) : Exception(Describe(code))
{
    public QueryError Code { get; } = code;
    private static string Describe(QueryError code) => code switch
    {
        QueryError.InvalidSql => "SQL could not be prepared. Check syntax and the documented schema.",
        QueryError.InvalidValue => "SQL produced a value that cannot be represented without loss; no partial result was returned.",
        QueryError.InvalidPath => "Use a valid Core HL7 field path.",
        QueryError.SourceUnavailable => "The captured source identity or complete value is unavailable.",
        QueryError.NotAuthorized => "This SQL operation, relation, column, or function is not allowed.",
        QueryError.MultipleStatements => "Run one read-only result statement at a time.",
        QueryError.ParameterMismatch => "Supply each named SQL parameter exactly once.",
        QueryError.ImportLimit => "The relational input exceeded its row or materialization budget.",
        QueryError.ImportRejected => "A relational input row violated the schema; the session was discarded.",
        QueryError.MaterializationLimit => "A complete SQL value or row exceeds the materialization limit; no value was truncated.",
        QueryError.ResultLimit => "The result exceeded its row or byte budget; no partial result was returned. Narrow the query or use LIMIT.",
        QueryError.ResourceLimit => "SQLite exceeded its memory, expression, or execution budget.",
        QueryError.Timeout => "The query worker exceeded its deadline and was terminated.",
        QueryError.WorkerUnavailable => "The query worker could not be started with the required containment.",
        QueryError.WorkerStopped => "The query worker stopped; no partial result was returned.",
        QueryError.ProtocolError => "The query worker returned an invalid response.",
        _ => "The query request is invalid."
    };
}

public static class QueryLimits
{
    public const int MaximumSqlCharacters = 65_536;
    public const int MaximumColumns = 64;
    // These bound explicitly materialized scalars, not the size of an HL7 source field.
    public const int MaximumValueCharacters = 65_536;
    public const int MaximumValueBytes = 256 * 1024;
    public const int MaximumRowBytes = 512 * 1024;
    public const int MaximumFrameBytes = 2 * 1024 * 1024;
    public const int MaximumImportBatchRows = 128;
    public const int MaximumPageRows = 100;
    public const int MaximumPageBytes = 512 * 1024;
    public const int MaximumResultRows = 100_000;
    public const int MaximumResultBytes = 16 * 1024 * 1024;
    public const int MaximumImportedRows = 250_000;
    public const int MaximumImportBytes = 64 * 1024 * 1024;
    public const int MaximumParameters = 64;
    public const int MaximumSourceReads = 100_000;
    public const long NativeHeapBytes = 128L * 1024 * 1024;
    public const long DatabaseBytes = 64L * 1024 * 1024;
    public const long WorkerMemoryBytes = 512L * 1024 * 1024;
    internal static readonly UTF8Encoding Utf8 = new(false, true);
}

/// <summary>Per-session budgets may only lower hard caps. Deadlines include preparation and all result pages.</summary>
public sealed record QueryBudgets
{
    public int ResultRows { get; init; } = QueryLimits.MaximumResultRows;
    public int ResultBytes { get; init; } = QueryLimits.MaximumResultBytes;
    public int ImportedRows { get; init; } = QueryLimits.MaximumImportedRows;
    public int ImportBytes { get; init; } = QueryLimits.MaximumImportBytes;
    public int QueryMilliseconds { get; init; } = 30_000;
    public int ProgressCallbacks { get; init; } = 100_000;
    internal void Validate()
    {
        if (ResultRows is < 1 or > QueryLimits.MaximumResultRows || ResultBytes is < 1 or > QueryLimits.MaximumResultBytes ||
            ImportedRows is < 1 or > QueryLimits.MaximumImportedRows || ImportBytes is < 1 or > QueryLimits.MaximumImportBytes ||
            QueryMilliseconds is < 1 or > 30_000 || ProgressCallbacks is < 1 or > 100_000)
            throw new QueryException(QueryError.InvalidRequest);
    }
}

public sealed record QueryWorkerOptions
{
    /// <summary>Total allowance for pipe connection and initialization through Ready.</summary>
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>Per-exchange allowance after the worker has reported Ready.</summary>
    public TimeSpan OperationTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan SessionTimeout { get; init; } = TimeSpan.FromMinutes(10);
    internal void Validate()
    {
        if (StartupTimeout <= TimeSpan.Zero || StartupTimeout > TimeSpan.FromSeconds(30) ||
            OperationTimeout <= TimeSpan.Zero || OperationTimeout > TimeSpan.FromSeconds(30) ||
            SessionTimeout <= TimeSpan.Zero || SessionTimeout > TimeSpan.FromMinutes(10))
            throw new QueryException(QueryError.InvalidRequest);
    }
}
