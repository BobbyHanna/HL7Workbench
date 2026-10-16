using System.ComponentModel;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Threading;
using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    private readonly ApplicationPreferences preferences;
    private PreferencesWindow? preferencesWindow;
    private UiCommand[] shellCommands = [];
    private void InitializeShell()
    {
        preferences.PropertyChanged += PreferencesChanged;
        scripts.OpenEditorRequested += () =>
        {
            factory.ShowTool(WorkspaceDockFactory.ScriptEditorId);
            Dispatcher.UIThread.Post(scripts.NotifyEditorShown);
        };
        scripts.ConfirmDeleteAsync = ConfirmDeleteScriptAsync;
        scripts.ShowReportsAsync = () => new ScriptReportsWindow(scripts).ShowDialog(DialogOwner());
        UiCommand Action(Action<object?, RoutedEventArgs> action, Func<bool>? enabled = null) => new(() => { action(this, new()); return Task.CompletedTask; }, enabled);
        var actions = new Dictionary<string, ICommand>
        {
            ["Open File…"] = Action(OpenFile),
            ["Save HL7"] = Action(SaveDocument, () => documents.ActiveDocument is { IsSaving: false, IsLoading: false }),
            ["Save HL7 As…"] = Action(SaveDocumentAs, () => documents.ActiveDocument is { IsSaving: false, IsLoading: false }),
            ["Save Layout"] = Action(SaveLayout), ["Restore Layout"] = Action(RestoreLayout), ["Preferences…"] = Action(ShowPreferences),
            ["Reset Layout / Recover Windows"] = Action(ResetLayout),
            ["Message Browser"] = Action(ShowBrowser), ["Segment Navigator"] = Action(ShowNavigator), ["Field Inspector"] = Action(ShowFields),
            ["Full Source Viewer"] = Action(ShowRaw), ["Activity Log"] = Action(ShowActivity)
        };
        shellCommands = actions.Values.OfType<UiCommand>().ToArray();
        documents.PropertyChanged += ShellCommandsChanged;
        RibbonHost.Content = new UtilityRibbon(preferences, scripts, actions);
    }
    private void ShellCommandsChanged(object? sender, PropertyChangedEventArgs e)
    { foreach (var command in shellCommands) command.Refresh(); }
    private void PreferencesChanged(object? sender, PropertyChangedEventArgs e)
    {
        documents.Preferences = preferences.Current; StartInEditMode.IsChecked = preferences.Current.StartInEditMode;
        Status.Text = preferences.Status;
    }
    private Window DialogOwner() => (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.Windows.FirstOrDefault(w => w.IsActive) ?? this;
    private async void ShowPreferences(object? sender, RoutedEventArgs e)
    {
        if (preferencesWindow is not null) { preferencesWindow.Activate(); return; }
        preferencesWindow = new(preferences);
        try { await preferencesWindow.ShowDialog(DialogOwner()); }
        finally { preferencesWindow = null; }
    }
    private async Task<bool> ConfirmDeleteScriptAsync(string name)
    {
        var dialog = ConfirmationDialog.Delete(name);
        return await dialog.ShowDialog<bool>(DialogOwner());
    }
}
