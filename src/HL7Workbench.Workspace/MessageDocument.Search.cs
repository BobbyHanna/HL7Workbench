using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed partial class MessageDocument
{
    private long searchRevision;
    private readonly Encoding searchEncoding;
    private readonly object searchGate = new();
    private int searchReaders;
    private TaskCompletionSource? searchDrained;
    private CancellationTokenSource searchChanges = new();
    /// <summary>Content-only revision: navigating, saving and changing inspection selection do not invalidate a search.</summary>
    public long SearchRevision => Interlocked.Read(ref searchRevision);
    internal bool IsSearchCurrent(long revision) => !disposed && SearchRevision == revision;
    /// <summary>Original ordinals permitted in the navigator, or null for the complete document. Session-only.</summary>
    public IReadOnlyList<int>? NavigationMatches { get; private set; }
    internal void SearchContentChanged()
    {
        Interlocked.Increment(ref searchRevision);
        searchChanges.Cancel(); searchChanges.Dispose(); searchChanges = new();
        ClearSearchFilter(); PropertyChanged?.Invoke(this, new(nameof(SearchRevision)));
    }
    public void ClearSearchFilter()
    { if (NavigationMatches is null) return; NavigationMatches = null; PropertyChanged?.Invoke(this, new(nameof(NavigationMatches))); }
    public void ApplySearchFilter(MessageSearchResults results)
    {
        if (!results.IsCurrent(this)) throw new InvalidOperationException("Search is no longer current. Run it again.");
        NavigationMatches = Array.AsReadOnly(results.Matches.Select(m => m.MessageNumber).ToArray());
        PropertyChanged?.Invoke(this, new(nameof(NavigationMatches)));
    }
    internal SearchCapture CaptureSearch()
    {
        lock (searchGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            // Immutable strings reuse the bounded Workspace drafts without reserialization,
            // so even a malformed or temporarily unencodable unsaved draft is searchable.
            var drafts = buffers.ToDictionary(p => p.Key, p => p.Value.Text);
            if (Index is null && !drafts.ContainsKey(1))
            {
                if (message?.HasDecodedText != true) throw new InvalidOperationException("Readable text is unavailable.");
                drafts[1] = message.RawText;
            }
            if (++searchReaders == 1) searchDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return new(this, Snapshot, Index, drafts, searchEncoding, SearchRevision, lifetime.Token, searchChanges.Token, ReleaseSearch);
        }
    }
    private void ReleaseSearch()
    { lock (searchGate) { if (--searchReaders == 0) searchDrained!.TrySetResult(); } }
    private Task DrainSearchReaders() { lock (searchGate) return searchReaders == 0 ? Task.CompletedTask : searchDrained!.Task; }

    internal sealed class SearchCapture(MessageDocument document, SourceSnapshot? snapshot, MessageFileIndex? index,
        IReadOnlyDictionary<int, string> drafts, Encoding encoding, long revision, CancellationToken lifetime, CancellationToken changes, Action release) : IDisposable
    {
        private Action? releaseLease = release;
        public MessageDocument Document => document;
        public SourceSnapshot? Snapshot => snapshot;
        public MessageFileIndex? Index => index;
        public IReadOnlyDictionary<int, string> Drafts => drafts;
        public long Revision => revision;
        public int Count => index?.Messages.Count ?? 1;
        public Encoding Encoding => (Encoding)encoding.Clone();
        public CancellationToken Lifetime => lifetime;
        public CancellationToken Changes => changes;
        public void Dispose() => Interlocked.Exchange(ref releaseLease, null)?.Invoke();
    }
}
