using System.Diagnostics;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ScriptReferenceNativeInputTests
{
    [Fact]
    public async Task ExactReferenceCopyPastesThroughNativeEditorCommandsAndRetainsLocalUndo()
    {
        const string marker = "HL7WORKBENCH_REFERENCE_NATIVE_CHILD";
        if (Environment.GetEnvironmentVariable(marker) == "1")
        { await ScriptApiReferenceTests.NativeReferencePasteCase(); return; }
        // AvaloniaEdit's process-wide default KeyBindings belong to their original
        // dispatcher. Match the existing native-input regressions without remapping keys.
        using var process = new Process { StartInfo = new ProcessStartInfo
        {
            FileName = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet",
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        } };
        process.StartInfo.ArgumentList.Add(typeof(ScriptReferenceNativeInputTests).Assembly.Location);
        process.StartInfo.ArgumentList.Add("-class"); process.StartInfo.ArgumentList.Add(typeof(ScriptReferenceNativeInputTests).FullName!);
        process.StartInfo.Environment[marker] = "1";
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken); deadline.CancelAfter(TimeSpan.FromMinutes(2));
        Assert.True(process.Start()); var output = process.StandardOutput.ReadToEndAsync(deadline.Token); var errors = process.StandardError.ReadToEndAsync(deadline.Token);
        try { await process.WaitForExitAsync(deadline.Token); }
        finally { if (!process.HasExited) process.Kill(entireProcessTree: true); }
        Assert.True(process.ExitCode == 0, await output + await errors);
    }
}
