using HL7Workbench.Querying;
using SQLitePCL;

namespace HL7Workbench.QueryHost;

internal sealed partial class QueryEngine
{
    private readonly Func<QuerySourceRead, QueryCell>? sourceRead;
    private readonly string? currentDocumentId;
    private readonly HashSet<QueryTable> plannedTables = [];
    private int sourceReads;
    private QueryError? sourceError;
    private bool trustedMetadataRead;
    private readonly List<FieldModule> modules = [];
    private static bool ValueTable(QueryTable table) => table is QueryTable.Fields or QueryTable.Components or QueryTable.Subcomponents;
    private string Physical(QueryTable table) => sourceRead is not null && ValueTable(table) ? "Source" + table : table.ToString();
    private bool AllowRead(string? name, string? column, string? database, string? inner)
    {
        if (database != "main" && (!string.IsNullOrEmpty(database) || !string.IsNullOrEmpty(column))) return false;
        var hidden = name?.StartsWith("Source", StringComparison.Ordinal) == true;
        if (!Enum.TryParse<QueryTable>(hidden ? name![6..] : name, true, out var table) || !Enum.IsDefined(table)) return false;
        if (hidden && (sourceRead is null || !ValueTable(table) || !trustedMetadataRead)) return false;
        if (!string.IsNullOrEmpty(column) && !(hidden && trustedMetadataRead && column.Equals("ROWID", StringComparison.OrdinalIgnoreCase)) &&
            !QuerySchema.Columns(table).Any(c => string.Equals(c.Name, column, StringComparison.OrdinalIgnoreCase))) return false;
        plannedTables.Add(table); return true;
    }
    private bool AllowFunction(string name, string? inner) => sourceRead is not null &&
        Enum.GetValues<QuerySourceOperation>().Any(op => name.Equals("HL7_" + op.ToString(), StringComparison.OrdinalIgnoreCase));

    internal QueryPlan Plan(string sql, QueryParameter[] parameters)
    {
        if (sealedInput) throw new QueryException(QueryError.InvalidRequest);
        ResetQuery(); plannedTables.Clear();
        Check(raw.sqlite3_set_authorizer(db, (strdelegate_authorizer)Authorize, null));
        try
        {
            using var statement = Prepare(sql, parameters);
            return new(plannedTables.Order().ToArray());
        }
        finally
        {
            // No user statement is stepped here. Only this pre-seal planning phase can restore
            // trusted, parameterized import. Execute permanently seals it; Plan cannot unseal it.
            Check(raw.sqlite3_set_authorizer(db, (strdelegate_authorizer)null!, null)); clock.Stop();
        }
    }
    internal QueryColumnOrigin[] Origins => query is null ? [] : Enumerable.Range(0, raw.sqlite3_column_count(query)).Select(i =>
    {
        var table = raw.sqlite3_column_table_name(query, i).utf8_to_string();
        return new QueryColumnOrigin(table?.StartsWith("Source", StringComparison.Ordinal) == true ? table[6..] : table,
            raw.sqlite3_column_origin_name(query, i).utf8_to_string());
    }).ToArray();

    private void ConfigureSources()
    {
        // Captured reads are DIRECTONLY, not INNOCUOUS. No schema may invoke them.
        foreach (var operation in Enum.GetValues<QuerySourceOperation>())
        {
            var op = operation;
            Check(raw.sqlite3_create_function(db, "HL7_" + op.ToString(), 3, raw.SQLITE_UTF8 | 0x80000 /* DIRECTONLY */, null,
                (context, _, values) => SourceFunction(context, op, values, 0)));
            if (currentDocumentId is not null)
                Check(raw.sqlite3_create_function(db, "HL7_" + op.ToString(), 2, raw.SQLITE_UTF8 | 0x80000, null,
                    (context, _, values) => SourceFunction(context, op, values, 0, currentDocumentId)));
        }
        foreach (var table in new[] { QueryTable.Fields, QueryTable.Components, QueryTable.Subcomponents })
        {
            var module = new FieldModule(this, table); modules.Add(module);
            module.Register(); Fixed($"CREATE VIRTUAL TABLE {table} USING mw_{table}");
        }
    }
    private void SourceFunction(sqlite3_context context, QuerySourceOperation operation, sqlite3_value[] values, int start, string? documentOverride = null)
    {
        try
        {
            cancellation.ThrowIfCancellationRequested();
            var id = start + (documentOverride is null ? 1 : 0); var path = id + 1;
            if (documentOverride is null && raw.sqlite3_value_type(values[start]) != raw.SQLITE_TEXT || raw.sqlite3_value_type(values[id]) != raw.SQLITE_INTEGER ||
                raw.sqlite3_value_type(values[path]) != raw.SQLITE_TEXT) throw new QueryException(QueryError.InvalidRequest);
            var read = new QuerySourceRead(operation, documentOverride ?? QueryLimits.Utf8.GetString(raw.sqlite3_value_blob(values[start])),
                raw.sqlite3_value_int64(values[id]), QueryLimits.Utf8.GetString(raw.sqlite3_value_blob(values[path])));
            DeliverSource(context, read);
        }
        catch (Exception error)
        {
            // Exceptions must never unwind through the unmanaged SQLite callback boundary.
            CallbackFailure(error);
            raw.sqlite3_result_error(context, "Captured value unavailable");
        }
    }
    private void DeliverSource(sqlite3_context context, QuerySourceRead read)
    {
        if (++sourceReads > QueryLimits.MaximumSourceReads) throw new QueryException(QueryError.ResourceLimit);
        read.Validate(); var value = sourceRead!(read); _ = value.Size();
        if (value.Kind == QueryValueKind.Null) raw.sqlite3_result_null(context);
        else if (value.Kind == QueryValueKind.Text)
        {
            if (value.Text!.Length == 0) raw.sqlite3_result_text(context, "");
            else raw.sqlite3_result_text(context, QueryLimits.Utf8.GetBytes(value.Text).AsSpan());
        }
        else throw new QueryException(QueryError.InvalidValue);
    }
    private int CallbackFailure(Exception error)
    {
        sourceError = error is QueryException known ? known.Code : cancellation.IsCancellationRequested ? QueryError.Timeout : QueryError.SourceUnavailable;
        return raw.SQLITE_ERROR;
    }
}
