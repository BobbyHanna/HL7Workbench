using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Read-only projection over the existing immutable search capture and Core scanner; no source edits or navigation.</summary>
public sealed class FieldExtraction(ExtractionOptions? options = null)
{
    private readonly ExtractionOptions options = options ?? new();
    public Task<ExtractionResults> RunAsync(MessageDocument document, ExtractionConfiguration configuration,
        ExtractionScope scope = ExtractionScope.WholeDocument, IProgress<ExtractionProgress>? progress = null, CancellationToken token = default)
    {
        options.Validate(); configuration = configuration.Snapshot();
        if (!Enum.IsDefined(scope)) throw new ArgumentException("Choose a supported extraction scope.");
        var capture = document.CaptureSearch(); var active = document.ActiveMessageId;
        return Task.Run(async () =>
        {
            using (capture)
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, capture.Lifetime, capture.Changes))
                return await ScanAsync(capture, configuration, scope, active, progress, linked.Token).ConfigureAwait(false);
        });
    }
    private async Task<ExtractionResults> ScanAsync(MessageDocument.SearchCapture capture, ExtractionConfiguration config,
        ExtractionScope scope, MessageId? active, IProgress<ExtractionProgress>? progress, CancellationToken token)
    {
        var selectors = config.Columns.Select(c => Hl7FieldSelection.Parse(c.Path)).ToArray();
        var entries = scope == ExtractionScope.WholeDocument ? capture.Entries.ToArray() : capture.Entries.Where(e => e.Entry.Id == active).ToArray();
        if (entries.Length == 0) throw new InvalidOperationException("Choose a document with an available message.");
        var offsets = new List<long>(); var identities = new Dictionary<long, MessageId>();
        var counters = selectors.Select(_ => new Counter()).ToArray();
        int examined = 0, matched = 0, unreadable = 0, expansionLimited = 0; var truncated = false; var limit = "";
        var clock = Stopwatch.StartNew();
        var journal = await SourceSnapshot.CreateAsync(async (output, cancellation) =>
        {
            foreach (var entry in entries)
            {
                cancellation.ThrowIfCancellationRequested(); examined++;
                DecodedMessageSource? decoded = null;
                try
                {
                    IHl7TextSource source;
                    var encoding = capture.Encoding;
                    if (entry.Text is { } draft) source = new StringHl7TextSource(draft);
                    else
                    {
                        var range = capture.Index!.Messages[entry.Entry.SourceOrdinal!.Value - 1].PayloadRange;
                        var prefix = capture.Snapshot!.ReadBytes(new(range.Offset, Math.Min(4, range.Length)));
                        var bom = Hl7SourceEncoding.Detect(prefix, encoding).BomLength;
                        if (range.Length <= 128 * 1024)
                        {
                            encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
                            source = new StringHl7TextSource(encoding.GetString(capture.Snapshot.ReadBytes(range, 128 * 1024).AsSpan(bom)));
                        }
                        else source = decoded = DecodedMessageSource.Create(capture.Snapshot, range, encoding, bom, cancellation);
                    }
                    var structure = Hl7StructureScanner.Scan(source, encoding, cancellationToken: cancellation);
                    var columns = selectors.Select(s => s.Select(structure, config.Mode == ExtractionMode.ExpandMatches, options.MaximumRowsPerMessage, cancellation)).ToArray();
                    var rowCount = columns.Max(c => c.Matches.Count);
                    if (columns.Any(c => c.Truncated)) { expansionLimited++; truncated = true; limit = "Per-message expansion limit"; }
                    if (columns.Any(c => c.Matches.Any(m => m.Node is not null))) matched++;
                    for (var n = 0; n < rowCount; n++)
                    {
                        cancellation.ThrowIfCancellationRequested();
                        var values = columns.Select(c => c.Matches.Count == 1 ? c.Matches[0].Read(source, structure, encoding, config.DecodeEscapes, options.MaximumValueCharacters) :
                            n < c.Matches.Count ? c.Matches[n].Read(source, structure, encoding, config.DecodeEscapes, options.MaximumValueCharacters) :
                            new Hl7ExtractedValue(Hl7ValueState.Missing, Hl7MissingKind.Alignment, Hl7ExtractionStatus.Complete, "", "")).ToArray();
                        if (!await Append(new(entry.Entry.Id.Value, entry.Position, n + 1, values), entry.Entry.Id, output, cancellation)) break;
                    }
                }
                catch (Exception error) when (error is DecoderFallbackException or Hl7LimitException)
                {
                    unreadable++;
                    var values = selectors.Select(s => new Hl7ExtractedValue(Hl7ValueState.Missing, Hl7MissingKind.None,
                        Hl7ExtractionStatus.Unreadable, "", s.CanonicalPath)).ToArray();
                    _ = await Append(new(entry.Entry.Id.Value, entry.Position, 1, values), entry.Entry.Id, output, cancellation);
                }
                finally { decoded?.Dispose(); }
                if (examined == 1 || clock.ElapsedMilliseconds >= 100 || examined == entries.Length)
                { progress?.Report(new(examined, entries.Length, offsets.Count)); clock.Restart(); }
                if (limit is "Result row limit" or "Result storage limit") break;
            }
            cancellation.ThrowIfCancellationRequested();
            async Task<bool> Append(ExtractionRow row, MessageId id, Stream stream, CancellationToken ct)
            {
                if (offsets.Count == options.MaximumRows) { truncated = true; limit = "Result row limit"; return false; }
                var bytes = JsonSerializer.SerializeToUtf8Bytes(row);
                if (bytes.Length > ExtractionOptions.MaximumRowBytes || stream.Position + bytes.Length > options.MaximumJournalBytes)
                { truncated = true; limit = "Result storage limit"; return false; }
                offsets.Add(stream.Position); identities.TryAdd(id.Value, id);
                await stream.WriteAsync(bytes, ct).ConfigureAwait(false);
                for (var i = 0; i < counters.Length; i++) counters[i].Add(row.Values[i], options.DistinctLimit);
                return true;
            }
        }, options.MaximumJournalBytes, token, options.StorageRoot).ConfigureAwait(false);
        try
        {
            token.ThrowIfCancellationRequested();
            if (!capture.Document.IsSearchCurrent(capture.Revision)) throw new OperationCanceledException(token);
            var summary = new ExtractionSummary(entries.Length, examined, matched, offsets.Count, unreadable, expansionLimited, truncated, limit,
                counters.Select((c, i) => c.Summary(config.Columns[i].Label)).ToArray());
            return new(journal, offsets.ToArray(), identities, capture.Document, capture.Revision, config, summary, options);
        }
        catch { journal.Dispose(); throw; }
    }
    private sealed class Counter
    {
        private long missing, empty, nulls, unavailable;
        private readonly HashSet<string> hashes = []; private bool saturated;
        internal void Add(Hl7ExtractedValue value, int maximum)
        {
            if (value.State == Hl7ValueState.Missing) missing++;
            if (value.State == Hl7ValueState.Empty) empty++;
            if (value.State == Hl7ValueState.ExplicitNull) nulls++;
            if (value.Status != Hl7ExtractionStatus.Complete) unavailable++;
            if (value.State != Hl7ValueState.Value || value.Status != Hl7ExtractionStatus.Complete || saturated) return;
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Text)));
            if (!hashes.Contains(hash)) { if (hashes.Count == maximum) saturated = true; else hashes.Add(hash); }
        }
        internal ExtractionColumnSummary Summary(string label) => new(label, missing, empty, nulls, unavailable, hashes.Count, saturated);
    }
}
