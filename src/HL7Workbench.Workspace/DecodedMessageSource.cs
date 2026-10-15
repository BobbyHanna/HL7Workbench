using System.Runtime.InteropServices;
using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Strict UTF-16 spool with sparse original-byte checkpoints. Resident text is bounded.</summary>
public sealed class DecodedMessageSource : IHl7TextSource, IDisposable
{
    private readonly FileStream text;
    private readonly Encoding encoding;
    private readonly List<(long Character, long Byte)> checkpoints;
    private readonly byte[] cacheBytes = new byte[65536];
    private readonly char[] cache = new char[32768];
    private long cacheStart = -1;
    private int cacheLength;
    private CancellationToken token;
    public long Length { get; }
    public long LongestLine { get; }
    public int BomLength { get; }
    public ByteRange Payload { get; }
    // Rebound only under the document's exclusive work gate. A single cached decoded
    // spool can survive navigation without retaining old structural/UI projections.
    internal void BindCancellation(CancellationToken cancellationToken) => token = cancellationToken;
    public const int MaxReadCharacters = 128 * 1024;
    private DecodedMessageSource(FileStream text, Encoding encoding, List<(long, long)> checkpoints,
        long length, long longestLine, int bomLength, ByteRange payload, CancellationToken token)
    { this.text = text; this.encoding = encoding; this.checkpoints = checkpoints; Length = length;
        LongestLine = longestLine; BomLength = bomLength; Payload = payload; this.token = token; }

