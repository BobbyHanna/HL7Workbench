using System.Text;
using System.Runtime.InteropServices;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class FieldExtractionTests : IAsyncDisposable
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20300101120000||ADT^A01|SYN-CONTROL|T|2.5.1\r";
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-extraction-" + Guid.NewGuid().ToString("N"));
    private readonly DocumentManager documents = new();
    private readonly List<ExtractionResults> results = [];
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private ExtractionOptions Options => new() { StorageRoot = Path.Combine(root, "results") };
    private async Task<MessageDocument> Open(string text, Encoding? encoding = null)
    {
        Directory.CreateDirectory(root); using var input = new MemoryStream((encoding ?? new UTF8Encoding(false)).GetBytes(text));
        return await MessageFileReader.OpenFileAsync(documents, "Synthetic.hl7", input, Token);
    }
    private async Task<ExtractionResults> Run(MessageDocument document, ExtractionConfiguration? config = null, ExtractionOptions? options = null, ExtractionScope scope = ExtractionScope.WholeDocument)
    {
        var result = await new FieldExtraction(options ?? Options).RunAsync(document, config ?? ExtractionLibrary.InterfaceOverview.Configuration, scope, token: Token);
        results.Add(result); return result;
    }
    private static byte[] Bytes(MessageDocument doc) => doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length), int.MaxValue);
    [Theory]
    [InlineData(ExtractionScope.CurrentMessage)] [InlineData(ExtractionScope.WholeDocument)]
    public async Task OverviewPreservesSourceAndUsesCapturedLogicalMessageIdentity(ExtractionScope scope)
    {
        var text = Header + "PID|1||SYN-ONE||Example^Ada\rPV1|||||||||||||||||||SYN-VISIT\rOBR|1|||SYN-SERVICE^Test\r";
        var doc = await Open(text + text.Replace("SYN-ONE", "SYN-TWO", StringComparison.Ordinal)); await doc.NavigateAsync(2, Token);
        var before = Bytes(doc); var revision = doc.SearchRevision; var id = doc.ActiveMessageId;
        var output = await Run(doc, scope: scope); Assert.Equal(scope == ExtractionScope.CurrentMessage ? 1 : 2, output.RowCount);
        Assert.Equal(revision, output.Revision); Assert.True(output.IsCurrent); Assert.Equal(doc.Id, output.DocumentId);
        var row = output.ReadRow(scope == ExtractionScope.CurrentMessage ? 0 : 1);
        Assert.Equal(id!.Value.Value, row.MessageKey); Assert.Equal(2, row.CapturedPosition);
        Assert.Equal(new[] { "ADT^A01", "SYN-CONTROL", "SYN-TWO", "Example^Ada", "SYN-VISIT", "SYN-SERVICE^Test" }, row.Values.Select(v => v.Text));
        Assert.Equal(before, Bytes(doc)); Assert.False(doc.IsDirty); Assert.False(doc.CanUndo); Assert.Null(doc.SavePath);
        Assert.Equal(revision, doc.SearchRevision); Assert.Equal(2, doc.MessageNumber);
    }
    [Fact]
    public async Task ExpansionZipsIndependentMatchesBroadcastsScalarsAndMarksUnalignedCells()
    {
        var doc = await Open(Header + "PID|1||A~B~C||Alpha~Beta\rPID|2||D||Delta\r");
        var config = new ExtractionConfiguration([new("Control", "MSH-10"), new("Ids", "PID-3"), new("Names", "PID-5"), new("Fixed", "PID[1]-3[1]")], ExtractionMode.ExpandMatches);
        var output = await Run(doc, config); Assert.Equal(4, output.RowCount);
        Assert.Equal(new[] { "A", "B", "C", "D" }, Enumerable.Range(0, 4).Select(n => output.ReadRow(n).Values[1].Text));
        Assert.Equal(new[] { "Alpha", "Beta", "Delta", "" }, Enumerable.Range(0, 4).Select(n => output.ReadRow(n).Values[2].Text));
        Assert.Equal(Hl7MissingKind.Alignment, output.ReadRow(3).Values[2].Missing);
        Assert.All(Enumerable.Range(0, 4), n => { Assert.Equal("SYN-CONTROL", output.ReadRow(n).Values[0].Text); Assert.Equal("A", output.ReadRow(n).Values[3].Text); });
        Assert.Equal("PID[2]-3[1]", output.ReadRow(3).Values[1].Path); Assert.False(output.Summary.Truncated);
        Assert.Equal(1, output.Summary.Columns[2].Missing);
    }
    [Theory]
    [InlineData("rows")] [InlineData("expansion")] [InlineData("storage")] [InlineData("values")]
    public async Task LimitsAreExplicitAndNeverPublishPreviewStringsAsValues(string limit)
    {
        var doc = await Open(Header + "PID|A~B~C~D\r" + Header + "PID|E~F~G~H\r");
        var options = limit switch
        {
            "rows" => Options with { MaximumRows = 3 }, "expansion" => Options with { MaximumRowsPerMessage = 2 },
            "storage" => Options with { MaximumJournalBytes = 1 }, _ => Options with { MaximumValueCharacters = 1 }
        };
        var output = await Run(doc, new([new("Value", "PID-1"), new("Control", "MSH-10")], ExtractionMode.ExpandMatches), options);
        if (limit == "rows") { Assert.True(output.Summary.Truncated); Assert.Equal(3, output.RowCount); Assert.Equal("Result row limit", output.Summary.Limit); }
        if (limit == "expansion") { Assert.True(output.Summary.Truncated); Assert.Equal(4, output.RowCount); Assert.Equal(2, output.Summary.ExpansionLimitedMessages); }
        if (limit == "storage") { Assert.True(output.Summary.Truncated); Assert.Equal(0, output.RowCount); }
        if (limit == "values") { Assert.Equal(Hl7ExtractionStatus.ValueLimit, output.ReadRow(0).Values[1].Status); Assert.Equal("", output.ReadRow(0).Values[1].Text); }
        Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task ValueStatesAndRawDecodedCustomSeparatorsSurviveTheJournalAndSummary()
    {
        var doc = await Open("MSH*$%!?*SYN\rPID**\"\"*A?B$MR%Second$MR**Name!S!Literal$Given\r");
        var config = new ExtractionConfiguration([new("Empty", "PID-1"), new("Null", "PID-2"), new("Missing", "NK1-1"), new("Sub", "PID-3.1.2"), new("Name", "PID-5.1")]);
        var output = await Run(doc, config); var row = output.ReadRow(0);
        Assert.Equal(new[] { Hl7ValueState.Empty, Hl7ValueState.ExplicitNull, Hl7ValueState.Missing }, row.Values.Take(3).Select(v => v.State));
        Assert.Equal("B", row.Values[3].Text); Assert.Equal("Name$Literal", row.Values[4].Text);
        Assert.Equal(1, output.Summary.Columns[0].Empty); Assert.Equal(1, output.Summary.Columns[1].ExplicitNull); Assert.Equal(1, output.Summary.Columns[2].Missing);
        var raw = await Run(doc, config with { DecodeEscapes = false }); Assert.Equal("Name!S!Literal", raw.ReadRow(0).Values[4].Text);
    }
    [Fact]
    public async Task MissingEmptyNullNumericAndTextFiltersSortOnlyTheStoredResultValues()
    {
        var text = new[] { "10", "2", "", "\"\"", "Alpha", "20" }.Select(v => Header + "PID|" + v + "\r");
        var doc = await Open(string.Concat(text) + Header + "NK1|1\r");
        var output = await Run(doc, new([new("Value", "PID-1")]));
        foreach (var op in new[] { ExtractionFilterOperator.Missing, ExtractionFilterOperator.Empty, ExtractionFilterOperator.ExplicitNull })
            Assert.Equal(1, (await output.CreateViewAsync(new(0, op), token: Token)).Count);
        Assert.Equal(1, (await output.CreateViewAsync(new(0, ExtractionFilterOperator.Contains, "ALP"), token: Token)).Count);
        Assert.Equal(1, (await output.CreateViewAsync(new(0, ExtractionFilterOperator.Equals, "alpha"), token: Token)).Count);
        var selected = await output.CreateViewAsync(new(0, ExtractionFilterOperator.NumericGreaterThan, "2"), new(0, true, true), Token);
        Assert.Equal(new[] { "20", "10" }, (await selected.PageAsync(0, Token)).Select(r => r.Values[0].Text));
        var sorted = await output.CreateViewAsync(sort: new(0, Numeric: true), token: Token);
        Assert.Equal(new[] { "", "", "", "2", "10", "20", "Alpha" }, (await sorted.PageAsync(0, Token)).Select(r => r.Values[0].Text));
        await Assert.ThrowsAsync<ArgumentException>(() => output.CreateViewAsync(new(0, ExtractionFilterOperator.NumericGreaterThan, "NaN"), token: Token));
        Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task CsvExportsAllFilteredSortedPagesWithStateFidelityFormulaProtectionAndCompleteValues()
    {
        var message = Hl7Parser.Parse(Header + "PID|1\r"); message.SetRaw("PID-1", " =1,\"quoted\"\\X0D0A\\" + new string('x', 400));
        message.SetNull("PID-2"); message.SetRaw("PID-3", "");
        var doc = await Open(string.Concat(Enumerable.Repeat(message.RawText, 205)));
        var output = await Run(doc, new([new("Value", "PID-1"), new("Null", "PID-2"), new("Absent", "NK1-1"), new("Empty", "PID-3")]));
        var page = await output.AllRows.PageAsync(0, Token); Assert.Equal(100, page.Count); Assert.True(page[0].Values[0].Abbreviated);
        var path = Path.Combine(root, "rows.csv"); await output.AllRows.ExportAsync(path, token: Token);
        var bytes = await File.ReadAllBytesAsync(path, Token); var csv = Encoding.UTF8.GetString(bytes);
        Assert.False(bytes.AsSpan().StartsWith(Encoding.UTF8.Preamble)); Assert.Equal(206, Csv(csv).Count);
        var data = Csv(csv); Assert.Equal("' =1,\"quoted\"\r\n" + new string('x', 400), data[1][5]);
        Assert.Equal("Value", data[1][6]); Assert.Equal("ExplicitNull", data[1][10]); Assert.Equal("MissingSegment", data[1][14]); Assert.Equal("Empty", data[1][18]);
        Assert.DoesNotContain("[preview]", csv);
        var selected = await output.CreateViewAsync(new(0, ExtractionFilterOperator.Contains, "quoted"), new(0, true), Token);
        var filtered = Path.Combine(root, "selected.csv"); await selected.ExportAsync(filtered, token: Token); Assert.Equal(206, Csv(await File.ReadAllTextAsync(filtered, Token)).Count);
        Assert.Equal(output.ReadRow(0).MessageKey.ToString(System.Globalization.CultureInfo.InvariantCulture), data[1][3]);
    }
    [Fact]
    public async Task EditsTabChangesInsertionsClosureAndNavigationNeverRetargetCapturedRows()
    {
        var source = await Open(Header + "PID|Old\r" + Header + "PID|Second\r");
        using var session = new FieldExtractionSession(documents, Options);
        await session.RunAsync(new([new("Value", "PID-1")]), ExtractionScope.WholeDocument, Token);
        var original = session.Page[1]; var originalId = source.GetMessageId(2);
        var other = await Open(Header + "PID|Other\r"); Assert.Same(source, session.Source);
        source.IsEditMode = true; source.InsertMessage(MessageInsertionPosition.Beginning, Header + "PID|Inserted\r"); await source.EditRefresh;
        Assert.False(session.Results!.IsCurrent); Assert.Contains("Stale", session.Results.SourceState);
        session.NavigationRequested += documents.Activate; await session.NavigateAsync(original, Token);
        Assert.Same(source, documents.ActiveDocument); Assert.Equal(originalId, source.ActiveMessageId); Assert.Equal(3, source.MessageNumber);
        Assert.Equal("Second", original.Values[0].Text); Assert.False(other.IsDirty);
        Assert.True(documents.Close(source.Id, discardChanges: true)); await source.Disposal;
        Assert.False(session.Results.SourceIsOpen); Assert.Contains("closed", session.Results.SourceState);
        await session.NavigateAsync(original, Token); Assert.Same(other, documents.ActiveDocument);
        await session.Results.AllRows.ExportAsync(Path.Combine(root, "closed-source.csv"), token: Token);
    }
    private sealed class Progress(Action<ExtractionProgress> report) : IProgress<ExtractionProgress>
    { public void Report(ExtractionProgress value) => report(value); }
    [Fact]
    public async Task CancellationSourceChangeAndCloseDrainCapturesAndDiscardPartialJournals()
    {
        var doc = await Open(string.Concat(Enumerable.Repeat(Header + "PID|SYN\r", 200)));
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Token); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new FieldExtraction(Options).RunAsync(doc, ExtractionLibrary.InterfaceOverview.Configuration, token: canceled.Token));
        var reached = new TaskCompletionSource(); using var release = new ManualResetEventSlim();
        var pending = new FieldExtraction(Options).RunAsync(doc, ExtractionLibrary.InterfaceOverview.Configuration, progress: new Progress(_ => { reached.TrySetResult(); release.Wait(Token); }), token: Token);
        await reached.Task.WaitAsync(Token); doc.IsEditMode = true; doc.ReplaceWorkingText(doc.EditableText + "NTE|1||SYN-LATER\r"); release.Set();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending); await doc.EditRefresh;
        Assert.True(doc.IsDirty); Assert.Empty(Directory.GetDirectories(Options.StorageRoot!));
        Assert.True(documents.Close(doc.Id, discardChanges: true)); await doc.Disposal;
    }
    [Fact]
    public async Task CanceledAndFailedExportsLeaveNoPartialFileAndSourceAliasesAreRefused()
    {
        var file = Path.Combine(root, "source.hl7"); Directory.CreateDirectory(root); await File.WriteAllTextAsync(file, Header + "PID|SYN\r", Token);
        MessageDocument doc; using (var input = File.OpenRead(file)) doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic.hl7", input, Token, localPath: file);
        var output = await Run(doc); var original = await File.ReadAllBytesAsync(file, Token);
        await Assert.ThrowsAnyAsync<IOException>(() => output.AllRows.ExportAsync(FileSaveDestination.NewFile(file), token: Token));
        var consent = await FileSaveDestination.CaptureAsync(file, Token);
        await Assert.ThrowsAnyAsync<IOException>(() => output.AllRows.ExportAsync(consent, token: Token)); Assert.Equal(original, await File.ReadAllBytesAsync(file, Token));
        var path = Path.Combine(root, "canceled.csv"); using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var progress = new ExportProgress(_ => canceled.Cancel());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => output.AllRows.ExportAsync(path, progress, canceled.Token)); Assert.False(File.Exists(path));
        var limited = await Run(doc, options: Options with { MaximumExportBytes = 1 });
        await Assert.ThrowsAnyAsync<IOException>(() => limited.AllRows.ExportAsync(Path.Combine(root, "limited.csv"), token: Token));
        Assert.False(File.Exists(Path.Combine(root, "limited.csv"))); Assert.Empty(Directory.GetFiles(root, ".hl7-save-*.tmp"));
    }
    private sealed class ExportProgress(Action<long> report) : IProgress<long> { public void Report(long value) => report(value); }
    [Fact]
    public async Task CanceledAndInvalidRerunsRetainThePreviousCompletedResultAndReleasePendingStorage()
    {
        var doc = await Open(string.Concat(Enumerable.Repeat(Header + "PID|SYN\r", 250)));
        using var session = new FieldExtractionSession(documents, Options);
        await session.RunAsync(new([new("Value", "PID-1")]), ExtractionScope.WholeDocument, Token);
        var previous = session.Results; var pending = session.RunAsync(new([new("Value", "PID-1")]), ExtractionScope.WholeDocument, Token);
        session.Cancel(); await pending; Assert.True(session.LastOperationCanceled); Assert.Same(previous, session.Results);
        Assert.Equal(250, session.View!.Count); Assert.Single(Directory.GetDirectories(Options.StorageRoot!));
        await session.RunAsync(new([new("Bad", "NOT-A-PATH")]), ExtractionScope.WholeDocument, Token);
        Assert.Same(previous, session.Results); Assert.Contains("unavailable", session.Status);
        using var canceled = CancellationTokenSource.CreateLinkedTokenSource(Token); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => previous!.CreateViewAsync(sort: new(0), token: canceled.Token));
        Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task ClosingAndReplacingATabWithTheSameDocumentIdCannotRetargetOldRows()
    {
        var source = await Open(Header + "PID|SYN-ORIGINAL\r"); using var session = new FieldExtractionSession(documents, Options);
        await session.RunAsync(new([new("Value", "PID-1")]), ExtractionScope.CurrentMessage, Token); var old = session.Page[0];
        Assert.True(documents.Close(source.Id)); await source.Disposal;
        using var replacement = new MessageDocument(source.Id, "Synthetic replacement", Hl7Parser.Parse(Header + "PID|SYN-REPLACED\r")); documents.Open(replacement);
        var navigations = 0; session.NavigationRequested += _ => navigations++;
        await session.NavigateAsync(old, Token); Assert.Equal(0, navigations); Assert.Same(replacement, documents.ActiveDocument);
        Assert.False(session.Results!.SourceIsOpen); Assert.Equal("SYN-ORIGINAL", session.Page[0].Values[0].Text);
        session.UseActiveDocument(); await session.RunAsync(new([new("Value", "PID-1")]), ExtractionScope.CurrentMessage, Token);
        Assert.Equal("SYN-REPLACED", session.Page[0].Values[0].Text); Assert.NotEqual(old.ResultId, session.Results!.Id);
    }
    [Fact]
    public async Task UnreadableMessageProducesAnExplicitUnavailableRowWithoutPretendingFieldsAreAbsent()
    {
        var bytes = Encoding.UTF8.GetBytes(Header + "PID|SYN-").Concat(new byte[] { 0xFF, 13 }).ToArray();
        using var input = new MemoryStream(bytes); var doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic malformed.hl7", input, Token);
        var result = await Run(doc, new([new("Value", "PID-1")]));
        Assert.Equal(1, result.RowCount); Assert.Equal(1, result.Summary.UnreadableMessages);
        Assert.Equal(Hl7ExtractionStatus.Unreadable, result.ReadRow(0).Values[0].Status);
        Assert.Equal(bytes, Bytes(doc)); Assert.False(doc.IsDirty);
    }
    [Fact]
    public async Task PendingExportKeepsItsJournalLeaseUntilPublicationThenDisposalDeletesTheOwnedStorage()
    {
        var doc = await Open(Header + "PID|SYN\r"); var result = await Run(doc); var reached = new TaskCompletionSource(); using var release = new ManualResetEventSlim();
        var export = result.AllRows.ExportAsync(Path.Combine(root, "drained.csv"), new ExportProgress(_ => { reached.TrySetResult(); release.Wait(Token); }), Token);
        await reached.Task.WaitAsync(Token); result.Dispose(); Assert.Single(Directory.GetDirectories(Options.StorageRoot!));
        release.Set(); await export; Assert.Empty(Directory.GetDirectories(Options.StorageRoot!)); Assert.True(File.Exists(Path.Combine(root, "drained.csv")));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => result.AllRows.PageAsync(0, Token));
    }
    [Fact]
    public async Task WindowsHardLinkAndOwnedSourceStorageCannotBeOverwrittenByCsv()
    {
        if (!OperatingSystem.IsWindows()) return;
        Directory.CreateDirectory(root); var file = Path.Combine(root, "source.hl7"); await File.WriteAllTextAsync(file, Header + "PID|SYN\r", Token);
        MessageDocument doc; using (var input = File.OpenRead(file)) doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic.hl7", input, Token, localPath: file);
        var result = await Run(doc); var alias = Path.Combine(root, "source-alias.csv"); Assert.True(CreateHardLink(alias, file, IntPtr.Zero));
        var destination = await FileSaveDestination.CaptureAsync(alias, Token);
        await Assert.ThrowsAsync<IOException>(() => result.AllRows.ExportAsync(destination, token: Token));
        await Assert.ThrowsAsync<IOException>(() => result.AllRows.ExportAsync(Path.Combine(doc.Snapshot!.DirectoryPath, "other.csv"), token: Token));
        Assert.Equal(await File.ReadAllBytesAsync(file, Token), await File.ReadAllBytesAsync(alias, Token));
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateHardLink(string name, string existing, IntPtr security);
    [Fact]
    public async Task PreviewPagingAndDistinctCountsAreBoundedAndLiteralStringificationIsSafe()
    {
        var doc = await Open(string.Concat(Enumerable.Range(0, 230).Select(n => Header + "PID|" + n.ToString(System.Globalization.CultureInfo.InvariantCulture) + "\r")));
        var output = await Run(doc, new([new("Value", "PID-1")]), Options with { DistinctLimit = 2 });
        Assert.Equal(3, output.AllRows.PageCount); Assert.Equal(30, (await output.AllRows.PageAsync(2, Token)).Count);
        Assert.Equal(2, output.Summary.Columns[0].DistinctValues); Assert.True(output.Summary.Columns[0].DistinctLowerBound);
        Assert.DoesNotContain("SYN-SECRET", new ExtractionFilter(0, ExtractionFilterOperator.Equals, "SYN-SECRET").ToString());
        Assert.DoesNotContain("SYN-SECRET", new Hl7ExtractedValue(Hl7ValueState.Value, Hl7MissingKind.None, Hl7ExtractionStatus.Complete, "SYN-SECRET", "PID-1").ToString());
    }
    [Fact]
    public async Task LargeSourceAndHighRowCountsRetainOnlyBoundedValuesMetadataAndPages()
    {
        var stress = Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") == "1";
        var count = stress ? 25_000 : 1200; var body = Header + "PID|" + new string('x', 512) + "\r";
        var doc = await Open(string.Concat(Enumerable.Repeat(body, count))); var before = GC.GetTotalMemory(true);
        var output = await Run(doc, new([new("Value", "PID-1")])); var retained = GC.GetTotalMemory(true) - before;
        Assert.Equal(count, output.RowCount); Assert.Equal(100, (await output.AllRows.PageAsync(0, Token)).Count);
        Assert.True(retained < 24L * 1024 * 1024, "Result values must live in the private journal, not a resident table.");
        var selected = await output.CreateViewAsync(new(0, ExtractionFilterOperator.Contains, "xxx"), new(0), Token); Assert.Equal(count, selected.Count);
        var huge = await Open(Header + "PID|KEEP\rOBX|1|ED|||" + new string('A', stress ? 16 * 1024 * 1024 : 512 * 1024) + "\r");
        var large = await Run(huge, new([new("Small", "PID-1"), new("Oversize", "OBX-5")]));
        Assert.Equal("KEEP", large.ReadRow(0).Values[0].Text); Assert.Equal(Hl7ExtractionStatus.ValueLimit, large.ReadRow(0).Values[1].Status);
        Assert.False(huge.IsDirty); GC.KeepAlive(output);
    }
    private static List<string[]> Csv(string text)
    {
        var rows = new List<string[]>(); var cells = new List<string>(); var cell = new StringBuilder(); var quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '"') { if (quoted && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; } else quoted = !quoted; }
            else if (!quoted && text[i] == ',') { cells.Add(cell.ToString()); cell.Clear(); }
            else if (!quoted && text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n') { cells.Add(cell.ToString()); cell.Clear(); rows.Add(cells.ToArray()); cells.Clear(); i++; }
            else cell.Append(text[i]);
        }
        Assert.False(quoted); Assert.Empty(cells); return rows;
    }
    public async ValueTask DisposeAsync()
    {
        foreach (var result in results) result.Dispose(); var pending = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(pending.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
