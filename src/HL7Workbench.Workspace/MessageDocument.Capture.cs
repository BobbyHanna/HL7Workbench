namespace HL7Workbench.Workspace;

public sealed partial class MessageDocument
{
    // Captures copy metadata and immutable bounded string references on the document's
    // owning context. Background readers never enumerate the live sequence or buffers.
    internal readonly record struct CapturedMessage(LogicalMessage Entry, int Position, string? Text);
    private CapturedMessage[] CaptureLogicalMessages() => sequence.Entries.Select((entry, index) =>
        new CapturedMessage(entry, index + 1, buffers.GetValueOrDefault(entry.Id)?.Text ??
            (entry.Origin == MessageOrigin.Memory ? initialComparisonText : null))).ToArray();

    internal LogicalMessage? FindMessage(MessageId id) => sequence.Position(id) is > 0 and var position ? sequence[position - 1] : null;
    internal MessageId OriginalMessageId(int sourceOrdinal) => originalIds[sourceOrdinal - 1];
}
