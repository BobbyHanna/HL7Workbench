using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace HL7Workbench.Querying.Tests;

public sealed class QueryProtocolTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData(-1)] [InlineData(0)] [InlineData(QueryLimits.MaximumFrameBytes + 1)]
    public async Task OversizedAndInvalidFramesAreRejectedBeforePayloadAllocation(int length)
    {
        var bytes = new byte[4]; BinaryPrimitives.WriteInt32LittleEndian(bytes, length);
        using var stream = new MemoryStream(bytes);
        Assert.Equal(QueryError.ProtocolError, (await Assert.ThrowsAsync<QueryException>(() => QueryProtocol.ReadAsync(stream, Token))).Code);
    }
    [Theory]
    [InlineData("{\"Kind\":1,\"Version\":1}")]
    [InlineData("{\"Kind\":1234}")]
    [InlineData("{\"Kind\":9,\"Error\":1234}")]
    public async Task UnsupportedVersionsKindsAndErrorCodesAreRejected(string json)
    {
        using var stream = new MemoryStream(); var payload = Encoding.UTF8.GetBytes(json);
        stream.Write(BitConverter.GetBytes(payload.Length)); stream.Write(payload); stream.Position = 0;
        Assert.Equal(QueryError.ProtocolError, (await Assert.ThrowsAsync<QueryException>(() => QueryProtocol.ReadAsync(stream, Token))).Code);
    }
    [Fact]
    public void ArgumentsNeverContainSqlOrValuesAndDllUsesAdjacentApphost()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "QueryHost", "HL7Workbench.QueryHost.dll");
        var info = QueryWorkerSession.StartInfo(executable, "hl7-query-synthetic-test");
        Assert.False(info.UseShellExecute); Assert.True(info.CreateNoWindow); Assert.True(info.RedirectStandardOutput); Assert.True(info.RedirectStandardError);
        Assert.Equal("hl7-query-synthetic-test", Assert.Single(info.ArgumentList)); Assert.EndsWith(OperatingSystem.IsWindows() ? ".exe" : "HL7Workbench.QueryHost", info.FileName);
        Assert.DoesNotContain(typeof(QueryWorkerSession).Assembly.GetReferencedAssemblies(), n => n.Name!.StartsWith("SQLite", StringComparison.Ordinal));
    }
    [Fact]
    public void BudgetHardCapsCannotBeRaisedAndScalarShapesAreValidated()
    {
        Assert.Throws<QueryException>(() => new QueryBudgets { ResultRows = int.MaxValue }.Validate());
        Assert.Throws<QueryException>(() => new QueryBudgets { QueryMilliseconds = 30_001 }.Validate());
        Assert.Throws<QueryException>(() => new QueryWorkerOptions { OperationTimeout = TimeSpan.FromHours(1) }.Validate());
        Assert.Throws<QueryException>(() => new QueryCell(QueryValueKind.Text).Size());
        Assert.Throws<QueryException>(() => new QueryCell(QueryValueKind.Null, Text: "SYN").Size());
        Assert.Throws<QueryException>(() => QueryCell.From(double.NaN).Size());
        Assert.Throws<QueryException>(() => QueryCell.From("\ud800").Size());
    }
    [Fact]
    public async Task OutgoingFramesAreBoundedBeforeWritingAnyPipeBytes()
    {
        using var destination = new MemoryStream();
        var frame = new QueryFrame(QueryFrameKind.Execute, Sql: new string('<', QueryLimits.MaximumFrameBytes));
        Assert.Equal(QueryError.MaterializationLimit, (await Assert.ThrowsAsync<QueryException>(() => QueryProtocol.WriteAsync(destination, frame, Token))).Code);
        Assert.Equal(0, destination.Length);
        Assert.DoesNotContain("SYNTHETIC-PRIVATE", new QueryFrame(QueryFrameKind.Execute, Sql: "SYNTHETIC-PRIVATE").ToString());
    }
}
