using System.Text;
using HL7Workbench.Scripting;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class ScriptExecutionTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task OrdinaryAndGeneratedHl7SaveAsRequireConsentAndKeepIdentityOnFailure(bool generated)
    {
        await using var s = new Session(); Directory.CreateDirectory(s.Root);
        var sourcePath = Path.Combine(s.Root, "source.hl7"); await File.WriteAllTextAsync(sourcePath, First, Token);
        await using var input = File.OpenRead(sourcePath);
        var source = await MessageFileReader.OpenFileAsync(s.Manager, "Synthetic.hl7", input, Token, localPath: sourcePath); var original = All(source);
        var document = generated ? (await s.Run(source, "Message.SetText(\"PID-5.2\", \"SYNTHETIC\");")).Document : source;
        if (!generated) { document.IsEditMode = true; document.ReplaceWorkingText(First.Replace("Ada", "SYNTHETIC", StringComparison.Ordinal)); await document.EditRefresh; }
        var title = document.Title; var id = document.Id; var initialPath = document.SavePath; var path = Path.Combine(s.Root, "existing.hl7");
        await File.WriteAllTextAsync(path, "SYNTHETIC OLD", Token);
        await Assert.ThrowsAnyAsync<IOException>(() => document.SaveAsync(path, Token));
        Assert.Equal("SYNTHETIC OLD", await File.ReadAllTextAsync(path, Token));
        var stale = await FileSaveDestination.CaptureAsync(path, Token);
        await File.WriteAllTextAsync(path, "SYNTHETIC CONCURRENT", Token);
        await Assert.ThrowsAsync<IOException>(() => document.SaveAsync(stale, Token));
        Assert.True(document.IsDirty); Assert.Equal(initialPath, document.SavePath); Assert.Equal(title, document.Title); Assert.Equal(id, document.Id);
        Assert.Equal("SYNTHETIC CONCURRENT", await File.ReadAllTextAsync(path, Token));
        await document.SaveAsync(await FileSaveDestination.CaptureAsync(path, Token), Token);
        Assert.False(document.IsDirty); Assert.Equal(path, document.SavePath); Assert.Equal(id, document.Id);
        Assert.Contains("SYNTHETIC", await File.ReadAllTextAsync(path, Token)); Assert.Empty(Directory.GetFiles(s.Root, "*.tmp"));
        Assert.Equal(original, All(source)); Assert.Equal(original, await File.ReadAllBytesAsync(sourcePath, Token));
        if (generated) { Assert.False(source.IsDirty); Assert.Equal(sourcePath, source.SavePath); Assert.NotSame(source, document); }
    }
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static Task<ScriptExecutionResult> BatchRun(Session s, MessageDocument source, string script,
        ScriptScope scope = ScriptScope.WholeDocument, IProgress<ScriptProgress>? progress = null, CancellationToken? token = null) =>
        new ScriptExecution(s.Manager, s.Options).RunAsync(source, new(script), scope, progress, token ?? Token);
    [Fact]
    public async Task BundledIdentityExampleCorrelatesRepeatedPatientsAndReportsOnlyCounts()
    {
        await using var s = new Session(); var source = await s.Open(First + Second + First);
        var original = All(source);
        var result = await BatchRun(s, source, ScriptLibrary.PatientExample.Source);
        using var reports = result.Reports!;
        Assert.Equal(3, result.Processed); Assert.Equal(3, result.Kept);
        Assert.Equal("SYN-P000001", result.Document.Message.Get("PID-3").Raw);
        Assert.Equal("PATIENT000001", result.Document.Message.Get("PID-5.2").Raw);
        await result.Document.NavigateAsync(2, Token); Assert.Equal("SYN-P000002", result.Document.Message.Get("PID-3").Raw);
        await result.Document.NavigateAsync(3, Token); Assert.Equal("SYN-P000001", result.Document.Message.Get("PID-3").Raw);
        Assert.Equal(original, All(source)); Assert.False(source.IsDirty); Assert.Null(result.Document.SavePath); Assert.True(result.Document.IsDirty);
        Assert.Equal(new ScriptReportInfo("synthetic-summary", 1, 32), Assert.Single(reports.Items));
        var path = Path.Combine(s.Root, "summary.csv"); await reports.ExportAsync("synthetic-summary", path, Token);
        Assert.Equal("\"Patients\",\"Messages\"\r\n\"2\",\"3\"\r\n", await File.ReadAllTextAsync(path, Token));
        Assert.DoesNotContain("SYN-001", await File.ReadAllTextAsync(path, Token));
    }
    [Theory]
    [InlineData("MSH|^~\\&|SYN\r")]
    [InlineData("MSH|^~\\&|SYN\rPID|1||||Example^Ada\r")]
    [InlineData("MSH|^~\\&|SYN\rPID|1||\"\"||Example^Ada\r")]
    [InlineData("MSH|^~\\&|SYN\rPID|1||   ||Example^Ada\r")]
    [InlineData("MSH|^~\\&|SYN\rPID|1||SYN-001~SYN-002||Example^Ada\r")]
    [InlineData("MSH|^~\\&|SYN\rPID|1||SYN-001||Example^Ada\rPID|2||SYN-002||Other^Bea\r")]
    [InlineData("MSH|^~\\&|SYN\rPID|1||SYN-001||unclosed\\escape\r")]
    public async Task MissingOrAmbiguousIdentityRollsBackEarlierWorkAndReports(string invalid)
    {
        await using var s = new Session(); var source = await s.Open(First + invalid);
        var error = await Assert.ThrowsAsync<ScriptRunException>(() => BatchRun(s, source, ScriptLibrary.PatientExample.Source));
        Assert.DoesNotContain("SYN-001", error.Message); Assert.Single(s.Manager.Documents); s.AssertNoStaging();
    }
    [Fact]
    public async Task AssigningAuthorityIsPartOfIdentityAndRunsReceiveFreshMappings()
    {
        await using var s = new Session(); var source = await s.Open(First.Replace("SYN-001", "SYN-001^^^ONE", StringComparison.Ordinal) +
            First.Replace("SYN-001", "SYN-001^^^TWO", StringComparison.Ordinal));
        for (var run = 0; run < 2; run++)
        {
            var result = await BatchRun(s, source, ScriptLibrary.PatientExample.Source); using var reports = result.Reports;
            Assert.Equal("SYN-P000001", result.Document.Message.Get("PID-3").Raw);
            await result.Document.NavigateAsync(2, Token); Assert.Equal("SYN-P000002", result.Document.Message.Get("PID-3").Raw);
        }
    }
    [Theory]
    [InlineData(ScriptScope.CurrentMessage)] [InlineData(ScriptScope.WholeDocument)]
    public async Task BatchCapturesUnsavedEditsInSourceOrderAndPreservesUndo(ScriptScope scope)
    {
        await using var s = new Session(); var source = await s.Open(); source.IsEditMode = true;
        source.ReplaceWorkingText(First.Replace("Ada", "SyntheticDraft", StringComparison.Ordinal));
        await source.EditRefresh;
        var original = All(source);
        var result = await BatchRun(s, source, """
            var n = 0;
            void Main()
            {
                var message = Message;
                n++;
                message.SetText("PID-5.1", n.ToString());
            }
            """, scope);
        Assert.Equal("SyntheticDraft", result.Document.Message.Get("PID-5.2").Raw);
        Assert.Equal(scope == ScriptScope.CurrentMessage ? 1 : 2, result.Processed);
        if (scope == ScriptScope.WholeDocument) { await result.Document.NavigateAsync(2, Token); Assert.Equal("2", result.Document.Message.Get("PID-5.1").Raw); }
        Assert.True(source.IsDirty); Assert.Equal(original, All(source));
        Assert.True(source.CanUndo); source.Undo(); await source.EditRefresh; Assert.Equal(First, source.WorkingText);
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(12000)]
    public async Task BatchNoOpRetainsWrappersAndEveryOriginalByte(int codePage)
    {
        await using var s = new Session(); var source = await s.Open("FHS|SYN\r\n" + First.Replace("\rPID", "\nPID", StringComparison.Ordinal) + Second + "FTS|1\r", codePage);
        var result = await BatchRun(s, source, """
            void Main()
            {
                var message = Message;
            }
            """);
        Assert.Equal(All(source), All(result.Document)); Assert.Null(result.Reports);
    }
    [Theory]
    [InlineData(false, false)] [InlineData(true, false)]
    [InlineData(false, true)] [InlineData(true, true)]
    public async Task DeferredEmissionFollowsNormalOutputAndReproducesMllpEnvelopes(bool framed, bool completion)
    {
        await using var s = new Session();
        var text = framed ? "\v" + First + "\x1c\r\v" + Second + "\x1c\r" : First + Second;
        var source = await s.Open(text);
        var script = completion ? """
            ScriptDeferredMessage? held = null;
            void Main()
            {
                var message = Message;
                if (Batch.MessageNumber == 1)
                {
                    held = Batch.Hold(message);
                    message.Exclude();
                }
            }

            void Complete()
            {
                Batch.Emit(held!);
            }
            """ :
            """
                void Main()
                {
                    var message = Message;
                    if (Batch.MessageNumber == 1)
                    {
                        Batch.Defer(message);
                        message.Exclude();
                    }
                }
                """;
        var result = await BatchRun(s, source, script);
        var expected = framed ? "\v" + Second + "\x1c\r\v" + First + "\x1c\r" : Second + First;
        Assert.Equal(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(expected)), All(result.Document));
        Assert.Equal(2, result.Processed); Assert.Equal(2, result.Kept);
    }
    [Fact]
    public async Task DeferredOutputCannotInvalidateWrappersOrMissingSeparators()
    {
        await using var s = new Session();
        foreach (var text in new[] { "FHS|SYN\r" + First + "FTS|1\r", First.TrimEnd('\r') })
        {
            var source = await s.Open(text);
            await Assert.ThrowsAsync<ScriptRunException>(() => BatchRun(s, source, """
                void Main()
                {
                    var message = Message;
                    Batch.Defer(message);
                }
                """));
            s.AssertNoStaging();
        }
    }
    [Theory]
    [InlineData("""
        var r = Batch.Report("safe", "Count");
        void Main()
        {
            var message = Message;
            r.Row("one");
        }

        void Complete()
        {
            throw new Exception("SYNTHETIC-PRIVATE");
        }
        """)]
    [InlineData("""
        var r = Batch.Report("safe", "Count");
        void Main()
        {
            var message = Message;
            r.Row("one");
            if (Batch.MessageNumber == 2)
                Environment.Exit(17);
        }
        """)]
    [InlineData("""
        var r = Batch.Report("safe", "Count");
        void Main()
        {
            var message = Message;
            for (int i = 0; i < 20; i++)
                r.Row(new string ('X', 16000));
        }
        """)]
    [InlineData("""
        void Main()
        {
            var message = Message;
            for (int i = 0; i < 129; i++)
                Batch.Defer(message);
        }
        """)]
    public async Task LateFailureCrashOrQuotaCleansAllPrivateArtifactsWithoutPublication(string script)
    {
        await using var s = new Session(); var source = await s.Open();
        var failure = await Assert.ThrowsAsync<ScriptRunException>(() => BatchRun(s, source, script));
        Assert.DoesNotContain("PRIVATE", failure.Message); Assert.Single(s.Manager.Documents); s.AssertNoStaging();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CancelProcessOrCompleteDiscardsReportJournalAndOutput(bool complete)
    {
        await using var s = new Session(); var source = await s.Open();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var script = "var r = Batch.Report(\"safe\", \"Count\"); void Main() { r.Row(\"one\"); " +
            (complete ? "} void Complete() { while(true) {} }" : "while(true) {} }");
        var progress = new Progress(p => { if (p.Phase == (complete ? "Completing" : "Compiling")) cancel.CancelAfter(1000); });
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => BatchRun(s, source, script, progress: progress, token: cancel.Token));
        Assert.Single(s.Manager.Documents); s.AssertNoStaging();
    }
    [Fact]
    public async Task MultipleInterleavedReportsExportIndependentlyAndRefuseReplacementOrCanceledPartialFiles()
    {
        await using var s = new Session(); var source = await s.Open();
        var result = await BatchRun(s, source, """
            var a = Batch.Report("first", "Value", "Empty");
            var b = Batch.Report("second", "Order");
            void Main()
            {
                var message = Message;
                a.Row("患者,\"synthetic\"\r\nvalue", null);
                b.Row(Batch.MessageNumber.ToString());
            }

            void Complete()
            {
                a.Row("=SUM(A1)", "");
            }
            """);
        var reports = result.Reports!;
        Assert.Equal(2, reports.Items.Count);
        var path = Path.Combine(s.Root, "first.csv"); await reports.ExportAsync("first", path, Token);
        var expected = "\"Value\",\"Empty\"\r\n" + string.Concat(Enumerable.Repeat("\"患者,\"\"synthetic\"\"\r\nvalue\",\"\"\r\n", 2)) + "\"'=SUM(A1)\",\"\"\r\n";
        Assert.Equal(expected, await File.ReadAllTextAsync(path, Token));
        await Assert.ThrowsAsync<IOException>(() => reports.ExportAsync("second", path, Token));
        Assert.Equal(expected, await File.ReadAllTextAsync(path, Token));
        var consent = await FileSaveDestination.CaptureAsync(path, Token);
        await reports.ExportAsync("second", consent, Token);
        Assert.Equal("\"Order\"\r\n\"1\"\r\n\"2\"\r\n", await File.ReadAllTextAsync(path, Token));
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var canceled = Path.Combine(s.Root, "canceled.csv");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => reports.ExportAsync("first", canceled, cancel.Token));
        Assert.False(File.Exists(canceled)); Assert.Empty(Directory.GetFiles(s.Root, "*.tmp"));
        await reports.ExportAsync("second", Path.Combine(s.Root, "second.csv"), Token);
        Assert.Equal("\"Order\"\r\n\"1\"\r\n\"2\"\r\n", await File.ReadAllTextAsync(Path.Combine(s.Root, "second.csv"), Token));
        reports.Dispose(); await Assert.ThrowsAsync<ObjectDisposedException>(() => reports.ExportAsync("first", canceled, Token));
        Assert.Equal(2, s.Manager.Documents.Count);
        s.Manager.Close(result.Document.Id, true); await result.Document.Disposal; s.AssertNoStaging();
    }
    [Fact]
    public async Task StatefulLargeBatchStreamsRepeatedPatientReferencesWithinRetainedMemoryBudget()
    {
        await using var s = new Session(); Directory.CreateDirectory(s.Root);
        var count = Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") == "1" ? 8192 : 256;
        var path = Path.Combine(s.Root, "batch.synthetic");
        await using (var writer = File.Create(path))
            for (var i = 0; i < count; i++)
                await writer.WriteAsync(Encoding.UTF8.GetBytes(First.Replace("SYN-001", "SYN-" + (i % 16), StringComparison.Ordinal)), Token);
        await using var reader = File.OpenRead(path);
        var source = await MessageFileReader.OpenFileAsync(s.Manager, "Synthetic", reader, Token);
        var baseline = GC.GetTotalMemory(true); var peak = baseline;
        var result = await BatchRun(s, source, ScriptLibrary.PatientExample.Source, progress: new Progress(p =>
        { if (p.Phase == "Processing" && p.Processed % 128 == 0) peak = Math.Max(peak, GC.GetTotalMemory(true)); }));
        using var reports = result.Reports!;
        Assert.Equal(count, result.Processed); Assert.Equal(count, result.Document.MessageCount);
        Assert.True(peak - baseline < 32L * 1024 * 1024, $"Additional retained bytes: {peak - baseline}");
        await reports.ExportAsync("synthetic-summary", Path.Combine(s.Root, "summary.csv"), Token);
        Assert.Contains($"\"16\",\"{count}\"", await File.ReadAllTextAsync(Path.Combine(s.Root, "summary.csv"), Token));
    }
}
