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
    private readonly MessageBrowserViewModel browser;
    private readonly ToolWindowRegistry registry = new();
    private readonly InspectionToolViewModel navigator;
    private readonly InspectionToolViewModel fields;
    private readonly ScriptEditorViewModel scripts;
    private readonly ScriptApiReferenceViewModel scriptReference = new();
    private readonly ExtractionConfigurationSession extractionDraft;
    private readonly SearchViewModel search;
    private readonly CancellationTokenSource lifetime = new();
    private bool opening;
    private CancellationTokenSource? openingCancellation;
    private readonly string layoutPath;

    public MainWindow() : this(Path.GetDirectoryName(EditorPreferences.DefaultPath)!) { }
    internal MainWindow(string configurationDirectory)
    {
        preferences = new(Path.Combine(configurationDirectory, "editor-preferences.json"));
        layoutPath = Path.Combine(configurationDirectory, "workspace-layout.json");
        InitializeComponent();
        browser = new MessageBrowserViewModel(documents);
        navigator = new(documents, true); fields = new(documents, false);
        scripts = new(documents, new ScriptLibrary(Path.Combine(configurationDirectory, "Scripts")),
            preferencesPath: Path.Combine(configurationDirectory, "script-editor-preferences.json"));
        scripts.PromptUnsavedAsync = PromptUnsavedScriptAsync;
        extractionDraft = new(new ExtractionLibrary(Path.Combine(configurationDirectory, "Extractions")));
        search = new(documents, new SearchLibrary(Path.Combine(configurationDirectory, "Searches")), preferences);
        registry.Register(new(WorkspaceDockFactory.BrowserId, "Message Browser", () => browser));
        registry.Register(new(WorkspaceDockFactory.ActivityLogId, "Activity Log", () => new ActivityLogViewModel(documents.Activity), Optional: true, ReleaseOnClose: true, RestoreVisibility: true));
        registry.Register(new(WorkspaceDockFactory.NavigatorId, "Segment Navigator", () => navigator));
        registry.Register(new(WorkspaceDockFactory.FieldInspectorId, "Field Inspector", () => fields));
        // Retain the draft model when the tool closes or Dock reconstructs its view.
        registry.Register(new(WorkspaceDockFactory.ScriptEditorId, "Script Editor", () => scripts, Optional: true));
        registry.Register(new(WorkspaceDockFactory.ScriptApiReferenceId, "Script API Reference", () => scriptReference, Optional: true, RestoreVisibility: true));
        scriptReference.CreateScriptAsync = scripts.CreateFromReferenceAsync;
        registry.Register(new(WorkspaceDockFactory.FieldExtractionId, "Field Extraction", CreateFieldExtraction, Optional: true, ReleaseOnClose: true));
        registry.Register(new(WorkspaceDockFactory.SyntheticPatientsId, "Synthetic Patients", CreateSyntheticPatients, Optional: true, ReleaseOnClose: true));
        registry.Register(new(WorkspaceDockFactory.SearchResultsId, "Find Messages", () => search, Optional: true, RestoreVisibility: true));
        registry.Register(new(WorkspaceDockFactory.ComparisonId, "Message Comparison", () => new ComparisonViewModel(documents), Optional: true, ReleaseOnClose: true));
        factory = new(documents, registry);
        factory.ConfirmDocumentCloseAsync = ConfirmDocumentCloseAsync;
        factory.ConfirmScriptCloseAsync = () => scripts.ConfirmCloseAsync(PromptUnsavedScriptAsync);
        scripts.ReviewRequested += factory.OpenDocument;
        search.Session.NavigationRequested += factory.OpenDocument;
        LoadEditPreferences();
        KeyDown += async (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.S && e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Control))
            { e.Handled = true; if (documents.ActiveDocument is { } active) await SaveDocumentAsync(active, e.KeyModifiers.HasFlag(Avalonia.Input.KeyModifiers.Shift)); }
        };
        registry.Register(new(WorkspaceDockFactory.FullRawId, "Full Source Viewer", () =>
        {
            var model = new FullRawInspectorViewModel();
            model.CloseRequested += (_, _) => factory.CloseDockable(factory.GetDockable<Dock.Model.Controls.ITool>(WorkspaceDockFactory.FullRawId));
            return model;
        }, Optional: true, ReleaseOnClose: true));
        AddHandler(Viewer.ViewerRequests.OpenInspectorEvent, (_, e) =>
        {
            var model = (FullRawInspectorViewModel)registry.GetContent(WorkspaceDockFactory.FullRawId);
            model.Open(e.Document, e.Range, e.Path); factory.ShowTool(WorkspaceDockFactory.FullRawId); e.Handled = true;
            e.Document.RecordActivity(ActivityKind.SourceViewerOpened);
        });
        AddHandler(Viewer.ViewerRequests.CompareOriginalEvent, (_, e) =>
        {
            OpenComparison().CompareWithOriginal(e.Document); e.Handled = true;
        });
        AddHandler(Viewer.ViewerRequests.FieldExtractionEvent, (_, e) => { OpenFieldExtraction(e.Document); e.Handled = true; });
        browser.OpenRequested += factory.OpenDocument;
        WorkspaceDock.Layout = factory.CreateLayout();
        InitializeShell();
        ActivityIndicatorHost.Content = new Viewer.ActivityStatusIndicator(documents.Activity, () => factory.ShowTool(WorkspaceDockFactory.ActivityLogId));
        documents.Activity.Record(ActivityKind.ApplicationReady);
        Opened += (_, _) => { if (File.Exists(layoutPath)) RestoreLayout(this, new()); };
        Closing += ConfirmWindowClose;
        Closed += (_, _) =>
        {
            lifetime.Cancel();
            lifetime.Dispose();
            factory.Dispose();
            browser.Dispose();
            navigator.Dispose(); fields.Dispose();
            scripts.Dispose();
            scriptReference.CreateScriptAsync = _ => Task.FromResult(false);
            search.Dispose();
            preferences.PropertyChanged -= PreferencesChanged;
            documents.PropertyChanged -= ShellCommandsChanged;
            documents.Dispose();
        };
    }

    private async void OpenFile(object? sender, RoutedEventArgs e)
    {
        if (opening || lifetime.IsCancellationRequested) return;
        opening = true;
        openingCancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var cancellationToken = openingCancellation.Token;
        CancelOpening.IsVisible = true;
        try
        {
            if (!StorageProvider.CanOpen) { Status.Text = "File picking is unavailable on this platform."; documents.Activity.Record(ActivityKind.ApplicationError, failure: ActivityFailure.Unsupported); return; }
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Open HL7 files", AllowMultiple = true,
                FileTypeFilter = [new("HL7 and text files") { Patterns = ["*.hl7", "*.txt", "*.dat"] }, FilePickerFileTypes.All]
            });
            var opened = 0;
            var allReadOnly = true;
            var failed = 0;
            var warnings = 0; var errors = 0;
            foreach (var file in files)
            {
                using (file)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var operation = documents.Activity.BeginFileOpen();
                    var readerStarted = false;
                    var reading = true;
                    try
                    {
                        await using var source = await file.OpenReadAsync();
                        var progress = new ThrottledProgress(new Progress<long>(bytes =>
                        { if (reading && !cancellationToken.IsCancellationRequested) Status.Text = $"Opening source (capture, index, first-message preparation): byte position {bytes:N0}. Cancel is available."; }));
                        readerStarted = true;
                        var document = await MessageFileReader.OpenFileAsync(documents, file.Name, source, cancellationToken, progress, activityDocumentNumber: operation, localPath: file.TryGetLocalPath());
                        cancellationToken.ThrowIfCancellationRequested();
                        factory.OpenDocument(document.Id);
                        opened++;
                        allReadOnly &= !document.IsEditMode;
                        var health = ActivityHealth.For(document); warnings += health.Warnings; errors += health.Errors;
                    }
                    catch (Exception error) when (error is IOException or UnauthorizedAccessException or Hl7LimitException or NotSupportedException)
                    { failed++; if (!readerStarted) documents.Activity.Record(ActivityKind.FileOpenFailed, operation, failure: ActivityLog.Classify(error)); }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    { if (!readerStarted) documents.Activity.Record(ActivityKind.FileOpenCanceled, operation); throw; }
                    finally { reading = false; }
                }
            }
            if (files.Count > 0)
                Status.Text = ActivityHealth.OpenStatus(opened, failed, new(warnings, errors), allReadOnly);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { Status.Text = "File opening canceled."; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
        { if (!cancellationToken.IsCancellationRequested) { Status.Text = "Unable to open the file picker. Try again."; documents.Activity.Record(ActivityKind.ApplicationError, failure: ActivityLog.Classify(error)); } }
        finally { opening = false; CancelOpening.IsVisible = false; openingCancellation.Dispose(); openingCancellation = null; }
    }
    private void CancelOpen(object? sender, RoutedEventArgs e) => openingCancellation?.Cancel();

    private void ShowBrowser(object? sender, RoutedEventArgs e) => factory.ShowTool(WorkspaceDockFactory.BrowserId);
    private void ShowActivity(object? sender, RoutedEventArgs e) => factory.ShowTool(WorkspaceDockFactory.ActivityLogId);
    private void ShowNavigator(object? sender, RoutedEventArgs e) => factory.ShowTool(WorkspaceDockFactory.NavigatorId);
    private void ShowFields(object? sender, RoutedEventArgs e) => factory.ShowTool(WorkspaceDockFactory.FieldInspectorId);
    private void ShowScripts(object? sender, RoutedEventArgs e) => factory.ShowTool(WorkspaceDockFactory.ScriptEditorId);
    private void ShowScriptApiReference(object? sender, RoutedEventArgs e)
    {
        factory.ShowTool(WorkspaceDockFactory.ScriptApiReferenceId);
        Avalonia.Threading.Dispatcher.UIThread.Post(scriptReference.NotifyShown);
    }
    private void ShowRaw(object? sender, RoutedEventArgs e)
    {
        if (documents.ActiveDocument is not { IsLoading: false } document || document.Source is null && document.MaterializedMessage?.HasDecodedText != true)
        { Status.Text = "Open an inspectable message first."; return; }
        if (document.HasWorkingSourceChanges) { Status.Text = "This message has edited working text. Use Save to publish it; original-source inspection is available on unchanged messages."; return; }
        var model = (FullRawInspectorViewModel)registry.GetContent(WorkspaceDockFactory.FullRawId);
        model.Open(document, null, null); factory.ShowTool(WorkspaceDockFactory.FullRawId);
        document.RecordActivity(ActivityKind.SourceViewerOpened);
    }
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
        { Status.Text = "Unable to save workspace layout; current workspace remains available."; documents.Activity.Record(ActivityKind.LayoutFailed, failure: ActivityLog.Classify(error)); }
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
            documents.Activity.Record(ActivityKind.LayoutFailed, failure: ActivityLog.Classify(error));
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
