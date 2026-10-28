using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Local read-only file/folder boundary. No URL, recursive discovery, persistence or external service.</summary>
public static class FhirPatientImport
{
    public static Task<FhirImportSession> FilesAsync(IEnumerable<string> paths, bool syntheticConfirmed,
        FhirImportLimits? limits = null, IProgress<FhirImportProgress>? progress = null, CancellationToken token = default)
    {
        if (!syntheticConfirmed) throw new InvalidOperationException("Confirm synthetic/test data before selecting an import.");
        return FilesAsync(paths, limits, progress, token);
    }
    /// <summary>Reads explicitly selected local JSON files without an acknowledgment gate.</summary>
    public static Task<FhirImportSession> FilesAsync(IEnumerable<string> paths,
        FhirImportLimits? limits = null, IProgress<FhirImportProgress>? progress = null, CancellationToken token = default)
    {
        limits ??= new(); CheckFileLimit(limits); var selected = paths.Take(limits.Files + 1).ToArray();
        return Task.Run(() => ImportAsync(selected, limits, progress, token), token);
    }
    public static Task<FhirImportSession> FolderAsync(string directory, bool syntheticConfirmed,
        FhirImportLimits? limits = null, IProgress<FhirImportProgress>? progress = null, CancellationToken token = default)
    {
        if (!syntheticConfirmed) throw new InvalidOperationException("Confirm synthetic/test data before selecting an import.");
        return FolderAsync(directory, limits, progress, token);
    }
    /// <summary>Reads the top-level local folder under existing file, path and resource limits.</summary>
    public static Task<FhirImportSession> FolderAsync(string directory,
        FhirImportLimits? limits = null, IProgress<FhirImportProgress>? progress = null, CancellationToken token = default)
    {
        limits ??= new(); CheckFileLimit(limits);
        return Task.Run(async () =>
        {
            token.ThrowIfCancellationRequested();
            try
            {
                directory = LocalPath(directory);
                var paths = Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly).Take(limits.Files + 1).ToArray();
                return await ImportAsync(paths, limits, progress, token).ConfigureAwait(false);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
            { throw Unavailable(); }
        }, token);
    }
    private static async Task<FhirImportSession> ImportAsync(string[] paths, FhirImportLimits limits, IProgress<FhirImportProgress>? progress, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
            if (paths.Length > limits.Files) throw Unavailable();
            var selected = paths.Select(LocalPath).Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
                .Order(StringComparer.Ordinal).ToArray();
            if (selected.Any(p => !string.Equals(Path.GetExtension(p), ".json", StringComparison.OrdinalIgnoreCase))) throw Unavailable();
            var inputs = selected.Select(path => new FhirImportInput(ct =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult<Stream>(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.SequentialScan));
            }));
            return await FhirR4Importer.ImportAsync(inputs, limits, token, progress).ConfigureAwait(false);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        { throw Unavailable(); }
    }
    private static InvalidDataException Unavailable() => new("Local synthetic FHIR import unavailable. Check file/folder access, JSON selection and import limits; source files and previous session retained.");
    private static void CheckFileLimit(FhirImportLimits limits)
    {
        // Validate before discovery/Take, including before Files + 1 can overflow.
        if (limits.Files is < 1 or > 64) throw new ArgumentException("Unsupported synthetic FHIR import limits.");
    }
    private static string LocalPath(string path)
    {
        var full = Path.GetFullPath(path); var root = Path.GetPathRoot(full)!;
        if (OperatingSystem.IsWindows() && (full.StartsWith(@"\\", StringComparison.Ordinal) || new DriveInfo(root).DriveType == DriveType.Network)) throw Unavailable();
        var current = root;
        foreach (var part in Path.GetRelativePath(root, full).Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            // Refuse redirects rather than accidentally reading a remote target through a local junction.
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw Unavailable();
        }
        return full;
    }
}
