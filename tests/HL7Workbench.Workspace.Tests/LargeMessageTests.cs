using System.Diagnostics;
using System.Text.Json;
using HL7Workbench.Core;
using HL7Workbench.Workspace;
using HL7Workbench.Tests;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class LargeMessageTests
{
    public static IEnumerable<object[]> Sizes()
    {
        foreach (var size in new[] { 20, 50, 100 }) yield return [size];
        if (Environment.GetEnvironmentVariable("HL7WORKBENCH_STRESS") == "1")
            foreach (var size in new[] { 256, 512 }) yield return [size];
    }
    [Theory]
    [MemberData(nameof(Sizes))]
    public async Task CompleteAttachmentAccessUsesBoundedMemory(int mib)
    {
        var token = TestContext.Current.CancellationToken;
        var size = mib * 1024L * 1024;
        var path = await LargeSyntheticFile.CreateAsync(size, token);
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
        var before = GC.GetTotalMemory(true);
        var allocatedBefore = GC.GetTotalAllocatedBytes();
        var process = Process.GetCurrentProcess(); process.Refresh();
        var privateBefore = process.PrivateMemorySize64;
        long peakManaged = before, peakPrivate = privateBefore;
        using var sampling = CancellationTokenSource.CreateLinkedTokenSource(token);
        var sampler = Task.Run(async () =>
        {
            try
            {
                while (!sampling.IsCancellationRequested)
                {
                    peakManaged = Math.Max(peakManaged, GC.GetTotalMemory(false));
                    process.Refresh(); peakPrivate = Math.Max(peakPrivate, process.PrivateMemorySize64);
                    await Task.Delay(10, sampling.Token);
                }
            }
            catch (OperationCanceledException) when (sampling.IsCancellationRequested) { }
        }, token);
        var timer = Stopwatch.StartNew();
        await using var input = File.OpenRead(path);
        using var snapshot = await SourceSnapshot.CaptureAsync(input, token);
        var captureMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart(); var index = MessageFileIndex.Build(snapshot, token); var indexMs = timer.Elapsed.TotalMilliseconds;
        Assert.Equal(2, index.Messages.Count);
        timer.Restart();
        using var source = DecodedMessageSource.Create(snapshot, index.Messages[0].PayloadRange, index.Encoding, 0, token);
        var decodeMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart(); var structure = Hl7StructureScanner.Scan(source, index.Encoding, cancellationToken: token);
        var scanMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart(); var presentation = MessagePresentation.Create(source, structure, token); var projectionMs = timer.Elapsed.TotalMilliseconds;
        timer.Restart(); var displayed = presentation.Read(source, new(0, presentation.Length)); var presentationReadMs = timer.Elapsed.TotalMilliseconds;
        Assert.Contains("AFTER-FIELD", displayed); Assert.Contains("AFTER-SEGMENT", displayed);
        Assert.True(displayed.Length < 2000); Assert.False(presentation.RequiresVirtualization);
        var inspection = new MessageInspection(structure, source);
        var obx = inspection.Segments.Single(s => s.Identifier == "OBX");
        inspection.SelectSegment(obx);
        var field = inspection.InspectField(5)!;
        var attachment = field.Children[0].Children[4];
        Assert.Equal(size, attachment.Range64!.Value.Length);
        timer.Restart(); var tail = inspection.ReadPreview(attachment, size - 8); var previewMs = timer.Elapsed.TotalMilliseconds;
        Assert.Equal("VEFJTA==", tail);
        Assert.Equal("QUJD", inspection.ReadPreview(attachment, size / 2, 4));
        Assert.Equal("AFTER-FIELD", inspection.ReadPreview(inspection.InspectField(6)!));
        Assert.Contains(inspection.Segments, s => s.Identifier == "NTE");
        timer.Restart(); var window = source.ReadWindow(attachment.Range64.Value.Offset + size - 100);
        var windowMs = timer.Elapsed.TotalMilliseconds;
        Assert.Contains("AFTER-SEGMENT", window);
        Assert.True(window.Length <= DecodedMessageSource.MaxReadCharacters);
        var retained = GC.GetTotalMemory(true) - before;
        var allocated = GC.GetTotalAllocatedBytes() - allocatedBefore;
        sampling.Cancel(); await sampler;
        Assert.True(retained < 64L * 1024 * 1024, $"Retained managed bytes: {retained}");
        Assert.True(allocated < 16L * 1024 * 1024, $"Source pipeline allocated {allocated} bytes for a fixed small structure.");
        // Exercise the actual file session and lazy re-entry, separately from scanner measurements.
        using var manager = new DocumentManager(); input.Position = 0;
        timer.Restart(); var document = await MessageFileReader.OpenFileAsync(manager, "Synthetic large ORU", input, token);
        var openMs = timer.Elapsed.TotalMilliseconds;
        Assert.NotNull(document.Source); Assert.Null(document.MaterializedMessage); Assert.Null(document.Failure);
        var cachedSource = document.Source;
        timer.Restart(); await document.NavigateAsync(2, token); var nextMs = timer.Elapsed.TotalMilliseconds;
        Assert.Null(document.Source); Assert.NotNull(document.MaterializedMessage);
        timer.Restart(); await document.NavigateAsync(1, token); var backMs = timer.Elapsed.TotalMilliseconds;
        Assert.NotNull(document.Source); Assert.Null(document.Failure);
        Assert.Same(cachedSource, document.Source);
        var navigationBefore = GC.GetTotalMemory(true);
        for (var repeat = 0; repeat < 3; repeat++)
        { await document.NavigateAsync(2, token); await document.NavigateAsync(1, token); }
        var navigationRetainedDelta = GC.GetTotalMemory(true) - navigationBefore;
        Assert.True(navigationRetainedDelta < 8L * 1024 * 1024);
        manager.Close(document.Id); await document.Disposal;
        Assert.False(Directory.Exists(document.Snapshot!.DirectoryPath));
        var report = new { encodedMiB = mib, sourceBytes = snapshot.Length, captureMs, indexMs, decodeMs, scanMs,
            previewMs, windowMs, openMs, nextMs, backMs, retainedManagedBytes = retained, totalAllocatedBytes = allocated,
            sampledPeakManagedDelta = peakManaged - before, sampledPeakPrivateDelta = peakPrivate - privateBefore, navigationRetainedDelta,
            projectionMs, presentationReadMs, displayCharacters = displayed.Length,
            runtime = Environment.Version.ToString(), os = Environment.OSVersion.ToString(), processors = Environment.ProcessorCount };
        await File.WriteAllTextAsync(Path.Combine(LargeSyntheticFile.ArtifactDirectory, $"benchmark-{mib}MiB.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), token);
        TestContext.Current.TestOutputHelper?.WriteLine(JsonSerializer.Serialize(report));
    }
}
