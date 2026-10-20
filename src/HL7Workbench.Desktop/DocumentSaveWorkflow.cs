using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop;

/// <summary>A native picker can outlive edits, closure or activation of another document.</summary>
internal sealed class DocumentSaveWorkflow(DocumentManager documents)
{
    private readonly HashSet<MessageDocument> pending = [];
    internal async Task<bool> SaveAsync(MessageDocument document, bool saveAs, Func<Task<FileSaveDestination?>> choose, CancellationToken token)
    {
        if (document.IsSaving || document.IsPreparingInsertion || !documents.Documents.Contains(document) || !pending.Add(document)) return false;
        try
        {
            var version = document.ContentVersion; var identity = document.SavePath;
            if (saveAs || identity is null)
            {
                var destination = await choose();
                if (destination is null) return false;
                if (!documents.Documents.Contains(document) || document.ContentVersion != version || document.SavePath != identity || document.IsSaving || document.IsPreparingInsertion)
                    throw new InvalidOperationException("Document changed while choosing a destination. Retry Save As.");
                await document.SaveAsync(destination, token);
            }
            else await document.SaveAsync(token: token);
            return true;
        }
        finally { pending.Remove(document); }
    }
}
