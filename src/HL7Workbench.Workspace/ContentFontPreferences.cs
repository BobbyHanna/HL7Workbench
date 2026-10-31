using System.Collections.Frozen;

namespace HL7Workbench.Workspace;

/// <summary>Content scale in device-independent pixels, separate from OS DPI and placement.</summary>
public sealed record ContentFontPreferences(double DefaultSize = ContentFontPreferences.Default)
{
    public const double Default = 14, Minimum = 12, Maximum = 32;
    public Dictionary<string, double> Overrides { get; init; } = [];
    // Stable feature keys only; never serialize document IDs, names or source content.
    public static IReadOnlySet<string> ToolKeys { get; } = new[]
    {
        "document.hl7", "tool.message-browser", "tool.diagnostics", "tool.full-raw-inspector",
        "tool.segment-navigator", "tool.field-inspector", "tool.script-editor", "tool.sql-query",
        "tool.script-api-reference", "tool.documentation", "tool.field-extraction", "tool.synthetic-patients",
        "tool.generation-report", "tool.deidentification-report", "tool.connection-manager", "tool.mllp-listener",
        "tool.batch-transmission", "tool.load-testing", "tool.search-results", "tool.message-comparison"
    }.ToFrozenSet(StringComparer.Ordinal);
    public static double Bound(double size) => double.IsFinite(size) ? Math.Clamp(size, Minimum, Maximum) : Default;
    public double For(string tool) => Overrides?.TryGetValue(tool, out var size) == true ? Bound(size) : Bound(DefaultSize);
    public ContentFontPreferences Validated() => new(Bound(DefaultSize))
    {
        Overrides = (Overrides ?? []).Where(p => ToolKeys.Contains(p.Key)).ToDictionary(p => p.Key, p => Bound(p.Value), StringComparer.Ordinal)
    };
}
