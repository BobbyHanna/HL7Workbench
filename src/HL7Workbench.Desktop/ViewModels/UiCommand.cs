using System.Windows.Input;

namespace HL7Workbench.Desktop.ViewModels;

/// <summary>Small async command adapter shared by menus, ribbon and tool controls.</summary>
public sealed class UiCommand(Func<Task> execute, Func<bool>? enabled = null) : ICommand
{
    public event EventHandler? CanExecuteChanged;
    public bool CanExecute(object? parameter) => enabled?.Invoke() ?? true;
    public async void Execute(object? parameter)
    {
        if (!CanExecute(parameter)) return;
        // The owning model changes busy/decision state before its first await so
        // every surface sees the same gate, including direct non-command callers.
        await execute();
    }
    public void Refresh() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
