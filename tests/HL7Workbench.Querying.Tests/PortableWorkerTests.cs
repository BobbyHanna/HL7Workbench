using Xunit;

namespace HL7Workbench.Querying.Tests;

public sealed class PortableWorkerTests
{
    [Theory]
    [InlineData("path with spaces")] [InlineData("Unicode-雪")]
    public void DllResolverSelectsOnlyItsExactAdjacentApphostAndKeepsArgumentsSeparate(string folder)
    {
        var root = Path.Combine(Path.GetTempPath(), "synthetic-query-layout-" + Guid.NewGuid().ToString("N"), folder);
        Directory.CreateDirectory(root);
        try
        {
            var dll = Path.Combine(root, "HL7Workbench.QueryHost.dll"); File.WriteAllBytes(dll, []);
            var native = Path.ChangeExtension(dll, OperatingSystem.IsWindows() ? ".exe" : null); File.WriteAllBytes(native, []);
            var start = QueryWorkerSession.StartInfo(dll, "hl7-query-synthetic");
            Assert.Equal(native, start.FileName); Assert.Equal(new[] { "hl7-query-synthetic" }, start.ArgumentList);
            Assert.False(start.UseShellExecute); Assert.True(start.CreateNoWindow);
            Assert.True(start.RedirectStandardOutput); Assert.True(start.RedirectStandardError);
            File.Delete(native);
            var fallback = QueryWorkerSession.StartInfo(dll, "hl7-query-synthetic");
            Assert.Equal(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet", fallback.FileName);
            Assert.Equal(new[] { dll, "hl7-query-synthetic" }, fallback.ArgumentList);
            // Development fallback is unchanged; current portable validation rejects
            // this missing-apphost layout before it can be mistaken for self-contained.
        }
        finally { Directory.Delete(Path.GetDirectoryName(root)!, true); }
    }
}
