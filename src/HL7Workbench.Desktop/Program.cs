using Avalonia;
using Dock.Settings;

namespace HL7Workbench.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .UseOwnerForFloatingWindows()
            .CloseFloatingWindowsOnMainWindowClose()
            .StartWithClassicDesktopLifetime(args);
}
