using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace HL7Workbench.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Viewer.ApplicationTheme.InitializeResources(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        DevelopmentDiagnostics.Initialize();
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnMainWindowClose;
            desktop.Exit += (_, e) => DevelopmentDiagnostics.Record($"Application exit requested (code {e.ApplicationExitCode}).");
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }
}

/// <summary>Development-only lifecycle diagnostics. Exception messages are omitted to avoid recording payloads.</summary>
internal static class DevelopmentDiagnostics
{
#if DEBUG
    private static readonly string LogPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HL7Workbench", "development-diagnostics.log");
#endif

    public static void Initialize()
    {
#if DEBUG
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            RecordException($"Unhandled exception (terminating={e.IsTerminating})", e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => RecordException("Unobserved task exception", e.Exception);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Record($"Process exit (code {Environment.ExitCode}).");
        Dispatcher.UIThread.UnhandledException += (_, e) => RecordException("Unhandled UI dispatcher exception", e.Exception);
#endif
    }

    public static void Record(string message)
    {
#if DEBUG
        Write(message);
#endif
    }

#if DEBUG
    private static void RecordException(string context, Exception? exception)
    {
        var details = exception is null ? "Exception object unavailable." :
            $"Type: {exception.GetType().FullName}{Environment.NewLine}Stack:{Environment.NewLine}{exception.StackTrace}";
        Write($"{context}{Environment.NewLine}{details}");
    }

    private static void Write(string details)
    {
        var entry = $"[{DateTimeOffset.Now:O}] {details}{Environment.NewLine}";
        System.Diagnostics.Debug.WriteLine(entry);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            File.AppendAllText(LogPath, entry);
        }
        catch (IOException error) { System.Diagnostics.Debug.WriteLine($"Unable to write development diagnostics: {error.GetType().Name}"); }
        catch (UnauthorizedAccessException error) { System.Diagnostics.Debug.WriteLine($"Unable to write development diagnostics: {error.GetType().Name}"); }
    }
#endif
}
