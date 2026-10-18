using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed partial class MessageDocument
{
    internal bool ComparisonHasUnsavedDraft(MessageId id) => generatedUnsaved || buffers.GetValueOrDefault(id)?.IsDirty == true;
    internal ComparisonCapture CaptureComparison(MessageId id, bool original)
    {
        lock (searchGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            var entry = FindMessage(id) ?? throw new ComparisonInputException("Selected message is unavailable. Undo may have removed it; redo or choose another source.");
            if (original && entry.Origin == MessageOrigin.Inserted)
                throw new ComparisonInputException("An inserted message has no Original Message during this open session, including after Save.");
            // Only this identity is captured. Its original ordinal is provenance, never a live position.
            var buffer = buffers.GetValueOrDefault(id);
            var text = original ? buffer?.OriginalText : buffer?.Text;
            if (entry.Origin == MessageOrigin.Memory) text ??= initialComparisonText;
            if (Snapshot is null && text is null) throw new InvalidOperationException("Readable comparison source unavailable.");
            if (++searchReaders == 1) searchDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return new(this, id, entry.SourceOrdinal, original, text, Snapshot, Index, searchEncoding, SearchRevision, lifetime.Token, searchChanges.Token, ReleaseSearch);
        }
    }
    internal sealed class ComparisonCapture(MessageDocument document, MessageId id, int? originalOrdinal, bool original, string? text,
        SourceSnapshot? snapshot, MessageFileIndex? index, Encoding encoding, long revision,
        CancellationToken lifetime, CancellationToken changes, Action release) : IDisposable
    {
        private Action? lease = release;
        public MessageDocument Document => document;
        public MessageId MessageId => id;
        public bool Original => original;
        public long Revision => revision;
        public CancellationToken Lifetime => lifetime;
        public CancellationToken Changes => changes;
        public Encoding Encoding => (Encoding)encoding.Clone();
        public IHl7TextSource Open(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (text is not null) return new StringHl7TextSource(text);
            var range = index!.Messages[originalOrdinal!.Value - 1].PayloadRange;
            var bom = Hl7SourceEncoding.Detect(snapshot!.ReadBytes(new(range.Offset, Math.Min(4, range.Length))), encoding).BomLength;
            return DecodedMessageSource.Create(snapshot, range, encoding, bom, token);
        }
        public void Dispose() => Interlocked.Exchange(ref lease, null)?.Invoke();
    }
}
