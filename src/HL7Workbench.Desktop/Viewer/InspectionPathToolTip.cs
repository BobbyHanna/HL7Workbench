using HL7Workbench.Workspace;

namespace HL7Workbench.Desktop.Viewer;

/// <summary>Formats structural data without raw values or inferred semantic definitions.</summary>
public static class InspectionPathToolTip
{
    public static string Format(InspectionPathInfo info)
    {
        var path = info.Path;
        var lines = new List<string> { $"Path: {path}", $"Segment: {path.Segment}",
            $"Segment occurrence: {path.Occurrence}", $"Field: {path.Field}" };
        if (path.Repetition is { } r) lines.Add($"Repetition: {r}");
        if (path.Component is { } c) lines.Add($"Component: {c}");
        if (path.Subcomponent is { } s) lines.Add($"Subcomponent: {s}");
        return string.Join(Environment.NewLine, lines);
    }
}
