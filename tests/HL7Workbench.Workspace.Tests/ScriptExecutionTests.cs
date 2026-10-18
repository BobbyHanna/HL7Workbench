using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Scripting;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class ScriptExecutionTests
{
    private const string First = "MSH|^~\\&|SYN|TEST|APP|TEST|20261015120000||ADT^A01|SYN-1|T|2.5\rPID|1||SYN-001||Example^Ada\r";
    private const string Second = "MSH|^~\\&|SYN|TEST|APP|TEST|20261015120000||ADT^A08|SYN-2|T|2.5\rPID|1||SYN-002||Other^Bea\r";
    private sealed class Progress(Action<ScriptProgress> report) : IProgress<ScriptProgress>
    { public void Report(ScriptProgress value) => report(value); }
    private sealed class Session : IAsyncDisposable
    {
        internal string Root { get; } = Path.Combine(Path.GetTempPath(), "hl7-script-test-" + Guid.NewGuid().ToString("N"));
        internal DocumentManager Manager { get; } = new();
        internal ScriptExecutionOptions Options => new() { StorageRoot = Path.Combine(Root, "snapshots") };
        internal async Task<MessageDocument> Open(string text = First + Second, int codePage = 65001)
        {
            Directory.CreateDirectory(Root);
            var encoding = Encoding.GetEncoding(codePage);
            using var input = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            return await MessageFileReader.OpenFileAsync(Manager, "Synthetic-private.hl7", input, TestContext.Current.CancellationToken);
        }
        internal Task<ScriptExecutionResult> Run(MessageDocument source, string script = "", ScriptScope scope = ScriptScope.WholeDocument,
            ScriptExecutionOptions? options = null, IProgress<ScriptProgress>? progress = null) =>
            new ScriptExecution(Manager, options ?? Options).RunAsync(source, new("void Main()\n{\n" + script + "\n}"), scope, progress,
                TestContext.Current.CancellationToken);
        internal void AssertNoStaging() => Assert.True(!Directory.Exists(Options.StorageRoot) || !Directory.EnumerateDirectories(Options.StorageRoot).Any());
        public async ValueTask DisposeAsync()
        {
            var docs = Manager.Documents.ToArray(); Manager.Dispose();
            await Task.WhenAll(docs.Select(d => d.Disposal));
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
    private static byte[] All(MessageDocument document) => document.Snapshot!.ReadBytes(new(0, document.Snapshot.Length), int.MaxValue);

    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(12000)] [InlineData(12001)]
    public async Task NoOpWholeDocumentPreservesEveryByteAndPublishesUnsavedIndependentDocument(int codePage)
    {
        await using var s = new Session();
        var source = await s.Open("FHS|SYN\r\n" + First.Replace("\rPID", "\nPID", StringComparison.Ordinal) + "opaque text\r\n" + Second + "FTS|1\r", codePage);
        var original = All(source);
        var result = await s.Run(source);
        Assert.Equal(original, All(result.Document)); Assert.Equal(original, All(source));
        Assert.Equal(2, result.Processed); Assert.Equal(2, result.Kept);
        Assert.True(result.Document.IsDirty); Assert.Null(result.Document.SavePath); Assert.False(source.IsDirty);
        Assert.NotSame(source.Snapshot, result.Document.Snapshot);
        Assert.False(s.Manager.Close(result.Document.Id));
        var path = Path.Combine(s.Root, "result.hl7"); await result.Document.SaveAsync(path, TestContext.Current.CancellationToken);
        Assert.False(result.Document.IsDirty); Assert.Equal(original, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Null(source.SavePath);
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)]
    public async Task WholeDocumentFilteringRemovesCompleteMllpEnvelopeAndKeepsInheritedBom(int codePage)
    {
        await using var s = new Session(); var encoding = Encoding.GetEncoding(codePage);
        using var stream = new MemoryStream();
        stream.Write(encoding.GetPreamble());
        foreach (var message in new[] { First, Second })
        { stream.WriteByte(0x0B); stream.Write(encoding.GetBytes(message)); stream.Write([0x1C, 0x0D]); }
        stream.Position = 0;
        var source = await MessageFileReader.OpenFileAsync(s.Manager, "Synthetic", stream, TestContext.Current.CancellationToken);
        var original = All(source);
        var result = await s.Run(source, "if (Message.Read(\"MSH-9.2\").Raw == \"A01\") Message.Exclude();");
        Assert.Equal(encoding.GetPreamble().Concat(new byte[] { 0x0B }).Concat(encoding.GetBytes(Second)).Concat(new byte[] { 0x1C, 0x0D }).ToArray(), All(result.Document));
        Assert.Equal(1, result.Kept); Assert.Equal(original, All(source));
    }
    [Fact]
    public async Task CurrentMessageKeepsIndependentCapturedEditsWithExplicitStaleSourceAttribution()
    {
        await using var s = new Session(); var source = await s.Open(); source.IsEditMode = true;
        source.ReplaceWorkingText(First.Replace("Ada", "Captured", StringComparison.Ordinal));
        // Capture must read the retained draft even while its debounced parse is pending.
        var revision = source.SearchRevision; var selected = source.ActiveMessageId;
        var result = await s.Run(source, "Message.SetText(\"PID-5.1\", \"Synthetic\");", ScriptScope.CurrentMessage,
            progress: new Progress(p =>
            {
                if (p.Phase == "Compiling") source.ReplaceWorkingText(First.Replace("Ada", "Later", StringComparison.Ordinal));
            }));
        Assert.True(result.SourceChangedDuringRun); Assert.Equal(revision, result.Input!.Revision);
        Assert.False(result.Input.IsCurrent(source)); Assert.Equal(selected, Assert.Single(result.MessageSources).InputMessageId);
        Assert.Equal("Synthetic^Captured", result.Document.Message.Get("PID-5").Raw);
        Assert.Contains("Later", source.WorkingText!); Assert.True(source.IsDirty);
        Assert.Equal(2, s.Manager.Documents.Count); Assert.True(result.Document.IsDirty); Assert.Null(result.Document.SavePath);
        await source.EditRefresh;
    }
    [Fact]
    public async Task UnsavedEditsAcrossInactiveMessagesAreIncluded()
    {
        await using var s = new Session(); var source = await s.Open(); source.IsEditMode = true;
        source.ReplaceWorkingText(First.Replace("Ada", "One", StringComparison.Ordinal));
        await source.NavigateAsync(2, TestContext.Current.CancellationToken);
        source.ReplaceWorkingText(Second.Replace("Bea", "Two", StringComparison.Ordinal));
        var result = await s.Run(source);
        Assert.Equal(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(First.Replace("Ada", "One", StringComparison.Ordinal) + Second.Replace("Bea", "Two", StringComparison.Ordinal))).ToArray(), All(result.Document));
        Assert.True(source.IsDirty); await source.EditRefresh;
    }
    [Theory]
    [InlineData("if (Message.Read(\"MSH-10\").Raw == \"SYN-2\") throw new Exception(Message.Text(\"PID-3\"));")]
    [InlineData("if (Message.Read(\"MSH-10\").Raw == \"SYN-2\") Environment.Exit(17);")]
    public async Task LateFailureOrCrashRollsBackAllOutputAndCleansStaging(string script)
    {
        await using var s = new Session(); var source = await s.Open();
        var original = All(source);
        await Assert.ThrowsAsync<ScriptRunException>(() => s.Run(source, script));
        Assert.Single(s.Manager.Documents); Assert.Equal(original, All(source)); s.AssertNoStaging();
        var log = string.Join("", s.Manager.Activity.Snapshot());
        Assert.Contains("no partial output", log); Assert.DoesNotContain("SYN-002", log); Assert.DoesNotContain("private", log);
    }
    [Theory]
    [InlineData(ScriptScope.CurrentMessage)] [InlineData(ScriptScope.WholeDocument)]
    public async Task UnsupportedTargetedSourceBackedMessageFailsWithoutMaterializingPreview(ScriptScope scope)
    {
        await using var s = new Session();
        var source = await s.Open(First + "OBX|1|ED|||" + new string('A', ScriptLimits.LineCharacters + 1) + "\r");
        Assert.NotNull(source.Source); Assert.NotNull(source.Presentation);
        var error = await Assert.ThrowsAsync<ScriptRunException>(() => s.Run(source, scope: scope));
        Assert.Contains(error.Diagnostics, d => d.Code == "InputLimit"); Assert.Single(s.Manager.Documents); s.AssertNoStaging();
    }
    [Fact]
    public async Task PlainFilteringAndEmptyOutputRemainIndependentUnsavedDocuments()
    {
        await using var s = new Session(); var source = await s.Open();
        var result = await s.Run(source, "Message.Exclude();");
        Assert.Equal(0, result.Kept); Assert.Equal(0, result.Document.MessageCount); Assert.True(result.Document.IsDirty);
        Assert.Equal(Encoding.UTF8.GetPreamble(), All(result.Document));
        await result.Document.SaveAsync(Path.Combine(s.Root, "empty.hl7"), TestContext.Current.CancellationToken);
        Assert.False(result.Document.IsDirty);
    }
    [Fact]
    public async Task WrapperFilteringAndExtractionRefuseToSilentlyDiscardOrRewriteMetadata()
    {
        await using var s = new Session(); var source = await s.Open("FHS|SYN\r" + First + Second + "FTS|1\r");
        foreach (var scope in new[] { ScriptScope.CurrentMessage, ScriptScope.WholeDocument })
        {
            var error = await Assert.ThrowsAsync<ScriptRunException>(() => s.Run(source, "Message.Exclude();", scope));
            Assert.Contains(error.Diagnostics, d => d.Code == "ContainerExtraction"); s.AssertNoStaging();
        }
    }
    [Fact]
    public async Task OutputQuotaFailureAndCompilationFailureHaveNoPartialPublication()
    {
        await using var s = new Session(); var source = await s.Open();
        var error = await Assert.ThrowsAsync<ScriptRunException>(() => s.Run(source, options: s.Options with { MaximumOutputBytes = 30 }));
        Assert.Contains(error.Diagnostics, d => d.Code == "OutputLimit"); s.AssertNoStaging();
        await Assert.ThrowsAsync<ScriptRunException>(() => s.Run(source, "broken +")); s.AssertNoStaging();
        Assert.Single(s.Manager.Documents);
    }
    [Fact]
    public async Task CancelAndSourceCloseDrainWorkerAndStagingWithoutPublishing()
    {
        await using var s = new Session(); var source = await s.Open();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var run = new ScriptExecution(s.Manager, s.Options).RunAsync(source, new("void Main() { while(true) { } }"), ScriptScope.WholeDocument,
            new Progress(p => { if (p.Phase == "Compiling") cancellation.CancelAfter(1000); }), cancellation.Token);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run); s.AssertNoStaging();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => s.Run(source, progress: new Progress(p =>
        { if (p.Phase == "Compiling") s.Manager.Close(source.Id); })));
        await source.Disposal; Assert.Empty(s.Manager.Documents); s.AssertNoStaging();
    }
    [Fact]
    public async Task EncodedBoundaryCollisionRollsBackOutput()
    {
        await using var s = new Session();
        var encoding = new UnicodeEncoding(false, true, true);
        using var stream = new MemoryStream(encoding.GetPreamble().Concat(new byte[] { 0x0B }).Concat(encoding.GetBytes(First)).Concat(new byte[] { 0x1C, 0x0D }).ToArray());
        var source = await MessageFileReader.OpenFileAsync(s.Manager, "Synthetic", stream, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ScriptRunException>(() => s.Run(source, "Message.SetText(\"PID-5.2\", \"\\u0D1C\");"));
        s.AssertNoStaging(); Assert.Single(s.Manager.Documents);
    }
    [Fact]
    public async Task WholeDocumentStreamsManyOrdinaryMessagesWithBoundedRetainedMemory()
    {
        await using var s = new Session(); Directory.CreateDirectory(s.Root);
        var count = Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") == "1" ? 4096 : 256;
        var payload = Encoding.UTF8.GetBytes(First + "NTE|1||" + new string('X', 4096) + "\r");
        var path = Path.Combine(s.Root, "batch.synthetic");
        await using (var writer = File.Create(path))
            for (var i = 0; i < count; i++) await writer.WriteAsync(payload, TestContext.Current.CancellationToken);
        await using var reader = File.OpenRead(path);
        var source = await MessageFileReader.OpenFileAsync(s.Manager, "Synthetic batch", reader, TestContext.Current.CancellationToken);
        var baseline = GC.GetTotalMemory(true); var maximumRetained = baseline;
        var result = await s.Run(source, progress: new Progress(p =>
        {
            if (p.Phase == "Processing" && p.Processed % 128 == 0)
                maximumRetained = Math.Max(maximumRetained, GC.GetTotalMemory(true));
        }));
        Assert.Equal(count, result.Processed); Assert.Equal(source.Snapshot!.Length, result.OutputBytes);
        Assert.True(maximumRetained - baseline < 32L * 1024 * 1024, $"Additional retained bytes: {maximumRetained - baseline}");
    }
    [Fact]
    public async Task CurrentScopeIgnoresUnsupportedInactiveDraftWhileWholeScopeFailsExplicitly()
    {
        await using var s = new Session(); var source = await s.Open(); source.IsEditMode = true;
        await source.NavigateAsync(2, TestContext.Current.CancellationToken);
        source.ReplaceWorkingText(Second + "NTE|1||" + new string('X', ScriptLimits.LineCharacters + 1));
        await source.EditRefresh; Assert.NotNull(source.EditError);
        await source.NavigateAsync(1, TestContext.Current.CancellationToken);
        var result = await s.Run(source, scope: ScriptScope.CurrentMessage);
        Assert.Equal(1, result.Processed); Assert.Equal(First, result.Document.Message.RawText); Assert.True(source.IsDirty);
        var failure = await Assert.ThrowsAsync<ScriptRunException>(() => s.Run(source));
        Assert.Contains(failure.Diagnostics, d => d.Code == "InputLimit" && d.MessageNumber == 2);
    }
}
