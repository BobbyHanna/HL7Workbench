using System.IO.Pipes;
using System.Threading.Channels;
using HL7Workbench.Querying;

namespace HL7Workbench.QueryHost;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 1 || !args[0].StartsWith("hl7-query-", StringComparison.Ordinal)) return 2;
        using var cancellation = new CancellationTokenSource();
        using var pipe = new NamedPipeClientStream(".", args[0], PipeDirection.InOut, PipeOptions.Asynchronous);
        QueryEngine? engine = null;
        TaskCompletionSource<QueryFrame>? pendingSource = null;
        try
        {
            await pipe.ConnectAsync(30_000, cancellation.Token);
            var requests = Channel.CreateBounded<QueryFrame>(1);
            var reader = ReadAsync(pipe, requests.Writer, cancellation, frame =>
            {
                var pending = Volatile.Read(ref pendingSource);
                if (pending is null || !pending.TrySetResult(frame)) throw new QueryException(QueryError.ProtocolError);
            });
            QueryCell ReadSource(QuerySourceRead read)
            {
                var pending = new TaskCompletionSource<QueryFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
                if (Interlocked.CompareExchange(ref pendingSource, pending, null) is not null) throw new QueryException(QueryError.ProtocolError);
                try
                {
                    QueryProtocol.WriteAsync(pipe, new(QueryFrameKind.SourceRead, Read: read), cancellation.Token).GetAwaiter().GetResult();
                    var reply = pending.Task.WaitAsync(cancellation.Token).GetAwaiter().GetResult();
                    if (reply.Error is { } error) throw new QueryException(error);
                    return reply.Value ?? throw new QueryException(QueryError.ProtocolError);
                }
                finally { Interlocked.Exchange(ref pendingSource, null); }
            }
            await foreach (var request in requests.Reader.ReadAllAsync(cancellation.Token))
            {
                QueryFrame response;
                switch (request.Kind)
                {
                    case QueryFrameKind.Initialize when engine is null && request.Budgets is not null:
                        engine = new(request.Budgets, cancellation.Token, request.SourceBacked ? ReadSource : null, request.CurrentDocumentId);
                        response = new(QueryFrameKind.Ready, EngineVersion: engine.EngineVersion); break;
                    case QueryFrameKind.Import when engine is not null && request.Input is not null:
                        engine.Import(request.Input); response = new(QueryFrameKind.Imported); break;
                    case QueryFrameKind.Execute when engine is not null && request.Sql is not null:
                        var columns = engine.Execute(request.Sql, request.Parameters ?? []);
                        response = new(QueryFrameKind.Header, Columns: columns, Origins: engine.Origins); break;
                    case QueryFrameKind.Plan when engine is not null && request.Sql is not null:
                        response = new(QueryFrameKind.Planned, Plan: engine.Plan(request.Sql, request.Parameters ?? [])); break;
                    case QueryFrameKind.Next when engine is not null:
                        var page = engine.Page(); response = new(QueryFrameKind.Page, Rows: page.Rows, Complete: page.Complete); break;
                    case QueryFrameKind.Close:
                        await QueryProtocol.WriteAsync(pipe, new(QueryFrameKind.Closed), cancellation.Token); return 0;
                    default: throw new QueryException(QueryError.InvalidRequest);
                }
                await QueryProtocol.WriteAsync(pipe, response, cancellation.Token);
            }
            await reader; return 1;
        }
        catch (Exception error)
        {
            // SQLite errmsg/exception messages can contain query literals and are never sent or logged.
            var code = error is QueryException known ? known.Code : QueryError.ProtocolError;
            try { await QueryProtocol.WriteAsync(pipe, new(QueryFrameKind.Failure, Error: code), CancellationToken.None); }
            catch (Exception) { }
            return 1;
        }
        finally { engine?.Dispose(); }
    }
    private static async Task ReadAsync(Stream pipe, ChannelWriter<QueryFrame> writer, CancellationTokenSource cancellation, Action<QueryFrame> sourceReply)
    {
        try
        {
            while (true)
            {
                var request = await QueryProtocol.ReadAsync(pipe, cancellation.Token);
                if (request.Kind == QueryFrameKind.Cancel) { cancellation.Cancel(); break; }
                if (request.Kind == QueryFrameKind.SourceValue) { sourceReply(request); continue; }
                await writer.WriteAsync(request, cancellation.Token);
            }
        }
        catch (Exception) { cancellation.Cancel(); }
        finally { writer.TryComplete(); }
    }
}
