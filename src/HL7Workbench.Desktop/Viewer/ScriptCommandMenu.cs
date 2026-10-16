using Avalonia.Controls;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop.Viewer;

internal static class ScriptCommandMenu
{
    internal static void Populate(MenuItem menu, ScriptEditorViewModel model)
    {
        foreach (var (label, command) in new[]
        {
            ("_New Script", model.NewCommand), ("_Open Script Editor", model.OpenCommand),
            ("_Save Script", model.SaveCommand), ("_Duplicate Script", model.DuplicateCommand),
            ("_Rename Script", model.RenameCommand), ("De_lete Script…", model.DeleteCommand),
            ("_Run Script", model.RunCommand), ("_Cancel Script", model.CancelCommand),
            ("Review _Output", model.ReviewCommand), ("Re_ports / Export CSV…", model.ReportsCommand)
        }) menu.Items.Add(new MenuItem { Header = label, Command = command });
    }
}
