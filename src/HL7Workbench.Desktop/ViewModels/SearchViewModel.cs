using System.ComponentModel;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

public sealed class SearchViewModel : INotifyPropertyChanged, IDisposable
{
    public MessageSearchSession Session { get; }
    public string Value { get; set; } = "";
    public string FieldPath { get; set; } = "PID-3.1";
    public MessageSearchMode Mode { get; set; }
    public MessageSearchComparison Comparison { get; set; }
    public bool CaseSensitive { get; set; }
    public Task SearchingTask { get; private set; } = Task.CompletedTask;
    public UiCommand SearchCommand { get; }
    public UiCommand CancelCommand { get; }
    public UiCommand ClearCommand { get; }
    public UiCommand OpenCommand { get; }
    public event Action? OpenRequested;
    public event Action? FocusRequested;
    public event PropertyChangedEventHandler? PropertyChanged;
    public SearchViewModel(DocumentManager documents)
    {
        Session = new(documents);
        SearchCommand = new(() => SearchingTask = Session.StartAsync(new(Mode, Value, CaseSensitive, FieldPath, Comparison)), () => Session.Document is not null && !Session.IsBusy);
        CancelCommand = new(() => { Session.Cancel(); return Task.CompletedTask; }, () => Session.IsBusy);
        ClearCommand = new(() => { Value = ""; Session.Clear(); return Task.CompletedTask; });
        OpenCommand = new(() => { OpenRequested?.Invoke(); return Task.CompletedTask; });
        Session.PropertyChanged += Changed;
    }
    internal void NotifyShown() => FocusRequested?.Invoke();
    private void Changed(object? sender, PropertyChangedEventArgs e)
    { SearchCommand.Refresh(); CancelCommand.Refresh(); ClearCommand.Refresh(); PropertyChanged?.Invoke(this, new(null)); }
    public void Dispose()
    { Session.PropertyChanged -= Changed; Session.Dispose(); Value = FieldPath = ""; OpenRequested = FocusRequested = null; PropertyChanged = null; }
}
