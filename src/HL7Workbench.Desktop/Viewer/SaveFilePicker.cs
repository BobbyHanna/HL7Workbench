using Avalonia.Controls;
using Avalonia.Platform.Storage;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Converts a picker result into version-scoped local save consent.</summary>
internal static class SaveFilePicker
{
    internal static bool HasNativeOverwriteConfirmation(IStorageProvider provider) => OperatingSystem.IsWindows() &&
        provider.GetType().FullName == "Avalonia.Win32.Win32StorageProvider" && provider.GetType().Assembly.GetName().Name == "Avalonia.Win32";

    internal static async Task<FileSaveDestination?> ChooseAsync(Window owner, FilePickerSaveOptions options, CancellationToken token = default)
    {
        if (!owner.StorageProvider.CanSave) throw new IOException("Local file saving is unavailable.");
        options.ShowOverwritePrompt = true;
        return await ChooseAsync(async () =>
        {
            using var file = await owner.StorageProvider.SaveFilePickerAsync(options);
            if (file is null) return null;
            return file.TryGetLocalPath() ?? throw new IOException("Local destination required for safe publication.");
        }, HasNativeOverwriteConfirmation(owner.StorageProvider), async path =>
        {
            var dialog = ConfirmationDialog.Overwrite(System.IO.Path.GetFileName(path));
            return await dialog.ShowDialog<bool>(owner);
        }, token);
    }
    // The boolean comes only from the audited built-in Windows provider above, not
    // an arbitrary path or a generic IStorageProvider contract. Other providers need
    // application consent. Keep the pre-confirmation version if that dialog waits.
    internal static async Task<FileSaveDestination?> ChooseAsync(Func<Task<string?>> pick, bool nativeConfirmation,
        Func<string, Task<bool>> confirm, CancellationToken token = default)
    {
        var path = await pick(); token.ThrowIfCancellationRequested();
        if (path is null) return null;
        var destination = await FileSaveDestination.CaptureAsync(path, token);
        if (destination.ReplacesExistingFile && !nativeConfirmation && !await confirm(destination.Path)) return null;
        token.ThrowIfCancellationRequested(); return destination;
    }
}
