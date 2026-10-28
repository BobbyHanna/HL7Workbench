using System.Diagnostics;
using System.Text;
using Xunit;

namespace HL7Workbench.Querying.Tests;

public sealed class QueryWorkerTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Host => Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll");
    private const string Slow = "WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<1000000000) SELECT sum(x) FROM n";
    private static Task<QueryWorkerSession> Start(QueryBudgets? budgets = null, QueryWorkerOptions? options = null) =>
        QueryWorkerSession.StartAsync(Host, budgets, options, Token);
    private static QueryImportRow Row(QueryTable table, params object?[] values) => new(table, values.Select(v => v switch
    {
        null => QueryCell.Null, int n => QueryCell.From((long)n), long n => QueryCell.From(n), string text => QueryCell.From(text),
        _ => throw new InvalidOperationException("Synthetic fixture type")
    }).ToArray());
    private static QueryImportRow Document(string document = "synthetic-document-a", int number = 1) => Row(QueryTable.Documents, document, number, 0);
    private static QueryImportRow Message(int id, string document = "synthetic-document-a", string trigger = "A01") =>
        Row(QueryTable.Messages, document, id, id, "ADT", trigger, "SYN-DUPLICATE", "2.5.1", "SYN", "TEST", "DEST", "TEST", "20300101000000", 0, "Complete");
    private static QueryImportRow Segment(int message, int id, string name = "PID", int occurrence = 1, string document = "synthetic-document-a") =>
        Row(QueryTable.Segments, document, message, id, name, occurrence, id, "Complete");
    private static async Task Seed(QueryWorkerSession session)
    {
        await session.ImportAsync([Document(), Document("synthetic-document-b", 2)], Token);
        await session.ImportAsync([Message(1), Message(2, trigger: "A04"), Message(1, "synthetic-document-b")], Token);
        await session.ImportAsync([Segment(1, 1), Segment(1, 2, "OBX"), Segment(1, 3, "OBX", 2), Segment(2, 1), Segment(1, 1, document: "synthetic-document-b")], Token);
        await session.ImportAsync([
            Row(QueryTable.Fields, "synthetic-document-a", 1, 1, "PID", 1, 3, 1, "SYN-PATIENT^MR", "SYN-PATIENT^MR", "Value", "Complete", 14),
            Row(QueryTable.Fields, "synthetic-document-a", 1, 1, "PID", 1, 3, 2, "SYN-ALIAS^MR", "SYN-ALIAS^MR", "Value", "Complete", 12),
            Row(QueryTable.Fields, "synthetic-document-a", 2, 1, "PID", 1, 3, 1, "", "", "Empty", "Complete", 0),
            Row(QueryTable.Fields, "synthetic-document-b", 1, 1, "PID", 1, 3, 1, "\"\"", null, "ExplicitNull", "Complete", 2)
        ], Token);
    }
    private static bool Exited(int id)
    {
        try { using var process = Process.GetProcessById(id); return process.HasExited; }
        catch (ArgumentException) { return true; }
    }
    [Fact]
    public async Task RealWorkerSupportsJoinsDistinctFiltersOrderingGroupingAggregatesAndLimit()
    {
        await using var session = await Start(); await Seed(session);
        Assert.Equal("3.53.4", session.EngineVersion);
        var result = await session.ExecuteAsync("""
            SELECT m.DocumentId, m.MessageId, f.DecodedValue AS PatientId
            FROM Messages m JOIN Fields f ON f.DocumentId=m.DocumentId AND f.MessageId=m.MessageId
            WHERE f.SegmentName='PID' AND f.FieldNumber=3 AND f.RepetitionNumber=1
            ORDER BY m.DocumentId, m.MessageNumber DESC LIMIT 2
            """, token: Token);
        Assert.Equal(new[] { "DocumentId", "MessageId", "PatientId" }, result.Columns);
        Assert.Equal(2, result.Rows.Count); Assert.Equal("", result.Rows[0].Cells[2].Text);
        Assert.Equal("SYN-PATIENT^MR", result.Rows[1].Cells[2].Text);
        var counts = await session.ExecuteAsync("SELECT TriggerEvent, count(*) AS N FROM Messages GROUP BY TriggerEvent ORDER BY N DESC", token: Token);
        Assert.Equal(2L, counts.Rows[0].Cells[1].Integer);
        var distinct = await session.ExecuteAsync("SELECT DISTINCT MessageControlId FROM Messages", token: Token);
        Assert.Single(distinct.Rows); // Control IDs are not source identities.
    }
    [Fact]
    public async Task NullEmptyIntegerRealBlobAndUnicodeAreTypedWithoutStringCoercion()
    {
        await using var session = await Start();
        var result = await session.ExecuteAsync("SELECT NULL AS N, '' AS E, 42 AS I, 1.25 AS R, X'00FF' AS B, 'λ synthetic' AS T", token: Token);
        var cells = Assert.Single(result.Rows).Cells;
        Assert.Equal(Enum.GetValues<QueryValueKind>().Order(), cells.Select(c => c.Kind).Distinct().Order());
        Assert.Equal("", cells[1].Text); Assert.Equal(42L, cells[2].Integer); Assert.Equal(1.25, cells[3].Real);
        Assert.Equal(new byte[] { 0, 255 }, cells[4].Blob); Assert.Equal("λ synthetic", cells[5].Text);
    }
    [Fact]
    public async Task BoundValuesCannotBecomeSqlAndSchemasKeepMultiDocumentIdentity()
    {
        await using var session = await Start(); await Seed(session);
        const string value = "synthetic' OR 1=1; DROP TABLE Messages;--";
        var result = await session.ExecuteAsync("SELECT @value AS Literal, count(*) FROM Messages WHERE DocumentId=@document",
            [new("@value", QueryCell.From(value)), new("@document", QueryCell.From("synthetic-document-b"))], Token);
        Assert.Equal(value, result.Rows[0].Cells[0].Text); Assert.Equal(1L, result.Rows[0].Cells[1].Integer);
        Assert.Equal(3L, (await session.ExecuteAsync("SELECT count(*) FROM Messages", token: Token)).Rows[0].Cells[0].Integer);
    }
    [Fact]
    public async Task ComponentsSubcomponentsAndExplicitStatesAreOrdinaryRelationalData()
    {
        await using var session = await Start(); await Seed(session);
        await session.ImportAsync([
            Row(QueryTable.Components, "synthetic-document-a", 1, 1, "PID", 1, 3, 1, 1, "A&B", "A&B", "Value", "Complete", 3),
            Row(QueryTable.Subcomponents, "synthetic-document-a", 1, 1, "PID", 1, 3, 1, 1, 2, "B", "B", "Value", "Complete", 1)
        ], Token);
        var selected = await session.ExecuteAsync("SELECT DecodedValue FROM Subcomponents WHERE ComponentNumber=1 AND SubcomponentNumber=2", token: Token);
        Assert.Equal("B", Assert.Single(selected.Rows).Cells[0].Text);
        var states = await session.ExecuteAsync("SELECT ValueState, DecodedValue FROM Fields ORDER BY DocumentId,MessageId,RepetitionNumber", token: Token);
        Assert.Equal(QueryValueKind.Text, states.Rows[2].Cells[1].Kind); Assert.Equal("", states.Rows[2].Cells[1].Text);
        Assert.Equal("ExplicitNull", states.Rows[3].Cells[0].Text); Assert.Equal(QueryValueKind.Null, states.Rows[3].Cells[1].Kind);
    }
    [Theory]
    [InlineData("INSERT INTO Messages DEFAULT VALUES")]
    [InlineData("UPDATE Messages SET MessageType='SYN'")]
    [InlineData("DELETE FROM Messages")]
    [InlineData("DROP TABLE Messages")]
    [InlineData("ALTER TABLE Messages ADD COLUMN Synthetic TEXT")]
    [InlineData("CREATE TABLE Synthetic(x)")]
    [InlineData("CREATE TEMP TABLE Synthetic(x)")]
    [InlineData("CREATE VIEW Synthetic AS SELECT * FROM Messages")]
    [InlineData("CREATE TRIGGER Synthetic AFTER DELETE ON Messages BEGIN DELETE FROM Fields; END")]
    [InlineData("CREATE VIRTUAL TABLE Synthetic USING fts5(x)")]
    [InlineData("ATTACH ':memory:' AS Synthetic")]
    [InlineData("DETACH main")]
    [InlineData("VACUUM INTO 'SYNTHETIC-QUERY-NOT-CREATED.db'")]
    [InlineData("PRAGMA query_only=OFF")]
    [InlineData("PRAGMA writable_schema=ON")]
    [InlineData("PRAGMA hard_heap_limit=0")]
    [InlineData("PRAGMA temp_store_directory='SYNTHETIC'")]
    [InlineData("PRAGMA database_list")]
    [InlineData("EXPLAIN PRAGMA query_only=OFF")]
    [InlineData("BEGIN")]
    [InlineData("COMMIT")]
    [InlineData("ROLLBACK")]
    [InlineData("SAVEPOINT Synthetic")]
    [InlineData("REINDEX")]
    [InlineData("ANALYZE")]
    [InlineData("SELECT load_extension('SYNTHETIC-NOT-LOADED')")]
    [InlineData("SELECT writefile('SYNTHETIC-NOT-CREATED','test')")]
    [InlineData("SELECT readfile('SYNTHETIC-NOT-READ')")]
    [InlineData("SELECT * FROM pragma_table_info('Messages')")]
    [InlineData("SELECT * FROM sqlite_schema")]
    [InlineData("SELECT * FROM sqlite_temp_schema")]
    [InlineData("SELECT rowid FROM Messages")]
    [InlineData("SELECT randomblob(1000000000)")]
    [InlineData("SELECT printf('%1000000000s','SYNTHETIC')")]
    [InlineData("WITH x AS (SELECT 1) DELETE FROM Messages")]
    public async Task DenyByDefaultPreventsMutationConfigurationExternalAccessAndUnapprovedFunctions(string sql)
    {
        await using var session = await Start(); var id = session.ProcessId;
        var error = await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync(sql, token: Token));
        Assert.Contains(error.Code, new[] { QueryError.NotAuthorized, QueryError.InvalidSql });
        Assert.True(Exited(id)); Assert.DoesNotContain("SYNTHETIC", error.ToString());
    }
    [Theory]
    [InlineData("SELECT 1; SELECT 2", QueryError.MultipleStatements)]
    [InlineData("SELECT 1; -- synthetic\n DELETE FROM Messages", QueryError.NotAuthorized)]
    [InlineData("SELECT 'synthetic;literal'; SELECT 2", QueryError.MultipleStatements)]
    [InlineData("SELECT 'SYNTHETIC-SECRET' FROM unavailable_synthetic_table", QueryError.InvalidSql)]
    public async Task WholeSubmissionIsPreparedBeforeAnyStatementRunsAndErrorsOmitSql(string sql, QueryError code)
    {
        await using var session = await Start();
        var error = await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync(sql, token: Token));
        Assert.Equal(code, error.Code); Assert.DoesNotContain("SYNTHETIC-SECRET", error.ToString()); Assert.Null(error.InnerException);
    }
    [Fact]
    public async Task SqliteHandlesCommentsSemicolonsAndCtesWithoutAPrefixParser()
    {
        await using var session = await Start();
        var result = await session.ExecuteAsync("/* SELECT; DROP */ WITH x(n) AS (VALUES('synthetic;literal')) SELECT n FROM x; -- tail\n /* trailing; */ ;", token: Token);
        Assert.Equal("synthetic;literal", result.Rows[0].Cells[0].Text);
    }
    [Fact]
    public async Task ForbiddenAttachCreatesNoFileAndFreshWorkerHasNoPriorDatabase()
    {
        var marker = Path.Combine(Path.GetTempPath(), "hl7-query-synthetic-" + Guid.NewGuid().ToString("N") + ".db");
        await using (var session = await Start())
        {
            await session.ImportAsync([Document()], Token);
            var error = await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync("ATTACH '" + marker.Replace("'", "''", StringComparison.Ordinal) + "' AS Synthetic", token: Token));
            Assert.Equal(QueryError.NotAuthorized, error.Code); Assert.False(File.Exists(marker));
        }
        await using var fresh = await Start();
        Assert.Equal(0L, (await fresh.ExecuteAsync("SELECT count(*) FROM Documents", token: Token)).Rows[0].Cells[0].Integer);
    }
    [Theory]
    [InlineData("SELECT @missing")]
    [InlineData("SELECT ?1")]
    public async Task MissingAndPositionalParametersAreRejected(string sql)
    {
        await using var session = await Start();
        Assert.Equal(QueryError.ParameterMismatch, (await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync(sql, token: Token))).Code);
    }
    [Fact]
    public async Task ExtraOrDuplicateParametersAreRejected()
    {
        await using var session = await Start();
        Assert.Equal(QueryError.ParameterMismatch, (await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync("SELECT @p",
            [new("@p", QueryCell.From("SYN")), new("@p", QueryCell.From("SYN"))], Token))).Code);
    }
    [Fact]
    public async Task CompleteMaterializationAtLimitSucceedsAndLargerValueFailsWithoutTruncation()
    {
        await using (var session = await Start())
        {
            var text = new string('λ', QueryLimits.MaximumValueCharacters);
            var result = await session.ExecuteAsync("SELECT @value AS V", [new("@value", QueryCell.From(text))], Token);
            Assert.Equal(text, result.Rows[0].Cells[0].Text);
        }
        await using var oversized = await Start();
        var error = await Assert.ThrowsAsync<QueryException>(() => oversized.ExecuteAsync("SELECT replace(hex(X'00'), '00', @value) || 'X' AS V",
            [new("@value", QueryCell.From(new string('S', QueryLimits.MaximumValueCharacters)))], Token));
        Assert.Equal(QueryError.MaterializationLimit, error.Code);
    }
    [Fact]
    public async Task ValueLengthMetadataDoesNotRequireMaterializingTheUnderlyingValue()
    {
        await using var session = await Start();
        await session.ImportAsync([Document(), Message(1), Segment(1, 1)], Token);
        await session.ImportAsync([Row(QueryTable.Fields, "synthetic-document-a", 1, 1, "PID", 1, 3, 1, null, null, "Value", "NotMaterialized", 50_000_000)], Token);
        var result = await session.ExecuteAsync("SELECT ValueLength, ValueStatus FROM Fields", token: Token);
        Assert.Equal(50_000_000L, result.Rows[0].Cells[0].Integer); Assert.Equal("NotMaterialized", result.Rows[0].Cells[1].Text);
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ResultBudgetFailureDiscardsEveryPageAndReapsWorker(bool rows)
    {
        await using var session = await Start(rows ? new() { ResultRows = 105 } : new() { ResultBytes = 4000 });
        var id = session.ProcessId;
        var error = await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync("WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<1000) SELECT x, 'synthetic' AS V FROM n", token: Token));
        Assert.Equal(QueryError.ResultLimit, error.Code); Assert.True(Exited(id));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public async Task ImportBudgetsAndRelationalConstraintsAreEnforced(bool rows)
    {
        await using var session = await Start(rows ? new() { ImportedRows = 1 } : new() { ImportBytes = 1 });
        if (rows) await session.ImportAsync([Document()], Token);
        Assert.Equal(QueryError.ImportLimit, (await Assert.ThrowsAsync<QueryException>(() => session.ImportAsync([Document("synthetic-document-c", 3)], Token))).Code);
    }
    [Fact]
    public async Task DuplicateKeysAndOrphanRowsRejectTheSession()
    {
        await using (var duplicate = await Start())
        {
            await duplicate.ImportAsync([Document()], Token);
            Assert.Equal(QueryError.ImportRejected, (await Assert.ThrowsAsync<QueryException>(() => duplicate.ImportAsync([Document()], Token))).Code);
        }
        await using var orphan = await Start();
        Assert.Equal(QueryError.ImportRejected, (await Assert.ThrowsAsync<QueryException>(() => orphan.ImportAsync([Message(1)], Token))).Code);
    }
    [Fact]
    public async Task CancellationInterruptsNativeExecutionAndReapsWorkerAndCanRecover()
    {
        await using (var session = await Start())
        {
            var id = session.ProcessId; using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token); cancel.CancelAfter(100);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ExecuteAsync(Slow, token: cancel.Token)); Assert.True(Exited(id));
        }
        await using var recovery = await Start();
        Assert.Single((await recovery.ExecuteAsync("SELECT 1", token: Token)).Rows);
    }
    [Fact]
    public async Task ParentDeadlineInterruptsNativeExecutionAfterReadinessAndReapsWorkerAndCanRecover()
    {
        // Start returns only after Ready. Initialization gets its own budget;
        // the 100 ms exchange deadline still applies to native query execution.
        await using var timed = await Start(options: new() { OperationTimeout = TimeSpan.FromMilliseconds(100) });
        var timedId = timed.ProcessId;
        Assert.Equal(QueryError.Timeout, (await Assert.ThrowsAsync<QueryException>(() => timed.ExecuteAsync(Slow, token: Token))).Code);
        Assert.True(Exited(timedId));
        await using var recovery = await Start();
        Assert.Single((await recovery.ExecuteAsync("SELECT 1", token: Token)).Rows);
    }
    [Fact]
    public async Task InitializationUsesStartupBudgetEvenWithMinimalPostReadinessExchangeBudget()
    {
        // No query is run with this deliberately unusable exchange budget. Ready
        // must be governed by StartupTimeout, independently of later operations.
        await using var session = await Start(options: new() { OperationTimeout = TimeSpan.FromTicks(1) });
        Assert.Equal("3.53.4", session.EngineVersion);
        var id = session.ProcessId;
        await session.DisposeAsync();
        Assert.True(Exited(id));
    }
    [Fact]
    public async Task SqliteDeadlineAndInstructionBudgetAreIndependentOfResultSize()
    {
        await using (var session = await Start(new() { QueryMilliseconds = 100 }))
            Assert.Equal(QueryError.Timeout, (await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync(Slow, token: Token))).Code);
        await using var limited = await Start(new() { ProgressCallbacks = 1 });
        Assert.Equal(QueryError.ResourceLimit, (await Assert.ThrowsAsync<QueryException>(() => limited.ExecuteAsync(Slow, token: Token))).Code);
    }
    [Fact]
    public async Task ThousandsOfRowsCrossPagesWithoutUiMaterializationOrIdentityLoss()
    {
        await using var session = await Start(); await session.ImportAsync([Document()], Token);
        for (var start = 1; start <= 3000; start += 100)
            await session.ImportAsync(Enumerable.Range(start, 100).Select(n => Message(n)).ToArray(), Token);
        var result = await session.ExecuteAsync("SELECT MessageId FROM Messages ORDER BY MessageNumber DESC", token: Token);
        Assert.Equal(3000, result.Rows.Count); Assert.Equal(3000L, result.Rows[0].Cells[0].Integer); Assert.Equal(1L, result.Rows[^1].Cells[0].Integer);
    }
    [Fact]
    public async Task ImportAfterQueryIsRejectedAndDiagnosticsNeverPrintValues()
    {
        await using var session = await Start(); await session.ExecuteAsync("SELECT 1", token: Token);
        Assert.Equal(QueryError.InvalidRequest, (await Assert.ThrowsAsync<QueryException>(() => session.ImportAsync([Document()], Token))).Code);
        Assert.DoesNotContain("SYNTHETIC-PRIVATE", new QueryRow([QueryCell.From("SYNTHETIC-PRIVATE")]).ToString());
        Assert.DoesNotContain("SYNTHETIC-PRIVATE", QueryCell.From("SYNTHETIC-PRIVATE").ToString());
        Assert.DoesNotContain("SYNTHETIC-PRIVATE", new QueryParameter("@SYNTHETIC-PRIVATE", QueryCell.From("SYNTHETIC-PRIVATE")).ToString());
    }
    [Fact]
    public async Task WorkerCrashIsContainedAndFreshWorkersHaveDifferentProcesses()
    {
        await using var session = await Start(); using (var process = Process.GetProcessById(session.ProcessId)) { process.Kill(true); await process.WaitForExitAsync(Token); }
        Assert.Equal(QueryError.WorkerStopped, (await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync("SELECT 1", token: Token))).Code);
        await using var fresh = await Start(); Assert.NotEqual(session.ProcessId, fresh.ProcessId);
        Assert.Single((await fresh.ExecuteAsync("SELECT 1", token: Token)).Rows);
    }
    [Fact]
    public async Task TextTransportPreservesEmbeddedNulQuotesNewlinesAndEmptyValues()
    {
        await using var session = await Start();
        const string literal = "synthetic\0middle,\"quoted\"\r\nλ";
        var result = await session.ExecuteAsync("SELECT @v AS V, @empty AS Empty", [new("@v", QueryCell.From(literal)), new("@empty", QueryCell.From(""))], Token);
        Assert.Equal(literal, result.Rows[0].Cells[0].Text); Assert.Equal("", result.Rows[0].Cells[1].Text);
    }
    [Fact]
    public async Task NativeHeapExhaustionFailsWithinWorkerAndDoesNotAffectFreshWorkers()
    {
        await using (var session = await Start())
        {
            var id = session.ProcessId;
            var error = await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync("""
                WITH RECURSIVE n(x) AS (VALUES(1) UNION ALL SELECT x+1 FROM n WHERE x<20000)
                SELECT sum(length(v)) FROM (SELECT @payload || x AS v FROM n ORDER BY v LIMIT -1)
                """, [new("@payload", QueryCell.From(new string('S', 16384)))], Token));
            Assert.Equal(QueryError.ResourceLimit, error.Code); Assert.True(Exited(id));
        }
        await using var fresh = await Start(); Assert.Single((await fresh.ExecuteAsync("SELECT 1", token: Token)).Rows);
    }
    [Fact]
    public async Task IdleSessionDeadlineReapsReadyWorkerAndCanRecover()
    {
        var clock = new ManualDeadlineClock();
        var session = await QueryWorkerSession.StartCoreAsync(Host, null, new() { SessionTimeout = TimeSpan.FromSeconds(1) },
            Token, null, null, clock, TimeProvider.System);
        var id = session.ProcessId;
        try
        {
            Assert.Equal("3.53.4", session.EngineVersion); Assert.Equal(TimeSpan.FromSeconds(1), clock.DueTime);
            using var worker = Process.GetProcessById(id);
            // The total lifetime deliberately includes startup. Advance only after
            // Ready, so CI scheduling cannot spend a real 1 s cap during setup.
            clock.Expire(); await worker.WaitForExitAsync(Token).WaitAsync(TimeSpan.FromSeconds(5), Token);
            Assert.True(Exited(id));
            Assert.Equal(QueryError.Timeout, (await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync("SELECT 1", token: Token))).Code);
        }
        finally { await session.DisposeAsync(); }
        Assert.True(clock.TimerDisposed);
        await using var recovery = await Start(); Assert.Single((await recovery.ExecuteAsync("SELECT 1", token: Token)).Rows);
    }
    [Fact]
    public async Task ExplicitDisposalReapsReadyWorkerAndIsIdempotent()
    {
        var other = await Start(); var otherId = other.ProcessId; await other.DisposeAsync(); await other.DisposeAsync(); Assert.True(Exited(otherId));
    }
    [Fact]
    public async Task ImportCancellationNeverExposesABatchOrKeepsAWorkerAfterDisposal()
    {
        var session = await Start(); var id = session.ProcessId;
        try
        {
            await session.ImportAsync([Document()], Token);
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token); cancel.CancelAfter(100);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            {
                for (var start = 1; start < 100_000; start += 100)
                    await session.ImportAsync(Enumerable.Range(start, 100).Select(n => Message(n)).ToArray(), cancel.Token);
            });
        }
        finally { await session.DisposeAsync(); }
        Assert.True(Exited(id));
    }
    [Fact]
    public async Task OversizedSqlDeepExpressionsAndOversizedNativeValuesAreBounded()
    {
        await using (var session = await Start())
            Assert.Equal(QueryError.InvalidRequest, (await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync(new string(' ', QueryLimits.MaximumSqlCharacters + 1), token: Token))).Code);
        await using (var deep = await Start())
            Assert.Equal(QueryError.InvalidSql, (await Assert.ThrowsAsync<QueryException>(() => deep.ExecuteAsync("SELECT " + string.Concat(Enumerable.Repeat("abs(", 100)) + "1" + new string(')', 100), token: Token))).Code);
        await using var large = await Start();
        var error = await Assert.ThrowsAsync<QueryException>(() => large.ExecuteAsync("SELECT @p || @p || @p || @p || @p || @p || @p || @p || @p || @p || @p || @p || @p || @p || @p || @p || @p",
            [new("@p", QueryCell.From(new string('S', QueryLimits.MaximumValueCharacters)))], Token));
        Assert.Equal(QueryError.ResourceLimit, error.Code);
    }
    [Theory]
    [InlineData("SELECT CAST(X'FF' AS TEXT)")]
    [InlineData("SELECT 1e999")]
    public async Task InvalidUtf8AndNonfiniteScalarsFailWithoutLossyConversion(string sql)
    {
        await using var session = await Start();
        Assert.Equal(QueryError.InvalidValue, (await Assert.ThrowsAsync<QueryException>(() => session.ExecuteAsync(sql, token: Token))).Code);
    }
}
