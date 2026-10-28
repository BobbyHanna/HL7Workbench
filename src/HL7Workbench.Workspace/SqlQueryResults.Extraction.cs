using HL7Workbench.Core;
using HL7Workbench.Querying;

namespace HL7Workbench.Workspace;

public sealed partial class SqlQueryResults
{
    private (int Document, int Message)? IdentityColumns()
    {
        var document = Columns.Select((name, i) => (name, i)).Where(c => c.name.Equals("DocumentId", StringComparison.OrdinalIgnoreCase)).ToArray();
        var message = Columns.Select((name, i) => (name, i)).Where(c => c.name.Equals("MessageId", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (document.Length != 1 || message.Length != 1 || origins.Length != Columns.Count) return null;
        bool IsIdentity(int i, string column) => origins[i].Column == column && Enum.TryParse<QueryTable>(origins[i].Table, out var table) && Enum.IsDefined(table);
        return IsIdentity(document[0].i, "DocumentId") && IsIdentity(message[0].i, "MessageId") ? (document[0].i, message[0].i) : null;
    }
    /// <summary>Whether named identity columns originate in the closed schema. Every actual pair is revalidated against the captured registry during extraction.</summary>
    public bool CanIdentifyMessages => IdentityColumns() is not null;

    /// <summary>Deduplicates the entire complete identity result. Page state and previews never participate in extraction.</summary>
    public async Task<MessageDocument?> OpenMatchingMessagesAsync(DocumentManager documents, CancellationToken token = default)
    {
        using var lease = ReadLease(); using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, closing.Token);
        var columns = IdentityColumns() ?? throw new QueryException(QueryError.SourceUnavailable);
        SourceSnapshot? output = null; MessageDocument? pending = null;
        try
        {
            var expected = new List<(ByteRange Payload, bool Framed)>();
            var selected = await Task.Run(() =>
            {
                var identities = new HashSet<(string Document, long Message)>();
                for (var i = 0; i < RowCount; i++)
                {
                    linked.Token.ThrowIfCancellationRequested(); var row = ReadCore(i);
                    var d = row.Cells[columns.Document]; var m = row.Cells[columns.Message];
                    if (d.Kind != QueryValueKind.Text || m.Kind != QueryValueKind.Integer || !sources.Identities.ContainsKey((d.Text!, m.Integer)))
                        throw new QueryException(QueryError.SourceUnavailable);
                    identities.Add((d.Text!, m.Integer));
                    if (identities.Count > options.MaximumMatchingMessages) throw new QueryException(QueryError.ResultLimit);
                }
                return sources.Documents.SelectMany(d => d.Messages.Where(m => identities.Contains((d.Id, m.Id))).Select(m => (Document: d, Message: m))).ToArray();
            }, linked.Token);
            if (selected.Length == 0) return null;
            var encoding = selected[0].Document.Encoding;
            if (selected.Any(x => !x.Document.Extractable || !x.Message.Serializable || x.Document.Encoding.CodePage != encoding.CodePage))
                throw new QueryException(QueryError.SourceUnavailable);
            output = await Task.Run(() => SourceSnapshot.CreateAsync(async (stream, ct) =>
            {
                await stream.WriteAsync(selected[0].Document.Bom, ct);
                foreach (var (document, message) in selected)
                {
                    ct.ThrowIfCancellationRequested();
                    await ScriptExecution.CopyAsync(document.Snapshot, message.Prefix, stream, ct);
                    var start = stream.Position;
                    await ScriptExecution.CopyAsync(document.Snapshot, message.Payload, stream, ct);
                    expected.Add((new(start, message.Payload.Length), message.Framed));
                    await ScriptExecution.CopyAsync(document.Snapshot, message.Suffix, stream, ct);
                }
            }, options.MaximumOutputBytes, linked.Token, options.StorageRoot), linked.Token);
            var index = await Task.Run(() => MessageFileIndex.Build(output, linked.Token, inheritedEncoding: encoding), linked.Token);
            if (index.Messages.Count != expected.Count || index.Regions.Any(r => r.MessageNumber is null && r.Reason != "File encoding BOM") ||
                index.Messages.Where((m, i) => m.PayloadRange != expected[i].Payload || m.Framed != expected[i].Framed ||
                    m.Diagnostics.Any(d => d.Code is "IncompleteMllp" or "AmbiguousFraming" or "InteriorBom")).Any())
                throw new QueryException(QueryError.SourceUnavailable);
            pending = new(Guid.NewGuid().ToString("N"), "SQL Matches.hl7", output, index); output = null;
            pending.MarkGeneratedUnsaved(); await pending.NavigateAsync(1, linked.Token); linked.Token.ThrowIfCancellationRequested();
            documents.Open(pending); var result = pending; pending = null; return result;
        }
        catch (Hl7LimitException) { throw new QueryException(QueryError.ResultLimit); }
        finally { output?.Dispose(); if (pending is not null) { pending.Dispose(); await pending.Disposal; } }
    }
}
