using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Querying;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class SqlQueryTests : IAsyncDisposable
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20300101000000||ADT^A01|SYN-DUP|T|2.5.1\r";
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-sql-synthetic-" + Guid.NewGuid().ToString("N"));
    private readonly DocumentManager documents = new();
    private readonly List<SqlQueryResults> results = [];
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private SqlQueryOptions Options => new() { WorkerPath = Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll"), StorageRoot = Path.Combine(root, "query") };
    private async Task<MessageDocument> Open(string text, Encoding? encoding = null)
    {
        encoding ??= new UTF8Encoding(false, true);
        using var input = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        return await MessageFileReader.OpenFileAsync(documents, "Synthetic.hl7", input, Token);
    }
    private async Task<SqlQueryResults> Run(string sql, IReadOnlyList<MessageDocument>? sources = null, SqlQueryOptions? options = null,
        IReadOnlyList<QueryParameter>? parameters = null)
    {
        var result = await new SqlQuery(documents, options ?? Options).RunAsync(sql, sources, parameters, token: Token); results.Add(result); return result;
    }
    private static byte[] Bytes(MessageDocument doc) => doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length), int.MaxValue);
    [Fact]
    public async Task MetadataOnlyUsesHeadersAndAuthoritativeIdentitiesWithoutDecodingLargeObx()
    {
        var doc = await Open(Header + "PID|1||SYN-ID\rOBX|1|ED|SYN||" + new string('Q', 2 * 1024 * 1024) + "\r");
        var original = Bytes(doc); var revision = doc.SearchRevision;
        using var result = await Run("SELECT DocumentId, MessageId, MessageNumber, MessageType, TriggerEvent, MessageControlId, DiagnosticStatus FROM Messages");
        var row = Assert.Single(result.ReadRow(0).Cells.Select(c => c).Chunk(7));
        Assert.Equal(doc.Id, row[0].Text); Assert.Equal(doc.GetMessageId(1).Value, row[1].Integer);
        Assert.Equal("ADT", row[3].Text); Assert.Equal("A01", row[4].Text); Assert.Equal("HeaderOnly", row[6].Text);
        Assert.Equal(0, result.Metrics.BodyDecodedCharacters); Assert.Equal(0, result.Metrics.ValueRequests); Assert.Equal(2, result.Metrics.ImportedRows);
        Assert.Equal(original, Bytes(doc)); Assert.Equal(revision, doc.SearchRevision); Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task JoinsRepetitionsComponentsSubcomponentsAndPatternQueriesReuseCoreCoordinates()
    {
        var doc = await Open(Header + "PID|1||SYN-A&LOCAL^MR~SYN-B^PI||Family^Given\rOBX|1|ST|SYN||Alpha\rOBX|2|ST|SYN||Beta\r");
        using var fields = await Run("""
            SELECT m.MessageNumber, f.RepetitionNumber, f.DecodedValue FROM Messages m
            JOIN Fields f ON f.DocumentId=m.DocumentId AND f.MessageId=m.MessageId
            WHERE f.SegmentName='PID' AND f.FieldNumber=3 ORDER BY f.RepetitionNumber
            """);
        Assert.Equal(2, fields.RowCount); Assert.Equal("SYN-A&LOCAL^MR", fields.ReadRow(0).Cells[2].Text); Assert.Equal("SYN-B^PI", fields.ReadRow(1).Cells[2].Text);
        using var component = await Run("SELECT DecodedValue FROM Subcomponents WHERE SegmentName='PID' AND FieldNumber=3 AND RepetitionNumber=1 AND ComponentNumber=1 AND SubcomponentNumber=2");
        Assert.Equal("LOCAL", component.ReadRow(0).Cells[0].Text);
        using var obx = await Run("SELECT SegmentOccurrence,DecodedValue FROM Fields WHERE SegmentName='OBX' AND FieldNumber=5 AND DecodedValue LIKE 'B%' ORDER BY SegmentOccurrence");
        Assert.Equal(1, obx.RowCount); Assert.Equal(2, obx.ReadRow(0).Cells[0].Integer); Assert.Equal("Beta", obx.ReadRow(0).Cells[1].Text);
        using var segments = await Run("SELECT SegmentName,SegmentOccurrence,SegmentOrdinal FROM Segments ORDER BY SegmentOrdinal");
        Assert.Equal(4, segments.RowCount); Assert.Equal(2, segments.ReadRow(3).Cells[1].Integer);
        Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task QualifiedFunctionsHandleCustomDelimitersEscapesAndExplicitValueStates()
    {
        await Open("MSH*$%!?*SYN\rPID**\"\"*A?B$MR%Second$PI**Family!S!Literal$Given\r");
        using var result = await Run("""
            SELECT HL7_VALUE(DocumentId,MessageId,'PID-5.1') AS Name,
              HL7_RAW(DocumentId,MessageId,'PID-5.1') AS Raw,
              HL7_VALUE(DocumentId,MessageId,'PID-3[2].1') AS Second,
              HL7_VALUE(DocumentId,MessageId,'PID-3.1.2') AS Sub,
              HL7_STATE(DocumentId,MessageId,'PID-1') AS Empty,
              HL7_STATE(DocumentId,MessageId,'PID-2') AS NullState,
              HL7_VALUE(DocumentId,MessageId,'PID-2') AS NullValue,
              HL7_STATE(DocumentId,MessageId,'NK1-1') AS Missing,
              HL7_STATUS(DocumentId,MessageId,'NK1-1') AS MissingStatus FROM Messages
            """);
        var cells = result.ReadRow(0).Cells;
        Assert.Equal("Family$Literal", cells[0].Text); Assert.Equal("Family!S!Literal", cells[1].Text);
        Assert.Equal("Second", cells[2].Text); Assert.Equal("B", cells[3].Text); Assert.Equal("Empty", cells[4].Text);
        Assert.Equal("ExplicitNull", cells[5].Text); Assert.Equal(QueryValueKind.Null, cells[6].Kind);
        Assert.Equal("Missing", cells[7].Text); Assert.Equal("MissingSegment", cells[8].Text);
    }
    [Fact]
    public async Task MultiDocumentScopeNeverCorrelatesDuplicateControlOrPatientIdentifiers()
    {
        var a = await Open(Header + "PID|1||SYN-SAME||First^A\r"); var b = await Open(Header + "PID|1||SYN-SAME||Second^B\r");
        using var result = await Run("SELECT DocumentId,MessageId,MessageControlId,HL7_VALUE(DocumentId,MessageId,'PID-5.1') AS Name FROM Messages ORDER BY DocumentId", [b, a]);
        Assert.Equal(2, result.RowCount);
        Assert.Equal(new[] { "First", "Second" }, Enumerable.Range(0, result.RowCount).Select(n => result.ReadRow(n).Cells[3].Text).Order());
        Assert.Equal(2, result.Sources.Select(s => (s.DocumentId, s.MessageId)).Distinct().Count());
        using var current = await Run("SELECT DocumentId FROM Messages"); Assert.Equal(b.Id, current.ReadRow(0).Cells[0].Text);
    }
    [Fact]
    public async Task LargeValueMetadataRemainsQueryableButValueMaterializationFailsCompletely()
    {
        await Open(Header + "PID|1||SYN\rOBX|1|ED|SYN||" + new string('Q', 100_000) + "\r");
        using var metadata = await Run("SELECT ValueLength,ValueState FROM Fields WHERE SegmentName='OBX' AND FieldNumber=5");
        Assert.Equal(100_000, metadata.ReadRow(0).Cells[0].Integer); Assert.Equal("Value", metadata.ReadRow(0).Cells[1].Text);
        using var status = await Run("SELECT HL7_STATUS(DocumentId,MessageId,'OBX-5') AS Status FROM Messages");
        Assert.Equal("ValueLimit", status.ReadRow(0).Cells[0].Text);
        var error = await Assert.ThrowsAsync<QueryException>(() => Run("SELECT DecodedValue FROM Fields WHERE SegmentName='OBX' AND FieldNumber=5"));
        Assert.Equal(QueryError.MaterializationLimit, error.Code);
        using var pid = await Run("SELECT DecodedValue FROM Fields WHERE SegmentName='PID' AND FieldNumber=3");
        Assert.Equal("SYN", pid.ReadRow(0).Cells[0].Text);
    }
    [Fact]
    public async Task MalformedSegmentsAndUnresolvedEscapesAreExplicitAndNeverBecomeValidLookingDecodedValues()
    {
        await Open(Header + "PID|1||SYN\\Zprivate\\\rnot-a-segment\r");
        using var diagnostic = await Run("SELECT m.HasDiagnostics,s.Status FROM Messages m JOIN Segments s ON s.DocumentId=m.DocumentId AND s.MessageId=m.MessageId WHERE s.Status='Unstructured'");
        Assert.Equal(1, diagnostic.RowCount); Assert.Equal(1, diagnostic.ReadRow(0).Cells[0].Integer);
        using var status = await Run("SELECT HL7_STATUS(DocumentId,MessageId,'PID-3') AS S,HL7_RAW(DocumentId,MessageId,'PID-3') AS R FROM Messages");
        Assert.Equal("DecodeDiagnostic", status.ReadRow(0).Cells[0].Text); Assert.Equal("SYN\\Zprivate\\", status.ReadRow(0).Cells[1].Text);
        Assert.Equal(QueryError.InvalidValue, (await Assert.ThrowsAsync<QueryException>(() => Run("SELECT HL7_VALUE(DocumentId,MessageId,'PID-3') FROM Messages"))).Code);
        Assert.Equal(QueryError.InvalidPath, (await Assert.ThrowsAsync<QueryException>(() => Run("SELECT HL7_VALUE(DocumentId,MessageId,'not-a-path') FROM Messages"))).Code);
    }
    [Fact]
    public async Task NotExistsFindsMissingPidWithoutAssumingAdtSchema()
    {
        await Open(Header + "PID|1||SYN\r" + Header.Replace("ADT^A01", "ORU^R01", StringComparison.Ordinal) + "OBX|1|ST|SYN||Test\r");
        using var result = await Run("SELECT m.DocumentId,m.MessageId,m.MessageNumber FROM Messages m WHERE NOT EXISTS (SELECT 1 FROM Segments s WHERE s.DocumentId=m.DocumentId AND s.MessageId=m.MessageId AND s.SegmentName='PID')");
        Assert.Equal(1, result.RowCount); Assert.Equal(2, result.ReadRow(0).Cells[2].Integer);
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)]
    public async Task ExtractionDeduplicatesAllPagesInSourceOrderAndPreservesExactPayloadBytes(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage); var text = Header + "PID|1||SYN-A\r\n" + Header + "PID|1||SYN-B\n";
        var doc = await Open(text, encoding); var before = Bytes(doc); var revision = doc.SearchRevision;
        using var result = await Run("SELECT m.DocumentId,m.MessageId FROM Messages m JOIN Fields f ON f.DocumentId=m.DocumentId AND f.MessageId=m.MessageId ORDER BY m.MessageNumber DESC");
        var output = await result.OpenMatchingMessagesAsync(documents, Token);
        Assert.NotNull(output); Assert.Equal(2, output.MessageCount); Assert.Equal(before, Bytes(output)); Assert.True(output.IsDirty); Assert.Null(output.SavePath);
        Assert.Equal(before, Bytes(doc)); Assert.Equal(revision, doc.SearchRevision); Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task FramedAndMalformedPayloadsAreCopiedWithoutNormalization()
    {
        var text = "\v" + Header + "PID|1||SYN-A\rmalformed-line\n\x1c\r";
        var doc = await Open(text); var before = Bytes(doc);
        var output = await new SqlQuery(documents, Options).OpenMatchingMessagesAsync("SELECT DocumentId,MessageId FROM Messages", token: Token);
        Assert.NotNull(output); Assert.Equal(before, Bytes(output)); Assert.Equal(before, Bytes(doc));
    }
    [Fact]
    public async Task AggregateComputedAndAliasedDisplayIdentitiesCannotAuthorizeExtraction()
    {
        await Open(Header + "PID|1||SYN\r");
        foreach (var sql in new[] { "SELECT count(*) FROM Messages", "SELECT DocumentId,MessageNumber AS MessageId FROM Messages", "SELECT DocumentId,CAST(MessageId AS TEXT) AS MessageId FROM Messages" })
        {
            using var result = await Run(sql); Assert.False(result.CanIdentifyMessages);
            await Assert.ThrowsAsync<QueryException>(() => result.OpenMatchingMessagesAsync(documents, Token));
        }
        using var none = await Run("SELECT DocumentId,MessageId FROM Messages WHERE 0");
        Assert.Null(await none.OpenMatchingMessagesAsync(documents, Token)); Assert.Single(documents.Documents);
    }
    [Fact]
    public async Task ResultAndMatchingLimitsCannotPublishPartialDocuments()
    {
        var doc = await Open(string.Concat(Enumerable.Range(0, 5).Select(_ => Header + "PID|1||SYN\r")));
        var limited = Options with { Budgets = new() { ResultRows = 2 } };
        Assert.Equal(QueryError.ResultLimit, (await Assert.ThrowsAsync<QueryException>(() => new SqlQuery(documents, limited).OpenMatchingMessagesAsync("SELECT DocumentId,MessageId FROM Messages", token: Token))).Code);
        using var result = await Run("SELECT DocumentId,MessageId FROM Messages", options: Options with { MaximumMatchingMessages = 2 });
        Assert.Equal(QueryError.ResultLimit, (await Assert.ThrowsAsync<QueryException>(() => result.OpenMatchingMessagesAsync(documents, Token))).Code);
        Assert.Single(documents.Documents); Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task MixedEncodingsWrappersAndUnsafeUnterminatedJoinsAreRefused()
    {
        var first = await Open(Header + "PID|1||SYN-A\r"); var second = await Open(Header + "PID|1||SYN-B\r", Encoding.Unicode);
        using var mixed = await Run("SELECT DocumentId,MessageId FROM Messages", [first, second]);
        await Assert.ThrowsAsync<QueryException>(() => mixed.OpenMatchingMessagesAsync(documents, Token));
        var wrapped = await Open("FHS|^~\\&|SYN\rBHS|^~\\&|SYN\r" + Header + "PID|1||SYN\rBTS|1\rFTS|1\r");
        using var wrapper = await Run("SELECT DocumentId,MessageId FROM Messages", [wrapped]);
        await Assert.ThrowsAsync<QueryException>(() => wrapper.OpenMatchingMessagesAsync(documents, Token));
        var unterminated = await Open(Header + "PID|1||SYN-END");
        var after = await Open(Header + "PID|1||SYN-AFTER\r");
        using var unsafeJoin = await Run("SELECT DocumentId,MessageId FROM Messages", [unterminated, after]);
        await Assert.ThrowsAsync<QueryException>(() => unsafeJoin.OpenMatchingMessagesAsync(documents, Token));
        Assert.Equal(5, documents.Documents.Count);
    }
    [Fact]
    public async Task WorkingEditsAndCompletedResultsRemainIndependentOfLaterEditsAndClosure()
    {
        var doc = await Open(Header + "PID|1||SYN-OLD\r"); doc.IsEditMode = true;
        doc.ReplaceWorkingText(Header + "PID|1||SYN-CAPTURED\r"); await doc.EditRefresh;
        using var result = await Run("SELECT DocumentId,MessageId,HL7_VALUE(DocumentId,MessageId,'PID-3') AS V FROM Messages");
        Assert.Equal("SYN-CAPTURED", result.ReadRow(0).Cells[2].Text); Assert.True(result.IsCurrent);
        doc.ReplaceWorkingText(Header + "PID|1||SYN-LATER\r"); await doc.EditRefresh; Assert.False(result.IsCurrent);
        Assert.True(documents.Close(doc.Id, discardChanges: true)); await doc.Disposal;
        var output = await result.OpenMatchingMessagesAsync(documents, Token);
        Assert.NotNull(output); Assert.Contains("SYN-CAPTURED", Encoding.UTF8.GetString(Bytes(output)), StringComparison.Ordinal);
        Assert.DoesNotContain("SYN-LATER", Encoding.UTF8.GetString(Bytes(output)), StringComparison.Ordinal);
    }
    [Fact]
    public async Task CsvExportsEveryRowWithTypesQuotingUnicodeFormulaProtectionAndNoPreviewTruncation()
    {
        await Open(Header + "PID|1||SYN\r"); var longText = "λ,\"quoted\"\r\n" + new string('S', 2000);
        using var result = await Run("SELECT @text AS Text,NULL AS N,'' AS E,'=SUM(1)' AS Formula FROM Messages", parameters: [new("@text", QueryCell.From(longText))]);
        var page = await result.PageAsync(0, Token); Assert.True(page[0].Cells[0].Abbreviated);
        Directory.CreateDirectory(root); var path = Path.Combine(root, "synthetic.csv"); await result.ExportCsvAsync(path, Token);
        var text = await File.ReadAllTextAsync(path, Token);
        Assert.Contains("λ,\"\"quoted\"\"\r\n" + new string('S', 2000), text, StringComparison.Ordinal);
        Assert.Contains("\"\",\"Null\",\"\",\"Text\"", text, StringComparison.Ordinal); Assert.Contains("'=SUM(1)", text, StringComparison.Ordinal);
        Assert.False((await File.ReadAllBytesAsync(path, Token)).AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
    }
    [Fact]
    public async Task ExportCannotOverwriteSourcesAndCanceledRunsReleaseSnapshotsWithoutPublishing()
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "source-synthetic.hl7"); await File.WriteAllTextAsync(path, Header + "PID|1||SYN\r", Token);
        await using var input = File.OpenRead(path); var doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic.hl7", input, Token);
        using var result = await Run("SELECT MessageControlId FROM Messages");
        await Assert.ThrowsAsync<IOException>(() => result.ExportCsvAsync(awaitableDestination(), Token));
        FileSaveDestination awaitableDestination() => FileSaveDestination.NewFile(path);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SqlQuery(documents, Options).RunAsync("SELECT * FROM Messages", token: cancel.Token));
        Assert.Single(documents.Documents); Assert.False(doc.IsDirty);
        Assert.DoesNotContain("SYN-DUP", string.Join("", documents.Activity.Snapshot()), StringComparison.Ordinal);
    }
    [Fact]
    public async Task ThousandsOfMessagesCanAggregateHeadersWithoutImportingObxValues()
    {
        await Open(string.Concat(Enumerable.Range(0, 1500).Select(n => Header.Replace("ADT^A01", n % 2 == 0 ? "ADT^A01" : "ADT^A03", StringComparison.Ordinal) +
            "PID|1||SYN-" + n + "\rOBX|1|ST|SYN||Alpha\rOBX|2|ST|SYN||Beta\r")));
        using var result = await Run("SELECT TriggerEvent,count(*) FROM Messages GROUP BY TriggerEvent ORDER BY TriggerEvent");
        Assert.Equal(2, result.RowCount); Assert.Equal(750, result.ReadRow(0).Cells[1].Integer); Assert.Equal(750, result.ReadRow(1).Cells[1].Integer);
        Assert.Equal(0, result.Metrics.BodyDecodedCharacters); Assert.Equal(1501, result.Metrics.ImportedRows);
    }
    [Fact]
    public async Task ExtractionAndCsvIncludeAllResultPagesRatherThanOnlyTheVisiblePage()
    {
        var doc = await Open(string.Concat(Enumerable.Range(0, 130).Select(n => Header + "PID|1||SYN-" + n + "\r")));
        using var result = await Run("SELECT DocumentId,MessageId FROM Messages ORDER BY MessageNumber DESC");
        Assert.Equal(100, (await result.PageAsync(0, Token)).Count);
        Assert.Equal(30, (await result.PageAsync(1, Token)).Count);
        var output = await result.OpenMatchingMessagesAsync(documents, Token);
        Assert.NotNull(output); Assert.Equal(130, output.MessageCount); Assert.Equal(Bytes(doc), Bytes(output));
        var csv = Path.Combine(root, "all-pages.csv"); await result.ExportCsvAsync(csv, Token);
        Assert.Equal(131, (await File.ReadAllLinesAsync(csv, Token)).Length);
    }
    private sealed class InlineProgress(Action<SqlQueryProgress> report) : IProgress<SqlQueryProgress>
    { public void Report(SqlQueryProgress value) => report(value); }
    [Theory]
    [InlineData("Capturing")] [InlineData("Indexing")]
    public async Task CancellationDuringCaptureOrIndexingReleasesOwnedStorage(string phase)
    {
        await Open(string.Concat(Enumerable.Range(0, 100).Select(_ => Header + "PID|1||SYN\rOBX|1|ST|SYN||Value\r")));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var progress = new InlineProgress(p => { if (p.Phase == phase) cancel.Cancel(); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SqlQuery(documents, Options).RunAsync(
            "SELECT SegmentName FROM Segments", progress: progress, token: cancel.Token));
        Assert.Empty(Directory.EnumerateFiles(Options.StorageRoot!, "*", SearchOption.AllDirectories));
        Assert.Single(documents.Documents); Assert.False(documents.Documents[0].IsDirty);
    }
    [Fact]
    public async Task CaptureOutputAndCsvLimitsFailWithoutPublishingAndDisposalRemovesJournals()
    {
        await Open(Header + "PID|1||SYN\r");
        Assert.Equal(QueryError.ImportLimit, (await Assert.ThrowsAsync<QueryException>(() => Run("SELECT * FROM Messages",
            options: Options with { MaximumSourceBytes = 16 }))).Code);
        using (var result = await Run("SELECT DocumentId,MessageId FROM Messages", options: Options with { MaximumOutputBytes = 16, MaximumCsvBytes = 16 }))
        {
            Assert.Equal(QueryError.ResultLimit, (await Assert.ThrowsAsync<QueryException>(() => result.OpenMatchingMessagesAsync(documents, Token))).Code);
            var path = Path.Combine(root, "limited.csv");
            Assert.Equal(QueryError.ResultLimit, (await Assert.ThrowsAsync<QueryException>(() => result.ExportCsvAsync(path, Token))).Code);
            Assert.False(File.Exists(path));
        }
        Assert.Empty(Directory.EnumerateFiles(Options.StorageRoot!, "*", SearchOption.AllDirectories)); Assert.Single(documents.Documents);
    }
    [Fact]
    public async Task InvalidBodyBytesDoNotPreventHeaderQueriesOrExactExtraction()
    {
        var bytes = Encoding.UTF8.GetBytes(Header + "OBX|1|ST|SYN||").Concat(new byte[] { 0xff, 0xfc, 13 }).ToArray();
        using var input = new MemoryStream(bytes); var doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic-invalid.hl7", input, Token);
        using var result = await Run("SELECT DocumentId,MessageId,MessageType,HL7_STATUS(DocumentId,MessageId,'OBX-5') AS S FROM Messages");
        Assert.Equal("ADT", result.ReadRow(0).Cells[2].Text); Assert.Equal("Unreadable", result.ReadRow(0).Cells[3].Text);
        var output = await result.OpenMatchingMessagesAsync(documents, Token); Assert.NotNull(output); Assert.Equal(bytes, Bytes(output)); Assert.Equal(bytes, Bytes(doc));
        Assert.Equal(QueryError.SourceUnavailable, (await Assert.ThrowsAsync<QueryException>(() => Run("SELECT HL7_VALUE(DocumentId,MessageId,'OBX-5') FROM Messages"))).Code);
    }
    [Fact]
    public async Task CurrentDocumentFunctionOverloadIsOnlyAvailableForOnePinnedDocument()
    {
        var first = await Open(Header + "PID|1||SYN-FIRST\r");
        using var result = await Run("SELECT HL7_VALUE(MessageId,'PID-3') FROM Messages"); Assert.Equal("SYN-FIRST", result.ReadRow(0).Cells[0].Text);
        var second = await Open(Header + "PID|1||SYN-SECOND\r");
        Assert.Equal(QueryError.InvalidSql, (await Assert.ThrowsAsync<QueryException>(() => Run("SELECT HL7_VALUE(MessageId,'PID-3') FROM Messages", [first, second]))).Code);
        Assert.Equal(QueryError.SourceUnavailable, (await Assert.ThrowsAsync<QueryException>(() => Run("SELECT HL7_VALUE('unknown-source',1,'PID-3')"))).Code);
    }
    [Fact]
    public async Task VirtualMetadataConstraintsRespectCollationsAndExplicitEmptyNullStates()
    {
        await Open(Header + "PID||\"\"|SYN\r");
        using var result = await Run("SELECT FieldNumber,RawValue,DecodedValue,ValueState,ValueStatus FROM Fields WHERE SegmentName='pid' COLLATE NOCASE ORDER BY FieldNumber");
        Assert.Equal(3, result.RowCount); Assert.Equal("", result.ReadRow(0).Cells[1].Text); Assert.Equal("Empty", result.ReadRow(0).Cells[3].Text);
        Assert.Equal("\"\"", result.ReadRow(1).Cells[1].Text); Assert.Equal(QueryValueKind.Null, result.ReadRow(1).Cells[2].Kind);
        Assert.Equal("ExplicitNull", result.ReadRow(1).Cells[3].Text); Assert.Equal("Complete", result.ReadRow(1).Cells[4].Text);
    }
    [Fact]
    public async Task SortingUsesCompleteJournalTypedKeysStableOrderAndValidatedCsvPermutation()
    {
        await Open(Header + "PID|1||SYN\r");
        using var result = await Run("SELECT NULL AS V UNION ALL SELECT 9007199254740993 UNION ALL SELECT 9007199254740992.0 UNION ALL SELECT '' UNION ALL SELECT 'Zulu' UNION ALL SELECT 'Alpha' UNION ALL SELECT X'01' UNION ALL SELECT 'Alpha'");
        var order = await result.SortOrderAsync(0, false, Token);
        Assert.Equal(new[] { 0, 2, 1, 3, 5, 7, 4, 6 }, order);
        var page = await result.OrderedPageAsync(0, order, Token); Assert.Equal(new[] { 1, 3, 2, 4, 6, 8, 5, 7 }, page.Select(r => r.RowNumber));
        Assert.Equal(new[] { 6, 4, 5, 7, 3, 1, 2, 0 }, await result.SortOrderAsync(0, true, Token));
        var path = Path.Combine(root, "sorted.csv"); await result.ExportCsvAsync(FileSaveDestination.NewFile(path), order, Token);
        var csv = await File.ReadAllTextAsync(path, Token); Assert.True(csv.IndexOf("9007199254740992", StringComparison.Ordinal) < csv.IndexOf("9007199254740993", StringComparison.Ordinal));
        var rejected = Path.Combine(root, "partial.csv");
        await Assert.ThrowsAsync<QueryException>(() => result.ExportCsvAsync(FileSaveDestination.NewFile(rejected), new[] { 0 }, Token));
        await Assert.ThrowsAsync<QueryException>(() => result.ExportCsvAsync(FileSaveDestination.NewFile(rejected), Enumerable.Repeat(0, 8).ToArray(), Token));
        Assert.False(File.Exists(rejected)); using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result.SortOrderAsync(0, false, cancel.Token));
    }
    [Fact]
    public async Task SourceResolutionUsesTypedJournalIdentityAndRejectsComputedAliases()
    {
        var doc = await Open(Header + "PID|1||SYN\r" + Header + "PID|1||SYN\r");
        using var result = await Run("SELECT DocumentId,MessageId FROM Messages ORDER BY MessageNumber DESC");
        var identity = result.SourceForRow(0); Assert.NotNull(identity); Assert.Equal(doc.Id, identity.DocumentId);
        Assert.Equal(doc.GetMessageId(2).Value, identity.MessageId); Assert.Equal(2, identity.MessageNumber);
        using var computed = await Run("SELECT DocumentId,MessageNumber AS MessageId FROM Messages"); Assert.Null(computed.SourceForRow(0));
    }
    public async ValueTask DisposeAsync()
    {
        foreach (var result in results) result.Dispose(); var pending = documents.Documents.ToArray(); documents.Dispose();
        await Task.WhenAll(pending.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
