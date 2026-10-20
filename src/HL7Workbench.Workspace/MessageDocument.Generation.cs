using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed partial class MessageDocument
{
    /// <summary>Transient mutation lock while a prospective generated batch is streamed and validated; no history/selection is published yet.</summary>
    public bool IsPreparingInsertion { get; private set; }
    internal async Task<IReadOnlyList<MessageId>> InsertGeneratedMessagesAsync(SyntheticMessageBatch batch,
        MessageInsertionPosition position, IProgress<DocumentSaveProgress>? progress, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var insertion = PrepareInsertion(position, batch.Messages, token, generatedContent: true);
        IsPreparingInsertion = true; PropertyChanged?.Invoke(this, new(nameof(IsPreparingInsertion)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            await work.WaitAsync(linked.Token);
            try
            {
                // Freeze mutations just as Save/capture do. The prospective order
                // contains only metadata; original bytes are streamed, never decoded.
                var order = sequence.Entries.ToList(); order.InsertRange(insertion.Index, insertion.Entries);
                var overlays = insertion.Entries.Select((e, i) => (e.Id, Buffer: insertion.Buffers[i])).ToDictionary(e => e.Id, e => e.Buffer);
                await Task.Run(async () =>
                {
                    await scanConcurrency.WaitAsync(linked.Token);
                    MessageSavePlan plan;
                    try { plan = BuildSavePlan(linked.Token, order, insertionBuffers: overlays); }
                    finally { scanConcurrency.Release(); }
                    // A leased private staging directory uses the established cleanup
                    // ownership. No public destination or original snapshot is written.
                    using var stage = await SourceSnapshot.CreateAsync((_, _) => Task.CompletedTask, 0, linked.Token);
                    await using var candidate = new FileStream(Path.Combine(stage.DirectoryPath, "insertion.bin"), FileMode.CreateNew,
                        FileAccess.ReadWrite, FileShare.Read, 65536, FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                    await plan.WriteAsync(candidate, progress, linked.Token); await candidate.FlushAsync(linked.Token);
                    plan.Validate(candidate, progress ?? new Progress<DocumentSaveProgress>(), linked.Token);
                }, linked.Token);
                // The only publication point. Cancellation/validation failures above
                // leave membership, selection, text, revision, save points and Redo intact.
                linked.Token.ThrowIfCancellationRequested();
                return PublishInsertion(insertion);
            }
            finally { work.Release(); }
        }
        finally { IsPreparingInsertion = false; PropertyChanged?.Invoke(this, new(nameof(IsPreparingInsertion))); }
    }
}
