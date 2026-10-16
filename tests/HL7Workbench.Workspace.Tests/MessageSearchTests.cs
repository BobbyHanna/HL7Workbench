using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class MessageSearchTests
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261016000000||ADT^A01|SYN-1|T|2.5\r";
    private const string Body = "PID|1||001^MR~002^ALT||Example^Ada~Other^Bea\rPID|2||003^MR||Other^ADA\rPV1||I\rOBX|1|TX|||zzzz\rBAD segment|NEEDLE\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class Progress(Action<MessageSearchProgress> report) : IProgress<MessageSearchProgress>
    { public void Report(MessageSearchProgress value) => report(value); }
    private sealed class Files : IAsyncDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-search-test-" + Guid.NewGuid().ToString("N"));
        public DocumentManager Manager { get; } = new();
        public Files() => Directory.CreateDirectory(Root);
        public async Task<MessageDocument> Open(string text = Header + Body, int codePage = 65001)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            var encoding = Encoding.GetEncoding(codePage); using var input = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            if (codePage == 1252)
            {
                var explicitMessage = new MessageDocument(Guid.NewGuid().ToString(), "Synthetic.hl7", Hl7Parser.Parse(input.ToArray(), new Hl7ParseOptions { Encoding = encoding }));
                Manager.Open(explicitMessage); return explicitMessage;
            }
            return await MessageFileReader.OpenFileAsync(Manager, "Synthetic.hl7", input, Token);
        }
        public async ValueTask DisposeAsync()
        { var docs = Manager.Documents.ToArray(); Manager.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal)); Directory.Delete(Root, true); }
    }
    [Theory]
    [InlineData("example", false, 1)] [InlineData("example", true, 0)] [InlineData("Example", true, 1)]
    [InlineData("zz", false, 2)] [InlineData("zzz", false, 1)] [InlineData("NEEDLE", true, 1)]
    [InlineData("not-present", false, 0)] [InlineData("Example^Ada", true, 1)] [InlineData(".*", false, 0)]
    public async Task LiteralTextIsOrdinalNonOverlappingAndIncludesOpaqueMalformedContent(string value, bool sensitive, int count)
    {
        await using var files = new Files(); var doc = await files.Open(); var original = doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length));
        var result = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Text, value, sensitive), token: Token);
        Assert.Equal(count == 0 ? 0 : 1, result.Matches.Count);
        if (count > 0) { var match = Assert.Single(result.Matches); Assert.Equal(count, match.MatchCount); Assert.Equal(1, match.MessageNumber); Assert.Equal("ADT^A01", match.MessageType); Assert.Equal("SYN-1", match.ControlId); }
        Assert.Equal(original, doc.Snapshot.ReadBytes(new(0, doc.Snapshot.Length))); Assert.False(doc.IsDirty); Assert.False(doc.IsEditMode);
    }
    [Theory]
    [InlineData("PID-3", "00", MessageSearchComparison.Contains, false, 3)]
    [InlineData("PID-3", "001^MR", MessageSearchComparison.Equals, false, 1)]
    [InlineData("PID-3", "001^MR~002^ALT", MessageSearchComparison.Equals, false, 0)]
    [InlineData("PID-3.1", "00", MessageSearchComparison.Contains, false, 3)]
    [InlineData("PID-3.1", "001", MessageSearchComparison.Equals, false, 1)]
    [InlineData("PID-3.1", "1", MessageSearchComparison.Equals, false, 0)]
    [InlineData("PID-5.1", "Other", MessageSearchComparison.Equals, false, 2)]
    [InlineData("PID-5.2", "ada", MessageSearchComparison.Equals, false, 2)]
    [InlineData("PID-5.2", "ada", MessageSearchComparison.Equals, true, 0)]
    [InlineData("PID-5.2", "Ada", MessageSearchComparison.Contains, true, 1)]
    [InlineData("PID-3.3", "001", MessageSearchComparison.Contains, false, 0)]
    [InlineData("PID-99", "1", MessageSearchComparison.Contains, false, 0)]
    [InlineData("ZZZ-1", "1", MessageSearchComparison.Contains, false, 0)]
    [InlineData("BAD-1", "NEEDLE", MessageSearchComparison.Contains, false, 0)]
    [InlineData("PV1-2", "I", MessageSearchComparison.Equals, true, 1)]
    [InlineData("MSH-9", "ADT^A01", MessageSearchComparison.Equals, true, 1)]
    [InlineData("MSH-9.1", "ADT", MessageSearchComparison.Equals, true, 1)]
    [InlineData("MSH-1", "|", MessageSearchComparison.Equals, true, 1)]
    [InlineData("MSH-2", "^~\\&", MessageSearchComparison.Equals, true, 1)]
    [InlineData("MSH-2.1", "^~\\&", MessageSearchComparison.Equals, true, 1)]
    [InlineData("MSH-2.2", "^", MessageSearchComparison.Contains, true, 0)]
    public async Task FieldsUseCoreCoordinatesAndIndividualRepetitions(string path, string value, MessageSearchComparison comparison, bool sensitive, int count)
    {
        await using var files = new Files(); var doc = await files.Open();
        var results = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Field, value, sensitive, path, comparison), token: Token);
        Assert.Equal(count == 0 ? 0 : 1, results.Matches.Count);
        if (count > 0) { var match = Assert.Single(results.Matches); Assert.Equal(count, match.MatchCount); Assert.StartsWith(path[..3], match.Location); Assert.Equal($"Message 1: {count} matches", match.ToString()); }
    }
    [Theory]
    [InlineData("", "X")] [InlineData("   ", "X")] [InlineData("pid-3", "X")] [InlineData("PID-0", "X")]
    [InlineData("PID-3[1]", "X")] [InlineData("PID-3.1.1", "X")] [InlineData("PID-3", "")]
    [InlineData("PID-3", " \r\n\t")]
    public async Task InvalidOrBlankInputsHaveSafeValidationWithoutSearching(string path, string value)
    {
        await using var files = new Files(); var doc = await files.Open();
        var query = new MessageSearchQuery(MessageSearchMode.Field, value, FieldPath: path);
        Assert.NotNull(query.ValidationError()); Assert.Equal("Message search query", query.ToString());
        await Assert.ThrowsAsync<ArgumentException>(() => new MessageSearch().SearchAsync(doc, query, token: Token));
        Assert.NotNull(new MessageSearchQuery(MessageSearchMode.Text, " ").ValidationError());
        Assert.NotNull(new MessageSearchQuery(MessageSearchMode.Text, new string('x', 1025)).ValidationError());
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(12000)] [InlineData(1252)]
    public async Task CustomDelimitersUnicodeAndTerminatorsUseOriginalRepresentation(int codePage)
    {
        await using var files = new Files();
        var text = "MSH*$%!@*SYN*TEST*DEST*TEST*20261016000000**ADT$A01*SYN-2*T*2.5\rPID*1**001$MR%002$ALT**Exämple$Ada\r\nZTX*Literal**X\n";
        var doc = await files.Open(text, codePage);
        var search = new MessageSearch();
        var field = await search.SearchAsync(doc, new(MessageSearchMode.Field, "002", FieldPath: "PID-3.1", Comparison: MessageSearchComparison.Equals), token: Token);
        Assert.Equal(1, Assert.Single(field.Matches).MatchCount);
        Assert.Single((await search.SearchAsync(doc, new(MessageSearchMode.Text, "exÄmple"), token: Token)).Matches);
        Assert.Equal(1, Assert.Single((await search.SearchAsync(doc, new(MessageSearchMode.Text, "Ada\r\n"), token: Token)).Matches).MatchCount);
        Assert.Equal(1, Assert.Single((await search.SearchAsync(doc, new(MessageSearchMode.Text, "X\n"), token: Token)).Matches).MatchCount);
    }
    [Fact]
    public async Task ChunkBoundaryMatchesIncludeNonAsciiAndIgnoreCaseSupplementaryCharacters()
    {
        await using var files = new Files(); var prefix = Header + "OBX|1|TX|||";
        var text = prefix + new string('x', 32767 - prefix.Length) + "😀cAfÉ𐐀" + new string('x', 40000);
        var doc = await files.Open(text);
        var results = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Text, "😀CAFÉ𐐨"), token: Token);
        Assert.Equal(1, Assert.Single(results.Matches).MatchCount);
    }
    [Fact]
    public async Task ResultsPageByOriginalOrdinalsWithBoundedHeadersAndSafeDisplay()
    {
        await using var files = new Files(); var text = string.Concat(Enumerable.Range(1, 451).Select(i => Header.Replace("SYN-1", "SYN-" + i, StringComparison.Ordinal) + "PID|1||SYN-PRIVATE-QUERY\r"));
        var doc = await files.Open(text); var results = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Text, "SYN-PRIVATE-QUERY"), token: Token);
        Assert.Equal(451, results.Matches.Count); Assert.Equal(3, results.PageCount);
        Assert.Equal(200, results.Page(0).Count); Assert.Equal(201, results.Page(1)[0].MessageNumber); Assert.Equal(51, results.Page(2).Count);
        Assert.All(results.Matches, m => { Assert.DoesNotContain("PRIVATE", m.Location); Assert.DoesNotContain("PRIVATE", m.ToString()); Assert.InRange(m.MessageType.Length, 0, 49); Assert.InRange(m.ControlId.Length, 0, 97); });
        Assert.Throws<ArgumentOutOfRangeException>(() => results.Page(3)); Assert.Equal(doc.Id, results.DocumentId);
    }
    [Fact]
    public async Task BadEncodingIsSkippedPerMessageAndStoredWhitespaceIsNeverTrimmed()
    {
        await using var files = new Files();
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes(Header + "PID|1||").Concat(new byte[] { 255, 13 })
            .Concat(Encoding.UTF8.GetBytes(Header + "PID|1|| 001 \r")).ToArray());
        var doc = await MessageFileReader.OpenFileAsync(files.Manager, "Synthetic malformed.hl7", bytes, Token);
        var search = new MessageSearch();
        var exact = await search.SearchAsync(doc, new(MessageSearchMode.Field, " 001 ", FieldPath: "PID-3", Comparison: MessageSearchComparison.Equals), token: Token);
        Assert.Equal(1, exact.SkippedMessages); Assert.Equal(2, Assert.Single(exact.Matches).MessageNumber);
        Assert.Empty((await search.SearchAsync(doc, new(MessageSearchMode.Field, "001", FieldPath: "PID-3", Comparison: MessageSearchComparison.Equals), token: Token)).Matches);
    }
    [Fact]
    public async Task MalformedButReadableMaterializedMessagesUseTheSameScanner()
    {
        using var docs = new DocumentManager(); var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse("bad line\rPID|1||SYN-001\r")); docs.Open(doc);
        var results = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Field, "SYN-001", FieldPath: "PID-3"), token: Token);
        Assert.Single(results.Matches); Assert.Empty(results.Matches[0].MessageType);
    }
    [Fact]
    public async Task SearchAndNavigatorRespectUnsavedRevisionsSavingAndNonContentNavigation()
    {
        await using var files = new Files(); var doc = await files.Open(Header + Body + Header + "PID|1||OTHER\r");
        using var session = new MessageSearchSession(files.Manager);
        await session.StartAsync(new(MessageSearchMode.Field, "OTHER", FieldPath: "PID-3", Comparison: MessageSearchComparison.Equals), Token);
        Assert.Equal(2, Assert.Single(session.Results!.Matches).MessageNumber);
        session.FilterNavigator = true; Assert.Equal(new[] { 2 }, doc.NavigationMatches);
        var old = session.Results; await session.NavigateAsync(old.Matches[0], Token); Assert.Equal(2, doc.MessageNumber); Assert.Same(old, session.Results);
        doc.IsEditMode = true; doc.ReplaceWorkingText(doc.EditableText!.Replace("OTHER", "UNSAVED", StringComparison.Ordinal));
        Assert.Null(session.Results); Assert.Null(doc.NavigationMatches); Assert.False(session.FilterNavigator); Assert.Contains("changed", session.Status);
        await doc.EditRefresh; await session.StartAsync(new(MessageSearchMode.Text, "UNSAVED"), Token);
        Assert.Equal(2, Assert.Single(session.Results!.Matches).MessageNumber);
        var revision = doc.SearchRevision; await doc.SaveAsync(Path.Combine(files.Root, "saved.hl7"), Token);
        Assert.Equal(revision, doc.SearchRevision); Assert.True(session.Results.IsCurrent(doc));
        session.FilterNavigator = true; session.FilterNavigator = false; Assert.Null(doc.NavigationMatches);
        session.Clear(); Assert.Null(session.Results); Assert.Null(doc.NavigationMatches);
    }
    [Fact]
    public async Task SwitchingClosureReloadAndSameIdDoNotReuseResultsOrFilters()
    {
        await using var files = new Files(); var first = await files.Open(); var second = await files.Open(Header + "PID|1||SECOND\r");
        using var session = new MessageSearchSession(files.Manager); files.Manager.Activate(first.Id);
        await session.StartAsync(new(MessageSearchMode.Text, "Example"), Token); var old = session.Results!; session.FilterNavigator = true;
        files.Manager.Activate(second.Id); Assert.Null(session.Results); Assert.Null(first.NavigationMatches); Assert.False(session.FilterNavigator);
        Assert.False(old.IsCurrent(second)); files.Manager.Activate(first.Id); Assert.Null(session.Results);
        files.Manager.Close(first.Id, true); await first.Disposal; Assert.False(old.IsCurrent(first));
        var replacement = new MessageDocument(first.Id, "Synthetic reload", Hl7Parser.Parse(Header)); files.Manager.Open(replacement);
        Assert.False(old.IsCurrent(replacement)); Assert.Null(session.Results); Assert.Null(replacement.NavigationMatches);
    }
    [Fact]
    public async Task CancellationAndSupersessionNeverPublishStaleOrPartialResultsAndDrainOnClose()
    {
        await using var files = new Files(); var doc = await files.Open(string.Concat(Enumerable.Repeat(Header + "PID|1||SYN-A\r", 6000)));
        using var session = new MessageSearchSession(files.Manager);
        var first = session.StartAsync(new(MessageSearchMode.Text, "SYN-A"), Token);
        var second = session.StartAsync(new(MessageSearchMode.Text, "not-present"), Token);
        await Task.WhenAll(first, second); Assert.Empty(session.Results!.Matches); Assert.False(session.IsBusy);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await session.StartAsync(new(MessageSearchMode.Text, "SYN-A"), cancel.Token); Assert.Null(session.Results); Assert.Contains("canceled", session.Status);
        var snapshotDirectory = doc.Snapshot!.DirectoryPath;
        var pending = session.StartAsync(new(MessageSearchMode.Text, "SYN-A"), Token);
        files.Manager.Close(doc.Id, true); await pending; await doc.Disposal;
        Assert.Null(session.Results); Assert.False(session.IsBusy); Assert.False(Directory.Exists(snapshotDirectory));
    }
    [Fact]
    public async Task EditingDuringSearchCancelsTheCapturedRevisionWithoutDiscardingDrafts()
    {
        await using var files = new Files(); var doc = await files.Open(string.Concat(Enumerable.Repeat(Header + Body, 4000)));
        using var session = new MessageSearchSession(files.Manager); doc.IsEditMode = true;
        var pending = session.StartAsync(new(MessageSearchMode.Text, "Example"), Token);
        doc.ReplaceWorkingText(doc.EditableText!.Replace("Example", "UNSAVED", StringComparison.Ordinal)); await pending; await doc.EditRefresh;
        Assert.Null(session.Results); Assert.True(doc.IsDirty); Assert.Contains("UNSAVED", doc.WorkingText!); Assert.Contains("changed", session.Status);
        await session.StartAsync(new(MessageSearchMode.Text, "UNSAVED"), Token); Assert.Equal(1, Assert.Single(session.Results!.Matches).MessageNumber);
    }
    [Fact]
    public async Task InFlightCancellationStopsAfterReportedWorkWithoutPublishingPartialRows()
    {
        await using var files = new Files(); var doc = await files.Open(string.Concat(Enumerable.Repeat(Header + Body, 1000)));
        using var cancel = new CancellationTokenSource(); var scanned = 0;
        var progress = new Progress(value => { scanned = value.Scanned; cancel.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Text, "Example"), progress, cancel.Token));
        Assert.Equal(1, scanned); Assert.Equal(1, doc.MessageNumber); Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task StructuralBudgetDoesNotHideLiteralMatchesOrClaimCompleteFieldCoverage()
    {
        await using var files = new Files(); var doc = await files.Open(Header + "PID|1||" + string.Join("~", Enumerable.Repeat("ZUNIQUE", 10001)) + "\r");
        var search = new MessageSearch();
        var text = await search.SearchAsync(doc, new(MessageSearchMode.Text, "ZUNIQUE"), token: Token);
        Assert.Equal(10001, Assert.Single(text.Matches).MatchCount);
        var field = await search.SearchAsync(doc, new(MessageSearchMode.Field, "ZUNIQUE", FieldPath: "PID-3"), token: Token);
        Assert.Empty(field.Matches); Assert.Equal(1, field.SkippedMessages);
    }
    [Fact]
    public async Task StressSearchLargeAndHighMatchDocumentsKeepsPagingAndCancellationBounded()
    {
        if (Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") != "1") return;
        await using var files = new Files();
        var doc = await files.Open(string.Concat(Enumerable.Repeat(Header + "PID|1||SYN-HIGH-MATCH\r", 50000)));
        var before = GC.GetTotalMemory(true); var results = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Text, "SYN-HIGH-MATCH"), token: Token);
        Assert.Equal(50000, results.Matches.Count); Assert.Equal(250, results.PageCount); Assert.Equal(200, results.Page(249).Count);
        // Retained growth has an upper bound; collecting earlier fixture state may
        // legitimately make the process-wide delta negative. Keep results live
        // through measurement so the bound includes the complete metadata set.
        var retained = GC.GetTotalMemory(true) - before; GC.KeepAlive(results);
        Assert.True(retained <= 80L * 1024 * 1024, $"Search metadata retained growth: {retained} bytes.");
        Assert.Empty((await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Text, "ZERO-MATCH"), token: Token)).Matches);
        using var cancel = new CancellationTokenSource(); var progress = new Progress(_ => cancel.Cancel());
        var pending = new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Text, "SYN-HIGH-MATCH"), progress, cancel.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        var large = await files.Open(Header + "OBX|1|TX|||" + new string('x', 32 * 1024 * 1024) + "LARGE-END");
        var largeBefore = GC.GetTotalMemory(true);
        var largeResult = await new MessageSearch().SearchAsync(large, new(MessageSearchMode.Field, "LARGE-END", FieldPath: "OBX-5"), token: Token);
        Assert.Equal(1, Assert.Single(largeResult.Matches).MatchCount);
        var largeRetained = GC.GetTotalMemory(true) - largeBefore; GC.KeepAlive(largeResult);
        Assert.True(largeRetained <= 16L * 1024 * 1024, $"Large-message search retained growth: {largeRetained} bytes.");
        TestContext.Current.TestOutputHelper?.WriteLine($"50,000 results / 250 pages: retained growth {retained} bytes; 32 MiB field search: retained growth {largeRetained} bytes.");
    }
}
