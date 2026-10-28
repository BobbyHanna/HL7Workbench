using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HL7Workbench.Querying;

internal enum QueryFrameKind { Initialize, Ready, Import, Imported, Execute, Header, Next, Page, Cancel, Failure, Close, Closed, Plan, Planned, SourceRead, SourceValue }
internal sealed record QueryFrame(QueryFrameKind Kind, int Version = 2, QueryBudgets? Budgets = null,
    QueryImportRow[]? Input = null, string? Sql = null, QueryParameter[]? Parameters = null, string[]? Columns = null,
    QueryRow[]? Rows = null, bool Complete = false, QueryError? Error = null, string? EngineVersion = null,
    bool SourceBacked = false, QueryPlan? Plan = null, QuerySourceRead? Read = null, QueryCell? Value = null,
    QueryColumnOrigin[]? Origins = null, string? CurrentDocumentId = null)
{ public override string ToString() => $"SQL protocol frame ({Kind})"; }

internal static class QueryProtocol
{
    private static readonly JsonSerializerOptions Options = new()
    { MaxDepth = 16, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true };
    internal static async Task WriteAsync(Stream stream, QueryFrame frame, CancellationToken token)
    {
        // Stream into a capped buffer: rejecting only after SerializeToUtf8Bytes could allocate
        // hundreds of MiB for a rejected batch whose JSON escaping greatly expands its scalars.
        using var buffer = new FrameBuffer();
        await JsonSerializer.SerializeAsync(buffer, frame, Options, token);
        var bytes = buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length));
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, token); await stream.WriteAsync(bytes, token); await stream.FlushAsync(token);
    }
    internal static async Task<QueryFrame> ReadAsync(Stream stream, CancellationToken token)
    {
        var header = new byte[4]; await stream.ReadExactlyAsync(header, token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 1 or > QueryLimits.MaximumFrameBytes) throw new QueryException(QueryError.ProtocolError);
        var bytes = new byte[length]; await stream.ReadExactlyAsync(bytes, token);
        var frame = JsonSerializer.Deserialize<QueryFrame>(bytes, Options);
        if (frame is null || frame.Version != 2 || !Enum.IsDefined(frame.Kind) || frame.Error is { } error && !Enum.IsDefined(error) ||
            frame.Input?.Length > QueryLimits.MaximumImportBatchRows || frame.Rows?.Length > QueryLimits.MaximumPageRows ||
            frame.Columns?.Length > QueryLimits.MaximumColumns || frame.Parameters?.Length > QueryLimits.MaximumParameters ||
            frame.Origins?.Length > QueryLimits.MaximumColumns || frame.Plan?.Tables.Length > 6)
            throw new QueryException(QueryError.ProtocolError);
        return frame;
    }
    private sealed class FrameBuffer : MemoryStream
    {
        private void Check(int count)
        {
            if (count > QueryLimits.MaximumFrameBytes - Length) throw new QueryException(QueryError.MaterializationLimit);
        }
        public override void Write(byte[] buffer, int offset, int count) { Check(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { Check(buffer.Length); base.Write(buffer); }
        public override void WriteByte(byte value) { Check(1); base.WriteByte(value); }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        { cancellationToken.ThrowIfCancellationRequested(); Write(buffer.Span); return ValueTask.CompletedTask; }
    }
}
