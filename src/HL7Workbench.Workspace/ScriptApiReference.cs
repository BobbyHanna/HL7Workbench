using System.Text.Json;

namespace HL7Workbench.Workspace;

/// <summary>One offline topic. Supported examples are complete lifecycle scripts; host-infrastructure topics have no runnable example.</summary>
public sealed record ScriptApiEntry(string Id, string Title, string Category, IReadOnlyList<string> Members,
    string Signature, string Description, string Parameters, string Returns, string Example,
    string Limitations, string Keywords, string? TemplateName = null)
{
    public string Errors { get; init; } = "";
    public string Mutation { get; init; } = "";
    public string NullBehavior { get; init; } = "";
    public string Modes { get; init; } = "";
    public IReadOnlyList<string> Related { get; init; } = [];
    public bool IsInfrastructure { get; init; }
    public IReadOnlyList<ScriptApiContract> Contracts { get; init; } = [];
    /// <summary>Runtime-derived signatures; prose-only workflow topics retain their explicit overview.</summary>
    public string ExactSignature => Contracts.Count == 0 ? Signature : string.Join('\n', Contracts.Select(c => c.Signature));
}

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
    public IReadOnlyList<ScriptApiContract> Contracts { get; }
    /// <summary>Public .NET orchestration surface, physically referenceable but unsupported for script authoring.</summary>
    public IReadOnlyList<ScriptApiContract> InfrastructureContracts { get; }
    private readonly Dictionary<string, string> searchText;

    private ScriptApiReference(Source source)
    {
        Version = source.Version; ApiVersion = source.ApiVersion; Scope = source.Scope;
        SupportedTypes = Array.AsReadOnly(source.SupportedTypes);
        InfrastructureTypes = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(source.InfrastructureTypes);
        Contracts = Array.AsReadOnly(ScriptApiContracts.Read(SupportedTypes));
        InfrastructureContracts = Array.AsReadOnly(ScriptApiContracts.Read(InfrastructureTypes.Keys.ToArray()));
        // The topic map must account for each actual overload exactly once. New
        // bindings cannot silently disappear from help or acquire invented docs.
        if (!Contracts.Select(c => c.Member).Order().SequenceEqual(source.Entries.SelectMany(e => e.Members).Order()))
            throw new InvalidDataException("Script API reference is out of sync with runtime bindings.");
        Entries = Array.AsReadOnly(source.Entries.Select(e => e with
        {
            Members = Array.AsReadOnly(e.Members.ToArray()), Related = Array.AsReadOnly(e.Related.ToArray()),
            Contracts = e.IsInfrastructure ? InfrastructureContracts : Array.AsReadOnly(Contracts.Where(c => e.Members.Contains(c.Member)).ToArray())
        }).ToArray());
        if (Entries.Any(e => e.Related.Any(id => !Entries.Any(t => t.Id == id)) ||
            new[] { e.Errors, e.Mutation, e.NullBehavior, e.Modes }.Any(string.IsNullOrWhiteSpace)))
            throw new InvalidDataException("Script API behavior documentation is incomplete.");
        Categories = Array.AsReadOnly(Entries.Select(e => e.Category).Distinct().ToArray());
        searchText = Entries.ToDictionary(e => e.Id, e => string.Join('\n', e.Id, e.Title, e.Category,
            string.Join(' ', e.Members), e.Signature, e.ExactSignature, e.Description, e.Parameters, e.Returns, e.Limitations, e.Keywords,
            e.Errors, e.Mutation, e.NullBehavior, e.Modes,
            e.TemplateName is null ? "" : "starter template " + e.TemplateName));
    }
    /// <summary>Complete machine-readable catalog including actual runtime signatures and reviewed behavior contracts.</summary>
    public string ExportJson() => JsonSerializer.Serialize(new { Version, ApiVersion, ContractVersion = 1, Scope, SupportedTypes,
        InfrastructureTypes, Contracts, InfrastructureContracts, Entries }, new JsonSerializerOptions { WriteIndented = true });

    /// <summary>Find supported authoring topics by their declared runtime member key, preserving overload groups.</summary>
    public IReadOnlyList<ScriptApiEntry> ForMember(string member) => Entries.Where(e => e.Members.Contains(member)).ToArray();
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
