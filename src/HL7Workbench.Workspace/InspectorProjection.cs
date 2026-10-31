using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

public enum InspectorAction { FindOccurrences, FilterByValue, Extract }

/// <summary>Read-only segment presentation over existing Core projections; absent rows have no source span.</summary>
public static class InspectorProjection
{
    public static IReadOnlyList<InspectionNode> Fields(MessageInspection inspection, bool showBlank, Hl7ValidationProfile? profile = null)
    {
        if (inspection.SelectedSegment is not { } segment) return [];
        var fields = inspection.Fields;
        if (showBlank && segment.HasFieldPaths && profile?.Segments.FirstOrDefault(s => s.Id == segment.Identifier) is { } definition)
        {
            var present = fields.Select(f => f.Path.Field).ToHashSet();
            fields = fields.Concat(definition.Fields.Where(f => !present.Contains(f.Position)).Select(f =>
                inspection.AbsentField(segment, f.Position))).OrderBy(f => f.Path.Field).ToArray();
        }
        bool Visible(InspectionNode field) => field.State != Hl7ValueState.Empty || inspection.SelectedNode?.Path.Field == field.Path.Field;
        return showBlank || fields.All(Visible) ? fields : fields.Where(Visible).ToArray();
    }
    public static IReadOnlyList<InspectionNode> Children(MessageInspection inspection, InspectionNode node, bool showBlank)
    {
        var children = node.VisibleChildren;
        bool Visible(InspectionNode child) => child.State != Hl7ValueState.Empty || inspection.SelectedVisibleBranch.Contains(child);
        return showBlank || children.All(Visible) ? children : children.Where(Visible).ToArray();
    }
    public static string Describe(InspectionNode node, Hl7ValidationProfile? profile)
    {
        var path = node.Path;
        var rule = profile?.Segments.FirstOrDefault(s => s.Id == path.Segment)?.Fields.FirstOrDefault(f => f.Position == path.Field);
        if (path.Component is { } component) rule = rule?.Children.FirstOrDefault(c => c.Position == component);
        if (path.Subcomponent is { } subcomponent) rule = rule?.Children.FirstOrDefault(c => c.Position == subcomponent);
        var label = path.Subcomponent is { } sub ? $"Subcomponent {sub}" : path.Component is { } c ? $"Component {c}" :
            path.Repetition is { } r ? $"Repetition {r}" : $"Field {path.Field}";
        // Current reviewed metadata declares types/constraints, not human-readable names.
        // Never infer a clinical definition from a familiar segment ID or unknown version.
        return rule is null ? label : $"{label} · {rule.DataType}";
    }
}

/// <summary>Captured row identity for explicit actions. Navigation/edit/close invalidates it.</summary>
public sealed class InspectionActionContext(MessageDocument document, MessageInspection inspection, InspectionNode node)
{
    private readonly MessageId? message = document.ActiveMessageId;
    private readonly long revision = document.SearchRevision;
    public MessageDocument Document { get; } = document;
    public InspectionNode Node { get; } = node;
    public bool IsCurrent => Document.IsSearchCurrent(revision) && Document.ActiveMessageId == message && inspection.IsCurrent &&
        ReferenceEquals(Document.ActiveInspection, inspection) && ReferenceEquals(inspection.SelectedNode, Node) && ReferenceEquals(inspection.SelectedSegment, Node.Owner);
    public string Path => Node.Path.ToString();
    public string? SearchPath => InspectionSearchPath.For(Node);
    public bool CanCopyValue => IsCurrent && Node.HasContent && Node.Range64 is { Length: <= DecodedMessageSource.MaxReadCharacters };
    public async Task<string?> ReadValueAsync(CancellationToken token = default)
    {
        if (!CanCopyValue) return null;
        var result = await Document.ReadTextAsync(_ => inspection.ReadPreview(Node, 0, DecodedMessageSource.MaxReadCharacters), token);
        return IsCurrent ? result : null;
    }
    public MessageSearchQuery? FindQuery() => SearchPath is { } path ? MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Exists, path)]) : null;
    public MessageSearchQuery? FilterQuery(string value)
    {
        // Search compares individual repetitions. A whole repeated field cannot be
        // passed as one repetition without changing its meaning.
        if (SearchPath is not { } path || Node.Path.Repetition is null && Node.Owner.RepetitionCount(Node.Path.Field) > 1) return null;
        var query = MessageSearchQuery.Advanced([new(SearchTarget.Field, SearchOperator.Equals, path, value, true)]);
        return query.ValidationError() is null ? query : null;
    }
}
