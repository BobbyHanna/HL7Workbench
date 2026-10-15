using System.ComponentModel;
using HL7Workbench.Core;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.ViewModels;

public sealed class FullRawInspectorViewModel : INotifyPropertyChanged, IDisposable
{
    public RawInspectorSession? Session { get; private set; }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CloseRequested;
    public void Open(MessageDocument document, CharacterRange? range, string? path)
    {
        Release(); Session = new(document, range, path); Session.Closed += SessionClosed;
        PropertyChanged?.Invoke(this, new(nameof(Session)));
    }
    private void SessionClosed(object? sender, EventArgs e) => CloseRequested?.Invoke(this, EventArgs.Empty);
    private void Release()
    { if (Session is { } session) { session.Closed -= SessionClosed; session.Dispose(); Session = null; } }
    public void Dispose() { Release(); PropertyChanged = null; CloseRequested = null; }
}
