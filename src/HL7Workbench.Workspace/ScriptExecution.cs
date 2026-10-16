using HL7Workbench.Core;
using HL7Workbench.Scripting;

namespace HL7Workbench.Workspace;

public sealed record ScriptExecutionOptions
{
    public string WorkerPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "ScriptHost", "HL7Workbench.ScriptHost.dll");
    public ScriptWorkerOptions Worker { get; init; } = new();
    public long MaximumInputBytes { get; init; } = 1024L * 1024 * 1024;
    public long MaximumOutputBytes { get; init; } = 1024L * 1024 * 1024;
    public TimeSpan TotalTimeout { get; init; } = TimeSpan.FromMinutes(10);
    public string? StorageRoot { get; init; }
    internal void Validate()
    {
        if (MaximumInputBytes is < 1 or > SourceSnapshot.MaxFileBytes || MaximumOutputBytes is < 1 or > SourceSnapshot.MaxFileBytes || TotalTimeout <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(ScriptExecutionOptions));
    }
}

/// <summary>Completed independent output. Caller owns Reports and must dispose it after export or abandonment.</summary>
public sealed record ScriptExecutionResult(MessageDocument Document, int Processed, int Kept, long OutputBytes, ScriptReports? Reports = null);

/// <summary>Unified scripting coordinator. Only completed, indexed artifacts are published; input documents are never mutated or saved.</summary>
public sealed class ScriptExecution(DocumentManager documents, ScriptExecutionOptions? options = null)
{
    private static readonly SemaphoreSlim jobs = new(1, 1);
    private readonly ScriptExecutionOptions settings = options ?? new();
    public async Task<ScriptExecutionResult> RunAsync(MessageDocument source, ScriptDefinition script, ScriptScope scope,
        IProgress<ScriptProgress>? progress = null, CancellationToken token = default)
    {
        settings.Validate();
        if (!Enum.IsDefined(scope)) throw new ArgumentOutOfRangeException(nameof(scope));
        if (!documents.Documents.Contains(source)) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputUnavailable"));
        var sourceNumber = source.MessageNumber; var sourceCount = source.MessageCount;
        var encoding = source.Index?.Encoding ?? source.MaterializedMessage?.Encoding;
        if (encoding is null || sourceCount == 0) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputUnavailable"));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token);
        linked.CancelAfter(settings.TotalTimeout);
        void Closed(object? sender, EventArgs e) => linked.Cancel();
        source.Closed += Closed;
        SourceSnapshot? output = null;
        SourceSnapshot? reportJournal = null;
        ScriptReports? reports = null;
        MessageDocument? pending = null;
        var entered = false;
        try
        {
            await jobs.WaitAsync(linked.Token); entered = true;
            documents.Activity.Record(ActivityKind.ScriptStarted, source.ActivityNumber);
            progress?.Report(new("Capturing input", 0, scope == ScriptScope.CurrentMessage ? 1 : sourceCount, 0));
            using var capture = await source.CaptureScriptAsync(settings.MaximumInputBytes, settings.StorageRoot,
                scope == ScriptScope.CurrentMessage ? sourceNumber : null, linked.Token);
            var index = await Task.Run(() => MessageFileIndex.Build(capture, linked.Token, inheritedEncoding: encoding), linked.Token);
            if (index.Messages.Count != sourceCount || sourceNumber < 1 || sourceNumber > sourceCount)
                throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "BoundaryChange"));
            var extracted = scope == ScriptScope.CurrentMessage;
            var canExtract = CanExtract(index);
            if (extracted && !canExtract) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "ContainerExtraction"));
            var targets = extracted ? new[] { index.Messages[sourceNumber - 1] } : index.Messages;
            if (targets.Count > ScriptLimits.BatchMessages) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "BatchLimit"));
            var expected = new List<(ByteRange Payload, bool Framed)>();
            var kept = 0; var processed = 0;
            var progressClock = System.Diagnostics.Stopwatch.StartNew();
            progress?.Report(new("Compiling", 0, targets.Count, 0));
            await using var worker = await ScriptWorkerSession.StartAsync(settings.WorkerPath, script, settings.Worker, linked.Token);
            var reportBuilder = new ScriptReportBuilder();
            // Nest the output writer inside the report journal transaction. Either writer
            // can fail while sealing; the outer ownership guard rolls both snapshots back.
            reportJournal = await Task.Run(() => SourceSnapshot.CreateAsync(async (reportStream, reportToken) =>
            {
                await reportBuilder.AppendAsync(reportStream, worker.DrainReports(), reportToken);
                output = await SourceSnapshot.CreateAsync(async (destination, cancellation) =>
                {
                    long cursor = 0;
                    if (extracted && index.BomLength > 0)
                        await CopyAsync(capture, new(0, index.BomLength), destination, cancellation);
                    foreach (var entry in targets)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        if (entry.PayloadRange.Length > ScriptLimits.MessageBytes)
                            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputLimit", MessageNumber: entry.Number));
                        if (entry.Diagnostics.Any(d => d.Code is "IncompleteMllp" or "AmbiguousFraming" or "InteriorBom"))
                            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "BoundaryChange", MessageNumber: entry.Number));
                        var original = capture.ReadBytes(entry.PayloadRange, ScriptLimits.MessageBytes);
                        var result = await worker.ProcessAsync(original, index.Encoding.CodePage, entry.Number, cancellation);
                        await reportBuilder.AppendAsync(reportStream, worker.DrainReports(), cancellation);
                        if (result.Excluded)
                        {
                            if (!canExtract) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Output, "ContainerExtraction", MessageNumber: entry.Number));
                            if (!extracted)
                            {
                                await CopyAsync(capture, new(cursor, entry.SourceRange.Offset - cursor), destination, cancellation);
                                cursor = entry.SourceRange.End;
                            }
                        }
                        else
                        {
                            var bytes = result.Bytes!;
                            // Parent validation is independent of the worker. No new framing controls,
                            // loss of required endings, or unsafe encoded boundaries may reach publication.
                            ValidatePayload(original, bytes, index.Encoding, entry.Number);
                            await CopyAsync(capture, new(extracted ? entry.SourceRange.Offset : cursor,
                                entry.PayloadRange.Offset - (extracted ? entry.SourceRange.Offset : cursor)), destination, cancellation);
                            expected.Add((new(destination.Length, bytes.Length), entry.Framed));
                            await destination.WriteAsync(bytes, cancellation);
                            if (extracted) await CopyAsync(capture, new(entry.PayloadRange.End, entry.SourceRange.End - entry.PayloadRange.End), destination, cancellation);
                            else cursor = entry.PayloadRange.End;
                            kept++;
                        }
                        processed++;
                        if (processed == targets.Count || progressClock.ElapsedMilliseconds >= 100)
                        { progress?.Report(new("Processing", processed, targets.Count, destination.Length)); progressClock.Restart(); }
                    }
                    if (!extracted) await CopyAsync(capture, new(cursor, capture.Length - cursor), destination, cancellation);
                    progress?.Report(new("Completing", processed, targets.Count, destination.Length));
                    await worker.CompleteAsync(cancellation);
                    await reportBuilder.AppendAsync(reportStream, worker.DrainReports(), cancellation);
                    long deferredBytes = 0;
                    while (await worker.TakeDeferredAsync(cancellation) is { } copy)
                    {
                        if (!canExtract || copy.MessageNumber < 1 || copy.MessageNumber > index.Messages.Count ||
                            extracted && copy.MessageNumber != sourceNumber)
                            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Output, "ContainerExtraction"));
                        deferredBytes += copy.Bytes.Length;
                        if (deferredBytes > ScriptLimits.DeferredBytes || expected.Count >= ScriptLimits.BatchMessages)
                            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Output, "DeferredLimit"));
                        var entry = index.Messages[copy.MessageNumber - 1];
                        var original = capture.ReadBytes(entry.PayloadRange, ScriptLimits.MessageBytes);
                        ValidatePayload(original, copy.Bytes, index.Encoding, entry.Number);
                        await CopyAsync(capture, new(entry.SourceRange.Offset, entry.PayloadRange.Offset - entry.SourceRange.Offset), destination, cancellation);
                        expected.Add((new(destination.Length, copy.Bytes.Length), entry.Framed));
                        await destination.WriteAsync(copy.Bytes, cancellation);
                        await CopyAsync(capture, new(entry.PayloadRange.End, entry.SourceRange.End - entry.PayloadRange.End), destination, cancellation);
                        kept++;
                    }
                }, settings.MaximumOutputBytes, reportToken, settings.StorageRoot);
            }, ScriptLimits.ReportBytes, linked.Token, settings.StorageRoot), linked.Token);
            if (output is null) throw new InvalidOperationException("Output staging did not complete.");
            progress?.Report(new("Validating output", processed, targets.Count, output.Length));
            var outputIndex = await Task.Run(() => MessageFileIndex.Build(output, linked.Token, inheritedEncoding: encoding), linked.Token);
            if (outputIndex.Messages.Count != expected.Count || outputIndex.Messages.Where((m, i) =>
                m.PayloadRange != expected[i].Payload || m.Framed != expected[i].Framed ||
                m.Diagnostics.Any(d => d.Code is "IncompleteMllp" or "AmbiguousFraming" or "InteriorBom")).Any())
                throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Output, "BoundaryChange"));
            var title = "Script result.hl7";
            for (var n = 2; documents.Documents.Any(d => d.Title == title); n++) title = $"Script result ({n}).hl7";
            pending = new(Guid.NewGuid().ToString("N"), title, output, outputIndex);
            output = null; // The pending document now owns the snapshot, including rollback/disposal.
            pending.MarkGeneratedUnsaved();
            if (kept > 0) await pending.NavigateAsync(1, linked.Token);
            linked.Token.ThrowIfCancellationRequested();
            if (!documents.Documents.Contains(source)) throw new OperationCanceledException(linked.Token);
            if (reportBuilder.Items.Count > 0) { reports = new(reportJournal, reportBuilder); reportJournal = null; }
            documents.Open(pending);
            var published = pending; pending = null;
            documents.Activity.Record(ActivityKind.ScriptCompleted, source.ActivityNumber, count: kept);
            var result = new ScriptExecutionResult(published, processed, kept, published.Snapshot!.Length, reports);
            reports = null; return result;
        }
        catch (OperationCanceledException) when (linked.IsCancellationRequested)
        {
            documents.Activity.Record(ActivityKind.ScriptCanceled, source.ActivityNumber);
            if (token.IsCancellationRequested || !documents.Documents.Contains(source)) throw;
            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Worker, "Timeout"));
        }
        catch (ScriptRunException)
        { documents.Activity.Record(ActivityKind.ScriptFailed, source.ActivityNumber); throw; }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or Hl7LimitException or ArgumentException or InvalidOperationException or System.Text.EncoderFallbackException)
        {
            documents.Activity.Record(ActivityKind.ScriptFailed, source.ActivityNumber, failure: ActivityLog.Classify(error));
            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Output, error is Hl7LimitException ? "OutputLimit" : "InputUnavailable"));
        }
        finally
        {
            source.Closed -= Closed;
            output?.Dispose();
            reportJournal?.Dispose(); reports?.Dispose();
            if (pending is not null) { pending.Dispose(); await pending.Disposal; }
            if (entered) jobs.Release();
        }
    }
    private static bool CanExtract(MessageFileIndex index) =>
        index.Regions.All(r => r.MessageNumber is not null || r.Reason == "File encoding BOM") &&
        index.Messages.All(m => m.Diagnostics.All(d => d.Code is not ("IncompleteMllp" or "AmbiguousFraming" or "InteriorBom")));

    private static void ValidatePayload(byte[] original, byte[] changed, System.Text.Encoding encoding, int number)
    {
        if (original.AsSpan().SequenceEqual(changed)) return;
        var parsed = ScriptMessageValidation.Parse(changed, encoding);
        var old = encoding.GetString(original);
        var current = parsed.RawText;
        var ending = old.Length;
        while (ending > 0 && old[ending - 1] is '\r' or '\n') ending--;
        if (current.IndexOfAny(['\v', '\x1c', '\uFEFF']) >= 0 || parsed.Segments.Any(s => s.Identifier is "FHS" or "BHS" or "BTS" or "FTS") ||
            parsed.Segments.Count(s => s.Identifier == "MSH") > 1 || old.StartsWith("MSH", StringComparison.Ordinal) && !current.StartsWith("MSH", StringComparison.Ordinal) ||
            !current.EndsWith(old[ending..], StringComparison.Ordinal))
            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Output, "BoundaryChange", MessageNumber: number));
    }
    internal static async Task CopyAsync(SourceSnapshot source, ByteRange range, Stream output, CancellationToken token)
    {
        var buffer = new byte[65536];
        for (long offset = range.Offset; offset < range.End;)
        {
            token.ThrowIfCancellationRequested(); var count = (int)Math.Min(buffer.Length, range.End - offset);
            source.Read(new(offset, count), buffer.AsSpan(0, count));
            await output.WriteAsync(buffer.AsMemory(0, count), token); offset += count;
        }
    }
}

internal static class ScriptMessageValidation
{
    internal static Hl7Message Parse(byte[] bytes, System.Text.Encoding encoding) => Hl7Parser.Parse(bytes,
        new() { Encoding = encoding, MaxMessageBytes = ScriptLimits.MessageBytes, MaxMessageCharacters = ScriptLimits.MessageCharacters,
            MaxSegmentLength = ScriptLimits.LineCharacters });
}
