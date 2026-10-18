using System.Diagnostics;
using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class StructuralEditingTests : IAsyncDisposable
{
    private const string A = "MSH|^~\\&|SYN-A|||||||SYN-DUP|T|2.5.1\rPID|A\r";
    private const string B = "MSH|^~\\&|SYN-B|||||||SYN-DUP|T|2.5.1\rPID|B\r";
    private const string C = "MSH*$%!@*SYN-C\rPID*C\r\n";
    private readonly DocumentManager manager = new();
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private Task<MessageDocument> Open(string text = A + B) => MessageFileReader.OpenFileAsync(manager,
        "Synthetic.hl7", new MemoryStream(Encoding.UTF8.GetBytes(text)), Token);
    public async ValueTask DisposeAsync()
    {
        var docs = manager.Documents.ToArray(); manager.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
    }
    [Theory]
    [InlineData(MessageInsertionPosition.BeforeCurrent, 1)]
    [InlineData(MessageInsertionPosition.AfterCurrent, 2)]
    [InlineData(MessageInsertionPosition.Beginning, 1)]
    [InlineData(MessageInsertionPosition.End, 3)]
    public async Task IdentityAndProvenanceSurviveEveryInsertionPosition(MessageInsertionPosition position, int expected)
    {
        var doc = await Open(); var a = doc.GetMessageId(1); var b = doc.GetMessageId(2);
        var inserted = doc.InsertMessage(position, C); await doc.EditRefresh;
        Assert.Equal(3, doc.MessageCount); Assert.Equal(expected, doc.GetLogicalPosition(inserted));
        Assert.Equal(inserted, doc.ActiveMessageId); Assert.Equal(MessageOrigin.Inserted, doc.ActiveEntry!.Value.Origin);
        Assert.Null(doc.ActiveEntry.Value.SourceOrdinal); Assert.Null(doc.ActiveOriginalMessage); Assert.False(doc.HasOriginalMessage);
        await doc.NavigateAsync(b, Token);
        Assert.Equal(b, doc.ActiveMessageId); Assert.Equal(2, doc.ActiveEntry!.Value.SourceOrdinal); Assert.Equal(B, doc.EditableText);
        await doc.NavigateAsync(a, Token);
        Assert.Equal(a, doc.ActiveMessageId); Assert.Equal(1, doc.ActiveEntry!.Value.SourceOrdinal); Assert.Equal(A, doc.EditableText);
        Assert.True(doc.IsDirty);
        doc.Undo(); await doc.EditRefresh;
        Assert.Equal(0, doc.GetLogicalPosition(inserted)); Assert.Equal(2, doc.MessageCount); Assert.False(doc.IsDirty);
        doc.Redo(); await doc.EditRefresh;
        Assert.Equal(inserted, doc.ActiveMessageId); Assert.Equal(C, doc.EditableText); Assert.Equal(expected, doc.MessageNumber);
    }
    [Fact]
    public async Task EmptyAndMemoryOriginsRetainIdentityWithoutInventingSourceOrdinals()
    {
        var empty = await Open(""); Assert.Empty(empty.Messages); Assert.Equal(0, empty.MessageNumber);
        var blank = empty.InsertMessage(MessageInsertionPosition.AfterCurrent); await empty.EditRefresh;
        Assert.Equal("", empty.EditableText); Assert.Equal(1, empty.MessageCount);
        empty.Undo(); await empty.EditRefresh; Assert.Null(empty.ActiveMessageId); Assert.Equal(0, empty.MessageNumber); Assert.False(empty.IsDirty);
        empty.Redo(); await empty.EditRefresh; Assert.Equal(blank, empty.ActiveMessageId);
        var memory = new MessageDocument("memory", "Synthetic", Hl7Parser.Parse(A)); manager.Open(memory);
        var original = memory.ActiveMessageId!.Value;
        Assert.Equal(MessageOrigin.Memory, memory.ActiveEntry!.Value.Origin); Assert.Null(memory.ActiveEntry.Value.SourceOrdinal);
        memory.InsertMessage(MessageInsertionPosition.Beginning, C); await memory.EditRefresh;
        await memory.NavigateAsync(original, Token); Assert.Equal(A, memory.EditableText); Assert.Equal(original, memory.ActiveMessageId);
        var foreign = empty.ActiveMessageId!.Value;
        Assert.Equal(0, memory.GetLogicalPosition(foreign));
        await Assert.ThrowsAsync<ArgumentException>(() => memory.NavigateAsync(foreign, Token));
    }
    [Fact]
    public async Task BatchOrderRepeatedAnchorsAndGlobalBranchingAreDeterministic()
    {
        var doc = await Open(); var a = doc.ActiveMessageId!.Value;
        var ids = doc.InsertMessages(MessageInsertionPosition.AfterCurrent, [C, ""]); await doc.EditRefresh;
        Assert.Equal(ids[0], doc.GetMessageId(2)); Assert.Equal(ids[1], doc.GetMessageId(3));
        await doc.NavigateAsync(a, Token);
        var nearest = doc.InsertMessage(MessageInsertionPosition.AfterCurrent, A); await doc.EditRefresh;
        Assert.Equal(nearest, doc.GetMessageId(2)); Assert.Equal(ids[0], doc.GetMessageId(3));
        doc.Undo(); await doc.EditRefresh; doc.Undo(); await doc.EditRefresh;
        Assert.Equal(2, doc.MessageCount); Assert.True(doc.CanRedo); Assert.False(doc.IsDirty);
        var replacement = doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        Assert.NotEqual(nearest, replacement); Assert.NotEqual(ids[0], replacement); Assert.False(doc.CanRedo);
    }
    [Fact]
    public async Task ChronologicalUndoAlwaysSelectsAffectedMessageEvenWithEditModeOff()
    {
        var doc = await Open(); var a = doc.GetMessageId(1); var b = doc.GetMessageId(2);
        doc.IsEditMode = true; doc.ReplaceWorkingText(A + "NTE|1\r"); await doc.EditRefresh;
        await doc.NavigateAsync(b, Token); doc.ReplaceWorkingText(B + "NTE|2\r"); await doc.EditRefresh;
        await doc.NavigateAsync(a, Token); doc.IsEditMode = false;
        doc.Undo(); await doc.EditRefresh; Assert.Equal(b, doc.ActiveMessageId); Assert.Equal(B, doc.EditableText);
        doc.Undo(); await doc.EditRefresh; Assert.Equal(a, doc.ActiveMessageId); Assert.Equal(A, doc.EditableText); Assert.False(doc.IsDirty);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(a, doc.ActiveMessageId); Assert.EndsWith("NTE|1\r", doc.EditableText);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(b, doc.ActiveMessageId); Assert.EndsWith("NTE|2\r", doc.EditableText);
    }
    [Fact]
    public async Task StructuralAndTextHistoryRetainPayloadAndSelectPreviousSurvivor()
    {
        var doc = await Open(); var a = doc.ActiveMessageId;
        var id = doc.InsertMessage(MessageInsertionPosition.AfterCurrent); await doc.EditRefresh;
        doc.IsEditMode = true; doc.ReplaceWorkingText(C); await doc.EditRefresh;
        await doc.NavigateAsync(3, Token); doc.Undo(); await doc.EditRefresh;
        Assert.Equal(id, doc.ActiveMessageId); Assert.Equal("", doc.EditableText);
        doc.Undo(); await doc.EditRefresh; Assert.Equal(a, doc.ActiveMessageId); Assert.False(doc.IsDirty);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(id, doc.ActiveMessageId); Assert.Equal("", doc.EditableText);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(C, doc.EditableText);
    }
    [Fact]
    public async Task RejectedBatchesLeaveSelectionHistoryAndRedoUnchanged()
    {
        var doc = await Open(); var id = doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        doc.Undo(); await doc.EditRefresh; var active = doc.ActiveMessageId; var revision = doc.SearchRevision;
        Assert.Throws<InvalidOperationException>(() => doc.InsertMessages(MessageInsertionPosition.End, [C, A + B]));
        Assert.Throws<Hl7LimitException>(() => doc.InsertMessage(MessageInsertionPosition.End, new string('X', MessagePresentation.MaximumEditorCharacters + 1)));
        Assert.Throws<Hl7LimitException>(() => doc.InsertMessages(MessageInsertionPosition.End, Enumerable.Repeat("", 100_000).ToArray()));
        var large = new string('X', MessagePresentation.MaximumEditorCharacters);
        Assert.Throws<Hl7LimitException>(() => doc.InsertMessages(MessageInsertionPosition.End, Enumerable.Repeat(large, 9).ToArray()));
        Assert.Equal(active, doc.ActiveMessageId); Assert.Equal(revision, doc.SearchRevision); Assert.Equal(2, doc.MessageCount); Assert.True(doc.CanRedo);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(id, doc.ActiveMessageId);
        var opaque = await Open("unclassified bytes");
        Assert.Throws<InvalidOperationException>(() => opaque.InsertMessage(MessageInsertionPosition.Beginning));
    }
    [Fact]
    public async Task PendingNavigationAndParseCannotPublishIntoAnotherIdentity()
    {
        var doc = await Open(); var original = doc.GetMessageId(2);
        var id = doc.InsertMessage(MessageInsertionPosition.Beginning); doc.IsEditMode = true;
        doc.ReplaceWorkingText(C); var old = doc.EditRefresh;
        var navigation = doc.NavigateAsync(original, Token); doc.Undo(); await doc.EditRefresh;
        await old; await navigation;
        Assert.Equal(id, doc.ActiveMessageId); Assert.Equal("", doc.EditableText);
        doc.Undo(); await doc.EditRefresh; Assert.NotEqual(id, doc.ActiveMessageId); Assert.Equal(A, doc.EditableText);
    }
    [Theory]
    [InlineData(1_000)] [InlineData(50_000)] [InlineData(100_000)]
    public void MetadataMemoryAndMaximumCountLookupAreBounded(int count)
    {
        var baseline = GC.GetAllocatedBytesForCurrentThread(); var watch = Stopwatch.StartNew();
        var sequence = new LogicalMessageSequence(count, MessageOrigin.File); var elapsed = watch.Elapsed.TotalMilliseconds;
        var bytes = GC.GetAllocatedBytesForCurrentThread() - baseline;
        Assert.Equal(count, sequence.Position(sequence[count - 1].Id));
        Assert.Equal(count, sequence[count - 1].SourceOrdinal);
        Assert.True(bytes < 24L * 1024 * 1024, $"Metadata allocated {bytes} bytes.");
        TestContext.Current.TestOutputHelper!.WriteLine($"metadata count={count}; allocatedBytes={bytes}; constructionMs={elapsed:F3}");
    }
    [Fact]
    public void MaximumSizeMetadataRangeSpliceAndUndoRedoUseOneBoundedShift()
    {
        foreach (var size in new[] { 1, 1000 })
        foreach (var index in new[] { 0, (100_000 - size) / 2, 100_000 - size })
        {
            var sequence = new LogicalMessageSequence(100_000 - size, MessageOrigin.File);
            var batch = Enumerable.Range(0, size).Select(_ => new LogicalMessage(MessageId.Create(), MessageOrigin.Inserted, null)).ToArray();
            var watch = Stopwatch.StartNew(); sequence.Insert(index, batch); var insert = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); sequence.Remove(index, batch.Length); var undo = watch.Elapsed.TotalMilliseconds;
            watch.Restart(); sequence.Insert(index, batch); var redo = watch.Elapsed.TotalMilliseconds;
            Assert.Equal(100_000, sequence.Count); Assert.Equal(index + size, sequence.Position(batch[^1].Id));
            Assert.Throws<Hl7LimitException>(() => sequence.Insert(0, [new(MessageId.Create(), MessageOrigin.Inserted, null)]));
            TestContext.Current.TestOutputHelper!.WriteLine($"splice size={size}; index={index}; insertMs={insert:F3}; undoMs={undo:F3}; redoMs={redo:F3}");
        }
    }
    [Fact]
    public async Task SeededStructuralHistoryMatchesIndependentSequenceOracle()
    {
        var doc = new MessageDocument("oracle", "Synthetic", Hl7Parser.Parse(A)); manager.Open(doc);
        var original = doc.ActiveMessageId!.Value;
        // Retain the original's bounded buffer so history selection stays synchronous;
        // parsing may be superseded, but sequence/history publication must be immediate.
        doc.IsEditMode = true; doc.ReplaceWorkingText(A + "NTE|SYN\r"); doc.Undo();
        var expected = new List<MessageId> { original };
        var past = new Stack<(MessageId[] Before, MessageId[] After, MessageId First, int Index)>();
        var future = new Stack<(MessageId[] Before, MessageId[] After, MessageId First, int Index)>();
        var selected = original; var random = new Random(8010203);
        for (var step = 0; step < 250; step++)
        {
            var action = random.Next(4);
            if (action == 0 && past.TryPop(out var undo))
            {
                expected = undo.Before.ToList(); future.Push(undo); doc.Undo();
                selected = expected[Math.Max(0, undo.Index - 1)];
            }
            else if (action == 1 && future.TryPop(out var redo))
            {
                expected = redo.After.ToList(); past.Push(redo); doc.Redo(); selected = redo.First;
            }
            else
            {
                var position = (MessageInsertionPosition)random.Next(4); var current = expected.IndexOf(selected);
                var index = position switch { MessageInsertionPosition.Beginning => 0, MessageInsertionPosition.End => expected.Count,
                    MessageInsertionPosition.BeforeCurrent => current, _ => current + 1 };
                var before = expected.ToArray();
                var ids = doc.InsertMessages(position, Enumerable.Repeat("", random.Next(1, 4)).ToArray());
                expected.InsertRange(index, ids); future.Clear(); past.Push((before, expected.ToArray(), ids[0], index)); selected = ids[0];
            }
            Assert.Equal(expected, doc.Messages.Select(e => e.Id)); Assert.Equal(selected, doc.ActiveMessageId);
            for (var i = 0; i < expected.Count; i++) Assert.Equal(i + 1, doc.GetLogicalPosition(expected[i]));
            Assert.Equal(expected.Count != 1, doc.IsDirty);
        }
        await doc.EditRefresh;
    }
    [Fact]
    public async Task StressMaximumLiveDraftCountAndHistoryBudgetsRejectAtomically()
    {
        if (Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") != "1") return;
        var doc = await Open(""); var before = GC.GetTotalMemory(true); var watch = Stopwatch.StartNew();
        var ids = doc.InsertMessages(MessageInsertionPosition.Beginning, Enumerable.Repeat("", 100_000).ToArray());
        var insertMs = watch.Elapsed.TotalMilliseconds; await doc.EditRefresh;
        Assert.Equal(100_000, doc.MessageCount); Assert.Equal(ids[0], doc.ActiveMessageId);
        Assert.Throws<Hl7LimitException>(() => doc.InsertMessage(MessageInsertionPosition.End));
        watch.Restart(); doc.Undo(); var undoMs = watch.Elapsed.TotalMilliseconds; await doc.EditRefresh;
        Assert.Equal(0, doc.MessageCount); Assert.False(doc.IsDirty);
        watch.Restart(); doc.Redo(); var redoMs = watch.Elapsed.TotalMilliseconds; await doc.EditRefresh;
        Assert.Equal(ids[0], doc.ActiveMessageId); Assert.Equal(100_000, doc.MessageCount);
        var retained = GC.GetTotalMemory(true) - before;
        Assert.True(retained < 128L * 1024 * 1024, $"Maximum draft metadata retained {retained} bytes.");
        TestContext.Current.TestOutputHelper!.WriteLine($"liveDrafts=100000; retainedBytes={retained}; insertMs={insertMs:F3}; undoMs={undoMs:F3}; redoMs={redoMs:F3}");
    }
}
