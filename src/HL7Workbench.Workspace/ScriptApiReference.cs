using System.Text.Json;

namespace HL7Workbench.Workspace;

/// <summary>One offline reference topic. Examples are complete unified-lifecycle scripts.</summary>
public sealed record ScriptApiEntry(string Id, string Title, string Category, IReadOnlyList<string> Members,
    string Signature, string Description, string Parameters, string Returns, string Example,
    string Limitations, string Keywords, string? TemplateName = null);

/// <summary>The versioned repository reference is embedded verbatim; opening help never reads user files or uses a network.</summary>
public sealed class ScriptApiReference
{
    private sealed record Source(int Version, int ApiVersion, string Scope, string[] SupportedTypes,
        Dictionary<string, string> InfrastructureTypes, ScriptApiEntry[] Entries);
    private static readonly Lazy<ScriptApiReference> current = new(Load);
    public static ScriptApiReference Current => current.Value;
    public int Version { get; }
    public int ApiVersion { get; }
    public string Scope { get; }
    public IReadOnlyList<string> SupportedTypes { get; }
    public IReadOnlyDictionary<string, string> InfrastructureTypes { get; }
    public IReadOnlyList<ScriptApiEntry> Entries { get; }
    public IReadOnlyList<string> Categories { get; }
    private readonly Dictionary<string, string> searchText;

    private ScriptApiReference(Source source)
    {
        Version = source.Version; ApiVersion = source.ApiVersion; Scope = source.Scope;
        SupportedTypes = Array.AsReadOnly(source.SupportedTypes);
        InfrastructureTypes = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(source.InfrastructureTypes);
        Entries = Array.AsReadOnly(source.Entries.Select(e => e with { Members = Array.AsReadOnly(e.Members.ToArray()) }).ToArray());
        Categories = Array.AsReadOnly(Entries.Select(e => e.Category).Distinct().ToArray());
        searchText = Entries.ToDictionary(e => e.Id, e => string.Join('\n', e.Id, e.Title, e.Category,
            string.Join(' ', e.Members), e.Signature, e.Description, e.Parameters, e.Returns, e.Limitations, e.Keywords,
            e.TemplateName is null ? "" : "starter template " + e.TemplateName));
    }
    /// <summary>Literal, case-insensitive AND search over a small immutable catalog; no regex or code execution.</summary>
    public IReadOnlyList<ScriptApiEntry> Search(string? query = null, string? category = null)
    {
        var words = (query ?? "")[..Math.Min(query?.Length ?? 0, 256)].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return Entries.Where(e => (category is null || e.Category == category) &&
            words.All(word => searchText[e.Id].Contains(word, StringComparison.OrdinalIgnoreCase))).ToArray();
    }
    private static ScriptApiReference Load()
    {
        using var stream = typeof(ScriptApiReference).Assembly.GetManifestResourceStream("HL7Workbench.ScriptApiReference.json")
            ?? throw new InvalidDataException("Bundled Script API Reference is unavailable.");
        var source = JsonSerializer.Deserialize<Source>(stream) ?? throw new InvalidDataException("Invalid bundled Script API Reference.");
        if (source.Version != 1 || source.ApiVersion != 2 || source.Entries.Length is < 1 or > 256 ||
            source.Entries.Select(e => e.Id).Distinct().Count() != source.Entries.Length)
            throw new InvalidDataException("Unsupported bundled Script API Reference.");
        return new(source);
    }
}
