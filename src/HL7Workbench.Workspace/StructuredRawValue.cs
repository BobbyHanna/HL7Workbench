namespace HL7Workbench.Workspace;

/// <summary>Shared preflight for single and bulk raw editing; Core SetRaw remains the structural/encoding authority.</summary>
internal static class StructuredRawValue
{
    internal static string? UnavailableReason(string value) => value.Length > MessageDocument.MaximumEditableLineCharacters
        ? "Replacement exceeds the existing 16 Ki-character line limit."
        : value.Any(c => char.IsControl(c) || c is '\u2028' or '\u2029')
            ? "Raw values cannot contain segment terminators, tabs or other control/line-separator characters." : null;
}
