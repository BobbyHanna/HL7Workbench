using System.ComponentModel;
using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>One file session. Only its current message owns decoded and structural state.</summary>
public sealed partial class MessageDocument : INotifyPropertyChanged, IDisposable
{
    private static readonly SemaphoreSlim scanConcurrency = new(2, 2);
    private readonly SemaphoreSlim work = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private CancellationTokenSource? request;
    private long generation;
    private bool disposed;
    private Hl7Message? message;
    private readonly string? initialComparisonText;
    private readonly StructureScanOptions? inspectionOptions;
    private DecodedMessageSource? cachedSource;
    private int cachedNumber;
    private MessageInspection? inspection;
    private DocumentActivity? activityDiagnostics;
    private ActivityLog? activity;
    private bool sourceFailureReported;
    public int ActivityNumber { get; internal set; }
    internal void AttachActivity(ActivityLog log)
    { activity = log; activityDiagnostics ??= new(log, ActivityNumber); ObserveActivity(); }
    internal void ObserveActivity() => activityDiagnostics?.Observe(this);
    /// <summary>Records a typed operation without retaining source, paths or imported labels.</summary>
    public void RecordActivity(ActivityKind kind, long? count = null, ActivityFailure failure = ActivityFailure.None, int? messageNumber = null)
    {
        if (kind == ActivityKind.SourceReadFailed)
        { if (sourceFailureReported) return; sourceFailureReported = true; }
        activity?.Record(kind, ActivityNumber, messageNumber ?? (MessageNumber > 0 ? MessageNumber : null), count, failure);
    }
    public string Id { get; }
    public string Title { get; private set; }
    public SourceSnapshot? Snapshot { get; }
    /// <summary>Session-only review for the producing transformation, independent of the Script Editor's latest run.</summary>
    public TransformationReport? TransformationReport { get; internal set; }
    /// <summary>Count-only captured FHIR generation provenance; independent of imported-session/tool lifetime.</summary>
    public FhirGenerationReport? GenerationReport { get; internal set; }
    /// <summary>Original counts for the most recent successful synthetic batch, independent of later history and tool lifetime.</summary>
    public SyntheticGenerationReport? SyntheticGenerationReport { get; private set; }
    internal void SetSyntheticGenerationReport(SyntheticGenerationReport report)
    { SyntheticGenerationReport = report; PropertyChanged?.Invoke(this, new(nameof(SyntheticGenerationReport))); }
    public MessageFileIndex? Index { get; }
    public Hl7Message Message => message ?? throw new InvalidOperationException("Active content is source-backed or unavailable.");
    public Hl7Message? MaterializedMessage => message;
    public MessageInspection Inspection => inspection ??= new(Message);
    public MessageInspection? ActiveInspection => inspection;
    public DecodedMessageSource? Source { get; private set; }
    public Hl7Structure? Structure { get; private set; }
    public MessagePresentation? Presentation { get; private set; }
    public string? DeclaredVersion => message?.Get("MSH-12").Raw;
    public int MessageCount => sequence.Count;
    /// <summary>Current one-based logical position, or zero when the document is empty.</summary>
    public int MessageNumber => activeId is { } id ? sequence.Position(id) : 0;
    public bool IsLoading { get; private set; }
    public long PreparedBytes { get; private set; }
    public string? Failure { get; private set; }
    public IReadOnlyList<Hl7Diagnostic> ActiveDiagnostics { get; private set; } = [];
    public Task Disposal { get; private set; } = Task.CompletedTask;
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? Closed;
    public long TextLength => Source?.Length ?? EditableText?.Length ?? 0;
    /// <summary>Session generation for UI intents that may outlive a context menu or native dialog.</summary>
    public long ContentVersion => generation;
    public MessageDocument(string id, string title, Hl7Message message)
    {
        Id = id; Title = title; this.message = message; searchEncoding = message.Encoding;
        initialComparisonText = message.HasDecodedText ? message.RawText : null;
        memoryOriginalBytes = Hl7Serializer.Serialize(message);
        sequence = new(1, MessageOrigin.Memory); originalIds = savedOrder = sequence.Snapshot(); activeId = originalIds[0];
    }
    internal MessageDocument(string id, string title, SourceSnapshot snapshot, MessageFileIndex index, StructureScanOptions? inspectionOptions = null)
    {
        Id = id; Title = title; Snapshot = snapshot; Index = index;
        searchEncoding = index.Encoding;
        sequence = new(index.Messages.Count, MessageOrigin.File); originalIds = savedOrder = sequence.Snapshot();
        this.inspectionOptions = inspectionOptions;
        if (index.Messages.Count == 0 && index.Diagnostics.Any(d => d.Code == "InvalidEncoding"))
            Failure = "Text unavailable: input cannot be decoded safely. Inspect retained source bytes.";
    }
    private void Changed() => PropertyChanged?.Invoke(this, new("ActiveContent"));

