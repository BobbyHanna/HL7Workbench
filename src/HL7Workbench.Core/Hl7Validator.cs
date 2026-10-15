using System.Collections.ObjectModel;

namespace HL7Workbench.Core;

public static class Hl7Validator
{
    /// <summary>Minimal structural checks, not version/profile conformance certification.</summary>
    public static ReadOnlyCollection<Hl7Diagnostic> Validate(Hl7Message message)
    {
        ArgumentNullException.ThrowIfNull(message);
        return ValidateHeader(message.Segments.Count, message.Segments.Any(s => s.Identifier == "MSH"),
            field => message.Get(new Hl7Path("MSH", field)).State);
    }

    /// <summary>The same minimal checks over source-backed structure, without reading full values.</summary>
    public static ReadOnlyCollection<Hl7Diagnostic> Validate(Hl7Structure structure)
    {
        ArgumentNullException.ThrowIfNull(structure);
        var header = structure.Segments.FirstOrDefault(s => s.Identifier == "MSH");
        return ValidateHeader(structure.Segments.Count, header is not null,
            field => header?.Fields.FirstOrDefault(f => f.Number == field)?.State ?? Hl7ValueState.Missing);
    }

    private static ReadOnlyCollection<Hl7Diagnostic> ValidateHeader(int segments, bool hasHeader, Func<int, Hl7ValueState> state)
    {
        var result = new List<Hl7Diagnostic>();
        if (segments == 0)
            result.Add(new("EmptyMessage", Hl7DiagnosticSeverity.Warning, "Message has no segments."));
        if (hasHeader)
            foreach (var field in new[] { 9, 10, 12 })
                if (state(field) is Hl7ValueState.Missing or Hl7ValueState.Empty or Hl7ValueState.ExplicitNull)
                    result.Add(new("MissingHeaderValue", Hl7DiagnosticSeverity.Warning,
                        "A commonly required header value is missing, empty, or explicitly null.", Path: new Hl7Path("MSH", field).ToString()));
        return result.AsReadOnly();
    }
}
