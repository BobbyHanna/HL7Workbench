using HL7Workbench.Scripting;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed partial class ScriptExecutionTests
{
    private static Task<ScriptExecutionResult> NamedRun(Session s, MessageDocument source, string? name,
        string script = "void Main() {}", IProgress<ScriptProgress>? progress = null) =>
        NamedRunWithCancellation(s, source, name, script, progress, TestContext.Current.CancellationToken);
    private static Task<ScriptExecutionResult> NamedRunWithCancellation(Session s, MessageDocument source, string? name,
        string script, IProgress<ScriptProgress>? progress, CancellationToken token) =>
        new ScriptExecution(s.Manager, s.Options).RunAsync(source, new(script), ScriptScope.WholeDocument, progress,
            token, name);

    [Theory]
    [InlineData("Patient Deidentifier", "Patient Deidentifier.hl7")]
    [InlineData("Normalize Phone Numbers", "Normalize Phone Numbers.hl7")]
    [InlineData("Extract ADT Messages", "Extract ADT Messages.hl7")]
    [InlineData(null, "Script Result.hl7")] [InlineData("CON", "Script Result.hl7")]
    public async Task NamedOutputRemainsIndependentUnsavedAndByteExact(string? name, string expected)
    {
        await using var s = new Session(); var source = await s.Open(); var id = source.Id; var title = source.Title;
        var result = await NamedRun(s, source, name);
        Assert.Equal(expected, result.SuggestedFileName); Assert.Equal(expected, result.Document.Title);
        Assert.Equal(expected + " *", result.Document.DisplayTitle); Assert.True(result.Document.IsDirty);
        Assert.Null(result.Document.SavePath); Assert.NotEqual(id, result.Document.Id); Assert.Equal(All(source), All(result.Document));
        Assert.Equal(id, source.Id); Assert.Equal(title, source.Title); Assert.Null(source.SavePath); Assert.False(source.IsDirty);
    }

    [Theory]
    [InlineData("void Main() { Batch.SetOutputFileName(\"Deidentified_Patients.hl7\"); }", "Deidentified_Patients.hl7", false)]
    [InlineData("void Main() {} void Complete() { Batch.SetOutputFileName(\"Final\"); }", "Final.hl7", false)]
    [InlineData("void Main() { Batch.SetOutputFileName(\"First\"); Batch.SetOutputFileName(\"Last\"); Batch.SetOutputFileName(\"../bad\"); }", "Last.hl7", true)]
    [InlineData("void Main() { Batch.SetOutputFileName(\"C:\\\\bad\"); }", "Captured Script.hl7", true)]
    public async Task OverridesAreAdvisoryAndLastValidWins(string code, string expected, bool warning)
    {
        await using var s = new Session(); var source = await s.Open();
        var result = await NamedRun(s, source, "Captured Script", code);
        Assert.Equal(expected, result.Document.Title); Assert.Equal(expected, result.SuggestedFileName);
        Assert.Equal(warning, result.IgnoredOutputFileNameSuggestion); Assert.Equal(All(source), All(result.Document));
    }

    [Fact]
    public async Task DuplicateSuggestionsDisambiguateOnlyDisplayAndSavingRetainsSourceDestinationAndIds()
    {
        await using var s = new Session(); var source = await s.Open();
        var sourcePath = Path.Combine(s.Root, "Source.hl7"); await source.SaveAsync(sourcePath, TestContext.Current.CancellationToken);
        var sourceIds = source.Messages.Select(m => m.Id).ToArray(); var sourceBytes = All(source);
        var first = (await NamedRun(s, source, "Same Name")).Document;
        var second = (await NamedRun(s, source, "Same Name")).Document;
        Assert.NotEqual(first.Id, second.Id); Assert.Equal("Same Name.hl7", first.Title); Assert.Equal(first.Title, second.Title);
        Assert.Equal("Same Name.hl7 *", first.DisplayTitle); Assert.Equal("Same Name.hl7 (2) *", second.DisplayTitle);
        // Saving the second output under the bare name must not leave an ambiguous unsaved first tab.
        var path = Path.Combine(s.Root, second.Title); await second.SaveAsync(path, TestContext.Current.CancellationToken);
        Assert.Equal("Same Name.hl7", second.DisplayTitle); Assert.False(second.IsDirty);
        Assert.Equal("Same Name.hl7 (2) *", first.DisplayTitle); Assert.Null(first.SavePath); Assert.True(first.IsDirty);
        Assert.Equal(sourcePath, source.SavePath); Assert.Equal("Source.hl7", source.Title);
        Assert.Equal(sourceIds, source.Messages.Select(m => m.Id)); Assert.Equal(sourceBytes, All(source));
        Assert.Equal(sourceBytes, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NamingOverrideRetainsStaleSourceIdentityAndReports()
    {
        await using var s = new Session(); var source = await s.Open(); var revision = source.SearchRevision;
        var ids = source.Messages.Select(m => m.Id).ToArray(); source.IsEditMode = true;
        var result = await NamedRun(s, source, "Captured", "var r = Batch.Report(\"count\", \"Count\"); void Main() { r.Row(\"1\"); } void Complete() { Batch.SetOutputFileName(\"Custom\"); }",
            new Progress(p => { if (p.Phase == "Compiling") source.ReplaceWorkingText(First + "NTE|SYN-LATER\r"); }));
        using var reports = result.Reports;
        Assert.Equal("Custom.hl7", result.Document.Title); Assert.True(result.SourceChangedDuringRun);
        Assert.Equal(revision, result.Input!.Revision); Assert.Equal(source.Id, result.Input.DocumentId);
        Assert.Equal(ids, result.MessageSources.Select(m => m.InputMessageId)); Assert.Single(reports!.Items);
        Assert.Equal(First, result.Document.EditableText); Assert.Equal(First + "NTE|SYN-LATER\r", source.EditableText);
        await source.EditRefresh;
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task FailedOrCanceledNamedRunsNeverPublish(bool cancel)
    {
        await using var s = new Session(); var source = await s.Open(); using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var task = NamedRunWithCancellation(s, source, "Captured", "Batch.SetOutputFileName(\"Never published\"); void Main() { throw new System.Exception(); }",
            new Progress(p => { if (cancel && p.Phase == "Compiling") stop.Cancel(); }), stop.Token);
        if (cancel) await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        else await Assert.ThrowsAsync<ScriptRunException>(() => task);
        Assert.Single(s.Manager.Documents); s.AssertNoStaging();
    }
}
