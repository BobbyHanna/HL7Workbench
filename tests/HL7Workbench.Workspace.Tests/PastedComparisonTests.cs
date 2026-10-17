using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class PastedComparisonTests
{
    private const string Header = "MSH|^~\\&|SYN|TEST|DEST|TEST|20261016000000||ORU^R01|SYN-PASTE|T|2.5\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("\r")] [InlineData("\n")] [InlineData("\r\n")]
    public async Task IndependentPastedSourcesRetainClipboardTerminatorsAndRanges(string ending)
    {
        var a = (Header + "PID|A\r").Replace("\r", ending, StringComparison.Ordinal);
        var b = (Header + "PID|B\r").Replace("\r", ending, StringComparison.Ordinal);
        using var comparison = await MessageComparison.CreateAsync(ComparisonSource.FromPastedText(a), ComparisonSource.FromPastedText(b), token: Token);
        var d = Assert.Single(comparison.Result.Differences); Assert.Equal("A", d.BeforeValue); Assert.Equal("B", d.AfterValue);
        Assert.Equal(a, (await comparison.WindowAsync(true, token: Token)).Text); Assert.Equal(b, (await comparison.WindowAsync(false, token: Token)).Text);
        Assert.Equal("B", await comparison.ValueAsync(false, d.After!.Range, Token)); Assert.True(comparison.IsCurrent);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task PastedVersusDocumentOrOriginalDoesNotTouchHistory(bool original)
    {
        using var manager = new DocumentManager(); var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Header + "PID|ORIGINAL\r")); manager.Open(doc);
        doc.IsEditMode = true; doc.ReplaceWorkingText(Header + "PID|DRAFT\r");
        var version = doc.SearchRevision; var before = doc.WorkingText;
        using var comparison = await MessageComparison.CreateAsync(ComparisonSource.FromPastedText(Header + "PID|PASTED\r"), new(doc, 1, original), token: Token);
        var diff = Assert.Single(comparison.Result.Differences); Assert.Equal("PASTED", diff.BeforeValue); Assert.Equal(original ? "ORIGINAL" : "DRAFT", diff.AfterValue);
        Assert.Equal(version, doc.SearchRevision); Assert.Equal(before, doc.WorkingText); Assert.True(doc.CanUndo); Assert.False(doc.CanRedo); Assert.True(doc.IsDirty);
        Assert.Contains(original ? "Original Message" : "Unsaved draft", new ComparisonSource(doc, 1, original).Label);
    }
    [Fact]
    public void SwitchingSourceKindsAndClearingRetainsIndependentInactiveDrafts()
    {
        using var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(Header + "PID|DOC\r"));
        var a = new ComparisonInput(); var b = new ComparisonInput(); a.SetPastedText(Header + "PID|A\r"); b.SetPastedText(Header + "PID|B\r");
        var originalA = a.PastedText; a.SelectDocument(doc); a.Kind = ComparisonSourceKind.OpenMessage;
        Assert.Same(doc, a.Source!.Document); Assert.Equal(originalA, a.PastedText);
        a.Kind = ComparisonSourceKind.OriginalMessage; Assert.True(a.Source!.Original); Assert.Equal(originalA, a.PastedText);
        a.Kind = ComparisonSourceKind.PastedText; Assert.Equal(originalA, a.Source!.PastedText); Assert.Null(a.Source.Document);
        a.SetPastedText(""); Assert.Null(a.Source); Assert.Equal(Header + "PID|B\r", b.PastedText);
        a.SetPastedText("PID|REPLACED"); Assert.Equal("PID|REPLACED", a.Source!.PastedText); Assert.Equal(Header + "PID|B\r", b.PastedText);
        a.Clear(); Assert.Empty(a.PastedText); Assert.Null(a.Document); Assert.Null(a.Source);
    }
    [Theory]
    [InlineData("")] [InlineData(" \r\n\t")]
    public async Task EmptyPastedTextGivesUsefulGuidanceWithoutPartialResults(string text)
    {
        await Assert.ThrowsAsync<ComparisonInputException>(() => MessageComparison.CreateAsync(ComparisonSource.FromPastedText(text), ComparisonSource.FromPastedText(Header), token: Token));
        var input = new ComparisonInput(); input.SetPastedText(text); Assert.Null(input.Source); Assert.Equal(text, input.PastedText);
    }
    [Fact]
    public async Task MultipleMessagesAreExplicitlyRejectedRatherThanSilentlySelectingTheFirst()
    {
        var text = Header + "PID|FIRST\r" + Header + "PID|SECOND\r";
        var error = await Assert.ThrowsAsync<ComparisonInputException>(() => MessageComparison.CreateAsync(ComparisonSource.FromPastedText(text), ComparisonSource.FromPastedText(Header), token: Token));
        Assert.Contains("multiple MSH", error.Message); Assert.DoesNotContain("FIRST", error.Message); Assert.DoesNotContain("SECOND", error.Message);
        var input = new ComparisonInput(); input.SetPastedText(text); Assert.Equal(text, input.PastedText);
    }
    [Theory]
    [InlineData("PID|A", "PID|B")]
    [InlineData("BAD malformed text\n", "BAD different text\n")]
    [InlineData("MSH|^~\\&|SYN\rPID|A\\unclosed^B\r", "MSH|^~\\&|SYN\rPID|C\\unclosed^B\r")]
    public async Task IncompleteAndMalformedInputRemainsLosslessAndDiagnosed(string a, string b)
    {
        using var comparison = await MessageComparison.CreateAsync(ComparisonSource.FromPastedText(a), ComparisonSource.FromPastedText(b), token: Token);
        Assert.NotEmpty(comparison.Result.Differences); Assert.NotEmpty(comparison.BeforeStructure.Diagnostics);
        Assert.Equal(a, (await comparison.WindowAsync(true, token: Token)).Text);
    }
    [Fact]
    public async Task CustomDelimitersAndCombinedIgnoresUseTheExistingEngine()
    {
        const string a = "MSH*$%!@*SYN*TEST*DEST*TEST*20261016000000**ORU$R01*SYN-A*T*2.5\rPID*A$B@C%D\r";
        var b = a.Replace("20261016000000", "20261017000000", StringComparison.Ordinal).Replace("SYN-A", "SYN-B", StringComparison.Ordinal).Replace("PID*A$B@C%D\r", "PID*A$B@C%D**\r\n", StringComparison.Ordinal);
        using var comparison = await MessageComparison.CreateAsync(ComparisonSource.FromPastedText(a), ComparisonSource.FromPastedText(b), new(true, true, true, true), Token);
        Assert.Empty(comparison.Result.Differences); Assert.Equal(a, (await comparison.WindowAsync(true, token: Token)).Text);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void OversizedPasteIsRejectedAtomicallyWithoutTruncation(bool longLine)
    {
        var input = new ComparisonInput(); input.SetPastedText("PID|RETAINED"); var revision = input.Revision;
        var oversized = new string('X', longLine ? MessageDocument.MaximumEditableLineCharacters + 1 : ComparisonInput.MaximumPastedCharacters + 1);
        Assert.Throws<ComparisonInputException>(() => input.SetPastedText(oversized)); Assert.Equal("PID|RETAINED", input.PastedText); Assert.Equal(revision, input.Revision);
        Assert.Throws<ComparisonInputException>(() => ComparisonSource.FromPastedText(oversized));
    }
    [Fact]
    public async Task LargePastedMessagesUseBoundedDetailsAndNeverCreateDocumentsOrSpools()
    {
        var a = Header + string.Concat(Enumerable.Range(0, 1200).Select(i => "OBX|" + new string('A', 600) + "\r"));
        var b = a.Replace("OBX|", "OBX|B", StringComparison.Ordinal);
        using var manager = new DocumentManager();
        var source = ComparisonSource.FromPastedText(a); Assert.DoesNotContain("AAAA", source.ToString());
        var comparison = await MessageComparison.CreateAsync(source, ComparisonSource.FromPastedText(b), token: Token);
        Assert.Equal(1200, comparison.Result.Differences.Count); Assert.Empty(manager.Documents);
        var diff = comparison.Result.Differences[0]; Assert.True(diff.BeforeAbbreviated);
        Assert.Equal(600, (await comparison.PreviewValueAsync(true, diff.Before!.Range, Token)).Length);
        comparison.Dispose(); await comparison.Disposal; Assert.False(comparison.IsCurrent);
    }
    [Fact]
    public async Task PreCanceledPastedCaptureAndDetailPreviewLimitsRemainSafe()
    {
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        var source = ComparisonSource.FromPastedText(Header + "OBX|" + new string('A', 5000));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MessageComparison.CreateAsync(source, source, token: cancel.Token));
        using var comparison = await MessageComparison.CreateAsync(source, ComparisonSource.FromPastedText(Header + "OBX|B"), token: Token);
        var diff = Assert.Single(comparison.Result.Differences); var preview = await comparison.PreviewValueAsync(true, diff.Before!.Range, Token);
        Assert.StartsWith(new string('A', 4096), preview); Assert.EndsWith("[preview]", preview);
        Assert.Equal(5000, (await comparison.ValueAsync(true, diff.Before.Range, Token)).Length);
    }
}
