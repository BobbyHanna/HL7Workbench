using System.Diagnostics;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed record SyntheticGenerationProgress(string Phase, long Completed, long Total);
public sealed record SyntheticGenerationResult(MessageDocument Document, IReadOnlyList<MessageId> MessageIds,
    SyntheticGenerationOptions Options, bool IsNewDocument, TimeSpan GenerationTime, TimeSpan PublicationTime);

/// <summary>Generates off-context, validates a whole prospective document and publishes one batch on the caller's owning context.</summary>
public sealed class SyntheticGeneration(DocumentManager documents)
{
    public async Task<SyntheticGenerationResult> GenerateAsync(SyntheticGenerationOptions options, MessageDocument? target = null,
        MessageInsertionPosition position = MessageInsertionPosition.End, IProgress<SyntheticGenerationProgress>? progress = null, CancellationToken token = default)
    {
        options = options.Normalize(); token.ThrowIfCancellationRequested();
        if (!Enum.IsDefined(position)) throw new ArgumentOutOfRangeException(nameof(position));
        var revision = target?.ContentVersion;
        void CheckTarget()
        {
            if (target is null) return;
            if (!documents.Documents.Contains(target) || target.ContentVersion != revision)
                throw new InvalidOperationException("The target document or selection changed during generation. Review it and generate again.");
            if (target.InsertionUnavailableReason is { } reason) throw new InvalidOperationException(reason);
            if (options.Count > MessageFileIndex.MaxIndexedMessages - target.MessageCount) throw new Hl7LimitException("IndexedMessages");
            if (target.ActiveMessageId is null && position is MessageInsertionPosition.BeforeCurrent or MessageInsertionPosition.AfterCurrent)
                throw new InvalidOperationException("Select a current message, or insert at Beginning/End.");
        }
        CheckTarget();
        var watch = Stopwatch.StartNew();
        var batch = await Task.Run(() => SyntheticMessageGenerator.Generate(options, token,
            new InlineProgress<int>(count =>
            {
                // Bound dispatcher notifications without changing generation or cancellation checks.
                if (count == 1 || count == options.Count || count % 25 == 0) progress?.Report(new("Generating", count, options.Count));
            })), token);
        var generationTime = watch.Elapsed; watch.Restart(); token.ThrowIfCancellationRequested(); CheckTarget();
        MessageDocument? owned = null;
        try
        {
            if (target is null)
            {
                var snapshot = await Task.Run(async () =>
                {
                    using var empty = new MemoryStream(); return await SourceSnapshot.CaptureAsync(empty, token);
                }, token);
                try { owned = new(Guid.NewGuid().ToString("N"), options.SuggestedFileName, snapshot, MessageFileIndex.Build(snapshot, token)); }
                catch { snapshot.Dispose(); throw; }
            }
            var destination = target ?? owned!;
            var ids = await destination.InsertGeneratedMessagesAsync(batch, position,
                new InlineProgress<DocumentSaveProgress>(p => progress?.Report(new(p.Phase, p.CompletedBytes, p.TotalBytes))), token);
            if (owned is not null)
            {
                await owned.EditRefresh; token.ThrowIfCancellationRequested();
                owned.MarkGeneratedUnsaved(); documents.Open(owned); owned = null;
            }
            destination.IsEditMode = true;
            return new(destination, ids, options, target is null, generationTime, watch.Elapsed);
        }
        finally { if (owned is not null) { owned.Dispose(); await owned.Disposal; } }
    }
    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T> { public void Report(T value) => report(value); }
}
