using System.Text;
using HL7Workbench.Querying;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class SqlHardeningTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-sql-hardening-" + Guid.NewGuid().ToString("N"));
    private readonly DocumentManager documents = new();
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private SqlQueryOptions Options => new() { StorageRoot = Path.Combine(root, "query") };
    private const string Header = "MSH|^~\\&|SYN|||||20300101||ADT^A01|SYN-DUP|T|2.5.1\r";
    private static string Message(int n) => Header + $"PID|1||SYN-{n:D4}\rOBX|1|ST|SYN||Value-{n:D4}\r";
    [Fact]
    public void EveryConfigurableWorkspaceBudgetRejectsValuesAboveItsHardCap()
    {
        SqlQueryOptions[] invalid = [Options with { MaximumDocuments = 17 }, Options with { MaximumSourceBytes = 1024L * 1024 * 1024 + 1 },
            Options with { MaximumOutputBytes = 1024L * 1024 * 1024 + 1 }, Options with { MaximumCsvBytes = 256L * 1024 * 1024 + 1 },
            Options with { MaximumMatchingMessages = 100_001 }, Options with { TotalTimeout = TimeSpan.FromMinutes(11) }];
        foreach (var option in invalid) Assert.Throws<ArgumentOutOfRangeException>(option.Validate);
        Options.Validate();
    }
    private async Task<MessageDocument> Open(string text, Encoding? encoding = null)
    {
        Directory.CreateDirectory(root); encoding ??= new UTF8Encoding(false, true);
        using var input = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        return await MessageFileReader.OpenFileAsync(documents, "Synthetic-only.hl7", input, Token);
    }
    [Fact]
    public async Task IndependentCoordinatesOracleIncludesMshNumberingRepeatedValuesAndMalformedRegions()
    {
        var source = await Open("MSH|^~\\&|SYN\rPID|1||SYN-A&LOCAL^MR~SYN-B^PI||Family^Given\rZXY|custom||\"\"\rOBX|1|ST|SYN||A\\F\\B\rOBX|2|ST|SYN||Bad\\Q\\Escape\rBAD malformed\r");
        using var values = await new SqlQuery(documents, Options).RunAsync("""
            SELECT HL7_RAW(MessageId,'MSH-1'),HL7_RAW(MessageId,'MSH-2'),
              HL7_VALUE(MessageId,'PID-3[2].1'),HL7_VALUE(MessageId,'PID-3.1.2'),
              HL7_VALUE(MessageId,'OBX-5'),HL7_STATE(MessageId,'ZXY-2'),
              HL7_STATE(MessageId,'ZXY-3'),HL7_STATUS(MessageId,'NK1-1'),HL7_STATUS(MessageId,'OBX[2]-5') FROM Messages
            """, token: Token);
        Assert.Equal(new[] { "|", "^~\\&", "SYN-B", "LOCAL", "A|B", "Empty", "ExplicitNull", "MissingSegment", "DecodeDiagnostic" }, values.ReadRow(0).Cells.Select(c => c.Text));
        using var segments = await new SqlQuery(documents, Options).RunAsync("SELECT SegmentName,SegmentOccurrence,SegmentOrdinal,Status FROM Segments ORDER BY SegmentOrdinal", token: Token);
        Assert.Equal(new[] { "MSH", "PID", "ZXY", "OBX", "OBX", "BAD" }, Enumerable.Range(0, segments.RowCount).Select(i => segments.ReadRow(i).Cells[0].Text));
        Assert.Equal(new long[] { 1, 1, 1, 1, 2, 1 }, Enumerable.Range(0, segments.RowCount).Select(i => segments.ReadRow(i).Cells[1].Integer));
        Assert.Equal("Unstructured", segments.ReadRow(5).Cells[3].Text);
        using var missing = await new SqlQuery(documents, Options).RunAsync("SELECT count(*) FROM Fields WHERE SegmentName='PID' AND FieldNumber=99", token: Token);
        Assert.Equal(0, missing.ReadRow(0).Cells[0].Integer); Assert.False(source.IsDirty);
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)]
    public async Task SortedPagedNoncontiguousExtractionUsesFixtureBytesAfterSourceAndUnrelatedDocumentsClose(int codePage)
    {
        var encoding = codePage switch { 1200 => new UnicodeEncoding(false, true, true), 1201 => new UnicodeEncoding(true, true, true), _ => (Encoding)new UTF8Encoding(true, true) };
        var messages = Enumerable.Range(1, 205).Select(n => Message(n).Replace("\r", n % 3 == 0 ? "\r\n" : n % 3 == 1 ? "\n" : "\r", StringComparison.Ordinal)).ToArray();
        var source = await Open(string.Concat(messages), encoding); var other = await Open(Message(999));
        using var result = await new SqlQuery(documents, Options).RunAsync("SELECT DocumentId,MessageId,MessageNumber FROM Messages WHERE MessageNumber%2=1 ORDER BY MessageNumber DESC", [source], token: Token);
        var order = await result.SortOrderAsync(2, false, Token); var final = await result.OrderedPageAsync(1, order, Token);
        Assert.Equal(3, final.Count);
        foreach (var row in final)
        {
            var number = (int)result.ReadRow(row.RowNumber - 1).Cells[2].Integer; var identity = result.SourceForRow(row.RowNumber - 1)!;
            Assert.Equal(source.Id, identity.DocumentId); Assert.Equal(number, identity.MessageNumber); Assert.Equal(source.GetMessageId(number).Value, identity.MessageId);
        }
        documents.Activate(other.Id); documents.Close(other.Id, true); await other.Disposal; documents.Close(source.Id, true); await source.Disposal;
        Assert.False(result.IsCurrent); var extracted = await result.OpenMatchingMessagesAsync(documents, Token); Assert.NotNull(extracted);
        var expected = encoding.GetPreamble().Concat(encoding.GetBytes(string.Concat(messages.Where((_, i) => i % 2 == 0)))).ToArray();
        Assert.Equal(expected, extracted.Snapshot!.ReadBytes(new(0, extracted.Snapshot.Length), int.MaxValue)); Assert.True(extracted.IsDirty); Assert.Null(extracted.SavePath);
    }
    [Theory]
    [InlineData("=1+1")] [InlineData("+SUM(1)")] [InlineData("-1+2")] [InlineData("@SYNTHETIC")]
    [InlineData(" \t=1")] [InlineData("\tplain")] [InlineData("λ,\"quoted\"\r\nline")]
    public async Task CsvIsCompleteSpreadsheetGuardedPresentationWithExplicitTypes(string text)
    {
        await Open(Message(1) + Message(2));
        using var result = await new SqlQuery(documents, Options).RunAsync("SELECT @text AS V,NULL AS N,'' AS E,-42 AS I FROM Messages", parameters: [new("@text", QueryCell.From(text))], token: Token);
        var expectedValue = text.StartsWith('λ') ? text : "'" + text;
        static string Quote(string value) => "\"" + value.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        var record = Quote(expectedValue) + ",\"Text\",\"\",\"Null\",\"\",\"Text\",\"'-42\",\"Integer\"\r\n";
        var expected = "\"V\",\"V [Type]\",\"N\",\"N [Type]\",\"E\",\"E [Type]\",\"I\",\"I [Type]\"\r\n" + record + record;
        var path = Path.Combine(root, "safe.csv"); await result.ExportCsvAsync(path, Token);
        Assert.Equal(Encoding.UTF8.GetBytes(expected), await File.ReadAllBytesAsync(path, Token));
        Assert.Equal(text, result.ReadRow(0).Cells[0].Text); Assert.Equal(-42, result.ReadRow(0).Cells[3].Integer);
    }
    [Theory]
    [InlineData("source", -1)] [InlineData("source", 0)] [InlineData("source", 1)]
    [InlineData("output", -1)] [InlineData("output", 0)] [InlineData("output", 1)]
    [InlineData("csv", -1)] [InlineData("csv", 0)] [InlineData("csv", 1)]
    [InlineData("import", -1)] [InlineData("import", 0)] [InlineData("import", 1)]
    [InlineData("matches", -1)] [InlineData("matches", 0)] [InlineData("matches", 1)]
    public async Task LoweredCaptureOutputCsvAndImportCapsHaveExactBoundaryAndRecover(string kind, int offset)
    {
        var fixture = Message(1) + Message(2); var source = await Open(fixture); var length = Encoding.UTF8.GetByteCount(fixture);
        var csv = "\"V\",\"V [Type]\"\r\n\"λ\",\"Text\"\r\n\"λ\",\"Text\"\r\n";
        var options = kind switch
        {
            "source" => Options with { MaximumSourceBytes = length + offset }, "output" => Options with { MaximumOutputBytes = length + offset },
            "csv" => Options with { MaximumCsvBytes = Encoding.UTF8.GetByteCount(csv) + offset },
            "matches" => Options with { MaximumMatchingMessages = 2 + offset }, _ => Options with { Budgets = new() { ImportedRows = 3 + offset } }
        };
        var query = new SqlQuery(documents, options);
        if (offset < 0 && kind is "source" or "import")
            Assert.Equal(QueryError.ImportLimit, (await Assert.ThrowsAsync<QueryException>(() => query.RunAsync("SELECT MessageNumber FROM Messages", token: Token))).Code);
        else
        {
            using var result = await query.RunAsync(kind == "csv" ? "SELECT 'λ' AS V FROM Messages" : "SELECT DocumentId,MessageId FROM Messages", token: Token);
            if (kind is "output" or "matches")
            {
                if (offset < 0) Assert.Equal(QueryError.ResultLimit, (await Assert.ThrowsAsync<QueryException>(() => result.OpenMatchingMessagesAsync(documents, Token))).Code);
                else { var output = await result.OpenMatchingMessagesAsync(documents, Token); Assert.NotNull(output); Assert.Equal(length, output.Snapshot!.Length); }
            }
            if (kind == "csv")
            {
                var path = Path.Combine(root, "bounded.csv");
                if (offset < 0) { Assert.Equal(QueryError.ResultLimit, (await Assert.ThrowsAsync<QueryException>(() => result.ExportCsvAsync(path, Token))).Code); Assert.False(File.Exists(path)); }
                else { await result.ExportCsvAsync(path, Token); Assert.Equal(csv, await File.ReadAllTextAsync(path, Token)); }
            }
        }
        using var recovery = await new SqlQuery(documents, Options).RunAsync("SELECT count(*) FROM Messages", [source], token: Token);
        Assert.Equal(2, recovery.ReadRow(0).Cells[0].Integer); Assert.False(source.IsDirty);
    }
    [Fact]
    public async Task SourceCaptureBarrierCancelsBeforeNextDocumentAndSupportsCleanRecovery()
    {
        var first = await Open(Message(1)); var second = await Open(Message(2));
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token); var entered = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new SqlQuery(documents, Options).RunAsync("SELECT count(*) FROM Messages", [first, second],
            progress: new Inline(p => { if (p.Phase == "Capturing" && p.Completed == 1) { entered = true; cancel.Cancel(); } }), token: cancel.Token));
        Assert.True(entered); Assert.Empty(Directory.EnumerateFiles(Options.StorageRoot!, "*", SearchOption.AllDirectories));
        using var recovery = await new SqlQuery(documents, Options).RunAsync("SELECT count(*) FROM Messages", [first, second], token: Token);
        Assert.Equal(2, recovery.ReadRow(0).Cells[0].Integer);
    }
    [Theory]
    [InlineData(15)] [InlineData(16)] [InlineData(17)]
    public async Task DocumentScopeBoundaryUsesAuthoritativeMembership(int count)
    {
        for (var i = 0; i < count; i++) await Open(Message(i));
        if (count > 16) Assert.Equal(QueryError.SourceUnavailable, (await Assert.ThrowsAsync<QueryException>(() => new SqlQuery(documents, Options).RunAsync("SELECT count(*) FROM Messages", documents.Documents.ToArray(), token: Token))).Code);
        else { using var result = await new SqlQuery(documents, Options).RunAsync("SELECT count(*) FROM Messages", documents.Documents.ToArray(), token: Token); Assert.Equal(count, result.ReadRow(0).Cells[0].Integer); }
    }
    private sealed class Inline(Action<SqlQueryProgress> report) : IProgress<SqlQueryProgress> { public void Report(SqlQueryProgress value) => report(value); }
    [Fact]
    public async Task CanceledConsumersNeverPublishAndDisposeRemovesAllOwnedQueryStorage()
    {
        await Open(string.Concat(Enumerable.Range(1, 120).Select(Message)));
        var result = await new SqlQuery(documents, Options).RunAsync("SELECT DocumentId,MessageId FROM Messages", token: Token);
        using var cancel = new CancellationTokenSource(); cancel.Cancel(); var csv = Path.Combine(root, "canceled.csv");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result.PageAsync(1, cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result.SortOrderAsync(0, false, cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result.ExportCsvAsync(csv, cancel.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => result.OpenMatchingMessagesAsync(documents, cancel.Token));
        Assert.Single(documents.Documents); Assert.False(File.Exists(csv)); result.Dispose();
        Assert.Throws<ObjectDisposedException>(() => result.ReadRow(0));
        Assert.Empty(Directory.EnumerateFiles(Options.StorageRoot!, "*", SearchOption.AllDirectories));
    }
    public async ValueTask DisposeAsync()
    {
        var pending = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(pending.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
