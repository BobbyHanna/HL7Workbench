using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class Stage3LifecycleTests
{
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(12000)] [InlineData(12001)]
    public async Task SplitUnicodeSequencesAndSurrogateCacheBoundariesRemainExact(int codePage)
    {
        var token = TestContext.Current.CancellationToken;
        var encoding = Encoding.GetEncoding(codePage);
        var text = "PID|" + new string('A', 32763) + "😀" + new string('B', 32763) + "é😀TAIL\r";
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        using var stream = new TinyReads(bytes); using var snapshot = await SourceSnapshot.CaptureAsync(stream, token);
        var index = MessageFileIndex.Build(snapshot, token);
        using var source = DecodedMessageSource.Create(snapshot, index.Messages[0].PayloadRange, index.Encoding, 0, token);
        Assert.Equal(text, string.Concat(source.ReadChunks(new(0, source.Length), 4096)));
        var emoji = text.IndexOf("😀", StringComparison.Ordinal);
        Assert.Equal(emoji, source.Align(emoji + 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => source.BytePosition(emoji + 1));
        var range = source.Bytes(new(emoji, 2))!.Value;
        Assert.Equal(encoding.GetBytes("😀"), snapshot.ReadBytes(new(index.Messages[0].PayloadRange.Offset + range.Offset, range.Length)));
    }
    [Theory]
    [InlineData("\vMSH|^~\\&|SYN\rPID|A\x1c\r", 1, false)]
    [InlineData("\vMSH|^~\\&|SYN\rPID|A\vMSH|^~\\&|SYN\rPID|B", 2, true)]
    [InlineData("\x1c\rMSH|^~\\&|SYN", 1, false)]
    public async Task MarkerRecoveryWorksAtTheEndOfLongUnterminatedLines(string text, int count, bool incomplete)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        using var snapshot = await SourceSnapshot.CaptureAsync(stream, TestContext.Current.CancellationToken);
        var index = MessageFileIndex.Build(snapshot, TestContext.Current.CancellationToken);
        Assert.Equal(count, index.Messages.Count);
        Assert.Equal(incomplete, index.Messages.SelectMany(m => m.Diagnostics).Any(d => d.Code == "IncompleteMllp"));
        Assert.DoesNotContain(index.Messages.Where(m => m.Framed && !incomplete), m => snapshot.ReadBytes(m.PayloadRange).Contains((byte)0x1C));
    }
    [Fact]
    public async Task InteriorBomsAreReportedAndNeverSelectAnotherEncoding()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rPID|A\uFEFFB"));
        using var snapshot = await SourceSnapshot.CaptureAsync(stream, TestContext.Current.CancellationToken);
        var index = MessageFileIndex.Build(snapshot, TestContext.Current.CancellationToken);
        Assert.Equal(65001, index.Encoding.CodePage);
        Assert.Contains(index.Messages[0].Diagnostics, d => d.Code == "InteriorBom");
    }
    [Fact]
    public async Task FailedAndCanceledCaptureNeverPublishesOrLeavesPartialStorage()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            using var source = new FailingStream();
            await Assert.ThrowsAsync<IOException>(() => SourceSnapshot.CaptureAsync(source, TestContext.Current.CancellationToken, storageRoot: root));
            Assert.Empty(Directory.GetDirectories(root));
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); cancellation.Cancel();
            using var input = new MemoryStream([1, 2, 3]);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SourceSnapshot.CaptureAsync(input, cancellation.Token, storageRoot: root));
            Assert.Empty(Directory.GetDirectories(root));
            using var changing = new ChangingStream();
            await Assert.ThrowsAsync<IOException>(() => SourceSnapshot.CaptureAsync(changing, TestContext.Current.CancellationToken, storageRoot: root));
            Assert.Empty(Directory.GetDirectories(root));
        }
        finally { Directory.Delete(root); }
    }
    [Fact]
    public async Task CleanupRemovesOnlyOldUnlockedOwnedDirectories()
    {
        var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        var abandoned = Path.Combine(root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(abandoned);
        await File.WriteAllTextAsync(Path.Combine(abandoned, "lease"), "", TestContext.Current.CancellationToken);
        Directory.SetLastWriteTimeUtc(abandoned, DateTime.UtcNow.AddDays(-2));
        var unrelated = Path.Combine(root, "unrelated"); Directory.CreateDirectory(unrelated);
        try
        {
            using var input = new MemoryStream([1]);
            using var snapshot = await SourceSnapshot.CaptureAsync(input, TestContext.Current.CancellationToken, storageRoot: root);
            Assert.False(Directory.Exists(abandoned)); Assert.True(Directory.Exists(unrelated));
            Directory.SetLastWriteTimeUtc(snapshot.DirectoryPath, DateTime.UtcNow.AddDays(-2));
            using var otherInput = new MemoryStream([2]);
            using var other = await SourceSnapshot.CaptureAsync(otherInput, TestContext.Current.CancellationToken, storageRoot: root);
            Assert.True(Directory.Exists(snapshot.DirectoryPath));
        }
        finally { Directory.Delete(unrelated); Directory.Delete(root); }
    }
    [Fact]
    public async Task SourceDeletionAfterCaptureDoesNotAffectNavigation()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".synthetic");
        await File.WriteAllTextAsync(path, "MSH|^~\\&|SYN\rPID|ONE\rMSH|^~\\&|SYN\rPID|TWO", TestContext.Current.CancellationToken);
        using var manager = new DocumentManager();
        MessageDocument document;
        await using (var stream = File.OpenRead(path)) document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", stream, TestContext.Current.CancellationToken);
        File.Delete(path);
        await document.NavigateAsync(2, TestContext.Current.CancellationToken);
        Assert.Equal("TWO", document.Message.Get("PID-1").Raw);
        manager.Close(document.Id); await document.Disposal;
    }
    [Fact]
    public async Task StructuralExhaustionRetainsTextAndCloseDuringScanningCleansResources()
    {
        using var manager = new DocumentManager();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|SYN\rPID|A~B\rMSH|^~\\&|SYN\rPID|C"));
        var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", stream, TestContext.Current.CancellationToken,
            inspectionOptions: new() { MaxElements = 4 });
        Assert.Contains(document.ActiveDiagnostics, d => d.Code == "InspectionIncomplete");
        Assert.Null(document.Structure); Assert.Null(document.ActiveInspection); Assert.NotNull(document.Source);
        Assert.Contains("PID|A~B", document.Source.ReadWindow(0));
        var pending = document.NavigateAsync(2, TestContext.Current.CancellationToken);
        manager.Close(document.Id); await pending; await document.Disposal;
        Assert.False(Directory.Exists(document.Snapshot!.DirectoryPath));
    }
    [Fact]
    public async Task InvalidUnclassifiedInputReportsEncodingFailureAndInheritedEncodingCannotBeMutated()
    {
        using var stream = new MemoryStream([0xFF, 0x81]);
        using var manager = new DocumentManager();
        var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", stream, TestContext.Current.CancellationToken);
        Assert.Equal(0, document.MessageCount); Assert.NotNull(document.Failure);
        Assert.Contains(document.Index!.Diagnostics, d => d.Code == "InvalidEncoding");
        document.Index.Encoding.DecoderFallback = DecoderFallback.ReplacementFallback;
        Assert.IsType<DecoderExceptionFallback>(document.Index.Encoding.DecoderFallback);
        manager.Close(document.Id); await document.Disposal;
    }
    [Theory]
    [InlineData(1200)] [InlineData(1201)] [InlineData(12000)] [InlineData(12001)]
    public async Task WideEncodedPayloadAndRawMllpEnvelopeUseSeparateAlignment(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage);
        var payload = encoding.GetBytes("MSH|^~\\&|SYN\rPID|é😀\r");
        var bytes = encoding.GetPreamble().Concat(new byte[] { 11 }).Concat(payload).Concat(new byte[] { 28, 13 }).ToArray();
        using var input = new MemoryStream(bytes); using var manager = new DocumentManager();
        var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
        Assert.Equal(1, document.MessageCount); Assert.True(document.Index!.Messages[0].Framed);
        Assert.Null(document.Failure); Assert.Equal("é😀", document.Message.Get("PID-1").Raw);
        Assert.Equal(payload.Length, document.Index.Messages[0].PayloadRange.Length);
        manager.Close(document.Id); await document.Disposal;
    }
    private sealed class TinyReads(byte[] bytes) : MemoryStream(bytes)
    {
        private int reads;
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            base.ReadAsync(buffer[..Math.Min(reads++ < 4 ? 1 : 1023, buffer.Length)], cancellationToken);
    }
    private sealed class FailingStream : MemoryStream
    {
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => throw new IOException("Synthetic read failure.");
    }
    private sealed class ChangingStream : MemoryStream
    {
        public ChangingStream() { Write([1, 2, 3]); Position = 0; }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        { SetLength(4); return base.ReadAsync(buffer, cancellationToken); }
    }
}
