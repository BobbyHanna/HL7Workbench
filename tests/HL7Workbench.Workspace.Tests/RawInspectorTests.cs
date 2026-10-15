using System.Text;
using System.Diagnostics;
using System.Text.Json;
using HL7Workbench.Core;
using HL7Workbench.Tests;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class RawInspectorTests
{
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(12000)] [InlineData(12001)]
    public async Task OriginalValueExportIsExactForAllEncodingsAndCustomDelimiters(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
        var value = "é😀" + new string('Q', 20000) + "Ω";
        var text = "MSH*$%!@*SYN\r\nZXY*" + value + "*AFTER\r\nNTE*1**TAIL";
        using var input = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        using var manager = new DocumentManager();
        var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
        var directory = Path.Combine(Path.GetTempPath(), "hl7-export-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            var marker = Assert.Single(doc.Presentation!.Fragments, f => f.Marker is not null);
            using var session = new RawInspectorSession(doc, marker.Omitted, "ZXY-1");
            Assert.Contains("ZXY-1 — Message 1", session.Label);
            var first = await session.ReadAsync(0, TestContext.Current.CancellationToken);
            Assert.StartsWith("é😀", first!.Text); Assert.DoesNotContain("omitted", first.Text);
            Assert.Equal(RawInspectorSession.ChunkCharacters, first.Text.Length);
            var last = await session.ReadAsync(value.Length - 20, TestContext.Current.CancellationToken);
            Assert.EndsWith("Ω", last!.Text); Assert.Equal(value.Length - 20, last.ScopeOffset);
            Assert.Equal(marker.Omitted!.Value.Offset + last.ScopeOffset, last.MessageOffset);
            var path = Path.Combine(directory, "encoded.dat");
            await session.ExportAsync(path, TestContext.Current.CancellationToken);
            Assert.Equal(encoding.GetBytes(value), await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
            Assert.Single(Directory.GetFiles(directory));
            using var whole = new RawInspectorSession(doc);
            Assert.Equal("Full Message — Message 1", whole.Label);
            var fullStart = await whole.ReadAsync(0, TestContext.Current.CancellationToken); Assert.StartsWith("MSH", fullStart!.Text);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => session.ReadAsync(2, TestContext.Current.CancellationToken));
        }
        finally { manager.Close(doc.Id); await doc.Disposal; Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task OrdinaryMessageScopesExportExactSnapshotBytesAndClosingInspectorRetainsDocument()
    {
        using var manager = new DocumentManager(); using var input = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rPID|é😀|NEXT"));
        var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
        var output = Path.Combine(Path.GetTempPath(), "hl7-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            doc.Inspection.SelectSegment(doc.Inspection.Segments[1]); var value = doc.Inspection.InspectField(1)!;
            using (var session = new RawInspectorSession(doc, value.Range64, value.DisplayPath))
            { await session.ExportAsync(output, TestContext.Current.CancellationToken); Assert.Equal(Encoding.UTF8.GetBytes("é😀"), await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken)); }
            Assert.Same(doc, manager.ActiveDocument); Assert.NotNull(doc.Snapshot); Assert.NotNull(doc.MaterializedMessage);
            doc.Message.SetText("PID-1", "CHANGED");
            using var changed = new RawInspectorSession(doc, value.Range64, value.DisplayPath);
            Assert.False(changed.CanExport);
            await Assert.ThrowsAsync<InvalidOperationException>(() => changed.ExportAsync(output, TestContext.Current.CancellationToken));
            Assert.Equal(Encoding.UTF8.GetBytes("é😀"), await File.ReadAllBytesAsync(output, TestContext.Current.CancellationToken));
        }
        finally { manager.Close(doc.Id); await doc.Disposal; File.Delete(output); }
    }
    [Fact]
    public async Task MessageAndDocumentClosureCancelBorrowedSessionsWithoutCrossDocumentChanges()
    {
        using var manager = new DocumentManager();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|ONE\rPID|1\rMSH|^~\\&|TWO\rPID|2"));
        var first = await MessageFileReader.OpenFileAsync(manager, "Synthetic first", input, TestContext.Current.CancellationToken);
        using var otherInput = new MemoryStream(Encoding.UTF8.GetBytes("PID|OTHER"));
        var other = await MessageFileReader.OpenFileAsync(manager, "Synthetic other", otherInput, TestContext.Current.CancellationToken);
        try
        {
            using var a = new RawInspectorSession(first); using var b = new RawInspectorSession(other);
            await first.NavigateAsync(2, TestContext.Current.CancellationToken); Assert.True(a.IsClosed); Assert.False(b.IsClosed);
            using var c = new RawInspectorSession(first); manager.Close(first.Id); Assert.True(c.IsClosed); await first.Disposal;
            Assert.False(b.IsClosed); Assert.Contains("OTHER", (await b.ReadAsync(0, TestContext.Current.CancellationToken))!.Text);
        }
        finally { manager.Close(other.Id); await other.Disposal; }
    }
    [Fact]
    public async Task CanceledAndFailedExportsDoNotPublishPartialFilesOrReplaceOriginal()
    {
        var directory = Path.Combine(Path.GetTempPath(), "hl7-export-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        var original = Path.Combine(directory, "original.hl7"); await File.WriteAllTextAsync(original, "PID|SYN", TestContext.Current.CancellationToken);
        using var manager = new DocumentManager(); await using var input = File.OpenRead(original);
        var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
        try
        {
            using var session = new RawInspectorSession(doc);
            await Assert.ThrowsAsync<IOException>(() => session.ExportAsync(original, TestContext.Current.CancellationToken));
            using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); cancel.Cancel();
            var target = Path.Combine(directory, "output.dat");
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => session.ExportAsync(target, cancel.Token)); Assert.False(File.Exists(target));
            var writeError = await Record.ExceptionAsync(() => session.ExportAsync(directory, TestContext.Current.CancellationToken));
            Assert.True(writeError is IOException or UnauthorizedAccessException);
            Assert.Equal("PID|SYN", await File.ReadAllTextAsync(original, TestContext.Current.CancellationToken));
            Assert.Single(Directory.GetFiles(directory));
            await Assert.ThrowsAsync<IOException>(() => session.ExportAsync(Path.Combine(doc.Snapshot!.DirectoryPath, "source.bin"), TestContext.Current.CancellationToken));
        }
        finally { manager.Close(doc.Id); await doc.Disposal; await input.DisposeAsync(); Directory.Delete(directory, true); }
    }
    [Fact]
    public async Task LargeExportStreamsWithoutValueSizedAllocations()
    {
        var path = await LargeSyntheticFile.CreateAsync(20L * 1024 * 1024, TestContext.Current.CancellationToken);
        using var manager = new DocumentManager(); await using var input = File.OpenRead(path);
        var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
        var output = Path.Combine(Path.GetTempPath(), "hl7-large-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var marker = Assert.Single(doc.Presentation!.Fragments, f => f.Marker is not null);
            using var session = new RawInspectorSession(doc, marker.Omitted, "OBX-5.5");
            var before = GC.GetTotalAllocatedBytes(true);
            var timer = Stopwatch.StartNew();
            await session.ExportAsync(output, TestContext.Current.CancellationToken);
            var elapsedMs = timer.Elapsed.TotalMilliseconds; var allocatedBytes = GC.GetTotalAllocatedBytes(true) - before;
            Assert.True(allocatedBytes < 8L * 1024 * 1024);
            Assert.Equal(20L * 1024 * 1024, new FileInfo(output).Length);
            await using var check = File.OpenRead(output); check.Seek(-8, SeekOrigin.End); var tail = new byte[8]; await check.ReadExactlyAsync(tail, TestContext.Current.CancellationToken);
            Assert.Equal("VEFJTA==", Encoding.ASCII.GetString(tail));
            await File.WriteAllTextAsync(Path.Combine(LargeSyntheticFile.ArtifactDirectory, "export-20MiB.json"),
                JsonSerializer.Serialize(new { encodedMiB = 20, elapsedMs, allocatedBytes }, new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        }
        finally { manager.Close(doc.Id); await doc.Disposal; File.Delete(output); }
    }
}
