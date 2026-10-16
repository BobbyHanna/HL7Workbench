using System.Diagnostics;
using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Scripting.Tests;

public sealed class BatchWorkerTests
{
    private static string Host => Path.Combine(AppContext.BaseDirectory, "ScriptHost", "HL7Workbench.ScriptHost.dll");
    private static byte[] Input => Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rPID|1||SYN-001||Example^Ada\r");
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Fact]
    public async Task CompletionCanChooseHeldCopiesUsingFinalStateWithoutRetainingFacades()
    {
        const string script = """
            var held = new List<ScriptDeferredMessage>();
            void Main()
            {
                var message = Message;
                message.SetText("PID-5.2", Batch.MessageNumber.ToString());
                held.Add(Batch.Hold(message));
                message.SetText("PID-5.2", "LATER");
                message.Exclude();
            }

            void Complete()
            {
                if (held.Count == 2)
                {
                    Batch.Emit(held[1]);
                    Batch.Emit(held[0]);
                }
            }
            """;
        await using var worker = await ScriptWorkerSession.StartAsync(Host, new(script), token: Token);
        foreach (var number in new[] { 1, 2 }) Assert.True((await worker.ProcessAsync(Input, 65001, number, Token)).Excluded);
        await worker.CompleteAsync(Token);
        foreach (var number in new[] { 2, 1 })
        {
            var copy = (await worker.TakeDeferredAsync(Token))!.Value;
            Assert.Equal(number, copy.MessageNumber); Assert.Equal(number.ToString(), Hl7Parser.Parse(copy.Bytes).Get("PID-5.2").Raw);
        }
    }
    [Fact]
    public async Task LifecycleSharesCapturedLocalsInOrderAndEachRunStartsFresh()
    {
        const string script = """
            var count = 0;
            var order = new List<int>();
            var report = Batch.Report("summary", "Order", "Count");
            void Main()
            {
                var message = Message;
                count++;
                order.Add(Batch.MessageNumber);
                message.SetText("PID-5.2", count.ToString());
            }

            void Complete()
            {
                if (Batch.MessageNumber != 0)
                    throw new Exception();
                report.Row(string.Join(";", order), count.ToString());
            }
            """;
        for (var run = 0; run < 2; run++)
        {
            await using var worker = await ScriptWorkerSession.StartAsync(Host, new(script), token: Token);
            Assert.Contains("Order", Encoding.UTF8.GetString(Assert.Single(worker.DrainReports()).Bytes));
            foreach (var number in new[] { 2, 5, 7 })
            {
                var result = await worker.ProcessAsync(Input, 65001, number, Token);
                Assert.Equal(Array.IndexOf(new[] { 2, 5, 7 }, number) + 1, int.Parse(Hl7Parser.Parse(result.Bytes!).Get("PID-5.2").Raw!));
                Assert.Empty(worker.DrainReports());
            }
            await worker.CompleteAsync(Token);
            Assert.Equal("\"2;5;7\",\"3\"\r\n", Encoding.UTF8.GetString(Assert.Single(worker.DrainReports()).Bytes));
        }
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(28591)]
    public async Task BatchNoOpPreservesExactBytesAndDeferredCopiesAreBoundedSnapshots(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes("MSH|^~\\&|SYN\r\nPID|1||SYN-001||Example^Ada\nopaque é||")).ToArray();
        await using var worker = await ScriptWorkerSession.StartAsync(Host, new("""
            void Main()
            {
                var message = Message;
                Batch.Defer(message);
            }
            """), token: Token);
        Assert.Empty(worker.DrainReports());
        Assert.Equal(bytes, (await worker.ProcessAsync(bytes, codePage, 9, Token)).Bytes);
        await worker.CompleteAsync(Token);
        var copy = await worker.TakeDeferredAsync(Token);
        Assert.Equal(9, copy!.Value.MessageNumber); Assert.Equal(bytes, copy.Value.Bytes);
        Assert.Null(await worker.TakeDeferredAsync(Token));
    }
    [Theory]
    [InlineData("""
        void Main()
        {
            var message = Message;
            message.Exclude();
        }
        """)]
    [InlineData("""
        void Main()
        {
            var message = Message;
            Batch.Defer(message);
            message.Exclude();
        }
        """)]
    public async Task ExclusionDoesNotImplicitlyIncludeOriginal(string script)
    {
        await using var worker = await ScriptWorkerSession.StartAsync(Host, new(script), token: Token);
        var result = await worker.ProcessAsync(Input, 65001, 1, Token);
        Assert.True(result.Excluded); Assert.Null(result.Bytes);
        await worker.CompleteAsync(Token);
    }
    [Theory]
    [InlineData("var count = 0;", "MissingMain")]
    [InlineData("""
        async void Main()
        {
            var message = Message;
            await System.Threading.Tasks.Task.Delay(1);
        }
        """, "EntryPointSignature")]
    [InlineData("""
        void Main()
        {
            var message = Message;
        }

        async void Complete()
        {
            await System.Threading.Tasks.Task.Delay(1);
        }
        """, "EntryPointSignature")]
    [InlineData("throw new Exception(\"SYNTHETIC-PRIVATE\"); void Main() {}", "RuntimeFailed")]
    public async Task InitializationFailuresAreSafeAndDoNotLeaveAUsableWorker(string script, string code)
    {
        var error = await Assert.ThrowsAsync<ScriptRunException>(() => ScriptWorkerSession.StartAsync(Host, new(script), token: Token));
        Assert.Contains(error.Diagnostics, d => d.Code == code); Assert.DoesNotContain("PRIVATE", error.Message);
    }
    [Theory]
    [InlineData("""
        void Main()
        {
            var message = Message;
            throw new Exception(message.Text("PID-3"));
        }
        """, false)]
    [InlineData("""
        void Main()
        {
            var message = Message;
        }

        void Complete()
        {
            throw new Exception("SYNTHETIC-PRIVATE");
        }
        """, true)]
    [InlineData("""
        void Main()
        {
            var message = Message;
            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Execution, message.Text("PID-3"), Path: "SYNTHETIC-PRIVATE"));
        }
        """, false)]
    public async Task ProcessAndCompletionExceptionsOmitImportedText(string script, bool complete)
    {
        await using var worker = await ScriptWorkerSession.StartAsync(Host, new(script), token: Token);
        ScriptRunException error;
        if (complete) { await worker.ProcessAsync(Input, 65001, 1, Token); error = await Assert.ThrowsAsync<ScriptRunException>(() => worker.CompleteAsync(Token)); }
        else error = await Assert.ThrowsAsync<ScriptRunException>(() => worker.ProcessAsync(Input, 65001, 1, Token));
        Assert.DoesNotContain("SYN-001", error.Message); Assert.DoesNotContain("PRIVATE", error.Message);
        Assert.Contains(error.Diagnostics, d => d.Code == "RuntimeFailed" && d.Line > 0);
    }
    [Theory]
    [InlineData("while (true) {} void Main() {}", "initialize")]
    [InlineData("""
        void Main()
        {
            var message = Message;
            while (true)
            {
            }
        }
        """, "process")]
    [InlineData("""
        void Main()
        {
            var message = Message;
        }

        void Complete()
        {
            while (true)
            {
            }
        }
        """, "complete")]
    public async Task EveryLifecyclePhaseHasADeadline(string script, string phase)
    {
        var options = new ScriptWorkerOptions { MessageTimeout = TimeSpan.FromMilliseconds(500) };
        if (phase == "initialize")
        {
            var failure = await Assert.ThrowsAsync<ScriptRunException>(() => ScriptWorkerSession.StartAsync(Host, new(script), options, Token));
            Assert.Contains(failure.Diagnostics, d => d.Code == "Timeout"); return;
        }
        await using var worker = await ScriptWorkerSession.StartAsync(Host, new(script), options, Token);
        var id = worker.ProcessId;
        ScriptRunException error;
        if (phase == "complete") { await worker.ProcessAsync(Input, 65001, 1, Token); error = await Assert.ThrowsAsync<ScriptRunException>(() => worker.CompleteAsync(Token)); }
        else error = await Assert.ThrowsAsync<ScriptRunException>(() => worker.ProcessAsync(Input, 65001, 1, Token));
        Assert.Contains(error.Diagnostics, d => d.Code == "Timeout");
        Assert.Throws<ArgumentException>(() => Process.GetProcessById(id));
    }
    [Theory]
    [InlineData("""
        void Main()
        {
            var message = Message;
            Environment.Exit(17);
        }
        """, false)]
    [InlineData("""
        void Main()
        {
            var message = Message;
        }

        void Complete()
        {
            Environment.Exit(17);
        }
        """, true)]
    public async Task CrashesInCallbacksAreSafe(string script, bool complete)
    {
        await using var worker = await ScriptWorkerSession.StartAsync(Host, new(script), token: Token);
        if (complete) await worker.ProcessAsync(Input, 65001, 1, Token);
        var error = await Assert.ThrowsAsync<ScriptRunException>(() => complete ? worker.CompleteAsync(Token) : worker.ProcessAsync(Input, 65001, 1, Token));
        Assert.Contains(error.Diagnostics, d => d.Code == "WorkerStopped");
    }
    [Fact]
    public async Task RetainedArbitraryStateIsContainedByWindowsProcessMemoryLimit()
    {
        if (!OperatingSystem.IsWindows()) return;
        const string script = """
            void Main()
            {
                var message = Message;
                var state = new List<byte[]>();
                while (true)
                    state.Add(new byte[16 * 1024 * 1024]);
            }
            """;
        await using var worker = await ScriptWorkerSession.StartAsync(Host, new(script), new() { MemoryLimitBytes = 256L * 1024 * 1024 }, Token);
        var error = await Assert.ThrowsAsync<ScriptRunException>(() => worker.ProcessAsync(Input, 65001, 1, Token));
        Assert.Contains(error.Diagnostics, d => d.Code is "WorkerStopped" or "RuntimeFailed");
    }
}
