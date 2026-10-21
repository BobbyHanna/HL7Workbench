using System.Text.Json;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class ExtractionLibraryTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-extraction-library-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private ExtractionLibrary Library => new(root);
    private static Task<UnsavedChangesDecision> Cancel() => Task.FromResult(UnsavedChangesDecision.Cancel);
    private static Task<UnsavedChangesDecision> Discard() => Task.FromResult(UnsavedChangesDecision.Discard);
    [Fact]
    public async Task BuiltInIsImmutableAndIndependentCopiesRenameEditReorderSaveReopenAndDelete()
    {
        var session = new ExtractionConfigurationSession(Library); var original = ExtractionLibrary.InterfaceOverview;
        session.Name = "Cannot change"; session.Columns[0].Path = "PID-1"; session.Mode = ExtractionMode.ExpandMatches;
        Assert.Equal(original.Name, session.Name); Assert.Equal("MSH-9", session.Columns[0].Path); Assert.False(await session.SaveAsync(Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Library.SaveAsync(original with { BuiltIn = false }, Token));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Library.DeleteAsync(original with { BuiltIn = false }, Token));
        Assert.True(session.Duplicate()); var id = session.Id; Assert.NotEqual(original.Id, id); Assert.True(session.IsDirty);
        session.Name = "My overview"; session.Mode = ExtractionMode.ExpandMatches; session.Columns[0].Label = "Type";
        session.Columns[0].Path = " MSH[01]-9[01] "; session.MoveColumn(0, 1); session.AddColumn();
        session.Columns[^1].Label = "Subcomponent"; session.Columns[^1].Path = "PID[2]-3[1].4.1";
        Assert.True(await session.SaveAsync(Token)); Assert.False(session.IsDirty);
        var reopened = new ExtractionConfigurationSession(new(root)); Assert.True(await reopened.OpenAsync(id, Cancel));
        Assert.Equal("My overview", reopened.Name); Assert.Equal(ExtractionMode.ExpandMatches, reopened.Mode); Assert.Equal("MSH[1]-9[1]", reopened.Columns[1].Path);
        Assert.Equal("PID[2]-3[1].4.1", reopened.Columns[^1].Path);
        Assert.Equal(JsonSerializer.Serialize(original), JsonSerializer.Serialize(ExtractionLibrary.InterfaceOverview));
        var copyBytes = File.ReadAllBytes(Path.Combine(root, id.ToString("N") + ".json"));
        _ = new ExtractionLibrary(root).List(); _ = new ExtractionLibrary(root).Open(ExtractionLibrary.OverviewId);
        Assert.Equal(copyBytes, File.ReadAllBytes(Path.Combine(root, id.ToString("N") + ".json")));
        Assert.False(await reopened.DeleteAsync(Cancel, _ => Task.FromResult(false)));
        Assert.True(await reopened.DeleteAsync(Cancel, _ => Task.FromResult(true))); Assert.True(reopened.IsBuiltIn); Assert.Single(Library.List().Items);
    }
    [Fact]
    public async Task DirtyAndNewerDraftDecisionsNeverDiscardUnreviewedChanges()
    {
        var session = new ExtractionConfigurationSession(Library); Assert.True(await session.NewAsync(Cancel));
        session.Name = "Unsaved"; session.Columns[0].Path = "PID-5.1";
        Assert.False(await session.OpenAsync(ExtractionLibrary.OverviewId, Cancel)); Assert.Equal("Unsaved", session.Name);
        var consent = new TaskCompletionSource<UnsavedChangesDecision>(); var opening = session.OpenAsync(ExtractionLibrary.OverviewId, () => consent.Task);
        session.Columns[0].Path = "PID-3.1"; consent.SetResult(UnsavedChangesDecision.Discard);
        Assert.False(await opening); Assert.Equal("PID-3.1", session.Columns[0].Path);
        session.Columns[0].Path = "invalid"; Assert.False(await session.SaveAsync(Token)); Assert.True(session.IsDirty); Assert.Equal("invalid", session.Columns[0].Path);
        Assert.True(await session.OpenAsync(ExtractionLibrary.OverviewId, Discard)); Assert.True(session.IsBuiltIn);
    }
    [Theory]
    [InlineData("unknown-version")] [InlineData("unknown-property")] [InlineData("bad-path")] [InlineData("missing-columns")]
    public async Task CorruptRecordsAreSkippedWithoutReplacingTheDraftOrOtherFiles(string corruption)
    {
        Directory.CreateDirectory(root); var id = Guid.NewGuid(); var file = Path.Combine(root, id.ToString("N") + ".json");
        var config = ExtractionLibrary.InterfaceOverview.Configuration;
        var json = JsonSerializer.Serialize(new { Version = 1, Id = id, Name = "Corrupt synthetic", Configuration = config });
        json = corruption switch
        {
            "unknown-version" => json.Replace("\"Version\":1", "\"Version\":999", StringComparison.Ordinal),
            "unknown-property" => json.Insert(1, "\"PatientValues\":[],"),
            "bad-path" => json.Replace("MSH-9", "BAD-PATH", StringComparison.Ordinal),
            _ => json.Replace("\"Columns\":", "\"UnknownColumns\":", StringComparison.Ordinal)
        };
        await File.WriteAllTextAsync(file, json, Token); Assert.Throws<InvalidDataException>(() => Library.Open(id));
        Assert.Equal(1, Library.List().InvalidEntries); var session = new ExtractionConfigurationSession(Library);
        Assert.False(await session.OpenAsync(id, Cancel)); Assert.True(session.IsBuiltIn); Assert.Equal(json, await File.ReadAllTextAsync(file, Token));
    }
    [Fact]
    public async Task OptimisticRevisionChecksPreventOverwritingOrDeletingAnExternalChange()
    {
        var original = await Library.SaveAsync(new(Guid.NewGuid(), "Synthetic", ExtractionLibrary.InterfaceOverview.Configuration, ""), Token);
        var changed = await Library.SaveAsync(original with { Name = "Externally changed" }, Token);
        await Assert.ThrowsAsync<IOException>(() => Library.SaveAsync(original, Token));
        await Assert.ThrowsAsync<IOException>(() => Library.DeleteAsync(original, Token)); Assert.Equal(changed.Name, Library.Open(original.Id).Name);
        Assert.Empty(Directory.GetFiles(root, ".hl7-save-*.tmp"));
    }
    [Fact]
    public async Task SavedSchemaContainsPreferencesButNoSourceResultOrLiteralFilterValues()
    {
        var session = new ExtractionConfigurationSession(Library); session.Duplicate(); session.Name = "Synthetic preferences";
        session.FilterColumn = 2; session.FilterOperator = ExtractionFilterOperator.Equals; session.SortColumn = 3; session.Descending = true; session.NumericSort = true;
        Assert.True(await session.SaveAsync(Token));
        var json = await File.ReadAllTextAsync(Path.Combine(root, session.Id.ToString("N") + ".json"), Token);
        Assert.DoesNotContain("Literal", json); Assert.DoesNotContain("Source", json); Assert.DoesNotContain("Result", json); Assert.DoesNotContain("MessageId", json);
        var saved = Library.Open(session.Id); Assert.Equal(2, saved.Configuration.FilterColumn); Assert.True(saved.Configuration.Descending);
        Assert.Throws<ArgumentException>(() => new ExtractionConfiguration([new("Same", "PID-3"), new("Same", "PID-5")]).Snapshot());
        Assert.Throws<ArgumentException>(() => new ExtractionConfiguration([new("Value", "PID-3"), new("Value State", "PID-5")]).Snapshot());
        Assert.Throws<ArgumentException>(() => new ExtractionConfiguration(Enumerable.Range(0, 33).Select(n => new ExtractionColumn(n.ToString(System.Globalization.CultureInfo.InvariantCulture), "PID-3")).ToArray()).Snapshot());
    }
    public void Dispose() { if (Directory.Exists(root)) Directory.Delete(root, true); }
}
