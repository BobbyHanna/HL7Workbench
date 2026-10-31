using System.ComponentModel;
using System.Text;
using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>One staged raw-value edit over an existing node. Owns no snapshot lease or independent history.</summary>
public sealed class StructuredValueEditSession : INotifyPropertyChanged, IDisposable
{
    private DocumentManager? documents;
    private MessageDocument? document;
    private MessageInspection? inspection;
    private InspectionNode? node;
    private readonly MessageId? message;
    private readonly long version, revision;
    private readonly CharacterRange range;
    private long appliedVersion, appliedRevision;
    private bool disposed, invalidated, applying;
    public Hl7Path Path { get; }
    public string OriginalValue { get; private set; }
    public string Status { get; private set; } = "Raw encoded text only; no automatic escape decoding or encoding.";
    public bool IsApplied { get; private set; }
    public bool CanApply => !disposed && !invalidated && !IsApplied && CurrentTarget;
    public event PropertyChangedEventHandler? PropertyChanged;

    public static string? UnavailableReason(MessageDocument document, InspectionNode node)
    {
        if (!document.IsEditMode) return "Enable the document's Edit Mode before editing inspector values.";
        if (!document.CanEditText) return document.EditingUnavailableReason ?? "Wait for the current document operation to finish.";
        if (document.InspectionState != DocumentInspectionState.Current || document.ActiveInspection?.IsCurrent != true)
            return "Wait for current inspection; previous parse coordinates cannot be edited.";
        if (!node.HasContent || node.Range64 is null) return "Absent nodes have no source span; creating structure is not supported.";
        if (node.Element is null || document.Source is not null) return "Source-backed values remain read-only; a complete editable message is required.";
        if (node.Path.Segment == "MSH" && node.Path.Field <= 2) return "MSH-1 and MSH-2 define parsing rules and cannot be edited here.";
        if (document.MaterializedMessage?.CanEdit != true || !node.Owner.CanEdit) return "Malformed or ambiguous structure cannot be edited safely.";
        if (!ReferenceEquals(node.Owner, document.ActiveInspection.SelectedSegment)) return "Select a node in the current segment.";
        if (node.Range64 is not { Offset: >= 0, Length: <= MessageDocument.MaximumEditableLineCharacters } span || span.End > document.TextLength)
            return "The value exceeds the existing bounded editing limits.";
        return null;
    }

    public StructuredValueEditSession(DocumentManager documents, InspectionActionContext context)
    {
        if (!ReferenceEquals(documents.ActiveDocument, context.Document) || !context.IsCurrent)
            throw new InvalidOperationException("Select a current node in the active document.");
        if (UnavailableReason(context.Document, context.Node) is { } reason) throw new InvalidOperationException(reason);
        this.documents = documents; document = context.Document; inspection = document.ActiveInspection!; node = context.Node;
        Path = node.Path; range = node.Range64!.Value;
        message = document.ActiveMessageId; version = document.ContentVersion; revision = document.SearchRevision;
        OriginalValue = node.Raw!;
        documents.PropertyChanged += ContextChanged; document.PropertyChanged += ContextChanged;
        document.Closed += DocumentClosed; inspection.SelectionChanged += SelectionChanged;
    }
    private bool CurrentTarget => document is { } owner && documents is { } manager && inspection is { } current && node is { } target &&
        ReferenceEquals(manager.ActiveDocument, owner) && manager.Documents.Contains(owner) && owner.IsSearchCurrent(revision) &&
        owner.ContentVersion == version && owner.ActiveMessageId == message && ReferenceEquals(owner.ActiveInspection, current) &&
        ReferenceEquals(current.SelectedNode, target) && target.Range64 == range && UnavailableReason(owner, target) is null;

