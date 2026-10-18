using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Captures and indexes a file session. The caller owns the input stream and publication context.</summary>
public static class MessageFileReader
{
    public static async Task<MessageDocument> OpenFileAsync(DocumentManager documents, string filename, Stream source,
        CancellationToken token = default, IProgress<long>? progress = null, StructureScanOptions? inspectionOptions = null,
        int? activityDocumentNumber = null, string? localPath = null)
    {
        var number = activityDocumentNumber ?? documents.Activity.BeginFileOpen();
        try
        {
            var document = await OpenCoreAsync(documents, filename, source, number, token, progress, inspectionOptions, localPath);
            documents.Activity.Record(ActivityKind.FileOpened, number); return document;
        }
        catch (OperationCanceledException) { documents.Activity.Record(ActivityKind.FileOpenCanceled, number); throw; }
        catch (Exception error)
        { documents.Activity.Record(ActivityKind.FileOpenFailed, number, failure: ActivityLog.Classify(error)); throw; }
    }
    private static async Task<MessageDocument> OpenCoreAsync(DocumentManager documents, string filename, Stream source,
        int activityNumber, CancellationToken token, IProgress<long>? progress, StructureScanOptions? inspectionOptions, string? localPath)
    {
        var snapshot = await SourceSnapshot.CaptureAsync(source, token, progress);
        MessageDocument? document = null;
        try
        {
            var index = await Task.Run(() => MessageFileIndex.Build(snapshot, token, progress), token);
            var title = filename;
            for (var occurrence = 2; documents.Documents.Any(d => string.Equals(d.Title, title, StringComparison.OrdinalIgnoreCase)); occurrence++)
                title = $"{filename} ({occurrence})";
            document = new(Guid.NewGuid().ToString("N"), title, snapshot, index, inspectionOptions);
            var destination = snapshot.OriginalDestination;
            if (localPath is not null)
                destination = destination is not null ? destination.AtSameLocation(localPath) :
                    await FileSaveDestination.MatchSnapshotAsync(localPath, snapshot.CapturedHash!, token);
            document.SetInitialSaveDestination(destination);
            document.ActivityNumber = activityNumber;
            documents.Activity.Record(ActivityKind.FileIndexed, activityNumber, count: index.Messages.Count);
            if (index.Messages.Count > 0) await document.NavigateAsync(1, token);
            token.ThrowIfCancellationRequested();
            documents.Open(document);
            return document;
        }
        catch { if (document is null) snapshot.Dispose(); else document.Dispose(); throw; }
    }
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
