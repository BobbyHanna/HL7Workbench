using System.Collections.ObjectModel;

namespace HL7Workbench.Workspace;

/// <summary>Opaque session identity. It is never a logical position, source ordinal, or persisted HL7 identifier.</summary>
public readonly record struct MessageId
{
    private static long next;
    internal static MessageId Create()
    {
        var value = Interlocked.Increment(ref next);
        if (value <= 0) throw new InvalidOperationException("Message identity space exhausted.");
        return new(value);
    }
    private MessageId(long value) => Value = value;
    public long Value { get; }
    public override string ToString() => $"MessageId({Value})";
}

public enum MessageOrigin { File, Memory, Inserted }
public enum MessageInsertionPosition { BeforeCurrent, AfterCurrent, Beginning, End }

/// <summary>Metadata only. SourceOrdinal belongs to the immutable original index, never the current navigator.</summary>
public readonly record struct LogicalMessage(MessageId Id, MessageOrigin Origin, int? SourceOrdinal);

/// <summary>Bounded rank/identity map. Only structural operations shift metadata; ordinary access is constant time.</summary>
internal sealed class LogicalMessageSequence
{
    private readonly List<LogicalMessage> entries;
    private readonly Dictionary<MessageId, int> positions;
    public ReadOnlyCollection<LogicalMessage> Entries { get; }
    public int Count => entries.Count;
    public LogicalMessage this[int index] => entries[index];
    public LogicalMessageSequence(int count, MessageOrigin origin)
    {
        if (count < 0 || count > MessageFileIndex.MaxIndexedMessages) throw new ArgumentOutOfRangeException(nameof(count));
        entries = new(count); positions = new(count);
        for (var i = 0; i < count; i++)
        {
            var entry = new LogicalMessage(MessageId.Create(), origin, origin == MessageOrigin.File ? i + 1 : null);
            positions.Add(entry.Id, i); entries.Add(entry);
        }
        Entries = entries.AsReadOnly();
    }
    public int Position(MessageId id) => positions.TryGetValue(id, out var index) ? index + 1 : 0;
    public void Insert(int index, LogicalMessage[] batch)
    {
        if (index < 0 || index > Count) throw new ArgumentOutOfRangeException(nameof(index));
        if (batch.Length > MessageFileIndex.MaxIndexedMessages - Count) throw new Core.Hl7LimitException("IndexedMessages");
        // Reserve before publication. A batch performs one range shift, not K full map rebuilds.
        entries.EnsureCapacity(Count + batch.Length); positions.EnsureCapacity(Count + batch.Length);
        entries.InsertRange(index, batch);
        Reindex(index);
    }
    public void Remove(int index, int count)
    {
        for (var i = index; i < index + count; i++) positions.Remove(entries[i].Id);
        entries.RemoveRange(index, count); Reindex(index);
    }
    private void Reindex(int from)
    { for (var i = from; i < Count; i++) positions[entries[i].Id] = i; }
    public MessageId[] Snapshot() => entries.Select(e => e.Id).ToArray();
    public bool Matches(MessageId[] saved)
    {
        if (saved.Length != Count) return false;
        for (var i = 0; i < Count; i++) if (saved[i] != entries[i].Id) return false;
        return true;
    }
}
