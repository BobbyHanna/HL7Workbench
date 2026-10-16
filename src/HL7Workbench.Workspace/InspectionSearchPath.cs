using System.Globalization;

namespace HL7Workbench.Workspace;

/// <summary>Structural coordinates accepted by discovery. Search examines every occurrence/repetition,
/// so copied paths intentionally omit indices rather than claiming an unsupported exact occurrence.</summary>
public static class InspectionSearchPath
{
    public static string? For(object? item)
    {
        if (item is SegmentEntry segment)
        {
            if (!segment.HasFieldPaths && segment.Range64.Length != 3) return null;
            var candidate = new SearchCondition(SearchTarget.Segment, SearchOperator.Exists, segment.Identifier);
            return candidate.ValidationError() is null ? candidate.Path : null;
        }
        if (item is not InspectionNode { HasContent: true } node) return null;
        var path = node.Path;
        var text = path.Segment + "-" + path.Field.ToString(CultureInfo.InvariantCulture) +
            (path.Component is { } c ? "." + c.ToString(CultureInfo.InvariantCulture) : "") +
            (path.Subcomponent is { } s ? "." + s.ToString(CultureInfo.InvariantCulture) : "");
        return new SearchCondition(SearchTarget.Field, SearchOperator.Exists, text).ValidationError() is null ? text : null;
    }
}
