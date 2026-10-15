using System.Text.Json;

namespace HL7Workbench.Workspace;

/// <summary>Application preferences are separate from placement-only workspace layouts.</summary>
public sealed record EditorPreferences(bool StartInEditMode = false)
{
    public static EditorPreferences Load(string path)
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 4096) throw new InvalidDataException("Oversized preferences.");
        return JsonSerializer.Deserialize<EditorPreferences>(File.ReadAllText(path)) ?? new();
    }
    public void Save(string path)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temporary, JsonSerializer.Serialize(this)); File.Move(temporary, full, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
