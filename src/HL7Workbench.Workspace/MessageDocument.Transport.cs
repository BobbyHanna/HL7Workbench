using System.Text;
using HL7Workbench.Core;
using HL7Workbench.Transport;

namespace HL7Workbench.Workspace;

/// <summary>One bounded immutable message, with source identity/version retained for review freshness.</summary>
public sealed class TransportMessageCapture
{
    private readonly MessageDocument document;
    private readonly long revision;
    private readonly byte[] bytes;
    public string DocumentId => document.Id;
    public MessageId MessageId { get; }
    public int MessageNumber { get; }
    public int Length => bytes.Length;
    public TransportEncoding Encoding { get; }
    public bool Transcoded { get; }
    public bool IsCurrent(DocumentManager manager) => manager.Documents.Contains(document) && document.IsSearchCurrent(revision) && document.GetLogicalPosition(MessageId) == MessageNumber;
    public byte[] CopyPayload() => (byte[])bytes.Clone();
    internal TransportMessageCapture(MessageDocument document, long revision, MessageId id, int position, byte[] bytes, TransportEncoding encoding, bool transcoded)
    { this.document = document; this.revision = revision; MessageId = id; MessageNumber = position; this.bytes = bytes; Encoding = encoding; Transcoded = transcoded; }
}

public sealed partial class MessageDocument
{
    /// <summary>Captures authoritative working text or original payload bytes, never editor previews or outer file framing.</summary>
    public Task<TransportMessageCapture> CaptureTransportMessageAsync(int messageNumber, TransportEncoding encoding, CancellationToken token = default)
    {
        var id = GetMessageId(messageNumber); var capture = CaptureSearch();
        var originalMemory = memoryOriginalBytes;
        return Task.Run(() =>
        {
            using (capture)
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(token, capture.Changes, capture.Lifetime))
            {
                linked.Token.ThrowIfCancellationRequested();
                var entry = capture.Entries.Single(e => e.Entry.Id == id); var target = TransportEndpoint.StrictEncoding(encoding);
                byte[] bytes; var transcoded = capture.Encoding.CodePage != target.CodePage;
                if (entry.Text is { } text)
                {
                    if (text.Length > MllpFrame.MaximumPayloadBytes || target.GetByteCount(text) > MllpFrame.MaximumPayloadBytes) throw new Hl7LimitException("TransportMessageBytes");
                    bytes = target.GetBytes(text);
                    // Preserve an unmodified materialized message's exact encoding/BOM.
                    if (!transcoded && entry.Entry.Origin == MessageOrigin.Memory && originalMemory is not null &&
                        Hl7Parser.Parse(originalMemory, new() { Encoding = target }).RawText == text) bytes = (byte[])originalMemory.Clone();
                }
                else
                {
                    var range = capture.Index!.Messages[entry.Entry.SourceOrdinal!.Value - 1].PayloadRange;
                    var source = capture.Snapshot!.ReadBytes(range, MllpFrame.MaximumPayloadBytes);
                    if (!transcoded) bytes = source;
                    else
                    {
                        var sourceEncoding = capture.Encoding; sourceEncoding.DecoderFallback = DecoderFallback.ExceptionFallback;
                        var bom = Hl7SourceEncoding.Detect(source, sourceEncoding).BomLength;
                        var decoded = sourceEncoding.GetString(source.AsSpan(bom));
                        if (target.GetByteCount(decoded) > MllpFrame.MaximumPayloadBytes) throw new Hl7LimitException("TransportMessageBytes");
                        bytes = target.GetBytes(decoded);
                    }
                }
                _ = MllpFrame.Encode(bytes); _ = Hl7TransportHeader.Read(bytes, target);
                linked.Token.ThrowIfCancellationRequested();
                return new TransportMessageCapture(this, capture.Revision, id, messageNumber, bytes, encoding, transcoded);
            }
        });
    }
}
