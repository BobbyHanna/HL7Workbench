using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class StructuralCompatibilityTests
{
    [Fact]
    public async Task ShiftedOriginalKeepsInspectorSourceBytesAndDiagnosticIdentityWithoutDuplicateWarnings()
    {
        const string diagnosed = "MSH|^~\\&|SYN\rPID|A\r";
        var doc = await Open(diagnosed + B); var original = doc.GetMessageId(1);
        var originalRange = doc.Index!.Messages[0].SourceRange;
        var before = documents.Activity.Snapshot().Count(e => e.MessageId == original);
        Assert.True(before > 0);
        var inserted = doc.InsertMessage(MessageInsertionPosition.Beginning, C); await doc.EditRefresh;
        Assert.Throws<InvalidOperationException>(() => new RawInspectorSession(doc));
        await doc.NavigateAsync(original, Token);
        Assert.Equal(2, doc.MessageNumber); Assert.Equal(1, doc.ActiveEntry!.Value.SourceOrdinal);
        Assert.Equal(originalRange, doc.ActiveOriginalMessage!.SourceRange);
        Assert.Equal("A", doc.Inspection.ResolveOffset(diagnosed.IndexOf("PID|", StringComparison.Ordinal) + 4)!.Raw);
        var activeDiagnostics = DocumentActivity.Diagnostics(doc, false).ToArray();
        Assert.NotEmpty(activeDiagnostics); Assert.All(activeDiagnostics, d =>
        { Assert.Equal(original, d.MessageId); Assert.Equal(2, d.Message); Assert.Equal(1, d.OriginalOrdinal); });
        Assert.Equal(before, documents.Activity.Snapshot().Count(e => e.MessageId == original));
        using var session = new RawInspectorSession(doc);
        Assert.Equal(original, session.MessageId); Assert.Equal(1, session.OriginalOrdinal); Assert.Equal(2, session.MessageNumber);
        var path = Path.Combine(root, "original.hl7"); await session.ExportAsync(path, Token);
        Assert.Equal(diagnosed, await File.ReadAllTextAsync(path, Token));
        await doc.NavigateAsync(inserted, Token); Assert.True(session.IsClosed);
        Assert.Equal("C", doc.Message.Get("PID-1").Raw); Assert.Null(doc.ActiveOriginalMessage);
    }

    [Fact]
    public async Task BoundaryDiagnosticsKeepOriginalOrdinalAndIdAndUnsupportedContainerRemainsProtected()
    {
        var doc = await Open("\v" + A + "\x1c\r\v" + B); var id = doc.GetMessageId(2);
        var boundary = Assert.Single(DocumentActivity.Diagnostics(doc), d => d.Diagnostic.Code == "IncompleteMllp");
        Assert.Equal(id, boundary.MessageId); Assert.Equal(2, boundary.OriginalOrdinal); Assert.Equal(2, boundary.Message);
        Assert.Throws<InvalidOperationException>(() => doc.InsertMessage(MessageInsertionPosition.Beginning, C));
        Assert.Equal(id, doc.GetMessageId(2)); Assert.Equal(2, doc.MessageCount);
        await doc.NavigateAsync(id, Token); Assert.Contains(doc.ActiveDiagnostics, d => d.Code == "IncompleteMllp");
        Assert.Contains(documents.Activity.Snapshot(), e => e.Diagnostic == ActivityDiagnostic.IncompleteMllp &&
            e.MessageId == id && e.OriginalOrdinal == 2);
    }
}
