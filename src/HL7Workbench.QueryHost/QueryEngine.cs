using System.Diagnostics;
using HL7Workbench.Querying;
using SQLitePCL;

namespace HL7Workbench.QueryHost;

/// <summary>One private database, owned by one worker. Only this class can prepare trusted import SQL.</summary>
internal sealed partial class QueryEngine : IDisposable
{
    private readonly sqlite3 db;
    private readonly QueryBudgets budgets;
    private readonly CancellationToken cancellation;
    private readonly Dictionary<QueryTable, sqlite3_stmt> inserts = [];
    private sqlite3_stmt? query;
    private readonly Stopwatch clock = new();
    private int importedRows, resultRows, callbacks;
    private long importedBytes, resultBytes;
    private bool sealedInput, complete, deadline, instructions, denied;
    private static readonly HashSet<string> Functions = new(StringComparer.OrdinalIgnoreCase)
    {
        "abs", "avg", "coalesce", "count", "glob", "ifnull", "iif", "if", "instr", "length", "like", "lower",
        "ltrim", "max", "min", "nullif", "replace", "round", "rtrim", "substr", "substring", "sum", "total", "trim",
        "typeof", "upper", "unicode", "char", "hex", "unhex", "quote", "group_concat", "string_agg", "concat", "concat_ws",
        "date", "datetime", "time", "julianday", "strftime", "unixepoch", "timediff", "row_number", "rank", "dense_rank",
        "percent_rank", "cume_dist", "ntile", "lag", "lead", "first_value", "last_value", "nth_value"
    };
    internal string EngineVersion => raw.sqlite3_libversion().utf8_to_string();

