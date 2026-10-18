using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed partial class MessageDocument
{
    private FileSaveDestination? associatedDestination;
    private long saveGeneration;
    /// <summary>Transient progress for the mutation-locked save; never source contents or paths.</summary>
    public DocumentSaveProgress? SaveProgress { get; private set; }
    /// <summary>Measured phases of the last successful publication. Not persisted or logged.</summary>
    public DocumentSaveMetrics? LastSaveMetrics { get; private set; }
    private bool? supportsStructuralSourceBoundary;
    // The original index is immutable. Cache this metadata-only check so command
    // enablement never traverses a large index on each editor notification.
    private bool SupportsStructuralSourceBoundary => supportsStructuralSourceBoundary ??= Index is not { } index ||
        !(index.Messages.Any(m => m.Diagnostics.Any(d => d.Code is "IncompleteMllp" or "AmbiguousFraming" or "InteriorBom")) ||
          index.Regions.Any(r => r.Reason.StartsWith("HL7 batch wrapper", StringComparison.Ordinal) ||
              r.Reason is "Provisional HL7 message" or "Unexpected MLLP ending"));
    private void ValidateStructuralSourceBoundary()
    {
        if (!SupportsStructuralSourceBoundary)
            throw new InvalidOperationException("Structural insertion in batch containers, provisional messages or ambiguous framing is unsupported. Original bytes and existing drafts are retained.");
    }
    private MessageSavePlan BuildSavePlan(CancellationToken token, IReadOnlyList<LogicalMessage>? capturedOrder = null,
        MessageId? currentScriptTarget = null)
    {
        var plan = new MessageSavePlan(Snapshot, searchEncoding, Index is not null || HasStructuralChanges);
        if (HasStructuralChanges) ValidateStructuralSourceBoundary();
        var pending = new List<LogicalMessage>();
        var regions = Index?.Regions.Where(r => r.MessageNumber is null).ToArray() ?? [];
        var gapIndex = 0; long cursor = 0;
        var memoryBom = memoryOriginalBytes is null ? 0 : Hl7SourceEncoding.Detect(memoryOriginalBytes, searchEncoding).BomLength;
        if (memoryOriginalBytes is { } memory) plan.MemoryBom(memory, memoryBom);
        bool? precedingFramed = null;
        void Validate(LogicalMessage entry, Action action)
        {
            try { action(); }
            catch (Exception error) when (capturedOrder is not null && error is Hl7LimitException or System.Text.EncoderFallbackException or InvalidOperationException)
            { throw new CaptureMessageException(entry.Id, error); }
        }
        void GapsThrough(long end)
        {
            while (gapIndex < regions.Length && regions[gapIndex].Range.Offset < end)
            {
                var gap = regions[gapIndex++];
                if (gap.Range.Offset != cursor || gap.Range.End > end) throw new InvalidOperationException("Source partition is inconsistent.");
                plan.Gap(gap.Range, gap.Reason); cursor = gap.Range.End;
            }
            if (cursor != end) throw new InvalidOperationException("Source partition is incomplete.");
        }
        void InsertPending(bool framed)
        {
            foreach (var entry in pending)
            {
                token.ThrowIfCancellationRequested(); var buffer = buffers[entry.Id];
                Validate(entry, () =>
                {
                    var parsed = buffer.Parse();
                    if (buffer.Text.Length == 0 || !buffer.Text.StartsWith("MSH", StringComparison.Ordinal))
                        throw new InvalidOperationException($"Save refused: message {GetLogicalPosition(entry.Id)} is an inserted draft that needs a recoverable MSH start. Empty drafts cannot be omitted. Complete the draft or undo insertion.");
                    ValidatePayloadBoundary(buffer, parsed);
                    plan.Inserted(entry, buffer, framed);
                });
            }
            pending.Clear();
        }
        foreach (var entry in capturedOrder ?? sequence.Entries)
        {
            token.ThrowIfCancellationRequested();
            if (entry.Origin == MessageOrigin.Inserted) { pending.Add(entry); continue; }
            var buffer = currentScriptTarget is null || currentScriptTarget == entry.Id ? buffers.GetValueOrDefault(entry.Id) : null;
            if (buffer is not null) Validate(entry, () => ValidateSaveBoundary(entry.SourceOrdinal ?? 1, buffer, buffer.Parse()));
            if (entry.Origin == MessageOrigin.File)
            {
                var original = Index!.Messages[entry.SourceOrdinal!.Value - 1];
                GapsThrough(original.SourceRange.Offset);
                InsertPending(original.Framed);
                plan.Original(entry, original, buffer);
                cursor = original.SourceRange.End; precedingFramed = original.Framed;
            }
            else
            {
                InsertPending(false);
                plan.MemoryOriginal(entry, memoryOriginalBytes!, memoryBom, buffer);
            }
        }
        if (originalIds.Length == 0 && Snapshot is { } emptySnapshot) GapsThrough(emptySnapshot.Length);
        InsertPending(precedingFramed ?? false);
        if (Snapshot is { } snapshot) GapsThrough(snapshot.Length);
        return plan;
    }
    private static void ValidatePayloadBoundary(EditableMessageBuffer buffer, Hl7Message parsed)
    {
        if (buffer.Text.IndexOfAny(['\v', '\x1c', '\uFEFF']) >= 0 ||
            parsed.Segments.Any(s => s.Raw.Length >= 3 && s.Raw[..3] is "FHS" or "BHS" or "BTS" or "FTS") ||
            parsed.Segments.Count(s => s.Raw.StartsWith("MSH", StringComparison.Ordinal)) > 1)
            throw new InvalidOperationException("Save refused: payload contains transport controls, wrappers or multiple message starts. Draft retained.");
    }
    internal void SetInitialSaveDestination(FileSaveDestination? destination)
    { associatedDestination = destination; SavePath = destination?.Path; }
}
