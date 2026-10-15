using System.Diagnostics;
using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Scripting.Tests;

public sealed class WorkerTests
{
    private static string Host => Path.Combine(AppContext.BaseDirectory, "ScriptHost", "HL7Workbench.ScriptHost.dll");
    private const string Synthetic = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261015120000||ADT^A01|SYN-1|T|2.5\rPID|1||SYN-001^^^TEST^MR||Example^Ada\r";
    private static byte[] Bytes(string text = Synthetic) => Encoding.UTF8.GetBytes(text);
    private static bool Exited(int id)
    {
        try { using var p = Process.GetProcessById(id); return p.HasExited; }
        catch (ArgumentException) { return true; }
    }
    [Fact]
    public async Task ControlledFacadeReadsEditsClearsAndReusesCompiledRunnerWithFreshLocals()
    {
        await using var session = await ScriptWorkerSession.StartAsync(Host, new("""
            int invocationLocal = 0;
            invocationLocal++;
            if (invocationLocal != 1 || Message.Read("PID-3.1").Raw != "SYN-001") throw new Exception();
            Message.SetText("PID-5.2", "Synthetic|Name");
            Message.SetNull("PID-5.3");
            Message.Clear("PID-19");
            foreach (var pid in Message.Segments("PID")) pid.Clear("3.1");
            """), token: TestContext.Current.CancellationToken);
        for (var i = 1; i <= 2; i++)
        {
            var result = await session.ProcessAsync(Bytes(), 65001, i, TestContext.Current.CancellationToken);
            var parsed = Hl7Parser.Parse(result.Bytes!);
            Assert.Equal("Synthetic|Name", parsed.GetDecoded("PID-5.2").Text);
            Assert.Equal(Hl7ValueState.ExplicitNull, parsed.Get("PID-5.3").State);
            Assert.Equal(Hl7ValueState.Empty, parsed.Get("PID-3.1").State);
            Assert.Equal(Hl7ValueState.Missing, parsed.Get("PID-19").State);
        }
        await session.CompleteAsync(TestContext.Current.CancellationToken);
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(12000)] [InlineData(28591)]
    public async Task NoOpPreservesBytesBomMalformedLinesMixedEndingsAndTrailingDelimiters(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage);
        var text = Synthetic.Replace("\rPID", "\r\nPID", StringComparison.Ordinal) + "opaque malformed\nZZZ|||é";
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        await using var session = await ScriptWorkerSession.StartAsync(Host, new("// preserve"), token: TestContext.Current.CancellationToken);
        Assert.Equal(bytes, (await session.ProcessAsync(bytes, codePage, 1, TestContext.Current.CancellationToken)).Bytes);
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(28591)]
    public async Task CustomDelimiterControlledEditPreservesUnchangedSpelling(int codePage)
    {
        const string text = "MSH*$%!@*SYN\r\nPID*1***Example$Ada\nZZZ***tail";
        var encoding = Encoding.GetEncoding(codePage);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        await using var session = await ScriptWorkerSession.StartAsync(Host, new("Message.SetText(\"PID-4.2\", \"SYN*VALUE\");"), token: TestContext.Current.CancellationToken);
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace("Ada", "SYN!F!VALUE", StringComparison.Ordinal))).ToArray(),
            (await session.ProcessAsync(bytes, codePage, 1, TestContext.Current.CancellationToken)).Bytes);
    }
    [Fact]
    public async Task ExclusionAndConsoleOutputDoNotPolluteProtocol()
    {
        await using var session = await ScriptWorkerSession.StartAsync(Host, new("Console.WriteLine(\"SYNTHETIC PRIVATE\"); Console.Error.WriteLine(\"PRIVATE\"); Message.Exclude();"), token: TestContext.Current.CancellationToken);
        var result = await session.ProcessAsync(Bytes(), 65001, 1, TestContext.Current.CancellationToken);
        Assert.True(result.Excluded); Assert.Null(result.Bytes);
    }
    [Theory]
    [InlineData("var invalid = ;", "CS1525")]
    [InlineData("#r \"SYNTHETIC-PRIVATE.dll\"", "DirectiveNotSupported")]
    [InlineData("#load \"SYNTHETIC-PRIVATE.csx\"", "DirectiveNotSupported")]
    [InlineData("#line 123 \"PRIVATE\"\nthrow new Exception();", "DirectiveNotSupported")]
    public async Task CompilationDiagnosticsAreLocationsAndCodesWithoutSourceOrPaths(string source, string code)
    {
        var failure = await Assert.ThrowsAsync<ScriptRunException>(() => ScriptWorkerSession.StartAsync(Host, new(source), token: TestContext.Current.CancellationToken));
        Assert.Contains(failure.Diagnostics, d => d.Code == code);
        Assert.DoesNotContain("PRIVATE", string.Join("", failure.Diagnostics));
    }
    [Theory]
    [InlineData("throw new Exception(Message.Text(\"PID-3.1\"));", "RuntimeFailed")]
    [InlineData("Message.SetText(\"MSH-1\", \"X\");", "EditRejected")]
    [InlineData("Message.Text(\"PID-99\");", "MissingValue")]
    [InlineData("Message.Read(\"SYNTHETIC-PRIVATE\");", "InvalidPath")]
    public async Task RuntimeErrorsNeverDiscloseImportedValues(string script, string code)
    {
        await using var session = await ScriptWorkerSession.StartAsync(Host, new(script), token: TestContext.Current.CancellationToken);
        var failure = await Assert.ThrowsAsync<ScriptRunException>(() => session.ProcessAsync(Bytes(), 65001, 7, TestContext.Current.CancellationToken));
        Assert.Contains(failure.Diagnostics, d => d.Code == code && d.MessageNumber == 7);
        Assert.DoesNotContain("SYN-001", string.Join("", failure.Diagnostics));
        Assert.DoesNotContain("PRIVATE", string.Join("", failure.Diagnostics));
    }
    [Theory]
    [InlineData("while (true) { Cancellation.ThrowIfCancellationRequested(); }")]
    [InlineData("while (true) { }")]
    public async Task CancellationReapsCooperativeAndNoncooperativeWorkers(string script)
    {
        var session = await ScriptWorkerSession.StartAsync(Host, new(script), token: TestContext.Current.CancellationToken);
        var id = session.ProcessId;
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(400));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ProcessAsync(Bytes(), 65001, 1, cancel.Token));
        await session.DisposeAsync(); Assert.True(Exited(id));
    }
    [Fact]
    public async Task TimeoutReapsInfiniteLoop()
    {
        var session = await ScriptWorkerSession.StartAsync(Host, new("while (true) { }"), new() { MessageTimeout = TimeSpan.FromMilliseconds(350) }, TestContext.Current.CancellationToken);
        var id = session.ProcessId;
        var failure = await Assert.ThrowsAsync<ScriptRunException>(() => session.ProcessAsync(Bytes(), 65001, 1, TestContext.Current.CancellationToken));
        Assert.Contains(failure.Diagnostics, d => d.Code == "Timeout");
        await session.DisposeAsync(); Assert.True(Exited(id));
    }
    [Fact]
    public async Task CrashIsSafeAndFreshJobsUseDifferentProcesses()
    {
        var first = await ScriptWorkerSession.StartAsync(Host, new("Environment.Exit(23);"), token: TestContext.Current.CancellationToken);
        var id = first.ProcessId;
        var failure = await Assert.ThrowsAsync<ScriptRunException>(() => first.ProcessAsync(Bytes(), 65001, 1, TestContext.Current.CancellationToken));
        Assert.Contains(failure.Diagnostics, d => d.Code == "WorkerStopped");
        await first.DisposeAsync(); Assert.True(Exited(id));
        await using var second = await ScriptWorkerSession.StartAsync(Host, new(""), token: TestContext.Current.CancellationToken);
        Assert.NotEqual(id, second.ProcessId);
    }
    [Fact]
    public async Task OversizedAndUndecodableMessagesFailExplicitly()
    {
        foreach (var bytes in new[] { Bytes(Synthetic + new string('X', ScriptLimits.LineCharacters + 1)), new byte[] { 0xFF } })
        {
            await using var session = await ScriptWorkerSession.StartAsync(Host, new(""), token: TestContext.Current.CancellationToken);
            var failure = await Assert.ThrowsAsync<ScriptRunException>(() => session.ProcessAsync(bytes, 65001, 1, TestContext.Current.CancellationToken));
            Assert.Contains(failure.Diagnostics, d => d.Code is "InputLimit" or "InvalidEncoding");
        }
    }
    [Fact]
    public async Task FutureModesAndOversizedScriptsAreRejectedBeforeLaunch()
    {
        foreach (var definition in new[] { new ScriptDefinition("", ScriptMode.Batch), new("", ScriptMode.Generate), new(new string(' ', ScriptLimits.ScriptCharacters + 1)) })
            await Assert.ThrowsAsync<ScriptRunException>(() => ScriptWorkerSession.StartAsync("unavailable", definition, token: TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task ProtocolRejectsOversizedFrameBeforePayloadRead()
    {
        using var stream = new MemoryStream(BitConverter.GetBytes(ScriptProtocol.MaxFrameBytes + 1));
        await Assert.ThrowsAsync<InvalidDataException>(() => ScriptProtocol.ReadAsync(stream, TestContext.Current.CancellationToken));
    }
    [Fact]
    public async Task RuntimeDiagnosticsRetainOriginalScriptLineAndDeploymentContainsWorkerDependencies()
    {
        Assert.True(File.Exists(Path.ChangeExtension(Host, ".runtimeconfig.json")));
        Assert.True(File.Exists(Path.Combine(Path.GetDirectoryName(Host)!, "Microsoft.CodeAnalysis.CSharp.Scripting.dll")));
        await using var session = await ScriptWorkerSession.StartAsync(Host, new("// synthetic failure\nthrow new Exception(\"SYNTHETIC-PRIVATE\");"), token: TestContext.Current.CancellationToken);
        var failure = await Assert.ThrowsAsync<ScriptRunException>(() => session.ProcessAsync(Bytes(), 65001, 1, TestContext.Current.CancellationToken));
        Assert.Contains(failure.Diagnostics, d => d.Code == "RuntimeFailed" && d.Line == 2);
        Assert.DoesNotContain(AppDomain.CurrentDomain.GetAssemblies(), a => a.GetName().Name == "Microsoft.CodeAnalysis.CSharp.Scripting");
    }
    [Fact]
    public async Task MissingWorkerFailsBeforeWaitingForAConnection()
    {
        var failure = await Assert.ThrowsAsync<ScriptRunException>(() => ScriptWorkerSession.StartAsync("missing-worker.dll", new(""), token: TestContext.Current.CancellationToken));
        Assert.Contains(failure.Diagnostics, d => d.Code == "WorkerUnavailable");
    }
}
