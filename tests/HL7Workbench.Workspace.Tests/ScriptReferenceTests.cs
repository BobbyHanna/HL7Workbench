using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Scripting;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class ScriptReferenceTests : IAsyncDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-script-reference-" + Guid.NewGuid().ToString("N"));
    private readonly DocumentManager documents = new();
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private ScriptLibrary Store => new(Path.Combine(root, "Scripts"));
    private static Task<UnsavedChangesDecision> Cancel() => Task.FromResult(UnsavedChangesDecision.Cancel);
    private static Task<UnsavedChangesDecision> Discard() => Task.FromResult(UnsavedChangesDecision.Discard);
    [Fact]
    public void EmbeddedCatalogSearchAndCategoriesWorkWithoutLocalFilesOrConfiguration()
    {
        var reference = ScriptApiReference.Current;
        Assert.Equal(1, reference.Version); Assert.Equal(2, reference.ApiVersion);
        Assert.Equal(reference.Entries.Count, reference.Search().Count);
        foreach (var category in reference.Categories)
        { Assert.NotEmpty(reference.Search(category: category)); Assert.All(reference.Search(category: category), e => Assert.Equal(category, e.Category)); }
        Assert.Contains(reference.Search("scriptmessage READ"), e => e.Id == "read");
        Assert.Contains(reference.Search("settext"), e => e.Id == "set-text");
        Assert.Contains(reference.Search("subcomponent"), e => e.Id == "paths");
        Assert.Empty(reference.Search("[not a regex]*")); Assert.Empty(reference.Search(category: "unknown"));
        Assert.DoesNotContain(reference.Search("SetText", "Reports and diagnostics"), e => e.Id == "set-text");
        Assert.Equal(reference.Search(new string('x', 256)), reference.Search(new string('x', 10_000)));
        Assert.Equal(6, reference.Entries.Count(e => e.TemplateName is not null));
        Assert.Equal(6, reference.Search("starter").Count(e => e.TemplateName is not null));
        Assert.False(Directory.Exists(root));
    }
    [Fact]
    public async Task StarterDraftUsesDirtyConsentAndPreservesExistingRecordsAndNewerEdits()
    {
        var session = new ScriptEditorSession(Store);
        Assert.False(await session.CreateFromReferenceAsync("security", Cancel));
        Assert.True(await session.CreateFromReferenceAsync("read", Cancel));
        Assert.False(session.IsBundled); Assert.True(session.IsDirty); Assert.Equal("Read and modify PID", session.Name);
        Assert.True(session.Rename("My starter")); Assert.True(session.Save()); var savedId = session.Id; var saved = Store.Open(savedId);
        session.Source += "\n// unsaved change"; var draft = session.Source;
        Assert.False(await session.CreateFromReferenceAsync("report", Cancel)); Assert.Equal(draft, session.Source);
        var consent = new TaskCompletionSource<UnsavedChangesDecision>();
        var changing = session.CreateFromReferenceAsync("report", () => consent.Task);
        session.Source += "\n// newer"; consent.SetResult(UnsavedChangesDecision.Discard);
        Assert.False(await changing); Assert.EndsWith("// newer", session.Source);
        Assert.True(await session.CreateFromReferenceAsync("report", Discard));
        Assert.NotEqual(savedId, session.Id); Assert.True(session.IsDirty); Assert.Equal(saved, Store.Open(savedId));
    }
    [Fact]
    public async Task PatientBuiltinCopyCanRenameEditSaveReopenAndRunWithoutChangingAnyBuiltin()
    {
        var originals = Builtins();
        var session = new ScriptEditorSession(Store);
        Assert.True(await session.OpenAsync(ScriptLibrary.PatientDeidentifierId, Cancel));
        var builtin = session.Source; session.Source = "not allowed"; Assert.Equal(builtin, session.Source);
        Assert.False(session.Rename("not allowed")); Assert.False(session.Save());
        Assert.True(session.Duplicate()); var id = session.Id;
        Assert.True(session.Rename("My customized Patient"));
        session.Source = session.Source.Replace("seed = 42", "seed = 43", StringComparison.Ordinal);
        Assert.NotEqual(builtin, session.Source); Assert.True(session.Save()); var customized = session.Source;
        var reopened = new ScriptEditorSession(new ScriptLibrary(Path.Combine(root, "Scripts")));
        Assert.True(await reopened.OpenAsync(id, Cancel)); Assert.Equal(customized, reopened.Source); Assert.False(reopened.IsBundled);
        Assert.Equal("My customized Patient", reopened.Name);
        Assert.Equal(originals, Builtins());
        foreach (var item in Store.List().Where(i => i.Bundled))
        {
            var entry = Store.Open(item.Id); Assert.Throws<InvalidOperationException>(() => Store.Save(entry with { Bundled = false }));
            Assert.Throws<InvalidOperationException>(() => Store.Delete(entry with { Bundled = false }));
        }
        var source = await Open(); var result = await Execute(source, reopened.Source, ScriptScope.WholeDocument);
        using var reports = result.Reports; Assert.NotNull(result.Document.TransformationReport); Assert.Equal(2, result.Processed);
        Assert.Equal(customized, Store.Open(id).Source); Assert.False(source.IsDirty);
        Assert.Equal(builtin, Store.Open(ScriptLibrary.PatientDeidentifierId).Source);
    }
    public static IEnumerable<object[]> Starters() => ScriptApiReference.Current.Entries.Where(e => e.TemplateName is not null)
        .SelectMany(e => new[] { new object[] { e.Id, ScriptScope.CurrentMessage }, new object[] { e.Id, ScriptScope.WholeDocument } });
    [Theory]
    [MemberData(nameof(Starters))]
    public async Task SavedStarterRunsInBothScopesWithIndependentOutputAndUntouchedSource(string id, ScriptScope scope)
    {
        var session = new ScriptEditorSession(Store); Assert.True(await session.CreateFromReferenceAsync(id, Cancel));
        Assert.True(session.Save()); var saved = Store.Open(session.Id);
        var source = await Open(); await source.NavigateAsync(2, Token);
        var before = source.Snapshot!.ReadBytes(new(0, source.Snapshot.Length));
        var result = await Execute(source, saved.Source, scope); using var reports = result.Reports;
        Assert.NotEqual(source.Id, result.Document.Id); Assert.True(result.Document.IsDirty); Assert.Null(result.Document.SavePath);
        Assert.Equal(scope == ScriptScope.CurrentMessage ? 1 : 2, result.Processed); Assert.Equal(result.Processed, result.Document.MessageCount);
        Assert.Equal(before, source.Snapshot.ReadBytes(new(0, source.Snapshot.Length))); Assert.False(source.IsDirty);
        if (id is "lifecycle" or "report")
        {
            Assert.Equal(1, Assert.Single(reports!.Items).Rows);
            var path = Path.Combine(root, "counts.csv"); await reports.ExportAsync(id == "report" ? "summary" : "counts", path, Token);
            Assert.Contains($"\"{result.Processed}\"", await File.ReadAllTextAsync(path, Token));
        }
        if (id == "set-text") Assert.Equal("Synthetic_Output.hl7", result.SuggestedFileName);
        if (id == "segments") Assert.Equal("SYNTHETIC SECOND", result.Document.Message.Get("PID-5[2].1").Raw);
        if (id == "read") Assert.Equal("SYNTHETIC", result.Document.Message.Get("PID-5.1").Raw);
        if (id == "identity-library")
        {
            var again = await Execute(source, saved.Source, scope); using var repeatedReports = again.Reports;
            Assert.Equal(result.Document.Snapshot!.ReadBytes(new(0, result.Document.Snapshot.Length)), again.Document.Snapshot!.ReadBytes(new(0, again.Document.Snapshot.Length)));
        }
    }
    private async Task<MessageDocument> Open()
    {
        Directory.CreateDirectory(root);
        var text = SyntheticMessageGenerator.Generate(new() { Count = 1 }, Token).Messages[0];
        var parsed = Hl7Parser.Parse(text); parsed.SetRaw("PID-5", "Example^Ada~Second^Bea"); text = parsed.RawText;
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text + text));
        return await MessageFileReader.OpenFileAsync(documents, "Synthetic.hl7", input, Token);
    }
    private Task<ScriptExecutionResult> Execute(MessageDocument source, string script, ScriptScope scope) =>
        new ScriptExecution(documents, new() { StorageRoot = Path.Combine(root, "snapshots") }).RunAsync(source, new(script), scope, token: Token);
    private LibraryScript[] Builtins() => Store.List().Where(i => i.Bundled).OrderBy(i => i.Id).Select(i => Store.Open(i.Id)).ToArray();
    public async ValueTask DisposeAsync()
    {
        var pending = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(pending.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
