using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Reads one bounded file as raw bytes. The caller owns the stream and publishes the result on its own context.</summary>
public static class MessageFileReader
{
    public static async Task<Hl7Message> ReadAsync(Stream source, CancellationToken cancellationToken = default)
    {
        var options = new Hl7ParseOptions();
        using var bytes = new MemoryStream();
        var buffer = new byte[81920];
        while (true)
        {
            var read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            // Bound non-seekable streams too, before allocating beyond the parser's byte budget.
            if (bytes.Length + read > options.MaxMessageBytes)
                throw new Hl7LimitException(nameof(options.MaxMessageBytes));
            bytes.Write(buffer, 0, read);
        }
        var input = bytes.ToArray();
        return await Task.Run(() => Hl7Parser.Parse(input, options), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Creates a session identity and a unique filename-based title, including repeated opens of the same file.</summary>
    public static MessageDocument Open(DocumentManager documents, string filename, Hl7Message message)
    {
        var title = filename;
        for (var occurrence = 2; documents.Documents.Any(d => string.Equals(d.Title, title, StringComparison.OrdinalIgnoreCase)); occurrence++)
            title = $"{filename} ({occurrence})";
        var document = new MessageDocument(Guid.NewGuid().ToString("N"), title, message);
        documents.Open(document);
        return document;
    }
}