    /// <summary>Navigates by current logical position. Retained feature selections should use MessageId instead.</summary>
    public async Task NavigateAsync(int number, CancellationToken cancellationToken = default)
    {
        if (disposed) throw new ObjectDisposedException(nameof(MessageDocument));
        if (number < 1 || number > MessageCount) throw new ArgumentOutOfRangeException(nameof(number));
        if (IsSaving || IsCapturingScript || IsPreparingInsertion) return;
        if (number == MessageNumber && !IsLoading && Failure is null && (message is not null || Source is not null)) return;
        var logical = sequence[number - 1];
        request?.Cancel();
        request?.Dispose();
        request = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        var token = request.Token;
        var version = ++generation;
        editRefresh?.Cancel(); IsRefreshingEdit = false; EditError = null;
        activeId = logical.Id; PreparedBytes = 0; IsLoading = true; Failure = null; ActiveDiagnostics = [];
        message = null; inspection = null; previousInspection = null; Structure = null; Source = null; Presentation = null;
        Changed();
        var progress = new ThrottledProgress(new Progress<long>(value =>
        {
            if (!disposed && version == generation && IsLoading)
            { PreparedBytes = value; PropertyChanged?.Invoke(this, new(nameof(PreparedBytes))); }
        }));
        try
        {
            await work.WaitAsync(token);
            try
            {
                if (!ReferenceEquals(Source, cachedSource)) Source?.Dispose();
                Source = null;
                var entry = logical.SourceOrdinal is { } ordinal ? Index!.Messages[ordinal - 1] : null;
                await scanConcurrency.WaitAsync(token);
                Loaded result;
                try { result = await Task.Run(() => Load(logical, entry, token, progress), token); }
                finally { scanConcurrency.Release(); }
                if (disposed || version != generation || token.IsCancellationRequested)
                { if (!ReferenceEquals(result.Source, cachedSource)) result.Source?.Dispose(); token.ThrowIfCancellationRequested(); return; }
                Source = result.Source; Structure = result.Structure; message = result.Message;
                Presentation = result.Presentation;
                inspection = result.Inspection; inspection?.ClearSelection();
                Failure = result.Failure;
                ActiveDiagnostics = (entry?.Diagnostics ?? []).Concat(result.Diagnostics).ToArray();
                IsLoading = false;
                Changed();
            }
            finally { work.Release(); }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            if (!disposed && version == generation)
            { IsLoading = false; Failure = "Message scan canceled. Choose Retry or another message."; Changed(); }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Hl7LimitException or DecoderFallbackException)
        {
            if (!disposed && version == generation)
            {
                IsLoading = false; Failure = "Inspection unavailable; original bytes remain accessible.";
                ActiveDiagnostics = [new("InspectionFailed", Hl7DiagnosticSeverity.Error, Failure)]; Changed();
            }
        }
    }
    private sealed record Loaded(DecodedMessageSource? Source, Hl7Structure? Structure, Hl7Message? Message,
        MessageInspection? Inspection, string? Failure, IReadOnlyList<Hl7Diagnostic> Diagnostics, MessagePresentation? Presentation = null);
    private Loaded Load(LogicalMessage logical, IndexedMessage? entry, CancellationToken token, IProgress<long> progress)
    {
        if (buffers.TryGetValue(logical.Id, out var buffer))
        {
            try
            {
                var parsed = buffer.Parse();
                return new(null, null, parsed, new(parsed), null, parsed.Diagnostics.Concat(Hl7Validator.Validate(parsed)).ToArray());
            }
            catch (Exception error) when (error is Hl7LimitException or EncoderFallbackException or ArgumentException)
            { return new(null, null, null, null, "Draft retained. Undo or shorten the edit to restore structural inspection and saving.", []); }
        }
        if (logical.Origin == MessageOrigin.Memory)
        {
            var parsed = Hl7Parser.Parse(memoryOriginalBytes!, new() { Encoding = searchEncoding });
            return new(null, null, parsed, parsed.HasDecodedText ? new(parsed) : null, null,
                parsed.Diagnostics.Concat(Hl7Validator.Validate(parsed)).ToArray());
        }
        if (entry is null) throw new InvalidOperationException("Inserted payload is unavailable.");
        var index = Index ?? throw new InvalidOperationException("File index required.");
        var snapshot = Snapshot ?? throw new InvalidOperationException("File snapshot required.");
        DecodedMessageSource? source = null;
        try
        {
            if (cachedNumber == entry.Number && cachedSource is { } cached)
            { source = cached; source.BindCancellation(token); }
            else source = DecodedMessageSource.Create(snapshot, entry.PayloadRange, index.Encoding, 0, token, progress);
            var limits = new Hl7ParseOptions();
            var interiorBom = Hl7SourceEncoding.Detect(snapshot.ReadBytes(new(entry.PayloadRange.Offset, Math.Min(4, entry.PayloadRange.Length)))).BomLength > 0;
            // Size and longest-line preflight avoids throwing after materializing a large value.
            if (inspectionOptions is null && !interiorBom && entry.PayloadRange.Length <= limits.MaxMessageBytes && source.Length <= Math.Min(limits.MaxMessageCharacters, MessagePresentation.MaximumEditorCharacters) &&
                source.LongestLine <= Math.Min(limits.MaxSegmentLength, MaximumEditableLineCharacters))
            {
                var bytes = snapshot.ReadBytes(entry.PayloadRange, limits.MaxMessageBytes);
                var parsed = Hl7Parser.Parse(bytes, limits with { Encoding = index.Encoding });
                var state = new MessageInspection(parsed);
                source.Dispose(); source = null;
                return new(null, null, parsed, state, null, parsed.Diagnostics.Concat(Hl7Validator.Validate(parsed)).ToArray());
            }
            var structure = Hl7StructureScanner.Scan(source, index.Encoding, inspectionOptions, token);
            var diagnostics = structure.Diagnostics.Select(d => new Hl7Diagnostic(d.Code, d.Severity,
                d.Description + (d.CharacterOffset > int.MaxValue ? $" (UTF-16 offset {d.CharacterOffset})" : ""),
                d.CharacterOffset is >= 0 and <= int.MaxValue ? (int)d.CharacterOffset : null, d.Path)).ToList();
            diagnostics.AddRange(Hl7Validator.Validate(structure));
            Cache(source, entry.Number);
            return new(source, structure, null, new MessageInspection(structure, source), null, diagnostics,
                MessagePresentation.Create(source, structure, token));
        }
        catch (DecoderFallbackException)
        {
            source?.Dispose();
            return new(null, null, null, null, "Text unavailable: input cannot be decoded safely. Original bytes are retained.",
                [new("InvalidEncoding", Hl7DiagnosticSeverity.Error, "Input cannot be decoded with the inherited file encoding; original bytes remain available.")]);
        }
        catch (Hl7LimitException)
        {
            if (source is not null) Cache(source, entry.Number);
            return new(source, null, null, null, "Structural inspection is incomplete because a structural budget was exceeded. Raw content remains accessible.",
                [new("InspectionIncomplete", Hl7DiagnosticSeverity.Warning, "Structural budget exhausted; no partial model is presented as complete.")],
                source is null ? null : MessagePresentation.Create(source, null, token));
        }
        catch
        {
            if (ReferenceEquals(source, cachedSource)) { cachedSource = null; cachedNumber = 0; }
            source?.Dispose(); throw;
        }
    }
    private void Cache(DecodedMessageSource source, int number)
    {
        if (!ReferenceEquals(source, cachedSource)) cachedSource?.Dispose();
        cachedSource = source; cachedNumber = number;
    }
    /// <summary>Serializes bounded source access with scan/disposal; stale results are not returned.</summary>
    public async Task<T?> ReadAsync<T>(Func<DecodedMessageSource, T> read, CancellationToken token = default) where T : class
    {
        var version = generation;
        await work.WaitAsync(token);
        try
        {
            if (disposed || IsLoading || Source is not { } source) return null;
            var value = await Task.Run(() => read(source), token);
            return disposed || version != generation ? null : value;
        }
        finally { work.Release(); }
    }
    /// <summary>Bounded reader shared by ordinary and source-backed inspectors. The delegate runs off the UI thread.</summary>
    public async Task<T?> ReadTextAsync<T>(Func<IHl7TextSource, T> read, CancellationToken token = default) where T : class
    {
        var version = generation;
        await work.WaitAsync(token);
        try
        {
            if (disposed || IsLoading) return null;
            IHl7TextSource? text = (IHl7TextSource?)Source ?? (message?.HasDecodedText == true ?
                new MaterializedSource(message.RawText, HasWorkingSourceChanges || message.IsModified ? null : Index?.Encoding) : null);
            if (text is null) return null;
            var result = await Task.Run(() => read(text), token);
            return disposed || version != generation ? null : result;
        }
        finally { work.Release(); }
    }
    private sealed class MaterializedSource(string text, Encoding? encoding) : IHl7TextSource
    {
        private readonly StringHl7TextSource source = new(text);
        public long Length => source.Length;
        public char this[long offset] => source[offset];
        public string Read(CharacterRange range) => range.Length <= DecodedMessageSource.MaxReadCharacters ? source.Read(range) : throw new Hl7LimitException("BoundedReadCharacters");
        public long IndexOfAny(ReadOnlySpan<char> chars, long start, long length) => source.IndexOfAny(chars, start, length);
        public ByteRange? Bytes(CharacterRange range)
        {
            if (range.Offset < 0 || range.End > Length || range.Length < 0) throw new ArgumentOutOfRangeException(nameof(range));
            if (encoding is null) return null;
            var start = encoding.GetByteCount(text.AsSpan(0, (int)range.Offset));
            return new(start, encoding.GetByteCount(text.AsSpan((int)range.Offset, (int)range.Length)));
        }
    }
    /// <summary>Streams one exact original interval. Caller owns output publication and partial-file handling.</summary>
    internal async Task CopyOriginalAsync(CharacterRange range, Stream output, CancellationToken token)
    {
        var version = generation;
        await work.WaitAsync(token);
        try
        {
            if (disposed || IsLoading || Snapshot is null || Index is null || HasWorkingSourceChanges || message?.IsModified == true)
                throw new InvalidOperationException("Exact original range unavailable for the current content.");
            var source = (IHl7TextSource?)Source ?? new MaterializedSource(Message.RawText, Index.Encoding);
            var bytes = await Task.Run(() => source.Bytes(range) ?? throw new InvalidOperationException("Exact byte range unavailable."), token);
            var payload = ActiveOriginalMessage?.PayloadRange ?? throw new InvalidOperationException("No original byte source.");
            var buffer = new byte[65536];
            for (long offset = 0; offset < bytes.Length;)
            {
                token.ThrowIfCancellationRequested();
                if (disposed || version != generation) throw new OperationCanceledException("Document changed.", token);
                var count = (int)Math.Min(buffer.Length, bytes.Length - offset);
                // Snapshot access is file-backed; move both range lookup and reads off the UI thread.
                await Task.Run(() => Snapshot.Read(new(payload.Offset + bytes.Offset + offset, count), buffer.AsSpan(0, count)), token);
                await output.WriteAsync(buffer.AsMemory(0, count), token);
                offset += count;
            }
        }
        finally { work.Release(); }
    }
    public void CancelScan() { if (IsLoading) request?.Cancel(); }
    public void Dispose()
    {
        if (disposed) return;
        disposed = true; generation++; lifetime.Cancel(); request?.Cancel();
        ClearSearchFilter(); searchChanges.Cancel();
        previousInspection = null;
        TransformationReport?.Dispose();
        Closed?.Invoke(this, EventArgs.Empty); Closed = null;
        PropertyChanged = null;
        Disposal = Task.Run(async () =>
        {
            // A search owns immutable draft references and borrows the original snapshot.
            // Let cancellation drain that reader before deleting any source/spool files.
            await DrainSearchReaders();
            await work.WaitAsync();
            try { Source?.Dispose(); Source = null; cachedSource?.Dispose(); cachedSource = null; buffers.Clear(); undo.Clear(); redo.Clear(); Snapshot?.Dispose(); editRefresh?.Dispose(); request?.Dispose(); lifetime.Dispose(); searchChanges.Dispose(); }
            finally { work.Release(); }
        });
    }
}
