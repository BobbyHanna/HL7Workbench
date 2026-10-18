using HL7Workbench.Core;

using System.Text;

namespace HL7Workbench.Workspace;

/// <summary>A stable selected-message identity, independent of position, filtering and original provenance.</summary>
public sealed record ComparisonSource
{
    public MessageDocument? Document { get; }
    public MessageId? MessageId { get; }
    /// <summary>Current logical position, or zero when the selected message is unavailable.</summary>
    public int Ordinal => Document is { } document && MessageId is { } id ? document.GetLogicalPosition(id) : 0;
    public int? OriginalOrdinal { get; }
    public bool IsAvailable => PastedText is not null || Document is { } document && document.IsSearchCurrent(document.SearchRevision) && MessageId is { } id &&
        document.FindMessage(id) is { } entry && (!Original || entry.Origin != MessageOrigin.Inserted);
    public bool Original { get; }
    public string? PastedText { get; }
    public ComparisonSource(MessageDocument document, int ordinal, bool original = false)
        : this(document, document.GetMessageId(ordinal), original) { }
    public ComparisonSource(MessageDocument document, MessageId messageId, bool original = false)
    { Document = document; MessageId = messageId; Original = original; OriginalOrdinal = document.FindMessage(messageId)?.SourceOrdinal; }
    private ComparisonSource(string text) { PastedText = text; }
    public static ComparisonSource FromPastedText(string text)
    { ComparisonInput.ValidatePastedText(text); return new(text); }
    public string Label
    {
        get
        {
            if (PastedText is { } text) return $"Pasted Text · temporary · {text.Length:N0} characters";
            var position = Ordinal;
            if (position == 0) return $"{Document!.Title} · selected message unavailable";
            var kind = Original ? IsAvailable ? "Original Message" : "No original source for inserted message" :
                Document!.FindMessage(MessageId!.Value)?.Origin == MessageOrigin.Inserted ? "Inserted Message" :
                Document.ComparisonHasUnsavedDraft(MessageId.Value) ? "Unsaved draft" : "Open Message";
            var provenance = OriginalOrdinal is { } ordinal && ordinal != position ? $" · original ordinal {ordinal}" : "";
            return $"{Document!.Title} · message {position}{provenance} · {kind}";
        }
    }
    public override string ToString() => Label;
}
public sealed record ComparisonWindow(string Text, long Origin, long TotalLength, CharacterRange? Selection)
{
    public string Scope => Origin == 0 && Text.Length == TotalLength ? "Complete message" : $"Source window: UTF-16 {Origin:N0}–{Origin + Text.Length:N0} of {TotalLength:N0}";
}

/// <summary>Owns two selected-message sources and their document read leases. Disposal drains pending bounded reads
/// before releasing decoded spools or document snapshots. Nothing is persisted.</summary>
public sealed class MessageComparison : IDisposable
{
    public const int WindowCharacters = 64 * 1024;
    public const int MaximumCopyCharacters = 1024 * 1024;
    private readonly Capture left;
    private readonly Capture right;
    private readonly IHl7TextSource a;
    private readonly IHl7TextSource b;
    private readonly SemaphoreSlim reads = new(1, 1);
    private volatile bool disposed;
    public Task Disposal { get; private set; } = Task.CompletedTask;
    public Hl7Structure BeforeStructure { get; }
    public Hl7Structure AfterStructure { get; }
    public Hl7ComparisonResult Result { get; }
    public bool IsCurrent => !disposed && left.IsCurrent && right.IsCurrent;
    private MessageComparison(Capture left, Capture right,
        IHl7TextSource a, IHl7TextSource b, Hl7Structure sa, Hl7Structure sb, Hl7ComparisonResult result)
    { this.left = left; this.right = right; this.a = a; this.b = b; BeforeStructure = sa; AfterStructure = sb; Result = result; }

