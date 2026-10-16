using System.Text.Json;
using System.Text.Json.Nodes;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class SearchLibraryTests
{
    [Theory]
    [InlineData(SearchCombination.All)] [InlineData(SearchCombination.Any)]
    public async Task FlatVersionOneLoadsWithoutWritingAndExplicitUpdateMigratesWithRevisionGuard(SearchCombination combination)
    {
        using var files = new Files(); Directory.CreateDirectory(files.DirectoryPath); var id = Guid.NewGuid();
        var old = JsonSerializer.SerializeToUtf8Bytes(new { Version = 1, QueryVersion = 1, Id = id, Name = "Synthetic legacy", Mode = SearchBuilderMode.Advanced, Combination = combination,
            Conditions = new[] { new { Target = SearchTarget.Field, Operator = SearchOperator.Equals, Path = "PID-3.1", Value = "SYN-001", CaseSensitive = true },
                new { Target = SearchTarget.Segment, Operator = SearchOperator.DoesNotExist, Path = "PV1", Value = "", CaseSensitive = false } } });
        await File.WriteAllBytesAsync(files.PathFor(id), old, Token);
        var loaded = files.Library.Open(id); Assert.Equal("Synthetic legacy", loaded.Name); Assert.Equal(combination, loaded.Query.Root!.Combination);
        Assert.Equal(2, loaded.Query.Root.Children.Count); Assert.Equal("SYN-001", loaded.Query.Conditions[0].Value); Assert.True(loaded.Query.Conditions[0].CaseSensitive);
        Assert.Single(files.Library.List().Items); Assert.Equal(old, await File.ReadAllBytesAsync(files.PathFor(id), Token));
        var updated = await files.Library.UpdateAsync(loaded, loaded.Query, Token); Assert.NotEqual(loaded.Revision, updated.Revision);
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(files.PathFor(id), Token)); Assert.Equal(2, json.RootElement.GetProperty("Version").GetInt32());
        Assert.Equal(combination, files.Library.Open(id).Query.Root!.Combination);
        await Assert.ThrowsAsync<IOException>(() => files.Library.UpdateAsync(loaded, loaded.Query, Token));
        Assert.Equal(updated.Revision, files.Library.Open(id).Revision);
    }
    [Fact]
    public async Task SimpleVersionOneMigratesWithoutChangingLiteralComparison()
    {
        using var files = new Files(); Directory.CreateDirectory(files.DirectoryPath); var id = Guid.NewGuid();
        await File.WriteAllTextAsync(files.PathFor(id), JsonSerializer.Serialize(new { Version = 1, QueryVersion = 1, Id = id, Name = "Synthetic simple", Mode = SearchBuilderMode.Simple, Combination = SearchCombination.All,
            Conditions = new[] { new { Target = SearchTarget.Field, Operator = SearchOperator.Equals, Path = "PID-3.1", Value = " SYN-001 ", CaseSensitive = true } } }), Token);
        var loaded = files.Library.Open(id); Assert.Equal(SearchBuilderMode.Simple, loaded.Query.BuilderMode); Assert.Equal(" SYN-001 ", loaded.Query.Value);
        Assert.Equal(MessageSearchComparison.Equals, loaded.Query.Comparison); Assert.True(loaded.Query.CaseSensitive);
        await files.Library.RenameAsync(loaded, "Synthetic migrated", Token); Assert.Equal(" SYN-001 ", files.Library.Open(id).Query.Value);
    }
    [Fact]
    public async Task NestedTreesRoundTripWithoutFlatteningOrderOrValues()
    {
        using var files = new Files();
        var query = MessageSearchQuery.Advanced(new SearchGroup(SearchCombination.Any,
            [new SearchGroup(SearchCombination.All, [new SearchCondition(SearchTarget.Field, SearchOperator.Equals, "PID-3.1.2", "SYN-001", true), new SearchCondition(SearchTarget.Segment, SearchOperator.Exists, "PV1")]), new SearchCondition(SearchTarget.MessageText, SearchOperator.DoesNotContain, Value: "SYN-ABSENT")]));
        var saved = await files.Library.CreateAsync("Synthetic tree", query, Token); var loaded = files.Library.Open(saved.Id);
        Assert.Equal(SearchCombination.Any, loaded.Query.Root!.Combination); var child = Assert.IsType<SearchGroup>(loaded.Query.Root.Children[0]);
        Assert.Equal(SearchCombination.All, child.Combination); Assert.Equal("PID-3.1.2", Assert.IsType<SearchCondition>(child.Children[0]).Path);
        Assert.IsType<SearchCondition>(loaded.Query.Root.Children[1]); Assert.Equal(3, loaded.Query.Conditions.Count);
        await files.Library.UpdateAsync(loaded, loaded.Query, Token); Assert.Empty(Directory.GetFiles(files.DirectoryPath, "*.tmp"));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public async Task MalformedTreesAreIsolatedWithoutRewritingDefinitions(int corruption)
    {
        using var files = new Files(); var good = await files.Library.CreateAsync("Synthetic good", Query, Token); var bad = await files.Library.CreateAsync("Synthetic bad", Query, Token);
        var json = JsonNode.Parse(await File.ReadAllTextAsync(files.PathFor(bad.Id), Token))!;
        var root = json["Root"]!; var children = root["Children"]!.AsArray(); var leaf = children[0]!.DeepClone();
        JsonObject Group(JsonArray contents) => new() { ["Combination"] = 0, ["Children"] = contents };
        JsonObject Node(JsonNode group) => new() { ["Condition"] = null, ["Group"] = group };
        switch (corruption)
        {
            case 0: root["Children"] = new JsonArray(); break;
            case 1: children[0]!["Group"] = Group(new JsonArray(leaf)); break;
            case 2: children[0]!["Condition"] = null; break;
            case 3:
                JsonNode deep = Group(new JsonArray(leaf));
                for (var i = 0; i < 4; i++) deep = Group(new JsonArray(Node(deep)));
                json["Root"] = deep; break;
            case 4: root["Children"] = new JsonArray(Enumerable.Range(0, 16).Select(_ => (JsonNode)Node(Group(new JsonArray(leaf.DeepClone())))).ToArray()); break;
            case 5: root["Children"] = new JsonArray(Enumerable.Range(0, 17).Select(_ => leaf.DeepClone()).ToArray()); break;
            case 6: root["Combination"] = 99; break;
            default: children[0]!["Results"] = "SYN-PRIVATE"; break;
        }
        var bytes = System.Text.Encoding.UTF8.GetBytes(json.ToJsonString()); await File.WriteAllBytesAsync(files.PathFor(bad.Id), bytes, Token);
        Assert.Throws<InvalidDataException>(() => files.Library.Open(bad.Id)); Assert.Equal(1, files.Library.List().InvalidEntries);
        Assert.Equal(good.Id, Assert.Single(files.Library.List().Items).Id); Assert.Equal(bytes, await File.ReadAllBytesAsync(files.PathFor(bad.Id), Token));
    }
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class Files : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-search-library-test-" + Guid.NewGuid().ToString("N"));
        public string DirectoryPath => Path.Combine(Root, "Searches");
        public SearchLibrary Library => new(DirectoryPath);
        public Files() => Directory.CreateDirectory(Root);
        public string PathFor(Guid id) => Path.Combine(DirectoryPath, id.ToString("N") + ".json");
        public void Dispose() => Directory.Delete(Root, true);
    }
    private static MessageSearchQuery Query => MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Equals, "MSH-9.1", "ADT"), new(SearchTarget.Segment, SearchOperator.DoesNotExist, "AL1")]);
    [Fact]
    public async Task SaveRestartLoadRenameUpdateAndDeleteKeepDefinitionsIndependent()
    {
        using var files = new Files(); var library = files.Library;
        Assert.Empty(library.List().Items); Assert.False(Directory.Exists(files.DirectoryPath));
        var first = await library.CreateAsync("Synthetic ADT", Query, Token);
        var second = await library.CreateAsync("Synthetic simple", new(MessageSearchMode.Field, "SYN-001", true, "PID-3.1", MessageSearchComparison.Equals), Token);
        var reopened = files.Library.Open(first.Id); Assert.Equal(first.Revision, reopened.Revision); Assert.Equal(2, reopened.Query.Conditions.Count);
        Assert.Equal(SearchBuilderMode.Advanced, reopened.Query.BuilderMode); Assert.Equal(SearchCombination.All, reopened.Query.Combination);
        var simple = files.Library.Open(second.Id); Assert.Equal("SYN-001", simple.Query.Value); Assert.True(simple.Query.CaseSensitive); Assert.Equal(MessageSearchComparison.Equals, simple.Query.Comparison);
        var renamed = await library.RenameAsync(reopened, "Synthetic renamed", Token); Assert.NotEqual(first.Revision, renamed.Revision);
        Assert.Equal("Synthetic renamed", files.Library.Open(first.Id).Name);
        var updated = await library.UpdateAsync(renamed, Query with { Combination = SearchCombination.Any }, Token);
        Assert.Equal(SearchCombination.Any, files.Library.Open(first.Id).Query.Combination);
        await library.DeleteAsync(updated, Token); Assert.Single(library.List().Items); Assert.Equal(second.Id, library.List().Items[0].Id);
        Assert.Equal(second.Revision, library.Open(second.Id).Revision); Assert.Empty(Directory.GetFiles(files.DirectoryPath, "*.tmp"));
    }
    [Fact]
    public async Task StoredSchemaWhitelistsOnlyEffectiveQueryInputsAndNeverCreatesHistory()
    {
        using var files = new Files(); var library = files.Library;
        var query = Query with { Value = "SYN-HIDDEN-INACTIVE", FieldPath = @"C:\SYN-source.hl7" };
        var entry = await library.CreateAsync("Synthetic definition", query, Token);
        var json = await File.ReadAllTextAsync(files.PathFor(entry.Id), Token);
        Assert.DoesNotContain("HIDDEN", json); Assert.DoesNotContain("source.hl7", json); Assert.DoesNotContain("Results", json); Assert.DoesNotContain("Document", json);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(new[] { "Id", "Mode", "Name", "QueryVersion", "Root", "Version" }, parsed.RootElement.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(2, parsed.RootElement.GetProperty("QueryVersion").GetInt32()); Assert.Equal(2, parsed.RootElement.GetProperty("Root").GetProperty("Children").GetArrayLength());
        Assert.Single(Directory.GetFiles(files.DirectoryPath, "*.json")); Assert.DoesNotContain("ADT", entry.ToString());
        var existence = await library.CreateAsync("Synthetic presence", MessageSearchQuery.Advanced([new(SearchTarget.Segment, SearchOperator.Exists, "PID", "SYN-IGNORED", true)]), Token);
        var stored = library.Open(existence.Id).Query.Conditions[0]; Assert.Equal("", stored.Value); Assert.False(stored.CaseSensitive);
        var simpleQuery = new MessageSearchQuery(MessageSearchMode.Text, "SYN-TEXT") { Conditions = [new(SearchTarget.Field, SearchOperator.Equals, "PID-3", "SYN-UNUSED")] };
        var simple = await library.CreateAsync("Synthetic text", simpleQuery, Token);
        Assert.DoesNotContain("UNUSED", await File.ReadAllTextAsync(files.PathFor(simple.Id), Token));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    [InlineData(8)] [InlineData(9)] [InlineData(10)]
    public async Task CorruptUnsupportedAndUnknownSchemasAreIsolatedWithPrivacySafeErrors(int corruption)
    {
        using var files = new Files(); var library = files.Library;
        var good = await library.CreateAsync("Synthetic good", Query, Token); var bad = await library.CreateAsync("Synthetic bad", Query, Token);
        var node = JsonNode.Parse(await File.ReadAllTextAsync(files.PathFor(bad.Id), Token))!.AsObject();
        switch (corruption)
        {
            case 0: node["Version"] = 99; break;
            case 1: node["QueryVersion"] = 99; break;
            case 2: node["Root"]!["Children"]![0]!["Condition"]!["Operator"] = 99; break;
            case 3: node["Root"]!["Children"]![0]!["Condition"]!["Path"] = "PID-0"; break;
            case 4: node["Results"] = "SYN-PRIVATE-RESULT"; break;
            case 5: node["Root"]!["Children"]![0]!["Condition"]!.AsObject().Remove("Operator"); break;
            case 6: node["Root"]!["Children"] = new JsonArray(); break;
            case 7: node["Id"] = Guid.NewGuid(); break;
            case 8: node["Mode"] = 99; break;
            case 9: node["Root"]!["Children"]![0]!["Condition"]!["Target"] = 99; break;
            default: node["Root"]!["Children"]![0]!["Condition"]!["CaseSensitive"] = "invalid"; break;
        }
        await File.WriteAllTextAsync(files.PathFor(bad.Id), node.ToJsonString(), Token);
        var error = Assert.Throws<InvalidDataException>(() => library.Open(bad.Id));
        Assert.Null(error.InnerException); Assert.DoesNotContain("SYN-PRIVATE", error.ToString()); Assert.DoesNotContain(files.Root, error.ToString());
        var listing = files.Library.List(); Assert.Equal(1, listing.InvalidEntries); Assert.Equal(good.Id, Assert.Single(listing.Items).Id);
        Assert.Equal(good.Revision, library.Open(good.Id).Revision); Assert.True(File.Exists(files.PathFor(bad.Id)));
        await File.WriteAllTextAsync(files.PathFor(bad.Id), "{SYN-PRIVATE-BROKEN", Token);
        Assert.Equal(1, library.List().InvalidEntries); Assert.DoesNotContain("PRIVATE", Assert.Throws<InvalidDataException>(() => library.Open(bad.Id)).ToString());
    }
    [Theory]
    [InlineData("")] [InlineData(" ")] [InlineData("../outside")] [InlineData("..\\outside")] [InlineData("C:\\outside")]
    [InlineData("bad\nname")] [InlineData("name*")] [InlineData(" leading")] [InlineData("..")] [InlineData(".")]
    public async Task UnsafeNamesNeverBecomePathsOrCreateStorage(string name)
    {
        using var files = new Files();
        await Assert.ThrowsAsync<ArgumentException>(() => files.Library.CreateAsync(name, Query, Token));
        Assert.False(Directory.Exists(files.DirectoryPath)); Assert.Empty(Directory.GetFiles(files.Root));
    }
    [Fact]
    public async Task ExplicitActionsCreateStorageAndDuplicateNamesNeverOverwriteRecords()
    {
        using var files = new Files(); var library = files.Library;
        Assert.Empty(library.List().Items); Assert.False(Directory.Exists(files.DirectoryPath));
        var first = await library.CreateAsync("Synthetic", Query, Token);
        var before = await File.ReadAllBytesAsync(files.PathFor(first.Id), Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.CreateAsync("SYNTHETIC", Query, Token));
        var second = await library.CreateAsync("Other synthetic", Query, Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.RenameAsync(second, "Synthetic", Token));
        Assert.Equal(first.Revision, library.Open(first.Id).Revision);
        Assert.Equal(before, await File.ReadAllBytesAsync(files.PathFor(first.Id), Token)); Assert.Equal(2, library.List().Items.Count);
    }
    [Fact]
    public async Task StaleUpdatesRenamesDeletesAndCanceledWritesPreserveExistingDefinitions()
    {
        using var files = new Files(); var library = files.Library; var first = await library.CreateAsync("Synthetic", Query, Token);
        var current = await files.Library.UpdateAsync(first, Query with { Combination = SearchCombination.Any }, Token);
        await Assert.ThrowsAsync<IOException>(() => library.UpdateAsync(first, Query, Token));
        await Assert.ThrowsAsync<IOException>(() => library.RenameAsync(first, "Synthetic rename", Token));
        await Assert.ThrowsAsync<IOException>(() => library.DeleteAsync(first, Token));
        Assert.Equal(current.Revision, library.Open(first.Id).Revision);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => library.UpdateAsync(current, Query, canceled.Token));
        Assert.Equal(current.Revision, library.Open(first.Id).Revision); Assert.Empty(Directory.GetFiles(files.DirectoryPath, "*.tmp"));
        await library.DeleteAsync(current, Token);
        await Assert.ThrowsAsync<IOException>(() => library.UpdateAsync(current, Query, Token)); Assert.Empty(library.List().Items);
    }
    [Fact]
    public async Task FailedReplacementAndLockContentionKeepOriginalBytesAndCleanOwnedStaging()
    {
        using var files = new Files(); var library = files.Library; var entry = await library.CreateAsync("Synthetic", Query, Token);
        var before = await File.ReadAllBytesAsync(files.PathFor(entry.Id), Token);
        using (var lease = new FileStream(Path.Combine(files.DirectoryPath, "library.lock"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAsync<IOException>(() => library.UpdateAsync(entry, Query with { Combination = SearchCombination.Any }, Token));
        if (OperatingSystem.IsWindows())
        {
            using var held = new FileStream(files.PathFor(entry.Id), FileMode.Open, FileAccess.Read, FileShare.Read);
            await Assert.ThrowsAsync<IOException>(() => library.UpdateAsync(entry, Query with { Combination = SearchCombination.Any }, Token));
        }
        Assert.Equal(before, await File.ReadAllBytesAsync(files.PathFor(entry.Id), Token)); Assert.Empty(Directory.GetFiles(files.DirectoryPath, "*.tmp"));
    }
    [Fact]
    public async Task RenameCannotCreateARecordOrAdoptUnconsentedDraftValues()
    {
        using var files = new Files(); var library = files.Library;
        var unsaved = new SavedSearch(Guid.NewGuid(), "Synthetic", Query, "");
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.RenameAsync(unsaved, "Synthetic renamed", Token));
        Assert.False(Directory.Exists(files.DirectoryPath));
        var saved = await library.CreateAsync("Synthetic", Query, Token);
        var draft = saved with { Query = new(MessageSearchMode.Text, "SYN-UNCONSENTED") };
        var renamed = await library.RenameAsync(draft, "Synthetic renamed", Token);
        Assert.Equal(SearchBuilderMode.Advanced, renamed.Query.BuilderMode);
        Assert.DoesNotContain("UNCONSENTED", await File.ReadAllTextAsync(files.PathFor(saved.Id), Token));
    }
    [Fact]
    public async Task ResourceLimitsUnknownFilenamesAndInvalidUnicodeRemainSafe()
    {
        using var files = new Files(); var library = files.Library;
        var invalidUnicode = new MessageSearchQuery(MessageSearchMode.Text, new string('\ud800', 1));
        await Assert.ThrowsAsync<ArgumentException>(() => library.CreateAsync("Synthetic", invalidUnicode, Token));
        Assert.False(Directory.Exists(files.DirectoryPath));
        Directory.CreateDirectory(files.DirectoryPath);
        await File.WriteAllTextAsync(Path.Combine(files.DirectoryPath, "unrelated.json"), "{}", Token);
        Assert.Equal(1, library.List().InvalidEntries);
        var oversized = Guid.NewGuid(); await File.WriteAllTextAsync(files.PathFor(oversized), new string('x', SearchLibrary.MaximumEntryBytes + 1), Token);
        Assert.Equal(2, library.List().InvalidEntries);
        for (var i = 2; i < SearchLibrary.MaximumEntries; i++) await File.WriteAllTextAsync(files.PathFor(Guid.NewGuid()), "{}", Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => library.CreateAsync("Synthetic", Query, Token));
        Assert.Equal(SearchLibrary.MaximumEntries, Directory.GetFiles(files.DirectoryPath, "*.json").Length);
    }
}
