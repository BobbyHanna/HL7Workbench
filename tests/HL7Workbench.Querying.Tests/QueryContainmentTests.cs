using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Xunit;

namespace HL7Workbench.Querying.Tests;

public sealed class QueryContainmentTests
{
    [Fact]
    public async Task WindowsWorkerIsActuallyAssignedToTheConfiguredMemoryAndKillOnCloseJob()
    {
        if (!OperatingSystem.IsWindows()) Assert.Skip("Windows Job Object enforcement is platform-specific.");
        var host = Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll");
        await using var session = await QueryWorkerSession.StartAsync(host, token: TestContext.Current.CancellationToken);
        var lifetime = typeof(QueryWorkerSession).GetField("containment", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        var handle = (SafeFileHandle)typeof(QueryWorkerLifetime).GetField("job", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(lifetime)!;
        Assert.True(QueryInformationJobObject(handle, 9, out var limits, (uint)Marshal.SizeOf<ExtendedLimits>(), out _));
        Assert.Equal((nuint)QueryLimits.WorkerMemoryBytes, limits.ProcessMemory); Assert.Equal((nuint)QueryLimits.WorkerMemoryBytes, limits.JobMemory);
        Assert.Equal(0x2300u, limits.Basic.Flags & 0x2300u);
        using var process = Process.GetProcessById(session.ProcessId);
        Assert.True(IsProcessInJob(process.Handle, handle, out var assigned)); Assert.True(assigned);
        await session.DisposeAsync(); Assert.True(process.HasExited);
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct BasicLimits
    {
        public long ProcessTime, JobTime;
        public uint Flags;
        public nuint MinimumWorkingSet, MaximumWorkingSet;
        public uint ActiveProcesses;
        public nuint Affinity;
        public uint Priority, Scheduling;
    }
    [StructLayout(LayoutKind.Sequential)]
    private struct IoCounters { public ulong ReadOperations, WriteOperations, OtherOperations, ReadBytes, WriteBytes, OtherBytes; }
    [StructLayout(LayoutKind.Sequential)]
    private struct ExtendedLimits { public BasicLimits Basic; public IoCounters Io; public nuint ProcessMemory, JobMemory, PeakProcessMemory, PeakJobMemory; }
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryInformationJobObject(SafeFileHandle job, int information, out ExtendedLimits limits, uint size, out uint returned);
    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(IntPtr process, SafeFileHandle job, [MarshalAs(UnmanagedType.Bool)] out bool assigned);
}

public sealed class QueryResourceStressTests
{
    [Theory(Explicit = true)]
    [InlineData(99_999)] [InlineData(100_000)] [InlineData(100_001)]
    public async Task ActualResultRowCeilingHasNoHiddenTruncation(int count)
    {
        var token = TestContext.Current.CancellationToken;
        var host = Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll");
        await using var worker = await QueryWorkerSession.StartAsync(host, token: token);
        const string sql = "WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<@count) SELECT x FROM n";
        QueryParameter[] parameters = [new("@count", QueryCell.From((long)count))];
        if (count > QueryLimits.MaximumResultRows) Assert.Equal(QueryError.ResultLimit, (await Assert.ThrowsAsync<QueryException>(() => worker.ExecuteAsync(sql, parameters, token))).Code);
        else { var result = await worker.ExecuteAsync(sql, parameters, token); Assert.Equal(count, result.Rows.Count); Assert.Equal(count, result.Rows[^1].Cells[0].Integer); }
    }
    [Fact(Explicit = true)]
    public async Task ActualImportRowCeilingAcceptsTheCapAndRejectsTheNextRow()
    {
        var token = TestContext.Current.CancellationToken;
        var host = Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll");
        await using var worker = await QueryWorkerSession.StartAsync(host, token: token);
        for (var start = 0; start < QueryLimits.MaximumImportedRows; start += QueryLimits.MaximumImportBatchRows)
        {
            var batch = Enumerable.Range(start, Math.Min(QueryLimits.MaximumImportBatchRows, QueryLimits.MaximumImportedRows - start)).Select(i =>
                new QueryImportRow(QueryTable.Documents, [QueryCell.From("S" + i), QueryCell.From((long)i), QueryCell.From(0L)])).ToArray();
            await worker.ImportAsync(batch, token);
        }
        Assert.Equal(QueryError.ImportLimit, (await Assert.ThrowsAsync<QueryException>(() => worker.ImportAsync([new(QueryTable.Documents, [QueryCell.From("over"), QueryCell.From(0L), QueryCell.From(0L)])], token))).Code);
        await using var recovery = await QueryWorkerSession.StartAsync(host, token: token); Assert.Single((await recovery.ExecuteAsync("SELECT 1", token: token)).Rows);
    }
    [Fact(Explicit = true)]
    public async Task SourceReadCeilingFailsAtTheActualHardCapAndReapsTheWorker()
    {
        var token = TestContext.Current.CancellationToken;
        var host = Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll");
        var reads = 0; var watch = Stopwatch.StartNew();
        await using var worker = await QueryWorkerSession.StartAsync(host, token: token, sourceRead: (_, _) => { reads++; return Task.FromResult(QueryCell.Null); });
        using var process = Process.GetProcessById(worker.ProcessId);
        var error = await Assert.ThrowsAsync<QueryException>(() => worker.ExecuteAsync("""
            WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<100001)
            SELECT count(HL7_RAW('synthetic',x,'PID-3')) FROM n
            """, token: token));
        Assert.Equal(QueryError.ResourceLimit, error.Code); Assert.Equal(QueryLimits.MaximumSourceReads, reads); Assert.True(process.HasExited);
        await using var recovery = await QueryWorkerSession.StartAsync(host, token: token); Assert.Single((await recovery.ExecuteAsync("SELECT 1", token: token)).Rows);
        if (Environment.GetEnvironmentVariable("HL7WORKBENCH_SQL_PERF_DIRECTORY") is { } directory)
        {
            Directory.CreateDirectory(directory);
            await File.WriteAllTextAsync(Path.Combine(directory, "source-read-cap.json"), JsonSerializer.Serialize(new { reads, elapsedMs = watch.Elapsed.TotalMilliseconds, error = error.Code.ToString() }), token);
        }
    }
}