    public static DecodedMessageSource Create(SourceSnapshot snapshot, ByteRange payload, Encoding encoding,
        int bomLength, CancellationToken token = default, IProgress<long>? progress = null)
    {
        encoding = (Encoding)encoding.Clone();
        encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
        var path = Path.Combine(snapshot.DirectoryPath, Guid.NewGuid().ToString("N") + ".text");
        var output = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read, 65536,
            FileOptions.DeleteOnClose | FileOptions.RandomAccess);
        try
        {
            var decoder = encoding.GetDecoder();
            var bytes = new byte[65536];
            var chars = new char[65536];
            var spool = new byte[131072];
            var points = new List<(long, long)> { (0, bomLength) };
            long position = bomLength, characters = 0, encoded = bomLength, line = 0, longest = 0;
            while (position < payload.Length)
            {
                token.ThrowIfCancellationRequested();
                var count = (int)Math.Min(bytes.Length, payload.Length - position);
                snapshot.Read(new(payload.Offset + position, count), bytes.AsSpan(0, count));
                var used = 0;
                while (used < count)
                {
                    decoder.Convert(bytes.AsSpan(used, count - used), chars, position + count == payload.Length,
                        out var bytesUsed, out var charsUsed, out _);
                    used += bytesUsed;
                    // Checkpoints use produced characters, never the decoder's buffered trailing bytes.
                    encoded = checked(encoded + encoding.GetByteCount(chars.AsSpan(0, charsUsed)));
                    var written = Encoding.Unicode.GetBytes(chars.AsSpan(0, charsUsed), spool);
                    output.Write(spool, 0, written);
                    foreach (var c in chars.AsSpan(0, charsUsed))
                    {
                        if (c is '\r' or '\n') { longest = Math.Max(longest, line); line = 0; }
                        else line++;
                    }
                    characters = checked(characters + charsUsed);
                    if (points.Count >= 1_000_000) throw new Hl7LimitException("CoordinateCheckpoints");
                    points.Add((characters, encoded));
                }
                position += count;
                progress?.Report(position);
            }
            // Flush also validates incomplete final code units, including an empty payload.
            decoder.Convert([], chars, true, out _, out var finalChars, out _);
            if (finalChars != 0) throw new InvalidDataException("Unexpected decoder remainder.");
            output.Flush();
            return new(output, encoding, points, characters, Math.Max(longest, line), bomLength, payload, token);
        }
        catch { output.Dispose(); throw; }
    }
    private void Load(long offset)
    {
        token.ThrowIfCancellationRequested();
        if (offset < 0 || offset >= Length) throw new ArgumentOutOfRangeException(nameof(offset));
        if (offset >= cacheStart && offset < cacheStart + cacheLength) return;
        cacheStart = offset / cache.Length * cache.Length;
        cacheLength = (int)Math.Min(cache.Length, Length - cacheStart);
        var filled = 0;
        while (filled < cacheLength * 2)
        {
            var read = RandomAccess.Read(text.SafeFileHandle, cacheBytes.AsSpan(filled, cacheLength * 2 - filled), checked(cacheStart * 2 + filled));
            if (read == 0) throw new EndOfStreamException();
            filled += read;
        }
        if (BitConverter.IsLittleEndian) MemoryMarshal.Cast<byte, char>(cacheBytes.AsSpan(0, filled)).CopyTo(cache);
        else for (var i = 0; i < cacheLength; i++) cache[i] = (char)(cacheBytes[i * 2] | cacheBytes[i * 2 + 1] << 8);
    }
    public char this[long offset] { get { Load(offset); return cache[checked((int)(offset - cacheStart))]; } }
    public string Read(CharacterRange range)
    {
        if (range.Offset < 0 || range.Length < 0 || range.End > Length) throw new ArgumentOutOfRangeException(nameof(range));
        if (range.Length > MaxReadCharacters) throw new Hl7LimitException("BoundedReadCharacters");
        var result = new char[checked((int)range.Length)];
        var filled = 0;
        while (filled < result.Length)
        {
            Load(range.Offset + filled);
            var start = checked((int)(range.Offset + filled - cacheStart));
            var count = Math.Min(result.Length - filled, cacheLength - start);
            cache.AsSpan(start, count).CopyTo(result.AsSpan(filled));
            filled += count;
        }
        return new(result);
    }
    public long IndexOfAny(ReadOnlySpan<char> characters, long start, long length)
    {
        var end = checked(start + length);
        if (start < 0 || length < 0 || end > Length) throw new ArgumentOutOfRangeException(nameof(start));
        while (start < end)
        {
            Load(start);
            var local = checked((int)(start - cacheStart));
            var count = (int)Math.Min(end - start, cacheLength - local);
            var found = cache.AsSpan(local, count).IndexOfAny(characters);
            if (found >= 0) return start + found;
            start += count;
        }
        return -1;
    }
    public long Align(long offset)
    {
        offset = Math.Clamp(offset, 0, Length);
        return offset > 0 && offset < Length && char.IsLowSurrogate(this[offset]) && char.IsHighSurrogate(this[offset - 1]) ? offset - 1 : offset;
    }
    public string ReadWindow(long offset, int maximum = MaxReadCharacters)
    {
        if (maximum < 2 || maximum > MaxReadCharacters) throw new ArgumentOutOfRangeException(nameof(maximum));
        offset = Align(offset);
        var end = Math.Min(Length, checked(offset + maximum));
        end = Align(end);
        return Read(new(offset, end - offset));
    }
    /// <summary>Message-local original-byte coordinates, including any retained interior preamble.</summary>
    public ByteRange? Bytes(CharacterRange range)
    {
        if (range.Offset < 0 || range.Length < 0 || range.End > Length) throw new ArgumentOutOfRangeException(nameof(range));
        var start = BytePosition(range.Offset);
        return new(start, BytePosition(range.End) - start);
    }
    public long BytePosition(long character)
    {
        if (character < 0 || character > Length || Align(character) != character) throw new ArgumentOutOfRangeException(nameof(character));
        var low = 0; var high = checkpoints.Count;
        while (low < high) { var mid = low + (high - low) / 2; if (checkpoints[mid].Character <= character) low = mid + 1; else high = mid; }
        var point = checkpoints[Math.Max(0, low - 1)];
        return checked(point.Byte + encoding.GetByteCount(Read(new(point.Character, character - point.Character))));
    }
    /// <summary>Incrementally reads the complete logical value without a full-value allocation.</summary>
    public IEnumerable<string> ReadChunks(CharacterRange range, int maximum = 4096)
    {
        if (range.Offset < 0 || range.Length < 0 || range.End > Length || Align(range.Offset) != range.Offset || Align(range.End) != range.End)
            throw new ArgumentOutOfRangeException(nameof(range));
        for (var position = range.Offset; position < range.End;)
        {
            var value = ReadWindow(position, (int)Math.Min(maximum, Math.Max(2, range.End - position)));
            if (position + value.Length > range.End) value = value[..checked((int)(range.End - position))];
            yield return value;
            position += value.Length;
        }
    }
    public void Dispose() => text.Dispose();
}
