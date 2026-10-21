using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed record FhirGenerationProgress(string Phase, long Completed, long Total);
public sealed record FhirGenerationResult(MessageDocument Document, IReadOnlyList<MessageId> MessageIds, FhirGenerationReport Report);

/// <summary>Maps a captured selection off-context and publishes only a complete new unsaved document through the existing insertion proof.</summary>
public sealed class FhirGenerationPublisher(DocumentManager documents)
{
    public async Task<FhirGenerationResult> PublishAsync(FhirGenerationPlan plan, IProgress<FhirGenerationProgress>? progress = null,
        CancellationToken token = default, Func<bool>? captureIsCurrent = null)
    {
        var batch = await Task.Run(() => plan.Generate(token, new Inline<int>(n =>
        { if (n == 1 || n % 25 == 0 || n == plan.Preview.Messages) progress?.Report(new("Mapping", n, plan.Preview.Messages)); })), token);
        token.ThrowIfCancellationRequested(); if (captureIsCurrent?.Invoke() == false) throw new OperationCanceledException(token);
        MessageDocument? owned = null;
        try
        {
            var snapshot = await Task.Run(async () => { using var empty = new MemoryStream(); return await SourceSnapshot.CaptureAsync(empty, token); }, token);
            try { owned = new(Guid.NewGuid().ToString("N"), "Synthetic FHIR HL7.hl7", snapshot, MessageFileIndex.Build(snapshot, token)); }
            catch { snapshot.Dispose(); throw; }
            var ids = await owned.InsertMappedFhirMessagesAsync(batch, new Inline<DocumentSaveProgress>(p => progress?.Report(new(p.Phase, p.CompletedBytes, p.TotalBytes))), token);
            await owned.EditRefresh; token.ThrowIfCancellationRequested();
            if (captureIsCurrent?.Invoke() == false) throw new OperationCanceledException(token);
            // Report association precedes the one public document-open point and contains no imported values.
            owned.GenerationReport = batch.Report; owned.MarkGeneratedUnsaved();
            var document = owned; documents.Open(document); owned = null; document.IsEditMode = true;
            return new(document, ids, batch.Report);
        }
        finally { if (owned is not null) { owned.Dispose(); await owned.Disposal; } }
    }
    private sealed class Inline<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
}
