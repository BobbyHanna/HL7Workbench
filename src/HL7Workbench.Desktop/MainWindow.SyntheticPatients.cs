using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    private object CreateSyntheticPatients()
    {
        var model = new SyntheticPatientsViewModel(documents);
        model.ChooseFilesAsync = async () =>
        {
            var owner = DialogOwner(); if (!owner.StorageProvider.CanOpen) throw new IOException("Local selection unavailable.");
            var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            { Title = "Import FHIR R4 Bundles", AllowMultiple = true, FileTypeFilter = [new("FHIR JSON") { Patterns = ["*.json"] }] });
            try { return files.Select(f => f.TryGetLocalPath() ?? throw new IOException("Local JSON required.")).ToArray(); }
            finally { foreach (var file in files) file.Dispose(); }
        };
        model.ChooseFolderAsync = async () =>
        {
            var owner = DialogOwner(); if (!owner.StorageProvider.CanPickFolder) throw new IOException("Local folder selection unavailable.");
            var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = "Import folder of FHIR R4 Bundles", AllowMultiple = false });
            try { return folders.Count == 0 ? null : folders[0].TryGetLocalPath() ?? throw new IOException("Local folder required."); }
            finally { foreach (var folder in folders) folder.Dispose(); }
        };
        model.Session.Generated += result => { factory.OpenDocument(result.Document.Id); Status.Text = $"Generated {result.MessageIds.Count:N0} FHIR-derived HL7 messages. Review the Generation Report and use Save As to keep output."; };
        return model;
    }
    private void ShowSyntheticPatients(object? sender, RoutedEventArgs e)
    {
        var model = (SyntheticPatientsViewModel)registry.GetContent(WorkspaceDockFactory.SyntheticPatientsId);
        factory.ShowTool(WorkspaceDockFactory.SyntheticPatientsId); Dispatcher.UIThread.Post(model.NotifyShown);
    }
}
