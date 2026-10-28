using System.Diagnostics;
using System.Text;
using Xunit;

namespace HL7Workbench.Querying.Tests;

public sealed class QueryHardeningTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Host => Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll");
    [Fact]
    public void EveryConfigurableWorkerBudgetRejectsValuesAboveItsHardCap()
    {
        QueryBudgets[] invalid = [new() { ResultRows = QueryLimits.MaximumResultRows + 1 }, new() { ResultBytes = QueryLimits.MaximumResultBytes + 1 },
            new() { ImportedRows = QueryLimits.MaximumImportedRows + 1 }, new() { ImportBytes = QueryLimits.MaximumImportBytes + 1 },
            new() { QueryMilliseconds = 30_001 }, new() { ProgressCallbacks = 100_001 }];
        foreach (var budget in invalid) Assert.Equal(QueryError.InvalidRequest, Assert.Throws<QueryException>(budget.Validate).Code);
        QueryWorkerOptions[] options = [new() { StartupTimeout = TimeSpan.FromSeconds(31) }, new() { OperationTimeout = TimeSpan.FromSeconds(31) }, new() { SessionTimeout = TimeSpan.FromMinutes(11) }];
        foreach (var option in options) Assert.Throws<QueryException>(option.Validate);
        new QueryBudgets().Validate(); new QueryWorkerOptions().Validate();
    }
    [Theory]
    [InlineData("{\"Kind\":13,\"Plan\":{\"Tables\":null}}")]
    [InlineData("{\"Kind\":13,\"Plan\":{\"Tables\":[999]}}")]
    [InlineData("{\"Kind\":7,\"Rows\":[{\"Cells\":null}]}")]
    [InlineData("{\"Kind\":7,\"Rows\":[null]}")]
    [InlineData("{\"Kind\":5,\"Columns\":[null]}")]
    [InlineData("{\"Kind\":5,\"Origins\":[null]}")]
    public async Task MalformedNestedResponseShapesHaveFixedProtocolErrors(string json)
    {
        var payload = Encoding.UTF8.GetBytes(json); using var input = new MemoryStream();
        input.Write(BitConverter.GetBytes(payload.Length)); input.Write(payload); input.Position = 0;
        Assert.Equal(QueryError.ProtocolError, (await Assert.ThrowsAsync<QueryException>(() => QueryProtocol.ReadAsync(input, Token))).Code);
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)]
    public async Task AccountedImportByteBoundaryIsExact(int offset)
    {
        // Row overhead 32 + three 32-byte cells + one ASCII character = 129.
        await using var worker = await QueryWorkerSession.StartAsync(Host, new() { ImportBytes = 129 + offset }, token: Token);
        QueryImportRow[] rows = [new(QueryTable.Documents, [QueryCell.From("S"), QueryCell.From(1L), QueryCell.From(0L)])];
        if (offset < 0) Assert.Equal(QueryError.ImportLimit, (await Assert.ThrowsAsync<QueryException>(() => worker.ImportAsync(rows, Token))).Code);
        else { await worker.ImportAsync(rows, Token); Assert.Single((await worker.ExecuteAsync("SELECT * FROM Documents", token: Token)).Rows); }
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)]
    public async Task AccountedResultByteBoundaryIncludesHeadersAndRows(int offset)
    {
        // Header 32 + UTF-8 alias byte; row 32 + integer cell 32 = 97.
        await using var worker = await QueryWorkerSession.StartAsync(Host, new() { ResultBytes = 97 + offset }, token: Token);
        if (offset < 0) Assert.Equal(QueryError.ResultLimit, (await Assert.ThrowsAsync<QueryException>(() => worker.ExecuteAsync("SELECT 1 AS V", token: Token))).Code);
        else Assert.Single((await worker.ExecuteAsync("SELECT 1 AS V", token: Token)).Rows);
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)]
    public async Task BlobByteMaterializationBoundaryPreservesCompleteValues(int offset)
    {
        await using var worker = await QueryWorkerSession.StartAsync(Host, token: Token);
        QueryParameter[] parameters = [new("@v", new(QueryValueKind.Blob, Blob: new byte[QueryLimits.MaximumValueBytes + offset]))];
        if (offset > 0) Assert.Equal(QueryError.MaterializationLimit, (await Assert.ThrowsAsync<QueryException>(() => worker.ExecuteAsync("SELECT @v", parameters, Token))).Code);
        else Assert.Equal(QueryLimits.MaximumValueBytes + offset, (await worker.ExecuteAsync("SELECT @v", parameters, Token)).Rows[0].Cells[0].Blob!.Length);
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)]
    public async Task IncomingFrameAtEncodedCapIsAcceptedWithoutChangingTheCap(int offset)
    {
        var payload = Encoding.UTF8.GetBytes("{\"Kind\":1,\"Version\":2}".PadRight(QueryLimits.MaximumFrameBytes + offset));
        using var stream = new MemoryStream(); stream.Write(BitConverter.GetBytes(payload.Length)); stream.Write(payload); stream.Position = 0;
        Assert.Equal(QueryFrameKind.Ready, (await QueryProtocol.ReadAsync(stream, Token)).Kind);
    }
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(1)]
    public async Task SqlLengthBoundaryIsValidatedBeforePreparingAnyStatement(int offset)
    {
        await using var worker = await QueryWorkerSession.StartAsync(Host, token: Token);
        var sql = "SELECT 1".PadRight(QueryLimits.MaximumSqlCharacters + offset);
        if (offset <= 0) Assert.Single((await worker.ExecuteAsync(sql, token: Token)).Rows);
        else Assert.Equal(QueryError.InvalidRequest, (await Assert.ThrowsAsync<QueryException>(() => worker.ExecuteAsync(sql, token: Token))).Code);
    }
    public static TheoryData<string, bool> Bypasses => new[]
    {
        "/* synthetic */ uPdAtE [Messages] SET [MessageType]='SYN' RETURNING MessageId",
        "\t-- SELECT\n INSERT INTO \"Documents\" VALUES('synthetic',1,0) RETURNING DocumentId",
        "WITH \"x\" AS (SELECT 1) DELETE FROM [Messages] RETURNING MessageId",
        "SELECT 1; /* safe? */ pRaGmA query_only=OFF",
        "SELECT (SELECT load_extension('synthetic'))",
        "WITH x AS (SELECT readfile('synthetic')) SELECT * FROM x",
        "SELECT writefile('synthetic', 'synthetic') WHERE 0",
        "SELECT * FROM pragma_database_list()",
        "SELECT * FROM main.sqlite_master",
        "WITH SourceFields AS (SELECT * FROM main.SourceFields) SELECT * FROM SourceFields",
        "SELECT * FROM \"SourceFields\" AS \"Fields\"",
        "EXPLAIN ATTACH ':memory:' AS synthetic",
        "RELEASE synthetic",
        "CREATE VIRTUAL TABLE synthetic USING mw_Fields"
    }.Aggregate(new TheoryData<string, bool>(), (data, sql) => { data.Add(sql, false); data.Add(sql, true); return data; });
    [Theory, MemberData(nameof(Bypasses))]
    public async Task AdversarialSyntaxCannotBypassPlanOrExecutionAuthorizer(string sql, bool sourceBacked)
    {
        await using var worker = await QueryWorkerSession.StartAsync(Host, token: Token,
            sourceRead: sourceBacked ? (_, _) => throw new InvalidOperationException("No source access authorized") : null);
        // Install/reuse the execution authorizer once before attacking; this also checks its lifetime across queries.
        await worker.ExecuteAsync("SELECT 1", token: Token);
        var error = await Assert.ThrowsAsync<QueryException>(() => worker.ExecuteAsync(sql, token: Token));
        Assert.Contains(error.Code, new[] { QueryError.NotAuthorized, QueryError.InvalidSql, QueryError.MultipleStatements });
        Assert.DoesNotContain("synthetic", error.ToString(), StringComparison.OrdinalIgnoreCase);
        await using var recovery = await QueryWorkerSession.StartAsync(Host, token: Token);
        Assert.Single((await recovery.ExecuteAsync("SELECT 1", token: Token)).Rows);
    }
    [Theory]
    [InlineData(99)] [InlineData(100)] [InlineData(101)]
    public async Task ResultRowBoundaryIsCompleteOrFailsAndCanRecover(int count)
    {
        await using var worker = await QueryWorkerSession.StartAsync(Host, new() { ResultRows = 100 }, token: Token);
        const string sql = "WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<@count) SELECT x FROM n";
        if (count <= 100)
        {
            var result = await worker.ExecuteAsync(sql, [new("@count", QueryCell.From((long)count))], Token);
            Assert.Equal(count, result.Rows.Count); Assert.Equal(count, result.Rows[^1].Cells[0].Integer);
        }
        else Assert.Equal(QueryError.ResultLimit, (await Assert.ThrowsAsync<QueryException>(() => worker.ExecuteAsync(sql, [new("@count", QueryCell.From((long)count))], Token))).Code);
        await using var recovery = await QueryWorkerSession.StartAsync(Host, token: Token); Assert.Single((await recovery.ExecuteAsync("SELECT 1", token: Token)).Rows);
    }
    [Theory]
    [InlineData(63)] [InlineData(64)] [InlineData(65)]
    public async Task NativeColumnAndParameterCeilingsCannotBeRaised(int count)
    {
        await using var worker = await QueryWorkerSession.StartAsync(Host, token: Token);
        var parameters = Enumerable.Range(0, count).Select(n => new QueryParameter("@p" + n, QueryCell.From((long)n))).ToArray();
        var sql = "SELECT " + string.Join(',', parameters.Select(p => p.Name));
        if (count <= QueryLimits.MaximumColumns) Assert.Equal(count, (await worker.ExecuteAsync(sql, parameters, Token)).Columns.Length);
        else Assert.Equal(QueryError.ParameterMismatch, (await Assert.ThrowsAsync<QueryException>(() => worker.ExecuteAsync(sql, parameters, Token))).Code);
    }
    [Theory]
    [InlineData(7)] [InlineData(8)]
    public async Task CompleteRowMaterializationUsesAccountedBytes(int cells)
    {
        await using var worker = await QueryWorkerSession.StartAsync(Host, token: Token);
        var sql = "SELECT " + string.Join(',', Enumerable.Repeat("@value", cells));
        QueryParameter[] parameters = [new("@value", QueryCell.From(new string('Q', QueryLimits.MaximumValueCharacters)))];
        if (cells == 7) Assert.Equal(cells, (await worker.ExecuteAsync(sql, parameters, Token)).Rows[0].Cells.Length);
        else Assert.Equal(QueryError.MaterializationLimit, (await Assert.ThrowsAsync<QueryException>(() => worker.ExecuteAsync(sql, parameters, Token))).Code);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SourceCallbackBarrierMakesCrashAndCancellationRecoveryDeterministic(bool crash)
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        await using var worker = await QueryWorkerSession.StartAsync(Host, token: Token, sourceRead: async (_, ct) =>
        { entered.SetResult(); await release.Task.WaitAsync(ct); return QueryCell.From("synthetic"); });
        var run = worker.ExecuteAsync("SELECT HL7_VALUE('synthetic',1,'PID-3')", token: cancel.Token);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5), Token);
        if (crash)
        {
            using var process = Process.GetProcessById(worker.ProcessId); process.Kill(true); await process.WaitForExitAsync(Token); release.SetResult();
            Assert.Equal(QueryError.WorkerStopped, (await Assert.ThrowsAsync<QueryException>(() => run.WaitAsync(TimeSpan.FromSeconds(5), Token))).Code);
        }
        else { cancel.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(5), Token)); }
        await using var recovery = await QueryWorkerSession.StartAsync(Host, token: Token);
        Assert.Single((await recovery.ExecuteAsync("SELECT 1", token: Token)).Rows);
    }
}