    private void ContextChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (disposed || applying) return;
        // Once a window changes documents, returning to it must not revive the staged edit.
        if (!ReferenceEquals(documents?.ActiveDocument, document) || !IsApplied && !CurrentTarget) Invalidate();
    }
    private void SelectionChanged(object? sender, InspectionSelectionChangedEventArgs e)
    { if (!applying && !IsApplied && !CurrentTarget) Invalidate(); }
    private void DocumentClosed(object? sender, EventArgs e) => Invalidate();
    private void Invalidate()
    {
        if (disposed || invalidated) return;
        invalidated = true; Feedback("Target changed or became unavailable. Cancel and reopen Edit Value for the intended node.");
    }
    private void Feedback(string text) { Status = text; PropertyChanged?.Invoke(this, new(null)); }

    /// <summary>Validates in an isolated bounded Core model, then commits one existing document text transaction.</summary>
    public bool TryApply(string replacement)
    {
        if (!CanApply) { Invalidate(); return false; }
        ArgumentNullException.ThrowIfNull(replacement);
        if (replacement == OriginalValue) { Feedback("The value is unchanged; no edit or undo entry was created."); return false; }
        if (StructuredRawValue.UnavailableReason(replacement) is { } reason)
        { Feedback(reason); return false; }
        var owner = document!;
        string candidateText;
        try
        {
            var text = owner.EditableText!;
            if (!text.AsSpan((int)range.Offset, (int)range.Length).SequenceEqual(OriginalValue))
            { Invalidate(); return false; }
            // Never mutate the document's published Core model. SetRaw owns level-specific
            // delimiters, closed escapes, sibling checks, encoding and exact span splicing.
            var candidate = Hl7Parser.Parse(text, new() { Encoding = owner.MaterializedMessage!.Encoding,
                MaxMessageCharacters = MessagePresentation.MaximumEditorCharacters,
                MaxSegmentLength = MessageDocument.MaximumEditableLineCharacters });
            candidate.SetRaw(Path, replacement);
            candidateText = candidate.RawText;
            var expected = string.Concat(text.AsSpan(0, (int)range.Offset), replacement, text.AsSpan((int)range.End));
            if (candidateText != expected) { Feedback("The target cannot be replaced without changing neighboring structure."); return false; }
        }
        catch (EncoderFallbackException) { Feedback("Replacement cannot be represented in the document's original source encoding."); return false; }
        catch (Hl7LimitException) { Feedback("Replacement exceeds existing message, line or Core structural limits."); return false; }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or KeyNotFoundException)
        { Feedback("Unsafe raw value: check the selected level's separators and closed escape sequences. No edit applied."); return false; }
        if (!CanApply) { Invalidate(); return false; }
        try
        {
            applying = true;
            owner.ReplaceWorkingText(candidateText);
            appliedVersion = owner.ContentVersion; appliedRevision = owner.SearchRevision;
            IsApplied = true; Feedback("Applied one document edit. Inspection is refreshing."); return true;
        }
        catch (Hl7LimitException) { Feedback("The existing document edit/history budget is full. No edit applied."); return false; }
        catch (InvalidOperationException) { Invalidate(); return false; }
        finally { applying = false; }
    }

    /// <summary>Restores by canonical identity after the existing debounce, only while the committed target is still active.</summary>
    public async Task RestoreSelectionAsync()
    {
        if (!IsApplied || document is not { } owner) return;
        await owner.EditRefresh;
        if (disposed || invalidated || !ReferenceEquals(documents?.ActiveDocument, owner) || owner.ActiveMessageId != message ||
            owner.ContentVersion != appliedVersion || !owner.IsSearchCurrent(appliedRevision) || owner.ActiveInspection is not { } current) return;
        var segment = current.Segments.FirstOrDefault(s => s.Identifier == Path.Segment && s.Occurrence == Path.Occurrence);
        if (segment is null) return;
        if (!ReferenceEquals(current.SelectedSegment, segment)) current.SelectSegment(segment);
        var target = current.Fields.FirstOrDefault(f => f.Path.Field == Path.Field);
        foreach (var position in new[] { Path.Repetition, Path.Component, Path.Subcomponent })
        {
            if (position is null || target is null) break;
            target = position.Value <= target.Children.Count ? target.Children[position.Value - 1] : null;
        }
        if (target is not null) current.SelectNode(target, revealCurrent: true);
    }
    public void Dispose()
    {
        if (disposed) return; disposed = true;
        if (documents is { } manager) manager.PropertyChanged -= ContextChanged;
        if (document is { } owner) { owner.PropertyChanged -= ContextChanged; owner.Closed -= DocumentClosed; }
        if (inspection is { } current) current.SelectionChanged -= SelectionChanged;
        documents = null; document = null; inspection = null; node = null; OriginalValue = ""; PropertyChanged = null;
    }
}
