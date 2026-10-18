using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class StructuralCompatibilityTests
{
    [Fact]
    public async Task SearchIncludesInsertedCustomDelimiterAndShiftedEditedOriginalWithStableFilterIdentity()
    {
        var doc = await Open(); var original = doc.GetMessageId(2);
        var inserted = doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
        var search = new MessageSearch();
        var insertedResults = await search.SearchAsync(doc, new(MessageSearchMode.Field, "C", FieldPath: "PID-1", Comparison: MessageSearchComparison.Equals), token: Token);
        var insertedMatch = Assert.Single(insertedResults.Matches);
        Assert.Equal(inserted, insertedMatch.MessageId); Assert.Equal(1, insertedMatch.MessageNumber); Assert.Null(insertedMatch.OriginalOrdinal);
        await doc.NavigateAsync(original, Token); doc.IsEditMode = true; doc.ReplaceWorkingText(B + "NTE|SYN-MATCH\r");
        using var session = new MessageSearchSession(documents);
        await session.StartAsync(new(MessageSearchMode.Text, "SYN-MATCH"), Token);
        var match = Assert.Single(session.Results!.Matches);
        Assert.Equal(original, match.MessageId); Assert.Equal(3, match.MessageNumber); Assert.Equal(2, match.OriginalOrdinal);
        session.FilterNavigator = true;
        Assert.Equal(new[] { original }, doc.NavigationMatchIds); Assert.Equal(new[] { 3 }, doc.NavigationMatches);
        Assert.True(doc.IsNavigationMatch(original)); Assert.False(doc.IsNavigationMatch(inserted));
        await doc.EditRefresh; await doc.NavigateAsync(inserted, Token);
        Assert.True(session.Results.IsCurrent(doc)); await session.NavigateAsync(match, Token);
        Assert.Equal(original, doc.ActiveMessageId); Assert.Equal(3, doc.MessageNumber);
        var results = session.Results; var revision = doc.SearchRevision;
        await doc.SaveAsync(Path.Combine(root, "search-save.hl7"), Token);
        Assert.Same(results, session.Results); Assert.True(results.IsCurrent(doc)); Assert.Equal(revision, doc.SearchRevision);
        Assert.Equal(new[] { original }, doc.NavigationMatchIds);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task StructuralUndoRedoInvalidatesResultsAndNeverNavigatesUsingCapturedPosition(bool redo)
    {
        var doc = await Open(); var original = doc.GetMessageId(1);
        var inserted = doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
        if (redo) { doc.Undo(); await doc.EditRefresh; }
        using var session = new MessageSearchSession(documents);
        await session.StartAsync(new(MessageSearchMode.Text, "SYN-A"), Token);
        var old = session.Results!; var match = Assert.Single(old.Matches);
        Assert.Equal(original, match.MessageId); session.FilterNavigator = true;
        if (redo) doc.Redo(); else doc.Undo();
        await doc.EditRefresh;
        Assert.Null(session.Results); Assert.Null(doc.NavigationMatchIds); Assert.Null(doc.NavigationMatches);
        Assert.False(old.IsCurrent(doc)); var active = doc.ActiveMessageId;
        await session.NavigateAsync(match, Token); Assert.Equal(active, doc.ActiveMessageId);
        if (redo) Assert.Equal(inserted, active);
    }

    [Fact]
    public async Task InsertionCancelsCapturedSearchAndDisposalDrainsItsLease()
    {
        var doc = await Open(string.Concat(Enumerable.Repeat(A, 500)));
        var inserted = false;
        var progress = new InlineProgress<MessageSearchProgress>(_ =>
        {
            if (inserted) return; inserted = true; doc.InsertMessage(MessageInsertionPosition.Beginning, C);
        });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MessageSearch().SearchAsync(doc,
            new(MessageSearchMode.Text, "SYN"), progress, Token));
        Assert.True(inserted); var directory = doc.Snapshot!.DirectoryPath;
        documents.Close(doc.Id, discardChanges: true); await doc.Disposal; Assert.False(Directory.Exists(directory));
    }

    [Fact]
    public async Task NestedAdvancedExistenceQueryAndSavedDefinitionSearchInsertedDraft()
    {
        var doc = await Open(); var id = doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        var query = MessageSearchQuery.Advanced(new SearchGroup(SearchCombination.All,
        [new SearchCondition(SearchTarget.Field, SearchOperator.Exists, "PID-1"),
         new SearchGroup(SearchCombination.Any,
         [new SearchCondition(SearchTarget.Field, SearchOperator.DoesNotExist, "PID-2"),
          new SearchCondition(SearchTarget.Field, SearchOperator.Equals, "PID-1", "absent")])]));
        var library = new SearchLibrary(Path.Combine(root, "searches"));
        var saved = await library.CreateAsync("Synthetic compatibility", query, Token);
        var loaded = library.Open(saved.Id);
        var results = await new MessageSearch().SearchAsync(doc, loaded.Query, token: Token);
        Assert.Contains(results.Matches, match => match.MessageId == id && match.OriginalOrdinal is null);
    }
}
