using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace HL7Workbench.Workspace;

/// <summary>A local save destination scoped to its authorized version. Callers must obtain
/// explicit user consent before authorizing replacement; a path alone permits creation only.</summary>
public sealed class FileSaveDestination
{
    // Stage concurrently, but serialize the final version check/publication in this
    // process. Otherwise two saves could validate the same old version before either
    // publishes. External name-swap races still require filesystem compare-and-replace.
    private static readonly object PublicationGate = new();
    private readonly string physicalPath;
    private readonly Version? version;
    public string Path { get; }
    public bool ReplacesExistingFile => version is not null;
    internal bool IsWithinDirectory(string directory) => physicalPath.StartsWith(PhysicalPath(directory) + System.IO.Path.DirectorySeparatorChar, Comparison);
    private FileSaveDestination(string path, string physicalPath, Version? version)
    { Path = path; this.physicalPath = physicalPath; this.version = version; }

    /// <summary>Allows only creation. A destination collision is never interpreted as consent.</summary>
    public static FileSaveDestination NewFile(string path) => new(System.IO.Path.GetFullPath(path), PhysicalPath(path), null);

    /// <summary>Captures the version immediately after trusted picker consent, or immediately
    /// before an application confirmation. Keep this same instance through writing/publication.</summary>
    public static Task<FileSaveDestination> CaptureAsync(string path, CancellationToken token = default) => Task.Run(() =>
    {
        var full = System.IO.Path.GetFullPath(path); var physical = PhysicalPath(full);
        using var input = OpenExisting(physical);
        return new FileSaveDestination(full, physical, input is null ? null : ReadVersion(input, token));
    }, token);

    // Capture the opened handle's identity, not a later version of its pathname. Hashing
    // happens while SourceSnapshot copies the stream, so opening adds no second file read.
    internal static Func<string, FileSaveDestination> BeginSnapshotCapture(FileStream input)
    {
        var before = ReadIdentity(input); var path = System.IO.Path.GetFullPath(input.Name); var physical = PhysicalPath(path);
        return hash => before == ReadIdentity(input) ? new(path, physical, new(before, hash)) :
            throw new IOException("Source changed during capture.");
    }
    internal FileSaveDestination AtSameLocation(string path)
    {
        var full = System.IO.Path.GetFullPath(path);
        if (!string.Equals(physicalPath, PhysicalPath(full), Comparison)) throw new IOException("Source location changed during open.");
        return new(full, physicalPath, version);
    }
    internal static async Task<FileSaveDestination> MatchSnapshotAsync(string path, string hash, CancellationToken token)
    {
        var captured = await CaptureAsync(path, token).ConfigureAwait(false);
        if (captured.version?.Hash != hash) throw new IOException("Source destination does not match the captured document.");
        return captured;
    }
    internal Task CheckCurrentAsync(CancellationToken token) => Task.Run(() =>
    {
        if (!string.Equals(physicalPath, PhysicalPath(Path), Comparison)) throw new IOException("Destination location changed; use Save As.");
        using var current = OpenExisting(physicalPath);
        if (version is null ? current is not null : current is null || version != ReadVersion(current, token))
            throw new IOException("Destination changed since open or the last save. Use Save As; working state is retained.");
    }, token);

    /// <summary>Stages, durably flushes and closes content before publication. Existing files
    /// require the captured version and filesystem replacement support; there is no destructive fallback.</summary>
    public Task WriteAsync(Func<Stream, CancellationToken, Task> write, CancellationToken token = default)
        => WriteCoreAsync(write, null, false, token);

    /// <summary>Validation borrows the flushed candidate before publication. Retain its verified
    /// content hash with the immediately observed published identity; never adopt external content.</summary>
    internal Task<FileSaveDestination> WriteValidatedAsync(Func<Stream, CancellationToken, Task> write,
        Func<FileStream, CancellationToken, string> validate, CancellationToken token)
        => WriteCoreAsync(write, validate, true, token);

