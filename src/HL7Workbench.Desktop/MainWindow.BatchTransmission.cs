using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    private readonly List<BatchTransmissionViewModel> batchModels = [];
    private bool batchShutdownDraining;
    private object CreateBatchTransmission()
    {
        var model = new BatchTransmissionViewModel(documents, transportProfilesPath);
        model.ConfirmAsync = (title, prompt) => ConfirmationDialog.Transport(title, prompt).ShowDialog<bool>(DialogOwner());
        model.ChooseExportAsync = () => SaveFilePicker.ChooseAsync(DialogOwner(), new FilePickerSaveOptions
        { Title = "Export privacy-safe batch transmission report", SuggestedFileName = "batch-transmission.csv", DefaultExtension = "csv", FileTypeChoices = [new("CSV") { Patterns = ["*.csv"] }] });
        model.OpenedDocument += document => factory.OpenDocument(document.Id);
        // Retain ownership while disposal drains, including after a Dock close/reset.
        model.Released += () => batchModels.Remove(model);
        batchModels.Add(model); return model;
    }
    private void ShowBatchTransmission(object? sender, RoutedEventArgs e) => factory.ShowTool(WorkspaceDockFactory.BatchTransmissionId);
    private async Task DrainBatchToolsAsync()
    {
        var models = batchModels.ToArray();
        // Cancel/drain before normal unsaved-document prompts. If a later prompt
        // cancels window closure, the stopped tool still has inspectable results.
        // Accepted closure releases models through Closed/factory disposal.
        foreach (var model in models) model.Cancel();
        try { await Task.WhenAll(models.SelectMany(m => new[] { m.ActionTask, m.Session.Execution, m.Disposal })); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or OperationCanceledException or InvalidOperationException or ArgumentException or HL7Workbench.Core.Hl7LimitException) { }
    }
}
