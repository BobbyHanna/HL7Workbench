using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Scripting;
using HL7Workbench.Tests;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class StructuralSaveTests : IAsyncDisposable
{
    private const string A = "MSH|^~\\&|SYN-A\rPID|A\r";
    private const string B = "MSH|^~\\&|SYN-B\rPID|B\r";
    private const string C = "MSH*$%!@*SYN-C\r\nPID*C\n";
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-structural-" + Guid.NewGuid().ToString("N"));
    private readonly DocumentManager documents = new();
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public StructuralSaveTests() => Directory.CreateDirectory(root);
    private string PathFor(string name) => Path.Combine(root, name);
    private Task<MessageDocument> Open(byte[] bytes) => MessageFileReader.OpenFileAsync(documents, "Synthetic.hl7", new MemoryStream(bytes), Token);
    private Task<MessageDocument> Open(string text = A + B) => Open(Encoding.UTF8.GetBytes(text));
    public async ValueTask DisposeAsync()
    {
        var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
        Directory.Delete(root, true);
    }
    private async Task<MessageFileIndex> Index(string path, Action<SourceSnapshot, MessageFileIndex>? check = null)
    {
        await using var input = File.OpenRead(path);
        using var snapshot = await SourceSnapshot.CaptureAsync(input, Token);
        var index = MessageFileIndex.Build(snapshot, Token); check?.Invoke(snapshot, index); return index;
    }
    [Theory]
    [InlineData(65001, false)] [InlineData(1200, false)] [InlineData(1201, false)]
    [InlineData(65001, true)] [InlineData(1200, true)] [InlineData(1201, true)]
    public async Task EveryPositionPreservesOriginalBytesBomTerminatorsAndFraming(int codePage, bool framed)
    {
        var encoding = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        byte[] Wrap(string text) => framed ? new byte[] { 11 }.Concat(encoding.GetBytes(text)).Concat(new byte[] { 28, 13 }).ToArray() : encoding.GetBytes(text);
        var original = encoding.GetPreamble().Concat(Wrap(A)).Concat(Wrap(B)).ToArray();
        foreach (var position in Enum.GetValues<MessageInsertionPosition>())
        {
            var doc = await Open(original); var first = doc.GetMessageId(1); var second = doc.GetMessageId(2);
            var inserted = doc.InsertMessage(position, C); await doc.EditRefresh;
            var logical = doc.Messages.Select(e => e.Id).ToArray();
            var expected = encoding.GetPreamble().Concat(logical.SelectMany(id => Wrap(id == first ? A : id == second ? B : C))).ToArray();
            var path = PathFor($"{codePage}-{framed}-{position}.hl7");
            await doc.SaveAsync(path, Token);
            Assert.Equal(expected, await File.ReadAllBytesAsync(path, Token));
            Assert.Equal(original, doc.Snapshot!.ReadBytes(new(0, original.Length)));
            Assert.False(doc.IsDirty); Assert.Equal(inserted, doc.ActiveMessageId);
            await Index(path, (_, index) => { Assert.Equal(3, index.Messages.Count); Assert.All(index.Messages, m => Assert.Equal(framed, m.Framed)); });
            doc.Undo(); await doc.EditRefresh; Assert.True(doc.IsDirty); doc.Redo(); await doc.EditRefresh;
            Assert.Equal(inserted, doc.ActiveMessageId); Assert.False(doc.IsDirty);
            await doc.SaveAsync(token: Token); Assert.Equal(expected, await File.ReadAllBytesAsync(path, Token));
            await doc.SaveAsync(token: Token); Assert.Equal(expected, await File.ReadAllBytesAsync(path, Token));
        }
    }
    [Fact]
    public async Task SaveAfterUndoRemovesPreviouslyPublishedInsertionWithoutRebasing()
    {
        var doc = await Open(); var snapshot = doc.Snapshot; var index = doc.Index; var original = doc.GetMessageId(1);
        var inserted = doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        var first = PathFor("first.hl7"); await doc.SaveAsync(first, Token); Assert.False(doc.IsDirty);
        doc.Undo(); await doc.EditRefresh; Assert.True(doc.IsDirty); await doc.SaveAsync(token: Token);
        Assert.Equal(A + B, await File.ReadAllTextAsync(first, Token)); Assert.False(doc.IsDirty);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(inserted, doc.ActiveMessageId); Assert.True(doc.IsDirty);
        var other = PathFor("other.hl7"); await doc.SaveAsync(other, Token); Assert.False(doc.IsDirty);
        Assert.Equal(A + B, await File.ReadAllTextAsync(first, Token)); Assert.Equal(A + B + C, await File.ReadAllTextAsync(other, Token));
        Assert.Same(snapshot, doc.Snapshot); Assert.Same(index, doc.Index); await doc.NavigateAsync(original, Token);
        Assert.Equal(1, doc.ActiveEntry!.Value.SourceOrdinal);
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)]
    public async Task EmptyBomDocumentAndMemoryOriginalCanPublishInsertions(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage);
        var doc = await Open(encoding.GetPreamble());
        var ids = doc.InsertMessages(MessageInsertionPosition.Beginning, [A, C]); await doc.EditRefresh;
        var path = PathFor("empty.hl7"); await doc.SaveAsync(path, Token);
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(A + C)), await File.ReadAllBytesAsync(path, Token));
        Assert.Equal(ids[0], doc.ActiveMessageId); Assert.False(doc.IsDirty);
        var memory = new MessageDocument("memory-" + codePage, "Synthetic", Hl7Parser.Parse(encoding.GetPreamble().Concat(encoding.GetBytes(A)).ToArray(), new() { Encoding = encoding }));
        documents.Open(memory); var original = memory.ActiveMessageId;
        memory.InsertMessage(MessageInsertionPosition.Beginning, C); await memory.EditRefresh;
        var memoryPath = PathFor("memory.hl7"); await memory.SaveAsync(memoryPath, Token);
        Assert.Equal(encoding.GetPreamble().Concat(encoding.GetBytes(C + A)), await File.ReadAllBytesAsync(memoryPath, Token));
        await memory.NavigateAsync(original!.Value, Token); Assert.Equal(A, memory.EditableText); Assert.True(memory.HasOriginalMessage);
    }
    [Theory]
    [InlineData("")] [InlineData("PID|SYN\r")] [InlineData("MSH|^~\\&|SYN")]
    public async Task UnsafeInsertedDraftCannotDisappearOrMergeWithFollowingOriginal(string draft)
    {
        var doc = await Open(); var id = doc.InsertMessage(MessageInsertionPosition.Beginning, draft); await doc.EditRefresh;
        var path = PathFor("retained.hl7"); await File.WriteAllTextAsync(path, "SYNTHETIC EXISTING", Token);
        var consent = await FileSaveDestination.CaptureAsync(path, Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => doc.SaveAsync(consent, Token));
        Assert.Equal("SYNTHETIC EXISTING", await File.ReadAllTextAsync(path, Token)); Assert.Equal(id, doc.ActiveMessageId);
        Assert.Equal(draft, doc.EditableText); Assert.True(doc.IsDirty); Assert.Null(doc.SavePath); Assert.True(doc.CanUndo);
        Assert.Empty(Directory.GetFiles(root, ".hl7-save-*"));
    }
    [Fact]
    public async Task IncompleteMshAtEndCanPublishWithoutClinicalCertification()
    {
        var doc = await Open(A); doc.InsertMessage(MessageInsertionPosition.End, "MSH"); await doc.EditRefresh;
        var path = PathFor("incomplete.hl7"); await doc.SaveAsync(path, Token);
        Assert.Equal(A + "MSH", await File.ReadAllTextAsync(path, Token)); Assert.Equal(2, (await Index(path)).Messages.Count);
    }
    [Fact]
    public async Task UnterminatedUntouchedOriginalIsNeverSilentlyRepaired()
    {
        var original = A.TrimEnd('\r'); var doc = await Open(original); var source = doc.ActiveMessageId!.Value;
        doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        var path = PathFor("unsafe.hl7");
        await Assert.ThrowsAsync<InvalidOperationException>(() => doc.SaveAsync(path, Token));
        Assert.False(File.Exists(path)); Assert.Equal(original, Encoding.UTF8.GetString(doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length))));
        await doc.NavigateAsync(source, Token); doc.IsEditMode = true; doc.ReplaceWorkingText(A);
        await doc.SaveAsync(path, Token); Assert.Equal(A + C, await File.ReadAllTextAsync(path, Token));
    }
    [Theory]
    [InlineData("PID|SYN\r")]
    [InlineData("FHS|SYN\rBHS|SYN\rMSH|^~\\&|SYN\rPID|A\rBTS|1\rFTS|1\r")]
    [InlineData("\vMSH|^~\\&|SYN\rPID|A\r")]
    public async Task UnsupportedStructuralContainersStillSupportExactUnchangedSave(string original)
    {
        var doc = await Open(original); var unchanged = PathFor("unchanged.hl7");
        await doc.SaveAsync(unchanged, Token); Assert.Equal(original, await File.ReadAllTextAsync(unchanged, Token));
        var active = doc.ActiveMessageId;
        Assert.Throws<InvalidOperationException>(() => doc.InsertMessage(MessageInsertionPosition.End, C));
        Assert.False(doc.IsDirty); Assert.Equal(active, doc.ActiveMessageId); Assert.Equal(original, await File.ReadAllTextAsync(unchanged, Token));
    }
    [Theory]
    [InlineData(1200, '\u0D1C')] [InlineData(1201, '\u1C0D')]
    public async Task EncodedFramingCollisionCannotReachPublication(int codePage, char collision)
    {
        var encoding = Encoding.GetEncoding(codePage);
        var input = encoding.GetPreamble().Concat(new byte[] { 11 }).Concat(encoding.GetBytes(A)).Concat(new byte[] { 28, 13 }).ToArray();
        var doc = await Open(input);
        doc.InsertMessage(MessageInsertionPosition.End, C + "NTE|1||" + collision + "\r"); await doc.EditRefresh;
        await Assert.ThrowsAsync<InvalidOperationException>(() => doc.SaveAsync(PathFor("collision.hl7"), Token));
        Assert.True(doc.IsDirty); Assert.Empty(Directory.GetFiles(root, ".hl7-save-*"));
    }
    [Fact]
    public async Task RetainedLeadingAndTrailingNonMessageRegionsStayExact()
    {
        var original = Encoding.UTF8.GetBytes("opaque-prefix\r").Concat(new byte[] { 11 }).Concat(Encoding.UTF8.GetBytes(A)).Concat(new byte[] { 28, 13, 10, 13 }).ToArray();
        var doc = await Open(original); doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
        var path = PathFor("gaps.hl7"); await doc.SaveAsync(path, Token);
        var expected = Encoding.UTF8.GetBytes("opaque-prefix\r").Concat(new byte[] { 11 }).Concat(Encoding.UTF8.GetBytes(C)).Concat(new byte[] { 28, 13, 11 })
            .Concat(Encoding.UTF8.GetBytes(A)).Concat(new byte[] { 28, 13, 10, 13 }).ToArray();
        Assert.Equal(expected, await File.ReadAllBytesAsync(path, Token));
    }
    [Fact]
    public async Task EqualCountsAndRangesCannotAuthorizeSwappedMessageIdentities()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(A + B));
        using var snapshot = await SourceSnapshot.CaptureAsync(input, Token); var index = MessageFileIndex.Build(snapshot, Token);
        var entries = new LogicalMessageSequence(2, MessageOrigin.File);
        var plan = new MessageSavePlan(snapshot, Encoding.UTF8, true);
        plan.Original(entries[0], index.Messages[0], null); plan.Original(entries[1], index.Messages[1], null);
        var path = PathFor("swapped.hl7"); await File.WriteAllTextAsync(path, "SYNTHETIC EXISTING", Token);
        var target = await FileSaveDestination.CaptureAsync(path, Token);
        await Assert.ThrowsAsync<InvalidOperationException>(() => target.WriteValidatedAsync(
            (stream, token) => stream.WriteAsync(Encoding.UTF8.GetBytes(B + A), token).AsTask(),
            (stream, token) => plan.Validate(stream, new Progress<DocumentSaveProgress>(), token), Token));
        Assert.Equal("SYNTHETIC EXISTING", await File.ReadAllTextAsync(path, Token));
        Assert.Empty(Directory.GetFiles(root, ".hl7-save-*"));
    }
    [Fact]
    public async Task StagedIndexerAlsoRejectsProvisionalMessageReclassification()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("PID|SYN\r"));
        using var snapshot = await SourceSnapshot.CaptureAsync(input, Token); var index = MessageFileIndex.Build(snapshot, Token);
        var original = new LogicalMessageSequence(1, MessageOrigin.File)[0];
        var plan = new MessageSavePlan(snapshot, Encoding.UTF8, true);
        plan.Original(original, index.Messages[0], null);
        plan.Inserted(new(MessageId.Create(), MessageOrigin.Inserted, null), new EditableMessageBuffer(C, Encoding.UTF8), false);
        var path = PathFor("reclassification.hl7"); var progress = new Progress<DocumentSaveProgress>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => FileSaveDestination.NewFile(path).WriteValidatedAsync(
            (stream, token) => plan.WriteAsync(stream, progress, token),
            (stream, token) => plan.Validate(stream, progress, token), Token));
        Assert.False(File.Exists(path)); Assert.Empty(Directory.GetFiles(root, ".hl7-save-*"));
    }
    [Fact]
    public async Task AssociatedSaveDetectsChangesSinceOpenAndSincePublication()
    {
        var path = PathFor("original.hl7"); await File.WriteAllTextAsync(path, A, Token);
        MessageDocument doc; await using (var input = File.OpenRead(path)) doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic", input, Token);
        var modified = File.GetLastWriteTimeUtc(path);
        await File.WriteAllTextAsync(path, B, Token); File.SetLastWriteTimeUtc(path, modified);
        doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        await Assert.ThrowsAsync<IOException>(() => doc.SaveAsync(token: Token));
        Assert.Equal(B, await File.ReadAllTextAsync(path, Token)); Assert.True(doc.IsDirty); Assert.Equal(path, doc.SavePath);
        var other = PathFor("other.hl7"); await doc.SaveAsync(other, Token); Assert.False(doc.IsDirty);
        Assert.Equal(A + C, await File.ReadAllTextAsync(other, Token));
        await File.WriteAllTextAsync(other, B, Token);
        await Assert.ThrowsAsync<IOException>(() => doc.SaveAsync(token: Token));
        Assert.Equal(B, await File.ReadAllTextAsync(other, Token)); Assert.False(doc.IsDirty);
        Assert.Equal(A, Encoding.UTF8.GetString(doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length))));
    }
    [Fact]
    public async Task ExternalReplacementWithIdenticalBytesDoesNotBecomeTheOpenBaseline()
    {
        if (!OperatingSystem.IsWindows()) return;
        var path = PathFor("original.hl7"); await File.WriteAllTextAsync(path, A, Token);
        MessageDocument doc; await using (var input = File.OpenRead(path)) doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic", input, Token);
        var created = File.GetCreationTimeUtc(path); var modified = File.GetLastWriteTimeUtc(path);
        var replacement = PathFor("replacement.hl7"); await File.WriteAllTextAsync(replacement, A, Token); File.Move(replacement, path, true);
        File.SetCreationTimeUtc(path, created); File.SetLastWriteTimeUtc(path, modified);
        await Assert.ThrowsAsync<IOException>(() => doc.SaveAsync(token: Token));
        Assert.False(doc.IsDirty); Assert.Equal(A, await File.ReadAllTextAsync(path, Token));
    }
    [Fact]
    public async Task CancellationAndDestinationChangesAfterValidationDoNotPublish()
    {
        var doc = await Open(); doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => doc.SaveAsync(PathFor("canceled.hl7"), canceled.Token));
        Assert.True(doc.IsDirty); Assert.False(doc.IsSaving); Assert.Null(doc.SavePath);
        var path = PathFor("changed.hl7"); await File.WriteAllTextAsync(path, A, Token);
        var target = await FileSaveDestination.CaptureAsync(path, Token);
        await Assert.ThrowsAnyAsync<IOException>(() => target.WriteValidatedAsync(
            (stream, token) => stream.WriteAsync(Encoding.UTF8.GetBytes(C), token).AsTask(),
            (_, _) => { File.WriteAllText(path, B); return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(C))); }, Token));
        Assert.Equal(B, await File.ReadAllTextAsync(path, Token)); Assert.Empty(Directory.GetFiles(root, ".hl7-save-*"));
    }
    [Fact]
    public async Task StructuralConsumersKeepIdentityAndOriginalExportRemainsProvenanceAware()
    {
        var doc = await Open(); using var search = new MessageSearchSession(documents);
        await search.StartAsync(new(MessageSearchMode.Text, "SYN-B"), Token);
        var old = Assert.Single(search.Results!.Matches); search.FilterNavigator = true;
        var originalSource = new ComparisonSource(doc, 2);
        var id = doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
        Assert.Null(search.Results); Assert.Null(doc.NavigationMatches); await search.NavigateAsync(old, Token); Assert.Equal(id, doc.ActiveMessageId);
        var results = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Text, "SYN"), token: Token);
        Assert.Contains(results.Matches, match => match.MessageId == id);
        using var comparison = await MessageComparison.CreateAsync(originalSource, originalSource, token: Token);
        Assert.Empty(comparison.Result.Differences); Assert.Equal(3, originalSource.Ordinal);
        var input = new ComparisonInput(); input.SelectDocument(doc, 1); Assert.Equal(id, input.MessageId);
        Assert.Throws<InvalidOperationException>(() => new RawInspectorSession(doc));
        var script = await new ScriptExecution(documents).RunAsync(doc,
            new ScriptDefinition("void Main() {}"), ScriptScope.CurrentMessage, token: Token);
        Assert.Equal(id, Assert.Single(script.MessageSources).InputMessageId);
        documents.Activate(doc.Id);
        var original = doc.Messages.Single(e => e.SourceOrdinal == 2).Id;
        await doc.NavigateAsync(original, Token); using var raw = new RawInspectorSession(doc);
        Assert.True(raw.CanExport); Assert.Equal(2, doc.ActiveOriginalMessage!.Number);
        var exported = PathFor("original-export.hl7"); await raw.ExportAsync(exported, Token);
        Assert.Equal(B, await File.ReadAllTextAsync(exported, Token));
        doc.Undo(); await doc.EditRefresh;
        await search.StartAsync(new(MessageSearchMode.Text, "SYN-B"), Token); Assert.Single(search.Results!.Matches);
    }
    [Theory]
    [InlineData(100)] [InlineData(256)] [InlineData(512)]
    public async Task LargeUntouchedPayloadSurvivesInsertionUndoRedoAndRepeatedPublication(int mib)
    {
        if (mib != 100 && Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") != "1") return;
        var fixture = await LargeSyntheticFile.CreateAsync(mib * 1024L * 1024, Token);
        var sourcePath = PathFor("large-source.hl7");
        await using (var output = new FileStream(sourcePath, FileMode.CreateNew, FileAccess.Write))
        {
            await output.WriteAsync(Encoding.UTF8.GetBytes(A), Token);
            await using var fixtureStream = File.OpenRead(fixture); await fixtureStream.CopyToAsync(output, Token);
        }
        MessageDocument doc;
        await using (var input = File.OpenRead(sourcePath)) doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic large", input, Token);
        var originalIndex = doc.Index; var originalLarge = doc.Index!.Messages[1].SourceRange;
        var largeId = doc.GetMessageId(2); var last = doc.GetMessageId(3);
        doc.InsertMessage(MessageInsertionPosition.AfterCurrent, C); await doc.EditRefresh;
        await doc.NavigateAsync(last, Token); var after = doc.InsertMessage(MessageInsertionPosition.BeforeCurrent, C); await doc.EditRefresh;
        Assert.Null(doc.Source); Assert.NotNull(doc.MaterializedMessage); Assert.Equal(5, doc.MessageCount);
        var path = PathFor("large-output.hl7"); var allocated = GC.GetTotalAllocatedBytes(); var retained = GC.GetTotalMemory(true); var clock = Stopwatch.StartNew();
        await doc.SaveAsync(path, Token); var saveMs = clock.Elapsed.TotalMilliseconds;
        var allocation = GC.GetTotalAllocatedBytes() - allocated; var growth = GC.GetTotalMemory(true) - retained;
        Assert.True(allocation < 16L * 1024 * 1024, $"Save allocation: {allocation}");
        Assert.True(growth < 8L * 1024 * 1024, $"Save retained growth: {growth}");
        await VerifyLarge(path);
        doc.Undo(); await doc.EditRefresh; Assert.Equal(largeId, doc.ActiveMessageId); Assert.Null(doc.MaterializedMessage);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(after, doc.ActiveMessageId); Assert.False(doc.IsDirty);
        await doc.SaveAsync(token: Token); await VerifyLarge(path);
        Assert.Same(originalIndex, doc.Index);
        var metrics = doc.LastSaveMetrics!;
        TestContext.Current.TestOutputHelper!.WriteLine($"largeMiB={mib}; firstSaveMs={saveMs:F3}; allocatedBytes={allocation}; retainedGrowth={growth}; bytes={metrics.OutputBytes}; writeMs={metrics.Writing.TotalMilliseconds:F3}; indexMs={metrics.Indexing.TotalMilliseconds:F3}; verificationMs={metrics.ByteVerification.TotalMilliseconds:F3}");
        async Task VerifyLarge(string outputPath)
        {
            await Index(outputPath, (snapshot, index) =>
            {
                Assert.Equal(5, index.Messages.Count); Assert.Equal(originalLarge.Length, index.Messages[2].SourceRange.Length);
                var expected = new byte[65536]; var actual = new byte[65536];
                for (long offset = 0; offset < originalLarge.Length;)
                {
                    Token.ThrowIfCancellationRequested(); var count = (int)Math.Min(expected.Length, originalLarge.Length - offset);
                    doc.Snapshot!.Read(new(originalLarge.Offset + offset, count), expected.AsSpan(0, count));
                    snapshot.Read(new(index.Messages[2].SourceRange.Offset + offset, count), actual.AsSpan(0, count));
                    Assert.True(expected.AsSpan(0, count).SequenceEqual(actual.AsSpan(0, count))); offset += count;
                }
            });
        }
    }
}
