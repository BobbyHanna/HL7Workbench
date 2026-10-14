using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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
    private readonly CancellationTokenSource lifetime = new();
    private bool opening;
    private readonly string layoutPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "HL7Workbench", "workspace-layout.json");

    public MainWindow()
    {
        InitializeComponent();
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
            lifetime.Cancel();
            lifetime.Dispose();
            factory.Dispose();
            browser.Dispose();
            diagnostics.Dispose();
        };
    }

    private async void OpenFile(object? sender, RoutedEventArgs e)
    {
        if (opening || lifetime.IsCancellationRequested) return;
        opening = true;
        var cancellationToken = lifetime.Token;
        try
        {
            if (!StorageProvider.CanOpen) { Status.Text = "File picking is unavailable on this platform."; return; }
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open HL7 files", AllowMultiple = true,
                FileTypeFilter = [new("HL7 and text files") { Patterns = ["*.hl7", "*.txt", "*.dat"] }, FilePickerFileTypes.All]
            });
            var opened = 0;
            var failed = 0;
            foreach (var file in files)
            {
                using (file)
                {
                    try
                    {
                        await using var source = await file.OpenReadAsync();
                        var message = await MessageFileReader.ReadAsync(source, cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        var document = MessageFileReader.Open(documents, file.Name, message);
                        factory.OpenDocument(document.Id);
                        opened++;
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or Hl7LimitException or NotSupportedException)
                    { failed++; }
                }
            }
            if (files.Count > 0)
                Status.Text = $"Opened {opened} file(s) read-only; {failed} failed (unavailable, unreadable, or beyond parser limits). See Diagnostics for retained input warnings.";
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
        { if (!cancellationToken.IsCancellationRequested) Status.Text = "Unable to open the file picker. Try again."; }
        finally { opening = false; }
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
