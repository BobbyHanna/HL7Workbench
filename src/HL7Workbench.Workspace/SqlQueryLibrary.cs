using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HL7Workbench.Querying;

namespace HL7Workbench.Workspace;

public sealed record SavedSqlQuery(Guid Id, string Name, string Sql, string Revision)
{ public override string ToString() => "Saved SQL query"; }
public sealed record SqlQueryLibraryItem(Guid Id, string Name)
{ public override string ToString() => "SQL query definition"; }
public sealed record SqlQueryLibraryListing(IReadOnlyList<SqlQueryLibraryItem> Items, int InvalidEntries);

/// <summary>Explicit local definitions only. GUID paths, revision checks and atomic publication follow Search Library conventions.
/// SQL may contain sensitive literals; callers must disclose storage. No result, history or document identity is serialized.</summary>
public sealed class SqlQueryLibrary(string directory)
{
    public const int MaximumEntries = 256;
    public const int MaximumEntryBytes = 512 * 1024;
    public const string StorageDisclosure = "Save stores the query name and SQL locally as unencrypted JSON. SQL literals may contain sensitive data. History and results are never saved automatically.";
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HL7Workbench", "SqlQueries");
    private static readonly UTF8Encoding Utf8 = new(false, true);
    private static readonly JsonSerializerOptions Json = new() { MaxDepth = 8, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true };
    private sealed record Stored(int Version, Guid Id, string Name, string Sql);
    public static bool ValidName(string? name) => SearchLibrary.ValidName(name);
    public static bool ValidSql(string? sql)
    {
        if (string.IsNullOrWhiteSpace(sql) || sql.Length > QueryLimits.MaximumSqlCharacters || sql.Contains('\0')) return false;
        try { _ = Utf8.GetByteCount(sql); return true; } catch (EncoderFallbackException) { return false; }
    }
    public SqlQueryLibraryListing List()
    {
        if (!Directory.Exists(directory)) return new([], 0);
        var files = Directory.EnumerateFiles(directory, "*.json").Take(MaximumEntries + 1).ToArray();
        if (files.Length > MaximumEntries) throw new InvalidDataException("SQL Library entry limit reached.");
        var items = new List<SqlQueryLibraryItem>(); var invalid = 0;
        foreach (var file in files)
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var id) || id == Guid.Empty) { invalid++; continue; }
            try { var entry = Open(id); items.Add(new(entry.Id, entry.Name)); }
            catch (InvalidDataException) { invalid++; }
        }
        return new(items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray(), invalid);
    }
    public SavedSqlQuery Open(Guid id)
    {
        try
        {
            var path = PathFor(id); RejectLink(path); var bytes = Read(path);
            var value = JsonSerializer.Deserialize<Stored>(bytes, Json);
            if (value is null || value.Version != 1 || value.Id != id || !ValidName(value.Name) || !ValidSql(value.Sql)) throw new InvalidDataException();
            _ = Utf8.GetByteCount(value.Name); return new(id, value.Name, value.Sql, Hash(bytes));
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException)
        { throw new InvalidDataException("SQL definition unavailable, malformed or unsupported; other definitions are unchanged."); }
    }
    public Task<SavedSqlQuery> CreateAsync(string name, string sql, CancellationToken token = default) => PersistAsync(new(Guid.NewGuid(), name, sql, ""), token);
    public Task<SavedSqlQuery> UpdateAsync(SavedSqlQuery entry, string name, string sql, CancellationToken token = default) => PersistAsync(entry with { Name = name, Sql = sql }, token);
    /// <summary>Renames the saved definition without adopting an unsaved SQL draft.</summary>
    public async Task<SavedSqlQuery> RenameAsync(SavedSqlQuery entry, string name, CancellationToken token = default)
    {
        var stored = await Task.Run(() => Open(entry.Id), token);
        if (stored.Revision != entry.Revision || entry.Revision.Length == 0) throw Changed();
        return await PersistAsync(stored with { Name = name }, token);
    }
    public Task DeleteAsync(SavedSqlQuery entry, CancellationToken token = default) => Task.Run(() =>
    {
        token.ThrowIfCancellationRequested(); using var lease = Lease(); var path = PathFor(entry.Id); RejectLink(path);
        if (entry.Revision.Length == 0 || Hash(Read(path)) != entry.Revision) throw Changed();
        token.ThrowIfCancellationRequested(); File.Delete(path);
    }, token);
    private Task<SavedSqlQuery> PersistAsync(SavedSqlQuery entry, CancellationToken token)
    {
        if (!ValidName(entry.Name) || !ValidSql(entry.Sql)) throw new ArgumentException("Use a valid query name and bounded Unicode SQL text.");
        _ = Utf8.GetByteCount(entry.Name);
        return Task.Run(async () =>
        {
            token.ThrowIfCancellationRequested(); Directory.CreateDirectory(directory); using var lease = Lease();
            var path = PathFor(entry.Id); RejectLink(path); var exists = File.Exists(path);
            var destination = exists ? await FileSaveDestination.CaptureAsync(path, token) : FileSaveDestination.NewFile(path);
            if (!destination.IsWithinDirectory(directory) || (exists ? entry.Revision.Length == 0 || Hash(Read(path)) != entry.Revision : entry.Revision.Length != 0)) throw Changed();
            var listing = List();
            if (listing.Items.Any(i => i.Id != entry.Id && i.Name.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("SQL query name already in use.");
            if (!exists && Directory.EnumerateFiles(directory, "*.json").Take(MaximumEntries).Count() >= MaximumEntries)
                throw new InvalidOperationException("SQL Library entry limit reached.");
            var bytes = JsonSerializer.SerializeToUtf8Bytes(new Stored(1, entry.Id, entry.Name, entry.Sql), Json);
            if (bytes.Length > MaximumEntryBytes) throw new InvalidDataException("SQL definition size limit reached.");
            await destination.WriteAsync((output, ct) => output.WriteAsync(bytes, ct).AsTask(), token);
            return entry with { Revision = Hash(bytes) };
        }, token);
    }
    private FileStream Lease() => new(Path.Combine(directory, "library.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    private string PathFor(Guid id) => id == Guid.Empty ? throw new InvalidDataException() : Path.Combine(directory, id.ToString("N") + ".json");
    private static void RejectLink(string path) { if (new FileInfo(path).LinkTarget is not null) throw new IOException("SQL definition links are unsupported."); }
    private static IOException Changed() => new("SQL definition changed externally; reload before retrying.");
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static byte[] Read(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumEntryBytes) throw new InvalidDataException();
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes); return bytes;
    }
}
