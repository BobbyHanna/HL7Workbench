using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed record ValidationProfileEntry(Hl7ValidationProfile Profile, string SourceFile, DateTimeOffset ImportedAt, string Revision, bool IsBuiltIn)
{
    public override string ToString() => $"{Profile.Name} · {Profile.Hl7Version} · {Profile.MessageType}^{Profile.TriggerEvent}";
}

/// <summary>Local bounded profile definitions only; never stores messages, diagnostics or applied-document selections.</summary>
public sealed class ValidationProfileLibrary
{
    public const long MaximumLibraryBytes = 16 * 1024 * 1024;
    private readonly string? directory;
    private readonly Dictionary<string, ValidationProfileEntry> imported = new(StringComparer.Ordinal);
    private static readonly JsonSerializerOptions json = new() { MaxDepth = 32, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
    private sealed record Stored(int Version, DateTimeOffset ImportedAt, string SourceFile, byte[] ProfileJson);
    public int InvalidEntries { get; private set; }
    public event EventHandler? Changed;
    public ValidationProfileLibrary(string? directory = null)
    {
        this.directory = directory is null ? null : Path.GetFullPath(directory);
        if (this.directory is null || !Directory.Exists(this.directory)) return;
        string[] files;
        try { RejectLink(this.directory); files = Directory.EnumerateFiles(this.directory, "*.json").Take(Hl7ValidationLimits.Profiles + 1).ToArray(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { InvalidEntries = 1; return; }
        if (files.Length > Hl7ValidationLimits.Profiles) { InvalidEntries = files.Length; return; }
        long total = 0;
        foreach (var file in files.Order(StringComparer.Ordinal))
        {
            try
            {
                RejectLink(file); var size = new FileInfo(file).Length; total += size;
                if (size > Hl7ValidationLimits.ProfileBytes * 2 || total > MaximumLibraryBytes) throw new InvalidDataException("Profile library size budget exceeded.");
                var bytes = File.ReadAllBytes(file); var stored = JsonSerializer.Deserialize<Stored>(bytes, json) ?? throw new InvalidDataException();
                if (stored.Version != 1 || stored.ProfileJson is null || stored.SourceFile is null || stored.SourceFile.Length > 256 || stored.SourceFile.Any(char.IsControl)) throw new InvalidDataException();
                var profile = Hl7ProfileImporter.ImportJson(stored.ProfileJson);
                if (Hl7ValidationCatalog.IsBuiltInId(profile.Id) || file != PathFor(profile.Id) || imported.ContainsKey(profile.Id)) throw new InvalidDataException();
                imported.Add(profile.Id, new(profile, stored.SourceFile, stored.ImportedAt, Digest(bytes), false));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or ArgumentException or JsonException or InvalidDataException)
            { InvalidEntries++; }
        }
    }
    public IReadOnlyList<ValidationProfileEntry> Entries => Array.AsReadOnly(Hl7ValidationCatalog.BuiltIns.Select(p => new ValidationProfileEntry(p, "Built-in / attributed nHapi v2.5.1 model", DateTimeOffset.MinValue, "builtin-1", true))
        .Concat(imported.Values).OrderBy(e => e.Profile.Name, StringComparer.OrdinalIgnoreCase).ThenBy(e => e.Profile.Id, StringComparer.Ordinal).ToArray());
    public ValidationProfileEntry? Find(string id) => Entries.FirstOrDefault(e => e.Profile.Id == id);
    public Hl7ResolvedProfile Resolve(string id) => Hl7ProfileResolver.Resolve(id, Entries.Select(e => e.Profile));
    /// <summary>Explicit import/update. Updates require the reviewed existing revision; all affected descendants are resolved before atomic publication.</summary>
    public ValidationProfileEntry Import(ReadOnlyMemory<byte> bytes, bool xml, string sourceFile, string? expectedRevision = null, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested(); var profile = xml ? Hl7ProfileImporter.ImportXml(bytes, token) : Hl7ProfileImporter.ImportJson(bytes, token);
        if (Hl7ValidationCatalog.IsBuiltInId(profile.Id)) throw new ArgumentException("Built-in profile identifiers cannot be replaced.");
        var existing = imported.GetValueOrDefault(profile.Id);
        if (existing is not null && (expectedRevision is null || existing.Revision != expectedRevision)) throw new InvalidOperationException("Profile identifier already exists or changed. Select it and explicitly reimport using its current revision.");
        if (existing is null && expectedRevision is not null) throw new InvalidOperationException("The profile selected for replacement is no longer present.");
        if (existing is null && imported.Count >= Hl7ValidationLimits.Profiles) throw new InvalidDataException("Profile library entry limit reached.");
        sourceFile = Path.GetFileName(sourceFile);
        if (sourceFile.Length is < 1 or > 256 || sourceFile.Any(char.IsControl)) throw new ArgumentException("Source file label is invalid.");
        var candidate = Entries.Select(e => e.Profile).Where(p => p.Id != profile.Id).Append(profile).ToArray();
        _ = Hl7ProfileResolver.Resolve(profile.Id, candidate);
        // Existing unrelated missing/corrupt parents remain explicitly unavailable;
        // an update must not break any currently resolvable descendant.
        foreach (var entry in imported.Values.Where(e => e.Profile.Id != profile.Id))
        {
            token.ThrowIfCancellationRequested();
            Hl7ResolvedProfile before;
            try { before = Resolve(entry.Profile.Id); } catch (ArgumentException) { continue; }
            if (before.Lineage.Contains(profile.Id, StringComparer.Ordinal)) _ = Hl7ProfileResolver.Resolve(entry.Profile.Id, candidate);
        }
        var canonical = Hl7ProfileImporter.ExportJson(profile);
        if (canonical.Length > Hl7ValidationLimits.ProfileBytes) throw new InvalidDataException("Canonical profile exceeds its reloadable storage limit.");
        var stored = new Stored(1, DateTimeOffset.UtcNow, sourceFile, canonical);
        var content = JsonSerializer.SerializeToUtf8Bytes(stored, json); token.ThrowIfCancellationRequested();
        if (directory is not null)
        {
            Directory.CreateDirectory(directory); RejectLink(directory);
            var total = Directory.EnumerateFiles(directory, "*.json").Sum(f => new FileInfo(f).Length) - (existing is null ? 0 : new FileInfo(PathFor(profile.Id)).Length);
            if (total + content.Length > MaximumLibraryBytes) throw new InvalidDataException("Profile library byte budget reached.");
            var path = PathFor(profile.Id); RejectLink(path);
            if (existing is null ? File.Exists(path) : !File.Exists(path) || Digest(File.ReadAllBytes(path)) != existing.Revision)
                throw new InvalidOperationException("Profile storage changed externally. Reload the library before replacing it.");
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(content); stream.Flush(true); }
                token.ThrowIfCancellationRequested(); File.Move(temporary, path, existing is not null);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        var result = new ValidationProfileEntry(profile, sourceFile, stored.ImportedAt, Digest(content), false);
        imported[profile.Id] = result; Changed?.Invoke(this, EventArgs.Empty); return result;
    }
    public void Remove(string id, string expectedRevision)
    {
        if (Hl7ValidationCatalog.IsBuiltInId(id)) throw new ArgumentException("Built-in profiles cannot be removed.");
        if (!imported.TryGetValue(id, out var existing) || existing.Revision != expectedRevision) throw new InvalidOperationException("Profile changed or is unavailable; reload before removing it.");
        if (directory is not null)
        {
            var path = PathFor(id); RejectLink(path);
            if (!File.Exists(path) || Digest(File.ReadAllBytes(path)) != expectedRevision) throw new InvalidOperationException("Profile storage changed externally; reload before removing it.");
            File.Delete(path);
        }
        imported.Remove(id); Changed?.Invoke(this, EventArgs.Empty);
    }
    private string PathFor(string id) => Path.Combine(directory!, Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(id))) + ".json");
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static void RejectLink(string path)
    { if ((File.Exists(path) || Directory.Exists(path)) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked profile storage is unsupported."); }
}
