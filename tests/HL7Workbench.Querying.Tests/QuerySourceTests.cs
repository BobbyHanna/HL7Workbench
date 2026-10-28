using Xunit;

namespace HL7Workbench.Querying.Tests;

public sealed class QuerySourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Host => Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll");
    [Fact]
    public async Task EmptyBlobRemainsBlobRatherThanSqlNull()
    {
        await using var session = await QueryWorkerSession.StartAsync(Host, token: Token);
        var result = await session.ExecuteAsync("SELECT @value AS B,typeof(@value) AS T", [new("@value", new(QueryValueKind.Blob, Blob: []))], Token);
        Assert.Equal(QueryValueKind.Blob, result.Rows[0].Cells[0].Kind); Assert.Empty(result.Rows[0].Cells[0].Blob!); Assert.Equal("blob", result.Rows[0].Cells[1].Text);
    }
    [Fact]
    public async Task PlanUsesTheAuthorizerWithoutSteppingOrSealingAndExecuteSealsPermanently()
    {
        var reads = 0;
        await using var session = await QueryWorkerSession.StartAsync(Host, token: Token, sourceRead: (read, _) =>
        { reads++; return Task.FromResult(QueryCell.From("SYN-VALUE")); });
        var plan = await session.PlanAsync("SELECT HL7_VALUE(DocumentId,MessageId,'PID-3') FROM Messages", token: Token);
        Assert.Contains(QueryTable.Messages, plan.Tables); Assert.Equal(0, reads);
        await session.ImportAsync([new(QueryTable.Documents, [QueryCell.From("synthetic"), QueryCell.From(1L), QueryCell.From(0L)])], Token);
        Assert.Single((await session.ExecuteAsync("SELECT * FROM Documents", token: Token)).Rows);
        Assert.Equal(QueryError.InvalidRequest, (await Assert.ThrowsAsync<QueryException>(() => session.PlanAsync("SELECT * FROM Documents", token: Token))).Code);
    }
    [Theory]
    [InlineData("PRAGMA query_only=OFF")]
    [InlineData("DELETE FROM Messages")]
    [InlineData("SELECT 1; DROP TABLE Messages")]
    [InlineData("SELECT MW_SOURCE(0,'synthetic',1,'PID-3')")]
    [InlineData("SELECT RawValue FROM SourceFields")]
    [InlineData("WITH Fields AS (SELECT RawValue FROM SourceFields) SELECT * FROM Fields")]
    [InlineData("WITH Fields AS (SELECT MW_SOURCE(0,'synthetic',1,'PID-3')) SELECT * FROM Fields")]
    [InlineData("ATTACH ':memory:' AS extra")]
    [InlineData("VACUUM INTO 'synthetic-forbidden.db'")]
    [InlineData("CREATE VIRTUAL TABLE unsafe USING mw_Fields")]
    [InlineData("SELECT * FROM mw_Fields")]
    [InlineData("SELECT * FROM pragma_table_info('Messages')")]
    [InlineData("SELECT * FROM sqlite_schema")]
    [InlineData("SELECT rowid FROM Fields")]
    [InlineData("SELECT load_extension('synthetic-forbidden')")]
    [InlineData("SELECT writefile('synthetic-forbidden', 'value')")]
    [InlineData("WITH SourceFields AS (SELECT RawValue FROM main.SourceFields) SELECT * FROM SourceFields")]
    [InlineData("SELECT RawValue FROM SourceFields AS Fields")]
    [InlineData("WITH Fields AS (SELECT RawValue FROM SourceComponents) SELECT * FROM Fields")]
    public async Task PlanningAndLazySourceModeDoNotBypassEnforcement(string sql)
    {
        await using var session = await QueryWorkerSession.StartAsync(Host, token: Token, sourceRead: (_, _) => throw new InvalidOperationException("Not reached"));
        var error = await Assert.ThrowsAsync<QueryException>(() => session.PlanAsync(sql, token: Token));
        Assert.Contains(error.Code, new[] { QueryError.NotAuthorized, QueryError.InvalidSql });
    }
    [Fact]
    public async Task CancellationWhileWaitingForSourceAndCallbackFailuresAreContained()
    {
        await using var session = await QueryWorkerSession.StartAsync(Host, token: Token, sourceRead: async (_, ct) =>
        { await Task.Delay(Timeout.Infinite, ct); return QueryCell.Null; });
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token); cancel.CancelAfter(150);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ExecuteAsync("SELECT HL7_VALUE('synthetic',1,'PID-3')", token: cancel.Token));
        await using var failed = await QueryWorkerSession.StartAsync(Host, token: Token, sourceRead: (_, _) => throw new QueryException(QueryError.SourceUnavailable));
        Assert.Equal(QueryError.SourceUnavailable, (await Assert.ThrowsAsync<QueryException>(() => failed.ExecuteAsync("SELECT HL7_VALUE('synthetic',1,'PID-3')", token: Token))).Code);
    }
}
