using HL7Workbench.Core;
using HL7Workbench.Scripting;

namespace HL7Workbench.Workspace;

public sealed partial class MessageDocument
{
    public bool IsCapturingScript { get; private set; }
    private bool generatedUnsaved;
    private int scriptTitleOrdinal = 1;
    internal bool HasGeneratedFileName => generatedUnsaved;
    internal void SetScriptTitleOrdinal(int ordinal)
    {
        if (scriptTitleOrdinal == ordinal) return;
        scriptTitleOrdinal = ordinal; PropertyChanged?.Invoke(this, new(nameof(DisplayTitle)));
    }
    private sealed class CaptureMessageException(MessageId id, Exception cause) : Exception("Captured message is not representable.", cause)
    { public MessageId MessageId => id; }
    internal sealed record ScriptInputCapture(SourceSnapshot Snapshot, MessageFileIndex Index, ScriptInputMapping Mapping,
        int? TargetOrdinal) : IDisposable
    { public void Dispose() => Snapshot.Dispose(); }

    /// <summary>Freeze logical order and job-local identity while streaming a validated private input.</summary>
    internal async Task<ScriptInputCapture> CaptureScriptAsync(long maximumBytes, string? storageRoot,
        MessageId? targetMessage, long revision, CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (IsSaving || IsCapturingScript || IsPreparingInsertion || IsLoading || !IsSearchCurrent(revision))
            throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputUnavailable"));
        IsCapturingScript = true; PropertyChanged?.Invoke(this, new(nameof(IsCapturingScript)));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        SourceSnapshot? captured = null; ScriptInputMapping? mapping = null;
        try
        {
            await work.WaitAsync(linked.Token);
            try
            {
                if (!IsSearchCurrent(revision) || targetMessage is { } target && FindMessage(target) is null)
                    throw new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputUnavailable"));
                // Current scope keeps original-file fallbacks for inactive originals.
                // Inactive insertions have no fallback and are omitted explicitly from
                // this job's mapping. Whole-document scope must represent every entry.
                var order = sequence.Entries.Where(e => targetMessage is null || e.Origin != MessageOrigin.Inserted || e.Id == targetMessage).ToArray();
                mapping = new(Id, revision, order.Select((e, i) => new ScriptInputMessage(i + 1, e.Id, GetLogicalPosition(e.Id), e.SourceOrdinal)).ToArray());
                var plan = await Task.Run(() => BuildSavePlan(linked.Token, order, targetMessage), linked.Token);
                captured = await Task.Run(() => SourceSnapshot.CreateAsync((output, cancellation) =>
                    plan.WriteAsync(output, null, cancellation), maximumBytes, linked.Token, storageRoot), linked.Token);
                var index = await Task.Run(() => MessageFileIndex.Build(captured, linked.Token, inheritedEncoding: searchEncoding), linked.Token);
                plan.ValidateIndex(index, linked.Token, requireMembership: true);
                var targetOrdinal = targetMessage is { } id ? mapping.Messages.Single(e => e.MessageId == id).JobOrdinal : (int?)null;
                var result = new ScriptInputCapture(captured, index, mapping, targetOrdinal); captured = null;
                return result;
            }
            finally { work.Release(); }
        }
        catch (CaptureMessageException error)
        {
            var ordinal = mapping!.Messages.Single(e => e.MessageId == error.MessageId).JobOrdinal;
            var failure = new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input,
                error.InnerException is Hl7LimitException ? "InputLimit" : error.InnerException is System.Text.EncoderFallbackException ? "InvalidEncoding" : "BoundaryChange", MessageNumber: ordinal));
            mapping.Attribute(failure); throw failure;
        }
        catch (Hl7LimitException)
        {
            var failure = new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "InputStorageLimit"));
            mapping?.Attribute(failure); throw failure;
        }
        catch (InvalidOperationException)
        {
            var failure = new ScriptRunException(new ScriptDiagnostic(ScriptPhase.Input, "BoundaryChange"));
            mapping?.Attribute(failure); throw failure;
        }
        finally { captured?.Dispose(); IsCapturingScript = false; PropertyChanged?.Invoke(this, new(nameof(IsCapturingScript))); }
    }

    internal void MarkGeneratedUnsaved() { generatedUnsaved = true; PropertyChanged?.Invoke(this, new(nameof(DisplayTitle))); }
}
