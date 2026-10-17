using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed partial class MessageDocument
{
    internal bool ComparisonHasUnsavedDraft(int ordinal) => generatedUnsaved || buffers.GetValueOrDefault(ordinal)?.IsDirty == true;
    internal ComparisonCapture CaptureComparison(int ordinal, bool original)
    {
        lock (searchGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (ordinal < 1 || ordinal > MessageCount) throw new ArgumentOutOfRangeException(nameof(ordinal));
            // Capture only the chosen ordinal. Navigator filters and the active parsed model are irrelevant.
            var text = original ? buffers.GetValueOrDefault(ordinal)?.OriginalText : buffers.GetValueOrDefault(ordinal)?.Text;
            if (Snapshot is null) text ??= initialComparisonText;
            if (Snapshot is null && text is null) throw new InvalidOperationException("Readable comparison source unavailable.");
            if (++searchReaders == 1) searchDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return new(this, ordinal, original, text, Snapshot, Index, searchEncoding, SearchRevision, lifetime.Token, searchChanges.Token, ReleaseSearch);
        }
    }
    internal sealed class ComparisonCapture(MessageDocument document, int ordinal, bool original, string? text,
        SourceSnapshot? snapshot, MessageFileIndex? index, Encoding encoding, long revision,
        CancellationToken lifetime, CancellationToken changes, Action release) : IDisposable
    {
        private Action? lease = release;
        public MessageDocument Document => document;
        public int Ordinal => ordinal;
        public bool Original => original;
        public long Revision => revision;
        public CancellationToken Lifetime => lifetime;
        public CancellationToken Changes => changes;
        public Encoding Encoding => (Encoding)encoding.Clone();
        public IHl7TextSource Open(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (text is not null) return new StringHl7TextSource(text);
            var range = index!.Messages[ordinal - 1].PayloadRange;
            var bom = Hl7SourceEncoding.Detect(snapshot!.ReadBytes(new(range.Offset, Math.Min(4, range.Length))), encoding).BomLength;
            return DecodedMessageSource.Create(snapshot, range, encoding, bom, token);
        }
        public void Dispose() => Interlocked.Exchange(ref lease, null)?.Invoke();
    }
}
