using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HL7Workbench.Workspace;

public sealed record SavedExtraction(Guid Id, string Name, ExtractionConfiguration Configuration, string Revision, bool BuiltIn = false)
{ public override string ToString() => "Saved extraction configuration"; }
public sealed record ExtractionLibraryItem(Guid Id, string Name, bool BuiltIn = false)
{ public override string ToString() => "Extraction library item"; }
public sealed record ExtractionLibraryListing(IReadOnlyList<ExtractionLibraryItem> Items, int InvalidEntries);

/// <summary>Bounded explicit configuration records, separate from application presets and all extracted/session values.</summary>
public sealed class ExtractionLibrary(string directory)
{
    public static readonly Guid OverviewId = new("56eccd64-9679-466d-8a18-e0a660a71533");
    public const int MaximumEntries = 256;
    private const int MaximumBytes = 128 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, MaxDepth = 16 };
    private sealed record Stored(int Version, Guid Id, string Name, ExtractionConfiguration Configuration);
    public static SavedExtraction InterfaceOverview => new(OverviewId, "Interface Overview", new ExtractionConfiguration(new[]
    {
        new ExtractionColumn("Message Type", "MSH-9"), new ExtractionColumn("Message Control ID", "MSH-10"),
        new ExtractionColumn("Patient Identifier", "PID-3"), new ExtractionColumn("Patient Name", "PID-5"),
        new ExtractionColumn("Visit Number", "PV1-19"), new ExtractionColumn("Universal Service ID", "OBR-4")
    }).Snapshot(), "built-in-v1", true);
    public ExtractionLibraryListing List()
    {
        var items = new List<ExtractionLibraryItem> { new(OverviewId, "Interface Overview", true) }; var invalid = 0;
        if (!Directory.Exists(directory)) return new(items, invalid);
        var files = Directory.EnumerateFiles(directory, "*.json").Take(MaximumEntries + 1).ToArray();
        if (files.Length > MaximumEntries) throw new IOException("Extraction library entry limit reached.");
        foreach (var file in files)
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var id) || id == OverviewId || id == Guid.Empty) { invalid++; continue; }
            try { var entry = Open(id); items.Add(new(entry.Id, entry.Name)); } catch (InvalidDataException) { invalid++; }
        }
        return new(items.OrderByDescending(i => i.BuiltIn).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray(), invalid);
    }
    public SavedExtraction Open(Guid id)
    {
        if (id == OverviewId) return InterfaceOverview;
        try
        {
            var path = PathFor(id); RejectLink(path); var bytes = Read(path);
            var record = JsonSerializer.Deserialize<Stored>(bytes, JsonOptions) ?? throw new InvalidDataException();
            if (record.Version != 1 || record.Id != id || !SearchLibrary.ValidName(record.Name)) throw new InvalidDataException();
            return new(id, record.Name, record.Configuration.Snapshot(), Hash(bytes));
        }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException or FormatException or NullReferenceException)
        { throw new InvalidDataException("Extraction configuration is unavailable, malformed or unsupported."); }
    }
    public Task<SavedExtraction> SaveAsync(SavedExtraction entry, CancellationToken token = default)
    {
        if (entry.BuiltIn || entry.Id == OverviewId || entry.Id == Guid.Empty) throw new InvalidOperationException("Duplicate the built-in before editing.");
        if (!SearchLibrary.ValidName(entry.Name)) throw new ArgumentException("Use a valid configuration name of at most 120 characters.");
        var frozen = entry with { Configuration = entry.Configuration.Snapshot() };
        _ = new System.Text.UTF8Encoding(false, true).GetByteCount(frozen.Name);
        return Task.Run(async () =>
        {
            token.ThrowIfCancellationRequested(); Directory.CreateDirectory(directory);
            using var lease = Lease(); var path = PathFor(frozen.Id); RejectLink(path); var exists = File.Exists(path);
            var destination = exists ? await FileSaveDestination.CaptureAsync(path, token).ConfigureAwait(false) : FileSaveDestination.NewFile(path);
            if (!destination.IsWithinDirectory(directory) || (exists ? frozen.Revision.Length == 0 || Hash(Read(path)) != frozen.Revision : frozen.Revision.Length != 0))
                throw new IOException("Configuration changed externally. Reload before saving.");
            if (List().Items.Any(i => i.Id != frozen.Id && string.Equals(i.Name, frozen.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Choose another configuration name.");
            if (!exists && Directory.EnumerateFiles(directory, "*.json").Take(MaximumEntries).Count() >= MaximumEntries)
                throw new IOException("Extraction library entry limit reached.");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Stored(1, frozen.Id, frozen.Name, frozen.Configuration), JsonOptions);
            if (bytes.Length > MaximumBytes) throw new InvalidDataException("Configuration exceeds its storage limit.");
            await destination.WriteAsync((output, ct) => output.WriteAsync(bytes, ct).AsTask(), token).ConfigureAwait(false);
            return frozen with { Revision = Hash(bytes) };
        }, token);
    }
    public Task DeleteAsync(SavedExtraction entry, CancellationToken token = default) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested();
        if (entry.BuiltIn || entry.Id == OverviewId || entry.Revision.Length == 0) throw new InvalidOperationException("Only saved user configurations can be deleted.");
        using var lease = Lease(); var path = PathFor(entry.Id); RejectLink(path);
        if (Hash(Read(path)) != entry.Revision) throw new IOException("Configuration changed externally. Reload before deleting.");
        token.ThrowIfCancellationRequested(); File.Delete(path);
    }, token);
    private FileStream Lease() => new(Path.Combine(directory, "library.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    private string PathFor(Guid id) => id == Guid.Empty ? throw new InvalidDataException() : Path.Combine(directory, id.ToString("N") + ".json");
    private static void RejectLink(string path) { if (new FileInfo(path).LinkTarget is not null) throw new IOException("Configuration links are unsupported."); }
    private static byte[] Read(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaximumBytes) throw new InvalidDataException();
        var bytes = new byte[(int)input.Length]; input.ReadExactly(bytes); return bytes;
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
}
