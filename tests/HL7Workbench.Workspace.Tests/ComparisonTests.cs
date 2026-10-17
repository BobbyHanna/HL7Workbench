using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class ComparisonTests
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261016000000||ORU^R01|SYN-1|T|2.5\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static async Task<MessageDocument> Open(DocumentManager manager, string text, int codePage = 65001)
    {
        var encoding = Encoding.GetEncoding(codePage);
        using var input = new MemoryStream(encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        return await MessageFileReader.OpenFileAsync(manager, "Synthetic.hl7", input, Token);
    }
    [Fact]
    public async Task OriginalAndUnsavedDraftStayDistinctThroughUndoRedoAndSave()
    {
        using var manager = new DocumentManager(); var doc = await Open(manager, Header + "PID|A\r");
        doc.IsEditMode = true; doc.ReplaceWorkingText(Header + "PID|B\r");
        using var changed = await MessageComparison.CreateAsync(new(doc, 1, true), new(doc, 1), token: Token);
        var diff = Assert.Single(changed.Result.Differences); Assert.Equal("A", diff.BeforeValue); Assert.Equal("B", diff.AfterValue);
        Assert.Equal("B", await changed.ValueAsync(false, diff.After!.Range, Token));
        Assert.True(doc.IsDirty); Assert.True(doc.CanUndo); Assert.False(doc.CanRedo);
        var version = doc.SearchRevision; doc.Undo(); Assert.False(changed.IsCurrent); Assert.True(doc.SearchRevision > version);
        using var undone = await MessageComparison.CreateAsync(new(doc, 1, true), new(doc, 1), token: Token); Assert.Empty(undone.Result.Differences);
        doc.Redo(); Assert.False(undone.IsCurrent);
        var output = Path.Combine(Path.GetTempPath(), "hl7-comparison-" + Guid.NewGuid().ToString("N") + ".hl7");
        try
        {
            await doc.SaveAsync(output, Token);
            using var saved = await MessageComparison.CreateAsync(new(doc, 1, true), new(doc, 1), token: Token);
            Assert.Single(saved.Result.Differences); Assert.False(doc.IsDirty); Assert.True(doc.CanUndo);
        }
        finally { if (File.Exists(output)) File.Delete(output); }
    }
    [Fact]
    public async Task FilteredNavigatorUsesOriginalOrdinalsAndDoesNotNavigateOrMutate()
    {
        using var manager = new DocumentManager(); var doc = await Open(manager, Header + "PID|A\r" + Header + "PID|B\r" + Header + "PID|C\r");
        var results = await new MessageSearch().SearchAsync(doc, new(MessageSearchMode.Text, "PID|C"), token: Token);
        doc.ApplySearchFilter(results); Assert.Equal(new[] { 3 }, doc.NavigationMatches);
        var bytes = doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length)); var number = doc.MessageNumber; var generation = doc.ContentVersion;
        using var comparison = await MessageComparison.CreateAsync(new(doc, 1), new(doc, 3), token: Token);
        var difference = Assert.Single(comparison.Result.Differences); Assert.Equal("A", difference.BeforeValue); Assert.Equal("C", difference.AfterValue);
        Assert.Equal(number, doc.MessageNumber); Assert.Equal(generation, doc.ContentVersion); Assert.False(doc.IsDirty);
        Assert.Equal(new[] { 3 }, doc.NavigationMatches); Assert.Equal(bytes, doc.Snapshot.ReadBytes(new(0, doc.Snapshot.Length)));
    }
    [Theory]
    [InlineData(65001)] [InlineData(1200)] [InlineData(1201)] [InlineData(12000)] [InlineData(12001)]
    public async Task CrossDocumentCapturesRespectEncodingAndIgnoreActiveNavigation(int encoding)
    {
        using var manager = new DocumentManager(); var a = await Open(manager, Header + "PID|Äda\r", encoding);
        var b = await Open(manager, Header + "PID|Other\r" + Header + "PID|Béa\r", encoding);
        using var comparison = await MessageComparison.CreateAsync(new(a, 1), new(b, 2), token: Token);
        var difference = Assert.Single(comparison.Result.Differences); Assert.Equal("Äda", difference.BeforeValue); Assert.Equal("Béa", difference.AfterValue);
        await b.NavigateAsync(2, Token); manager.Activate(a.Id); Assert.True(comparison.IsCurrent);
        Assert.Equal(Header + "PID|Béa\r", (await comparison.WindowAsync(false, token: Token)).Text);
    }
    [Fact]
    public async Task ClosingSourceInvalidatesResultAndDrainsOwnedReadersBeforeDeletingSnapshot()
    {
        using var manager = new DocumentManager(); var doc = await Open(manager, Header + "PID|A\r");
        var comparison = await MessageComparison.CreateAsync(new(doc, 1), new(doc, 1), token: Token);
        var path = doc.Snapshot!.DirectoryPath;
        Assert.True(manager.Close(doc.Id)); Assert.False(comparison.IsCurrent);
        Assert.True(Directory.Exists(path)); Assert.False(doc.Disposal.IsCompleted);
        await Assert.ThrowsAsync<InvalidOperationException>(() => comparison.WindowAsync(true, token: Token));
        comparison.Dispose(); await comparison.Disposal; await doc.Disposal.WaitAsync(TimeSpan.FromSeconds(10), Token);
        Assert.False(Directory.Exists(path));
    }
    [Fact]
    public async Task CancelAndCloseDuringCaptureReleaseLeases()
    {
        using var manager = new DocumentManager(); var doc = await Open(manager, Header + "OBX|" + new string('A', 2 * 1024 * 1024) + "\r");
        var running = MessageComparison.CreateAsync(new(doc, 1), new(doc, 1), token: Token);
        manager.Close(doc.Id);
        try { using var result = await running; Assert.False(result.IsCurrent); }
        catch (OperationCanceledException) { }
        await doc.Disposal.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }
    [Fact]
    public async Task RepeatedPreCanceledRequestsReleaseBothLeasesAndDoNotRetainSpools()
    {
        using var manager = new DocumentManager(); var doc = await Open(manager, Header + "PID|A\r");
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        for (var i = 0; i < 40; i++)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MessageComparison.CreateAsync(new(doc, 1), new(doc, 1), token: cancellation.Token));
        manager.Close(doc.Id); await doc.Disposal.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }
    [Fact]
    public async Task SourceBackedValuesKeepFullRangesAndRefusePreviewCopies()
    {
        using var manager = new DocumentManager(); var a = await Open(manager, Header + "OBX|" + new string('A', 2 * 1024 * 1024) + "\r");
        var b = await Open(manager, Header + "OBX|" + new string('B', 2 * 1024 * 1024) + "\r");
        using var comparison = await MessageComparison.CreateAsync(new(a, 1), new(b, 1), token: Token);
        var difference = Assert.Single(comparison.Result.Differences); Assert.True(difference.BeforeAbbreviated);
        var window = await comparison.WindowAsync(true, difference.Before!.Range, token: Token);
        Assert.Equal(MessageComparison.WindowCharacters, window.Text.Length); Assert.Contains("Source window", window.Scope);
        var tail = await comparison.WindowAsync(false, origin: long.MaxValue, token: Token); Assert.EndsWith("B\r", tail.Text); Assert.Equal(tail.TotalLength, tail.Origin + tail.Text.Length);
        await Assert.ThrowsAsync<Hl7LimitException>(() => comparison.ValueAsync(true, difference.Before.Range, Token));
        Assert.False(a.IsDirty); Assert.False(b.IsDirty);
    }
    [Fact]
    public async Task InvalidSecondSourceAndDisposalDuringReadReleaseEveryResource()
    {
        using var manager = new DocumentManager(); var doc = await Open(manager, Header + "PID|A\r");
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => MessageComparison.CreateAsync(new(doc, 1), new(doc, 2), token: Token));
        var comparison = await MessageComparison.CreateAsync(new(doc, 1), new(doc, 1), token: Token);
        var reading = comparison.WindowAsync(true, token: Token); comparison.Dispose();
        try { await reading; } catch (ObjectDisposedException) { } catch (InvalidOperationException) { }
        await comparison.Disposal; comparison.Dispose(); manager.Close(doc.Id); await doc.Disposal.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }
    [Fact]
    public async Task InitiallyLoadedInMemoryMessageAndMalformedDraftRemainReadable()
    {
        using var manager = new DocumentManager(); var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Header + "PID|A\r")); manager.Open(doc);
        doc.IsEditMode = true; doc.ReplaceWorkingText("temporarily malformed draft\r");
        using var comparison = await MessageComparison.CreateAsync(new(doc, 1, true), new(doc, 1), token: Token);
        Assert.NotEmpty(comparison.Result.Differences); Assert.Equal(Header + "PID|A\r", (await comparison.WindowAsync(true, token: Token)).Text);
        Assert.Equal("temporarily malformed draft\r", (await comparison.WindowAsync(false, token: Token)).Text); Assert.True(doc.CanUndo);
    }
    [Fact]
    public async Task EscapeEquivalenceUsesEachCapturedSourceEncoding()
    {
        using var manager = new DocumentManager(); var a = await Open(manager, Header + "PID|\\X4100\\\r", 1200); var b = await Open(manager, Header + "PID|A\r");
        using var comparison = await MessageComparison.CreateAsync(new(a, 1), new(b, 1), token: Token);
        Assert.Equal(Hl7DifferenceKind.SourceSpelling, Assert.Single(comparison.Result.Differences).Kind);
    }
    [Fact]
    public async Task SourceWindowBoundariesDoNotSplitSurrogatePairs()
    {
        var prefix = Header + "OBX|";
        var text = prefix + new string('A', MessageComparison.WindowCharacters - prefix.Length - 1) + "😀" + new string('B', 90000) + "\r";
        using var manager = new DocumentManager(); var doc = await Open(manager, text);
        using var comparison = await MessageComparison.CreateAsync(new(doc, 1), new(doc, 1), token: Token);
        var first = await comparison.WindowAsync(true, token: Token); Assert.Equal(MessageComparison.WindowCharacters - 1, first.Text.Length);
        var middle = await comparison.WindowAsync(true, origin: MessageComparison.WindowCharacters, token: Token);
        Assert.False(char.IsLowSurrogate(middle.Text[0])); Assert.False(char.IsHighSurrogate(middle.Text[^1]));
        Assert.Equal(text.Substring((int)middle.Origin, middle.Text.Length), middle.Text);
        var end = await comparison.WindowAsync(true, origin: long.MaxValue, token: Token); Assert.Equal(text.Length, end.Origin + end.Text.Length);
    }
    public static IEnumerable<object[]> LargeSizes()
    {
        yield return [8];
        if (Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") == "1") yield return [32];
    }
    [Theory]
    [MemberData(nameof(LargeSizes))]
    public async Task LargeComparisonKeepsResidentMemoryBoundedAndDeletesPrivateSpools(int mib)
    {
        using var manager = new DocumentManager(); var doc = await Open(manager, Header + "OBX|" + new string('A', mib * 1024 * 1024) + "\r");
        var files = Directory.GetFiles(doc.Snapshot!.DirectoryPath).Order().ToArray(); var baseline = GC.GetTotalMemory(true);
        var comparison = await MessageComparison.CreateAsync(new(doc, 1), new(doc, 1), token: Token);
        Assert.Empty(comparison.Result.Differences);
        Assert.True(GC.GetTotalMemory(true) - baseline < 24 * 1024 * 1024, "Selected-message comparison must not retain payload-sized strings.");
        Assert.Equal(files.Length + 2, Directory.GetFiles(doc.Snapshot.DirectoryPath).Length);
        comparison.Dispose(); await comparison.Disposal; Assert.Equal(files, Directory.GetFiles(doc.Snapshot.DirectoryPath).Order());
        manager.Close(doc.Id); await doc.Disposal.WaitAsync(TimeSpan.FromSeconds(10), Token);
    }
}
