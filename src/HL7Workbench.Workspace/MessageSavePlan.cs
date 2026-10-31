using System.Diagnostics;
using System.Security.Cryptography;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed record DocumentSaveProgress(string Phase, long CompletedBytes, long TotalBytes);
public sealed record DocumentSaveMetrics(long OutputBytes, TimeSpan Writing, TimeSpan Indexing, TimeSpan ByteVerification);

/// <summary>A frozen, metadata-only composition. Original copy coordinates never refer to previous saved output.</summary>
internal sealed class MessageSavePlan(SourceSnapshot? snapshot, System.Text.Encoding encoding, bool verifyMembership)
{
    private sealed record Piece(ByteRange Range, byte[]? Bytes = null, EditableMessageBuffer? Buffer = null);
    private sealed record ExpectedMessage(MessageId Id, ByteRange Source, ByteRange Payload, bool Framed);
    private readonly List<Piece> pieces = [];
    private readonly List<ExpectedMessage> messages = [];
    private readonly List<(ByteRange Range, string Reason)> gaps = [];
    private static readonly byte[] frameStart = [0x0B], frameEnd = [0x1C, 0x0D];
    public long Length { get; private set; }
    public DocumentSaveMetrics Metrics { get; private set; } = new(0, TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero);

    private void Add(Piece piece)
    {
        if (piece.Range.Length == 0) return;
        var projected = checked(Length + piece.Range.Length);
        if (projected > SourceSnapshot.MaxFileBytes) throw new Hl7LimitException(nameof(SourceSnapshot.MaxFileBytes));
        // Coalesce adjacent untouched bytes, so an unchanged 100,000-message document
        // still streams through one reusable buffer and one contiguous copy descriptor.
        if (piece.Buffer is null && pieces.LastOrDefault() is { Buffer: null } previous &&
            ReferenceEquals(previous.Bytes, piece.Bytes) && previous.Range.End == piece.Range.Offset)
            pieces[^1] = previous with { Range = new(previous.Range.Offset, previous.Range.Length + piece.Range.Length) };
        else pieces.Add(piece);
        Length = projected;
    }
    public void Copy(ByteRange range) => Add(new(range));
    public void Bytes(byte[] bytes, int skip = 0) => Add(new(new(skip, bytes.Length - skip), bytes));
    public void Replacement(EditableMessageBuffer buffer, int skip = 0)
    {
        var length = buffer.Serialize().Length;
        Add(new(new(skip, length - skip), Buffer: buffer));
    }
    public void Gap(ByteRange original, string reason)
    {
        var start = Length; Copy(original); gaps.Add((new(start, original.Length), reason));
    }
    public void MemoryBom(byte[] bytes, int length)
    {
        if (length == 0) return;
        Add(new(new(0, length), bytes)); gaps.Add((new(0, length), "File encoding BOM"));
    }
    public void Original(LogicalMessage logical, IndexedMessage original, EditableMessageBuffer? buffer, byte[]? validatedPayload = null)
    {
        var start = Length;
        var prefix = original.PayloadRange.Offset - original.SourceRange.Offset;
        var suffix = original.SourceRange.End - original.PayloadRange.End;
        Copy(new(original.SourceRange.Offset, prefix));
        var payloadStart = Length;
        if (validatedPayload is not null) Bytes(validatedPayload);
        else if (buffer?.DiffersFromOriginal == true) Replacement(buffer);
        else Copy(original.PayloadRange);
        var payloadEnd = Length;
        Copy(new(original.PayloadRange.End, suffix));
        messages.Add(new(logical.Id, new(start, Length - start), new(payloadStart, payloadEnd - payloadStart), original.Framed));
    }
    public void MemoryOriginal(LogicalMessage logical, byte[] bytes, int bom, EditableMessageBuffer? buffer)
    {
        var start = Length;
        if (buffer?.DiffersFromOriginal == true) Replacement(buffer, bom); else Bytes(bytes, bom);
        messages.Add(new(logical.Id, new(start, Length - start), new(start, Length - start), false));
    }
    public void Inserted(LogicalMessage logical, EditableMessageBuffer buffer, bool framed)
    {
        var start = Length;
        if (framed) Bytes(frameStart);
        var payloadStart = Length; Replacement(buffer); var payloadEnd = Length;
        if (framed) Bytes(frameEnd);
        messages.Add(new(logical.Id, new(start, Length - start), new(payloadStart, payloadEnd - payloadStart), framed));
    }
    private void Read(Piece piece, long offset, Span<byte> destination, byte[]? serialized)
    {
        if (piece.Buffer is not null) serialized!.AsSpan(checked((int)(piece.Range.Offset + offset)), destination.Length).CopyTo(destination);
        else if (piece.Bytes is not null) piece.Bytes.AsSpan(checked((int)(piece.Range.Offset + offset)), destination.Length).CopyTo(destination);
        else snapshot!.Read(new(piece.Range.Offset + offset, destination.Length), destination);
    }
    public async Task WriteAsync(Stream output, IProgress<DocumentSaveProgress>? progress, CancellationToken token)
    {
        var clock = Stopwatch.StartNew(); var throttle = Stopwatch.StartNew(); var chunk = new byte[65536]; long written = 0;
        foreach (var piece in pieces)
        {
            token.ThrowIfCancellationRequested();
            var serialized = piece.Buffer?.Serialize();
            for (long offset = 0; offset < piece.Range.Length;)
            {
                token.ThrowIfCancellationRequested(); var count = (int)Math.Min(chunk.Length, piece.Range.Length - offset);
                Read(piece, offset, chunk.AsSpan(0, count), serialized);
                await output.WriteAsync(chunk.AsMemory(0, count), token).ConfigureAwait(false);
                offset += count; written += count;
                if (throttle.ElapsedMilliseconds >= 100) { progress?.Report(new("Writing", written, Length)); throttle.Restart(); }
            }
        }
        Metrics = Metrics with { OutputBytes = Length, Writing = clock.Elapsed };
        progress?.Report(new("Writing", written, Length));
    }
    public string Validate(FileStream staged, IProgress<DocumentSaveProgress> progress, CancellationToken token)
    {
        progress.Report(new("Indexing staged output", 0, Length)); var watch = Stopwatch.StartNew();
        if (staged.Length != Length) throw BoundaryFailure();
        var indexed = MessageFileIndex.BuildStaged(staged, token, inheritedEncoding: encoding);
        Metrics = Metrics with { Indexing = watch.Elapsed };
        ValidateIndex(indexed, token);
        progress.Report(new("Verifying staged bytes", 0, Length)); watch.Restart();
        var result = VerifyBytes(staged.Length, (range, destination) =>
        {
            var used = 0;
            while (used < destination.Length)
            {
                var read = RandomAccess.Read(staged.SafeFileHandle, destination[used..], range.Offset + used);
                if (read == 0) throw new EndOfStreamException(); used += read;
            }
        }, progress, token);
        Metrics = Metrics with { ByteVerification = watch.Elapsed };
        return result;
    }
    // Script capture uses the same membership/partition proof over its sealed
    // private snapshot. Save additionally verifies the destination's staged bytes.
    internal void ValidateIndex(MessageFileIndex indexed, CancellationToken token, bool requireMembership = false)
    {
        if (verifyMembership || requireMembership)
        {
            if (indexed.Messages.Count != messages.Count) throw BoundaryFailure();
            for (var i = 0; i < messages.Count; i++)
            {
                token.ThrowIfCancellationRequested(); var actual = indexed.Messages[i]; var expected = messages[i];
                // Identity lives only in this mapping. Equal counts/control IDs cannot establish membership.
                if (actual.SourceRange != expected.Source || actual.PayloadRange != expected.Payload || actual.Framed != expected.Framed)
                    throw BoundaryFailure();
            }
            var actualGaps = indexed.Regions.Where(r => r.MessageNumber is null).ToArray();
            if (actualGaps.Length != gaps.Count || actualGaps.Where((g, i) => g.Range != gaps[i].Range || g.Reason != gaps[i].Reason).Any())
                throw BoundaryFailure();
        }
    }
    private delegate void ReadRange(ByteRange range, Span<byte> destination);
    internal string ValidateSnapshot(SourceSnapshot staged, MessageFileIndex index, IProgress<DocumentSaveProgress> progress, CancellationToken token)
    {
        ValidateIndex(index, token, requireMembership: true);
        return VerifyBytes(staged.Length, staged.Read, progress, token);
    }
    private string VerifyBytes(long length, ReadRange read, IProgress<DocumentSaveProgress> progress, CancellationToken token)
    {
        if (length != Length) throw BoundaryFailure();
        // Verify the complete mapping, including opaque bytes and encoded replacements,
        // and compute the publication hash without another full candidate read.
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var expectedBytes = new byte[65536]; var actualBytes = new byte[65536]; long position = 0;
        var throttle = Stopwatch.StartNew();
        foreach (var piece in pieces)
        {
            var serialized = piece.Buffer?.Serialize();
            for (long offset = 0; offset < piece.Range.Length;)
            {
                token.ThrowIfCancellationRequested(); var count = (int)Math.Min(expectedBytes.Length, piece.Range.Length - offset);
                Read(piece, offset, expectedBytes.AsSpan(0, count), serialized);
                read(new(position, count), actualBytes.AsSpan(0, count));
                if (!expectedBytes.AsSpan(0, count).SequenceEqual(actualBytes.AsSpan(0, count))) throw BoundaryFailure();
                hash.AppendData(actualBytes, 0, count); offset += count; position += count;
                if (throttle.ElapsedMilliseconds >= 100) { progress.Report(new("Verifying staged bytes", position, Length)); throttle.Restart(); }
            }
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }
    private static InvalidOperationException BoundaryFailure() => new(
        "Save refused: staged output does not retain the intended messages, framing or source regions. Check message starts/endings; no automatic boundary repair was applied. Drafts retained.");
}
