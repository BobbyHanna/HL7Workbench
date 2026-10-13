namespace HL7Workbench.Core;

public enum Hl7DiagnosticSeverity { Information, Warning, Error }

// Diagnostics intentionally contain no imported field values.
public sealed record Hl7Diagnostic(string Code, Hl7DiagnosticSeverity Severity,
    string Description, int? CharacterOffset = null, string? Path = null);

public enum Hl7ValueState { Missing, Empty, ExplicitNull, Value }

public readonly record struct Hl7Value
{
    public string? Raw { get; }
    public Hl7ValueState State => Raw switch
    {
        null => Hl7ValueState.Missing,
        "" => Hl7ValueState.Empty,
        "\"\"" => Hl7ValueState.ExplicitNull,
        _ => Hl7ValueState.Value
    };

    public Hl7Value(string? raw) => Raw = raw;
}
