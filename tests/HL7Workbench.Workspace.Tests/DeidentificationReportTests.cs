using System.Text;
using HL7Workbench.Scripting;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class DeidentificationReportTests : IAsyncDisposable
{
    private readonly DocumentManager documents = new();
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-review-report-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    private const string Input = "MSH|^~\\&|SYN-PRIVATE-APP|SYN-FAC|DEST|TEST|20300102120000||ADT^A01|SYN-CONTROL|T|2.5.1\rPID|1||SYN-PRIVATE-ID^^^SYN-AUTH^MR||SYN-PRIVATE-FAMILY^SYN-PRIVATE-GIVEN\r";
    private async Task<ScriptExecutionResult> Run(string input, ScriptScope scope = ScriptScope.WholeDocument, string? script = null)
    {
        Directory.CreateDirectory(root); using var stream = new MemoryStream(Encoding.UTF8.GetBytes(input));
        var source = await MessageFileReader.OpenFileAsync(documents, "SYN-PRIVATE-FILENAME", stream, Token);
        await source.NavigateAsync(source.MessageCount, Token);
        return await new ScriptExecution(documents, new() { StorageRoot = Path.Combine(root, "jobs") }).RunAsync(source,
            new(script ?? ScriptLibrary.PatientDeidentifier.Source), scope, token: Token);
    }
    [Fact]
    public async Task SuccessfulNoFindingsRunKeepsSummaryCsvColumnsAndNoValuesInEitherExport()
    {
        var result = await Run(Input); using var reports = result.Reports!;
        var report = Assert.IsType<TransformationReport>(result.Document.TransformationReport);
        Assert.Equal("Completed — no flagged findings", report.Summary.Status); Assert.True(report.FindingDetailsAvailable); Assert.Empty(report.Findings);
        Assert.True(report.SourceIsCurrent); Assert.True(report.OutputIsCurrent);
        Assert.Equal(new[] { "Status", "Version", "Messages", "Missing identities", "Ambiguous identities", "Replaced values", "Suppressed values", "Shifted timestamps", "Unsupported timestamps", "Unknown segments", "High risk fields", "Retained values", "Unassessed scope", "Preset" }, DeidentificationSummary.Columns.Take(14));
        foreach (var item in reports.Items)
        {
            var path = Path.Combine(root, item.Name + ".csv"); await reports.ExportAsync(item.Name, path, Token);
            var csv = await File.ReadAllTextAsync(path, Token); Assert.DoesNotContain("SYN-PRIVATE", csv); Assert.DoesNotContain("SYN-AUTH", csv);
            Assert.EndsWith("\r\n", csv);
        }
    }
    [Fact]
    public async Task CurrentMessageFindingsResolveToCapturedLogicalPositionAndSurviveJournalDisposal()
    {
        var result = await Run(Input + Input + "NTE|1||SYN-PRIVATE-NARRATIVE\r", ScriptScope.CurrentMessage);
        using var reports = result.Reports!; var report = result.Document.TransformationReport!;
        var finding = Assert.Single(report.Findings); Assert.Equal("NTE[1]-3", finding.Path);
        Assert.Equal(1, finding.MessageNumber); Assert.Equal(2, report.SourceMessageNumber(finding.MessageNumber));
        var path = Path.Combine(root, "findings.csv"); await reports.ExportAsync("deidentification-findings", path, Token);
        var csv = await File.ReadAllTextAsync(path, Token); Assert.Contains("SensitiveContentSuppressed", csv); Assert.DoesNotContain("SYN-PRIVATE", csv);
        reports.Dispose(); Assert.Single(report.Findings); Assert.True(report.Summary.RequiresReview);
        Assert.DoesNotContain("SYN-PRIVATE", string.Join(" ", documents.Activity.Snapshot().Select(e => e.ToString())));
    }
    [Theory]
    [InlineData("cells[2] = \"SYN-PRIVATE-SEGMENT\";")]
    [InlineData("cells[5] = \"SYN-PRIVATE-REASON\";")]
    [InlineData("cells[0] = \"999999\";")]
    public async Task UntrustedFindingCellsAreNeverProjectedIntoThePrivacyTool(string mutation)
    {
        var script = ScriptLibrary.PatientDeidentifier.Source.Replace("findings.Row(finding.Cells);",
            "{ var cells = finding.Cells; " + mutation + " findings.Row(cells); }", StringComparison.Ordinal);
        var result = await Run(Input + "NTE|1||SYN-PRIVATE-NARRATIVE\r", script: script); using var reports = result.Reports;
        var report = Assert.IsType<TransformationReport>(result.Document.TransformationReport);
        Assert.True(report.Summary.RequiresReview); Assert.False(report.FindingDetailsAvailable); Assert.Empty(report.Findings);
    }
    public async ValueTask DisposeAsync()
    {
        var docs = documents.Documents.ToArray(); documents.Dispose(); await Task.WhenAll(docs.Select(d => d.Disposal));
        if (Directory.Exists(root)) Directory.Delete(root, true);
    }
}
