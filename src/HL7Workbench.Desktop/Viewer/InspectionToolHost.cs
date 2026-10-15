using Avalonia;
using Avalonia.Controls;
using HL7Workbench.Desktop.ViewModels;

namespace HL7Workbench.Desktop.Viewer;

internal sealed class InspectionToolHost(InspectionToolViewModel model) : ContentControl
{
    private void Refresh()
    {
        Content = null;
        var document = model.Document;
        Content = document is { IsLoading: false } && (document.Source is not null || document.MaterializedMessage?.HasDecodedText == true) &&
            (document.ActiveInspection is not null || document.MaterializedMessage is not null) ?
            new InspectionPanes(document.Inspection, document, model.IsNavigator ? InspectionPaneKind.Navigator : InspectionPaneKind.Inspector) :
            new TextBlock { Text = "No inspectable active message.", Margin = new(8) };
    }
    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => Refresh();
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    { base.OnAttachedToVisualTree(e); model.PropertyChanged += Changed; Refresh(); }
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    { model.PropertyChanged -= Changed; Content = null; base.OnDetachedFromVisualTree(e); }
}
