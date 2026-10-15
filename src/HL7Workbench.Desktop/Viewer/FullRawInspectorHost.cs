using Avalonia;
using Avalonia.Controls;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop.Viewer;

internal sealed class FullRawInspectorHost(FullRawInspectorViewModel model) : ContentControl
{
    private FullRawInspectorView? view;
    private void Refresh()
    {
        view?.ReleaseView(); view = model.Session is { IsClosed: false } session ? new(session) : null;
        Content = view;
    }
    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Refresh();
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); model.PropertyChanged += Changed; Refresh(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        model.PropertyChanged -= Changed;
        // Dock reparenting detaches controls without closing the tool session.
        view?.ReleaseView(); Content = null; view = null; base.OnDetachedFromVisualTree(e);
    }
}
