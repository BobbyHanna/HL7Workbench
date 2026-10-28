using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Querying;

namespace HL7Workbench.Workspace;

internal sealed record SqlCapturedMessage(long Id, int Number, ByteRange Payload, ByteRange Prefix, ByteRange Suffix,
    bool Framed, bool Serializable, string? Text, bool IndexDiagnostics);

internal sealed class SqlCapturedDocument : IDisposable
{
    internal required string Id { get; init; }
    internal required int Number { get; init; }
    internal required long Revision { get; init; }
    internal required SourceSnapshot Snapshot { get; init; }
    internal required Encoding Encoding { get; init; }
    internal required byte[] Bom { get; init; }
    internal required SqlCapturedMessage[] Messages { get; init; }
    internal required bool Extractable { get; init; }
    internal required WeakReference<MessageDocument> Original { get; init; }
    internal required FileSaveDestination[] ProtectedDestinations { get; init; }
    internal string? OriginalStorage { get; init; }
    public void Dispose() => Snapshot.Dispose();
}

/// <summary>Owns independent original-byte copies plus frozen draft payloads. No result retains a live document read lease.</summary>
internal sealed class SqlCapturedSources : IDisposable
{
    internal List<SqlCapturedDocument> Documents { get; } = [];
    internal Dictionary<(string Document, long Message), (SqlCapturedDocument Document, SqlCapturedMessage Message)> Identities { get; } = [];
    private DecodedMessageSource? decoded;
    private IHl7TextSource? text;
    private Hl7Structure? structure;
    private (string Document, long Message)? cacheKey;
    private bool full;
    internal long HeaderCharacters, BodyDecodedCharacters, ValueRequests;
    internal int StructuredMessages;
    internal static async Task<SqlCapturedSources> CreateAsync(MessageDocument.SearchCapture[] captures, byte[]?[] memory,
        FileSaveDestination[][] guards, SqlQueryOptions options, IProgress<SqlQueryProgress>? progress, CancellationToken token)
    {
        var result = new SqlCapturedSources(); long total = 0;
        try
        {
            for (var d = 0; d < captures.Length; d++)
            {
                token.ThrowIfCancellationRequested(); var capture = captures[d]; var entries = new List<SqlCapturedMessage>();
                var encoding = capture.Encoding; encoding.EncoderFallback = EncoderFallback.ExceptionFallback;
                byte[] bom = [];
                var remaining = options.MaximumSourceBytes - total;
                var snapshot = await SourceSnapshot.CreateAsync(async (output, ct) =>
                {
                    if (capture.Snapshot is { } original)
                    {
                        await ScriptExecution.CopyAsync(original, new(0, original.Length), output, ct);
                        if (capture.Index!.BomLength > 0) bom = original.ReadBytes(new(0, capture.Index.BomLength));
                    }
                    foreach (var entry in capture.Entries)
                    {
                        ct.ThrowIfCancellationRequested(); ByteRange payload = default, prefix = default, suffix = default;
                        var framed = false; var serializable = true; var diagnostic = false;
                        if (entry.Entry.SourceOrdinal is { } ordinal && capture.Index is { } index)
                        {
                            var indexed = index.Messages[ordinal - 1]; payload = indexed.PayloadRange; framed = indexed.Framed;
                            prefix = new(indexed.SourceRange.Offset, payload.Offset - indexed.SourceRange.Offset);
                            suffix = new(payload.End, indexed.SourceRange.End - payload.End); diagnostic = indexed.Diagnostics.Count != 0;
                        }
                        if (entry.Text is not null)
                        {
                            var start = output.Position;
                            try
                            {
                                byte[] bytes;
                                if (entry.Entry.Origin == MessageOrigin.Memory && memory[d] is { } initial && capture.Entries.Count == 1 &&
                                    encoding.GetString(initial.AsSpan(Hl7SourceEncoding.Detect(initial.AsSpan(0, Math.Min(4, initial.Length)), encoding).BomLength)) == entry.Text)
                                {
                                    bytes = initial; var count = Hl7SourceEncoding.Detect(bytes.AsSpan(0, Math.Min(4, bytes.Length)), encoding).BomLength;
                                    bom = bytes[..count]; payload = new(start + count, bytes.Length - count);
                                }
                                else { bytes = encoding.GetBytes(entry.Text); payload = new(start, bytes.Length); }
                                await output.WriteAsync(bytes, ct);
                            }
                            catch (EncoderFallbackException) { serializable = false; payload = default; }
                        }
                        entries.Add(new(entry.Entry.Id.Value, entry.Position, payload, prefix, suffix, framed, serializable, entry.Text, diagnostic));
                    }
                }, remaining, token, options.StorageRoot);
                total += snapshot.Length;
                var extractable = capture.Index is null || capture.Index.Regions.All(r => r.MessageNumber is not null || r.Reason == "File encoding BOM") &&
                    capture.Index.Messages.All(m => m.Diagnostics.All(x => x.Code is not ("IncompleteMllp" or "AmbiguousFraming" or "InteriorBom")));
                var owned = new SqlCapturedDocument
                {
                    Id = capture.Document.Id, Number = capture.Document.ActivityNumber, Revision = capture.Revision, Snapshot = snapshot,
                    Encoding = encoding, Bom = bom, Messages = entries.ToArray(), Extractable = extractable,
                    Original = new(capture.Document), ProtectedDestinations = guards[d], OriginalStorage = capture.Snapshot?.DirectoryPath
                };
                result.Documents.Add(owned);
                foreach (var entry in owned.Messages) result.Identities.Add((owned.Id, entry.Id), (owned, entry));
                progress?.Report(new("Capturing", d + 1, captures.Length));
            }
            return result;
        }
        catch { result.Dispose(); throw; }
    }
    internal void ResetReader()
    { decoded?.Dispose(); decoded = null; text = null; structure = null; cacheKey = null; }

