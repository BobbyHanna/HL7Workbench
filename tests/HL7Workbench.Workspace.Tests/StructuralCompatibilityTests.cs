using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class StructuralCompatibilityTests : IAsyncDisposable
{
    private const string A = "MSH|^~\\&|SYN-A||||||ADT^A01|SYN-DUP|T|2.5\rPID|A\r";
    private const string B = "MSH|^~\\&|SYN-B||||||ADT^A08|SYN-DUP|T|2.5\rPID|B\r";
    private const string C = "MSH*$%!@*SYN-C******ADT$A01*SYN-DUP*T*2.5\rPID*C\r";
    private readonly DocumentManager documents = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-compatibility-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private sealed class InlineProgress<T>(Action<T> action) : IProgress<T>
    { public void Report(T value) => action(value); }
    private async Task<MessageDocument> Open(string text = A + B, int codePage = 65001)
    {
        Directory.CreateDirectory(root); var encoding = Encoding.GetEncoding(codePage);
        using var stream = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        return await MessageFileReader.OpenFileAsync(documents, "Synthetic.hl7", stream, Token);
    }
    public async ValueTask DisposeAsync()
    {
        var opened = documents.Documents.ToArray(); documents.Dispose();
        await Task.WhenAll(opened.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    private static async Task<string> ComparedText(MessageComparison comparison, bool before)
    {
        var window = await comparison.WindowAsync(before, token: Token);
        Assert.Equal(0, window.Origin); Assert.Equal(window.TotalLength, window.Text.Length); return window.Text;
    }
}
