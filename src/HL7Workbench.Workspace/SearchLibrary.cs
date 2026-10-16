using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace HL7Workbench.Workspace;

public sealed record SavedSearchItem(Guid Id, string Name)
{ public override string ToString() => "Saved search"; }
public sealed record SavedSearch(Guid Id, string Name, MessageSearchQuery Query, string Revision)
{ public override string ToString() => "Saved search definition"; }
public sealed record SearchLibraryListing(IReadOnlyList<SavedSearchItem> Items, int InvalidEntries);

/// <summary>Explicit local query persistence, separate from scripts and results. Names never become paths.
/// Values may be sensitive: creation/update are explicit actions, never automatic history.</summary>
public sealed class SearchLibrary(string directory)
{
    public const int MaximumEntries = 256;
    public const int MaximumEntryBytes = 128 * 1024;
    public static string DefaultDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HL7Workbench", "Searches");
    private static readonly JsonSerializerOptions JsonOptions = new() { UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, RespectRequiredConstructorParameters = true, MaxDepth = 32 };
    // Whitelist query inputs rather than serializing a UI/session object (including its hidden inputs).
    private sealed record StoredCondition(SearchTarget Target, SearchOperator Operator, string Path, string Value, bool CaseSensitive);
    private sealed record StoredSearch(int Version, int QueryVersion, Guid Id, string Name, SearchBuilderMode Mode,
        SearchCombination Combination, StoredCondition[] Conditions);
    private sealed record StoredNode(StoredCondition? Condition, StoredGroup? Group);
    private sealed record StoredGroup(SearchCombination Combination, StoredNode[] Children);
    private sealed record StoredTree(int Version, int QueryVersion, Guid Id, string Name, SearchBuilderMode Mode, StoredGroup Root);
    private static StoredGroup Store(SearchGroup group) => new(group.Combination, group.Children.Select(c => c is SearchCondition leaf ?
        new StoredNode(new(leaf.Target, leaf.Operator, leaf.Path, leaf.Value, leaf.CaseSensitive), null) : new StoredNode(null, Store((SearchGroup)c))).ToArray());
    private static SearchGroup Restore(StoredGroup root)
    {
        var groups = 0; var leaves = 0;
        SearchGroup Visit(StoredGroup group, int depth)
        {
            if (++groups > MessageSearchQuery.MaximumGroups || depth > MessageSearchQuery.MaximumDepth || !Enum.IsDefined(group.Combination) ||
                group.Children is null || group.Children.Length is < 1 or > MessageSearchQuery.MaximumConditions + MessageSearchQuery.MaximumGroups) throw new InvalidDataException();
            var children = new List<SearchExpression>();
            foreach (var node in group.Children)
            {
                if (node is null || (node.Condition is null) == (node.Group is null)) throw new InvalidDataException();
                if (node.Condition is { } c)
                {
                    if (++leaves > MessageSearchQuery.MaximumConditions) throw new InvalidDataException();
                    children.Add(new SearchCondition(c.Target, c.Operator, c.Path, c.Value, c.CaseSensitive));
                }
                else children.Add(Visit(node.Group!, depth + 1));
            }
            return new(group.Combination, children.AsReadOnly());
        }
        return Visit(root, 1);
    }

    public static bool ValidName(string? name) => !string.IsNullOrWhiteSpace(name) && name.Length <= 120 &&
        name == name.Trim() && name is not ("." or "..") && !name.Any(c => char.IsControl(c) || "<>:\"/\\|?*".Contains(c));

