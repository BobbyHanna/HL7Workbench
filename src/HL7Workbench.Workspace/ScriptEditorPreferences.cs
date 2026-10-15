using System.Text.Json;

namespace HL7Workbench.Workspace;

/// <summary>Whole-point code font preference, independent of placement and device scaling.</summary>
public sealed record ScriptEditorPreferences(int FontSizePoints = ScriptEditorPreferences.DefaultPoints)
{
    public const int DefaultPoints = 12;
    public const int MinimumPoints = 9;
    public const int MaximumPoints = 24;
    public static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HL7Workbench", "script-editor-preferences.json");
    public static int BoundPoints(int points) => Math.Clamp(points, MinimumPoints, MaximumPoints);
    public static double ToDevicePixels(int points) => BoundPoints(points) * 4d / 3;

    // V1 stored DIPs and defaulted to 14 pt. Convert only on load; integer point
    // arithmetic thereafter avoids accumulated floating-point drift.
    private sealed record StoredPreferences(int Version = 1, double FontSize = 56d / 3, int FontSizePoints = DefaultPoints);

    public static ScriptEditorPreferences Load(string path)
    {
        if (!File.Exists(path)) return new();
        using var stream = File.OpenRead(path);
        if (stream.Length > 4096) throw new InvalidDataException("Oversized editor preferences.");
        var bytes = new byte[(int)stream.Length]; stream.ReadExactly(bytes);
        var stored = JsonSerializer.Deserialize<StoredPreferences>(bytes) ?? throw new InvalidDataException("Invalid editor preferences.");
        var points = stored.Version switch
        {
            1 => double.IsFinite(stored.FontSize) ? (int)Math.Round(Math.Clamp(stored.FontSize * .75, MinimumPoints, MaximumPoints), MidpointRounding.AwayFromZero) : DefaultPoints,
            2 => BoundPoints(stored.FontSizePoints),
            _ => throw new InvalidDataException("Unsupported editor preferences.")
        };
        return new(points);
    }

    public void Save(string path)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var temporary = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(new { Version = 2, FontSizePoints = BoundPoints(FontSizePoints) }));
            File.Move(temporary, full, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
