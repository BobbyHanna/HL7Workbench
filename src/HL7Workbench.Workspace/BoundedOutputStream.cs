using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Sequential writer that refuses disk quota overflow before each write, leaving the owner responsible for cleanup.</summary>
internal sealed class BoundedOutputStream(Stream output, long maximum) : Stream
{
    private long length;
    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => length;
    public override long Position { get => length; set => throw new NotSupportedException(); }
    public override void Flush() => output.Flush();
    public override Task FlushAsync(CancellationToken cancellationToken) => output.FlushAsync(cancellationToken);
    private void Reserve(int count)
    {
        if (count > maximum - length) throw new Hl7LimitException("StagedOutputBytes");
        length += count;
    }
    public override void Write(byte[] buffer, int offset, int count) { Reserve(count); output.Write(buffer, offset, count); }
    public override void Write(ReadOnlySpan<byte> buffer) { Reserve(buffer.Length); output.Write(buffer); }
    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    { cancellationToken.ThrowIfCancellationRequested(); Reserve(buffer.Length); return output.WriteAsync(buffer, cancellationToken); }
    public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
        WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
}
