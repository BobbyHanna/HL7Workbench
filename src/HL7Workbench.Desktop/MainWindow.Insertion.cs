using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    private async Task InsertMessageAsync(MessageInsertionPosition position)
    {
        var document = documents.ActiveDocument;
        if (!MessageInsertionWorkflow.TryInsert(document, position, out var id, out var feedback))
        { Status.Text = feedback; return; }
        Status.Text = feedback;
        factory.OpenDocument(document!.Id);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            // Menus may close, tabs may change and Dock may rebuild the view before
            // this runs. Resolve the live host only for the original active document.
            if (!ReferenceEquals(documents.ActiveDocument, document) || document.ActiveMessageId != id) return;
            var windows = new[] { this }.OfType<Window>().Concat(factory.Root.Windows?.Select(w => w.Host).OfType<Window>() ?? []);
            foreach (var window in windows)
            {
                window.UpdateLayout();
                var view = window.GetVisualDescendants().OfType<FileDocumentView>().FirstOrDefault(v => ReferenceEquals(v.Document, document) && v.IsEffectivelyVisible);
                if (view is null) continue;
                view.FocusInsertedMessage(id); break;
            }
        }, DispatcherPriority.Loaded);
    }
}
