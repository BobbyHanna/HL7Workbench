using Xunit;

namespace HL7Workbench.Scripting.Tests;

public sealed class PortableWorkerTests
{
    [Theory]
    [InlineData("path with spaces")] [InlineData("Unicode-雪")]
    public void ApphostResolutionUsesTheProvidedAbsoluteLayoutWithoutShellQuoting(string folder)
    {
        var parent = Path.Combine(Path.GetTempPath(), "synthetic-script-layout-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, folder); Directory.CreateDirectory(root);
        try
        {
            var dll = Path.Combine(root, "HL7Workbench.ScriptHost.dll"); File.WriteAllBytes(dll, []);
            var native = Path.ChangeExtension(dll, OperatingSystem.IsWindows() ? ".exe" : null); File.WriteAllBytes(native, []);
            var start = ScriptWorkerSession.CreateStartInfo(dll, "synthetic-pipe");
            Assert.Equal(native, start.FileName); Assert.Equal(new[] { "synthetic-pipe" }, start.ArgumentList);
            Assert.False(start.UseShellExecute); Assert.True(start.CreateNoWindow);
        }
        finally { Directory.Delete(parent, true); }
    }
    [Fact]
    public void DefaultDllSelectsOnlyItsAdjacentApphostWithoutInstalledDotnetOrPayloadArguments()
    {
        var dll = Path.Combine(AppContext.BaseDirectory, "ScriptHost", "HL7Workbench.ScriptHost.dll");
        var host = Path.ChangeExtension(dll, OperatingSystem.IsWindows() ? ".exe" : null);
        Assert.True(File.Exists(host));
        var start = ScriptWorkerSession.CreateStartInfo(dll, "synthetic-pipe");
        Assert.Equal(host, start.FileName); Assert.Equal(new[] { "synthetic-pipe" }, start.ArgumentList);
        Assert.False(start.UseShellExecute); Assert.True(start.CreateNoWindow);
        Assert.True(start.RedirectStandardOutput); Assert.True(start.RedirectStandardError);
    }

    [Fact]
    public void ExplicitNativeHostAndDllOnlyDevelopmentFallbackRetainTheirLaunchContracts()
    {
        var root = Path.Combine(Path.GetTempPath(), "synthetic-worker-launch-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try
        {
            var dll = Path.Combine(root, "worker.dll"); File.WriteAllBytes(dll, []);
            var fallback = ScriptWorkerSession.CreateStartInfo(dll, "synthetic-pipe");
            Assert.Equal(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet", fallback.FileName);
            Assert.Equal(new[] { dll, "synthetic-pipe" }, fallback.ArgumentList);
            var native = Path.ChangeExtension(dll, OperatingSystem.IsWindows() ? ".exe" : null); File.WriteAllBytes(native, []);
            Assert.Equal(native, ScriptWorkerSession.CreateStartInfo(dll, "synthetic-pipe").FileName);
            Assert.Equal(native, ScriptWorkerSession.CreateStartInfo(native, "synthetic-pipe").FileName);
        }
        finally { Directory.Delete(root, true); }
    }
}
