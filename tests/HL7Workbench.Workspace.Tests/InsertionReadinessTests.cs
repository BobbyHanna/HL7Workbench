using System.Text;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class InsertionReadinessTests
{
    private const string A = "MSH|^~\\&|SYN\rPID|SYN\r";
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    [Theory]
    [InlineData("", true)] [InlineData("\r", true)] [InlineData("\n\r\n", true)] [InlineData(" \t\u2003\r", true)]
    [InlineData("MSH", false)] [InlineData("NTE|", false)] [InlineData(A, false)]
    public async Task LogicalTrailingGuardUsesRetainedDraftBeforeDebouncedParse(string text, bool blank)
    {
        using var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(A));
        var inserted = doc.InsertMessage(MessageInsertionPosition.End); doc.IsEditMode = true;
        doc.ReplaceWorkingText(text); doc.IsEditMode = false;
        Assert.Equal(blank, doc.IsEmptyTrailingInsertion); Assert.Null(doc.InsertionUnavailableReason);
        Assert.Equal(inserted, doc.ActiveMessageId); Assert.Equal(text, doc.WorkingText);
        await doc.NavigateAsync(1, Token); Assert.False(doc.IsEmptyTrailingInsertion);
        await doc.NavigateAsync(inserted, Token); Assert.Equal(blank, doc.IsEmptyTrailingInsertion);
        await doc.EditRefresh;
    }
    [Theory]
    [InlineData("")] [InlineData("\uFEFF")]
    public async Task EmptyAndBomOnlySourcesRemainValidZeroMessageStates(string source)
    {
        using var manager = new DocumentManager(); using var input = new MemoryStream(Encoding.UTF8.GetBytes(source));
        var doc = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, Token);
        try
        {
            Assert.Equal(0, doc.MessageNumber); Assert.Null(doc.InsertionUnavailableReason); Assert.False(doc.IsEmptyTrailingInsertion);
            var id = doc.InsertMessage(MessageInsertionPosition.Beginning); await doc.EditRefresh;
            Assert.True(doc.IsEmptyTrailingInsertion); Assert.True(doc.IsDirty);
            doc.Undo(); await doc.EditRefresh; Assert.Empty(doc.Messages); Assert.False(doc.IsDirty); Assert.False(doc.IsEmptyTrailingInsertion);
            doc.Redo(); await doc.EditRefresh; Assert.Equal(id, doc.ActiveMessageId); Assert.True(doc.IsEmptyTrailingInsertion);
        }
        finally { manager.Dispose(); await doc.Disposal; }
    }
    [Fact]
    public async Task ManagerNotifiesInsertionCapabilityWhileSavingAndFailedBlankSaveRestoresReadiness()
    {
        using var manager = new DocumentManager(); var doc = new MessageDocument("synthetic", "Synthetic", Hl7Parser.Parse(A)); manager.Open(doc);
        doc.InsertMessage(MessageInsertionPosition.End); await doc.EditRefresh;
        var capabilities = new List<bool>();
        manager.PropertyChanged += (_, e) => { if (e.PropertyName == "ActiveContent") capabilities.Add(doc.InsertionUnavailableReason is null); };
        var root = Path.Combine(Path.GetTempPath(), "hl7-insertion-readiness-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var destination = Path.Combine(root, "synthetic.hl7");
            var error = await Assert.ThrowsAsync<InvalidOperationException>(() => doc.SaveAsync(destination, Token));
            Assert.Contains("message 2", error.Message); Assert.False(File.Exists(destination));
            Assert.Contains(false, capabilities); Assert.True(capabilities[^1]); Assert.True(doc.IsDirty); Assert.True(doc.IsEmptyTrailingInsertion);
        }
        finally { manager.Dispose(); await doc.Disposal; Directory.Delete(root, true); }
    }
}
