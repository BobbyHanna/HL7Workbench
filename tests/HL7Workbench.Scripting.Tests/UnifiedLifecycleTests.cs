using System.Text;
using System.Text.Json;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Scripting.Tests;

public sealed class UnifiedLifecycleTests
{
    private static string Host => Path.Combine(AppContext.BaseDirectory, "ScriptHost", "HL7Workbench.ScriptHost.dll");
    private static byte[] Input => Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rPID|1||SYN-001||Example^Ada\r");
    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task InitializationHelpersSharedVariablesMainLocalsAndCompletionUseOneInstancePerRun()
    {
        const string source = """
            int initialized = 0;
            initialized++;
            int shared = 0;
            int completed = 0;
            var report = Batch.Report("counts", "Initialized", "Processed", "Completed");
            int Next() => ++shared;
            void Main()
            {
                int local = 0;
                local++;
                Message.SetText("PID-5.2", $"{initialized}:{Next()}:{local}");
            }
            void Complete()
            {
                completed++;
                report.Row(initialized.ToString(), shared.ToString(), completed.ToString());
            }
            """;
        for (var run = 0; run < 2; run++)
        {
            await using var worker = await ScriptWorkerSession.StartAsync(Host, new(source), token: Token);
            Assert.Single(worker.DrainReports());
            for (var n = 1; n <= 3; n++)
            {
                var result = await worker.ProcessAsync(Input, 65001, n, Token);
                Assert.Equal($"1:{n}:1", Hl7Parser.Parse(result.Bytes!).Get("PID-5.2").Raw);
                Assert.Empty(worker.DrainReports());
            }
            await worker.CompleteAsync(Token);
            Assert.Equal("\"1\",\"3\",\"1\"\r\n", Encoding.UTF8.GetString(Assert.Single(worker.DrainReports()).Bytes));
            await Assert.ThrowsAsync<InvalidOperationException>(() => worker.CompleteAsync(Token));
        }
    }

    [Fact]
    public async Task HelpersResolveTheCurrentMessageAndCompletionIsOptional()
    {
        await using var worker = await ScriptWorkerSession.StartAsync(Host, new("string Name() => Message.Text(\"PID-5.2\"); void Main() { Message.SetText(\"PID-5.1\", Name()); }"), token: Token);
        foreach (var name in new[] { "SyntheticOne", "SyntheticTwo" })
        {
            var bytes = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(Input).Replace("Ada", name, StringComparison.Ordinal));
            var result = await worker.ProcessAsync(bytes, 65001, 1, Token);
            Assert.Equal(name, Hl7Parser.Parse(result.Bytes!).Get("PID-5.1").Raw);
        }
        await worker.CompleteAsync(Token); Assert.Empty(worker.DrainReports());
    }

    [Theory]
    [InlineData("// void Main() {}", "MissingMain")]
    [InlineData("class Helper { public void Main() {} }", "MissingMain")]
    [InlineData("Action Main = () => {};", "EntryPointSignature")]
    [InlineData("int Main() => 1;", "EntryPointSignature")]
    [InlineData("static void Main() {}", "EntryPointSignature")]
    [InlineData("void Main(int value) {}", "EntryPointSignature")]
    [InlineData("void Main<T>() {}", "EntryPointSignature")]
    [InlineData("async void Main() { await System.Threading.Tasks.Task.Yield(); }", "EntryPointSignature")]
    [InlineData("System.Threading.Tasks.Task Main() => System.Threading.Tasks.Task.CompletedTask;", "EntryPointSignature")]
    [InlineData("void Main() {} void Main(int n) {}", "AmbiguousEntryPoint")]
    [InlineData("void Main() {} void Main() {}", "CS0111")]
    [InlineData("void Main() {} int Complete() => 0;", "EntryPointSignature")]
    [InlineData("void Main() {} async void Complete() { await System.Threading.Tasks.Task.Yield(); }", "EntryPointSignature")]
    [InlineData("void Main() {} void Complete() {} void Complete(int n) {}", "AmbiguousEntryPoint")]
    [InlineData("void Main() {} Action Complete = () => {};", "EntryPointSignature")]
    [InlineData("Message.Exclude();", "MissingMain")]
    public async Task InvalidOrObsoleteEntryPointsFailBeforeInitialization(string source, string code)
    {
        var error = await Assert.ThrowsAsync<ScriptRunException>(() => ScriptWorkerSession.StartAsync(Host, new(source), token: Token));
        Assert.Contains(error.Diagnostics, d => d.Code == code && d.Line > 0);
        Assert.DoesNotContain("Message.Exclude", error.Message);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task InitializationOrProcessingFailureNeverCallsCompletion(bool initialization)
    {
        var marker = Path.Combine(Path.GetTempPath(), "hl7-synthetic-completion-" + Guid.NewGuid().ToString("N"));
        var source = (initialization ? "throw new Exception();\n" : "") + "void Main() { throw new Exception(); }\n" +
            "void Complete() { System.IO.File.WriteAllText(" + JsonSerializer.Serialize(marker) + ", \"synthetic\"); }";
        try
        {
            if (initialization) await Assert.ThrowsAsync<ScriptRunException>(() => ScriptWorkerSession.StartAsync(Host, new(source), token: Token));
            else
            {
                await using var worker = await ScriptWorkerSession.StartAsync(Host, new(source), token: Token);
                await Assert.ThrowsAsync<ScriptRunException>(() => worker.ProcessAsync(Input, 65001, 1, Token));
            }
            Assert.False(File.Exists(marker));
        }
        finally { File.Delete(marker); }
    }

    [Theory]
    [InlineData("var bad = Message.Read(\"PID-3\"); void Main() {}", true)]
    [InlineData("void Main() {} void Complete() { Message.Exclude(); }", false)]
    public async Task MessageOutsideMainFailsWithSafePhaseDiagnostic(string source, bool initialize)
    {
        ScriptRunException failure;
        if (initialize) failure = await Assert.ThrowsAsync<ScriptRunException>(() => ScriptWorkerSession.StartAsync(Host, new(source), token: Token));
        else
        {
            await using var worker = await ScriptWorkerSession.StartAsync(Host, new(source), token: Token);
            await worker.ProcessAsync(Input, 65001, 1, Token);
            failure = await Assert.ThrowsAsync<ScriptRunException>(() => worker.CompleteAsync(Token));
        }
        Assert.Contains(failure.Diagnostics, d => d.Code == "LifecyclePhase" && d.Line == 1);
    }

    [Fact]
    public async Task CompilerAndHelperRuntimeLocationsReferToOriginalSource()
    {
        var compile = await Assert.ThrowsAsync<ScriptRunException>(() => ScriptWorkerSession.StartAsync(Host, new("// synthetic\nvoid Main()\n{\n var invalid = ;\n}"), token: Token));
        Assert.Contains(compile.Diagnostics, d => d.Code == "CS1525" && d.Line == 4);
        await using var worker = await ScriptWorkerSession.StartAsync(Host, new("// synthetic\nvoid Helper() { throw new Exception(\"SYNTHETIC-PRIVATE\"); }\nvoid Main() { Helper(); }"), token: Token);
        var runtime = await Assert.ThrowsAsync<ScriptRunException>(() => worker.ProcessAsync(Input, 65001, 7, Token));
        Assert.Contains(runtime.Diagnostics, d => d.Code == "RuntimeFailed" && d.Line == 2 && d.MessageNumber == 7);
        Assert.DoesNotContain("PRIVATE", runtime.Message);
    }
}
