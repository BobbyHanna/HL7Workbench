using System.Buffers.Binary;
using System.Text.Json;

namespace HL7Workbench.Scripting;

internal enum FrameKind { Initialize, Ready, Message, Result, Finish, Finished, Cancel, Failure, Begin, Begun, Deferred }
internal sealed record ScriptFrame(FrameKind Kind, ScriptDefinition? Definition = null, byte[]? Bytes = null,
    int CodePage = 65001, int Number = 0, bool Excluded = false, ScriptDiagnostic[]? Diagnostics = null, int Version = 2,
    ScriptReportChunk[]? Reports = null, int DeferredCount = 0);

/// <summary>Single outstanding request; bounded frames provide backpressure and reject lengths before allocation.</summary>
internal static class ScriptProtocol
{
    internal const int MaxFrameBytes = 12 * 1024 * 1024;
    internal static async Task WriteAsync(Stream stream, ScriptFrame frame, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(frame);
        if (bytes.Length > MaxFrameBytes) throw new InvalidDataException();
        var header = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(header, bytes.Length);
        await stream.WriteAsync(header, token); await stream.WriteAsync(bytes, token); await stream.FlushAsync(token);
    }
    internal static async Task<ScriptFrame> ReadAsync(Stream stream, CancellationToken token)
    {
        var header = new byte[4]; await stream.ReadExactlyAsync(header, token);
        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length is < 1 or > MaxFrameBytes) throw new InvalidDataException();
        var bytes = new byte[length]; await stream.ReadExactlyAsync(bytes, token);
        var frame = JsonSerializer.Deserialize<ScriptFrame>(bytes) ?? throw new InvalidDataException();
        if (frame.Version != 2 || !Enum.IsDefined(frame.Kind) || frame.Diagnostics?.Length > ScriptLimits.Diagnostics)
            throw new InvalidDataException();
        if (frame.Reports is { } reports && (reports.Length > ScriptLimits.ReportRows + ScriptLimits.Reports ||
            reports.Any(r => r is null || r.Bytes is null || r.Name is null) ||
            reports.Sum(r => (long)r.Bytes.Length) > ScriptLimits.ReportOperationBytes)) throw new InvalidDataException();
        return frame;
    }
}
