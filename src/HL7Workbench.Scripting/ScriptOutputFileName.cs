using System.Globalization;
using System.Text;

namespace HL7Workbench.Scripting;

/// <summary>Portable Windows filename policy for suggestions only. Never resolves paths or accesses a filesystem.</summary>
public static class ScriptOutputFileName
{
    public const string Fallback = "Script Result.hl7";
    /// <summary>Maximum UTF-16 length including the normalized .hl7 extension.</summary>
    public const int MaximumCharacters = 128;

    public static string FromScriptName(string? name) => TryNormalize(name, out var fileName) ? fileName : Fallback;

    /// <summary>Rejects paths, device names and unsafe text; appends .hl7 unless already present, ignoring extension case.</summary>
    public static bool TryNormalize(string? suggestion, out string fileName)
    {
        fileName = "";
        if (string.IsNullOrEmpty(suggestion) || suggestion.Length > MaximumCharacters ||
            char.IsWhiteSpace(suggestion[0]) || char.IsWhiteSpace(suggestion[^1]) || suggestion[^1] == '.') return false;
        for (var i = 0; i < suggestion.Length; i++)
        {
            var c = suggestion[i];
            if (c is '<' or '>' or ':' or '"' or '/' or '\\' or '|' or '?' or '*' || char.IsControl(c) ||
                char.GetUnicodeCategory(c) == UnicodeCategory.Format) return false;
            if (char.IsHighSurrogate(c))
            {
                if (++i >= suggestion.Length || !Rune.TryCreate(c, suggestion[i], out var scalar) ||
                    Rune.GetUnicodeCategory(scalar) == UnicodeCategory.Format) return false;
            }
            else if (char.IsLowSurrogate(c)) return false;
        }
        var stem = suggestion.EndsWith(".hl7", StringComparison.OrdinalIgnoreCase) ? suggestion[..^4] : suggestion;
        if (stem.Length == 0 || stem.Length + 4 > MaximumCharacters) return false;
        // Windows recognizes devices even before an extension, including superscript digits.
        // Use a fixed list rather than the host OS's invalid-character/path rules.
        var device = stem.Split('.')[0].TrimEnd(' ', '.');
        if (device.Equals("CON", StringComparison.OrdinalIgnoreCase) || device.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            device.Equals("AUX", StringComparison.OrdinalIgnoreCase) || device.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            device.Equals("CONIN$", StringComparison.OrdinalIgnoreCase) || device.Equals("CONOUT$", StringComparison.OrdinalIgnoreCase) ||
            device.Length == 4 && (device.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || device.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) &&
            device[3] is >= '1' and <= '9' or '¹' or '²' or '³') return false;
        fileName = stem + ".hl7";
        return true;
    }
}

// Optional versioned extension of the existing v2 Finished frame. Older readers
// ignore this metadata; missing/unknown naming versions fall back without altering payload publication.
internal sealed record ScriptOutputNaming(string? FileName = null, bool IgnoredSuggestion = false, int Version = 1);