    internal QueryEngine(QueryBudgets budgets, CancellationToken cancellation, Func<QuerySourceRead, QueryCell>? sourceRead = null, string? currentDocumentId = null)
    {
        budgets.Validate(); this.budgets = budgets; this.cancellation = cancellation; this.sourceRead = sourceRead; this.currentDocumentId = currentDocumentId;
        if (currentDocumentId is not null && (sourceRead is null || currentDocumentId.Length is < 1 or > 128)) throw new QueryException(QueryError.InvalidRequest);
        // A dedicated process makes SQLite's process-global hard heap cap independent of Desktop/other features.
        Batteries_V2.Init();
        Check(raw.sqlite3_open_v2(":memory:", out db, raw.SQLITE_OPEN_READWRITE | raw.SQLITE_OPEN_CREATE | raw.SQLITE_OPEN_MEMORY, null));
        try
        {
            Configure(raw.SQLITE_DBCONFIG_DEFENSIVE, 1);
            Configure(raw.SQLITE_DBCONFIG_TRUSTED_SCHEMA, 0);
            Configure(raw.SQLITE_DBCONFIG_ENABLE_LOAD_EXTENSION, 0);
            Configure(raw.SQLITE_DBCONFIG_ENABLE_TRIGGER, 0);
            Configure(raw.SQLITE_DBCONFIG_ENABLE_VIEW, 0);
            Check(raw.sqlite3_enable_load_extension(db, 0));
            Fixed($"PRAGMA hard_heap_limit={QueryLimits.NativeHeapBytes}");
            Fixed("PRAGMA page_size=4096");
            Fixed($"PRAGMA max_page_count={QueryLimits.DatabaseBytes / 4096}");
            Fixed("PRAGMA temp_store=MEMORY");
            Fixed("PRAGMA foreign_keys=ON");
            if (Scalar("PRAGMA hard_heap_limit") != QueryLimits.NativeHeapBytes ||
                Scalar("PRAGMA temp_store") != 2 || Scalar("PRAGMA max_page_count") != QueryLimits.DatabaseBytes / 4096)
                throw new QueryException(QueryError.WorkerUnavailable);
            foreach (var table in Enum.GetValues<QueryTable>()) CreateTable(table);
            if (sourceRead is not null) ConfigureSources();
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_LENGTH, 1024 * 1024);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_SQL_LENGTH, QueryLimits.MaximumSqlCharacters * 4);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_COLUMN, QueryLimits.MaximumColumns);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_EXPR_DEPTH, 32);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_COMPOUND_SELECT, 16);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_VDBE_OP, 100_000);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_FUNCTION_ARG, 16);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_ATTACHED, 0);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_LIKE_PATTERN_LENGTH, 4096);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_VARIABLE_NUMBER, QueryLimits.MaximumParameters);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_TRIGGER_DEPTH, 0);
            raw.sqlite3_limit(db, raw.SQLITE_LIMIT_WORKER_THREADS, 0);
            raw.sqlite3_progress_handler(db, 1000, _ => Progress(), null);
        }
        catch { Dispose(); throw; }
    }
    private void Configure(int setting, int value)
    {
        Check(raw.sqlite3_db_config(db, setting, value, out var applied));
        if (applied != value) throw new QueryException(QueryError.WorkerUnavailable);
    }
    private int Progress()
    {
        if (cancellation.IsCancellationRequested) return 1;
        if (clock.IsRunning && clock.ElapsedMilliseconds >= budgets.QueryMilliseconds) { deadline = true; return 1; }
        if (++callbacks > budgets.ProgressCallbacks) { instructions = true; return 1; }
        return 0;
    }
    private void CreateTable(QueryTable table)
    {
        var columns = QuerySchema.Columns(table);
        var keys = table switch
        {
            QueryTable.Documents => "DocumentId",
            QueryTable.Messages => "DocumentId,MessageId",
            QueryTable.Segments => "DocumentId,MessageId,SegmentId",
            QueryTable.Fields => "DocumentId,MessageId,SegmentId,FieldNumber,RepetitionNumber",
            QueryTable.Components => "DocumentId,MessageId,SegmentId,FieldNumber,RepetitionNumber,ComponentNumber",
            _ => "DocumentId,MessageId,SegmentId,FieldNumber,RepetitionNumber,ComponentNumber,SubcomponentNumber"
        };
        var foreign = table switch
        {
            QueryTable.Documents => "",
            QueryTable.Messages => ",FOREIGN KEY(DocumentId) REFERENCES Documents(DocumentId)",
            QueryTable.Segments => ",FOREIGN KEY(DocumentId,MessageId) REFERENCES Messages(DocumentId,MessageId)",
            _ => ",FOREIGN KEY(DocumentId,MessageId,SegmentId) REFERENCES Segments(DocumentId,MessageId,SegmentId)"
        };
        // Every identifier in DDL/INSERT comes from this closed schema, never query text or imported values.
        var physical = Physical(table);
        Fixed($"CREATE TABLE {physical} (" + string.Join(',', columns.Select(c => $"\"{c.Name}\" {(c.Kind == QueryValueKind.Integer ? "INTEGER" : "TEXT")}" +
            (c.Nullable ? "" : " NOT NULL"))) + $",PRIMARY KEY({keys}){foreign}) STRICT");
        Check(raw.sqlite3_prepare_v2(db, $"INSERT INTO {physical} VALUES ({string.Join(',', columns.Select(_ => "?"))})", out var insert));
        inserts.Add(table, insert);
    }
    private void Fixed(string sql)
    {
        Check(raw.sqlite3_prepare_v2(db, sql, out var statement));
        using (statement)
        {
            int code;
            do { code = raw.sqlite3_step(statement); } while (code == raw.SQLITE_ROW);
            Check(code);
        }
    }
    private long Scalar(string sql)
    {
        Check(raw.sqlite3_prepare_v2(db, sql, out var statement));
        using (statement) { Check(raw.sqlite3_step(statement)); return raw.sqlite3_column_int64(statement, 0); }
    }
    internal void Import(QueryImportRow[] rows)
    {
        if (sealedInput || rows.Length is < 1 or > QueryLimits.MaximumImportBatchRows) throw new QueryException(QueryError.InvalidRequest);
        callbacks = 0;
        foreach (var row in rows)
        {
            cancellation.ThrowIfCancellationRequested();
            if (row is null) throw new QueryException(QueryError.InvalidRequest);
            var size = row.Validate();
            if (++importedRows > budgets.ImportedRows || (importedBytes += size) > budgets.ImportBytes) throw new QueryException(QueryError.ImportLimit);
            var insert = inserts[row.Table];
            Check(raw.sqlite3_reset(insert)); Check(raw.sqlite3_clear_bindings(insert));
            for (var i = 0; i < row.Values.Length; i++) Bind(insert, i + 1, row.Values[i]);
            var code = raw.sqlite3_step(insert);
            if ((code & 255) == raw.SQLITE_CONSTRAINT || code == raw.SQLITE_MISMATCH) throw new QueryException(QueryError.ImportRejected);
            Check(code);
        }
    }
    internal string[] Execute(string sql, QueryParameter[] parameters)
    {
        if (query is not null && !complete) throw new QueryException(QueryError.InvalidRequest);
        query?.Dispose(); query = null;
        if (!sealedInput)
        {
            foreach (var insert in inserts.Values) insert.Dispose(); inserts.Clear();
            Fixed("PRAGMA query_only=ON");
            if (Scalar("PRAGMA query_only") != 1) throw new QueryException(QueryError.WorkerUnavailable);
            Check(raw.sqlite3_set_authorizer(db, (strdelegate_authorizer)Authorize, null));
            sealedInput = true;
        }
        ResetQuery();
        query = Prepare(sql, parameters);
        var columns = Enumerable.Range(0, raw.sqlite3_column_count(query)).Select(n => raw.sqlite3_column_name(query, n).utf8_to_string()).ToArray();
        if (columns.Length > QueryLimits.MaximumColumns) throw new QueryException(QueryError.ResourceLimit);
        resultBytes = columns.Sum(n => 32L + QueryLimits.Utf8.GetByteCount(n));
        if (resultBytes > budgets.ResultBytes) throw new QueryException(QueryError.ResultLimit);
        return columns;
    }
    private void ResetQuery()
    {
        callbacks = resultRows = sourceReads = 0; resultBytes = 0; complete = deadline = instructions = denied = false; sourceError = null; clock.Restart();
    }
    private sqlite3_stmt Prepare(string sql, QueryParameter[] parameters)
    {
        if (sql.Length is < 1 or > QueryLimits.MaximumSqlCharacters || sql.Contains('\0') ||
            parameters.Length > QueryLimits.MaximumParameters) throw new QueryException(QueryError.InvalidRequest);
        _ = QueryLimits.Utf8.GetByteCount(sql);
        Check(raw.sqlite3_prepare_v3(db, sql, sourceRead is null ? raw.SQLITE_PREPARE_NO_VTAB : 0U, out var statement, out string tail));
        try
        {
        if (statement is null || statement.IsInvalid || raw.sqlite3_column_count(statement) == 0 || raw.sqlite3_stmt_readonly(statement) == 0)
            throw new QueryException(QueryError.NotAuthorized);
        // Let SQLite consume comments/semicolons. Never split SQL text or execute before validating the entire tail.
        while (!string.IsNullOrEmpty(tail))
        {
            Check(raw.sqlite3_prepare_v3(db, tail, sourceRead is null ? raw.SQLITE_PREPARE_NO_VTAB : 0U, out var extra, out string rest));
            using (extra)
                if (extra is not null && !extra.IsInvalid) throw new QueryException(QueryError.MultipleStatements);
            if (rest.Length >= tail.Length) throw new QueryException(QueryError.InvalidSql);
            tail = rest;
        }
        var required = raw.sqlite3_bind_parameter_count(statement);
        if (parameters.Length != required) throw new QueryException(QueryError.ParameterMismatch);
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in parameters)
        {
            if (parameter is null || parameter.Name is not { Length: > 1 and <= 64 } name || name[0] is not ('@' or ':' or '$') ||
                name.Skip(1).Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_') || !names.Add(name) || parameter.Value is null)
                throw new QueryException(QueryError.ParameterMismatch);
            var index = raw.sqlite3_bind_parameter_index(statement, name);
            if (index == 0) throw new QueryException(QueryError.ParameterMismatch);
            _ = parameter.Value.Size(); Bind(statement, index, parameter.Value);
        }
        return statement;
        }
        catch { statement?.Dispose(); throw; }
    }
    private int Authorize(object _, int action, string? first, string? second, string? database, string? inner)
    {
        var tableRead = action == raw.SQLITE_READ && AllowRead(first, second, database, inner);
        var allowed = action is raw.SQLITE_SELECT or raw.SQLITE_RECURSIVE ||
            action == raw.SQLITE_FUNCTION && second is not null && (Functions.Contains(second) || AllowFunction(second, inner)) || tableRead;
        if (!allowed) denied = true;
        return allowed ? raw.SQLITE_OK : raw.SQLITE_DENY;
    }
    internal (QueryRow[] Rows, bool Complete) Page()
    {
        if (query is null || complete) throw new QueryException(QueryError.InvalidRequest);
        var rows = new List<QueryRow>(); var bytes = 0;
        while (rows.Count < QueryLimits.MaximumPageRows && bytes < QueryLimits.MaximumPageBytes)
        {
            cancellation.ThrowIfCancellationRequested();
            if (clock.ElapsedMilliseconds >= budgets.QueryMilliseconds) throw new QueryException(QueryError.Timeout);
            var code = raw.sqlite3_step(query);
            if (code == raw.SQLITE_DONE) { complete = true; clock.Stop(); break; }
            Check(code);
            if (++resultRows > budgets.ResultRows) throw new QueryException(QueryError.ResultLimit);
            var cells = new QueryCell[raw.sqlite3_column_count(query)]; var rowBytes = 32;
            for (var i = 0; i < cells.Length; i++)
            {
                var kind = raw.sqlite3_column_type(query, i);
                if (kind is raw.SQLITE_TEXT or raw.SQLITE_BLOB && raw.sqlite3_column_bytes(query, i) > QueryLimits.MaximumValueBytes)
                    throw new QueryException(QueryError.MaterializationLimit);
                cells[i] = kind switch
                {
                    raw.SQLITE_NULL => QueryCell.Null,
                    raw.SQLITE_INTEGER => QueryCell.From(raw.sqlite3_column_int64(query, i)),
                    raw.SQLITE_FLOAT => Real(raw.sqlite3_column_double(query, i)),
                    // Length-delimited UTF-8 preserves embedded NUL characters; a C-string helper would silently shorten a value.
                    raw.SQLITE_TEXT => Text(raw.sqlite3_column_blob(query, i)),
                    raw.SQLITE_BLOB => new(QueryValueKind.Blob, Blob: raw.sqlite3_column_blob(query, i).ToArray()),
                    _ => throw new QueryException(QueryError.ProtocolError)
                };
                rowBytes = checked(rowBytes + cells[i].Size());
                if (rowBytes > QueryLimits.MaximumRowBytes) throw new QueryException(QueryError.MaterializationLimit);
            }
            resultBytes += rowBytes;
            if (resultBytes > budgets.ResultBytes) throw new QueryException(QueryError.ResultLimit);
            rows.Add(new(cells)); bytes += rowBytes;
        }
        return (rows.ToArray(), complete);
    }
    private static QueryCell Real(double value) => double.IsFinite(value) ? QueryCell.From(value) : throw new QueryException(QueryError.InvalidValue);
    private static QueryCell Text(ReadOnlySpan<byte> value)
    {
        try { return QueryCell.From(QueryLimits.Utf8.GetString(value)); }
        catch (System.Text.DecoderFallbackException) { throw new QueryException(QueryError.InvalidValue); }
    }
    private void Bind(sqlite3_stmt statement, int index, QueryCell value) => Check(value.Kind switch
    {
        QueryValueKind.Null => raw.sqlite3_bind_null(statement, index),
        QueryValueKind.Integer => raw.sqlite3_bind_int64(statement, index, value.Integer),
        QueryValueKind.Real => raw.sqlite3_bind_double(statement, index, value.Real),
        QueryValueKind.Text => value.Text!.Length == 0 ? raw.sqlite3_bind_text(statement, index, "") :
            raw.sqlite3_bind_text(statement, index, QueryLimits.Utf8.GetBytes(value.Text).AsSpan()),
        QueryValueKind.Blob => value.Blob!.Length == 0 ? raw.sqlite3_bind_zeroblob(statement, index, 0) : raw.sqlite3_bind_blob(statement, index, value.Blob),
        _ => throw new QueryException(QueryError.InvalidRequest)
    });
    private void Check(int code)
    {
        if (code is raw.SQLITE_OK or raw.SQLITE_ROW or raw.SQLITE_DONE) return;
        cancellation.ThrowIfCancellationRequested();
        throw new QueryException(sourceError ?? (deadline ? QueryError.Timeout : denied || code == raw.SQLITE_AUTH ? QueryError.NotAuthorized :
            instructions || code is raw.SQLITE_NOMEM or raw.SQLITE_FULL or raw.SQLITE_TOOBIG or raw.SQLITE_INTERRUPT ? QueryError.ResourceLimit : QueryError.InvalidSql));
    }
    public void Dispose()
    {
        query?.Dispose(); foreach (var insert in inserts.Values) insert.Dispose(); inserts.Clear(); db.Dispose();
        foreach (var module in modules) module.Dispose(); modules.Clear();
    }
}
