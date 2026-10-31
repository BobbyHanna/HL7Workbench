using System.ComponentModel;
using System.Text.Json;

namespace HL7Workbench.Workspace;

/// <summary>One shared preference state. A failed save never applies a partial UI change.</summary>
public sealed class ApplicationPreferences : INotifyPropertyChanged
{
    private readonly string path;
    public EditorPreferences Current { get; private set; } = new();
    public string Status { get; private set; } = "";
    public event PropertyChangedEventHandler? PropertyChanged;
    public ApplicationPreferences(string path)
    {
        this.path = path;
        try { Current = EditorPreferences.Load(path); }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
        { Status = "Preferences unavailable; safe defaults are in use. Apply to save new preferences."; }
    }
    public bool Apply(EditorPreferences next)
    {
        next = next with { ContentFonts = next.ContentFonts?.Validated() };
        try { next.Save(path); Current = next; Status = "Preferences saved."; }
        catch (Exception e) when (e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { Status = "Preferences could not be saved; previous preferences remain active."; PropertyChanged?.Invoke(this, new(nameof(Status))); return false; }
        PropertyChanged?.Invoke(this, new(nameof(Current))); return true;
    }
    public bool SetContentFont(string tool, double? size)
    {
        if (!ContentFontPreferences.ToolKeys.Contains(tool)) return false;
        var fonts = (Current.ContentFonts ?? new()).Validated();
        var overrides = new Dictionary<string, double>(fonts.Overrides, StringComparer.Ordinal);
        if (size is { } value) overrides[tool] = ContentFontPreferences.Bound(value); else overrides.Remove(tool);
        return Apply(Current with { ContentFonts = fonts with { Overrides = overrides } });
    }
}
