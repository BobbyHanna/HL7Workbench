using Avalonia.Interactivity;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    private readonly List<TransportToolViewModel> transportModels = [];
    private string transportProfilesPath = "";
    private object CreateTransport(bool listener)
    {
        var model = new TransportToolViewModel(documents, transportProfilesPath, listener);
        model.ConfirmAsync = (title, prompt) => ConfirmationDialog.Transport(title, prompt).ShowDialog<bool>(DialogOwner());
        model.OpenedDocument += document => factory.OpenDocument(document.Id);
        model.Released += () => transportModels.Remove(model);
        transportModels.Add(model); return model;
    }
    private void ShowConnections(object? sender, RoutedEventArgs e) => factory.ShowTool(WorkspaceDockFactory.ConnectionManagerId);
    private void ShowListener(object? sender, RoutedEventArgs e) => factory.ShowTool(WorkspaceDockFactory.MllpListenerId);
}
