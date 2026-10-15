using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HL7Workbench.Scripting;

namespace HL7Workbench.Workspace;

public sealed record ScriptLibraryItem(Guid Id, string Name, bool Bundled = false);
public sealed record LibraryScript(Guid Id, string Name, string Source, string Revision, bool Bundled = false);

/// <summary>Versioned local entries. Source and name share one atomic record so renames cannot tear a script save.</summary>
public sealed class ScriptLibrary(string directory)
{
    public const int MaximumEntries = 256;
    private const int MaximumEntryBytes = 512 * 1024;
    public static readonly Guid ExampleId = new("bb560a82-6b4f-4ccf-ac58-0712e27b4c30");
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HL7Workbench", "Scripts");
    public static LibraryScript Example => new(ExampleId, "Example — synthetic name replacement", """
        // Duplicate this example to create an editable copy.
        // Changes only the chosen name fields. This is not complete de-identification.
        foreach (var pid in Message.Segments("PID"))
        {
            pid.SetText("5.1", "SYNTHETIC");
            pid.SetText("5.2", "EXAMPLE");
        }
        """, "bundled-v1", true);
    private sealed record StoredScript(int Version, Guid Id, string Name, string Source);
    public IReadOnlyList<ScriptLibraryItem> List()
    {
        var result = new List<ScriptLibraryItem> { new(Example.Id, Example.Name, true) };
        if (!Directory.Exists(directory)) return result;
        var files = Directory.EnumerateFiles(directory, "*.json").Take(MaximumEntries + 1).ToArray();
        if (files.Length > MaximumEntries) throw new InvalidDataException("Script Library entry limit reached.");
        foreach (var file in files)
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var id) || id == ExampleId)
                throw new InvalidDataException("Invalid Script Library identity.");
            var entry = Open(id);
            result.Add(new(entry.Id, entry.Name));
        }
        return result.OrderByDescending(i => i.Bundled).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public LibraryScript Open(Guid id)
    {
        if (id == ExampleId) return Example;
        var bytes = ReadEntry(PathFor(id));
        var entry = JsonSerializer.Deserialize<StoredScript>(bytes) ?? throw new InvalidDataException("Invalid script record.");
        if (entry.Version != 1 || entry.Id != id) throw new InvalidDataException("Unsupported script record.");
        Validate(entry.Name, entry.Source);
        return new(id, entry.Name, entry.Source, Hash(bytes));
    }
    public LibraryScript Create(string name, string source = "") => Save(new(Guid.NewGuid(), name, source, ""));
    public LibraryScript Duplicate(LibraryScript source) => Create(source.Name[..Math.Min(source.Name.Length, 113)] + " (copy)", source.Source);
    public LibraryScript Save(LibraryScript script)
    {
        if (script.Bundled || script.Id == ExampleId) throw new InvalidOperationException("Duplicate the bundled example before saving changes.");
        Validate(script.Name, script.Source); Directory.CreateDirectory(directory);
        var path = PathFor(script.Id);
        // Cooperating app instances serialize compare-and-save; external changes are detected
        // by a hash of the complete entry rather than timestamps alone.
        using var lease = new FileStream(Path.Combine(directory, "library.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var exists = File.Exists(path);
        if (exists ? script.Revision.Length == 0 || Hash(ReadEntry(path)) != script.Revision : script.Revision.Length != 0)
            throw new IOException("Script changed externally. Reopen it or duplicate the draft before saving.");
        if (!exists && Directory.EnumerateFiles(directory, "*.json").Take(MaximumEntries).Count() >= MaximumEntries)
            throw new InvalidDataException("Script Library entry limit reached.");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new StoredScript(1, script.Id, script.Name, script.Source));
        if (bytes.Length > MaximumEntryBytes) throw new InvalidDataException("Script record size limit reached.");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes); output.Flush(true); }
            if (exists) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            return script with { Revision = Hash(bytes) };
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
    private string PathFor(Guid id) => Path.Combine(directory, id.ToString("N") + ".json");
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static byte[] ReadEntry(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > MaximumEntryBytes) throw new InvalidDataException("Script record size limit reached.");
        var bytes = new byte[checked((int)stream.Length)]; stream.ReadExactly(bytes); return bytes;
    }
    private static void Validate(string name, string source)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120 || name.Any(char.IsControl) || source is null || source.Length > ScriptLimits.ScriptCharacters)
            throw new InvalidDataException("Script name or source is outside its limits.");
        _ = new UTF8Encoding(false, true).GetByteCount(source);
    }
}
