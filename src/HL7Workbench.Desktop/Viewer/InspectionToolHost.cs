using Avalonia;
using Avalonia.Controls;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

internal sealed class InspectionToolHost(InspectionToolViewModel model) : ContentControl
{
    private MessageDocument? owner;
    private InspectionPanes? panes;
    private readonly TextBlock unavailable = new() { Text = "No inspectable active message.", Margin = new(8) };
    private void Refresh()
    {
        var document = model.Document;
        if (document?.InspectionSnapshot is { } snapshot)
        {
            if (!ReferenceEquals(owner, document) || panes is null)
            {
                owner = document;
                panes = new(snapshot, document, model.IsNavigator ? InspectionPaneKind.Navigator : InspectionPaneKind.Inspector);
                Content = panes;
            }
            else panes.UpdateInspection(snapshot);
        }
        else
        {
            owner = document; panes = null;
            if (!ReferenceEquals(Content, unavailable)) Content = unavailable;
        }
    }
    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Refresh();
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); model.PropertyChanged += Changed; Refresh(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { model.PropertyChanged -= Changed; Content = null; panes = null; owner = null; base.OnDetachedFromVisualTree(e); }
}
