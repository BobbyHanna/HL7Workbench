using Avalonia.Controls;
using Avalonia.Interactivity;
using HL7Workbench.Core;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop;

public partial class MainWindow : Window
{
    private readonly DocumentManager documents = new();
    private readonly WorkspaceDockFactory factory;
    private readonly DiagnosticsViewModel diagnostics;
    private readonly MessageBrowserViewModel browser;
    private readonly string layoutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HL7Workbench", "workspace-layout.json");

    public MainWindow()
    {
        InitializeComponent();
        documents.Open(new("synthetic.adt", "Synthetic ADT", Hl7Parser.Parse("MSH|^~\\&|SYN|TEST|||20261013120000||ADT^A01|SYN-1|T|2.5\rPID|1||SYN-001\r")));
        documents.Open(new("synthetic.oru", "Synthetic ORU", Hl7Parser.Parse("MSH|^~\\&|SYN|TEST|||||ORU^R01||T|2.5\rOBX|1|TX|||Synthetic\\Q\\\r")));
        documents.Activate("synthetic.adt");
        browser = new MessageBrowserViewModel(documents);
        diagnostics = new(documents);
        var registry = new ToolWindowRegistry();
        registry.Register(new(WorkspaceDockFactory.BrowserId, "Message Browser", () => browser));
        registry.Register(new(WorkspaceDockFactory.DiagnosticsId, "Diagnostics", () => diagnostics));
        factory = new(documents, registry);
        browser.OpenRequested += factory.OpenDocument;
        WorkspaceDock.Layout = factory.CreateLayout();
        Opened += (_, _) => { if (File.Exists(layoutPath)) RestoreLayout(this, new()); };
        Closing += (_, _) => SaveLayout(this, new());
        Closed += (_, _) =>
        {
            factory.Dispose();
            browser.Dispose();
            diagnostics.Dispose();
        };
    }

    private void ShowBrowser(object? sender, RoutedEventArgs e) => factory.ShowTool(WorkspaceDockFactory.BrowserId);
    private void ShowDiagnostics(object? sender, RoutedEventArgs e) => factory.ShowTool(WorkspaceDockFactory.DiagnosticsId);
    private void SaveLayout(object? sender, RoutedEventArgs e)
    {
        try
        {
            var json = LayoutCodec.Encode(factory.Capture());
            Directory.CreateDirectory(Path.GetDirectoryName(layoutPath)!);
            var temporary = layoutPath + ".tmp";
            File.WriteAllText(temporary, json);
            File.Move(temporary, layoutPath, true);
            Status.Text = "Workspace layout saved.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { Status.Text = "Unable to save workspace layout; current workspace remains available."; }
    }
    private void RestoreLayout(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(layoutPath)) { Status.Text = "No saved layout. Use Save Layout first."; return; }
            if (new FileInfo(layoutPath).Length > LayoutCodec.MaxBytes) throw new InvalidDataException("Oversized layout.");
            var saved = LayoutCodec.Decode(File.ReadAllText(layoutPath));
            CloseFloatingWindows();
            var screens = Screens.All.Select(s => new ScreenArea(s.WorkingArea.X, s.WorkingArea.Y, s.WorkingArea.Width, s.WorkingArea.Height)).ToArray();
            WorkspaceDock.Layout = factory.Restore(saved, screens);
            Status.Text = "Workspace layout restored; unreachable windows moved to an available screen.";
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        {
            CloseFloatingWindows();
            WorkspaceDock.Layout = factory.CreateLayout();
            Status.Text = "Saved layout unavailable or invalid; default workspace restored.";
        }
    }
    private void ResetLayout(object? sender, RoutedEventArgs e)
    {
        CloseFloatingWindows();
        WorkspaceDock.Layout = factory.CreateLayout();
        Status.Text = "Default workspace restored. Save Layout to keep it.";
    }
    private void CloseFloatingWindows()
    {
        factory.CloseFloatingWindows();
    }
}
