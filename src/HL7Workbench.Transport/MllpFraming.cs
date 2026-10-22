namespace HL7Workbench.Transport;

public enum FramingFailure { UnexpectedData, InvalidTerminator, Oversized, Truncated, QueueFull }
public sealed class MllpException(FramingFailure failure) : IOException("MLLP framing failed: " + failure)
{ public FramingFailure Failure { get; } = failure; }

/// <summary>Incremental byte framing. TCP read boundaries never carry protocol meaning.</summary>
public sealed class MllpDecoder(int maximumBytes = MllpFrame.MaximumPayloadBytes, int maximumQueuedFrames = 16) : IDisposable
{
    private readonly MemoryStream payload = new();
    private readonly Queue<byte[]> frames = new();
    private int state;
    public int Pending => frames.Count;
    public int PartialPayloadBytes => checked((int)payload.Length);
    public bool FrameInProgress => state != 0;
    public void Feed(ReadOnlySpan<byte> bytes)
    {
        if (maximumBytes is < 1 or > MllpFrame.MaximumPayloadBytes || maximumQueuedFrames is < 1 or > 64) throw new ArgumentOutOfRangeException();
        foreach (var b in bytes)
        {
            if (state == 0) { if (b != 0x0B) throw new MllpException(FramingFailure.UnexpectedData); state = 1; }
            else if (state == 2)
            {
                if (b != 0x0D) throw new MllpException(FramingFailure.InvalidTerminator);
                if (frames.Count >= maximumQueuedFrames) throw new MllpException(FramingFailure.QueueFull);
                frames.Enqueue(payload.ToArray()); payload.SetLength(0); state = 0;
            }
            else if (b == 0x1C) state = 2;
            else
            {
                if (b == 0x0B) throw new MllpException(FramingFailure.UnexpectedData);
                if (payload.Length >= maximumBytes) throw new MllpException(FramingFailure.Oversized);
                payload.WriteByte(b);
            }
        }
    }
    public byte[]? Take() => frames.TryDequeue(out var frame) ? frame : null;
    public void Complete() { if (state != 0) throw new MllpException(FramingFailure.Truncated); }
    public void Dispose() => payload.Dispose();
}

public static class MllpFrame
{
    public const int MaximumPayloadBytes = 1024 * 1024;
    public static byte[] Encode(ReadOnlySpan<byte> payload)
    {
        if (payload.Length is 0 or > MaximumPayloadBytes || payload.Contains((byte)0x0B) || payload.Contains((byte)0x1C))
            throw new InvalidDataException("Payload size or reserved MLLP control bytes are not transport eligible.");
        var result = new byte[payload.Length + 3]; result[0] = 0x0B; payload.CopyTo(result.AsSpan(1)); result[^2] = 0x1C; result[^1] = 0x0D; return result;
    }
}

internal sealed class MllpReader(Stream stream, int maximumBytes) : IDisposable
{
    private readonly MllpDecoder decoder = new(maximumBytes);
    private readonly byte[] buffer = new byte[8192];
    public int Pending => decoder.Pending;
    public long SocketBytesRead { get; private set; }
    public int PartialPayloadBytes => decoder.PartialPayloadBytes;
    public bool FrameInProgress => decoder.FrameInProgress;
    public async Task<byte[]?> ReadAsync(CancellationToken token)
    {
        while (true)
        {
            if (decoder.Take() is { } frame) return frame;
            var read = await stream.ReadAsync(buffer, token).ConfigureAwait(false);
            if (read == 0) { decoder.Complete(); return null; }
            SocketBytesRead += read;
            decoder.Feed(buffer.AsSpan(0, read));
        }
    }
    public void Dispose() => decoder.Dispose();
}
