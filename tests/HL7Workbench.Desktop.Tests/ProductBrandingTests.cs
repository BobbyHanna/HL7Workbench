using System.Diagnostics;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Model.Controls;
using Dock.Model.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Transport;
using HL7Workbench.Workspace;
using Xunit;

namespace HL7Workbench.Desktop.Tests;

public sealed class ProductBrandingTests
{
    [Fact]
    public void PublicMetadataChangesWithoutRenamingExecutableAssemblyOrDefaultSettingsRoot()
    {
        var assembly = typeof(MainWindow).Assembly;
        Assert.Equal("HL7Workbench.Desktop", assembly.GetName().Name);
        Assert.Equal("Message Workbench", assembly.GetCustomAttribute<AssemblyProductAttribute>()!.Product);
        Assert.Equal("Message Workbench", assembly.GetCustomAttribute<AssemblyTitleAttribute>()!.Title);
        Assert.Equal(ProductBranding.Description, assembly.GetCustomAttribute<AssemblyDescriptionAttribute>()!.Description);
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HL7Workbench", "editor-preferences.json"), EditorPreferences.DefaultPath);
        if (OperatingSystem.IsWindows())
        {
            var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
            var executable = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../src/HL7Workbench.Desktop/bin", configuration, "net10.0/HL7Workbench.Desktop.exe"));
            var metadata = FileVersionInfo.GetVersionInfo(executable);
            Assert.Equal("Message Workbench", metadata.ProductName);
            Assert.Equal("Message Workbench", metadata.FileDescription);
            Assert.Equal("HL7Workbench.Desktop.dll", metadata.InternalName);
        }
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task MainStartupHelpAndAboutUseOfficialNameWithExistingThemeIconAndSettings(bool dark)
    {
        await using var session = HeadlessUnitTestSession.StartNew(typeof(ViewerTestApplication));
        await session.Dispatch(() =>
        {
            var root = Path.Combine(Path.GetTempPath(), "message-workbench-branding-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
            var preferencesPath = Path.Combine(root, "editor-preferences.json");
            new EditorPreferences(StartInEditMode: true, RibbonExpanded: true, Theme: dark ? ApplicationThemePreference.Dark : ApplicationThemePreference.Light).Save(preferencesPath);
            var profilesPath = Path.Combine(root, "transport-endpoints.json"); new TransportProfiles(profilesPath).Save([new() { Name = "Synthetic loopback" }]);
            var originalPreferences = File.ReadAllBytes(preferencesPath); var originalProfiles = File.ReadAllBytes(profilesPath);
            var main = new MainWindow(root); AboutWindow? about = null;
            try
            {
                main.Show(); Pump(main);
                Assert.Equal("Message Workbench", main.Title);
                Assert.StartsWith("Message Workbench •", main.FindControl<TextBlock>("Status")!.Text);
                var preferences = (ApplicationPreferences)typeof(MainWindow).GetField("preferences", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
                Assert.True(preferences.Current.StartInEditMode); Assert.True(preferences.Current.RibbonExpanded);
                var menu = main.FindControl<MenuItem>("AboutMenu")!;
                Assert.Equal("_About Message Workbench…", menu.Header);
                menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(main);
                about = (AboutWindow)typeof(MainWindow).GetField("aboutWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main)!;
                Assert.True(about.IsVisible); Assert.Equal("About Message Workbench", about.Title);
                Assert.Equal(dark ? ThemeVariant.Dark : ThemeVariant.Light, about.ActualThemeVariant);
                Assert.Same(main.Icon, about.Icon);
                var text = about.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
                Assert.Contains("Message Workbench", text); Assert.Contains(ProductBranding.Description, text);
                Assert.Contains(text, t => t is not null && t.Contains("FHIR R4") && t.Contains("TCP/MLLP"));
                menu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump(about);
                Assert.Same(about, typeof(MainWindow).GetField("aboutWindow", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(main));
                about.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                main.KeyRelease(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null); Pump(main);
                Assert.False(about.IsVisible); Assert.True(main.IsVisible);
                main.Close();
                Assert.Equal(originalPreferences, File.ReadAllBytes(preferencesPath)); Assert.Equal(originalProfiles, File.ReadAllBytes(profilesPath));
                var reopened = new MainWindow(root);
                try
                {
                    reopened.Show(); Pump(reopened); Assert.Equal("Message Workbench", reopened.Title);
                    Assert.Equal(originalPreferences, File.ReadAllBytes(preferencesPath)); Assert.Equal(originalProfiles, File.ReadAllBytes(profilesPath));
                }
                finally { reopened.Close(); }
            }
            finally { about?.Close(); main.Close(); Directory.Delete(root, true); }
            return true;
        }, TestContext.Current.CancellationToken);
    }

    [Fact]
    public void ExistingFloatingLayoutRestoresPlacementWithOfficialPublicTitleAndStableToolIdentity()
    {
        using var documents = new DocumentManager(); var tools = new ToolWindowRegistry();
        tools.Register(new(WorkspaceDockFactory.BrowserId, "Message Browser", () => new object()));
        tools.Register(new(WorkspaceDockFactory.DiagnosticsId, "Activity Log", () => new object()));
        using var factory = new WorkspaceDockFactory(documents, tools) { HostWindowLocator = new() { [nameof(IDockWindow)] = () => new FakeHostWindow() } };
        factory.CreateLayout(); factory.ShowTool(WorkspaceDockFactory.DiagnosticsId);
        var tool = factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId)!; factory.FloatDockable(tool);
        var host = Assert.Single(factory.Root.Windows!).Host!; host.SetPosition(100, 100); host.SetSize(640, 480);
        var legacy = LayoutCodec.Encode(factory.Capture()); factory.CloseFloatingWindows(); factory.Restore(LayoutCodec.Decode(legacy), [new(0, 0, 1920, 1080)]);
        var restored = Assert.Single(factory.Root.Windows!);
        Assert.Equal("Message Workbench", restored.Title); Assert.Equal(640, restored.Width); Assert.Equal(480, restored.Height);
        Assert.Equal(WorkspaceDockFactory.DiagnosticsId, factory.GetDockable<ITool>(WorkspaceDockFactory.DiagnosticsId)!.Id);
        factory.CloseFloatingWindows();
    }

    private static void Pump(Window window) { window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout(); }
}
