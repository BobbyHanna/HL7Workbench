using System.Globalization;
using System.Runtime.InteropServices;
using HL7Workbench.Querying;
using SQLitePCL;

namespace HL7Workbench.QueryHost;

internal sealed partial class QueryEngine
{
    /// <summary>Read-only SQLite data adapter, not a SQL interpreter. Only xColumn materializes selected values.
    /// Backing-table SQL is constant/parameterized and prepared under a narrow synchronous capability;
    /// authorizer view/CTE context names are deliberately not treated as authority.</summary>
    private sealed class FieldModule : IDisposable
    {
        private readonly QueryEngine owner;
        private readonly QueryTable table;
        private readonly IReadOnlyList<QueryColumn> columns;
        private readonly Dictionary<IntPtr, Cursor> cursors = [];
        private readonly List<Delegate> roots = [];
        private readonly IntPtr module;
        private sealed class Cursor { internal sqlite3_stmt? Statement; internal bool Eof; }
        internal FieldModule(QueryEngine owner, QueryTable table)
        {
            this.owner = owner; this.table = table; columns = QuerySchema.Columns(table);
            IntPtr Root(Delegate callback) { roots.Add(callback); return Marshal.GetFunctionPointerForDelegate(callback); }
            var definition = new Module
            {
                Version = 1,
                Create = Root((Connect)ConnectTable), Connect = Root((Connect)ConnectTable),
                BestIndex = Root((TwoPointers)BestIndex), Disconnect = Root((OnePointer)Disconnect), Destroy = Root((OnePointer)Disconnect),
                Open = Root((OpenCursor)Open), Close = Root((OnePointer)Close), Filter = Root((FilterCursor)Filter),
                Next = Root((OnePointer)Next), Eof = Root((OnePointer)Eof), Column = Root((ColumnCursor)Column), Rowid = Root((GetRowid)Rowid)
                // xUpdate and all write/transaction callbacks remain NULL.
            };
            module = Marshal.AllocHGlobal(Marshal.SizeOf<Module>()); Marshal.StructureToPtr(definition, module, false);
        }
        internal void Register() => owner.Check(Native.sqlite3_create_module_v2(owner.db.DangerousGetHandle(), "mw_" + table, module, IntPtr.Zero, IntPtr.Zero));
        private int ConnectTable(IntPtr db, IntPtr aux, int argc, IntPtr argv, out IntPtr output, out IntPtr error)
        {
            output = error = IntPtr.Zero;
            try
            {
                owner.Check(Native.sqlite3_declare_vtab(db, "CREATE TABLE x(" + string.Join(',', columns.Select(c =>
                    c.Name + (c.Kind == QueryValueKind.Integer ? " INTEGER" : " TEXT"))) + ")"));
                output = Marshal.AllocHGlobal(Marshal.SizeOf<Vtab>()); Marshal.StructureToPtr(new Vtab(), output, false); return raw.SQLITE_OK;
            }
            catch (Exception failure) { return owner.CallbackFailure(failure); }
        }
        private static int Disconnect(IntPtr table) { Marshal.FreeHGlobal(table); return raw.SQLITE_OK; }
        private int Open(IntPtr table, out IntPtr output)
        {
            output = IntPtr.Zero;
            try { output = Marshal.AllocHGlobal(IntPtr.Size); Marshal.WriteIntPtr(output, table); cursors.Add(output, new()); return raw.SQLITE_OK; }
            catch (Exception failure) { return owner.CallbackFailure(failure); }
        }
        private int Close(IntPtr pointer)
        {
            try { if (cursors.Remove(pointer, out var cursor)) cursor.Statement?.Dispose(); Marshal.FreeHGlobal(pointer); return raw.SQLITE_OK; }
            catch (Exception failure) { return owner.CallbackFailure(failure); }
        }
        private int BestIndex(IntPtr table, IntPtr pointer)
        {
            try
            {
                var info = Marshal.PtrToStructure<IndexInfo>(pointer); var selected = new List<int>();
                for (var n = 0; n < info.ConstraintCount && selected.Count < QueryLimits.MaximumParameters; n++)
                {
                    var constraint = Marshal.PtrToStructure<Constraint>(info.Constraints + n * Marshal.SizeOf<Constraint>());
                    if (constraint.Usable == 0 || constraint.Operation != 2 || constraint.Column < 0 || constraint.Column >= columns.Count) continue;
                    var column = columns[constraint.Column];
                    if (column.Name is "RawValue" or "DecodedValue" or "ValueStatus") continue;
                    // Never narrow a NOCASE/RTRIM comparison with a BINARY backing-table predicate.
                    if (column.Kind == QueryValueKind.Text && Marshal.PtrToStringUTF8(Native.sqlite3_vtab_collation(pointer, n)) != "BINARY") continue;
                    selected.Add(constraint.Column);
                    Marshal.StructureToPtr(new Usage { Argument = selected.Count, Omit = 0 }, info.Usage + n * Marshal.SizeOf<Usage>(), false);
                }
                var bytes = QueryLimits.Utf8.GetBytes(string.Join(',', selected) + "\0");
                info.IndexString = raw.sqlite3_malloc(bytes.Length); if (info.IndexString == IntPtr.Zero) return raw.SQLITE_NOMEM;
                Marshal.Copy(bytes, 0, info.IndexString, bytes.Length); info.FreeString = 1;
                info.EstimatedCost = selected.Count == 0 ? 250_000 : 1000.0 / selected.Count;
                info.EstimatedRows = selected.Count == 0 ? 250_000 : 1000;
                Marshal.StructureToPtr(info, pointer, false); return raw.SQLITE_OK;
            }
            catch (Exception failure) { return owner.CallbackFailure(failure); }
        }
        private int Filter(IntPtr pointer, int index, IntPtr expression, int count, IntPtr arguments)
        {
            try
            {
                owner.cancellation.ThrowIfCancellationRequested(); var cursor = cursors[pointer]; cursor.Statement?.Dispose();
                var text = Marshal.PtrToStringUTF8(expression) ?? "";
                var selected = text.Length == 0 ? [] : text.Split(',').Select(s => int.Parse(s, CultureInfo.InvariantCulture)).ToArray();
                if (selected.Length != count || selected.Any(i => i < 0 || i >= columns.Count)) throw new QueryException(QueryError.ProtocolError);
                var sql = "SELECT rowid,* FROM " + owner.Physical(table) + (count == 0 ? "" : " WHERE " + string.Join(" AND ", selected.Select(i => columns[i].Name + "=?")));
                owner.trustedMetadataRead = true;
                try { owner.Check(raw.sqlite3_prepare_v2(owner.db, sql, out var statement)); cursor.Statement = statement; }
                finally { owner.trustedMetadataRead = false; }
                for (var n = 0; n < count; n++)
                {
                    var value = NativeValue(Marshal.ReadIntPtr(arguments, n * IntPtr.Size)); owner.Bind(cursor.Statement!, n + 1, value);
                }
                return Next(pointer);
            }
            catch (Exception failure) { return owner.CallbackFailure(failure); }
        }
        private QueryCell NativeValue(IntPtr pointer)
        {
            var kind = Native.sqlite3_value_type(pointer);
            if (kind == raw.SQLITE_NULL) return QueryCell.Null;
            if (kind == raw.SQLITE_INTEGER) return QueryCell.From(Native.sqlite3_value_int64(pointer));
            if (kind == raw.SQLITE_FLOAT) return Real(Native.sqlite3_value_double(pointer));
            var length = Native.sqlite3_value_bytes(pointer);
            if (length > QueryLimits.MaximumValueBytes) throw new QueryException(QueryError.MaterializationLimit);
            var bytes = new byte[length]; if (length != 0) Marshal.Copy(Native.sqlite3_value_blob(pointer), bytes, 0, length);
            return kind == raw.SQLITE_BLOB ? new(QueryValueKind.Blob, Blob: bytes) : Text(bytes);
        }
        private int Next(IntPtr pointer)
        {
            try
            {
                owner.cancellation.ThrowIfCancellationRequested(); var cursor = cursors[pointer];
                var code = raw.sqlite3_step(cursor.Statement!); owner.Check(code); cursor.Eof = code == raw.SQLITE_DONE; return raw.SQLITE_OK;
            }
            catch (Exception failure) { return owner.CallbackFailure(failure); }
        }
        private int Eof(IntPtr pointer) => cursors.TryGetValue(pointer, out var cursor) && !cursor.Eof ? 0 : 1;
        private int Column(IntPtr pointer, IntPtr contextPointer, int column)
        {
            try
            {
                var statement = cursors[pointer].Statement!;
                // sqlite3_context is a provider wrapper with an internal constructor. Results use
                // the native API here, while all source reads still use the same bounded RPC.
                var name = columns[column].Name;
                if (name is "RawValue" or "DecodedValue" or "ValueStatus")
                {
                    string TextAt(int n) => QueryLimits.Utf8.GetString(raw.sqlite3_column_blob(statement, n + 1));
                    string NumberAt(int n) => raw.sqlite3_column_int64(statement, n + 1).ToString(CultureInfo.InvariantCulture);
                    var path = TextAt(3) + "[" + NumberAt(4) + "]-" + NumberAt(5) + "[" + NumberAt(6) + "]" +
                        (table != QueryTable.Fields ? "." + NumberAt(7) : "") + (table == QueryTable.Subcomponents ? "." + NumberAt(8) : "");
                    if (++owner.sourceReads > QueryLimits.MaximumSourceReads) throw new QueryException(QueryError.ResourceLimit);
                    var read = new QuerySourceRead(name == "RawValue" ? QuerySourceOperation.Raw : name == "DecodedValue" ? QuerySourceOperation.Value : QuerySourceOperation.Status,
                        TextAt(0), raw.sqlite3_column_int64(statement, 2), path);
                    read.Validate(); var value = owner.sourceRead!(read); _ = value.Size();
                    if (value.Kind == QueryValueKind.Null) Native.sqlite3_result_null(contextPointer);
                    else if (value.Kind == QueryValueKind.Text)
                    {
                        var bytes = QueryLimits.Utf8.GetBytes(value.Text! + "\0"); Native.sqlite3_result_text(contextPointer, bytes, bytes.Length - 1, new IntPtr(-1));
                    }
                    else throw new QueryException(QueryError.InvalidValue);
                }
                else Native.sqlite3_result_value(contextPointer, Native.sqlite3_column_value(statement.DangerousGetHandle(), column + 1));
                return raw.SQLITE_OK;
            }
            catch (Exception failure) { return owner.CallbackFailure(failure); }
        }
        private int Rowid(IntPtr pointer, out long value)
        {
            value = 0;
            try { value = raw.sqlite3_column_int64(cursors[pointer].Statement!, 0); return raw.SQLITE_OK; }
            catch (Exception failure) { return owner.CallbackFailure(failure); }
        }
        public void Dispose() { foreach (var cursor in cursors.Values) cursor.Statement?.Dispose(); cursors.Clear(); Marshal.FreeHGlobal(module); GC.KeepAlive(roots); }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Connect(IntPtr db, IntPtr aux, int count, IntPtr args, out IntPtr table, out IntPtr error);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int TwoPointers(IntPtr first, IntPtr second);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int OnePointer(IntPtr pointer);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int OpenCursor(IntPtr table, out IntPtr cursor);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int FilterCursor(IntPtr cursor, int index, IntPtr text, int count, IntPtr args);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ColumnCursor(IntPtr cursor, IntPtr context, int column);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetRowid(IntPtr cursor, out long rowid);
        [StructLayout(LayoutKind.Sequential)] private struct Vtab { public IntPtr Module; public int References; public IntPtr Error; }
        [StructLayout(LayoutKind.Sequential)] private struct Module
        {
            public int Version;
            public IntPtr Create, Connect, BestIndex, Disconnect, Destroy, Open, Close, Filter, Next, Eof, Column, Rowid,
                Update, Begin, Sync, Commit, Rollback, FindFunction, Rename;
        }
        [StructLayout(LayoutKind.Sequential)] private struct Constraint { public int Column; public byte Operation, Usable; public int Offset; }
        [StructLayout(LayoutKind.Sequential)] private struct Usage { public int Argument; public byte Omit; }
        [StructLayout(LayoutKind.Sequential)] private struct IndexInfo
        {
            public int ConstraintCount; public IntPtr Constraints; public int OrderCount; public IntPtr Order;
            public IntPtr Usage; public int Index; public IntPtr IndexString; public int FreeString, Ordered;
            public double EstimatedCost; public long EstimatedRows; public int Flags; public ulong Used;
        }
        private static class Native
        {
            private const string Library = "e_sqlite3";
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_create_module_v2(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string name, IntPtr module, IntPtr aux, IntPtr destroy);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_declare_vtab(IntPtr db, [MarshalAs(UnmanagedType.LPUTF8Str)] string schema);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_vtab_collation(IntPtr info, int constraint);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_column_value(IntPtr statement, int column);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_value_type(IntPtr value);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern long sqlite3_value_int64(IntPtr value);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern double sqlite3_value_double(IntPtr value);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern int sqlite3_value_bytes(IntPtr value);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern IntPtr sqlite3_value_blob(IntPtr value);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void sqlite3_result_null(IntPtr context);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void sqlite3_result_value(IntPtr context, IntPtr value);
            [DllImport(Library, CallingConvention = CallingConvention.Cdecl)] internal static extern void sqlite3_result_text(IntPtr context, byte[] bytes, int length, IntPtr destructor);
        }
    }
}
