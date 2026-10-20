using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Scripting;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class DeidentificationExecutionTests : IAsyncDisposable
{
    private readonly DocumentManager documents = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-deidentification-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private static string Input => SyntheticMessageGenerator.Generate(new() { Count = 1 }, Token).Messages[0];
    private sealed class Progress(Action<ScriptProgress> action) : IProgress<ScriptProgress> { public void Report(ScriptProgress p) => action(p); }
    private ScriptExecutionOptions Options => new() { StorageRoot = Path.Combine(root, "jobs") };
    private async Task<MessageDocument> Open(string? text = null)
    {
        Directory.CreateDirectory(root); using var input = new MemoryStream(Encoding.UTF8.GetBytes(text ?? Input + Input));
        return await MessageFileReader.OpenFileAsync(documents, "SYN-PRIVATE-PATIENT-INPUT.hl7", input, Token);
    }
    private Task<ScriptExecutionResult> Run(MessageDocument doc, LibraryScript script, ScriptScope scope = ScriptScope.WholeDocument,
        IProgress<ScriptProgress>? progress = null, ScriptExecutionOptions? options = null, CancellationToken? token = null) =>
        new ScriptExecution(documents, options ?? Options).RunAsync(doc, new(script.Source), scope, progress, token ?? Token, script.Name);
    public async ValueTask DisposeAsync()
    {
        var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
    private static LibraryScript Preset(int preset) => preset switch { 0 => ScriptLibrary.PatientDeidentifier, 1 => ScriptLibrary.IdentifierPseudonymizer, _ => ScriptLibrary.TimestampShifter };
    [Theory]
    [InlineData(0, ScriptScope.CurrentMessage)] [InlineData(0, ScriptScope.WholeDocument)]
    [InlineData(1, ScriptScope.CurrentMessage)] [InlineData(1, ScriptScope.WholeDocument)]
    [InlineData(2, ScriptScope.CurrentMessage)] [InlineData(2, ScriptScope.WholeDocument)]
    public async Task EveryBundledPresetRunsInTheRealWorkerPublishesIndependentOutputAndOneSafeReport(int preset, ScriptScope scope)
    {
        var doc = await Open(); var original = doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length)); var ids = doc.Messages.ToArray();
        var result = await Run(doc, Preset(preset), scope); using var reports = result.Reports!;
        Assert.Equal(scope == ScriptScope.CurrentMessage ? 1 : 2, result.Document.MessageCount); Assert.True(result.Document.IsDirty); Assert.Null(result.Document.SavePath);
        Assert.Contains("Review Required", result.Document.Title); Assert.DoesNotContain("PRIVATE", result.Document.Title);
        if (preset != 2) Assert.NotEqual(doc.Message.Get("PID-3").Raw, result.Document.Message.Get("PID-3").Raw);
        if (preset != 1) Assert.NotEqual(doc.Message.Get("MSH-7").Raw, result.Document.Message.Get("MSH-7").Raw);
        Assert.Equal(original, doc.Snapshot.ReadBytes(new(0, doc.Snapshot.Length))); Assert.Equal(ids, doc.Messages); Assert.False(doc.IsDirty);
        Assert.Single(reports.Items); Assert.Equal(1, reports.Items[0].Rows); Assert.True(reports.Items[0].Bytes < 4096);
        Assert.NotNull(result.Document.TransformationReport); Assert.Equal(Preset(preset).Name, result.Document.TransformationReport.PresetName);
        Assert.Equal(scope, result.Document.TransformationReport.Scope); Assert.Equal(result.Input!.Revision, result.Document.TransformationReport.CapturedRevision);
        var path = Path.Combine(root, "review.csv"); await reports.ExportAsync("deidentification-review", path, Token);
        var csv = await File.ReadAllTextAsync(path, Token); Assert.Contains("Review required", csv); Assert.Contains("container bytes", csv);
        Assert.DoesNotContain("SYN-0000002A", csv); Assert.DoesNotContain("EXAMPLE", csv); Assert.DoesNotContain("2030", csv);
        var save = Path.Combine(root, "transformed.hl7"); await result.Document.SaveAsync(save, Token); Assert.False(result.Document.IsDirty);
        using var saved = File.OpenRead(save); var reopened = await MessageFileReader.OpenFileAsync(documents, "Reopened", saved, Token);
        Assert.Equal(result.Document.MessageCount, reopened.MessageCount); Assert.Equal(result.Document.EditableText, reopened.EditableText);
    }
    [Fact]
    public async Task PatientVisitAccountAndOrderReferencesStayConsistentAcrossMessagesAndExecutions()
    {
        var parsed = Hl7Parser.Parse(Input); parsed.SetText("PID-18.1", "SYN-ACCOUNT"); parsed.SetText("PID-18.4", "SYN-AUTH"); parsed.SetText("PID-18.5", "AN");
        var text = parsed.RawText + "ORC|NW|SYN-ORDER^SYN-AUTH|SYN-FILL^SYN-LAB\rOBR|1|SYN-ORDER^SYN-AUTH|SYN-FILL^SYN-LAB\r";
        var doc = await Open(text + text); var first = await Run(doc, ScriptLibrary.PatientDeidentifier); using var report1 = first.Reports;
        var one = first.Document.Message;
        await first.Document.NavigateAsync(2, Token); var two = first.Document.Message;
        foreach (var path in new[] { "PID-3", "PID-5", "PID-18", "PV1-19", "ORC-2", "OBR-2", "ORC-3", "OBR-3" }) Assert.Equal(one.Get(path).Raw, two.Get(path).Raw);
        Assert.Equal(one.Get("ORC-2").Raw, one.Get("OBR-2").Raw);
        var repeated = await Run(doc, ScriptLibrary.PatientDeidentifier); using var report2 = repeated.Reports;
        Assert.NotSame(first.Document.TransformationReport, repeated.Document.TransformationReport);
        report1?.Dispose(); Assert.Equal(2, first.Document.TransformationReport!.Summary.Messages);
        Assert.Equal(first.Document.Snapshot!.ReadBytes(new(0, first.Document.Snapshot.Length)), repeated.Document.Snapshot!.ReadBytes(new(0, repeated.Document.Snapshot.Length)));
        var changed = await Run(doc, ScriptLibrary.PatientDeidentifier with { Source = ScriptLibrary.PatientDeidentifier.Source.Replace("seed = 42", "seed = 43", StringComparison.Ordinal) }); using var report3 = changed.Reports;
        Assert.NotEqual(one.Get("PID-3").Raw, changed.Document.Message.Get("PID-3").Raw);
    }
    [Fact]
    public async Task Stage8CInsertionsAndStage8DGeneratedEntriesRemainIndependentAndKeepCapturedAttribution()
    {
        var doc = await Open(Input); var original = doc.GetMessageId(1);
        var inserted = doc.InsertMessage(MessageInsertionPosition.Beginning, Input); await doc.EditRefresh;
        var generated = await new SyntheticGeneration(documents).GenerateAsync(new() { Count = 2 }, doc, MessageInsertionPosition.End, token: Token); await doc.EditRefresh;
        var ids = doc.Messages.Select(m => m.Id).ToArray(); var revision = doc.SearchRevision; var changed = false;
        var result = await Run(doc, ScriptLibrary.PatientDeidentifier, progress: new Progress(p =>
        { if (changed || p.Phase != "Compiling") return; changed = true; doc.ReplaceWorkingText(doc.EditableText + "NTE|1||SYN-LATER\r"); })); using var reports = result.Reports;
        Assert.True(result.SourceChangedDuringRun); Assert.Equal(revision, result.Input!.Revision); Assert.Equal(ids, result.MessageSources.Select(m => m.InputMessageId));
        Assert.True(result.Document.TransformationReport!.SourceChangedDuringRun); Assert.False(result.Document.TransformationReport.SourceIsCurrent);
        Assert.Equal(new int?[] { null, 1, null, null }, result.Input.Messages.Select(m => m.OriginalOrdinal)); Assert.Equal(4, result.Document.MessageCount);
        Assert.Equal(ids, doc.Messages.Select(m => m.Id)); Assert.Equal(inserted, doc.GetMessageId(1)); Assert.Equal(original, doc.GetMessageId(2));
        Assert.Equal(generated.MessageIds, doc.Messages.Skip(2).Select(m => m.Id)); Assert.True(doc.IsDirty);
        Assert.DoesNotContain("SYN-LATER", result.Document.EditableText!); Assert.Contains("SYN-LATER", doc.EditableText!); await doc.EditRefresh;
    }
    [Theory]
    [InlineData("cancel")] [InlineData("output-limit")] [InlineData("mapping-limit")] [InlineData("timeout")]
    public async Task CancellationLimitsAndTimeoutPublishNeitherPartialOutputNorReports(string failure)
    {
        var doc = await Open(); var before = doc.Snapshot!.ReadBytes(new(0, doc.Snapshot.Length)); var ids = doc.Messages.ToArray();
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var script = ScriptLibrary.PatientDeidentifier; var options = Options;
        if (failure == "output-limit") options = options with { MaximumOutputBytes = 1 };
        if (failure == "mapping-limit") script = script with { Source = script.Source.Replace("Seed = seed", "Seed = seed, MaximumMappings = 1", StringComparison.Ordinal) };
        if (failure == "timeout")
        {
            script = script with { Source = script.Source.Replace("workflow.Apply(Message);", "workflow.Apply(Message); System.Threading.Thread.Sleep(3000);", StringComparison.Ordinal) };
            options = options with { Worker = new() { MessageTimeout = TimeSpan.FromMilliseconds(700) } };
        }
        var task = Run(doc, script, progress: new Progress(p => { if (failure == "cancel" && p.Phase == "Processing") cancellation.Cancel(); }), options: options, token: cancellation.Token);
        if (failure == "cancel") await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        else
        {
            var error = await Assert.ThrowsAsync<ScriptRunException>(() => task);
            Assert.DoesNotContain("SYN-", string.Join(" ", error.Diagnostics.Select(d => d.Description)));
            if (failure == "mapping-limit") Assert.Contains(error.Diagnostics, d => d.Code == "TransformationLimit");
        }
        Assert.Single(documents.Documents); Assert.False(doc.IsDirty); Assert.Equal(ids, doc.Messages); Assert.Equal(before, doc.Snapshot.ReadBytes(new(0, doc.Snapshot.Length)));
        Assert.True(!Directory.Exists(Options.StorageRoot) || !Directory.EnumerateDirectories(Options.StorageRoot).Any());
    }
    [Fact]
    public async Task NonMessageContainerBytesAreExplicitlyUnassessedInTheReviewReport()
    {
        var doc = await Open("FHS|SYN-PRIVATE-CONTAINER\r" + Input + "FTS|1\r");
        var result = await Run(doc, ScriptLibrary.PatientDeidentifier); using var reports = result.Reports!;
        Assert.Contains("SYN-PRIVATE-CONTAINER", Encoding.UTF8.GetString(result.Document.Snapshot!.ReadBytes(new(0, result.Document.Snapshot.Length))));
        var path = Path.Combine(root, "unassessed.csv"); await reports.ExportAsync("deidentification-review", path, Token);
        Assert.Contains("container bytes", await File.ReadAllTextAsync(path, Token)); Assert.Contains("Review Required", result.Document.Title);
    }
    [Fact]
    public void BundledPresetsAreDiscoverableProtectedAndNeverOverwriteEditedCopies()
    {
        var library = new ScriptLibrary(Path.Combine(root, "library")); var all = library.List(); Assert.Equal(5, all.Count);
        foreach (var preset in new[] { ScriptLibrary.PatientDeidentifier, ScriptLibrary.IdentifierPseudonymizer, ScriptLibrary.TimestampShifter })
        {
            Assert.Contains(all, x => x.Id == preset.Id && x.Bundled); Assert.Equal(preset, library.Open(preset.Id));
            Assert.Throws<InvalidOperationException>(() => library.Save(preset with { Bundled = false }));
            var copy = library.Duplicate(preset); var changed = library.Save(copy with { Source = copy.Source.Replace("seed = 42", "seed = 123", StringComparison.Ordinal) });
            Assert.Equal(changed, new ScriptLibrary(Path.Combine(root, "library")).Open(copy.Id)); Assert.Equal(preset, library.Open(preset.Id));
        }
    }
    [Fact]
    public async Task DocumentReportsRetainTheirOwnRunAndTrackSourceEditsAndClosure()
    {
        var source = await Open(); var first = await Run(source, ScriptLibrary.PatientDeidentifier); first.Reports?.Dispose();
        var old = Assert.IsType<TransformationReport>(first.Document.TransformationReport); var summary = old.Summary;
        Assert.True(old.SourceIsCurrent); var revision = old.CapturedRevision;
        documents.Activate(source.Id); var second = await Run(source, ScriptLibrary.TimestampShifter, ScriptScope.CurrentMessage); second.Reports?.Dispose();
        Assert.NotSame(old, second.Document.TransformationReport); Assert.Equal(ScriptScope.CurrentMessage, second.Document.TransformationReport!.Scope);
        documents.Activate(first.Document.Id); Assert.Same(old, first.Document.TransformationReport); Assert.Same(summary, old.Summary);
        source.IsEditMode = true; source.ReplaceWorkingText(source.EditableText + "NTE|1||SYN-LATER\r"); await source.EditRefresh;
        Assert.False(old.SourceIsCurrent); Assert.Contains("Stale source", old.SourceState); Assert.Equal(revision, old.CapturedRevision);
        Assert.False(old.SourceChangedDuringRun); Assert.False(second.Document.TransformationReport.SourceIsCurrent);
        Assert.True(documents.Close(source.Id, discardChanges: true)); await source.Disposal;
        Assert.Contains("Source closed", old.SourceState); Assert.Equal(2, old.Summary.Messages);
    }
    [Theory]
    [InlineData("PHI-SHAPED-NOT-A-COUNT")] [InlineData("-1")] [InlineData("9999999999999999999999999")] [InlineData("0")]
    public async Task ArbitraryOrInvalidCsvCannotBecomeTheDocumentPrivacyPanel(string value)
    {
        var source = await Open(); var script = ScriptLibrary.PatientDeidentifier with
        { Source = ScriptLibrary.PatientDeidentifier.Source.Replace("review.Row(workflow.Summary.Cells);", "var cells = workflow.Summary.Cells; cells[2] = \"" + value + "\"; review.Row(cells);", StringComparison.Ordinal) };
        var result = await Run(source, script); using var reports = result.Reports;
        Assert.Null(result.Document.TransformationReport); Assert.NotNull(reports); Assert.Single(reports.Items);
        Assert.False(source.IsDirty); // Original user-script CSV remains available through existing explicit export.
    }
    [Fact]
    public async Task ClosingOutputDetachesItsSummaryFromTheSource()
    {
        var source = await Open(); var result = await Run(source, ScriptLibrary.PatientDeidentifier); result.Reports?.Dispose();
        var report = result.Document.TransformationReport!; var notices = 0; report.PropertyChanged += (_, _) => notices++;
        Assert.True(documents.Close(result.Document.Id, discardChanges: true)); await result.Document.Disposal;
        source.IsEditMode = true; source.ReplaceWorkingText(source.EditableText + "NTE|1||SYN-LATER\r"); await source.EditRefresh;
        Assert.Equal(0, notices); Assert.Single(documents.Documents);
    }
}