    internal (IHl7TextSource Text, Hl7Structure Structure) Open(SqlCapturedDocument document, SqlCapturedMessage message, bool all, CancellationToken token)
    {
        var key = (document.Id, message.Id);
        if (cacheKey == key && structure is not null && (!all || full)) { decoded?.BindCancellation(token); return (text!, structure); }
        ResetReader(); full = all;
        if (!all)
        {
            var header = Header(document, message, token); HeaderCharacters += header.Length; text = new StringHl7TextSource(header);
        }
        else
        {
            if (message.Text is { } draft) text = new StringHl7TextSource(draft);
            else
            {
                var prefix = document.Snapshot.ReadBytes(new(message.Payload.Offset, Math.Min(4, message.Payload.Length)));
                var bom = Hl7SourceEncoding.Detect(prefix, document.Encoding).BomLength;
                text = decoded = DecodedMessageSource.Create(document.Snapshot, message.Payload, document.Encoding, bom, token);
                BodyDecodedCharacters += decoded.Length;
            }
            StructuredMessages++;
        }
        structure = Hl7StructureScanner.Scan(text, document.Encoding, cancellationToken: token);
        cacheKey = key;
        return (text, structure);
    }
    private static string Header(SqlCapturedDocument document, SqlCapturedMessage message, CancellationToken token)
    {
        if (message.Text is { } draft)
        {
            var end = draft.AsSpan().IndexOfAny('\r', '\n'); if (end < 0) end = draft.Length;
            if (end > QueryLimits.MaximumValueCharacters) throw new Hl7LimitException("SqlHeaderMaterialization");
            return draft[..end];
        }
        var encoding = document.Encoding; encoding.DecoderFallback = DecoderFallback.ExceptionFallback;
        var prefix = document.Snapshot.ReadBytes(new(message.Payload.Offset, Math.Min(4, message.Payload.Length)));
        var bom = Hl7SourceEncoding.Detect(prefix, encoding).BomLength;
        var decoder = encoding.GetDecoder(); var buffer = new byte[4096]; var character = new char[2]; var header = new StringBuilder();
        // Decode only through the first terminator. The bounded byte buffer may contain later
        // source bytes, but no OBX text is decoded or imported to obtain MSH metadata.
        for (long position = bom; position < message.Payload.Length;)
        {
            token.ThrowIfCancellationRequested(); var count = (int)Math.Min(buffer.Length, message.Payload.Length - position);
            document.Snapshot.Read(new(message.Payload.Offset + position, count), buffer.AsSpan(0, count));
            for (var i = 0; i < count; i++)
            {
                decoder.Convert(buffer.AsSpan(i, 1), character, position + i + 1 == message.Payload.Length, out _, out var written, out _);
                for (var c = 0; c < written; c++)
                {
                    if (character[c] is '\r' or '\n') return header.ToString();
                    if (header.Length == QueryLimits.MaximumValueCharacters) throw new Hl7LimitException("SqlHeaderMaterialization");
                    header.Append(character[c]);
                }
            }
            position += count;
        }
        return header.ToString();
    }
    internal Task<QueryCell> ReadAsync(QuerySourceRead read, CancellationToken token)
    {
        token.ThrowIfCancellationRequested(); ValueRequests++;
        if (!Identities.TryGetValue((read.DocumentId, read.MessageId), out var item)) throw new QueryException(QueryError.SourceUnavailable);
        Hl7FieldSelection selection;
        try { selection = Hl7FieldSelection.Parse(read.Path); }
        catch (Exception error) when (error is ArgumentException or FormatException) { throw new QueryException(QueryError.InvalidPath); }
        try
        {
            var header = selection.Path.Segment == "MSH" && selection.Path.Occurrence == 1;
            var opened = Open(item.Document, item.Message, !header, token);
            if (header && !opened.Structure.Segments.Any(s => s.Identifier == "MSH")) opened = Open(item.Document, item.Message, true, token);
            var match = selection.Select(opened.Structure, false, token: token).Matches[0];
            QueryCell value;
            if (read.Operation == QuerySourceOperation.State) value = QueryCell.From(match.Node?.State.ToString() ?? (match.Status == Hl7ExtractionStatus.Complete ? "Missing" : "Unavailable"));
            else
            {
                var decoded = match.Read(opened.Text, opened.Structure, item.Document.Encoding, read.Operation != QuerySourceOperation.Raw,
                    QueryLimits.MaximumValueCharacters);
                if (read.Operation == QuerySourceOperation.Status) value = QueryCell.From(decoded.Status == Hl7ExtractionStatus.Complete && decoded.State == Hl7ValueState.Missing ? "Missing" + decoded.Missing : decoded.Status.ToString());
                else if (decoded.Status == Hl7ExtractionStatus.ValueLimit) throw new QueryException(QueryError.MaterializationLimit);
                else if (read.Operation == QuerySourceOperation.Value && decoded.Status != Hl7ExtractionStatus.Complete) throw new QueryException(QueryError.InvalidValue);
                else if (decoded.Status == Hl7ExtractionStatus.Unreadable) throw new QueryException(QueryError.SourceUnavailable);
                else if (decoded.State is Hl7ValueState.Missing or Hl7ValueState.ExplicitNull) value = read.Operation == QuerySourceOperation.Raw && decoded.State == Hl7ValueState.ExplicitNull ? QueryCell.From("\"\"") : QueryCell.Null;
                else value = QueryCell.From(decoded.Text);
            }
            return Task.FromResult(value);
        }
        catch (DecoderFallbackException)
        {
            if (read.Operation == QuerySourceOperation.Status) return Task.FromResult(QueryCell.From("Unreadable"));
            throw new QueryException(QueryError.SourceUnavailable);
        }
        catch (Hl7LimitException) { throw new QueryException(QueryError.ResourceLimit); }
    }
    public void Dispose() { ResetReader(); foreach (var document in Documents) document.Dispose(); Documents.Clear(); Identities.Clear(); }
}
