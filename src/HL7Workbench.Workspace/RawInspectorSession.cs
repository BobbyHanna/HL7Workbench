using System.ComponentModel;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed record RawSourceWindow(long ScopeOffset, long MessageOffset, string Text, ByteRange? MessageBytes);

/// <summary>Borrows its document's source. Navigation or document closure ends the session predictably.</summary>
public sealed class RawInspectorSession : IDisposable
{
    public const int ChunkCharacters = 16384;
    private MessageDocument? document;
    private readonly CancellationTokenSource lifetime = new();
    private bool disposed;
    public CharacterRange Scope { get; }
    public string Label { get; }
    public int MessageNumber { get; }
    public long MessageLength { get; }
    public long WindowOffset { get; set; }
    public bool IsClosed => disposed;
    public bool CanExport => !disposed && document?.Snapshot is not null && !document.HasWorkingSourceChanges && document.MaterializedMessage?.IsModified != true;
    public MessageInspection? Inspection => document?.ActiveInspection ?? (document?.MaterializedMessage?.HasDecodedText == true ? document.Inspection : null);
    public Hl7Structure? Structure => document?.Structure;
    public event EventHandler? Closed;
    public void ReportReadFailure() => document?.RecordActivity(ActivityKind.SourceReadFailed, messageNumber: MessageNumber);
    public RawInspectorSession(MessageDocument document, CharacterRange? scope = null, string? path = null)
    {
        if (document.HasWorkingSourceChanges) throw new InvalidOperationException("Original-source ranges are unavailable for edited working text. Inspect an unchanged message or reopen the saved file.");
        if (document.IsLoading || document.Source is null && document.MaterializedMessage?.HasDecodedText != true) throw new InvalidOperationException("Active source unavailable.");
        this.document = document; Scope = scope ?? new(0, document.TextLength);
        if (Scope.Offset < 0 || Scope.Length < 0 || Scope.End > document.TextLength) throw new ArgumentOutOfRangeException(nameof(scope));
        MessageNumber = document.MessageNumber;
        MessageLength = document.TextLength;
        Label = $"{path ?? "Full Message"} — Message {MessageNumber}";
        document.PropertyChanged += Changed; document.Closed += DocumentClosed;
    }
    private void Changed(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == "ActiveContent") Dispose(); }
    private void DocumentClosed(object? sender, EventArgs e) => Dispose();
    public async Task<RawSourceWindow?> ReadAsync(long scopeOffset, CancellationToken token = default, bool align = false)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (scopeOffset < 0 || scopeOffset > Scope.Length) throw new ArgumentOutOfRangeException(nameof(scopeOffset));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token);
        var owner = document!;
        var result = await owner.ReadTextAsync(source =>
        {
            var start = Scope.Offset + scopeOffset;
            bool Aligned(long p) => p == 0 || p == source.Length || !(char.IsLowSurrogate(source[p]) && char.IsHighSurrogate(source[p - 1]));
            if (align && !Aligned(start)) start--;
            if (!Aligned(start) || !Aligned(Scope.Offset) || !Aligned(Scope.End)) throw new ArgumentOutOfRangeException(nameof(scopeOffset), "Offset falls inside a surrogate pair.");
            var end = Math.Min(Scope.End, start + ChunkCharacters);
            if (!Aligned(end)) end--;
            var range = new CharacterRange(start, end - start);
            return new RawSourceWindow(start - Scope.Offset, start, source.Read(range), source.Bytes(range));
        }, linked.Token);
        return disposed ? null : result;
    }
    /// <summary>Writes encoded source content unchanged. Publishes only after the full stream succeeds.</summary>
    public Task ExportAsync(string destination, CancellationToken token = default) => ExportAsync(FileSaveDestination.NewFile(destination), token);
    /// <summary>Exports original bytes with explicit version-scoped replacement consent.</summary>
    public async Task ExportAsync(FileSaveDestination destination, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        var owner = document!;
        owner.RecordActivity(ActivityKind.ExportStarted, messageNumber: MessageNumber);
        try
        {
            var bytes = await ExportCoreAsync(owner, destination, token);
            owner.RecordActivity(ActivityKind.ExportCompleted, bytes, messageNumber: MessageNumber);
        }
        catch (OperationCanceledException) { owner.RecordActivity(ActivityKind.ExportCanceled, messageNumber: MessageNumber); throw; }
        catch (Exception error) { owner.RecordActivity(ActivityKind.ExportFailed, failure: ActivityLog.Classify(error), messageNumber: MessageNumber); throw; }
    }
    private async Task<long> ExportCoreAsync(MessageDocument owner, FileSaveDestination destination, CancellationToken token)
    {
        var snapshot = owner.Snapshot ?? throw new InvalidOperationException("No exact original bytes.");
        var path = destination.Path;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        var physical = PhysicalPath(path);
        if (string.Equals(physical, snapshot.OriginalFilePath is { } original ? PhysicalPath(original) : null, comparison) ||
            string.Equals(physical, owner.SavePath is { } saved ? PhysicalPath(saved) : null, comparison) ||
            physical.StartsWith(PhysicalPath(snapshot.DirectoryPath) + Path.DirectorySeparatorChar, comparison))
            throw new IOException("Export cannot replace the original source or snapshot.");
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, token);
        long bytes = 0;
        await destination.WriteAsync(async (output, cancellation) =>
        {
            await owner.CopyOriginalAsync(Scope, output, cancellation); bytes = output.Position;
        }, linked.Token);
        return bytes;
    }
    private static string PhysicalPath(string path)
    {
        var full = Path.GetFullPath(path); var current = Path.GetPathRoot(full)!;
        // Resolve existing directory aliases as well as file links before guarding the authoritative source.
        foreach (var part in full[current.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.Length == 0) continue;
            current = Path.Combine(current, part);
            FileSystemInfo? item = Directory.Exists(current) ? new DirectoryInfo(current) : File.Exists(current) ? new FileInfo(current) : null;
            if (item?.LinkTarget is not null) current = item.ResolveLinkTarget(true)?.FullName ?? current;
        }
        return current;
    }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; lifetime.Cancel();
        if (document is { } owner) { owner.PropertyChanged -= Changed; owner.Closed -= DocumentClosed; }
        document = null; Closed?.Invoke(this, EventArgs.Empty); Closed = null;
        // Tokens already borrowed by reads remain usable after the CTS is disposed.
        lifetime.Dispose();
    }
}
