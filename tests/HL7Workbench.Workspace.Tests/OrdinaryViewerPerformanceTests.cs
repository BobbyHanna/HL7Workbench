using System.Diagnostics;
using System.Text;
using System.Text.Json;
using HL7Workbench.Tests;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class OrdinaryViewerPerformanceTests
{
    [Fact]
    public async Task OrdinaryFileOpeningRetainsMaterializedRouteAndMeasuresWarmLatency()
    {
        var data = Encoding.UTF8.GetBytes("MSH|^~\\&|SYN|TEST|||||ADT^A01|SYN|T|2.5\rPID|1||SYNTHETIC\rNTE|1||SYNTHETIC NOTE");
        var timings = new List<double>();
        for (var iteration = 0; iteration < 31; iteration++)
        {
            using var manager = new DocumentManager(); using var input = new MemoryStream(data, false);
            var timer = Stopwatch.StartNew(); var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic", input, TestContext.Current.CancellationToken);
            if (iteration > 0) timings.Add(timer.Elapsed.TotalMilliseconds);
            Assert.NotNull(document.MaterializedMessage); Assert.Null(document.Source); Assert.Null(document.Presentation);
            manager.Close(document.Id); await document.Disposal;
        }
        timings.Sort();
        await File.WriteAllTextAsync(Path.Combine(LargeSyntheticFile.ArtifactDirectory, "ordinary-open.json"),
            JsonSerializer.Serialize(new { samples = timings.Count, meanMs = timings.Average(), medianMs = timings[timings.Count / 2], maxMs = timings[^1] },
                new JsonSerializerOptions { WriteIndented = true }), TestContext.Current.CancellationToken);
        // Timings are observations, not environment-dependent unit-test deadlines. Correct route/allocation bounds are the regression contract.
    }
}