    public static Task<MessageComparison> CreateAsync(ComparisonSource before, ComparisonSource after,
        Hl7ComparisonOptions? options = null, CancellationToken token = default)
    {
        var left = new Capture(before);
        Capture right;
        try { right = new(after); }
        catch { left.Dispose(); throw; }
        // Delegate must execute even when already canceled, to release both leases.
        return Task.Run(() =>
        {
            IHl7TextSource? a = null; IHl7TextSource? b = null;
            try
            {
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, left.Lifetime, right.Lifetime, left.Changes, right.Changes);
                var ct = linked.Token; a = left.Open(ct); b = right.Open(ct);
                var sa = Hl7StructureScanner.Scan(a, left.Encoding, cancellationToken: ct);
                var sb = Hl7StructureScanner.Scan(b, right.Encoding, cancellationToken: ct);
                ValidatePastedStructure(left, sa, "A"); ValidatePastedStructure(right, sb, "B");
                var result = Hl7Comparison.Compare(a, sa, b, sb, options, ct, left.Encoding, right.Encoding);
                ct.ThrowIfCancellationRequested();
                // Decoded readers bind their construction token; later reads have their own cancellation guard.
                if (a is DecodedMessageSource da) da.BindCancellation(CancellationToken.None);
                if (b is DecodedMessageSource db) db.BindCancellation(CancellationToken.None);
                return new MessageComparison(left, right, a, b, sa, sb, result);
            }
            catch { (a as IDisposable)?.Dispose(); (b as IDisposable)?.Dispose(); left.Dispose(); right.Dispose(); throw; }
        });
    }
    private static void ValidatePastedStructure(Capture capture, Hl7Structure structure, string side)
    {
        if (!capture.IsPasted) return;
        if (structure.Segments.Count == 0) throw new ComparisonInputException($"Pasted Text {side} is empty. Paste one HL7 message.");
        if (structure.Diagnostics.Any(d => d.Code == "MultipleMessages"))
            throw new ComparisonInputException($"Pasted Text {side} contains multiple MSH messages. Paste one message per side; no message was silently omitted.");
    }
    // Documents borrow their existing lease. Pasted strings are immutable, session-only captures;
    // they need neither a Workbench document nor a disk snapshot and cannot mutate document history.
    private sealed class Capture : IDisposable
    {
        private readonly MessageDocument.ComparisonCapture? document;
        private readonly string? text;
        public bool IsPasted => text is not null;
        public Encoding Encoding => document?.Encoding ?? Encoding.UTF8;
        public CancellationToken Lifetime => document?.Lifetime ?? CancellationToken.None;
        public CancellationToken Changes => document?.Changes ?? CancellationToken.None;
        public bool IsCurrent => document is null || document.Document.IsSearchCurrent(document.Revision);
        public Capture(ComparisonSource source)
        {
            text = source.PastedText;
            if (text is not null)
            {
                if (string.IsNullOrWhiteSpace(text)) throw new ComparisonInputException("Pasted Text is empty. Paste one HL7 message per side.");
                ComparisonInput.ValidatePastedText(text);
            }
            else document = source.Document!.CaptureComparison(source.MessageId!.Value, source.Original);
        }
        public IHl7TextSource Open(CancellationToken token)
        { token.ThrowIfCancellationRequested(); return text is not null ? new StringHl7TextSource(text) : document!.Open(token); }
        public void Dispose() => document?.Dispose();
    }
    private async Task<T> ReadAsync<T>(Func<T> read, CancellationToken token)
    {
        await reads.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!IsCurrent) throw new InvalidOperationException("Comparison is stale. Compare again.");
            var result = await Task.Run(read, token);
            token.ThrowIfCancellationRequested();
            if (!IsCurrent) throw new InvalidOperationException("Comparison is stale. Compare again.");
            return result;
        }
        finally { reads.Release(); }
    }
    public Task<ComparisonWindow> WindowAsync(bool before, CharacterRange? target = null, long? origin = null, CancellationToken token = default) => ReadAsync(() =>
    {
        var source = before ? a : b;
        var start = Math.Clamp(origin ?? (source.Length <= WindowCharacters ? 0 : (target?.Offset ?? 0) - 4096), 0, Math.Max(0, source.Length - WindowCharacters));
        // Move the beginning forward and the end backward when a window boundary
        // intersects a surrogate pair. Overlapping paging still exposes every character.
        if (start > 0 && start < source.Length && char.IsLowSurrogate(source[start]) && char.IsHighSurrogate(source[start - 1])) start++;
        var end = start + Math.Min(WindowCharacters, source.Length - start);
        if (end > start && end < source.Length && char.IsLowSurrogate(source[end]) && char.IsHighSurrogate(source[end - 1])) end--;
        return new ComparisonWindow(source.Read(new(start, end - start)), start, source.Length, target);
    }, token);
    /// <summary>Bounded selected-value detail; abbreviated text is explicitly marked and never used by complete-value copy.</summary>
    public Task<string> PreviewValueAsync(bool before, CharacterRange range, CancellationToken token = default) => ReadAsync(() =>
    {
        var source = before ? a : b; var length = Math.Min(4096, range.Length);
        if (length > 0 && length < range.Length && char.IsHighSurrogate(source[range.Offset + length - 1]) && char.IsLowSurrogate(source[range.Offset + length])) length--;
        return source.Read(new(range.Offset, length)) + (length < range.Length ? " … [preview]" : "");
    }, token);
    /// <summary>Explicit complete-value copy; refuses oversized values rather than copying a preview as authoritative.</summary>
    public Task<string> ValueAsync(bool before, CharacterRange range, CancellationToken token = default) => ReadAsync(() =>
    {
        if (range.Length > MaximumCopyCharacters) throw new Hl7LimitException("ComparisonCopyCharacters");
        var source = before ? a : b; var text = new System.Text.StringBuilder((int)range.Length);
        for (long p = 0; p < range.Length; p += WindowCharacters)
        { token.ThrowIfCancellationRequested(); text.Append(source.Read(new(range.Offset + p, Math.Min(WindowCharacters, range.Length - p)))); }
        return text.ToString();
    }, token);
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        Disposal = DrainAsync();
    }
    private async Task DrainAsync()
    {
        await reads.WaitAsync();
        try { (a as IDisposable)?.Dispose(); (b as IDisposable)?.Dispose(); left.Dispose(); right.Dispose(); }
        finally { reads.Release(); }
    }
}
