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
    /// <summary>Current logical positions for display compatibility. NavigationMatchIds supplies identity.</summary>
    public IReadOnlyList<int>? NavigationMatches { get; private set; }
    /// <summary>Stable identities permitted in the navigator, or null for the complete document. Session-only.</summary>
    public IReadOnlyList<MessageId>? NavigationMatchIds { get; private set; }
    private HashSet<MessageId>? navigationIds;
    public bool IsNavigationMatch(MessageId id) => GetLogicalPosition(id) > 0 && (navigationIds is null || navigationIds.Contains(id));
    internal void SearchContentChanged()
    {
        Interlocked.Increment(ref searchRevision);
        searchChanges.Cancel(); searchChanges.Dispose(); searchChanges = new();
        ClearSearchFilter(); PropertyChanged?.Invoke(this, new(nameof(SearchRevision)));
    }
    public void ClearSearchFilter()
    {
        if (NavigationMatchIds is null) return;
        NavigationMatches = null; NavigationMatchIds = null; navigationIds = null;
        PropertyChanged?.Invoke(this, new(nameof(NavigationMatches)));
        PropertyChanged?.Invoke(this, new(nameof(NavigationMatchIds)));
    }
    public void ApplySearchFilter(MessageSearchResults results)
    {
        if (!results.IsCurrent(this)) throw new InvalidOperationException("Search is no longer current. Run it again.");
        var ids = results.Matches.Select(m => m.MessageId).ToArray();
        if (ids.Any(id => GetLogicalPosition(id) == 0)) throw new InvalidOperationException("A matching message is no longer available. Run the search again.");
        NavigationMatchIds = Array.AsReadOnly(ids); navigationIds = ids.ToHashSet();
        NavigationMatches = Array.AsReadOnly(ids.Select(GetLogicalPosition).ToArray());
        PropertyChanged?.Invoke(this, new(nameof(NavigationMatches)));
        PropertyChanged?.Invoke(this, new(nameof(NavigationMatchIds)));
    }
    internal SearchCapture CaptureSearch()
    {
        lock (searchGate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            // Immutable strings reuse the bounded Workspace drafts without reserialization,
            // so even a malformed or temporarily unencodable unsaved draft is searchable.
            var entries = CaptureLogicalMessages();
            if (entries.Any(e => e.Entry.Origin == MessageOrigin.Memory && e.Text is null))
                throw new InvalidOperationException("Readable text is unavailable.");
            if (++searchReaders == 1) searchDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
            return new(this, Snapshot, Index, entries, searchEncoding, SearchRevision, lifetime.Token, searchChanges.Token, ReleaseSearch);
        }
    }
    private void ReleaseSearch()
    { lock (searchGate) { if (--searchReaders == 0) searchDrained!.TrySetResult(); } }
    private Task DrainSearchReaders() { lock (searchGate) return searchReaders == 0 ? Task.CompletedTask : searchDrained!.Task; }

    internal sealed class SearchCapture(MessageDocument document, SourceSnapshot? snapshot, MessageFileIndex? index,
        CapturedMessage[] entries, Encoding encoding, long revision, CancellationToken lifetime, CancellationToken changes, Action release) : IDisposable
    {
        private Action? releaseLease = release;
        public MessageDocument Document => document;
        public SourceSnapshot? Snapshot => snapshot;
        public MessageFileIndex? Index => index;
        public IReadOnlyList<CapturedMessage> Entries => entries;
        public long Revision => revision;
        public int Count => entries.Length;
        public Encoding Encoding => (Encoding)encoding.Clone();
        public CancellationToken Lifetime => lifetime;
        public CancellationToken Changes => changes;
        public void Dispose() => Interlocked.Exchange(ref releaseLease, null)?.Invoke();
    }
}
