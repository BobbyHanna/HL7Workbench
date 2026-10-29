using HL7Workbench.Core;

namespace HL7Workbench.Workspace;

/// <summary>Session-only count summary of the most recent successful synthetic insertion; never reassesses later document edits.</summary>
public sealed record SyntheticGenerationReport(string Scenario, int GeneratorVersion, uint Seed, DateTimeOffset BaseTimestamp,
    int Patients, int Encounters, int Orders, int Results, int Observations, int Messages,
    IReadOnlyDictionary<string, int> MessageTypes, TimeSpan GenerationTime, TimeSpan PublicationTime, bool IsNewDocument)
{
    public const string Notice = "Fictional synthetic test data only; not for clinical decisions or a claim of HL7 profile conformance. Counts describe this successful batch, not later edits or the entire destination. Failed/canceled operations publish no batch/report; operation status shows cancellation or rejection. No automatic transmission.";
    public bool IsCanceled => false;
    /// <summary>Seed is always the resolved value, including when selected automatically once for this operation.</summary>
    public bool SeedWasAutomatic { get; init; }
    public SyntheticDemographicStyle? DemographicStyle { get; init; }
    public string DemographicStyleLabel => DemographicStyle switch
    {
        SyntheticDemographicStyle.RealisticSynthetic => "Realistic Synthetic",
        SyntheticDemographicStyle.ObviousPlaceholders => "Obvious Placeholders",
        _ => "Standalone v1 placeholders"
    };
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
