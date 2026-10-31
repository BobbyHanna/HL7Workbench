using HL7Workbench.Desktop.Docking;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Desktop.ViewModels;
using HL7Workbench.Workspace;
using Avalonia.Controls;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    internal Task InspectorActionTask { get; private set; } = Task.CompletedTask;
    private void HandleInspectionAction(object? sender, InspectionActionRequestEventArgs e)
    {
        e.Handled = true;
        InspectorActionTask = RunInspectionActionAsync(e.Context, e.Action,
            e.Source is Control source ? TopLevel.GetTopLevel(source) as Window : null);
    }
    private async Task RunInspectionActionAsync(InspectionActionContext context, InspectorAction action, Window? owner)
    {
        if (!context.IsCurrent || !ReferenceEquals(documents.ActiveDocument, context.Document)) return;
        try
        {
            if (action == InspectorAction.EditValue)
            {
                using var edit = new StructuredValueEditSession(documents, context);
                var dialog = new StructuredValueEditWindow(edit);
                await dialog.ShowDialog<bool>(owner ?? DialogOwner());
                if (edit.IsApplied) await edit.RestoreSelectionAsync();
            }
            else if (action == InspectorAction.Extract)
            {
                var model = (FieldExtractionViewModel)registry.GetContent(WorkspaceDockFactory.FieldExtractionId);
                if (!model.CanManage || !await model.Draft.NewAsync(model.PromptUnsavedAsync,
                    () => context.IsCurrent && ReferenceEquals(documents.ActiveDocument, context.Document)) || !context.IsCurrent) return;
                model.Draft.Columns[0].Path = context.Path;
                model.Draft.Columns[0].Label = context.Path;
                model.Draft.DecodeEscapes = false;
                model.Draft.Mode = ExtractionMode.ExpandMatches;
                model.Session.SetSource(context.Document);
                factory.ShowTool(WorkspaceDockFactory.FieldExtractionId);
                model.NotifyShown();
            }
            else
            {
                factory.ShowTool(WorkspaceDockFactory.SearchResultsId);
                await search.StartFromInspectorAsync(context, action == InspectorAction.FilterByValue, lifetime.Token);
            }
        }
        catch (Exception error) when (error is OperationCanceledException or ObjectDisposedException or IOException or InvalidOperationException)
        { Status.Text = "Inspector action canceled or unavailable; source and unsaved edits are unchanged."; }
    }
}
