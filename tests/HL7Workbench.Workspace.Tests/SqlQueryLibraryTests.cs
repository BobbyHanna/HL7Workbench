using System.Text.Json;
using System.Text.Json.Nodes;
using HL7Workbench.Querying;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class SqlQueryLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "sql-library-synthetic-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private SqlQueryLibrary Library => new(root);
    private string FileFor(Guid id) => Path.Combine(root, id.ToString("N") + ".json");
    [Fact]
    public async Task ExplicitDefinitionsRoundTripAndRenamePreservesStoredSqlRatherThanAnUnsavedDraft()
    {
        var saved = await Library.CreateAsync("Synthetic admissions", "SELECT 'SYN-EXPLICIT-LITERAL'", Token);
        var opened = Library.Open(saved.Id); Assert.Equal(saved, opened);
        var renamed = await Library.RenameAsync(opened with { Sql = "SELECT 'SYN-UNSAVED'" }, "Synthetic renamed", Token);
        Assert.Equal(saved.Sql, Library.Open(saved.Id).Sql);
        var updated = await Library.UpdateAsync(renamed, renamed.Name, "SELECT 2", Token);
        Assert.Equal("SELECT 2", new SqlQueryLibrary(root).Open(saved.Id).Sql);
        using var json = JsonDocument.Parse(await File.ReadAllBytesAsync(FileFor(saved.Id), Token));
        Assert.Equal(new[] { "Version", "Id", "Name", "Sql" }, json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.DoesNotContain("SYN-EXPLICIT-LITERAL", saved.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain("SYN-EXPLICIT-LITERAL", new SqlQueryLibraryItem(saved.Id, saved.Name).ToString(), StringComparison.Ordinal);
        await Library.DeleteAsync(updated, Token); Assert.Empty(Library.List().Items);
    }
    [Fact]
    public async Task DuplicateNamesExternalChangesAndStaleDeleteNeverOverwriteDefinitions()
    {
        var saved = await Library.CreateAsync("Synthetic", "SELECT 1", Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Library.CreateAsync("synthetic", "SELECT 2", Token));
        var changed = await Library.UpdateAsync(saved, saved.Name, "SELECT 3", Token);
        await Assert.ThrowsAsync<IOException>(() => Library.UpdateAsync(saved, saved.Name, "SELECT 4", Token));
        await Assert.ThrowsAsync<IOException>(() => Library.RenameAsync(saved, "Stale", Token));
        await Assert.ThrowsAsync<IOException>(() => Library.DeleteAsync(saved, Token));
        Assert.Equal(changed, Library.Open(saved.Id)); Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)]
    public async Task MalformedRecordsAreIsolatedAndNeverRewrittenByListing(int kind)
    {
        var good = await Library.CreateAsync("Good synthetic", "SELECT 1", Token);
        var bad = await Library.CreateAsync("Bad synthetic", "SELECT 2", Token);
        var node = JsonNode.Parse(await File.ReadAllTextAsync(FileFor(bad.Id), Token))!;
        switch (kind)
        {
            case 0: node["Version"] = 9; break;
            case 1: node["Sql"] = null; break;
            case 2: node["Extra"] = "unsupported"; break;
            case 3: node["Name"] = "../unsafe"; break;
            case 4: node["Sql"] = new string('X', QueryLimits.MaximumSqlCharacters + 1); break;
            case 5: node["Id"] = Guid.NewGuid(); break;
        }
        await File.WriteAllTextAsync(FileFor(bad.Id), node.ToJsonString(), Token); var before = await File.ReadAllBytesAsync(FileFor(bad.Id), Token);
        Assert.Throws<InvalidDataException>(() => Library.Open(bad.Id));
        var listing = Library.List(); Assert.Equal(good.Id, Assert.Single(listing.Items).Id); Assert.Equal(1, listing.InvalidEntries);
        Assert.Equal(before, await File.ReadAllBytesAsync(FileFor(bad.Id), Token));
    }
    [Fact]
    public async Task TextEntryAndCancellationLimitsAreIndependentOfSqlValidity()
    {
        foreach (var sql in new[] { "", "SELECT '\0'", "SELECT '\ud800'", new string('X', QueryLimits.MaximumSqlCharacters + 1) })
            await Assert.ThrowsAsync<ArgumentException>(() => Library.CreateAsync("Synthetic", sql, Token));
        var invalidSql = await Library.CreateAsync("Work in progress", "SELECT unfinished syntax", Token);
        Assert.Equal(invalidSql.Sql, Library.Open(invalidSql.Id).Sql); // Saving is not execution.
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Library.CreateAsync("Canceled", "SELECT 1", cancel.Token));
        Assert.Single(Library.List().Items);
        await File.WriteAllBytesAsync(FileFor(invalidSql.Id), new byte[SqlQueryLibrary.MaximumEntryBytes + 1], Token);
        Assert.Throws<InvalidDataException>(() => Library.Open(invalidSql.Id));
    }
    [Fact]
    public async Task EntryQuotaCountsCorruptFilesAndCannotGrowWithoutBound()
    {
        Directory.CreateDirectory(root);
        for (var i = 0; i < SqlQueryLibrary.MaximumEntries; i++) await File.WriteAllTextAsync(FileFor(Guid.NewGuid()), "{}", Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Library.CreateAsync("Synthetic overflow", "SELECT 1", Token));
        Assert.Equal(SqlQueryLibrary.MaximumEntries, Library.List().InvalidEntries);
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
