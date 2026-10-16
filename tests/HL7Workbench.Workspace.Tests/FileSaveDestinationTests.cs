using System.Text;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Workspace.Tests;

public sealed class FileSaveDestinationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "hl7-save-test-" + Guid.NewGuid().ToString("N"));
    private static CancellationToken Token => TestContext.Current.CancellationToken;
    public FileSaveDestinationTests() => Directory.CreateDirectory(root);
    private string Target => Path.Combine(root, "synthetic.hl7");
    private static Task Content(Stream output, CancellationToken token) => output.WriteAsync(Encoding.UTF8.GetBytes("SYNTHETIC NEW"), token).AsTask();
    private void NoTemporaryFiles() => Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    public void Dispose() => Directory.Delete(root, true);

    [Fact]
    public async Task NewDestinationPublishesCompleteContentAndRefusesReuseWithoutConsent()
    {
        var destination = FileSaveDestination.NewFile(Target);
        await destination.WriteAsync(Content, Token);
        Assert.Equal("SYNTHETIC NEW", await File.ReadAllTextAsync(Target, Token));
        await Assert.ThrowsAnyAsync<IOException>(() => destination.WriteAsync(Content, Token)); NoTemporaryFiles();
    }
    [Fact]
    public async Task CapturedExistingVersionAllowsReplacementButUnauthorizedPathDoesNot()
    {
        await File.WriteAllTextAsync(Target, "SYNTHETIC OLD", Token);
        await Assert.ThrowsAnyAsync<IOException>(() => FileSaveDestination.NewFile(Target).WriteAsync(Content, Token));
        Assert.Equal("SYNTHETIC OLD", await File.ReadAllTextAsync(Target, Token));
        var consent = await FileSaveDestination.CaptureAsync(Target, Token); Assert.True(consent.ReplacesExistingFile);
        await consent.WriteAsync(Content, Token);
        Assert.Equal("SYNTHETIC NEW", await File.ReadAllTextAsync(Target, Token)); NoTemporaryFiles();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task WriteFailureOrCancellationPreservesDestinationAndCleansStaging(bool cancel)
    {
        await File.WriteAllTextAsync(Target, "SYNTHETIC OLD", Token);
        var consent = await FileSaveDestination.CaptureAsync(Target, Token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(Token);
        var error = await Record.ExceptionAsync(() => consent.WriteAsync(async (stream, token) =>
        {
            await Content(stream, token);
            if (cancel) { cancellation.Cancel(); token.ThrowIfCancellationRequested(); }
            throw new IOException("Synthetic write failure.");
        }, cancellation.Token));
        if (cancel) Assert.IsAssignableFrom<OperationCanceledException>(error); else Assert.IsType<IOException>(error);
        Assert.Equal("SYNTHETIC OLD", await File.ReadAllTextAsync(Target, Token)); NoTemporaryFiles();
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task CollisionOrChangedDestinationDuringStagingIsNotReplaced(bool existing)
    {
        if (existing) await File.WriteAllTextAsync(Target, "SYNTHETIC OLD", Token);
        var consent = await FileSaveDestination.CaptureAsync(Target, Token);
        await Assert.ThrowsAnyAsync<IOException>(() => consent.WriteAsync(async (stream, token) =>
        {
            await Content(stream, token); await File.WriteAllTextAsync(Target, "SYNTHETIC CONCURRENT", token);
        }, Token));
        Assert.Equal("SYNTHETIC CONCURRENT", await File.ReadAllTextAsync(Target, Token)); NoTemporaryFiles();
    }
    [Fact]
    public async Task ChangedContentWithRestoredTimestampIsDetected()
    {
        await File.WriteAllTextAsync(Target, "SYNTHETIC OLD", Token); var time = File.GetLastWriteTimeUtc(Target);
        var consent = await FileSaveDestination.CaptureAsync(Target, Token);
        await File.WriteAllTextAsync(Target, "SYNTHETIC BAD", Token); File.SetLastWriteTimeUtc(Target, time);
        await Assert.ThrowsAsync<IOException>(() => consent.WriteAsync(Content, Token));
        Assert.Equal("SYNTHETIC BAD", await File.ReadAllTextAsync(Target, Token)); NoTemporaryFiles();
    }
    [Fact]
    public async Task WindowsIdentityDetectsReplacementWithIdenticalBytesAndTimes()
    {
        if (!OperatingSystem.IsWindows()) return;
        await File.WriteAllTextAsync(Target, "SYNTHETIC OLD", Token);
        var creation = File.GetCreationTimeUtc(Target); var modified = File.GetLastWriteTimeUtc(Target);
        var consent = await FileSaveDestination.CaptureAsync(Target, Token);
        var replacement = Path.Combine(root, "other.hl7"); await File.WriteAllTextAsync(replacement, "SYNTHETIC OLD", Token);
        File.Move(replacement, Target, true); File.SetCreationTimeUtc(Target, creation); File.SetLastWriteTimeUtc(Target, modified);
        await Assert.ThrowsAsync<IOException>(() => consent.WriteAsync(Content, Token));
        Assert.Equal("SYNTHETIC OLD", await File.ReadAllTextAsync(Target, Token)); NoTemporaryFiles();
    }
    [Fact]
    public async Task WindowsReplacementFailurePreservesOriginal()
    {
        if (!OperatingSystem.IsWindows()) return;
        await File.WriteAllTextAsync(Target, "SYNTHETIC OLD", Token);
        var consent = await FileSaveDestination.CaptureAsync(Target, Token);
        using (var held = new FileStream(Target, FileMode.Open, FileAccess.Read, FileShare.Read))
            await Assert.ThrowsAnyAsync<IOException>(() => consent.WriteAsync(Content, Token));
        Assert.Equal("SYNTHETIC OLD", await File.ReadAllTextAsync(Target, Token)); NoTemporaryFiles();
    }
    [Fact]
    public async Task ConcurrentSavesCannotBothPublishAgainstTheSameAuthorizedVersion()
    {
        await File.WriteAllTextAsync(Target, "SYNTHETIC OLD", Token);
        var first = await FileSaveDestination.CaptureAsync(Target, Token); var second = await FileSaveDestination.CaptureAsync(Target, Token);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); var count = 0;
        async Task<bool> Save(FileSaveDestination destination)
        {
            try
            {
                await destination.WriteAsync(async (stream, token) =>
                {
                    await Content(stream, token); if (Interlocked.Increment(ref count) == 2) ready.SetResult();
                    await ready.Task.WaitAsync(token);
                }, Token);
                return true;
            }
            catch (IOException) { return false; }
        }
        var outcomes = await Task.WhenAll(Save(first), Save(second));
        Assert.Single(outcomes, success => success); Assert.Single(outcomes, success => !success);
        Assert.Equal("SYNTHETIC NEW", await File.ReadAllTextAsync(Target, Token)); NoTemporaryFiles();
    }
    [Fact]
    public async Task DisappearingAuthorizedDestinationIsNotRecreated()
    {
        await File.WriteAllTextAsync(Target, "SYNTHETIC OLD", Token); var consent = await FileSaveDestination.CaptureAsync(Target, Token);
        File.Delete(Target);
        await Assert.ThrowsAsync<IOException>(() => consent.WriteAsync(Content, Token)); Assert.False(File.Exists(Target)); NoTemporaryFiles();
    }
}
