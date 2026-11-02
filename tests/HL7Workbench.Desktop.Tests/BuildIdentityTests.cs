using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.VisualTree;
using HL7Workbench.Desktop.Viewer;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class BuildIdentityTests
{
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("0.1.0-beta")]
    [InlineData("0.1.0-beta+short")] [InlineData("0.1.0-beta+https://private.invalid/token")]
    [InlineData("C:\\private user\\build")] [InlineData("0.1.0-beta+12345678+other")]
    public void MissingOrUntrustedRevisionHasSafeFallback(string? metadata)
    {
        var identity = BuildIdentity.FromMetadata(metadata);
        Assert.Equal("unavailable", identity.Build);
        Assert.DoesNotContain("private", identity.Version); Assert.DoesNotContain("token", identity.Version);
    }
    [Theory]
    [InlineData(40)] [InlineData(64)]
    public void FullRevisionDisplaysOnlyBoundedShortIdentity(int length)
    {
        var identity = BuildIdentity.FromMetadata("0.1.0-beta+" + new string('A', length));
        Assert.Equal("0.1.0-beta", identity.Version); Assert.Equal("aaaaaaaa", identity.Build);
    }
    [Fact]
    public async Task AboutUsesActualAssemblyMetadataWithoutDeveloperTools()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var expected = BuildIdentity.FromMetadata(typeof(MainWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
            var window = new AboutWindow(); window.Show(); window.UpdateLayout();
            try { Assert.Equal("Build " + expected.Build, Assert.Single(window.GetVisualDescendants().OfType<TextBlock>(), t => t.Name == "BuildIdentifier").Text); }
            finally { window.Close(); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
    [Fact]
    public void CoveredResourceIsEmbeddedAsTheExactSourceBytesDeliveredByTheBuilder()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "global.json"))) root = root.Parent;
        Assert.NotNull(root);
        var expected = File.ReadAllBytes(Path.Combine(root.FullName, "src/HL7Workbench.Core/ValidationCatalog/v251-model.json"));
        var assembly = typeof(HL7Workbench.Core.Hl7Parser).Assembly;
        using var resource = assembly.GetManifestResourceStream(Assert.Single(assembly.GetManifestResourceNames(), n => n.EndsWith("v251-model.json", StringComparison.Ordinal)))!;
        using var bytes = new MemoryStream(); resource.CopyTo(bytes); Assert.Equal(expected, bytes.ToArray());
    }
}
