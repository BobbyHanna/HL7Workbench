using HL7Workbench.Core;
using System.Security.Cryptography;
using System.Text;

namespace HL7Workbench.Workspace;

/// <summary>Owned immutable capture. Original pathnames are never reopened by navigation.</summary>
public sealed class SourceSnapshot : IDisposable
{
    private readonly FileStream lease;
    private readonly FileStream file;
    private bool disposed;
    public string DirectoryPath { get; }
    internal string? OriginalFilePath { get; }
    public long Length => file.Length;
    public const long MaxFileBytes = 16L * 1024 * 1024 * 1024;
    private SourceSnapshot(string directory, FileStream lease, FileStream file, string? originalPath)
    { DirectoryPath = directory; this.lease = lease; this.file = file; OriginalFilePath = originalPath; }

    public static async Task<SourceSnapshot> CaptureAsync(Stream source, CancellationToken token = default,
        IProgress<long>? progress = null, string? storageRoot = null)
    {
        var expected = source.CanSeek ? source.Length - source.Position : (long?)null;
        var sourceWriteTime = source is FileStream original ? File.GetLastWriteTimeUtc(original.Name) : (DateTime?)null;
        return await CreateAsync(async (output, cancellation) =>
        {
            var buffer = new byte[65536]; long length = 0;
            while (true)
            {
                var read = await source.ReadAsync(buffer, cancellation).ConfigureAwait(false);
                if (read == 0) break;
                length = checked(length + read);
                if (length > MaxFileBytes) throw new Hl7LimitException(nameof(MaxFileBytes));
                await output.WriteAsync(buffer.AsMemory(0, read), cancellation).ConfigureAwait(false);
                progress?.Report(length);
            }
            if (expected is { } size && (length != size || source.Length - source.Position != 0))
                throw new IOException("Source length changed during capture.");
            if (sourceWriteTime is { } time && source is FileStream originalFile && File.GetLastWriteTimeUtc(originalFile.Name) != time)
                throw new IOException("Source modification detected during capture.");
        }, MaxFileBytes, token, storageRoot, (source as FileStream)?.Name);
    }

    /// <summary>Builds a bounded private snapshot, then seals it read-only. Ownership transfers only after successful completion.</summary>
    internal static async Task<SourceSnapshot> CreateAsync(Func<Stream, CancellationToken, Task> write, long maximumBytes,
        CancellationToken token, string? storageRoot = null, string? originalPath = null)
    {
        if (maximumBytes < 0 || maximumBytes > MaxFileBytes) throw new ArgumentOutOfRangeException(nameof(maximumBytes));
        var owner = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Environment.UserName + "|" +
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))))[..16];
        var root = storageRoot ?? Path.Combine(Path.GetTempPath(), "HL7Workbench-snapshots-" + owner);
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(root);
        else Directory.CreateDirectory(root, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        CleanupAbandoned(root);
        var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        if (OperatingSystem.IsWindows()) Directory.CreateDirectory(directory);
        else Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        FileStream? lease = null;
        try
        {
            lease = new(Path.Combine(directory, "lease"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
            var path = Path.Combine(directory, "source.bin");
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                using var bounded = new BoundedOutputStream(output, maximumBytes);
                await write(bounded, token).ConfigureAwait(false);
                await output.FlushAsync(token).ConfigureAwait(false);
            }
            token.ThrowIfCancellationRequested();
            return new(directory, lease, new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1), originalPath);
        }
        catch
        {
            lease?.Dispose();
            RemoveOwnedDirectory(directory);
            throw;
        }
    }

    /// <summary>Reads exact bytes into caller-owned bounded storage.</summary>
    public void Read(ByteRange range, Span<byte> destination)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (range.Offset < 0 || range.Length != destination.Length || range.End > Length) throw new ArgumentOutOfRangeException(nameof(range));
        var filled = 0;
        while (filled < destination.Length)
        {
            var read = RandomAccess.Read(file.SafeFileHandle, destination[filled..], range.Offset + filled);
            if (read == 0) throw new EndOfStreamException();
            filled += read;
        }
    }
    public byte[] ReadBytes(ByteRange range, int maximum = 128 * 1024)
    {
        if (range.Length < 0 || range.Length > maximum) throw new Hl7LimitException("BoundedReadBytes");
        var bytes = new byte[checked((int)range.Length)];
        Read(range, bytes);
        return bytes;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        file.Dispose();
        lease.Dispose();
        RemoveOwnedDirectory(DirectoryPath);
    }
    private static void RemoveOwnedDirectory(string directory)
    {
        try { Directory.Delete(directory, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
    private static void CleanupAbandoned(string root)
    {
        // Only our GUID-named directories, older than one day, with an obtainable lease
        // are eligible. A live process retains its exclusive lease on every platform.
        foreach (var directory in Directory.EnumerateDirectories(root).Take(256))
        {
            try
            {
                // Another capture/process can dispose an entry after enumeration.
                // Metadata lookups need the same best-effort I/O guard as lease checks.
                if (!Guid.TryParseExact(Path.GetFileName(directory), "N", out _) ||
                    (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0 ||
                    Directory.GetLastWriteTimeUtc(directory) > DateTime.UtcNow.AddDays(-1)) continue;
                using (new FileStream(Path.Combine(directory, "lease"), FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                RemoveOwnedDirectory(directory);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }
}
