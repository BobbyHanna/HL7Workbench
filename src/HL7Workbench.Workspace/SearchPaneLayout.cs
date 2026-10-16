namespace HL7Workbench.Workspace;

/// <summary>Placement-only search preferences. Root tool identity and query persistence are separate.</summary>
public sealed record SearchPaneLayout(int Version = 1, double AdvancedBuilderFraction = 0.6)
{
    public SearchPaneLayout Validated() => Version == 1 && double.IsFinite(AdvancedBuilderFraction) &&
        AdvancedBuilderFraction is >= 0.1 and <= 0.9 ? this : new();
}