    public SearchLibraryListing List()
    {
        try
        {
            if (!Directory.Exists(directory)) return new([], 0);
            var files = Directory.EnumerateFiles(directory, "*.json").Take(MaximumEntries + 1).ToArray();
            if (files.Length > MaximumEntries) throw new InvalidDataException("Search Library entry limit reached.");
            var items = new List<SavedSearchItem>(); var invalid = 0;
            foreach (var file in files)
            {
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out var id) || id == Guid.Empty) { invalid++; continue; }
                try { var entry = Open(id); items.Add(new(entry.Id, entry.Name)); }
                catch (InvalidDataException) { invalid++; }
            }
            return new(items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToArray(), invalid);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new IOException("Search Library unavailable or over its entry limit."); }
    }

    public SavedSearch Open(Guid id)
    {
        try
        {
            var path = PathFor(id); RejectLink(path); var bytes = ReadEntry(path);
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 32 });
            var version = json.RootElement.GetProperty("Version").GetInt32();
            string name; SearchBuilderMode mode; SearchGroup root;
            if (version == 1)
            {
                var old = JsonSerializer.Deserialize<StoredSearch>(bytes, JsonOptions);
                if (old is null || old.QueryVersion != 1 || old.Id != id || old.Conditions is null || old.Conditions.Length is < 1 or > MessageSearchQuery.MaximumConditions || old.Conditions.Any(c => c is null)) throw new InvalidDataException();
                name = old.Name; mode = old.Mode;
                root = new(old.Combination, old.Conditions.Select(c => (SearchExpression)new SearchCondition(c.Target, c.Operator, c.Path, c.Value, c.CaseSensitive)).ToArray());
            }
            else if (version == 2)
            {
                var stored = JsonSerializer.Deserialize<StoredTree>(bytes, JsonOptions);
                if (stored is null || stored.QueryVersion != MessageSearchQuery.CurrentVersion || stored.Id != id || stored.Root is null) throw new InvalidDataException();
                name = stored.Name; mode = stored.Mode; root = Restore(stored.Root);
            }
            else throw new InvalidDataException();
            if (!ValidName(name) || !Enum.IsDefined(mode) || MessageSearchQuery.ValidateTree(root) is not null) throw new InvalidDataException();
            MessageSearchQuery query;
            if (mode == SearchBuilderMode.Simple)
            {
                if (root.Children.Count != 1 || root.Children[0] is not SearchCondition condition || root.Combination != SearchCombination.All ||
                    condition.Target is not (SearchTarget.MessageText or SearchTarget.Field) ||
                    condition.Operator is not (SearchOperator.Contains or SearchOperator.Equals) ||
                    condition.Target == SearchTarget.MessageText && condition.Operator != SearchOperator.Contains || condition.ValidationError() is not null)
                    throw new InvalidDataException();
                query = new(condition.Target == SearchTarget.MessageText ? MessageSearchMode.Text : MessageSearchMode.Field,
                    condition.Value, condition.CaseSensitive, condition.Path, condition.Operator == SearchOperator.Equals ? MessageSearchComparison.Equals : MessageSearchComparison.Contains);
            }
            else query = MessageSearchQuery.Advanced(root);
            return new(id, name, query.Snapshot(), Hash(bytes));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException or InvalidOperationException or KeyNotFoundException or FormatException)
        { throw new InvalidDataException("Saved search unavailable, malformed or unsupported. Other definitions are unchanged."); }
    }

    public Task<SavedSearch> CreateAsync(string name, MessageSearchQuery query, CancellationToken token = default)
    {
        return PersistAsync(new(Guid.NewGuid(), name, query, ""), token);
    }
    public Task<SavedSearch> UpdateAsync(SavedSearch entry, MessageSearchQuery query, CancellationToken token = default)
    {
        return PersistAsync(entry with { Query = query }, token);
    }
    public async Task<SavedSearch> RenameAsync(SavedSearch entry, string name, CancellationToken token = default)
    {
        if (string.IsNullOrEmpty(entry.Revision)) throw new InvalidOperationException("Load a saved definition before renaming.");
        // Rename must preserve the stored query, even if an API caller supplies a
        // modified draft record. Only explicit Update may adopt new query values.
        var stored = await Task.Run(() => Open(entry.Id), token);
        if (stored.Revision != entry.Revision) throw Changed();
        return await PersistAsync(stored with { Name = name }, token);
    }
    public Task DeleteAsync(SavedSearch entry, CancellationToken token = default) => Task.Run(() =>
    {
        try
        {
            token.ThrowIfCancellationRequested();
            using var lease = Lease(); var path = PathFor(entry.Id); RejectLink(path);
            if (entry.Revision.Length == 0 || Hash(ReadEntry(path)) != entry.Revision) throw Changed();
            token.ThrowIfCancellationRequested(); File.Delete(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        { throw new IOException("Search Library delete failed or the definition changed. Reload before retrying."); }
    }, token);

    private Task<SavedSearch> PersistAsync(SavedSearch entry, CancellationToken token)
    {
        if (!ValidName(entry.Name)) throw new ArgumentException("Use a nonblank search name of at most 120 characters, without path or control characters.");
        var frozen = entry with { Query = entry.Query.Snapshot() };
        try
        {
            var utf8 = new UTF8Encoding(false, true); _ = utf8.GetByteCount(frozen.Name);
            foreach (var condition in frozen.Query.EffectiveConditions().Select(c => c.Canonical()))
            { _ = utf8.GetByteCount(condition.Path); _ = utf8.GetByteCount(condition.Value); }
        }
        catch (EncoderFallbackException) { throw new ArgumentException("Saved search names and values require valid Unicode; the current query is unchanged."); }
        return Task.Run(async () =>
        {
            try
            {
                token.ThrowIfCancellationRequested(); Directory.CreateDirectory(directory);
                using var lease = Lease(); var path = PathFor(frozen.Id); RejectLink(path);
                var exists = File.Exists(path);
                // Capture the destination version before our revision comparison, so a
                // subsequent external write cannot be adopted as replacement consent.
                var destination = exists ? await FileSaveDestination.CaptureAsync(path, token) : FileSaveDestination.NewFile(path);
                if (!destination.IsWithinDirectory(directory) || (exists ? frozen.Revision.Length == 0 || Hash(ReadEntry(path)) != frozen.Revision : frozen.Revision.Length != 0))
                    throw Changed();
                if (List().Items.Any(i => i.Id != frozen.Id && string.Equals(i.Name, frozen.Name, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("That search name is already in use. Choose another name.");
                if (!exists && Directory.EnumerateFiles(directory, "*.json").Take(MaximumEntries).Count() >= MaximumEntries)
                    throw new InvalidOperationException("Search Library entry limit reached.");
                var root = frozen.Query.BuilderMode == SearchBuilderMode.Simple ? new SearchGroup(SearchCombination.All, frozen.Query.EffectiveConditions().Select(c => (SearchExpression)c.Canonical()).ToArray()) : frozen.Query.EffectiveRoot;
                var stored = new StoredTree(2, MessageSearchQuery.CurrentVersion, frozen.Id, frozen.Name, frozen.Query.BuilderMode, Store(root));
                var bytes = JsonSerializer.SerializeToUtf8Bytes(stored, JsonOptions);
                if (bytes.Length > MaximumEntryBytes) throw new InvalidOperationException("Saved search size limit reached.");
                await destination.WriteAsync((output, cancellation) => output.WriteAsync(bytes, cancellation).AsTask(), token);
                return frozen with { Revision = Hash(bytes) };
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or JsonException)
            { throw new IOException("Search Library save failed or the definition changed. Reload before retrying."); }
        }, token);
    }
    private FileStream Lease() => new(Path.Combine(directory, "library.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
    private string PathFor(Guid id) => id == Guid.Empty ? throw new InvalidDataException() : Path.Combine(directory, id.ToString("N") + ".json");
    private static void RejectLink(string path)
    { if (new FileInfo(path).LinkTarget is not null) throw new IOException("Saved search links are unsupported."); }
    private static IOException Changed() => new("Saved search changed externally. Reload before retrying.");
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    private static byte[] ReadEntry(string path)
    {
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (input.Length > MaximumEntryBytes) throw new InvalidDataException();
        var bytes = new byte[(int)input.Length]; input.ReadExactly(bytes); return bytes;
    }
}
