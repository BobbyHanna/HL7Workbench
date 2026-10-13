namespace HL7Workbench.Core;

// The last slot becomes a suppression notice only when the capacity is exceeded.
internal sealed class Hl7DiagnosticCollector(int capacity)
{
    private readonly List<Hl7Diagnostic> items = [];
    private bool suppressed;
    internal void Add(Hl7Diagnostic diagnostic)
    {
        if (suppressed) return;
        if (items.Count < capacity) { items.Add(diagnostic); return; }
        suppressed = true;
        items[^1] = new("DiagnosticsSuppressed", Hl7DiagnosticSeverity.Warning,
            "Additional diagnostics were suppressed by the configured storage limit.");
    }
    internal void AddRange(IEnumerable<Hl7Diagnostic> diagnostics)
    {
        foreach (var diagnostic in diagnostics)
        {
            if (diagnostic.Code == "DiagnosticsSuppressed")
            {
                // A child collector already suppressed diagnostics: report it even
                // if this collector has remaining capacity.
                Add(diagnostic);
                continue;
            }
            Add(diagnostic);
        }
    }
    internal List<Hl7Diagnostic> ToList() => items;
}
