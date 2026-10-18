using System.Diagnostics;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class MessageInsertionNativeInputTests
{
    [Fact]
    public async Task NextToolbarFocusAndNativePasteRetainExactHl7WithoutAnEditorClick()
    {
        const string marker = "HL7WORKBENCH_INSERTION_NATIVE_CHILD";
        if (Environment.GetEnvironmentVariable(marker) == "1")
        { await MessageInsertionViewTests.NativePasteCase(); return; }
        // Keep real default keyboard/clipboard commands on their owning dispatcher;
        // unrelated headless tests intentionally create different UI dispatchers.
        using var process = new Process { StartInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        } };
        process.StartInfo.ArgumentList.Add(typeof(MessageInsertionNativeInputTests).Assembly.Location);
        process.StartInfo.ArgumentList.Add("-class"); process.StartInfo.ArgumentList.Add(typeof(MessageInsertionNativeInputTests).FullName!);
        process.StartInfo.Environment[marker] = "1";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); timeout.CancelAfter(TimeSpan.FromMinutes(2));
        Assert.True(process.Start()); var output = process.StandardOutput.ReadToEndAsync(timeout.Token); var errors = process.StandardError.ReadToEndAsync(timeout.Token);
        try { await process.WaitForExitAsync(timeout.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        Assert.True(process.ExitCode == 0, await output + await errors);
    }
}
