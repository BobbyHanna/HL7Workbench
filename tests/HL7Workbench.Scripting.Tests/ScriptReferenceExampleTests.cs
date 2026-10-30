using System.Text;
using System.Text.Json;
using HL7Workbench.Core;
using Xunit;

namespace HL7Workbench.Scripting.Tests;

public sealed class ScriptReferenceExampleTests
{
    [Theory]
    [InlineData("read")] [InlineData("exclude")] [InlineData("report")] [InlineData("exception")] [InlineData("naming")]
    public async Task CopiedExamplesHaveVerifiedTransformFilterCountErrorAndFilenameOutcomes(string topic)
    {
        var token = TestContext.Current.CancellationToken;
        using var stream = typeof(ScriptReferenceExampleTests).Assembly.GetManifestResourceStream("HL7Workbench.ScriptApiReference.json")!;
        using var catalog = JsonDocument.Parse(stream);
        var script = catalog.RootElement.GetProperty("Entries").EnumerateArray().Single(e => e.GetProperty("Id").GetString() == topic).GetProperty("Example").GetString()!;
        var input = Encoding.UTF8.GetBytes(SyntheticMessageGenerator.Generate(new(), token).Messages[0]); var original = input.ToArray();
        await using var worker = await ScriptWorkerSession.StartAsync(Path.Combine(AppContext.BaseDirectory, "ScriptHost", "HL7Workbench.ScriptHost.dll"), new(script), token: token);
        var reports = new List<ScriptReportChunk>(worker.DrainReports());
        for (var number = 1; number <= 2; number++)
        {
            var result = await worker.ProcessAsync(input, 65001, number, token); reports.AddRange(worker.DrainReports());
            if (topic == "exclude") Assert.Equal(number == 2, result.Excluded);
            else if (topic is "read" or "naming") Assert.Equal("SYNTHETIC", Hl7Parser.Parse(result.Bytes!).Get("PID-5.1").Raw);
            else Assert.Equal(original, result.Bytes);
            Assert.Equal(original, input);
        }
        await worker.CompleteAsync(token); reports.AddRange(worker.DrainReports());
        if (topic == "report") Assert.Equal("\"2\"\r\n", Encoding.UTF8.GetString(Assert.Single(reports, r => !r.Header).Bytes));
        if (topic == "exception")
        {
            var rows = reports.Where(r => !r.Header).ToArray(); Assert.Equal(2, rows.Length);
            Assert.All(rows, row => Assert.Equal("\"MissingValue\"\r\n", Encoding.UTF8.GetString(row.Bytes)));
        }
        if (topic == "naming") Assert.Equal("Synthetic_Output.hl7", worker.OutputFileName);
    }
}
