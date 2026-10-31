using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class AdvancedSearchTests
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261016000000||ADT^A01|SYN-1|T|2.5\r";
    private const string Body = "PID|1||001^MR~002^ALT||Example^Ada\rPID|2||003^MR||Other^Bea\rPV1||I\rOBX|1|TX|||zzzz\rZEX|\rBAD opaque content\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class Documents : IAsyncDisposable
    {
        public DocumentManager Manager { get; } = new();
        private readonly List<MessageDocument> owned = [];
        public async Task<MessageDocument> Open(string text = Header + Body)
        {
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
            var document = await MessageFileReader.OpenFileAsync(Manager, "Synthetic", stream, Token); owned.Add(document); return document;
        }
        public async ValueTask DisposeAsync() { Manager.Dispose(); await Task.WhenAll(owned.Select(d => d.Disposal)); }
    }
    private sealed class Progress(Action<MessageSearchProgress> report) : IProgress<MessageSearchProgress>
    { public void Report(MessageSearchProgress progress) => report(progress); }
    private static SearchGroup All(params SearchExpression[] children) => new(SearchCombination.All, children);
    private static SearchGroup Any(params SearchExpression[] children) => new(SearchCombination.Any, children);
    private static SearchCondition Field(string path, string value) => new(SearchTarget.Field, SearchOperator.Equals, path, value);
    private static SearchCondition Segment(string path, bool exists = true) => new(SearchTarget.Segment, exists ? SearchOperator.Exists : SearchOperator.DoesNotExist, path);
    [Theory]
    [InlineData(0, true)] [InlineData(1, true)] [InlineData(2, false)] [InlineData(3, true)] [InlineData(4, false)]
    public async Task NestedGroupsPreserveBooleanMeaningAndOneRowPerMessage(int example, bool expected)
    {
        await using var documents = new Documents(); var document = await documents.Open();
        var root = example switch
        {
            0 => Any(Segment("PV1"), All(Field("MSH-9.1", "ADT"), Field("PID-3.1", "001"))),
            1 => All(Any(Field("PID-3.1", "001"), Field("PID-3.1", "002")), Segment("OBX")),
            2 => All(Field("MSH-9.1", "ORU"), Any(Field("OBX-2", "NM"), Field("OBX-2", "ST"))),
            3 => All(Any(All(Any(Field("PID-3.1", "001"), Segment("AL1")), Segment("PID")), Segment("ZZZ"))),
            _ => Any(All(Segment("PID"), Segment("AL1")), All(Field("PID-3.1", "999"), Segment("PV1")))
        };
        var query = MessageSearchQuery.Advanced(root); Assert.Null(query.ValidationError());
        var results = await new MessageSearch().SearchAsync(document, query, token: Token);
        Assert.Equal(expected ? 1 : 0, results.Matches.Count);
        if (expected) { Assert.InRange(results.Matches[0].MatchCount, 1, 16); Assert.DoesNotContain("001", results.Matches[0].ConditionSummary); }
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)]
    public void InvalidTreesNeverBecomeMatchAllAndSnapshotFreezesChildren(int failure)
    {
        var valid = Segment("PID"); SearchGroup root = failure switch
        {
            0 => All(), 1 => All(Any()),
            2 => All(All(All(All(All(valid))))),
            3 => All(Enumerable.Repeat<SearchExpression>(valid, 17).ToArray()),
            4 => All(Enumerable.Range(0, 16).Select(_ => (SearchExpression)All(valid)).ToArray()),
            5 => new((SearchCombination)99, [valid]),
            _ => All(null!)
        };
        Assert.NotNull(MessageSearchQuery.Advanced(root).ValidationError());
        Assert.Throws<ArgumentException>(() => MessageSearchQuery.Advanced(root).Snapshot());
        var mutable = new List<SearchExpression> { valid }; var snapshot = MessageSearchQuery.Advanced(new SearchGroup(SearchCombination.Any, mutable)).Snapshot();
        mutable.Clear(); Assert.Single(snapshot.Root!.Children); Assert.Null(snapshot.ValidationError());
        var cycle = new List<SearchExpression>(); var cyclic = new SearchGroup(SearchCombination.All, cycle); cycle.Add(cyclic);
        Assert.NotNull(MessageSearchQuery.Advanced(cyclic).ValidationError());
    }
    [Fact]
    public async Task NestedShortCircuitCancellationDraftsAndInvalidationReuseExistingLifecycle()
    {
        await using var documents = new Documents(); var document = await documents.Open(Header + "PID|1||" + string.Join('~', Enumerable.Repeat("SYN-HIT", 10001)) + "\r");
        var root = Any(All(new SearchCondition(SearchTarget.MessageText, SearchOperator.Contains, Value: "SYN-HIT")), All(Segment("PID")));
        var results = await new MessageSearch().SearchAsync(document, MessageSearchQuery.Advanced(root), token: Token);
        Assert.Single(results.Matches); Assert.Equal(0, results.SkippedMessages);
        var required = await new MessageSearch().SearchAsync(document, MessageSearchQuery.Advanced(All(All(Segment("PID")))), token: Token);
        Assert.Empty(required.Matches); Assert.Equal(1, required.SkippedMessages);
        var editable = await documents.Open(); using var session = new MessageSearchSession(documents.Manager);
        var query = MessageSearchQuery.Advanced(All(Any(Field("PID-3.1", "001"), Segment("ZZZ")), Segment("PV1")));
        await session.StartAsync(query, Token); session.FilterNavigator = true; var before = session.Results!;
        editable.IsEditMode = true; editable.ReplaceWorkingText(editable.EditableText!.Replace("001", "SYN-DRAFT", StringComparison.Ordinal));
        Assert.Null(session.Results); Assert.Null(editable.NavigationMatches); Assert.False(before.IsCurrent(editable));
        await session.StartAsync(MessageSearchQuery.Advanced(All(Any(Field("PID-3.1", "SYN-DRAFT")))), Token); Assert.Single(session.Page);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MessageSearch().SearchAsync(editable, query, token: canceled.Token));
        var many = await documents.Open(string.Concat(Enumerable.Repeat(Header + Body, 451)));
        var paged = await new MessageSearch().SearchAsync(many, query, token: Token); Assert.Equal(3, paged.PageCount); Assert.Equal(401, paged.Page(2)[0].MessageNumber);
        using var inFlight = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MessageSearch().SearchAsync(many, query, new Progress(_ => inFlight.Cancel()), inFlight.Token));
    }
    [Theory]
    [InlineData("PV1", SearchTarget.Segment, SearchOperator.Exists, true)]
    [InlineData("PV1", SearchTarget.Segment, SearchOperator.DoesNotExist, false)]
    [InlineData("PV1-1", SearchTarget.Field, SearchOperator.Exists, true)]
    [InlineData("PV1-1", SearchTarget.Field, SearchOperator.DoesNotExist, false)]
    [InlineData("PID-3.1.2", SearchTarget.Field, SearchOperator.Exists, true)]
    [InlineData("PID-3.1.3", SearchTarget.Field, SearchOperator.DoesNotExist, true)]
    public async Task SegmentFieldAndSubcomponentPresenceShareCoreStructure(string path, SearchTarget target, SearchOperator operation, bool expected)
    {
        await using var documents = new Documents(); var document = await documents.Open(Header + "PV1|\rPID|1||A&B^MR~C&D\rPID|2||E\r");
        var condition = new SearchCondition(target, operation, path); Assert.Null(condition.ValidationError());
        Assert.Equal(expected ? 1 : 0, (await new MessageSearch().SearchAsync(document, MessageSearchQuery.Advanced(All(condition)), token: Token)).Matches.Count);
        var simple = await new MessageSearch().SearchAsync(document, new(MessageSearchMode.Field, "D", FieldPath: "PID-3.1.2", Comparison: MessageSearchComparison.Equals), token: Token);
        Assert.Single(simple.Matches);
        var custom = await documents.Open("MSH*$%!?*SYN*******SYN|T*2.5\rPV1*\rPID*1**A?B$MR%C?D\r");
        Assert.Single((await new MessageSearch().SearchAsync(custom, new(MessageSearchMode.Field, "D", FieldPath: "PID-3.1.2"), token: Token)).Matches);
    }
    [Theory]
    [InlineData("PID-3.1", SearchOperator.Contains, "00", true)]
    [InlineData("PID-3.1", SearchOperator.Equals, "001", true)]
    [InlineData("PID-3.1", SearchOperator.DoesNotContain, "001", false)]
    [InlineData("PID-3.1", SearchOperator.DoesNotEqual, "001", false)]
    [InlineData("PID-3.1", SearchOperator.DoesNotContain, "999", true)]
    [InlineData("PID-3.1", SearchOperator.DoesNotEqual, "00", true)]
    [InlineData("PID-3", SearchOperator.Equals, "001^MR~002^ALT", false)]
    [InlineData("PID-3", SearchOperator.DoesNotEqual, "001^MR", false)]
    [InlineData("PID-3.1", SearchOperator.Equals, "1", false)]
    [InlineData("PID-99", SearchOperator.Contains, "X", false)]
    [InlineData("PID-99", SearchOperator.Equals, "X", false)]
    [InlineData("PID-99", SearchOperator.DoesNotContain, "X", false)]
    [InlineData("PID-99", SearchOperator.DoesNotEqual, "X", false)]
    [InlineData("PID-99", SearchOperator.Exists, "", false)]
    [InlineData("PID-99", SearchOperator.DoesNotExist, "", true)]
    [InlineData("ZZZ-1", SearchOperator.DoesNotEqual, "X", false)]
    [InlineData("ZEX-1", SearchOperator.Exists, "", true)]
    [InlineData("ZEX-1", SearchOperator.DoesNotExist, "", false)]
    [InlineData("ZEX-1", SearchOperator.DoesNotEqual, "X", true)]
    [InlineData("ZEX-1", SearchOperator.DoesNotContain, "X", true)]
    [InlineData("ZEX-1.1", SearchOperator.Exists, "", true)]
    [InlineData("ZEX-1.2", SearchOperator.DoesNotEqual, "X", false)]
    [InlineData("ZEX-1.2", SearchOperator.DoesNotExist, "", true)]
    [InlineData("MSH-1", SearchOperator.Equals, "|", true)]
    [InlineData("MSH-2", SearchOperator.Equals, "^~\\&", true)]
    [InlineData("MSH-2.2", SearchOperator.DoesNotExist, "", true)]
    public async Task FieldOperatorsRespectPresenceAndEveryApplicableRepetition(string path, SearchOperator operation, string value, bool expected)
    {
        await using var documents = new Documents(); var document = await documents.Open();
        var results = await new MessageSearch().SearchAsync(document, MessageSearchQuery.Advanced([new(SearchTarget.Field, operation, path, value)]), token: Token);
        Assert.Equal(expected ? 1 : 0, results.Matches.Count);
        if (expected) { var match = Assert.Single(results.Matches); Assert.Equal(1, match.MatchCount); Assert.Equal("All 1 conditions", match.ConditionSummary); }
        Assert.False(document.IsDirty); Assert.False(document.IsEditMode);
    }
    [Theory]
    [InlineData(SearchOperator.Contains, "example", false, true)]
    [InlineData(SearchOperator.Contains, "example", true, false)]
    [InlineData(SearchOperator.DoesNotContain, "example", false, false)]
    [InlineData(SearchOperator.DoesNotContain, "example", true, true)]
    [InlineData(SearchOperator.DoesNotContain, "NEVER-PRESENT", true, true)]
    public async Task TextOperatorsRemainLiteralOrdinal(SearchOperator operation, string value, bool sensitive, bool expected)
    {
        await using var documents = new Documents(); var document = await documents.Open();
        var result = await new MessageSearch().SearchAsync(document, MessageSearchQuery.Advanced([new(SearchTarget.MessageText, operation, Value: value, CaseSensitive: sensitive)]), token: Token);
        Assert.Equal(expected ? 1 : 0, result.Matches.Count);
    }
    [Theory]
    [InlineData(SearchOperator.Equals, false, true)] [InlineData(SearchOperator.Equals, true, false)]
    [InlineData(SearchOperator.DoesNotEqual, false, false)] [InlineData(SearchOperator.DoesNotEqual, true, true)]
    [InlineData(SearchOperator.DoesNotContain, false, false)] [InlineData(SearchOperator.DoesNotContain, true, true)]
    public async Task RepeatedFieldComparisonsUseTheSelectedOrdinalCaseSetting(SearchOperator operation, bool sensitive, bool expected)
    {
        await using var documents = new Documents(); var document = await documents.Open();
        var result = await new MessageSearch().SearchAsync(document, MessageSearchQuery.Advanced([new(SearchTarget.Field, operation, "PID-5.1", "EXAMPLE", sensitive)]), token: Token);
        Assert.Equal(expected ? 1 : 0, result.Matches.Count);
    }
    [Theory]
    [InlineData("PID", SearchOperator.Exists, true)]
    [InlineData("AL1", SearchOperator.DoesNotExist, true)]
    [InlineData("OBX", SearchOperator.DoesNotExist, false)]
    [InlineData("BAD", SearchOperator.Exists, false)]
    public async Task SegmentPresenceUsesExposedCoreStructure(string path, SearchOperator operation, bool expected)
    {
        await using var documents = new Documents(); var document = await documents.Open();
        var result = await new MessageSearch().SearchAsync(document, MessageSearchQuery.Advanced([new(SearchTarget.Segment, operation, path)]), token: Token);
        Assert.Equal(expected ? 1 : 0, result.Matches.Count);
    }
    [Fact]
    public async Task BooleanQueriesKeepOneRowOriginalOrdinalsAndBoundedSafeConditionSummaries()
    {
        await using var documents = new Documents(); var document = await documents.Open(
            Header + "PID|1||SYN-001\rPV1||I\r" + Header + "PID|1||SYN-002\rPV1||O\r" + Header.Replace("ADT^A01", "ORU^R01", StringComparison.Ordinal) + "PID|1||SYN-003\r");
        var search = new MessageSearch();
        var all = await search.SearchAsync(document, MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Equals, "MSH-9.1", "ADT"),
            new(SearchTarget.Field, SearchOperator.Equals, "PV1-2", "I")]), token: Token);
        var row = Assert.Single(all.Matches); Assert.Equal(1, row.MessageNumber); Assert.Equal(2, row.MatchCount); Assert.Equal("All 2 conditions", row.ConditionSummary);
        var any = await search.SearchAsync(document, MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Contains, "PID-3.1", "SYN-001"),
            new(SearchTarget.Field, SearchOperator.Contains, "PID-3.1", "SYN-002")], SearchCombination.Any), token: Token);
        Assert.Equal(new[] { 1, 2 }, any.Matches.Select(m => m.MessageNumber)); Assert.All(any.Matches, m => Assert.Equal(1, m.MatchCount));
        Assert.Contains("Condition 2", any.Matches[1].ConditionSummary); Assert.DoesNotContain("SYN", any.Matches[1].ConditionSummary);
        Assert.DoesNotContain("SYN", any.Matches[1].ToString());
        var investigation = await search.SearchAsync(document, MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Equals, "MSH-9.1", "ORU"),
            new(SearchTarget.Segment, SearchOperator.DoesNotExist, "OBX"), new(SearchTarget.MessageText, SearchOperator.DoesNotContain, Value: "NEVER")]), token: Token);
        Assert.Equal(3, Assert.Single(investigation.Matches).MessageNumber);
    }
    [Fact]
    public async Task CustomDelimitersWhitespaceMalformedAndBareSegmentsAreNotNormalized()
    {
        await using var documents = new Documents(); var document = await documents.Open("MSH*$%!@*SYN******ADT$A01*SYN-1*T*2.5\rPID*1** 001 $MR%002$ALT\rPV1\rOBXopaque\r");
        var query = MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Equals, "PID-3.1", " 001 "), new(SearchTarget.Segment, SearchOperator.Exists, "PV1"), new(SearchTarget.Segment, SearchOperator.DoesNotExist, "OBX")]);
        Assert.Single((await new MessageSearch().SearchAsync(document, query, token: Token)).Matches);
        var notEqual = MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.DoesNotEqual, "PID-3.1", "001")]);
        Assert.Single((await new MessageSearch().SearchAsync(document, notEqual, token: Token)).Matches);
        using var docs = new DocumentManager(); var malformed = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse("bad line\rPID|1||SYN-ONLY\r")); docs.Open(malformed);
        Assert.Single((await new MessageSearch().SearchAsync(malformed, MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Contains, "PID-3", "SYN")]), token: Token)).Matches);
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)] [InlineData(8)] [InlineData(9)] [InlineData(10)]
    [InlineData(11)] [InlineData(12)] [InlineData(13)] [InlineData(14)]
    public async Task InvalidQueriesNeverBecomeMatchAll(int invalid)
    {
        var valid = new SearchCondition(SearchTarget.Field, SearchOperator.Equals, "PID-3.1", "SYN");
        var query = invalid switch
        {
            0 => MessageSearchQuery.Advanced([]),
            1 => MessageSearchQuery.Advanced(Enumerable.Repeat(valid, 17)),
            // Blank containment remains invalid; Stage 20A exact field equality
            // deliberately supports represented empty/whitespace values.
            2 => MessageSearchQuery.Advanced([valid with { Operator = SearchOperator.Contains, Value = " " }]),
            3 => MessageSearchQuery.Advanced([valid with { Path = "PID-0" }]),
            4 => MessageSearchQuery.Advanced([valid with { Path = "PID-3[1]" }]),
            5 => MessageSearchQuery.Advanced([valid with { Path = "PID-3.1.1.1" }]),
            6 => MessageSearchQuery.Advanced([new(SearchTarget.Segment, SearchOperator.Exists, "pid")]),
            7 => MessageSearchQuery.Advanced([new(SearchTarget.MessageText, SearchOperator.Equals, Value: "SYN")]),
            8 => MessageSearchQuery.Advanced([valid with { Operator = (SearchOperator)99 }]),
            9 => MessageSearchQuery.Advanced([valid]) with { Version = 99 },
            10 => MessageSearchQuery.Advanced([valid], (SearchCombination)99),
            11 => MessageSearchQuery.Advanced([valid with { Target = (SearchTarget)99 }]),
            12 => MessageSearchQuery.Advanced([new(SearchTarget.Segment, SearchOperator.Contains, "PID", "SYN")]),
            13 => MessageSearchQuery.Advanced([valid with { Value = new string('x', 1025) }]),
            _ => MessageSearchQuery.Advanced([null!])
        };
        Assert.NotNull(query.ValidationError()); Assert.DoesNotContain("SYN", query.ToString()); Assert.DoesNotContain("SYN", valid.ToString());
        await using var documents = new Documents(); var document = await documents.Open();
        await Assert.ThrowsAsync<ArgumentException>(() => new MessageSearch().SearchAsync(document, query, token: Token));
    }
    [Fact]
    public async Task SnapshotsAndShortCircuitingDoNotUseMutableInputsOrPartialStructure()
    {
        await using var documents = new Documents(); var document = await documents.Open();
        var conditions = new List<SearchCondition> { new(SearchTarget.Segment, SearchOperator.Exists, "PID") };
        var query = MessageSearchQuery.Advanced(conditions) with { Conditions = conditions };
        var pending = new MessageSearch().SearchAsync(document, query, token: Token); conditions[0] = new(SearchTarget.Segment, SearchOperator.Exists, "ZZZ");
        Assert.Single((await pending).Matches);
        Assert.Null(MessageSearchQuery.Advanced(Enumerable.Repeat(new SearchCondition(SearchTarget.Segment, SearchOperator.Exists, "PID"), 16)).ValidationError());
        var overBudget = await documents.Open(Header + "PID|1||" + string.Join('~', Enumerable.Repeat("ZUNIQUE", 10001)) + "\r");
        var any = await new MessageSearch().SearchAsync(overBudget, MessageSearchQuery.Advanced([new(SearchTarget.MessageText, SearchOperator.Contains, Value: "ZUNIQUE"), new(SearchTarget.Segment, SearchOperator.Exists, "PID")], SearchCombination.Any), token: Token);
        Assert.Single(any.Matches); Assert.Equal(0, any.SkippedMessages);
        var structural = await new MessageSearch().SearchAsync(overBudget, MessageSearchQuery.Advanced([new(SearchTarget.Segment, SearchOperator.Exists, "PID")]), token: Token);
        Assert.Empty(structural.Matches); Assert.Equal(1, structural.SkippedMessages);
    }
    [Fact]
    public async Task AdvancedSessionsPreserveDraftsIdentityPagingFilteringAndSupersession()
    {
        await using var documents = new Documents(); var document = await documents.Open(string.Concat(Enumerable.Repeat(Header + Body, 451)));
        var query = MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Contains, "PID-3.1", "001"), new(SearchTarget.Segment, SearchOperator.DoesNotExist, "AL1")]);
        using var session = new MessageSearchSession(documents.Manager); await session.StartAsync(query, Token);
        Assert.Equal(451, session.ResultCount); Assert.Equal(3, session.Results!.PageCount); session.SetPage(2); Assert.Equal(51, session.Page.Count);
        session.FilterNavigator = true; await session.NavigateAsync(session.Page[0], Token); Assert.Equal(401, document.MessageNumber);
        var old = session.Results; document.IsEditMode = true; document.ReplaceWorkingText(document.EditableText!.Replace("001", "UNSAVED", StringComparison.Ordinal));
        Assert.Null(session.Results); Assert.Null(document.NavigationMatches); Assert.False(old!.IsCurrent(document)); Assert.True(document.IsDirty);
        var pending = session.StartAsync(query, Token); var latest = session.StartAsync(MessageSearchQuery.Advanced([new(SearchTarget.MessageText, SearchOperator.Contains, Value: "UNSAVED")]), Token);
        await Task.WhenAll(pending, latest); Assert.Equal(401, Assert.Single(session.Results!.Matches).MessageNumber);
        var other = await documents.Open(); Assert.Null(session.Results); Assert.Null(document.NavigationMatches);
        documents.Manager.Activate(document.Id); await session.StartAsync(query, Token); session.FilterNavigator = true;
        var scanning = session.StartAsync(query, Token); var directory = document.Snapshot!.DirectoryPath;
        documents.Manager.Close(document.Id, true); await scanning; await document.Disposal;
        Assert.Null(session.Results); Assert.False(Directory.Exists(directory)); Assert.False(session.IsBusy); Assert.NotSame(document, other);
    }
    [Fact]
    public async Task StressAdvancedAllAnyNegativesMissingSegmentsAndLargeFieldsRemainBounded()
    {
        if (Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") != "1") return;
        await using var documents = new Documents(); var document = await documents.Open(string.Concat(Enumerable.Repeat(Header + "PID|1||SYN-HIGH\r", 50000)));
        var query = MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Equals, "MSH-9.1", "ADT"), new(SearchTarget.Segment, SearchOperator.DoesNotExist, "AL1"), new(SearchTarget.MessageText, SearchOperator.DoesNotContain, Value: "NEVER")]);
        var before = GC.GetTotalMemory(true); var result = await new MessageSearch().SearchAsync(document, query, token: Token);
        Assert.Equal(50000, result.Matches.Count); Assert.Equal(250, result.PageCount); Assert.Equal(200, result.Page(249).Count);
        var retained = GC.GetTotalMemory(true) - before; GC.KeepAlive(result); Assert.True(retained < 80L * 1024 * 1024, $"Advanced result retained growth: {retained}");
        var nested = MessageSearchQuery.Advanced(All(Any(Field("PID-3.1", "SYN-HIGH"), Segment("OBX")), All(Field("MSH-9.1", "ADT"), Segment("AL1", false))));
        Assert.Equal(50000, (await new MessageSearch().SearchAsync(document, nested, token: Token)).Matches.Count);
        var any = query with { Combination = SearchCombination.Any, Conditions = [new(SearchTarget.Field, SearchOperator.DoesNotEqual, "PID-3.1", "OTHER"), new(SearchTarget.Segment, SearchOperator.DoesNotExist, "OBX")] };
        Assert.Equal(50000, (await new MessageSearch().SearchAsync(document, any, token: Token)).Matches.Count);
        Assert.Empty((await new MessageSearch().SearchAsync(document, MessageSearchQuery.Advanced([new(SearchTarget.MessageText, SearchOperator.Contains, Value: "ZERO")]), token: Token)).Matches);
        using var cancellation = new CancellationTokenSource(); var scanned = 0;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MessageSearch().SearchAsync(document, query, new Progress(p => { scanned = p.Scanned; cancellation.Cancel(); }), cancellation.Token));
        Assert.Equal(1, scanned);
        var large = await documents.Open(Header + "OBX|1|TX|||" + new string('x', 32 * 1024 * 1024) + "SYN-END"); var largeBefore = GC.GetTotalMemory(true);
        var largeResult = await new MessageSearch().SearchAsync(large, MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Contains, "OBX-5", "SYN-END"), new(SearchTarget.Segment, SearchOperator.Exists, "OBX")]), token: Token);
        Assert.Single(largeResult.Matches); var largeRetained = GC.GetTotalMemory(true) - largeBefore; GC.KeepAlive(largeResult);
        Assert.True(largeRetained < 16L * 1024 * 1024, $"Advanced large-message retained growth: {largeRetained}");
    }
}
