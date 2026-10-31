using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Owns one immutable complete capture and bounded preview. Disposal cancels and drains active publication before cleanup.</summary>
public sealed class BulkFieldResults : IDisposable
{
    private readonly MessageDocument document;
    private readonly long revision;
    private readonly IReadOnlyList<MessageId>? filter;
    private readonly MessageDocument.ScriptInputCapture captured;
    private readonly BulkFieldOptions options;
    private readonly IReadOnlyList<BulkUniqueValue> values;
    private readonly IReadOnlyList<BulkFieldChange> changes;
    private readonly Dictionary<int, byte[]> overlays;
    private readonly CancellationToken sourceLifetime, sourceChanges;
    private readonly CancellationTokenSource closing = new();
    private readonly object gate = new();
    private readonly TaskCompletionSource drained = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int readers;
    private bool disposed, published, publishing;
    public Task Disposal => drained.Task;
    public BulkFieldRequest Request { get; }
    public BulkFieldSummary Summary { get; }
    public string SourceTitle => document.DisplayTitle;
    public long Revision => revision;
    public bool IsCurrent => !disposed && document.IsSearchCurrent(revision) &&
        (filter is null || ReferenceEquals(filter, document.NavigationMatchIds));
    public bool CanApply => IsCurrent && !published && !publishing && Request.Operation != BulkFieldOperation.UniqueValues && Summary.Invalid == 0 && Summary.Replacements > 0;
    public int RowCount => Request.Operation == BulkFieldOperation.UniqueValues ? values.Count : changes.Count;
    public int PageCount => Math.Max(1, (RowCount + BulkFieldOptions.PageSize - 1) / BulkFieldOptions.PageSize);
    internal BulkFieldResults(MessageDocument document, long revision, IReadOnlyList<MessageId>? filter, BulkFieldRequest request,
        MessageDocument.ScriptInputCapture captured, BulkFieldOptions options, BulkUniqueValue[] values, BulkFieldChange[] changes,
        Dictionary<int, byte[]> overlays, BulkFieldSummary summary, CancellationToken sourceLifetime, CancellationToken sourceChanges)
    {
        this.document = document; this.revision = revision; this.filter = filter; Request = request; this.captured = captured;
        this.options = options; this.values = Array.AsReadOnly(values); this.changes = Array.AsReadOnly(changes); this.overlays = overlays; Summary = summary;
        this.sourceLifetime = sourceLifetime; this.sourceChanges = sourceChanges;
    }
    public IReadOnlyList<BulkUniqueValue> UniquePage(int page, BulkFieldSort sort = BulkFieldSort.Value, bool descending = false)
    {
        CheckPage(page); if (!Enum.IsDefined(sort)) throw new ArgumentException("Choose value or frequency sort.");
        var ordered = sort == BulkFieldSort.Frequency ? values.OrderBy(v => v.Occurrences).ThenBy(v => v.Raw, StringComparer.Ordinal).ThenBy(v => v.State) :
            values.OrderBy(v => v.Raw, StringComparer.Ordinal).ThenBy(v => v.State);
        return (descending ? ordered.Reverse() : ordered).Skip(page * BulkFieldOptions.PageSize).Take(BulkFieldOptions.PageSize).ToArray();
    }
    public IReadOnlyList<BulkFieldChange> ChangePage(int page)
    { CheckPage(page); return changes.Skip(page * BulkFieldOptions.PageSize).Take(BulkFieldOptions.PageSize).ToArray(); }
    private void CheckPage(int page)
    { ObjectDisposedException.ThrowIf(disposed, this); if (page < 0 || page >= PageCount) throw new ArgumentOutOfRangeException(nameof(page)); }
    public void FilterNavigator(BulkUniqueValue value)
    {
        if (!IsCurrent || !values.Contains(value)) throw new InvalidOperationException("Analysis is stale or the selected value is unavailable.");
        var matches = value.Messages.Select(m => new MessageSearchMatch(document.GetLogicalPosition(m.Key), "", "", Request.Path.ToString(), m.Value)
            { MessageId = m.Key }).OrderBy(m => m.MessageNumber).ToList();
        document.ApplySearchFilter(new(document, revision, matches, Summary.Skipped));
    }
    public async Task NavigateAsync(BulkUniqueValue value, CancellationToken token = default)
    {
        if (!IsCurrent || !values.Contains(value)) throw new InvalidOperationException("Analysis is stale or the selected value is unavailable.");
        var first = value.Messages.Keys.OrderBy(document.GetLogicalPosition).First();
        await document.NavigateAsync(first, token);
    }

