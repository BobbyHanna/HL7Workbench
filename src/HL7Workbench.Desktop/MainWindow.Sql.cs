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
    private readonly SqlQueryHistory sqlHistory = new();
    private string sqlLibraryDirectory = "";
    private SqlQueryViewModel? ActiveSql => registry.IsCreated(WorkspaceDockFactory.SqlQueryId) ?
        (SqlQueryViewModel)registry.GetContent(WorkspaceDockFactory.SqlQueryId) : null;
    private object CreateSqlQuery()
    {
        var model = new SqlQueryViewModel(documents, new(sqlLibraryDirectory), sqlHistory);
        model.OpenDocumentRequested += factory.OpenDocument;
        model.PromptUnsavedAsync = () => ConfirmationDialog.Unsaved("Save the SQL query before continuing? Enter a query name in the tool before choosing Save. Saved SQL may contain sensitive literals.")
            .ShowDialog<UnsavedChangesDecision>(DialogOwner());
        model.ConfirmDeleteAsync = name => ConfirmationDialog.DeleteSql(name).ShowDialog<bool>(DialogOwner());
        model.ChooseExportAsync = () => SaveFilePicker.ChooseAsync(DialogOwner(), new FilePickerSaveOptions
        { Title = "Export all SQL result rows", SuggestedFileName = "query-results.csv", DefaultExtension = "csv", FileTypeChoices = [new("CSV") { Patterns = ["*.csv"] }] }, lifetime.Token);
        return model;
    }
    private void ShowSqlQuery(object? sender, RoutedEventArgs e)
    {
        factory.ShowTool(WorkspaceDockFactory.SqlQueryId);
        if (ActiveSql is { } model) Dispatcher.UIThread.Post(model.NotifyShown, DispatcherPriority.Loaded);
    }
    private Task<bool> ConfirmSqlCloseAsync() => ActiveSql?.ConfirmCloseAsync() ?? Task.FromResult(true);
}
