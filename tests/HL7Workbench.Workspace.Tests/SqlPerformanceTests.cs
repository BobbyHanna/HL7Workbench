using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Reflection;
using System.Text;
using System.Text.Json;
using HL7Workbench.Querying;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

/// <summary>Explicit Release measurements, kept outside ordinary CI. Only generated synthetic inputs enter artifacts.</summary>
public sealed class SqlPerformanceTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-sql-performance-" + Guid.NewGuid().ToString("N"));
    private readonly DocumentManager documents = new();
    private readonly List<object> observations = [];
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private SqlQueryOptions Options => new() { StorageRoot = Path.Combine(root, "query") };
    private static string Header(int n) => $"MSH|^~\\&|||||||ADT^{(n % 2 == 0 ? "A01" : "A04")}|SYN-DUP|T|2.5.1\r";
    private async Task<MessageDocument> Open(int count, int valueBytes = 0)
    {
        Directory.CreateDirectory(root); var path = Path.Combine(root, "synthetic-" + Guid.NewGuid().ToString("N") + ".hl7");
        await using (var output = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            for (var n = 1; n <= count; n++)
            {
                await output.WriteAsync(Encoding.UTF8.GetBytes(Header(n) + $"PID|1||SYN-{n:D6}\rOBX|1|ST|SYN||"), Token);
                if (valueBytes == 0) await output.WriteAsync(Encoding.UTF8.GetBytes($"VALUE-{n % 10}\r"), Token);
                else
                {
                    var block = Enumerable.Repeat((byte)'Q', Math.Min(valueBytes, 65536)).ToArray();
                    for (var offset = 0; offset < valueBytes; offset += block.Length) await output.WriteAsync(block.AsMemory(0, Math.Min(block.Length, valueBytes - offset)), Token);
                    await output.WriteAsync(new byte[] { 13 }, Token);
                }
            }
        }
        await using var input = File.OpenRead(path);
        return await MessageFileReader.OpenFileAsync(documents, "Synthetic performance.hl7", input, Token);
    }
    private async Task<SqlQueryResults?> Measure(string label, MessageDocument source, string sql, QueryError? expected = null)
    {
        var phases = new Dictionary<string, double>(); var watch = Stopwatch.StartNew(); long parentPeak = 0, workerPeak = 0;
        var existing = Process.GetProcessesByName("HL7Workbench.QueryHost").Select(p => { using (p) return p.Id; }).ToHashSet();
        using var sampling = new CancellationTokenSource();
        var monitor = Task.Run(async () =>
        {
            while (!sampling.IsCancellationRequested)
            {
                using var parent = Process.GetCurrentProcess(); parentPeak = Math.Max(parentPeak, parent.WorkingSet64);
                foreach (var process in Process.GetProcessesByName("HL7Workbench.QueryHost"))
                {
                    using (process)
                        try { if (!existing.Contains(process.Id)) workerPeak = Math.Max(workerPeak, process.WorkingSet64); }
                        catch (InvalidOperationException) { }
                }
                try { await Task.Delay(25, sampling.Token); } catch (OperationCanceledException) { }
            }
        }, CancellationToken.None);
        SqlQueryResults? result = null; QueryError? error = null;
        try
        {
            result = await new SqlQuery(documents, Options).RunAsync(sql, [source], progress: new Inline(p => phases.TryAdd(p.Phase, watch.Elapsed.TotalMilliseconds)), token: Token);
            Assert.Null(expected);
        }
        catch (QueryException failure) { error = failure.Code; Assert.Equal(expected, error); }
        finally
        {
            sampling.Cancel(); await monitor;
            observations.Add(new { label, elapsedMs = watch.Elapsed.TotalMilliseconds, phases, error, parentSampledPeakBytes = parentPeak,
                workerSampledPeakBytes = workerPeak, rows = result?.RowCount, sqlTransferMs = result?.ExecutionDuration.TotalMilliseconds,
                journalBytes = result is null ? 0 : ((SourceSnapshot)typeof(SqlQueryResults).GetField("journal", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(result)!).Length,
                metrics = result?.Metrics, ownedStorageBytes = Directory.Exists(Options.StorageRoot) ? Directory.EnumerateFiles(Options.StorageRoot!, "*", SearchOption.AllDirectories).Sum(p => new FileInfo(p).Length) : 0 });
        }
        return result;
    }
    [Theory(Explicit = true)]
    [InlineData(50)] [InlineData(4_000)] [InlineData(10_000)] [InlineData(100_000)]
    public async Task MessageAndStructuralWorkloadsHaveIndependentExpectedCounts(int count)
    {
        var setup = Stopwatch.StartNew(); var source = await Open(count); observations.Add(new { label = "Fixture/open", count, elapsedMs = setup.Elapsed.TotalMilliseconds });
        for (var run = 0; run < (count == 50 ? 3 : 1); run++)
        {
            using var grouped = await Measure($"{count}-metadata-group-{run}", source, "SELECT TriggerEvent,count(*) FROM Messages GROUP BY TriggerEvent ORDER BY TriggerEvent");
            Assert.Equal(2, grouped!.RowCount); Assert.Equal(count / 2, grouped.ReadRow(0).Cells[1].Integer); Assert.Equal("A01", grouped.ReadRow(0).Cells[0].Text);
            Assert.Equal(0, grouped.Metrics.BodyDecodedCharacters); Assert.Equal(0, grouped.Metrics.ValueRequests);
        }
        using var many = await Measure($"{count}-metadata-rows", source, "SELECT DocumentId,MessageId,MessageNumber FROM Messages ORDER BY MessageNumber DESC");
        Assert.Equal(count, many!.RowCount); Assert.Equal(count, many.ReadRow(0).Cells[2].Integer);
        var sort = Stopwatch.StartNew(); var order = await many.SortOrderAsync(2, false, Token); var page = await many.OrderedPageAsync((count - 1) / 100, order, Token);
        observations.Add(new { label = $"{count}-sort-final-page", elapsedMs = sort.Elapsed.TotalMilliseconds });
        Assert.Equal(count, many.SourceForRow(page[^1].RowNumber - 1)!.MessageNumber);
        if (count <= 10_000)
        {
            using var segments = await Measure($"{count}-segment-filter", source, "SELECT count(*) FROM Segments WHERE SegmentName='OBX'");
            Assert.Equal(count, segments!.ReadRow(0).Cells[0].Integer);
            using var fields = await Measure($"{count}-field-join", source, """
                SELECT m.MessageNumber,f.DecodedValue FROM Messages m JOIN Fields f
                ON m.DocumentId=f.DocumentId AND m.MessageId=f.MessageId
                WHERE f.SegmentName='PID' AND f.FieldNumber=3 ORDER BY m.MessageNumber
                """, count == 10_000 ? QueryError.ImportLimit : null);
            if (fields is not null)
            {
                Assert.Equal(count, fields.RowCount); Assert.Equal("SYN-000001", fields.ReadRow(0).Cells[1].Text);
                Assert.Equal($"SYN-{count:D6}", fields.ReadRow(count - 1).Cells[1].Text);
            }
            using var path = await Measure($"{count}-path-filter", source, "SELECT MessageNumber FROM Messages WHERE HL7_VALUE(MessageId,'PID-3')='SYN-000001'");
            Assert.Equal(1, path!.RowCount); Assert.Equal(1, path.ReadRow(0).Cells[0].Integer);
        }
        var export = Stopwatch.StartNew(); var csv = Path.Combine(root, "synthetic.csv"); await many.ExportCsvAsync(FileSaveDestination.NewFile(csv), order, Token);
        observations.Add(new { label = $"{count}-csv", elapsedMs = export.Elapsed.TotalMilliseconds, bytes = new FileInfo(csv).Length });
        if (count == 100_000)
        {
            var extract = Stopwatch.StartNew(); var output = await many.OpenMatchingMessagesAsync(documents, Token);
            Assert.NotNull(output); Assert.Equal(count, output.MessageCount); Assert.Equal(source.Snapshot!.Length, output.Snapshot!.Length);
            observations.Add(new { label = "100000-matched-extraction", elapsedMs = extract.Elapsed.TotalMilliseconds });
            var extra = await Open(1);
            Assert.Equal(QueryError.ImportLimit, (await Assert.ThrowsAsync<QueryException>(() => new SqlQuery(documents, Options).RunAsync("SELECT count(*) FROM Messages", [source, extra], token: Token))).Code);
        }
    }
    [Theory(Explicit = true)]
    [InlineData(1)] [InlineData(16)] [InlineData(64)]
    public async Task LargeValuesKeepMetadataLazyAndExtractionExact(int mib)
    {
        var source = await Open(1, mib * 1024 * 1024);
        using var metadata = await Measure($"{mib}MiB-metadata", source, "SELECT DocumentId,MessageId,MessageNumber FROM Messages");
        Assert.Equal(0, metadata!.Metrics.BodyDecodedCharacters); Assert.Equal(0, metadata.Metrics.ValueRequests);
        using var lengths = await Measure($"{mib}MiB-value-metadata", source, "SELECT ValueLength FROM Fields WHERE SegmentName='OBX' AND FieldNumber=5");
        Assert.Equal(mib * 1024L * 1024, lengths!.ReadRow(0).Cells[0].Integer); Assert.Equal(0, lengths.Metrics.ValueRequests);
        using var rejected = await Measure($"{mib}MiB-value-rejected", source, "SELECT DecodedValue FROM Fields WHERE SegmentName='OBX' AND FieldNumber=5", QueryError.MaterializationLimit);
        using var preview = await Measure($"{mib}MiB-substr-rejected", source, "SELECT substr(DecodedValue,1,32) FROM Fields WHERE SegmentName='OBX' AND FieldNumber=5", QueryError.MaterializationLimit);
        using var filter = await Measure($"{mib}MiB-filter-rejected", source, "SELECT MessageId FROM Fields WHERE SegmentName='OBX' AND FieldNumber=5 AND DecodedValue LIKE 'Q%'", QueryError.MaterializationLimit);
        var extraction = Stopwatch.StartNew(); var extracted = await metadata.OpenMatchingMessagesAsync(documents, Token);
        Assert.NotNull(extracted); Assert.Equal(source.Snapshot!.Length, extracted.Snapshot!.Length);
        var a = new byte[65536]; var b = new byte[65536];
        for (long offset = 0; offset < source.Snapshot.Length; offset += a.Length)
        {
            var length = (int)Math.Min(a.Length, source.Snapshot.Length - offset); source.Snapshot.Read(new(offset, length), a.AsSpan(0, length));
            extracted.Snapshot.Read(new(offset, length), b.AsSpan(0, length)); Assert.True(a.AsSpan(0, length).SequenceEqual(b.AsSpan(0, length)));
        }
        observations.Add(new { label = $"{mib}MiB-extraction", elapsedMs = extraction.Elapsed.TotalMilliseconds });
    }
    [Fact(Explicit = true)]
    public async Task MalformedWorkloadHasExplicitStructureAndCanStillExtractExactBytes()
    {
        var messages = Enumerable.Range(1, 1000).Select(n => Header(n) + "ZXY|custom|||\rBAD malformed\rOBX|1|ST|SYN||bad\\Q\\escape\r").ToArray();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(string.Concat(messages)));
        var source = await MessageFileReader.OpenFileAsync(documents, "Synthetic malformed.hl7", input, Token);
        using var metadata = await Measure("malformed-1000-metadata", source, "SELECT DocumentId,MessageId FROM Messages");
        Assert.Equal(1000, metadata!.RowCount);
        using var malformed = await Measure("malformed-1000-structure", source, "SELECT count(*) FROM Segments WHERE Status='Unstructured'");
        Assert.Equal(1000, malformed!.ReadRow(0).Cells[0].Integer);
        var extracted = await metadata.OpenMatchingMessagesAsync(documents, Token); Assert.NotNull(extracted);
        Assert.Equal(Encoding.UTF8.GetBytes(string.Concat(messages)), extracted.Snapshot!.ReadBytes(new(0, extracted.Snapshot.Length), int.MaxValue));
    }
    [Fact(Explicit = true)]
    public async Task WorkerStartupAndSynchronizedCancellationAreMeasured()
    {
        var startup = new List<double>(); var worker = Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll");
        for (var i = 0; i < 5; i++)
        {
            var watch = Stopwatch.StartNew(); await using var session = await QueryWorkerSession.StartAsync(worker, token: Token);
            startup.Add(watch.Elapsed.TotalMilliseconds); Assert.Single((await session.ExecuteAsync("SELECT 1", token: Token)).Rows);
            using var process = Process.GetProcessById(session.ProcessId); await session.DisposeAsync(); Assert.True(process.HasExited);
        }
        observations.Add(new { label = "Worker-startup", samplesMs = startup });
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token); var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var waiting = await QueryWorkerSession.StartAsync(worker, token: Token, sourceRead: async (_, ct) =>
        { entered.SetResult(); await Task.Delay(Timeout.Infinite, ct); return QueryCell.Null; });
        var run = waiting.ExecuteAsync("SELECT HL7_VALUE('synthetic',1,'PID-3')", token: cancel.Token); await entered.Task.WaitAsync(Token);
        var stop = Stopwatch.StartNew(); cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run);
        observations.Add(new { label = "Source-RPC-cancellation", elapsedMs = stop.Elapsed.TotalMilliseconds });
    }
    private sealed class Inline(Action<SqlQueryProgress> report) : IProgress<SqlQueryProgress> { public void Report(SqlQueryProgress value) => report(value); }
    public async ValueTask DisposeAsync()
    {
        var pending = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(pending.Select(d => d.Disposal));
        if (Environment.GetEnvironmentVariable("HL7WORKBENCH_SQL_PERF_DIRECTORY") is { } directory)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json"), JsonSerializer.Serialize(new
            { environment = new { os = RuntimeInformation.OSDescription, architecture = RuntimeInformation.ProcessArchitecture.ToString(), runtime = RuntimeInformation.FrameworkDescription,
                availableGcMemoryBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes, processors = Environment.ProcessorCount }, observations }, new JsonSerializerOptions { WriteIndented = true }), CancellationToken.None);
        }
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
