namespace HL7Workbench.Querying;

public enum QueryTable { Documents, Messages, Segments, Fields, Components, Subcomponents }
public sealed record QueryColumn(string Name, QueryValueKind Kind, bool Nullable = true);

/// <summary>Closed relational transport; callers cannot submit DDL or import SQL. Workspace projects live HL7 using Core.</summary>
public static class QuerySchema
{
    private static QueryColumn Text(string name, bool nullable = true) => new(name, QueryValueKind.Text, nullable);
    private static QueryColumn Int(string name, bool nullable = true) => new(name, QueryValueKind.Integer, nullable);
    private static readonly QueryColumn[] Identity = [Text("DocumentId", false), Int("MessageId", false)];
    private static readonly QueryColumn[] FieldCoordinates = [.. Identity, Int("SegmentId", false), Text("SegmentName", false),
        Int("SegmentOccurrence", false), Int("FieldNumber", false), Int("RepetitionNumber", false)];
    private static readonly QueryColumn[] Value = [Text("RawValue"), Text("DecodedValue"), Text("ValueState", false), Text("ValueStatus", false), Int("ValueLength")];
    private static readonly IReadOnlyDictionary<QueryTable, IReadOnlyList<QueryColumn>> Tables = new Dictionary<QueryTable, IReadOnlyList<QueryColumn>>
    {
        [QueryTable.Documents] = Array.AsReadOnly<QueryColumn>([Text("DocumentId", false), Int("DocumentNumber", false), Int("Revision", false)]),
        [QueryTable.Messages] = Array.AsReadOnly<QueryColumn>([ .. Identity, Int("MessageNumber", false), Text("MessageType"), Text("TriggerEvent"),
            Text("MessageControlId"), Text("Hl7Version"), Text("SendingApplication"), Text("SendingFacility"), Text("ReceivingApplication"),
            Text("ReceivingFacility"), Text("MessageTimestamp"), Int("HasDiagnostics"), Text("DiagnosticStatus", false) ]),
        [QueryTable.Segments] = Array.AsReadOnly<QueryColumn>([ .. Identity, Int("SegmentId", false), Text("SegmentName", false),
            Int("SegmentOccurrence", false), Int("SegmentOrdinal", false), Text("Status", false) ]),
        [QueryTable.Fields] = Array.AsReadOnly<QueryColumn>([ .. FieldCoordinates, .. Value ]),
        [QueryTable.Components] = Array.AsReadOnly<QueryColumn>([ .. FieldCoordinates, Int("ComponentNumber", false), .. Value ]),
        [QueryTable.Subcomponents] = Array.AsReadOnly<QueryColumn>([ .. FieldCoordinates, Int("ComponentNumber", false), Int("SubcomponentNumber", false), .. Value ])
    };
    public static IReadOnlyList<QueryColumn> Columns(QueryTable table) => Tables.TryGetValue(table, out var columns) ? columns :
        throw new QueryException(QueryError.InvalidRequest);
}

public sealed record QueryImportRow(QueryTable Table, QueryCell[] Values)
{
    public override string ToString() => "Relational import row";
    internal int Validate()
    {
        var columns = QuerySchema.Columns(Table);
        if (Values is null || Values.Length != columns.Count) throw new QueryException(QueryError.InvalidRequest);
        var size = new QueryRow(Values).Size();
        for (var i = 0; i < Values.Length; i++)
            if (Values[i].Kind != columns[i].Kind && (Values[i].Kind != QueryValueKind.Null || !columns[i].Nullable))
                throw new QueryException(QueryError.InvalidRequest);
        return size;
    }
}
