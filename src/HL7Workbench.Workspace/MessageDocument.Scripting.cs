using HL7Workbench.Core;
using HL7Workbench.Scripting;

namespace HL7Workbench.Workspace;

public sealed partial class MessageDocument
{
    public bool IsCapturingScript { get; private set; }
    private bool generatedUnsaved;

    /// <summary>Capture actual working bytes while edits/saves/navigation are frozen; previews are never execution input.</summary>
    internal async Task<SourceSnapshot> CaptureScriptAsync(long maximumBytes, string? storageRoot, int? targetMessage, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsSaving || IsCapturingScript || IsLoading) throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputUnavailable"));
        IsCapturingScript = true; PropertyChanged?.Invoke(this, new(nameof(IsCapturingScript)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        try
        {
            await work.WaitAsync(linked.Token);
            try
            {
                // One writer holds the document gate and synchronous edit guards until the capture is sealed.
                // Only bounded changed payloads are materialized; untouched file ranges stream from the original.
                return await Task.Run(() => SourceSnapshot.CreateAsync(async (output, cancellation) =>
                {
                    if (Snapshot is { } snapshot && Index is { } index)
                    {
                        long cursor = 0;
                        foreach (var (number, buffer) in buffers.OrderBy(p => p.Key))
                        {
                            // Other drafts are not inputs to a current-message job. In particular,
                            // an invalid inactive draft must not block an unrelated supported message.
                            if (targetMessage is { } target && number != target) continue;
                            if (!buffer.DiffersFromOriginal) continue;
                            try
                            {
                                var parsed = buffer.Parse(); ValidateSaveBoundary(number, buffer, parsed);
                                if (parsed.Encoding.CodePage is 1200 or 1201)
                                    await ValidateEncodedBoundaryAsync(number, buffer, parsed.Encoding, cancellation);
                            }
                            catch (Hl7LimitException) { throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputLimit", MessageNumber: number)); }
                            catch (System.Text.EncoderFallbackException) { throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InvalidEncoding", MessageNumber: number)); }
                            catch (InvalidOperationException) { throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "BoundaryChange", MessageNumber: number)); }
                            var range = index.Messages[number - 1].PayloadRange;
                            await CopySnapshotAsync(snapshot, new(cursor, range.Offset - cursor), output, cancellation);
                            await output.WriteAsync(buffer.Serialize(), cancellation); cursor = range.End;
                        }
                        await CopySnapshotAsync(snapshot, new(cursor, snapshot.Length - cursor), output, cancellation);
                    }
                    else
                    {
                        var bytes = buffers.GetValueOrDefault(1)?.Serialize() ?? Hl7Serializer.Serialize(Message);
                        await output.WriteAsync(bytes, cancellation);
                    }
                }, maximumBytes, linked.Token, storageRoot), linked.Token);
            }
            finally { work.Release(); }
        }
        catch (Hl7LimitException) { throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputStorageLimit")); }
        finally { IsCapturingScript = false; PropertyChanged?.Invoke(this, new(nameof(IsCapturingScript))); }
    }

    internal void MarkGeneratedUnsaved() { generatedUnsaved = true; PropertyChanged?.Invoke(this, new(nameof(DisplayTitle))); }
}
