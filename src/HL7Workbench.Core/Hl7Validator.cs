using System.Collections.ObjectModel;

namespace HL7Workbench.Core;

public static class Hl7Validator
{
    /// <summary>Minimal structural checks, not version/profile conformance certification.</summary>
    public static ReadOnlyCollection<Hl7Diagnostic> Validate(Hl7Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        var result = new List<Hl7Diagnostic>();
        if (message.Segments.Count == 0)
            result.Add(new("EmptyMessage", Hl7DiagnosticSeverity.Warning, "Message has no segments."));
        if (message.Segments.Any(s => s.Identifier == "MSH"))
            foreach (var field in new[] { 9, 10, 12 })
            {
                var path = new Hl7Path("MSH", field);
                if (message.Get(path).State is Hl7ValueState.Missing or Hl7ValueState.Empty or Hl7ValueState.ExplicitNull)
                    result.Add(new("MissingHeaderValue", Hl7DiagnosticSeverity.Warning,
                        "A commonly required header value is missing, empty, or explicitly null.", Path: path.ToString()));
            }
        return result.AsReadOnly();
    }
}
