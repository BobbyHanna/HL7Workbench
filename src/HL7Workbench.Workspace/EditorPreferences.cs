using System.Text.Json;

namespace HL7Workbench.Workspace;

/// <summary>Initial selection in explicit Apply Schema only; never activates validation or changes structural parsing.</summary>
public enum SchemaInterpretationMode { MessageDeclared, SelectedTarget }
public enum ApplicationThemePreference { FollowSystem, Light, Dark }

/// <summary>Application preferences are separate from placement-only workspace layouts.</summary>
public sealed record EditorPreferences(bool StartInEditMode = false, bool RibbonExpanded = false,
    SchemaInterpretationMode SchemaMode = SchemaInterpretationMode.MessageDeclared, string? TargetSchema = null,
    ApplicationThemePreference Theme = ApplicationThemePreference.FollowSystem)
{
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HL7Workbench", "editor-preferences.json");
    // Preserve settings from a newer app when saving known preferences. Schema choices
    // select explicit validation defaults; they never affect source bytes.
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalSettings { get; init; }
    public SearchPaneLayout? SearchLayout { get; init; }
    public static EditorPreferences Load(string path)
    {
        if (!File.Exists(path)) return new();
        if (new FileInfo(path).Length > 4096) throw new InvalidDataException("Oversized preferences.");
        var loaded = JsonSerializer.Deserialize<EditorPreferences>(File.ReadAllText(path)) ?? new();
        return loaded with { SchemaMode = Enum.IsDefined(loaded.SchemaMode) ? loaded.SchemaMode : SchemaInterpretationMode.MessageDeclared,
            Theme = Enum.IsDefined(loaded.Theme) ? loaded.Theme : ApplicationThemePreference.FollowSystem,
            SearchLayout = loaded.SearchLayout?.Validated() };
    }
    public void Save(string path)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var bytes = JsonSerializer.SerializeToUtf8Bytes(this);
            if (bytes.Length > 4096) throw new InvalidDataException("Oversized preferences.");
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { stream.Write(bytes); stream.Flush(true); }
            File.Move(temporary, full, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
