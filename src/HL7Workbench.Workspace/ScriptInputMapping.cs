using HL7Workbench.Scripting;

namespace HL7Workbench.Workspace;

/// <summary>Immutable job-local ordinal mapped to document identity and separately captured provenance.</summary>
public sealed record ScriptInputMessage(int JobOrdinal, MessageId MessageId, int LogicalPosition, int? OriginalOrdinal);
/// <summary>Attribution for one independently published output message, including deferred copies.</summary>
public sealed record ScriptOutputMessage(MessageId OutputMessageId, MessageId InputMessageId, int JobOrdinal);

/// <summary>Session-only attribution. Worker ordinals are meaningful only within this mapping.</summary>
public sealed class ScriptInputMapping
{
    public string DocumentId { get; }
    public long Revision { get; }
    public IReadOnlyList<ScriptInputMessage> Messages { get; }
    internal ScriptInputMapping(string documentId, long revision, ScriptInputMessage[] messages)
    { DocumentId = documentId; Revision = revision; Messages = Array.AsReadOnly(messages); }
    /// <summary>Resolves only this job's ordinal; it is never interpreted as a current document position.</summary>
    public ScriptInputMessage? Resolve(int jobOrdinal) => jobOrdinal >= 1 && jobOrdinal <= Messages.Count ? Messages[jobOrdinal - 1] : null;
    public bool IsCurrent(MessageDocument document) => document.Id == DocumentId && document.IsSearchCurrent(Revision);
    // Parent-only exception context avoids adding Workspace identities to worker DTOs
    // or changing the isolated protocol. The bounded metadata contains no source text.
    public static ScriptInputMapping? ForFailure(ScriptRunException error) => error.Data[typeof(ScriptInputMapping)] as ScriptInputMapping;
    internal void Attribute(ScriptRunException error) => error.Data[typeof(ScriptInputMapping)] = this;
}
