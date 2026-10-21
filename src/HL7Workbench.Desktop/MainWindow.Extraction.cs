using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    private object CreateFieldExtraction()
    {
        var model = new FieldExtractionViewModel(documents, extractionDraft);
        model.Session.NavigationRequested += factory.OpenDocument;
        model.PromptUnsavedAsync = PromptExtractionChangesAsync;
        model.ConfirmDeleteAsync = async name => await ConfirmationDialog.DeleteExtraction(name).ShowDialog<bool>(DialogOwner());
        model.ChooseExportAsync = () => SaveFilePicker.ChooseAsync(DialogOwner(), new FilePickerSaveOptions
        { Title = "Export selected extraction rows", SuggestedFileName = "extraction.csv", DefaultExtension = "csv", FileTypeChoices = [new("CSV") { Patterns = ["*.csv"] }] });
        return model;
    }
    private void ShowFieldExtraction(object? sender, RoutedEventArgs e) => OpenFieldExtraction(null);
    private void OpenFieldExtraction(MessageDocument? document)
    {
        var model = (FieldExtractionViewModel)registry.GetContent(WorkspaceDockFactory.FieldExtractionId);
        if (document is not null) model.Session.SetSource(document);
        factory.ShowTool(WorkspaceDockFactory.FieldExtractionId);
        Dispatcher.UIThread.Post(model.NotifyShown);
    }
    private Task<UnsavedChangesDecision> PromptExtractionChangesAsync() =>
        ConfirmationDialog.Unsaved("Save changes to the extraction configuration before continuing?").ShowDialog<UnsavedChangesDecision>(DialogOwner());
}
