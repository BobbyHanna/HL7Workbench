using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public sealed partial class MessageDocument
{
    // Reuse Stage 3's complete-editor and longest-line budgets. Aggregate retained text
    // (including history/save points) is capped at 32 MiB UTF-16 per file document.
    public const int MaximumEditableLineCharacters = 16384;
    public const int MaximumRetainedEditCharacters = 16 * 1024 * 1024;
    public const int MaximumHistoryEntries = 4096;
    private readonly Dictionary<int, EditableMessageBuffer> buffers = [];
    private bool editMode;
    private CancellationTokenSource? editRefresh;
    private MessageInspection? previousInspection;
    /// <summary>Current projection, or one bounded previous parse for explicitly stale presentation only.</summary>
    public MessageInspection? InspectionSnapshot => IsLoading ? null : inspection ??
        (message?.HasDecodedText == true ? Inspection : previousInspection);
    public DocumentInspectionState InspectionState => IsLoading ? DocumentInspectionState.Unavailable :
        EditableText?.Length == 0 ? DocumentInspectionState.Empty :
        IsRefreshingEdit ? DocumentInspectionState.Pending :
        message?.HasDecodedText == true || inspection is not null ? DocumentInspectionState.Current :
        previousInspection is not null ? DocumentInspectionState.Stale : DocumentInspectionState.Unavailable;
    public bool IsEditMode
    {
        get => editMode;
        set { if (editMode == value) return; editMode = value; PropertyChanged?.Invoke(this, new(nameof(IsEditMode))); }
    }
    public string DisplayTitle => Title + (IsDirty ? " *" : "");
    public bool IsDirty => generatedUnsaved || buffers.Values.Any(b => b.IsDirty);
    public bool IsSaving { get; private set; }
    public string? SavePath { get; private set; }
    public string? EditError { get; private set; }
    public bool IsRefreshingEdit { get; private set; }
    public Task EditRefresh { get; private set; } = Task.CompletedTask;
    public bool HasWorkingSourceChanges => buffers.TryGetValue(MessageNumber, out var b) && b.DiffersFromOriginal;
    public string? WorkingText => buffers.GetValueOrDefault(MessageNumber)?.Text;
    public string? EditableText => WorkingText ?? (message?.HasDecodedText == true ? message.RawText : null);
    public bool CanUndo => !IsSaving && !IsCapturingScript && buffers.GetValueOrDefault(MessageNumber)?.CanUndo == true;
    public bool CanRedo => !IsSaving && !IsCapturingScript && buffers.GetValueOrDefault(MessageNumber)?.CanRedo == true;
    public string? EditingUnavailableReason => IsLoading ? "Wait for message preparation." :
        buffers.ContainsKey(MessageNumber) ? null :
        Source is not null ? "This source-backed message remains read-only. Editing requires a complete message within the existing 1 Mi-character / 16 Ki-character line viewer limits." :
        message?.HasDecodedText != true ? "Safely decoded complete text is unavailable." :
        message.RawText.Length > MessagePresentation.MaximumEditorCharacters || message.Segments.Any(s => s.Raw.Length > MaximumEditableLineCharacters) ?
            "Message exceeds the complete-editor or line rendering limit; source inspection remains available." :
        !(message.Encoding.IsSingleByte || message.Encoding.CodePage is 65001 or 1200 or 1201) ? "This encoding supports inspection and exact copying; manual editing is unavailable." : null;
    public bool CanEditText => !disposed && !IsSaving && !IsCapturingScript && IsEditMode && EditingUnavailableReason is null;

    /// <summary>Updates a bounded draft synchronously; parsing is debounced and version-checked off the UI thread.</summary>
    public void ReplaceWorkingText(string text)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!CanEditText) throw new InvalidOperationException("Enable Edit Mode on a supported message first.");
        ArgumentNullException.ThrowIfNull(text);
        if (text.Length > MessagePresentation.MaximumEditorCharacters) throw new Hl7LimitException("EditableMessageCharacters");
        var existing = buffers.GetValueOrDefault(MessageNumber);
        if (text == (existing?.Text ?? message!.RawText)) return;
        var buffer = existing ?? new EditableMessageBuffer(message!);
        var change = buffer.Plan(text);
        var others = buffers.Values.Where(b => !ReferenceEquals(b, existing)).Sum(b => b.RetainedCharacters);
        if (others + buffer.CostAfter(text, change) > MaximumRetainedEditCharacters ||
            buffers.Values.Where(b => !ReferenceEquals(b, existing)).Sum(b => b.HistoryCount) + buffer.HistoryCountAfterReplace > MaximumHistoryEntries)
            throw new Hl7LimitException("RetainedEditHistory");
        buffer.Replace(text, change); buffers[MessageNumber] = buffer;
        WorkingChanged();
    }
    public void Undo()
    {
        if (!CanEditText || !CanUndo) return;
        buffers[MessageNumber].Undo(); WorkingChanged();
    }
    public void Redo()
    {
        if (!CanEditText || !CanRedo) return;
        buffers[MessageNumber].Redo(); WorkingChanged();
    }
    private void WorkingChanged()
    {
        editRefresh?.Cancel(); editRefresh?.Dispose();
        editRefresh = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        var version = ++generation;
        var buffer = buffers[MessageNumber]; var text = buffer.Text;
        previousInspection ??= inspection ?? (message?.HasDecodedText == true ? Inspection : null);
        previousInspection?.Invalidate();
        message = null; inspection = null; ActiveDiagnostics = []; EditError = null; IsRefreshingEdit = true;
        Changed(); PropertyChanged?.Invoke(this, new(nameof(DisplayTitle)));
        EditRefresh = RefreshEditAsync(buffer, text, version, editRefresh.Token);
    }
    private async Task RefreshEditAsync(EditableMessageBuffer buffer, string text, long version, CancellationToken token)
    {
        try
        {
            await Task.Delay(180, token);
            await work.WaitAsync(token);
            try
            {
                await scanConcurrency.WaitAsync(token);
                Hl7Message parsed;
                try { parsed = await Task.Run(() => buffer.Parse(text), token); }
                finally { scanConcurrency.Release(); }
                if (disposed || version != generation) return;
                message = parsed; inspection = new(parsed);
                if (previousInspection is { } previous) inspection.RestoreSelection(previous);
                previousInspection = null;
                var boundary = Index is { } index ? index.Messages[MessageNumber - 1].Diagnostics : [];
                ActiveDiagnostics = boundary.Concat(parsed.Diagnostics).Concat(Hl7Validator.Validate(parsed)).ToArray();
            }
            finally { work.Release(); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception error) when (error is Hl7LimitException or EncoderFallbackException or ArgumentException)
        {
            if (!disposed && version == generation) EditError = "Draft retained. Inspection and saving require representable text within the editor and Core structural limits. Undo or shorten the edit.";
        }
        finally
        {
            if (!disposed && version == generation) { IsRefreshingEdit = false; Changed(); }
        }
    }

    /// <summary>Publishes the whole file, copying untouched snapshot bytes and substituting bounded payload overlays.</summary>
    public async Task SaveAsync(string? destination = null, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsSaving || IsCapturingScript) throw new InvalidOperationException("A save or script capture is already running.");
        var path = Path.GetFullPath(destination ?? SavePath ?? throw new InvalidOperationException("Choose a local destination with Save As."));
        if (Snapshot is { } owned && PhysicalPath(path).StartsWith(PhysicalPath(owned.DirectoryPath) + Path.DirectorySeparatorChar,
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            throw new IOException("Cannot replace owned snapshot storage.");
        IsSaving = true; PropertyChanged?.Invoke(this, new(nameof(IsSaving)));
        RecordActivity(ActivityKind.SaveStarted);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            await work.WaitAsync(linked.Token);
            try
            {
                // Edits are frozen until publication. No file-sized string or output byte array.
                await Task.Run(async () =>
                {
                    await scanConcurrency.WaitAsync(linked.Token);
                    try
                    {
                        foreach (var (number, buffer) in buffers)
                        {
                            linked.Token.ThrowIfCancellationRequested();
                            var parsed = buffer.Parse(); ValidateSaveBoundary(number, buffer, parsed);
                            if (buffer.DiffersFromOriginal && parsed.Encoding.CodePage is 1200 or 1201)
                                await ValidateEncodedBoundaryAsync(number, buffer, parsed.Encoding, linked.Token);
                        }
                    }
                    finally { scanConcurrency.Release(); }
                    var temporary = Path.Combine(Path.GetDirectoryName(path)!, ".hl7-save-" + Guid.NewGuid().ToString("N") + ".tmp");
                    try
                    {
                        await using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65536, true))
                        {
                            if (Snapshot is { } snapshot && Index is { } index)
                            {
                                long cursor = 0;
                                foreach (var (number, buffer) in buffers.OrderBy(p => p.Key))
                                {
                                    if (!buffer.DiffersFromOriginal) continue;
                                    var range = index.Messages[number - 1].PayloadRange;
                                    await CopySnapshotAsync(snapshot, new(cursor, range.Offset - cursor), output, linked.Token);
                                    await output.WriteAsync(buffer.Serialize(), linked.Token); cursor = range.End;
                                }
                                await CopySnapshotAsync(snapshot, new(cursor, snapshot.Length - cursor), output, linked.Token);
                            }
                            else await output.WriteAsync(buffers.GetValueOrDefault(1)?.Serialize() ?? Hl7Serializer.Serialize(Message), linked.Token);
                            await output.FlushAsync(linked.Token); output.Flush(true);
                        }
                        linked.Token.ThrowIfCancellationRequested();
                        // Same-directory publication avoids a cross-volume copy. Existing files
                        // use replacement; failure never deletes the destination or working state.
                        if (File.Exists(path)) File.Replace(temporary, path, null);
                        else File.Move(temporary, path);
                    }
                    finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
                }, linked.Token);
                foreach (var buffer in buffers.Values) buffer.MarkSaved();
                generatedUnsaved = false;
                SavePath = path; Title = Path.GetFileName(path);
                RecordActivity(ActivityKind.SaveCompleted);
            }
            finally { work.Release(); }
        }
        catch (OperationCanceledException) { RecordActivity(ActivityKind.SaveCanceled); throw; }
        catch (Exception error) { RecordActivity(ActivityKind.SaveFailed, failure: ActivityLog.Classify(error)); throw; }
        finally { IsSaving = false; PropertyChanged?.Invoke(this, new(nameof(DisplayTitle))); PropertyChanged?.Invoke(this, new(nameof(IsSaving))); }
    }
    private void ValidateSaveBoundary(int number, EditableMessageBuffer buffer, Hl7Message parsed)
    {
        if (!buffer.DiffersFromOriginal) return;
        if (buffer.Text.IndexOfAny(['\v', '\x1c', '\uFEFF']) >= 0 || parsed.Segments.Any(s => s.Raw.Length >= 3 &&
            s.Raw[..3] is "FHS" or "BHS" or "BTS" or "FTS") || parsed.Segments.Count(s => s.Raw.StartsWith("MSH", StringComparison.Ordinal)) > 1 ||
            buffer.OriginalText.StartsWith("MSH", StringComparison.Ordinal) && !buffer.Text.StartsWith("MSH", StringComparison.Ordinal))
            throw new InvalidOperationException("Save refused: editing message boundaries, batch wrappers or transport framing is not supported. Draft retained.");
        if (Index is { } index && Snapshot is { } snapshot)
        {
            var entry = index.Messages[number - 1];
            // Keep the exact terminating suffix where bytes follow an unframed payload.
            // Otherwise deleting CR can concatenate the next MSH/batch wrapper on reopen.
            if (!entry.Framed && entry.PayloadRange.End < snapshot.Length)
            {
                var end = buffer.OriginalText.Length;
                while (end > 0 && buffer.OriginalText[end - 1] is '\r' or '\n') end--;
                var suffix = buffer.OriginalText[end..];
                if (suffix.Length == 0 || !buffer.Text.EndsWith(suffix, StringComparison.Ordinal))
                    throw new InvalidOperationException("Save refused: retain the original ending before the next source region. Draft retained.");
            }
        }
    }
    private async Task ValidateEncodedBoundaryAsync(int number, EditableMessageBuffer buffer, Encoding encoding, CancellationToken token)
    {
        var bytes = buffer.Serialize();
        var bom = Hl7SourceEncoding.Detect(bytes, encoding).BomLength;
        var framed = Index?.Messages[number - 1].Framed == true;
        // Reuse the authoritative indexer over one bounded encoded payload. Wide
        // code units can resemble raw MLLP octets without containing U+001C/U+000B.
        // The synthetic BOM/frame select the inherited encoding and framing context;
        // they are validation-only and never enter saved output.
        using var candidate = new MemoryStream();
        candidate.Write(encoding.GetPreamble());
        if (framed) candidate.WriteByte(0x0B);
        candidate.Write(bytes.AsSpan(bom));
        if (framed) candidate.Write(new byte[] { 0x1C, 0x0D });
        candidate.Position = 0;
        using var snapshot = await SourceSnapshot.CaptureAsync(candidate, token);
        var index = MessageFileIndex.Build(snapshot, token);
        var expected = new ByteRange(index.BomLength + (framed ? 1 : 0), bytes.Length - bom);
        if (index.Messages.Count != 1 || index.Messages[0].PayloadRange != expected || index.Messages[0].Framed != framed)
            throw new InvalidOperationException("Save refused: encoded text would change a message or transport boundary when reopened. Draft retained.");
    }
    private static async Task CopySnapshotAsync(SourceSnapshot snapshot, ByteRange range, Stream output, CancellationToken token)
    {
        var chunk = new byte[65536];
        for (long offset = range.Offset; offset < range.End;)
        {
            token.ThrowIfCancellationRequested(); var count = (int)Math.Min(chunk.Length, range.End - offset);
            snapshot.Read(new(offset, count), chunk.AsSpan(0, count));
            await output.WriteAsync(chunk.AsMemory(0, count), token); offset += count;
        }
    }
    private static string PhysicalPath(string path)
    {
        var full = Path.GetFullPath(path); var current = Path.GetPathRoot(full)!;
        foreach (var part in full[current.Length..].Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            FileSystemInfo? item = Directory.Exists(current) ? new DirectoryInfo(current) : File.Exists(current) ? new FileInfo(current) : null;
            if (item?.LinkTarget is not null) current = item.ResolveLinkTarget(true)?.FullName ?? current;
        }
        return current;
    }
    internal void SetInitialSavePath(string? path) => SavePath = path;
}
