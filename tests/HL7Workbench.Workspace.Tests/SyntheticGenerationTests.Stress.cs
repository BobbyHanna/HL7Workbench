using System.Diagnostics;
using System.Text;
using HL7Workbench.Tests;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class SyntheticGenerationTests
{
    [Fact]
    public async Task NearCapacityBatchUsesOneSpliceAndRejectsOverflowWithoutMutation()
    {
        var sourceCount = Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") == "1" ? 99_000 : 2_000;
        var doc = await Open(string.Concat(Enumerable.Repeat(A, sourceCount))); var first = doc.GetMessageId(1); var last = doc.GetMessageId(sourceCount);
        var clock = Stopwatch.StartNew(); var allocated = GC.GetTotalAllocatedBytes();
        var result = await Generate(doc, 1000, MessageInsertionPosition.Beginning); await doc.EditRefresh;
        var bytes = GC.GetTotalAllocatedBytes() - allocated;
        Assert.Equal(sourceCount + 1000, doc.MessageCount); Assert.Equal(first, doc.GetMessageId(1001)); Assert.Equal(last, doc.GetMessageId(doc.MessageCount));
        Assert.True(bytes < 256L * 1024 * 1024, $"Near-capacity generation allocated {bytes} bytes");
        TestContext.Current.TestOutputHelper!.WriteLine($"generator nearCapacity={doc.MessageCount}; totalMs={clock.Elapsed.TotalMilliseconds:F3}; allocatedBytes={bytes}");
        if (sourceCount == 99_000)
        {
            var before = Capture(doc); await Assert.ThrowsAsync<InvalidOperationException>(() => Generate(doc)); Unchanged(before, doc);
        }
        doc.Undo(); await doc.EditRefresh; Assert.Equal(sourceCount, doc.MessageCount);
        doc.Redo(); await doc.EditRefresh; Assert.Equal(result.MessageIds, doc.Messages.Take(1000).Select(m => m.Id));
    }
    [Theory]
    [InlineData(16)] [InlineData(100)] [InlineData(256)] [InlineData(512)]
    public async Task LargeSourceValidationAndSaveStreamUntouchedBytesWithBoundedAllocations(int mib)
    {
        if (mib != 16 && Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") != "1") return;
        Directory.CreateDirectory(root); var path = await LargeSyntheticFile.CreateAsync(mib * 1024L * 1024, Token);
        MessageDocument doc;
        await using (var input = File.OpenRead(path)) doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic large", input, Token);
        var original = doc.Messages.ToArray(); var retained = GC.GetTotalMemory(true); var allocated = GC.GetTotalAllocatedBytes(); var clock = Stopwatch.StartNew();
        var result = await Generate(doc, 1000, MessageInsertionPosition.Beginning); await doc.EditRefresh;
        var bytes = GC.GetTotalAllocatedBytes() - allocated; var memory = GC.GetTotalMemory(true) - retained;
        Assert.Equal(original, doc.Messages.Skip(1000)); Assert.Null(doc.Source); Assert.Null(doc.Presentation);
        Assert.True(bytes < 128L * 1024 * 1024, $"Large-source generation allocated {bytes} bytes");
        Assert.True(memory < 32L * 1024 * 1024, $"Large-source retained {memory} bytes");
        TestContext.Current.TestOutputHelper!.WriteLine($"generator largeMiB={mib}; totalMs={clock.Elapsed.TotalMilliseconds:F3}; allocatedBytes={bytes}; retainedBytes={memory}");
        var saved = Path.Combine(root, "large-generated.hl7"); await doc.SaveAsync(saved, Token);
        await using var output = File.OpenRead(saved); output.Position = Encoding.UTF8.GetByteCount(string.Concat(HL7Workbench.Core.SyntheticMessageGenerator.Generate(result.Options, Token).Messages));
        var expected = new byte[65536]; var actual = new byte[65536];
        for (long offset = 0; offset < doc.Snapshot!.Length;)
        {
            var count = (int)Math.Min(expected.Length, doc.Snapshot.Length - offset);
            doc.Snapshot.Read(new(offset, count), expected.AsSpan(0, count)); await output.ReadExactlyAsync(actual.AsMemory(0, count), Token);
            Assert.True(expected.AsSpan(0, count).SequenceEqual(actual.AsSpan(0, count))); offset += count;
        }
        Assert.Equal(output.Length, output.Position);
    }
}
