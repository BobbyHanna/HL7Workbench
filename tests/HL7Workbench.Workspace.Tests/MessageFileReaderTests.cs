using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class MessageFileReaderTests
{
    [Theory]
    [InlineData(65001)]
    [InlineData(1200)]
    [InlineData(1201)]
    [InlineData(12000)]
    [InlineData(12001)]
    public async Task FileBytesBomAndMixedTerminatorsSurviveOpening(int codePage)
    {
        const string text = "MSH|^~\\&|SYN|||||||SYN-1||99.7\rPID|1||SYN-é||Example^Ada|\r\n\nbad line\nOBX|1|TX|||\\Zsynthetic\\";
        var encoding = Encoding.GetEncoding(codePage);
        var bytes = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".dat");
        try
        {
            await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
            await using var source = File.OpenRead(path);
            var message = await MessageFileReader.ReadAsync(source, TestContext.Current.CancellationToken);
            Assert.Equal(text, message.RawText);
            Assert.Equal(bytes, message.OriginalBytes);
            Assert.Equal(bytes, Hl7Serializer.Serialize(message));
            Assert.Equal(codePage, message.Encoding.CodePage);
            Assert.Equal(encoding.GetPreamble(), message.ByteOrderMark);
            Assert.False(message.IsModified);
            Assert.Equal("99.7", new MessageDocument("id", "file", message).DeclaredVersion);
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad line\rPID|1||SYN\n")]
    [InlineData("MSH|bad|SYN\rPID|1")]
    [InlineData("MSH|^~\\&|SYN\rMSH*$%!@*SYN\r")]
    public async Task MalformedAndEmptyInputIsRetained(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var stream = new MemoryStream(bytes);
        var message = await MessageFileReader.ReadAsync(stream, TestContext.Current.CancellationToken);
        Assert.Equal(text, message.RawText);
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
    }

    [Fact]
    public async Task BomOnlyInputIsDecodedEmptyWithRetainedPreamble()
    {
        var bytes = Encoding.UTF8.GetPreamble();
        using var stream = new MemoryStream(bytes);
        var message = await MessageFileReader.ReadAsync(stream, TestContext.Current.CancellationToken);
        Assert.True(message.HasDecodedText);
        Assert.Equal(string.Empty, message.RawText);
        Assert.Equal(bytes, message.ByteOrderMark);
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
    }

    [Fact]
    public async Task InvalidEncodingRetainsBytesWithoutInventingText()
    {
        byte[] bytes = [0xFF, 0x81];
        using var stream = new MemoryStream(bytes);
        var message = await MessageFileReader.ReadAsync(stream, TestContext.Current.CancellationToken);
        Assert.False(message.HasDecodedText);
        Assert.Equal(bytes, message.OriginalBytes);
        Assert.Equal(bytes, Hl7Serializer.Serialize(message));
    }

    [Fact]
    public async Task OversizedInputAndCancellationDoNotPublishDocuments()
    {
        using var stream = new MemoryStream(new byte[new Hl7ParseOptions().MaxMessageBytes + 1]);
        await Assert.ThrowsAsync<Hl7LimitException>(() => MessageFileReader.ReadAsync(stream, TestContext.Current.CancellationToken));
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MessageFileReader.ReadAsync(stream, canceled.Token));
    }

    [Fact]
    public void RepeatedAndCaseVariantFilenamesHaveDistinctTitlesAndIdentities()
    {
        var manager = new DocumentManager();
        var message = Hl7Parser.Parse("PID|1");
        var one = MessageFileReader.Open(manager, "synthetic.hl7", message);
        var two = MessageFileReader.Open(manager, "Synthetic.hl7", message);
        var three = MessageFileReader.Open(manager, "synthetic.hl7", message);
        Assert.Equal("synthetic.hl7", one.Title);
        Assert.Equal("Synthetic.hl7 (2)", two.Title);
        Assert.Equal("synthetic.hl7 (3)", three.Title);
        Assert.Equal(3, manager.Documents.Select(d => d.Id).Distinct().Count());
        Assert.Same(three, manager.ActiveDocument);
        manager.Close(two.Id);
        Assert.Same(three, manager.ActiveDocument);
    }
}