    private async Task<FileSaveDestination> WriteCoreAsync(Func<Stream, CancellationToken, Task> write,
        Func<FileStream, CancellationToken, string>? validate, bool retainVersion, CancellationToken token)
    {
        var temporary = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(physicalPath)!, ".hl7-save-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
            {
                await write(output, token).ConfigureAwait(false);
                await output.FlushAsync(token).ConfigureAwait(false); output.Flush(true);
            }
            token.ThrowIfCancellationRequested();
            using var staged = OpenExisting(temporary) ?? throw new IOException("Staged output is unavailable.");
            var stagedIdentity = ReadIdentity(staged);
            var stagedHash = validate?.Invoke(staged, token);
            var stagedVersion = retainVersion ? new Version(stagedIdentity, stagedHash!) : null;
            if (stagedIdentity != ReadIdentity(staged)) throw new IOException("Staged output changed during validation.");
            return await Task.Run(() =>
            {
                lock (PublicationGate)
                {
                    token.ThrowIfCancellationRequested();
                    if (!string.Equals(physicalPath, PhysicalPath(Path), Comparison)) throw new IOException("Destination location changed; choose it again.");
                    using (var namedStage = OpenExisting(temporary))
                        if (namedStage is null || ReadIdentity(namedStage) != stagedIdentity) throw new IOException("Staged output identity changed.");
                    if (version is null)
                    {
                        return Publish(false);
                    }
                    // On Windows this read handle denies in-place writes until replacement.
                    // Delete sharing is needed for File.Replace; name replacement can still race
                    // the last check. Portable filesystems expose no compare-and-replace primitive.
                    using var current = OpenExisting(physicalPath);
                    if (current is null || version != ReadVersion(current, token)) throw new IOException("Destination changed; choose it again.");
                    using var named = OpenExisting(physicalPath);
                    if (named is null || ReadIdentity(current) != ReadIdentity(named)) throw new IOException("Destination identity changed; choose it again.");
                    return Publish(true);
                }
                FileSaveDestination Publish(bool replace)
                {
                    // Windows ReplaceFile requires closing replacement-file readers. Keep
                    // validation write-denying until the final identity-checked handoff.
                    // No portable filesystem compare-and-replace primitive closes this name race.
                    staged.Dispose();
                    using (var publicationLease = OpenExisting(temporary))
                        if (publicationLease is null || ReadIdentity(publicationLease) != stagedIdentity) throw new IOException("Staged output identity changed.");
                    token.ThrowIfCancellationRequested();
                    if (replace) File.Replace(temporary, physicalPath, null);
                    else File.Move(temporary, physicalPath, overwrite: false);
                    if (stagedVersion is null) return new(Path, physicalPath, null);
                    // Publication has committed: cancellation or failure to inspect its new
                    // name must not report an unpublished save. Retain the validated hash;
                    // never adopt externally changed bytes as our associated save baseline.
                    var identity = stagedIdentity;
                    try
                    {
                        using var published = OpenExisting(physicalPath);
                        if (published is not null)
                        {
                            var observed = ReadIdentity(published);
                            if (!OperatingSystem.IsWindows() || observed.Volume == stagedIdentity.Volume && observed.FileId == stagedIdentity.FileId)
                                identity = observed;
                        }
                    }
                    catch (IOException) { } catch (UnauthorizedAccessException) { }
                    return new(Path, physicalPath, new(identity, stagedVersion.Hash));
                }
            }, token).ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(temporary); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
    }
    private static StringComparison Comparison => OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
    private static FileStream? OpenExisting(string path)
    {
        try { return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 65536); }
        catch (FileNotFoundException) { return null; }
    }
    private sealed record Version(Identity Identity, string Hash);
    private readonly record struct Identity(long Length, long Created, long Modified, uint Volume, ulong FileId);
    private static Version ReadVersion(FileStream input, CancellationToken token)
    {
        var before = ReadIdentity(input);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256); var buffer = new byte[65536];
        input.Position = 0; int count;
        while ((count = input.Read(buffer)) != 0) { token.ThrowIfCancellationRequested(); hash.AppendData(buffer, 0, count); }
        if (before != ReadIdentity(input)) throw new IOException("Destination changed while checking it.");
        return new(before, Convert.ToHexString(hash.GetHashAndReset()));
    }
    private static Identity ReadIdentity(FileStream input)
    {
        if (OperatingSystem.IsWindows())
        {
            if (!GetFileInformationByHandle(input.SafeFileHandle, out var info)) throw new IOException("Unable to check destination identity.", new Win32Exception(Marshal.GetLastWin32Error()));
            return new(((long)info.SizeHigh << 32) | info.SizeLow, ((long)info.CreatedHigh << 32) | info.CreatedLow,
                ((long)info.ModifiedHigh << 32) | info.ModifiedLow, info.Volume, ((ulong)info.IndexHigh << 32) | info.IndexLow);
        }
        var file = new FileInfo(input.Name);
        return new(input.Length, file.CreationTimeUtc.Ticks, file.LastWriteTimeUtc.Ticks, 0, 0);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInformation
    {
        public uint Attributes, CreatedLow, CreatedHigh, AccessedLow, AccessedHigh, ModifiedLow, ModifiedHigh,
            Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out FileInformation information);

    private static string PhysicalPath(string path)
    {
        var full = System.IO.Path.GetFullPath(path); var current = System.IO.Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split([System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = System.IO.Path.Combine(current, part);
            FileSystemInfo? item = Directory.Exists(current) ? new DirectoryInfo(current) : File.Exists(current) ? new FileInfo(current) : null;
            if (item?.LinkTarget is not null) current = item.ResolveLinkTarget(true)?.FullName ?? current;
        }
        return current;
    }
}
