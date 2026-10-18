using System.Diagnostics;
using System.Text;
using HL7Workbench.Scripting;
using HL7Workbench.Tests;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class StructuralCompatibilityTests
{
    [Theory]
    [InlineData(16)] [InlineData(100)] [InlineData(256)] [InlineData(512)]
    public async Task LargeUntouchedSearchAndCurrentScriptCaptureRemainLazyAndByteExact(int mib)
    {
        if (mib != 16 && Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") != "1") return;
        Directory.CreateDirectory(root);
        var fixture = await LargeSyntheticFile.CreateAsync(mib * 1024L * 1024, Token);
        var path = Path.Combine(root, "lazy-source.hl7");
        await using (var output = File.Create(path))
        {
            await output.WriteAsync(Encoding.UTF8.GetBytes(A), Token);
            await using var original = File.OpenRead(fixture); await original.CopyToAsync(output, Token);
        }
        MessageDocument doc;
        await using (var input = File.OpenRead(path)) doc = await MessageFileReader.OpenFileAsync(documents, "Synthetic lazy", input, Token);
        var largeId = doc.GetMessageId(2); var largeRange = doc.Index!.Messages[1].PayloadRange;
        var inserted = doc.InsertMessage(MessageInsertionPosition.AfterCurrent, C); await doc.EditRefresh;
        var retained = GC.GetTotalMemory(true); var clock = Stopwatch.StartNew();
        var results = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Field, "AFTER-FIELD", FieldPath: "OBX-6"), token: Token);
        var searchMs = clock.Elapsed.TotalMilliseconds;
        Assert.Equal(largeId, Assert.Single(results.Matches).MessageId); Assert.Null(doc.Source);
        Assert.Equal(inserted, doc.ActiveMessageId); Assert.Null(doc.Presentation);
        clock.Restart(); var allocated = GC.GetTotalAllocatedBytes();
        using (var capture = await doc.CaptureScriptAsync(1024L * 1024 * 1024, Path.Combine(root, "captures"), inserted, doc.SearchRevision, Token))
        {
            var captureMs = clock.Elapsed.TotalMilliseconds; var allocation = GC.GetTotalAllocatedBytes() - allocated;
            Assert.Equal(inserted, capture.Mapping.Resolve(capture.TargetOrdinal!.Value)!.MessageId);
            var ordinal = capture.Mapping.Messages.Single(m => m.MessageId == largeId).JobOrdinal;
            var capturedRange = capture.Index.Messages[ordinal - 1].PayloadRange;
            Assert.Equal(largeRange.Length, capturedRange.Length);
            var expected = new byte[65536]; var actual = new byte[65536];
            for (long offset = 0; offset < largeRange.Length;)
            {
                var count = (int)Math.Min(expected.Length, largeRange.Length - offset);
                doc.Snapshot!.Read(new(largeRange.Offset + offset, count), expected.AsSpan(0, count));
                capture.Snapshot.Read(new(capturedRange.Offset + offset, count), actual.AsSpan(0, count));
                Assert.True(expected.AsSpan(0, count).SequenceEqual(actual.AsSpan(0, count))); offset += count;
            }
            Assert.True(allocation < 16L * 1024 * 1024, $"Capture allocation {allocation}");
            Assert.True(GC.GetTotalMemory(true) - retained < 24L * 1024 * 1024);
            TestContext.Current.TestOutputHelper!.WriteLine($"compatibility largeMiB={mib}; searchMs={searchMs:F3}; captureMs={captureMs:F3}; captureAllocatedBytes={allocation}");
        }
        Assert.Null(doc.Source); Assert.Equal(C, doc.EditableText);
    }

    [Fact]
    public async Task MaximumLogicalMetadataSearchAndScriptMappingStayBoundedAcrossInsertionPositions()
    {
        var count = Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") == "1" ? 99_997 : 997;
        var doc = await Open(string.Concat(Enumerable.Repeat(A, count)));
        var beginning = doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
        await doc.NavigateAsync((count + 1) / 2, Token);
        var middle = doc.InsertMessage(MessageInsertionPosition.AfterCurrent, C); await doc.EditRefresh;
        var end = doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        var baseline = GC.GetTotalMemory(true); var clock = Stopwatch.StartNew();
        var search = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Text, "SYN"), token: Token);
        var searchMs = clock.Elapsed.TotalMilliseconds; Assert.Equal(count + 3, search.Matches.Count);
        doc.ApplySearchFilter(search); Assert.Equal(count + 3, doc.NavigationMatchIds!.Count);
        clock.Restart(); var allocated = GC.GetTotalAllocatedBytes();
        using var capture = await doc.CaptureScriptAsync(1024L * 1024 * 1024, Path.Combine(root, "captures"), null, doc.SearchRevision, Token);
        var captureMs = clock.Elapsed.TotalMilliseconds;
        Assert.Equal(doc.Messages.Select(m => m.Id), capture.Mapping.Messages.Select(m => m.MessageId));
        foreach (var id in new[] { beginning, middle, end })
        {
            var message = Assert.Single(capture.Mapping.Messages, m => m.MessageId == id);
            Assert.Equal(doc.GetLogicalPosition(id), message.LogicalPosition); Assert.Null(message.OriginalOrdinal);
        }
        var retained = GC.GetTotalMemory(true) - baseline; var allocations = GC.GetTotalAllocatedBytes() - allocated;
        Assert.True(retained < 128L * 1024 * 1024, $"Metadata retained {retained}");
        Assert.True(allocations < 128L * 1024 * 1024, $"Capture metadata allocated {allocations}");
        TestContext.Current.TestOutputHelper!.WriteLine($"compatibility messages={count + 3}; searchMs={searchMs:F3}; captureMs={captureMs:F3}; allocatedBytes={allocations}; retainedBytes={retained}");
    }
}
