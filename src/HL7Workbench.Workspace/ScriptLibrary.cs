using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HL7Workbench.Scripting;

namespace HL7Workbench.Workspace;

public sealed record ScriptLibraryItem(Guid Id, string Name, bool Bundled = false, bool Obsolete = false);
public sealed record LibraryScript(Guid Id, string Name, string Source, string Revision, bool Bundled = false, bool Obsolete = false);

/// <summary>Versioned local entries. Source and name share one atomic record so renames cannot tear a script save.</summary>
public sealed partial class ScriptLibrary(string directory)
{
    public const int MaximumEntries = 256;
    private const int MaximumEntryBytes = 512 * 1024;
    public static readonly Guid ExampleId = new("bb560a82-6b4f-4ccf-ac58-0712e27b4c30");
    public static readonly Guid PatientExampleId = new("afe3e16b-d4a7-46e1-bf93-6ba417437bb1");
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HL7Workbench", "Scripts");
    public static LibraryScript Example => new(ExampleId, "Example — synthetic name replacement", """
        // Duplicate this example to create an editable copy.
        // Changes only the chosen name fields. This is not complete de-identification.
        void Main()
        {
            Message.SetText("PID-5.1", "SYNTHETIC");
        }
        """, "bundled-unified-v1", true);
    public static LibraryScript PatientExample => new(PatientExampleId, "Example — consistent synthetic patients", """
        // Duplicate to edit. This is NOT complete clinical de-identification or a HIPAA guarantee.
        // Fail the whole run on missing/ambiguous PID identity; never guess correlations.
        // Raw composite keys include assigning authority; spelling differences stay distinct.
        // Keys/mappings remain in this worker only. Summary contains counts, never source values.
        var patients = new Dictionary<string, int>(StringComparer.Ordinal);
        var count = 0;
        var summary = Batch.Report("synthetic-summary", "Patients", "Messages");
        void Main()
        {
            var message = Message;
            if (!message.CanEdit || message.HasDiagnostics || message.Segments("PID").Count != 1 ||
                message.Read("PID-3.1").State != ValueState.Value ||
                string.IsNullOrWhiteSpace(message.Text("PID-3.1")) ||
                message.Read("PID-3[2]").State != ValueState.Missing)
                throw new InvalidOperationException();
            var key = message.Read("PID-3").Raw!;
            if (!patients.TryGetValue(key, out var identity))
            {
                if (patients.Count >= 10000) throw new InvalidOperationException();
                identity = patients.Count + 1;
                patients.Add(key, identity);
            }
            var number = identity.ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            message.SetText("PID-3", "SYN-P" + number);
            message.SetText("PID-5", "SYNTHETIC");
            message.SetText("PID-5.2", "PATIENT" + number);
            message.SetText("PID-7", "20000101");
            message.SetText("PID-8", "U");
            foreach (var field in new[] { "2", "4", "9", "11", "13", "14", "18", "19", "20" })
                message.Clear("PID-" + field);
            count++;
        }
        void Complete()
        {
            summary.Row(patients.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        """, "bundled-unified-v1", true);
    private sealed record StoredScript(int Version, Guid Id, string Name, string Source, bool RequiresUpdate = false);
    public IReadOnlyList<ScriptLibraryItem> List()
    {
        var result = Builtins.Select(s => new ScriptLibraryItem(s.Id, s.Name, true)).ToList();
        if (!Directory.Exists(directory)) return result;
        var files = Directory.EnumerateFiles(directory, "*.json").Take(MaximumEntries + 1).ToArray();
        if (files.Length > MaximumEntries) throw new InvalidDataException("Script Library entry limit reached.");
        foreach (var file in files)
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var id) || IsBuiltin(id))
                throw new InvalidDataException("Invalid Script Library identity.");
            var entry = Open(id);
            result.Add(new(entry.Id, entry.Name, Obsolete: entry.Obsolete));
        }
        return result.OrderByDescending(i => i.Bundled).ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    }
    public LibraryScript Open(Guid id)
    {
        if (Builtins.FirstOrDefault(s => s.Id == id) is { } builtin) return builtin;
        var bytes = ReadEntry(PathFor(id));
        var entry = JsonSerializer.Deserialize<StoredScript>(bytes) ?? throw new InvalidDataException("Invalid script record.");
        if (entry.Version is not (1 or 2 or 3) || entry.Id != id) throw new InvalidDataException("Unsupported script record.");
        Validate(entry.Name, entry.Source);
        return new(id, entry.Name, entry.Source, Hash(bytes), Obsolete: entry.Version < 3 || entry.RequiresUpdate);
    }
    public LibraryScript Create(string name, string source = "") => Save(new(Guid.NewGuid(), name, source, ""));
    public LibraryScript Duplicate(LibraryScript source) => Save(new(Guid.NewGuid(), source.Name[..Math.Min(source.Name.Length, 113)] + " (copy)",
        source.Source, "", Obsolete: source.Obsolete), adopt: false);
    /// <summary>Explicitly saves the author's draft as unified format. Obsolete source is retained until this user-controlled adoption.</summary>
    public LibraryScript Save(LibraryScript script) => Save(script, adopt: true);
    /// <summary>Deletes one saved user record after checking its revision under the same lock as Save.
    /// An external change must be reviewed before deletion; bundled examples are never filesystem entries.</summary>
    public void Delete(LibraryScript script)
    {
        if (script.Bundled || IsBuiltin(script.Id) || script.Revision.Length == 0)
            throw new InvalidOperationException("Only saved user scripts can be deleted.");
        using var lease = new FileStream(Path.Combine(directory, "library.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var path = PathFor(script.Id);
        if (Hash(ReadEntry(path)) != script.Revision) throw new IOException("Script changed externally. Reopen before deleting.");
        File.Delete(path);
    }
    public static bool ValidName(string name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 120 && !name.Any(char.IsControl);
    private LibraryScript Save(LibraryScript script, bool adopt)
    {
        if (script.Bundled || IsBuiltin(script.Id)) throw new InvalidOperationException("Duplicate the bundled example before saving changes.");
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
        var obsolete = !adopt && script.Obsolete;
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new StoredScript(3, script.Id, script.Name, script.Source, obsolete));
        if (bytes.Length > MaximumEntryBytes) throw new InvalidDataException("Script record size limit reached.");
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { output.Write(bytes); output.Flush(true); }
            if (exists) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            return script with { Revision = Hash(bytes), Obsolete = obsolete };
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
        if (!ValidName(name) || source is null || source.Length > ScriptLimits.ScriptCharacters)
            throw new InvalidDataException("Script name or source is outside its limits.");
        _ = new UTF8Encoding(false, true).GetByteCount(source);
    }
}
