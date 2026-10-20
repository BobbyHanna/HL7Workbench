using HL7Workbench.Desktop.Viewer;
using HL7Workbench.Workspace;
using Avalonia.VisualTree;
using System.ComponentModel;

namespace HL7Workbench.Desktop;

public partial class MainWindow
{
    private SyntheticGenerationDialog? generationDialog;
    private async Task GenerateMessagesAsync()
    {
        if (generationDialog is not null) { generationDialog.Activate(); return; }
        if (RibbonHost.Content is UtilityRibbon ribbon) ribbon.TemporaryPopup.IsOpen = false;
        generationDialog = new(documents);
        try
        {
            var result = await generationDialog.ShowDialog<SyntheticGenerationResult?>(DialogOwner());
            if (result is not null) await ActivateGeneratedResultAsync(result);
        }
        finally { generationDialog = null; }
    }
    internal async Task ActivateGeneratedResultAsync(SyntheticGenerationResult result)
    {
        if (!documents.Documents.Contains(result.Document)) return;
        factory.OpenDocument(result.Document.Id);
        Activate();
        Status.Text = $"Generated {result.MessageIds.Count} synthetic {result.Options.TypeLabel} message(s). Use Save As to keep new output.";
        // Dock can mount a new tab after modal dismissal and the first dispatcher
        // layout pass. Wait for the actual host, rather than guessing a delay.
        await WaitForGeneratedViewAsync(result.Document, result.MessageIds[0]);
        await FocusInsertedMessageAsync(result.Document, result.MessageIds[0]);
    }
    private async Task WaitForGeneratedViewAsync(MessageDocument document, MessageId id)
    {
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Check()
        {
            if (!IsVisible || !ReferenceEquals(documents.ActiveDocument, document) || document.ActiveMessageId != id ||
                this.GetVisualDescendants().OfType<FileDocumentView>().Any(v => ReferenceEquals(v.Document, document) && v.IsEffectivelyVisible)) ready.TrySetResult();
        }
        void LayoutReady(object? sender, EventArgs e) => Check();
        void DocumentChanged(object? sender, PropertyChangedEventArgs e) => Check();
        void OwnerClosed(object? sender, EventArgs e) => ready.TrySetResult();
        LayoutUpdated += LayoutReady; documents.PropertyChanged += DocumentChanged; Closed += OwnerClosed;
        try { Check(); await ready.Task; }
        finally { LayoutUpdated -= LayoutReady; documents.PropertyChanged -= DocumentChanged; Closed -= OwnerClosed; }
    }
}
