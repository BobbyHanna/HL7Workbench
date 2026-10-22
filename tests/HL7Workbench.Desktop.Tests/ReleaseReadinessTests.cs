using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ReleaseReadinessTests
{
    [Fact]
    public void BetaMetadataPreservesAssemblyBindingIdentityAndSettingsRoot()
    {
        var assembly = typeof(MainWindow).Assembly;
        Assert.Equal(new Version(1, 0, 0, 0), assembly.GetName().Version);
        Assert.StartsWith("0.1.0-beta", assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion);
        Assert.Equal("0.1.0.0", assembly.GetCustomAttribute<AssemblyFileVersionAttribute>()!.Version);
        Assert.EndsWith(Path.Combine("HL7Workbench", "editor-preferences.json"), EditorPreferences.DefaultPath);
    }

    [Theory]
    [InlineData("malformed")] [InlineData("unsupported")] [InlineData("oversized")] [InlineData("invalid-graph")]
    public async Task CorruptStartupLayoutsRecoverWithoutDisclosureOrInitializingNetworkTools(string kind)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "synthetic-beta-layout-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var path = Path.Combine(root, "workspace-layout.json");
            var value = kind switch
            {
                "malformed" => "{SYNTHETIC-PRIVATE-PATH",
                "oversized" => new string(' ', LayoutCodec.MaxBytes + 1),
                "invalid-graph" => "{\"Version\":2,\"Root\":{\"Kind\":\"SYNTHETIC-PRIVATE\"},\"Windows\":[],\"HiddenTools\":[]}",
                _ => "{\"Version\":99,\"Root\":{\"Kind\":\"root\"},\"Windows\":[],\"HiddenTools\":[]}"
            };
            File.WriteAllText(path, value); var main = new MainWindow(root);
            try
            {
                main.Show(); main.UpdateLayout(); Dispatcher.UIThread.RunJobs();
                Assert.True(main.IsVisible); Assert.Contains("default workspace restored", main.FindControl<TextBlock>("Status")!.Text);
                Assert.DoesNotContain("SYNTHETIC-PRIVATE", main.FindControl<TextBlock>("Status")!.Text);
                Assert.Equal(value, File.ReadAllText(path)); // Recovery does not overwrite forensic metadata until normal Save/close.
                var registry = (ToolWindowRegistry)typeof(MainWindow).GetField("registry", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
                Assert.False(registry.IsCreated(Desktop.Docking.WorkspaceDockFactory.ConnectionManagerId));
                Assert.False(registry.IsCreated(Desktop.Docking.WorkspaceDockFactory.MllpListenerId));
                Assert.False(registry.IsCreated(Desktop.Docking.WorkspaceDockFactory.BatchTransmissionId));
                Assert.False(registry.IsCreated(Desktop.Docking.WorkspaceDockFactory.LoadTestingId));
                var about = new AboutWindow(); about.Show(main);
                Assert.Contains(about.GetVisualDescendants().OfType<TextBlock>(), t => t.Text == "Version 0.1.0-beta"); about.Close();
            }
            finally { main.Close(); Directory.Delete(root, true); }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task FailedLayoutPublicationCleansOnlyItsOwnTemporaryFileAndKeepsWorkspaceAvailable()
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "synthetic-beta-save-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var main = new MainWindow(root);
            try
            {
                main.Show(); var target = Path.Combine(root, "workspace-layout.json"); Directory.CreateDirectory(target);
                var unrelated = target + ".tmp"; File.WriteAllText(unrelated, "synthetic pre-existing metadata");
                typeof(MainWindow).GetMethod("SaveLayout", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(main, [main, new RoutedEventArgs()]);
                Assert.Contains("Unable to save", main.FindControl<TextBlock>("Status")!.Text); Assert.True(main.IsVisible);
                Assert.Equal("synthetic pre-existing metadata", File.ReadAllText(unrelated));
                Assert.Empty(Directory.GetFiles(root, "workspace-layout.json.*.tmp")); Assert.True(Directory.Exists(target));
            }
            finally { main.Close(); Directory.Delete(root, true); }
            return true;
        }, TestContext.Current.CancellationToken);
    }
}
