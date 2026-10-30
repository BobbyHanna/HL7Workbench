using Avalonia.Interactivity;
using Avalonia.Threading;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    private object CreateDocumentation()
    {
        var model = new DocumentationViewModel();
        model.ScriptApiRequested += () => ShowScriptApiReference(this, new());
        return model;
    }
    private void ShowDocumentation(object? sender, RoutedEventArgs e)
    {
        factory.ShowTool(WorkspaceDockFactory.DocumentationId);
        var model = (DocumentationViewModel)registry.GetContent(WorkspaceDockFactory.DocumentationId);
        Dispatcher.UIThread.Post(model.NotifyShown, DispatcherPriority.Loaded);
    }
}
