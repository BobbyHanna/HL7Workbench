using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed partial class MessageDocument
{
    private readonly LogicalMessageSequence sequence;
    private readonly MessageId[] originalIds;
    private readonly byte[]? memoryOriginalBytes;
    private MessageId? activeId;
    private MessageId[] savedOrder;
    private bool structuralDirty;
    private int dirtyBuffers;
    private long bufferCharacters, historyCharacters, redoCharacters, detachedCharacters;
    private readonly List<HistoryOperation> undo = [];
    private readonly List<HistoryOperation> redo = [];
    private abstract record HistoryOperation(long Characters);
    private sealed record TextOperation(MessageId Id, EditableMessageBuffer.Change Change)
        : HistoryOperation((long)Change.Removed.Length + Change.Inserted.Length);
    private sealed record InsertOperation(int Index, LogicalMessage[] Entries) : HistoryOperation(0);

    /// <summary>Current logical order. Entries contain no message text or mutable source offsets.</summary>
    public IReadOnlyList<LogicalMessage> Messages => sequence.Entries;
    public MessageId? ActiveMessageId => activeId;
    /// <summary>True while live membership differs from the initial document, even after saving.</summary>
    public bool HasStructuralChanges => sequence.Count != originalIds.Length;
    public LogicalMessage? ActiveEntry => activeId is { } id && sequence.Position(id) is > 0 and var position ? sequence[position - 1] : null;
    public IndexedMessage? ActiveOriginalMessage => ActiveEntry is { Origin: MessageOrigin.File, SourceOrdinal: { } ordinal } ? Index!.Messages[ordinal - 1] : null;
    public bool HasOriginalMessage => ActiveEntry is { Origin: not MessageOrigin.Inserted };
    public MessageId GetMessageId(int logicalPosition) => logicalPosition >= 1 && logicalPosition <= MessageCount ?
        sequence[logicalPosition - 1].Id : throw new ArgumentOutOfRangeException(nameof(logicalPosition));
    /// <summary>One-based live logical position, or zero for a foreign or undone identity.</summary>
    public int GetLogicalPosition(MessageId id) => sequence.Position(id);
    public Task NavigateAsync(MessageId id, CancellationToken token = default) => NavigateAsync(
        sequence.Position(id) is > 0 and var position ? position : throw new ArgumentException("Message is not in this document.", nameof(id)), token);

    /// <summary>Capability feedback for a blank insertion; reads metadata and bounded draft budgets, never source bytes.</summary>
    public string? InsertionUnavailableReason => !CanMutateHistory || IsLoading ? "Wait for document preparation, saving or script capture." :
        MessageCount >= MessageFileIndex.MaxIndexedMessages ? "The document already contains the maximum 100,000 messages." :
        undo.Count >= MaximumHistoryEntries || bufferCharacters - detachedCharacters + historyCharacters - redoCharacters > MaximumRetainedEditCharacters ?
            "The document edit/history budget is full. Existing work is retained." :
        !(searchEncoding.IsSingleByte || searchEncoding.CodePage is 65001 or 1200 or 1201) ?
            "This encoding supports exact copying but not inserted payloads." :
        !SupportsStructuralSourceBoundary ? "Insertion is unavailable for batch containers, provisional messages or ambiguous framing. Original bytes are retained." :
        originalIds.Length == 0 && Snapshot is { } snapshot && snapshot.Length != Index!.BomLength ?
            "Insertion into non-message source content is unsupported. Original bytes are retained." : null;

    /// <summary>Whether the selected logical EOF is an inserted placeholder containing only whitespace/segment terminators.
    /// Uses the current retained draft even while parsing is pending or Edit Mode is off.</summary>
    public bool IsEmptyTrailingInsertion => ActiveEntry is { Origin: MessageOrigin.Inserted } &&
        MessageNumber == MessageCount && string.IsNullOrWhiteSpace(WorkingText);

    /// <summary>Inserts one bounded draft and selects it. No source bytes are changed; blank drafts may not yet be saveable.</summary>
    public MessageId InsertMessage(MessageInsertionPosition position, string text = "") => InsertMessages(position, [text])[0];

    /// <summary>Atomically inserts one ordered batch as one history operation. No generator or UI behavior is implied.</summary>
    public IReadOnlyList<MessageId> InsertMessages(MessageInsertionPosition position, IReadOnlyList<string> texts)
        => PublishInsertion(PrepareInsertion(position, texts));

    private sealed record PreparedInsertion(int Index, LogicalMessage[] Entries, EditableMessageBuffer[] Buffers, long Characters);
    private PreparedInsertion PrepareInsertion(MessageInsertionPosition position, IReadOnlyList<string> texts, CancellationToken token = default, bool generatedContent = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!Enum.IsDefined(position)) throw new ArgumentOutOfRangeException(nameof(position));
        ArgumentNullException.ThrowIfNull(texts);
        if (!CanMutateHistory || IsLoading) throw new InvalidOperationException("Wait for document preparation, saving or script capture.");
        if (texts.Count == 0) throw new ArgumentException("Insert at least one message.", nameof(texts));
        if (texts.Count > MessageFileIndex.MaxIndexedMessages - MessageCount) throw new Hl7LimitException("IndexedMessages");
        var requested = texts.ToArray();
        if (requested.Length != texts.Count) throw new ArgumentException("The insertion batch changed.", nameof(texts));
        if (!(searchEncoding.IsSingleByte || searchEncoding.CodePage is 65001 or 1200 or 1201))
            throw new InvalidOperationException("This encoding supports exact copying but not inserted payloads.");
        ValidateStructuralSourceBoundary();
        // Zero classified messages does not imply an empty file: preserve opaque input as source, not a new document.
        if (originalIds.Length == 0 && Snapshot is { } snapshot && snapshot.Length != Index!.BomLength)
            throw new InvalidOperationException("Insertion into non-message source content is unsupported. Original bytes are retained.");
        var index = position switch
        {
            MessageInsertionPosition.Beginning => 0,
            MessageInsertionPosition.End => MessageCount,
            _ when MessageCount == 0 => 0,
            MessageInsertionPosition.BeforeCurrent => MessageNumber - 1,
            _ => MessageNumber
        };
        if (index < 0) throw new InvalidOperationException("Select a message first.");
        // Check the entire request before allocating retained buffers or mutating the redo branch.
        long characters = 0;
        for (var i = 0; i < requested.Length; i++)
        {
            token.ThrowIfCancellationRequested();
            var text = requested[i] ?? throw new ArgumentException("A draft cannot be null.", nameof(texts));
            // Only the closed-format Core generator can supply prevalidated content.
            // Arbitrary UI/API drafts retain their existing scanner validation.
            if (!generatedContent) ValidateInsertedText(text);
            characters = checked(characters + 2L * text.Length);
            if (characters > MaximumRetainedEditCharacters) throw new Hl7LimitException("RetainedEditHistory");
        }
        CheckHistoryBudget(characters, 0);
        var entries = new LogicalMessage[requested.Length];
        var prepared = new EditableMessageBuffer[requested.Length];
        for (var i = 0; i < requested.Length; i++)
        {
            entries[i] = new(MessageId.Create(), MessageOrigin.Inserted, null);
            prepared[i] = new(requested[i], searchEncoding);
        }
        return new(index, entries, prepared, characters);
    }
    private IReadOnlyList<MessageId> PublishInsertion(PreparedInsertion insertion)
    {
        var (index, entries, prepared, characters) = insertion;
        CheckHistoryBudget(characters, 0);
        buffers.EnsureCapacity(buffers.Count + entries.Length); undo.EnsureCapacity(undo.Count + 1);
        sequence.Insert(index, entries);
        DropRedo();
        for (var i = 0; i < entries.Length; i++) buffers.Add(entries[i].Id, prepared[i]);
        bufferCharacters += characters;
        undo.Add(new InsertOperation(index, entries));
        StructuralChanged(entries[0].Id);
        return Array.AsReadOnly(entries.Select(e => e.Id).ToArray());
    }
    private static void ValidateInsertedText(string text)
    {
        if (text.Length > MessagePresentation.MaximumEditorCharacters) throw new Hl7LimitException("EditableMessageCharacters");
        if (text.Length == 0) return;
        var structure = Hl7StructureScanner.Scan(new StringHl7TextSource(text), Encoding.UTF8);
        if (structure.Diagnostics.Any(d => d.Code == "MultipleMessages"))
            throw new InvalidOperationException("One insertion draft cannot contain multiple MSH messages. Use batch insertion.");
    }
    private bool CanMutateHistory => !disposed && !IsSaving && !IsCapturingScript && !IsPreparingInsertion;
    private void CheckHistoryBudget(long addedBufferCharacters, long addedHistoryCharacters)
    {
        if (bufferCharacters - detachedCharacters + historyCharacters - redoCharacters + addedBufferCharacters + addedHistoryCharacters > MaximumRetainedEditCharacters ||
            undo.Count + 1 > MaximumHistoryEntries) throw new Hl7LimitException("RetainedEditHistory");
    }
    private void DropRedo()
    {
        foreach (var operation in redo.OfType<InsertOperation>())
            foreach (var entry in operation.Entries)
                if (sequence.Position(entry.Id) == 0 && buffers.Remove(entry.Id, out var buffer))
                    bufferCharacters -= buffer.RetainedCharacters;
        historyCharacters -= redoCharacters; redoCharacters = 0; detachedCharacters = 0; redo.Clear();
    }
    private void StructuralChanged(MessageId? selected)
    {
        structuralDirty = !sequence.Matches(savedOrder);
        dirtyBuffers = buffers.Count(p => sequence.Position(p.Key) > 0 && p.Value.IsDirty);
        SearchContentChanged();
        SelectForHistory(selected);
        PropertyChanged?.Invoke(this, new(nameof(MessageCount)));
        PropertyChanged?.Invoke(this, new(nameof(HasStructuralChanges)));
        PropertyChanged?.Invoke(this, new(nameof(DisplayTitle)));
    }
    private void SelectForHistory(MessageId? selected)
    {
        request?.Cancel(); editRefresh?.Cancel();
        var same = activeId == selected;
        activeId = selected; IsLoading = false; Failure = null; EditError = null;
        if (!same || selected is null || !buffers.ContainsKey(selected.Value))
        { message = null; inspection = null; previousInspection = null; Structure = null; Source = null; Presentation = null; }
        if (selected is { } id && buffers.ContainsKey(id)) WorkingChanged();
        else
        {
            generation++; IsRefreshingEdit = false; ActiveDiagnostics = []; Changed();
            EditRefresh = selected is null ? Task.CompletedTask : NavigateAsync(selected.Value);
        }
    }
    private void ApplyHistory(bool forward)
    {
        if (!CanMutateHistory) return;
        var from = forward ? redo : undo; var to = forward ? undo : redo;
        if (from.Count == 0) return;
        to.EnsureCapacity(to.Count + 1);
        var operation = from[^1];
        from.RemoveAt(from.Count - 1); to.Add(operation);
        redoCharacters += forward ? -operation.Characters : operation.Characters;
        if (operation is TextOperation text)
        {
            var buffer = buffers[text.Id]; var wasDirty = buffer.IsDirty;
            buffer.Apply(text.Change, forward);
            dirtyBuffers += (buffer.IsDirty ? 1 : 0) - (wasDirty ? 1 : 0);
            SelectForHistory(text.Id);
        }
        else if (operation is InsertOperation insertion)
        {
            var retained = insertion.Entries.Sum(e => buffers[e.Id].RetainedCharacters);
            if (forward) { sequence.Insert(insertion.Index, insertion.Entries); detachedCharacters -= retained; }
            else { sequence.Remove(insertion.Index, insertion.Entries.Length); detachedCharacters += retained; }
            var selected = forward ? insertion.Entries[0].Id :
                sequence.Count == 0 ? (MessageId?)null : sequence[Math.Max(0, insertion.Index - 1)].Id;
            StructuralChanged(selected);
        }
    }
}
