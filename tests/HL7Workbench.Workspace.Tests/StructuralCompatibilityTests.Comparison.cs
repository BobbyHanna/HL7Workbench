using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class StructuralCompatibilityTests
{
    [Fact]
    public async Task OriginalAndEditedSelectionsKeepIdentityAcrossInsertionBeforeSource()
    {
        var doc = await Open(); var id = doc.GetMessageId(2);
        var before = new ComparisonSource(doc, 2, true); var after = new ComparisonSource(doc, 2);
        var input = new ComparisonInput(); input.SelectSource(after);
        doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
        Assert.Equal(id, before.MessageId); Assert.Equal(3, before.Ordinal); Assert.Equal(2, before.OriginalOrdinal);
        Assert.Equal(id, input.Source!.MessageId); Assert.Equal(3, input.Ordinal);
        await doc.NavigateAsync(id, Token); doc.IsEditMode = true; doc.ReplaceWorkingText(B + "NTE|SYN-CHANGED\r");
        using var comparison = await MessageComparison.CreateAsync(before, after, token: Token);
        Assert.Equal(B, await ComparedText(comparison, true)); Assert.Equal(B + "NTE|SYN-CHANGED\r", await ComparedText(comparison, false));
        Assert.NotEmpty(comparison.Result.Differences);
    }

    [Fact]
    public async Task InsertedSourcesCompareWithinAndAcrossDocumentsWithoutOriginalProvenance()
    {
        var first = await Open(); var a = first.InsertMessage(MessageInsertionPosition.Beginning, C); await first.EditRefresh;
        var b = first.InsertMessage(MessageInsertionPosition.End, A); await first.EditRefresh;
        var other = await Open(); var c = other.InsertMessage(MessageInsertionPosition.End, B); await other.EditRefresh;
        using var same = await MessageComparison.CreateAsync(new(first, a), new(first, b), token: Token);
        Assert.NotEmpty(same.Result.Differences);
        using var cross = await MessageComparison.CreateAsync(new(first, a), new(other, c), token: Token);
        Assert.Equal(C, await ComparedText(cross, true)); Assert.Equal(B, await ComparedText(cross, false));
        var error = await Assert.ThrowsAsync<ComparisonInputException>(() => MessageComparison.CreateAsync(new(first, a, true), new(first, b), token: Token));
        Assert.Contains("no Original Message", error.Message);
        var path = Path.Combine(root, "inserted-save.hl7"); await first.SaveAsync(path, Token);
        Assert.False(new ComparisonSource(first, a, true).IsAvailable);
        using var input = File.OpenRead(path);
        var reopened = await MessageFileReader.OpenFileAsync(documents, "Reopened synthetic", input, Token);
        Assert.NotEqual(a, reopened.GetMessageId(1)); Assert.True(new ComparisonSource(reopened, 1, true).IsAvailable);
    }

    [Fact]
    public async Task UndoUnavailableSelectionNeverClampsAndRedoRestoresSameSourceWithFreshResults()
    {
        var doc = await Open(); var id = doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
        var input = new ComparisonInput(); input.SelectDocument(doc, 1); input.Kind = ComparisonSourceKind.OpenMessage;
        var source = input.Source!;
        using var old = await MessageComparison.CreateAsync(source, source, token: Token);
        doc.Undo(); await doc.EditRefresh;
        Assert.Equal(id, input.MessageId); Assert.Equal(id, input.Source!.MessageId); Assert.Equal(0, input.Ordinal);
        Assert.False(source.IsAvailable); Assert.Contains("unavailable", input.Label); Assert.False(old.IsCurrent);
        await Assert.ThrowsAsync<ComparisonInputException>(() => MessageComparison.CreateAsync(input.Source!, new(doc, 1), token: Token));
        doc.Redo(); await doc.EditRefresh;
        Assert.Equal(id, input.MessageId); Assert.Equal(1, input.Ordinal); Assert.True(source.IsAvailable); Assert.False(old.IsCurrent);
        using var current = await MessageComparison.CreateAsync(input.Source!, input.Source!, token: Token);
        Assert.True(current.IsCurrent); Assert.Equal(C, await ComparedText(current, true));
    }

    [Fact]
    public async Task PastedOnlyComparisonSurvivesDocumentInsertionAndCanceledCaptureReleasesReaders()
    {
        var doc = await Open(); var source = new ComparisonSource(doc, 1);
        using var pasted = await MessageComparison.CreateAsync(ComparisonSource.FromPastedText(A), ComparisonSource.FromPastedText(B), token: Token);
        doc.InsertMessage(MessageInsertionPosition.End, C); await doc.EditRefresh;
        Assert.True(pasted.IsCurrent);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MessageComparison.CreateAsync(source, source, token: canceled.Token));
        var directory = doc.Snapshot!.DirectoryPath; documents.Close(doc.Id, discardChanges: true); await doc.Disposal;
        Assert.False(Directory.Exists(directory)); Assert.True(pasted.IsCurrent);
    }
}
