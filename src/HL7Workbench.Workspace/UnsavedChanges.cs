namespace HL7Workbench.Workspace;

public enum UnsavedChangesDecision { Cancel, Save, Discard }

/// <summary>Shared asynchronous close policy. Dismissal is Cancel; failed/canceled saves retain the document.</summary>
public static class UnsavedChanges
{
    public static async Task<bool> ConfirmAsync(MessageDocument document,
        Func<MessageDocument, Task<UnsavedChangesDecision>> prompt, Func<MessageDocument, Task<bool>> save)
    {
        if (document.IsSaving) return false;
        if (!document.IsDirty) return true;
        var version = document.ContentVersion;
        var choice = await prompt(document);
        // A dialog can outlive changes from another view; discard consent is scoped to
        // the revision the user saw, rather than permission to discard later edits.
        if (version != document.ContentVersion) return false;
        return choice switch
        {
            UnsavedChangesDecision.Discard => true,
            UnsavedChangesDecision.Save => await save(document) && !document.IsDirty,
            _ => false
        };
    }
}