    /// <summary>Explicit confirmation publishes one independent full dataset. The optional owning-context
    /// presenter must complete successfully before publication commits; it rolls back its own presentation on failure.</summary>
    public async Task<MessageDocument> ApplyAsync(DocumentManager documents, bool confirmed,
        IProgress<BulkFieldProgress>? progress = null, CancellationToken token = default, Action<MessageDocument>? presentResult = null)
    {
        lock (gate)
        {
            if (!confirmed || !CanApply || !documents.Documents.Contains(document)) throw new InvalidOperationException("Review a current valid preview and confirm all proposals before Apply.");
            publishing = true; readers++;
        }
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, closing.Token, sourceLifetime, sourceChanges);
        SourceSnapshot? output = null; MessageDocument? pending = null;
        try
        {
            var prepared = await Task.Run(async () =>
            {
                var index = captured.Index; var plan = new MessageSavePlan(captured.Snapshot, index.Encoding, true);
                long cursor = 0; var gaps = index.Regions.Where(r => r.MessageNumber is null).ToArray(); var gapIndex = 0;
                for (var n = 0; n < index.Messages.Count; n++)
                {
                    linked.Token.ThrowIfCancellationRequested(); var original = index.Messages[n];
                    GapsThrough(original.SourceRange.Offset);
                    plan.Original(new(captured.Mapping.Messages[n].MessageId, MessageOrigin.File, n + 1), original, null, overlays.GetValueOrDefault(n));
                    cursor = original.SourceRange.End;
                }
                GapsThrough(captured.Snapshot.Length);
                void GapsThrough(long end)
                {
                    while (gapIndex < gaps.Length && gaps[gapIndex].Range.Offset < end)
                    {
                        var gap = gaps[gapIndex++];
                        if (gap.Range.Offset != cursor || gap.Range.End > end) throw new InvalidOperationException("Captured source partition is inconsistent.");
                        plan.Gap(gap.Range, gap.Reason); cursor = gap.Range.End;
                    }
                    if (cursor != end) throw new InvalidOperationException("Captured source partition is incomplete.");
                }
                progress?.Report(new("Writing independent result", 0, 1));
                SourceSnapshot? candidate = null;
                try
                {
                    candidate = await SourceSnapshot.CreateAsync((stream, ct) => plan.WriteAsync(stream, null, ct), options.MaximumOutputBytes, linked.Token, options.StorageRoot);
                    var candidateIndex = MessageFileIndex.Build(candidate, linked.Token, inheritedEncoding: index.Encoding);
                    plan.ValidateSnapshot(candidate, candidateIndex, new Progress<DocumentSaveProgress>(), linked.Token);
                    var result = (candidate, candidateIndex); candidate = null; return result;
                }
                finally { candidate?.Dispose(); }
            });
            output = prepared.candidate;
            if (!IsCurrent) throw new InvalidOperationException("Source or filter changed. Analyze again.");
            pending = new(Guid.NewGuid().ToString("N"), "Bulk Result.hl7", output, prepared.candidateIndex); output = null;
            pending.MarkGeneratedUnsaved();
            var first = changes.First(c => c.Status == "Proposed");
            var ordinal = captured.Mapping.Messages.Single(m => m.MessageId == first.MessageId).JobOrdinal;
            await pending.NavigateAsync(ordinal, linked.Token);
            SelectResultPath(pending, first.Path);
            // The sole publication point is on the owning context, after all byte/index
            // proofs and freshness/cancellation checks. Opening cannot retarget the source.
            linked.Token.ThrowIfCancellationRequested();
            if (!IsCurrent || !documents.Documents.Contains(document)) throw new InvalidOperationException("Source changed or closed before publication.");
            var previous = documents.ActiveDocument;
            try
            {
                documents.Open(pending);
                presentResult?.Invoke(pending);
                if (!documents.Documents.Contains(pending) || !ReferenceEquals(documents.ActiveDocument, pending))
                    throw new InvalidOperationException("Result was not activated.");
            }
            catch (Exception error)
            {
                // Registration can notify observers before throwing. Withdraw that
                // exact owned document and restore context, never leave a disposed ghost.
                if (documents.Documents.Contains(pending)) documents.Close(pending.Id, discardChanges: true);
                if (previous is not null && documents.Documents.Contains(previous)) documents.Activate(previous.Id);
                if (error is InvalidOperationException or IOException or UnauthorizedAccessException or ArgumentException)
                    throw new BulkFieldPublicationException();
                throw;
            }
            published = true; var result = pending; pending = null;
            return result;
        }
        finally
        {
            output?.Dispose(); if (pending is not null) { pending.Dispose(); await pending.Disposal; }
            lock (gate) { publishing = false; readers--; Cleanup(); }
        }
    }
    private static void SelectResultPath(MessageDocument result, Hl7Path path)
    {
        var inspection = result.ActiveInspection ?? throw new InvalidOperationException("Result inspection is unavailable.");
        var segment = inspection.Segments.Single(s => s.Identifier == path.Segment && s.Occurrence == path.Occurrence);
        inspection.SelectSegment(segment); var node = inspection.InspectField(path.Field)!;
        foreach (var index in new[] { path.Repetition, path.Component, path.Subcomponent })
        { if (index is null) break; node = node.Children[index.Value - 1]; }
        inspection.SelectNode(node, revealCurrent: true);
    }
    private void Cleanup()
    { if (disposed && readers == 0) { captured.Dispose(); overlays.Clear(); closing.Dispose(); drained.TrySetResult(); } }
    public void Dispose()
    { lock (gate) { if (disposed) return; disposed = true; closing.Cancel(); Cleanup(); } }
}
