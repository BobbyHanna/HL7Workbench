using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    private readonly List<LoadTestingViewModel> loadModels = [];
    private object CreateLoadTesting()
    {
        var model = new LoadTestingViewModel(documents, transportProfilesPath);
        model.ConfirmAsync = (title, prompt) => LoadTestingConfirmationDialog.Create(title, prompt).ShowDialog<bool>(DialogOwner());
        model.ChooseExportAsync = summary => SaveFilePicker.ChooseAsync(DialogOwner(), new FilePickerSaveOptions
        { Title = "Export privacy-safe load test report", SuggestedFileName = summary ? "load-test-summary.txt" : "load-test.csv", DefaultExtension = summary ? "txt" : "csv", FileTypeChoices = [new(summary ? "Text summary" : "CSV") { Patterns = [summary ? "*.txt" : "*.csv"] }] });
        model.Released += () => loadModels.Remove(model); loadModels.Add(model); return model;
    }
    private void ShowLoadTesting(object? sender, RoutedEventArgs e) => factory.ShowTool(WorkspaceDockFactory.LoadTestingId);
    private async Task DrainLoadToolsAsync()
    {
        var models = loadModels.ToArray(); foreach (var model in models) model.Stop();
        try { await Task.WhenAll(models.SelectMany(m => new[] { m.ActionTask, m.Session.Execution, m.Disposal })); }
        catch (Exception error) when (error is IOException or InvalidDataException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException or ArgumentException or NotSupportedException or HL7Workbench.Core.Hl7LimitException) { }
    }
}
