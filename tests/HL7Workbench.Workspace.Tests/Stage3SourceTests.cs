using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class Stage3SourceTests
{
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("MSH|^~\\&|SYN\rPID|A\rMSH*$%!@*SYN\nPID*B", 2)]
    [InlineData("\vMSH|^~\\&|SYN\rPID|A\r\x1c\r\vMSH|bad|SYN\n\x1c\r", 2)]
    [InlineData("MSH|bad|SYN\n\vMSH|^~\\&|SYN\r\x1c\rMSH\rPID|X", 3)]
    [InlineData("\vMSH|^~\\&|SYN\r\vMSH|^~\\&|SYN", 2)]
    [InlineData("FHS|SYN\rBHS|SYN\rMSH|^~\\&|SYN\rBTS|1\rFTS|1", 1)]
    [InlineData("PID|SYN\rOBX|A", 1)]
    [InlineData("bad line\rPID|SYN\rOBX|A", 1)]
    [InlineData("FHS|SYN\rBHS|SYN\rPID|SYN\rBTS|1\rFTS|1", 1)]
    [InlineData("FHS|SYN\rBHS|SYN\rBTS|1\rFTS|1", 0)]
    [InlineData("unrelated content", 0)]
    [InlineData("", 0)]
    public async Task IndexPartitionsEveryByteWithDeterministicRecovery(string text, int count)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var input = new MemoryStream(bytes);
        using var source = await SourceSnapshot.CaptureAsync(input, Token);
        var index = MessageFileIndex.Build(source, Token);
        Assert.Equal(count, index.Messages.Count);
        long end = 0;
        foreach (var region in index.Regions)
        { Assert.Equal(end, region.Range.Offset); end = region.Range.End; }
        Assert.Equal(bytes.Length, end);
        Assert.Equal(bytes, source.ReadBytes(new(0, source.Length)));
        Assert.All(index.Messages, m => { Assert.True(m.PayloadRange.Offset >= m.SourceRange.Offset); Assert.True(m.PayloadRange.End <= m.SourceRange.End); });
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(12000)] [InlineData(12001)]
    public async Task EncodingAndSparseCoordinatesMatchOriginalBytes(int codePage)
    {
        var encoding = Encoding.GetEncoding(codePage);
        var text = "MSH|^~\\&|SYN\rPID|é😀\rMSH|^~\\&|SYN\nOBX|TAIL";
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        using var input = new MemoryStream(bytes);
        using var snapshot = await SourceSnapshot.CaptureAsync(input, Token);
        var index = MessageFileIndex.Build(snapshot, Token);
        Assert.Equal(2, index.Messages.Count);
        Assert.Equal(codePage, index.Encoding.CodePage);
        using var decoded = DecodedMessageSource.Create(snapshot, index.Messages[0].PayloadRange, index.Encoding, 0, Token);
        var first = text[..text.LastIndexOf("MSH", StringComparison.Ordinal)];
        Assert.Equal(first, decoded.Read(new(0, decoded.Length)));
        var range = new CharacterRange(first.IndexOf('é'), 3);
        var mapped = decoded.Bytes(range)!.Value;
        Assert.Equal(encoding.GetBytes("é😀"), snapshot.ReadBytes(new(index.Messages[0].PayloadRange.Offset + mapped.Offset, mapped.Length)));
    }
    [Theory]
    [InlineData("PID||\"\"|A^B&C~D\r\n\nbad")]
    [InlineData("MSH*$%!@*SYN\rPID*A$B@C%D*!Zwith$delimiter!\r")]
    [InlineData("MSH|^^^^|SYN\rPID|A\\broken^B~C")]
    [InlineData("MSH|^~|SYN\rMSH|^~|SYN\rPID|A&B\\C")]
    public async Task SourceBackedAndMaterializedInspectionAgree(string text)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        using var snapshot = await SourceSnapshot.CaptureAsync(input, Token);
        using var source = DecodedMessageSource.Create(snapshot, new(0, snapshot.Length), Encoding.UTF8, 0, Token);
        var structure = Hl7StructureScanner.Scan(source, Encoding.UTF8, cancellationToken: Token);
        var message = Hl7Parser.Parse(text);
        Assert.Equal(message.Diagnostics.Select(d => (d.Code, d.CharacterOffset)), structure.Diagnostics.Select(d => (d.Code, (int?)d.CharacterOffset)));
        var old = new MessageInspection(message); var current = new MessageInspection(structure, source);
        for (var i = 0; i < text.Length; i++)
        {
            var expected = old.ResolveOffset(i); var actual = current.ResolveOffset(i);
            Assert.Equal(expected?.Path, actual?.Path);
            Assert.Equal(expected?.Range, actual?.Range);
            Assert.Equal(expected?.State, actual?.State);
            if (actual is not null) Assert.Equal(expected!.Raw, current.ReadPreview(actual));
        }
    }
    [Fact]
    public async Task ThousandsOfMessagesDoNotProduceEagerModelsAndSnapshotIsIndependent()
    {
        var bytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("MSH|^~\\&|SYN\rPID|X\r", 10000)));
        using var input = new MemoryStream(bytes);
        using var snapshot = await SourceSnapshot.CaptureAsync(input, Token);
        await File.WriteAllBytesAsync(Path.Combine(HL7Workbench.Tests.LargeSyntheticFile.ArtifactDirectory, "synthetic-10000-messages.hl7"), bytes, Token);
        Array.Fill(bytes, (byte)0);
        var index = MessageFileIndex.Build(snapshot, Token);
        Assert.Equal(10000, index.Messages.Count);
        Assert.Equal((byte)'M', snapshot.ReadBytes(new(0, 1))[0]);
        var path = snapshot.DirectoryPath;
        snapshot.Dispose();
        Assert.False(Directory.Exists(path));
    }
    [Fact]
    public async Task BudgetAndInvalidEncodingKeepRawAccessAndNavigation()
    {
        using var input = new MemoryStream([0x4D, 0x53, 0x48, 0x7C, 0xFF, 0x0D]);
        using var manager = new DocumentManager();
        var document = await MessageFileReader.OpenFileAsync(manager, "synthetic", input, Token);
        Assert.Equal(1, document.MessageCount);
        Assert.Contains(document.ActiveDiagnostics, d => d.Code == "InvalidEncoding");
        Assert.NotNull(document.Failure);
        Assert.Equal(0xFF, document.Snapshot!.ReadBytes(new(4, 1))[0]);
        manager.Close(document.Id); await document.Disposal;
        Assert.False(Directory.Exists(document.Snapshot.DirectoryPath));
        Assert.Throws<Hl7LimitException>(() => Hl7StructureScanner.Scan(new StringHl7TextSource("PID|A~B"), Encoding.UTF8, new() { MaxElements = 4 }, Token));
    }
    [Fact]
    public async Task RapidNavigationAndClosingCannotPublishStaleContent()
    {
        using var manager = new DocumentManager();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("MSH|^~\\&|ONE\rPID|ONE\rMSH|^~\\&|TWO\rPID|TWO"));
        var document = await MessageFileReader.OpenFileAsync(manager, "synthetic", input, Token);
        var tasks = Enumerable.Range(0, 20).Select(i => document.NavigateAsync(i % 2 + 1, Token)).ToArray();
        await Task.WhenAll(tasks);
        Assert.Equal(2, document.MessageNumber);
        Assert.Equal("TWO", document.Message.Get("PID-1").Raw);
        Assert.Null(document.Inspection.SelectedNode);
        manager.Close(document.Id); await document.Disposal;
        Assert.Empty(manager.Documents);
    }
}
