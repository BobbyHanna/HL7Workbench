using HL7Workbench.Core;
using HL7Workbench.Scripting;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class SyntheticGenerationTests
{
    [Fact]
    public async Task GeneratedMessagesSupportInspectorSearchAdvancedFilterAndComparisonWithoutOriginalProvenance()
    {
        var doc = await Open(); var result = await Generate(doc, 3); await doc.EditRefresh;
        Assert.Equal("SYNTHETIC0001", doc.Message.Get("PID-5.1").Raw); Assert.NotNull(doc.InspectionSnapshot);
        var search = new MessageSearch();
        var results = await search.SearchAsync(doc, new(MessageSearchMode.Field, result.Options.ControlId(2), FieldPath: "MSH-10", Comparison: MessageSearchComparison.Equals), token: Token);
        var match = Assert.Single(results.Matches); Assert.Equal(result.MessageIds[1], match.MessageId); Assert.Null(match.OriginalOrdinal);
        doc.ApplySearchFilter(results); Assert.Equal(new[] { result.MessageIds[1] }, doc.NavigationMatchIds);
        Assert.Equal(new[] { 4 }, doc.NavigationMatches); Assert.False(doc.IsNavigationMatch(result.MessageIds[0]));
        await doc.NavigateAsync(match.MessageId, Token); Assert.Equal(4, doc.MessageNumber);
        var query = MessageSearchQuery.Advanced(new SearchGroup(SearchCombination.All,
            [new SearchCondition(SearchTarget.Field, SearchOperator.Equals, "MSH-12", "2.5.1"), new SearchCondition(SearchTarget.Field, SearchOperator.Contains, "PID-5.1", "SYNTHETIC")]));
        var advanced = await search.SearchAsync(doc, query, token: Token); Assert.Equal(result.MessageIds, advanced.Matches.Select(m => m.MessageId));
        using var comparison = await MessageComparison.CreateAsync(new(doc, result.MessageIds[0]), new(doc, result.MessageIds[1]), token: Token);
        Assert.NotEmpty(comparison.Result.Differences);
        await Assert.ThrowsAsync<ComparisonInputException>(() => MessageComparison.CreateAsync(new(doc, result.MessageIds[0], true), new(doc, 1), token: Token));
        doc.Undo(); await doc.EditRefresh; Assert.False(comparison.IsCurrent); Assert.False(results.IsCurrent(doc)); Assert.Null(doc.NavigationMatches);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(result.MessageIds[0], doc.ActiveMessageId);
    }
    [Theory]
    [InlineData(ScriptScope.CurrentMessage)] [InlineData(ScriptScope.WholeDocument)]
    public async Task ScriptsAndCsvReportsRetainGeneratedIdentityAndStaleCapturedSourceAttribution(ScriptScope scope)
    {
        var doc = (await Generate(count: 3)).Document; await doc.EditRefresh;
        var ids = doc.Messages.Select(m => m.Id).ToArray(); var revision = doc.SearchRevision; var changed = false;
        const string code = "var report = Batch.Report(\"synthetic\", \"Control ID\"); void Main() { report.Row(Message.Read(\"MSH-10\").Raw); }";
        var result = await new ScriptExecution(documents, new() { StorageRoot = Path.Combine(root, "jobs") }).RunAsync(doc, new(code), scope,
            new InlineProgress<ScriptProgress>(p =>
            {
                if (changed || p.Phase != "Compiling") return; changed = true;
                doc.ReplaceWorkingText(doc.EditableText + "NTE|1||SYNTHETIC LATER EDIT\r");
            }), Token);
        using var reports = result.Reports!;
        Assert.True(result.SourceChangedDuringRun); Assert.Equal(revision, result.Input!.Revision); Assert.False(result.Input.IsCurrent(doc));
        var expected = scope == ScriptScope.CurrentMessage ? ids.Take(1) : ids;
        Assert.Equal(expected, result.MessageSources.Select(m => m.InputMessageId)); Assert.All(result.Input.Messages, m => Assert.Null(m.OriginalOrdinal));
        Assert.Equal(scope == ScriptScope.CurrentMessage ? 1 : 3, result.Document.MessageCount);
        Assert.DoesNotContain("LATER EDIT", result.Document.EditableText!); Assert.Contains("LATER EDIT", doc.EditableText!);
        Assert.Single(reports.Items); var path = Path.Combine(root, scope + ".csv"); await reports.ExportAsync("synthetic", path, Token);
        var csv = await File.ReadAllTextAsync(path, Token); Assert.Contains("Control ID", csv); Assert.Contains("SYN-0000002A-A01-0001", csv);
        if (scope == ScriptScope.WholeDocument) Assert.Contains("SYN-0000002A-A01-0003", csv);
        await doc.EditRefresh;
    }
}
